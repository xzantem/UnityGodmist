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

        public LootTableSystem(
            Func<float> randomValue = null,
            ItemInstanceFactory itemInstanceFactory = null,
            EquipmentGenerationService equipmentGenerationService = null,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null)
        {
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
            this.itemInstanceFactory = itemInstanceFactory ?? new ItemInstanceFactory();
            this.equipmentGenerationService = equipmentGenerationService;
            this.equipmentParts = equipmentParts;
        }

        public LootTableSystem(
            System.Random random,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts,
            ItemInstanceFactory itemInstanceFactory = null)
            : this(
                () => (float)(random ?? throw new ArgumentNullException(nameof(random))).NextDouble(),
                itemInstanceFactory,
                new EquipmentGenerationService(random, itemInstanceFactory),
                equipmentParts)
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
    }
}
