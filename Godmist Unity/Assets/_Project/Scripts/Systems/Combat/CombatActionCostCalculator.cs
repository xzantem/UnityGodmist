using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class CombatActionCostCalculator
    {
        public static int CalculateSkillCost(
            BattleUnit unit, 
            SkillData skill)
        {
            if (unit == null || skill == null)
            {
                return 0;
            }
            return CalculatePercentageCost(
                unit.MaxActionPoints, 
                skill.ActionCost);
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

            return Mathf.CeilToInt(
                maxActionPoints 
                * clampedPercentage 
                / 100f);
        }
        public static int CalculateMovementCost(
            BattleUnit unit)
        {
            if (unit == null)
            {
                return int.MaxValue;
            }

            return unit.MovementStepsThisTurn + 1;
        }
    }
}