using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class CombatSession
    {
        public string CombatId = string.Empty;
        public CombatOutcomeState Outcome = CombatOutcomeState.Ongoing;
        public int CurrentEscapeChancePercent = GameDefinesRuntime.GetInt(
            GameDefineIds.EscapeBaseChancePercent, 60);
        public CombatRewardResult Rewards = new();
        public bool RewardsGranted;
        public bool EscapePenaltyApplied;
        public bool HasEscapePenaltyEligibleCharacters;
        public HashSet<string> EscapePenaltyEligibleCharacterIds = new();
        public bool IsDebugSession;
        [NonSerialized] public DungeonCombatContext DungeonContext;
        public bool IsSeamless;
        public bool IsDungeonWide;
        public bool MembershipChangePending;
        [NonSerialized] public DungeonContactState Contact;
        [NonSerialized] public Action<CombatTurnParticipant> ResumeTurn;
        public readonly Dictionary<string, (string EncounterId, string EnemyId)> EnemySources = new();
        public readonly HashSet<CombatTurnParticipant> SuspendedTurns = new();
        [NonSerialized] public Action<BattleUnit> UnitEnteredCell;
        [NonSerialized] public Func<BattleUnit, IReadOnlyList<GridPosition>, float> EvaluateTrapRisk;
        [NonSerialized] public Func<IReadOnlyList<GridPosition>, string> TrapRouteState;
        public void NotifyUnitEnteredCell(BattleUnit unit) => UnitEnteredCell?.Invoke(unit);
        // Rescue eligibility is supplied by the dungeon owner, independent of UI focus.
        public bool CanReceiveReinforcements;
        public readonly Dictionary<string, BattleUnit> DepartedUnits = new();
        public readonly HashSet<BattleCellEdge> OpenExitEdges = new();
        [NonSerialized] public Func<GridPosition, bool> ExternalPositionOccupied;
        [NonSerialized] public Func<GridPosition, GridPosition, bool> EnvironmentBlocksLineOfSight;
        public bool IsExitPosition(GridPosition position) => IsSeamless && !Grid.IsWalkable(position);
        public bool CanMoveBetween(BattleUnit unit, GridPosition from, GridPosition to)
        {
            if (ExternalPositionOccupied?.Invoke(to) == true) return false;
            if (Grid.CanMoveBetween(from, to)) return true;
            return IsSeamless && unit?.IsPlayerSide == true && Grid.IsWalkable(from)
                && !Grid.IsBlocked(from) && IsExitPosition(to)
                && Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) == 1
                && OpenExitEdges.Contains(new BattleCellEdge(from, to));
        }
        public DungeonTheme DungeonTheme = DungeonTheme.Story;
        public float ActionPointsPerSpeed = GameDefinesRuntime.GetFloat(
            GameDefineIds.ActionPointsPerSpeed, 0.25f);
        public BattleGrid Grid;
        public readonly Dictionary<string, PendingScoutKnives> PendingScoutKnifeActions = new();
        public List<BattleUnit> Units = new();
        public List<ScheduledCombatEffect> ScheduledEffects = new();
        public List<SummonedBattleUnit> Summons = new();
        public HashSet<string> DefeatedEnemyInstanceIds = new();
        private readonly Dictionary<string, GridPosition> enemyDeathPositions = new();
        [NonSerialized] public CombatObjectDamageAction ObjectDamageAction;
        [NonSerialized] public Action<CombatMapObject> ObjectDurabilityChanged;
        public List<CombatMapObject> MapObjects = new();
        public List<SummonedCombatMapObject> SummonedObjects = new();
        public List<SummonedCombatMapObject> SummonedObjectTurnOrder = new();
        [NonSerialized] public PassiveEffectCombatEventDispatcher PassiveEffectEventDispatcher;

        public List<CombatTurnParticipant> TurnParticipants = new();
        public List<BattleUnit> TurnOrder = new();
        public int ActiveTurnIndex;
        public BattleUnit ActiveUnit;
        public CombatTurnParticipant ActiveParticipant;
        public SummonedCombatMapObject ActiveSummonedObject;

        public bool IsPositionOccupied(
            GridPosition position,
            BattleUnit ignoredUnit = null)
        {
            if (ExternalPositionOccupied?.Invoke(position) == true) return true;
            foreach (var unit in Units)
            {
                if (unit == null || unit == ignoredUnit)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPositionOccupiedByMapObject(
            GridPosition position,
            CombatMapObject ignoredObject = null)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject is ObstacleCombatMapObject { IsActive: false }) continue;
                if (mapObject == null || mapObject == ignoredObject)
                {
                    continue;
                }

                if (mapObject.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPositionOccupiedByLivingUnit(GridPosition position)
        {
            foreach (var unit in Units)
            {
                if (unit?.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool DoesObjectBlockMovement(CombatMapObject mapObject)
        {
            // Doors block their edge, never the floor cell used as their object position.
            if (mapObject == null || mapObject is DoorCombatMapObject || !mapObject.BlocksMovement)
            {
                return false;
            }

            return !IsPositionOccupiedByLivingUnit(mapObject.Position);
        }

        public bool DoesObjectBlockLineOfSight(CombatMapObject mapObject)
        {
            if (mapObject == null || !mapObject.BlocksLineOfSight)
            {
                return false;
            }

            return !IsPositionOccupiedByLivingUnit(mapObject.Position);
        }

        public CombatMapObject GetMapObjectAt(GridPosition position)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject is ObstacleCombatMapObject { IsActive: false }) continue;
                if (mapObject != null && mapObject.Position == position)
                {
                    return mapObject;
                }
            }

            return null;
        }

        public CombatMapObject GetMapObject(string objectId)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject != null && mapObject.ObjectId == objectId)
                {
                    return mapObject;
                }
            }

            return null;
        }

        public SummonedCombatMapObject GetSummonedObject(
            string objectInstanceId)
        {
            foreach (var summonedObject in SummonedObjects)
            {
                if (summonedObject != null
                    && summonedObject.ObjectId == objectInstanceId)
                {
                    return summonedObject;
                }
            }

            return null;
        }

        public bool TryMarkEnemyDefeated(string enemyInstanceId)
        {
            if (string.IsNullOrWhiteSpace(enemyInstanceId))
            {
                return false;
            }

            return DefeatedEnemyInstanceIds.Add(enemyInstanceId);
        }

        public bool TryMarkEnemyDefeated(
            string enemyInstanceId,
            GridPosition deathPosition)
        {
            if (!TryMarkEnemyDefeated(enemyInstanceId))
            {
                return false;
            }

            enemyDeathPositions[enemyInstanceId] = deathPosition;
            return true;
        }

        public bool TryGetEnemyDeathPosition(
            string enemyInstanceId,
            out GridPosition deathPosition)
        {
            deathPosition = default;

            return !string.IsNullOrWhiteSpace(enemyInstanceId)
                && enemyDeathPositions.TryGetValue(
                    enemyInstanceId,
                    out deathPosition);
        }
    }
}

