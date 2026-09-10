using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class JewelryCraftRequest
    {
        public JewelryItemDefinition Definition;
        public JewelryMetalData Metal;
        public JewelryGemDefinition Gem;
        public string CustomName = string.Empty;
        public string RecipientCharacterId = string.Empty;
    }

    public sealed class JewelryCraftPreview
    {
        public JewelryItemInstance Result;
        public IReadOnlyList<CraftingMaterialCost> Materials = Array.Empty<CraftingMaterialCost>();
        public bool CanCraft;
        public string FailureReason = string.Empty;
    }

    /// <summary>
    /// Owns deterministic Enchanter jewelry crafting. The chosen metal is an
    /// arbitrary material recipe; its material rarities determine level and
    /// rarity. The chosen gem is a separate, single recipe input.
    /// </summary>
    public sealed class JewelryCraftingService
    {
        private readonly JewelryGenerationService generation;
        private readonly InventoryService inventory;
        private readonly TradeService trade;
        private readonly NpcLoyaltyService loyalty = new();

        public JewelryCraftingService(
            JewelryGenerationService generation = null,
            InventoryService inventory = null,
            TradeService trade = null)
        {
            this.generation = generation ?? new JewelryGenerationService();
            this.inventory = inventory ?? new InventoryService();
            this.trade = trade ?? new TradeService();
        }

        public JewelryCraftPreview CreatePreview(
            GameSession session,
            NpcInstance enchanter,
            JewelryCraftRequest request)
        {
            var preview = new JewelryCraftPreview();
            if (!TryValidate(session, enchanter, request, out var recipient, out var failure))
            {
                preview.FailureReason = failure;
                return preview;
            }

            preview.Result = generation.GenerateSelected(
                request.Definition, request.Metal, request.Gem);
            if (preview.Result == null)
            {
                preview.FailureReason = "Nie udało się przygotować podglądu biżuterii.";
                return preview;
            }

            preview.Result.CustomName = NormalizeName(request.CustomName);
            if (!TryBuildCosts(session, enchanter, request, out var costs, out failure))
            {
                preview.FailureReason = failure;
                return preview;
            }

            preview.Materials = costs;
            if (costs.Any(cost => cost.MissingAmount > 0))
            {
                preview.FailureReason = "Brakuje materiałów. Kup brakujące pozycje z oferty Zaklinacza.";
                return preview;
            }

            if (!CanReceive(recipient.Inventory, preview.Result))
            {
                preview.FailureReason = "Postać nie ma wystarczającego udźwigu na gotową biżuterię.";
                return preview;
            }

            preview.CanCraft = true;
            return preview;
        }

        public bool TryCraft(
            GameSession session,
            NpcInstance enchanter,
            JewelryCraftRequest request,
            out JewelryItemInstance crafted,
            out string failure)
        {
            crafted = null;
            failure = string.Empty;
            var preview = CreatePreview(session, enchanter, request);
            if (!preview.CanCraft)
            {
                failure = preview.FailureReason;
                return false;
            }

            if (!TryGetRecipient(session, request.RecipientCharacterId, out var recipient)
                || !TrySelectConsumedItems(session, preview.Materials, out var consumed))
            {
                failure = "Nie udało się przygotować materiałów do craftingu.";
                return false;
            }

            var result = generation.GenerateSelected(request.Definition, request.Metal, request.Gem);
            if (result == null)
            {
                failure = "Nie udało się wytworzyć biżuterii.";
                return false;
            }

            result.CustomName = NormalizeName(request.CustomName);
            foreach (var item in consumed) item.Inventory.Items.Remove(item.Item);
            if (!inventory.TryAddItem(recipient.Inventory, result))
            {
                foreach (var item in consumed) item.Inventory.Items.Add(item.Item);
                failure = "Nie udało się przekazać biżuterii do inventory postaci.";
                return false;
            }

            crafted = result;
            return true;
        }

        public bool TryBuyMissingMaterial(
            GameSession session,
            NpcInstance enchanter,
            JewelryCraftRequest request,
            string materialId,
            out int paidGold,
            out string failure)
        {
            paidGold = 0;
            failure = string.Empty;
            if (!TryValidate(session, enchanter, request, out var recipient, out failure)
                || enchanter.Definition.GetRole<TraderRoleData>() == null
                || !TryBuildCosts(session, enchanter, request, out var costs, out failure))
            {
                return false;
            }

            var cost = costs.FirstOrDefault(value => value.Material?.Id == materialId);
            if (cost == null || cost.MissingAmount < 1)
            {
                failure = "Ten materiał nie jest obecnie potrzebny.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(cost.PurchaseFailureReason))
            {
                failure = cost.PurchaseFailureReason;
                return false;
            }

            var stock = GetMaterialPurchaseStock(enchanter, materialId)
                .Take(cost.PurchasableAmount)
                .ToList();
            var stockItems = stock.Select(entry => entry.Item).ToList();
            paidGold = stock.Sum(entry => entry.Price);
            if (stock.Count != cost.PurchasableAmount || paidGold <= 0)
            {
                failure = "Zaklinacz nie ma wystarczającego zapasu tego materiału.";
                paidGold = 0;
                return false;
            }

            if (session.Gold < paidGold || !CanReceiveAll(recipient.Inventory, stockItems))
            {
                failure = session.Gold < paidGold
                    ? "Brakuje złota na ten materiał."
                    : "Postać nie ma wystarczającego udźwigu na materiał.";
                paidGold = 0;
                return false;
            }

            var removed = new List<MaterialPurchase>();
            foreach (var purchase in stock)
            {
                if (RemoveFromTrader(enchanter, purchase))
                {
                    removed.Add(purchase);
                    continue;
                }

                foreach (var restored in removed) RestoreToTrader(enchanter, restored);
                failure = "Oferta Zaklinacza zmieniła się. Odśwież okno i spróbuj ponownie.";
                paidGold = 0;
                return false;
            }

            if (!session.TrySpendGold(paidGold))
            {
                foreach (var purchase in removed) RestoreToTrader(enchanter, purchase);
                failure = "Brakuje złota na ten materiał.";
                paidGold = 0;
                return false;
            }

            foreach (var item in stockItems)
            {
                if (inventory.TryAddItem(recipient.Inventory, item)) continue;
                foreach (var transferred in stockItems)
                {
                    recipient.Inventory.Items.Remove(transferred);
                }
                foreach (var purchase in removed) RestoreToTrader(enchanter, purchase);
                session.TryAddGold(paidGold);
                failure = "Nie udało się przekazać materiału do inventory postaci.";
                paidGold = 0;
                return false;
            }

            loyalty.TryRecordGoldSpent(enchanter, paidGold);
            return true;
        }

        private bool TryBuildCosts(
            GameSession session,
            NpcInstance enchanter,
            JewelryCraftRequest request,
            out List<CraftingMaterialCost> costs,
            out string failure)
        {
            costs = new List<CraftingMaterialCost>();
            failure = string.Empty;
            var required = new Dictionary<ItemDefinition, int>();
            if (!AddRequirement(required, new CraftingMaterialRequirement(request.Metal.Material, request.Metal.Amount)))
            {
                failure = "Wybrany metal ma niepoprawnie skonfigurowany materiał.";
                return false;
            }

            if (!AddRequirement(required, new CraftingMaterialRequirement(
                    request.Gem.CraftingMaterial, request.Gem.Amount)))
            {
                failure = "Wybrany klejnot nie ma skonfigurowanego materiału.";
                return false;
            }

            foreach (var pair in required.OrderBy(value => value.Key.DisplayName))
            {
                var owned = CountPartyItems(session, pair.Key.Id);
                var missing = Mathf.Max(0, pair.Value - owned);
                var purchaseStock = GetMaterialPurchaseStock(enchanter, pair.Key.Id);
                var stock = purchaseStock.Count;
                var purchasable = Mathf.Min(missing, stock);
                var price = purchaseStock.Take(purchasable).Sum(entry => entry.Price);
                var cost = new CraftingMaterialCost
                {
                    Material = pair.Key,
                    RequiredAmount = pair.Value,
                    OwnedAmount = owned,
                    AvailableFromBlacksmith = stock,
                    PurchasePrice = price
                };
                cost.CanAffordPurchase = purchasable > 0 && price > 0 && session?.Gold >= price;
                cost.PurchaseState = missing <= 0 ? CraftingMaterialPurchaseState.NotNeeded
                    : purchasable == 0 || price <= 0 ? CraftingMaterialPurchaseState.OutOfStock
                    : cost.CanAffordPurchase ? CraftingMaterialPurchaseState.Available
                    : CraftingMaterialPurchaseState.InsufficientGold;
                costs.Add(cost);
            }
            return true;
        }

        private List<MaterialPurchase> GetMaterialPurchaseStock(
            NpcInstance enchanter,
            string materialId)
        {
            var stock = new List<MaterialPurchase>();
            foreach (var item in enchanter?.ShopInventory?.Items ?? new List<ItemInstance>())
            {
                if (item?.Definition?.Id != materialId) continue;
                stock.Add(new MaterialPurchase(
                    item,
                    null,
                    trade.GetPriceToBuyFromTrader(enchanter, item)));
            }

            foreach (var buyback in enchanter?.ShopInventory?.BuybackStock
                ?? new List<BuybackEntry>())
            {
                if (buyback?.Item?.Definition?.Id != materialId) continue;
                stock.Add(new MaterialPurchase(
                    buyback.Item,
                    buyback,
                    trade.GetPriceFromBuyback(enchanter, buyback)));
            }

            return stock;
        }

        private static bool RemoveFromTrader(NpcInstance enchanter, MaterialPurchase purchase)
        {
            if (purchase.Buyback != null)
                return enchanter?.ShopInventory?.BuybackStock?.Remove(purchase.Buyback) == true;

            return enchanter?.ShopInventory?.Items?.Remove(purchase.Item) == true;
        }

        private static void RestoreToTrader(NpcInstance enchanter, MaterialPurchase purchase)
        {
            if (purchase.Buyback != null)
            {
                if (enchanter?.ShopInventory?.BuybackStock != null
                    && !enchanter.ShopInventory.BuybackStock.Contains(purchase.Buyback))
                {
                    enchanter.ShopInventory.BuybackStock.Add(purchase.Buyback);
                }

                return;
            }

            if (enchanter?.ShopInventory?.Items != null
                && !enchanter.ShopInventory.Items.Contains(purchase.Item))
            {
                enchanter.ShopInventory.Items.Add(purchase.Item);
            }
        }

        private static bool AddRequirement(
            IDictionary<ItemDefinition, int> required,
            CraftingMaterialRequirement requirement)
        {
            if (requirement?.Material == null || requirement.Amount < 1) return false;
            required.TryGetValue(requirement.Material, out var count);
            required[requirement.Material] = count + requirement.Amount;
            return true;
        }

        private static bool TryValidate(
            GameSession session,
            NpcInstance enchanter,
            JewelryCraftRequest request,
            out CharacterInstance recipient,
            out string failure)
        {
            recipient = null;
            failure = string.Empty;
            var role = enchanter?.Definition?.GetRole<EnchanterRoleData>();
            if (role == null)
            {
                failure = "Wybrany NPC nie oferuje usług Zaklinacza.";
                return false;
            }
            if (!TryGetRecipient(session, request?.RecipientCharacterId, out recipient))
            {
                failure = "Wybierz aktywną postać, która odbierze biżuterię.";
                return false;
            }
            if (request.Definition == null || !role.JewelryOffers.Contains(request.Definition))
            {
                failure = "Wybierz typ biżuterii z oferty Zaklinacza.";
                return false;
            }
            if (request.Metal == null || !request.Definition.Metals.Contains(request.Metal))
            {
                failure = "Wybierz metal dla biżuterii.";
                return false;
            }
            if (request.Gem == null || !request.Definition.Gems.Contains(request.Gem))
            {
                failure = "Wybierz jeden klejnot dla biżuterii.";
                return false;
            }
            return true;
        }

        private static bool TryGetRecipient(GameSession session, string id, out CharacterInstance recipient)
        {
            recipient = null;
            return session != null && !string.IsNullOrWhiteSpace(id)
                && session.PlayerParty.IsActiveMember(id)
                && session.CharactersById.TryGetValue(id, out recipient)
                && recipient.Inventory?.Items != null;
        }

        private static int CountPartyItems(GameSession session, string id) => session?.PlayerParty.ActiveMemberIds
            .Where(characterId => session.CharactersById.TryGetValue(characterId, out _))
            .Sum(characterId => session.CharactersById[characterId].Inventory?.Items?.Count(item =>
                item?.Definition?.Id == id) ?? 0) ?? 0;

        private static bool TrySelectConsumedItems(GameSession session, IReadOnlyList<CraftingMaterialCost> costs, out List<PartyItem> selected)
        {
            selected = new List<PartyItem>();
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
                        selected.Add(new PartyItem(character.Inventory, item));
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private static bool CanReceive(Inventory inventory, ItemInstance item) => CanReceiveAll(inventory, new[] { item });
        private static bool CanReceiveAll(Inventory inventory, IEnumerable<ItemInstance> items)
        {
            if (inventory?.Items == null || items == null) return false;
            if (!inventory.MaxWeight.HasValue) return true;
            var current = inventory.Items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f));
            var incoming = items.Sum(item => Mathf.Max(0f, item?.Definition?.Weight ?? 0f));
            return current + incoming <= inventory.MaxWeight.Value;
        }

        private static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private readonly struct PartyItem { public PartyItem(Inventory inventory, ItemInstance item) { Inventory = inventory; Item = item; } public Inventory Inventory { get; } public ItemInstance Item { get; } }
        private readonly struct MaterialPurchase
        {
            public MaterialPurchase(ItemInstance item, BuybackEntry buyback, int price)
            {
                Item = item;
                Buyback = buyback;
                Price = price;
            }

            public ItemInstance Item { get; }
            public BuybackEntry Buyback { get; }
            public int Price { get; }
        }
    }
}
