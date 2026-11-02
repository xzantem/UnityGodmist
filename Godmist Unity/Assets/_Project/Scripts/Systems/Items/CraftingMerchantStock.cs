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

        public static bool TryQuote(NpcInstance npc, IEnumerable<ItemInstance> items, out int total, out string reason)
        {
            total = 0; reason = string.Empty;
            var trade = new TradeService();
            long sum = 0;
            foreach (var item in items)
            {
                if (!trade.TryGetBuyPrice(npc, item, out var price, out reason)) return false;
                sum += price;
                if (sum > int.MaxValue) { reason = "Zbyt duża wartość transakcji."; return false; }
            }
            total = (int)sum;
            return true;
        }

        public static bool TryQuoteMaterial(NpcInstance npc, string definitionId, int amount, out int total, out string reason)
            => TryQuote(npc, Items(npc).Where(item => item.Definition?.Id == definitionId).Take(System.Math.Max(0, amount)), out total, out reason);

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
