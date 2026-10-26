using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class ModifyStatSystem
    {
        private readonly Action<string> battleLog;
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;

        public ModifyStatSystem(
            Action<string> battleLog = null,
            PassiveEffectLifecycleSystem passiveEffectLifecycleSystem = null)
        {
            this.battleLog = battleLog;
            this.passiveEffectLifecycleSystem = passiveEffectLifecycleSystem
                ?? new PassiveEffectLifecycleSystem();
        }

        public bool Apply(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (target?.Character?.Stats == null
                || skill == null
                || effect == null
                || effectIndex < 0)
            {
                return false;
            }

            var runtimeEffect = CreateRuntimeEffect(
                source,
                skill,
                effect,
                effectIndex);

            var wasApplied = runtimeEffect.Lifetime == PassiveEffectLifetime.CombatScoped
                ? passiveEffectLifecycleSystem.TryApplyCombatScopedEffect(
                    target.Character,
                    runtimeEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndValues,
                    out _)
                : passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndValues,
                    out _);

            if (!wasApplied)
            {
                return false;
            }

            var targetName = target.Character.Definition?.DisplayName
                ?? target.Character.CharacterId;

            battleLog?.Invoke(
                $"{targetName} otrzymuje modyfikator statystyki " +
                $"{effect.ModifierStatType}.");

            return true;
        }

        public bool ApplyFromSummonedObject(
            BattleUnit source,
            BattleUnit target,
            CharacterStatType statType,
            StatModifierOperation operation,
            StatModifierStacking stacking,
            float value,
            int durationTurns,
            string sourceId)
        {
            if (target?.Character?.Stats == null || value == 0f)
            {
                return false;
            }

            var runtimeEffect = new PassiveEffectRuntime(
                sourceId,
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromRuntimeSystem(source, sourceId)
                },
                1,
                0,
                0,
                PassiveEffectLifetime.CombatScoped,
                PassiveEffectTickPhase.None)
            {
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = statType,
                        Operation = operation,
                        Stacking = stacking,
                        Value = value
                    }
                }
            };

            return passiveEffectLifecycleSystem.TryApplyCombatScopedEffect(
                target.Character,
                runtimeEffect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public bool RemoveFromSummonedObject(
            BattleUnit target,
            string sourceId)
        {
            if (target?.Character == null || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            return passiveEffectLifecycleSystem.RemoveEffect(
                target.Character,
                sourceId);
        }

        private static PassiveEffectRuntime CreateRuntimeEffect(
            BattleUnit source,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            var sources = new List<PassiveEffectSourceRuntime>
            {
                PassiveEffectSourceFactory.FromSkill(
                    source,
                    skill,
                    effectIndex)
            };

            var isTimed = !skill.IsToggle && effect.DurationTurns > 0;

            return new PassiveEffectRuntime(
                BuildEffectId(skill.SkillId, effectIndex),
                sources,
                stacks: 1,
                remainingDuration: isTimed ? effect.DurationTurns : 0,
                totalDamage: 0,
                tickPhase: isTimed ? PassiveEffectTickPhase.TurnEnd : PassiveEffectTickPhase.None,
                lifetime: isTimed ? PassiveEffectLifetime.Timed : PassiveEffectLifetime.CombatScoped)
            {
                DisplayName = skill.DisplayName,
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = effect.ModifierStatType,
                        Operation = effect.ModifierOperation,
                        Stacking = effect.ModifierStacking,
                        Value = effect.ModifierValue
                    }
                }
            };
        }

        private static string BuildEffectId(
            string skillId,
            int effectIndex)
        {
            return $"{skillId}.modify_stat.{effectIndex}";
        }
    }

    /// <summary>
    /// Applies a resistance change as a source-tracked passive effect. Keeping the
    /// modifier in the passive-effect list makes duration expiry and toggle removal
    /// deterministic instead of leaving a value behind in BattleUnit state.
    /// </summary>
    public sealed class ModifyResistanceSystem
    {
        private readonly Action<string> battleLog;
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;

        public ModifyResistanceSystem(
            Action<string> battleLog = null,
            PassiveEffectLifecycleSystem passiveEffectLifecycleSystem = null)
        {
            this.battleLog = battleLog;
            this.passiveEffectLifecycleSystem = passiveEffectLifecycleSystem
                ?? new PassiveEffectLifecycleSystem();
        }

        public bool Apply(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (target?.Character == null
                || skill == null
                || effect == null
                || effectIndex < 0
                || effect.ModifierValue == 0f)
            {
                return false;
            }

            var runtimeEffect = CreateRuntimeEffect(
                source,
                skill,
                effect,
                effectIndex);

            var wasApplied = runtimeEffect.Lifetime == PassiveEffectLifetime.CombatScoped
                ? passiveEffectLifecycleSystem.TryApplyCombatScopedEffect(
                    target.Character,
                    runtimeEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndValues,
                    out _)
                : passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndValues,
                    out _);

            if (!wasApplied)
            {
                return false;
            }

            var targetName = target.Character.Definition?.DisplayName
                ?? target.Character.CharacterId;
            battleLog?.Invoke(
                $"{targetName} otrzymuje modyfikator odporności " +
                $"{effect.ResistanceType}.");
            return true;
        }

        /// <summary>
        /// Returns raw typed and legacy modifiers, excluding base/class/equipment
        /// resistance. Use GetEffectiveResistance for gameplay calculations.
        /// The dictionary remains supported for legacy callers, while new skill
        /// effects are read directly from their active typed payloads.
        /// </summary>
        public static float GetModifier(BattleUnit unit, ResistanceType resistanceType)
        {
            var result = GetLegacyModifier(unit, resistanceType);

            foreach (var effect in unit?.Character?.PassiveEffects
                ?? new List<PassiveEffectRuntime>())
            {
                if (!PassiveEffectLifecycleSystem.IsActive(effect))
                {
                    continue;
                }

                var payload = effect.GetPayload<ResistanceModifierPayload>();
                if (payload != null && (payload.ResistanceType == resistanceType || payload.ResistanceType == ResistanceType.All))
                {
                    result += payload.Value;
                }
            }

            return result;
        }

        // Character.Resistances is an already-derived display snapshot. Resolve
        // from the immutable base and clamp the complete contribution only once.
        public static float GetEffectiveResistance(
            BattleUnit unit,
            ResistanceType type,
            float additionalModifier = 0f)
        {
            return CalculateResistance(
                unit?.Character,
                type,
                GetLegacyModifier(unit, type) + additionalModifier);
        }

        public static float CalculateResistance(
            CharacterInstance character,
            ResistanceType type,
            float combatModifier = 0f)
        {
            if (character == null) return 0f;

            var values = character.BaseResistances ?? character.Resistances;
            var result = type switch
            {
                ResistanceType.Bleed => values?.BleedResistance ?? 0f,
                ResistanceType.Poison => values?.PoisonResistance ?? 0f,
                ResistanceType.Fire => values?.FireResistance ?? 0f,
                ResistanceType.Weakening => values?.WeakeningResistance ?? 0f,
                ResistanceType.Suppression => values?.SuppressionResistance ?? 0f,
                ResistanceType.Move => values?.MoveResistance ?? 0f,
                _ => 0f
            };
            if (character.PassiveEffects != null)
            {
                foreach (var effect in character.PassiveEffects)
                {
                    if (!PassiveEffectLifecycleSystem.IsActive(effect)) continue;

                    var payload = effect.GetPayload<ResistanceModifierPayload>();
                    if (payload != null && (payload.ResistanceType == type
                        || payload.ResistanceType == ResistanceType.All))
                    {
                        result += payload.Value;
                    }
                }
            }

            if (type == ResistanceType.Fire)
            {
                result += .01f * MageCombatSystem.Points(character, MageCombatSystem.Fire);
            }

            return UnityEngine.Mathf.Clamp01(result + EquipmentEffectSystem.Resistance(character, type) + combatModifier);
        }

        private static float GetLegacyModifier(BattleUnit unit, ResistanceType type)
        {
            var result = 0f;
            if (unit?.ResistanceModifiers == null) return result;
            if (unit.ResistanceModifiers.TryGetValue(type, out var specific)) result += specific;
            if (type != ResistanceType.All && unit.ResistanceModifiers.TryGetValue(ResistanceType.All, out var all)) result += all;
            return result;
        }

        private static PassiveEffectRuntime CreateRuntimeEffect(
            BattleUnit source,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            var isTimed = !skill.IsToggle && effect.DurationTurns > 0;

            var runtimeEffect = new PassiveEffectRuntime(
                BuildEffectId(skill.SkillId, effectIndex),
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromSkill(
                        source,
                        skill,
                        effectIndex)
                },
                stacks: 1,
                remainingDuration: isTimed ? effect.DurationTurns : 0,
                totalDamage: 0,
                lifetime: isTimed
                    ? PassiveEffectLifetime.Timed
                    : PassiveEffectLifetime.CombatScoped,
                tickPhase: isTimed
                    ? PassiveEffectTickPhase.TurnEnd
                    : PassiveEffectTickPhase.None)
            {
                DisplayName = skill.DisplayName
            };
            runtimeEffect.SetPayload(new ResistanceModifierPayload
            {
                ResistanceType = effect.ResistanceType,
                Value = effect.ModifierValue
            });
            return runtimeEffect;
        }

        public static string BuildEffectId(string skillId, int effectIndex)
        {
            // Crushing Blow's suppression debuff historically occupied slot 2.
            // Preserve its identity when moving it before stun, including saved effects.
            if (skillId == "skill.paladin.crushing_blow") effectIndex = 2;
            return $"{skillId}.modify_resistance.{effectIndex}";
        }
    }
}
