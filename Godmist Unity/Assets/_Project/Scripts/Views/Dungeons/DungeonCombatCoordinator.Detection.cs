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
        private float nextDetectionTime;
        private float nextPreviewTime;
        private readonly List<(CombatSession First, CombatSession Second)> pendingMerges = new();
        private readonly HashSet<GridPosition> shownFov = new();
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
            if (Time.unscaledTime < nextDetectionTime) return;
            nextDetectionTime = Time.unscaledTime + .1f;
            foreach (var trigger in encounters.Triggers)
            foreach (var enemy in trigger.EnemyViews)
            {
                if (!IsLiving(enemy) || enemy.IsCrossingLowWall) continue;
                foreach (var target in party.ActiveMemberViews)
                {
                    if (!IsLiving(target) || target.IsCrossingLowWall || !CanEnemyDetect(enemy, target)) continue;
                    var observerSession = Owner(enemy);
                    var targetSession = Owner(target);
                    if (observerSession != null && targetSession == observerSession) continue;
                    if ((observerSession != null && observerSession.Outcome != CombatOutcomeState.Ongoing)
                        || (targetSession != null && targetSession.Outcome != CombatOutcomeState.Ongoing)) continue;
                    if (observerSession == null && targetSession == null) TryStartSpottedEncounter(trigger, enemy, target);
                    else if (observerSession == null) TryJoinSpotted(targetSession, enemy, trigger);
                    else if (targetSession == null) TryJoinSpotted(observerSession, target, null);
                    else if (!pendingMerges.Any(pair => pair.First == observerSession && pair.Second == targetSession
                        || pair.First == targetSession && pair.Second == observerSession))
                    {
                        observerSession.MembershipChangePending = targetSession.MembershipChangePending = true;
                        pendingMerges.Add((observerSession, targetSession));
                    }
                }
            }
        }

        private bool TryJoinSpotted(CombatSession session, ExplorationCharacterView view, EncounterTriggerView source)
        {
            var driver = combatHost.GetDriver(session);
            if (driver == null || session.MembershipChangePending || driver.IsActionInProgress) return false;
            var position = DungeonBattleGridFactory.ToBattleTraversalPosition(session.DungeonContext, view.DungeonPosition);
            var unit = source == null && retainedPlayers.TryGetValue(view.CharacterId, out var retained)
                ? retained : sessionFactory.CreateJoiningUnit(view.RuntimeCharacter, position, source == null);
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
                if (!DungeonCombatMembershipSystem.TryMerge(destination, source))
                { Debug.LogError("Nie można połączyć sesji na wspólnym piętrze."); pendingMerges.Remove(pair); continue; }
                foreach (var binding in bindings.ToArray())
                    if (bindingSessions[binding.Key] == source)
                    { bindingSessions[binding.Key] = destination; unitViews.Borrow(destination, binding.Key, binding.Value); }
                combatHost.End(source, true);
                combatCamera.Forget(source);
                game.TryRemoveCombat(source);
                sources.Remove(source);
                source.UnitEnteredCell = null; source.EvaluateTrapRisk = null; source.TrapRouteState = null;
                source.ResumeTurn = null;
                pendingMerges.Remove(pair);
                for (var i = 0; i < pendingMerges.Count; i++)
                {
                    var other = pendingMerges[i];
                    pendingMerges[i] = (other.First == source ? destination : other.First, other.Second == source ? destination : other.Second);
                }
                pendingMerges.RemoveAll(value => value.First == value.Second);
                SetFocus(game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId), true);
            }
            foreach (var session in game.Combats)
                session.MembershipChangePending = pendingMerges.Any(pair => pair.First == session || pair.Second == session);
        }

        private void InvalidateFovPreview() => previewRevision = -1;

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
            if (Time.unscaledTime < nextPreviewTime && previewRevision == map.PresentationRevision && previewFocus == focused) return;
            nextPreviewTime = Time.unscaledTime + .1f;
            var observers = party.ActiveMemberViews.Where(IsLiving).ToArray();
            var livingEnemies = encounters.Triggers.SelectMany(trigger => trigger.EnemyViews).Where(IsLiving).ToArray();
            var actors = observers.Concat(livingEnemies).Select(view => (view, view.DungeonPosition, view.FacingDirection, view.VisionRange, view.VisionAngle)).ToArray();
            var geometry = floor.Objects.Select(item => (item.InstanceId, item.IsActive, item is DoorDungeonObjectInstance door && door.IsOpen)).ToArray();
            if (previewActors != null && actors.SequenceEqual(previewActors) && previewObjects != null && geometry.SequenceEqual(previewObjects)
                && previewRevision == map.PresentationRevision && previewFocus == focused) return;
            previewActors = actors; previewObjects = geometry;
            bool PartySees(DungeonTraversalPosition node) => observers.Any(view => visibility.HasLineOfSight(
                floor, view.DungeonPosition, node));
            // Party visibility selects observers, not cells: show their complete threat area for sneaking.
            var enemies = livingEnemies.Where(view => PartySees(view.DungeonPosition)).ToArray();
            var cells = new HashSet<GridPosition>();
            void Add(DungeonTraversalPosition node)
            {
                if (!map.IsTraversalSelectable(node)
                    || !enemies.Any(enemy => visibility.CanDetect(floor, enemy.DungeonPosition,
                        enemy.FacingDirection, enemy.VisionRange, enemy.VisionAngle, node))) return;
                floor.Map.TryGetTraversalCell(node, out var cell);
                cells.Add(focused != null ? DungeonBattleGridFactory.ToBattleTraversalPosition(focused.DungeonContext, node)
                    : new GridPosition(cell.x, cell.y));
            }
            foreach (var surface in floor.Map.Surfaces) Add(new DungeonTraversalPosition(surface));
            foreach (var stairs in floor.Map.Stairs)
                if (floor.Map.TryGetStairPath(stairs, out var path))
                    for (var i = 1; i < path.Count - 1; i++) Add(new DungeonTraversalPosition(stairs, i));
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
