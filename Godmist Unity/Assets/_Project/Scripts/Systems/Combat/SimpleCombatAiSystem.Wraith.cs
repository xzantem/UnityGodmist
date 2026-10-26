using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private readonly CombatChargeSystem preparationChargePreview = new();

        // This file used to hold the Wraith exception. The pattern is now authored:
        // a status-gated, one-turn offensive buff that consumes its setup status.
        private static bool TryGetTimedPreparation(BattleUnit unit, out SkillData preparation, out SkillData[] attacks)
        {
            var skills = HasBossAiOwner(unit) ? null : unit?.Character?.GetActiveSkills();
            preparation = skills?.FirstOrDefault(skill => skill != null && IsShortOffensivePreparation(skill)
                && !string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                && StatusEffectSystem.HasStatus(unit, skill.RequiredCasterStatusId)
                && skill.Effects.Any(effect => effect?.EffectType == EffectType.RemoveStatus
                    && string.Equals(effect.StatusId, skill.RequiredCasterStatusId, StringComparison.OrdinalIgnoreCase)
                    && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Caster));
            var setup = preparation;
            attacks = setup == null ? Array.Empty<SkillData>() : skills.Where(skill => skill != null && skill != setup
                && skill.Effects.Any(effect => effect?.EffectType == EffectType.DealDamage
                    && skill.TargetModes[effect.TargetModeIndex].TargetingType == TargetingType.Enemies
                    && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Single)
                && !string.Equals(skill.RequiredCasterStatusId, setup.RequiredCasterStatusId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return setup != null && attacks.Length > 0;
        }

        private bool ShouldUseTimedPreparationSkill(CombatSession session, BattleUnit unit, SkillData skill)
        {
            if (!TryGetTimedPreparation(unit, out var preparation, out var attacks)
                || skill != preparation && !attacks.Contains(skill)) return true;
            if (session.CanUseBossSkill?.Invoke(unit, preparation) == false) return skill != preparation;
            var emergency = CanAffordSkill(unit, preparation) && IsEmergencyPreparationHeal(session, unit, preparation);
            var ready = attacks.Any(attack => CanPayPreparationFollowUp(unit, preparation, attack)
                && unit.CurrentActionPoints >= CombatActionCostCalculator.CalculateSkillCost(unit, preparation)
                    + CombatActionCostCalculator.CalculateSkillCost(unit, attack)
                && session.Units.Any(target => CanPreparedAttackFrom(session, unit, attack, target, unit.Position)));
            if (skill == preparation) return emergency || ready;
            if (emergency || ready) return false;
            return !attacks.Any(attack => CanPayPreparationFollowUp(unit, preparation, attack))
                || !unit.Character.PassiveEffects.Any(effect => effect != null
                    && string.Equals(effect.EffectId, preparation.RequiredCasterStatusId, StringComparison.OrdinalIgnoreCase)
                    && effect.RemainingDuration > 1 && PassiveEffectLifecycleSystem.IsActive(effect));
        }

        private static bool CanPayPreparationFollowUp(BattleUnit unit, SkillData preparation, SkillData attack)
        {
            var pool = unit.Character.ResourcePool;
            var setupCost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, preparation);
            var attackCost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, attack);
            return CombatActionCostCalculator.CalculateSkillCost(unit, preparation)
                    + CombatActionCostCalculator.CalculateSkillCost(unit, attack) <= unit.MaxActionPoints
                && (setupCost <= 0 || pool?.ResourceType == preparation.ResourceType)
                && (attackCost <= 0 || pool?.ResourceType == attack.ResourceType)
                && (pool?.CurrentValue ?? 0) >= setupCost + attackCost;
        }

        private bool CanPreparedAttackFrom(CombatSession session, BattleUnit unit, SkillData attack, BattleUnit target, GridPosition origin)
        {
            if (target?.Character?.LifeState != CharacterLifeState.Alive
                || target.IsPlayerSide == unit.IsPlayerSide || StatusEffectSystem.IsInvisible(target)
                || session.Contact != null && !session.Contact.Sees(unit, target)
                || !string.IsNullOrWhiteSpace(attack.RequiredCasterStatusId) && !StatusEffectSystem.HasStatus(unit, attack.RequiredCasterStatusId)
                || session.CanUseBossSkill?.Invoke(unit, attack) == false
                || StatusEffectSystem.TryGetTaunter(session, unit, out var taunter) && target != taunter) return false;
            foreach (var effect in attack.Effects)
            {
                if (effect?.EffectType != EffectType.DealDamage) continue;
                var mode = PassiveEffectCombatExecutionSystem.ResolveMode(unit, attack, attack.TargetModes[effect.TargetModeIndex]);
                if (mode.TargetType != TargetType.Single || mode.TargetingType != TargetingType.Enemies) continue;
                if (!string.IsNullOrWhiteSpace(mode.RequiredStatusId) && !StatusEffectSystem.HasStatus(target, mode.RequiredStatusId)) continue;
                if (!CombatTargetingService.IsPositionWithinRange(origin, target.Position, mode.AbsoluteRange)
                    || attack.RequiresLineOfSight && !lineOfSightService.HasLineOfSight(session, origin, target.Position)) continue;
                if (attack.Effects.Any(charge => charge?.EffectType == EffectType.Charge
                    && !preparationChargePreview.CanStartCharge(session, unit, target, charge, origin))) continue;
                return true;
            }
            return false;
        }

        private static bool IsEmergencyPreparationHeal(CombatSession session, BattleUnit unit, SkillData skill)
        {
            var maximum = unit.Character.Stats.MaxHealth.GetValue();
            var healing = skill.Effects.Where(effect => effect != null
                    && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Caster
                    && SkillExecutionSystem.CanPlanEffect(session, effect, unit, unit))
                .Sum(effect => effect.EffectType == EffectType.HealPercent ? Mathf.CeilToInt(maximum * Mathf.Clamp01(effect.PercentageValue))
                    : effect.EffectType == EffectType.Heal ? CombatRecoverySystem.CalculateSkillHealing(unit, effect) : 0);
            return unit.Character.CurrentHealth < maximum && healing >= unit.Character.CurrentHealth;
        }
    }
}
