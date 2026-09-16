using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
using UnityEngine;
namespace Godmist.Systems.SaveLoad
{
    public static class DungeonPatrolPersistence
    {
        public static List<DungeonPatrolSaveData> Capture(DungeonFloorInstance floor)
            => floor.Patrols.OrderBy(p => p.Key, System.StringComparer.Ordinal).Select(p => new DungeonPatrolSaveData
            {
                StopIndices = p.Value.StopIndices.OrderBy(i => i).ToList(),
                EnemyKey = p.Key, NextPoint = p.Value.NextPoint, Stops = p.Value.Stops,
                WaitRemaining = p.Value.WaitRemaining, ObservedCharacterId = p.Value.ObservedCharacterId,
                Route = p.Value.Route.Select(n => new DungeonActorPositionSaveData
                {
                    IsOnStairs = n.IsOnStairs, X = n.Surface.Cell.x, Y = n.Surface.Cell.y, Level = n.Surface.Level,
                    StairStep = n.StairStep, FirstX = n.Stair.First.Cell.x, FirstY = n.Stair.First.Cell.y, FirstLevel = n.Stair.First.Level,
                    SecondX = n.Stair.Second.Cell.x, SecondY = n.Stair.Second.Cell.y, SecondLevel = n.Stair.Second.Level
                }).ToList()
            }).ToList();

        public static bool Restore(DungeonFloorInstance floor, List<DungeonPatrolSaveData> saved)
        {
            if (saved == null) return true; // Older saves initialize patrols once when the floor is presented.
            var result = new Dictionary<string, DungeonPatrolState>();
            foreach (var entry in saved)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyKey) || result.ContainsKey(entry.EnemyKey)
                    || entry.Route == null || entry.Route.Count == 0 || entry.NextPoint < 0 || entry.NextPoint >= entry.Route.Count
                    || entry.Stops < 0 || !float.IsFinite(entry.WaitRemaining) || entry.WaitRemaining < 0 || entry.WaitRemaining > 3) return false;
                var state = new DungeonPatrolState { NextPoint = entry.NextPoint, Stops = entry.Stops,
                    WaitRemaining = entry.WaitRemaining, ObservedCharacterId = entry.ObservedCharacterId };
                foreach (var node in entry.Route)
                {
                    if (node == null) return false;
                    DungeonTraversalPosition position;
                    if (node.IsOnStairs)
                    {
                        if (node.StairStep <= 0 || node.FirstLevel >= node.SecondLevel) return false;
                        position = new DungeonTraversalPosition(new DungeonStairConnection(
                            new DungeonSurfacePosition(new Vector2Int(node.FirstX, node.FirstY), node.FirstLevel),
                            new DungeonSurfacePosition(new Vector2Int(node.SecondX, node.SecondY), node.SecondLevel)), node.StairStep);
                    }
                    else
                    {
                        if (node.StairStep != 0) return false;
                        position = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(node.X, node.Y), node.Level));
                    }
                    if (!floor.Map.TryGetTraversalCell(position, out _)) return false;
                    state.Route.Add(position);
                }
                if (entry.StopIndices != null)
                    foreach (var index in entry.StopIndices)
                        if (index < 0 || index >= state.Route.Count || !state.StopIndices.Add(index)) return false;
                // No stops means an old plan: rebuild it once when the floor is presented.
                result.Add(entry.EnemyKey, state);
            }
            floor.Patrols.Clear();foreach (var pair in result) floor.Patrols.Add(pair.Key, pair.Value);
            return true;
        }
    }
}
