using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Dungeons;

namespace Godmist.Systems.Combat
{
    public sealed class ExplorationStatusEffectSystem
    {
        public const float ExplorationTickSeconds = 6f;
        public const int ExplorationMomentumCap = 50;
        private readonly CombatSession environment = new();
        private readonly HashSet<Godmist.Runtime.Characters.CharacterInstance> visitedCharacters = new();
        private bool advancing;
        private static readonly Unity.Profiling.ProfilerMarker ServicesSample = new("Godmist.Exploration.StatusServices");
        private static readonly Unity.Profiling.ProfilerMarker TickSample = new("Godmist.Exploration.StatusTick");
        private static readonly Unity.Profiling.ProfilerMarker ResourcesSample = new("Godmist.Exploration.StatusResources");
        private static readonly Unity.Profiling.ProfilerMarker StartSample = new("Godmist.Exploration.StatusTurnStart");
        private static readonly Unity.Profiling.ProfilerMarker EndSample = new("Godmist.Exploration.StatusTurnEnd");
        public void Advance(float seconds, IReadOnlyList<DungeonTrapOccupant> occupants, CombatFeedbackBus feedback = null)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f || occupants == null) return;
            // Feedback can reenter with another occupant list. A nested call
            // gets its own scratch, while ordinary ticks reuse the owner's set.
            var visited = advancing ? new HashSet<Godmist.Runtime.Characters.CharacterInstance>() : visitedCharacters;
            bool previousAdvancing = advancing; advancing = true;
            visited.Clear();
            CombatDamageSystem damage = null;
            StatusEffectSystem status = null;
            ResourceRuleSystem resources = null;
            try
            {
                for (var index = 0; index < occupants.Count; index++)
                {
                    var occupant = occupants[index];
                    var unit = occupant.Unit;
                    if (unit?.Character == null || occupant.Session != null || unit.Character.LifeState == CharacterLifeState.Dead) continue;
                    var character = unit.Character;
                    if (!visited.Add(character)) continue;
                    ClampExplorationMomentum(character);
                    ProcessMomentumIdle(character, seconds);
                    var time = character.ExplorationStatusElapsedSeconds;
                    time += seconds;
                    if (time < ExplorationTickSeconds)
                    {
                        character.ExplorationStatusElapsedSeconds = time;
                        continue;
                    }
                    using (ServicesSample.Auto())
                    {
                        damage ??= new CombatDamageSystem(feedbackBus: feedback);
                        status ??= new StatusEffectSystem(damageSystem: damage, feedbackBus: feedback);
                        resources ??= new ResourceRuleSystem(feedbackBus: feedback);
                    }
                    while (time >= ExplorationTickSeconds && character.LifeState != CharacterLifeState.Dead)
                    {
                        using var tickSample = TickSample.Auto();
                        time -= ExplorationTickSeconds;
                        if (character.LifeState == CharacterLifeState.Downed)
                        {
                            status.ProcessDownedTurn(unit);
                            continue;
                        }
                        using (ResourcesSample.Auto()) resources.ProcessExplorationTick(unit);
                        using (StartSample.Auto()) status.ProcessTurnStart(environment, unit);
                        using (EndSample.Auto()) status.ProcessTurnEnd(unit);
                        ClampExplorationMomentum(character);
                    }
                    character.ExplorationStatusElapsedSeconds = time;
                }
            }
            finally { visited.Clear(); advancing = previousAdvancing; }
        }

        public static void ClampExplorationMomentum(Godmist.Runtime.Characters.CharacterInstance character)
        {
            var pool = character?.ResourcePool;
            if (pool?.ResourceType != ResourceType.Momentum) return;
            var limit = System.Math.Min(ExplorationMomentumCap, System.Math.Max(0, pool.MaxValue));
            var previousResource = pool.CurrentValue;
            pool.CurrentValue = System.Math.Clamp(previousResource, 0, limit);
            if (pool.CurrentValue == limit) pool.FractionalGainRemainder = 0f;
            // No threshold lookup or stat refresh for an unchanged pool.
            if (previousResource != pool.CurrentValue)
                MomentumResourceRule.RefreshSpeedAfterChange(character, previousResource);
        }

        public static int ProcessMovement(Godmist.Runtime.Characters.CharacterInstance character, float distanceInCells)
        {
            if (character?.LifeState != CharacterLifeState.Alive || character.ResourcePool?.ResourceType != ResourceType.Momentum
                || !float.IsFinite(distanceInCells) || distanceInCells <= 0f) return 0;
            ClampExplorationMomentum(character);
            character.ExplorationMomentumIdleSeconds = 0f;
            // Count travelled cells, not render frames or requested input against a wall.
            var distance = character.ExplorationMomentumDistanceRemainder + distanceInCells;
            var steps = (int)System.Math.Min(int.MaxValue, System.Math.Floor(distance + .00001f));
            character.ExplorationMomentumDistanceRemainder = System.Math.Max(0f, distance - steps);
            if (steps == 0) return 0;
            var previousSteps = character.ExplorationMomentumSteps;
            character.ExplorationMomentumSteps = (int)System.Math.Min(int.MaxValue, (long)previousSteps + steps);
            var pool=character.ResourcePool;
            var room=System.Math.Min(ExplorationMomentumCap,pool.MaxValue)-pool.CurrentValue;
            if (room<=0 || ResourceRuleSystem.IsResourceGainBlocked(character)) return 0;
            var gain=MomentumResourceRule.MovementGain(character.ExplorationMomentumSteps,
                    character.ExplorationMomentumSteps - previousSteps)
                * UnityEngine.Mathf.Max(0,EquipmentEffectSystem.ResourceMultiplier(character))+pool.FractionalGainRemainder;
            var whole=gain>=room ? room : UnityEngine.Mathf.FloorToInt(gain);
            pool.FractionalGainRemainder=gain>=room ? 0f : gain-whole;
            return pool.Restore(whole);
        }

        private static void ProcessMomentumIdle(Godmist.Runtime.Characters.CharacterInstance character, float seconds)
        {
            var pool = character.ResourcePool;
            if (pool?.ResourceType != ResourceType.Momentum) return;
            var time = character.ExplorationMomentumIdleSeconds + (double)seconds;
            character.ExplorationMomentumIdleSeconds = (float)(time % ExplorationTickSeconds);
            if (time < ExplorationTickSeconds) return;
            character.ExplorationMomentumSteps = 0;
            character.ExplorationMomentumDistanceRemainder = 0f;
            pool.FractionalGainRemainder = 0f;
            var loss = (int)System.Math.Min(pool.CurrentValue, System.Math.Floor(time / ExplorationTickSeconds) * 10d);
            if (loss > 0) pool.Drain(loss);
        }

        public static void ResetMovementSeries(Godmist.Runtime.Characters.CharacterInstance character)
        {
            if (character == null) return;
            character.ExplorationMomentumIdleSeconds = 0f;
            character.ExplorationMomentumDistanceRemainder = 0f;
            character.ExplorationMomentumSteps = 0;
        }

    }
}
