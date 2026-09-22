using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed partial class PassiveEffectCombatExecutionSystem
    {
        // Pure queries: never roll a proc, spend resources or mutate a shared SkillData.
        public static int ResolveResourceCost(BattleUnit owner, SkillData skill)
        {
            if (skill == null) return 0;
            return Mathf.Max(0, Mathf.CeilToInt(ResolveParameter(owner, skill,
                "resource_cost", skill.ResourceCost) - .00001f));
        }

        public static SkillEffectData ResolveEffect(BattleUnit owner, SkillData skill, SkillEffectData effect)
        {
            if (effect == null) return null;
            var index = -1;
            for (var i = 0; skill != null && i < skill.Effects.Count; i++) if (ReferenceEquals(skill.Effects[i], effect)) { index = i; break; }
            effect = ScoutCombatSystem.ResolveEffect(owner, skill, effect);
            return effect.WithRuntimeParameters(
                Mathf.RoundToInt(ResolveParameter(owner, skill, "hit_count", effect.DamageHitCount, effectIndex: index)),
                Mathf.RoundToInt(ResolveParameter(owner, skill, "duration_turns", effect.DurationTurns, effectIndex: index)),
                ResolveParameter(owner, skill, "status_chance", effect.BaseChance, effectIndex: index))
                .WithAdditionalHitTrials(Mathf.RoundToInt(ResolveParameter(owner, skill, "additional_hit_trials", effect.AdditionalHitTrials, effectIndex: index)));
        }

        public static SkillTargetModeData ResolveMode(BattleUnit owner, SkillData skill, SkillTargetModeData mode)
        {
            if (mode == null) return null;
            var index = -1;
            for (var i = 0; skill != null && i < skill.TargetModes.Count; i++) if (ReferenceEquals(skill.TargetModes[i], mode)) { index = i; break; }
            var extra = skill?.SkillId == "skill.scout.ricochet" ? ScoutCombatSystem.AssassinRank(owner,"farsight") switch {1=>1,2=>3,_=>0} : 0;
            return mode.WithChainTargets(mode.ChainTargets+(skill?.SkillId == "skill.scout.ricochet" ? 2*ScoutCombatSystem.AssassinRank(owner,"knife_bounce") : 0)).WithRuntimeRanges(
                Mathf.RoundToInt(ResolveParameter(owner, skill, "range", mode.AbsoluteRange.MaxRange + extra, modeIndex: index)),
                Mathf.RoundToInt(ResolveParameter(owner, skill, "area_range", mode.RelativeRange.MaxRange + extra, modeIndex: index)));
        }

        public static float ResolveParameter(BattleUnit owner, SkillData skill,
            string parameter, float baseValue, int effectIndex = -1, int modeIndex = -1)
        {
            if (owner?.Character == null || skill == null) return baseValue;
            var context = new PassiveEffectCombatContext(source: owner, target: owner,
                skill: skill, skillId: skill.SkillId, attackType: skill.AttackType,
                resourceType: skill.ResourceType, resourceAmount: skill.ResourceCost);
            float flat = 0, percent = 0, multiplier = 1;
            foreach (var runtime in owner.Character.PassiveEffects ?? new List<PassiveEffectRuntime>())
            {
                if (!PassiveEffectLifecycleSystem.IsActive(runtime)
                    || !TryResolveDefinition(owner, runtime.DefinitionId, out var definition)) continue;
                var result = PassiveEffectCombatEvaluator.Evaluate(definition, runtime,
                    new PassiveEffectCombatEvent(PassiveEffectTriggerType.OnAttack, context));
                if (!result.CanApply) continue;
                foreach (var operation in result.Operations)
                {
                    if (operation == null || !TargetsOwner(operation.Target)) continue;
                    if (!MatchesIndex(operation, "effect_index", effectIndex)
                        || !MatchesIndex(operation, "target_mode_index", modeIndex)) continue;
                    var cost = operation.Type == PassiveEffectOperationType.ModifyResourceCost
                        && parameter == "resource_cost";
                    if (!cost && (operation.Type != PassiveEffectOperationType.ModifySkillParameter
                        || !string.Equals(operation.ReferenceId, parameter, StringComparison.OrdinalIgnoreCase))) continue;
                    if (HasParameter(operation, "scope", "offensive")
                        && !IsOffensiveSkill(skill)) continue;
                    var value = ResolveValue(operation.Value, runtime);
                    if (operation.ModifierOperation == StatModifierOperation.Percent)
                    {
                        if (operation.ModifierStacking == StatModifierStacking.Multiplicative)
                            multiplier *= Mathf.Max(0, 1 + value);
                        else percent += value;
                    }
                    else flat += value;
                }
            }
            return Mathf.Max(0, (baseValue + flat) * Mathf.Max(0, 1 + percent) * multiplier);
        }
        public static bool IsOffensiveSkill(SkillData skill) => skill?.Effects != null
            && skill.TargetModes != null && skill.Effects.Any(e => e != null
                && e.TargetModeIndex >= 0 && e.TargetModeIndex < skill.TargetModes.Count
                && skill.TargetModes[e.TargetModeIndex] != null
                && skill.TargetModes[e.TargetModeIndex].TargetType != TargetType.Caster
                && skill.TargetModes[e.TargetModeIndex].TargetingType == TargetingType.Enemies);

        private static bool MatchesIndex(PassiveEffectOperationDefinition operation, string key, int actual)
        {
            foreach (var p in operation.Parameters ?? new List<PassiveEffectParameterDefinition>())
                if (p != null && p.Key == key && p.IntegerValue != actual) return false;
            return true;
        }
    }
}
