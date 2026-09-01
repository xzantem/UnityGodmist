using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public sealed class ConsumableService
    {
        private readonly InventoryService inventoryService = new();
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycle = new();

        public bool TryUseInExploration(CharacterInstance character, ConsumableItemInstance item,
            out ExplorationConsumableEffect effect)
        {
            effect = ExplorationConsumableEffect.None;
            var definition = item?.ConsumableDefinition;
            if (character?.Inventory?.Items == null || definition == null || !item.HasCharges || definition.IsCombatOnly) return false;
            var applied = false;
            var runtimeEffects = item.RuntimeEffects ?? new System.Collections.Generic.List<ConsumableEffectRuntime>();
            if (runtimeEffects.Count == 0
                && ConsumableRuntimeRules.TryGetImpliedUtilityEffect(
                    definition,
                    out var impliedUtilityEffect))
            {
                runtimeEffects = new System.Collections.Generic.List<ConsumableEffectRuntime>
                {
                    impliedUtilityEffect
                };
            }
            var processed = new System.Collections.Generic.HashSet<ConsumableEffectRuntime>();
            for (var runtimeIndex = 0; runtimeIndex < runtimeEffects.Count; runtimeIndex++)
            {
                var runtimeEffect = runtimeEffects[runtimeIndex];
                if (runtimeEffect == null || !processed.Add(runtimeEffect)) continue;
                var rawValue = 0f;
                var occurrenceCount = 0;
                var durationTurns = 0;
                foreach (var candidate in runtimeEffects)
                {
                    if (!ConsumableRuntimeRules.CanCombineEffects(runtimeEffect, candidate)) continue;
                    processed.Add(candidate);
                    rawValue += Mathf.Max(0f, candidate.Value);
                    occurrenceCount++;
                    durationTurns = Mathf.Max(durationTurns, candidate.DurationTurns);
                }
                var combinedEffect = new ConsumableEffectRuntime
                {
                    EffectType = runtimeEffect.EffectType,
                    Value = rawValue,
                    DurationTurns = durationTurns,
                    Chance = runtimeEffect.Chance,
                    TargetModeIndex = runtimeEffect.TargetModeIndex,
                    StatusId = runtimeEffect.StatusId,
                    ResistanceType = runtimeEffect.ResistanceType,
                    TargetType = runtimeEffect.TargetType,
                    TargetingType = runtimeEffect.TargetingType
                };
                if (!TryApplyExplorationEffect(
                        character,
                        combinedEffect,
                        occurrenceCount,
                        item.CatalystEffectMultiplier,
                        runtimeIndex,
                        item.InstanceId,
                        out var appliedEffect)) continue;
                effect = effect == ExplorationConsumableEffect.None
                    ? appliedEffect
                    : effect;
                applied = true;
            }
            if (!applied) { effect = ExplorationConsumableEffect.None; return false; }
            if (effect == ExplorationConsumableEffect.ReturnToArungard) return true;
            item.CurrentCharges--;
            return item.CurrentCharges > 0 || inventoryService.TryRemoveItem(character.Inventory, item.InstanceId, out _);
        }

        public bool TrySpendCharge(CharacterInstance character, ConsumableItemInstance item)
        {
            if (character?.Inventory?.Items == null || item == null || !item.HasCharges || !character.Inventory.Items.Contains(item)) return false;
            item.CurrentCharges--;
            return item.CurrentCharges > 0 || inventoryService.TryRemoveItem(character.Inventory, item.InstanceId, out _);
        }

        private bool TryApplyExplorationEffect(
            CharacterInstance character,
            ConsumableEffectRuntime runtimeEffect,
            int occurrenceCount,
            float catalystEffectMultiplier,
            int runtimeIndex,
            string sourceItemId,
            out ExplorationConsumableEffect result)
        {
            result = ExplorationConsumableEffect.None;
            var effectiveValue = runtimeEffect.Value
                * ConsumableRuntimeRules.GetDuplicateMultiplier(occurrenceCount)
                * Mathf.Max(0f, catalystEffectMultiplier);
            switch (runtimeEffect.EffectType)
            {
                case ConsumableRuntimeEffectType.RestoreHealth:
                case ConsumableRuntimeEffectType.RegenHealth:
                    var maxHealth = Mathf.Max(0, Mathf.RoundToInt(character.Stats?.MaxHealth.GetValue() ?? 0f));
                    if (character.CurrentHealth >= maxHealth) return false;
                    character.CurrentHealth = Mathf.Min(maxHealth, character.CurrentHealth + Mathf.CeilToInt(maxHealth * effectiveValue));
                    result = ExplorationConsumableEffect.RestoreHealthPercent;
                    return true;
                case ConsumableRuntimeEffectType.RestoreResource:
                case ConsumableRuntimeEffectType.RegenResource:
                    if (character.ResourcePool == null
                        || character.ResourcePool.Restore(
                            Mathf.CeilToInt(character.ResourcePool.MaxValue * effectiveValue)) <= 0)
                        return false;
                    result = ExplorationConsumableEffect.RestoreResourcePercent;
                    return true;
                case ConsumableRuntimeEffectType.RemoveStatus:
                    if (string.IsNullOrWhiteSpace(runtimeEffect.StatusId)
                        || !passiveEffectLifecycle.RemoveEffect(character, runtimeEffect.StatusId))
                        return false;
                    result = runtimeEffect.StatusId switch
                    {
                        StatusEffectIds.Bleed => ExplorationConsumableEffect.RemoveBleed,
                        StatusEffectIds.Poison => ExplorationConsumableEffect.RemovePoison,
                        StatusEffectIds.Burn => ExplorationConsumableEffect.RemoveBurn,
                        _ => ExplorationConsumableEffect.None
                    };
                    return result != ExplorationConsumableEffect.None;
                case ConsumableRuntimeEffectType.ReturnToArungard:
                    result = ExplorationConsumableEffect.ReturnToArungard;
                    return true;
                default:
                    return TryApplyExplorationModifier(
                        character,
                        runtimeEffect,
                        occurrenceCount,
                        catalystEffectMultiplier,
                        runtimeIndex,
                        sourceItemId);
            }
        }

        private bool TryApplyExplorationModifier(
            CharacterInstance character,
            ConsumableEffectRuntime runtimeEffect,
            int occurrenceCount,
            float catalystEffectMultiplier,
            int runtimeIndex,
            string sourceItemId)
        {
            if (character == null || runtimeEffect == null) return false;
            var value = runtimeEffect.Value
                * ConsumableRuntimeRules.GetDuplicateMultiplier(occurrenceCount)
                * Mathf.Max(0f, catalystEffectMultiplier);
            var duration = Mathf.Max(1, runtimeEffect.DurationTurns);
            var effect = new PassiveEffectRuntime(
                $"consumable.{sourceItemId}.{runtimeEffect.EffectType}.{runtimeIndex}",
                new System.Collections.Generic.List<PassiveEffectSourceRuntime>(),
                1,
                duration,
                0,
                PassiveEffectLifetime.Timed,
                PassiveEffectTickPhase.None)
            {
                DisplayName = runtimeEffect.EffectType.ToString()
            };

            switch (runtimeEffect.EffectType)
            {
                case ConsumableRuntimeEffectType.ModifyMaxResource:
                case ConsumableRuntimeEffectType.ModifyPhysicalDefense:
                case ConsumableRuntimeEffectType.ModifyMagicalDefense:
                case ConsumableRuntimeEffectType.ModifySpeed:
                case ConsumableRuntimeEffectType.ModifyCritChance:
                case ConsumableRuntimeEffectType.ModifyDodge:
                    effect.SetPayload(new StatModifiersPayload
                    {
                        Modifiers = new System.Collections.Generic.List<StatModifier>
                        {
                            new()
                            {
                                StatType = runtimeEffect.EffectType switch
                                {
                                    ConsumableRuntimeEffectType.ModifyMaxResource => CharacterStatType.MaxResource,
                                    ConsumableRuntimeEffectType.ModifyPhysicalDefense => CharacterStatType.PhysicalDefense,
                                    ConsumableRuntimeEffectType.ModifyMagicalDefense => CharacterStatType.MagicalDefense,
                                    ConsumableRuntimeEffectType.ModifySpeed => CharacterStatType.Speed,
                                    ConsumableRuntimeEffectType.ModifyCritChance => CharacterStatType.CritChance,
                                    _ => CharacterStatType.Dodge
                                },
                                Operation = runtimeEffect.EffectType == ConsumableRuntimeEffectType.ModifyDodge
                                    ? StatModifierOperation.FlatAbsolute
                                    : StatModifierOperation.Percent,
                                Stacking = StatModifierStacking.Additive,
                                Value = value
                            }
                        }
                    });
                    break;
                case ConsumableRuntimeEffectType.ModifyDamage:
                    effect.SetPayload(new VariableModifiersPayload
                    {
                        Modifiers = new System.Collections.Generic.List<GameplayVariableModifier>
                        {
                            new()
                            {
                                VariableType = GameplayVariableType.DamageDealtPercent,
                                Stacking = StatModifierStacking.Additive,
                                Value = value
                            }
                        }
                    });
                    break;
                case ConsumableRuntimeEffectType.ModifyResistance:
                    effect.SetPayload(new ResistanceModifierPayload
                    {
                        ResistanceType = runtimeEffect.ResistanceType,
                        Value = value
                    });
                    break;
                default:
                    return false;
            }

            return passiveEffectLifecycle.TryApplyTimedEffect(
                character,
                effect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

    }
}
