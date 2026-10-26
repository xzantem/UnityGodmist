using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Quests;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    // Scene owner: map and character GameObjects are borrowed, never rebuilt on mode changes.
    public sealed partial class DungeonCombatCoordinator : MonoBehaviour
    {
        private static readonly Unity.Profiling.ProfilerMarker ExitRefreshSample = new("Godmist.Dungeon.Exits");
        private static readonly Unity.Profiling.ProfilerMarker NavigationSetupSample = new("Godmist.Dungeon.Setup.Navigation");
        private static readonly Unity.Profiling.ProfilerMarker PatrolSetupSample = new("Godmist.Dungeon.Setup.Patrols");
        private static readonly Unity.Profiling.ProfilerMarker ContactRefreshSample = new("Godmist.Dungeon.Contact");
        private static readonly Unity.Profiling.ProfilerMarker PatrolSample = new("Godmist.Dungeon.Patrols");
        private static readonly Unity.Profiling.ProfilerMarker DetectionSample = new("Godmist.Dungeon.Detection");
        private static readonly Unity.Profiling.ProfilerMarker FogSample = new("Godmist.Dungeon.Fog");
        private static readonly Unity.Profiling.ProfilerMarker TrapsSample = new("Godmist.Dungeon.Traps");
        private static readonly Unity.Profiling.ProfilerMarker TrapGatherSample = new("Godmist.Dungeon.Traps.GatherOccupants");
        private static readonly Unity.Profiling.ProfilerMarker TrapStatusSample = new("Godmist.Dungeon.Traps.AdvanceStatuses");
        private static readonly Unity.Profiling.ProfilerMarker TrapEntriesSample = new("Godmist.Dungeon.Traps.CellEntries");
        private static readonly Unity.Profiling.ProfilerMarker TrapDeathsSample = new("Godmist.Dungeon.Traps.Deaths");
        private static readonly Unity.Profiling.ProfilerMarker RoomAlertsSample = new("Godmist.Dungeon.RoomAlerts");
        private static readonly Unity.Profiling.ProfilerMarker MergesSample = new("Godmist.Dungeon.Merges");
        private static readonly Unity.Profiling.ProfilerMarker SessionsSample = new("Godmist.Dungeon.Sessions");
        private static readonly Unity.Profiling.ProfilerMarker FocusSample = new("Godmist.Dungeon.Focus");
        private static readonly Unity.Profiling.ProfilerMarker FovPreviewSample = new("Godmist.Dungeon.FovPreview");
        private static readonly Unity.Profiling.ProfilerMarker CombatEntryDetectionSample = new("Godmist.Dungeon.CombatEntry.Detection");
        private static readonly Unity.Profiling.ProfilerMarker CombatEntryBindingSample = new("Godmist.Dungeon.CombatEntry.Binding");
        [SerializeField] private CombatSandboxBootstrap combatHost;
        [SerializeField] private CombatUnitViewSpawner unitViews;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatMovementInputController movementInput;
        [SerializeField] private CombatUnitInspectController inspectInput;
        [SerializeField] private CombatGridFeedbackRenderer gridFeedback;
        [SerializeField] private ExplorationCameraFollow explorationCamera;
        [SerializeField] private CombatCameraFramer combatCamera;
        [SerializeField] private DungeonTacticalTooltip tacticalTooltip;
        [SerializeField] private DungeonMinimapView minimap;
        [SerializeField] private Godmist.Views.UI.CombatPartyRailView partyRail;
        [SerializeField] private GameObject combatHudRoot;
        [SerializeField] private GameObject explorationHudRoot;
        [SerializeField] private CanvasGroup placementNotice;
        [SerializeField] private TMPro.TMP_Text placementNoticeText;
        private float placementNoticeUntil;
        private BattleUnit elevationActiveUnit;
        private int feedbackElevation;
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
        private readonly List<DungeonTrapOccupant> explorationTrapOccupants = new();
        private readonly List<ExplorationCharacterView> explorationTrapDeaths = new();
        private bool initialized;
        private int syncedDoorRevision = -1;

        private QuestObjectiveProgressSystem CreateQuestProgress() =>
            new(game, GameBootstrap.Instance?.QuestService ?? new QuestService(game));
        private readonly DungeonVisibilitySystem visibility = new();
        public void CenterPartyMember(BattleUnit unit) => combatCamera.FocusUnit(unit);
        private int doorInteractionFrame = -1;
        public bool CanInteractWithDoor(string objectId) => initialized && !game.IsDefeated
            && !DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput()
            && targeting?.IsSelectingTarget != true
            && combatHost.Session != null
            && combatHost.GetDriver(combatHost.Session)?.CanInteractWithDoor(objectId) == true;

        public bool TryInteractWithDoor(string objectId)
        {
            if (doorInteractionFrame == Time.frameCount || !CanInteractWithDoor(objectId)) return false;
            if (!combatHost.GetDriver(combatHost.Session).TryInteractWithDoor(objectId)) return false;
            doorInteractionFrame = Time.frameCount;
            return true;
        }

        // Queries share the same geometry as detection and the transient FOV preview.
        public bool CanEnemyDetect(ExplorationCharacterView observer, ExplorationCharacterView target)
            => observer != null && target != null && visibility.CanDetect(floor, observer.DungeonPosition,
                observer.FacingDirection, observer.VisionRange, EnemyObservationAngle(observer), target.DungeonPosition);

        private float EnemyObservationAngle(ExplorationCharacterView observer)
            => Owner(observer)?.Outcome == CombatOutcomeState.Ongoing ? 360f : observer.VisionAngle;

        public void CollectPartyVisibility(HashSet<DungeonTraversalPosition> result)
        {
            if (party == null) { result.Clear(); return; }
            visibility.CollectPartyVisible(floor, party.ActiveMemberViews
                .Where(view => view.RuntimeCharacter?.LifeState == CharacterLifeState.Alive)
                .Select(view => view.DungeonPosition).ToArray(), result);
        }
        private CombatSession focused;
        public System.Action<string, double> InitializationStageMeasured;

        public bool Initialize(GameSession session, DungeonFloorInstance dungeonFloor,
            DungeonFloorMapView floorView, ExplorationPartyController controller,
            DungeonEncounterSpawner encounterSpawner, DungeonObjectSpawner objectSpawner)
        {
            var stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            void ReportStage(string stage)
            {
                if (InitializationStageMeasured == null) return;
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                InitializationStageMeasured(stage, (now-stageStarted)*1000d/System.Diagnostics.Stopwatch.Frequency);
                stageStarted = now;
            }
            if (combatHost == null || !combatHost.SeamlessDungeon || unitViews == null
                || targeting == null || movementInput == null || inspectInput == null
                || gridFeedback == null || explorationCamera == null || combatCamera == null
                || combatHudRoot == null || explorationHudRoot == null)
            { Debug.LogError("DungeonCombatCoordinator: niekompletny wiring seamless."); return false; }
            game = session; floor = dungeonFloor; map = floorView; party = controller;
            movementInput.IsWorldInteractionClaimedThisFrame = () => party.IsWorldInteractionClaimedThisFrame;
            objects = objectSpawner; encounters = encounterSpawner;
            if (minimap != null) minimap.Bind(floor, map, party, encounters);
            objects.BindElevationView(map);
            tacticalTooltip?.Bind(floor, map, objects.SpawnedObjects);
            unitViews.ConfigureDungeon(game, map);
            combatCamera.CanPresentUnit = unitViews.IsUnitVisible;
            unitViews.ExplorationWorldPosition = ResolveExplorationFeedbackPosition;
            unitViews.ExplorationView = ResolveExplorationFeedbackView;
            unitViews.ExplorationPresentationVisible = unit =>
            {
                var view = party.ActiveMemberViews.Concat(encounters.Triggers.SelectMany(trigger => trigger.EnemyViews))
                    .FirstOrDefault(candidate => candidate.RuntimeCharacter == unit.Character);
                return view == null || map.IsTraversalVisible(view.DungeonPosition);
            };
            combatHost.BindDungeonFeedback();
            foreach (var view in party.ActiveMemberViews) ConfigureView(view, "player:" + view.CharacterId);
            foreach (var trigger in encounters.Triggers)
                foreach (var view in trigger.EnemyViews)
                {
                    view.RuntimeCharacter.CharacterId = DungeonCombatMembershipSystem.EnemyKey(trigger.Encounter.InstanceId, view.EnemyInstanceId);
                    ConfigureView(view, "enemy:" + trigger.Encounter.InstanceId + ":" + view.EnemyInstanceId);
                }
            ReportStage("bindings");
            using (NavigationSetupSample.Auto()) party.ConfigureTraversal(floor, map, gridFeedback);
            ReportStage("navigation");
            gridFeedback.SetFloorSorting(map.FloorFeedbackSortingLayerID, map.GetFloorFeedbackDepthSortingOrder());
            foreach (var view in encounters.Triggers.SelectMany(trigger => trigger.EnemyViews))
            {
                view.CanEnterTraversal = position => party.CanEnterTraversal(view, position);
                view.CanEnterTraversalAtWorld = (position, world) =>
                    party.CanEnterTraversalAtWorld(view, position, world);
            }
            party.SetCombatCoordinator(this);
            foreach (var trigger in encounters.Triggers) trigger.SetCombatCoordinator(this);
            combatHost.CombatMapObjectChanged += HandleMapObjectChanged;
            combatHost.UnitLeftCombat += HandleUnitLeftCombat;
            floor.Map.ProgressionGateStateChanged += InvalidateFovPreview;
            foreach (var view in party.ActiveMemberViews) previousExplorationPositions[view.CharacterId] = view.Position;
            trapSystem = new DungeonTrapSystem(floor, game.ActiveDungeon.DungeonLevel,
                session => session == null ? combatHost.DungeonFeedback : combatHost.GetDriver(session)?.FeedbackBus);
            using (PatrolSetupSample.Auto()) InitializePatrols();
            ReportStage("patrols");
            initialized = true;
            combatHudRoot.GetComponentInChildren<CombatSkillBarView>(true)?.BindExploration(
                GetExplorationSkillActor, skill => TrySelectOpeningSkill(skill), ShowExplorationSkillHover);
            UpdateFog(true);
            ReportStage("fog");
            SetFocus(null, true);
            ReportStage("focus");
            return true;
        }

        private void ConfigureView(ExplorationCharacterView view, string key)
        {
            view.ConfigureTerrain(floor.Map, map, floor, key);
            view.CanCrossTo = cell => !floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                .Any(obstacle => obstacle.IsActive && obstacle.BlocksMovement && obstacle.OccupiesCell(cell));
        }

        public bool IsRoomFighting(string roomId) => game != null
            && game.Combats.Any(value => value.DungeonContext?.EncounterRoomId == roomId);

        public bool TryStartEncounter(EncounterTriggerView trigger)
        {
            if (!initialized || trigger == null || game.DebugEnemyAggroDisabled) return false;
            foreach (var enemy in trigger.EnemyViews)
                foreach (var target in party.ActiveMemberViews)
                    if (IsLiving(enemy) && IsLiving(target) && CanEnemyDetect(enemy, target)
                        && floor.GetEnemyAwareness(DungeonCombatMembershipSystem.EnemyKey(trigger.Encounter.InstanceId, enemy.EnemyInstanceId)).IsDetected)
                        return TryStartSpottedEncounter(trigger, enemy, target);
            return false;
        }

        private bool TryStartSpottedEncounter(EncounterTriggerView trigger, ExplorationCharacterView observer, ExplorationCharacterView spotted)
        {
            using var entrySample = CombatEntryDetectionSample.Auto();
            if (!initialized || trigger == null || observer.IsCrossingLowWall || spotted.IsCrossingLowWall
                || observer.CombatUnit != null || spotted.CombatUnit != null
                || !contextFactory.TryCreate(game.ActiveDungeon, trigger.Encounter, out var context, true)) return false;
            // Detection singles out the initiating hero. Combat observation is
            // omnidirectional, so include nearby visible allies in the same placement
            // transaction instead of waiting for another cone check after actors snap.
            var playerCells = party.ActiveMemberViews.Where(view => IsLiving(view)
                && !view.IsCrossingLowWall && Owner(view) == null
                && (view == spotted || visibility.CanObserveCombat(floor,
                    observer.DungeonPosition, observer.VisionRange, view.DungeonPosition)))
                .ToDictionary(view => view.CharacterId, view => map.WorldToCell(view.Position));
            var enemyCells = new Dictionary<string, Vector2Int> { [observer.EnemyInstanceId] = map.WorldToCell(observer.transform.position) };
            if (!sessionFactory.TryCreateSeamless(game, trigger.Encounter, context, playerCells, enemyCells,
                out var session, out var placementFailed, true))
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
                    : observer;
                if (view == null || view.CombatUnit != null) return RejectStart();
                if (unit.IsPlayerSide && retainedPlayers.TryGetValue(unit.Character.CharacterId, out var retained))
                {
                    originalPositions[retained] = retained.Position;
                    retained.Position = unit.Position;
                    session.Units[index] = unit = retained;
                }
                if (!unit.IsPlayerSide && view.RuntimeCharacter != null)
                { view.RuntimeCharacter.CharacterId = unit.Character.CharacterId; unit.Character = view.RuntimeCharacter; }
                nextBindings.Add(unit, view);
            }
            if (!ActivateBoundSession(session, nextBindings, trigger, driver => driver.StartTurns())) return RejectStart();
            trigger.MarkTriggered();
            party.SeparateForCombat();
            RefreshFocus();
            QueueRoomAlert(observer, spotted);
            return true;
        }

        private bool ActivateBoundSession(CombatSession session,
            Dictionary<BattleUnit, ExplorationCharacterView> nextBindings, EncounterTriggerView trigger,
            System.Action<CombatExecutionDriver> startTurns)
        {
            using var entrySample = CombatEntryBindingSample.Auto();
            if (!game.TryRegisterDungeonCombat(session)) return false;
            session.Contact = new DungeonContactState((enemy, player) =>
                CanEnemySeeCombatTarget(session, enemy, player));
            session.CanReceiveReinforcements = DungeonCombatArenaSystem.CanReceiveReinforcements(game, session);
            RefreshExits(session);
            // Start execution only after every participant has a valid, unique view.
            if (!combatHost.TryBegin(session)) return false;
            sources.Add(session, trigger);
            foreach(var trap in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                if(session.DungeonContext.ContainsWorldCell(trap.Position))
                    session.MapObjects.Add(new TrapCombatMapObject(trap, DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, trap.Position)));
            session.UnitEnteredCell = unit => {
                using (ContactRefreshSample.Auto()) session.Contact?.Refresh(session);
                if (unit.Position.Level == 0 && !unit.Position.IsOnStairs) trapSystem.EnterCell(
                DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, unit.Position), GatherTrapOccupants()); };
            session.TrapRouteState = path => trapSystem.RouteState(path.Where(cell => cell.Level == 0 && !cell.IsOnStairs).Select(cell => DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, cell)).ToArray());
            session.EvaluateTrapRisk = (unit, path) => trapSystem.EstimateRisk(unit,
                path.Where(cell => cell.Level == 0 && !cell.IsOnStairs).Select(cell => DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, cell)).ToArray());
            foreach (var pair in nextBindings)
            { bindings.Add(pair.Key, pair.Value); bindingSessions[pair.Key] = session; unitViews.Borrow(session, pair.Key, pair.Value); }
            session.TrySpawnReinforcements = () => TrySpawnReinforcements(session);
            session.EnemyDefeated = id => rewards.TryGrantDefeatExperience(game, session, id);
            startTurns(combatHost.GetDriver(session));
            return true;
        }

        private bool CanEnemySeeCombatTarget(CombatSession session, BattleUnit enemy, BattleUnit player)
        {
            var visionRange = ResolveCombatVisionRange(session, enemy);
            return visionRange.HasValue && visibility.CanObserveCombat(floor,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, enemy.Position),
                visionRange.Value,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, player.Position));
        }

        private float? ResolveCombatVisionRange(CombatSession session, BattleUnit enemy)
        {
            if (bindings.TryGetValue(enemy, out var enemyView)) return enemyView.VisionRange;

            // Summons are created during combat and have no borrowed exploration view.
            // Give them the same default range as encounter enemies while preserving
            // the existing requirement for ordinary enemies to have a bound view.
            return session?.Summons?.Any(summon => summon?.Unit == enemy) == true
                ? 10f
                : null;
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
            if (!initialized || game.IsDefeated || DevConsoleView.IsOpen
                || game.CurrentMode is GameModeState.Menu or GameModeState.Loading)
            { detectionElapsed = 0f; ClearFovPreview(); return; }
            if (!UIWorkspaceInputRouter.HasFocusedTextInput()
                && (game.CurrentMode == GameModeState.Combat || party.CanAcceptExplorationInput))
            {
                if (Input.GetKeyDown(KeyCode.PageUp)) { map.StepViewedLevel(1); gridFeedback.RefreshVisibility(); }
                else if (Input.GetKeyDown(KeyCode.PageDown)) { map.StepViewedLevel(-1); gridFeedback.RefreshVisibility(); }
            }
            var assetAuditRoom = game.ActiveDungeon?.IsAssetAuditRoom == true;
            if (!assetAuditRoom)
            {
                using (TrapsSample.Auto()) AdvanceExplorationTraps();
                UpdatePatrols();
            }
            RefreshSharedDoorStates();
            map.RefreshEntArenaFire(game.Combats);
            UpdateFog();
            if (!assetAuditRoom)
            {
                UpdateDetection();
                using (RoomAlertsSample.Auto()) ProcessRoomAlerts();
                using (MergesSample.Auto()) ProcessPendingMerges();
            }
            if (CampaignDefeatSystem.ShouldDefeat(game) && !HasActionPresentation()) { EndCampaign(); return; }
            using (SessionsSample.Auto())
            foreach (var session in game.Combats.ToArray())
            {
                if (!session.IsSeamless) continue;
                PresentDefeatedEnemies(session);
                session.CanReceiveReinforcements = DungeonCombatArenaSystem.CanReceiveReinforcements(game, session);
                RefreshExits(session);
                var driver = combatHost.GetDriver(session);
                using (ContactRefreshSample.Auto()) session.Contact?.Refresh(session);
                if (!session.IsDungeonWide && session.Outcome == CombatOutcomeState.Ongoing) JoinArrivals(session, driver);
                outcomeEvaluator.EvaluateCombatOutcome(session);
                if (CampaignDefeatSystem.ShouldDefeat(game) && !HasActionPresentation()) { EndCampaign(); return; }
                if (session.Outcome != CombatOutcomeState.Ongoing && !driver.IsSkillPresentationInProgress) Finish(session);
            }
            using (FocusSample.Auto()) RefreshFocus();
            UpdateOpeningPreparation();
            RefreshExplorationSkillHover();
            using (FovPreviewSample.Auto()) UpdateFovPreview();
            var focusView = party.ActiveMemberViews.FirstOrDefault(v => v.CharacterId == game.PlayerParty.ActiveLeaderId);
            if (focused?.ActiveUnit != null) map.FocusElevation(DungeonBattleGridFactory.ToDungeonTraversalPosition(focused.DungeonContext, focused.ActiveUnit.Position));
            else if (focusView != null) map.FocusElevation(focusView.DungeonPosition);
            if (focused?.ActiveUnit != elevationActiveUnit)
            {
                elevationActiveUnit = focused?.ActiveUnit;
                if (elevationActiveUnit != null && floor.Map.TryGetTraversalHeight(
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(focused.DungeonContext, elevationActiveUnit.Position), out var height))
                    map.FollowElevation(Mathf.CeilToInt(height), true);
            }
            gridFeedback.SetFloorSorting(map.FloorFeedbackSortingLayerID, map.GetFloorFeedbackDepthSortingOrder());
            if (feedbackElevation != map.PresentationRevision) { feedbackElevation = map.PresentationRevision; gridFeedback.RefreshVisibility(); }
            foreach (var view in party.ActiveMemberViews)
                if (view.CombatUnit == null) previousExplorationPositions[view.CharacterId] = view.Position;
        }

        private ExplorationCharacterView ResolveExplorationFeedbackView(BattleUnit unit) => unit?.Character==null?null:
            party.ActiveMemberViews.FirstOrDefault(candidate=>candidate.RuntimeCharacter==unit.Character)
            ?? encounters.Triggers.SelectMany(trigger=>trigger.EnemyViews).FirstOrDefault(candidate=>candidate.RuntimeCharacter==unit.Character);
        private Vector3? ResolveExplorationFeedbackPosition(BattleUnit unit)
        {
            var view = party.ActiveMemberViews.FirstOrDefault(candidate => candidate.RuntimeCharacter == unit.Character)
                ?? encounters.Triggers.SelectMany(trigger => trigger.EnemyViews)
                    .FirstOrDefault(candidate => candidate.RuntimeCharacter == unit.Character);
            return view != null ? view.transform.position : (Vector3?)null;
        }

        private List<DungeonTrapOccupant> GatherTrapOccupants(List<DungeonTrapOccupant> result = null)
        {
            result ??= new List<DungeonTrapOccupant>();
            result.Clear();
            foreach (var session in game.Combats)
                foreach (var unit in session.Units)
                    result.Add(new DungeonTrapOccupant(unit, session,
                        DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, unit.Position), unit.Position.Level == 0 && !unit.Position.IsOnStairs));
            foreach (var view in party.ActiveMemberViews)
            {
                if (game.GetCombatForCharacter(view.CharacterId) != null
                    || !game.CharactersById.TryGetValue(view.CharacterId, out var character)) continue;
                if (!retainedPlayers.TryGetValue(view.CharacterId, out var unit))
                    retainedPlayers[view.CharacterId] = unit = sessionFactory.CreateJoiningUnit(character, new GridPosition(0, 0));
                var position = view.DungeonPosition;
                result.Add(new DungeonTrapOccupant(unit, null, map.WorldToCell(view.transform.position), !position.IsOnStairs && position.Surface.Level == 0));
            }
            var triggers = encounters.Triggers;
            for (int triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                var trigger = triggers[triggerIndex];
                var enemies = trigger.EnemyViews;
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    var view = enemies[enemyIndex];
                    if (view.CombatUnit != null || view.RuntimeCharacter == null || !view.gameObject.activeInHierarchy) continue;
                    if (!exploringEnemies.TryGetValue(view.RuntimeCharacter.CharacterId, out var unit))
                        exploringEnemies[view.RuntimeCharacter.CharacterId] = unit = new BattleUnit { Character = view.RuntimeCharacter, IsPlayerSide = false };
                    var position = view.DungeonPosition;
                    result.Add(new DungeonTrapOccupant(unit, null, map.WorldToCell(view.transform.position), !position.IsOnStairs && position.Surface.Level == 0));
                }
            }
            return result;
        }

        private void AdvanceExplorationTraps()
            => AdvanceExplorationTrapsForElapsed(Time.deltaTime);

        private void AdvanceExplorationTrapsForElapsed(float seconds)
        {
            List<DungeonTrapOccupant> occupants;
            using (TrapGatherSample.Auto()) occupants = GatherTrapOccupants(explorationTrapOccupants);
            using (TrapStatusSample.Auto()) trapSystem.AdvanceExploration(seconds, occupants);
            using (TrapEntriesSample.Auto())
            foreach(var occupant in occupants)
            {
                var id = occupant.Unit.Character.CharacterId;
                if (occupant.Session != null || !occupant.IsOnGroundSurface) { trapPreviousCells.Remove(id); continue; }
                if (!trapPreviousCells.TryGetValue(id, out var previous) || previous != occupant.Cell)
                    trapSystem.EnterCell(occupant.Cell, occupants);
                trapPreviousCells[id] = occupant.Cell;
            }
            using (TrapDeathsSample.Auto())
            for (int triggerIndex = 0; triggerIndex < encounters.Triggers.Count; triggerIndex++)
            {
                var trigger = encounters.Triggers[triggerIndex];
                explorationTrapDeaths.Clear();
                var enemies = trigger.EnemyViews;
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    var view = enemies[enemyIndex];
                    if (view.CombatUnit == null && view.RuntimeCharacter?.LifeState == CharacterLifeState.Dead
                        && !trigger.Encounter.IsEnemyDefeated(view.EnemyInstanceId)) explorationTrapDeaths.Add(view);
                }
                if(explorationTrapDeaths.Count == 0 || !contextFactory.TryCreate(game.ActiveDungeon, trigger.Encounter, out var context)) continue;
                var result = new CombatSession { DungeonContext = context };
                if(game.ActiveDungeon.TryGetPreRolledRewards(trigger.Encounter.InstanceId, out var loot)) result.Rewards = loot;
                result.EnemyDefeated = id => rewards.TryGrantDefeatExperience(game, result, id);
                foreach(var view in explorationTrapDeaths)
                {
                    if (trigger.Encounter.TryMarkEnemyDefeated(view.EnemyInstanceId)
                        && !trigger.Encounter.IsReinforcement)
                    {
                        foreach (var enemy in trigger.Encounter.Enemies)
                            if (enemy?.InstanceId == view.EnemyInstanceId
                                && enemy.CharacterDefinition != null)
                            {
                                CreateQuestProgress()
                                    .ReportEnemyDefeated(game.ActiveDungeon.DungeonId,
                                        game.ActiveDungeon.DungeonLevel,
                                        enemy.CharacterDefinition.Id, false);
                                break;
                            }
                    }
                    result.TryMarkEnemyDefeated(view.EnemyInstanceId, DungeonBattleGridFactory.ToBattleTraversalPosition(context, view.DungeonPosition));
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
                .Select(view => DungeonBattleGridFactory.ToBattleTraversalPosition(
                    session.DungeonContext, view.DungeonPosition))
                .Where(cell => session.Grid.IsWalkable(cell) && !session.Grid.IsBlocked(cell)));
            foreach (var view in party.ActiveMemberViews)
            {
                if (view.IsCrossingLowWall || view.CombatUnit != null || game.GetCombatForCharacter(view.CharacterId) != null
                    || !game.CharactersById.TryGetValue(view.CharacterId, out var character)
                    || character.LifeState != CharacterLifeState.Alive) continue;
                var cell = map.WorldToCell(view.transform.position);
                if (!session.DungeonContext.ContainsWorldCell(cell)) continue;
                var position = DungeonBattleGridFactory.ToBattleTraversalPosition(session.DungeonContext, view.DungeonPosition);
                var unit = retainedPlayers.TryGetValue(view.CharacterId, out var retained)
                    ? retained : sessionFactory.CreateJoiningUnit(character, position);
                var reservedArrivals = new HashSet<GridPosition>(arrivalCells);
                reservedArrivals.Remove(position);
                if (!driver.TryJoin(unit, position, reservedArrivals))
                {
                    if (!view.UsesLayeredTraversal && previousExplorationPositions.TryGetValue(view.CharacterId, out var previous)) view.SetPosition(previous);
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
            using var sample = ExitRefreshSample.Auto();
            var explorationCells = new HashSet<Vector2Int>(party.ActiveMemberViews
                .Where(view => view.CombatUnit == null)
                .Select(view => map.WorldToCell(view.transform.position)));
            DungeonCombatArenaSystem.RefreshExits(session, floor, explorationCells,
                new HashSet<DungeonTraversalPosition>(party.ActiveMemberViews.Where(view => view.CombatUnit == null).Select(view => view.DungeonPosition)));
        }

        private void HandleMapObjectChanged(CombatMapObject value)
        {
            InvalidateFovPreview();
            if (value is ObstacleCombatMapObject) UpdateFog(true);
            foreach (var session in game.Combats)
                if (session.MapObjects.Contains(value)) stateSync.TrySyncDoorStates(game, session);
            RefreshSharedDoorStates(true);
            foreach (var session in game.Combats) RefreshExits(session);
            objects.RefreshDoorViews();
        }

        private void RefreshSharedDoorStates(bool force = false)
        {
            if (!force && syncedDoorRevision == floor.Map.TraversalRevision) return;
            foreach (var session in game.Combats) stateSync.RefreshDoorsFromFloor(floor, session);
            syncedDoorRevision = floor.Map.TraversalRevision;
        }

        private bool HasActionPresentation() => game.Combats.Any(combat =>
            combatHost.GetDriver(combat)?.IsSkillPresentationInProgress == true);

        private void PresentDefeatedEnemies(CombatSession session)
        {
            var changed = false;
            foreach (var pair in bindings)
            {
                if (bindingSessions[pair.Key] != session || pair.Key.IsPlayerSide
                    || pair.Key.Character.LifeState != CharacterLifeState.Dead || !pair.Value.gameObject.activeSelf) continue;
                if (pair.Value.HasCombatPose) continue;
                session.TryMarkEnemyDefeated(pair.Key.Character.CharacterId, pair.Key.Position);
                pair.Value.gameObject.SetActive(false);
                changed = true;
            }
            if (!changed) return;
            stateSync.TrySyncDefeatedEnemyStates(game, session, CreateQuestProgress());
            DungeonCombatOutcomeSystem.TryCreateEnemyBodies(game.ActiveDungeon, session.DungeonContext, session);
            objects.RefreshBodies(floor, party);
        }

        private void Finish(CombatSession session)
        {
            if (!outcomes.TryApplyOutcome(game, session, CreateQuestProgress()))
            { Debug.LogError("Nie udało się zastosować wyniku walki " + session.CombatId); return; }
            foreach (var unit in session.Units.Concat(session.DepartedUnits.Values))
                if (game.GetCombatForCharacter(unit.Character.CharacterId) is not {} owner || owner == session)
                    unit.HasUnfinishedCombatTurn = false;
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
            foreach (var trigger in encounters.Triggers)
                if (session.EnemySources.Values.Any(source => source.EncounterId == trigger.Encounter.InstanceId)) trigger.ReleaseEncounter();
            sources.Remove(session);
            session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null; session.TrySpawnReinforcements = null;
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
                session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null; session.TrySpawnReinforcements = null;
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
                party.RejoinAfterCombat();
            }
        }

        private void HandleUnitLeftCombat(CombatSession session, BattleUnit unit)
        {
            retainedPlayers[unit.Character.CharacterId] = unit;
            Release(unit);
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
            if (partyRail != null) partyRail.SelectedCharacterId = game.PlayerParty.ActiveLeaderId;
            SetFocus(game.GetCombatForCharacter(game.PlayerParty.ActiveLeaderId));
        }

        private void SetFocus(CombatSession next, bool force = false)
        {
            if (!force && focused == next) return;
            var leaderView = party.ActiveMemberViews.FirstOrDefault(view => view.CharacterId == game.PlayerParty.ActiveLeaderId);
            if (leaderView != null) map.FocusElevation(leaderView.DungeonPosition, true);
            if (combatHost.IsOpeningPreview) CancelOpeningPreparation(false);
            targeting.CancelTargetSelection();
            movementInput.enabled = false;
            inspectInput.enabled = false;
            gridFeedback.ClearAll();
            previewRevision = -1;
            gridFeedback.IsPositionVisible = next?.DungeonContext == null ? null
                : position => map.IsTraversalSelectable(DungeonBattleGridFactory.ToDungeonTraversalPosition(next.DungeonContext, position))
                    && map.IsCurrentlySeen(DungeonBattleGridFactory.ToDungeonTraversalPosition(next.DungeonContext, position));
            gridFeedback.IsEnemyFovPositionVisible = next?.DungeonContext == null ? null
                : position => map.IsTraversalSelectable(DungeonBattleGridFactory.ToDungeonTraversalPosition(next.DungeonContext, position));
            inspectInput.IsPositionVisible = gridFeedback.IsPositionVisible;
            focused = next;
            game.TryFocusCombat(next);
            combatHost.Focus(next);
            game.TrySetMode(next == null ? GameModeState.Exploration : GameModeState.Combat);
            combatHudRoot.SetActive(true);
            // An opening skill executes synchronously after focus changes,
            // before the HUD's next LateUpdate can subscribe to its new bus.
            combatHudRoot.GetComponentInChildren<CombatHudView>(true)?.RefreshSkillFeedbackBinding();
            // The shared party rail stays available when changing the focused combat session.
            explorationHudRoot.SetActive(true);
            targeting.enabled = next != null;
            movementInput.enabled = next != null;
            inspectInput.enabled = true;
            if (next == null) combatCamera.Focus(null, default, default, null);
            explorationCamera.SetCombatSuspended(next != null);
            if (next != null)
            {
                party.CloseInventoryForCombat();
                var context = next.DungeonContext;
                var origin = map.CellToWorldCenter(context.WorldBounds.min);
                var cellSize = Vector2.Distance(origin, map.CellToWorldCenter(context.WorldBounds.min + Vector2Int.right));
                GridPosition? ResolvePointer(Vector3 world) => map.TryGetVisibleTraversal(world, out var position)
                    ? (map.IsTraversalSelectable(position) && map.IsCurrentlySeen(position) ? DungeonBattleGridFactory.ToBattleTraversalPosition(context, position) : null) : null;
                targeting.ResolvePointerPosition = ResolvePointer;
                movementInput.ResolvePointerPosition = ResolvePointer;
                inspectInput.ResolvePointerPosition = ResolvePointer;
                targeting.SetGridMapping(origin, cellSize);
                movementInput.SetGridMapping(origin, cellSize);
                inspectInput.SetGridMapping(origin, cellSize);
                gridFeedback.SetGridMapping(origin, cellSize);
                var framingCells = next.IsDungeonWide
                    ? next.Units.Where(unitViews.IsUnitVisible).Select(unit => DungeonBattleGridFactory.ToWorldPosition(context, unit.Position)).ToArray()
                    : context.WorldCells.ToArray();
                var bounds = new Bounds(framingCells.Length > 0 ? map.CellToWorldCenter(framingCells[0]) : origin, Vector3.zero);
                foreach (var cell in framingCells) bounds.Encapsulate(map.CellToWorldCenter(cell));
                bounds.Expand(cellSize);
                map.TryGetWorldBounds(out var floorBounds);
                Bounds Participants()
                {
                    var result = bounds;
                    var any = false;
                    foreach (var unit in next.Units)
                    {
                        if (unit.Character?.LifeState == CharacterLifeState.Dead || !unitViews.IsUnitVisible(unit)
                            || !unitViews.TryGetPresentationBounds(unit, out var body)) continue;
                        if (!any) { result = body; any = true; } else result.Encapsulate(body);
                    }
                    return result;
                }
                var bossFight = next.Units.Any(u => CombatBossPresentation.IsBoss(u) && unitViews.IsUnitVisible(u));
                combatCamera.Focus(next, bounds, floorBounds, unit =>
                    unitViews.TryGetWorldPosition(unit, out var position) ? position
                        : (Vector3)(origin + new Vector2(unit.Position.X, unit.Position.Y) * cellSize),
                    bossFight || map.UsesVegetationPresentation ? Participants : null);
            }
            party.CancelMovement();
            party.RefreshControlState();
        }

        private void OnDestroy()
        {
            if (combatCamera != null) combatCamera.CanPresentUnit = null;
            if (unitViews != null) { unitViews.ExplorationWorldPosition = null; unitViews.ExplorationPresentationVisible = null; unitViews.ExplorationView = null; }
            if (!initialized) return;
            combatHost.CombatMapObjectChanged -= HandleMapObjectChanged;
            combatHost.UnitLeftCombat -= HandleUnitLeftCombat;
            floor.Map.ProgressionGateStateChanged -= InvalidateFovPreview;
            foreach (var session in sources.Keys.ToArray())
            {
                session.UnitEnteredCell = null; session.EvaluateTrapRisk = null; session.TrapRouteState = null; session.TrySpawnReinforcements = null;
                if (combatCamera != null) combatCamera.Forget(session);
                combatHost.End(session); game.TryRemoveCombat(session);
            }
            foreach (var unit in bindings.Keys.ToArray()) Release(unit);
        }
    }
}
