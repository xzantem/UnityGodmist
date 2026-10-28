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
        public static float ResolveConditionalPenetration(BattleUnit source, BattleUnit target, DamageType type)
        {
            if (source == null || target == null || type != DamageType.Physical && type != DamageType.Magical) return 0;
            var stat = type == DamageType.Physical ? CharacterStatType.PhysicalDefensePenetration : CharacterStatType.MagicalDefensePenetration;
            var context = new PassiveEffectCombatContext(source: source, target: target, damageType: type);
            float value = 0;
            foreach (var match in FindMatchesForOwner(source, PassiveEffectTriggerType.OnAttack, context))
                foreach (var op in match.Operations)
                    if (op.Type == PassiveEffectOperationType.ModifyStat && op.StatType == stat && TargetsOwner(op.Target))
                        value += ResolveValue(op.Value, match.Runtime);
            return Mathf.Max(0, value);
        }
        public static bool TryPreventStatus(BattleUnit source, BattleUnit target, string statusId)
        {
            if (target == null) return false;
            var context = new PassiveEffectCombatContext(source: source, target: target);
            foreach (var match in FindMatchesForOwner(target, PassiveEffectTriggerType.OnStatusApplied, context))
                foreach (var op in match.Operations)
                    if (op.Type == PassiveEffectOperationType.PreventStatus && op.ReferenceId == statusId
                        && UnityEngine.Random.value < Mathf.Clamp01(ResolveValue(op.Value, match.Runtime))) return true;
            return false;
        }
        // Pure queries: never roll a proc, spend resources or mutate a shared SkillData.
        public static int ResolveResourceCost(BattleUnit owner, SkillData skill, bool recoveringKnife = false, CombatValueExplanation explanation = null)
        {
            if (skill == null) return 0;
            if (skill.SkillId == WarriorDischargeRules.SkillId) return WarriorDischargeRules.Cost(owner);
            explanation?.Add("resource_base",skill.ResourceCost);
            var parameter=ResolveParameter(owner,skill,"resource_cost",skill.ResourceCost,explanation:explanation);
            var cost=Mathf.Max(0,Mathf.CeilToInt(parameter-.00001f));
            explanation?.Add("parameter_rounding",cost);
            cost=WarriorCombatSystem.Cost(owner,skill,cost);
            explanation?.Add("warrior_cost",cost);
            cost=MageCombatSystem.Cost(owner,skill,cost);
            explanation?.Add("mage_cost",cost);
            var equipment=1-Mathf.Clamp01(EquipmentEffectSystem.Sum(owner?.Character,GalduryteEffectType.ResourceCostReduction,explanation));
            explanation?.Add("resource_equipment",equipment);
            cost=Mathf.CeilToInt(cost*equipment);
            explanation?.Add("equipment_rounding",cost);
            cost=ScoutCombatSystem.KnifeRecoveryCost(owner,skill,cost,recoveringKnife);
            explanation?.Add("resource_result",cost);
            return cost;
        }

        public static SkillEffectData ResolveEffect(BattleUnit owner, SkillData skill, SkillEffectData effect)
        {
            if (effect == null) return null;
            var index = -1;
            for (var i = 0; skill != null && i < skill.Effects.Count; i++) if (ReferenceEquals(skill.Effects[i], effect)) { index = i; break; }
            effect = MageCombatSystem.ResolveEffect(owner,skill,WarriorCombatSystem.ResolveEffect(owner,skill,ScoutCombatSystem.ResolveEffect(owner, skill, effect)));
            if(skill?.SkillId=="skill.paladin.grounding_wave")effect=effect.WithDisplacementDistance(1+PaladinCombatSystem.C(owner,"destruction_wave"));
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
            if (skill?.SkillId == "skill.mage.sacrifice") mode = mode.WithChainTargets(1 + MageCombatSystem.W(owner, "mass_sacrifice"));
            return mode.WithChainTargets(mode.ChainTargets+(skill?.SkillId == "skill.scout.ricochet" ? 2*ScoutCombatSystem.AssassinRank(owner,"knife_bounce") : 0)).WithRuntimeRanges(
                Mathf.RoundToInt(ResolveParameter(owner, skill, "range", mode.AbsoluteRange.MaxRange + extra, modeIndex: index)),
                Mathf.RoundToInt(ResolveParameter(owner, skill, "area_range", mode.RelativeRange.MaxRange + extra, modeIndex: index)));
        }

        public static float ResolveParameter(BattleUnit owner, SkillData skill,
            string parameter, float baseValue, int effectIndex = -1, int modeIndex = -1, CombatValueExplanation explanation = null)
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
                    explanation?.Add(!string.IsNullOrWhiteSpace(runtime.DisplayName)?runtime.DisplayName:runtime.DefinitionId,
                        operation.ModifierOperation==StatModifierOperation.Percent
                            ? operation.ModifierStacking==StatModifierStacking.Multiplicative
                                ? "×"+(1+value).ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))
                                : (value*100).ToString("+0.####;-0.####;0",System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))+"%"
                            : value.ToString("+0.####;-0.####;0",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
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
            && skill.Effects.Any(e => IsEnemyTargetedEffect(skill, e));

        // Cast intent, not actual HP loss: missed, absorbed and delayed damage
        // still qualifies, while pure control/debuffs only prevent Rage decay.
        public static bool IsDamagingOffensiveSkill(SkillData skill) => skill?.Effects != null
            && skill.Effects.Any(e => IsEnemyTargetedEffect(skill, e)
                && (e.EffectType == EffectType.DealDamage
                    || e.EffectType == EffectType.ApplyStatus
                    && StatusEffectSystem.IsDamageOverTimeStatus(e.StatusId)
                    && e.StatusDamageMultiplier > 0f
                    // The boss owner resolves this charged attack after two turns;
                    // the authored effect only roots the target while it charges.
                    || skill.SkillId == SkeletonSlayerBossSystem.HammerId));

        private static bool IsEnemyTargetedEffect(SkillData skill, SkillEffectData effect) => effect != null
            && skill?.TargetModes != null
            && effect.TargetModeIndex >= 0 && effect.TargetModeIndex < skill.TargetModes.Count
            && skill.TargetModes[effect.TargetModeIndex] != null
            && skill.TargetModes[effect.TargetModeIndex].TargetType != TargetType.Caster
            && skill.TargetModes[effect.TargetModeIndex].TargetingType == TargetingType.Enemies;

        private static bool MatchesIndex(PassiveEffectOperationDefinition operation, string key, int actual)
        {
            foreach (var p in operation.Parameters ?? new List<PassiveEffectParameterDefinition>())
                if (p != null && p.Key == key && p.IntegerValue != actual) return false;
            return true;
        }
    }
}
