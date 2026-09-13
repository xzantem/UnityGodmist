using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;

namespace Godmist.Systems.Items
{
    internal static class CraftingMerchantStock
    {
        public static IEnumerable<ItemInstance> Items(NpcInstance npc) =>
            (npc?.ShopInventory?.Items ?? new List<ItemInstance>())
            .Concat((npc?.ShopInventory?.BuybackStock ?? new List<BuybackEntry>()).Select(entry => entry.Item))
            .Where(item => item != null).Distinct();

        public static int Price(NpcInstance npc, ItemInstance item)
        {
            var buyback = npc.ShopInventory.BuybackStock.FirstOrDefault(entry => ReferenceEquals(entry.Item, item));
            var trade = new TradeService();
            return buyback == null ? trade.GetPriceToBuyFromTrader(npc, item) : trade.GetPriceFromBuyback(npc, buyback);
        }

        public static Dictionary<ItemInstance, BuybackEntry> Remove(NpcInstance npc, IEnumerable<ItemInstance> items)
        {
            var origins = new Dictionary<ItemInstance, BuybackEntry>();
            foreach (var item in items)
            {
                var entry = npc.ShopInventory.BuybackStock.FirstOrDefault(candidate => ReferenceEquals(candidate.Item, item));
                origins.Add(item, entry);
                if (entry == null) npc.ShopInventory.Items.Remove(item);
                else npc.ShopInventory.BuybackStock.Remove(entry);
            }
            return origins;
        }

        public static void Restore(NpcInstance npc, Dictionary<ItemInstance, BuybackEntry> origins)
        {
            foreach (var pair in origins)
            {
                if (pair.Value != null) npc.ShopInventory.BuybackStock.Add(pair.Value);
                else npc.ShopInventory.Items.Add(pair.Key);
            }
        }
    }
}
