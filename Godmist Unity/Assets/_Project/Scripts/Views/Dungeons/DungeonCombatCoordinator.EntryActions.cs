using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private sealed class QueuedEntry
        {
            public SkillData Skill;
            public Dictionary<int, GridPosition?> Positions;
            public GridPosition? Teleport, Placement;
            public Dictionary<int, int> Rotations;
            public Dictionary<int, IReadOnlyList<GridPosition>> Chosen;
            public HashSet<string> TargetIds;
        }
        private QueuedEntry queuedEntry;
        // Combat summons have no borrowed ExplorationCharacterView.
        private readonly Dictionary<BattleUnit,BattleUnit> openingCombatExtras = new();
        private CombatSession OpeningOwner(BattleUnit unit) => openingCombatExtras.TryGetValue(unit,out var actual)
            ? game.Combats.FirstOrDefault(combat => combat.Units.Contains(actual)) : Owner(openingViews[unit]);
        private Godmist.Runtime.Dungeons.DungeonTraversalPosition OpeningPosition(BattleUnit unit)
            => openingCombatExtras.ContainsKey(unit)
                ? DungeonBattleGridFactory.ToDungeonTraversalPosition(combatHost.OpeningPreview.Session.DungeonContext,unit.Position)
                : openingViews[unit].DungeonPosition;
        private float OpeningVision(BattleUnit unit) => openingCombatExtras.TryGetValue(unit,out var actual)
            ? ResolveCombatVisionRange(OpeningOwner(unit),actual) ?? 10f : openingViews[unit].VisionRange;

        private void AddOpeningCombatExtras(CombatSession preview)
        {
            foreach(var combat in game.Combats)
            {
                if(combat.DungeonContext?.FloorIndex != preview.DungeonContext.FloorIndex) continue;
                foreach(var actual in combat.Units)
                {
                    if(actual.Character?.LifeState == CharacterLifeState.Dead
                        || preview.Units.Any(u=>u.Character==actual.Character)) continue;
                    var position=DungeonBattleGridFactory.ToBattleTraversalPosition(preview.DungeonContext,
                        DungeonBattleGridFactory.ToDungeonTraversalPosition(combat.DungeonContext,actual.Position));
                    var wrapper=sessionFactory.CreateJoiningUnit(actual.Character,position,actual.IsPlayerSide);
                    wrapper.IsAiControlledSummon=actual.IsAiControlledSummon;wrapper.IsStationarySummon=actual.IsStationarySummon;
                    CopyBudgetFromActual(wrapper,actual);preview.Units.Add(wrapper);openingCombatExtras.Add(wrapper,actual);
                }
            }
        }

        private void CopyOpeningBudget(BattleUnit wrapper, ExplorationCharacterView view)
        {
            var actual = Owner(view)?.Units.FirstOrDefault(u => u.Character == view.RuntimeCharacter);
            if (actual == null) retainedPlayers.TryGetValue(view.CharacterId, out actual);
            if (actual == null) return;
            CopyBudgetFromActual(wrapper,actual,Owner(view)!=null);
        }
        private static void CopyBudgetFromActual(BattleUnit wrapper,BattleUnit actual,bool inCombat=true)
        {
            wrapper.HasUnfinishedCombatTurn = actual.HasUnfinishedCombatTurn;
            if (actual.HasUnfinishedCombatTurn || inCombat)
            { wrapper.CurrentActionPoints = actual.CurrentActionPoints; wrapper.MaxActionPoints = actual.MaxActionPoints; }
            if (actual.HasInitializedCombatResources)
            {
                wrapper.HasInitializedCombatResources = true;
                wrapper.MaxKnives = actual.MaxKnives; wrapper.AvailableKnives = actual.AvailableKnives;
            }
            wrapper.ActiveToggleSkillIds.Clear(); wrapper.ActiveToggleSkillIds.UnionWith(actual.ActiveToggleSkillIds);
            wrapper.ToggleSkillIdsUsedThisTurn.Clear(); wrapper.ToggleSkillIdsUsedThisTurn.UnionWith(actual.ToggleSkillIdsUsedThisTurn);
            wrapper.ConsumableUseTypesUsedThisTurn.Clear(); wrapper.ConsumableUseTypesUsedThisTurn.UnionWith(actual.ConsumableUseTypesUsedThisTurn);
        }

        private bool TryCommitOutsideAction(CombatExecutionDriver preview, SkillData skill,
            Dictionary<int, IReadOnlyList<BattleUnit>> targets,
            IReadOnlyDictionary<int, GridPosition?> positions, GridPosition? teleport, GridPosition? placement,
            IReadOnlyDictionary<int, int> rotations, IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosen,
            out bool handled)
        {
            var direct = targets.Values.SelectMany(group => group).Distinct().ToArray();
            var actor = preview.Session.ActiveUnit;
            IEnumerable<BattleUnit> Room(BattleUnit enemy) => openingCombatExtras.ContainsKey(enemy) ? System.Array.Empty<BattleUnit>()
                : RoomAlertCandidates(openingViews[enemy]).Select(pair => openingViews.FirstOrDefault(v => v.Value == pair.Enemy).Key).Where(u => u != null);
            bool Observe(BattleUnit enemy, BattleUnit ally) => visibility.CanObserveCombat(floor,
                OpeningPosition(enemy),OpeningVision(enemy),OpeningPosition(ally));
            var included = DungeonCombatOpeningSystem.CollectParticipants(preview.Session, actor, direct, null, Room, Observe);
            included.UnionWith(direct);
            var owners = included.Select(OpeningOwner).Where(s => s != null).Distinct().ToList();
            handled = owners.Count > 0;
            if (!handled)
            {
                // Positive exploration casts have no enemy anchor and never register a battle.
                // World entities and movement still require a persistent arena owner.
                if (skill.Effects.Any(effect => effect.EffectType is EffectType.CreateObject or EffectType.Summon
                    or EffectType.Teleport or EffectType.Charge or EffectType.SwapPositions || effect.DelayCasterTurns > 0)) return false;
                // Several authored heals carry the legacy default Negative polarity.
                // Hostile intent belongs to the targeting modes, not that metadata.
                if (direct.Any(u => !u.IsPlayerSide) || skill.TargetModes.Where((mode,index) => mode.TargetType != TargetType.Caster
                    && (mode.TargetingType == TargetingType.Enemies || mode.TargetingType == TargetingType.All
                        && skill.Effects.Any(e => e.TargetModeIndex == index && e.Polarity == EffectPolarity.Negative))).Any()) return false;
                handled = true;
                return ExecuteExplorationSupport(preview, actor, skill, targets, positions, teleport, placement, rotations);
            }
            var targetIds = direct.Select(u => u.Character.CharacterId).ToHashSet();
            if (queuedEntry != null && !queuedEntry.TargetIds.SetEquals(targetIds))
            { ShowPlacementNotice("Cel zmienił pozycję. Wybierz akcję ponownie."); return false; }

            var destination = owners[0];
            // Preflight the complete group before performing any merge or charging anything.
            if (owners.Any(s => !game.Combats.Contains(s) || s.Outcome != CombatOutcomeState.Ongoing)
                || owners.Skip(1).Any(s => !DungeonCombatMembershipSystem.CanMerge(destination, s))
                || owners.SelectMany(s => s.Units).GroupBy(u => u.Character.CharacterId).Any(g => g.Count() != 1)) return false;
            if (owners.Any(s => combatHost.GetDriver(s)?.IsActionInProgress != false || s.MembershipChangePending))
            {
                queuedEntry ??= new QueuedEntry { Skill = skill, Positions = new(positions), Teleport = teleport,
                    Placement = placement, Rotations = rotations?.ToDictionary(p => p.Key,p => p.Value),
                    Chosen = chosen?.ToDictionary(p => p.Key,p => (IReadOnlyList<GridPosition>)p.Value.ToArray()), TargetIds = targetIds };
                return true;
            }
            GridPosition? Remap(GridPosition? cell) => cell.HasValue
                ? DungeonBattleGridFactory.ToBattleTraversalPosition(destination.DungeonContext,
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(preview.Session.DungeonContext,cell.Value)) : null;
            var arrivals=included.Where(u=>OpeningOwner(u)==null).ToArray();
            var occupied=owners.SelectMany(combat=>combat.Units).Select(u=>u.Position).ToHashSet();
            DungeonCombatParticipationSystem.AddExternalReservations(destination,occupied);
            var preferred=arrivals.Select(u=>Remap(u.Position).Value).ToList();
            if(!DungeonCombatPositionResolver.TryResolveGroup(destination.Grid,preferred,occupied,out var resolved))
            { ShowPlacementNotice("Brak miejsca na dołączenie do walki."); return false; }
            var arrivalsAt=arrivals.Select((u,index)=>(u,index)).ToDictionary(p=>p.u,p=>resolved[p.index]);
            var actualPositions=positions.ToDictionary(p=>p.Key,p=>Remap(p.Value));
            var actualTeleport=Remap(teleport);var actualPlacement=Remap(placement);
            queuedEntry = null;
            foreach (var source in owners.Skip(1)) if (!MergeCombatSessions(destination, source)) return false;
            var driver = combatHost.GetDriver(destination);
            var actuals = new Dictionary<BattleUnit, BattleUnit>();
            var admittedViews = new Dictionary<BattleUnit, ExplorationCharacterView>();
            foreach (var unit in included)
            {
                if(openingCombatExtras.TryGetValue(unit,out var extra)) {actuals[unit]=extra;continue;}
                var view = openingViews[unit];
                var actual = destination.Units.FirstOrDefault(u => u.Character == view.RuntimeCharacter);
                if (actual == null)
                {
                    if (!unit.IsPlayerSide || !retainedPlayers.TryGetValue(view.CharacterId, out actual))
                        actual = sessionFactory.CreateJoiningUnit(view.RuntimeCharacter, arrivalsAt[unit], unit.IsPlayerSide);
                    if (!driver.TryJoin(actual, arrivalsAt[unit], beginIfIdle: false, allowDowned: unit != actor && actual.IsPlayerSide))
                    { ShowPlacementNotice("Brak miejsca na dołączenie do walki."); return false; }
                    if (actual.IsPlayerSide) retainedPlayers[view.CharacterId] = actual;
                    else
                    {
                        var source = openingSources[unit];
                        game.ActiveDungeon.TryGetPreRolledRewards(source.Encounter.InstanceId, out var loot);
                        DungeonCombatMembershipSystem.RegisterEnemy(destination, source.Encounter.InstanceId, view.EnemyInstanceId, loot, actual);
                        actual.IsBoss = ScoutCombatSystem.IsBoss(game.ActiveDungeon, source.Encounter.Definition, view.EnemyInstanceId);
                        source.MarkTriggered();
                    }
                    bindings[actual] = view; bindingSessions[actual] = destination;
                    unitViews.Borrow(destination, actual, view);
                }
                actuals[unit] = actual; admittedViews[actual] = view;
            }
            var actualActor = actuals[actor];
            var actualTargets = targets.ToDictionary(p => p.Key, p => (IReadOnlyList<BattleUnit>)p.Value.Select(u => actuals[u]).ToArray());
            combatHost.CancelOpeningPreview(); openingViews.Clear(); openingSources.Clear(); openingCombatExtras.Clear();
            party.SeparateForCombat(); party.CancelMovement(); party.RefreshControlState();
            SetFocus(destination, true);
            var executed = driver.TryExecuteEntrySkill(actualActor, skill, actualTargets, actualPositions, actualTeleport, actualPlacement, rotations);
            foreach (var enemy in admittedViews.Where(p => !p.Key.IsPlayerSide)) QueueRoomAlert(enemy.Value, admittedViews[actualActor]);
            if (!executed) ShowPlacementNotice("Postać dołączyła do walki. Warunki nie pozwalają teraz wykonać akcji.");
            return true;
        }

        private bool ExecuteExplorationSupport(CombatExecutionDriver preview, BattleUnit actor, SkillData skill,
            Dictionary<int, IReadOnlyList<BattleUnit>> targets, IReadOnlyDictionary<int, GridPosition?> positions,
            GridPosition? teleport, GridPosition? placement, IReadOnlyDictionary<int, int> rotations)
        {
            var view = openingViews[actor];
            var context = preview.Session;
            if (retainedPlayers.TryGetValue(view.CharacterId, out var retained))
            {
                context.Units.Remove(actor); context.Units.Add(retained); retained.Position = actor.Position;
                foreach (var key in targets.Keys.ToArray()) targets[key] = targets[key].Select(u => u == actor ? retained : u).ToArray();
                actor = retained; context.ActiveUnit = actor;
                if (!actor.HasUnfinishedCombatTurn) CombatTurnSystem.RefreshMaxActionPoints(context, actor, true);
            }
            var bus = combatHost.DungeonFeedback;
            var damage = new CombatDamageSystem(feedbackBus: bus);
            var recovery = new CombatRecoverySystem(feedbackBus: bus);
            var status = new StatusEffectSystem(damageSystem: damage, feedbackBus: bus);
            var execution = new SkillExecutionSystem(statusEffectSystem: status, damageSystem: damage,
                recoverySystem: recovery, feedbackBus: bus);
            // The targeting-only preview does not subscribe to action feedback.
            // Route only this execution through its existing pose/VFX presenter.
            bus.Published += preview.PresentExplorationSupportFeedback;
            bool executed;
            try {executed=execution.TryExecute(context,actor,skill,targets,teleport,placement,positions,rotationsByModeIndex:rotations);}
            finally {bus.Published -= preview.PresentExplorationSupportFeedback;}
            if (!executed) return false;
            retainedPlayers[view.CharacterId] = actor;
            CancelOpeningPreparation();
            return true;
        }
    }
}
