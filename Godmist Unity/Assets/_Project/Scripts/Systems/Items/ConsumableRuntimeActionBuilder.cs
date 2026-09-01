using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    /// <summary>Builds a transient combat action from an instance payload.</summary>
    public static class ConsumableRuntimeActionBuilder
    {
        public static SkillData Build(ConsumableItemInstance item)
        {
            if (item == null) return null;
            var runtimeEffects = new List<ConsumableEffectRuntime>(item.RuntimeEffects ?? new List<ConsumableEffectRuntime>());
            if (runtimeEffects.Count == 0
                && ConsumableRuntimeRules.TryGetImpliedUtilityEffect(
                    item.ConsumableDefinition,
                    out var impliedUtilityEffect))
            {
                runtimeEffects.Add(impliedUtilityEffect);
            }
            if (runtimeEffects.Count == 0) return null;

            var skill = ScriptableObject.CreateInstance<SkillData>();
            Set(skill, "skillId", $"runtime.consumable.{item.InstanceId}");
            Set(skill, "displayName", item.DisplayName);
            Set(skill, "actionCost", 20);
            var modes = new List<SkillTargetModeData>();
            var firstEffect = runtimeEffects[0];
            var mode = new SkillTargetModeData();
            // Normalize legacy runtime payloads as well as newly-created ones.
            // Potions are always selected on a living ally/caster, while bombs
            // select an enemy tile; old saves may still contain the historical
            // Caster default in their effect payload.
            var useType = item.ConsumableDefinition?.UseType ?? ConsumableUseType.Utility;
            var targetType = useType == ConsumableUseType.Potion
                ? TargetType.Single
                : useType == ConsumableUseType.Bomb
                    ? TargetType.AreaAroundTile
                    : firstEffect.TargetType;
            var targetingType = useType == ConsumableUseType.Potion
                ? TargetingType.Allies
                : useType == ConsumableUseType.Bomb
                    ? TargetingType.Enemies
                    : firstEffect.TargetingType;
            Set(mode, "targetType", targetType);
            Set(mode, "targetingType", targetingType);
            Set(mode, "includesCaster", targetingType == TargetingType.Allies);
            if (targetType == TargetType.Single)
            {
                // A single-target consumable is allowed to target the caster and
                // adjacent allies. The caster's tile is occupied, so targeting
                // service treats it specially instead of requiring walkability.
                Set(mode, "absoluteRange", new SkillRangeData(RangeShape.Square, 0, 1));
            }
            if (targetType == TargetType.AreaAroundTile)
            {
                var range = item.IsBomb
                    ? item.BlastRange
                    : ConsumableItemInstance.DefaultBombRange;
                var areaSize = item.IsBomb
                    ? item.BlastAreaSize
                    : ConsumableItemInstance.DefaultBombAreaSize;
                range = Mathf.Clamp(range, ConsumableItemInstance.DefaultBombRange, ConsumableItemInstance.MaxBombRange);
                areaSize = Mathf.Clamp(areaSize, ConsumableItemInstance.DefaultBombAreaSize, ConsumableItemInstance.MaxBombAreaSize);

                // Absolute range is the distance to the selected tile. The
                // relative range is a centered square whose side length is
                // the complete blast footprint (3 means 3x3, 5 means 5x5).
                Set(mode, "absoluteRange", new SkillRangeData(RangeShape.Radial, 0, range));
                Set(mode, "relativeRange", new SkillRangeData(RangeShape.Square, 0, areaSize - 1));
            }
            modes.Add(mode);
            Set(skill, "targetModes", modes);
            var effects = new List<SkillEffectData>();
            var processed = new HashSet<ConsumableEffectRuntime>();
            foreach (var runtime in runtimeEffects)
            {
                // Return scrolls are exploration actions, not combat effects;
                // keep them out of the transient SkillData payload.
                if (runtime == null
                    || runtime.EffectType == ConsumableRuntimeEffectType.ReturnToArungard
                    || !processed.Add(runtime)) continue;

                // A runtime payload may contain the same effect more than once.
                // Resolve that as one stronger effect instead of emitting several
                // independent modifiers/statuses/damage instances. This keeps
                // duplicate-efficiency rules meaningful and prevents damage
                // mitigation from being calculated separately for every hit.
                var rawValue = 0f;
                var occurrenceCount = 0;
                var durationTurns = 0;
                foreach (var candidate in runtimeEffects)
                {
                    if (!ConsumableRuntimeRules.CanCombineEffects(runtime, candidate)) continue;
                    processed.Add(candidate);
                    rawValue += Mathf.Max(0f, candidate.Value);
                    occurrenceCount++;
                    durationTurns = Mathf.Max(durationTurns, candidate.DurationTurns);
                }

                var effectiveValue = rawValue
                    * ConsumableRuntimeRules.GetDuplicateMultiplier(occurrenceCount)
                    * Mathf.Max(0f, item.CatalystEffectMultiplier);
                var effect = new SkillEffectData();
                var skillEffectType = runtime.EffectType switch
                {
                    ConsumableRuntimeEffectType.RestoreHealth => EffectType.HealPercent,
                    ConsumableRuntimeEffectType.RegenHealth => EffectType.HealOverTimePercent,
                    ConsumableRuntimeEffectType.RestoreResource => EffectType.RestoreResourcePercent,
                    ConsumableRuntimeEffectType.RegenResource => EffectType.RestoreResourceOverTimePercent,
                    ConsumableRuntimeEffectType.ModifyActionPoints => EffectType.ModifyActionPointsPercent,
                    ConsumableRuntimeEffectType.ModifyDamage => EffectType.ModifyVar,
                    ConsumableRuntimeEffectType.DealPhysicalDamage
                        or ConsumableRuntimeEffectType.DealMagicalDamage
                        or ConsumableRuntimeEffectType.DealInevitableDamage => EffectType.DealDamage,
                    ConsumableRuntimeEffectType.ModifyResistance => EffectType.ModifyResistance,
                    ConsumableRuntimeEffectType.ApplyStatus
                        or ConsumableRuntimeEffectType.ApplyBleed
                        or ConsumableRuntimeEffectType.ApplyPoison
                        or ConsumableRuntimeEffectType.ApplyBurn
                        or ConsumableRuntimeEffectType.ApplyRegeneration
                        or ConsumableRuntimeEffectType.ApplyStun
                        or ConsumableRuntimeEffectType.ApplyFreeze
                        or ConsumableRuntimeEffectType.ApplySleep
                        or ConsumableRuntimeEffectType.ApplyChill
                        or ConsumableRuntimeEffectType.ApplyParalysis
                        or ConsumableRuntimeEffectType.ApplyImmobilize
                        or ConsumableRuntimeEffectType.ApplyInvisibility
                        or ConsumableRuntimeEffectType.ApplyTaunt => EffectType.ApplyStatus,
                    ConsumableRuntimeEffectType.RemoveStatus => EffectType.RemoveStatus,
                    ConsumableRuntimeEffectType.DealDamage => EffectType.DealDamage,
                    _ => EffectType.ModifyStat
                };
                Set(effect, "effectType", skillEffectType);
                // Consumables do not perform an attack roll. Status chance and
                // damage hit resolution remain separate mechanics; recovery and
                // modifier effects must never report a misleading MISS.
                Set(effect, "alwaysHits", skillEffectType != EffectType.DealDamage);
                Set(effect, "polarity",
                    useType == ConsumableUseType.Bomb
                        ? EffectPolarity.Negative
                        : EffectPolarity.Positive);
                Set(effect, "baseValue", Mathf.CeilToInt(Mathf.Max(0f, effectiveValue)));
                Set(effect, "percentageValue", Mathf.Clamp01(Mathf.Max(0f, effectiveValue)));
                Set(effect, "targetModeIndex", Mathf.Max(0, runtime.TargetModeIndex));
                Set(effect, "statusId", string.IsNullOrWhiteSpace(runtime.StatusId)
                    ? ConsumableRuntimeRules.GetImpliedStatusId(runtime.EffectType)
                    : runtime.StatusId);
                var duration = ConsumableRuntimeRules.GetDuration(
                    useType,
                    runtime.EffectType,
                    durationTurns);
                if (duration > 0)
                {
                    duration = Mathf.Max(1, duration + item.CatalystDurationModifier);
                }
                Set(effect, "durationTurns", duration);
                var chance = runtime.EffectType is
                    ConsumableRuntimeEffectType.ApplyStun
                    or ConsumableRuntimeEffectType.ApplyFreeze
                    ? Mathf.Clamp01(effectiveValue)
                    : Mathf.Clamp01(runtime.Chance);
                Set(effect, "baseChance", chance);
                if (skillEffectType == EffectType.DealDamage)
                {
                    Set(effect, "damageType", GetDamageType(runtime.EffectType));
                    Set(effect, "damageMultiplier", Mathf.Max(0f, effectiveValue));
                }
                if (skillEffectType == EffectType.ModifyStat)
                {
                    Set(effect, "modifierStatType", GetModifierStat(runtime.EffectType));
                    Set(effect, "modifierOperation", runtime.EffectType == ConsumableRuntimeEffectType.ModifyDodge
                        ? StatModifierOperation.FlatAbsolute
                        : StatModifierOperation.Percent);
                    Set(effect, "modifierValue", GetSignedModifierValue(
                        item,
                        runtime.EffectType,
                        effectiveValue));
                }
                if (skillEffectType == EffectType.ModifyVar)
                {
                    Set(effect, "modifierVariableType", GameplayVariableType.DamageDealtPercent);
                    Set(effect, "modifierValue", effectiveValue);
                    Set(effect, "modifierStacking", StatModifierStacking.Additive);
                }
                if (skillEffectType == EffectType.ModifyResistance)
                {
                    Set(effect, "resistanceType", runtime.ResistanceType);
                    Set(effect, "modifierValue", effectiveValue);
                }
                if (skillEffectType == EffectType.ApplyStatus
                    && ConsumableRuntimeRules.IsDamageOverTime(
                        string.IsNullOrWhiteSpace(runtime.StatusId)
                            ? ConsumableRuntimeRules.GetImpliedStatusId(runtime.EffectType)
                            : runtime.StatusId))
                {
                    Set(effect, "statusDamageMultiplier", Mathf.Max(0f, effectiveValue));
                }
                effects.Add(effect);
            }
            Set(skill, "effects", effects);
            return skill;
        }

        private static void Set(object target, string field, object value)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var info = target.GetType().GetField(field, flags)
                ?? target.GetType().GetField($"<{field}>k__BackingField", flags);
            info ??= target.GetType().GetField(
                $"<{char.ToUpperInvariant(field[0])}{field.Substring(1)}>k__BackingField",
                flags);
            info?.SetValue(target, value);
        }

        private static CharacterStatType GetModifierStat(ConsumableRuntimeEffectType effectType)
        {
            return effectType switch
            {
                ConsumableRuntimeEffectType.ModifyMaxResource => CharacterStatType.MaxResource,
                ConsumableRuntimeEffectType.ModifyPhysicalDefense
                    or ConsumableRuntimeEffectType.ModifyDefense => CharacterStatType.PhysicalDefense,
                ConsumableRuntimeEffectType.ModifyMagicalDefense => CharacterStatType.MagicalDefense,
                ConsumableRuntimeEffectType.ModifySpeed => CharacterStatType.Speed,
                ConsumableRuntimeEffectType.ModifyCritChance => CharacterStatType.CritChance,
                ConsumableRuntimeEffectType.ModifyDodge => CharacterStatType.Dodge,
                ConsumableRuntimeEffectType.ModifyAccuracy => CharacterStatType.WeaponAccuracy,
                ConsumableRuntimeEffectType.Slow => CharacterStatType.Speed,
                _ => CharacterStatType.MaxHealth
            };
        }

        private static float GetSignedModifierValue(
            ConsumableItemInstance item,
            ConsumableRuntimeEffectType effectType,
            float value)
        {
            if (item?.ConsumableDefinition?.UseType != ConsumableUseType.Bomb)
            {
                return value;
            }

            return effectType is
                ConsumableRuntimeEffectType.ModifyPhysicalDefense
                or ConsumableRuntimeEffectType.ModifyMagicalDefense
                or ConsumableRuntimeEffectType.ModifyDefense
                or ConsumableRuntimeEffectType.ModifySpeed
                or ConsumableRuntimeEffectType.ModifyCritChance
                or ConsumableRuntimeEffectType.ModifyDodge
                or ConsumableRuntimeEffectType.ModifyAccuracy
                or ConsumableRuntimeEffectType.Slow
                ? -Mathf.Abs(value)
                : value;
        }

        private static DamageType GetDamageType(ConsumableRuntimeEffectType effectType)
        {
            return effectType switch
            {
                ConsumableRuntimeEffectType.DealMagicalDamage => DamageType.Magical,
                ConsumableRuntimeEffectType.DealInevitableDamage => DamageType.Inevitable,
                _ => DamageType.Physical
            };
        }

    }
}
