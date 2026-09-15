using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private DungeonFloorInstance traversalFloor;
        private DungeonFloorMapView traversalMapView;
        private readonly Queue<DungeonTraversalPosition> traversalPath = new();
        private readonly Dictionary<string, FollowerRoute> followerRoutes = new();
        private readonly Dictionary<DungeonCellEdge, DoorDungeonObjectInstance> navigationDoors = new();
        private sealed class FollowerRoute
        {
            public readonly Queue<DungeonTraversalPosition> Path = new();
            public DungeonTraversalPosition? Target;
            public float NextPlan;
        }
        private float leaderStalledTime;
        private Vector2 lastLeaderNavigationPosition;

        public bool CanInteractWithGroundObjects => GetActiveLeaderView() is { } leader
            && (!leader.UsesLayeredTraversal || (!leader.DungeonPosition.IsOnStairs && leader.DungeonPosition.Surface.Level == 0));

        public bool CanInteractAt(DungeonTraversalPosition target)
        {
            var leader = GetActiveLeaderView();
            if (leader == null) return false;
            if (!leader.UsesLayeredTraversal) return !target.IsOnStairs && target.Surface.Level == 0;
            return target == leader.DungeonPosition || traversalFloor.Map.GetTraversalNeighbours(leader.DungeonPosition).Contains(target);
        }

        public void ConfigureTraversal(DungeonFloorInstance floor, DungeonFloorMapView map, CombatGridFeedbackRenderer feedback)
        {
            CancelMovement();
            traversalFloor = floor; traversalMapView = map;
            navigationDoors.Clear();
            if (floor != null) foreach (var door in floor.Objects.OfType<DoorDungeonObjectInstance>()) navigationDoors[door.Edge] = door;
            foreach (var view in viewsById.Values) view.CanEnterTraversal = position => CanEnterTraversal(view, position);
        }

        public bool CanEnterTraversal(ExplorationCharacterView actor, DungeonTraversalPosition position)
        {
            if (traversalFloor?.Map == null || !traversalFloor.Map.TryGetTraversalCell(position, out var cell)) return false;
            if (!position.IsOnStairs && position.Surface.Level == 0
                && traversalFloor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o => o.IsActive && o.BlocksMovement && o.Position == cell)) return false;
            return true;
        }

        private bool FindTraversalPath(ExplorationCharacterView actor, DungeonTraversalPosition target, out List<DungeonTraversalPosition> path)
            => DungeonExplorationPathfinder.TryFindPath(traversalFloor.Map, actor.DungeonPosition, target, out path,
                position => CanEnterTraversal(actor, position), CanOpenNavigationDoor);

        private bool CanOpenNavigationDoor(DungeonSurfaceEdge edge)
            => edge.Level == 0 && navigationDoors.TryGetValue(edge.CellEdge, out var door)
                && door.InteractionMode != Godmist.Core.Enums.DoorInteractionMode.LeverOnly;

        private void TrySetTraversalDestination(Vector3 world)
        {
            var leader = GetActiveLeaderView();
            if (leader == null || traversalFloor?.Map == null || !traversalMapView.TryGetVisibleTraversal(world, out var target)
                || traversalMapView.Knowledge(target) == DungeonKnowledge.Unexplored
                || DungeonEnemyBodyView.IsPointerOverBody(world) || !FindTraversalPath(leader, target, out var path)) return;
            CancelMovement();
            movementDestination = world; leaderStalledTime = 0; lastLeaderNavigationPosition = leader.Position;
            foreach (var position in path.Skip(1)) traversalPath.Enqueue(position);
        }

        private void TickTraversalMovement(ExplorationCharacterView leader)
        {
            var keyboard = cameraController == null || cameraController.IsFollowingLeader
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
            if (keyboard.sqrMagnitude > 0)
            {
                destroyTarget = null;
                traversalPath.Clear(); movementDestination = null; leader.PreferredTraversalDestination = null;
                leader.Move(Vector2.ClampMagnitude(keyboard, 1) * movementSpeed * Time.fixedDeltaTime);
                traversalMapView.FocusElevation(leader.DungeonPosition, true);
                return;
            }
            TickDestroyObstacle(leader);
            var before = lastLeaderNavigationPosition;
            lastLeaderNavigationPosition = leader.Position;
            if (!AdvanceRoute(leader, traversalPath, movementDestination, 0))
            {
                var pending = destroyTarget;
                var arrived = movementDestination.HasValue && Vector2.Distance(leader.Position, movementDestination.Value) < .1f;
                CancelMovement();
                if (arrived) destroyTarget = pending;
            }
            if (movementDestination.HasValue && traversalPath.Count == 0
                && Vector2.Distance(leader.Position, movementDestination.Value) <= .03f) movementDestination = null;
            if (movementDestination.HasValue && !leader.IsCrossingLowWall)
            {
                leaderStalledTime = Vector2.Distance(before, lastLeaderNavigationPosition) < .0001f
                    ? leaderStalledTime + Time.fixedDeltaTime : 0;
                if (leaderStalledTime > .6f) CancelMovement();
            }
            if (traversalPath.Count > 0 || movementDestination.HasValue) traversalMapView.FocusElevation(leader.DungeonPosition, true);
        }

        // Consume a waypoint only after reaching it, not merely entering its cell.
        // This prevents cutting the inside of a blocked corner and keeps stair identity explicit.
        private bool AdvanceRoute(ExplorationCharacterView actor, Queue<DungeonTraversalPosition> route,
            Vector2? finalPoint, float stoppingDistance)
        {
            while (route.Count > 0)
            {
                var next = route.Peek();
                if (!traversalFloor.Map.TryGetTraversalCell(next, out var cell) || !CanEnterTraversal(actor, next)) return false;
                var center = (Vector2)traversalMapView.CellToWorldCenter(cell);
                if (actor.DungeonPosition == next && Vector2.Distance(actor.Position, center) <= .03f)
                { route.Dequeue(); continue; }
                if (actor.DungeonPosition != next)
                {
                    if (!traversalFloor.Map.GetTraversalNeighbours(actor.DungeonPosition, CanOpenNavigationDoor).Contains(next)) return false;
                    OpenNextDoor(actor, next);
                }
                actor.PreferredTraversalDestination = next;
                actor.MoveTowards(center, movementSpeed * Time.fixedDeltaTime, 0);
                return true;
            }
            actor.PreferredTraversalDestination = null;
            if (finalPoint.HasValue) actor.MoveTowards(finalPoint.Value, movementSpeed * Time.fixedDeltaTime, stoppingDistance);
            return true;
        }

        private void OpenNextDoor(ExplorationCharacterView actor, DungeonTraversalPosition next)
        {
            var current = actor.DungeonPosition;
            if (current.IsOnStairs || next.IsOnStairs || current.Surface.Level != 0 || next.Surface.Level != 0) return;
            var delta = next.Surface.Cell - current.Surface.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return;
            var edge = new DungeonCellEdge(current.Surface.Cell, next.Surface.Cell);
            if (!navigationDoors.TryGetValue(edge, out var door) || door.IsOpen
                || door.InteractionMode == Godmist.Core.Enums.DoorInteractionMode.LeverOnly) return;
            var at = (Vector2)traversalMapView.CellToWorldCenter(current.Surface.Cell);
            var beyond = (Vector2)traversalMapView.CellToWorldCenter(next.Surface.Cell);
            if (Vector2.Distance(actor.Position, (at + beyond) * .5f) <= Vector2.Distance(at, beyond) * .8f)
                traversalFloor.TrySetDoorOpen(door.InstanceId, true);
        }

        private readonly Queue<Vector2> cityLeaderRoute = new();
        private readonly Dictionary<string, CityRoute> cityFollowerRoutes = new();
        private sealed class CityRoute
        {
            public readonly Queue<Vector2> Points = new();
            public Vector2 Target;
            public float NextPlan;
        }

        private bool TrySetCityDestination(Vector2 goal)
        {
            var actor = GetActiveLeaderView();
            if (actor == null || !TryFindWorldRoute(actor, goal, out var points)) return false;
            CancelMovement(); movementDestination = goal;
            foreach (var point in points) cityLeaderRoute.Enqueue(point);
            return true;
        }

        private static bool TryFindWorldRoute(ExplorationCharacterView actor, Vector2 goal, out List<Vector2> points)
        {
            points = new(); var start = actor.Position;
            if (!actor.HasClearMovementSegment(goal, goal)) return false;
            if (actor.HasClearMovementSegment(start, goal)) { points.Add(goal); return true; }
            const float step = .5f;
            var open = new List<Vector2Int> { Vector2Int.zero };
            var costs = new Dictionary<Vector2Int, float> { [Vector2Int.zero] = 0 };
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            Vector2 World(Vector2Int cell) => start + (Vector2)cell * step;
            for (var budget = 0; budget < 2048 && open.Count > 0; budget++)
            {
                var best = 0; var score = float.PositiveInfinity;
                for (var i = 0; i < open.Count; i++)
                {
                    var candidate = costs[open[i]] + Vector2.Distance(World(open[i]), goal);
                    if (candidate < score) { best = i; score = candidate; }
                }
                var current = open[best]; open.RemoveAt(best); closed.Add(current);
                var at = World(current);
                if (Vector2.Distance(at, goal) <= step * 1.5f && actor.HasClearMovementSegment(at, goal))
                {
                    points.Add(goal);
                    while (current != Vector2Int.zero) { points.Add(World(current)); current = parents[current]; }
                    points.Reverse(); return true;
                }
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0) continue;
                    var next = current + new Vector2Int(x, y);
                    if (closed.Contains(next) || !actor.HasClearMovementSegment(at, World(next))) continue;
                    var cost = costs[current] + Vector2.Distance(at, World(next));
                    if (costs.TryGetValue(next, out var old) && old <= cost) continue;
                    if (!costs.ContainsKey(next)) open.Add(next);
                    costs[next] = cost; parents[next] = current;
                }
            }
            return false;
        }

        private static Vector2 CityWaypoint(ExplorationCharacterView actor, Queue<Vector2> points, Vector2 final)
        {
            if (actor.HasClearMovementSegment(actor.Position, final)) { points.Clear(); return final; }
            while (points.Count > 0 && Vector2.Distance(actor.Position, points.Peek()) < .06f) points.Dequeue();
            // Skip intermediate search nodes when the character footprint has a clear segment.
            while (points.Count > 1)
            {
                var next = points.ElementAt(1);
                if (!actor.HasClearMovementSegment(actor.Position, next)) break;
                points.Dequeue();
            }
            return points.Count > 0 ? points.Peek() : actor.Position;
        }

        private void FollowCity(ExplorationCharacterView actor, ExplorationCharacterView target)
        {
            if (actor.HasClearMovementSegment(actor.Position, target.Position))
            {
                cityFollowerRoutes.Remove(actor.CharacterId);
                actor.MoveTowards(target.Position, movementSpeed * Time.fixedDeltaTime, followerDistance);
                return;
            }
            if (!cityFollowerRoutes.TryGetValue(actor.CharacterId, out var route)) cityFollowerRoutes.Add(actor.CharacterId, route = new CityRoute());
            if (Time.time >= route.NextPlan && (route.Points.Count == 0 || Vector2.Distance(route.Target, target.Position) > .5f))
            {
                route.NextPlan = Time.time + .5f; route.Target = target.Position; route.Points.Clear();
                if (TryFindWorldRoute(actor, target.Position, out var points)) foreach (var point in points) route.Points.Enqueue(point);
                else route.NextPlan = Time.time + 1f;
            }
            if (route.Points.Count > 0) actor.MoveTowards(CityWaypoint(actor, route.Points, route.Target), movementSpeed * Time.fixedDeltaTime, 0);
        }

        private bool CanFollowDirectly(ExplorationCharacterView actor, ExplorationCharacterView target)
        {
            if (actor.DungeonPosition.IsOnStairs || target.DungeonPosition.IsOnStairs
                || actor.DungeonPosition.Surface.Level != target.DungeonPosition.Surface.Level) return false;
            var level = actor.DungeonPosition.Surface.Level;
            var start = actor.Position; var end = target.Position;
            var step = Vector2.Distance(traversalMapView.CellToWorldCenter(Vector2Int.zero), traversalMapView.CellToWorldCenter(Vector2Int.right)) * .2f;
            var count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / step));
            var radius = actor.MovementRadius + traversalMapView.WallHalfThickness;
            foreach (var offset in new[] { Vector2.zero, Vector2.up * radius, Vector2.down * radius, Vector2.left * radius, Vector2.right * radius })
            {
                var previous = new DungeonSurfacePosition(traversalMapView.WorldToCell(start + offset), level);
                if (!CanEnterTraversal(actor, new DungeonTraversalPosition(previous))) return false;
                for (var i = 1; i <= count; i++)
                {
                    var current = new DungeonSurfacePosition(traversalMapView.WorldToCell(Vector2.Lerp(start, end, (float)i / count) + offset), level);
                    if (!CanEnterTraversal(actor, new DungeonTraversalPosition(current))) return false;
                    if (current != previous)
                    {
                        var corner = new DungeonSurfacePosition(new Vector2Int(current.Cell.x, previous.Cell.y), level);
                        var other = new DungeonSurfacePosition(new Vector2Int(previous.Cell.x, current.Cell.y), level);
                        bool Open(DungeonSurfacePosition a, DungeonSurfacePosition b) => a == b || traversalFloor.Map.CanTraverseBetweenSurfaces(a, b);
                        if (!Open(previous, corner) || !Open(corner, current) || !Open(previous, other) || !Open(other, current)) return false;
                    }
                    previous = current;
                }
            }
            return true;
        }

        private void FollowTraversal(ExplorationCharacterView follower, ExplorationCharacterView previous)
        {
            var target = previous.DungeonPosition;
            if (!followerRoutes.TryGetValue(follower.CharacterId, out var route))
                followerRoutes.Add(follower.CharacterId, route = new FollowerRoute());
            if (CanFollowDirectly(follower, previous))
            {
                route.Path.Clear(); route.Target = null;
                follower.PreferredTraversalDestination = null;
                follower.MoveTowards(previous.Position, movementSpeed * Time.fixedDeltaTime, followerDistance);
                return;
            }
            traversalFloor.Map.TryGetTraversalCell(follower.DungeonPosition, out var currentCell);
            var atWaypoint = Vector2.Distance(follower.Position, traversalMapView.CellToWorldCenter(currentCell)) <= .04f;
            if (Time.time >= route.NextPlan && (route.Path.Count == 0 || (route.Target != target && atWaypoint)))
            {
                route.NextPlan = Time.time + .3f;
                route.Path.Clear(); route.Target = target;
                if (FindTraversalPath(follower, target, out var path))
                    foreach (var step in path.Skip(1)) route.Path.Enqueue(step);
                else route.NextPlan = Time.time + 1f;
            }
            if (route.Path.Count == 0) return;
            if (!AdvanceRoute(follower, route.Path, null, 0))
            { route.Path.Clear(); route.Target = null; follower.PreferredTraversalDestination = null; }
        }

        private ObstacleDungeonObjectInstance destroyTarget;
        private float nextDestroyHit;
        public ObstacleDungeonObjectInstance DestructionTarget => destroyTarget;

        public bool CanOrderDestruction => gameSession != null && !gameSession.IsDefeated
            && gameSession.CurrentMode == Godmist.Core.Enums.GameModeState.Exploration
            && gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) == null;

        public bool BeginDestroyObstacle(ObstacleDungeonObjectInstance target)
        {
            var leader = GetActiveLeaderView();
            if (!CanOrderDestruction || target == null || !target.IsActive || !target.IsDestructible || leader == null || traversalMapView == null
                || leader.RuntimeCharacter?.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive
                || gameSession.GetCombatForCharacter(leader.CharacterId) != null
                || !traversalMapView.IsCurrentlySeen(new DungeonTraversalPosition(new DungeonSurfacePosition(target.Position)))) return false;
            List<DungeonTraversalPosition> best = null;
            var destination = new DungeonTraversalPosition(new DungeonSurfacePosition(target.Position));
            foreach (var adjacent in traversalFloor.Map.GetTraversalNeighbours(destination))
                if (CanEnterTraversal(leader, adjacent) && FindTraversalPath(leader, adjacent, out var path)
                    && (best == null || path.Count < best.Count)) best = path;
            if (best == null) return false;
            CancelMovement();
            traversalFloor.Map.TryGetTraversalCell(best[best.Count - 1], out var cell);
            movementDestination = traversalMapView.CellToWorldCenter(cell);
            foreach (var node in best.Skip(1)) traversalPath.Enqueue(node);
            lastLeaderNavigationPosition = leader.Position; leaderStalledTime = 0;
            destroyTarget = target; nextDestroyHit = Time.time + .75f;
            return true;
        }
        private void TickDestroyObstacle(ExplorationCharacterView leader)
        {
            if (destroyTarget == null) return;
            if (!destroyTarget.IsActive || leader.RuntimeCharacter?.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive
                || gameSession.GetCombatForCharacter(leader.CharacterId) != null
                || !CanInteractWithGroundObjects) { destroyTarget = null; return; }
            if (movementDestination.HasValue || Time.time < nextDestroyHit) return;
            var node = new DungeonTraversalPosition(new DungeonSurfacePosition(destroyTarget.Position));
            if (!traversalMapView.IsCurrentlySeen(node)
                || !traversalFloor.Map.GetTraversalNeighbours(leader.DungeonPosition).Contains(node)) { destroyTarget = null; return; }
            leader.FaceTowards(traversalMapView.CellToWorldCenter(destroyTarget.Position));
            destroyTarget.ApplyHits(1); nextDestroyHit = Time.time + .75f;
            if (!destroyTarget.IsActive) destroyTarget = null;
        }
    }
}
