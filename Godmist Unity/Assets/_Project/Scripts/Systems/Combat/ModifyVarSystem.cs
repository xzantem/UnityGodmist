using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class ModifyVarSystem
    {
        private readonly Action<string> battleLog;
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;

        public ModifyVarSystem(
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
                $"{targetName} otrzymuje modyfikator {effect.ModifierVariableType}.");

            return true;
        }

        public bool ApplyFromSummonedObject(
            BattleUnit source,
            BattleUnit target,
            GameplayVariableType variableType,
            StatModifierStacking stacking,
            float value,
            int durationTurns,
            string sourceId)
        {
            if (target?.Character == null || value == 0f)
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
                VariableModifiers = new List<GameplayVariableModifier>
                {
                    new()
                    {
                        VariableType = variableType,
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
                DisplayName = string.IsNullOrWhiteSpace(skill.DisplayName)
                    ? $"Modyfikator: {effect.ModifierVariableType}"
                    : skill.DisplayName,
                VariableModifiers = new List<GameplayVariableModifier>
                {
                    new()
                    {
                        VariableType = effect.ModifierVariableType,
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
            return $"{skillId}.modify_var.{effectIndex}";
        }
    }
}
