using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public partial class LootTableSystem
    {
        private readonly Func<float> randomValue;

        private readonly ItemInstanceFactory itemInstanceFactory;
        private readonly EquipmentGenerationService equipmentGenerationService;
        private readonly IReadOnlyList<EquipmentPartDefinition> equipmentParts;
        private readonly int lootLevel;
        private readonly float dropChanceBonus;
        private readonly JewelryGenerationService jewelryGenerationService;
        private System.Random galduryteRandom;

        public LootTableSystem(
            Func<float> randomValue = null,
            ItemInstanceFactory itemInstanceFactory = null,
            EquipmentGenerationService equipmentGenerationService = null,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null,
            int lootLevel = 1, float dropChanceBonus = 0)
        {
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
            this.itemInstanceFactory = itemInstanceFactory ?? new ItemInstanceFactory();
            this.equipmentGenerationService = equipmentGenerationService;
            this.equipmentParts = equipmentParts;
            this.lootLevel = Mathf.Clamp(lootLevel, 1, 50);
            this.dropChanceBonus = Mathf.Clamp01(dropChanceBonus);
            jewelryGenerationService = new JewelryGenerationService(this.randomValue, this.itemInstanceFactory);
        }

        public LootTableSystem(
            System.Random random,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts,
            ItemInstanceFactory itemInstanceFactory = null,
            int lootLevel = 1, float dropChanceBonus = 0)
            : this(
                () => (float)(random ?? throw new ArgumentNullException(nameof(random))).NextDouble(),
                itemInstanceFactory,
                new EquipmentGenerationService(random, itemInstanceFactory),
                equipmentParts,
                lootLevel, dropChanceBonus)
        {
            galduryteRandom = random;
        }

        public List<ItemInstance> RollLoot(LootTableData lootTable)
            => RollLoot(lootTable, 1f);

        public List<ItemInstance> RollDirectLoot(LootTableData lootTable) => RollLoot(lootTable, 1f, true);

        private List<ItemInstance> RollLoot(LootTableData lootTable, float equipmentChanceScale, bool direct = false)
        {
            var items = new List<ItemInstance>();

            if (lootTable?.Entries == null) return items;

            foreach (var entry in lootTable.Entries)
            {
                if (entry?.ItemDefinition == null) continue;

                var scale = ExpectedEquipmentCount(entry.ItemDefinition) > 0f ? equipmentChanceScale : 1f;
                var chance = Mathf.Clamp01(entry.DropChance * scale + (entry.DropChance > 0 ? dropChanceBonus : 0));
                if (direct && entry.ItemDefinition is LootBagItemDefinition equipmentBag
                    && (equipmentBag.BossUniqueDrop?.EquipmentDefinitions?.Count ?? 0) == 0
                    && ExpectedEquipmentCount(equipmentBag) > 0f)
                {
                    RollIndependentEquipment(equipmentBag, entry.Amount.Min, entry.Amount.Max, chance, items);
                    continue;
                }
                if (direct && entry.ItemDefinition is EquipmentItemDefinition)
                {
                    var count = RollAmount(Mathf.Max(0, entry.Amount.Min), Mathf.Max(entry.Amount.Min, entry.Amount.Max));
                    for (var i = 0; i < count; i++)
                        if (PassesDrop(chance))
                        {
                            var equipment = CreateItem(entry.ItemDefinition);
                            if (equipment != null) items.Add(equipment);
                        }
                    continue;
                }
                var roll = randomValue();
                if (chance <= 0f || (chance < 1f && roll >= chance)) continue;

                var minAmount = Mathf.Max(0, entry.Amount.Min);
                var maxAmount = Mathf.Max(minAmount, entry.Amount.Max);
                var amount = RollAmount(minAmount, maxAmount);

                for (var index = 0; index < amount; index++)
                {
                    var item = CreateItem(entry.ItemDefinition);
                    if (item != null && (item is not LootBagItemInstance bag || !bag.IsEmpty))
                        items.Add(item);
                }
            }

            return items;
        }

        public List<ItemInstance> RollChestLoot(LootTableData lootTable, int equipmentChestCount = 0)
        {
            var contents = new List<ItemInstance>();
            var scale = 1f;
            if (lootTable != null && lootTable.ExpectedEquipmentPerDungeon > 0f && equipmentChestCount > 0)
            {
                var expected = lootTable.Entries.Where(e => e?.ItemDefinition != null).Sum(e =>
                    e.DropChance * (Mathf.Max(0, e.Amount.Min) + Mathf.Max(e.Amount.Min, e.Amount.Max)) * .5f
                    * ExpectedEquipmentCount(e.ItemDefinition));
                if (expected > 0f)
                    scale = lootTable.ExpectedEquipmentPerDungeon / (equipmentChestCount * expected);
            }
            foreach (var item in RollLoot(lootTable, scale, true))
            {
                if (item is LootBagItemInstance bag)
                    contents.AddRange(bag.Contents);
                else
                    contents.Add(item);
            }

            return contents;
        }

        // Only pure equipment rewards participate; resource bags retain their own probabilities.
        private float ExpectedEquipmentCount(ItemDefinition definition)
        {
            if (definition is EquipmentItemDefinition equipment
                && equipment.ItemType is ItemType.Weapon or ItemType.Armor) return 1f;
            if (definition is not LootBagItemDefinition bag
                || !bag.TryGetBracket(LootLevelBracketResolver.Resolve(lootLevel), out var bracket)
                || bracket.Pools == null) return 0f;
            var expected = 0f;
            foreach (var pool in bracket.Pools)
            {
                if (pool?.Entries == null) continue;
                var entries = pool.Entries.Where(e => e?.ItemDefinition != null).ToArray();
                if (entries.Length == 0) continue;
                if (entries.Any(e => e.ItemDefinition is not EquipmentItemDefinition
                    || e.ItemDefinition.ItemType is not (ItemType.Weapon or ItemType.Armor))) return 0f;
                expected += pool.DropChance * entries.Average(e =>
                    (Mathf.Max(0, e.Amount.Min) + Mathf.Max(e.Amount.Min, e.Amount.Max)) * .5f);
            }
            return expected;
        }

        public LootBagItemInstance CreateLootBag(
            LootBagItemDefinition definition)
        {
            return definition == null
                ? null
                : CreateItem(definition) as LootBagItemInstance;
        }

        public ItemInstance GenerateSingleItem(ItemDefinition definition) =>
            definition == null ? null : CreateItem(definition);

        private int RollAmount(int minAmount, int maxAmount)
        {
            if (minAmount >= maxAmount) return minAmount;

            var rangeSize = maxAmount - minAmount + 1;
            var randomIndex = Mathf.FloorToInt(
                Mathf.Clamp01(randomValue()) * rangeSize);

            return minAmount + Mathf.Min(randomIndex, rangeSize - 1);
        }

        private ItemInstance CreateItem(ItemDefinition definition,
            EquipmentQuality? fixedEquipmentQuality = null,
            RarityTier minimumGalduryteRarity = RarityTier.Common, int fixedGalduryteLevel = 0)
        {
            if (definition is GalduryteItemDefinition galduryteDefinition)
            {
                return CreateGalduryte(galduryteDefinition, minimumGalduryteRarity, fixedGalduryteLevel);
            }

            if (definition is JewelryItemDefinition jewelryDefinition)
            {
                return jewelryGenerationService.Generate(jewelryDefinition, lootLevel);
            }

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

            var availableParts = equipmentParts.Where(p => p != null
                && (int)p.LevelBracket == Mathf.Min(45, ((lootLevel - 1) / 10) * 10 + 5)).ToList();
            return fixedEquipmentQuality.HasValue
                ? equipmentGenerationService.Generate(equipmentDefinition,
                    availableParts, fixedEquipmentQuality.Value)
                : equipmentGenerationService.Generate(equipmentDefinition,
                    availableParts);
        }

        private GalduryteItemInstance CreateGalduryte(GalduryteItemDefinition definition,
            RarityTier minimumRarity, int fixedLevel = 0)
        {
            if (lootLevel < 11 || lootLevel > 20) return null;
            var random = galduryteRandom ??= new System.Random(
                Mathf.FloorToInt(randomValue() * int.MaxValue));
            var targetType = random.Next(2) == 0
                ? GalduryteTargetType.Weapon : GalduryteTargetType.Armor;
            var colors = new[] { GaldurytePowderColor.Red,
                GaldurytePowderColor.Blue, GaldurytePowderColor.Purple };
            if (!GalduryteRuntimeRules.TryRollRarity(random, out var rarity))
                return null;
            if (rarity < minimumRarity) rarity = minimumRarity;
            var color = colors[random.Next(colors.Length)];
            var itemLevel = fixedLevel >= 11 && fixedLevel <= 20 ? fixedLevel : 11;
            if (!GalduryteRuntimeRules.TryRollEffects(targetType, color,
                    rarity, itemLevel, random, out var effects, out _))
                return null;
            var item = itemInstanceFactory.Create(definition) as GalduryteItemInstance;
            if (item == null) return null;
            item.TargetType = targetType;
            item.Tier = GalduryteTier.Limestone;
            item.ItemLevel = itemLevel;
            item.CurrentRarity = rarity;
            item.GuaranteedPowderColor = color;
            item.StoneDefinitionId = "item.runic.galduryte_stone.limestone";
            item.PowderDefinitionId = "item.runic.galduryte_powder." + color.ToString().ToLowerInvariant();
            item.StoneDisplayName = GalduryteRuntimeRules.GetStoneName(item.Tier);
            item.CraftedBaseValue = Mathf.Max(1, definition.BaseValue);
            item.Effects = effects;
            item.IsIdentified = false;
            return item;
        }

        private List<ItemInstance> RollLootBagContents(
            LootBagItemDefinition definition)
        {
            var contents = new List<ItemInstance>();
            var selectedByGroup = new Dictionary<string, HashSet<string>>();
            var bracket = LootLevelBracketResolver.Resolve(lootLevel);

            if (!definition.TryGetBracket(bracket, out var bracketData)
                || bracketData.Pools == null)
            {
                return contents;
            }

            foreach (var pool in bracketData.Pools)
            {
                if (pool == null)
                {
                    continue;
                }

                var chance = Mathf.Clamp01(pool.DropChance + (pool.DropChance > 0 ? dropChanceBonus : 0));
                var roll = randomValue();
                if (chance <= 0f || (chance < 1f && roll >= chance)) continue;

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

                if (!string.IsNullOrWhiteSpace(pool.DistinctSelectionGroup))
                {
                    if (!selectedByGroup.TryGetValue(pool.DistinctSelectionGroup, out var selected))
                    {
                        selected = new HashSet<string>(StringComparer.Ordinal);
                        selectedByGroup.Add(pool.DistinctSelectionGroup, selected);
                    }
                    entries.RemoveAll(entry => selected.Contains(entry.ItemDefinition.Id));
                }

                if (entries.Count == 0) continue;

                // Keep the uniform legacy path and its single RNG draw intact.
                // Weighted pools use the same one draw after distinct filtering.
                var rollSelection = Mathf.Clamp01(randomValue());
                var totalWeight = 0d;
                var weighted = false;
                foreach (var entry in entries)
                {
                    var weight = SelectionWeight(entry);
                    totalWeight += weight;
                    weighted |= weight != 1f;
                }
                var selectedIndex = Mathf.Min(Mathf.FloorToInt(rollSelection * entries.Count), entries.Count - 1);
                if (weighted)
                {
                    var threshold = rollSelection * totalWeight;
                    var cumulative = 0d;
                    selectedIndex = entries.Count - 1;
                    for (var index = 0; index < entries.Count; index++)
                    {
                        cumulative += SelectionWeight(entries[index]);
                        if (threshold >= cumulative) continue;
                        selectedIndex = index;
                        break;
                    }
                }

                var selectedEntry = entries[selectedIndex];
                if (!string.IsNullOrWhiteSpace(pool.DistinctSelectionGroup))
                    selectedByGroup[pool.DistinctSelectionGroup].Add(selectedEntry.ItemDefinition.Id);
                var minAmount = Mathf.Max(0, selectedEntry.Amount.Min);
                var maxAmount = Mathf.Max(minAmount, selectedEntry.Amount.Max);
                var amount = RollAmount(minAmount, maxAmount);

                for (var index = 0; index < amount; index++)
                {
                    var item = CreateItem(selectedEntry.ItemDefinition,
                        selectedEntry.HasFixedEquipmentQuality
                            ? selectedEntry.EquipmentQuality : null,
                        selectedEntry.MinimumGalduryteRarity, selectedEntry.FixedGalduryteLevel);
                    if (item != null) contents.Add(item);
                }
            }

            var bossUnique = RollBossUniqueDrop(definition.BossUniqueDrop);
            if (bossUnique != null)
            {
                contents.Add(bossUnique);
            }

            return contents;
        }

        private static float SelectionWeight(LootBagPoolEntryData entry)
        {
            var weight = entry.SelectionWeight;
            return weight > 0f && !float.IsNaN(weight) && !float.IsInfinity(weight) ? weight : 1f;
        }

        private EquipmentItemInstance RollBossUniqueDrop(BossUniqueDropData data)
        {
            if (data == null
                || equipmentParts == null
                || equipmentGenerationService == null
                || data.EquipmentDefinitions == null
                || data.EquipmentDefinitions.Count == 0)
            {
                return null;
            }

            var chance = Mathf.Clamp01(data.DropChance + (data.DropChance > 0 ? dropChanceBonus : 0));
            var roll = randomValue();
            if (chance <= 0f || (chance < 1f && roll >= chance)) return null;

            var candidates = new List<EquipmentItemDefinition>();
            foreach (var definition in data.EquipmentDefinitions)
            {
                if (definition != null && definition.RequiredClass != null
                    && definition is UniqueEquipmentDefinition
                    && definition.ItemType is ItemType.Weapon or ItemType.Armor)
                {
                    candidates.Add(definition);
                }
            }

            if (candidates.Count != 4) return null;

            var classIds = new HashSet<string>();

            foreach (var candidate in candidates)
            {
                if (!classIds.Add(candidate.RequiredClass.Id))
                {
                    return null;
                }
            }

            var selectedIndex = Mathf.FloorToInt(
                Mathf.Clamp01(randomValue()) * candidates.Count);
            selectedIndex = Mathf.Min(selectedIndex, candidates.Count - 1);

            return equipmentGenerationService.GenerateUnique(
                candidates[selectedIndex],
                equipmentParts.Where(p => p != null && (int)p.LevelBracket == (lootLevel >= 20 ? 15 : 5)).ToList());
        }
    }
}
