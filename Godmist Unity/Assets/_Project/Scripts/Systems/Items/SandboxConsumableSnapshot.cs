using System.Collections.Generic;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    /// <summary>One-way imports owned by one sandbox driver, never by the campaign.</summary>
    public sealed class SandboxConsumableSnapshot
    {
        private readonly Dictionary<CharacterInstance, HashSet<string>> imported = new();

        public int Import(CharacterInstance source, CharacterInstance target)
        {
            if (source == null || target == null || ReferenceEquals(source, target)
                || source.Inventory?.Items == null || target.Inventory?.Items == null) return 0;
            if (!imported.TryGetValue(target, out var ids))
                imported.Add(target, ids = new HashSet<string>());

            var count = 0;
            foreach (var item in source.Inventory.Items)
            {
                if (item is not ConsumableItemInstance original || !original.HasCharges
                    || original.ConsumableDefinition == null || string.IsNullOrWhiteSpace(original.InstanceId)
                    || !ids.Add(original.InstanceId)) continue;

                var copy = new ConsumableItemInstance(original.ConsumableDefinition)
                {
                    CurrentRarity = original.CurrentRarity,
                    ItemLevel = original.ItemLevel,
                    CustomName = original.CustomName,
                    IsSaleProtected = original.IsSaleProtected,
                    CurrentCharges = original.CurrentCharges,
                    BlastRange = original.BlastRange,
                    BlastAreaSize = original.BlastAreaSize,
                    CatalystDurationModifier = original.CatalystDurationModifier,
                    CatalystEffectMultiplier = original.CatalystEffectMultiplier,
                    CatalystChargeBonus = original.CatalystChargeBonus,
                    CatalystDisplayName = original.CatalystDisplayName,
                    CraftedBaseValue = original.CraftedBaseValue,
                    RuntimeEffects = original.RuntimeEffects == null ? null : new List<ConsumableEffectRuntime>()
                };
                if (original.RuntimeEffects != null)
                    foreach (var effect in original.RuntimeEffects)
                        copy.RuntimeEffects.Add(effect == null ? null : new ConsumableEffectRuntime
                        {
                            EffectType = effect.EffectType,
                            Value = effect.Value,
                            DurationTurns = effect.DurationTurns,
                            Chance = effect.Chance,
                            TargetModeIndex = effect.TargetModeIndex,
                            StatusId = effect.StatusId,
                            ResistanceType = effect.ResistanceType,
                            TargetType = effect.TargetType,
                            TargetingType = effect.TargetingType
                        });
                target.Inventory.Items.Add(copy);
                count++;
            }
            return count;
        }
    }
}
