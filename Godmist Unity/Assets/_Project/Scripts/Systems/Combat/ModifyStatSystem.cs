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

            var wasApplied = effect.DurationTurns > 0
                ? passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndValues,
                    out _)
                : passiveEffectLifecycleSystem.TryApplyCombatScopedEffect(
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

            var isTimed = effect.DurationTurns > 0;

            return new PassiveEffectRuntime(
                BuildEffectId(skill.SkillId, effectIndex),
                sources,
                stacks: 1,
                remainingDuration: isTimed ? effect.DurationTurns : 0,
                totalDamage: 0,
                tickPhase: isTimed ? PassiveEffectTickPhase.TurnEnd : PassiveEffectTickPhase.None,
                lifetime: isTimed ? PassiveEffectLifetime.Timed : PassiveEffectLifetime.CombatScoped)
            {
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
}