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
    public sealed class CombatExecutionDriver : IDisposable
    {
        private readonly CharacterDefinition jerzyDefinition;
        private readonly CharacterDefinition allyDefinition;
        private readonly CharacterDefinition enemyDefinition;
        private readonly CharacterClassData scoutClass;
        private readonly CharacterClassData warriorClass;
        private readonly int gridWidth;
        private readonly int gridHeight;
        private readonly float aiDecisionDelay;
        private readonly float aiMovementResultDelay;
        private readonly float aiSkillResultDelay;
        private readonly CombatBattleLog battleLog;
        private readonly CombatFloatingFeedbackController floatingFeedback;
        private readonly List<GridPosition> blockedPositions;
        private readonly CombatDungeonAreaView dungeonAreaView;
        private readonly CombatCameraFramer cameraFramer;
        private readonly CombatScenarioRunner scenarioRunner;
        private readonly CombatSandboxBootstrap host;
        public CombatExecutionDriver(CombatSandboxBootstrap host, CombatUnitViewSpawner spawner, CharacterDefinition jerzyDefinition, CharacterDefinition allyDefinition, CharacterDefinition enemyDefinition, CharacterClassData scoutClass, CharacterClassData warriorClass, int gridWidth, int gridHeight, float aiDecisionDelay, float aiMovementResultDelay, float aiSkillResultDelay, CombatBattleLog battleLog, CombatFloatingFeedbackController floatingFeedback, List<GridPosition> blockedPositions, CombatDungeonAreaView dungeonAreaView, CombatCameraFramer cameraFramer, CombatScenarioRunner scenarioRunner)
        {
            this.host = host;
            unitViewSpawner = spawner;
            this.jerzyDefinition = jerzyDefinition;
            this.allyDefinition = allyDefinition;
            this.enemyDefinition = enemyDefinition;
            this.scoutClass = scoutClass;
            this.warriorClass = warriorClass;
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.aiDecisionDelay = aiDecisionDelay;
            this.aiMovementResultDelay = aiMovementResultDelay;
            this.aiSkillResultDelay = aiSkillResultDelay;
            this.battleLog = battleLog;
            this.floatingFeedback = floatingFeedback;
            this.blockedPositions = blockedPositions;
            this.dungeonAreaView = dungeonAreaView;
            this.cameraFramer = cameraFramer;
            this.scenarioRunner = scenarioRunner;
        }
        private readonly List<string> logEntries = new();
        public CombatFeedbackBus FeedbackBus => feedbackBus;
        public void BindFeedback()
        {
            if (!Session.IsSeamless) floatingFeedback?.Bind(feedbackBus);
            battleLog?.ClearLog();
            foreach (var entry in logEntries) battleLog?.AddEntry(entry);
        }
        public bool TryJoin(BattleUnit unit, GridPosition position, ISet<GridPosition> reservedArrivals = null)
        {
            if (!DungeonCombatParticipationSystem.TryJoin(Session, unit, position, reservedArrivals)) return false;
            ScoutKnifeSystem.ResetForCombat(Session, true);
            if (Session.ActiveParticipant == null) turnSystem.AdvanceTurn(Session);
            return true;
        }
        public bool TryLeave(BattleUnit unit)
        {
            if (IsPlayerMovementPresentationInProgress) return false;
            if (!DungeonCombatParticipationSystem.TryLeave(Session, unit)) return false;
            summonSystem?.RemoveSummonsCreatedBy(Session, unit.Character.CharacterId);
            skillExecutionSystem?.CancelScheduledEffectsByCaster(Session, unit.Character.CharacterId);
            outcomeSystem.EvaluateCombatOutcome(Session);
            if (Session.Outcome == CombatOutcomeState.Ongoing && Session.ActiveParticipant == null)
                turnSystem.AdvanceTurn(Session);
            return true;
        }
        // Systems
        [field: NonSerialized] public CombatSession Session { get; private set; }
        private CombatOutcomeSystem outcomeSystem;
        private CombatTurnSystem turnSystem;
        private SimpleCombatAiSystem aiSystem;
        private SkillExecutionSystem skillExecutionSystem;
        private StatusEffectSystem statusEffectSystem;
        private CombatMovementSystem movementSystem;
        private CombatDamageSystem damageSystem;
        private CombatShieldSystem shieldSystem;
        private CombatFeedbackBus feedbackBus;
        private CombatReviveSystem reviveSystem;
        private CombatTargetingService targetingService;
        private ResourceRuleSystem resourceRuleSystem;
        private CombatRecoverySystem recoverySystem;
        private CombatInteractionSystem interactionSystem;
        private CombatEscapeSystem escapeSystem;
        private CombatLineOfSightService lineOfSightService;
        private SummonedObjectSystem summonedObjectSystem;
        private SummonSystem summonSystem;
        private CharacterStatPipelineSystem statPipeline;
        private PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;
        private ModifyStatSystem modifyStatSystem;
        private CombatVariableSystem combatVariableSystem;
        private ModifyVarSystem modifyVarSystem;
        private ModifyResistanceSystem modifyResistanceSystem;
        private PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;
        private PassiveEffectCombatExecutionSystem passiveEffectCombatExecutionSystem;
        private bool hasClearedCombatScopedEffects;
        private readonly CombatRewardSystem rewardSystem = new();
        private readonly DungeonCombatOutcomeSystem dungeonCombatOutcomeSystem = new();
        private bool usesGameSessionCombat;
        private bool hasRequestedExplorationReturn;
        private Coroutine activeAiTurnCoroutine;
        private Coroutine activePlayerMovementPresentationCoroutine;
        private CombatUnitViewSpawner unitViewSpawner;
        public event Action<CombatMapObject> CombatMapObjectChanged;
        public bool IsPlayerMovementPresentationInProgress =>
            activePlayerMovementPresentationCoroutine != null;

        private readonly CharacterInstanceFactory characterInstanceFactory = new();

        public void Initialize(CombatSession suppliedSession = null)
        {
            passiveEffectEventDispatcher = new PassiveEffectCombatEventDispatcher();
            turnSystem = new CombatTurnSystem(
                AddBattleLog,
                HandleUnitTurnBegan,
                HandleUnitTurnEnded,
                HandleSummonedObjectTurnBegan,
                HandleSummonedObjectTurnEnded,
                HandleUnitDied,
                passiveEffectEventDispatcher);
            Session = suppliedSession ?? CreateInitialSession();
            usesGameSessionCombat = suppliedSession != null || usesGameSessionCombat;
            Session.PassiveEffectEventDispatcher = passiveEffectEventDispatcher;
            if (!Session.IsSeamless) ScoutKnifeSystem.ResetForCombat(Session);
            RebuildDungeonCombatArea();
            outcomeSystem = new CombatOutcomeSystem(AddBattleLog);
            escapeSystem = new CombatEscapeSystem(AddBattleLog);
            feedbackBus = new CombatFeedbackBus();
            if (!Session.IsSeamless) floatingFeedback?.Bind(feedbackBus);
            resourceRuleSystem = new ResourceRuleSystem(AddBattleLog, feedbackBus);
            combatVariableSystem = new CombatVariableSystem();
            damageSystem = new CombatDamageSystem(
                AddBattleLog,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                RemoveUnitFromCombat,
                resourceRuleSystem,
                combatVariableSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            recoverySystem = new CombatRecoverySystem(AddBattleLog, feedbackBus);
            shieldSystem = new CombatShieldSystem(AddBattleLog, feedbackBus);
            reviveSystem = new CombatReviveSystem(AddBattleLog);
            statPipeline = new CharacterStatPipelineSystem();

            passiveEffectLifecycleSystem = new PassiveEffectLifecycleSystem(
                AddBattleLog,
                new IPassiveEffectTurnProcessor[]
                {
                    new DamageOverTimePassiveEffectProcessor(damageSystem),
                    new SleepPassiveEffectProcessor(recoverySystem),
                    new PeriodicRecoveryPassiveEffectProcessor(recoverySystem)
                },
                statPipeline);

            modifyStatSystem = new ModifyStatSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            modifyVarSystem = new ModifyVarSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            modifyResistanceSystem = new ModifyResistanceSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            statusEffectSystem = new StatusEffectSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                damageSystem,
                passiveEffectLifecycleSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            passiveEffectCombatExecutionSystem = new PassiveEffectCombatExecutionSystem(
                Session,
                statusEffectSystem,
                passiveEffectLifecycleSystem,
                AddBattleLog);
            lineOfSightService = new CombatLineOfSightService();
            summonedObjectSystem = new SummonedObjectSystem(
                AddBattleLog,
                HandleCombatMapObjectChanged,
                new SummonedObjectEffectExecutor(
                    AddBattleLog,
                    damageSystem,
                    recoverySystem,
                    shieldSystem,
                    statusEffectSystem,
                    modifyStatSystem,
                    modifyVarSystem));
            summonSystem = new SummonSystem(AddBattleLog);
            skillExecutionSystem = new SkillExecutionSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                statusEffectSystem,
                damageSystem,
                recoverySystem,
                shieldSystem,
                lineOfSightService: lineOfSightService,
                modifyStatSystem: modifyStatSystem,
                modifyVarSystem: modifyVarSystem,
                summonedObjectSystem: summonedObjectSystem,
                summonSystem: summonSystem,
                feedbackBus: feedbackBus,
                modifyResistanceSystem: modifyResistanceSystem,
                passiveEffectEventDispatcher: passiveEffectEventDispatcher);
            ConfigurePassiveEffectExecutionCallbacks();
            movementSystem = new CombatMovementSystem(
                AddBattleLog,
                resourceRuleSystem,
                statusEffectSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            interactionSystem = new CombatInteractionSystem(
                AddBattleLog, HandleCombatMapObjectChanged, feedbackBus);
            interactionSystem.InitializeMapObjects(Session);
            targetingService = new CombatTargetingService();
            aiSystem = new SimpleCombatAiSystem(
                battleLog: AddBattleLog, 
                endTurn: session => turnSystem.EndActiveTurn(session), 
                evaluateOutcome: session => outcomeSystem.EvaluateCombatOutcome(session),
                skillExecutionSystem: skillExecutionSystem, targetingService: targetingService,
                movementSystem: movementSystem, lineOfSightService: lineOfSightService,
                interactionSystem: interactionSystem);
            if (!Session.IsSeamless) battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta.");

            if (!Session.IsSeamless) StartTurns();
        }

        public void StartTurns()
        {
            ScoutKnifeSystem.ResetForCombat(Session, Session.IsSeamless);
            turnSystem.InitializeTurnOrder(Session);
        }

        public void Dispose()
        {
            passiveEffectLifecycleSystem?.RemoveCombatScopedEffects(Session);
            if (Session != null) Session.PassiveEffectEventDispatcher = null;
            passiveEffectCombatExecutionSystem?.Dispose();
            passiveEffectCombatExecutionSystem = null;
            if (activeAiTurnCoroutine != null)
            {
                host.StopCoroutine(activeAiTurnCoroutine);
                activeAiTurnCoroutine = null;
            }

            if (activePlayerMovementPresentationCoroutine != null)
            {
                host.StopCoroutine(activePlayerMovementPresentationCoroutine);
                activePlayerMovementPresentationCoroutine = null;
            }
        }
        public void Tick(bool acceptsInput)
        {
            ClearCombatScopedEffectsIfNeeded();
            if (Session?.IsSeamless == true) return;
            if (usesGameSessionCombat && TryReturnToExploration()) return;
            if (!acceptsInput) return;
            if (DevConsoleView.IsOpen) return;
            if (Session == null || Session.ActiveUnit == null || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                RequestEndTurn();
                return;
            }
            
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.K))
            {
                RemoveFirstEnemyForDebug();
                return;
            }
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.L))
            {
                RemoveActivePlayerUnitForDebug();
                return;
            }
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.O))
            {
                DownFirstAllyForDebug();
                return;
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                RequestEscape();
                return;
            }
        }
        private CombatSession CreateSandboxSession()
        {
            var session = new CombatSession
            {
                CombatId = "sandbox.combat.01",
                Outcome = CombatOutcomeState.Ongoing,
                ActionPointsPerSpeed = GameDefinesRuntime.GetFloat(
                    GameDefineIds.ActionPointsPerSpeed, .25f),
                Grid = new BattleGrid(gridWidth, gridHeight),
                Units = new List<BattleUnit>()
            };
            foreach (var blockedPosition in blockedPositions)
            {
                session.Grid.SetBlocked(blockedPosition);
            }
            session.MapObjects.Add(new DoorCombatMapObject(
                "door.sandbox.01",
                new GridPosition(4, 4),
                new GridPosition(4, 5),
                DoorInteractionMode.DirectAndLever));
            session.MapObjects.Add(new LeverCombatMapObject("lever.sandbox.01",
                new GridPosition(4, 3), "door.sandbox.01"));

            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle.high_tree.01",
                new GridPosition(3, 4), blocksMovement: true, blocksLineOfSight: true));
            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle.sandbox.02",
                new GridPosition(5, 4), blocksMovement: true, blocksLineOfSight: false));

            var jerzy = CreateUnit(jerzyDefinition, scoutClass, new GridPosition(1, 1),
                100, 0, 200, ResourceType.Momentum, 60f, true);
            CopyDebugConsumablesFromGameSession(jerzy.Character);
            var ally = CreateUnit(allyDefinition, scoutClass, new GridPosition(1, 2),
                100, 0, 200, ResourceType.Momentum, 55f, true);
            var enemyA = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 4),
                90, 0, 50, ResourceType.Rage, 48f, false);
            var enemyB = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 2),
                90, 0, 50, ResourceType.Rage, 48f, false);
                
            session.Units.Add(jerzy);
            session.Units.Add(ally);
            session.Units.Add(enemyA);
            session.Units.Add(enemyB);

            return session;
        }

        private static void CopyDebugConsumablesFromGameSession(CharacterInstance target)
        {
            var gameSession = GameBootstrap.Instance?.Session;
            var leaderId = gameSession?.PlayerParty?.ActiveLeaderId;
            if (target?.Inventory?.Items == null
                || string.IsNullOrWhiteSpace(leaderId)
                || gameSession?.CharactersById == null
                || !gameSession.CharactersById.TryGetValue(leaderId, out var source)
                || source?.Inventory?.Items == null)
            {
                return;
            }

            // The standalone debug sandbox creates throwaway characters. Keep
            // debug-created consumables usable there by copying the runtime
            // item references from the active game-session leader at startup.
            foreach (var item in source.Inventory.Items)
            {
                if (item is ConsumableItemInstance consumable
                    && consumable.HasCharges
                    && !target.Inventory.Items.Contains(consumable))
                {
                    target.Inventory.Items.Add(consumable);
                }
            }
        }

        public void SyncDebugConsumablesToActiveUnit()
        {
            if (Session?.IsSeamless == true) return;
            if (Session?.ActiveUnit?.IsPlayerSide == true)
            {
                CopyDebugConsumablesFromGameSession(Session.ActiveUnit.Character);
            }
        }

        private void RebuildDungeonCombatArea()
        {
            if (Session?.IsSeamless == true || dungeonAreaView == null) return;

            if (!usesGameSessionCombat)
            {
                dungeonAreaView.ClearArea();
                return;
            }

            var context = GameBootstrap.Instance?
                .Session
                .ActiveDungeonCombatContext;

            if (context == null) return;

            dungeonAreaView.Rebuild(context, host);

            if (dungeonAreaView.TryGetWorldBounds(out var worldBounds))
            {
                cameraFramer?.Frame(worldBounds);
            }
        }

        private BattleUnit CreateUnit(
            CharacterDefinition definition,
            CharacterClassData classData,
            GridPosition position,
            int health,
            int resourceCurrent,
            int resourceMax,
            ResourceType resourceType,
            float speed,
            bool isPlayerSide)
        {
            var instance = characterInstanceFactory.Create(definition, classData);
            instance.CurrentHealth = health;
            instance.WeaponAccuracy = 100f;
            instance.Stats.Speed = new Core.Types.Stat(speed);
            instance.Stats.MaxHealth = new Core.Types.Stat(health);
            instance.Stats.MinAttack = new Core.Types.Stat(8);
            instance.Stats.MaxAttack = new Core.Types.Stat(12);
            instance.Stats.PhysicalDefense = new Core.Types.Stat(10);
            instance.Stats.MagicalDefense = new Core.Types.Stat(10);
            instance.Stats.Dodge = new Core.Types.Stat(15);
            instance.Stats.CritChance = new Core.Types.Stat(0.25f);
            instance.Stats.CritDamage = new Core.Types.Stat(1.5f);
            instance.Stats.ResourceRegeneration = new Core.Types.Stat(0);
            instance.ResourcePool = new ResourcePool(resourceType, resourceCurrent, resourceMax, 0);
            var maxAp = turnSystem.CalculateMaxActionPoints(instance, GameDefinesRuntime.GetFloat(
                GameDefineIds.ActionPointsPerSpeed, .25f));

            return new BattleUnit
            {
                Character = instance,
                Position = position,
                CurrentActionPoints = maxAp,
                MaxActionPoints = maxAp,
                CurrentActionValue = 0f,
                HasReacted = false,
                IsReadyToEscape = false,
                IsPlayerSide = isPlayerSide
            };
        }
        // Debug Commands
        private void RestartCombatForDebug()
        {
            if (usesGameSessionCombat)
            {
                Debug.LogWarning("Restart debugowy jest niedostepny dla walki uruchomionej z eksploracji.");
                return;
            }

            passiveEffectCombatExecutionSystem?.Dispose();
            passiveEffectCombatExecutionSystem = null;
            Session = CreateInitialSession();
            Session.PassiveEffectEventDispatcher = passiveEffectEventDispatcher;
            passiveEffectCombatExecutionSystem = new PassiveEffectCombatExecutionSystem(
                Session,
                statusEffectSystem,
                passiveEffectLifecycleSystem,
                AddBattleLog);
            ConfigurePassiveEffectExecutionCallbacks();
            ScoutKnifeSystem.ResetForCombat(Session, Session.IsSeamless);
            hasClearedCombatScopedEffects = false;

            if (!Session.IsSeamless) battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta ponownie.");

            turnSystem.InitializeTurnOrder(Session);
        }

        private void ConfigurePassiveEffectExecutionCallbacks()
        {
            if (passiveEffectCombatExecutionSystem == null)
                return;

            passiveEffectCombatExecutionSystem.DuplicateActionExecutor =
                skillExecutionSystem == null
                    ? null
                    : skillExecutionSystem.TryExecutePassiveDuplicate;
            passiveEffectCombatExecutionSystem.RuntimeFlagTriggered -=
                HandleRuntimeFlagTriggered;
            passiveEffectCombatExecutionSystem.RuntimeFlagTriggered +=
                HandleRuntimeFlagTriggered;
            if (skillExecutionSystem != null)
            {
                skillExecutionSystem.PassiveActionResolved =
                    passiveEffectCombatExecutionSystem.NotifyActionResolved;
            }
        }

        private void HandleRuntimeFlagTriggered(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (!string.Equals(
                    operation?.ReferenceId,
                    "summon.random-skeleton",
                    System.StringComparison.OrdinalIgnoreCase)
                || owner?.Character == null
                || summonSystem == null)
            {
                return;
            }

            var skeletonDefinition = GameBootstrap.Instance?.ContentCatalog
                ?.CharacterDefinitions?
                .FirstOrDefault(definition => string.Equals(
                    definition?.Id,
                    "skeleton.test",
                    System.StringComparison.OrdinalIgnoreCase));
            if (skeletonDefinition == null)
            {
                Debug.LogWarning(
                    "Nie znaleziono definicji skeleton.test dla pasywki summon.random-skeleton.");
                return;
            }

            if (!summonSystem.TrySummonAdjacent(
                    Session,
                    owner,
                    skeletonDefinition,
                    owner.Character.ClassData,
                    context?.SkillId ?? "passive-summon",
                    durationTurns: 3,
                    maxActiveSummons: 1,
                    maxHealthPercent: .25f,
                    out _))
            {
                AddBattleLog("Nie ma wolnego pola na przyzwanie szkieletu.");
            }
        }

        private void RemoveFirstEnemyForDebug()
        {
            if (Session == null)
            {
                return;
            }

            var enemy = Session.Units
                .FirstOrDefault(unit => unit != null && !unit.IsPlayerSide);

            if (enemy == null)
            {
                Debug.LogWarning("No enemy to remove.");
                return;
            }

            RemoveUnitFromCombat(enemy);
            outcomeSystem.EvaluateCombatOutcome(Session);
        }
        private void RemoveActivePlayerUnitForDebug()
        {
            if (Session?.ActiveUnit == null)
            {
                Debug.LogWarning("No active unit to remove.");
                return;
            }

            if (!Session.ActiveUnit.IsPlayerSide)
            {
                Debug.Log("Debug remove skipped: active unit is not on player side.");
                return;
            }

            var removedUnit = Session.ActiveUnit;
            RemoveUnitFromCombat(removedUnit);
            outcomeSystem.EvaluateCombatOutcome(Session);
        }

        private void RemoveUnitFromCombat(BattleUnit removedUnit)
        {
            if (Session == null || removedUnit == null)
            {
                return;
            }

            var removedWasActive = Session.ActiveUnit == removedUnit;

            if (removedUnit.Character?.LifeState == CharacterLifeState.Dead)
            {
                summonSystem?.RemoveSummonsCreatedBy(
                    Session,
                    removedUnit.Character.CharacterId);
                skillExecutionSystem?.CancelScheduledEffectsByCaster(
                    Session,
                    removedUnit.Character.CharacterId);
            }

            Session.Units.Remove(removedUnit);
            Session.TurnParticipants ??= new();
            Session.TurnParticipants?.RemoveAll(
                participant => participant?.Unit == removedUnit);
            Session.TurnOrder = Session.TurnParticipants
                .Where(participant => participant?.Unit != null)
                .Select(participant => participant.Unit)
                .ToList();

            if (Session.TurnParticipants.Count == 0)
            {
                Session.ActiveParticipant = null;
                Session.ActiveUnit = null;
                Session.ActiveTurnIndex = 0;
                return;
            }

            if (removedWasActive)
            {
                Session.ActiveParticipant = null;
                Session.ActiveUnit = null;
                Session.ActiveSummonedObject = null;
                Session.ActiveTurnIndex = -1;
                turnSystem.AdvanceTurn(Session);
            }
            else
            {
                var activeIndex = Session.TurnOrder.IndexOf(Session.ActiveUnit);

                if (activeIndex >= 0)
                {
                    Session.ActiveTurnIndex = activeIndex;
                }
                else
                {
                    Session.ActiveUnit = Session.TurnOrder.Count > 0
                        ? Session.TurnOrder[0]
                        : null;
                    Session.ActiveTurnIndex = Session.ActiveUnit == null
                        ? -1
                        : 0;
                }

                Session.ActiveParticipant = Session.TurnParticipants
                    .FirstOrDefault(
                        participant => participant?.Unit == Session.ActiveUnit);
            }
        }
        private void AddBattleLog(string message)
        {
            logEntries.Add(message);
            if (logEntries.Count > 300) logEntries.RemoveAt(0);
            if (host.Session == Session) battleLog?.AddEntry(message);
        }
        private void HandleCombatMapObjectChanged(CombatMapObject mapObject)
        {
            CombatMapObjectChanged?.Invoke(mapObject);
        }
        private void HandleUnitTurnBegan(BattleUnit unit)
        {
            if (unit == null || Session == null)
            {
                return;
            }
            resourceRuleSystem?.ProcessTurnStart(unit);
            skillExecutionSystem?.ProcessToggleUpkeep(Session, unit);
            statusEffectSystem?.ProcessTurnStart(Session, unit);
            CombatTurnSystem.RefreshMaxActionPoints(
                Session,
                unit,
                refillCurrentActionPoints: true);

            if (Session.Outcome != CombatOutcomeState.Ongoing
                || !Session.Units.Contains(unit))
            {
                return;
            }

            if (unit.Character == null
                || unit.Character.LifeState != CharacterLifeState.Alive)
            {
                turnSystem.AdvanceTurn(Session);
                return;
            }
            if (TryGetTurnBlockingStatus(unit, out var blockingStatusLabel))
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit,
                    CombatFeedbackType.TurnSkipped,
                    effectName: blockingStatusLabel));
                AddBattleLog(
                    $"{unit.Character.Definition.DisplayName} " +
                    "jest pod wpływem twardej kontroli i traci turę.");

                turnSystem.EndActiveTurn(Session);
                return;
            }

            if (!unit.IsPlayerSide)
            {
                activeAiTurnCoroutine = host.StartCoroutine(ExecuteAiTurnVisually(unit));
            }
        }

        private static bool TryGetTurnBlockingStatus(
            BattleUnit unit,
            out string statusLabel)
        {
            if (StatusEffectSystem.IsStunned(unit))
            {
                statusLabel = "Ogłuszony";
                return true;
            }

            if (StatusEffectSystem.IsFrozen(unit))
            {
                statusLabel = "Zamrożony";
                return true;
            }

            if (StatusEffectSystem.IsParalyzed(unit))
            {
                statusLabel = "Sparaliżowany";
                return true;
            }

            if (StatusEffectSystem.IsSleeping(unit))
            {
                statusLabel = "Śpiący";
                return true;
            }

            statusLabel = string.Empty;
            return false;
        }

        private IEnumerator ExecuteAiTurnVisually(BattleUnit unit)
        {
            var turnState = aiSystem?.CreateTurnState();
            while (Session != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit == unit
                && unit?.Character?.LifeState == CharacterLifeState.Alive
                && unit.CurrentActionPoints > 0
                && turnState != null)
            {
                yield return WaitForAiPresentation(aiDecisionDelay);

                if (Session.Outcome != CombatOutcomeState.Ongoing
                    || Session.ActiveUnit != unit
                    || unit.Character.LifeState != CharacterLifeState.Alive)
                {
                    break;
                }

                if (!aiSystem.TryExecuteNextAction(
                        Session,
                        unit,
                        turnState,
                        out var action))
                {
                    break;
                }

                if (action.Type == SimpleCombatAiActionType.Movement)
                {
                    yield return PlayMovementPath(unit, action.MovementPath);
                }

                var resultDelay = action.Type == SimpleCombatAiActionType.Skill
                    ? aiSkillResultDelay
                    : aiMovementResultDelay;
                yield return WaitForAiPresentation(resultDelay);
            }

            activeAiTurnCoroutine = null;
            if (Session != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit == unit)
            {
                turnSystem.EndActiveTurn(Session);
            }
        }

        private static IEnumerator WaitForAiPresentation(float duration)
        {
            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return null;
            }
        }

        private IEnumerator PlayMovementPath(
            BattleUnit unit,
            IReadOnlyList<GridPosition> path)
        {
            if (unitViewSpawner == null
                || path == null
                || path.Count == 0
                || !unitViewSpawner.TryBeginPresentationMovement(unit))
            {
                yield break;
            }

            foreach (var position in path)
            {
                if (Session == null
                    || Session.Outcome != CombatOutcomeState.Ongoing
                    || !unitViewSpawner.TryMovePresentationStep(unit, position))
                {
                    break;
                }

                while (unitViewSpawner.IsPresentationMovementInProgress(unit))
                {
                    yield return null;
                }
            }

            unitViewSpawner.EndPresentationMovement(unit);
        }

        private IEnumerator PlayPlayerMovementPath(
            BattleUnit unit,
            IReadOnlyList<GridPosition> path)
        {
            yield return PlayMovementPath(unit, path);
            activePlayerMovementPresentationCoroutine = null;
        }

        private void HandleUnitTurnEnded(BattleUnit unit)
        {
            statusEffectSystem?.ProcessTurnEnd(unit);
            skillExecutionSystem?.ProcessScheduledEffects(Session, unit);
            summonSystem?.AdvanceDurationAtTurnEnd(Session, unit);
            CombatTurnSystem.RefreshMaxActionPoints(Session, unit);
        }

        private void HandleUnitDied(BattleUnit unit)
        {
            if (Session == null || unit?.Character == null)
            {
                return;
            }

            summonSystem?.RemoveSummonsCreatedBy(
                Session,
                unit.Character.CharacterId);
            skillExecutionSystem?.CancelScheduledEffectsByCaster(
                Session,
                unit.Character.CharacterId);
            skillExecutionSystem?.DeactivateAllToggles(Session, unit);
        }

        private void HandleSummonedObjectTurnBegan(
            SummonedCombatMapObject summonedObject)
        {
            if (summonedObject == null || Session == null)
            {
                return;
            }

            AddBattleLog(
                $"{summonedObject.Definition?.DisplayName ?? summonedObject.ObjectId} " +
                "wykonuje automatyczna ture.");

            summonedObjectSystem.ExecuteTurnEffects(
                Session,
                summonedObject);

            turnSystem.EndActiveTurn(Session);
        }

        private void HandleSummonedObjectTurnEnded(
            SummonedCombatMapObject summonedObject)
        {
            if (summonedObject == null || Session == null)
            {
                return;
            }

            if (!summonedObject.AdvanceDuration())
            {
                return;
            }

            summonedObjectSystem?.Remove(Session, summonedObject);
        }

        private bool IsActiveUnitHardCrowdControlled()
        {
            return Session?.ActiveUnit != null
                && StatusEffectSystem.IsHardCrowdControlled(Session.ActiveUnit);
        }

        public bool CanEndTurn()
        {
            return Session != null
                && Session.ActiveUnit != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit.IsPlayerSide
                && !IsPlayerMovementPresentationInProgress;
        }

        public bool CanEscape()
        {
            return Session != null && !Session.IsSeamless && Session.ActiveUnit != null && Session.ActiveUnit.IsPlayerSide 
                && Session.ActiveUnit.Character != null 
                && Session.ActiveUnit.Character.LifeState == CharacterLifeState.Alive
                && Session.Outcome == CombatOutcomeState.Ongoing
                && !IsPlayerMovementPresentationInProgress;
        }

        public void RequestEscape()
        {
            if (!CanEscape() || escapeSystem == null) return;

            var actor = Session.ActiveUnit;

            if (IsActiveUnitHardCrowdControlled())
            {
                AddBattleLog(
                    $"{actor.Character.Definition.DisplayName} " +
                    "nie może uciec przez status kontroli.");

                return;
            }

            if (!escapeSystem.TryEscape(Session, actor)) return;

            if (Session.Outcome == CombatOutcomeState.Ongoing)
            {
                turnSystem.EndActiveTurn(Session);
            }
        }

        public void RequestEndTurn()
        {
            if (!CanEndTurn())
            {
                return;
            }

            turnSystem.EndActiveTurn(Session);
        }

        public bool TryForceOutcome(CombatOutcomeState outcome)
        {
            if (Session == null
                || Session.Outcome != CombatOutcomeState.Ongoing
                || outcome == CombatOutcomeState.Ongoing)
            {
                return false;
            }

            Session.Outcome = outcome;
            return true;
        }

        public Dictionary<GridPosition, List<GridPosition>>
            GetActiveUnitMovementPaths()
        {
            if (Session == null
                || Session.ActiveUnit == null
                || !Session.ActiveUnit.IsPlayerSide
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return new Dictionary<
                    GridPosition,
                    List<GridPosition>>();
            }

            return movementSystem.CalculateReachablePaths(
                Session,
                Session.ActiveUnit);
        }

        public Dictionary<GridPosition, List<GridPosition>>
            GetUnitNextTurnMovementPaths(BattleUnit unit)
        {
            if (Session == null
                || unit == null
                || !Session.Units.Contains(unit)
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return new Dictionary<GridPosition, List<GridPosition>>();
            }

            return movementSystem.CalculateReachablePathsForNextTurn(Session, unit);
        }

        public int GetActiveUnitMovementPathCost(IReadOnlyList<GridPosition> path)
        {
            if (path == null || Session?.ActiveUnit == null) return 0;
            return CombatMovementSystem.CalculatePathCost(Session, Session.ActiveUnit.Position,
                Session.ActiveUnit.MovementStepsThisTurn, path);
        }

        public bool TryMoveActiveUnitAlongPath(
            IReadOnlyList<GridPosition> path)
        {
            if (Session == null
                || Session.ActiveUnit == null
                || !Session.ActiveUnit.IsPlayerSide
                || Session.Outcome != CombatOutcomeState.Ongoing
                || IsPlayerMovementPresentationInProgress)
            {
                return false;
            }

            var unit = Session.ActiveUnit;
            if (!movementSystem.TryMoveAlongPath(
                Session,
                unit,
                path))
            {
                return false;
            }

            activePlayerMovementPresentationCoroutine = host.StartCoroutine(
                PlayPlayerMovementPath(unit, path));
            return true;
        }
        public bool TryExecuteSkill(
            SkillData skill,
            GridPosition? selectedPosition)
        {
            if (skill?.TargetModes == null || skill.TargetModes.Count != 1)
            {
                Debug.LogWarning(
                    $"Skill '{skill?.SkillId}' wymaga wielu celów. " +
                    "Użyj wariantu z selectedPositionsByModeIndex.");

                return false;
            }

            var selectedPositionsByModeIndex =
                new Dictionary<int, GridPosition?>
                {
                    [0] = selectedPosition
                };

            return TryExecuteSkill(skill, selectedPositionsByModeIndex);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex)
        {
            return TryExecuteSkill(
                skill,
                selectedPositionsByModeIndex,
                null,
                null);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination)
        {
            return TryExecuteSkill(
                skill,
                selectedPositionsByModeIndex,
                teleportDestination,
                null);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination,
            IReadOnlyDictionary<int, int> rotationsByModeIndex = null)
        {
            if (Session == null || Session.ActiveUnit == null
                || skillExecutionSystem == null || targetingService == null
                || IsPlayerMovementPresentationInProgress)
            {
                return false;
            }

            if (skill?.IsToggle == true
                && Session.ActiveUnit.ActiveToggleSkillIds != null
                && Session.ActiveUnit.ActiveToggleSkillIds.Contains(skill.SkillId))
            {
                return skillExecutionSystem.TryExecute(
                    Session,
                    Session.ActiveUnit,
                    skill,
                    new Dictionary<int, IReadOnlyList<BattleUnit>>(),
                    null,
                    null,
                    new Dictionary<int, GridPosition?>());
            }

            if (!TryBuildSkillTargetGroups(
                    skill,
                    selectedPositionsByModeIndex,
                    rotationsByModeIndex,
                    out var targetsByModeIndex))
            {
                return false;
            }

            return skillExecutionSystem.TryExecute(
                Session,
                Session.ActiveUnit,
                skill,
                targetsByModeIndex,
                teleportDestination,
                objectPlacementDestination,
                selectedPositionsByModeIndex);
        }

        private bool TryBuildSkillTargetGroups(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            IReadOnlyDictionary<int, int> rotationsByModeIndex,
            out Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            targetsByModeIndex =
                new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill == null || selectedPositionsByModeIndex == null)
            {
                return false;
            }

            if (IsObjectPlacementOnlySkill(skill))
            {
                return true;
            }

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                return false;
            }

            for (var targetModeIndex = 0;
                targetModeIndex < skill.TargetModes.Count;
                targetModeIndex++)
            {
                var targetMode = skill.TargetModes[targetModeIndex];

                if (targetMode == null)
                {
                    return false;
                }

                if (!selectedPositionsByModeIndex.TryGetValue(
                        targetModeIndex,
                        out var selectedPosition))
                {
                    if (targetMode.TargetType != TargetType.Caster)
                    {
                        return false;
                    }

                    selectedPosition = null;
                }

                var targets = GetSkillTargets(
                    skill,
                    targetMode,
                    targetModeIndex,
                    selectedPosition,
                    rotationsByModeIndex != null
                        && rotationsByModeIndex.TryGetValue(targetModeIndex, out var rotation)
                        ? rotation
                        : 0);

                if (targets.Count == 0
                    && targetMode.TargetType != TargetType.AreaAroundTile
                    && targetMode.TargetType != TargetType.AreaAroundCaster)
                {
                    return false;
                }

                targetsByModeIndex.Add(targetModeIndex, targets);
            }

            return true;
        }

        public bool TryInteractAt(GridPosition position)
        {
            if (Session == null || Session.ActiveUnit == null || interactionSystem == null 
                || !Session.ActiveUnit.IsPlayerSide || Session.Outcome != CombatOutcomeState.Ongoing) return false;

            if (IsActiveUnitHardCrowdControlled())
            {
                AddBattleLog("Nie można interagować przez status kontroli.");
                return false;
            }

            if (!interactionSystem.TryInteract(Session, Session.ActiveUnit, position)) return false;

            turnSystem.EndActiveTurn(Session);

            return true;
        }

        public CombatMapObject GetCombatMapObject(string objectId)
        {
            return Session?.GetMapObject(objectId);
        }

        public bool TryReviveAt(GridPosition position)
        {
            if (Session == null
                || Session.ActiveUnit == null
                || reviveSystem == null
                || !Session.ActiveUnit.IsPlayerSide
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return false;
            }

            if (IsActiveUnitHardCrowdControlled())
            {
                AddBattleLog("Nie można podnieść sojusznika przez status kontroli.");
                return false;
            }

            var target = Session.Units.FirstOrDefault(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit.Position == position
                    && unit.Character != null
                    && unit.Character.LifeState
                        == CharacterLifeState.Downed);

            if (target == null)
            {
                return false;
            }

            var revived = reviveSystem.TryRevive(
                Session,
                Session.ActiveUnit,
                target);

            if (!revived)
            {
                return false;
            }

            turnSystem.EndActiveTurn(Session);
            return true;
        }
        private void DownFirstAllyForDebug()
        {
            if (Session == null)
            {
                return;
            }

            var ally = Session.Units.FirstOrDefault(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit != Session.ActiveUnit
                    && unit.Character != null
                    && unit.Character.LifeState
                        == CharacterLifeState.Alive);

            if (ally == null)
            {
                return;
            }

            ally.Character.CurrentHealth = 0;
            ally.Character.LifeState = CharacterLifeState.Downed;
            ally.DownedTurnsRemaining = BattleUnit.DownedTurnLimit;

            AddBattleLog(
                $"{ally.Character.Definition.DisplayName} " +
                "zostaje powalony testowo.");
        }
        public IReadOnlyList<BattleUnit> GetSkillTargets(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex,
            GridPosition? selectedPosition,
            int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<BattleUnit>();
            }

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var targets = targetingService.GetTargets(
                Session,
                Session.ActiveUnit,
                targetMode,
                selectedPosition,
                includeDownedTargets,
                requiredEnemyTarget,
                rotationQuarterTurns);

            return FilterTargetsByLineOfSight(skill, targets);
        }
        public IReadOnlyList<GridPosition> GetSelectableSkillPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var positions = targetingService.GetSelectablePositions(
                Session,
                Session.ActiveUnit,
                targetMode,
                includeDownedTargets,
                requiredEnemyTarget);

            if (targetMode.TargetType == TargetType.AreaAroundCaster
                && CombatTargetingService.IsDirectionalShape(
                    targetMode.AbsoluteRange?.Shape ?? RangeShape.None))
            {
                return positions;
            }

            return FilterPositionsByLineOfSight(skill, positions);
        }
        public IReadOnlyList<GridPosition> GetAbsoluteSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var positions = targetingService.GetAbsoluteRangePositions(Session, Session.ActiveUnit, targetMode);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetRelativeSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode, GridPosition center, int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var positions = targetingService.GetRelativeRangePositions(Session, Session.ActiveUnit, 
                targetMode, center, rotationQuarterTurns);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetDirectionalSkillRangePositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            GridPosition directionReference,
            int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var positions = targetingService.GetDirectionalRangePositions(
                Session,
                Session.ActiveUnit,
                targetMode.AbsoluteRange,
                directionReference,
                null,
                rotationQuarterTurns);
            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetTeleportDestinationPositions(
            SkillData skill,
            SkillEffectData teleportEffect,
            GridPosition selectedTargetPosition)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || teleportEffect == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var destinationPositions = targetingService.GetPositionsInRange(
                Session,
                selectedTargetPosition,
                teleportEffect.TeleportDestinationRange);

            var validPositions = new List<GridPosition>();

            foreach (var position in destinationPositions)
            {
                if (position == selectedTargetPosition
                    || !Session.Grid.IsWalkable(position)
                    || Session.Grid.IsBlocked(position)
                    || IsOccupiedByLivingUnit(position))
                {
                    continue;
                }

                if (skill.RequiresLineOfSight
                    && !lineOfSightService.HasLineOfSight(
                        Session,
                        Session.ActiveUnit.Position,
                        position))
                {
                    continue;
                }

                validPositions.Add(position);
            }

            return validPositions;
        }

        public IReadOnlyList<GridPosition> GetObjectPlacementPositions(
            SkillData skill,
            SkillEffectData createObjectEffect)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || createObjectEffect == null
                || (createObjectEffect.EffectType == EffectType.CreateObject
                    && createObjectEffect.SummonedObjectDefinition == null)
                || (createObjectEffect.EffectType == EffectType.Summon
                    && (createObjectEffect.SummonedCharacterDefinition == null
                        || createObjectEffect.SummonedClassData == null))
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var positions = targetingService.GetPositionsInRange(
                Session,
                Session.ActiveUnit.Position,
                createObjectEffect.EffectType == EffectType.Summon
                    ? createObjectEffect.SummonPlacementRange
                    : createObjectEffect.ObjectPlacementRange);

            var validPositions = new List<GridPosition>();

            foreach (var position in positions)
            {
                if (!Session.Grid.IsWalkable(position)
                    || Session.Grid.IsBlocked(position)
                    || (createObjectEffect.EffectType == EffectType.Summon
                        && Session.IsPositionOccupied(position))
                    || Session.IsPositionOccupiedByMapObject(position))
                {
                    continue;
                }

                if (skill.RequiresLineOfSight
                    && !lineOfSightService.HasLineOfSight(
                        Session,
                        Session.ActiveUnit.Position,
                        position))
                {
                    continue;
                }

                validPositions.Add(position);
            }

            return validPositions;
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsOccupiedByLivingUnit(GridPosition position)
        {
            foreach (var unit in Session.Units)
            {
                if (unit == null || unit.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<GridPosition> GetEligibleSkillTargetPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var positions = targetingService.GetEligibleTargetPositions(
                Session,
                Session.ActiveUnit,
                targetMode,
                includeDownedTargets,
                requiredEnemyTarget);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        private static bool CanTargetModeIncludeDownedTargets(
            SkillData skill,
            int targetModeIndex)
        {
            if (skill?.Effects == null || targetModeIndex < 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect == null
                    || effect.TargetModeIndex != targetModeIndex)
                {
                    continue;
                }

                if (effect.EffectType == EffectType.Heal
                    || effect.EffectType == EffectType.HealPercent
                    || effect.EffectType == EffectType.Teleport)
                {
                    return true;
                }
            }

            return false;
        }

        private BattleUnit GetRequiredEnemyTarget(
            SkillTargetModeData targetMode)
        {
            if (targetMode?.TargetingType != TargetingType.Enemies)
            {
                return null;
            }

            return StatusEffectSystem.TryGetTaunter(
                Session,
                Session.ActiveUnit,
                out var taunter)
                ? taunter
                : null;
        }

        private IReadOnlyList<BattleUnit> FilterTargetsByLineOfSight(SkillData skill, 
            IReadOnlyList<BattleUnit> targets)
        {
            if (skill == null || !skill.RequiresLineOfSight || Session?.ActiveUnit == null) return targets;

            return targets.Where(target => target != null && 
                lineOfSightService.HasLineOfSight(Session, Session.ActiveUnit.Position, target.Position)).ToList();
        }

        private IReadOnlyList<GridPosition> FilterPositionsByLineOfSight(SkillData skill,
            IReadOnlyList<GridPosition> positions)
        {
            if (skill == null || !skill.RequiresLineOfSight || Session?.ActiveUnit == null) return positions;

            return positions.Where(position => 
                lineOfSightService.HasLineOfSight(Session, Session.ActiveUnit.Position, position)).ToList();
        }

        private CombatSession CreateInitialSession()
        {
            var gameSession = GameBootstrap.Instance?.Session;

            if (gameSession?.ActiveCombat != null)
            {
                usesGameSessionCombat = true;
                return gameSession.ActiveCombat;
            }

            usesGameSessionCombat = false;

            if (scenarioRunner != null
                && scenarioRunner.TryCreateSession(
                    gameSession,
                    GameDefinesRuntime.GetFloat(GameDefineIds.ActionPointsPerSpeed, .25f),
                    out var encounterSession))
            {
                return encounterSession;
            }

            return CreateSandboxSession();
        }

        private static void RestoreDungeonPositionAfterEscape(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (gameSession?.ActiveDungeon == null || combatSession == null) return;

            if (!gameSession.ActiveDungeon.TryGetCurrentFloor(out var floor)) return;

            if (!floor.TryGetEncounter(
                    combatSession.CombatId,
                    out var encounter))
            {
                return;
            }

            if (!encounter.HasRetreatPosition) return;

            gameSession.ActiveDungeon.SetPlayerPosition(
                new Vector2(
                    encounter.RetreatPosition.x,
                    encounter.RetreatPosition.y));
        }

        private void ClearCombatScopedEffectsIfNeeded()
        {
            if (hasClearedCombatScopedEffects
                || Session == null
                || Session.Outcome == CombatOutcomeState.Ongoing)
            {
                return;
            }

            passiveEffectLifecycleSystem?.RemoveCombatScopedEffects(Session);
            hasClearedCombatScopedEffects = true;
        }

        private bool TryReturnToExploration()
        {
            if (hasRequestedExplorationReturn || Session == null)
            {
                return false;
            }

            var outcome = Session.Outcome;
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null)
            {
                return false;
            }

            var gameSession = gameBootstrap.Session;

            if (outcome == CombatOutcomeState.Defeat)
            {
                if (Session.IsDebugSession)
                {
                    return TryFinishDebugCombat(
                        gameBootstrap,
                        gameSession);
                }

                var restarted = gameBootstrap.TryRestartAfterDefeat();

                if (!restarted)
                {
                    return false;
                }

                hasRequestedExplorationReturn = true;
                return true;
            }

            if (outcome != CombatOutcomeState.Victory
                && outcome != CombatOutcomeState.Escaped)
            {
                return false;
            }

            if (Session.IsDebugSession)
            {
                return TryFinishDebugCombat(
                    gameBootstrap,
                    gameSession);
            }

            if (!dungeonCombatOutcomeSystem.TryApplyOutcome(
                    gameSession,
                    Session))
            {
                Debug.LogError(
                    "CombatSandboxBootstrap: nie udalo sie zapisac wyniku walki w stanie lochu.");

                return false;
            }

            if (outcome == CombatOutcomeState.Victory)
            {
                rewardSystem.TryGrantVictoryRewards(gameSession, Session);
            }

            if (outcome == CombatOutcomeState.Escaped)
            {
                rewardSystem.TryApplyEscapePenalty(gameSession, Session);
                RestoreDungeonPositionAfterEscape(gameSession, Session);
            }

            if (!gameBootstrap.TryReturnFromCombat())
            {
                return false;
            }

            hasRequestedExplorationReturn = true;
            gameSession.TryClearActiveCombat();

            return true;
        }

        private bool TryFinishDebugCombat(
            GameBootstrap gameBootstrap,
            GameSession gameSession)
        {
            if (!gameBootstrap.TryReturnFromCombat())
            {
                return false;
            }

            hasRequestedExplorationReturn = true;
            gameSession.TryClearActiveCombat();

            return true;
        }
    }
}
