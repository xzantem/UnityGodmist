using System.Collections.Generic;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Dungeons
{
    public sealed class DungeonFogSystem
    {
        private readonly DungeonVisibilitySystem visibility = new();
        private readonly HashSet<DungeonTraversalPosition> visible = new();
        private readonly HashSet<DungeonTraversalPosition> discovered = new();
        public const float DiscoveryRadius = 5f;
        public void Update(DungeonFloorInstance floor, IReadOnlyList<DungeonTraversalPosition> observers)
        {
            if (floor?.Map == null) return;
            if (floor.Fog.RevealLegacyMap)
            {
                foreach (var surface in floor.Map.Surfaces) floor.Fog.RestoreDiscovered(floor.Map, new DungeonTraversalPosition(surface));
                foreach (var stair in floor.Map.Stairs)
                    if (floor.Map.TryGetStairPath(stair, out var path))
                        for (var i = 1; i < path.Count - 1; i++) floor.Fog.RestoreDiscovered(floor.Map, new DungeonTraversalPosition(stair, i));
                foreach (var obj in floor.Objects) Remember(floor, obj);
                foreach (var body in floor.EnemyBodies) floor.Fog.Bodies.Add(body.InstanceId);
                floor.Fog.ClosedGates.UnionWith(floor.Map.SurfaceClosedProgressionGateEdges);
                floor.Fog.RevealLegacyMap = false;
            }
            visibility.CollectPartyVisible(floor, observers, visible);
            discovered.Clear();
            foreach (var position in visible)
            {
                if (!floor.Map.TryGetTraversalCell(position, out var cell)) continue;
                foreach (var observer in observers)
                    if (floor.Map.TryGetTraversalCell(observer, out var origin)
                        && (cell - origin).sqrMagnitude <= DiscoveryRadius * DiscoveryRadius
                        && visibility.HasLineOfSight(floor, observer, position))
                    { discovered.Add(position); break; }
            }
            floor.Fog.SetVisible(visible, discovered);
            foreach (var obj in floor.Objects) if (IsObjectVisible(floor, obj)) Remember(floor, obj);
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
        private static bool EdgeSeen(DungeonFloorInstance floor, DungeonSurfaceEdge edge)
            => floor.Fog.IsVisible(new DungeonTraversalPosition(edge.First)) || floor.Fog.IsVisible(new DungeonTraversalPosition(edge.Second));

        public static bool IsObjectVisible(DungeonFloorInstance floor, DungeonObjectInstance obj)
            => obj is DoorDungeonObjectInstance door
                ? floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(door.Edge.FirstCell)))
                    || floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(door.Edge.SecondCell)))
                : floor.Fog.IsVisible(new DungeonTraversalPosition(new DungeonSurfacePosition(obj.Position)));

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
        }
    }
}
