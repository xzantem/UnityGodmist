using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private readonly Dictionary<BattleUnit, ExplorationCharacterView> openingViews = new();
        private readonly Dictionary<BattleUnit, EncounterTriggerView> openingSources = new();
        private string openingLeader;
        private float nextOpeningRefresh;
        private readonly CombatSession explorationSkillContext = new();
        private BattleUnit explorationSkillActor;
        private float nextExplorationSkillRefresh;
        private SkillData explorationHoveredSkill;
        private BattleUnit explorationHoverActor;
        private DungeonTraversalPosition? explorationHoverPosition;
        private int explorationHoverFogRevision = -1, explorationHoverLevel = -1;
        private void ShowExplorationSkillHover(SkillData skill)
        {
            explorationHoveredSkill = skill;
            explorationHoverActor = null;
            targeting.PresentSkillHover(null);
            RefreshExplorationSkillHover();
        }

        private void RefreshExplorationSkillHover()
        {
            var skill = explorationHoveredSkill;
            if (skill == null) return;
            if (IsPreparingOpening || game.CurrentMode != GameModeState.Exploration)
            { explorationHoveredSkill = null; targeting.PresentSkillHover(null); return; }
            var actor = GetExplorationSkillActor();
            var view = party.ActiveMemberViews.FirstOrDefault(v => v.CharacterId == game.PlayerParty.ActiveLeaderId);
            if (actor == null || view == null || Owner(view) != null) return;
            if (explorationHoverActor == actor && explorationHoverPosition == view.DungeonPosition
                && explorationHoverFogRevision == floor.Fog.Revision && explorationHoverLevel == map.ViewedLevel) return;
            explorationHoverActor = actor; explorationHoverPosition = view.DungeonPosition;
            explorationHoverFogRevision = floor.Fog.Revision; explorationHoverLevel = map.ViewedLevel;
            var originCell = map.WorldToCell(view.Position);
            var origin = new GridPosition(originCell.x, originCell.y, map.ViewedLevel);
            var cells = new HashSet<GridPosition>();
            foreach (var authored in skill.TargetModes)
            {
                var mode = PassiveEffectCombatExecutionSystem.ResolveMode(actor, skill, authored);
                foreach (var node in floor.Fog.Visible)
                {
                    if (node.IsOnStairs) continue;
                    var surface = node.Surface;
                    var cell = new GridPosition(surface.Cell.x, surface.Cell.y, surface.Level);
                    if (mode.TargetType == TargetType.All || mode.TargetType == TargetType.Caster && cell == origin
                        || mode.TargetType != TargetType.Caster && CombatTargetingService.IsPositionWithinRange(origin, cell, mode.AbsoluteRange)) cells.Add(cell);
                }
            }
            var worldOrigin = map.CellToWorldCenter(Vector2Int.zero);
            gridFeedback.SetGridMapping(worldOrigin, Vector2.Distance(worldOrigin, map.CellToWorldCenter(Vector2Int.right)));
            gridFeedback.IsPositionVisible = p => map.IsCurrentlySeen(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(p.X,p.Y),p.Level)));
            targeting.PresentSkillHover(cells);
        }
        public bool IsPreparingOpening => combatHost != null && combatHost.IsOpeningPreview;

        // HUD-only wrapper: no grid, battle registration, turns or resource spend.
        public BattleUnit GetExplorationSkillActor()
        {
            if (!initialized || game.IsDefeated || game.CurrentMode != GameModeState.Exploration
                || game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId) != null) return null;
            ExplorationCharacterView leader = null;
            foreach (var view in party.ActiveMemberViews)
                if (view.CharacterId == game.PlayerParty.ActiveLeaderId) { leader = view; break; }
            if (!IsLiving(leader)) return null;
            if (explorationSkillActor?.Character != leader.RuntimeCharacter)
            {
                explorationSkillActor = sessionFactory.CreateJoiningUnit(leader.RuntimeCharacter, new GridPosition(0, 0));
                explorationSkillContext.Units.Clear(); explorationSkillContext.Units.Add(explorationSkillActor);
                nextExplorationSkillRefresh = 0;
            }
            if (Time.unscaledTime >= nextExplorationSkillRefresh)
            {
                nextExplorationSkillRefresh = Time.unscaledTime + .25f;
                ScoutKnifeSystem.ResetForCombat(explorationSkillContext);
                CombatTurnSystem.RefreshMaxActionPoints(explorationSkillContext, explorationSkillActor, true);
                CopyOpeningBudget(explorationSkillActor, leader);
                if (retainedPlayers.TryGetValue(leader.CharacterId, out var retained) && retained.HasInitializedCombatResources)
                { explorationSkillActor.MaxKnives = retained.MaxKnives; explorationSkillActor.AvailableKnives = retained.AvailableKnives; }
            }
            return explorationSkillActor;
        }

        public bool TrySelectOpeningSkill(SkillData skill)
        {
            var actor = GetExplorationSkillActor();
            if (actor?.Character?.GetActiveSkills()?.Contains(skill) != true) return false;
            if (!IsPreparingOpening)
            {
                var leader = party.ActiveMemberViews.First(v => v.CharacterId == actor.Character.CharacterId);
                // This seed only supplies the floor context. The actual skill's
                // targets choose combat membership when the action is committed.
                var seed = encounters.Triggers.SelectMany(t => t.EnemyViews)
                    .Where(v => IsLiving(v) && !v.IsCrossingLowWall)
                    .OrderBy(v => (v.Position - leader.Position).sqrMagnitude).FirstOrDefault();
                if (!TryPrepareOpening(seed, false)) return false;
            }
            targeting.BeginTargetSelection(skill);
            return true;
        }

        public bool CanPrepareOpening(ExplorationCharacterView enemy) => initialized && !game.IsDefeated
            && game.CurrentMode == GameModeState.Exploration
            && !IsPreparingOpening && game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId) == null
            && IsLiving(enemy) && !string.IsNullOrEmpty(enemy.EnemyInstanceId)
            && !enemy.IsCrossingLowWall && enemy.IsVisibleForInspection;

        public bool TryPrepareOpening(ExplorationCharacterView enemy) => TryPrepareOpening(enemy, true);

        private bool TryPrepareOpening(ExplorationCharacterView enemy, bool requireVisible)
        {
            if (requireVisible ? !CanPrepareOpening(enemy) : GetExplorationSkillActor() == null
                || IsPreparingOpening || enemy != null && (!IsLiving(enemy) || enemy.IsCrossingLowWall)) return false;
            var actorView = party.ActiveMemberViews.FirstOrDefault(v => v.CharacterId == game.PlayerParty.ActiveLeaderId);
            var trigger = enemy == null ? null : encounters.Triggers.FirstOrDefault(t => t.EnemyViews.Contains(enemy));
            var encounter = trigger?.Encounter ?? floor.Encounters.FirstOrDefault();
            if (!IsLiving(actorView) || actorView.IsCrossingLowWall || encounter == null
                || !contextFactory.TryCreate(game.ActiveDungeon, encounter, out var context, true)) return false;
            var playerCells = party.ActiveMemberViews.Where(v => IsLiving(v) && !v.IsCrossingLowWall && Owner(v) == null)
                .ToDictionary(v => v.CharacterId, v => map.WorldToCell(v.Position));
            var enemyCells = new Dictionary<string, Vector2Int>();
            if (enemy != null) enemyCells.Add(enemy.EnemyInstanceId, map.WorldToCell(enemy.Position));
            if (!sessionFactory.TryCreateSeamless(game, encounter, context, playerCells, enemyCells,
                out var preview, out _, true, allowEmptyEnemies: enemy == null)) return false;
            openingViews.Clear(); openingSources.Clear(); openingCombatExtras.Clear();
            foreach (var unit in preview.Units)
            {
                var view = unit.IsPlayerSide ? party.ActiveMemberViews.First(v => v.CharacterId == unit.Character.CharacterId) : enemy;
                unit.Character = view.RuntimeCharacter;
                openingViews.Add(unit, view);
                if (!unit.IsPlayerSide) openingSources.Add(unit, trigger);
            }
            foreach (var view in party.ActiveMemberViews)
            {
                if (view.RuntimeCharacter?.LifeState == CharacterLifeState.Dead || view.IsCrossingLowWall
                    || openingViews.Values.Contains(view)) continue;
                var unit = sessionFactory.CreateJoiningUnit(view.RuntimeCharacter,
                    DungeonBattleGridFactory.ToBattleTraversalPosition(context, view.DungeonPosition));
                preview.Units.Add(unit); openingViews.Add(unit, view);
            }
            foreach (var source in encounters.Triggers)
            foreach (var view in source.EnemyViews)
            {
                if (view == enemy || !IsLiving(view) || view.IsCrossingLowWall) continue;
                var unit = sessionFactory.CreateJoiningUnit(view.RuntimeCharacter,
                    DungeonBattleGridFactory.ToBattleTraversalPosition(context, view.DungeonPosition), false);
                preview.Units.Add(unit); openingViews.Add(unit, view); openingSources.Add(unit, source);
            }
            // Preparation owns temporary battle participants, never the retained combat state.
            ScoutKnifeSystem.ResetForCombat(new CombatSession { Units = preview.Units.Where(u => u.IsPlayerSide).ToList() });
            foreach (var unit in preview.Units.Where(u => u.IsPlayerSide)) CombatTurnSystem.RefreshMaxActionPoints(preview, unit, true);
            foreach (var unit in preview.Units.Where(u => u.IsPlayerSide))
                if (retainedPlayers.TryGetValue(unit.Character.CharacterId, out var retained) && retained.HasInitializedCombatResources)
                { unit.MaxKnives = retained.MaxKnives; unit.AvailableKnives = retained.AvailableKnives; }
            foreach (var pair in openingViews) CopyOpeningBudget(pair.Key, pair.Value);
            AddOpeningCombatExtras(preview);
            preview.ActiveUnit = preview.Units.First(u => u.IsPlayerSide && u.Character.CharacterId == actorView.CharacterId);
            if (!combatHost.BeginOpeningPreview(preview, CommitOpeningSkill,
                unit => openingViews.TryGetValue(unit, out var view) ? view.IsVisibleForInspection
                    : openingCombatExtras.TryGetValue(unit,out var actual) && unitViews.IsUnitVisible(actual))) return false;
            openingLeader = actorView.CharacterId;
            party.CancelMovement(); party.CloseInventoryForCombat();
            combatHudRoot.SetActive(true); targeting.enabled = true; movementInput.enabled = false;
            ConfigureOpeningMapping(preview);
            return true;
        }

        private void ConfigureOpeningMapping(CombatSession preview)
        {
            var context = preview.DungeonContext;
            var origin = map.CellToWorldCenter(context.WorldBounds.min);
            var size = Vector2.Distance(origin, map.CellToWorldCenter(context.WorldBounds.min + Vector2Int.right));
            GridPosition? Pointer(Vector3 world) => map.TryGetVisibleTraversal(world, out var p)
                && map.IsTraversalSelectable(p) && map.IsCurrentlySeen(p)
                ? DungeonBattleGridFactory.ToBattleTraversalPosition(context, p) : null;
            targeting.ResolvePointerPosition = Pointer; inspectInput.ResolvePointerPosition = Pointer;
            targeting.SetGridMapping(origin, size); inspectInput.SetGridMapping(origin, size);
            gridFeedback.SetGridMapping(origin, size);
            gridFeedback.ResolveExplorationMarker = p => {
                foreach(var unit in preview.Units)
                    if(unit.Position==p)return openingViews.TryGetValue(unit,out var actorView)?(Vector2?)actorView.Position:null;
                return null;
            };
            System.Func<BattleUnit,Vector2?> feet = unit => openingViews.TryGetValue(unit,out var view)
                && view.IsVisibleForInspection && map.IsTraversalSelectable(view.DungeonPosition) ? (Vector2?)view.Position : null;
            targeting.ResolvePointerUnit = world => CombatTargetSelectionController.FindUnitAtFeet(preview.Units,feet,world,size);
            gridFeedback.IsPositionVisible = p => map.IsTraversalSelectable(
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context, p)) && map.IsCurrentlySeen(
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context, p));
            inspectInput.IsPositionVisible = gridFeedback.IsPositionVisible;
        }

        private bool SyncOpeningPositions()
        {
            var session = combatHost.OpeningPreview?.Session;
            if (session == null || openingLeader != game.PlayerParty.ActiveLeaderId) return false;
            foreach (var pair in openingViews.ToArray())
            {
                if (pair.Value.RuntimeCharacter?.LifeState == CharacterLifeState.Dead || pair.Value.IsCrossingLowWall)
                {
                    if (pair.Key == session.ActiveUnit) return false;
                    session.Units.Remove(pair.Key); openingViews.Remove(pair.Key); openingSources.Remove(pair.Key);
                    continue;
                }
                pair.Key.Position = DungeonBattleGridFactory.ToBattleTraversalPosition(session.DungeonContext, pair.Value.DungeonPosition);
                CopyOpeningBudget(pair.Key, pair.Value);
            }
            foreach(var pair in openingCombatExtras.ToArray())
            {
                if(pair.Value.Character?.LifeState == CharacterLifeState.Dead || OpeningOwner(pair.Key)==null)
                {session.Units.Remove(pair.Key);openingCombatExtras.Remove(pair.Key);continue;}
                var owner=OpeningOwner(pair.Key);
                pair.Key.Position=DungeonBattleGridFactory.ToBattleTraversalPosition(session.DungeonContext,
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(owner.DungeonContext,pair.Value.Position));
                CopyBudgetFromActual(pair.Key,pair.Value);
            }
            return true;
        }

        private void UpdateOpeningPreparation()
        {
            if (!IsPreparingOpening) return;
            if (game.CurrentMode != GameModeState.Exploration)
            {
                CancelOpeningPreparation(false);
                targeting.enabled = movementInput.enabled = false;
                return;
            }
            if (queuedEntry != null)
            {
                if (openingLeader != game.PlayerParty.ActiveLeaderId) { CancelOpeningPreparation(); return; }
                if (Time.unscaledTime < nextOpeningRefresh) return;
                nextOpeningRefresh = Time.unscaledTime + .1f;
                var queued = queuedEntry;
                if (CommitOpeningSkill(queued.Skill, queued.Positions, queued.Teleport, queued.Placement, queued.Rotations, queued.Chosen)) return;
                queuedEntry = null;
                CancelOpeningPreparation();
                return;
            }
            if (!targeting.IsSelectingTarget || openingLeader != game.PlayerParty.ActiveLeaderId)
            { CancelOpeningPreparation(); return; }
            if (Time.unscaledTime < nextOpeningRefresh) return;
            nextOpeningRefresh = Time.unscaledTime + .1f;
            if (!SyncOpeningPositions()) { CancelOpeningPreparation(); return; }
            targeting.RefreshOpeningHighlights();
        }

        private void CancelOpeningPreparation(bool restoreFocus = true)
        {
            if (combatHost == null || !combatHost.IsOpeningPreview) return;
            if (targeting?.ConsumedPrimaryClickThisFrame == true) party.ClaimWorldPrimaryClick();
            queuedEntry = null;
            gridFeedback.ResolveExplorationMarker=null; targeting.ResolvePointerUnit=null;
            combatHost.CancelOpeningPreview(); openingViews.Clear(); openingSources.Clear(); openingCombatExtras.Clear();
            targeting?.CancelTargetSelection();
            if (restoreFocus) SetFocus(focused, true);
        }

        private bool ResolveOpeningPlacement(CombatExecutionDriver preview, BattleUnit actor, BattleUnit anchor,
            HashSet<BattleUnit> included, System.Func<BattleUnit, IEnumerable<BattleUnit>> room,
            System.Func<BattleUnit, BattleUnit, bool> observe, SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> originalPicks,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> originalChosen,
            GridPosition? teleport, GridPosition? placement, IReadOnlyDictionary<int, int> rotations,
            Dictionary<int, IReadOnlyList<BattleUnit>> originalTargets,
            out Dictionary<int, GridPosition?> picked,
            out Dictionary<int, IReadOnlyList<GridPosition>> chosen)
        {
            var session = preview.Session;
            var original = session.Units.ToDictionary(u => u, u => u.Position);
            picked = null; chosen = null;
            void Restore() { foreach (var pair in original) pair.Key.Position = pair.Value; }
            // Membership only grows. Each retry adds at least one participant, so
            // this is bounded by the available actors, never a per-frame search.
            for (var attempt = 0; attempt <= session.Units.Count; attempt++)
            {
                Restore();
                var ordered = session.Units.Where(included.Contains).ToArray();
                var occupied = new HashSet<GridPosition>(session.Units.Where(u => !included.Contains(u)).Select(u => u.Position));
                DungeonCombatParticipationSystem.AddExternalReservations(session, occupied);
                if (!DungeonCombatPositionResolver.TryResolveGroup(session.Grid, ordered.Select(u => u.Position).ToList(), occupied, out var resolved)) break;
                for (var n = 0; n < ordered.Length; n++) ordered[n].Position = resolved[n];
                GridPosition Remap(int mode, GridPosition cell)
                {
                    if (!originalTargets.TryGetValue(mode, out var units)) return cell;
                    var unit = units.FirstOrDefault(u => original[u] == cell);
                    return unit?.Position ?? cell;
                }
                picked = originalPicks.ToDictionary(p => p.Key, p => p.Value.HasValue
                    && p.Key < skill.TargetModes.Count
                    && (skill.TargetModes[p.Key].TargetType == TargetType.Single || skill.TargetModes[p.Key].TargetType == TargetType.Chain
                        || skill.TargetModes[p.Key].TargetType == TargetType.MultipleChosen)
                    ? (GridPosition?)Remap(p.Key, p.Value.Value) : p.Value);
                chosen = originalChosen?.ToDictionary(p => p.Key, p => (IReadOnlyList<GridPosition>)p.Value.Select(cell => Remap(p.Key, cell)).ToArray());
                if (!preview.ValidateOpeningSkill(skill, picked, teleport, placement, rotations, chosen, out var finalTargets)) break;
                var finalGroup = DungeonCombatOpeningSystem.CollectParticipants(session, actor, finalTargets.Values.SelectMany(v => v), anchor, room, observe);
                if (finalGroup.All(included.Contains)) return true;
                included.UnionWith(finalGroup);
            }
            Restore(); return false;
        }

        private bool CommitOpeningSkill(SkillData skill, IReadOnlyDictionary<int, GridPosition?> positions,
            GridPosition? teleport, GridPosition? placement, IReadOnlyDictionary<int, int> rotations,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosen)
        {
            var preview = combatHost.OpeningPreview;
            if (preview == null || !SyncOpeningPositions()) return false;
            // Equipment/status changes while choosing a spell must use current standard AP.
            if (!preview.Session.ActiveUnit.HasUnfinishedCombatTurn)
                CombatTurnSystem.RefreshMaxActionPoints(preview.Session, preview.Session.ActiveUnit, true);
            if (!preview.ValidateOpeningSkill(skill, positions, teleport, placement, rotations, chosen, out var targets))
            { ShowPlacementNotice("Nie można użyć umiejętności: sprawdź cel, zasięg, widoczność i koszty."); return false; }
            var session = preview.Session;
            var actor = session.ActiveUnit;
            if (TryCommitOutsideAction(preview, skill, targets, positions, teleport, placement, rotations, chosen, out var handled))
                return true;
            if (handled) return false;
            var anchor = targets.Values.SelectMany(group => group).FirstOrDefault(u => !u.IsPlayerSide && openingSources.ContainsKey(u))
                ?? session.Units.Where(u => !u.IsPlayerSide && openingViews.ContainsKey(u)
                    && Owner(openingViews[u]) == null && openingViews[u].IsVisibleForInspection)
                    .OrderBy(u => (openingViews[u].Position - openingViews[actor].Position).sqrMagnitude).FirstOrDefault();
            if (anchor == null)
            { ShowPlacementNotice("Brak przeciwnika do rozpoczęcia walki."); return false; }
            var source = openingSources[anchor];
            if (session.DungeonContext.EncounterInstanceId != source.Encounter.InstanceId)
            {
                if (!contextFactory.TryCreate(game.ActiveDungeon, source.Encounter, out var actualContext, true)) return false;
                session.DungeonContext = actualContext;
            }
            IEnumerable<BattleUnit> Room(BattleUnit enemy) => RoomAlertCandidates(openingViews[enemy])
                .Select(pair => openingViews.FirstOrDefault(v => v.Value == pair.Enemy).Key).Where(u => u != null);
            bool Observe(BattleUnit enemy, BattleUnit ally) => visibility.CanObserveCombat(floor,
                openingViews[enemy].DungeonPosition, openingViews[enemy].VisionRange, openingViews[ally].DungeonPosition);
            var included = DungeonCombatOpeningSystem.CollectParticipants(session, actor,
                targets.Values.SelectMany(v => v), anchor, Room, Observe);
            var picked = new Dictionary<int, GridPosition?>(positions);
            var rotated = rotations == null ? null : rotations.ToDictionary(p => p.Key, p => p.Value);
            var multiple = chosen == null ? null : chosen.ToDictionary(p => p.Key, p => (IReadOnlyList<GridPosition>)p.Value.ToArray());
            // Followers may share an exploration cell. Use the normal placement owner
            // and revalidate the final action before registering or spending anything.
            if (!ResolveOpeningPlacement(preview, actor, anchor, included, Room, Observe, skill,
                positions, chosen, teleport, placement, rotations, targets, out picked, out multiple))
            { ShowPlacementNotice("Brak miejsca na poprawne rozpoczęcie walki wybraną umiejętnością."); return false; }
            var detection = included.Where(u => !u.IsPlayerSide).ToDictionary(u => u, u =>
                floor.GetEnemyAwareness(DungeonCombatMembershipSystem.EnemyKey(openingSources[u].Encounter.InstanceId,
                    openingViews[u].EnemyInstanceId)).Progress);
            var nextBindings = openingViews.Where(pair => included.Contains(pair.Key)).ToDictionary(p => p.Key, p => p.Value);
            var originalPositions = new Dictionary<BattleUnit, GridPosition>();
            session.Units = included.OrderBy(u => session.Units.IndexOf(u)).ToList();
            session.EnemySources.Clear(); session.Rewards = new CombatRewardResult();
            foreach (var enemy in session.Units.Where(u => !u.IsPlayerSide))
            {
                var trigger = openingSources[enemy]; var view = openingViews[enemy];
                game.ActiveDungeon.TryGetPreRolledRewards(trigger.Encounter.InstanceId, out var loot);
                DungeonCombatMembershipSystem.RegisterEnemy(session, trigger.Encounter.InstanceId, view.EnemyInstanceId, loot, enemy);
                enemy.IsBoss = ScoutCombatSystem.IsBoss(game.ActiveDungeon, trigger.Encounter.Definition, view.EnemyInstanceId);
            }
            for (var i = 0; i < session.Units.Count; i++)
            {
                var unit = session.Units[i];
                if (!unit.IsPlayerSide || !retainedPlayers.TryGetValue(unit.Character.CharacterId, out var retained)) continue;
                originalPositions[retained] = retained.Position; retained.Position = unit.Position;
                var view = nextBindings[unit]; nextBindings.Remove(unit); nextBindings.Add(retained, view);
                session.Units[i] = retained;
                if (unit == actor) actor = retained;
            }
            gridFeedback.ResolveExplorationMarker=null; targeting.ResolvePointerUnit=null;
            combatHost.CancelOpeningPreview(); openingViews.Clear(); openingSources.Clear(); openingCombatExtras.Clear();
            if (!ActivateBoundSession(session, nextBindings, source, driver => driver.StartTurns(actor, detection)))
            {
                foreach (var original in originalPositions) original.Key.Position = original.Value;
                game.TryRemoveCombat(session); SetFocus(focused, true); return false;
            }
            foreach (var trigger in nextBindings.Where(p => !p.Key.IsPlayerSide)
                .Select(p => encounters.Triggers.First(t => t.EnemyViews.Contains(p.Value))).Distinct()) trigger.MarkTriggered();
            party.SeparateForCombat(); RefreshFocus();
            // Ordinary execution owns costs, hit rolls, status effects, presentation and rewards.
            var executed = combatHost.GetDriver(session).TryExecuteSkill(skill, picked, teleport, placement, rotated, multiple);
            foreach (var enemy in nextBindings.Where(p => !p.Key.IsPlayerSide)) QueueRoomAlert(enemy.Value, nextBindings[actor]);
            if (!executed) ShowPlacementNotice("Warunki zmieniły się na początku tury. Wybierz akcję w rozpoczętej walce.");
            return true;
        }
    }
}
