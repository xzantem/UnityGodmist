using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public class LootTableSystem
    {
        private readonly Func<float> randomValue;

        private readonly ItemInstanceFactory itemInstanceFactory;
        private readonly EquipmentGenerationService equipmentGenerationService;
        private readonly IReadOnlyList<EquipmentPartDefinition> equipmentParts;
        private readonly int lootLevel;

        public LootTableSystem(
            Func<float> randomValue = null,
            ItemInstanceFactory itemInstanceFactory = null,
            EquipmentGenerationService equipmentGenerationService = null,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null,
            int lootLevel = 1)
        {
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
            this.itemInstanceFactory = itemInstanceFactory ?? new ItemInstanceFactory();
            this.equipmentGenerationService = equipmentGenerationService;
            this.equipmentParts = equipmentParts;
            this.lootLevel = Mathf.Clamp(lootLevel, 1, 50);
        }

        public LootTableSystem(
            System.Random random,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts,
            ItemInstanceFactory itemInstanceFactory = null,
            int lootLevel = 1)
            : this(
                () => (float)(random ?? throw new ArgumentNullException(nameof(random))).NextDouble(),
                itemInstanceFactory,
                new EquipmentGenerationService(random, itemInstanceFactory),
                equipmentParts,
                lootLevel)
        {
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
                    var item = CreateItem(entry.ItemDefinition);
                    if (item != null) items.Add(item);
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

        private ItemInstance CreateItem(ItemDefinition definition)
        {
            if (definition is LootBagItemDefinition lootBagDefinition)
            {
                var lootBag = (LootBagItemInstance)itemInstanceFactory.Create(
                    lootBagDefinition);
                lootBag.ItemLevel = lootLevel;
                lootBag.TryRestoreContents(
                    RollLootBagContents(lootBagDefinition));
                return lootBag;
            }

            if (definition is not EquipmentItemDefinition equipmentDefinition
                || equipmentDefinition.ItemType is not (ItemType.Weapon or ItemType.Armor)
                || equipmentParts == null
                || equipmentGenerationService == null)
            {
                return itemInstanceFactory.Create(definition);
            }

            return equipmentGenerationService.Generate(
                equipmentDefinition,
                equipmentParts);
        }

        private List<ItemInstance> RollLootBagContents(
            LootBagItemDefinition definition)
        {
            var contents = new List<ItemInstance>();
            var bracket = LootLevelBracketResolver.Resolve(lootLevel);

            if (!definition.TryGetBracket(bracket, out var bracketData)
                || bracketData.Pools == null)
            {
                return contents;
            }

            foreach (var pool in bracketData.Pools)
            {
                if (pool == null
                    || randomValue() > Mathf.Clamp01(pool.DropChance))
                {
                    continue;
                }

                var entries = new List<LootBagPoolEntryData>();

                if (pool.Entries != null)
                {
                    foreach (var entry in pool.Entries)
                    {
                        if (entry?.ItemDefinition != null)
                        {
                            entries.Add(entry);
                        }
                    }
                }

                if (entries.Count == 0) continue;

                var selectedIndex = Mathf.FloorToInt(
                    Mathf.Clamp01(randomValue()) * entries.Count);
                selectedIndex = Mathf.Min(selectedIndex, entries.Count - 1);

                var selectedEntry = entries[selectedIndex];
                var minAmount = Mathf.Max(0, selectedEntry.Amount.Min);
                var maxAmount = Mathf.Max(minAmount, selectedEntry.Amount.Max);
                var amount = RollAmount(minAmount, maxAmount);

                for (var index = 0; index < amount; index++)
                {
                    var item = CreateItem(selectedEntry.ItemDefinition);
                    if (item != null) contents.Add(item);
                }
            }

            return contents;
        }
    }
}