namespace Godmist.Runtime.Combat
{
    // Per-session contact; never serialized as a mid-combat save.
    public sealed class DungeonContactState
    {
        private readonly Func<BattleUnit, BattleUnit, bool> canSee;
        private sealed class Observation
        {
            public GridPosition Position;
            public UnityEngine.Vector2Int Direction;
            public GridPosition SearchPosition;
            public int ForwardSteps;
            public bool Checked;
            public Observation(GridPosition position) { Position = SearchPosition = position; }
        }
        private readonly Dictionary<(BattleUnit Enemy, BattleUnit Target), Observation> memory = new();
        private readonly HashSet<(BattleUnit Enemy, BattleUnit Target)> previousVisible = new();
        private readonly HashSet<(BattleUnit Enemy, BattleUnit Target)> visible = new();
        public int EscapeReactionTurns { get; } = Math.Max(1, GameDefinesRuntime.GetInt(GameDefineIds.EscapeContactLossTurns, 2));
        private readonly Dictionary<BattleUnit, Dictionary<BattleUnit, int>> completed = new();
        private readonly Dictionary<BattleUnit, HashSet<BattleUnit>> started = new();
        private readonly HashSet<BattleUnit> seenPlayers = new();
        public DungeonContactState(Func<BattleUnit, BattleUnit, bool> canSee) => this.canSee = canSee;
        private static bool Alive(BattleUnit unit) => unit?.Character?.LifeState == CharacterLifeState.Alive;

