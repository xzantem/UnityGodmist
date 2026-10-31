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
        private readonly Dictionary<string,string> forestEnemyRooms = new();
        private DungeonFloorInstance traversalFloor;
        private DungeonFloorMapView traversalMapView;
        private readonly Queue<DungeonTraversalPosition> traversalPath = new();
        private readonly Dictionary<string, FollowerRoute> followerRoutes = new();
        private readonly Dictionary<DungeonCellEdge, DoorDungeonObjectInstance> navigationDoors = new();
        private static readonly int[] followerGoalRingOrder = { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6 };
        // The first ring preserves the authored party spacing. A single wider
        // ring is only considered when that spacing cannot be placed safely
        // (for example when the leader is standing beside a pillar).
        private static readonly float[] followerGoalRingRadiusMultipliers = { 1f, 1.5f };
        private const float FollowerGoalRingStep = Mathf.PI / 6f;
        private static readonly Vector2[] footprintDirections =
            { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        private System.Func<DungeonSurfaceEdge, bool> openNavigationDoorQuery;
        // Kept for focused editor regressions. An invalid endpoint must be
        // rejected before the any-angle search starts, rather than exhausting
        // the map on every follower tick.
        private int lastTraversalPathExpandedNodes;
        private sealed class FollowerRoute
        {
            public readonly Queue<DungeonTraversalPosition> Path = new();
            public DungeonTraversalPosition? Target;
            public Vector2? PlannedTargetWorld;
            public float NextPlan;
            public bool HasFailedPlan;
            public DungeonTraversalPosition FailedStart;
            public DungeonTraversalPosition FailedTarget;
            public Vector2 FailedStartWorld;
            public Vector2 FailedTargetWorld;
            public int FailedNavigationRevision;
            public bool HasResolvedGoal;
            public bool ResolvedGoalAvailable;
            public DungeonTraversalPosition ResolvedStart;
            public DungeonTraversalPosition ResolvedTarget;
            public Vector2 ResolvedStartWorld;
            public Vector2 ResolvedTargetWorld;
            public int ResolvedNavigationRevision;
            public DungeonTraversalPosition ResolvedGoal;
            public Vector2 ResolvedGoalWorld;
            public bool ResolvedAtStopDistance;

            private const float FailedStartPositionTolerance = .02f;
            private const float FailedTargetPositionTolerance = .02f;
            private const float ResolvedPositionTolerance = .02f;

            public bool MatchesFailedRequest(
                DungeonTraversalPosition start,
                Vector2 startWorld,
                DungeonTraversalPosition target,
                Vector2 targetWorld,
                int navigationRevision)
                => HasFailedPlan
                    && FailedStart == start
                    && FailedTarget == target
                    && (FailedStartWorld - startWorld).sqrMagnitude <= FailedStartPositionTolerance * FailedStartPositionTolerance
                    && (FailedTargetWorld - targetWorld).sqrMagnitude <= FailedTargetPositionTolerance * FailedTargetPositionTolerance
                    && FailedNavigationRevision == navigationRevision;

            public void ClearFailedPlan() => HasFailedPlan = false;

            public void InvalidateResolvedGoal() => HasResolvedGoal = false;

            public bool MatchesResolvedGoal(
                DungeonTraversalPosition start,
                Vector2 startWorld,
                DungeonTraversalPosition target,
                Vector2 targetWorld,
                int navigationRevision)
                => HasResolvedGoal
                    && ResolvedStart == start
                    && ResolvedTarget == target
                    && (ResolvedStartWorld - startWorld).sqrMagnitude <= ResolvedPositionTolerance * ResolvedPositionTolerance
                    && (ResolvedTargetWorld - targetWorld).sqrMagnitude <= ResolvedPositionTolerance * ResolvedPositionTolerance
                    && ResolvedNavigationRevision == navigationRevision;

            public void RememberResolvedGoal(
                DungeonTraversalPosition start,
                Vector2 startWorld,
                DungeonTraversalPosition target,
                Vector2 targetWorld,
                int navigationRevision,
                bool available,
                DungeonTraversalPosition goal,
                Vector2 goalWorld,
                bool atStopDistance)
            {
                HasResolvedGoal = true;
                ResolvedStart = start;
                ResolvedStartWorld = startWorld;
                ResolvedTarget = target;
                ResolvedTargetWorld = targetWorld;
                ResolvedNavigationRevision = navigationRevision;
                ResolvedGoalAvailable = available;
                ResolvedGoal = goal;
                ResolvedGoalWorld = goalWorld;
                ResolvedAtStopDistance = atStopDistance;
            }

            public void RememberFailedPlan(
                DungeonTraversalPosition start,
                Vector2 startWorld,
                DungeonTraversalPosition target,
                Vector2 targetWorld,
                int navigationRevision)
            {
                HasFailedPlan = true;
                FailedStart = start;
                FailedTarget = target;
                FailedStartWorld = startWorld;
                FailedTargetWorld = targetWorld;
                FailedNavigationRevision = navigationRevision;
            }
        }
        private float leaderStalledTime;
        private Vector2 lastLeaderNavigationPosition;
        private int followerNavigationRevision;
        private bool followerNavigationRevisionValid;
        private bool followerNavigationRevisionBatch;
        private int followerBatchTopologyRevision;
        private readonly Dictionary<Vector2Int, List<ObstacleDungeonObjectInstance>> followerObstacleCells = new();
        private DungeonFloorInstance followerObstacleFloor;
        private int followerObstacleRevision;
        private readonly Dictionary<(Vector2Int Cell, string Room, bool Ent), bool> forestOccupancy = new();
        private DungeonFloorMap forestOccupancyMap;
        private int forestOccupancyRevision;
        public int ForestOccupancyEvaluationCount { get; private set; }

        // Patrol pathfinding shares the follower footprint index. Each
        // synchronous batch starts with fresh object state; ordinary callers
        // keep immediate queries. Nested scopes restore their caller's state.
        public TraversalQueryBatch BeginTraversalQueryBatch() => new(this);
        public readonly struct TraversalQueryBatch : System.IDisposable
        {
            private readonly ExplorationPartyController owner;
            private readonly bool previousBatch, previousValid;
            private readonly int previousRevision, previousTopologyRevision;
            internal TraversalQueryBatch(ExplorationPartyController owner)
            {
                this.owner = owner;
                previousBatch = owner.followerNavigationRevisionBatch;
                previousValid = owner.followerNavigationRevisionValid;
                previousRevision = owner.followerNavigationRevision;
                previousTopologyRevision = owner.followerBatchTopologyRevision;
                if (!previousBatch || !previousValid
                    || previousTopologyRevision != (owner.traversalFloor?.Map?.TraversalRevision ?? 0))
                {
                    owner.followerNavigationRevision = owner.GetTraversalNavigationRevision();
                    owner.followerBatchTopologyRevision = owner.traversalFloor?.Map?.TraversalRevision ?? 0;
                }
                owner.followerNavigationRevisionBatch = owner.followerNavigationRevisionValid = true;
            }
            public void Dispose()
            {
                if (owner == null) return;
                owner.followerNavigationRevisionBatch = previousBatch;
                owner.followerNavigationRevisionValid = previousValid;
                owner.followerNavigationRevision = previousRevision;
                owner.followerBatchTopologyRevision = previousTopologyRevision;
            }
        }

        private IEnumerable<ObstacleDungeonObjectInstance> NavigationObstaclesAt(Vector2Int cell)
        {
            // The navigation batch already computes a fresh topology/object
            // revision. Reuse it for all footprint samples, rather than
            // scanning every dungeon object for every sampled empty cell.
            // Other callers retain immediate, uncached object enumeration.
            if (!followerNavigationRevisionBatch || !followerNavigationRevisionValid)
                return traversalFloor.Objects.OfType<ObstacleDungeonObjectInstance>();
            CurrentTraversalNavigationRevision();
            if (followerObstacleFloor != traversalFloor || followerObstacleRevision != followerNavigationRevision)
            {
                using var indexSample = ObstacleIndexSample.Auto();
                followerObstacleCells.Clear();
                foreach (var obstacle in traversalFloor.Objects.OfType<ObstacleDungeonObjectInstance>())
                {
                    var occupied = new HashSet<Vector2Int>(obstacle.EnumerateOccupiedCells());
                    occupied.UnionWith(obstacle.EnumeratePhysicalExplorationCells());
                    foreach (var position in occupied)
                    {
                        if (!followerObstacleCells.TryGetValue(position, out var objects))
                            followerObstacleCells.Add(position, objects = new List<ObstacleDungeonObjectInstance>());
                        objects.Add(obstacle);
                    }
                }
                followerObstacleFloor = traversalFloor;
                followerObstacleRevision = followerNavigationRevision;
            }
            return followerObstacleCells.TryGetValue(cell, out var candidates)
                ? candidates : System.Array.Empty<ObstacleDungeonObjectInstance>();
        }

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
            DisposeNavigationMeshes();
            followerObstacleCells.Clear(); followerObstacleFloor = null;
            forestOccupancy.Clear(); forestOccupancyMap = null;
            traversalFloor = floor; traversalMapView = map;
#if UNITY_EDITOR
            failedTraversalDiagnosticsLogged = false;
#endif
            navigationDoors.Clear();
            forestEnemyRooms.Clear();
            if (floor != null) foreach(var encounter in floor.Encounters) foreach(var enemy in encounter.Enemies)
                forestEnemyRooms[enemy.InstanceId] = encounter.RoomId;
            if (floor != null) foreach (var door in floor.Objects.OfType<DoorDungeonObjectInstance>()) navigationDoors[door.Edge] = door;
            foreach (var view in viewsById.Values)
            {
                view.CanEnterTraversal = position => CanEnterTraversal(view, position);
                view.CanEnterTraversalAtWorld = (position, world) =>
                    CanEnterTraversalAtWorld(view, position, world);
            }
            PrepareNavigationMeshes();
        }

        private int GetTraversalNavigationRevision()
        {
            using var revisionSample = NavigationRevisionSample.Auto();
            if (traversalFloor?.Map == null) return 0;

            unchecked
            {
                var revision = traversalFloor.Map.TraversalRevision;
                foreach (var dungeonObject in traversalFloor.Objects)
                {
                    switch (dungeonObject)
                    {
                        case ObstacleDungeonObjectInstance obstacle:
                            revision = revision * 31 + obstacle.Position.GetHashCode();
                            revision = revision * 31 + obstacle.IsActive.GetHashCode();
                            revision = revision * 31 + obstacle.BlocksMovement.GetHashCode();
                            break;
                        case DoorDungeonObjectInstance door:
                            revision = revision * 31 + door.Edge.GetHashCode();
                            revision = revision * 31 + door.IsActive.GetHashCode();
                            revision = revision * 31 + door.IsOpen.GetHashCode();
                            break;
                    }
                }

                return revision;
            }
        }

        private int CurrentTraversalNavigationRevision()
        {
            if (!followerNavigationRevisionBatch) return GetTraversalNavigationRevision();
            var topologyRevision = traversalFloor?.Map?.TraversalRevision ?? 0;
            if (!followerNavigationRevisionValid || followerBatchTopologyRevision != topologyRevision)
            {
                followerNavigationRevision = GetTraversalNavigationRevision();
                followerBatchTopologyRevision = topologyRevision;
                followerNavigationRevisionValid = true;
            }
            return followerNavigationRevision;
        }

        private bool ForestEnemyCanEnter(ExplorationCharacterView actor, Vector2Int cell)
        {
            if (actor == null || string.IsNullOrEmpty(actor.EnemyInstanceId) || traversalMapView == null
                || !traversalMapView.UsesVegetationPresentation) return true;
            var ent = ForestEnemyNavigation.IsEnt(actor.RuntimeCharacter?.Definition?.Id);
            forestEnemyRooms.TryGetValue(actor.EnemyInstanceId,out var homeRoom);
            return CanOccupyForestCell(traversalFloor.Map, cell, homeRoom, ent);
        }

        private bool CanOccupyForestCell(DungeonFloorMap map, Vector2Int cell, string room, bool ent)
        {
            if (map == null) return false;
            if (!ReferenceEquals(forestOccupancyMap, map) || forestOccupancyRevision != map.TraversalRevision)
            {
                forestOccupancy.Clear(); forestOccupancyMap = map;
                forestOccupancyRevision = map.TraversalRevision;
            }
            // This caches only immutable map footprint rules, never physical
            // blockers, doors, actor positions or gameplay enterability.
            var key = (cell, ent ? room : null, ent);
            if (!forestOccupancy.TryGetValue(key, out var allowed))
            {
                if (forestOccupancy.Count >= 4096) forestOccupancy.Clear();
                forestOccupancy.Add(key, allowed = ForestEnemyNavigation.CanOccupy(map, cell, room, ent));
                ForestOccupancyEvaluationCount++;
            }
            return allowed;
        }

        public bool CanEnterTraversal(ExplorationCharacterView actor, DungeonTraversalPosition position)
        {
            if (traversalFloor?.Map == null || !traversalFloor.Map.TryGetTraversalCell(position, out var cell)) return false;
            if (!ForestEnemyCanEnter(actor, cell)) return false;
            if (actor != null && !string.IsNullOrEmpty(actor.EnemyInstanceId)
                && !actor.CanStandAt(TraversalAnchorWorld(actor,cell))) return false;
            if (!position.IsOnStairs && position.Surface.Level == 0
                && HasBlockingTraversalObstacle(cell)) return false;
            return true;
        }

        private bool HasBlockingTraversalObstacle(Vector2Int cell)
        {
            var obstacles = NavigationObstaclesAt(cell);
            // The batched patrol query returns a concrete local list. Avoid a
            // captured predicate and boxing its enumerator for each candidate.
            if (obstacles is List<ObstacleDungeonObjectInstance> candidates)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var obstacle = candidates[i];
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesCell(cell)) return true;
                }
            }
            else
                foreach (var obstacle in obstacles)
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesCell(cell)) return true;
            return false;
        }

        private bool IsLogicallyBlockedByObstacle(
            DungeonTraversalPosition position,
            Vector2Int cell)
        {
            if (position.IsOnStairs || position.Surface.Level != 0) return false;
            var obstacles = NavigationObstaclesAt(cell);
            if (obstacles is List<ObstacleDungeonObjectInstance> candidates)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var obstacle = candidates[i];
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesCell(cell)
                        && !obstacle.UsesPhysicalExplorationFootprint) return true;
                }
            }
            else
                foreach (var obstacle in obstacles)
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesCell(cell)
                        && !obstacle.UsesPhysicalExplorationFootprint) return true;
            return false;
        }

        private bool HasPhysicalTraversalObstacle(Vector2Int cell)
        {
            var obstacles = NavigationObstaclesAt(cell);
            if (obstacles is List<ObstacleDungeonObjectInstance> candidates)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var obstacle = candidates[i];
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesPhysicalExplorationCell(cell)
                        && obstacle.UsesPhysicalExplorationFootprint) return true;
                }
            }
            else
                foreach (var obstacle in obstacles)
                    if (obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesPhysicalExplorationCell(cell)
                        && obstacle.UsesPhysicalExplorationFootprint) return true;
            return false;
        }

        public bool CanEnterTraversalAtWorld(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position,
            Vector2 world)
        {
            if (traversalFloor?.Map == null
                || !traversalFloor.Map.TryGetTraversalCell(position, out var cell)) return false;
            if (!ForestEnemyCanEnter(actor,cell)) return false;
            if (IsLogicallyBlockedByObstacle(position, cell)) return false;
            var hasPhysicalObstacle = !position.IsOnStairs && position.Surface.Level == 0
                && HasPhysicalTraversalObstacle(cell);
            return !hasPhysicalObstacle || actor != null && actor.CanStandAt(world);
        }

        private Vector2 TraversalAnchorWorld(
            ExplorationCharacterView actor,
            Vector2Int cell)
            => (Vector2)traversalMapView.CellToWorldCenter(cell)
                - (actor?.MovementColliderCenterOffset ?? Vector2.zero);

        private Vector2 TraversalPathNodeWorld(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position,
            DungeonTraversalPosition target,
            Vector2 targetWorld)
        {
            if (!traversalFloor.Map.TryGetTraversalCell(position, out var cell)
                || !traversalFloor.Map.TryGetTraversalCell(target, out var targetCell))
                return targetWorld;

            var targetAnchor = TraversalAnchorWorld(actor, targetCell);
            return TraversalAnchorWorld(actor, cell) + (targetWorld - targetAnchor);
        }

        private bool CanEnterTraversalPathNode(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position,
            DungeonTraversalPosition target,
            Vector2? targetWorldOverride)
            // Recovery applies only to the real start, even when the click has
            // an exact world endpoint. It never permits re-entering that cell.
            => position == actor.DungeonPosition && position != target
                ? CanEnterTraversalFromActualStart(actor, position)
                : targetWorldOverride.HasValue
                ? CanEnterTraversalAtWorld(
                    actor,
                    position,
                    position == target
                        ? targetWorldOverride.Value
                        : TraversalPathNodeWorld(actor, position, target, targetWorldOverride.Value))
                : CanEnterTraversalFromActualStart(actor, position);

        private bool CanNavigateTraversalPathSegment(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            DungeonTraversalPosition destination,
            DungeonTraversalPosition target,
            Vector2 targetWorld)
        {
            var startWorld = start == actor.DungeonPosition
                ? actor.Position
                : TraversalPathNodeWorld(actor, start, target, targetWorld);
            var destinationWorld = destination == target
                ? targetWorld
                : TraversalPathNodeWorld(actor, destination, target, targetWorld);
            return CanNavigateDirectly(
                actor, start, startWorld, destination, destinationWorld);
        }

        private bool CanEnterTraversalFromActualStart(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position)
        {
            if (CanEnterTraversal(actor, position)) return true;
            if (!IsActualBlockedStart(actor, position)) return false;

            // A narrow obstacle can occupy the logical cell while the actor is
            // already standing on a clear part of that cell. A physics cast may
            // report a separating initial overlap while OverlapCircle still sees
            // the contact, so accept either a clear footprint or a demonstrably
            // clear exit toward a traversable neighbour. Never make the cell
            // enterable as a destination or an intermediate waypoint.
            return actor.CanStandAt(actor.Position) || HasClearExitFromActualStart(actor, position);
        }

        private bool IsActualBlockedStart(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position)
        {
            return actor != null && !actor.IsCrossingLowWall && !position.IsOnStairs
                && position == actor.DungeonPosition && traversalMapView != null
                && traversalMapView.WorldToCell(actor.Position) == position.Surface.Cell
                && traversalFloor?.Map != null
                && traversalFloor.Map.TryGetTraversalCell(position, out _);
        }

        private bool HasClearExitFromActualStart(
            ExplorationCharacterView actor,
            DungeonTraversalPosition position)
        {
            if (!IsActualBlockedStart(actor, position)) return false;
            foreach (var next in traversalFloor.Map.GetTraversalNeighbours(position, CanOpenNavigationDoor))
            {
                if (next.IsOnStairs || next.Surface.Level != position.Surface.Level
                    || !CanEnterTraversal(actor, next)
                    || !traversalFloor.Map.TryGetTraversalCell(next, out var cell)) continue;

                var exitWorld = TraversalAnchorWorld(actor, cell);
                if (actor.HasClearMovementSegment(actor.Position, exitWorld)) return true;
            }

            return false;
        }

        private bool FindTraversalPath(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            out List<DungeonTraversalPosition> path)
            => FindTraversalPathWithTargetWorld(actor, target, out path, null);

        private bool FindTraversalPathWithTargetWorld(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            out List<DungeonTraversalPosition> path,
            Vector2? targetWorldOverride = null)
        {
            if (actor != null) meshRoutes.Remove(actor);
            lastTraversalPathExpandedNodes = 0;
            // The scoped start recovery below must not make a click on the
            // actor's own logically blocked cell look like a valid target.
            var blockedCurrentTarget = actor != null
                && target == actor.DungeonPosition
                && (!targetWorldOverride.HasValue
                    ? !CanEnterTraversal(actor, target)
                    : !CanEnterTraversalAtWorld(actor, target, targetWorldOverride.Value));
            var endpointIsValid = !blockedCurrentTarget
                && (!targetWorldOverride.HasValue
                || CanStandAtTraversalEndpoint(actor, target, targetWorldOverride.Value));
#if UNITY_EDITOR
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var expandedNodes = 0;
            path = new List<DungeonTraversalPosition>();
            var plannedPath = new List<DungeonTraversalPosition>();
            var found = endpointIsValid && TryFindTraversalPathFromStart(
                    actor, target, targetWorldOverride, out plannedPath, out expandedNodes);
            if (endpointIsValid) path = plannedPath;
            lastTraversalPathExpandedNodes = expandedNodes;
            if (ShouldRecordPerformanceSample(started))
                RecordPerformanceSample(
                    "FindTraversalPath",
                    started,
                    $"actor={actor?.name ?? "none"} id={actor?.CharacterId ?? "none"} start={(actor == null ? "none" : FormatTraversalPosition(actor.DungeonPosition))} target={FormatTraversalPosition(target)} found={found} nodes={path?.Count ?? 0} expanded={expandedNodes} backend={(actor != null && meshRoutes.ContainsKey(actor) ? "NavMesh" : "Traversal")}");
            if (!found && !failedTraversalDiagnosticsLogged)
            {
                failedTraversalDiagnosticsLogged = true;
                RecordFailedTraversalDiagnostic(actor, target, targetWorldOverride, expandedNodes);
            }
            return found;
#else
            path = new List<DungeonTraversalPosition>();
            return endpointIsValid && TryFindTraversalPathFromStart(
                actor, target, targetWorldOverride, out path, out _);
#endif
        }

        private bool TryFindTraversalPathFromStart(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            Vector2? targetWorldOverride,
            out List<DungeonTraversalPosition> path,
            out int expandedNodes)
        {
            expandedNodes = 0;
            var meshFound = TryPlanMeshRoute(actor, target, targetWorldOverride, out path, out var meshHandled);
            if (meshHandled) return meshFound;
            if (TryFindTraversalPathViaAnchor(actor, target, targetWorldOverride, out path, out var anchorExpandedNodes))
            {
                expandedNodes = anchorExpandedNodes;
                return true;
            }
            using var traversalSample = TraversalPlanSample.Auto();
            var found = DungeonExplorationPathfinder.TryFindAnyAnglePath(
                traversalFloor.Map, actor.DungeonPosition, target, out path,
                position => CanEnterTraversalPathNode(
                    actor, position, target, targetWorldOverride),
                CanOpenNavigationDoor,
                (start, destination) => targetWorldOverride.HasValue
                    ? CanNavigateTraversalPathSegment(
                        actor, start, destination, target, targetWorldOverride.Value)
                    : CanNavigateDirectly(actor, start, destination), out expandedNodes);
            expandedNodes += anchorExpandedNodes;
            return found;
        }

        private bool TryFindTraversalPathViaAnchor(
            ExplorationCharacterView actor, DungeonTraversalPosition target, Vector2? targetWorldOverride,
            out List<DungeonTraversalPosition> path, out int expandedNodes)
        {
            path = new List<DungeonTraversalPosition>();
            expandedNodes = 0;
            if (actor.DungeonPosition.IsOnStairs || target.IsOnStairs
                || target == actor.DungeonPosition
                || !CanEnterTraversalFromActualStart(actor, actor.DungeonPosition)
                || !traversalFloor.Map.TryGetTraversalCell(actor.DungeonPosition, out var startCell)) return false;

            var anchor = TraversalAnchorWorld(actor, startCell);
            if ((actor.Position - anchor).sqrMagnitude <= .03f * .03f
                || !CanNavigateDirectly(actor, actor.DungeonPosition, actor.Position,
                    actor.DungeonPosition, anchor)) return false;

            // A reachable side branch does not imply that the exit toward the
            // goal is reachable. Prefer the anchor only when it opens such an
            // exit, before exhausting the unrelated branch of the map.
            var anchorOpensExit = false;
            foreach (var neighbour in traversalFloor.Map.GetTraversalNeighbours(actor.DungeonPosition, CanOpenNavigationDoor))
            {
                if (neighbour.IsOnStairs || !CanEnterTraversalPathNode(
                        actor, neighbour, target, targetWorldOverride)
                    || !traversalFloor.Map.TryGetTraversalCell(neighbour, out var neighbourCell)) continue;
                if ((neighbourCell - target.Surface.Cell).sqrMagnitude
                    >= (startCell - target.Surface.Cell).sqrMagnitude) continue;
                var neighbourWorld = targetWorldOverride.HasValue
                    ? TraversalPathNodeWorld(actor, neighbour, target, targetWorldOverride.Value)
                    : TraversalAnchorWorld(actor, neighbourCell);
                if (!CanNavigateDirectly(actor, actor.DungeonPosition, neighbour)
                    && CanNavigateDirectly(actor, actor.DungeonPosition, anchor, neighbour,
                        neighbourWorld))
                {
                    anchorOpensExit = true;
                    break;
                }
            }
            if (!anchorOpensExit) return false;

            // An off-centre start can see no neighbour because its straight
            // exits cut an obstacle corner. Plan from the reachable anchor and
            // retain the within-cell movement as a real waypoint for execution.
            var found = DungeonExplorationPathfinder.TryFindAnyAnglePath(
                traversalFloor.Map,
                actor.DungeonPosition,
                target,
                out path,
                position => CanEnterTraversalPathNode(
                    actor, position, target, targetWorldOverride),
                CanOpenNavigationDoor,
                (start, destination) => targetWorldOverride.HasValue
                    ? CanNavigateTraversalPathSegment(
                        actor, start, destination, target, targetWorldOverride.Value)
                    : traversalFloor.Map.TryGetTraversalCell(start, out var fromCell)
                        && traversalFloor.Map.TryGetTraversalCell(destination, out var toCell)
                        && CanNavigateDirectly(actor, start, TraversalAnchorWorld(actor, fromCell), destination,
                            TraversalAnchorWorld(actor, toCell)),
                out expandedNodes);
            if (found) path.Insert(0, actor.DungeonPosition);
            return found;
        }

        private bool CanStandAtTraversalEndpoint(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            Vector2 targetWorld)
        {
            return TryValidateTraversalEndpoint(actor, target, targetWorld, out _);
        }

        private bool TryValidateTraversalEndpoint(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            Vector2 targetWorld,
            out string failureReason)
        {
            using var endpointSample = TargetEndpointSample.Auto();
            failureReason = null;
            if (actor == null || traversalFloor?.Map == null
                || !traversalFloor.Map.TryGetTraversalCell(target, out _))
            {
                failureReason = "endpoint-missing-cell";
                return false;
            }
            if (!CanEnterTraversalAtWorld(actor, target, targetWorld))
            {
                failureReason = "endpoint-not-enterable";
                return false;
            }

            // Stairs have authored traversal geometry rather than a regular
            // surface footprint. Keep their existing adjacent-node handling.
            if (target.IsOnStairs) return true;
            if (!actor.CanStandAt(targetWorld))
            {
                failureReason = "endpoint-physics-overlap";
                return false;
            }

            // Reuse the same footprint/topology sampler used by direct
            // movement, but evaluate only the endpoint. This catches a
            // pointer/follower point whose circle extends into void before
            // invoking the expensive map search.
            if (CanNavigateDirectly(actor, target, targetWorld, target, targetWorld, out failureReason)) return true;
            failureReason ??= "endpoint-footprint-invalid";
            return false;
        }

