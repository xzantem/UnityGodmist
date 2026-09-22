using System;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class ResourceRuleSystem
    {
        private readonly Action<string> battleLog;
        private readonly ResourceRuleRegistry registry = new();
        private readonly CombatFeedbackBus feedbackBus;

        public ResourceRuleSystem(
            Action<string> battleLog = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.feedbackBus = feedbackBus;
        }

        public void ProcessTurnStart(BattleUnit unit)
        {
            // Missing-mana regeneration must be recalculated for every tick, including exploration.
            if (MageCombatSystem.W(unit, "energy_saving") > 0)
                new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(unit.Character);
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
            if (IsResourceGainBlocked(unit)) return;

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
            if (IsResourceGainBlocked(unit)) return;

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

            var previousValue = pool.CurrentValue;
            var previousHasStartedTurn = pool.HasStartedTurn;
            var previousHasGainedResourceSinceLastTurn =
                pool.HasGainedResourceSinceLastTurn;
            var previousFractionalGainRemainder = pool.FractionalGainRemainder;
            var change = action(rule, pool);

            if (change <= 0 || !IsResourceGainBlocked(unit))
            {
                return change;
            }

            // Rules may mutate auxiliary state even when the pool is already full
            // (Rage is the important case). Restore the entire relevant pool state,
            // so a blocked action never counts as a gain either.
            pool.CurrentValue = previousValue;
            pool.HasStartedTurn = previousHasStartedTurn;
            pool.HasGainedResourceSinceLastTurn =
                previousHasGainedResourceSinceLastTurn;
            pool.FractionalGainRemainder = previousFractionalGainRemainder;
            return 0;
        }

        private static bool IsResourceGainBlocked(BattleUnit unit)
        {
            return unit?.Character?.PassiveEffects != null
                && unit.Character.PassiveEffects.Exists(effect =>
                    PassiveEffectLifecycleSystem.IsActive(effect)
                    && effect.GetPayload<ResourceRegenerationBlockPayload>() != null);
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

            feedbackBus?.Publish(new CombatFeedbackEvent(
                unit,
                change > 0
                    ? CombatFeedbackType.ResourceGain
                    : CombatFeedbackType.ResourceSpend,
                Math.Abs(change),
                resourceType: unit.Character.ResourcePool.ResourceType));
        }
    }
}
