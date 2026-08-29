using System;
using System.Collections.Generic;
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
            item.ItemLevel = itemLevel;
            var metalSource = definition.Metals != null && definition.Metals.Count > 0
                ? definition.Metals
                : definition.MetalSource?.Metals;
            var metal = Choose(metalSource, value => value.MinimumLevel <= itemLevel);
            var gem = Choose(definition.Gems, _ => true);
            if (gem == null) return item;

            if (metal != null)
            {
                item.MetalId = metal.Id;
                item.MetalDisplayName = metal.DisplayName;
                item.StatModifiers = metal.StatModifiers == null ? new List<StatModifier>() : new List<StatModifier>(metal.StatModifiers);
                item.ResistanceType = metal.ResistanceModifier?.ResistanceType ?? default;
                item.ResistanceValue = metal.ResistanceModifier?.Value ?? 0f;
            }
            item.GemId = gem.Id;
            item.GemDisplayName = gem.DisplayName;
            item.GemDescription = gem.Description;
            item.PassiveType = gem.PassiveType;
            item.PassiveValue = gem.Value;
            item.PassiveDungeonTheme = gem.DungeonTheme;
            if (gem.IsBossUnique) item.Quality = Godmist.Core.Enums.EquipmentQuality.Masterpiece;
            return item;
        }

        private T Choose<T>(IReadOnlyList<T> values, Func<T, bool> predicate) where T : class
        {
            var valid = new List<T>();
            foreach (var value in values ?? Array.Empty<T>()) if (value != null && predicate(value)) valid.Add(value);
            if (valid.Count == 0) return null;
            return valid[Math.Min(valid.Count - 1, (int)(randomValue() * valid.Count))];
        }
    }
}
