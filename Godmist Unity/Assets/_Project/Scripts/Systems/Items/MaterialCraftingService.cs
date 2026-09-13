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
    public sealed class MaterialCraftRequest
    {
        public CraftableItemDefinition Recipe;
        public string RecipientCharacterId = string.Empty;
    }

    public sealed class MaterialCraftPreview
    {
        public ItemDefinition Result;
        public int ResultAmount;
        public IReadOnlyList<CraftingMaterialCost> Materials;
        public bool CanCraft;
        public string FailureReason = string.Empty;
    }

    /// <summary>
    /// Crafts deterministic material recipes for a specialist NPC. This service
    /// deliberately creates regular ItemInstances; the existing inventory is the
    /// owner of stack presentation and persistence.
    /// </summary>
    public sealed class MaterialCraftingService
    {
        private readonly InventoryService inventoryService;
        private readonly ItemInstanceFactory itemFactory;
        private readonly TradeService tradeService;
        private readonly NpcLoyaltyService npcLoyaltyService = new();

        public MaterialCraftingService(
            InventoryService inventoryService = null,
            ItemInstanceFactory itemFactory = null,
            TradeService tradeService = null)
        {
            this.inventoryService = inventoryService ?? new InventoryService();
            this.itemFactory = itemFactory ?? new ItemInstanceFactory();
            this.tradeService = tradeService ?? new TradeService();
        }

        public MaterialCraftPreview CreatePreview(
            GameSession session,
            NpcInstance blacksmith,
            MaterialCraftRequest request)
        {
            var preview = new MaterialCraftPreview
            {
                Materials = Array.Empty<CraftingMaterialCost>()
            };

            if (!IsValidSpecialist(blacksmith))
            {
                preview.FailureReason = "Wybrany NPC nie oferuje material craftingu.";
                return preview;
            }

            if (!TryGetRecipient(session, request?.RecipientCharacterId, out var recipient))
            {
                preview.FailureReason = "Wybierz aktywną postać, która odbierze przedmioty.";
                return preview;
            }

            if (!IsValidRecipe(blacksmith, request?.Recipe))
            {
                preview.FailureReason = GetRecipeFailureReason(
                    blacksmith,
                    request?.Recipe);
                return preview;
            }

            preview.Result = request.Recipe;
            preview.ResultAmount = request.Recipe.OutputAmount;
            preview.Materials = BuildMaterialCosts(session, blacksmith, request.Recipe);
            if (preview.Materials.Count == 0)
            {
                preview.FailureReason = "Recepta nie ma poprawnie skonfigurowanych składników.";
                return preview;
            }

            if (preview.Materials.Any(cost => cost.MissingAmount > 0))
            {
                preview.FailureReason = "Brakuje materiałów. Kup brakujące pozycje z oferty specjalisty.";
                return preview;
            }

            if (!CanReceiveAll(recipient.Inventory, request.Recipe, request.Recipe.OutputAmount))
            {
                preview.FailureReason = "Postać nie ma wystarczającego udźwigu na wynik craftingu.";
                return preview;
            }

            preview.CanCraft = true;
            return preview;
        }

        public bool TryCraft(
            GameSession session,
            NpcInstance blacksmith,
            MaterialCraftRequest request,
            out IReadOnlyList<ItemInstance> craftedItems,
            out string failureReason)
        {
            craftedItems = Array.Empty<ItemInstance>();
            failureReason = string.Empty;

            var preview = CreatePreview(session, blacksmith, request);
            if (!preview.CanCraft)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryGetRecipient(session, request.RecipientCharacterId, out var recipient)
                || !TrySelectConsumedItems(session, preview.Materials, out var consumed))
            {
                failureReason = "Nie udało się przygotować materiałów do craftingu.";
                return false;
            }

            var created = new List<ItemInstance>(request.Recipe.OutputAmount);
            for (var index = 0; index < request.Recipe.OutputAmount; index++)
            {
                created.Add(itemFactory.Create(request.Recipe));
            }

            foreach (var item in consumed) item.Inventory.Items.Remove(item.Item);

            foreach (var item in created)
            {
                if (inventoryService.TryAddItem(recipient.Inventory, item)) continue;

                foreach (var added in created)
                {
                    recipient.Inventory.Items.Remove(added);
                }
                foreach (var itemToRestore in consumed)
                {
                    itemToRestore.Inventory.Items.Add(itemToRestore.Item);
                }

                failureReason = "Nie udało się przekazać wyniku do inventory postaci.";
                return false;
            }

            craftedItems = created;
            return true;
        }

        public bool TryBuyMissingMaterial(
            GameSession session,
            NpcInstance blacksmith,
            MaterialCraftRequest request,
            string materialId,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            failureReason = string.Empty;

            if (!IsValidSpecialist(blacksmith)
                || blacksmith.Definition.GetRole<TraderRoleData>() == null
                || blacksmith.ShopInventory?.Items == null
                || string.IsNullOrWhiteSpace(materialId)
                || !IsValidRecipe(blacksmith, request?.Recipe)
                || !TryGetRecipient(session, request.RecipientCharacterId, out var recipient))
            {
                failureReason = "Nie można kupić tego materiału u wybranego specjalisty.";
                return false;
            }

            var cost = BuildMaterialCosts(session, blacksmith, request.Recipe)
                .FirstOrDefault(candidate => candidate.Material.Id == materialId);
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
                failureReason = "Kowal nie ma wystarczającego zapasu tego materiału.";
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

        private static IMaterialCraftingSpecialist GetCraftingSpecialist(
            NpcInstance npc)
        {
            return npc?.Definition?.Roles?
                .OfType<IMaterialCraftingSpecialist>()
                .FirstOrDefault();
        }

        private static bool IsValidSpecialist(NpcInstance npc) =>
            GetCraftingSpecialist(npc) != null;

        private static bool IsValidRecipe(
            NpcInstance specialist,
            CraftableItemDefinition recipe)
        {
            var craftingRole = GetCraftingSpecialist(specialist);
            if (recipe == null
                || craftingRole == null
                || recipe.CraftingDiscipline != craftingRole.CraftingDiscipline
                || recipe.OutputAmount < 1
                || recipe.Ingredients == null
                || recipe.Ingredients.Count == 0)
            {
                return false;
            }

            if (!(craftingRole.MaterialRecipes?.Any(candidate =>
                    candidate != null
                    && string.Equals(candidate.Id, recipe.Id, StringComparison.Ordinal))
                    ?? false))
            {
                return false;
            }

            return recipe.Ingredients.All(requirement =>
                requirement?.Material != null
                && requirement.Material.ItemType == ItemType.Material
                && GetMaterialDiscipline(requirement.Material)
                    == craftingRole.CraftingDiscipline
                && requirement.Amount > 0);
        }

        private static string GetRecipeFailureReason(
            NpcInstance specialist,
            CraftableItemDefinition recipe)
        {
            var craftingRole = GetCraftingSpecialist(specialist);
            if (craftingRole == null) return "Wybrany NPC nie oferuje material craftingu.";
            if (!(craftingRole.MaterialRecipes?.Any(candidate =>
                    candidate != null
                    && string.Equals(candidate.Id, recipe?.Id, StringComparison.Ordinal))
                    ?? false))
            {
                return "Ta recepta nie jest przypisana do oferty wybranego specjalisty.";
            }
            if (recipe == null)
            {
                return "Wybierz poprawną receptę.";
            }
            if (recipe.CraftingDiscipline != craftingRole.CraftingDiscipline)
            {
                return $"Recepta musi mieć Crafting Discipline = {craftingRole.CraftingDiscipline}.";
            }
            if (recipe.OutputAmount < 1)
            {
                return "CID musi mieć Output Amount większe od zera.";
            }
            if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                return "CID musi mieć co najmniej jeden składnik.";
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient?.Material == null)
                    return "Każdy składnik CID musi wskazywać Item Definition.";
                if (ingredient.Material.ItemType != ItemType.Material)
                    return "Składnik CID musi mieć Item Type = Material.";
                if (GetMaterialDiscipline(ingredient.Material)
                    != craftingRole.CraftingDiscipline)
                {
                    return $"Każdy składnik CID musi mieć kategorię {craftingRole.CraftingDiscipline}.";
                }
                if (ingredient.Amount < 1)
                    return "Ilość każdego składnika CID musi być większa od zera.";
            }

                return "Recepta specjalisty jest niepoprawna.";
        }

        private static MaterialDiscipline GetMaterialDiscipline(ItemDefinition item)
        {
            return item is CraftableItemDefinition craftable
                ? craftable.CraftingDiscipline
                : item?.MaterialDiscipline ?? MaterialDiscipline.None;
        }

        private List<CraftingMaterialCost> BuildMaterialCosts(
            GameSession session,
            NpcInstance blacksmith,
            CraftableItemDefinition recipe)
        {
            var requirements = new Dictionary<ItemDefinition, int>();
            foreach (var ingredient in recipe.Ingredients)
            {
                requirements.TryGetValue(ingredient.Material, out var amount);
                requirements[ingredient.Material] = amount + ingredient.Amount;
            }

            var costs = new List<CraftingMaterialCost>();
            foreach (var pair in requirements.OrderBy(entry => entry.Key.DisplayName))
            {
                var owned = CountPartyItems(session, pair.Key.Id);
                var missing = Mathf.Max(0, pair.Value - owned);
                var stock = GetBlacksmithStock(blacksmith, pair.Key.Id);
                var price = GetStockPurchasePrice(blacksmith, pair.Key.Id, Mathf.Min(missing, stock));
                var purchasable = Mathf.Min(missing, stock);

                costs.Add(new CraftingMaterialCost
                {
                    Material = pair.Key,
                    RequiredAmount = pair.Value,
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
                });
                var cost = costs[costs.Count - 1];
            }

            return costs;
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

        private static int GetBlacksmithStock(NpcInstance blacksmith, string definitionId) =>
            CraftingMerchantStock.Items(blacksmith).Count(item => item?.Definition?.Id == definitionId);

        private int GetStockPurchasePrice(NpcInstance blacksmith, string definitionId, int amount) =>
            amount <= 0 ? 0 : CraftingMerchantStock.Items(blacksmith)
                .Where(item => item?.Definition?.Id == definitionId)
                .Take(amount)
                .Sum(item => CraftingMerchantStock.Price(blacksmith, item));

        private static bool CanReceiveAll(Inventory inventory, ItemDefinition definition, int amount)
        {
            if (inventory?.Items == null || definition == null || amount < 1) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var weight = inventory.Items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f));
            return weight + Mathf.Max(0f, definition.Weight) * amount <= inventory.MaxWeight.Value;
        }

        private static bool CanReceiveAll(Inventory inventory, IEnumerable<ItemInstance> items)
        {
            if (inventory?.Items == null || items == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var weight = inventory.Items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f));
            return weight + items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f)) <= inventory.MaxWeight.Value;
        }

        private static bool TrySelectConsumedItems(GameSession session, IReadOnlyList<CraftingMaterialCost> costs, out List<PartyInventoryItem> items)
        {
            items = new List<PartyInventoryItem>();
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
                        items.Add(new PartyInventoryItem(character.Inventory, item));
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                if (remaining > 0) return false;
            }
            return true;
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