        public void Refresh(CombatSession session)
        {
            previousVisible.Clear(); previousVisible.UnionWith(visible);
            visible.Clear(); seenPlayers.Clear();
            foreach (var enemy in session.Units)
            {
                if (enemy.IsPlayerSide || !Alive(enemy)) continue;
                foreach (var player in session.Units)
                {
                    if (!player.IsPlayerSide || !Alive(player) || !canSee(enemy, player)) continue;
                    visible.Add((enemy, player)); seenPlayers.Add(player);
                    var key = (enemy, player);
                    if (!memory.TryGetValue(key, out var observation))
                        memory.Add(key, observation = new Observation(player.Position));
                    if (!previousVisible.Contains(key)) observation.Direction = UnityEngine.Vector2Int.zero;
                    else if (observation.Position != player.Position)
                    {
                        var delta = new UnityEngine.Vector2Int(player.Position.X - observation.Position.X,
                            player.Position.Y - observation.Position.Y);
                        // Only observed, adjacent movement supplies a direction; never infer a hidden path or teleport.
                        observation.Direction = Math.Abs(delta.x) + Math.Abs(delta.y) == 1 ? delta : UnityEngine.Vector2Int.zero;
                    }
                    observation.Position = observation.SearchPosition = player.Position;
                    observation.ForwardSteps = 0; observation.Checked = false;
                }
            }
            foreach (var player in seenPlayers)
            {
                completed.Remove(player);
                foreach (var candidates in started.Values) candidates.Remove(player);
            }
        }
        public bool Sees(BattleUnit enemy, BattleUnit target) => visible.Contains((enemy, target));
        public void ReceiveAlert(BattleUnit enemy, BattleUnit target, GridPosition lastSeenPosition)
        {
            if (Alive(enemy) && Alive(target) && !memory.ContainsKey((enemy, target))) memory[(enemy, target)] = new Observation(lastSeenPosition);
        }
        public void BeginTurn(CombatSession session, BattleUnit enemy)
        {
            Refresh(session);
            if (enemy.IsPlayerSide) return;
            var candidates = new HashSet<BattleUnit>();
            foreach (var player in session.Units)
                if (player.IsPlayerSide && Alive(player) && !seenPlayers.Contains(player)) candidates.Add(player);
            started[enemy] = candidates;
        }
        public void EndTurn(CombatSession session, BattleUnit enemy)
        {
            Refresh(session);
            if (!started.TryGetValue(enemy, out var candidates)) return;
            foreach (var player in candidates)
            {
                if (!completed.TryGetValue(player, out var turns)) completed.Add(player, turns = new());
                turns.TryGetValue(enemy, out var count);
                turns[enemy] = Math.Min(EscapeReactionTurns, count + 1);
            }
            started.Remove(enemy);
        }
        public bool CanLeave(CombatSession session, BattleUnit player)
        {
            if (!Alive(player) || !player.IsPlayerSide || seenPlayers.Contains(player)
                || !completed.TryGetValue(player, out var turns)) return false;
            var hasEnemy = false;
            foreach (var enemy in session.Units)
                if (!enemy.IsPlayerSide && Alive(enemy))
                { hasEnemy = true; if (!turns.TryGetValue(enemy, out var count) || count < EscapeReactionTurns) return false; }
            return hasEnemy;
        }
        public bool TryGetSearchPosition(CombatSession session, BattleUnit enemy, out GridPosition position)
        {
            position = default;
            var best = int.MaxValue;
            foreach (var target in session.Units)
            {
                if (!target.IsPlayerSide || !Alive(target) || !memory.TryGetValue((enemy, target), out var observation)
                    || observation.Checked) continue;
                if (enemy.Position == observation.SearchPosition)
                {
                    var advanced = false;
                    if (observation.ForwardSteps < 2 && observation.Direction != UnityEngine.Vector2Int.zero)
                        foreach (var next in session.Grid.GetNeighbours(observation.SearchPosition))
                        {
                            if (next.X - observation.SearchPosition.X != observation.Direction.x
                                || next.Y - observation.SearchPosition.Y != observation.Direction.y
                                || !session.Grid.CanMoveBetween(observation.SearchPosition, next)
                                || session.Grid.IsBlocked(next) || session.DoesObjectBlockMovement(session.GetMapObjectAt(next))) continue;
                            observation.SearchPosition = next; observation.ForwardSteps++; advanced = true; break;
                        }
                    if (!advanced) { observation.Checked = true; continue; }
                }
                var last = observation.SearchPosition;
                var distance = Math.Abs(last.X - enemy.Position.X) + Math.Abs(last.Y - enemy.Position.Y);
                if (distance >= best) continue;
                best = distance; position = last;
            }
            return best != int.MaxValue;
        }
        public void Forget(BattleUnit player)
        {
            completed.Remove(player);
            foreach (var candidates in started.Values) candidates.Remove(player);
        }
        public void Absorb(DungeonContactState source)
        {
            if (source == null) return;
            foreach (var pair in source.memory) memory[pair.Key] = pair.Value;
            // A merged fight gives every newly involved enemy a full opportunity to react.
            completed.Clear(); started.Clear();
        }
    }
}
