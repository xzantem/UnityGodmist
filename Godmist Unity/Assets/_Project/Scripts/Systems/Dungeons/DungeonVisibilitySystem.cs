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

        /// <summary>Alert combatants observe all directions; range and physical occlusion still apply.</summary>
        public bool CanObserveCombat(DungeonFloorInstance floor, DungeonTraversalPosition observer,
            float range, DungeonTraversalPosition target)
            => CanDetect(floor, observer, Vector2.zero, range, 360, target);

        public bool HasLineOfSight(DungeonFloorInstance floor, DungeonTraversalPosition observer, DungeonTraversalPosition target)
            => HasLineOfSight(floor, observer, target, floor?.Objects);

        private static bool HasLineOfSight(DungeonFloorInstance floor, DungeonTraversalPosition observer,
            DungeonTraversalPosition target, IReadOnlyList<DungeonObjectInstance> objects)
        {
            var map = floor?.Map;
            if (map == null || !map.TryGetSightPoint(observer, out var from)
                || !map.TryGetSightPoint(target, out var to) || map.BlocksStructuralSight(observer, target)) return false;
            for (var index = 0; index < objects.Count; index++)
            {
                var obj = objects[index];
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door && !door.IsOpen
                    && DungeonFloorMap.SightCrossesEdge(from, to, new DungeonSurfaceEdge(door.Edge))) return false;
                if (obj is ObstacleDungeonObjectInstance obstacle && obstacle.BlocksLineOfSight)
                {
                    var size = obstacle.OccupiedSizeInCells;
                    for (var y = 0; y < size.y; y++)
                    for (var x = 0; x < size.x; x++)
                    {
                        var occupiedCell = obstacle.Position + new Vector2Int(x, y);
                        var cell = new DungeonSurfacePosition(occupiedCell);
                        // The occluding object itself is observable, but hides the space behind it.
                        if ((!observer.IsOnStairs && observer.Surface == cell) || (!target.IsOnStairs && target.Surface == cell)) continue;
                        if (DungeonFloorMap.SightCrossesCell(from, to, cell)) return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Shared current visibility. Discovery memory and fog rendering are separate consumers.</summary>
        public void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            HashSet<DungeonTraversalPosition> result) => CollectPartyVisible(floor, observers, null, result);

        public void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            float range, HashSet<DungeonTraversalPosition> result) => CollectPartyVisible(floor, observers, (float?)range, result);

        public void CollectPartyVisibleAndDiscovered(DungeonFloorInstance floor,
            IReadOnlyList<DungeonTraversalPosition> observers, HashSet<DungeonTraversalPosition> result,
            HashSet<DungeonTraversalPosition> discovered)
            => CollectPartyVisible(floor, observers, null, result, discovered);

        private void CollectPartyVisible(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers,
            float? range, HashSet<DungeonTraversalPosition> result, HashSet<DungeonTraversalPosition> discovered = null)
        {
            foreach (var step in CollectPartyVisibleWork(floor, observers, range, result, discovered)) { }
        }

        internal IEnumerable<bool> CollectPartyVisibleWork(DungeonFloorInstance floor,
            IReadOnlyList<DungeonTraversalPosition> observers, float? range,
            HashSet<DungeonTraversalPosition> result, HashSet<DungeonTraversalPosition> discovered)
        {
            var input = CaptureInput(floor, floor?.Map);
            foreach (var step in CollectPartyVisibleWork(input, observers, range, result, discovered)) yield return step;
        }

        // Capture on the owner thread. Workers receive no floor, actor, object instance or ScriptableObject.
        internal sealed class VisibilityInput
        {
            public DungeonFloorMap Map;
            public readonly List<DungeonSurfaceEdge> Doors = new();
            public readonly List<DungeonSurfacePosition> Obstacles = new();
        }

        internal static VisibilityInput CaptureInput(DungeonFloorInstance floor, DungeonFloorMap map)
        {
            var input = new VisibilityInput { Map = map };
            if (floor == null) return input;
            foreach (var obj in floor.Objects)
            {
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door && !door.IsOpen)
                    input.Doors.Add(new DungeonSurfaceEdge(door.Edge));
                if (obj is ObstacleDungeonObjectInstance obstacle && obstacle.BlocksLineOfSight)
                    foreach (var cell in obstacle.EnumerateOccupiedCells())
                        input.Obstacles.Add(new DungeonSurfacePosition(cell));
            }
            return input;
        }

        private static bool HasLineOfSight(VisibilityInput input, DungeonTraversalPosition observer,
            DungeonTraversalPosition target)
        {
            var map = input.Map;
            if (!map.TryGetSightPoint(observer, out var from) || !map.TryGetSightPoint(target, out var to)
                || map.BlocksStructuralSight(observer, target)) return false;
            foreach (var edge in input.Doors)
                if (DungeonFloorMap.SightCrossesEdge(from, to, edge)) return false;
            foreach (var cell in input.Obstacles)
            {
                if ((!observer.IsOnStairs && observer.Surface == cell) || (!target.IsOnStairs && target.Surface == cell)) continue;
                if (DungeonFloorMap.SightCrossesCell(from, to, cell)) return false;
            }
            return true;
        }
        internal static IEnumerable<bool> CollectPartyVisibleWork(VisibilityInput input,
            IReadOnlyList<DungeonTraversalPosition> observers, float? range,
            HashSet<DungeonTraversalPosition> result, HashSet<DungeonTraversalPosition> discovered)
        {
            result.Clear();
            discovered?.Clear();
            var map = input.Map;
            if (map == null || observers == null || observers.Count == 0) yield break;
            // Terrain visibility and lasting discovery share the same default
            // radius. Explicit ranges are still used for enemy FOV previews.
            range ??= DungeonFogSystem.DiscoveryRadius;
            foreach (var candidate in Candidates())
            {
                yield return false;
                map.TryGetTraversalCell(candidate, out var cell);
                var seen = false;
                foreach (var observer in observers)
                {
                    yield return false;
                    var near = discovered != null && map.TryGetTraversalCell(observer, out var origin)
                        && (cell - origin).sqrMagnitude <= DungeonFogSystem.DiscoveryRadius * DungeonFogSystem.DiscoveryRadius;
                    if (seen && !near) continue;
                    if (range.HasValue && (!float.IsFinite(range.Value) || range.Value < 0
                        || !map.TryGetSightPoint(observer, out var from)
                        || !map.TryGetSightPoint(candidate, out var to)
                        || new Vector2(to.x - from.x, to.y - from.y).sqrMagnitude > range.Value * range.Value)) continue;
                    if (!HasLineOfSight(input, observer, candidate)) continue;
                    if (!seen) { result.Add(candidate); seen = true; }
                    if (near) discovered.Add(candidate);
                    if (discovered == null || near) break;
                }
            }

            IEnumerable<DungeonTraversalPosition> Candidates()
            {
                foreach (var surface in map.Surfaces) yield return new DungeonTraversalPosition(surface);
                foreach (var stair in map.Stairs)
                    if (map.TryGetStairPath(stair, out var path))
                        for (var step = 1; step < path.Count - 1; step++)
                            yield return new DungeonTraversalPosition(stair, step);
            }
        }
    }
}
