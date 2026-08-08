using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.NPC
{
    public class TradeService
    {
        private readonly InventoryService inventoryService = new();

        public int GetPriceToBuyFromTrader(ItemInstance item)
        {
            if (item?.Definition == null) return 0;

            return Mathf.Max(0, item.Definition.BaseValue);
        }

        public int GetPriceWhenSellingToTrader(
            NpcInstance trader,
            ItemInstance item)
        {
            if (!CanTrade(trader) || item?.Definition == null) return 0;

            var multiplier = IsRelevantForTrader(trader, item) ? 0.5f : 0.25f;

            return Mathf.FloorToInt(
                Mathf.Max(0, item.Definition.BaseValue) * multiplier);
        }

        public bool TryBuyFromTrader(
            GameSession session,
            NpcInstance trader,
            CharacterInstance buyer,
            string itemInstanceId,
            out int paidGold)
        {
            paidGold = 0;

            if (!CanTrade(trader)
                || session == null
                || buyer?.Inventory == null
                || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            var item = FindItem(trader.ShopInventory, itemInstanceId);
            var price = GetPriceToBuyFromTrader(item);

            if (price <= 0 || session.Gold < price) return false;

            if (!inventoryService.TryRemoveItem(
                    trader.ShopInventory,
                    itemInstanceId,
                    out var removedItem))
            {
                return false;
            }

            if (!inventoryService.TryAddItem(buyer.Inventory, removedItem))
            {
                inventoryService.TryAddItem(trader.ShopInventory, removedItem);
                return false;
            }

            if (!session.TrySpendGold(price))
            {
                inventoryService.TryRemoveItem(
                    buyer.Inventory,
                    removedItem.InstanceId,
                    out _);

                inventoryService.TryAddItem(trader.ShopInventory, removedItem);
                return false;
            }

            trader.SpentGoldTotal += price;
            paidGold = price;
            return true;
        }

        public bool TrySellToTrader(
            GameSession session,
            NpcInstance trader,
            CharacterInstance seller,
            string itemInstanceId,
            out int receivedGold)
        {
            receivedGold = 0;

            if (!CanTrade(trader)
                || session == null
                || seller?.Inventory == null
                || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            var item = FindItem(seller.Inventory, itemInstanceId);
            var price = GetPriceWhenSellingToTrader(trader, item);

            if (price <= 0) return false;

            if (!inventoryService.TryRemoveItem(
                    seller.Inventory,
                    itemInstanceId,
                    out var removedItem))
            {
                return false;
            }

            if (!TryAddToBuyback(trader.ShopInventory, removedItem))
            {
                inventoryService.TryAddItem(seller.Inventory, removedItem);
                return false;
            }

            if (!session.TryAddGold(price))
            {
                RemoveFromBuyback(trader.ShopInventory, removedItem.InstanceId);
                inventoryService.TryAddItem(seller.Inventory, removedItem);
                return false;
            }

            receivedGold = price;
            return true;
        }

        private static bool CanTrade(NpcInstance trader)
        {
            return trader?.Definition?.GetRole<TraderRoleData>() != null
                && trader.ShopInventory != null;
        }

        private static bool IsRelevantForTrader(
            NpcInstance trader,
            ItemInstance item)
        {
            var traderRole = trader.Definition.GetRole<TraderRoleData>();

            if (traderRole.RelevantItemTypes == null
                || traderRole.RelevantItemTypes.Count == 0)
            {
                return true;
            }

            return traderRole.RelevantItemTypes.Contains(item.Definition.ItemType);
        }

        private static ItemInstance FindItem(
            Inventory inventory,
            string itemInstanceId)
        {
            if (inventory?.Items == null) return null;

            foreach (var item in inventory.Items)
            {
                if (item?.InstanceId == itemInstanceId) return item;
            }

            return null;
        }

        private static bool TryAddToBuyback(
            ShopInventory shopInventory,
            ItemInstance item)
        {
            if (shopInventory?.BuybackStock == null || item == null) return false;

            foreach (var candidate in shopInventory.BuybackStock)
            {
                if (candidate?.InstanceId == item.InstanceId) return false;
            }

            shopInventory.BuybackStock.Add(item);
            return true;
        }

        private static void RemoveFromBuyback(
            ShopInventory shopInventory,
            string itemInstanceId)
        {
            if (shopInventory?.BuybackStock == null) return;

            shopInventory.BuybackStock.RemoveAll(
                item => item?.InstanceId == itemInstanceId);
        }
    }
}