#if UNITY_EDITOR
        private bool failedTraversalDiagnosticsLogged;

        private void RecordFailedTraversalDiagnostic(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            Vector2? targetWorldOverride,
            int expandedNodes)
        {
            if (actor == null || traversalFloor?.Map == null || traversalMapView == null) return;

            var map = traversalFloor.Map;
            var start = actor.DungeonPosition;
            var hasStartCell = map.TryGetTraversalCell(start, out var startCell);
            var hasTargetCell = map.TryGetTraversalCell(target, out var targetCell);
            var targetCellWorld = hasTargetCell
                ? TraversalAnchorWorld(actor, targetCell)
                : Vector2.zero;
            var targetWorld = targetWorldOverride ?? targetCellWorld;
            var endpointReason = "not-click-endpoint";
            var endpointValid = !targetWorldOverride.HasValue
                || TryValidateTraversalEndpoint(actor, target, targetWorld, out endpointReason);
            var startEnter = hasStartCell && CanEnterTraversal(actor, start);
            var targetEnter = hasTargetCell && CanEnterTraversal(actor, target);
            var actualStart = IsActualBlockedStart(actor, start);
            var startCanStand = actualStart && actor.CanStandAt(actor.Position);
            var startClearExit = actualStart && HasClearExitFromActualStart(actor, start);
            var topologyOpen = !start.IsOnStairs && !target.IsOnStairs
                && start.Surface.Level == target.Surface.Level
                && map.CanTraverseBetweenSurfaces(start.Surface, target.Surface, CanOpenNavigationDoor);
            var clearToCell = hasStartCell && hasTargetCell
                && actor.HasClearMovementSegment(actor.Position, targetCellWorld);
            var clearToWorld = hasStartCell && hasTargetCell
                && actor.HasClearMovementSegment(actor.Position, targetWorld);
            var directCellReason = "not-evaluated";
            var directWorldReason = "not-evaluated";
            var directToCell = hasStartCell && hasTargetCell
                && CanNavigateDirectly(actor, start, target, out directCellReason);
            var directToWorld = hasStartCell && hasTargetCell
                && CanNavigateDirectly(actor, start, actor.Position, target, targetWorld, out directWorldReason);
            var neighbours = hasStartCell
                ? map.GetTraversalNeighbours(start, CanOpenNavigationDoor).ToList()
                : new List<DungeonTraversalPosition>();
            var neighbourDetails = new List<string>();
            foreach (var neighbour in neighbours)
            {
                var canNavigate = CanNavigateDirectly(actor, start, neighbour, out var reason);
                neighbourDetails.Add($"{FormatTraversalPosition(neighbour)}:{(canNavigate ? "ok" : reason)}");
            }
            var blockers = !clearToCell
                ? GetMovementBlockers(actor, actor.Position, targetCellWorld)
                : new string[0];

            UnityEngine.Debug.LogWarning(
                $"[ExplorationPerf] FailedTraversalDiagnostic actor={actor.CharacterId} "
                + $"startWorld={actor.Position} targetWorld={targetWorld} targetCellWorld={targetCellWorld} "
                + $"startCell={startCell} targetCell={targetCell} hasCells={hasStartCell}/{hasTargetCell} "
                + $"enter={startEnter}/{targetEnter} topologyOpen={topologyOpen} "
                + $"endpointValid={endpointValid} endpointReason={endpointReason} "
                + $"actualStart={actualStart} crossing={actor.IsCrossingLowWall} "
                + $"worldCell={traversalMapView.WorldToCell(actor.Position)} logicalCell={start.Surface.Cell} "
                + $"canStand={startCanStand} clearExit={startClearExit} "
                + $"clearCell={clearToCell} clearWorld={clearToWorld} directCell={directToCell}:{directCellReason ?? "ok"} "
                + $"directWorld={directToWorld}:{directWorldReason ?? "ok"} neighbours={neighbours.Count} "
                + $"neighbourDetails=[{string.Join(",", neighbourDetails)}] "
                + $"expanded={expandedNodes} radius={actor.MovementRadius:F3} offset={actor.MovementColliderCenterOffset} "
                + $"blockers={(blockers.Length == 0 ? "none" : string.Join(",", blockers))}");
        }

        private string[] GetMovementBlockers(ExplorationCharacterView actor, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var mask = Physics2D.GetLayerCollisionMask(actor.gameObject.layer);
            var blockers = new List<string>();
            foreach (var collider in Physics2D.OverlapCircleAll(
                         from + actor.MovementColliderCenterOffset, actor.MovementRadius, mask))
            {
                if (collider == null || collider.isTrigger
                    || collider.GetComponentInParent<ExplorationCharacterView>() != null) continue;
                blockers.Add(collider.name + ":" + collider.GetType().Name + ":overlap");
            }

            foreach (var hit in Physics2D.CircleCastAll(
                from + actor.MovementColliderCenterOffset,
                actor.MovementRadius,
                delta.sqrMagnitude > .000001f ? delta.normalized : Vector2.right,
                delta.magnitude,
                mask))
            {
                var collider = hit.collider;
                if (collider == null || collider.isTrigger
                    || collider.GetComponentInParent<ExplorationCharacterView>() != null) continue;
                blockers.Add(collider.name + ":" + collider.GetType().Name + $":cast@{hit.fraction:F3}");
            }

            return blockers
                .Distinct()
                .Take(6)
                .ToArray();
        }
