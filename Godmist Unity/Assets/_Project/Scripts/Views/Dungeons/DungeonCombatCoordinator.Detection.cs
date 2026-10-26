using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private float detectionElapsed;
        private readonly List<ExplorationCharacterView> detectionTargets = new(4), detectedTargets = new(4);
        private readonly Dictionary<ExplorationCharacterView, DungeonTraversalPosition> detectionPositions = new();
        public int DetectionPositionReadCount { get; private set; }

        private DungeonTraversalPosition ReadDetectionPosition(ExplorationCharacterView view)
        {
            if (!detectionPositions.TryGetValue(view, out var position))
            { position = view.DungeonPosition; detectionPositions.Add(view, position); DetectionPositionReadCount++; }
            return position;
        }
        private static readonly Unity.Profiling.ProfilerMarker DetectionCombatSample = new("Godmist.Dungeon.Detection.CombatMembership");
        private float nextPreviewTime;
        private readonly Dictionary<ExplorationCharacterView, (DungeonTraversalPosition Position, Vector2 Facing, float Range, float Angle, List<DungeonTraversalPosition> Cells)> enemyPreviewCache = new();
        private readonly Dictionary<Vector2Int, List<DungeonTraversalPosition>> previewNodes = new();
        private DungeonFloorMap previewMap;

        private readonly List<(CombatSession First, CombatSession Second)> pendingMerges = new();
        private readonly HashSet<GridPosition> shownFov = new();
        private readonly List<(ExplorationCharacterView Enemy, EncounterTriggerView Source,
            BattleUnit Target, GridPosition Position)> pendingRoomAlerts = new();
        private int previewRevision = -1;
        private CombatSession previewFocus;
        private (ExplorationCharacterView View, DungeonTraversalPosition Position, Vector2 Facing, float Range, float Angle)[] previewActors;
        private (string Id, bool Active, bool Open)[] previewObjects;

        private static bool IsLiving(ExplorationCharacterView view) => view != null
            && view.gameObject.activeInHierarchy && view.RuntimeCharacter?.LifeState == CharacterLifeState.Alive;

        private CombatSession Owner(ExplorationCharacterView view) => view.CombatUnit != null
            && bindingSessions.TryGetValue(view.CombatUnit, out var session) ? session : null;

        private void UpdateDetection()
        {
            using var sample = DetectionSample.Auto();
            detectionElapsed += Time.deltaTime;
            if (detectionElapsed < .1f) return;
            var elapsed = detectionElapsed;
            detectionElapsed = 0f;
            detectionTargets.Clear();
            foreach (var target in party.ActiveMemberViews) if (IsLiving(target)) detectionTargets.Add(target);
            detectionPositions.Clear();
            try { UpdateDetectionCore(elapsed); }
            finally { detectionPositions.Clear(); }
        }

        private void UpdateDetectionCore(float elapsed)
        {
            var triggers = encounters.Triggers;
            if (game?.DebugEnemyAggroDisabled == true)
            {
                foreach (var trigger in triggers)
                foreach (var enemy in trigger.EnemyViews)
                {
                    if (enemy == null) continue;
                    if (Owner(enemy) == null) floor.GetEnemyAwareness(PatrolKey(trigger, enemy)).TryRestore(0f);
                    enemy.ShowAwareness(0f, false);
                }
                return;
            }
            for (int triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                var trigger = triggers[triggerIndex];
                var enemies = trigger.EnemyViews;
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    var enemy = enemies[enemyIndex];
                    if (enemy == null) continue;
                    var state = floor.GetEnemyAwareness(PatrolKey(trigger, enemy));
                    if (!IsLiving(enemy))
                    { state.TryRestore(0f); enemy.ShowAwareness(0f, false); continue; }
                    var fighting = Owner(enemy) != null;
                    detectedTargets.Clear();
                    if (!enemy.IsCrossingLowWall)
                        foreach (var target in detectionTargets)
                            if (!target.IsCrossingLowWall && (fighting
                                ? visibility.CanObserveCombat(floor, ReadDetectionPosition(enemy), enemy.VisionRange, ReadDetectionPosition(target))
                                : visibility.CanDetect(floor, ReadDetectionPosition(enemy), enemy.FacingDirection,
                                    enemy.VisionRange, EnemyObservationAngle(enemy), ReadDetectionPosition(target))))
                                detectedTargets.Add(target);
                    var detected = state.Advance(elapsed, detectedTargets.Count > 0, fighting);
                    // A zero-progress indicator is hidden regardless of party LOS.
                    // Keep detection above live; avoid a map-wide presentation-only
                    // ray query for every unaware enemy on every detection tick.
                    bool showAwareness = false;
                    if (!fighting && state.Progress > 0f)
                        foreach (var target in detectionTargets)
                            if (visibility.HasLineOfSight(floor, ReadDetectionPosition(target), ReadDetectionPosition(enemy))) { showAwareness = true; break; }
                    enemy.ShowAwareness(state.Progress, showAwareness);
                    if (!detected) continue;
                    foreach (var target in detectedTargets)
                    {
                        using var membershipSample = DetectionCombatSample.Auto();
                        var observerSession = Owner(enemy);
                        var targetSession = Owner(target);
                        if (observerSession != null && targetSession == observerSession) continue;
                        if ((observerSession != null && observerSession.Outcome != CombatOutcomeState.Ongoing)
                            || (targetSession != null && targetSession.Outcome != CombatOutcomeState.Ongoing)) continue;
                        if (observerSession == null && targetSession == null)
                        {
                            TryStartSpottedEncounter(trigger, enemy, target);
                        }
                        else if (observerSession == null) TryJoinSpotted(targetSession, enemy, trigger);
                        else if (targetSession == null) TryJoinSpotted(observerSession, target, null);
                        else if (!pendingMerges.Any(pair => pair.First == observerSession && pair.Second == targetSession
                            || pair.First == targetSession && pair.Second == observerSession))
                        {
                            observerSession.MembershipChangePending = targetSession.MembershipChangePending = true;
                            pendingMerges.Add((observerSession, targetSession));
                        }
                    }
                    // Starting/joining combat can snap actors to tactical cells.
                    // The next observer must see their post-callback positions.
                    if (detectedTargets.Count > 0) detectionPositions.Clear();
                    if (Owner(enemy) != null) { state.TryRestore(0f); enemy.ShowAwareness(0f, false); }
                }
            }
        }

        private void OnDisable()
        {
            CancelOpeningPreparation(false);
            CancelFogUpdate();
            detectionElapsed = 0f;
            pendingRoomAlerts.Clear();
            if (encounters != null)
                foreach (var trigger in encounters.Triggers)
                    foreach (var enemy in trigger.EnemyViews) enemy?.ShowAwareness(0f, false);
            ClearFovPreview();
        }

        private void QueueRoomAlert(ExplorationCharacterView observer, ExplorationCharacterView target)
        {
            if (game?.DebugEnemyAggroDisabled == true || target.CombatUnit == null) return;
            foreach (var (enemy, trigger) in RoomAlertCandidates(observer))
                pendingRoomAlerts.Add((enemy, trigger, target.CombatUnit, target.CombatUnit.Position));
        }

        private IEnumerable<(ExplorationCharacterView Enemy, EncounterTriggerView Trigger)> RoomAlertCandidates(ExplorationCharacterView observer)
        {
            var origin = observer.DungeonPosition;
            if (!floor.Map.TryGetRoomId(origin.IsOnStairs ? origin.Stair.First : origin.Surface, out var room)) yield break;
            foreach (var trigger in encounters.Triggers)
            foreach (var enemy in trigger.EnemyViews)
            {
                if (enemy == observer || !IsLiving(enemy)) continue;
                var position = enemy.DungeonPosition;
                if (floor.Map.TryGetRoomId(position.IsOnStairs ? position.Stair.First : position.Surface, out var otherRoom)
                    && otherRoom == room) yield return (enemy, trigger);
            }
        }

        private void ProcessRoomAlerts()
        {
            if (game?.DebugEnemyAggroDisabled == true) { pendingRoomAlerts.Clear(); return; }
            for (var i = pendingRoomAlerts.Count - 1; i >= 0; i--)
            {
                var alert = pendingRoomAlerts[i];
                if (!IsLiving(alert.Enemy) || alert.Target.Character.LifeState != CharacterLifeState.Alive
                    || !bindingSessions.TryGetValue(alert.Target, out var session)
                    || session.Outcome != CombatOutcomeState.Ongoing)
                { pendingRoomAlerts.RemoveAt(i); continue; }
                if (alert.Enemy.IsCrossingLowWall) continue;
                var enemySession = Owner(alert.Enemy);
                if (enemySession != null && enemySession != session)
                {
                    if (enemySession.Outcome != CombatOutcomeState.Ongoing)
                    { pendingRoomAlerts.RemoveAt(i); continue; }
                    if (!pendingMerges.Any(pair => pair.First == session && pair.Second == enemySession
                        || pair.First == enemySession && pair.Second == session))
                    {
                        session.MembershipChangePending = enemySession.MembershipChangePending = true;
                        pendingMerges.Add((session, enemySession));
                    }
                    continue;
                }
                if (enemySession == null && !TryJoinSpotted(session, alert.Enemy, alert.Source)) continue;
                session.Contact?.ReceiveAlert(alert.Enemy.CombatUnit, alert.Target, alert.Position);
                pendingRoomAlerts.RemoveAt(i);
            }
        }

        private bool TryJoinSpotted(CombatSession session, ExplorationCharacterView view, EncounterTriggerView source)
        {
            if (game?.DebugEnemyAggroDisabled == true) return false;
            var driver = combatHost.GetDriver(session);
            if (driver == null || session.MembershipChangePending || driver.IsActionInProgress) return false;
            var position = DungeonBattleGridFactory.ToBattleTraversalPosition(session.DungeonContext, view.DungeonPosition);
            var unit = source == null && retainedPlayers.TryGetValue(view.CharacterId, out var retained)
                ? retained : sessionFactory.CreateJoiningUnit(view.RuntimeCharacter, position, source == null);
            if(source!=null)unit.IsBoss=ScoutCombatSystem.IsBoss(game.ActiveDungeon,source.Encounter.Definition,view.EnemyInstanceId);
            if (!driver.TryJoin(unit, position)) return false;
            if (source != null)
            {
                game.ActiveDungeon.TryGetPreRolledRewards(source.Encounter.InstanceId, out var loot);
                DungeonCombatMembershipSystem.RegisterEnemy(session, source.Encounter.InstanceId, view.EnemyInstanceId, loot, unit);
            }
            else retainedPlayers[view.CharacterId] = unit;
            bindings[unit] = view;
            bindingSessions[unit] = session;
            unitViews.Borrow(session, unit, view);
            party.SeparateForCombat();
            party.RefreshControlState();
            return true;
        }

        private void ProcessPendingMerges()
        {
            if (game?.DebugEnemyAggroDisabled == true) pendingMerges.Clear();
            foreach (var pair in pendingMerges.ToArray())
            {
                var first = pair.First;
                var second = pair.Second;
                if (!game.Combats.Contains(first) || !game.Combats.Contains(second)
                    || first.Outcome != CombatOutcomeState.Ongoing || second.Outcome != CombatOutcomeState.Ongoing)
                { pendingMerges.Remove(pair); continue; }
                var destination = first == focused || second != focused ? first : second;
                var source = destination == first ? second : first;
                if (combatHost.GetDriver(destination).IsActionInProgress || combatHost.GetDriver(source).IsActionInProgress) continue;
                if (!MergeCombatSessions(destination, source))
                { Debug.LogError("Nie można połączyć sesji na wspólnym piętrze."); pendingMerges.Remove(pair); continue; }
                SetFocus(game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId), true);
            }
            foreach (var session in game.Combats)
                session.MembershipChangePending = pendingMerges.Any(pair => pair.First == session || pair.Second == session);
        }

        private bool MergeCombatSessions(CombatSession destination, CombatSession source)
        {
            if (!DungeonCombatMembershipSystem.TryMerge(destination, source)) return false;
            foreach (var binding in bindings.ToArray())
                if (bindingSessions[binding.Key] == source)
                { bindingSessions[binding.Key] = destination; unitViews.Borrow(destination, binding.Key, binding.Value); }
            combatHost.End(source, true);
            combatCamera.Forget(source);
            game.TryRemoveCombat(source);
            sources.Remove(source);
            source.UnitEnteredCell = null; source.EvaluateTrapRisk = null; source.TrapRouteState = null;
            source.ResumeTurn = null; source.TrySpawnReinforcements = null;
            for (var i = 0; i < pendingMerges.Count; i++)
            {
                var other = pendingMerges[i];
                pendingMerges[i] = (other.First == source ? destination : other.First, other.Second == source ? destination : other.Second);
            }
            pendingMerges.RemoveAll(value => value.First == value.Second);
            return true;
        }


        private void InvalidateFovPreview() { previewRevision = -1; fogGeometryDirty = true; enemyPreviewCache.Clear(); }

        private void ClearFovPreview()
        {
            if (gridFeedback == null) return;
            gridFeedback.ClearOutline(CombatGridFeedbackLayer.EnemyFov);
            gridFeedback.ClearFill(CombatGridFeedbackLayer.EnemyFov);
            shownFov.Clear(); previewRevision = -1;
        }

        private void UpdateFovPreview()
        {
            if (UIWorkspaceInputRouter.HasFocusedTextInput() || !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            { ClearFovPreview(); return; }
            if (Time.unscaledTime < nextPreviewTime && previewRevision >= 0 && previewFocus == focused) return;
            nextPreviewTime = Time.unscaledTime + .1f;
            var observers = party.ActiveMemberViews.Where(IsLiving).ToArray();
            var livingEnemies = encounters.Triggers.SelectMany(trigger => trigger.EnemyViews).Where(IsLiving).ToArray();
            var actors = observers.Concat(livingEnemies).Select(view => (view, view.DungeonPosition, view.FacingDirection, view.VisionRange, EnemyObservationAngle(view))).ToArray();
            var geometry = floor.Objects.Select(item => (item.InstanceId, item.IsActive, item is DoorDungeonObjectInstance door && door.IsOpen)).ToArray();
            if (previewActors != null && actors.SequenceEqual(previewActors) && previewObjects != null && geometry.SequenceEqual(previewObjects)
                && previewRevision == map.PresentationRevision && previewFocus == focused) return;
            if (previewObjects == null || !geometry.SequenceEqual(previewObjects)) enemyPreviewCache.Clear();
            previewActors = actors; previewObjects = geometry;
            if (previewMap != floor.Map)
            {
                previewMap = floor.Map; previewNodes.Clear(); enemyPreviewCache.Clear();
                void Index(DungeonTraversalPosition node)
                {
                    if (!floor.Map.TryGetTraversalCell(node, out var cell)) return;
                    if (!previewNodes.TryGetValue(cell, out var nodes)) previewNodes.Add(cell, nodes = new());
                    nodes.Add(node);
                }
                foreach (var surface in floor.Map.Surfaces) Index(new DungeonTraversalPosition(surface));
                foreach (var stair in floor.Map.Stairs)
                    if (floor.Map.TryGetStairPath(stair, out var steps))
                        for (var i = 1; i < steps.Count - 1; i++) Index(new DungeonTraversalPosition(stair, i));
            }
            var cells = new HashSet<GridPosition>();
            foreach (var enemy in livingEnemies)
            {
                if (!map.IsCurrentlySeen(enemy.DungeonPosition)) continue;
                var position = enemy.DungeonPosition; var facing = enemy.FacingDirection;
                var range = enemy.VisionRange; var angle = EnemyObservationAngle(enemy);
                if (!enemyPreviewCache.TryGetValue(enemy, out var cached) || cached.Position != position
                    || cached.Facing != facing || cached.Range != range || cached.Angle != angle)
                {
                    var area = new List<DungeonTraversalPosition>();
                    if (floor.Map.TryGetTraversalCell(position, out var origin))
                    {
                        var radius = Mathf.CeilToInt(range);
                        for (var y = origin.y - radius; y <= origin.y + radius; y++)
                        for (var x = origin.x - radius; x <= origin.x + radius; x++)
                            if (previewNodes.TryGetValue(new Vector2Int(x, y), out var nodes))
                                foreach (var node in nodes)
                                    if (visibility.CanDetect(floor, position, facing, range, angle, node)) area.Add(node);
                    }
                    cached = (position, facing, range, angle, area); enemyPreviewCache[enemy] = cached;
                }
                foreach (var node in cached.Cells)
                {
                    if (!map.IsTraversalSelectable(node)) continue;
                    floor.Map.TryGetTraversalCell(node, out var cell);
                    cells.Add(focused != null ? DungeonBattleGridFactory.ToBattleTraversalPosition(focused.DungeonContext, node)
                        : new GridPosition(cell.x, cell.y));
                }
            }
            if (shownFov.SetEquals(cells) && previewRevision == map.PresentationRevision && previewFocus == focused) return;
            shownFov.Clear(); shownFov.UnionWith(cells);
            previewRevision = map.PresentationRevision; previewFocus = focused;
            if (focused == null)
            {
                var origin = map.CellToWorldCenter(Vector2Int.zero);
                gridFeedback.SetGridMapping(origin, Vector2.Distance(origin, map.CellToWorldCenter(Vector2Int.right)));
            }
            gridFeedback.ShowFill(CombatGridFeedbackLayer.EnemyFov, cells, new Color(1f, .12f, .08f, .2f));
            gridFeedback.ShowOutline(CombatGridFeedbackLayer.EnemyFov, cells, new Color(1f, .2f, .1f, .8f));
        }
    }
}
