using System.Collections.Generic;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Dungeons
{
    public sealed class DungeonFogSystem
    {
        private readonly DungeonVisibilitySystem visibility = new();
        private DungeonFloorMap snapshotSource, sightSnapshot;
        private int snapshotRevision = -1;
        private DungeonFloorMap rememberedBoundaryMap;
        private int rememberedBoundaryRevision = -1;
        private readonly HashSet<DungeonSurfacePosition> staticBoundaryCells = new();
        public const float DiscoveryRadius = 20f;
        public void Update(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers)
        {
            if (floor?.Map == null) return;
            var visible = new HashSet<DungeonTraversalPosition>();
            var discovered = new HashSet<DungeonTraversalPosition>();
            visibility.CollectPartyVisibleAndDiscovered(floor, observers, visible, discovered);
            ApplyVisibility(floor, visible, discovered);
        }

        public DungeonFogUpdateJob CreateUpdateJob(DungeonFloorInstance floor,
            IReadOnlyList<DungeonTraversalPosition> observers, System.Func<bool> canContinue = null,
            bool background = false)
        {
            DungeonVisibilitySystem.VisibilityInput input = null;
            if (background && floor?.Map != null)
            {
                if (!ReferenceEquals(snapshotSource, floor.Map) || snapshotRevision != floor.Map.TraversalRevision)
                {
                    snapshotSource = floor.Map;
                    snapshotRevision = floor.Map.TraversalRevision;
                    sightSnapshot = floor.Map.CreateSightSnapshot();
                }
                input = DungeonVisibilitySystem.CaptureInput(floor, sightSnapshot);
            }
            return new(floor, observers, visibility, ApplyVisibility, canContinue, input);
        }

        private void ApplyVisibility(DungeonFloorInstance floor,
            HashSet<DungeonTraversalPosition> visible, HashSet<DungeonTraversalPosition> discovered)
        {
            if (floor.Fog.RevealLegacyMap) RevealMap(floor);
            floor.Fog.SetVisible(visible, discovered);
            // A seen immovable boundary remains known even when it was first
            // observed beyond the ordinary terrain discovery radius.
            if (!ReferenceEquals(rememberedBoundaryMap, floor.Map) || rememberedBoundaryRevision != floor.Map.TraversalRevision)
            {
                staticBoundaryCells.Clear();
                foreach (var edge in floor.Map.SurfaceWallEdges)
                { staticBoundaryCells.Add(edge.First); staticBoundaryCells.Add(edge.Second); }
                rememberedBoundaryMap = floor.Map; rememberedBoundaryRevision = floor.Map.TraversalRevision;
            }
            foreach (var position in visible)
                if (!position.IsOnStairs && staticBoundaryCells.Contains(position.Surface))
                    floor.Fog.RestoreDiscovered(floor.Map, position);
            foreach (var obj in floor.Objects)
            {
                if (IsObjectVisible(floor, obj)) Remember(floor, obj);
                else if (obj is ObstacleDungeonObjectInstance obstacle
                    && floor.Fog.Objects.TryGetValue(obj.InstanceId, out var known) && known.Active)
                    RememberStaticFootprint(floor, obstacle);
            }
            foreach (var body in floor.EnemyBodies)
            {
                var position = floor.TryGetActorPosition("body:" + body.InstanceId, out var stored) ? stored
                    : new DungeonTraversalPosition(new DungeonSurfacePosition(body.Position));
                if (floor.Fog.IsVisible(position)) floor.Fog.Bodies.Add(body.InstanceId);
            }
            var closed = new HashSet<DungeonSurfaceEdge>(floor.Map.SurfaceClosedProgressionGateEdges);
            floor.Fog.ClosedGates.RemoveWhere(edge => !closed.Contains(edge) && EdgeSeen(floor, edge));
            foreach (var edge in closed) if (EdgeSeen(floor, edge)) floor.Fog.ClosedGates.Add(edge);
        }
        public static void RevealMap(DungeonFloorInstance floor)
        {
            if (floor?.Map == null) return;
            foreach (var surface in floor.Map.Surfaces) floor.Fog.RestoreDiscovered(floor.Map, new DungeonTraversalPosition(surface));
            foreach (var stair in floor.Map.Stairs)
                if (floor.Map.TryGetStairPath(stair, out var path))
                    for (var i = 1; i < path.Count - 1; i++) floor.Fog.RestoreDiscovered(floor.Map, new DungeonTraversalPosition(stair, i));
            foreach (var obj in floor.Objects) Remember(floor, obj);
            foreach (var body in floor.EnemyBodies) floor.Fog.Bodies.Add(body.InstanceId);
            floor.Fog.ClosedGates.UnionWith(floor.Map.SurfaceClosedProgressionGateEdges);
            floor.Fog.RevealLegacyMap = false;
        }

        private static bool EdgeSeen(DungeonFloorInstance floor, DungeonSurfaceEdge edge)
            => floor.Fog.IsVisible(new DungeonTraversalPosition(edge.First)) || floor.Fog.IsVisible(new DungeonTraversalPosition(edge.Second));

        public static bool IsObjectVisible(DungeonFloorInstance floor, DungeonObjectInstance obj)
        {
            if (obj is ObstacleDungeonObjectInstance obstacle)
            {
                var size = obstacle.OccupiedSizeInCells;
                for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++)
                    if (floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(obstacle.Position + new UnityEngine.Vector2Int(x, y))))) return true;
                return false;
            }
            return obj is DoorDungeonObjectInstance door
                ? floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(door.Edge.FirstCell)))
                    || floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(door.Edge.SecondCell)))
                : floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(obj.Position)));
        }

        public static bool ReadState(DungeonObjectInstance obj) => obj switch
        {
            DoorDungeonObjectInstance door => door.IsOpen,
            LeverDungeonObjectInstance lever => lever.IsActivated,
            ConditionDungeonObjectInstance condition => condition.IsSatisfied,
            SpikeTrapDungeonObjectInstance trap => trap.IsTriggered,
            ChestDungeonObjectInstance chest => chest.IsUnlocked,
            _ => false
        };

        private static void Remember(DungeonFloorInstance floor, DungeonObjectInstance obj)
        {
            if (!floor.Fog.Objects.TryGetValue(obj.InstanceId, out var known))
                floor.Fog.Objects.Add(obj.InstanceId, known = new DungeonObjectKnowledge());
            known.Active = obj.IsActive;
            known.State = ReadState(obj);
            if (obj is ObstacleDungeonObjectInstance obstacle) RememberStaticFootprint(floor, obstacle);
        }
        private static void RememberStaticFootprint(DungeonFloorInstance floor, ObstacleDungeonObjectInstance obstacle)
        {
            var size = obstacle.OccupiedSizeInCells;
            for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++)
                floor.Fog.RestoreDiscovered(floor.Map, new DungeonTraversalPosition(new DungeonSurfacePosition(obstacle.Position + new UnityEngine.Vector2Int(x, y))));
        }
    }
}
