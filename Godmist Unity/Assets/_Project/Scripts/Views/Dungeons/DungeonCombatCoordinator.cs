using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
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
            party.SetCombatCoordinator(this);
            foreach (var trigger in encounters.Triggers) trigger.SetCombatCoordinator(this);
            combatHost.CombatMapObjectChanged += HandleMapObjectChanged;
            foreach (var view in party.ActiveMemberViews) previousExplorationPositions[view.CharacterId] = view.Position;
            initialized = true;
            SetFocus(null, true);
            return true;
        }

        public bool IsRoomFighting(string roomId) => game != null
            && game.Combats.Any(value => value.DungeonContext?.EncounterRoomId == roomId);

        public bool TryStartEncounter(EncounterTriggerView trigger)
        {
            if (!initialized || trigger == null || IsRoomFighting(trigger.Encounter.RoomId)
                || !contextFactory.TryCreate(game.ActiveDungeon, trigger.Encounter, out var context)) return false;
            var playerCells = party.ActiveMemberViews.ToDictionary(view => view.CharacterId, view => map.WorldToCell(view.transform.position));
            var enemyCells = trigger.EnemyViews.ToDictionary(view => view.EnemyInstanceId, view => map.WorldToCell(view.transform.position));
            if (!sessionFactory.TryCreateSeamless(game, trigger.Encounter, context, playerCells, enemyCells, out var session)) return false;
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
            foreach (var pair in nextBindings)
            { bindings.Add(pair.Key, pair.Value); bindingSessions[pair.Key] = session; unitViews.Borrow(session, pair.Key, pair.Value); }
            combatHost.GetDriver(session).StartTurns();
            trigger.MarkTriggered();
            party.SeparateForCombat();
            party.CancelMovement();
            RefreshFocus();
            return true;
        }

        private void Update()
        {
            if (!initialized || game.IsDefeated || DevConsoleView.IsOpen) return;
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

        private void JoinArrivals(CombatSession session, CombatExecutionDriver driver)
        {
            foreach (var view in party.ActiveMemberViews)
            {
                if (view.CombatUnit != null || game.GetCombatForCharacter(view.CharacterId) != null
                    || !game.CharactersById.TryGetValue(view.CharacterId, out var character)
                    || character.LifeState != CharacterLifeState.Alive) continue;
                var cell = map.WorldToCell(view.transform.position);
                if (!session.DungeonContext.ContainsWorldCell(cell)) continue;
                var position = DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, cell);
                var unit = retainedPlayers.TryGetValue(view.CharacterId, out var retained)
                    ? retained : sessionFactory.CreateJoiningUnit(character, position);
                if (!driver.TryJoin(unit, position))
                {
                    if (previousExplorationPositions.TryGetValue(view.CharacterId, out var previous)) view.SetPosition(previous);
                    party.CancelMovement();
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
            combatHost.End(session);
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
                combatHost.End(session);
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
                combatCamera.Frame(bounds);
            }
            party.CancelMovement();
            party.RefreshControlState();
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            combatHost.CombatMapObjectChanged -= HandleMapObjectChanged;
            foreach (var session in sources.Keys.ToArray())
            { combatHost.End(session); game.TryRemoveCombat(session); }
            foreach (var unit in bindings.Keys.ToArray()) Release(unit);
        }
    }
}
