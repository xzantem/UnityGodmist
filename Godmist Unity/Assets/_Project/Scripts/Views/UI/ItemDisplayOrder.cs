using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godmist.Core.Utilities;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;

namespace Godmist.Views.UI
{
    /// <summary>Default item-grid order; quantity and representative instance IDs never order a stack.</summary>
    public static class ItemDisplayOrder
    {
        private static readonly StringComparer Names = StringComparer.Create(UiTerms.PolishCulture, true);

        public static List<T> Sort<T>(IEnumerable<T> stacks, Func<T, ItemInstance> representative)
        {
            return stacks.Select(stack => new { Stack = stack, Item = representative(stack) })
                .OrderBy(entry => entry.Item.Definition.ItemType)
                .ThenByDescending(entry => entry.Item.CurrentRarity)
                .ThenBy(entry => entry.Item.DisplayName, Names)
                .ThenByDescending(entry => TryGetIntrinsicValue(entry.Item, out var price) ? price : 0)
                // Equal visible keys still need a stable order when the first
                // physical instance is consumed, sold or transferred away.
                .ThenBy(entry => entry.Item.Definition.Id, StringComparer.Ordinal)
                .ThenBy(entry => entry.Item.ItemLevel)
                .ThenBy(entry => entry.Item.IsSaleProtected)
                .ThenBy(entry => ConsumableVariantKey(entry.Item), StringComparer.Ordinal)
                .ThenBy(entry => entry.Item.CanStackWith(entry.Item) ? string.Empty : entry.Item.InstanceId, StringComparer.Ordinal)
                .Select(entry => entry.Stack).ToList();
        }

        public static bool TryGetIntrinsicValue(ItemInstance item, out int value)
        {
            value = 0;
            if (item?.Definition == null
                || !CraftingValueCalculator.TryGetValue(item.Definition, out var definitionValue, out _)) return false;
            value = Math.Max(0, item.BaseValue);
            if (value <= 0 && item.Definition is CraftableItemDefinition) value = definitionValue;
            return true;
        }

        private static string ConsumableVariantKey(ItemInstance item)
        {
            if (item is not ConsumableItemInstance consumable || !consumable.CanStackWith(consumable)) return string.Empty;
            var key = new StringBuilder();
            void Add(object value)
            {
                if (value == null) { key.Append('N'); return; }
                var text = value is float number ? (number == 0f ? 0f : number).ToString("R", CultureInfo.InvariantCulture)
                    : value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture)
                    : value?.ToString() ?? string.Empty;
                key.Append('V').Append(text.Length).Append(':').Append(text);
            }
            Add(consumable.CustomName); Add(consumable.CurrentCharges);
            Add(consumable.BlastRange); Add(consumable.BlastAreaSize);
            Add(consumable.CatalystDurationModifier); Add(consumable.CatalystEffectMultiplier);
            Add(consumable.CatalystChargeBonus); Add(consumable.CatalystDisplayName);
            Add(consumable.CraftedBaseValue);
            var effects = consumable.RuntimeEffects;
            Add(effects?.Count ?? 0);
            if (effects != null) foreach (var effect in effects)
            {
                Add(effect != null);
                if (effect == null) continue;
                Add(effect.EffectType); Add(effect.Value); Add(effect.DurationTurns); Add(effect.Chance);
                Add(effect.TargetModeIndex); Add(effect.StatusId); Add(effect.ResistanceType);
                Add(effect.TargetType); Add(effect.TargetingType);
            }
            return key.ToString();
        }
    }
}
