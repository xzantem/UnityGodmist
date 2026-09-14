using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    // Scene owner: map and character GameObjects are borrowed, never rebuilt on mode changes.
    public sealed class DungeonCombatCoordinator : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap combatHost;
        [SerializeField] private CombatUnitViewSpawner unitViews;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatMovementInputController movementInput;
        [SerializeField] private CombatUnitInspectController inspectInput;
        [SerializeField] private CombatGridFeedbackRenderer gridFeedback;
        [SerializeField] private ExplorationCameraFollow explorationCamera;
        [SerializeField] private CombatCameraFramer combatCamera;
        [SerializeField] private GameObject combatHudRoot;
        [SerializeField] private GameObject explorationHudRoot;
        [SerializeField] private CanvasGroup placementNotice;
        [SerializeField] private TMPro.TMP_Text placementNoticeText;
        private float placementNoticeUntil;
        private GameSession game;
        private DungeonFloorInstance floor;
        private DungeonFloorMapView map;
        private ExplorationPartyController party;
        private DungeonObjectSpawner objects;
        private DungeonEncounterSpawner encounters;
        private readonly Dictionary<CombatSession, EncounterTriggerView> sources = new();
        private readonly Dictionary<BattleUnit, ExplorationCharacterView> bindings = new();
        private readonly Dictionary<string, BattleUnit> retainedPlayers = new();
        private readonly Dictionary<BattleUnit, CombatSession> bindingSessions = new();
        private readonly Dictionary<string, Vector2> previousExplorationPositions = new();
        private readonly DungeonCombatContextFactory contextFactory = new();
        private readonly CombatSessionFactory sessionFactory = new();
        private readonly DungeonCombatOutcomeSystem outcomes = new();
        private readonly DungeonCombatStateSyncSystem stateSync = new();
        private readonly CombatRewardSystem rewards = new();
        private readonly CombatOutcomeSystem outcomeEvaluator = new();
        private DungeonTrapSystem trapSystem;
        private readonly Dictionary<string, BattleUnit> exploringEnemies = new();
        private readonly Dictionary<string, Vector2Int> trapPreviousCells = new();
        private bool initialized;
        private CombatSession focused;

        public bool Initialize(GameSession session, DungeonFloorInstance dungeonFloor,
            DungeonFloorMapView floorView, ExplorationPartyController controller,
            DungeonEncounterSpawner encounterSpawner, DungeonObjectSpawner objectSpawner)
        {
            if (combatHost == null || !combatHost.SeamlessDungeon || unitViews == null
                || targeting == null || movementInput == null || inspectInput == null
                || gridFeedback == null || explorationCamera == null || combatCamera == null
                || combatHudRoot == null || explorationHudRoot == null)
            { Debug.LogError("DungeonCombatCoordinator: niekompletny wiring seamless."); return false; }
            game = session; floor = dungeonFloor; map = floorView; party = controller;
            objects = objectSpawner; encounters = encounterSpawner;
            unitViews.ConfigureDungeon(game, map);
            unitViews.ExplorationWorldPosition = ResolveExplorationFeedbackPosition;
            combatHost.BindDungeonFeedback();
            foreach (var view in party.ActiveMemberViews.Concat(encounters.Triggers.SelectMany(trigger => trigger.EnemyViews)))
            {
                view.ConfigureTerrain(floor.Map, map);
                view.CanCrossTo = cell => !floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                    .Any(obstacle => obstacle.IsActive && obstacle.BlocksMovement && obstacle.Position == cell);
            }
            party.SetCombatCoordinator(this);
            foreach (var trigger in encounters.Triggers) trigger.SetCombatCoordinator(this);
            combatHost.CombatMapObjectChanged += HandleMapObjectChanged;
            foreach (var view in party.ActiveMemberViews) previousExplorationPositions[view.CharacterId] = view.Position;
            trapSystem = new DungeonTrapSystem(floor, game.ActiveDungeon.DungeonLevel,
                session => session == null ? combatHost.DungeonFeedback : combatHost.GetDriver(session)?.FeedbackBus);
            initialized = true;
            SetFocus(null, true);
            return true;
        }

        public bool IsRoomFighting(string roomId) => game != null
            && game.Combats.Any(value => value.DungeonContext?.EncounterRoomId == roomId);

        public bool TryStartEncounter(EncounterTriggerView trigger)
        {
            if (!initialized || trigger == null || party.ActiveMemberViews.Any(view => view.IsCrossingLowWall)
                || trigger.EnemyViews.Any(view => view.IsCrossingLowWall) || IsRoomFighting(trigger.Encounter.RoomId)
                || !contextFactory.TryCreate(game.ActiveDungeon, trigger.Encounter, out var context)) return false;
            var playerCells = party.ActiveMemberViews.ToDictionary(view => view.CharacterId, view => map.WorldToCell(view.transform.position));
            var enemyCells = trigger.EnemyViews.ToDictionary(view => view.EnemyInstanceId, view => map.WorldToCell(view.transform.position));
            if (!sessionFactory.TryCreateSeamless(game, trigger.Encounter, context, playerCells, enemyCells,
                out var session, out var placementFailed))
            {
                if (placementFailed) ShowPlacementNotice("Brak miejsca na rozpoczęcie walki");
                return false;
            }
            var nextBindings = new Dictionary<BattleUnit, ExplorationCharacterView>();
            var originalPositions = new Dictionary<BattleUnit, GridPosition>();
            bool RejectStart()
            {
                foreach (var original in originalPositions) original.Key.Position = original.Value;
                game.TryRemoveCombat(session);
                return false;
            }
            for (var index = 0; index < session.Units.Count; index++)
            {
                var unit = session.Units[index];
                var view = unit.IsPlayerSide
                    ? party.ActiveMemberViews.FirstOrDefault(value => value.CharacterId == unit.Character.CharacterId)
                    : trigger.EnemyViews.FirstOrDefault(value => value.EnemyInstanceId == unit.Character.CharacterId);
                if (view == null || view.CombatUnit != null) return RejectStart();
                if (unit.IsPlayerSide && retainedPlayers.TryGetValue(unit.Character.CharacterId, out var retained))
                {
                    originalPositions[retained] = retained.Position;
                    retained.Position = unit.Position;
                    session.Units[index] = unit = retained;
                }
                if (!unit.IsPlayerSide && view.RuntimeCharacter != null) unit.Character = view.RuntimeCharacter;
                nextBindings.Add(unit, view);
            }
            if (!game.TryRegisterDungeonCombat(session)) return RejectStart();
            session.CanReceiveReinforcements = DungeonCombatArenaSystem.CanReceiveReinforcements(game, session);
            RefreshExits(session);
            // Start execution only after every participant has a valid, unique view.
            if (!combatHost.TryBegin(session)) return RejectStart();
            sources.Add(session, trigger);
            foreach(var trap in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                if(session.DungeonContext.ContainsWorldCell(trap.Position))
                    session.MapObjects.Add(new TrapCombatMapObject(trap, DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, trap.Position)));
            session.UnitEnteredCell = unit => trapSystem.EnterCell(
                DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, unit.Position), GatherTrapOccupants());
            session.TrapRouteState = path => trapSystem.RouteState(path.Select(cell => DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, cell)).ToArray());
            session.EvaluateTrapRisk = (unit, path) => trapSystem.EstimateRisk(unit,
                path.Select(cell => DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, cell)).ToArray());
            foreach (var pair in nextBindings)
            { bindings.Add(pair.Key, pair.Value); bindingSessions[pair.Key] = session; unitViews.Borrow(session, pair.Key, pair.Value); }
            combatHost.GetDriver(session).StartTurns();
            trigger.MarkTriggered();
            party.SeparateForCombat();
            party.CancelMovement();
            RefreshFocus();
            return true;
        }

        private void ShowPlacementNotice(string message)
        {
            if (placementNotice == null || placementNoticeText == null) return;
            // Repeated collision callbacks must not keep extending the same notification.
            if (placementNotice.alpha > 0f && placementNoticeText.text == message) return;
            placementNoticeText.text = message;
            placementNotice.alpha = 1f;
            placementNoticeUntil = Time.unscaledTime + 4f;
        }

        private void Update()
        {
            if (placementNotice != null && Time.unscaledTime >= placementNoticeUntil)
                placementNotice.alpha = 0f;
            if (!initialized || game.IsDefeated || DevConsoleView.IsOpen) return;
            AdvanceExplorationTraps();
            if (CampaignDefeatSystem.ShouldDefeat(game)) { EndCampaign(); return; }
            foreach (var session in game.Combats.ToArray())
            {
                if (!session.IsSeamless) continue;
                session.CanReceiveReinforcements = DungeonCombatArenaSystem.CanReceiveReinforcements(game, session);
                RefreshExits(session);
                var driver = combatHost.GetDriver(session);
                foreach (var unit in session.Units.ToArray())
                {
                    if (!unit.IsPlayerSide || !session.IsExitPosition(unit.Position) || !driver.TryLeave(unit)) continue;
                    retainedPlayers[unit.Character.CharacterId] = unit;
                    session.HasEscapePenaltyEligibleCharacters = true;
                    session.EscapePenaltyEligibleCharacterIds.Add(unit.Character.CharacterId);
                    Release(unit);
                }
                if (session.Outcome == CombatOutcomeState.Ongoing) JoinArrivals(session, driver);
                outcomeEvaluator.EvaluateCombatOutcome(session);
                if (CampaignDefeatSystem.ShouldDefeat(game)) { EndCampaign(); return; }
                if (session.Outcome != CombatOutcomeState.Ongoing) Finish(session);
            }
            RefreshFocus();
            foreach (var view in party.ActiveMemberViews)
                if (view.CombatUnit == null) previousExplorationPositions[view.CharacterId] = view.Position;
        }

        private Vector3? ResolveExplorationFeedbackPosition(BattleUnit unit)
        {
            var view = party.ActiveMemberViews.FirstOrDefault(candidate => candidate.RuntimeCharacter == unit.Character)
                ?? encounters.Triggers.SelectMany(trigger => trigger.EnemyViews)
                    .FirstOrDefault(candidate => candidate.RuntimeCharacter == unit.Character);
            return view != null ? view.transform.position : (Vector3?)null;
        }

        private List<DungeonTrapOccupant> GatherTrapOccupants()
        {
            var result = new List<DungeonTrapOccupant>();
            foreach (var session in game.Combats)
                foreach (var unit in session.Units)
                    result.Add(new DungeonTrapOccupant(unit, session,
                        DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, unit.Position)));
            foreach (var view in party.ActiveMemberViews)
            {
                if (game.GetCombatForCharacter(view.CharacterId) != null
                    || !game.CharactersById.TryGetValue(view.CharacterId, out var character)) continue;
                if (!retainedPlayers.TryGetValue(view.CharacterId, out var unit))
                    retainedPlayers[view.CharacterId] = unit = sessionFactory.CreateJoiningUnit(character, new GridPosition(0, 0));
                result.Add(new DungeonTrapOccupant(unit, null, map.WorldToCell(view.transform.position)));
            }
            foreach (var view in encounters.Triggers.SelectMany(trigger => trigger.EnemyViews))
            {
                if (view.CombatUnit != null || view.RuntimeCharacter == null || !view.gameObject.activeInHierarchy) continue;
                if (!exploringEnemies.TryGetValue(view.EnemyInstanceId, out var unit))
                    exploringEnemies[view.EnemyInstanceId] = unit = new BattleUnit { Character = view.RuntimeCharacter, IsPlayerSide = false };
                result.Add(new DungeonTrapOccupant(unit, null, map.WorldToCell(view.transform.position)));
            }
            return result;
        }

        private void AdvanceExplorationTraps()
        {
            var occupants = GatherTrapOccupants();
            trapSystem.AdvanceExploration(Time.deltaTime, occupants);
            foreach(var occupant in occupants)
            {
                var id = occupant.Unit.Character.CharacterId;
                if (occupant.Session != null) { trapPreviousCells.Remove(id); continue; }
                if (!trapPreviousCells.TryGetValue(id, out var previous) || previous != occupant.Cell)
                    trapSystem.EnterCell(occupant.Cell, occupants);
                trapPreviousCells[id] = occupant.Cell;
            }
            foreach(var trigger in encounters.Triggers)
            {
                var dead = trigger.EnemyViews.Where(view => view.CombatUnit == null
                    && view.RuntimeCharacter?.LifeState == CharacterLifeState.Dead
                    && !trigger.Encounter.IsEnemyDefeated(view.EnemyInstanceId)).ToArray();
                if(dead.Length == 0 || !contextFactory.TryCreate(game.ActiveDungeon, trigger.Encounter, out var context)) continue;
                var result = new CombatSession { DungeonContext = context };
                if(game.ActiveDungeon.TryGetPreRolledRewards(trigger.Encounter.InstanceId, out var loot)) result.Rewards = loot;
                foreach(var view in dead)
                {
                    trigger.Encounter.TryMarkEnemyDefeated(view.EnemyInstanceId);
                    result.TryMarkEnemyDefeated(view.EnemyInstanceId, DungeonBattleGridFactory.ToBattlePosition(context, map.WorldToCell(view.transform.position)));
                    view.gameObject.SetActive(false);
                }
                DungeonCombatOutcomeSystem.TryCreateEnemyBodies(game.ActiveDungeon, context, result);
                if (trigger.EnemyViews.All(view => trigger.Encounter.IsEnemyDefeated(view.EnemyInstanceId)))
                    game.ActiveDungeon.TryMarkEncounterCleared(trigger.Encounter.InstanceId);
                trigger.ReleaseEncounter();
                objects.RefreshBodies(floor, party);
            }
        }

        private void JoinArrivals(CombatSession session, CombatExecutionDriver driver)
        {
            var arrivalCells = new HashSet<GridPosition>(party.ActiveMemberViews
                .Where(view => view.CombatUnit == null
                    && game.GetCombatForCharacter(view.CharacterId) == null
                    && game.CharactersById.TryGetValue(view.CharacterId, out var character)
                    && character.LifeState == CharacterLifeState.Alive)
                .Select(view => DungeonBattleGridFactory.ToBattlePosition(
                    session.DungeonContext, map.WorldToCell(view.transform.position)))
                .Where(cell => session.Grid.IsWalkable(cell) && !session.Grid.IsBlocked(cell)));
            foreach (var view in party.ActiveMemberViews)
            {
                if (view.IsCrossingLowWall || view.CombatUnit != null || game.GetCombatForCharacter(view.CharacterId) != null
                    || !game.CharactersById.TryGetValue(view.CharacterId, out var character)
                    || character.LifeState != CharacterLifeState.Alive) continue;
                var cell = map.WorldToCell(view.transform.position);
                if (!session.DungeonContext.ContainsWorldCell(cell)) continue;
                var position = DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, cell);
                var unit = retainedPlayers.TryGetValue(view.CharacterId, out var retained)
                    ? retained : sessionFactory.CreateJoiningUnit(character, position);
                var reservedArrivals = new HashSet<GridPosition>(arrivalCells);
                reservedArrivals.Remove(position);
                if (!driver.TryJoin(unit, position, reservedArrivals))
                {
                    if (previousExplorationPositions.TryGetValue(view.CharacterId, out var previous)) view.SetPosition(previous);
                    party.CancelMovement();
                    ShowPlacementNotice("Brak miejsca, aby dołączyć");
                    continue;
                }
                retainedPlayers[view.CharacterId] = unit;
                bindings[unit] = view;
                bindingSessions[unit] = session;
                unitViews.Borrow(session, unit, view);
                party.CancelMovement();
            }
        }

        private void RefreshExits(CombatSession session)
        {
            var explorationCells = new HashSet<Vector2Int>(party.ActiveMemberViews
                .Where(view => view.CombatUnit == null)
                .Select(view => map.WorldToCell(view.transform.position)));
            DungeonCombatArenaSystem.RefreshExits(session, floor, explorationCells);
        }

        private void HandleMapObjectChanged(CombatMapObject value)
        {
            foreach (var session in game.Combats)
                if (session.MapObjects.Contains(value)) stateSync.TrySyncDoorStates(game, session);
            objects.RefreshDoorViews();
        }

        private void Finish(CombatSession session)
        {
            if (!outcomes.TryApplyOutcome(game, session))
            { Debug.LogError("Nie udało się zastosować wyniku walki " + session.CombatId); return; }
            if (session.Outcome == CombatOutcomeState.Victory) rewards.TryGrantVictoryRewards(game, session);
            if (session.EscapePenaltyEligibleCharacterIds.Count > 0) rewards.TryApplyEscapePenalty(game, session);
            foreach (var pair in bindings.ToArray())
            {
                if (bindingSessions[pair.Key] == session)
                {
                    if (!pair.Key.IsPlayerSide && pair.Key.Character.LifeState == CharacterLifeState.Dead)
                        pair.Value.gameObject.SetActive(false);
                    Release(pair.Key);
                }
            }
            objects.RefreshBodies(floor, party);
            sources[session].ReleaseEncounter();
            sources.Remove(session);
            session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null;
            combatHost.End(session);
            combatCamera.Forget(session);
            game.TryRemoveCombat(session);
            ClearFinishedCombatState();
            CampaignDefeatSystem.StoreRecoverableCompanions(game);
            party.RemoveUnavailableViews();
            party.CancelMovement();
        }

        private void EndCampaign()
        {
            game.MarkDefeated();
            foreach (var session in sources.Keys.ToArray())
            {
                session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null;
                combatHost.End(session);
                combatCamera.Forget(session);
                game.TryRemoveCombat(session);
            }
            foreach (var unit in bindings.Keys.ToArray()) Release(unit);
            sources.Clear();
            retainedPlayers.Clear();
            targeting.CancelTargetSelection();
            targeting.enabled = movementInput.enabled = inspectInput.enabled = false;
            gridFeedback.ClearAll();
            combatHudRoot.SetActive(false);
            explorationHudRoot.SetActive(false);
            party.CancelMovement();
            party.CloseInventoryForCombat();
        }

        private void ClearFinishedCombatState()
        {
            game.ActiveDungeon.ClearPendingPlayerPartyPositions();
            if (!game.HasCombat)
            {
                var lifecycle = new PassiveEffectLifecycleSystem();
                foreach (var id in game.PlayerParty.ActiveMemberIds)
                    if (game.CharactersById.TryGetValue(id, out var character)) lifecycle.RemoveCombatScopedEffects(character);
                retainedPlayers.Clear();
            }
        }

        private void Release(BattleUnit unit)
        {
            unitViews.Release(unit);
            bindings.Remove(unit);
            bindingSessions.Remove(unit);
            party.RefreshControlState();
        }

        public void RefreshFocus()
        {
            if (!initialized || game.IsDefeated) return;
            SetFocus(game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId));
        }

        private void SetFocus(CombatSession next, bool force = false)
        {
            if (!force && focused == next) return;
            targeting.CancelTargetSelection();
            movementInput.enabled = false;
            inspectInput.enabled = false;
            gridFeedback.ClearAll();
            focused = next;
            game.TryFocusCombat(next);
            combatHost.Focus(next);
            game.TrySetMode(next == null ? GameModeState.Exploration : GameModeState.Combat);
            combatHudRoot.SetActive(next != null);
            explorationHudRoot.SetActive(next == null);
            targeting.enabled = next != null;
            movementInput.enabled = next != null;
            inspectInput.enabled = next != null;
            if (next == null) combatCamera.Focus(null, default, default, null);
            explorationCamera.SetCombatSuspended(next != null);
            if (next != null)
            {
                party.CloseInventoryForCombat();
                var context = next.DungeonContext;
                var origin = map.CellToWorldCenter(context.WorldBounds.min);
                var cellSize = Vector2.Distance(origin, map.CellToWorldCenter(context.WorldBounds.min + Vector2Int.right));
                targeting.SetGridMapping(origin, cellSize);
                movementInput.SetGridMapping(origin, cellSize);
                inspectInput.SetGridMapping(origin, cellSize);
                gridFeedback.SetGridMapping(origin, cellSize);
                var bounds = new Bounds(origin, Vector3.zero);
                foreach (var cell in context.WorldCells) bounds.Encapsulate(map.CellToWorldCenter(cell));
                bounds.Expand(cellSize);
                map.TryGetWorldBounds(out var floorBounds);
                combatCamera.Focus(next, bounds, floorBounds, unit =>
                    unitViews.TryGetWorldPosition(unit, out var position) ? position
                        : (Vector3)(origin + new Vector2(unit.Position.X, unit.Position.Y) * cellSize));
            }
            party.CancelMovement();
            party.RefreshControlState();
        }

        private void OnDestroy()
        {
            if (unitViews != null) unitViews.ExplorationWorldPosition = null;
            if (!initialized) return;
            combatHost.CombatMapObjectChanged -= HandleMapObjectChanged;
            foreach (var session in sources.Keys.ToArray())
            {
                session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null;
                if (combatCamera != null) combatCamera.Forget(session);
                combatHost.End(session); game.TryRemoveCombat(session);
            }
            foreach (var unit in bindings.Keys.ToArray()) Release(unit);
        }
    }
}
