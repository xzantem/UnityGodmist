using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
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

        [Header("AP Config")]
        [SerializeField] private float actionPointsPerSpeed = 0.25f;

        [Header("Debug References")]
        [SerializeField] private CombatBattleLog battleLog;

        [Header("Debug Obstacles")]
        [SerializeField] private List<GridPosition> blockedPositions = new();
        [Header("Dungeon Combat Area")]
        [SerializeField] private CombatDungeonAreaView dungeonAreaView;
        [SerializeField] private CombatCameraFramer cameraFramer;
        [Header("Scenario Runner")]
        [SerializeField] private CombatScenarioRunner scenarioRunner;
        

        // Systems
        public CombatSession Session {get; private set;}
        private CombatOutcomeSystem outcomeSystem;
        private CombatTurnSystem turnSystem;
        private SimpleCombatAiSystem aiSystem;
        private SkillExecutionSystem skillExecutionSystem;
        private StatusEffectSystem statusEffectSystem;
        private CombatMovementSystem movementSystem;
        private CombatDamageSystem damageSystem;
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
        private bool hasClearedCombatScopedEffects;
        private readonly CombatRewardSystem rewardSystem = new();
        private readonly DungeonCombatOutcomeSystem dungeonCombatOutcomeSystem = new();
        private bool usesGameSessionCombat;
        private bool hasRequestedExplorationReturn;
        public event Action<CombatMapObject> CombatMapObjectChanged;

        private readonly CharacterInstanceFactory characterInstanceFactory = new();

        private void Start()
        {
            turnSystem = new CombatTurnSystem(
                AddBattleLog,
                HandleUnitTurnBegan,
                HandleUnitTurnEnded,
                HandleSummonedObjectTurnBegan,
                HandleSummonedObjectTurnEnded,
                HandleUnitDied);
            Session = CreateInitialSession();
            RebuildDungeonCombatArea();
            outcomeSystem = new CombatOutcomeSystem(AddBattleLog);
            escapeSystem = new CombatEscapeSystem(AddBattleLog);
            resourceRuleSystem = new ResourceRuleSystem(AddBattleLog);
            combatVariableSystem = new CombatVariableSystem();
            damageSystem = new CombatDamageSystem(
                AddBattleLog,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                RemoveUnitFromCombat, resourceRuleSystem, combatVariableSystem);
            recoverySystem = new CombatRecoverySystem(AddBattleLog);
            reviveSystem = new CombatReviveSystem(AddBattleLog);
            statPipeline = new CharacterStatPipelineSystem();

            passiveEffectLifecycleSystem = new PassiveEffectLifecycleSystem(
                AddBattleLog,
                new IPassiveEffectTurnProcessor[]
                {
                    new DamageOverTimePassiveEffectProcessor(damageSystem),
                    new SleepPassiveEffectProcessor(recoverySystem)
                },
                statPipeline);

            modifyStatSystem = new ModifyStatSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            modifyVarSystem = new ModifyVarSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            statusEffectSystem = new StatusEffectSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                damageSystem,
                passiveEffectLifecycleSystem);
            lineOfSightService = new CombatLineOfSightService();
            summonedObjectSystem = new SummonedObjectSystem(
                AddBattleLog,
                HandleCombatMapObjectChanged,
                new SummonedObjectEffectExecutor(
                    AddBattleLog,
                    damageSystem,
                    recoverySystem,
                    new CombatShieldSystem(AddBattleLog),
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
                lineOfSightService: lineOfSightService,
                modifyStatSystem: modifyStatSystem,
                modifyVarSystem: modifyVarSystem,
                summonedObjectSystem: summonedObjectSystem,
                summonSystem: summonSystem);
            movementSystem = movementSystem = new CombatMovementSystem(AddBattleLog, resourceRuleSystem, statusEffectSystem);
            interactionSystem = new CombatInteractionSystem(AddBattleLog, HandleCombatMapObjectChanged);
            interactionSystem.InitializeMapObjects(Session);
            targetingService = new CombatTargetingService();
            aiSystem = new SimpleCombatAiSystem(
                battleLog: AddBattleLog, 
                endTurn: session => turnSystem.EndActiveTurn(session), 
                evaluateOutcome: session => outcomeSystem.EvaluateCombatOutcome(session),
                skillExecutionSystem: skillExecutionSystem, targetingService: targetingService,
                movementSystem: movementSystem, lineOfSightService: lineOfSightService);
            battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta.");

            turnSystem.InitializeTurnOrder(Session);
        }
        private void Update()
        {
            ClearCombatScopedEffectsIfNeeded();
            if (usesGameSessionCombat && TryReturnToExploration()) return;
            if (DevConsoleView.IsOpen) return;
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.R))
            {
                RestartCombatForDebug();
                return;
            }
            if (Session == null || Session.ActiveUnit == null || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
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
            if (Input.GetKeyDown(KeyCode.E))
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
                ActionPointsPerSpeed = actionPointsPerSpeed,
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
            var ally = CreateUnit(allyDefinition, scoutClass, new GridPosition(1, 2),
                100, 0, 200, ResourceType.Momentum, 55f, true);
            var enemyA = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 4),
                90, 0, 50, ResourceType.Rage, 48f, false);
            var enemyB = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 2),
                90, 0, 50, ResourceType.Rage, 68f, false);
                
            session.Units.Add(jerzy);
            session.Units.Add(ally);
            session.Units.Add(enemyA);
            session.Units.Add(enemyB);

            return session;
        }

        private void RebuildDungeonCombatArea()
        {
            if (dungeonAreaView == null) return;

            if (!usesGameSessionCombat)
            {
                dungeonAreaView.ClearArea();
                return;
            }

            var context = GameBootstrap.Instance?
                .Session
                .ActiveDungeonCombatContext;

            if (context == null) return;

            dungeonAreaView.Rebuild(context, this);

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
            var maxAp = turnSystem.CalculateMaxActionPoints(instance, actionPointsPerSpeed);

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

            Session = CreateInitialSession();
            hasClearedCombatScopedEffects = false;

            battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta ponownie.");

            turnSystem.InitializeTurnOrder(Session);
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
            battleLog?.AddEntry(message);
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
            if (StatusEffectSystem.IsStunned(unit)
                || StatusEffectSystem.IsFrozen(unit)
                || StatusEffectSystem.IsParalyzed(unit))
            {
                AddBattleLog(
                    $"{unit.Character.Definition.DisplayName} " +
                    "jest pod wpływem twardej kontroli i traci turę.");

                turnSystem.EndActiveTurn(Session);
                return;
            }

            if (!unit.IsPlayerSide)
            {
                aiSystem.ExecuteSimpleAiTurn(Session, unit);
            }
        }

        private void HandleUnitTurnEnded(BattleUnit unit)
        {
            statusEffectSystem?.ProcessTurnEnd(unit);
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
                && Session.ActiveUnit.IsPlayerSide;
        }

        public bool CanEscape()
        {
            return Session != null && Session.ActiveUnit != null && Session.ActiveUnit.IsPlayerSide 
                && Session.ActiveUnit.Character != null 
                && Session.ActiveUnit.Character.LifeState == CharacterLifeState.Alive
                && Session.Outcome == CombatOutcomeState.Ongoing;
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

        public bool TryMoveActiveUnitAlongPath(
            IReadOnlyList<GridPosition> path)
        {
            if (Session == null
                || Session.ActiveUnit == null
                || !Session.ActiveUnit.IsPlayerSide
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return false;
            }

            return movementSystem.TryMoveAlongPath(
                Session,
                Session.ActiveUnit,
                path);
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
            GridPosition? objectPlacementDestination)
        {
            if (Session == null || Session.ActiveUnit == null
                || skillExecutionSystem == null || targetingService == null)
            {
                return false;
            }

            if (!TryBuildSkillTargetGroups(
                    skill,
                    selectedPositionsByModeIndex,
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
                objectPlacementDestination);
        }

        private bool TryBuildSkillTargetGroups(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
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
                    selectedPosition);

                if (targets.Count == 0
                    && targetMode.TargetType != TargetType.AreaAroundTile)
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
            GridPosition? selectedPosition)
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
                requiredEnemyTarget);

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
            SkillTargetModeData targetMode, GridPosition center)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var positions = targetingService.GetRelativeRangePositions(Session, Session.ActiveUnit, 
                targetMode, center);

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
                    actionPointsPerSpeed,
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
