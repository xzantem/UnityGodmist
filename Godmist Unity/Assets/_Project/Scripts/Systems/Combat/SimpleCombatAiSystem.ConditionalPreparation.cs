using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private sealed class AiConditionalStatusPreparation
        {
            public SkillData Skill;
            public SkillEffectData Strong;
            public SkillEffectData Weak;
            public int StrongIndex;
            public string StatusId;
        }

        // Recognize authored self-buff pairs, not particular enemies/skill IDs.
        // More complex condition chains keep the ordinary scorer until supported.
        private static List<AiConditionalStatusPreparation> FindConditionalStatusPreparations(
            BattleUnit unit, IReadOnlyList<SkillData> skills)
        {
            var result = new List<AiConditionalStatusPreparation>();
            foreach (var skill in skills)
            {
                if (skill == null || skill.IsToggle || skill.KnifeCost > 0 || skill.ActionCost <= 0
                    || skill.Effects.Count != 2 || skill.TargetModes.Count != 1) continue;
                for (var index = 0; index < 2; index++)
                {
                    var strong = PassiveEffectCombatExecutionSystem.ResolveEffect(unit, skill, skill.Effects[index]);
                    var weak = PassiveEffectCombatExecutionSystem.ResolveEffect(unit, skill, skill.Effects[1 - index]);
                    if (strong == null || weak == null || strong.Conditions?.Count != 1 || weak.Conditions?.Count != 1
                        || strong.EffectType != weak.EffectType
                        || strong.EffectType is not (EffectType.ModifyStat or EffectType.ModifyVar)
                        || strong.ModifierStatType != weak.ModifierStatType
                        || strong.ModifierVariableType != weak.ModifierVariableType
                        || strong.ModifierOperation != weak.ModifierOperation
                        || strong.ModifierOperation == StatModifierOperation.Maximum
                        || strong.ModifierStacking != weak.ModifierStacking
                        || strong.EffectType == EffectType.ModifyVar
                            && strong.ModifierVariableType is not (GameplayVariableType.DamageDealtPercent or GameplayVariableType.HitChanceBonus)
                        || weak.ModifierValue <= 0f || strong.ModifierValue <= weak.ModifierValue
                        || strong.DurationTurns <= 0 || strong.DurationTurns != weak.DurationTurns
                        || strong.DelayCasterTurns > 0 || weak.DelayCasterTurns > 0
                        || strong.TargetModeIndex != weak.TargetModeIndex
                        || strong.TargetModeIndex >= skill.TargetModes.Count) continue;
                    var condition = strong.Conditions[0];
                    var inverse = weak.Conditions[0];
                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(unit, skill, skill.TargetModes[strong.TargetModeIndex]);
                    if (mode?.TargetType != TargetType.Caster
                        || !string.IsNullOrWhiteSpace(mode.RequiredStatusId) && !StatusEffectSystem.HasStatus(unit, mode.RequiredStatusId)
                        || condition?.ConditionType != EffectConditionType.AnyOpponentHasStatus
                        || inverse?.ConditionType != EffectConditionType.NoOpponentHasStatus
                        || string.IsNullOrWhiteSpace(condition.StatusId)
                        || !string.Equals(condition.StatusId, inverse.StatusId, StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new AiConditionalStatusPreparation
                    {
                        Skill = skill, Strong = strong, Weak = weak, StrongIndex = index, StatusId = condition.StatusId
                    });
                }
            }
            return result;
        }

        private static bool IsObservedLivingOpponent(CombatSession session, BattleUnit caster, BattleUnit target) =>
            target?.Character?.LifeState == CharacterLifeState.Alive && target.IsPlayerSide != caster.IsPlayerSide
            && !StatusEffectSystem.IsInvisible(target) && (session.Contact == null || session.Contact.Sees(caster, target));

        private static bool ShouldDeferConditionalPreparation(CombatSession session, BattleUnit caster, SkillData skill,
            List<AiConditionalStatusPreparation> preparations)
        {
            foreach (var preparation in preparations)
            {
                if (preparation.Skill != skill || SkillExecutionSystem.CanPlanEffect(session, preparation.Strong, caster, caster)) continue;
                var observed = session.Units.Where(target => IsObservedLivingOpponent(session, caster, target)).ToArray();
                if (observed.Length == 0) return true;
                // Hold the weak variant while the known kit can still establish
                // the stronger condition, including after a failed attempt.
                foreach (var opener in caster.Character.GetActiveSkills())
                foreach (var effect in opener?.Effects ?? Array.Empty<SkillEffectData>())
                    if (effect?.EffectType == EffectType.ApplyStatus
                        && string.Equals(effect.StatusId, preparation.StatusId, StringComparison.OrdinalIgnoreCase)
                        && session.CanUseBossSkill?.Invoke(caster, opener) != false
                        && (string.IsNullOrWhiteSpace(opener.RequiredCasterStatusId)
                            || StatusEffectSystem.HasStatus(caster, opener.RequiredCasterStatusId))
                        && effect.TargetModeIndex < opener.TargetModes.Count
                        && opener.TargetModes[effect.TargetModeIndex].TargetingType == TargetingType.Enemies
                        && observed.Any(target => SkillExecutionSystem.CanPlanEffect(session, effect, caster, target)
                            && StatusEffectSystem.CalculateStatusApplicationChance(caster, target, effect.StatusId, effect.BaseChance) > 0f))
                        return true;
                // If every observed opponent is immune, a weaker buff remains a
                // legitimate fallback. Do not chase an impossible prerequisite.
            }
            return false;
        }

        private float ConditionalStatusPreparationBonus(CombatSession session, BattleUnit caster, SkillData opener,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            List<AiConditionalStatusPreparation> preparations)
        {
            if (preparations.Count == 0 || opener.IsToggle || opener.KnifeCost > 0
                || opener.Effects.Any(effect => effect == null || effect.DelayCasterTurns > 0
                    || effect.EffectType is not (EffectType.DealDamage or EffectType.ApplyStatus))) return 0f;
            var bestBonus = 0f;
            foreach (var preparation in preparations)
            {
                var followUp = preparation.Skill;
                var prerequisiteMet = SkillExecutionSystem.CanPlanEffect(session, preparation.Strong, caster, caster);
                if (!opener.Effects.Any(effect => effect.EffectType == EffectType.ApplyStatus
                        && string.Equals(effect.StatusId, preparation.StatusId, StringComparison.OrdinalIgnoreCase))
                    || session.CanUseBossSkill?.Invoke(caster, followUp) == false
                    || !string.IsNullOrWhiteSpace(followUp.RequiredCasterStatusId)
                        && !StatusEffectSystem.HasStatus(caster, followUp.RequiredCasterStatusId)
                    || caster.CurrentActionPoints < CombatActionCostCalculator.CalculateSkillCost(caster, opener)
                        + CombatActionCostCalculator.CalculateSkillCost(caster, followUp)) continue;
                var suffix = preparation.Strong.EffectType == EffectType.ModifyStat ? "modify_stat" : "modify_var";
                if (PassiveEffectLifecycleSystem.HasActiveEffect(caster.Character,
                        SkillEffectIdentity.RuntimeId(followUp, preparation.StrongIndex, suffix))) continue;

                var pool = caster.Character.ResourcePool;
                var followUpCost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, followUp);
                var remaining = (pool?.CurrentValue ?? 0) - PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, opener);
                if (pool?.ResourceType == ResourceType.Rage && !ResourceRuleSystem.IsResourceGainBlocked(caster)
                    && PassiveEffectCombatExecutionSystem.IsOffensiveSkill(opener))
                {
                    // Run the existing deterministic Rage rule on a detached pool.
                    var previewPool = new ResourcePool(pool.ResourceType, remaining, pool.MaxValue, pool.Regeneration);
                    new RageResourceRule().ProcessOffensiveSkillUsed(previewPool);
                    remaining = previewPool.CurrentValue;
                }
                if (followUpCost > 0 && (pool?.ResourceType != followUp.ResourceType || remaining < followUpCost)) continue;

                var chance = 0f;
                foreach (var mode in targetsByModeIndex)
                foreach (var target in mode.Value)
                {
                    if (!IsObservedLivingOpponent(session, caster, target)) continue;
                    var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, opener, mode.Key);
                    if (string.Equals(preview.StatusId, preparation.StatusId, StringComparison.OrdinalIgnoreCase)
                        && (!preview.Damage.HasDamage || preview.Damage.Minimum < target.Character.CurrentHealth))
                        chance = Mathf.Max(chance, prerequisiteMet ? 1f : preview.StatusChance);
                }
                var cost = ActionPointUnits.ToPoints(CombatActionCostCalculator.CalculateSkillCost(caster, followUp))
                    * Ai(GameDefineIds.AiActionCostPenaltyPerPoint, .35f)
                    + followUpCost * Ai(GameDefineIds.AiResourceCostPenaltyPerPoint, .1f);
                var value = Ai(GameDefineIds.AiModifyBonus, 40f)
                    * preparation.Strong.ModifierValue / preparation.Weak.ModifierValue;
                bestBonus = Mathf.Max(bestBonus, chance * Mathf.Max(0f, value - cost));
            }
            return bestBonus;
        }
    }
}
