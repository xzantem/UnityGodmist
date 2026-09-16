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
        private readonly Dictionary<string, PatrolNavigation> patrolNavigation = new();
        private float nextPatrolSense, patrolElapsed;
        private const float PatrolSpeed = 2f;

        private void InitializePatrols()
        {
            patrolNavigation.Clear();
            foreach (var trigger in encounters.Triggers)
            {
                foreach (var enemy in trigger.EnemyViews.Where(IsLiving))
                {
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
                if (enemies.Length == 0 || enemies.All(v => floor.Patrols.TryGetValue(PatrolKey(trigger, v), out var saved) && saved.StopIndices.Count > 0)) continue;
                var paths = DungeonPatrolPlanner.Build(floor.Map, trigger.Encounter.RoomId,
                    enemies.Select(v => PatrolOrigin(trigger, v)).ToArray(), p => party.CanEnterTraversal(enemies[0], p), out var stops);
                for (var i = 0; i < enemies.Length; i++)
                {
                    var key = PatrolKey(trigger, enemies[i]);
                    floor.Patrols.TryGetValue(key, out var old);
                    if (old?.StopIndices.Count > 0) continue;
                    if (paths[i].Count == 0)
                    {
                        // An old guard outside its room keeps its return route until it gets home.
                        if (old != null) for (var n = 0; n < old.Route.Count; n++) old.StopIndices.Add(n);
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

        private DungeonTraversalPosition PatrolOrigin(EncounterTriggerView trigger, ExplorationCharacterView enemy)
        {
            bool InRoom(DungeonTraversalPosition position) => floor.Map.TryGetRoomId(
                position.IsOnStairs ? position.Stair.First : position.Surface, out var room)
                && room == trigger.Encounter.RoomId && party.CanEnterTraversal(enemy, position);
            if (InRoom(enemy.DungeonPosition)) return enemy.DungeonPosition;
            // Legacy saves can leave a survivor outside its home room after combat.
            // Plan in its original room, then walk back from the actual saved position.
            if (floor.Patrols.TryGetValue(PatrolKey(trigger, enemy), out var old))
                foreach (var position in old.Route) if (InRoom(position)) return position;
            return enemy.DungeonPosition;
        }

        private static string PatrolKey(EncounterTriggerView trigger, ExplorationCharacterView view)
            => DungeonCombatMembershipSystem.EnemyKey(trigger.Encounter.InstanceId, view.EnemyInstanceId);

        private void UpdatePatrols()
        {
            patrolElapsed += Time.deltaTime;
            if (patrolElapsed < .02f) return;
            var elapsed = Mathf.Min(patrolElapsed, .1f); patrolElapsed = 0;
            var sense = Time.time >= nextPatrolSense;
            if (sense) nextPatrolSense = Time.time + .1f;
            var targets = party.ActiveMemberViews.Where(IsLiving).OrderBy(v => v.CharacterId, System.StringComparer.Ordinal).ToArray();
            foreach (var trigger in encounters.Triggers)
            foreach (var enemy in trigger.EnemyViews)
            {
                if (!IsLiving(enemy)) continue;
                var key = PatrolKey(trigger, enemy);
                if (!floor.Patrols.TryGetValue(key, out var state)) continue;
                if (Owner(enemy) != null || enemy.CombatUnit != null)
                { state.ObservedCharacterId = null; patrolNavigation.Remove(key);continue; }
                var observed = targets.FirstOrDefault(v => v.CharacterId == state.ObservedCharacterId);
                if (sense)
                {
                    // A noticed character is tracked by LOS, not lost when it leaves the old facing cone.
                    if (observed != null && !visibility.HasLineOfSight(floor, enemy.DungeonPosition, observed.DungeonPosition)) observed = null;
                    observed ??= targets.FirstOrDefault(target => CanEnemyDetect(enemy, target));
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
                if (enemy.DungeonPosition == goal && Vector2.Distance(enemy.Position, map.CellToWorldCenter(goalCell)) < .035f)
                { state.Arrive(key);patrolNavigation.Remove(key);continue; }
                if (!patrolNavigation.TryGetValue(key, out var navigation)) patrolNavigation.Add(key, navigation = new PatrolNavigation());
                if (navigation.Target != goal || navigation.Path.Count == 0)
                {
                    if (Time.time < navigation.RetryAfter) continue;
                    navigation.RetryAfter = Time.time + 1f;navigation.Target = goal;navigation.Path.Clear();
                    if (!DungeonExplorationPathfinder.TryFindPath(floor.Map, enemy.DungeonPosition, goal, out var path,
                        p => party.CanEnterTraversal(enemy, p))) continue;
                    foreach (var point in path) navigation.Path.Enqueue(point);
                }
                while (navigation.Path.Count > 0)
                {
                    var next = navigation.Path.Peek();
                    floor.Map.TryGetTraversalCell(next, out var cell);
                    if (enemy.DungeonPosition == next && Vector2.Distance(enemy.Position, map.CellToWorldCenter(cell)) < .035f)
                    { navigation.Path.Dequeue();continue; }
                    if (!party.CanEnterTraversal(enemy, next) || enemy.DungeonPosition != next
                        && !floor.Map.GetTraversalNeighbours(enemy.DungeonPosition).Contains(next))
                    { navigation.Path.Clear();enemy.PreferredTraversalDestination = null;break; }
                    enemy.PreferredTraversalDestination = next;
                    var size = Vector2.Distance(map.CellToWorldCenter(Vector2Int.zero), map.CellToWorldCenter(Vector2Int.right));
                    enemy.MoveTowards(map.CellToWorldCenter(cell), PatrolSpeed * size * elapsed, 0);
                    PersistPatrolPosition(trigger, enemy);break;
                }
            }
        }

        private void PersistPatrolPosition(EncounterTriggerView trigger, ExplorationCharacterView enemy)
        {
            var position = enemy.DungeonPosition;
            if (!floor.Map.TryGetTraversalCell(position, out var cell)) return;
            var id = "enemy:" + trigger.Encounter.InstanceId + ":" + enemy.EnemyInstanceId;
            floor.TrySetActorPosition(id, position);floor.SetActorOffset(id, enemy.Position - map.CellToWorldCenter(cell));
            trigger.Encounter.TrySetEnemyExplorationPosition(enemy.EnemyInstanceId, cell);
        }
    }
}
