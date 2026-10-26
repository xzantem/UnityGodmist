using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    // A forest wicket faces the outside of the whole floor, never a narrow
    // divider or a pocket between rooms. Shared by generation and presentation.
    public static class DungeonForestTransitionBoundary
    {
        public static bool TryResolve(DungeonFloorInstance floor, string roomId,
            out Vector2Int approach, out Vector2Int outward, bool requireExterior = true)
        {
            approach = default; outward = default;
            if (floor?.Map == null) return false;
            var map = floor.Map;
            var cells = new HashSet<Vector2Int>();
            var columns = new Dictionary<int, (int Min, int Max)>();
            foreach (var cell in map.FloorCells)
            {
                if (!columns.TryGetValue(cell.x, out var range)) range = (cell.y, cell.y);
                columns[cell.x] = (Mathf.Min(range.Min, cell.y), Mathf.Max(range.Max, cell.y));
                if (!floor.IsConnectionCell(cell) && map.TryGetRoomId(cell, out var id) && id == roomId)
                    cells.Add(cell);
            }
            if (cells.Count == 0) return false;
            var occupied = floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                .Where(o => o.IsActive && o.BlocksMovement).SelectMany(o => o.EnumerateOccupiedCells()).ToHashSet();
            var centerX = cells.Average(c => (float)c.x);
            foreach (var normal in new[] { Vector2Int.up, Vector2Int.down })
            {
                bool Exterior(Vector2Int cell) => columns.TryGetValue(cell.x, out var range)
                    && cell.y == (normal.y > 0 ? range.Max : range.Min);
                bool Candidate(Vector2Int cell) => Enumerable.Range(-1, 3).All(x =>
                {
                    var inner = cell + Vector2Int.right * x;
                    return cells.Contains(inner) && !map.IsWalkable(inner + normal)
                        && (!requireExterior || Exterior(inner))
                        && cells.Contains(inner - normal) && !occupied.Contains(inner) && !occupied.Contains(inner - normal)
                        && map.CanMoveBetween(inner - normal, inner, true);
                });
                var candidates = cells.Where(Candidate).OrderByDescending(c => normal.y * c.y)
                    .ThenByDescending(c =>
                    {
                        var left = 0; var right = 0;
                        while (cells.Contains(c + Vector2Int.left * (left + 1))
                            && !map.IsWalkable(c + Vector2Int.left * (left + 1) + normal)) left++;
                        while (cells.Contains(c + Vector2Int.right * (right + 1))
                            && !map.IsWalkable(c + Vector2Int.right * (right + 1) + normal)) right++;
                        return Mathf.Min(left, right);
                    }).ThenBy(c => Mathf.Abs(c.x - centerX)).ThenBy(c => c.x).ToArray();
                if (candidates.Length == 0) continue;
                approach = candidates[0]; outward = normal; return true;
            }
            return false;
        }
    }
}
