using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Combat;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonReinforcementSystem
    {
        public static EncounterDefinition PickEncounter(DungeonInstance dungeon, System.Random random)
        {
            var definitions = dungeon?.Definition?.EncounterDefinitions;
            if (definitions == null) return null;
            var boss = dungeon.Definition.BossEncounterDefinition;
            var candidates = definitions.Where(e => e != null && e != boss && e.Id != boss?.Id
                && !string.IsNullOrWhiteSpace(e.Id) && e.Enemies.Count > 0
                && e.Enemies.All(enemy => enemy?.CharacterDefinition != null && enemy.ClassData != null
                    && !string.IsNullOrWhiteSpace(enemy.InstanceId))
                && e.Enemies.Select(enemy => enemy.InstanceId).Distinct().Count() == e.Enemies.Count).Distinct().ToArray();
            return candidates.Length == 0 ? null : candidates[random.Next(candidates.Length)];
        }

        public static bool TryPlan(DungeonInstance dungeon, DungeonFloorInstance floor, CombatSession session,
            ISet<Vector2Int> occupiedWorldCells, System.Random random, out DungeonEncounterInstance encounter)
        {
            encounter = null;
            var enemy = CombatStallWarningSystem.Synchronize(session);
            if (enemy == null || !session.IsDungeonWide || session.DungeonContext == null || floor?.Map == null) return false;
            var definition = PickEncounter(dungeon, random);
            if (definition == null) return false;
            var world = DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, enemy.Position);
            if (!floor.Map.TryGetRoomId(world, out var roomId) || !floor.TryGetRoom(roomId, out _))
                roomId = session.DungeonContext.EncounterRoomId;
            if (!floor.TryGetRoom(roomId, out _)) return false;
            var doors = floor.Connections.Where(c => c.HasGeometry && c.DoorwayLevel == 0
                    && (!c.HasProgressionGate || c.IsProgressionGateUnlocked)
                    && (c.FirstRoomId == roomId || c.SecondRoomId == roomId))
                .Select(c => c.GetEntranceCell(roomId))
                .Distinct().OrderBy(c => c.x).ThenBy(c => c.y).ToList();
            if (doors.Count == 0) return false;
            var roomCells = floor.Map.FloorCells.Where(c => !floor.IsConnectionCell(c)
                && floor.Map.TryGetRoomId(c, out var id) && id == roomId).ToArray();
            if (roomCells.Length == 0) return false;
            var occupied = new HashSet<GridPosition>(session.Units.Select(u => u.Position));
            DungeonCombatParticipationSystem.AddExternalReservations(session, occupied);
            var count = (definition.Enemies.Count + 1) / 2;
            var spawnCells = new List<Vector2Int>();
            // Search a small connected area inside an entrance, never across walls or onto another actor.
            foreach (var door in doors)
            {
                var queue = new Queue<(Vector2Int Cell, int Distance)>();
                var seen = new HashSet<Vector2Int> { door };
                queue.Enqueue((door, 0));
                while (queue.Count > 0 && spawnCells.Count < count)
                {
                    var (cell, distance) = queue.Dequeue();
                    if (!roomCells.Contains(cell)) continue;
                    var position = DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, cell);
                    if (session.Grid.IsBlocked(position)) continue;
                    if (!occupied.Contains(position) && !occupiedWorldCells.Contains(cell) && !spawnCells.Contains(cell)) spawnCells.Add(cell);
                    if (distance >= 2) continue;
                    foreach (var offset in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    {
                        var next = cell + offset;
                        if (seen.Add(next) && session.Grid.CanMoveBetween(position,
                                DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, next))) queue.Enqueue((next, distance + 1));
                    }
                }
                if (spawnCells.Count == count) break;
            }
            if (spawnCells.Count < count) return false;
            var selected = definition.Enemies.ToList();
            for (var i = selected.Count - 1; i > 0; i--)
            { var j = random.Next(i + 1); (selected[i], selected[j]) = (selected[j], selected[i]); }
            var bounds = new RectInt(roomCells.Min(c => c.x), roomCells.Min(c => c.y),
                roomCells.Max(c => c.x) - roomCells.Min(c => c.x) + 1,
                roomCells.Max(c => c.y) - roomCells.Min(c => c.y) + 1);
            encounter = new DungeonEncounterInstance("reinforcement." + Guid.NewGuid().ToString("N"), definition, roomId, bounds);
            if (!encounter.TryConfigureReinforcements(selected.Take(count).Select(e => e.InstanceId))) return false;
            encounter.TrySetExplorationPosition(spawnCells[0]);
            for (var i = 0; i < count; i++)
            {
                encounter.TrySetEnemyExplorationPosition(selected[i].InstanceId, spawnCells[i]);
                encounter.StationaryEnemyHomes.Add(selected[i].InstanceId, spawnCells[i]);
            }
            return true;
        }
    }
}
