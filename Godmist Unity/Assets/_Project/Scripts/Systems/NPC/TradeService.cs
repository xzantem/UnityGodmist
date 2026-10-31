using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool TryGetBuyPrice(ItemInstance item, out int price, out string reason)
        {
            price = 0;
            reason = "Brak definicji przedmiotu.";
            if (item?.Definition == null) return false;
            if (!CraftingValueCalculator.TryGetValue(item.Definition, out var definitionValue, out var failure))
            { reason = failure.Message; return false; }
            price = IsFreeStarter(item) ? 0 : item.BaseValue > 0 ? item.BaseValue : definitionValue;
            reason = string.Empty;
            return true;
        }

        public bool TryGetBuyPrice(NpcInstance trader, ItemInstance item, out int price, out string reason)
        {
            price = 0;
            reason = "Ten NPC nie oferuje poprawnej usługi handlu.";
            if (!CanTrade(trader) || !TryGetBuyPrice(item, out var basis, out reason)) return false;
            price = npcLoyaltyService.GetDiscountedGoldCost(trader, basis);
            return true;
        }

        public bool TryGetBuybackPrice(NpcInstance trader, BuybackEntry entry, out int price, out string reason)
            => TryGetBuyPrice(trader, entry?.Item, out price, out reason);

        public bool TryGetSellPrice(NpcInstance trader, ItemInstance item, out int price, out string reason)
        {
            price = 0;
            reason = "Ten NPC nie oferuje poprawnej usługi handlu.";
            if (!CanTrade(trader) || !TryGetBuyPrice(item, out var basis, out reason)) return false;
            if (item.IsSaleProtected)
            { reason = "Przedmiot jest zablokowany przed sprzedażą. Odblokuj go w menu PPM."; return false; }
            var multiplier = IsRelevantForTrader(trader, item)
                ? GameDefinesRuntime.GetFloat(GameDefineIds.TraderRelevantSellMultiplier, .5f)
                : GameDefinesRuntime.GetFloat(GameDefineIds.TraderIrrelevantSellMultiplier, .25f);
            price = Mathf.FloorToInt(basis * multiplier);
            return true;
        }

        // Strict convenience accessors for callers requiring a valid quote. Failure is never a price.
        public int GetPriceToBuyFromTrader(ItemInstance item)
            => TryGetBuyPrice(item, out var price, out var reason) ? price : throw new InvalidOperationException(reason);
        public int GetPriceToBuyFromTrader(NpcInstance trader, ItemInstance item)
            => TryGetBuyPrice(trader, item, out var price, out var reason) ? price : throw new InvalidOperationException(reason);
        public int GetPriceFromBuyback(BuybackEntry entry) => GetPriceToBuyFromTrader(entry?.Item);
        public int GetPriceFromBuyback(NpcInstance trader, BuybackEntry entry)
            => TryGetBuybackPrice(trader, entry, out var price, out var reason) ? price : throw new InvalidOperationException(reason);
        public int GetPriceWhenSellingToTrader(NpcInstance trader, ItemInstance item)
            => TryGetSellPrice(trader, item, out var price, out var reason) ? price : throw new InvalidOperationException(reason);

        public bool TryGetSellQuote(
            NpcInstance trader,
            ItemInstance item,
            out int normalPrice,
            out int payableGold,
            out string failureReason)
        {
            normalPrice = 0;
            payableGold = 0;

            if (!TryGetSellPrice(trader, item, out normalPrice, out failureReason))
            {
                return false;
            }

            if (normalPrice <= 0 && !IsFreeStarter(item))
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

        public bool CanBuyItem(NpcInstance trader, ItemInstance item, out string failureReason)
            => TryGetBuyPrice(trader, item, out _, out failureReason);

        public bool CanSellItem(NpcInstance trader, ItemInstance item, out string failureReason)
            => TryGetSellPrice(trader, item, out _, out failureReason);

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
            if (!TryGetBuyPrice(trader, item, out var price, out _)) return false;

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

        public static bool IsFreeStarter(ItemInstance item) =>
            item?.Definition is EquipmentItemDefinition { IsStarterEquipment: true };

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
            if (!TryGetBuybackPrice(trader, entry, out var price, out _)) return false;

            if (entry == null || price <= 0 && !IsFreeStarter(entry.Item) || session.Gold < price) return false;

            trader.ShopInventory.BuybackStock.Remove(entry);

            if (!inventoryService.TryAddItem(buyer.Inventory, entry.Item))
            {
                trader.ShopInventory.BuybackStock.Add(entry);
                return false;
            }

            if (price > 0 && !session.TrySpendGold(price))
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
            if (!TryGetSellPrice(trader, item, out var price, out _)) return false;

            if (price <= 0 && !IsFreeStarter(item)
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

        public bool TryGetSellBatchQuote(NpcInstance trader, CharacterInstance seller,
            IReadOnlyList<string> ids, out int normalPrice, out int payableGold, out string reason)
        {
            normalPrice = payableGold = 0;
            reason = "Wybrane przedmioty nie są już dostępne.";
            if (!TryResolveItems(seller?.Inventory, ids, out var items)) return false;
            long total = 0;
            foreach (var item in items)
            {
                if (!TryGetSellQuote(trader, item, out var price, out _, out reason)) return false;
                total += price;
            }
            if (total > int.MaxValue) { reason = "Zbyt duża wartość transakcji."; return false; }
            normalPrice = (int)total;
            payableGold = (int)Math.Min(trader.GoldBalance, total);
            reason = string.Empty;
            return true;
        }

        public bool TrySellBatch(GameSession session, NpcInstance trader, CharacterInstance seller,
            IReadOnlyList<string> ids, bool acceptPartialPayment, out int receivedGold, out string reason)
        {
            receivedGold = 0;
            if (!TryGetSellBatchQuote(trader, seller, ids, out var total, out var payable, out reason)) return false;
            if (trader.ShopInventory.BuybackStock == null)
            { reason = "Odkup kupca nie jest dostępny."; return false; }
            if (session == null || session.IsCharacterInCombat(seller.CharacterId))
            { reason = "Nie można handlować podczas walki."; return false; }
            if (!acceptPartialPayment && payable < total)
            { reason = "Kupiec nie ma wystarczającej ilości złota."; return false; }
            if ((long)session.Gold + payable > int.MaxValue)
            { reason = "Nie można przyjąć więcej złota."; return false; }
            TryResolveItems(seller.Inventory, ids, out var items);
            if (items.Any(item => FindBuybackEntry(trader.ShopInventory, item.InstanceId) != null
                || FindItem(trader.ShopInventory, item.InstanceId) != null))
            { reason = "Ten egzemplarz jest już w ofercie kupca."; return false; }
            var prices = new Dictionary<ItemInstance, int>();
            foreach (var item in items)
            {
                if (!TryGetSellPrice(trader, item, out var price, out reason)) return false;
                prices.Add(item, price);
            }
            // All mutations below are synchronous and have been fully validated.
            if (payable > 0 && !npcEconomyService.TrySpendGold(trader, payable)) return false;
            if (payable > 0) session.TryAddGold(payable);
            var remaining = payable;
            foreach (var item in items)
            {
                var paid = Math.Min(remaining, prices[item]);
                remaining -= paid;
                seller.Inventory.Items.Remove(item);
                trader.ShopInventory.BuybackStock.Add(new BuybackEntry(item, paid));
            }
            receivedGold = payable;
            reason = string.Empty;
            return true;
        }

        public bool TryBuyBatch(GameSession session, NpcInstance trader, CharacterInstance buyer,
            IReadOnlyList<string> ids, bool buyback, out int paidGold, out string reason)
        {
            paidGold = 0;
            reason = "Wybrane przedmioty nie są już dostępne.";
            if (!CanTrade(trader) || session == null || buyer?.Inventory == null
                || ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count) return false;
            if (session.IsCharacterInCombat(buyer.CharacterId))
            { reason = "Nie można handlować podczas walki."; return false; }
            var items = ids.Select(id => buyback ? FindBuybackEntry(trader.ShopInventory, id)?.Item
                : FindItem(trader.ShopInventory, id)).ToList();
            long total = 0;
            foreach (var item in items)
            {
                if (!TryGetBuyPrice(trader, item, out var price, out reason)) return false;
                if (price <= 0 && !(buyback && IsFreeStarter(item)))
                { reason = "Brak poprawnej ceny zakupu."; return false; }
                total += price;
                if (FindItem(buyer.Inventory, item.InstanceId) != null)
                { reason = "Ten egzemplarz jest już w plecaku."; return false; }
            }
            if (total > int.MaxValue || total > session.Gold)
            { reason = "Brakuje złota na ten zakup."; return false; }
            var weight = items.Sum(item => Mathf.Max(0, item.Definition.Weight));
            if (buyer.Inventory.MaxWeight.HasValue
                && inventoryService.GetCurrentWeight(buyer.Inventory) + weight > buyer.Inventory.MaxWeight.Value)
            { reason = "Brak miejsca w plecaku. Sprawdź limit wagi."; return false; }
            if (total > 0 && !session.TrySpendGold((int)total)) return false;
            foreach (var item in items)
            {
                if (buyback) RemoveFromBuyback(trader.ShopInventory, item.InstanceId);
                else trader.ShopInventory.Items.Remove(item);
            }
            buyer.Inventory.Items.AddRange(items);
            if (!buyback && total > 0) npcLoyaltyService.TryRecordGoldSpent(trader, (int)total);
            if (total > 0) npcEconomyService.TryReceiveGold(trader, total);
            paidGold = (int)total;
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveItems(Inventory inventory, IReadOnlyList<string> ids, out List<ItemInstance> items)
        {
            items = null;
            if (inventory?.Items == null || ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count) return false;
            items = ids.Select(id => FindItem(inventory, id)).ToList();
            return items.All(item => item?.Definition != null && !string.IsNullOrWhiteSpace(item.InstanceId));
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
