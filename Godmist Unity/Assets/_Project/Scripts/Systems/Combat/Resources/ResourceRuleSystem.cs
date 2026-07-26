using System;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class ResourceRuleSystem
    {
        private readonly Action<string> battleLog;
        private readonly ResourceRuleRegistry registry = new();

        public ResourceRuleSystem(
            Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public void ProcessTurnStart(BattleUnit unit)
        {
            var change = Execute(
                unit,
                (rule, pool) => rule.ProcessTurnStart(pool));

            if (change > 0)
            {
                LogChange(unit, change, "regeneruje");
            }
            else if (change < 0)
            {
                LogChange(unit, change, "traci");
            }
        }

        public void ProcessUnitMoved(
            BattleUnit unit,
            int spentActionPoints)
        {
            var change = Execute(
                unit,
                (rule, pool) => rule.ProcessMovement(
                    pool,
                    spentActionPoints));

            if (change > 0)
            {
                LogChange(unit, change, "zyskuje");
            }
        }

        public void ProcessDamageDealt(BattleUnit unit)
        {
            var change = Execute(
                unit,
                (rule, pool) => rule.ProcessDamageDealt(pool));

            if (change > 0)
            {
                LogChange(unit, change, "zyskuje");
            }
        }

        private int Execute(
            BattleUnit unit,
            Func<IResourceRule, ResourcePool, int> action)
        {
            if (unit?.Character?.ResourcePool == null
                || unit.Character.LifeState
                    != Core.Enums.CharacterLifeState.Alive)
            {
                return 0;
            }

            var pool = unit.Character.ResourcePool;

            if (!registry.TryGetRule(
                    pool.ResourceType,
                    out var rule))
            {
                return 0;
            }

            return action(rule, pool);
        }

        private void LogChange(
            BattleUnit unit,
            int change,
            string action)
        {
            if (unit?.Character?.ResourcePool == null)
            {
                return;
            }

            var displayName = unit.Character.Definition == null
                ? "Jednostka"
                : unit.Character.Definition.DisplayName;

            battleLog?.Invoke(
                $"{displayName} {action} " +
                $"{Math.Abs(change)} " +
                $"{unit.Character.ResourcePool.ResourceType}.");
        }
    }
}