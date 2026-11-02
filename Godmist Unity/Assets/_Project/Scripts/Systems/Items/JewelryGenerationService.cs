using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class JewelryGenerationService
    {
        private readonly Func<float> randomValue;
        private readonly ItemInstanceFactory itemFactory;

        public JewelryGenerationService(Func<float> randomValue = null, ItemInstanceFactory itemFactory = null)
        {
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
            this.itemFactory = itemFactory ?? new ItemInstanceFactory();
        }

        public JewelryItemInstance Generate(JewelryItemDefinition definition, int itemLevel)
        {
            var item = (JewelryItemInstance)itemFactory.Create(definition);
            var metal = Choose(definition.Metals, value => GetItemLevel(value) <= itemLevel);
            var gem = Choose(definition.Gems, _ => true);
            if (metal == null || gem == null) return item;

            if (!CraftingValueCalculator.TryValidate(new[] { metal.Material, gem.CraftingMaterial }, out _)) return null;
            ApplySelection(item, metal, gem);
            return item;
        }

        public JewelryItemInstance GenerateSelected(
            JewelryItemDefinition definition,
            JewelryMetalData metal,
            JewelryGemDefinition gem)
        {
            if (definition == null || metal == null || gem == null) return null;

            var item = (JewelryItemInstance)itemFactory.Create(definition);
            if (!CraftingValueCalculator.TryValidate(new[] { metal.Material, gem.CraftingMaterial }, out _)) return null;
            ApplySelection(item, metal, gem);
            return item;
        }

        private static void ApplySelection(
            JewelryItemInstance item,
            JewelryMetalData metal,
            JewelryGemDefinition gem)
        {
            var rarity = GetMaterialRarity(metal.Material);
            var itemLevel = GetItemLevel(rarity);
            item.ItemLevel = itemLevel;
            item.RequiredLevel = itemLevel;
            item.RequiredClassId = string.Empty;
            item.CurrentRarity = rarity;
            item.MetalId = GetMetalRecipeKey(metal);
            item.MetalDisplayName = GetMetalDisplayName(metal);
            item.BaseStatModifiers = CollectStatModifiers(metal.Modifiers, gem.Modifiers);
            item.StatModifiers = new List<StatModifier>(item.BaseStatModifiers);
            item.GemId = gem.CraftingMaterial?.Id ?? string.Empty;
            item.GemDisplayName = gem.CraftingMaterial?.DisplayName ?? string.Empty;
            item.CraftedBaseValue = CalculateMaterialValue(metal, gem);
        }

        private static List<StatModifier> CollectStatModifiers(
            List<JewelryModifierData> metalModifiers,
            List<JewelryModifierData> gemModifiers)
        {
            var result = new List<StatModifier>();
            AddStatModifiers(result, metalModifiers);
            AddStatModifiers(result, gemModifiers);
            return result;
        }

        private static void AddStatModifiers(
            List<StatModifier> result,
            List<JewelryModifierData> modifiers)
        {
            foreach (var modifier in modifiers ?? new List<JewelryModifierData>())
            {
                if (modifier?.Target != JewelryModifierTarget.Stat) continue;
                result.Add(new StatModifier
                {
                    StatType = modifier.StatType,
                    Operation = modifier.Operation,
                    Stacking = StatModifierStacking.Additive,
                    Value = modifier.Value
                });
            }
        }

        public static string GetMetalRecipeKey(JewelryMetalData metal)
        {
            return metal?.Material == null || metal.Amount < 1 ? string.Empty : $"{metal.Material.Id}:{metal.Amount}";
        }

        public static string GetMetalDisplayName(JewelryMetalData metal)
        {
            return metal?.Material?.DisplayName ?? string.Empty;
        }

        public static RarityTier GetMaterialRarity(
            ItemDefinition material)
        {
            return material?.BaseRarity ?? RarityTier.Common;
        }

        public static int GetItemLevel(JewelryMetalData metal) =>
            GetItemLevel(GetMaterialRarity(metal?.Material));

        public static int GetItemLevel(RarityTier rarity) => rarity switch
        {
            RarityTier.Uncommon => 20,
            RarityTier.Rare => 30,
            RarityTier.Ancient => 40,
            RarityTier.Legendary or RarityTier.Mythic or RarityTier.Godly => 50,
            _ => 10
        };

        private static int CalculateMaterialValue(JewelryMetalData metal, JewelryGemDefinition gem)
        {
            var value = 0;
            value += GetMaterialValue(metal?.Material) * Math.Max(0, metal?.Amount ?? 0);

            value += GetMaterialValue(gem.CraftingMaterial)
                * Math.Max(0, gem.Amount);
            return value;
        }

        private static int GetMaterialValue(ItemDefinition material) => material is CraftableItemDefinition craftable
            ? Math.Max(0, craftable.CalculatedBaseValue)
            : Math.Max(0, material?.BaseValue ?? 0);

        private T Choose<T>(IReadOnlyList<T> values, Func<T, bool> predicate) where T : class
        {
            var valid = new List<T>();
            foreach (var value in values ?? Array.Empty<T>()) if (value != null && predicate(value)) valid.Add(value);
            if (valid.Count == 0) return null;
            return valid[Math.Min(valid.Count - 1, (int)(randomValue() * valid.Count))];
        }
    }
}
