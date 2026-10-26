using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public partial class LootTableSystem
    {
        private bool PassesDrop(float chance)
        {
            var roll = randomValue();
            return chance > 0f && (chance >= 1f || roll < chance);
        }

        // Equipment bags are only a source of authored item/quality weights
        // here. Direct rewards never roll a shared all-or-nothing package.
        // Creating/opening an actual lootbag continues using the original path.
        private void RollIndependentEquipment(LootBagItemDefinition definition,
            int minimum, int maximum, float sourceChance, List<ItemInstance> items)
        {
            if (!definition.TryGetBracket(LootLevelBracketResolver.Resolve(lootLevel), out var bracket)) return;
            var repetitions = RollAmount(Mathf.Max(0, minimum), Mathf.Max(minimum, maximum));
            for (var repeat = 0; repeat < repetitions; repeat++)
            {
                var selectedByGroup = new Dictionary<string, HashSet<string>>();
                foreach (var pool in bracket.Pools)
                {
                    var entries = pool?.Entries?.Where(e => e?.ItemDefinition != null).ToList();
                    if (entries == null || entries.Count == 0) continue;
                    HashSet<string> selected = null;
                    if (!string.IsNullOrWhiteSpace(pool.DistinctSelectionGroup))
                    {
                        if (!selectedByGroup.TryGetValue(pool.DistinctSelectionGroup, out selected))
                            selectedByGroup.Add(pool.DistinctSelectionGroup, selected = new HashSet<string>());
                        entries.RemoveAll(e => selected.Contains(e.ItemDefinition.Id));
                    }
                    if (entries.Count == 0) continue;
                    var threshold = Mathf.Clamp01(randomValue()) * entries.Sum(SelectionWeight);
                    var entry = entries[entries.Count - 1];
                    foreach (var candidate in entries)
                    {
                        threshold -= SelectionWeight(candidate);
                        if (threshold > 0f) continue;
                        entry = candidate;
                        break;
                    }
                    var amount = RollAmount(Mathf.Max(0, entry.Amount.Min), Mathf.Max(entry.Amount.Min, entry.Amount.Max));
                    var chance = sourceChance * Mathf.Clamp01(pool.DropChance + (pool.DropChance > 0 ? dropChanceBonus : 0));
                    for (var i = 0; i < amount; i++)
                    {
                        if (!PassesDrop(chance)) continue;
                        var item = CreateItem(entry.ItemDefinition,
                            entry.HasFixedEquipmentQuality ? entry.EquipmentQuality : null,
                            entry.MinimumGalduryteRarity, entry.FixedGalduryteLevel);
                        if (item == null) continue;
                        items.Add(item);
                        selected?.Add(entry.ItemDefinition.Id);
                    }
                }
            }
        }
    }
}
