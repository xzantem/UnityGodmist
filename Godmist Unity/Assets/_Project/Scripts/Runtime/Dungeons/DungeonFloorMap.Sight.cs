using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public partial class DungeonFloorMap
    {
        public bool TryGetTraversalHeight(DungeonTraversalPosition position, out float height)
        {
            height = 0;
            if (!TryGetTraversalCell(position, out _)) return false;
            height = position.IsOnStairs
                ? Mathf.Lerp(position.Stair.First.Level, position.Stair.Second.Level,
                    (float)position.StairStep / (stairPaths[position.Stair].Count - 1))
                : position.Surface.Level;
            return true;
        }

        public bool TryGetSightPoint(DungeonTraversalPosition position, out Vector3 point)
        {
            point = default;
            if (!TryGetTraversalCell(position, out var cell) || !TryGetTraversalHeight(position, out var height)) return false;
            // Rays connect cell centres. Height is normalized in layers, not a range bonus.
            point = new Vector3(cell.x + .5f, cell.y + .5f, height + .5f);
            return true;
        }

        public bool BlocksFloorSight(DungeonTraversalPosition from, DungeonTraversalPosition to)
        {
            if (!TryGetSightPoint(from, out var start) || !TryGetSightPoint(to, out var end)) return true;
            var delta = end - start;
            if (Mathf.Abs(delta.z) > .00001f)
            {
                foreach (var surface in Surfaces)
                {
                    var t = (surface.Level - start.z) / delta.z;
                    if (t <= 0 || t >= 1) continue;
                    var point = start + delta * t;
                    // Test the interior of the union, including seams between adjacent floor tiles.
                    const float epsilon = .0001f;
                    bool FloorAt(float x, float y) => IsWalkable(new DungeonSurfacePosition(
                        new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y)), surface.Level));
                    if (point.x < surface.Cell.x - epsilon || point.x > surface.Cell.x + 1 + epsilon
                        || point.y < surface.Cell.y - epsilon || point.y > surface.Cell.y + 1 + epsilon) continue;
                    if (FloorAt(point.x - epsilon, point.y - epsilon) && FloorAt(point.x + epsilon, point.y - epsilon)
                        && FloorAt(point.x - epsilon, point.y + epsilon) && FloorAt(point.x + epsilon, point.y + epsilon)) return true;
                }
            }
            return false;
        }

        public bool BlocksStructuralSight(DungeonTraversalPosition from, DungeonTraversalPosition to)
        {
            if (BlocksFloorSight(from, to) || !TryGetSightPoint(from, out var start)
                || !TryGetSightPoint(to, out var end)) return true;
            foreach (var edge in SurfaceWallEdges)
                if (GetWallType(edge) == DungeonWallType.Full && SightCrossesEdge(start, end, edge)) return true;
            foreach (var edge in closedProgressionGateEdges)
                if (SightCrossesEdge(start, end, edge)) return true;
            return false;
        }

        public static bool SightCrossesEdge(Vector3 from, Vector3 to, DungeonSurfaceEdge edge)
        {
            var vertical = edge.FirstCell.x != edge.SecondCell.x;
            var boundary = vertical ? Math.Max(edge.FirstCell.x, edge.SecondCell.x) : Math.Max(edge.FirstCell.y, edge.SecondCell.y);
            var delta = vertical ? to.x - from.x : to.y - from.y;
            if (Mathf.Abs(delta) < .00001f) return false;
            var t = (boundary - (vertical ? from.x : from.y)) / delta;
            if (t <= 0 || t >= 1) return false;
            var point = Vector3.Lerp(from, to, t);
            var along = vertical ? point.y : point.x;
            var minimum = vertical ? edge.FirstCell.y : edge.FirstCell.x;
            return along >= minimum && along <= minimum + 1 && point.z > edge.Level && point.z < edge.Level + 1;
        }

        public static bool SightCrossesCell(Vector3 from, Vector3 to, DungeonSurfacePosition cell)
        {
            var minimum = new Vector3(cell.Cell.x, cell.Cell.y, cell.Level);
            var maximum = minimum + Vector3.one;
            var enter = 0f; var leave = 1f;
            for (var axis = 0; axis < 3; axis++)
            {
                var delta = to[axis] - from[axis];
                if (Mathf.Abs(delta) < .00001f)
                { if (from[axis] <= minimum[axis] || from[axis] >= maximum[axis]) return false; continue; }
                var a = (minimum[axis] - from[axis]) / delta;
                var b = (maximum[axis] - from[axis]) / delta;
                enter = Mathf.Max(enter, Mathf.Min(a, b)); leave = Mathf.Min(leave, Mathf.Max(a, b));
                if (enter >= leave) return false;
            }
            return leave > 0 && enter < 1;
        }
    }
}
