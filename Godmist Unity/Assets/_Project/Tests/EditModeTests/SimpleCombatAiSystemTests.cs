using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SimpleCombatAiSystemTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new();

        [Test]
        public void Navigation_UnreachableUpperTargetVisitsBoundedGraphInsteadOfEnumeratingPaths()
        {
            var session = CreateNavigationSession(12, 12, new GridPosition(0, 0), new GridPosition(11, 11, 1));
            var grid = new BattleGrid(12, 12, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < 12; x++) for (var y = 0; y < 12; y++) grid.SetWalkable(new GridPosition(x, y));
            grid.SetWalkable(session.Units[1].Position);
            var visits = 0;
            grid.TraversalNeighbours = p =>
            {
                Assert.That(++visits, Is.LessThan(5000), "An unreachable layer must not enumerate combinatorial route lengths.");
                return p.Level == 0 ? new[] { new GridPosition(p.X + 1, p.Y), new GridPosition(p.X - 1, p.Y),
                    new GridPosition(p.X, p.Y + 1), new GridPosition(p.X, p.Y - 1) } : Array.Empty<GridPosition>();
            };
            session.Grid = grid;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_EnemyOnUpperFloorDescendsThroughActualStairNodes()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(3, 0, 1), new GridPosition(0, 0));
            var map = new DungeonFloorMap();
            for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            var upper = new DungeonSurfacePosition(new Vector2Int(3, 0), 1);
            map.TryAddFloorCell(upper, "room");
            Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                Enumerable.Range(0, 4).Select(x => new Vector2Int(x, 0)).ToArray()), Is.True);
            var context = new DungeonCombatContext("d", 0, "enc", "room", Vector2Int.zero, Vector2Int.right,
                map.Surfaces.Select(s => s.Cell).Distinct(), null, null, surfaceMap: map);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            session.Grid = grid;
            session.DungeonContext = context;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
            Assert.That(session.ActiveUnit.Position.IsOnStairs, Is.True);
            Assert.That(session.ActiveUnit.Position.StairStep, Is.EqualTo(1));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(ActionPointUnits.FromPoints(7)));
        }

        [Test]
        public void Navigation_DisconnectedLargeUpperRoomCompletesSearchPromptly()
        {
            const int size = 48;
            var session = CreateNavigationSession(size, size, new GridPosition(0, 0, 1), new GridPosition(size - 1, size - 1));
            var grid = new BattleGrid(size, size, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < size; x++) for (var y = 0; y < size; y++) grid.SetWalkable(new GridPosition(x, y, 1));
            grid.SetWalkable(session.Units[1].Position);
            grid.TraversalNeighbours = p => new[] { new GridPosition(p.X + 1, p.Y, p.Level),
                new GridPosition(p.X - 1, p.Y, p.Level), new GridPosition(p.X, p.Y + 1, p.Level), new GridPosition(p.X, p.Y - 1, p.Level) };
            session.Grid = grid;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            TestContext.WriteLine($"Disconnected upper room search: {watch.ElapsedMilliseconds} ms");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2000), "Unreachable upper rooms must not block the main thread for seconds.");
        }

        [Test]
        public void ContactEscapeRequiresFullTurnsOfEveryEnemyAndResetsOnReacquisition()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var first = session.Units[0]; var player = session.Units[1];
            var second = CreateAiCombatUnit("second", false, new GridPosition(0, 1));
            session.Units.Add(second);
            var seen = false;
            session.Contact = new DungeonContactState((e, p) => seen);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, second);
            seen = true; session.Contact.Refresh(session);
            seen = false; session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            session.Contact.BeginTurn(session, second); session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, second); session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.True);
            session.IsSeamless = session.IsDungeonWide = true;
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, player), Is.True);
            Assert.That(session.DepartedUnits[player.Character.CharacterId], Is.SameAs(player));
            Assert.That(session.Units.Contains(player), Is.False);
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
        }

        [Test]
        public void ContactDoesNotCountPartialTurnAndWaitsForNewEnemy()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var seen = true;
            var contact = session.Contact = new DungeonContactState((e, p) => seen);
            contact.BeginTurn(session, enemy);
            seen = false; contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, enemy); contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, enemy); contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.True);
            var reinforcement = CreateAiCombatUnit("reinforcement", false, new GridPosition(0, 1));
            session.Units.Add(reinforcement); contact.Refresh(session);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, reinforcement); contact.EndTurn(session, reinforcement);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, reinforcement); contact.EndTurn(session, reinforcement);
            Assert.That(contact.CanLeave(session, player), Is.True);
        }

        [Test]
        public void ContactIsIndividualAndSearchUsesLastSeenPositionInsteadOfHiddenTarget()
        {
            var session = CreateNavigationSession(10, 2, new GridPosition(0, 0), new GridPosition(3, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var visible = true;
            var contact = session.Contact = new DungeonContactState((e, p) => visible);
            contact.Refresh(session);
            visible = false; player.Position = new GridPosition(8, 0); contact.Refresh(session);
            Assert.That(contact.TryGetSearchPosition(session, enemy, out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(3, 0)));
            var otherEnemy = CreateAiCombatUnit("uninformed", false, new GridPosition(0, 1));
            session.Units.Add(otherEnemy);
            Assert.That(contact.TryGetSearchPosition(session, otherEnemy, out _), Is.False);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.FindClosestPlayerUnit(session, enemy), Is.Null);
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(3));
            Assert.That(player.Position.X, Is.EqualTo(8));
        }

        [Test]
        public void ContactOtherVisiblePartyMemberDoesNotPreventIndividualEscape()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var hidden = session.Units[1];
            var visible = CreateAiCombatUnit("visible", true, new GridPosition(3, 0));
            session.Units.Add(visible);
            session.Contact = new DungeonContactState((e, p) => p == visible);
            session.Contact.BeginTurn(session, enemy); session.Contact.EndTurn(session, enemy);
            Assert.That(session.Contact.CanLeave(session, hidden), Is.False);
            session.Contact.BeginTurn(session, enemy); session.Contact.EndTurn(session, enemy);
            Assert.That(session.Contact.CanLeave(session, hidden), Is.True);
            Assert.That(session.Contact.CanLeave(session, visible), Is.False);
            Assert.That(new SimpleCombatAiSystem().FindClosestPlayerUnit(session, enemy), Is.SameAs(visible));
        }

        [Test]
        public void ContactMergePreservesIndividualMemoryButRequiresFreshReactionTurns()
        {
            var destination = CreateNavigationSession(10, 2, new GridPosition(0, 0), new GridPosition(3, 0));
            var source = CreateNavigationSession(10, 2, new GridPosition(0, 1), new GridPosition(3, 1));
            var seen = true;
            destination.Contact = new DungeonContactState((e, p) => seen);
            source.Contact = new DungeonContactState((e, p) => seen);
            destination.Contact.Refresh(destination); source.Contact.Refresh(source);
            seen = false;
            destination.Contact.BeginTurn(destination, destination.Units[0]);
            destination.Contact.EndTurn(destination, destination.Units[0]);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.False);
            destination.Contact.BeginTurn(destination, destination.Units[0]);
            destination.Contact.EndTurn(destination, destination.Units[0]);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.True);
            destination.Units.AddRange(source.Units);
            destination.Contact.Absorb(source.Contact); destination.Contact.Refresh(destination);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.False);
            Assert.That(destination.Contact.TryGetSearchPosition(destination, source.Units[0], out var last), Is.True);
            Assert.That(last, Is.EqualTo(source.Units[1].Position));
        }

        [Test]
        public void UnseenTargetCannotBeChosenEvenBySkillWithoutLineOfSightRequirement()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(8, 0));
            session.Contact = new DungeonContactState((e, p) => false);
            session.Contact.Refresh(session);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var method = typeof(SimpleCombatAiSystem).GetMethod("FilterTargetsByLineOfSight", BindingFlags.NonPublic | BindingFlags.Instance);
            var targets = (IReadOnlyList<BattleUnit>)method.Invoke(new SimpleCombatAiSystem(),
                new object[] { session, session.Units[0], skill, new[] { session.Units[1] } });
            Assert.That(targets.Count, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PursuitCrossesRoomBoundaryToVisibleOrLastSeenTarget(bool remainsVisible)
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 10; x++) map.TryAddFloorCell(new Vector2Int(x, 0), x < 4 ? "one" : "two");
            var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0); floor.TrySetMap(map);
            var dungeon = new Godmist.Runtime.Dungeons.DungeonInstance("test", 1); dungeon.TryAddFloor(floor);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
            createdAssets.Add(definition);
            var encounter = new Godmist.Runtime.Dungeons.DungeonEncounterInstance("encounter", definition, "one", new RectInt(0, 0, 4, 1));
            Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context, true), Is.True);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            session.Grid = grid; session.DungeonContext = context; session.IsSeamless = session.IsDungeonWide = true;
            var enemy = session.Units[0]; var target = session.Units[1];
            var seen = true;
            session.Contact = new DungeonContactState((e, p) => seen);
            session.Contact.Refresh(session);
            seen = remainsVisible;
            if (!seen) target.Position = new GridPosition(9, 0);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.GreaterThanOrEqualTo(4), "Enemy must enter the second room.");
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void RoomAlertSeedsPursuitWithoutGivingSightOrFollowingHiddenMovement()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var contact = session.Contact = new DungeonContactState((e, p) => false);
            contact.ReceiveAlert(enemy, player, new GridPosition(4, 0));
            player.Position = new GridPosition(9, 0); contact.Refresh(session);
            Assert.That(contact.Sees(enemy, player), Is.False);
            Assert.That(contact.TryGetSearchPosition(session, enemy, out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(4, 0)));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void DelayedRoomAlertDoesNotOverwritePersonalObservation()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            var visible = true;
            var contact = session.Contact = new DungeonContactState((e, p) => visible);
            contact.Refresh(session); visible = false; contact.Refresh(session);
            contact.ReceiveAlert(session.Units[0], session.Units[1], new GridPosition(2, 0));
            Assert.That(contact.TryGetSearchPosition(session, session.Units[0], out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(6, 0)));
        }

        [Test]
        public void PursuitContinuesToOtherKnownPositionAfterCheckingNearestOne()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(2, 0), new GridPosition(8, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var companion = CreateAiCombatUnit("companion", true, new GridPosition(9, 0)); session.Units.Add(companion);
            session.Contact = new DungeonContactState((e,p) => false);
            session.Contact.ReceiveAlert(enemy, companion, enemy.Position);
            session.Contact.ReceiveAlert(enemy, player, new GridPosition(5, 0));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.GreaterThan(2), "A checked empty location must not mask another observed destination.");
        }

        [Test]
        public void MovementThroughDoorRecordsVisibleStepsBeforePlayerHidesBesideWall()
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 7; x++) for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x,y), x < 3 ? "one" : "two");
            for (var y = 0; y < 4; y++) if (y != 1) map.TryAddWall(new Vector2Int(2,y), new Vector2Int(3,y));
            var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0); floor.TrySetMap(map);
            var dungeon = new Godmist.Runtime.Dungeons.DungeonInstance("d",1); dungeon.TryAddFloor(floor);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>(); createdAssets.Add(definition);
            var encounter = new Godmist.Runtime.Dungeons.DungeonEncounterInstance("e",definition,"one",new RectInt(0,0,3,4));
            new DungeonCombatContextFactory().TryCreate(dungeon,encounter,out var context,true);
            new DungeonBattleGridFactory().TryCreate(context,out var grid);
            var session = CreateNavigationSession(7,4,new GridPosition(1,1),new GridPosition(2,1));
            session.Grid=grid; session.DungeonContext=context;
            var enemy=session.Units[0]; var player=session.Units[1];
            var vision = new Godmist.Systems.Dungeons.DungeonVisibilitySystem();
            session.Contact=new DungeonContactState((e,p)=>vision.CanObserveCombat(floor,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context,e.Position),10,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context,p.Position)));
            session.UnitEnteredCell=unit=>session.Contact.Refresh(session);
            session.Contact.Refresh(session);
            new CombatMovementSystem().TryMoveAlongPath(session,player,new[]{new GridPosition(3,1),new GridPosition(4,1),new GridPosition(4,2)});
            Assert.That(session.Contact.Sees(enemy,player),Is.False);
            Assert.That(session.Contact.TryGetSearchPosition(session,enemy,out var last),Is.True);
            Assert.That(last,Is.EqualTo(new GridPosition(4,1)));
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position.X,Is.EqualTo(2), "From the doorway approach the open edge already reveals the player; stop before crossing it.");
            Assert.That(enemy.Position.Y,Is.EqualTo(1));
            Assert.That(session.Contact.Sees(enemy,player),Is.True);
            Assert.That(ai.FindClosestPlayerUnit(session,enemy),Is.SameAs(player));
        }

        [TestCase(false, 4)]
        [TestCase(true, 2)]
        public void PursuitContinuesTwoCellsInObservedDirectionButNeverThroughWall(bool wall, int expectedX)
        {
            var session = CreateNavigationSession(10, 2, new GridPosition(0,0), new GridPosition(1,0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var seen = true;
            session.Contact = new DungeonContactState((e,p)=>seen);
            session.Contact.Refresh(session);
            player.Position = new GridPosition(2,0); session.Contact.Refresh(session);
            seen = false;
            player.Position = new GridPosition(8,1); // Hidden movement must not redirect the search.
            session.Contact.Refresh(session);
            if (wall) session.Grid.SetWallEdge(new GridPosition(2,0),new GridPosition(3,0),DungeonWallType.Full);
            var ai = new SimpleCombatAiSystem();
            var steps = 0;
            while (ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _))
                Assert.That(++steps,Is.LessThan(5),"Search must terminate after its two forward steps.");
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(expectedX,0)));
        }

        [Test]
        public void SearchStopsMidRouteOnReacquisitionAndNextActionMovesTowardsVisibleTarget()
        {
            var session = CreateNavigationSession(10,3,new GridPosition(0,0),new GridPosition(2,2));
            var enemy=session.Units[0]; var player=session.Units[1];
            session.Contact=new DungeonContactState((e,p)=>e.Position.X >= 2);
            session.Contact.ReceiveAlert(enemy,player,new GridPosition(8,0));
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(2,0)));
            Assert.That(session.Contact.Sees(enemy,player),Is.True);
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(2,1)));
        }

        [Test]
        public void ReacquisitionDoesNotInferDirectionFromUnobservedMovement()
        {
            var session = CreateNavigationSession(10,2,new GridPosition(0,0),new GridPosition(1,0));
            var seen=true; var player=session.Units[1]; var enemy=session.Units[0];
            session.Contact=new DungeonContactState((e,p)=>seen);
            session.Contact.Refresh(session); seen=false; session.Contact.Refresh(session);
            player.Position=new GridPosition(4,0); seen=true; session.Contact.Refresh(session);
            seen=false; player.Position=new GridPosition(9,1); session.Contact.Refresh(session);
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(4,0)));
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.False);
        }

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
        public void AiSkipsActiveNonstackingBuffButAllowsItAfterExpiration()
        {
            var caster = CreateUnit("archer", 100, 10, 10);
            var buff = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(buff, "SkillId", "archer.quiver");
            var active = new PassiveEffectRuntime
            {
                EffectId = "archer.quiver.modify_stat.0",
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 3
            };
            caster.Character.PassiveEffects.Add(active);
            var targets = new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { caster } };
            var method = typeof(SimpleCombatAiSystem).GetMethod("IsRedundantBuff",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method.Invoke(null, new object[] { caster, buff, targets }), Is.True);
            active.RemainingDuration = 0;
            Assert.That(method.Invoke(null, new object[] { caster, buff, targets }), Is.False);
        }

        [Test]
        public void ArcherSelectsAttackAfterQuiverIsActive()
        {
            var archer = CreateAiCombatUnit("archer", false, new GridPosition(0, 0));
            var enemy = CreateAiCombatUnit("enemy", true, new GridPosition(1, 0));
            var session = CreateAiSession(archer, enemy);
            var quiver = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(quiver, "SkillId", "archer.quiver");
            SetAutoProperty(quiver, "ActionCost", 40);
            SetField(quiver.TargetModes[0], "targetType", TargetType.Caster);
            SetField(quiver.TargetModes[0], "targetingType", TargetingType.Allies);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "archer.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            archer.Character.Skills.Add(quiver);
            archer.Character.Skills.Add(attack);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, archer, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(quiver));
            archer.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "archer.quiver.modify_stat.0",
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 3
            });
            Assert.That(Chosen(), Is.SameAs(attack));
        }

        [Test]
        public void AiDoesNotRepeatActiveControlOrAttemptFullyResistedControl()
        {
            var caster = CreateAiCombatUnit("caster", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("target", true, new GridPosition(1, 0));
            var session = CreateAiSession(caster, target);
            var controlEffect = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(controlEffect, "statusId", StatusEffectIds.Stun);
            SetField(controlEffect, "baseChance", 1f);
            var control = CreateSkill(controlEffect);
            SetAutoProperty(control, "SkillId", "ai.control");
            ConfigureTargetMode(control, TargetingType.Enemies, 1);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "ai.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            caster.Character.Skills.Add(control);
            caster.Character.Skills.Add(attack);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, caster, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(control));
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Stun,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 2
            });
            Assert.That(Chosen(), Is.SameAs(attack));
            target.Character.PassiveEffects.Clear();
            target.Character.Resistances = new ResistanceSet { SuppressionResistance = 1f };
            Assert.That(Chosen(), Is.SameAs(attack));
        }

        [Test]
        public void AiSelectsDematerializationThenMaterializationCombo()
        {
            var caster = CreateAiCombatUnit("wraith", false, new GridPosition(0, 0));
            var enemy = CreateAiCombatUnit("enemy", true, new GridPosition(1, 0));
            var session = CreateAiSession(caster, enemy);
            var setupEffect = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(setupEffect, "statusId", "invisibility");
            var setup = CreateSkill(setupEffect);
            SetAutoProperty(setup, "SkillId", "wraith.dematerialize");
            SetField(setup.TargetModes[0], "targetType", TargetType.Caster);
            SetField(setup.TargetModes[0], "targetingType", TargetingType.Allies);
            var followUp = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(followUp, "SkillId", "wraith.materialize");
            SetAutoProperty(followUp, "RequiredCasterStatusId", "invisibility");
            SetField(followUp.TargetModes[0], "targetType", TargetType.Caster);
            SetField(followUp.TargetModes[0], "targetingType", TargetingType.Allies);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "wraith.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            caster.Character.Skills.Add(attack);
            caster.Character.Skills.Add(setup);
            caster.Character.Skills.Add(followUp);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, caster, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(setup));
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "invisibility", Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 3
            });
            Assert.That(Chosen(), Is.SameAs(followUp));
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

        [TestCase(30)]
        [TestCase(35)]
        public void TryExecuteNextAction_PaidAttackUsesPointUnitsForUtility(int costPercent)
        {
            var attacker = CreateAiCombatUnit("ai", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("target", true, new GridPosition(1, 0));
            var skill = CreateZeroCostDamageSkill("skill.paid");
            SetAutoProperty(skill, "ActionCost", costPercent);
            SetField(skill.Effects[0], "damageMultiplier", 1f);
            attacker.Character.Skills.Add(skill);
            var remaining = attacker.CurrentActionPoints - CombatActionCostCalculator.CalculateSkillCost(attacker, skill);
            var session = CreateAiSession(attacker, target);
            session.ActionPointsPerSpeed = 1f; // Keep the fixture budget consistent with Speed = 10.
            var ai = CreateExecutableAiSystem();

            Assert.That(ai.TryExecuteNextAction(session, attacker, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(90));
            Assert.That(attacker.CurrentActionPoints, Is.EqualTo(remaining));
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
                Is.EqualTo(300));
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
            unit.MaxActionPoints = 300;
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
            aiUnit.CurrentActionPoints = 300;
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
                unit.CurrentActionPoints = 100;
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
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(700));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var opening), Is.True);
            Assert.That(opening.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(opening.MovementPath, Is.Null);
            Assert.That(unit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(500));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.Grid.CanMoveBetween(new GridPosition(2, 0), new GridPosition(3, 0)), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var crossing), Is.True);
            Assert.That(crossing.MovementPath, Is.EqualTo(new[] { new GridPosition(3, 0) }));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(200));
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
            session.ActiveUnit.CurrentActionPoints = 100;
            var ended = 0;
            var ai = new SimpleCombatAiSystem(endTurn: _ => ended++);
            ai.ExecuteSimpleAiTurn(session, session.ActiveUnit);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(100));
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
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(400));
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
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(700));
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
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1000));
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
            session.ActiveUnit.CurrentActionPoints = 200;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(
                session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(200));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_RangedRoleDetoursWhenTargetIsInRangeButBehindWall()
        {
            var session = CreateNavigationSession(4, 2, new GridPosition(0, 0), new GridPosition(2, 0));
            var unit = session.ActiveUnit;
            SetAutoProperty(unit.Character.Definition, "AiRole", CombatAiRole.Dps);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(skill, "RequiresLineOfSight", true);
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
            session.ActiveUnit.CurrentActionPoints = 300;
            var bus = new CombatFeedbackBus();
            var events = new List<CombatFeedbackEvent>();
            bus.Published += events.Add;
            var ai = new SimpleCombatAiSystem(movementSystem: new CombatMovementSystem(feedbackBus: bus));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath.Count, Is.EqualTo(2));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Type, Is.EqualTo(CombatFeedbackType.ActionPointSpend));
            Assert.That(events[0].Amount, Is.EqualTo(300));
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
                CurrentActionPoints = 1000,
                MaxActionPoints = 1000,
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
