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
        private SkillExecutionSystem rageOpportunityValidator;

        public ResourceRuleSystem(
            Action<string> battleLog = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.feedbackBus = feedbackBus;
        }

        public void ProcessTurnStart(BattleUnit unit)
        {
            if (unit != null)
            {
                unit.WarriorState.RageOffensiveActionUsed = false;
                unit.WarriorState.RageHadOffensiveOpportunity = false;
                unit.WarriorState.RageControlBlocked = false;
            }
            EquipmentEffectSystem.Tick(unit);
            RefreshManaRegeneration(unit);
            var change = Execute(
                unit,
                (rule, pool) => UsesActionPointManaRegeneration(unit) ? 0 : rule.ProcessTurnStart(pool));

            if (change > 0)
            {
                LogChange(unit, change, "regeneruje");
            }
            else if (change < 0)
            {
                LogChange(unit, change, "traci");
            }
        }

        public static bool UsesActionPointManaRegeneration(BattleUnit unit) =>
            unit?.IsPlayerControlled == true
            && unit.Character?.ResourcePool?.ResourceType == Core.Enums.ResourceType.Mana;

        private static void RefreshManaRegeneration(BattleUnit unit)
        {
            if (MageCombatSystem.W(unit, "energy_saving") > 0)
                new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(unit.Character);
        }

        public void ProcessTurnEnd(BattleUnit unit, CombatSession session = null)
        {
            if (unit?.Character?.LifeState == Core.Enums.CharacterLifeState.Alive
                && unit.Character.ResourcePool?.ResourceType == Core.Enums.ResourceType.Rage)
            {
                RecordRageOffensiveOpportunity(session, unit);
                var rageChange = new RageResourceRule().ProcessTurnEnd(unit.Character.ResourcePool, unit.WarriorState);
                if (rageChange < 0) LogChange(unit, rageChange, "traci");
                WarriorCombatSystem.Refresh(unit);
            }
            if (unit?.Character?.LifeState == Core.Enums.CharacterLifeState.Alive
                && unit.Character.ResourcePool?.ResourceType == Core.Enums.ResourceType.Momentum
                && unit.MovementStepsThisTurn == 0)
            {
                var previous = unit.Character.ResourcePool.CurrentValue;
                var lost = unit.Character.ResourcePool.Drain(10);
                if (lost > 0)
                {
                    MomentumResourceRule.RefreshSpeedAfterChange(unit.Character, previous);
                    LogChange(unit, -lost, "traci");
                }
            }
            if (!UsesActionPointManaRegeneration(unit)) return;
            RefreshManaRegeneration(unit);
            var change = Execute(unit, (rule, pool) => rule is ManaResourceRule mana
                ? mana.ProcessTurnEnd(pool, unit.CurrentActionPoints, unit.MaxActionPoints) : 0);
            if (change > 0) LogChange(unit, change, "regeneruje");
        }

        public void ProcessExplorationTick(BattleUnit unit)
        {
            if (unit?.Character?.LifeState != Core.Enums.CharacterLifeState.Alive) return;
            var pool = unit.Character.ResourcePool;
            if (pool == null) return;
            if (pool.ResourceType == Core.Enums.ResourceType.Mana)
            {
                // Waiting outside combat cannot refill mana. Keep equipment HP
                // recovery, while explicit consumable/camp recovery has its own owner.
                EquipmentEffectSystem.Tick(unit, allowResourceRecovery: false);
            }
            else if (pool.ResourceType == Core.Enums.ResourceType.Rage)
            {
                unit.WarriorState.RageIdleTurns = 0;
                var lost = pool.Drain(Godmist.Data.Configuration.GameDefinesRuntime.GetInt(
                    Godmist.Data.Configuration.GameDefineIds.RageDecayPerMissedTurn, 10));
                pool.HasGainedResourceSinceLastTurn = false;
                if (lost > 0) LogChange(unit, -lost, "traci");
                WarriorCombatSystem.Refresh(unit);
            }
        }

        public void ProcessUnitMoved(
            BattleUnit unit,
            int spentActionPoints,
            int completedSteps = 1)
        {
            if (IsResourceGainBlocked(unit)) return;

            var change = Execute(
                unit,
                (rule, pool) => rule is MomentumResourceRule momentum
                    ? momentum.ProcessMovementSteps(pool, unit.MovementStepsThisTurn, completedSteps)
                    : rule.ProcessMovement(pool, spentActionPoints));

            if (change > 0)
            {
                LogChange(unit, change, "zyskuje");
            }
        }

        public void ProcessOffensiveSkillUsed(BattleUnit unit, bool generateResource = true)
        {
            // An attempted attack counts even when recovery is blocked or the
            // resource pool is full. Resource generation is a separate rule.
            if (unit?.Character?.ResourcePool?.ResourceType == Core.Enums.ResourceType.Rage)
            {
                unit.WarriorState.RageOffensiveActionUsed = true;
                unit.WarriorState.RageIdleTurns = 0;
            }
            if (!generateResource || IsResourceGainBlocked(unit)) return;

            var change = Execute(
                unit,
                (rule, pool) => rule.ProcessOffensiveSkillUsed(pool));

            if (change > 0)
            {
                LogChange(unit, change, "zyskuje");
            }
            WarriorCombatSystem.Refresh(unit);
        }

        public void RecordRageOffensiveOpportunity(CombatSession session, BattleUnit unit)
        {
            if (unit?.Character?.ResourcePool?.ResourceType != Core.Enums.ResourceType.Rage
                || unit.Character.LifeState != Core.Enums.CharacterLifeState.Alive) return;
            var state = unit.WarriorState;
            if (StatusEffectSystem.IsHardCrowdControlled(unit)) { state.RageControlBlocked = true; return; }
            if (!StatusEffectSystem.IsImmobilized(unit)) { state.RageHadOffensiveOpportunity = true; return; }
            state.RageControlBlocked = true;
            if (state.RageHadOffensiveOpportunity || session == null) return;
            rageOpportunityValidator ??= new SkillExecutionSystem(resourceRuleSystem: this);
            state.RageHadOffensiveOpportunity = rageOpportunityValidator.HasAvailableOffensiveAction(session, unit);
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
            if (change > 0 && !IsResourceGainBlocked(unit))
                change += pool.Restore(PeriodicRecoveryPassiveEffectProcessor.GetRecoveryAmount(change * (EquipmentEffectSystem.ResourceMultiplier(unit.Character) - 1), ref unit.Character.EquipmentGenerationCarry));

            if (change <= 0 || !IsResourceGainBlocked(unit))
            {
                MomentumResourceRule.RefreshSpeedAfterChange(unit.Character, previousValue);
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

        public static bool IsResourceGainBlocked(BattleUnit unit)
            => IsResourceGainBlocked(unit?.Character);

        public static bool IsResourceGainBlocked(Godmist.Runtime.Characters.CharacterInstance character)
        {
            return character?.PassiveEffects != null
                && character.PassiveEffects.Exists(effect =>
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
