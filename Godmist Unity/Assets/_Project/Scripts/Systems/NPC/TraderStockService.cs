using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;

namespace Godmist.Systems.NPC
{
    public sealed class TraderStockService
    {
        private readonly Random random;
        private readonly ItemInstanceFactory factory = new();
        private readonly TraderCraftedStockService craftedStock;
        public TraderStockService(Random random = null)
        {
            this.random = random ?? new Random();
            craftedStock = new TraderCraftedStockService(this.random);
        }

        public bool TryRefreshAfterReturn(GameSession session, DungeonInstance dungeon,
            GameContentCatalog catalog = null)
        {
            if (session == null || dungeon == null || dungeon.TraderStockRefreshed
                || dungeon.GetCompletedFloorCount() < 1) return false;
            var level = 1;
            foreach (var character in session.CharactersById.Values)
                if (character != null)
                    level = Math.Max(level, character.Level);
            foreach (var npc in session.NpcsById.Values) Refresh(npc, level, catalog: catalog);
            dungeon.MarkTraderStockRefreshed();
            return true;
        }

        public void Refresh(NpcInstance npc, int level, bool rotate = true,
            GameContentCatalog catalog = null)
        {
            var role = npc?.Definition?.GetRole<TraderRoleData>();
            if (role == null || npc.ShopInventory == null) return;
            var local = (role.LocalStock ?? new List<TraderStockEntry>())
                .Where(e => e != null && e.IsAvailable(level))
                .GroupBy(e => e.Item.Id).Select(g => g.First()).ToList();
            var localIds = new HashSet<string>(local.Select(e => e.Item.Id));
            var groups = (role.RotatingStock ?? new List<TraderStockGroup>())
                .Where(g => g != null && g.Slots > 0).ToList();
            var previous = new HashSet<string>(npc.RotatingStockItemIds ?? new List<string>());
            // Infer legacy/current initial offers once, including profiles added to an existing save.
            if (previous.Count == 0)
                foreach (var entry in groups.SelectMany(g => g.Candidates ?? new List<TraderStockEntry>()))
                    if (entry?.Item != null && npc.ShopInventory.Items.Any(i => i?.Definition?.Id == entry.Item.Id))
                        previous.Add(entry.Item.Id);
            var selected = new List<(TraderStockGroup Group, TraderStockEntry Entry)>();
            var pools = new Dictionary<TraderStockGroup, List<TraderStockEntry>>();
            foreach (var group in groups)
            {
                var pool = (group.Candidates ?? new List<TraderStockEntry>())
                    .Where(e => e != null && e.IsAvailable(level) && !localIds.Contains(e.Item.Id))
                    .GroupBy(e => e.Item.Id).Select(g => g.First()).ToList();
                pools[group] = pool;
                foreach (var entry in pool.Where(e => previous.Contains(e.Item.Id)).Take(group.Slots))
                    if (!selected.Any(s => s.Entry.Item.Id == entry.Item.Id)) selected.Add((group, entry));
            }
            if (rotate)
            {
                // Half of the whole rotating offer, not half of every material category.
                var count = (selected.Count + 1) / 2;
                var eligible = selected.OrderBy(_ => random.Next()).ToList();
                foreach (var old in eligible)
                {
                    if (count == 0) break;
                    var replacements = pools[old.Group].Where(e => !previous.Contains(e.Item.Id)
                        && !selected.Any(s => s.Entry.Item.Id == e.Item.Id)).ToList();
                    if (replacements.Count == 0) continue;
                    selected.Remove(old);
                    selected.Add((old.Group, replacements[random.Next(replacements.Count)]));
                    count--;
                }
            }
            foreach (var group in groups)
            {
                var available = pools[group].Where(e => !selected.Any(s => s.Entry.Item.Id == e.Item.Id)).ToList();
                while (selected.Count(s => s.Group == group) < group.Slots && available.Count > 0)
                {
                    var index = random.Next(available.Count);
                    selected.Add((group, available[index]));
                    available.RemoveAt(index);
                }
            }
            var retained = new HashSet<string>(selected.Select(s => s.Entry.Item.Id));
            npc.ShopInventory.Items.RemoveAll(i => i?.Definition != null
                && previous.Contains(i.Definition.Id) && !retained.Contains(i.Definition.Id)
                && !localIds.Contains(i.Definition.Id));
            npc.RotatingStockItemIds = retained.ToList();
            foreach (var entry in local.Concat(selected.Select(s => s.Entry)))
            {
                var existing = npc.ShopInventory.Items.Count(i => i?.Definition?.Id == entry.Item.Id);
                for (var amount = existing; amount < entry.Limit; amount++)
                    npc.ShopInventory.Items.Add(factory.Create(entry.Item));
            }
            RemoveInvalidEquipmentOffers(npc);
            EnforceAlchemyIngredientLimits(npc);
            craftedStock.Refresh(npc, level, catalog, rotate);
            // BuybackStock deliberately has an independent lifecycle.
        }

        // Starter/base-definition instances are not crafted merchandise. Only
        // the main offer is repaired; player buyback has a separate owner.
        public static int RemoveInvalidEquipmentOffers(NpcInstance npc)
        {
            if (npc?.ShopInventory == null || npc.Definition?.GetRole<BlacksmithRoleData>() == null) return 0;
            var removed = npc.ShopInventory.Items.RemoveAll(item => item is Godmist.Runtime.Items.EquipmentItemInstance gear
                && gear.Definition.ItemType is Godmist.Core.Enums.ItemType.Weapon or Godmist.Core.Enums.ItemType.Armor
                && (TradeService.IsFreeStarter(gear) || gear.BaseValue <= 0
                    || gear.Definition is not Godmist.Data.Items.UniqueEquipmentDefinition && gear.PartIds.Count < 3));
            if (removed > 0) npc.CraftedStockInstanceIds.RemoveAll(id => !npc.ShopInventory.Items.Any(item => item.InstanceId == id));
            return removed;
        }

        // Apply authored limits to retained legacy stock without rotating/refilling
        // it or touching player inventories and the independent buyback list.
        public static int EnforceAlchemyIngredientLimits(NpcInstance npc)
        {
            var alchemist = npc?.Definition?.GetRole<AlchemistRoleData>();
            var trader = npc?.Definition?.GetRole<TraderRoleData>();
            if (alchemist == null || trader == null || npc.ShopInventory == null) return 0;
            var ingredients = (alchemist.IngredientOptions ?? new()).Where(e => e?.Material != null)
                .Select(e => e.Material.Id).ToHashSet();
            var limits = (trader.LocalStock ?? new()).Concat((trader.RotatingStock ?? new())
                    .Where(g => g != null).SelectMany(g => g.Candidates ?? new()))
                .Where(e => e?.Item != null && ingredients.Contains(e.Item.Id))
                .GroupBy(e => e.Item.Id).ToDictionary(g => g.Key, g => Math.Max(0, g.Min(e => e.Limit)));
            var seen = new Dictionary<string, int>();
            return npc.ShopInventory.Items.RemoveAll(item =>
            {
                if (item?.Definition == null || !limits.TryGetValue(item.Definition.Id, out var limit)) return false;
                seen.TryGetValue(item.Definition.Id, out var count); seen[item.Definition.Id] = count + 1;
                return count >= limit;
            });
        }
    }
}
