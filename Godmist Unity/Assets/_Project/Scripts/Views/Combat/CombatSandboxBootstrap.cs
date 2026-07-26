using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using UnityEngine;

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

        [Header("Skills")]
        [SerializeField] private SkillData knifeTossSkill;
        [SerializeField] private SkillData dashSkill;
        [SerializeField] private SkillData focusSkill;
        [SerializeField] private SkillData pushSkill;
        [SerializeField] private SkillData bleedSkill;
        [SerializeField] private SkillData bandageSkill;
        [SerializeField] private SkillData restoreResourceSkill;
        [SerializeField] private SkillData enemyStrikeSkill;
        [Header("Debug Obstacles")]
        [SerializeField] private List<GridPosition> blockedPositions = new();
        

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
        public event Action<CombatMapObject> CombatMapObjectChanged;

        private void Start()
        {
            turnSystem = new CombatTurnSystem(AddBattleLog, HandleUnitTurnBegan, HandleUnitTurnEnded);
            Session = CreateSandboxSession();
            outcomeSystem = new CombatOutcomeSystem(AddBattleLog);
            escapeSystem = new CombatEscapeSystem(AddBattleLog);
            resourceRuleSystem = new ResourceRuleSystem(AddBattleLog);
            damageSystem = new CombatDamageSystem(
                AddBattleLog,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                RemoveUnitFromCombat, resourceRuleSystem);
            recoverySystem = new CombatRecoverySystem(AddBattleLog);
            reviveSystem = new CombatReviveSystem(AddBattleLog);
            statusEffectSystem = new StatusEffectSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session), damageSystem);
            lineOfSightService = new CombatLineOfSightService();
            skillExecutionSystem = new SkillExecutionSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                statusEffectSystem, damageSystem, recoverySystem, lineOfSightService: lineOfSightService);
            movementSystem = new CombatMovementSystem(AddBattleLog, resourceRuleSystem);
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
            if (Input.GetKeyDown(KeyCode.R))
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
            if (Input.GetKeyDown(KeyCode.K))
            {
                RemoveFirstEnemyForDebug();
                return;
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                RemoveActivePlayerUnitForDebug();
                return;
            }
            if (Input.GetKeyDown(KeyCode.O))
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
                Grid = new BattleGrid(gridWidth, gridHeight),
                Units = new List<BattleUnit>()
            };
            foreach (var blockedPosition in blockedPositions)
            {
                session.Grid.SetBlocked(blockedPosition);
            }
            session.MapObjects.Add(new DoorCombatMapObject("door.sandbox.01", 
                new GridPosition(4, 4), DoorInteractionMode.DirectAndLever));
            session.MapObjects.Add(new LeverCombatMapObject("lever.sandbox.01", 
                new GridPosition(4, 3), "door.sandbox.01"));

            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle.high_tree.01", 
                new GridPosition(3, 4), blocksMovement: true, blocksLineOfSight: true));

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
            var instance = new CharacterInstance
            {
                Definition = definition,
                ClassData = classData,
                Level = 1,
                CurrentExperience = 0,
                AvailableSkillPoints = 0,
                CurrentHealth = health,
                WeaponAccuracy = 100f,
                Honor = 0,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Stats = new CharacterStats {
                    Speed = new Core.Types.Stat(speed), 
                    MaxHealth = new Core.Types.Stat(health),
                    MinAttack = new Core.Types.Stat(8),
                    MaxAttack = new Core.Types.Stat(12),
                    PhysicalDefense = new Core.Types.Stat(10),
                    MagicalDefense = new Core.Types.Stat(10),
                    Dodge = new Core.Types.Stat(15),
                    CritChance = new Core.Types.Stat(0.25f),
                    CritDamage = new Core.Types.Stat(1.5f),
                    ResourceRegeneration = new Core.Types.Stat(0),
                    },
                ResourcePool = new ResourcePool(resourceType, resourceCurrent, resourceMax, 0),
                Resistances = new ResistanceSet(),
                ShieldPool = new ShieldPool()
            };
            var maxAp = turnSystem.CalculateMaxActionPoints(instance, actionPointsPerSpeed);

            if (isPlayerSide)
            {
                if (knifeTossSkill != null)
                {
                    instance.Skills.Add(knifeTossSkill);
                }
                if (pushSkill != null)
                {
                    instance.Skills.Add(pushSkill);
                }
                if (bleedSkill != null)
                {
                    instance.Skills.Add(bleedSkill);
                }
                if (dashSkill != null)
                {
                    instance.Skills.Add(dashSkill);
                }
                if (focusSkill != null)
                {
                    instance.Skills.Add(focusSkill);
                }
                if (bandageSkill != null)
                {
                    instance.Skills.Add(bandageSkill);
                }
                if (restoreResourceSkill != null)
                {
                    instance.Skills.Add(restoreResourceSkill);
                }
            }
            else if (enemyStrikeSkill != null) instance.Skills.Add(enemyStrikeSkill);

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
            Session = CreateSandboxSession();

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

            Session.Units.Remove(removedUnit);
            Session.TurnOrder.Remove(removedUnit);

            if (Session.TurnOrder.Count == 0)
            {
                Session.ActiveUnit = null;
                Session.ActiveTurnIndex = 0;
                return;
            }

            if (removedWasActive)
            {
                if (Session.ActiveTurnIndex >= Session.TurnOrder.Count)
                {
                    Session.ActiveTurnIndex = 0;
                }

                Session.ActiveUnit = Session.TurnOrder[Session.ActiveTurnIndex];
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
                    Session.ActiveTurnIndex = 0;
                    Session.ActiveUnit = Session.TurnOrder[0];
                }
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
            if (StatusEffectSystem.IsStunned(unit))
            {
                AddBattleLog($"{unit.Character.Definition.DisplayName} jest ogłuszony i traci turę.");
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
        public bool TryExecuteSkill(SkillData skill, GridPosition? selectedPosition)
        {
            if (skill?.TargetModes == null || skill.TargetModes.Count != 1)
            {
                Debug.LogWarning($"Skill '{skill?.SkillId}' wymaga wielu celow." +
                    $" Uzyj wariantu z selectedPositionsByModeIndex.");
                return false;
            }

            var selectedPositionsByModeIndex = new Dictionary<int, GridPosition?>
                { [0] = selectedPosition };

            return TryExecuteSkill(skill, selectedPositionsByModeIndex);
        }

        public bool TryExecuteSkill(SkillData skill, 
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null || skillExecutionSystem == null 
                || targetingService == null) return false;

            if (!TryBuildSkillTargetGroups(skill, selectedPositionsByModeIndex, out var targetsByModeIndex)) return false;

            return skillExecutionSystem.TryExecute(Session, Session.ActiveUnit, skill, targetsByModeIndex);
        }

        private bool TryBuildSkillTargetGroups(SkillData skill, 
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex, 
            out Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill?.TargetModes == null || selectedPositionsByModeIndex == null) return false;

            for (var targetModeIndex = 0; targetModeIndex < skill.TargetModes.Count; targetModeIndex++)
            {
                var targetMode = skill.TargetModes[targetModeIndex];

                if (targetMode == null) return false;
                    
                if (!selectedPositionsByModeIndex.TryGetValue(targetModeIndex, out var selectedPosition))
                {
                    if (targetMode.TargetType != TargetType.Caster) return false;
                    selectedPosition = null;
                }

                var targets = GetSkillTargets(
                    skill,
                    targetMode,
                    targetModeIndex,
                    selectedPosition);

                if (targets.Count == 0) return false;

                targetsByModeIndex.Add(targetModeIndex, targets);
            }
            return true;
        }

        public bool TryInteractAt(GridPosition position)
        {
            if (Session == null || Session.ActiveUnit == null || interactionSystem == null 
                || !Session.ActiveUnit.IsPlayerSide || Session.Outcome != CombatOutcomeState.Ongoing) return false;

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
        public IReadOnlyList<BattleUnit> GetSkillTargets(SkillData skill, SkillTargetModeData targetMode,
            int targetModeIndex, GridPosition? selectedPosition)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null 
                || targetMode == null || targetingService == null) return Array.Empty<BattleUnit>();

            var includeDownedTargets = CanTargetModeHealDowned(skill, targetModeIndex);

            var targets = targetingService.GetTargets(Session, Session.ActiveUnit, targetMode, 
                selectedPosition, includeDownedTargets);
            return FilterTargetsByLineOfSight(skill, targets);
        }
        public IReadOnlyList<GridPosition> GetSelectableSkillPositions(SkillData skill, 
            SkillTargetModeData targetMode, int targetModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var includeDownedTargets = CanTargetModeHealDowned(skill, targetModeIndex);

            var positions = targetingService.GetSelectablePositions(Session, Session.ActiveUnit, 
                targetMode, includeDownedTargets);

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

        public IReadOnlyList<GridPosition> GetEligibleSkillTargetPositions(SkillData skill, 
            SkillTargetModeData targetMode, int targetModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var includeDownedTargets = CanTargetModeHealDowned(skill, targetModeIndex);

            var positions = targetingService.GetEligibleTargetPositions(Session, Session.ActiveUnit, 
                targetMode, includeDownedTargets);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        private static bool CanTargetModeHealDowned(SkillData skill, int targetModeIndex)
        {
            if (skill?.Effects == null || targetModeIndex < 0) return false;

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == EffectType.Heal
                    && effect.TargetModeIndex == targetModeIndex) return true;
            }
            return false;
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
    }
}
