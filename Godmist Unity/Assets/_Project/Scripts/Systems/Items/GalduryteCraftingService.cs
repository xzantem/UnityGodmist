using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeRandom = System.Random;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public sealed class GalduryteCraftRequest
    {
        public ItemDefinition Stone;
        public ItemDefinition Powder;
        public GalduryteTargetType TargetType;
        public string RecipientCharacterId = string.Empty;
    }

    public sealed class GalduryteCraftPreview
    {
        public GalduryteItemInstance Result;
        public IReadOnlyList<CraftingMaterialCost> Materials = Array.Empty<CraftingMaterialCost>();
        public bool CanCraft;
        public string FailureReason = string.Empty;
    }

    public sealed class GalduryteIdentificationPreview
    {
        public GalduryteItemInstance Galduryte;
        public int GoldCost;
        public bool CanIdentify;
        public string FailureReason = string.Empty;
    }

    public sealed class GalduryteIdentificationRequest
    {
        public string GalduryteInstanceId = string.Empty;
    }

    /// <summary>
    /// Handles the Enchanter's galduryte production and paid identification.
    /// Socketing and application of the rolled effects are intentionally left
    /// for the follow-up socketing card.
    /// </summary>
    public sealed class GalduryteCraftingService
    {
        private const float DefaultIdentificationCostPercent = .30f;

        private readonly InventoryService inventoryService;
        private readonly ItemInstanceFactory itemFactory;
        private readonly TradeService tradeService;
        private readonly NpcLoyaltyService loyaltyService = new();
        private readonly NpcEconomyService economyService = new();
        private readonly RuntimeRandom random;

        public GalduryteCraftingService(
            InventoryService inventoryService = null,
            ItemInstanceFactory itemFactory = null,
            TradeService tradeService = null,
            RuntimeRandom random = null)
        {
            this.inventoryService = inventoryService ?? new InventoryService();
            this.itemFactory = itemFactory ?? new ItemInstanceFactory();
            this.tradeService = tradeService ?? new TradeService();
            this.random = random ?? new RuntimeRandom();
        }

        public GalduryteCraftPreview CreateCraftPreview(
            GameSession session,
            NpcInstance enchanter,
            GalduryteCraftRequest request)
        {
            var preview = new GalduryteCraftPreview();
            if (!GalduryteServiceAccess.IsUnlocked(session))
            { preview.FailureReason = GalduryteServiceAccess.LockedMessage; return preview; }
            if (!TryGetRole(enchanter, out var role))
            {
                preview.FailureReason = "Wybrany NPC nie oferuje usług galdurytowych.";
                return preview;
            }

            if (!TryGetRecipient(session, request?.RecipientCharacterId, out var recipient))
            {
                preview.FailureReason = "Wybierz aktywną postać, która odbierze galduryt.";
                return preview;
            }

            if (!TryResolveInputs(role, request, out var stone, out var powder, out var tier, out var inputFailure))
            {
                preview.FailureReason = inputFailure;
                return preview;
            }

            preview.Materials = BuildMaterialCosts(session, enchanter, stone, powder);
            if (!TryBuildPreview(role, stone, powder, tier, request.TargetType, out var result, out var rollFailure))
            {
                preview.Result = result;
                preview.FailureReason = rollFailure;
                return preview;
            }

            preview.Result = result;
            if (preview.Materials.Any(cost => cost.MissingAmount > 0))
            {
                preview.FailureReason = "Brakuje materiałów. Kup brakujące pozycje z oferty Zaklinacza.";
                return preview;
            }

            if (!CanReceive(recipient.Inventory, result))
            {
                preview.FailureReason = "Postać nie ma wystarczającego udźwigu na galduryt.";
                return preview;
            }

            preview.CanCraft = true;
            return preview;
        }

        public bool TryCraft(
            GameSession session,
            NpcInstance enchanter,
            GalduryteCraftRequest request,
            out GalduryteItemInstance crafted,
            out string failureReason)
        {
            crafted = null;
            failureReason = string.Empty;

            var preview = CreateCraftPreview(session, enchanter, request);
            if (!preview.CanCraft)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryGetRole(enchanter, out var role)
                || !TryGetRecipient(session, request.RecipientCharacterId, out var recipient)
                || !TryResolveInputs(role, request, out var stone, out var powder, out var tier, out failureReason)
                || !TrySelectConsumedItems(session, preview.Materials, out var consumed))
            {
                if (string.IsNullOrWhiteSpace(failureReason))
                    failureReason = "Nie udało się przygotować materiałów do craftingu galdurytu.";
                return false;
            }

            if (!TryBuildResult(role, stone, powder, tier, request.TargetType, out crafted, out failureReason))
                return false;

            foreach (var item in consumed) item.Inventory.Items.Remove(item.Item);

            if (inventoryService.TryAddItem(recipient.Inventory, crafted))
                return true;

            foreach (var item in consumed) item.Inventory.Items.Add(item.Item);
            crafted = null;
            failureReason = "Nie udało się przekazać galdurytu do inventory postaci.";
            return false;
        }

        public bool TryBuyMissingMaterial(
            GameSession session,
            NpcInstance enchanter,
            GalduryteCraftRequest request,
            string materialId,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            failureReason = string.Empty;

            if (!GalduryteServiceAccess.IsUnlocked(session))
            { failureReason = GalduryteServiceAccess.LockedMessage; return false; }

            if (!TryGetRole(enchanter, out var role)
                || enchanter.Definition.GetRole<TraderRoleData>() == null
                || enchanter.ShopInventory?.Items == null
                || string.IsNullOrWhiteSpace(materialId)
                || !TryGetRecipient(session, request?.RecipientCharacterId, out var recipient)
                || !TryResolveInputs(role, request, out var stone, out var powder, out _, out failureReason))
            {
                if (string.IsNullOrWhiteSpace(failureReason))
                    failureReason = "Nie można kupić tego materiału u wybranego Zaklinacza.";
                return false;
            }

            var required = new[] { stone, powder }
                .Where(item => item != null && item.Id == materialId)
                .FirstOrDefault();
            if (required == null)
            {
                failureReason = "Ten materiał nie jest częścią wybranego galdurytu.";
                return false;
            }

            var owned = CountPartyItems(session, materialId);
            if (owned >= 1)
            {
                failureReason = "Ten materiał nie jest obecnie potrzebny.";
                return false;
            }

            var stock = CraftingMerchantStock.Items(enchanter)
                .Where(item => item?.Definition?.Id == materialId)
                .Take(1)
                .ToList();
            if (stock.Count != 1)
            {
                failureReason = "Zaklinacz nie ma już dostępnego zapasu tego materiału.";
                return false;
            }

            paidGold = CraftingMerchantStock.Price(enchanter, stock[0]);
            if (paidGold <= 0)
            {
                failureReason = "Nie udało się ustalić ceny tego materiału.";
                return false;
            }

            if (session.Gold < paidGold || !CanReceive(recipient.Inventory, stock[0]))
            {
                failureReason = session.Gold < paidGold
                    ? "Brakuje złota na ten materiał."
                    : "Postać nie ma wystarczającego udźwigu na materiał.";
                paidGold = 0;
                return false;
            }

            if (!session.TrySpendGold(paidGold))
            {
                failureReason = "Brakuje złota na ten materiał.";
                paidGold = 0;
                return false;
            }

            var origins = CraftingMerchantStock.Remove(enchanter, stock);
            if (inventoryService.TryAddItem(recipient.Inventory, stock[0]))
            {
                loyaltyService.TryRecordGoldSpent(enchanter, paidGold);
                economyService.TryReceiveGold(enchanter, paidGold);
                return true;
            }

            CraftingMerchantStock.Restore(enchanter, origins);
            session.TryAddGold(paidGold);
            paidGold = 0;
            failureReason = "Nie udało się przekazać materiału do inventory postaci.";
            return false;
        }

        public GalduryteIdentificationPreview CreateIdentificationPreview(
            GameSession session,
            NpcInstance enchanter,
            GalduryteIdentificationRequest request)
        {
            var preview = new GalduryteIdentificationPreview();
            if (!GalduryteServiceAccess.IsUnlocked(session))
            { preview.FailureReason = GalduryteServiceAccess.LockedMessage; return preview; }
            if (!TryGetRole(enchanter, out _))
            {
                preview.FailureReason = "Wybrany NPC nie oferuje identyfikowania galdurytów.";
                return preview;
            }

            if (!TryFindGalduryte(session, request?.GalduryteInstanceId, out var galduryte, out _))
            {
                preview.FailureReason = "Nie znaleziono wskazanego galdurytu w inventory ani skarbcu.";
                return preview;
            }

            if (galduryte.IsIdentified)
            {
                preview.FailureReason = "Ten galduryt jest już zidentyfikowany.";
                return preview;
            }

            preview.Galduryte = galduryte;
            preview.GoldCost = GetIdentificationCost(enchanter, galduryte);
            preview.CanIdentify = preview.GoldCost > 0 && session != null && session.Gold >= preview.GoldCost;
            if (!preview.CanIdentify)
            {
                preview.FailureReason = preview.GoldCost <= 0
                    ? "Nie udało się ustalić kosztu identyfikacji galdurytu."
                    : "Brakuje złota na identyfikację galdurytu.";
            }

            return preview;
        }

        public int GetIdentificationCost(NpcInstance enchanter, GalduryteItemInstance galduryte)
        {
            if (galduryte == null || galduryte.BaseValue <= 0) return 0;
            var percentage = Mathf.Clamp01(GameDefinesRuntime.GetFloat(
                GameDefineIds.GalduryteIdentificationCostPercent,
                DefaultIdentificationCostPercent));
            var baseCost = Math.Max(1, (int)Math.Ceiling(galduryte.BaseValue * percentage));
            return loyaltyService.GetDiscountedGoldCost(enchanter, baseCost);
        }

        public bool TryIdentify(
            GameSession session,
            NpcInstance enchanter,
            GalduryteIdentificationRequest request,
            out GalduryteItemInstance identified,
            out int paidGold,
            out string failureReason)
        {
            identified = null;
            paidGold = 0;
            failureReason = string.Empty;

            var preview = CreateIdentificationPreview(session, enchanter, request);
            if (!preview.CanIdentify)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!session.TrySpendGold(preview.GoldCost))
            {
                failureReason = "Brakuje złota na identyfikację galdurytu.";
                return false;
            }

            preview.Galduryte.IsIdentified = true;
            loyaltyService.TryRecordGoldSpent(enchanter, preview.GoldCost);
            economyService.TryReceiveGold(enchanter, preview.GoldCost);
            identified = preview.Galduryte;
            paidGold = preview.GoldCost;
            return true;
        }

        public bool TryCreateDebugGalduryte(
            GalduryteItemDefinition definition,
            ItemDefinition stone,
            ItemDefinition powder,
            GalduryteTier tier,
            GalduryteTargetType targetType,
            bool identified,
            out GalduryteItemInstance galduryte,
            out string failureReason)
        {
            galduryte = null;
            failureReason = string.Empty;
            if (definition == null)
            {
                failureReason = "Brak Galduryte Item Definition w katalogu.";
                return false;
            }

            var level = GalduryteRuntimeRules.GetLevel(tier);
            if (level <= 0)
            {
                failureReason = "Nieprawidłowy tier galdurytu.";
                return false;
            }

            if (!GalduryteRuntimeRules.TryRollRarity(random, out var rarity)
                || !GalduryteRuntimeRules.TryRollEffects(
                    targetType,
                    GaldurytePowderColor.Red,
                    rarity,
                    level,
                    random,
                    out var effects,
                    out failureReason))
            {
                return false;
            }

            galduryte = itemFactory.Create(definition) as GalduryteItemInstance;
            if (galduryte == null)
            {
                failureReason = "Nie udało się utworzyć instancji galdurytu.";
                return false;
            }

            galduryte.TargetType = targetType;
            galduryte.Tier = tier;
            galduryte.ItemLevel = level;
            galduryte.CurrentRarity = rarity;
            galduryte.GuaranteedPowderColor = GaldurytePowderColor.Red;
            galduryte.StoneDefinitionId = $"debug.galduryte.stone.{tier}";
            galduryte.PowderDefinitionId = "debug.galduryte.powder.red";
            galduryte.StoneDisplayName = GalduryteRuntimeRules.GetStoneName(tier);
            galduryte.CraftedBaseValue = Math.Max(1,
                ItemDefinitionValueUtility.GetValue(stone)
                + ItemDefinitionValueUtility.GetValue(powder));
            galduryte.Effects = effects;
            galduryte.IsIdentified = identified;
            return true;
        }

        private bool TryBuildPreview(
            EnchanterRoleData role,
            ItemDefinition stone,
            ItemDefinition powder,
            GalduryteTier tier,
            GalduryteTargetType targetType,
            out GalduryteItemInstance result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;
            var powderData = role.GaldurytePowders?.FirstOrDefault(option => option?.Material?.Id == powder.Id);
            if (powderData == null)
            {
                failureReason = "Wybrany proszek nie jest skonfigurowany u Zaklinacza.";
                return false;
            }

            var level = GalduryteRuntimeRules.GetLevel(tier);
            var canGenerate = GalduryteRuntimeRules.CanGenerateEffects(
                    targetType,
                    powderData.Color,
                    level,
                    out failureReason);

            result = itemFactory.Create(role.GalduryteDefinition) as GalduryteItemInstance;
            if (result == null)
            {
                failureReason = "Zaklinacz nie ma poprawnej definicji wynikowego galdurytu.";
                return false;
            }

            result.TargetType = targetType;
            result.Tier = tier;
            result.ItemLevel = level;
            result.CurrentRarity = RarityTier.Common;
            result.GuaranteedPowderColor = powderData.Color;
            result.StoneDefinitionId = stone.Id;
            result.PowderDefinitionId = powder.Id;
            result.StoneDisplayName = GalduryteRuntimeRules.GetStoneName(tier);
            result.CraftedBaseValue = Math.Max(1,
                ItemDefinitionValueUtility.GetValue(stone)
                + ItemDefinitionValueUtility.GetValue(powder));
            result.Effects = new List<GalduryteEffectInstance>();
            result.IsIdentified = false;
            if (!canGenerate) failureReason = "Ten tier galdurytu nie jest jeszcze dostępny.";
            return canGenerate;
        }

        private bool TryBuildResult(
            EnchanterRoleData role,
            ItemDefinition stone,
            ItemDefinition powder,
            GalduryteTier tier,
            GalduryteTargetType targetType,
            out GalduryteItemInstance result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;
            if (!GalduryteRuntimeRules.TryRollRarity(random, out var rarity))
            {
                failureReason = "Nie udało się wylosować rarity galdurytu.";
                return false;
            }

            var powderData = role.GaldurytePowders.FirstOrDefault(option => option?.Material?.Id == powder.Id);
            if (powderData == null)
            {
                failureReason = "Wybrany proszek nie jest skonfigurowany u Zaklinacza.";
                return false;
            }

            var level = GalduryteRuntimeRules.GetLevel(tier);
            if (!GalduryteRuntimeRules.TryRollEffects(
                    targetType,
                    powderData.Color,
                    rarity,
                    level,
                    random,
                    out var effects,
                    out failureReason))
            {
                return false;
            }

            result = itemFactory.Create(role.GalduryteDefinition) as GalduryteItemInstance;
            if (result == null)
            {
                failureReason = "Zaklinacz nie ma poprawnej definicji wynikowego galdurytu.";
                return false;
            }

            result.TargetType = targetType;
            result.Tier = tier;
            result.ItemLevel = level;
            result.CurrentRarity = rarity;
            result.GuaranteedPowderColor = powderData.Color;
            result.StoneDefinitionId = stone.Id;
            result.PowderDefinitionId = powder.Id;
            result.StoneDisplayName = GalduryteRuntimeRules.GetStoneName(tier);
            result.CraftedBaseValue = Math.Max(1,
                ItemDefinitionValueUtility.GetValue(stone)
                + ItemDefinitionValueUtility.GetValue(powder));
            result.Effects = effects;
            result.IsIdentified = true;
            return true;
        }

        private static bool TryResolveInputs(
            EnchanterRoleData role,
            GalduryteCraftRequest request,
            out ItemDefinition stone,
            out ItemDefinition powder,
            out GalduryteTier tier,
            out string failureReason)
        {
            stone = null;
            powder = null;
            tier = default;
            failureReason = string.Empty;

            if (request == null)
            {
                failureReason = "Wybierz galdurytowy kamień i kolor proszku.";
                return false;
            }

            if (!Enum.IsDefined(typeof(GalduryteTargetType), request.TargetType))
            {
                failureReason = "Nieprawidłowy cel galdurytu.";
                return false;
            }

            var stoneOption = role.GalduryteStones?.FirstOrDefault(option =>
                option?.Material != null && option.Material.Id == request.Stone?.Id);
            var powderOption = role.GaldurytePowders?.FirstOrDefault(option =>
                option?.Material != null && option.Material.Id == request.Powder?.Id);

            if (stoneOption == null)
            {
                failureReason = "Wybrany kamień galdurytu nie jest dostępny u Zaklinacza.";
                return false;
            }

            if (powderOption == null)
            {
                failureReason = "Wybrany proszek galdurytu nie jest dostępny u Zaklinacza.";
                return false;
            }

            if (!Enum.IsDefined(typeof(GalduryteTier), stoneOption.Tier)
                || GalduryteRuntimeRules.GetLevel(stoneOption.Tier) <= 0)
            {
                failureReason = "Kamień galdurytu ma nieprawidłowy tier.";
                return false;
            }

            stone = stoneOption.Material;
            powder = powderOption.Material;
            tier = stoneOption.Tier;
            return true;
        }

        private List<CraftingMaterialCost> BuildMaterialCosts(
            GameSession session,
            NpcInstance enchanter,
            ItemDefinition stone,
            ItemDefinition powder)
        {
            return new[] { stone, powder }
                .Where(item => item != null)
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.DisplayName)
                .Select(material =>
                {
                    var owned = CountPartyItems(session, material.Id);
                    var missing = Mathf.Max(0, 1 - owned);
                    var stock = CraftingMerchantStock.Items(enchanter).Count(item => item?.Definition?.Id == material.Id);
                    var purchasable = Mathf.Min(missing, stock);
                    var price = purchasable <= 0
                        ? 0
                        : CraftingMerchantStock.Items(enchanter)
                            .Where(item => item?.Definition?.Id == material.Id)
                            .Take(purchasable)
                            .Sum(item => CraftingMerchantStock.Price(enchanter, item));
                    return new CraftingMaterialCost
                    {
                        Material = material,
                        RequiredAmount = 1,
                        OwnedAmount = owned,
                        AvailableFromBlacksmith = stock,
                        PurchasePrice = price,
                        CanAffordPurchase = purchasable > 0 && price > 0 && session != null && session.Gold >= price,
                        PurchaseState = missing <= 0
                            ? CraftingMaterialPurchaseState.NotNeeded
                            : purchasable <= 0 || price <= 0
                                ? CraftingMaterialPurchaseState.OutOfStock
                                : session != null && session.Gold >= price
                                    ? CraftingMaterialPurchaseState.Available
                                    : CraftingMaterialPurchaseState.InsufficientGold
                    };
                })
                .ToList();
        }

        private static bool TrySelectConsumedItems(
            GameSession session,
            IReadOnlyList<CraftingMaterialCost> costs,
            out List<PartyInventoryItem> selected)
        {
            selected = new List<PartyInventoryItem>();
            foreach (var cost in costs ?? Array.Empty<CraftingMaterialCost>())
            {
                var remaining = cost.RequiredAmount;
                foreach (var characterId in session.PlayerParty.ActiveMemberIds)
                {
                    if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                    foreach (var item in character.Inventory?.Items ?? new List<ItemInstance>())
                    {
                        if (remaining <= 0) break;
                        if (item?.Definition?.Id != cost.Material?.Id) continue;
                        selected.Add(new PartyInventoryItem(character.Inventory, item));
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private static bool TryFindGalduryte(
            GameSession session,
            string instanceId,
            out GalduryteItemInstance galduryte,
            out Inventory inventory)
        {
            galduryte = null;
            inventory = null;
            if (session == null || string.IsNullOrWhiteSpace(instanceId)) return false;

            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                var item = character.Inventory?.Items?.FirstOrDefault(candidate => candidate?.InstanceId == instanceId)
                    as GalduryteItemInstance;
                if (item == null) continue;
                galduryte = item;
                inventory = character.Inventory;
                return true;
            }

            var stashItem = session.Stash.Items?.FirstOrDefault(candidate => candidate?.InstanceId == instanceId)
                as GalduryteItemInstance;
            if (stashItem == null) return false;
            galduryte = stashItem;
            inventory = session.Stash;
            return true;
        }

        private static int CountPartyItems(GameSession session, string definitionId)
        {
            if (session == null || string.IsNullOrWhiteSpace(definitionId)) return 0;
            return session.PlayerParty.ActiveMemberIds.Sum(characterId =>
                session.CharactersById.TryGetValue(characterId, out var character)
                    ? character.Inventory?.Items?.Count(item => item?.Definition?.Id == definitionId) ?? 0
                    : 0);
        }

        private static bool CanReceive(Inventory inventory, ItemInstance item)
        {
            if (inventory?.Items == null || item?.Definition == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var currentWeight = inventory.Items.Sum(candidate => Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            return currentWeight + Mathf.Max(0f, item.Definition.Weight) <= inventory.MaxWeight.Value;
        }

        private static bool TryGetRecipient(
            GameSession session,
            string characterId,
            out CharacterInstance recipient)
        {
            recipient = null;
            return session != null
                && !string.IsNullOrWhiteSpace(characterId)
                && session.PlayerParty.IsActiveMember(characterId)
                && session.CharactersById.TryGetValue(characterId, out recipient)
                && recipient?.Inventory?.Items != null;
        }

        private static bool TryGetRole(NpcInstance enchanter, out EnchanterRoleData role)
        {
            role = enchanter?.Definition?.GetRole<EnchanterRoleData>();
            return role != null && role.GalduryteDefinition != null;
        }

        private readonly struct PartyInventoryItem
        {
            public PartyInventoryItem(Inventory inventory, ItemInstance item)
            {
                Inventory = inventory;
                Item = item;
            }

            public Inventory Inventory { get; }
            public ItemInstance Item { get; }
        }
    }
}
