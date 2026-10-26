using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.Game;
using UnityEngine;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;

namespace Godmist.Views.Combat
{
    public class CombatSandboxBootstrap : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField] private CharacterDefinition jerzyDefinition;
        [SerializeField] private CharacterDefinition allyDefinition;
        [SerializeField] private CharacterDefinition enemyDefinition;

        [Header("Classes")]
        [SerializeField] private CharacterClassData scoutClass;
        [SerializeField] private CharacterClassData warriorClass;

        [Header("Config")]
        [SerializeField] private int gridWidth = 8;
        [SerializeField] private int gridHeight = 8;

        [Header("AI Presentation")]
        [SerializeField, Min(0f)] private float aiDecisionDelay = 0.65f;
        [SerializeField, Min(0f)] private float aiMovementResultDelay = 0.9f;
        [SerializeField, Min(0f)] private float aiSkillResultDelay = 1.2f;

        [Header("Debug References")]
        [SerializeField] private CombatBattleLog battleLog;
        [SerializeField] private CombatFloatingFeedbackController floatingFeedback;

        [Header("Debug Obstacles")]
        [SerializeField] private List<GridPosition> blockedPositions = new();
        [Header("Dungeon Combat Area")]
        [SerializeField] private CombatDungeonAreaView dungeonAreaView;
        [SerializeField] private CombatCameraFramer cameraFramer;
        public CombatCameraFramer CameraFramer => cameraFramer;
        [Header("Scenario Runner")]
        [SerializeField] private CombatScenarioRunner scenarioRunner;
        

        [SerializeField] private bool seamlessDungeon;
        [SerializeField] private CombatUnitViewSpawner unitViewSpawner;
        private readonly Dictionary<CombatSession, CombatExecutionDriver> drivers = new();
        private CombatExecutionDriver focused;
        private readonly CombatFeedbackBus dungeonFeedback = new();
        public CombatFeedbackBus DungeonFeedback => dungeonFeedback;
        public bool SeamlessDungeon => seamlessDungeon;
        private CombatExecutionDriver openingPreview;
        public bool IsOpeningPreview => openingPreview != null;
        public delegate bool OpeningSkillCommit(SkillData skill, IReadOnlyDictionary<int, GridPosition?> positions,
            GridPosition? teleport, GridPosition? placement, IReadOnlyDictionary<int, int> rotations,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosen);
        private OpeningSkillCommit commitOpening;
        private Func<BattleUnit, bool> openingVisibility;
        public CombatSession Session => focused?.Session ?? openingPreview?.Session;
        public CombatExecutionDriver OpeningPreview => openingPreview;
        public bool BeginOpeningPreview(CombatSession session, OpeningSkillCommit commit, Func<BattleUnit, bool> visible)
        {
            if (!seamlessDungeon || focused != null || session?.ActiveUnit == null || commit == null) return false;
            openingPreview = CreateDriver();
            openingPreview.InitializeTargetingPreview(session);
            commitOpening = commit; openingVisibility = visible;
            return true;
        }
        public void CancelOpeningPreview()
        { openingPreview = null; commitOpening = null; openingVisibility = null; }
        public bool IsUnitVisible(BattleUnit unit) => unit != null && !Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)
            && (IsOpeningPreview ? openingVisibility?.Invoke(unit) == true : unitViewSpawner == null || unitViewSpawner.IsUnitVisible(unit));
        public bool IsUnitKnown(BattleUnit unit) => unit != null && !Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)
            && (IsOpeningPreview ? openingVisibility?.Invoke(unit) == true : unitViewSpawner == null || unitViewSpawner.IsUnitKnown(unit));
        public bool IsPlayerMovementPresentationInProgress => focused?.IsPlayerMovementPresentationInProgress == true;
        public bool IsGameplaySuspended => GameBootstrap.Instance?.Session?.CurrentMode is GameModeState.Menu or GameModeState.Loading;
        public event Action<CombatMapObject> CombatMapObjectChanged;
        public event Action<CombatSession, BattleUnit> UnitLeftCombat;
        public IReadOnlyCollection<CombatExecutionDriver> Drivers => drivers.Values;
        // Cheap cache key: parallel sessions share occupancy, while AP still bounds the path search.
        public int GetDungeonMovementStateHash()
        {
            var state = new HashCode();
            foreach (var driver in drivers.Values)
            {
                var combat = driver.Session;
                state.Add(combat);
                foreach (var unit in combat.Units)
                { state.Add(unit); state.Add(unit.Position); state.Add(unit.Character?.LifeState); }
                foreach (var item in combat.MapObjects)
                {
                    state.Add(item.ObjectId); state.Add(item.Position); state.Add(item.BlocksMovement);
                    if (item is TrapCombatMapObject trap) state.Add(trap.Trap.IsTriggered);
                }
                if (combat.DungeonContext?.SurfaceMap != null)
                    foreach (var edge in combat.DungeonContext.SurfaceMap.SurfaceClosedProgressionGateEdges) state.Add(edge);
            }
            if (Session?.ActiveUnit != null)
            {
                state.Add(StatusEffectSystem.IsHardCrowdControlled(Session.ActiveUnit));
                state.Add(StatusEffectSystem.IsSleeping(Session.ActiveUnit));
                state.Add(StatusEffectSystem.IsImmobilized(Session.ActiveUnit));
            }
            return state.ToHashCode();
        }
        private CombatExecutionDriver CreateDriver() => new(this, unitViewSpawner, jerzyDefinition, allyDefinition, enemyDefinition, scoutClass, warriorClass, gridWidth, gridHeight, aiDecisionDelay, aiMovementResultDelay, aiSkillResultDelay, battleLog, floatingFeedback, blockedPositions, dungeonAreaView, cameraFramer, scenarioRunner);
        private void Start()
        {
            if (seamlessDungeon) return;
            if (unitViewSpawner == null) unitViewSpawner = GetComponent<CombatUnitViewSpawner>();
            focused = CreateDriver();
            focused.CombatMapObjectChanged += RelayMapObjectChanged;
            focused.Initialize();
            drivers.Add(focused.Session, focused);
        }
        public void BindDungeonFeedback()
        {
            if (seamlessDungeon) floatingFeedback?.Bind(dungeonFeedback);
        }

        public bool TryBegin(CombatSession session)
        {
            if (!seamlessDungeon || session == null || drivers.ContainsKey(session) || unitViewSpawner == null) return false;
            var driver = CreateDriver();
            try
            {
                driver.CombatMapObjectChanged += RelayMapObjectChanged;
                driver.UnitLeftCombat += RelayUnitLeftCombat;
                driver.Initialize(session);
                drivers.Add(session, driver);
                driver.FeedbackBus.Published += dungeonFeedback.Publish;
                floatingFeedback?.Bind(dungeonFeedback);
                return true;
            }
            catch (Exception exception)
            {
                driver.CombatMapObjectChanged -= RelayMapObjectChanged;
                driver.UnitLeftCombat -= RelayUnitLeftCombat;
                driver.Dispose();
                Debug.LogException(exception);
                return false;
            }
        }
        public CombatExecutionDriver GetDriver(CombatSession session) =>
            session != null && drivers.TryGetValue(session, out var driver) ? driver : null;
        public void Focus(CombatSession session)
        {
            if (!seamlessDungeon) if (floatingFeedback != null) floatingFeedback.Bind(null);
            focused = GetDriver(session);
            focused?.BindFeedback();
        }
        public void End(CombatSession session, bool preserveCombatState = false)
        {
            var driver = GetDriver(session);
            if (driver == null) return;
            if (focused == driver)
            {
                if (!seamlessDungeon) if (floatingFeedback != null) floatingFeedback.Bind(null);
                focused = null;
            }
            driver.FeedbackBus.Published -= dungeonFeedback.Publish;
            driver.CombatMapObjectChanged -= RelayMapObjectChanged;
            driver.UnitLeftCombat -= RelayUnitLeftCombat;
            driver.Dispose(preserveCombatState);
            drivers.Remove(session);
            // Exploration continues publishing to the same scene-wide bus after the last fight.
        }
        private void RelayUnitLeftCombat(CombatSession session, BattleUnit unit) => UnitLeftCombat?.Invoke(session, unit);
        private void RelayMapObjectChanged(CombatMapObject value) => CombatMapObjectChanged?.Invoke(value);
        private void Update()
        {
            if (IsGameplaySuspended) return;
            foreach (var driver in new List<CombatExecutionDriver>(drivers.Values))
                driver.Tick(driver == focused);
            if (!seamlessDungeon || focused == null || DevConsoleView.IsOpen) return;
            if (Input.GetKeyDown(KeyCode.E)) focused.RequestEndTurn();
            if (!Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedTextInput()
                && !Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedContextMenu()
                && Godmist.Views.UI.Workspace.UIWorkspaceWindowManager.Instance?.HasOpenWindows != true
                && !Input.GetKeyDown(KeyCode.E)
                && Input.GetKeyDown(KeyCode.X)) focused.RequestEscape();
        }
        private void OnDestroy()
        {
            if (floatingFeedback != null) floatingFeedback.Bind(null);
            foreach (var driver in drivers.Values)
            {
                driver.FeedbackBus.Published -= dungeonFeedback.Publish;
                driver.CombatMapObjectChanged -= RelayMapObjectChanged;
                driver.UnitLeftCombat -= RelayUnitLeftCombat;
                driver.Dispose();
            }
            drivers.Clear();
            focused = null;
        }
        public void SyncDebugConsumablesToActiveUnit() => focused?.SyncDebugConsumablesToActiveUnit();
        public bool CanEndTurn() => focused?.CanEndTurn() ?? false;
        public bool CanEscape() => focused?.CanEscape() ?? false;
        public string GetEscapeBlockReason() => focused?.GetEscapeBlockReason() ?? "Brak aktywnej walki.";
        public void RequestEscape() { if (!IsGameplaySuspended) focused?.RequestEscape(); }
        public void RequestEndTurn() { if (!IsGameplaySuspended) focused?.RequestEndTurn(); }
        public bool TryForceOutcome(CombatOutcomeState outcome) => focused?.TryForceOutcome(outcome) ?? false;
        public Dictionary<GridPosition, List<GridPosition>> GetActiveUnitMovementPaths() => focused?.GetActiveUnitMovementPaths() ?? new();
        public Dictionary<GridPosition, List<GridPosition>> GetUnitNextTurnMovementPaths(BattleUnit unit) => focused?.GetUnitNextTurnMovementPaths(unit) ?? new();
        public int GetActiveUnitMovementPathCost(IReadOnlyList<GridPosition> path) => focused?.GetActiveUnitMovementPathCost(path) ?? 0;
        public bool TryMoveActiveUnitAlongPath(
            IReadOnlyList<GridPosition> path) => focused?.TryMoveActiveUnitAlongPath(path) ?? false;
        public bool TryExecuteSkill(
            SkillData skill,
            GridPosition? selectedPosition) => TryExecuteSkill(skill, new Dictionary<int, GridPosition?> { [0] = selectedPosition }, null, null);
        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex) => TryExecuteSkill(skill, selectedPositionsByModeIndex, null, null);
        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination) => TryExecuteSkill(skill, selectedPositionsByModeIndex, teleportDestination, null);
        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination,
            IReadOnlyDictionary<int, int> rotationsByModeIndex = null,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosenPositionsByModeIndex = null) => IsOpeningPreview
                ? commitOpening?.Invoke(skill, selectedPositionsByModeIndex, teleportDestination, objectPlacementDestination, rotationsByModeIndex, chosenPositionsByModeIndex) == true
                : focused?.TryExecuteSkill(skill, selectedPositionsByModeIndex, teleportDestination, objectPlacementDestination, rotationsByModeIndex, chosenPositionsByModeIndex) ?? false;
        public bool TryInteractAt(GridPosition position) => focused?.TryInteractAt(position) ?? false;
        public bool TryInteractWithDoor(string objectId) => focused?.TryInteractWithDoor(objectId) ?? false;
        public CombatMapObject GetCombatMapObject(string objectId) => focused?.GetCombatMapObject(objectId);
        public bool TryReviveAt(GridPosition position) => focused?.TryReviveAt(position) ?? false;
        public IReadOnlyList<BattleUnit> GetSkillTargets(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex,
            GridPosition? selectedPosition,
            int rotationQuarterTurns = 0) => (focused ?? openingPreview)?.GetSkillTargets(skill, targetMode, targetModeIndex, selectedPosition, rotationQuarterTurns) ?? Array.Empty<BattleUnit>();
        public IReadOnlyList<GridPosition> GetSelectableSkillPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex) => (focused ?? openingPreview)?.GetSelectableSkillPositions(skill, targetMode, targetModeIndex) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetAbsoluteSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode) => (focused ?? openingPreview)?.GetAbsoluteSkillRangePositions(skill, targetMode) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetRelativeSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode, GridPosition center, int rotationQuarterTurns = 0) => (focused ?? openingPreview)?.GetRelativeSkillRangePositions(skill, targetMode, center, rotationQuarterTurns) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetDirectionalSkillRangePositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            GridPosition directionReference,
            int rotationQuarterTurns = 0) => (focused ?? openingPreview)?.GetDirectionalSkillRangePositions(skill, targetMode, directionReference, rotationQuarterTurns) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetTeleportDestinationPositions(
            SkillData skill,
            SkillEffectData teleportEffect,
            GridPosition selectedTargetPosition) => (focused ?? openingPreview)?.GetTeleportDestinationPositions(skill, teleportEffect, selectedTargetPosition) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetObjectPlacementPositions(
            SkillData skill,
            SkillEffectData createObjectEffect) => (focused ?? openingPreview)?.GetObjectPlacementPositions(skill, createObjectEffect) ?? Array.Empty<GridPosition>();
        public IReadOnlyList<GridPosition> GetEligibleSkillTargetPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex) => (focused ?? openingPreview)?.GetEligibleSkillTargetPositions(skill, targetMode, targetModeIndex) ?? Array.Empty<GridPosition>();
    }
}
