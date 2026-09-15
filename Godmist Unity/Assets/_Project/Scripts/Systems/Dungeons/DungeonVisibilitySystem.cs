using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    /// <summary>Logical visibility, independent of the camera, renderer and combat session.</summary>
    public sealed class DungeonVisibilitySystem
    {
        public bool CanDetect(DungeonFloorInstance floor, DungeonTraversalPosition observer,
            Vector2 facing, float range, float angle, DungeonTraversalPosition target)
        {
            if (floor?.Map == null || !float.IsFinite(range) || range < 0
                || !float.IsFinite(angle) || angle <= 0 || angle > 360
                || !floor.Map.TryGetSightPoint(observer, out var from)
                || !floor.Map.TryGetSightPoint(target, out var to)) return false;
            var offset = new Vector2(to.x - from.x, to.y - from.y);
            if (offset.sqrMagnitude > range * range) return false;
            if (angle < 360 && offset.sqrMagnitude > .000001f)
            {
                if (!float.IsFinite(facing.x) || !float.IsFinite(facing.y) || facing.sqrMagnitude < .000001f) return false;
                var threshold = Mathf.Cos(angle * .5f * Mathf.Deg2Rad);
                if (Vector2.Dot(facing.normalized, offset.normalized) + .000001f < threshold) return false;
            }
            return HasLineOfSight(floor, observer, target);
        }

        public bool HasLineOfSight(DungeonFloorInstance floor, DungeonTraversalPosition observer, DungeonTraversalPosition target)
        {
            var map = floor?.Map;
            if (map == null || !map.TryGetSightPoint(observer, out var from)
                || !map.TryGetSightPoint(target, out var to) || map.BlocksStructuralSight(observer, target)) return false;
            foreach (var obj in floor.Objects)
            {
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door && !door.IsOpen
                    && DungeonFloorMap.SightCrossesEdge(from, to, new DungeonSurfaceEdge(door.Edge))) return false;
                if (obj is ObstacleDungeonObjectInstance obstacle && obstacle.BlocksLineOfSight)
                {
                    var cell = new DungeonSurfacePosition(obstacle.Position);
                    // The occluding object itself is observable, but hides the space behind it.
                    if ((!observer.IsOnStairs && observer.Surface == cell) || (!target.IsOnStairs && target.Surface == cell)) continue;
                    if (DungeonFloorMap.SightCrossesCell(from, to, cell)) return false;
                }
            }
            return true;
        }

        /// <summary>Shared current visibility. Discovery memory and fog rendering are separate consumers.</summary>
        public void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            HashSet<DungeonTraversalPosition> result) => CollectPartyVisible(floor, observers, null, result);

        public void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            float range, HashSet<DungeonTraversalPosition> result) => CollectPartyVisible(floor, observers, (float?)range, result);

        private void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            float? range, HashSet<DungeonTraversalPosition> result)
        {
            result.Clear();
            if (floor?.Map == null || observers == null) return;
            foreach (var surface in floor.Map.Surfaces) AddIfVisible(new DungeonTraversalPosition(surface));
            foreach (var stair in floor.Map.Stairs)
                if (floor.Map.TryGetStairPath(stair, out var path))
                    for (var step = 1; step < path.Count - 1; step++) AddIfVisible(new DungeonTraversalPosition(stair, step));

            void AddIfVisible(DungeonTraversalPosition candidate)
            {
                foreach (var observer in observers)
                    if (range.HasValue ? CanDetect(floor, observer, Vector2.zero, range.Value, 360, candidate)
                        : HasLineOfSight(floor, observer, candidate))
                    { result.Add(candidate); return; }
            }
        }
    }
}
