using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SimpleCombatAiSystemTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CalculateSkillScore_PrefersGuaranteedKillOverNonLethalAreaDamage()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var lowHealthEnemy = CreateUnit("low", 10, 0, 0);
            var firstAreaEnemy = CreateUnit("area-first", 20, 0, 0);
            var secondAreaEnemy = CreateUnit("area-second", 20, 0, 0);
            var killSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var areaSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 0.9f));

            var killScore = CalculateScore(caster, killSkill, lowHealthEnemy);
            var areaScore = CalculateScore(caster, areaSkill,
                firstAreaEnemy, secondAreaEnemy);

            Assert.That(killScore, Is.GreaterThan(areaScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersHardControlOverOrdinaryStatus()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var enemy = CreateUnit("enemy", 100, 0, 0);
            var stun = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(stun, "statusId", "stun");
            var poison = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(poison, "statusId", "poison");

            var stunScore = CalculateScore(caster, CreateSkill(stun), enemy);
            var poisonScore = CalculateScore(caster, CreateSkill(poison), enemy);

            Assert.That(stunScore, Is.GreaterThan(poisonScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersReviveOverShield()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var downedAlly = CreateUnit("downed", 0, 0, 0);
            downedAlly.Character.LifeState = CharacterLifeState.Downed;
            var heal = CreateEffect(EffectType.Heal, 1f);
            SetField(heal, "baseValue", 20);
            var shield = CreateEffect(EffectType.Shield, 1f);
            SetField(shield, "baseValue", 100);

            var reviveScore = CalculateScore(caster, CreateSkill(heal), downedAlly);
            var shieldScore = CalculateScore(caster, CreateSkill(shield), downedAlly);

            Assert.That(reviveScore, Is.GreaterThan(shieldScore));
        }

        [Test]
        public void GetPreferredCombatRange_IgnoresLongRangeHealForMeleeDps()
        {
            var meleeAttack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(meleeAttack, TargetingType.Enemies, 1);
            var longRangeHeal = CreateSkill(CreateEffect(EffectType.Heal, 1f));
            ConfigureTargetMode(longRangeHeal, TargetingType.Allies, 4);
            var unit = CreateUnit("dps", 100, 10, 10);
            unit.Character.Skills.Add(meleeAttack);
            unit.Character.Skills.Add(longRangeHeal);
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "GetPreferredCombatRange",
                BindingFlags.Static | BindingFlags.NonPublic);

            var range = (int)method.Invoke(null, new object[] { unit });

            Assert.That(range, Is.EqualTo(1));
        }

        [Test]
        public void CombatFeedbackEvent_CapturesTargetPositionAtPublishTime()
        {
            var target = CreateUnit("target", 100, 0, 0);
            target.Position = new GridPosition(2, 3);

            var feedback = new CombatFeedbackEvent(
                target,
                CombatFeedbackType.Damage,
                amount: 12);

            target.Position = new GridPosition(7, 6);

            Assert.That(feedback.Position, Is.EqualTo(new GridPosition(2, 3)));
        }

        [Test]
        public void TryExecuteNextAction_ExecutesOneZeroCostSkillPerCallAndThenStops()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            var firstSkill = CreateZeroCostDamageSkill("skill.first");
            var secondSkill = CreateZeroCostDamageSkill("skill.second");
            aiUnit.Character.Skills.Add(firstSkill);
            aiUnit.Character.Skills.Add(secondSkill);

            var session = CreateAiSession(aiUnit, target);
            var aiSystem = CreateExecutableAiSystem();
            var turnState = aiSystem.CreateTurnState();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var firstAction),
                Is.True);
            Assert.That(firstAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(firstAction.Skill, Is.SameAs(firstSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(99));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var secondAction),
                Is.True);
            Assert.That(secondAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(secondAction.Skill, Is.SameAs(secondSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out _),
                Is.False);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
        }

        [Test]
        public void ExecuteSimpleAiTurn_EndsAfterZeroCostSkillsAreExhausted()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.first"));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.second"));

            var session = CreateAiSession(aiUnit, target);
            var endTurnCalls = 0;
            var aiSystem = new SimpleCombatAiSystem(
                endTurn: _ => endTurnCalls++,
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());

            aiSystem.ExecuteSimpleAiTurn(session, aiUnit);

            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
            Assert.That(endTurnCalls, Is.EqualTo(1));
        }

        [Test]
        public void TryMoveAlongPath_PublishesSingleApAndMomentumFeedback()
        {
            var unit = CreateAiCombatUnit(
                "moving-unit",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            unit.Character.ResourcePool = new ResourcePool(
                ResourceType.Momentum,
                currentValue: 0,
                maxValue: 200,
                regeneration: 0);

            var session = new CombatSession
            {
                CombatId = "test.path-feedback",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(3, 1),
                ActiveUnit = unit,
                Units = new List<BattleUnit> { unit }
            };
            var feedbackBus = new CombatFeedbackBus();
            var feedbackEvents = new List<CombatFeedbackEvent>();
            feedbackBus.Published += feedbackEvents.Add;
            var movementSystem = new CombatMovementSystem(
                resourceRuleSystem: new ResourceRuleSystem(feedbackBus: feedbackBus),
                feedbackBus: feedbackBus);

            Assert.That(
                movementSystem.TryMoveAlongPath(
                    session,
                    unit,
                    new[] { new GridPosition(1, 0), new GridPosition(2, 0) }),
                Is.True);

            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ActionPointSpend),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ResourceGain),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ActionPointSpend).Amount,
                Is.EqualTo(3));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ResourceGain).Amount,
                Is.EqualTo(6));
        }

        [Test]
        public void CalculateReachablePathsForNextTurn_UsesFreshActionPointsAndMovementCost()
        {
            var unit = CreateAiCombatUnit(
                "preview-unit",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            unit.CurrentActionPoints = 0;
            unit.MaxActionPoints = 3;
            unit.MovementStepsThisTurn = 4;
            var session = new CombatSession
            {
                CombatId = "test.next-turn-movement-preview",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 1),
                Units = new List<BattleUnit> { unit }
            };

            var paths = new CombatMovementSystem()
                .CalculateReachablePathsForNextTurn(session, unit);

            Assert.That(paths.ContainsKey(new GridPosition(1, 0)), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(2, 0)), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(3, 0)), Is.False);
        }

        [Test]
        public void DirectionalArea_TargetsConfiguredDiagonalRays()
        {
            var caster = CreateAiCombatUnit("caster", false, new GridPosition(3, 3));
            var first = CreateAiCombatUnit("first", true, new GridPosition(4, 2));
            var second = CreateAiCombatUnit("second", true, new GridPosition(5, 5));
            var excluded = CreateAiCombatUnit("excluded", true, new GridPosition(4, 3));
            var mode = new SkillTargetModeData();
            SetField(mode, "targetType", TargetType.AreaAroundCaster);
            SetField(mode, "targetingType", TargetingType.Enemies);
            SetField(mode, "absoluteRange", new SkillRangeData(RangeShape.Diagonal, 1, 2));
            var session = new CombatSession
            {
                CombatId = "test.directional-diagonal",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(8, 8),
                Units = new List<BattleUnit> { caster, first, second, excluded }
            };

            var targets = new CombatTargetingService().GetTargets(
                session, caster, mode, new GridPosition(4, 3));

            Assert.That(targets, Is.EquivalentTo(new[] { first, second }));
        }

        [Test]
        public void RelativeSquare_RotationChangesEvenSideAnchor()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(2, 2));
            var session = new CombatSession
            {
                CombatId = "test.relative-square-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit> { caster }
            };
            var range = new SkillRangeData(RangeShape.Square, 0, 1);
            var service = new CombatTargetingService();

            var unrotated = service.GetRelativeRangePositions(
                session,
                caster,
                range,
                new GridPosition(5, 5));
            var rotated = service.GetRelativeRangePositions(
                session,
                caster,
                range,
                new GridPosition(5, 5),
                rotationQuarterTurns: 1);

            Assert.That(unrotated, Is.EquivalentTo(new[]
            {
                new GridPosition(5, 5),
                new GridPosition(6, 5),
                new GridPosition(5, 6),
                new GridPosition(6, 6)
            }));
            Assert.That(rotated, Is.EquivalentTo(new[]
            {
                new GridPosition(4, 5),
                new GridPosition(5, 5),
                new GridPosition(4, 6),
                new GridPosition(5, 6)
            }));
        }

        [Test]
        public void Rectangle_RotationDoesNotDependOnDirectionReference()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(4, 4));
            var session = new CombatSession
            {
                CombatId = "test.rectangle-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit> { caster }
            };
            var range = new SkillRangeData(RangeShape.Rectangle, 1, 2, 2);
            var service = new CombatTargetingService();

            var fromEast = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(5, 4));
            var fromNorth = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(4, 5));
            var rotated = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(5, 4),
                rotationQuarterTurns: 1);

            Assert.That(fromNorth, Is.EquivalentTo(fromEast));
            Assert.That(rotated, Is.Not.EquivalentTo(fromEast));
        }

        [Test]
        public void AreaAroundTile_RelativeSquareTargetsFollowRotation()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(2, 2));
            var unrotatedTarget = CreateAiCombatUnit(
                "unrotated-target",
                true,
                new GridPosition(6, 6));
            var rotatedTarget = CreateAiCombatUnit(
                "rotated-target",
                true,
                new GridPosition(4, 6));
            var mode = new SkillTargetModeData();
            SetField(mode, "targetType", TargetType.AreaAroundTile);
            SetField(mode, "targetingType", TargetingType.Enemies);
            SetField(mode, "absoluteRange", new SkillRangeData(RangeShape.Square, 0, 8));
            SetField(mode, "relativeRange", new SkillRangeData(RangeShape.Square, 0, 1));
            var session = new CombatSession
            {
                CombatId = "test.area-around-tile-square-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit>
                {
                    caster,
                    unrotatedTarget,
                    rotatedTarget
                }
            };
            var service = new CombatTargetingService();
            var selectedTile = new GridPosition(5, 5);

            var unrotatedTargets = service.GetTargets(
                session,
                caster,
                mode,
                selectedTile,
                rotationQuarterTurns: 0);
            var rotatedTargets = service.GetTargets(
                session,
                caster,
                mode,
                selectedTile,
                rotationQuarterTurns: 1);

            Assert.That(unrotatedTargets, Is.EquivalentTo(new[] { unrotatedTarget }));
            Assert.That(rotatedTargets, Is.EquivalentTo(new[] { rotatedTarget }));
        }

        [Test]
        public void TryExecuteNextAction_MovesReachablePathPrefixWhenTargetIsFartherThanAp()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            aiUnit.CurrentActionPoints = 3;
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(3, 0));
            var session = new CombatSession
            {
                CombatId = "test.ai-partial-path",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };

            var aiSystem = CreateExecutableAiSystem();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    aiSystem.CreateTurnState(),
                    out var action),
                Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(action.MovementPath, Is.EqualTo(new[]
            {
                new GridPosition(1, 0),
                new GridPosition(2, 0)
            }));
            Assert.That(aiUnit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(aiUnit.CurrentActionPoints, Is.EqualTo(0));
        }

        [Test]
        public void Navigation_DetoursAroundEdgeWallAcrossTurnsInitiallyMovingAway()
        {
            var session = CreateNavigationSession(5, 4, new GridPosition(1, 1), new GridPosition(3, 1));
            var unit = session.ActiveUnit;
            for (var y = 0; y < 3; y++)
                session.Grid.SetBlockedEdge(new GridPosition(1, y), new GridPosition(2, y));
            var ai = new SimpleCombatAiSystem();
            var visited = new List<GridPosition>();
            for (var turn = 0; turn < 8; turn++)
            {
                unit.CurrentActionPoints = 1;
                unit.MovementStepsThisTurn = 0;
                var before = unit.Position;
                if (!ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action)) break;
                Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
                foreach (var step in action.MovementPath)
                {
                    Assert.That(session.Grid.CanMoveBetween(before, step), Is.True);
                    before = step;
                    visited.Add(step);
                }
                Assert.That(unit.CurrentActionPoints, Is.Zero);
            }
            Assert.That(ai.GetManhattanDistance(visited[0], session.Units[1].Position), Is.GreaterThan(2));
            Assert.That(visited, Does.Contain(new GridPosition(1, 3)));
            Assert.That(ai.IsAdjacent(unit.Position, session.Units[1].Position), Is.True);
        }

        [Test]
        public void Navigation_ClosedDoorIsSeparatePaidInteractionAndThenTraversable()
        {
            var session = CreateNavigationSession(6, 1, new GridPosition(0, 0), new GridPosition(5, 0));
            var unit = session.ActiveUnit;
            var door = AddNavigationDoor(session, 2, DoorInteractionMode.DirectOnly);
            var changed = 0;
            var interaction = new CombatInteractionSystem(onObjectChanged: _ => changed++);
            var ai = new SimpleCombatAiSystem(interactionSystem: interaction);
            var state = ai.CreateTurnState();

            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var approach), Is.True);
            Assert.That(approach.MovementPath, Is.EqualTo(new[] { new GridPosition(1, 0), new GridPosition(2, 0) }));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(7));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var opening), Is.True);
            Assert.That(opening.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(opening.MovementPath, Is.Null);
            Assert.That(unit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(5));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.Grid.CanMoveBetween(new GridPosition(2, 0), new GridPosition(3, 0)), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var crossing), Is.True);
            Assert.That(crossing.MovementPath, Is.EqualTo(new[] { new GridPosition(3, 0) }));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(2));
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out _), Is.False);
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void Navigation_TargetImmediatelyBehindDoorOpensWithoutEnteringOccupiedCell()
        {
            var session = CreateNavigationSession(2, 1, new GridPosition(0, 0), new GridPosition(1, 0));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out _), Is.False);
        }

        [Test]
        public void Navigation_CannotAffordDoorEndsTurnWithoutTogglingOrSpending()
        {
            var session = CreateNavigationSession(3, 1, new GridPosition(0, 0), new GridPosition(2, 0));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            session.ActiveUnit.CurrentActionPoints = 1;
            var ended = 0;
            var ai = new SimpleCombatAiSystem(endTurn: _ => ended++);
            ai.ExecuteSimpleAiTurn(session, session.ActiveUnit);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Navigation_LeverOnlyDoorRequiresExternalOpening(bool open)
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            var door = AddNavigationDoor(session, 1, DoorInteractionMode.LeverOnly, open);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.EqualTo(open));
            Assert.That(door.IsOpen, Is.EqualTo(open));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(open ? 2 : 0, 0)));
        }

        [Test]
        public void Navigation_ReplansAfterOccupancyAndDoorChanges()
        {
            var session = CreateNavigationSession(5, 1, new GridPosition(0, 0), new GridPosition(4, 0));
            var blocker = CreateAiCombatUnit("blocker", false, new GridPosition(2, 0));
            session.Units.Add(blocker);
            var door = AddNavigationDoor(session, 2, DoorInteractionMode.LeverOnly);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            session.Units.Remove(blocker);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            door.IsOpen = true;
            new CombatInteractionSystem().InitializeMapObjects(session);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(3, 0)));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(4));
        }

        [Test]
        public void Navigation_ReplansAfterObstacleRemoval()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.Grid.SetBlocked(new GridPosition(1, 0), true);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            session.Grid.SetBlocked(new GridPosition(1, 0), false);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
        }

        [Test]
        public void Navigation_AdjacentTargetAcrossWallRequiresRealDetour()
        {
            var session = CreateNavigationSession(3, 2, new GridPosition(0, 0), new GridPosition(1, 0));
            session.Grid.SetBlockedEdge(new GridPosition(0, 0), new GridPosition(1, 0));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Is.EqualTo(new[] { new GridPosition(0, 1), new GridPosition(1, 1) }));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(7));
        }

        [Test]
        public void Navigation_UnreachableTargetEndsTurnWithoutFutileMovement()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.Grid.SetBlockedEdge(new GridPosition(1, 0), new GridPosition(2, 0));
            var ended = 0;
            new SimpleCombatAiSystem(endTurn: _ => ended++).ExecuteSimpleAiTurn(session, session.ActiveUnit);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(10));
        }

        [Test]
        public void Navigation_EnemyCannotUseRoomExitAsDetour()
        {
            var session = CreateNavigationSession(3, 2, new GridPosition(0, 0), new GridPosition(2, 0));
            session.IsSeamless = true;
            session.Grid = new BattleGrid(3, 2, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < 3; x++) session.Grid.SetWalkable(new GridPosition(x, 0));
            session.Grid.SetBlocked(new GridPosition(1, 0), true);
            session.OpenExitEdges.Add(new BattleCellEdge(new GridPosition(0, 0), new GridPosition(0, 1)));
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(
                session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_RespectsMovementStepsAlreadySpent()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.ActiveUnit.MovementStepsThisTurn = 2;
            session.ActiveUnit.CurrentActionPoints = 2;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(
                session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(2));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_RangedRoleDetoursWhenTargetIsInRangeButBehindWall()
        {
            var session = CreateNavigationSession(4, 2, new GridPosition(0, 0), new GridPosition(2, 0));
            var unit = session.ActiveUnit;
            SetAutoProperty(unit.Character.Definition, "AiRole", CombatAiRole.Dps);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(skill, TargetingType.Enemies, 3);
            unit.Character.Skills.Add(skill);
            var wall = new BattleCellEdge(new GridPosition(1, 0), new GridPosition(2, 0));
            session.Grid.SetBlockedEdge(wall.FirstCell, wall.SecondCell);
            // Grid edges describe movement; the dungeon supplies physical sight blockers separately.
            session.EnvironmentBlocksLineOfSight = (from, to) => CombatLineOfSightService.CrossesEdge(from, to, wall);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, unit.Position, session.Units[1].Position), Is.False);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Does.Contain(new GridPosition(1, 1)));
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, unit.Position, session.Units[1].Position), Is.True);
        }

        [Test]
        public void Navigation_RangedRetreatSkipsWallAndUsesLegalAlternative()
        {
            var session = CreateNavigationSession(4, 3, new GridPosition(1, 1), new GridPosition(0, 1));
            var unit = session.ActiveUnit;
            SetAutoProperty(unit.Character.Definition, "AiRole", CombatAiRole.Dps);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(skill, TargetingType.Enemies, 3);
            unit.Character.Skills.Add(skill);
            session.Grid.SetBlockedEdge(new GridPosition(1, 1), new GridPosition(2, 1));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Is.EqualTo(new[] { new GridPosition(1, 2) }));
        }

        [Test]
        public void Navigation_PartialMovementPublishesOneAggregatedApEvent()
        {
            var session = CreateNavigationSession(7, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            session.ActiveUnit.CurrentActionPoints = 3;
            var bus = new CombatFeedbackBus();
            var events = new List<CombatFeedbackEvent>();
            bus.Published += events.Add;
            var ai = new SimpleCombatAiSystem(movementSystem: new CombatMovementSystem(feedbackBus: bus));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath.Count, Is.EqualTo(2));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Type, Is.EqualTo(CombatFeedbackType.ActionPointSpend));
            Assert.That(events[0].Amount, Is.EqualTo(3));
        }

        private CombatSession CreateNavigationSession(int width, int height, GridPosition start, GridPosition target)
        {
            var unit = CreateAiCombatUnit("ai", false, start);
            return new CombatSession
            {
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(width, height),
                ActiveUnit = unit,
                Units = new List<BattleUnit> { unit, CreateAiCombatUnit("target", true, target) }
            };
        }

        private static DoorCombatMapObject AddNavigationDoor(
            CombatSession session, int x, DoorInteractionMode mode, bool open = false)
        {
            var door = new DoorCombatMapObject("door", new GridPosition(x, 0), new GridPosition(x + 1, 0), mode, open);
            session.MapObjects.Add(door);
            new CombatInteractionSystem().InitializeMapObjects(session);
            return door;
        }

        private float CalculateScore(
            BattleUnit caster,
            SkillData skill,
            params BattleUnit[] targets)
        {
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "CalculateSkillScore",
                BindingFlags.Static | BindingFlags.NonPublic);
            var targetsByMode = new Dictionary<int, IReadOnlyList<BattleUnit>>
            {
                [0] = targets
            };

            return (float)method.Invoke(null, new object[]
            {
                caster,
                skill,
                targetsByMode
            });
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetField(skill, "targetModes", new List<SkillTargetModeData>
            {
                new()
            });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static void ConfigureTargetMode(
            SkillData skill,
            TargetingType targetingType,
            int maxRange)
        {
            var mode = skill.TargetModes[0];
            SetField(mode, "targetType", TargetType.Single);
            SetField(mode, "targetingType", targetingType);
            SetField(mode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, maxRange));
        }

        private static SkillEffectData CreateEffect(EffectType type, float multiplier)
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", type);
            SetField(effect, "targetModeIndex", 0);
            SetField(effect, "damageMultiplier", multiplier);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            return effect;
        }

        private static BattleUnit CreateUnit(
            string id,
            int health,
            float minAttack,
            float maxAttack)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    CurrentHealth = health,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(minAttack),
                        MaxAttack = new Stat(maxAttack)
                    }
                }
            };
        }

        private BattleUnit CreateAiCombatUnit(
            string id,
            bool isPlayerSide,
            GridPosition position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdAssets.Add(definition);
            SetAutoProperty(definition, "DisplayName", id);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    Definition = definition,
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(ResourceType.None, 0, 0, 0),
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(10f),
                        MaxAttack = new Stat(10f),
                        PhysicalDefense = new Stat(0f),
                        MagicalDefense = new Stat(0f),
                        Dodge = new Stat(0f),
                        CritChance = new Stat(0f),
                        CritDamage = new Stat(1.5f),
                        Speed = new Stat(10f),
                        WeaponAccuracy = new Stat(100f)
                    }
                },
                Position = position,
                CurrentActionPoints = 10,
                MaxActionPoints = 10,
                IsPlayerSide = isPlayerSide
            };
        }

        private static CombatSession CreateAiSession(
            BattleUnit aiUnit,
            BattleUnit target)
        {
            return new CombatSession
            {
                CombatId = "test.ai-action-step",
                Outcome = CombatOutcomeState.Ongoing,
                // Keep the adjacent units in a one-row arena so that the
                // post-skill assertion cannot accidentally observe a move
                // around the target as another valid AI action.
                Grid = new BattleGrid(2, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };
        }

        private SkillData CreateZeroCostDamageSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetAutoProperty(skill, "SkillId", skillId);
            SetAutoProperty(skill, "DisplayName", skillId);
            SetAutoProperty(skill, "ActionCost", 0);
            SetAutoProperty(skill, "SkillAccuracy", 100f);
            SetAutoProperty(skill, "RequiresLineOfSight", false);

            var targetMode = new SkillTargetModeData();
            SetField(targetMode, "targetType", TargetType.Single);
            SetField(targetMode, "targetingType", TargetingType.Enemies);
            SetField(targetMode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, 1));

            var effect = CreateEffect(EffectType.DealDamage, 0.1f);
            SetField(effect, "alwaysHits", true);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            SetField(effect, "damageType", DamageType.Physical);

            SetField(skill, "targetModes", new List<SkillTargetModeData> { targetMode });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static SimpleCombatAiSystem CreateExecutableAiSystem()
        {
            return new SimpleCombatAiSystem(
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }
    }
}
