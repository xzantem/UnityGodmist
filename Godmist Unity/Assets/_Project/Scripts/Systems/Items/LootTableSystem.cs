using System;
using System.Collections.Generic;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public class LootTableSystem
    {
        private readonly Func<float> randomValue;

        public LootTableSystem(Func<float> randomValue = null)
        {
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
        }

        public List<ItemInstance> RollLoot(LootTableData lootTable)
        {
            var items = new List<ItemInstance>();

            if (lootTable?.Entries == null) return items;

            foreach (var entry in lootTable.Entries)
            {
                if (entry?.ItemDefinition == null) continue;

                var chance = Mathf.Clamp01(entry.DropChance);

                if (randomValue() > chance) continue;

                var minAmount = Mathf.Max(0, entry.Amount.Min);
                var maxAmount = Mathf.Max(minAmount, entry.Amount.Max);
                var amount = RollAmount(minAmount, maxAmount);

                for (var index = 0; index < amount; index++)
                {
                    items.Add(new ItemInstance(entry.ItemDefinition));
                }
            }

            return items;
        }

        private int RollAmount(int minAmount, int maxAmount)
        {
            if (minAmount >= maxAmount) return minAmount;

            var rangeSize = maxAmount - minAmount + 1;
            var randomIndex = Mathf.FloorToInt(
                Mathf.Clamp01(randomValue()) * rangeSize);

            return minAmount + Mathf.Min(randomIndex, rangeSize - 1);
        }
    }
}