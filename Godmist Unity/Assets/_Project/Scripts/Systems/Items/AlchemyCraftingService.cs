using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
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
    public sealed class AlchemyCraftRequest
    {
        public ConsumableUseType UseType = ConsumableUseType.Potion;
        public List<ItemDefinition> Ingredients = new();
        public ItemDefinition Medium;
        public ItemDefinition Catalyst;
        public string CustomName = string.Empty;
        public string RecipientCharacterId = string.Empty;
    }

    public sealed class AlchemyMaterialCost
    {
        public ItemDefinition Material;
        public int RequiredAmount;
        public int OwnedAmount;
        public int AvailableFromAlchemist;
        public int MissingAmount => Mathf.Max(0, RequiredAmount - OwnedAmount);
        public int PurchasableAmount => Mathf.Min(MissingAmount, AvailableFromAlchemist);
        public bool HasPurchasableStock => PurchasableAmount > 0;
        public bool CanAffordPurchase;
        public CraftingMaterialPurchaseState PurchaseState;
        public int PurchasePrice;
        public string PurchaseFailureReason = string.Empty;
    }

    public sealed class AlchemyCraftPreview
    {
        public ConsumableItemInstance Result;
        public int ResultAmount;
        public int ResultLevel;
        public int ResultEffectTier;
        public ItemDefinition Medium;
        public ItemDefinition Catalyst;
        public IReadOnlyList<ItemDefinition> Ingredients;
        public IReadOnlyList<AlchemyMaterialCost> Materials;
        public bool CanCraft;
        public string FailureReason = string.Empty;
    }

    public sealed class AlchemyCraftingService
    {
        private readonly InventoryService inventoryService;
        private readonly ItemInstanceFactory itemFactory;
        private readonly TradeService tradeService;
        private readonly NpcLoyaltyService loyaltyService = new();

        public AlchemyCraftingService(
            InventoryService inventoryService = null,
            ItemInstanceFactory itemFactory = null,
            TradeService tradeService = null)
        {
            this.inventoryService = inventoryService ?? new InventoryService();
            this.itemFactory = itemFactory ?? new ItemInstanceFactory();
            this.tradeService = tradeService ?? new TradeService();
        }

        public IReadOnlyList<AlchemyIngredientData> GetIngredientOptions(NpcInstance alchemist) =>
            alchemist?.Definition?.GetRole<AlchemistRoleData>()?.IngredientOptions?
                .Where(option => IsConfiguredAlchemyMaterial(option?.Material))
                .ToList()
                ?? Enumerable.Empty<AlchemyIngredientData>().ToList();

        public IReadOnlyList<AlchemyMediumData> GetMediumOptions(
            NpcInstance alchemist,
            ConsumableUseType useType)
        {
            var role = alchemist?.Definition?.GetRole<AlchemistRoleData>();
            if (role == null) return Array.Empty<AlchemyMediumData>();
            return useType == ConsumableUseType.Bomb
                ? role.BombGunpowders?.Where(option => IsConfiguredAlchemyMaterial(option?.Material)).ToList()
                    ?? Enumerable.Empty<AlchemyMediumData>().ToList()
                : role.PotionAlcohols?.Where(option => IsConfiguredAlchemyMaterial(option?.Material)).ToList()
                    ?? Enumerable.Empty<AlchemyMediumData>().ToList();
        }

        public IReadOnlyList<AlchemyCatalystData> GetCatalystOptions(NpcInstance alchemist) =>
            alchemist?.Definition?.GetRole<AlchemistRoleData>()?.Catalysts
                ?.Where(option => IsConfiguredAlchemyMaterial(option?.Material))
                .ToList()
                ?? Enumerable.Empty<AlchemyCatalystData>().ToList();

        public AlchemyCraftPreview CreatePreview(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request)
        {
            var preview = new AlchemyCraftPreview
            {
                Ingredients = request?.Ingredients ?? Enumerable.Empty<ItemDefinition>().ToList(),
                Materials = Array.Empty<AlchemyMaterialCost>()
            };

            if (!TryGetRole(alchemist, out var role))
            {
                preview.FailureReason = "Wybrany NPC nie oferuje usług alchemicznych.";
                return preview;
            }

            if (!AlchemyRuntimeRules.IsValidConsumableType(request?.UseType ?? ConsumableUseType.Utility))
            {
                preview.FailureReason = "Nieprawidłowy typ consumabla.";
                return preview;
            }

            if (!TryGetRecipient(session, request?.RecipientCharacterId, out var recipient))
            {
                preview.FailureReason = "Wybierz aktywną postać, która odbierze wynik craftingu.";
                return preview;
            }

            if (!TryResolveConfiguration(role, request, out var configuration, out var configurationFailure))
            {
                preview.FailureReason = configurationFailure;
                return preview;
            }

            preview.ResultLevel = configuration.ResultLevel;
            preview.ResultEffectTier = configuration.EffectTier;
            preview.Medium = request.Medium;
            preview.Catalyst = request.Catalyst;
            preview.Result = BuildResult(role, request, configuration);
            preview.ResultAmount = 1;
            preview.Materials = BuildMaterialCosts(session, alchemist, request);

            if (preview.Materials.Any(cost => cost.MissingAmount > 0))
            {
                preview.FailureReason = "Brakuje składników. Kup brakujące materiały albo uzupełnij zapasy drużyny.";
                return preview;
            }

            if (!CanReceiveAll(recipient.Inventory, preview.Result, preview.ResultAmount))
            {
                preview.FailureReason = "Postać nie ma wystarczającego udźwigu na wynik craftingu.";
                return preview;
            }

            preview.CanCraft = true;
            return preview;
        }

        public bool TryCraft(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request,
            out List<ConsumableItemInstance> craftedItems,
            out string failureReason)
        {
            craftedItems = new List<ConsumableItemInstance>();
            failureReason = string.Empty;

            var preview = CreatePreview(session, alchemist, request);
            if (!preview.CanCraft)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryGetRecipient(session, request.RecipientCharacterId, out var recipient)
                || !TrySelectConsumedItems(session, preview.Materials, out var consumedItems))
            {
                failureReason = "Nie udało się przygotować składników do craftingu.";
                return false;
            }

            foreach (var item in consumedItems) item.Inventory.Items.Remove(item.Item);

            if (inventoryService.TryAddItem(recipient.Inventory, preview.Result))
            {
                craftedItems.Add(preview.Result);
                return true;
            }

            foreach (var item in consumedItems) item.Inventory.Items.Add(item.Item);
            failureReason = "Nie udało się przekazać wyniku do inventory postaci.";
            return false;
        }

        public bool TryBuyMissingMaterial(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request,
            string materialId,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            failureReason = string.Empty;

            if (!TryGetRole(alchemist, out _)
                || alchemist.Definition.GetRole<TraderRoleData>() == null
                || alchemist.ShopInventory?.Items == null
                || !TryGetRecipient(session, request?.RecipientCharacterId, out var recipient))
            {
                failureReason = "Nie można kupić tego składnika u wybranego Alchemika.";
                return false;
            }

            var costs = CreatePreviewCosts(session, alchemist, request, out failureReason);
            if (costs == null) return false;

            var cost = costs.FirstOrDefault(candidate => candidate.Material?.Id == materialId);
            if (cost == null || cost.MissingAmount < 1)
            {
                failureReason = "Ten składnik nie jest obecnie potrzebny.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(cost.PurchaseFailureReason))
            {
                failureReason = cost.PurchaseFailureReason;
                return false;
            }

            var stock = alchemist.ShopInventory.Items
                .Where(item => item?.Definition?.Id == materialId)
                .Take(cost.PurchasableAmount)
                .ToList();
            paidGold = stock.Sum(item => tradeService.GetPriceToBuyFromTrader(alchemist, item));

            if (stock.Count != cost.PurchasableAmount || paidGold <= 0)
            {
                paidGold = 0;
                failureReason = "Alchemik nie ma już dostępnego zapasu tego składnika.";
                return false;
            }

            if (session.Gold < paidGold || !CanReceiveAll(recipient.Inventory, stock))
            {
                failureReason = session.Gold < paidGold
                    ? "Brakuje złota na ten składnik."
                    : "Postać nie ma wystarczającego udźwigu na składnik.";
                paidGold = 0;
                return false;
            }

            if (!session.TrySpendGold(paidGold))
            {
                failureReason = "Brakuje złota na ten składnik.";
                paidGold = 0;
                return false;
            }

            foreach (var item in stock) alchemist.ShopInventory.Items.Remove(item);
            foreach (var item in stock)
            {
                if (inventoryService.TryAddItem(recipient.Inventory, item)) continue;

                foreach (var transferred in stock)
                {
                    recipient.Inventory.Items.Remove(transferred);
                    if (!alchemist.ShopInventory.Items.Contains(transferred))
                        alchemist.ShopInventory.Items.Add(transferred);
                }

                session.TryAddGold(paidGold);
                paidGold = 0;
                failureReason = "Nie udało się przekazać składnika do inventory postaci.";
                return false;
            }

            loyaltyService.TryRecordGoldSpent(alchemist, paidGold);
            return true;
        }

        private List<AlchemyMaterialCost> CreatePreviewCosts(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request,
            out string failureReason)
        {
            failureReason = string.Empty;
            // Purchasing a selected draft material does not require the entire recipe
            // to be complete. In particular, alcohol/gunpowder may be selected later.
            if (!TryGetRole(alchemist, out _))
            {
                failureReason = "Wybrany NPC nie oferuje usług alchemicznych.";
                return null;
            }

            return BuildMaterialCosts(session, alchemist, request);
        }

        /// <summary>
        /// Returns the material panel state for an unfinished crafting request.
        /// Unlike <see cref="CreatePreview"/>, this intentionally accepts a partial
        /// selection so the UI can show requirements as soon as the player chooses them.
        /// </summary>
        public IReadOnlyList<AlchemyMaterialCost> GetDraftMaterialCosts(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request)
        {
            return BuildMaterialCosts(session, alchemist, request);
        }

        private List<AlchemyMaterialCost> BuildMaterialCosts(
            GameSession session,
            NpcInstance alchemist,
            AlchemyCraftRequest request)
        {
            var requirements = new Dictionary<string, (ItemDefinition Definition, int Amount)>();
            foreach (var ingredient in request?.Ingredients ?? Enumerable.Empty<ItemDefinition>())
                AddRequirement(requirements, ingredient, 1);
            AddRequirement(requirements, request?.Medium, 1);
            AddRequirement(requirements, request?.Catalyst, 1);

            var costs = new List<AlchemyMaterialCost>();
            foreach (var pair in requirements.Values.OrderBy(value => value.Definition.DisplayName))
            {
                var owned = CountPartyItems(session, pair.Definition.Id);
                var missing = Mathf.Max(0, pair.Amount - owned);
                var stock = GetAlchemistStock(alchemist, pair.Definition.Id);
                var purchasable = Mathf.Min(missing, stock);
                var price = GetStockPurchasePrice(alchemist, pair.Definition.Id, purchasable);
                var cost = new AlchemyMaterialCost
                {
                    Material = pair.Definition,
                    RequiredAmount = pair.Amount,
                    OwnedAmount = owned,
                    AvailableFromAlchemist = stock,
                    PurchasePrice = price,
                    CanAffordPurchase = purchasable > 0 && session != null && session.Gold >= price,
                    PurchaseState = missing <= 0
                        ? CraftingMaterialPurchaseState.NotNeeded
                        : purchasable <= 0 || price <= 0
                            ? CraftingMaterialPurchaseState.OutOfStock
                            : session != null && session.Gold >= price
                                ? CraftingMaterialPurchaseState.Available
                                : CraftingMaterialPurchaseState.InsufficientGold
                };

                costs.Add(cost);
            }

            return costs;
        }

        private static bool TryResolveConfiguration(
            AlchemistRoleData role,
            AlchemyCraftRequest request,
            out AlchemyConfiguration configuration,
            out string failureReason)
        {
            configuration = null;
            failureReason = string.Empty;
            if (request == null)
            {
                failureReason = "Wybierz składniki do craftingu.";
                return false;
            }

            var ingredientCount = AlchemyRuntimeRules.RequiredIngredientCount;
            if (request.Ingredients == null || request.Ingredients.Count != ingredientCount
                || request.Ingredients.Any(ingredient => ingredient == null))
            {
                failureReason = $"Wybierz dokładnie {ingredientCount} składniki alchemiczne.";
                return false;
            }

            if (request.Ingredients
                    .Select(ingredient => ingredient.Id)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != ingredientCount)
            {
                failureReason = $"Wybierz {ingredientCount} różne składniki alchemiczne.";
                return false;
            }

            var options = new List<AlchemyIngredientData>();
            foreach (var ingredient in request.Ingredients)
            {
                var option = role.IngredientOptions?.FirstOrDefault(candidate =>
                    IsConfiguredAlchemyMaterial(candidate?.Material)
                    && string.Equals(candidate.Material.Id, ingredient.Id, StringComparison.Ordinal));
                if (option?.GetEffect(request.UseType) == null)
                {
                    failureReason = "Każdy wybrany składnik musi mieć skonfigurowany efekt alchemiczny.";
                    return false;
                }

                options.Add(option);
            }

            var effectTier = options.Max(option => AlchemyRuntimeRules.GetEffectTier(option.Material));
            var level = AlchemyRuntimeRules.GetConsumableLevel(effectTier);
            var mediumOptions = request.UseType == ConsumableUseType.Bomb
                ? role.BombGunpowders
                : role.PotionAlcohols;
            var medium = mediumOptions?.FirstOrDefault(candidate =>
                IsConfiguredAlchemyMaterial(candidate?.Material)
                    && string.Equals(candidate.Material.Id, request.Medium?.Id, StringComparison.Ordinal));
            if (medium == null || !AlchemyRuntimeRules.IsMediumCompatible(level, medium.Tier))
            {
                failureReason = request.UseType == ConsumableUseType.Bomb
                    ? "Wybrany proch strzelniczy nie pasuje do poziomu bomby."
                    : "Wybrany alkohol nie pasuje do poziomu potiona.";
                return false;
            }

            AlchemyCatalystData catalyst = null;
            if (request.Catalyst != null)
            {
                catalyst = role.Catalysts?.FirstOrDefault(candidate =>
                    IsConfiguredAlchemyMaterial(candidate?.Material)
                    && string.Equals(candidate.Material.Id, request.Catalyst.Id, StringComparison.Ordinal));
                if (catalyst == null)
                {
                    failureReason = "Wybrany katalizator nie jest dostępny u tego Alchemika.";
                    return false;
                }
            }

            var resultDefinition = request.UseType == ConsumableUseType.Bomb
                ? role.BombBaseDefinition
                : role.PotionBaseDefinition;
            if (resultDefinition == null || resultDefinition.UseType != request.UseType)
            {
                failureReason = "Alchemik nie ma skonfigurowanego poprawnego typu wynikowego.";
                return false;
            }

            configuration = new AlchemyConfiguration(
                options, catalyst, effectTier, level, resultDefinition);
            return true;
        }

        private static void AddRequirement(
            IDictionary<string, (ItemDefinition Definition, int Amount)> requirements,
            ItemDefinition definition,
            int amount)
        {
            if (definition == null || amount < 1) return;
            if (requirements.TryGetValue(definition.Id, out var current))
                requirements[definition.Id] = (current.Definition, current.Amount + amount);
            else
                requirements[definition.Id] = (definition, amount);
        }

        private static bool TryGetRole(NpcInstance npc, out AlchemistRoleData role)
        {
            role = npc?.Definition?.GetRole<AlchemistRoleData>();
            return role != null;
        }

        private static bool IsConfiguredAlchemyMaterial(ItemDefinition material) =>
            material != null
            && material.ItemType == ItemType.Material
            && material.MaterialDiscipline == MaterialDiscipline.Alchemy;

        private ConsumableItemInstance BuildResult(
            AlchemistRoleData role,
            AlchemyCraftRequest request,
            AlchemyConfiguration configuration)
        {
            var result = (ConsumableItemInstance)itemFactory.Create(configuration.ResultDefinition);
            result.ItemLevel = configuration.ResultLevel;
            result.CurrentRarity = GetCraftedRarity(request);
            result.CraftedBaseValue = GetCraftedBaseValue(request);
            result.CustomName = string.IsNullOrWhiteSpace(request.CustomName)
                ? BuildDefaultName(role, request.UseType, configuration)
                : request.CustomName.Trim();
            result.RuntimeEffects = configuration.Ingredients
                .Select(option => option.GetEffect(request.UseType))
                .Where(effect => effect != null)
                .Select(effect => new ConsumableEffectRuntime
                {
                    EffectType = effect.EffectType,
                    Value = effect.Value,
                    DurationTurns = effect.DurationTurns,
                    Chance = Mathf.Clamp01(effect.Chance),
                    TargetModeIndex = effect.TargetModeIndex,
                    StatusId = effect.StatusId ?? string.Empty,
                    ResistanceType = effect.ResistanceType,
                    TargetType = effect.TargetType,
                    TargetingType = effect.TargetingType
                })
                .ToList();

            if (configuration.Catalyst != null)
            {
                result.ApplyCatalyst(configuration.Catalyst.CatalystType, configuration.Catalyst.Tier);
                result.CatalystDisplayName = GetCatalystShortName(configuration.Catalyst.Material?.DisplayName);
            }

            return result;
        }

        private static RarityTier GetCraftedRarity(AlchemyCraftRequest request)
        {
            var rarities = (request?.Ingredients ?? new List<ItemDefinition>())
                .Append(request?.Medium)
                .Append(request?.Catalyst)
                .Where(item => item != null)
                .Select(item => item.BaseRarity)
                .ToList();
            return rarities.Count == 0 ? RarityTier.Common : rarities.Max();
        }

        private static int GetCraftedBaseValue(AlchemyCraftRequest request)
        {
            var materials = (request?.Ingredients ?? new List<ItemDefinition>())
                .Append(request?.Medium)
                .Append(request?.Catalyst)
                .Where(item => item != null);
            return materials.Sum(ItemDefinitionValueUtility.GetValue);
        }

        private static string BuildDefaultName(
            AlchemistRoleData role,
            ConsumableUseType useType,
            AlchemyConfiguration configuration)
        {
            var adjectives = configuration.Ingredients
                .Select(option => new { Option = option, Effect = option.GetEffect(useType) })
                .Where(pair => pair.Effect != null)
                .Select(pair => string.IsNullOrWhiteSpace(pair.Option.EffectAdjective)
                    ? AlchemyRuntimeRules.GetFallbackEffectAdjective(pair.Effect.EffectType)
                    : pair.Option.EffectAdjective.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var tierAdjective = role.TierNameAdjectives?
                .FirstOrDefault(entry => entry != null && entry.EffectTier == configuration.EffectTier)
                ?.Adjective;
            adjectives.Add(string.IsNullOrWhiteSpace(tierAdjective)
                ? AlchemyRuntimeRules.GetFallbackTierAdjective(configuration.EffectTier)
                : tierAdjective.Trim());
            adjectives.Add(AlchemyRuntimeRules.GetConsumableNoun(useType));
            var name = string.Join(" ", adjectives).Trim();
            return string.IsNullOrEmpty(name)
                ? name
                : char.ToUpperInvariant(name[0]) + name[1..];
        }

        private static string GetCatalystShortName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Katalizator";
            var separator = displayName.LastIndexOf(':');
            return separator >= 0 && separator + 1 < displayName.Length
                ? displayName[(separator + 1)..].Trim()
                : displayName.Trim();
        }

        private static bool TryGetRecipient(GameSession session, string characterId, out CharacterInstance recipient)
        {
            recipient = null;
            return session != null
                && !string.IsNullOrWhiteSpace(characterId)
                && session.PlayerParty.IsActiveMember(characterId)
                && session.CharactersById.TryGetValue(characterId, out recipient)
                && recipient.Inventory?.Items != null;
        }

        private static int CountPartyItems(GameSession session, string definitionId)
        {
            if (session == null || string.IsNullOrWhiteSpace(definitionId)) return 0;
            return session.PlayerParty.ActiveMemberIds.Sum(characterId =>
                session.CharactersById.TryGetValue(characterId, out var character)
                    ? character.Inventory?.Items?.Count(item => item?.Definition?.Id == definitionId) ?? 0
                    : 0);
        }

        private static int GetAlchemistStock(NpcInstance alchemist, string definitionId) =>
            alchemist?.ShopInventory?.Items?.Count(item => item?.Definition?.Id == definitionId) ?? 0;

        private int GetStockPurchasePrice(NpcInstance alchemist, string definitionId, int amount) =>
            amount <= 0 ? 0 : alchemist?.ShopInventory?.Items?
                .Where(item => item?.Definition?.Id == definitionId)
                .Take(amount)
                .Sum(item => tradeService.GetPriceToBuyFromTrader(alchemist, item)) ?? 0;

        private static bool CanReceiveAll(Inventory inventory, ItemInstance item, int amount)
        {
            if (inventory?.Items == null || item?.Definition == null || amount < 1) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var weight = inventory.Items.Sum(candidate => Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            return weight + Mathf.Max(0f, item.Definition.Weight) * amount <= inventory.MaxWeight.Value;
        }

        private static bool CanReceiveAll(Inventory inventory, IEnumerable<ItemInstance> items)
        {
            if (inventory?.Items == null || items == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var weight = inventory.Items.Sum(candidate => Mathf.Max(0f, candidate?.Definition?.Weight ?? 0f));
            return weight + items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f)) <= inventory.MaxWeight.Value;
        }

        private static bool TrySelectConsumedItems(
            GameSession session,
            IReadOnlyList<AlchemyMaterialCost> costs,
            out List<PartyInventoryItem> items)
        {
            items = new List<PartyInventoryItem>();
            foreach (var cost in costs ?? Array.Empty<AlchemyMaterialCost>())
            {
                var remaining = cost.RequiredAmount;
                foreach (var characterId in session.PlayerParty.ActiveMemberIds)
                {
                    if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                    foreach (var item in character.Inventory?.Items ?? new List<ItemInstance>())
                    {
                        if (remaining <= 0) break;
                        if (item?.Definition?.Id != cost.Material.Id) continue;
                        items.Add(new PartyInventoryItem(character.Inventory, item));
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private sealed class AlchemyConfiguration
        {
            public AlchemyConfiguration(
                IReadOnlyList<AlchemyIngredientData> ingredients,
                AlchemyCatalystData catalyst,
                int effectTier,
                int resultLevel,
                ConsumableItemDefinition resultDefinition)
            {
                Ingredients = ingredients;
                Catalyst = catalyst;
                EffectTier = effectTier;
                ResultLevel = resultLevel;
                ResultDefinition = resultDefinition;
            }

            public IReadOnlyList<AlchemyIngredientData> Ingredients { get; }
            public AlchemyCatalystData Catalyst { get; }
            public int EffectTier { get; }
            public int ResultLevel { get; }
            public ConsumableItemDefinition ResultDefinition { get; }
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