#endif

        private bool CanNavigateDirectly(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target)
        {
            if (!traversalFloor.Map.TryGetTraversalCell(start, out var startCell)
                || !traversalFloor.Map.TryGetTraversalCell(target, out var targetCell)) return false;
            var startWorld = start == actor.DungeonPosition
                ? actor.Position
                : TraversalAnchorWorld(actor, startCell);
            var targetWorld = TraversalAnchorWorld(actor, targetCell);
            return CanNavigateDirectly(actor, start, startWorld, target, targetWorld);
        }

        private bool CanNavigateDirectly(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target,
            out string failureReason)
        {
            failureReason = null;
            if (!traversalFloor.Map.TryGetTraversalCell(start, out var startCell)
                || !traversalFloor.Map.TryGetTraversalCell(target, out var targetCell))
            {
                failureReason = "missing-cell";
                return false;
            }

            var startWorld = start == actor.DungeonPosition
                ? actor.Position
                : TraversalAnchorWorld(actor, startCell);
            var targetWorld = TraversalAnchorWorld(actor, targetCell);
            return CanNavigateDirectly(actor, start, startWorld, target, targetWorld, out failureReason);
        }

        private bool CanNavigateDirectly(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target,
            Vector2 targetWorld)
        {
            if (!traversalFloor.Map.TryGetTraversalCell(start, out var startCell)
                || !traversalFloor.Map.TryGetTraversalCell(target, out _)) return false;
            var startWorld = start == actor.DungeonPosition
                ? actor.Position
                : TraversalAnchorWorld(actor, startCell);
            return CanNavigateDirectly(actor, start, startWorld, target, targetWorld);
        }

        private void SmoothTraversalPath(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            Vector2 targetWorld,
            List<DungeonTraversalPosition> path,
            bool retainTargetWaypoint = false)
        {
            if (meshRoutes.TryGetValue(actor, out var native) && ReferenceEquals(native.LogicalPath, path)) return;
            if (path == null || path.Count < 2) return;
            // A repeated start represents an explicit move to its own anchor.
            // Shortcutting it would recreate the blocked off-centre exit.
            if (path[0] == path[1]) return;

            var originalPath = new List<DungeonTraversalPosition>(path);
            var smoothed = new List<DungeonTraversalPosition> { path[0] };
            var currentIndex = 0;
            var currentWorld = actor.Position;

            while (currentIndex < path.Count - 1)
            {
                var current = path[currentIndex];

                // Once the exact pointer position is reachable, the remaining grid
                // nodes only add a visible zig-zag and can be dropped.
                // Layered actors keep the target cell queued so MoveFreeAxis can
                // prefer its adjacent surface node over an authored stair option.
                if (!retainTargetWaypoint && !actor.UsesLayeredTraversal
                    && CanNavigateDirectly(actor, current, currentWorld, target, targetWorld))
                {
                    // Keep the logical target in the queue even when the last
                    // intermediate node can see it. AdvanceRoute uses the
                    // finalPoint only for the logical target; dropping this node
                    // would make it apply the pointer position to the intermediate
                    // corner and skip the turn entirely.
                    if (smoothed[smoothed.Count - 1] != target) smoothed.Add(target);
                    break;
                }

                var nextIndex = currentIndex + 1;
                for (var candidateIndex = path.Count - 1; candidateIndex > currentIndex; candidateIndex--)
                {
                    if (!traversalFloor.Map.TryGetTraversalCell(path[candidateIndex], out var candidateCell)) continue;
                    var candidateWorld = candidateIndex == path.Count - 1 && path[candidateIndex] == target
                        ? targetWorld
                        : TraversalPathNodeWorld(actor, path[candidateIndex], target, targetWorld);
                    if (!CanNavigateDirectly(actor, current, currentWorld, path[candidateIndex], candidateWorld)) continue;
                    nextIndex = candidateIndex;
                    break;
                }

                smoothed.Add(path[nextIndex]);
                currentIndex = nextIndex;
                if (!traversalFloor.Map.TryGetTraversalCell(path[currentIndex], out var cell)) break;
                currentWorld = path[currentIndex] == target
                    ? targetWorld
                    : TraversalPathNodeWorld(actor, path[currentIndex], target, targetWorld);
            }

            // The physics/topology snapshot used while shortcutting can become
            // stale before the next FixedUpdate (for example after another
            // collider moved). Never publish a shortcut that the executor would
            // reject; retain the planner's original connected waypoints so the
            // next tick can advance or replan from a concrete node.
            var smoothedPathIsValid = true;
            var previousWorld = actor.Position;
            for (var index = 1; index < smoothed.Count; index++)
            {
                if (!traversalFloor.Map.TryGetTraversalCell(smoothed[index], out var cell))
                {
                    smoothedPathIsValid = false;
                    break;
                }

                var waypointWorld = index == smoothed.Count - 1 && smoothed[index] == target
                    ? targetWorld
                    : TraversalPathNodeWorld(actor, smoothed[index], target, targetWorld);
                if (!CanNavigateDirectly(
                        actor,
                        smoothed[index - 1],
                        previousWorld,
                        smoothed[index],
                        waypointWorld))
                {
                    smoothedPathIsValid = false;
                    break;
                }

                previousWorld = waypointWorld;
            }

            if (!smoothedPathIsValid) smoothed = originalPath;

            path.Clear();
            path.AddRange(smoothed);
        }

        private bool CanNavigateDirectly(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            Vector2 startWorld,
            DungeonTraversalPosition target,
            Vector2 targetWorld)
            => CanNavigateDirectly(actor, start, startWorld, target, targetWorld, out _);

        private bool CanNavigateDirectly(
            ExplorationCharacterView actor,
            DungeonTraversalPosition start,
            Vector2 startWorld,
            DungeonTraversalPosition target,
            Vector2 targetWorld,
            out string failureReason)
        {
            using var validationSample = DirectValidationSample.Auto();
            failureReason = null;
            if (start.IsOnStairs || target.IsOnStairs
                || start.Surface.Level != target.Surface.Level)
            {
                failureReason = "stairs-or-level";
                return false;
            }
            var startEnterable = CanEnterTraversalAtWorld(actor, start, startWorld);
            if (!startEnterable
                && (start != actor.DungeonPosition
                    || !CanEnterTraversalFromActualStart(actor, start)))
            {
                failureReason = "start-not-enterable";
                return false;
            }
            if (!CanEnterTraversalAtWorld(actor, target, targetWorld))
            {
                failureReason = "target-not-enterable";
                return false;
            }
            if (!actor.HasClearMovementSegment(startWorld, targetWorld))
            {
                failureReason = "physics-segment";
                return false;
            }

            var startCell = traversalMapView.WorldToCell(startWorld);
            var targetCell = traversalMapView.WorldToCell(targetWorld);
            var cellDelta = targetCell - startCell;
            // Recovery is only valid for the actor's current grounded position.
            // A later planner/smoothing waypoint starts from a cell centre and
            // must never inherit this exception just because its offset sample
            // happens to touch void.
            var canRecoverInitialOverlap = start == actor.DungeonPosition
                && (startWorld - actor.Position).sqrMagnitude <= .02f * .02f;
            // Physics/transform state does not change during this synchronous
            // validation. Resolve the feet-to-collider offset once, not once
            // for every footprint sample along a long follower segment.
            var colliderOffset = actor.MovementColliderCenterOffset;
            if (Mathf.Max(Mathf.Abs(cellDelta.x), Mathf.Abs(cellDelta.y)) <= 1)
            {
                // The topology samples describe the collider footprint in floor
                // cells. Wall thickness is already enforced by the authored edge
                // checks and by HasClearMovementSegment; adding it here expands
                // the footprint a second time and can require a nonexistent
                // neighbouring floor cell in an otherwise valid corridor.
                var localRadius = actor.MovementRadius;
                var localMovementCenter = startWorld + colliderOffset;
                var localChecksValid = true;
                foreach (var direction in footprintDirections)
                {
                    var offset = direction * localRadius;
                    var previousWorld = localMovementCenter + offset;
                    var currentWorld = targetWorld + colliderOffset + offset;
                    var previous = new DungeonSurfacePosition(traversalMapView.WorldToCell(previousWorld), start.Surface.Level);
                    var current = new DungeonSurfacePosition(
                        traversalMapView.WorldToCell(currentWorld), start.Surface.Level);
                    var previousEnterable = CanEnterFootprintSample(previous);
                    var currentEnterable = CanEnterFootprintSample(current);
                    // A grounded actor can begin one sample over the edge of
                    // its start cell because the collider is offset above the
                    // feet. Allow only recovery into a valid cell; the target
                    // footprint itself must still be completely on floor.
                    if (!currentEnterable)
                    {
                        failureReason = "footprint-no-floor";
                        return false;
                    }
                    if (!previousEnterable
                        && (!canRecoverInitialOverlap
                            || !CanRecoverInitialFootprintSample(actor, start.Surface, current)))
                    {
                        failureReason = "footprint-no-floor";
                        return false;
                    }
                    if (current == previous || !previousEnterable) continue;
                    var delta = current.Cell - previous.Cell;
                    if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > 1)
                    {
                        // The actor roots are neighbouring cells, but a feet-to-
                        // collider offset can put the sampled footprint endpoints
                        // farther apart. Fall through to the regular segment
                        // sampler instead of treating that displacement as an
                        // invalid corner jump.
                        localChecksValid = false;
                        break;
                    }
                    if (!CanTraverseFootprintSegment(
                            actor, previous, current, previousWorld, currentWorld, out failureReason)) return false;
                }

                if (localChecksValid) return true;
            }

            var cellSize = Vector2.Distance(
                traversalMapView.CellToWorldCenter(Vector2Int.zero),
                traversalMapView.CellToWorldCenter(Vector2Int.right));
            var step = Mathf.Max(.01f, cellSize * .2f);
            var count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(startWorld, targetWorld) / step));
            // Keep this aligned with FitsTraversalCollider: WallHalfThickness is
            // an edge clearance, not an additional floor-cell footprint radius.
            var radius = actor.MovementRadius;
            // The exploration root is the visible feet baseline. Sample the
            // traversal topology around the same collider centre used by the
            // physics cast, rather than around that grounded root.
            var movementCenter = startWorld + colliderOffset;
            foreach (var direction in footprintDirections)
            {
                var offset = direction * radius;
                var previousWorld = movementCenter + offset;
                var previous = new DungeonSurfacePosition(traversalMapView.WorldToCell(previousWorld), start.Surface.Level);
                var previousEnterable = CanEnterFootprintSample(previous);

                for (var index = 1; index <= count; index++)
                {
                    var sample = Vector2.Lerp(startWorld, targetWorld, (float)index / count)
                        + colliderOffset + offset;
                    var current = new DungeonSurfacePosition(traversalMapView.WorldToCell(sample), start.Surface.Level);
                    var currentEnterable = CanEnterFootprintSample(current);
                    if (!currentEnterable
                        && (previousEnterable || current != previous))
                    {
                        failureReason = "sample-no-floor";
                        return false;
                    }
                    if (!currentEnterable) continue;
                    if (!previousEnterable)
                    {
                        if (!canRecoverInitialOverlap
                            || !CanRecoverInitialFootprintSample(actor, start.Surface, current))
                        {
                            failureReason = "sample-no-floor";
                            return false;
                        }

                        previousEnterable = true;
                        previous = current;
                        previousWorld = sample;
                        continue;
                    }
                    if (!CanTraverseFootprintSegment(
                            actor, previous, current, previousWorld, sample, out failureReason)) return false;
                    previous = current;
                    previousWorld = sample;
                }

                // A footprint sample may begin in the one-cell edge overlap,
                // but it must actually re-enter a traversable floor cell before
                // this segment is accepted. Otherwise a trace that stays in
                // void for its entire length would be treated as clear.
                if (!previousEnterable)
                {
                    failureReason = "sample-no-floor";
                    return false;
                }
            }

            return true;
        }

        private bool CanEnterFootprintSample(DungeonSurfacePosition surface)
        {
            // This is a point on the already swept collider, not another actor
            // feet position. Recasting a full circle here doubles its clearance
            // and reapplies the feet-to-centre offset around narrow trunks.
            var position = new DungeonTraversalPosition(surface);
            return traversalFloor.Map.TryGetTraversalCell(position, out var cell)
                && !IsLogicallyBlockedByObstacle(position, cell);
        }

        private bool CanTraverseFootprintSegment(
            ExplorationCharacterView actor,
            DungeonSurfacePosition previous,
            DungeonSurfacePosition current,
            Vector2 previousWorld,
            Vector2 currentWorld,
            out string failureReason)
        {
            failureReason = null;
            if (previous == current) return true;

            var delta = current.Cell - previous.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            {
                if (CanTraverseDirectly(previous, current)) return true;
                failureReason = "footprint-wall";
                return false;
            }

            if (Mathf.Abs(delta.x) != 1 || Mathf.Abs(delta.y) != 1)
            {
                failureReason = "sample-too-far";
                return false;
            }

            var previousCenter = (Vector2)traversalMapView.CellToWorldCenter(previous.Cell);
            var cellRight = (Vector2)traversalMapView.CellToWorldCenter(previous.Cell + Vector2Int.right);
            var cellUp = (Vector2)traversalMapView.CellToWorldCenter(previous.Cell + Vector2Int.up);
            var cellSizeX = Mathf.Abs(cellRight.x - previousCenter.x);
            var cellSizeY = Mathf.Abs(cellUp.y - previousCenter.y);
            var worldDelta = currentWorld - previousWorld;
            var xBoundary = previousCenter.x + Mathf.Sign(delta.x) * cellSizeX * .5f;
            var yBoundary = previousCenter.y + Mathf.Sign(delta.y) * cellSizeY * .5f;
            var tX = Mathf.Abs(worldDelta.x) > .000001f
                ? (xBoundary - previousWorld.x) / worldDelta.x : float.PositiveInfinity;
            var tY = Mathf.Abs(worldDelta.y) > .000001f
                ? (yBoundary - previousWorld.y) / worldDelta.y : float.PositiveInfinity;
            var xFirst = new DungeonSurfacePosition(
                new Vector2Int(current.Cell.x, previous.Cell.y), previous.Level);
            var yFirst = new DungeonSurfacePosition(
                new Vector2Int(previous.Cell.x, current.Cell.y), previous.Level);
            var segmentFailureReason = (string)null;

            bool CheckIntermediate(DungeonSurfacePosition intermediate)
            {
                if (IsLogicallyBlockedByObstacle(
                        new DungeonTraversalPosition(intermediate),
                        intermediate.Cell))
                {
                    segmentFailureReason = "footprint-corner-no-floor";
                    return false;
                }
                if (!CanTraverseDirectly(previous, intermediate)
                    || !CanTraverseDirectly(intermediate, current))
                {
                    segmentFailureReason = "footprint-corner-wall";
                    return false;
                }
                return true;
            }

            // A diagonal only enters the intermediate cell on the side whose
            // grid boundary the continuous segment crosses first. At an exact
            // corner tie both cells are part of the supercover and stay guarded.
            const float crossingEpsilon = .0001f;
            bool result;
            // Layered execution decomposes every move into horizontal then
            // vertical MoveFreeAxis steps, so its topology order must match that
            // executor even when the straight segment's geometric order differs.
            if (actor.UsesLayeredTraversal) result = CheckIntermediate(xFirst);
            else if (tX < tY - crossingEpsilon) result = CheckIntermediate(xFirst);
            else if (tY < tX - crossingEpsilon) result = CheckIntermediate(yFirst);
            else result = CheckIntermediate(xFirst) && CheckIntermediate(yFirst);
            failureReason = segmentFailureReason;
            return result;
        }

        private bool CanTraverseDirectly(DungeonSurfacePosition first, DungeonSurfacePosition second)
            => first == second || traversalFloor.Map.CanTraverseBetweenSurfaces(
                first,
                second,
                openNavigationDoorQuery ??= IsNavigationDoorOpen);

        private bool IsNavigationDoorOpen(DungeonSurfaceEdge edge)
            => navigationDoors.TryGetValue(edge.CellEdge, out var door) && door.IsOpen;

        private bool CanRecoverInitialFootprintSample(
            ExplorationCharacterView actor,
            DungeonSurfacePosition start,
            DungeonSurfacePosition current)
        {
            if (current == start) return true;
            if (current.Level != start.Level) return false;

            var delta = current.Cell - start.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return CanTraverseDirectly(start, current);
            if (Mathf.Abs(delta.x) != 1 || Mathf.Abs(delta.y) != 1) return false;

            var first = new DungeonSurfacePosition(
                new Vector2Int(current.Cell.x, start.Cell.y), start.Level);
            var second = new DungeonSurfacePosition(
                new Vector2Int(start.Cell.x, current.Cell.y), start.Level);
            return CanEnterTraversal(actor, new DungeonTraversalPosition(first))
                && CanEnterTraversal(actor, new DungeonTraversalPosition(second))
                && CanTraverseDirectly(start, first)
                && CanTraverseDirectly(first, current)
                && CanTraverseDirectly(start, second)
                && CanTraverseDirectly(second, current);
        }

        private bool TryFindSafeDungeonFormationPosition(
            ExplorationCharacterView actor,
            Vector2 desired,
            IReadOnlyList<Vector2> placed,
            out Vector2 position)
        {
            position = desired;
            if (actor == null || traversalFloor?.Map == null || traversalMapView == null) return false;

            var directions = new[]
            {
                Vector2.left, Vector2.down, Vector2.up, Vector2.right,
                new Vector2(-1f, -1f).normalized, new Vector2(-1f, 1f).normalized,
                new Vector2(1f, -1f).normalized, new Vector2(1f, 1f).normalized
            };
            var current = actor.DungeonPosition;
            var level = current.IsOnStairs ? actor.PresentationLevel : current.Surface.Level;
            for (var ring = 0; ring <= 4; ring++)
            {
                foreach (var direction in ring == 0 ? new[] { Vector2.zero } : directions)
                {
                    var candidate = ring == 0 ? desired : desired + direction * followerDistance * ring;
                    if (placed != null && placed.Any(other =>
                            Vector2.Distance(other, candidate) < followerDistance * .7f)) continue;

                    var cell = traversalMapView.WorldToCell(candidate);
                    var traversal = new DungeonTraversalPosition(
                        new DungeonSurfacePosition(cell, level));
                    if (!traversalFloor.Map.TryGetTraversalCell(traversal, out _)
                        || !CanEnterTraversal(actor, traversal)
                        || !actor.CanStandAt(candidate)
                        || !actor.HasClearMovementSegment(desired, candidate)) continue;

                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool CanOpenNavigationDoor(DungeonSurfaceEdge edge)
            => edge.Level == 0 && navigationDoors.TryGetValue(edge.CellEdge, out var door)
                && door.IsActive && door.InteractionMode != Godmist.Core.Enums.DoorInteractionMode.LeverOnly;

        // Patrols use the same door eligibility and proximity rules as party routes.
        public bool CanOpenTraversalDoor(DungeonSurfaceEdge edge) => CanOpenNavigationDoor(edge);
        public void OpenTraversalDoor(ExplorationCharacterView actor, DungeonTraversalPosition next)
            => OpenNextDoor(actor, next);

        private bool TryResolveExplorationTarget(
            ExplorationCharacterView leader,
            Vector3 world,
            out DungeonTraversalPosition target)
        {
            target = default;
            if (leader == null || traversalFloor?.Map == null || traversalMapView == null) return false;

            // Keep the existing projected target for visible and remembered cells.
            // An unexplored projected cell is presentation-only: it may represent a
            // higher surface hidden by fog, so resolve that click on the actor's
            // current level instead of selecting a floor from the projection.
            if (traversalMapView.TryGetVisibleTraversal(world, out var projected)
                && traversalMapView.Knowledge(projected) != DungeonKnowledge.Unexplored)
            {
                target = projected;
                return true;
            }

            var level = leader.DungeonPosition.IsOnStairs
                ? leader.PresentationLevel
                : leader.DungeonPosition.Surface.Level;
            var candidate = new DungeonTraversalPosition(
                new DungeonSurfacePosition(traversalMapView.WorldToCell(world), level));
            if (!traversalFloor.Map.TryGetTraversalCell(candidate, out _)) return false;

            target = candidate;
            return true;
        }

        private bool TrySetTraversalDestination(Vector3 world)
        {
            using var targetSample = NewTargetSample.Auto();
            var leader = GetActiveLeaderView();
            // One fresh, local obstacle index for the entire click, including endpoint
            // validation, planner callbacks, failed-query diagnostics and smoothing.
            using var navigationBatch = BeginTraversalQueryBatch();
            DungeonTraversalPosition target;
            using (TargetResolutionSample.Auto())
                if (!TryResolveExplorationTarget(leader, world, out target)
                    || DungeonEnemyBodyView.IsPointerOverBody(world)) return false;
            if (!FindTraversalPathWithTargetWorld(leader, target, out var path, world)) return false;
            using (TargetSmoothingSample.Auto()) SmoothTraversalPath(leader, target, world, path);
            meshRoutes.TryGetValue(leader, out var plannedMeshRoute);
            CancelMovement();
            if (plannedMeshRoute != null) meshRoutes[leader] = plannedMeshRoute;
            movementDestination = world; leaderStalledTime = 0; lastLeaderNavigationPosition = leader.Position;
            foreach (var position in path.Skip(1)) traversalPath.Enqueue(position);
            return true;
        }

        private void TickTraversalMovement(ExplorationCharacterView leader)
        {
            var keyboard = ExplorationKeyboardInput.ReadGameplayMovement(cameraController == null || cameraController.IsFollowingLeader);
            if (keyboard.sqrMagnitude > 0)
            {
                destroyTarget = null;
                meshRoutes.Remove(leader);
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
                var chestTarget = pendingChest;
                var worldObjectTarget = pendingWorldObject;
                var arrived = movementDestination.HasValue && Vector2.Distance(leader.Position, movementDestination.Value) < .1f;
                CancelMovement();
                if (arrived) { destroyTarget = pending; pendingChest = chestTarget; pendingWorldObject = worldObjectTarget; }
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
            // Match follower/patrol queries: inspect only obstacles at each sampled cell.
            // The scope is refreshed per step and restores any enclosing query batch.
            using var navigationBatch = BeginTraversalQueryBatch();
            if (route.Count > 0 && meshRoutes.TryGetValue(actor, out var nativeRoute))
                return AdvanceMeshRoute(actor, route, nativeRoute);
            while (route.Count > 0)
            {
                var next = route.Peek();
                if (!traversalFloor.Map.TryGetTraversalCell(next, out var cell)) return false;
                var waypoint = finalPoint.HasValue && route.Count == 1
                    ? finalPoint.Value
                    : finalPoint.HasValue
                        ? TraversalPathNodeWorld(actor, next, route.Last(), finalPoint.Value)
                        : TraversalAnchorWorld(actor, cell);
                if (!CanEnterTraversalPathNode(
                        actor,
                        next,
                        route.Last(),
                        finalPoint)) return false;
                if (actor.DungeonPosition == next && Vector2.Distance(actor.Position, waypoint) <= .03f)
                { route.Dequeue(); continue; }
                var current = actor.DungeonPosition;
                var adjacent = current == next || traversalFloor.Map.GetTraversalNeighbours(current, CanOpenNavigationDoor).Contains(next);
                DungeonTraversalPosition steering = next;
                if (current != next && !adjacent)
                {
                    // Smoothed same-level waypoints are intentionally non-adjacent.
                    // Revalidate the whole segment before allowing one to execute;
                    // layered traversal still needs an adjacent steering node for
                    // MoveFreeAxis to select the correct cell transition.
                    if (!CanNavigateDirectly(actor, current, actor.Position, next, waypoint)) return false;
                    if (actor.UsesLayeredTraversal
                        && !TryGetImmediateTraversalStep(actor, next, out steering)) return false;
                }
                if (actor.DungeonPosition != next)
                {
                    if (adjacent) OpenNextDoor(actor, next);
                    else if (actor.UsesLayeredTraversal) OpenNextDoor(actor, steering);
                }
                actor.PreferredTraversalDestination = actor.UsesLayeredTraversal && !adjacent ? steering : next;
                actor.MoveTowards(waypoint, movementSpeed * Time.fixedDeltaTime, 0);
                return true;
            }
            actor.PreferredTraversalDestination = null;
            if (finalPoint.HasValue) actor.MoveTowards(finalPoint.Value, movementSpeed * Time.fixedDeltaTime, stoppingDistance);
            return true;
        }

        private bool TryGetImmediateTraversalStep(
            ExplorationCharacterView actor,
            DungeonTraversalPosition target,
            out DungeonTraversalPosition step)
        {
            step = default;
            var current = actor.DungeonPosition;
            if (current.IsOnStairs || target.IsOnStairs
                || current.Surface.Level != target.Surface.Level) return false;

            var delta = target.Surface.Cell - current.Surface.Cell;
            var direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0)
                : new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));
            if (direction == Vector2Int.zero) return false;

            foreach (var candidate in traversalFloor.Map.GetTraversalNeighbours(current, CanOpenNavigationDoor))
                if (!candidate.IsOnStairs && candidate.Surface.Level == current.Surface.Level
                    && candidate.Surface.Cell == current.Surface.Cell + direction)
                {
                    step = candidate;
                    return true;
                }

            return false;
        }

        private void OpenNextDoor(ExplorationCharacterView actor, DungeonTraversalPosition next)
        {
            var current = actor.DungeonPosition;
            if (current.IsOnStairs || next.IsOnStairs || current.Surface.Level != 0 || next.Surface.Level != 0) return;
            var delta = next.Surface.Cell - current.Surface.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return;
            var edge = new DungeonCellEdge(current.Surface.Cell, next.Surface.Cell);
            if (!navigationDoors.TryGetValue(edge, out var door) || !door.IsActive || door.IsOpen
                || door.InteractionMode == Godmist.Core.Enums.DoorInteractionMode.LeverOnly) return;
            var at = (Vector2)traversalMapView.CellToWorldCenter(current.Surface.Cell);
            var beyond = (Vector2)traversalMapView.CellToWorldCenter(next.Surface.Cell);
            if (Vector2.Distance(actor.Position, (at + beyond) * .5f) <= Vector2.Distance(at, beyond) * .8f)
                traversalFloor.TrySetDoorOpen(door.InstanceId, true);
        }

        private readonly Queue<Vector2> cityLeaderRoute = new();
        private readonly Dictionary<string, float> cityShortcutReplanTimes = new();
        private readonly Dictionary<string, CityRoute> cityFollowerRoutes = new();
        private sealed class CityRoute
        {
            public readonly Queue<Vector2> Points = new();
            public Vector2 Target;
            public float NextPlan;
        }

        private const float ReviveInteractionRadius = 1f;
        private const float HelpUpPoseDuration = 1.25f;
        private ExplorationCharacterView pendingReviveTarget;
        private readonly Godmist.Systems.Combat.CombatReviveSystem explorationRevive = new();

        private ExplorationCharacterView GetReviveActor()
        {
            bool Available(ExplorationCharacterView view) => view != null && view.CombatUnit == null
                && gameSession.PlayerParty.IsActiveMember(view.CharacterId)
                && view.RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && !gameSession.IsCharacterInCombat(view.CharacterId);
            var leader = GetActiveLeaderView();
            return Available(leader) ? leader : viewsById.Values.FirstOrDefault(Available);
        }

        public bool CanRequestRevive(ExplorationCharacterView target)
            => CanAcceptExplorationInput && target != null && target.CombatUnit == null && target.IsVisibleForInspection
                && target.RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Downed
                && viewsById.TryGetValue(target.CharacterId, out var member) && member == target
                && gameSession.PlayerParty.IsActiveMember(target.CharacterId)
                && !gameSession.IsCharacterInCombat(target.CharacterId)
                && GetReviveActor() != null;

        public bool RequestReviveInteraction(ExplorationCharacterView target)
        {
            if (!CanRequestRevive(target)) return false;
            var actor = GetReviveActor();
            if (actor != GetActiveLeaderView() && !SelectMember(actor.CharacterId)) return false;
            CancelMovement();
            if (CanReachReviveTarget(actor, target, actor.Position))
                return TryReviveWithHelperPose(actor, target);
            if (!TryApproachPosition(target.Position, ReviveInteractionRadius,
                goal => CanReachReviveTarget(actor, target, goal))) return false;
            pendingReviveTarget = target;
            return true;
        }

        private bool TryReviveWithHelperPose(ExplorationCharacterView actor, ExplorationCharacterView target)
        {
            if (!explorationRevive.TryReviveExploration(actor.RuntimeCharacter, target.RuntimeCharacter)) return false;
            var towardTarget = target.Position - actor.Position;
            if (towardTarget.sqrMagnitude < .000001f) towardTarget = actor.FacingDirection;
            actor.PlayHelpUpPose(towardTarget, HelpUpPoseDuration);
            return true;
        }

        private bool CanReachReviveTarget(ExplorationCharacterView actor, ExplorationCharacterView target, Vector2 from)
        {
            var offset = target.Position - from;
            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > ReviveInteractionRadius) return false;
            if (traversalFloor?.Map == null || traversalMapView == null)
                return actor.HasClearMovementSegment(from, target.Position);
            if (!traversalMapView.TryGetVisibleTraversal(from, out var start)) return false;
            var end = target.DungeonPosition;
            if (start.IsOnStairs || end.IsOnStairs)
                return (start == end || traversalFloor.Map.GetTraversalNeighbours(start).Contains(end))
                    && actor.HasClearMovementSegment(from, target.Position);
            return CanNavigateDirectly(actor, start, from, end, target.Position);
        }

        private bool TickReviveApproach()
        {
            if (pendingReviveTarget == null) return false;
            if (!CanRequestRevive(pendingReviveTarget)
                || ExplorationKeyboardInput.ReadGameplayMovement().sqrMagnitude > 0f)
            { CancelMovement(); return false; }
            var actor = GetActiveLeaderView();
            if (actor?.RuntimeCharacter?.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive)
            { CancelMovement(); return false; }
            if (CanReachReviveTarget(actor, pendingReviveTarget, actor.Position))
            {
                var target = pendingReviveTarget;
                CancelMovement();
                return TryReviveWithHelperPose(actor, target);
            }
            if (!movementDestination.HasValue) pendingReviveTarget = null;
            return false;
        }

        private DungeonChestView pendingChest;
        public bool RequestChestInteraction(DungeonChestView target)
        {
            if (target == null || !target.IsOwnedBy(this) || target.Floor != traversalFloor
                || traversalFloor?.Map == null || traversalMapView == null
                || !target.CanRequestInteraction || !CanAcceptExplorationInput) return false;
            var actor = GetActiveLeaderView();
            if (actor?.RuntimeCharacter?.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive) return false;
            CancelMovement();
            if (target.CanInteract()) { target.Interact(); return true; }
            if (!TryApproachPosition(target.transform.position, target.InteractionRadius,
                goal => target.CanInteractFrom(goal)
                    && traversalMapView.TryGetVisibleTraversal(goal, out var position)
                    && !position.IsOnStairs && position.Surface.Level == 0)) return false;
            pendingChest = target;
            return true;
        }

        private bool TickChestApproach()
        {
            if (ReferenceEquals(pendingChest, null)) return false;
            if (pendingChest == null || pendingChest.Floor != traversalFloor || !pendingChest.CanRequestInteraction
                || ExplorationKeyboardInput.ReadGameplayMovement().sqrMagnitude > 0f)
            { CancelMovement(); return false; }
            if (pendingChest.CanInteract())
            {
                var target = pendingChest; CancelMovement(); target.Interact(); return true;
            }
            if (!movementDestination.HasValue) CancelMovement();
            return false;
        }

        private Godmist.Views.NPC.NpcView pendingNpc;
        public bool RequestNpcInteraction(Godmist.Views.NPC.NpcView npc)
        {
            if (npc == null || !npc.isActiveAndEnabled || !CanAcceptExplorationInput) return false;
            CancelMovement();
            if (npc.CanInteract()) { npc.Interact(); return true; }
            if (!TryApproachPosition(npc.transform.position, npc.InteractionRadius)) return false;
            pendingNpc = npc;
            return true;
        }
        private DungeonEntranceView pendingEntrance;
        public bool RequestEntranceInteraction(DungeonEntranceView entrance)
        {
            if (entrance == null || !entrance.isActiveAndEnabled || !CanAcceptExplorationInput) return false;
            CancelMovement();
            if (entrance.CanInteract()) return entrance.TryInteract();
            if (entrance.UsesGateExit
                ? !TrySetCityDestination(entrance.GateApproachPosition)
                : !TryApproachPosition(entrance.transform.position, entrance.InteractionRadius)) return false;
            pendingEntrance = entrance;
            return true;
        }
        private bool TryApproachPosition(Vector2 center, float radius, System.Func<Vector2, bool> canInteractFrom = null)
        {
            var actor = GetActiveLeaderView();
            if (actor == null) return false;
            var angle = Mathf.Atan2(actor.Position.y-center.y, actor.Position.x-center.x);
            for (var i=0; i<12; i++)
            {
                var turn = i == 0 ? 0 : ((i+1)/2) * (i%2 == 0 ? -1 : 1);
                var direction = angle + turn * Mathf.PI / 6f;
                var goal = center + new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * Mathf.Max(.1f, radius-.15f);
                if (canInteractFrom?.Invoke(goal) == false) continue;
                if (traversalFloor?.Map != null ? TrySetTraversalDestination(goal) : TrySetCityDestination(goal)) return true;
            }
            return false;
        }
        private bool TickNpcApproach()
        {
            if (pendingEntrance != null)
            {
                if (!pendingEntrance.isActiveAndEnabled || ExplorationKeyboardInput.ReadGameplayMovement().sqrMagnitude > 0f)
                { CancelMovement(); return false; }
                if (pendingEntrance.CanInteract())
                { var entrance = pendingEntrance; CancelMovement(); entrance.TryInteract(); return true; }
                if (!movementDestination.HasValue) pendingEntrance = null;
                return false;
            }
            if (pendingNpc == null) return false;
            if (!pendingNpc.isActiveAndEnabled) { CancelMovement(); return false; }
            if (ExplorationKeyboardInput.ReadGameplayMovement().sqrMagnitude > 0f) { CancelMovement(); return false; }
            if (pendingNpc.CanInteract())
            {
                var npc = pendingNpc; CancelMovement(); npc.Interact(); return true;
            }
            if (!movementDestination.HasValue) pendingNpc = null;
            return false;
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
            if (!actor.CanStandAt(goal)) return false;
            if (HasWalkableCitySegment(actor, start, goal)) { points.Add(goal); return true; }
            var polylinePoints = new List<Vector2>();
            bool TryPolyline(Vector2[] route)
            {
                for (var i = 1; i < route.Length; i++)
                    if (!HasWalkableCitySegment(actor, route[i - 1], route[i])) return false;
                polylinePoints.Clear();
                polylinePoints.AddRange(route.Skip(1));
                return true;
            }

            // Keep only the two direct axis detours before entering the bounded
            // grid search. The former offset cross-product (196 polylines) made
            // every blocked click run hundreds of physics sweeps synchronously.
            if (TryPolyline(new[] { start, new Vector2(start.x, goal.y), goal })
                || TryPolyline(new[] { start, new Vector2(goal.x, start.y), goal }))
            {
                points.AddRange(polylinePoints);
                return true;
            }
            const float step = .5f;
            // Keep the freeform city search local to the requested trip. Without
            // a finite envelope, a blocked target can make the A* frontier expand
            // through the entire unbounded integer grid before its node budget is
            // exhausted; that work runs synchronously on the input call and can
            // freeze the city when a route skirts a large building.
            const int detourMargin = 16;
            var goalCell = new Vector2Int(
                Mathf.RoundToInt((goal.x - start.x) / step),
                Mathf.RoundToInt((goal.y - start.y) / step));
            var minX = Mathf.Min(0, goalCell.x) - detourMargin;
            var maxX = Mathf.Max(0, goalCell.x) + detourMargin;
            var minY = Mathf.Min(0, goalCell.y) - detourMargin;
            var maxY = Mathf.Max(0, goalCell.y) + detourMargin;
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
                if (Vector2.Distance(at, goal) <= step * 1.5f && HasWalkableCitySegment(actor, at, goal))
                {
                    points.Add(goal);
                    while (current != Vector2Int.zero) { points.Add(World(current)); current = parents[current]; }
                    points.Reverse(); return true;
                }
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    // Cardinal expansion keeps city routing cheap and avoids
                    // accepting diagonal corner cuts through a character
                    // footprint that is only clear on its two axes.
                    if (Mathf.Abs(x) + Mathf.Abs(y) != 1) continue;
                    var next = current + new Vector2Int(x, y);
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                    if (closed.Contains(next) || !HasWalkableCitySegment(actor, at, World(next))) continue;
                    var cost = costs[current] + Vector2.Distance(at, World(next));
                    if (costs.TryGetValue(next, out var old) && old <= cost) continue;
                    if (!costs.ContainsKey(next)) open.Add(next);
                    costs[next] = cost; parents[next] = current;
                }
            }
            return false;
        }

        private static bool HasWalkableCitySegment(ExplorationCharacterView actor, Vector2 from, Vector2 to)
        {
            if (!actor.HasClearMovementSegment(from, to)) return false;
            var distance = Vector2.Distance(from, to);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / .2f));
            var recoveringFromInitialOverlap = !actor.CanStandAt(from);
            for (var i = 1; i <= samples; i++)
            {
                // HasClearMovementSegment deliberately permits a separating
                // cast origin when a spawned/loaded actor starts slightly inside
                // a collider. Keep sampling until the footprint is clear, but
                // never skip the endpoint (including a short segment's only
                // sample). HasClearMovementSegment guarantees this recovery
                // path is separating; CanStandAt still validates its endpoint.
                var canStand = HasCityContactClearance(
                    actor,
                    Vector2.Lerp(from, to, i / (float)samples));
                if (recoveringFromInitialOverlap)
                {
                    if (canStand) recoveringFromInitialOverlap = false;
                    else if (i == samples) return false;
                    continue;
                }
                if (!canStand) return false;
            }
            return !recoveringFromInitialOverlap;
        }

        private static bool HasCityContactClearance(ExplorationCharacterView actor, Vector2 position)
        {
            if (!actor.CanStandAt(position)) return false;
            var margin = Physics2D.defaultContactOffset;
            if (margin <= 0f || actor.MovementRadius <= 0f) return true;
            var mask = Physics2D.GetLayerCollisionMask(actor.gameObject.layer);
            foreach (var hit in Physics2D.OverlapCircleAll(
                         position + actor.MovementColliderCenterOffset,
                         actor.MovementRadius + margin,
                         mask))
            {
                if (hit != null && !hit.isTrigger
                    && hit.GetComponentInParent<ExplorationCharacterView>() == null)
                    return false;
            }
            return true;
        }

        private Vector2 CityWaypoint(ExplorationCharacterView actor, Queue<Vector2> points, Vector2 final)
        {
            if (HasWalkableCitySegment(actor, actor.Position, final)) { points.Clear(); return final; }
            // Direct movement can lose line of sight after collision sliding
            // around a corner. Rebuild a city route instead of returning the
            // current position forever with an empty queue.
            if (points.Count == 0
                && (!cityShortcutReplanTimes.TryGetValue(actor.CharacterId, out var nextPlan)
                    || Time.time >= nextPlan))
            {
                cityShortcutReplanTimes[actor.CharacterId] = Time.time + .5f;
                if (TryFindWorldRoute(actor, final, out var replanned))
                    foreach (var point in replanned) points.Enqueue(point);
            }
            while (points.Count > 0 && Vector2.Distance(actor.Position, points.Peek()) < .06f) points.Dequeue();
            // Skip intermediate search nodes when the character footprint has a clear segment.
            while (points.Count > 1)
            {
                var next = points.ElementAt(1);
                if (!HasWalkableCitySegment(actor, actor.Position, next)) break;
                points.Dequeue();
            }
            return points.Count > 0 ? points.Peek() : actor.Position;
        }

        private void FollowCity(ExplorationCharacterView actor, ExplorationCharacterView target)
        {
            if (HasWalkableCitySegment(actor, actor.Position, target.Position))
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
            if (route.Points.Count > 0)
                actor.MoveTowards(
                    CityWaypoint(actor, route.Points, route.Target),
                    movementSpeed * Time.fixedDeltaTime,
                    0);
        }

        private bool CanFollowDirectly(ExplorationCharacterView actor, ExplorationCharacterView target)
        {
            if (!TryResolveFollowerGoal(actor, target, out var goal, out var goalWorld, out var atStopDistance))
                return false;
            return atStopDistance || CanNavigateDirectly(
                actor, actor.DungeonPosition, actor.Position, goal, goalWorld);
        }

        private static bool RequiresAuthoredTraversalRoute(
            ExplorationCharacterView follower,
            ExplorationCharacterView previous)
            => follower.DungeonPosition.IsOnStairs
                || previous.DungeonPosition.IsOnStairs
                || follower.DungeonPosition.Surface.Level != previous.DungeonPosition.Surface.Level;

        private bool TryResolveFollowerGoal(
            ExplorationCharacterView follower,
            ExplorationCharacterView previous,
            out DungeonTraversalPosition goal,
            out Vector2 goalWorld,
            out bool atStopDistance)
        {
            goal = previous.DungeonPosition;
            goalWorld = previous.Position;
            atStopDistance = false;

            var toPrevious = previous.Position - follower.Position;
            var distance = toPrevious.magnitude;
            // A follower can only use a freeform stop point while both actors
            // are on the same authored surface. Stairs and level changes keep
            // the existing traversal-node route so elevation state remains
            // explicit in the executor.
            if (RequiresAuthoredTraversalRoute(follower, previous))
                return false;

            if (distance <= followerDistance)
            {
                goal = follower.DungeonPosition;
                goalWorld = follower.Position;
                atStopDistance = true;
                return true;
            }

            var awayFromPrevious = -toPrevious / distance;
            var baseAngle = Mathf.Atan2(awayFromPrevious.y, awayFromPrevious.x);
            foreach (var radiusMultiplier in followerGoalRingRadiusMultipliers)
            {
                var ringRadius = followerDistance * radiusMultiplier;
                foreach (var ringIndex in followerGoalRingOrder)
                {
                    var angle = baseAngle + ringIndex * FollowerGoalRingStep;
                    var candidateWorld = previous.Position
                        + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    if (Vector2.Distance(follower.Position, candidateWorld) <= .05f) continue;

                    var candidateCell = traversalMapView.WorldToCell(candidateWorld);
                    var candidate = new DungeonTraversalPosition(
                        new DungeonSurfacePosition(candidateCell, previous.DungeonPosition.Surface.Level));
                    if (!traversalFloor.Map.TryGetTraversalCell(candidate, out _)
                        || !CanEnterTraversal(follower, candidate)
                        || !CanStandAtTraversalEndpoint(follower, candidate, candidateWorld)) continue;

                    // A freeform goal in the follower's current cell cannot
                    // be detoured by the cell planner: equal endpoints make
                    // it return [start]. Only accept it when the actual
                    // movement segment is clear; otherwise keep searching
                    // the outer ring for a cell that can be planned to.
                    if (candidate == follower.DungeonPosition
                        && !CanNavigateDirectly(
                            follower,
                            follower.DungeonPosition,
                            follower.Position,
                            candidate,
                            candidateWorld)) continue;

                    goal = candidate;
                    goalWorld = candidateWorld;
                    return true;
                }
            }

            // No point on the bounded stop ring is a valid footprint. Keep the
            // original target in the out parameters for diagnostics, but let
            // the caller wait instead of planning toward an unsafe exact point.
            goal = previous.DungeonPosition;
            goalWorld = previous.Position;
            return false;
        }

        private bool TryResolveFollowerGoal(
            ExplorationCharacterView follower,
            ExplorationCharacterView previous,
            FollowerRoute route,
            int navigationRevision,
            out DungeonTraversalPosition goal,
            out Vector2 goalWorld,
            out bool atStopDistance)
        {
            var start = follower.DungeonPosition;
            var startWorld = follower.Position;
            var target = previous.DungeonPosition;
            var targetWorld = previous.Position;
            if (route.MatchesResolvedGoal(start, startWorld, target, targetWorld, navigationRevision))
            {
                if (route.ResolvedGoalAvailable
                    && !route.ResolvedAtStopDistance
                    && (!TryValidateTraversalEndpoint(
                            follower,
                            route.ResolvedGoal,
                            route.ResolvedGoalWorld,
                            out _)
                        || route.ResolvedGoal == follower.DungeonPosition
                            && !CanNavigateDirectly(
                                follower,
                                follower.DungeonPosition,
                                follower.Position,
                                route.ResolvedGoal,
                                route.ResolvedGoalWorld)))
                {
                    route.InvalidateResolvedGoal();
                }
                else
                {
                    goal = route.ResolvedGoal;
                    goalWorld = route.ResolvedGoalWorld;
                    atStopDistance = route.ResolvedAtStopDistance;
                    return route.ResolvedGoalAvailable;
                }
            }

            var available = TryResolveFollowerGoal(
                follower, previous, out goal, out goalWorld, out atStopDistance);
            route.RememberResolvedGoal(
                start, startWorld, target, targetWorld, navigationRevision,
                available, goal, goalWorld, atStopDistance);
            return available;
        }

        private void FollowTraversal(ExplorationCharacterView follower, ExplorationCharacterView previous)
        {
#if UNITY_EDITOR
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                FollowTraversalCore(follower, previous);
            }
            finally
            {
                if (ShouldRecordPerformanceSample(started))
                    RecordPerformanceSample(
                        "FollowTraversal",
                        started,
                        $"follower={follower?.name ?? "none"}");
            }
