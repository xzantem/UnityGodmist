using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Quests;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatOutcomeSystem
    {
        private readonly DungeonCombatStateSyncSystem stateSyncSystem;

        public DungeonCombatOutcomeSystem(
            DungeonCombatStateSyncSystem stateSyncSystem = null)
        {
            this.stateSyncSystem =
                stateSyncSystem ?? new DungeonCombatStateSyncSystem();
        }

        public bool TryApplyOutcome(
            GameSession gameSession,
            CombatSession combatSession,
            QuestObjectiveProgressSystem questProgress = null)
        {
            if (combatSession == null
                || (combatSession.Outcome != CombatOutcomeState.Victory
                    && combatSession.Outcome != CombatOutcomeState.Escaped
                    && !(combatSession.IsSeamless && combatSession.Outcome == CombatOutcomeState.Defeat)))
            {
                return false;
            }

            if (gameSession == null
                || gameSession.ActiveDungeon == null
                || (combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext) == null)
            {
                return false;
            }

            var context = combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext;

            if (!combatSession.IsDungeonWide && !string.Equals(
                    combatSession.CombatId,
                    context.EncounterInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!stateSyncSystem.TrySyncDoorStates(gameSession, combatSession)
                || !stateSyncSystem.TrySyncDefeatedEnemyStates(
                    gameSession, combatSession, questProgress))
            {
                return false;
            }

            if (!TryCreateEnemyBodies(
                    gameSession.ActiveDungeon,
                    context,
                    combatSession))
            {
                return false;
            }

            if (combatSession.Outcome != CombatOutcomeState.Victory)
            {
                gameSession.ActiveDungeon.ClearPendingPlayerPartyPositions();
                return true;
            }

            if (!TryStorePlayerPartyPositions(
                    gameSession,
                    context,
                    combatSession))
            {
                return false;
            }

            var dungeon = gameSession.ActiveDungeon;
            if (combatSession.IsDungeonWide)
            {
                foreach (var result in DungeonCombatMembershipSystem.EncounterResults(dungeon, combatSession))
                {
                    if (!dungeon.TryGetFloor(context.FloorIndex, out var floor)
                        || !floor.TryGetEncounter(result.CombatId, out var encounter)) return false;
                    if (System.Linq.Enumerable.All(encounter.Definition.Enemies, enemy => encounter.IsEnemyDefeated(enemy.InstanceId))
                        && !dungeon.IsEncounterCleared(encounter.InstanceId)
                        && !dungeon.TryMarkEncounterCleared(encounter.InstanceId)) return false;
                }
                return true;
            }

            if (!dungeon.IsEncounterCleared(context.EncounterInstanceId)
                && !dungeon.TryMarkEncounterCleared(
                    context.EncounterInstanceId))
            {
                return false;
            }

            return true;
        }

        private static bool TryStorePlayerPartyPositions(
            GameSession gameSession,
            DungeonCombatContext context,
            CombatSession combatSession)
        {
            var positions = new Dictionary<string, UnityEngine.Vector2>();

            foreach (var unit in combatSession.Units)
            {
                if (unit?.Character == null
                    || !unit.IsPlayerSide
                    || string.IsNullOrWhiteSpace(unit.Character.CharacterId))
                {
                    continue;
                }

                var worldCell = DungeonBattleGridFactory.ToWorldPosition(
                    context,
                    unit.Position);
                positions[unit.Character.CharacterId] =
                    new UnityEngine.Vector2(
                        worldCell.x + 0.5f,
                        worldCell.y + 0.5f);
            }

            if (!gameSession.ActiveDungeon.TrySetPendingPlayerPartyPositions(
                    positions))
            {
                return false;
            }

            if (gameSession.PlayerParty.ActiveLeaderId != null
                && positions.TryGetValue(
                    gameSession.PlayerParty.ActiveLeaderId,
                    out var leaderPosition))
            {
                gameSession.ActiveDungeon.SetPlayerPosition(leaderPosition);
            }

            return true;
        }

        public static bool TryCreateEnemyBodies(
            DungeonInstance dungeon,
            DungeonCombatContext context,
            CombatSession combatSession)
        {
            if (combatSession?.IsDungeonWide == true && dungeon != null)
            {
                foreach (var result in DungeonCombatMembershipSystem.EncounterResults(dungeon, combatSession))
                    if (!TryCreateEnemyBodies(dungeon, result.DungeonContext, result)) return false;
                return true;
            }
            if (dungeon == null
                || context == null
                || combatSession == null
                || !dungeon.TryGetFloor(context.FloorIndex, out var floor)
                || !floor.TryGetEncounter(
                    context.EncounterInstanceId,
                    out var encounter))
            {
                return true;
            }

            var rewardsByEnemyId = new Dictionary<string, CombatEnemyReward>(
                StringComparer.Ordinal);

            foreach (var reward in combatSession.Rewards?.EnemyRewards
                         ?? new List<CombatEnemyReward>())
            {
                if (reward == null
                    || string.IsNullOrWhiteSpace(reward.EnemyInstanceId))
                {
                    continue;
                }

                rewardsByEnemyId.TryAdd(reward.EnemyInstanceId, reward);
            }

            var occupiedBodyPositions = new HashSet<UnityEngine.Vector2Int>();

            foreach (var existingBody in floor.EnemyBodies)
            {
                if (existingBody != null)
                {
                    occupiedBodyPositions.Add(existingBody.Position);
                }
            }

            var defeatedEnemyInstanceIds = new List<string>(
                combatSession.DefeatedEnemyInstanceIds);
            defeatedEnemyInstanceIds.Sort(StringComparer.Ordinal);

            foreach (var enemyInstanceId in defeatedEnemyInstanceIds)
            {
                if (encounter.IsEnemyBodyGenerated(enemyInstanceId))
                {
                    continue;
                }

                var bodyInstanceId =
                    $"{encounter.InstanceId}.body.{enemyInstanceId}";

                if (floor.TryGetEnemyBody(bodyInstanceId, out _))
                {
                    encounter.TryMarkEnemyBodyGenerated(enemyInstanceId);
                    continue;
                }

                rewardsByEnemyId.TryGetValue(
                    enemyInstanceId,
                    out var reward);

                reward ??= new CombatEnemyReward
                {
                    EnemyInstanceId = enemyInstanceId
                };

                if (reward.Gold < 0)
                {
                    return false;
                }

                DungeonTraversalPosition? deathNode = null;
                if (floor.Map.HasElevatedSurfaces && combatSession.TryGetEnemyDeathPosition(enemyInstanceId, out var deathCell))
                {
                    var node = DungeonBattleGridFactory.ToDungeonTraversalPosition(context, deathCell);
                    if (floor.Map.TryGetTraversalCell(node, out _)) deathNode = node;
                }
                var body = new DungeonEnemyBodyInstance(
                    bodyInstanceId,
                    enemyInstanceId,
                    deathNode.HasValue && floor.Map.TryGetTraversalCell(deathNode.Value, out var deathWorld) ? deathWorld : ResolveBodyPosition(
                        floor,
                        context,
                        combatSession,
                        enemyInstanceId,
                        occupiedBodyPositions),
                    reward.Gold,
                    reward.Items);

                if (!floor.TryAddEnemyBody(body))
                {
                    return false;
                }

                if (deathNode.HasValue) floor.TrySetActorPosition("body:" + body.InstanceId, deathNode.Value);
                occupiedBodyPositions.Add(body.Position);
                encounter.TryMarkEnemyBodyGenerated(enemyInstanceId);
            }

            return true;
        }

        private static UnityEngine.Vector2Int ResolveBodyPosition(
            DungeonFloorInstance floor,
            DungeonCombatContext context,
            CombatSession combatSession,
            string enemyInstanceId,
            ISet<UnityEngine.Vector2Int> occupiedBodyPositions)
        {
            var map = floor?.Map;

            if (combatSession != null
                && combatSession.TryGetEnemyDeathPosition(
                    enemyInstanceId,
                    out var deathPosition))
            {
                var preferredPosition = DungeonBattleGridFactory.ToWorldPosition(
                    context,
                    deathPosition);

                if (TryResolveBodyPosition(
                        floor,
                        context,
                        preferredPosition,
                        occupiedBodyPositions,
                        out var resolvedPosition))
                {
                    return resolvedPosition;
                }
            }

            if (map == null || map.FloorCells.Count == 0)
            {
                return context.EnemyAnchorWorldPosition;
            }

            var fallbackCandidates = new List<UnityEngine.Vector2Int>();

            foreach (var cell in map.FloorCells)
            {
                if (map.TryGetRoomId(cell, out var roomId)
                    && string.Equals(
                        roomId,
                        context.EncounterRoomId,
                        StringComparison.Ordinal))
                {
                    fallbackCandidates.Add(cell);
                }
            }

            if (fallbackCandidates.Count == 0)
            {
                fallbackCandidates.AddRange(map.FloorCells);
            }

            fallbackCandidates.Sort((first, second) =>
            {
                var firstDistance = GetManhattanDistance(
                    first,
                    context.EnemyAnchorWorldPosition);
                var secondDistance = GetManhattanDistance(
                    second,
                    context.EnemyAnchorWorldPosition);

                var distanceComparison = firstDistance.CompareTo(secondDistance);
                if (distanceComparison != 0) return distanceComparison;

                var yComparison = first.y.CompareTo(second.y);
                return yComparison != 0
                    ? yComparison
                    : first.x.CompareTo(second.x);
            });

            foreach (var candidate in fallbackCandidates)
            {
                if (IsBodyPositionAvailable(
                        floor,
                        context,
                        candidate,
                        occupiedBodyPositions))
                {
                    return candidate;
                }
            }

            return fallbackCandidates[0];
        }

        private static bool TryResolveBodyPosition(
            DungeonFloorInstance floor,
            DungeonCombatContext context,
            UnityEngine.Vector2Int preferredPosition,
            ISet<UnityEngine.Vector2Int> occupiedBodyPositions,
            out UnityEngine.Vector2Int resolvedPosition)
        {
            var candidates = new List<UnityEngine.Vector2Int>
            {
                preferredPosition,
                preferredPosition + UnityEngine.Vector2Int.right,
                preferredPosition + UnityEngine.Vector2Int.left,
                preferredPosition + UnityEngine.Vector2Int.up,
                preferredPosition + UnityEngine.Vector2Int.down
            };

            var map = floor?.Map;
            if (map != null)
            {
                var mapCells = new List<UnityEngine.Vector2Int>(map.FloorCells);
                mapCells.Sort((first, second) =>
                {
                    var distanceComparison = GetManhattanDistance(
                            first,
                            preferredPosition)
                        .CompareTo(GetManhattanDistance(
                            second,
                            preferredPosition));
                    if (distanceComparison != 0)
                    {
                        return distanceComparison;
                    }

                    var yComparison = first.y.CompareTo(second.y);
                    return yComparison != 0
                        ? yComparison
                        : first.x.CompareTo(second.x);
                });

                foreach (var cell in mapCells)
                {
                    if (!candidates.Contains(cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }

            foreach (var candidate in candidates)
            {
                if (IsBodyPositionAvailable(
                        floor,
                        context,
                        candidate,
                        occupiedBodyPositions))
                {
                    resolvedPosition = candidate;
                    return true;
                }
            }

            resolvedPosition = default;
            return false;
        }

        private static bool IsBodyPositionAvailable(
            DungeonFloorInstance floor,
            DungeonCombatContext context,
            UnityEngine.Vector2Int position,
            ISet<UnityEngine.Vector2Int> occupiedBodyPositions)
        {
            var map = floor?.Map;

            if (map == null
                || !map.IsWalkable(position)
                || (context.IsFloorWide ? !context.ContainsWorldCell(position)
                    : !map.TryGetRoomId(position, out var roomId) || !string.Equals(roomId, context.EncounterRoomId, StringComparison.Ordinal))
                || occupiedBodyPositions.Contains(position))
            {
                return false;
            }

            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject is ObstacleDungeonObjectInstance obstacle
                    && obstacle.IsActive
                    && obstacle.Position == position)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetManhattanDistance(
            UnityEngine.Vector2Int first,
            UnityEngine.Vector2Int second)
        {
            return Math.Abs(first.x - second.x) + Math.Abs(first.y - second.y);
        }

    }
}
