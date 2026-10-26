using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Exploration;
using UnityEngine;
namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private sealed class PatrolNavigation
        {
            public readonly Queue<DungeonTraversalPosition> Path = new();
            public DungeonTraversalPosition? Target;
            public float RetryAfter;
        }
        private sealed class PatrolIdentity
        {
            public DungeonEncounterInstance Encounter;
            public string EnemyId, Key, ActorId;
        }
        private readonly Dictionary<ExplorationCharacterView, PatrolIdentity> patrolIdentities = new();
        private readonly Dictionary<string, PatrolNavigation> patrolNavigation = new();
        private readonly HashSet<Vector2Int> patrolTrapCells = new();
        private readonly Dictionary<DungeonTraversalPosition, bool> patrolEnterability = new();
        private readonly List<DungeonTraversalPosition> patrolFacingNeighbours = new(16);
        private readonly List<DungeonTraversalPosition> patrolFacingExits = new(16);
        private System.Func<DungeonSurfaceEdge, bool> patrolCanOpenDoor;
        private readonly List<ExplorationCharacterView> patrolTargets = new();
        private readonly Dictionary<ExplorationCharacterView, DungeonTraversalPosition> patrolTargetPositions = new();
        private static readonly Unity.Profiling.ProfilerMarker PatrolSenseSample = new("Godmist.Dungeon.Patrols.Sense");
        private static readonly Unity.Profiling.ProfilerMarker PatrolPlanSample = new("Godmist.Dungeon.Patrols.Plan");
        private static readonly Unity.Profiling.ProfilerMarker PatrolMoveSample = new("Godmist.Dungeon.Patrols.Move");
        private static readonly Unity.Profiling.ProfilerMarker PatrolFacingSample = new("Godmist.Dungeon.Patrols.Facing");
        private static readonly Unity.Profiling.ProfilerMarker PatrolValidationSample = new("Godmist.Dungeon.Patrols.ValidateMove");
        private static readonly Unity.Profiling.ProfilerMarker PatrolExecutionSample = new("Godmist.Dungeon.Patrols.ExecuteMove");
        private static readonly Unity.Profiling.ProfilerMarker PatrolPersistSample = new("Godmist.Dungeon.Patrols.Persist");
        private static readonly Unity.Profiling.ProfilerMarker PatrolReadPositionSample = new("Godmist.Dungeon.Patrols.ReadPosition");
        private static readonly Unity.Profiling.ProfilerMarker PatrolStoreActorSample = new("Godmist.Dungeon.Patrols.StoreActor");
        private static readonly Unity.Profiling.ProfilerMarker PatrolStoreEncounterSample = new("Godmist.Dungeon.Patrols.StoreEncounter");
        private readonly Dictionary<Vector2Int, Vector2> patrolCellCenters = new();
        private bool patrolCenterBatch;
        public int PatrolCellCenterQueryCount { get; private set; }
        private float nextPatrolSense, patrolElapsed;
        private const float PatrolSpeed = 2f;

        private void InitializePatrols()
        {
            patrolNavigation.Clear();
            patrolIdentities.Clear();
            patrolTrapCells.Clear();
            foreach (var trap in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                if (trap.IsActive && !trap.IsTriggered) patrolTrapCells.Add(trap.Position);
            foreach (var trigger in encounters.Triggers)
            {
                foreach (var enemy in trigger.EnemyViews.Where(IsLiving))
                {
                    FaceOpenPatrolSpace(enemy, onlyIfBlocked: true);
                    if (!trigger.Encounter.StationaryEnemyHomes.TryGetValue(enemy.EnemyInstanceId, out var home)) continue;
                    var key = PatrolKey(trigger, enemy);
                    if (floor.Patrols.ContainsKey(key)) continue;
                    var guard = new DungeonPatrolState();
                    guard.Route.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(home)));
                    guard.StopIndices.Add(0); floor.Patrols.Add(key, guard);
                    PersistPatrolPosition(trigger, enemy);
                }
                var enemies = trigger.EnemyViews.Where(v => IsLiving(v)
                    && !trigger.Encounter.StationaryEnemyHomes.ContainsKey(v.EnemyInstanceId)).OrderBy(v => v.EnemyInstanceId, System.StringComparer.Ordinal).ToArray();
                if (enemies.Length == 0 || enemies.All(v => floor.Patrols.TryGetValue(PatrolKey(trigger, v), out var saved)
                    && saved.StopIndices.Count > 0 && saved.Route.All(p => IsSafePatrolPosition(p) && party.CanEnterTraversal(v,p)))) continue;
                var paths = DungeonPatrolPlanner.Build(floor.Map, trigger.Encounter.RoomId,
                    enemies.Select(v => PatrolOrigin(trigger, v)).ToArray(),
                    p => IsSafePatrolPosition(p) && party.CanEnterTraversal(enemies[0], p), out var stops,
                    party.CanOpenTraversalDoor);
                for (var i = 0; i < enemies.Length; i++)
                {
                    var key = PatrolKey(trigger, enemies[i]);
                    floor.Patrols.TryGetValue(key, out var old);
                    if (old?.StopIndices.Count > 0 && old.Route.All(p => IsSafePatrolPosition(p) && party.CanEnterTraversal(enemies[i],p))) continue;
                    if (paths[i].Count == 0)
                    {
                        // An old guard outside its room keeps its return route until it gets home.
                        if (old != null && old.Route.All(p => IsSafePatrolPosition(p) && party.CanEnterTraversal(enemies[i],p)))
                            for (var n = 0; n < old.Route.Count; n++) old.StopIndices.Add(n);
                        else floor.Patrols.Remove(key);
                        continue;
                    }
                    var state = new DungeonPatrolState
                    {
                        WaitRemaining = old?.WaitRemaining ?? 0,
                        ObservedCharacterId = old?.ObservedCharacterId,
                        Stops = old?.Stops ?? 0
                    };
                    state.Route.AddRange(paths[i]); state.StopIndices.UnionWith(stops[i]);
                    floor.Patrols[key] = state;
                    PersistPatrolPosition(trigger, enemies[i]);
                }
            }
        }

        private bool IsSafePatrolPosition(DungeonTraversalPosition position)
            => floor.Map.TryGetTraversalCell(position, out var cell)
                && !patrolTrapCells.Contains(cell);

        private DungeonTraversalPosition PatrolOrigin(EncounterTriggerView trigger, ExplorationCharacterView enemy)
        {
            bool InRoom(DungeonTraversalPosition position) => floor.Map.TryGetRoomId(
                position.IsOnStairs ? position.Stair.First : position.Surface, out var room)
                && room == trigger.Encounter.RoomId && IsSafePatrolPosition(position)
                && party.CanEnterTraversal(enemy, position);
            if (InRoom(enemy.DungeonPosition)) return enemy.DungeonPosition;
            // Legacy saves can leave a survivor outside its home room after combat.
            // Plan in its original room, then walk back from the actual saved position.
            if (floor.Patrols.TryGetValue(PatrolKey(trigger, enemy), out var old))
                foreach (var position in old.Route) if (InRoom(position)) return position;
            return enemy.DungeonPosition;
        }

        private PatrolIdentity PatrolIdentityFor(EncounterTriggerView trigger, ExplorationCharacterView view)
        {
            if (!patrolIdentities.TryGetValue(view, out var identity)
                || !ReferenceEquals(identity.Encounter, trigger.Encounter) || identity.EnemyId != view.EnemyInstanceId)
            {
                identity = new PatrolIdentity {
                    Encounter = trigger.Encounter, EnemyId = view.EnemyInstanceId,
                    Key = DungeonCombatMembershipSystem.EnemyKey(trigger.Encounter.InstanceId, view.EnemyInstanceId),
                    ActorId = "enemy:" + trigger.Encounter.InstanceId + ":" + view.EnemyInstanceId
                };
                patrolIdentities[view] = identity;
            }
            return identity;
        }

        private string PatrolKey(EncounterTriggerView trigger, ExplorationCharacterView view)
            => PatrolIdentityFor(trigger, view).Key;

        private void UpdatePatrols()
            => UpdatePatrolsForElapsed(Time.deltaTime);

        private void UpdatePatrolsForElapsed(float deltaTime)
            => UpdatePatrolsAtTime(deltaTime, Time.time);

        private void UpdatePatrolsAtTime(float deltaTime, float time)
        {
            patrolCellCenters.Clear(); patrolCenterBatch = true;
            try { UpdatePatrolsCore(deltaTime, time); }
            finally { patrolCenterBatch = false; patrolCellCenters.Clear(); patrolEnterability.Clear(); }
        }

        private void UpdatePatrolsCore(float deltaTime, float time)
        {
            using var sample = PatrolSample.Auto();
            patrolElapsed += deltaTime;
            if (patrolElapsed < .02f) return;
            var elapsed = Mathf.Min(patrolElapsed, .1f); patrolElapsed = 0;
            var sense = time >= nextPatrolSense;
            if (sense) nextPatrolSense = time + .1f;
            using var navigationBatch = party.BeginTraversalQueryBatch();
            patrolTargets.Clear();
            patrolTargetPositions.Clear();
            foreach (var target in party.ActiveMemberViews) if (IsLiving(target))
            { patrolTargets.Add(target); patrolTargetPositions[target] = target.DungeonPosition; }
            patrolTargets.Sort((a, b) => System.StringComparer.Ordinal.Compare(a.CharacterId, b.CharacterId));
            var movementScale = Vector2.Distance(PatrolCellWorld(Vector2Int.zero), PatrolCellWorld(Vector2Int.right));
            var triggers = encounters.Triggers;
            for (int triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                var trigger = triggers[triggerIndex];
                var enemies = trigger.EnemyViews;
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    var enemy = enemies[enemyIndex];
                    if (!IsLiving(enemy)) continue;
                    patrolEnterability.Clear();
                    var key = PatrolKey(trigger, enemy);
                    if (!floor.Patrols.TryGetValue(key, out var state)) continue;
                    if (Owner(enemy) != null || enemy.CombatUnit != null)
                    { state.ObservedCharacterId = null; patrolNavigation.Remove(key);continue; }
                    var enemyPosition = enemy.DungeonPosition;
                    ExplorationCharacterView observed = null;
                    if (game?.DebugEnemyAggroDisabled == true) state.ObservedCharacterId = null;
                    foreach (var target in patrolTargets) if (target.CharacterId == state.ObservedCharacterId) { observed = target; break; }
                    if (sense && game?.DebugEnemyAggroDisabled != true)
                    {
                        using var senseSample = PatrolSenseSample.Auto();
                        // A noticed character is tracked by LOS, not lost when it leaves the old facing cone.
                        if (observed != null && !visibility.HasLineOfSight(floor, enemyPosition, patrolTargetPositions[observed])) observed = null;
                        if (observed == null)
                            foreach (var target in patrolTargets)
                                if (visibility.CanDetect(floor, enemyPosition, enemy.FacingDirection, enemy.VisionRange,
                                    EnemyObservationAngle(enemy), patrolTargetPositions[target])) { observed = target; break; }
                        state.ObservedCharacterId = observed?.CharacterId;
                    }
                    if (observed != null)
                    {
                        enemy.PreferredTraversalDestination = null;patrolNavigation.Remove(key);
                        enemy.FaceTowards(observed.Position);continue;
                    }
                    if (enemy.IsCrossingLowWall) { PersistPatrolPosition(trigger, enemy);continue; }
                    if (state.WaitRemaining > 0) { state.WaitRemaining = Mathf.Max(0, state.WaitRemaining - elapsed);continue; }
                    var goal = state.Route[state.NextPoint];
                    floor.Map.TryGetTraversalCell(goal, out var goalCell);
                    if (enemyPosition == goal && Vector2.Distance(enemy.Position, PatrolWaypointWorld(enemy, goalCell)) < .035f)
                    { state.Arrive(key); FaceOpenPatrolSpace(enemy); patrolNavigation.Remove(key);continue; }
                    if (!patrolNavigation.TryGetValue(key, out var navigation)) patrolNavigation.Add(key, navigation = new PatrolNavigation());
                    if (navigation.Target != goal || navigation.Path.Count == 0)
                    {
                        using var planSample = PatrolPlanSample.Auto();
                        if (time < navigation.RetryAfter) continue;
                        navigation.RetryAfter = time + 1f;navigation.Target = goal;navigation.Path.Clear();
                        if (!TryBuildPatrolPath(enemy, goal, out var path))
                        { state.Arrive(key); enemy.PreferredTraversalDestination = null; continue; }
                        foreach (var point in path) navigation.Path.Enqueue(point);
                    }
                    while (navigation.Path.Count > 0)
                    {
                        using var moveSample = PatrolMoveSample.Auto();
                        var next = navigation.Path.Peek();
                        floor.Map.TryGetTraversalCell(next, out var cell);
                        var waypoint = Vector2.zero;
                        if (enemyPosition == next)
                        {
                            waypoint = PatrolWaypointWorld(enemy, cell);
                            if (Vector2.Distance(enemy.Position, waypoint) < .035f)
                            { navigation.Path.Dequeue();continue; }
                        }
                        bool canMove;
                        using (PatrolValidationSample.Auto())
                            canMove = CanEnterPatrolQuery(enemy, next)
                                && (enemyPosition == next || IsPatrolStepOpen(enemyPosition, next));
                        if (!canMove)
                        { navigation.Path.Clear();enemy.PreferredTraversalDestination = null;break; }
                        if (enemyPosition != next)
                        {
                            party.OpenTraversalDoor(enemy, next);
                            // Door callbacks can change scene geometry. Re-read after
                            // that mutation, while same-cell movement reuses its target.
                            waypoint = PatrolWaypointWorld(enemy, cell);
                        }
                        // A door/movement can change physical state. Only reuse
                        // planning results before the first mutation for this actor.
                        patrolEnterability.Clear();
                        enemy.PreferredTraversalDestination = next;
                        using (PatrolExecutionSample.Auto())
                            enemy.MoveTowards(waypoint, PatrolSpeed * movementScale * elapsed, 0);
                        PersistPatrolPosition(trigger, enemy);break;
                    }
                }
            }
        }

        private void FaceOpenPatrolSpace(ExplorationCharacterView enemy, bool onlyIfBlocked = false)
        {
            using var facingSample = PatrolFacingSample.Auto();
            if (!TrySelectPatrolFacing(enemy, onlyIfBlocked, out var target)) return;
            floor.Map.TryGetTraversalCell(target, out var cell);
            enemy.FaceTowards(PatrolWaypointWorld(enemy, cell));
        }

        private bool TrySelectPatrolFacing(ExplorationCharacterView enemy, bool onlyIfBlocked, out DungeonTraversalPosition target)
        {
            target = default;
            patrolEnterability.Clear();
            if (patrolCanOpenDoor == null || !ReferenceEquals(patrolCanOpenDoor.Target, party))
                patrolCanOpenDoor = party.CanOpenTraversalDoor;
            floor.Map.CollectTraversalNeighbours(enemy.DungeonPosition, patrolFacingNeighbours, patrolCanOpenDoor);
            int bestScore = -1;
            foreach (var candidate in patrolFacingNeighbours)
            {
                if (!CanEnterPatrolQuery(enemy, candidate)) continue;
                if (onlyIfBlocked)
                {
                    floor.Map.TryGetTraversalCell(candidate, out var cell);
                    if (Vector2.Dot((PatrolWaypointWorld(enemy, cell) - enemy.Position).normalized,
                        enemy.FacingDirection.normalized) > .7f) return false;
                }
                floor.Map.CollectTraversalNeighbours(candidate, patrolFacingExits, patrolCanOpenDoor);
                int score = 0;
                foreach (var exit in patrolFacingExits) if (CanEnterPatrolQuery(enemy, exit)) score++;
                // Strict comparison retains the first candidate on ties, exactly
                // like stable OrderByDescending, without iterators or sorting.
                if (score > bestScore) { bestScore = score; target = candidate; }
            }
            return bestScore >= 0;
        }

        // Cache only within one synchronous query, never across movement,
        // opening doors, obstacle changes or actors with different footprints.
        private bool CanEnterPatrolQuery(ExplorationCharacterView enemy, DungeonTraversalPosition point)
        {
            if (!patrolEnterability.TryGetValue(point, out var allowed))
                patrolEnterability.Add(point, allowed = IsSafePatrolPosition(point) && party.CanEnterTraversal(enemy, point));
            return allowed;
        }

        private bool TryBuildPatrolPath(ExplorationCharacterView enemy, DungeonTraversalPosition goal,
            out List<DungeonTraversalPosition> path)
        {
            patrolEnterability.Clear();
            var start = enemy.DungeonPosition;
            if (start != goal && IsPatrolStepOpen(start, goal)
                && CanEnterPatrolQuery(enemy, goal))
            { path = new List<DungeonTraversalPosition> { start, goal }; return true; }
            return DungeonExplorationPathfinder.TryFindPath(floor.Map, start, goal, out path,
                p => CanEnterPatrolQuery(enemy, p), party.CanOpenTraversalDoor);
        }

        private bool IsPatrolStepOpen(DungeonTraversalPosition from, DungeonTraversalPosition to)
            // Ordinary same-level movement is exactly the surface branch of
            // GetTraversalNeighbours. Keep stair/landing transitions on its
            // original path, and recheck doors/gates on every move.
        {
            if (patrolCanOpenDoor == null || !ReferenceEquals(patrolCanOpenDoor.Target, party))
                patrolCanOpenDoor = party.CanOpenTraversalDoor;
            return !from.IsOnStairs && !to.IsOnStairs && from.Surface.Level == to.Surface.Level
                ? floor.Map.CanTraverseBetweenSurfaces(from.Surface, to.Surface, patrolCanOpenDoor)
                : floor.Map.GetTraversalNeighbours(from, patrolCanOpenDoor).Contains(to);
        }

        private Vector2 PatrolWaypointWorld(ExplorationCharacterView enemy, Vector2Int cell)
            // Match party navigation: the cell centre anchors the collision footprint,
            // while the world root remains at the sprite's grounded feet pivot.
            => PatrolCellWorld(cell) - enemy.MovementColliderCenterOffset;

        private Vector2 PatrolCellWorld(Vector2Int cell)
        {
            if (!patrolCenterBatch) return map.CellToWorldCenter(cell);
            if (!patrolCellCenters.TryGetValue(cell, out var position))
            { position = map.CellToWorldCenter(cell); patrolCellCenters.Add(cell, position); PatrolCellCenterQueryCount++; }
            return position;
        }

        private void PersistPatrolPosition(EncounterTriggerView trigger, ExplorationCharacterView enemy)
        {
            using var persistSample = PatrolPersistSample.Auto();
            DungeonTraversalPosition position;
            Vector2Int cell;
            Vector2 world;
            using (PatrolReadPositionSample.Auto())
            {
                enemy.ReadTraversalPosition(out world, out position);
                if (!floor.Map.TryGetTraversalCell(position, out cell)) return;
            }
            var id = PatrolIdentityFor(trigger, enemy).ActorId;
            using (PatrolStoreActorSample.Auto())
            {
                // Within-cell movement changes the offset, not the traversal
                // cell. Avoid removing and re-inserting its offset each tick.
                if (!floor.TryGetActorPosition(id, out var saved) || saved != position)
                    floor.TrySetActorPosition(id, position);
                floor.SetActorOffset(id, world - PatrolCellWorld(cell));
            }
            using (PatrolStoreEncounterSample.Auto())
                if (!trigger.Encounter.TryGetEnemyExplorationPosition(enemy.EnemyInstanceId, out var savedCell) || savedCell != cell)
                    trigger.Encounter.TrySetEnemyExplorationPosition(enemy.EnemyInstanceId, cell);
        }
    }
}