#else
            FollowTraversalCore(follower, previous);
#endif
        }

        private void FollowTraversalCore(ExplorationCharacterView follower, ExplorationCharacterView previous)
        {
            var requestedTarget = previous.DungeonPosition;
            var requestedTargetWorld = previous.Position;
            var target = requestedTarget;
            var targetWorld = requestedTargetWorld;
            var start = follower.DungeonPosition;
            var startWorld = follower.Position;
            if (!followerRoutes.TryGetValue(follower.CharacterId, out var route))
                followerRoutes.Add(follower.CharacterId, route = new FollowerRoute());
            if (!followerNavigationRevisionBatch || !followerNavigationRevisionValid)
            {
                followerNavigationRevision = GetTraversalNavigationRevision();
                followerBatchTopologyRevision = traversalFloor.Map.TraversalRevision;
                followerNavigationRevisionValid = true;
            }
            var navigationRevision = followerNavigationRevision;
            bool hasResolvedGoal, atStopDistance;
            DungeonTraversalPosition resolvedGoal;
            Vector2 resolvedGoalWorld;
            using (FollowerGoalSample.Auto())
                hasResolvedGoal = TryResolveFollowerGoal(
                    follower, previous, route, navigationRevision,
                    out resolvedGoal, out resolvedGoalWorld, out atStopDistance);
            if (!hasResolvedGoal && !RequiresAuthoredTraversalRoute(follower, previous))
            {
                // Do not pass the leader's exact pointer position to the
                // planner when no safe follower stop point exists. That point
                // may overlap a pillar or leave the footprint in void; the
                // endpoint preflight would reject it after an unnecessary
                // search, and the old fallback retried the same unsafe goal.
                route.Path.Clear();
                route.Target = null;
                route.PlannedTargetWorld = null;
                follower.PreferredTraversalDestination = null;
                if (!route.MatchesFailedRequest(
                        start,
                        startWorld,
                        requestedTarget,
                        requestedTargetWorld,
                        navigationRevision))
                {
                    route.RememberFailedPlan(
                        start,
                        startWorld,
                        requestedTarget,
                        requestedTargetWorld,
                        navigationRevision);
                    route.NextPlan = Time.time + 1f;
                }
                return;
            }
            if (hasResolvedGoal)
            {
                target = resolvedGoal;
                targetWorld = resolvedGoalWorld;
            }
            bool canReachResolvedGoal;
            using (FollowerDirectSample.Auto())
                canReachResolvedGoal = hasResolvedGoal && (atStopDistance || CanNavigateDirectly(
                    follower, follower.DungeonPosition, follower.Position, target, targetWorld));
            if (canReachResolvedGoal)
            {
                meshRoutes.Remove(follower);
                route.Path.Clear(); route.Target = null; route.PlannedTargetWorld = null; route.ClearFailedPlan();
                follower.PreferredTraversalDestination = null;
                using (FollowerMoveSample.Auto())
                    follower.MoveTowards(
                        resolvedGoalWorld,
                        movementSpeed * Time.fixedDeltaTime,
                        atStopDistance ? followerDistance : 0f);
                return;
            }
            // A freeform stop point in the current cell cannot be reached by
            // an any-angle search when its direct segment is blocked: the
            // planner intentionally returns [start] for equal endpoints.
            // The resolver normally skips that candidate; this guard handles
            // a collider/topology change between resolution and execution.
            if (hasResolvedGoal && target == follower.DungeonPosition)
            {
                // The resolver already rejected blocked same-cell goals. This
                // guard covers a collider/topology change between resolution
                // and execution; retry through the same safe-goal path rather
                // than handing the planner an unsafe exact leader point.
                route.InvalidateResolvedGoal();
                route.Path.Clear();
                route.Target = null;
                route.PlannedTargetWorld = null;
                follower.PreferredTraversalDestination = null;
                if (!route.MatchesFailedRequest(
                        start,
                        startWorld,
                        requestedTarget,
                        requestedTargetWorld,
                        navigationRevision))
                {
                    route.RememberFailedPlan(
                        start,
                        startWorld,
                        requestedTarget,
                        requestedTargetWorld,
                        navigationRevision);
                    route.NextPlan = Time.time + 1f;
                }
                return;
            }
            traversalFloor.Map.TryGetTraversalCell(follower.DungeonPosition, out var currentCell);
            var atWaypoint = Vector2.Distance(follower.Position, TraversalAnchorWorld(follower, currentCell)) <= .04f;
            var sameFailedRequest = route.MatchesFailedRequest(
                start, startWorld, target, targetWorld, navigationRevision);
            if (sameFailedRequest) return;

            var requestChanged = route.HasFailedPlan;
            if ((requestChanged || Time.time >= route.NextPlan)
                && (route.Path.Count == 0 || (route.Target != target && atWaypoint)))
            {
                using var planSample = FollowerPlanSample.Auto();
                route.NextPlan = Time.time + .3f;
                route.Path.Clear(); route.Target = target; route.PlannedTargetWorld = null;
                if (FindTraversalPathWithTargetWorld(follower, target, out var path, targetWorld))
                {
                    route.ClearFailedPlan();
                    route.PlannedTargetWorld = targetWorld;
                    SmoothTraversalPath(follower, target, targetWorld, path, retainTargetWaypoint: true);
                    foreach (var step in path.Skip(1)) route.Path.Enqueue(step);
                }
                else
                {
                    route.NextPlan = Time.time + 1f;
                    route.RememberFailedPlan(start, startWorld, target, targetWorld, navigationRevision);
                }
            }
            if (route.Path.Count == 0) return;
            using var moveSample = FollowerMoveSample.Auto();
            if (!AdvanceRoute(follower, route.Path, route.PlannedTargetWorld, 0))
            { route.Path.Clear(); route.Target = null; route.PlannedTargetWorld = null; follower.PreferredTraversalDestination = null; }
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
