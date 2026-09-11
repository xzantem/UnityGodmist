using System;
using Godmist.Data.Items;
using Godmist.Data.Configuration;
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
        private readonly NpcLoyaltyService npcLoyaltyService = new();
        private readonly NpcEconomyService npcEconomyService;

        public TradeService(NpcEconomyService npcEconomyService = null)
        {
            this.npcEconomyService = npcEconomyService ?? new NpcEconomyService();
        }

        public int GetPriceToBuyFromTrader(ItemInstance item)
        {
            if (item?.Definition == null) return 0;

            return GetBaseValue(item);
        }

        public int GetPriceToBuyFromTrader(
            NpcInstance trader,
            ItemInstance item)
        {
            if (!CanBuyItem(trader, item, out _)) return 0;

            return npcLoyaltyService.GetDiscountedGoldCost(
                trader,
                GetPriceToBuyFromTrader(item));
        }

        public int GetPriceFromBuyback(BuybackEntry entry)
        {
            if (entry?.Item?.Definition == null) return 0;

            return GetPriceToBuyFromTrader(entry.Item);
        }

        public int GetPriceFromBuyback(
            NpcInstance trader,
            BuybackEntry entry)
        {
            if (entry?.Item == null || !CanBuyItem(trader, entry.Item, out _)) return 0;

            return npcLoyaltyService.GetDiscountedGoldCost(
                trader,
                GetPriceToBuyFromTrader(entry.Item));
        }

        public bool TryGetSellQuote(
            NpcInstance trader,
            ItemInstance item,
            out int normalPrice,
            out int payableGold,
            out string failureReason)
        {
            normalPrice = 0;
            payableGold = 0;

            if (!CanSellItem(trader, item, out failureReason))
            {
                return false;
            }

            normalPrice = GetPriceWhenSellingToTrader(trader, item);
            if (normalPrice <= 0)
            {
                failureReason = "Nie można ustalić poprawnej ceny sprzedaży.";
                return false;
            }

            payableGold = (int)Math.Min(
                trader.GoldBalance,
                normalPrice);

            failureReason = string.Empty;
            return true;
        }

        public bool CanBuyItem(
            NpcInstance trader,
            ItemInstance item,
            out string failureReason)
        {
            return CanTradeItem(trader, item, out failureReason);
        }

        public bool CanSellItem(
            NpcInstance trader,
            ItemInstance item,
            out string failureReason)
        {
            return CanTradeItem(trader, item, out failureReason);
        }

        private bool CanTradeItem(
            NpcInstance trader,
            ItemInstance item,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!CanTrade(trader) || item?.Definition == null)
            {
                failureReason = "Ten NPC nie oferuje poprawnej usługi handlu.";
                return false;
            }

            return true;
        }

        public int GetPriceWhenSellingToTrader(
            NpcInstance trader,
            ItemInstance item)
        {
            if (!CanSellItem(trader, item, out _)) return 0;

            var multiplier = IsRelevantForTrader(trader, item)
                ? GameDefinesRuntime.GetFloat(GameDefineIds.TraderRelevantSellMultiplier, .5f)
                : GameDefinesRuntime.GetFloat(GameDefineIds.TraderIrrelevantSellMultiplier, .25f);

            return Mathf.FloorToInt(
                GetBaseValue(item) * multiplier);
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
            var price = GetPriceToBuyFromTrader(trader, item);

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

            npcLoyaltyService.TryRecordGoldSpent(trader, price);
            npcEconomyService.TryReceiveGold(trader, price);
            paidGold = price;
            return true;
        }

        private static int GetBaseValue(ItemInstance item)
        {
            var runtimeValue = Mathf.Max(0, item?.BaseValue ?? 0);
            if (runtimeValue > 0)
            {
                return runtimeValue;
            }

            return item?.Definition is CraftableItemDefinition craftable
                ? craftable.CalculatedBaseValue
                : runtimeValue;
        }

        public bool TryBuyFromBuyback(
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

            var entry = FindBuybackEntry(trader.ShopInventory, itemInstanceId);
            var price = GetPriceFromBuyback(trader, entry);

            if (entry == null || price <= 0 || session.Gold < price) return false;

            trader.ShopInventory.BuybackStock.Remove(entry);

            if (!inventoryService.TryAddItem(buyer.Inventory, entry.Item))
            {
                trader.ShopInventory.BuybackStock.Add(entry);
                return false;
            }

            if (!session.TrySpendGold(price))
            {
                inventoryService.TryRemoveItem(
                    buyer.Inventory,
                    entry.Item.InstanceId,
                    out _);

                trader.ShopInventory.BuybackStock.Add(entry);
                return false;
            }

            npcEconomyService.TryReceiveGold(trader, price);
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
            return TrySellToTrader(
                session,
                trader,
                seller,
                itemInstanceId,
                acceptPartialPayment: false,
                out receivedGold);
        }

        public bool TrySellToTrader(
            GameSession session,
            NpcInstance trader,
            CharacterInstance seller,
            string itemInstanceId,
            bool acceptPartialPayment,
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

            if (price <= 0
                || !acceptPartialPayment && trader.GoldBalance < price)
            {
                return false;
            }

            var payableGold = (int)Math.Min(trader.GoldBalance, price);

            if (!inventoryService.TryRemoveItem(
                    seller.Inventory,
                    itemInstanceId,
                    out var removedItem))
            {
                return false;
            }

            if (!TryAddToBuyback(
                    trader.ShopInventory,
                    removedItem,
                    payableGold))
            {
                inventoryService.TryAddItem(seller.Inventory, removedItem);
                return false;
            }

            var npcPaid = payableGold == 0
                || npcEconomyService.TrySpendGold(trader, payableGold);
            var playerReceived = payableGold == 0
                || session.TryAddGold(payableGold);
            if (!npcPaid || !playerReceived)
            {
                RemoveFromBuyback(trader.ShopInventory, removedItem.InstanceId);
                inventoryService.TryAddItem(seller.Inventory, removedItem);

                if (npcPaid && payableGold > 0)
                {
                    npcEconomyService.TryReceiveGold(trader, payableGold);
                }

                return false;
            }

            receivedGold = payableGold;
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

        private static BuybackEntry FindBuybackEntry(
            ShopInventory shopInventory,
            string itemInstanceId)
        {
            if (shopInventory?.BuybackStock == null) return null;

            foreach (var entry in shopInventory.BuybackStock)
            {
                if (entry?.Item?.InstanceId == itemInstanceId) return entry;
            }

            return null;
        }

        private static bool TryAddToBuyback(
            ShopInventory shopInventory,
            ItemInstance item,
            int soldPrice)
        {
            if (shopInventory?.BuybackStock == null
                || item == null
                || soldPrice < 0)
            {
                return false;
            }

            if (FindBuybackEntry(shopInventory, item.InstanceId) != null)
            {
                return false;
            }

            shopInventory.BuybackStock.Add(
                new BuybackEntry(item, soldPrice));

            return true;
        }

        private static void RemoveFromBuyback(
            ShopInventory shopInventory,
            string itemInstanceId)
        {
            if (shopInventory?.BuybackStock == null) return;

            shopInventory.BuybackStock.RemoveAll(
                entry => entry?.Item?.InstanceId == itemInstanceId);
        }
    }
}
