using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class CombatActionCostCalculator
    {
        public static int CalculateSkillCost(
            BattleUnit unit, 
            SkillData skill, bool recoveringKnife = false)
        {
            if (unit == null || skill == null)
            {
                return 0;
            }
            return Mathf.Max(0,Mathf.RoundToInt(CalculatePercentageCost(
                unit.MaxActionPoints, 
                ScoutCombatSystem.KnifeRecoveryCost(unit, skill, skill.ActionCost, recoveringKnife))
                * UniqueEquipmentCombatRules.OrderCostMultiplier(unit,skill)));
        }
        public static int CalculatePercentageCost(
            int maxActionPoints, 
            int percentage)
        {
            if (maxActionPoints <= 0 || percentage <= 0)
            {
                return 0;
            }

            var clampedPercentage = Mathf.Clamp(
                percentage, 
                0, 
                100);

            return Mathf.Max(1, Mathf.RoundToInt(
                maxActionPoints 
                * clampedPercentage 
                / 100f));
        }
        public static int CalculateMovementCost(
            BattleUnit unit)
        {
            if (unit == null)
            {
                return int.MaxValue;
            }

            if(WarriorCombatSystem.FreeStep(unit))return 0;
            return Mathf.Max(0, unit.MovementStepsThisTurn + 1 - ScoutCombatSystem.MovementDiscount(unit)) * ActionPointUnits.PerPoint;
        }
    }

    public static class ToggleSkillUpkeepCalculator
    {
        public static int Calculate(SkillData skill, ResourcePool resourcePool)
        {
            if (skill == null || !skill.IsToggle) return 0;

            var percentageCost = resourcePool == null
                ? 0
                : Mathf.CeilToInt(
                    Mathf.Max(0, resourcePool.MaxValue)
                    * Mathf.Clamp01(skill.ToggleUpkeepMaxResourcePercent));

            return Mathf.Max(
                Mathf.Max(0, skill.ToggleUpkeepFlatResourceCost),
                percentageCost);
        }
    }
}
