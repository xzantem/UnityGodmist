using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class EquipmentCraftRequest
    {
        public EquipmentItemDefinition Definition;
        public IReadOnlyList<EquipmentPartDefinition> Parts;
        public EquipmentQuality Quality;
        public string CustomName = string.Empty;
        public string RecipientCharacterId = string.Empty;
    }

    public sealed class CraftingMaterialCost
    {
        public ItemDefinition Material;
        public int RequiredAmount;
        public int OwnedAmount;
        public int AvailableFromBlacksmith;
        public int MissingAmount => Mathf.Max(0, RequiredAmount - OwnedAmount);
        public int PurchasableAmount => Mathf.Min(MissingAmount, AvailableFromBlacksmith);
        public bool HasPurchasableStock => PurchasableAmount > 0;
        public bool CanAffordPurchase;
        public CraftingMaterialPurchaseState PurchaseState;
        public int PurchasePrice;
        public string PurchaseFailureReason = string.Empty;
    }

    public enum CraftingMaterialPurchaseState
    {
        NotNeeded,
        Available,
        InsufficientGold,
        OutOfStock
    }

    public sealed class EquipmentCraftPreview
    {
        public EquipmentItemInstance Result;
        public IReadOnlyList<CraftingMaterialCost> Materials;
        public int MissingPurchasePrice;
        public bool CanCraft;
        public string FailureReason = string.Empty;
    }

    public sealed class BlacksmithCraftingService
    {
        private readonly EquipmentGenerationService equipmentGeneration;
        private readonly InventoryService inventoryService;
        private readonly TradeService tradeService;
        private readonly NpcLoyaltyService npcLoyaltyService = new();

        public BlacksmithCraftingService(
            EquipmentGenerationService equipmentGeneration = null,
            InventoryService inventoryService = null,
            TradeService tradeService = null)
        {
            this.equipmentGeneration = equipmentGeneration
                ?? new EquipmentGenerationService();
            this.inventoryService = inventoryService ?? new InventoryService();
            this.tradeService = tradeService ?? new TradeService();
        }

        public EquipmentCraftPreview CreatePreview(
            GameSession session,
            NpcInstance blacksmith,
            EquipmentCraftRequest request)
        {
            var preview = new EquipmentCraftPreview
            {
                Materials = Array.Empty<CraftingMaterialCost>()
            };

            if (!IsValidBlacksmith(blacksmith))
            {
                preview.FailureReason = "Wybrany NPC nie oferuje usług kowalskich.";
                return preview;
            }

            if (!TryGetRecipient(session, request?.RecipientCharacterId, out var recipient))
            {
                preview.FailureReason = "Wybierz aktywną postać, która odbierze przedmiot.";
                return preview;
            }

            if (request.Definition == null)
            {
                preview.FailureReason = "Wybierz wyposażenie do wytworzenia.";
                return preview;
            }

            if (request.Definition.IsStarterEquipment)
            { preview.FailureReason = "Wyposażenie startowe nie jest dostępne w rzemiośle."; return preview; }

            if (request.Definition is UniqueEquipmentDefinition)
            {
                preview.FailureReason = "Unikatowego wyposażenia bossa nie można wytworzyć u kowala.";
                return preview;
            }

            if (!IsCraftableQuality(request.Quality))
            {
                preview.FailureReason = "Wybierz jakość: kiepska, normalna albo doskonała.";
                return preview;
            }

            if (request.Parts?.Any(part => part != null && part.IsStarterOnly) == true)
            { preview.FailureReason = "Części startowe nie są dostępne w rzemiośle."; return preview; }

            var result = equipmentGeneration.GenerateFromSelectedParts(
                request.Definition,
                request.Parts,
                request.Quality,
                RarityTier.Common);

            if (result == null)
            {
                preview.FailureReason = "Wybierz trzy poprawne części dla tego wyposażenia.";
                return preview;
            }

            result.CustomName = NormalizeName(request.CustomName);
            preview.Result = result;

            if (!TryBuildMaterialCosts(session, blacksmith, request, out var costs))
            {
                preview.FailureReason = "Każda wybrana część musi mieć skonfigurowane materiały.";
                return preview;
            }

            preview.Materials = costs;
            preview.MissingPurchasePrice = costs.Sum(cost => cost.PurchasePrice);

            if (costs.Any(cost => cost.MissingAmount > 0))
            {
                preview.FailureReason = "Brakuje materiałów. Kup brakujące pozycje z oferty kowala.";
                return preview;
            }

            if (!CanReceive(recipient.Inventory, result))
            {
                preview.FailureReason = "Postać nie ma wystarczającego udźwigu na gotowy przedmiot.";
                return preview;
            }

            preview.CanCraft = true;
            return preview;
        }

        public bool TryCraft(
            GameSession session,
            NpcInstance blacksmith,
            EquipmentCraftRequest request,
            out EquipmentItemInstance craftedItem,
            out string failureReason)
        {
            craftedItem = null;
            failureReason = string.Empty;

            var preview = CreatePreview(session, blacksmith, request);
            if (!preview.CanCraft)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryGetRecipient(session, request.RecipientCharacterId, out var recipient)
                || !TrySelectConsumedItems(
                    session,
                    preview.Materials,
                    out var partyItems))
            {
                failureReason = "Nie udało się przygotować materiałów do craftingu.";
                return false;
            }

            var result = equipmentGeneration.GenerateFromSelectedParts(
                request.Definition,
                request.Parts,
                request.Quality);

            if (result == null)
            {
                failureReason = "Nie udało się wygenerować wybranego wyposażenia.";
                return false;
            }

            // Keep the crafting contract explicit at the service boundary. The generator
            // already applies the same value while building stats, but the runtime item
            // must retain the player's selected quality as its persisted identity too.
            result.Quality = request.Quality;
            result.CustomName = NormalizeName(request.CustomName);

            foreach (var item in partyItems)
            {
                item.Inventory.Items.Remove(item.Item);
            }

            if (!inventoryService.TryAddItem(recipient.Inventory, result))
            {
                foreach (var item in partyItems) item.Inventory.Items.Add(item.Item);
                failureReason = "Nie udało się przekazać przedmiotu do inventory postaci.";
                return false;
            }

            craftedItem = result;
            return true;
        }

        public bool TryBuyMissingMaterial(
            GameSession session,
            NpcInstance blacksmith,
            EquipmentCraftRequest request,
            string materialId,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            failureReason = string.Empty;

            if (!IsValidBlacksmith(blacksmith)
                || blacksmith.Definition.GetRole<TraderRoleData>() == null
                || blacksmith.ShopInventory?.Items == null
                || !TryGetRecipient(session, request?.RecipientCharacterId, out var recipient)
                || string.IsNullOrWhiteSpace(materialId)
                || !TryBuildMaterialCosts(session, blacksmith, request, out var costs))
            {
                failureReason = "Nie można kupić tego materiału u wybranego kowala.";
                return false;
            }

            var cost = costs.FirstOrDefault(candidate => candidate.Material.Id == materialId);
            if (cost == null || cost.MissingAmount < 1)
            {
                failureReason = "Ten materiał nie jest obecnie potrzebny.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(cost.PurchaseFailureReason))
            {
                failureReason = cost.PurchaseFailureReason;
                return false;
            }

            var stock = CraftingMerchantStock.Items(blacksmith).Where(item =>
                    item?.Definition?.Id == materialId)
                .Take(cost.PurchasableAmount)
                .ToList();
            paidGold = stock.Sum(item =>
                CraftingMerchantStock.Price(blacksmith, item));

            if (stock.Count != cost.PurchasableAmount || paidGold <= 0)
            {
                failureReason = stock.Count == 0
                    ? "Kowal nie ma tego materiału na sprzedaż."
                    : "Kowal nie ma już dostępnego zapasu tego materiału.";
                paidGold = 0;
                return false;
            }

            if (session.Gold < paidGold || !CanReceiveAll(recipient.Inventory, stock))
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

            var origins = CraftingMerchantStock.Remove(blacksmith, stock);
            foreach (var item in stock)
            {
                if (inventoryService.TryAddItem(recipient.Inventory, item)) continue;

                foreach (var transferred in stock)
                {
                    recipient.Inventory.Items.Remove(transferred);
                }
                CraftingMerchantStock.Restore(blacksmith, origins);
                session.TryAddGold(paidGold);
                failureReason = "Nie udało się przekazać materiału do inventory postaci.";
                paidGold = 0;
                return false;
            }

            npcLoyaltyService.TryRecordGoldSpent(blacksmith, paidGold);
            new NpcEconomyService().TryReceiveGold(blacksmith, paidGold);
            return true;
        }

        private static bool IsValidBlacksmith(NpcInstance npc) =>
            npc?.Definition?.GetRole<BlacksmithRoleData>() != null;

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
                && recipient.Inventory?.Items != null;
        }

        private static bool CanReceive(Inventory inventory, ItemInstance item)
        {
            if (inventory?.Items == null || item?.Definition == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;

            var weight = inventory.Items.Sum(candidate =>
                Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            return weight + Mathf.Max(0f, item.Definition.Weight)
                <= inventory.MaxWeight.Value;
        }

        private static bool CanReceiveAll(
            Inventory inventory,
            IEnumerable<ItemInstance> items)
        {
            if (inventory?.Items == null || items == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;

            var currentWeight = inventory.Items.Sum(candidate =>
                Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            var incomingWeight = items.Sum(candidate =>
                Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            return currentWeight + incomingWeight <= inventory.MaxWeight.Value;
        }

        private bool TryBuildMaterialCosts(
            GameSession session,
            NpcInstance blacksmith,
            EquipmentCraftRequest request,
            out List<CraftingMaterialCost> costs)
        {
            costs = new List<CraftingMaterialCost>();
            var requiredAmounts = new Dictionary<ItemDefinition, int>();

            foreach (var part in request?.Parts ?? Array.Empty<EquipmentPartDefinition>())
            {
                if (part?.CraftingMaterials == null || part.CraftingMaterials.Count == 0)
                {
                    return false;
                }

                foreach (var requirement in part.CraftingMaterials)
                {
                    if (requirement?.Material == null
                        || requirement.Material.ItemType != ItemType.Material
                        || GetMaterialDiscipline(requirement.Material)
                            != MaterialDiscipline.Smithing
                        || requirement.Amount < 1)
                    {
                        return false;
                    }

                    var scaledAmount = Mathf.CeilToInt(
                        requirement.Amount * GetQualityCostMultiplier(request.Quality));
                    scaledAmount = Mathf.Max(1, scaledAmount);
                    requiredAmounts.TryGetValue(requirement.Material, out var existing);
                    requiredAmounts[requirement.Material] = existing + scaledAmount;
                }
            }

            foreach (var pair in requiredAmounts.OrderBy(entry => entry.Key.DisplayName))
            {
                var owned = CountPartyItems(session, pair.Key.Id);
                var missing = Mathf.Max(0, pair.Value - owned);
                var stock = GetBlacksmithStock(blacksmith, pair.Key.Id);

                costs.Add(new CraftingMaterialCost
                {
                    Material = pair.Key,
                    RequiredAmount = pair.Value,
                    OwnedAmount = owned,
                    AvailableFromBlacksmith = stock,
                    PurchasePrice = GetStockPurchasePrice(blacksmith, pair.Key.Id, Mathf.Min(missing, stock))
                });
                var cost = costs[costs.Count - 1];
                cost.CanAffordPurchase = cost.PurchasableAmount > 0
                    && cost.PurchasePrice > 0
                    && session != null
                    && session.Gold >= cost.PurchasePrice;
                cost.PurchaseState = cost.MissingAmount <= 0
                    ? CraftingMaterialPurchaseState.NotNeeded
                    : !cost.HasPurchasableStock || cost.PurchasePrice <= 0
                        ? CraftingMaterialPurchaseState.OutOfStock
                        : cost.CanAffordPurchase
                            ? CraftingMaterialPurchaseState.Available
                            : CraftingMaterialPurchaseState.InsufficientGold;
            }

            return costs.Count > 0;
        }

        private static float GetQualityCostMultiplier(EquipmentQuality quality) => quality switch
        {
            EquipmentQuality.Poor => GameDefinesRuntime.GetFloat(
                GameDefineIds.EquipmentPoorQualityCostMultiplier, .5f),
            EquipmentQuality.Excellent => GameDefinesRuntime.GetFloat(
                GameDefineIds.EquipmentExcellentQualityCostMultiplier, 2f),
            _ => 1f
        };

        private static MaterialDiscipline GetMaterialDiscipline(ItemDefinition item)
        {
            return item is CraftableItemDefinition craftable
                ? craftable.CraftingDiscipline
                : item?.MaterialDiscipline ?? MaterialDiscipline.None;
        }

        private static bool IsCraftableQuality(EquipmentQuality quality) => quality is
            EquipmentQuality.Poor or EquipmentQuality.Normal or EquipmentQuality.Excellent;

        private static int CountPartyItems(GameSession session, string definitionId)
        {
            if (session == null || string.IsNullOrWhiteSpace(definitionId)) return 0;

            var count = 0;
            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                count += character.Inventory?.Items?.Count(item =>
                    item?.Definition?.Id == definitionId) ?? 0;
            }

            return count;
        }

        private static int GetBlacksmithStock(NpcInstance blacksmith, string definitionId) =>
            CraftingMerchantStock.Items(blacksmith).Count(item =>
                item?.Definition?.Id == definitionId);

        private int GetStockPurchasePrice(
            NpcInstance blacksmith,
            string definitionId,
            int amount)
        {
            if (amount <= 0) return 0;

            return CraftingMerchantStock.Items(blacksmith)
                .Where(candidate => candidate?.Definition?.Id == definitionId)
                .Take(amount)
                .Sum(item => CraftingMerchantStock.Price(blacksmith, item));
        }

        private static bool TrySelectConsumedItems(
            GameSession session,
            IReadOnlyList<CraftingMaterialCost> costs,
            out List<PartyInventoryItem> partyItems)
        {
            partyItems = new List<PartyInventoryItem>();

            foreach (var cost in costs)
            {
                var remaining = cost.RequiredAmount;
                foreach (var characterId in session.PlayerParty.ActiveMemberIds)
                {
                    if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                    foreach (var item in character.Inventory?.Items ?? new List<ItemInstance>())
                    {
                        if (remaining <= 0) break;
                        if (item?.Definition?.Id != cost.Material.Id) continue;
                        partyItems.Add(new PartyInventoryItem(character.Inventory, item));
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }

                if (remaining > 0) return false;
            }

            return true;
        }

        private static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

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
