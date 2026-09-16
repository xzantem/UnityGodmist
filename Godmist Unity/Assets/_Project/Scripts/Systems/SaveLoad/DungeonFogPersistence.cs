using System.Linq;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    public static class DungeonFogPersistence
    {
        public static DungeonFogSaveData Capture(DungeonFloorInstance floor)
        {
            // Preserve legacy migration if this floor has not been presented yet.
            if (floor.Fog.RevealLegacyMap) return null;
            var result = new DungeonFogSaveData();
            result.Bodies.AddRange(floor.Fog.Bodies.OrderBy(id => id, System.StringComparer.Ordinal));
            foreach (var edge in floor.Fog.ClosedGates.OrderBy(e => e.FirstCell.x).ThenBy(e => e.FirstCell.y)
                .ThenBy(e => e.SecondCell.x).ThenBy(e => e.SecondCell.y).ThenBy(e => e.Level))
                result.ClosedGates.Add(new DungeonFogGateSaveData { X = edge.FirstCell.x, Y = edge.FirstCell.y,
                    NextX = edge.SecondCell.x, NextY = edge.SecondCell.y, Level = edge.Level });
            foreach (var position in floor.Fog.Discovered.OrderBy(p => p.Surface.Cell.x).ThenBy(p => p.Surface.Cell.y)
                .ThenBy(p => p.Surface.Level).ThenBy(p => p.IsOnStairs).ThenBy(p => p.Stair.First.Cell.x)
                .ThenBy(p => p.Stair.First.Cell.y).ThenBy(p => p.Stair.First.Level)
                .ThenBy(p => p.Stair.Second.Cell.x).ThenBy(p => p.Stair.Second.Cell.y).ThenBy(p => p.Stair.Second.Level).ThenBy(p => p.StairStep))
                result.Discovered.Add(new DungeonActorPositionSaveData
                {
                    IsOnStairs = position.IsOnStairs, X = position.Surface.Cell.x, Y = position.Surface.Cell.y, Level = position.Surface.Level,
                    StairStep = position.StairStep,
                    FirstX = position.Stair.First.Cell.x, FirstY = position.Stair.First.Cell.y, FirstLevel = position.Stair.First.Level,
                    SecondX = position.Stair.Second.Cell.x, SecondY = position.Stair.Second.Cell.y, SecondLevel = position.Stair.Second.Level
                });
            foreach (var entry in floor.Fog.Objects.OrderBy(p => p.Key, System.StringComparer.Ordinal))
                result.Objects.Add(new DungeonObjectKnowledgeSaveData { Id = entry.Key, Active = entry.Value.Active, State = entry.Value.State });
            return result;
        }

        public static bool Restore(DungeonFloorInstance floor, DungeonFogSaveData saved)
        {
            if (saved == null) { floor.Fog.RevealLegacyMap = true; return true; }
            if (saved.Discovered == null || saved.Objects == null) return false;
            if (saved.Bodies == null || saved.ClosedGates == null) return false;
            foreach (var id in saved.Bodies)
                if (string.IsNullOrWhiteSpace(id) || !floor.Fog.Bodies.Add(id)) return false;
            foreach (var gate in saved.ClosedGates)
            {
                if (gate == null || gate.Level < 0 || System.Math.Abs((long)gate.X - gate.NextX) + System.Math.Abs((long)gate.Y - gate.NextY) != 1) return false;
                var edge = new DungeonSurfaceEdge(new DungeonSurfacePosition(new Vector2Int(gate.X, gate.Y), gate.Level),
                    new DungeonSurfacePosition(new Vector2Int(gate.NextX, gate.NextY), gate.Level));
                if (!floor.Map.IsWalkable(edge.First) || !floor.Map.IsWalkable(edge.Second) || !floor.Fog.ClosedGates.Add(edge)) return false;
            }
            foreach (var node in saved.Discovered)
            {
                if (node == null || node.Level < 0 || node.FirstLevel < 0 || node.SecondLevel < 0) return false;
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
                if (!floor.Fog.RestoreDiscovered(floor.Map, position)) return false;
            }
            foreach (var known in saved.Objects)
            {
                if (known == null || string.IsNullOrWhiteSpace(known.Id) || !floor.TryGetObject(known.Id, out _)
                    || floor.Fog.Objects.ContainsKey(known.Id)) return false;
                floor.Fog.Objects.Add(known.Id, new DungeonObjectKnowledge { Active = known.Active, State = known.State });
            }
            return true;
        }
    }
}
