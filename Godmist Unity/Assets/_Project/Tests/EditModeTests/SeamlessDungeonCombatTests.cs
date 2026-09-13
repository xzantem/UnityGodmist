using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class SeamlessDungeonCombatTests
    {
        private static CombatSession Combat(string id, string room = null) => new()
        {
            CombatId = id, IsSeamless = true, Grid = new BattleGrid(3, 2),
            DungeonContext = new DungeonCombatContext("dungeon", 0, id, room ?? id,
                Vector2Int.zero, Vector2Int.right,
                new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up }, null, null)
        };
        private static GameSession Game()
        {
            var game = new GameSession();
            game.TrySetActiveDungeon(new DungeonInstance("dungeon", 12));
            return game;
        }
        private static BattleUnit Unit(string id, bool player = true) => new()
        {
            IsPlayerSide = player, Position = new GridPosition(0, 0),
            CurrentActionPoints = 7, AvailableKnives = 2,
            Character = new CharacterInstance
            {
                CharacterId = id, LifeState = CharacterLifeState.Alive,
                CurrentHealth = 12, Stats = new CharacterStats { Speed = new Stat(20f) }
            }
        };

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void RingRoom_SightCrossesOutsideFloor_UnlessRealWallBlocks(bool wall, bool visible)
        {
            var map = new DungeonFloorMap();
            var room = new List<Vector2Int>();
            for (var x=0;x<5;x++) for(var y=0;y<3;y++)
            {
                var cell=new Vector2Int(x,y);
                var inside=y!=1 || x==0 || x==4;
                map.TryAddFloorCell(cell,inside ? "arena" : "outside");
                if(inside)room.Add(cell);
            }
            // The wall is completely outside the combat footprint, inside the ring.
            if(wall)map.TryAddWall(new Vector2Int(2,1),new Vector2Int(3,1));
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
            var context=new DungeonCombatContext("d",0,"e","arena",Vector2Int.zero,Vector2Int.zero,room,null,null);
            new DungeonBattleGridFactory().TryCreate(context,out var grid);
            var combat=new CombatSession{Grid=grid,DungeonContext=context,IsSeamless=true};
            combat.EnvironmentBlocksLineOfSight=(a,b)=>DungeonCombatArenaSystem.BlocksEnvironmentSight(floor,context,a,b);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(combat,new GridPosition(0,1),new GridPosition(4,1)),Is.EqualTo(visible));
            Assert.That(grid.IsWalkable(new GridPosition(2,1)),Is.False);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(combat,new GridPosition(4,1),new GridPosition(0,1)),Is.EqualTo(visible));
        }

        [Test]
        public void ContextIncludesOuterWall_AndItBlocksSight()
        {
            var definition=ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                var map=new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero,"arena");
                map.TryAddFloorCell(Vector2Int.right,"outside");
                map.TryAddFloorCell(Vector2Int.right*2,"arena");
                map.TryAddWall(Vector2Int.zero,Vector2Int.right);
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var dungeon=new DungeonInstance("d",1);dungeon.TryAddFloor(floor);
                var encounter=new DungeonEncounterInstance("e",definition,"arena",new RectInt(0,0,3,1));
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon,encounter,out var context),Is.True);
                Assert.That(context.WallEdges.Count,Is.EqualTo(1));
                new DungeonBattleGridFactory().TryCreate(context,out var grid);
                Assert.That(new CombatLineOfSightService().HasLineOfSight(new CombatSession{Grid=grid,DungeonContext=context},new GridPosition(0,0),new GridPosition(2,0)),Is.False);
            }
            finally{Object.DestroyImmediate(definition);}
        }

        [Test]
        public void DoorlessExit_TracksOccupancyAndDoesNotExpandArena()
        {
            var map=new DungeonFloorMap();map.TryAddFloorCell(Vector2Int.zero,"arena");map.TryAddFloorCell(Vector2Int.left,"corridor");
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
            var context=new DungeonCombatContext("d",0,"e","arena",Vector2Int.zero,Vector2Int.zero,new[]{Vector2Int.zero},null,null);
            new DungeonBattleGridFactory().TryCreate(context,out var grid);
            var combat=new CombatSession{Grid=grid,DungeonContext=context,IsSeamless=true};
            var occupied=new HashSet<Vector2Int>();
            DungeonCombatArenaSystem.RefreshExits(combat,floor,occupied);
            Assert.That(combat.OpenExitEdges.Count,Is.EqualTo(1));
            Assert.That(grid.IsWalkable(new GridPosition(-1,0)),Is.False);
            occupied.Add(Vector2Int.left);DungeonCombatArenaSystem.RefreshExits(combat,floor,occupied);
            Assert.That(combat.OpenExitEdges,Is.Empty);
            occupied.Clear();DungeonCombatArenaSystem.RefreshExits(combat,floor,occupied);
            Assert.That(combat.OpenExitEdges.Count,Is.EqualTo(1));
            map.TryAddWall(Vector2Int.zero,Vector2Int.left);DungeonCombatArenaSystem.RefreshExits(combat,floor,occupied);
            Assert.That(combat.OpenExitEdges,Is.Empty);
        }

        [Test]
        public void OutsideDoor_StateChangesAffectExistingSightQuery()
        {
            var definition=ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            try
            {
                var map=new DungeonFloorMap();
                for(var x=0;x<4;x++)map.TryAddFloorCell(new Vector2Int(x,0),x==0||x==3?"arena":"outside");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var door=new DoorDungeonObjectInstance("door",definition,new DungeonCellEdge(Vector2Int.right,Vector2Int.right*2));
                floor.TryAddObject(door);
                var context=new DungeonCombatContext("d",0,"e","arena",Vector2Int.zero,Vector2Int.zero,new[]{Vector2Int.zero,Vector2Int.right*3},null,null);
                new DungeonBattleGridFactory().TryCreate(context,out var grid);
                var combat=new CombatSession{Grid=grid,DungeonContext=context};
                combat.EnvironmentBlocksLineOfSight=(a,b)=>DungeonCombatArenaSystem.BlocksEnvironmentSight(floor,context,a,b);
                var sight=new CombatLineOfSightService();
                door.TryRestoreOpen(false);
                Assert.That(sight.HasLineOfSight(combat,new GridPosition(0,0),new GridPosition(3,0)),Is.False);
                door.TrySetOpen(true);
                Assert.That(sight.HasLineOfSight(combat,new GridPosition(0,0),new GridPosition(3,0)),Is.True);
                door.TrySetOpen(false);
                Assert.That(sight.HasLineOfSight(combat,new GridPosition(0,0),new GridPosition(3,0)),Is.False);
            }
            finally{Object.DestroyImmediate(definition);}
        }

        [TestCase(false,true)]
        [TestCase(true,false)]
        public void OutsideObstacle_UsesSightFlagIndependentlyOfMovement(bool blocksSight,bool visible)
        {
            var definition=ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                typeof(ObstacleDungeonObjectDefinition).GetField("<BlocksLineOfSight>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(definition,blocksSight);
                var map=new DungeonFloorMap();for(var x=0;x<3;x++)map.TryAddFloorCell(new Vector2Int(x,0),x==1?"outside":"arena");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);floor.TryAddObject(new ObstacleDungeonObjectInstance("obstacle",definition,Vector2Int.right));
                var context=new DungeonCombatContext("d",0,"e","arena",Vector2Int.zero,Vector2Int.zero,new[]{Vector2Int.zero,Vector2Int.right*2},null,null);
                new DungeonBattleGridFactory().TryCreate(context,out var grid);
                var combat=new CombatSession{Grid=grid,DungeonContext=context,IsSeamless=true};
                combat.EnvironmentBlocksLineOfSight=(a,b)=>DungeonCombatArenaSystem.BlocksEnvironmentSight(floor,context,a,b);
                Assert.That(new CombatLineOfSightService().HasLineOfSight(combat,new GridPosition(0,0),new GridPosition(2,0)),Is.EqualTo(visible));
                DungeonCombatArenaSystem.RefreshExits(combat,floor,new HashSet<Vector2Int>());
                Assert.That(combat.OpenExitEdges,Is.Empty,"Movement remains blocked even by a see-through obstacle.");
            }
            finally{Object.DestroyImmediate(definition);}
        }

        [Test]
        public void Focus_DoesNotChangeMembershipOrPermitSave()
        {
            var game = Game(); var first = Combat("one"); var second = Combat("two");
            first.Units.Add(Unit("a")); second.Units.Add(Unit("b"));
            Assert.That(game.TryRegisterDungeonCombat(first), Is.True);
            Assert.That(game.TryRegisterDungeonCombat(second), Is.True);
            Assert.That(game.TryFocusCombat(second), Is.True);
            Assert.That(game.ActiveDungeonCombatContext, Is.SameAs(second.DungeonContext));
            game.TryFocusCombat(null);
            Assert.That(game.HasCombat, Is.True);
            Assert.That(game.IsCharacterInCombat("a"), Is.True);
            Assert.That(game.IsCharacterInCombat("explorer"), Is.False);
            Assert.That(game.TryClearActiveDungeon(), Is.False);
            Assert.That(game.GetCombatForCharacter("a"), Is.SameAs(first));
            Assert.Throws<InvalidOperationException>(() => new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "blocked"));
            Assert.That(game.TryRemoveCombat(first), Is.True);
            Assert.That(game.HasCombat, Is.True);
            Assert.That(game.TryRemoveCombat(second), Is.True);
            Assert.That(game.HasCombat, Is.False);
        }

        [Test]
        public void Registration_RejectsDuplicateRoomAndCharacter_WithoutChangingOtherSession()
        {
            var game = Game(); var first = Combat("one", "room"); first.Units.Add(Unit("a"));
            game.TryRegisterDungeonCombat(first);
            Assert.That(game.TryRegisterDungeonCombat(Combat("two", "room")), Is.False);
            var overlapping = Combat("three"); overlapping.Units.Add(Unit("a"));
            Assert.That(game.TryRegisterDungeonCombat(overlapping), Is.False);
            Assert.That(game.Combats.Count, Is.EqualTo(1));
        }

        [TestCase(DungeonWallType.Low)]
        [TestCase(DungeonWallType.Railing)]
        public void TypedWalls_BlockDiagonalPushThroughClosedCorner(DungeonWallType type)
        {
            var session = Combat("one"); var unit = Unit("player");
            session.Units.Add(unit);
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), type);
            session.Grid.SetWallEdge(new(0, 0), new(0, 1), type);
            var method = typeof(SkillExecutionSystem).GetMethod("CanForcedDisplacementMove",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That((bool)method.Invoke(null, new object[] { session, unit, new GridPosition(1, 1), 1, 1 }), Is.False);
            var charge = typeof(CombatChargeSystem).GetMethod("CanTraverse", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That((bool)charge.Invoke(null, new object[] { session, new GridPosition(0, 0), new GridPosition(1, 1) }), Is.False);
            Assert.That((bool)charge.Invoke(null, new object[] { session, new GridPosition(0, 0), new GridPosition(1, 0) }), Is.False);

        }

        [Test]
        public void LowWall_AiPaysActualCrossingCost()
        {
            var session = Combat("one");
            session.Grid = new BattleGrid(3, 1);
            var enemy = Unit("enemy", false); var player = Unit("player");
            player.Position = new GridPosition(2, 0);
            session.Units.Add(enemy); session.Units.Add(player); session.ActiveUnit = enemy;
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), DungeonWallType.Low);
            enemy.CurrentActionPoints = 2;
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, enemy, player), Is.False);
            enemy.CurrentActionPoints = 3;
            Assert.That(ai.TryMoveAiTowardsTarget(session, enemy, player), Is.True);
            Assert.That(enemy.CurrentActionPoints, Is.Zero);
            Assert.That(enemy.Position, Is.EqualTo(new GridPosition(1, 0)));
        }

        [TestCase(DungeonWallType.Full, false, false)]
        [TestCase(DungeonWallType.Low, true, true)]
        [TestCase(DungeonWallType.Railing, false, true)]
        public void TypedWalls_ShareMovementAndSightRules(DungeonWallType type, bool walk, bool sight)
        {
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            var context = new DungeonCombatContext("dungeon", 0, "encounter", "room",
                Vector2Int.zero, Vector2Int.right, new[] { Vector2Int.zero, Vector2Int.right },
                new[] { edge }, null, nextWallTypes: new Dictionary<DungeonCellEdge, DungeonWallType> { [edge] = type });
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var session = new CombatSession { Grid = grid, DungeonContext = context };
            Assert.That(grid.CanMoveBetween(new(0, 0), new(1, 0)), Is.EqualTo(walk));
            Assert.That(grid.CanDisplaceBetween(new(0, 0), new(1, 0)), Is.False);
            Assert.That(grid.CanDisplaceBetween(new(1, 0), new(0, 0)), Is.False);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, new(0, 0), new(1, 0)), Is.EqualTo(sight));
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, new(1, 0), new(0, 0)), Is.EqualTo(sight));
        }

        [TestCase(4, false)]
        [TestCase(5, true)]
        public void LowWall_ThirdStepCostsFiveAp_PreviewMatchesExecution(int ap, bool succeeds)
        {
            var session = Combat("one");
            var unit = Unit("player");
            session.Units.Add(unit);
            unit.CurrentActionPoints = ap;
            unit.MovementStepsThisTurn = 2;
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), DungeonWallType.Low);
            var system = new CombatMovementSystem();
            var paths = system.CalculateReachablePaths(session, unit);
            Assert.That(paths.ContainsKey(new GridPosition(1, 0)), Is.EqualTo(succeeds));
            Assert.That(system.TryMoveOneStep(session, unit, 1, 0), Is.EqualTo(succeeds));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(succeeds ? 0 : ap));
            Assert.That(unit.MovementStepsThisTurn, Is.EqualTo(succeeds ? 3 : 2));
        }

        [Test]
        public void LowWall_ReachabilitySelectsCheaperEqualLengthPath()
        {
            var session = Combat("one");
            var unit = Unit("player");
            session.Units.Add(unit);
            unit.CurrentActionPoints = 3;
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), DungeonWallType.Low);
            var paths = new CombatMovementSystem().CalculateReachablePaths(session, unit);
            Assert.That(paths[new GridPosition(1, 1)], Is.EqualTo(new[] { new GridPosition(0, 1), new GridPosition(1, 1) }));
            Assert.That(CombatMovementSystem.CalculatePathCost(session, unit.Position, 0,
                paths[new GridPosition(1, 1)]), Is.EqualTo(3));
        }

        [Test]
        public void LowWall_ExitChargesSurchargeButVoidRemainsInaccessible()
        {
            var session = Combat("one");
            var unit = Unit("player");
            session.Units.Add(unit);
            session.Grid.SetWallEdge(new(0, 0), new(-1, 0), DungeonWallType.Low);
            Assert.That(session.CanMoveBetween(unit, new(0, 0), new(-1, 0)), Is.False);
            session.OpenExitEdges.Add(new BattleCellEdge(new(0, 0), new(-1, 0)));
            unit.CurrentActionPoints = 3;
            Assert.That(new CombatMovementSystem().TryMoveOneStep(session, unit, -1, 0), Is.True);
            Assert.That(unit.CurrentActionPoints, Is.Zero);
        }

        [TestCase(DungeonWallType.Low)]
        [TestCase(DungeonWallType.Railing)]
        public void TypedWalls_TeleportStillUsesLegalDestination(DungeonWallType type)
        {
            var session = Combat("one");
            var unit = Unit("player"); session.Units.Add(unit);
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), type);
            var teleport = new CombatTeleportSystem();
            Assert.That(teleport.CanTeleport(session, unit, unit, new(1, 0), true), Is.True);
            Assert.That(teleport.CanTeleport(session, unit, unit, new(-1, 0), false), Is.False);
        }

        [Test]
        public void GroupPositions_ReserveOriginalCellsBeforeDisplacingCollision()
        {
            var preferred = new List<GridPosition> { new(0, 0), new(0, 0), new(0, 1) };
            var reserved = new HashSet<GridPosition>();
            Assert.That(DungeonCombatPositionResolver.TryResolveGroup(
                new BattleGrid(2, 2), preferred, reserved, out var result), Is.True);
            Assert.That(result, Is.EqualTo(new[] { new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(0, 1) }));
            Assert.That(reserved, Is.Empty);
            Assert.That(preferred[1], Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void GroupPositions_RespectsExternalReservationsAndIsDeterministic()
        {
            var grid = new BattleGrid(3, 2);
            var preferred = new[] { new GridPosition(0, 0), new GridPosition(0, 0), new GridPosition(1, 0) };
            var reserved = new HashSet<GridPosition> { new(0, 1) };
            Assert.That(DungeonCombatPositionResolver.TryResolveGroup(grid, preferred, reserved, out var first), Is.True);
            Assert.That(DungeonCombatPositionResolver.TryResolveGroup(grid, preferred, reserved, out var second), Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first[2], Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(first, Has.No.Member(new GridPosition(0, 1)));
            Assert.That(new HashSet<GridPosition>(first).Count, Is.EqualTo(3));
            Assert.That(reserved.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GroupPositions_CannotDisplaceThroughClosedEdgeOrBlockedCell(bool blockedCell)
        {
            var grid = new BattleGrid(2, 1);
            if (blockedCell) grid.SetBlocked(new GridPosition(1, 0), true);
            else grid.SetBlockedEdge(new GridPosition(0, 0), new GridPosition(1, 0));
            var reserved = new HashSet<GridPosition>();
            Assert.That(DungeonCombatPositionResolver.TryResolveGroup(grid,
                new[] { new GridPosition(0, 0), new GridPosition(0, 0) }, reserved, out var result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(reserved, Is.Empty);
        }

        [Test]
        public void Join_ReservesOtherArrivalsOriginalPositions()
        {
            var session = Combat("one");
            session.Units.Add(Unit("occupant"));
            var joining = Unit("joining");
            var reserved = new HashSet<GridPosition> { new(0, 1) };
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(0, 0), reserved), Is.True);
            Assert.That(joining.Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(reserved.Count, Is.EqualTo(1));
        }

        [Test]
        public void GroupPositions_FullRoomFailsAtomically()
        {
            var reserved = new HashSet<GridPosition>();
            Assert.That(DungeonCombatPositionResolver.TryResolveGroup(new BattleGrid(2, 1),
                new[] { new GridPosition(0, 0), new GridPosition(0, 0), new GridPosition(1, 0) },
                reserved, out var result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(reserved, Is.Empty);
        }

        [Test]
        public void PositionConflict_SearchIsDeterministicAndCannotCrossClosedEdge()
        {
            var grid = new BattleGrid(3, 1);
            var occupied = new HashSet<GridPosition> { new(0, 0) };
            grid.SetBlockedEdge(new(0, 0), new(1, 0));
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out _), Is.False);
            grid.SetBlockedEdge(new(0, 0), new(1, 0), false);
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out var found), Is.True);
            Assert.That(found, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out var repeated), Is.True);
            Assert.That(repeated, Is.EqualTo(found));
        }

        [Test]
        public void Exit_IsPlayerOnly_AndMovementPreviewStopsBeyondThreshold()
        {
            var session = Combat("one"); var player = Unit("a");
            session.Units.Add(player);
            var outside = new GridPosition(-1, 0);
            session.OpenExitEdges.Add(new BattleCellEdge(player.Position, outside));
            var movement = new CombatMovementSystem();
            var paths = movement.CalculateReachablePaths(session, player);
            Assert.That(paths.ContainsKey(outside), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(-2, 0)), Is.False);
            Assert.That(player.CurrentActionPoints, Is.EqualTo(7));
            Assert.That(session.CanMoveBetween(Unit("enemy", false), player.Position, outside), Is.False);
            Assert.That(session.Grid.IsWalkable(outside), Is.False, "Exit is not an extra arena tile for skills.");
        }

        [Test]
        public void ThresholdTransit_ReservesDestinationAgainstAnotherCombat()
        {
            var game = Game(); var source = Combat("source"); var target = Combat("target");
            target.DungeonContext = new DungeonCombatContext("dungeon", 0, "target", "target",
                new Vector2Int(3, 0), new Vector2Int(4, 0),
                new[] { new Vector2Int(3, 0), new Vector2Int(4, 0) }, null, null);
            var leaving = Unit("a"); leaving.Position = new GridPosition(3, 0);
            source.Units.Add(leaving);
            var other = Unit("b"); other.Position = new GridPosition(1, 0); target.Units.Add(other);
            game.TryRegisterDungeonCombat(source); game.TryRegisterDungeonCombat(target);
            Assert.That(target.CanMoveBetween(other, new(1, 0), new(0, 0)), Is.False);
            Assert.That(target.IsPositionOccupied(new(0, 0)), Is.True);
            var arrival = Unit("c");
            Assert.That(DungeonCombatParticipationSystem.TryJoin(target, arrival, new(0, 0)), Is.True);
            Assert.That(arrival.Position, Is.Not.EqualTo(new GridPosition(0, 0)));
            source.Units.Remove(leaving);
            Assert.That(target.CanMoveBetween(other, new(1, 0), new(0, 0)), Is.True);
        }

        [Test]
        public void ClosedExit_IsAbsentFromPreview()
        {
            var session = Combat("one"); var unit = Unit("a"); session.Units.Add(unit);
            Assert.That(new CombatMovementSystem().CalculateReachablePaths(session, unit)
                .ContainsKey(new GridPosition(-1, 0)), Is.False);
        }

        [Test]
        public void Reentry_PreservesResourcesAndWaitsFullActionValue_WithoutChangingActiveTurn()
        {
            var session = Combat("one"); var actor = Unit("a"); var joining = Unit("b");
            session.Units.Add(actor); session.ActiveUnit = actor;
            session.ActiveParticipant = new CombatTurnParticipant(actor);
            session.TurnParticipants.Add(session.ActiveParticipant);
            joining.CurrentActionValue = 750f;
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(1, 0)), Is.True);
            Assert.That(joining.CurrentActionValue, Is.EqualTo(750f));
            Assert.That(joining.CurrentActionPoints, Is.EqualTo(7));
            Assert.That(joining.AvailableKnives, Is.EqualTo(2));
            Assert.That(session.ActiveUnit, Is.SameAs(actor));
            joining.Position = new(-1, 0);
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, joining), Is.True);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(1, 0)), Is.True);
            Assert.That(joining.CurrentActionValue, Is.GreaterThanOrEqualTo(CombatTurnSystem.CalculateBaseActionValue(joining)));
            Assert.That(session.Units.FindAll(value => value == joining).Count, Is.EqualTo(1));
        }

        [Test]
        public void FailedJoin_DoesNotMoveOrScheduleUnit()
        {
            var session = Combat("one"); session.Grid = new BattleGrid(1, 1);
            session.Units.Add(Unit("a")); var joining = Unit("b"); joining.Position = new(8, 9);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(0, 0)), Is.False);
            Assert.That(joining.Position, Is.EqualTo(new GridPosition(8, 9)));
            Assert.That(session.Units.Count, Is.EqualTo(1));
            Assert.That(session.TurnParticipants, Is.Empty);
        }

        [Test]
        public void DownedWithRescuerOutside_DoesNotResolveDefeat()
        {
            var session = Combat("one"); var player = Unit("a");
            player.Character.LifeState = CharacterLifeState.Downed;
            session.Units.Add(player); session.Units.Add(Unit("enemy", false));
            session.CanReceiveReinforcements = true;
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Ongoing));
            session.CanReceiveReinforcements = false;
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Defeat));
        }

        [Test]
        public void LastLivingParticipantLeaving_ResolvesEscape_NotCampaignDefeat()
        {
            var session = Combat("one"); var player = Unit("a");
            session.Units.Add(player); session.Units.Add(Unit("enemy", false));
            player.Position = new(-1, 0);
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, player), Is.True);
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
            Assert.That(player.Character.CurrentHealth, Is.EqualTo(12));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Factory_UsesOnlyMembersInsideRoom_AndKeepsContactPositions(bool overlap)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                SetProperty(definition, "Id", "enemy.definition");
                SetProperty(definition, "BaseStats", new CharacterBaseStatsData());
                SetProperty(classData, "Id", "class.warrior");
                SetProperty(classData, "ResourceType", ResourceType.Rage);
                var enemy = new EncounterEnemyData();
                SetProperty(enemy, "InstanceId", "enemy");
                SetProperty(enemy, "CharacterDefinition", definition);
                SetProperty(enemy, "ClassData", classData);
                SetProperty(encounterDefinition, "Enemies", new List<EncounterEnemyData> { enemy });
                var encounter = new DungeonEncounterInstance("one", encounterDefinition, "one", new RectInt(0, 0, 2, 2));
                var game = Game();
                foreach (var id in new[] { "inside", "outside" })
                {
                    var character = Unit(id).Character;
                    character.AvailabilityState = CharacterAvailabilityState.Available;
                    game.TryRegisterCharacter(character);
                    Assert.That(game.TryAddCharacterToActiveParty(id), Is.True);
                }
                var players = new Dictionary<string, Vector2Int>
                    { ["inside"] = overlap ? Vector2Int.zero : Vector2Int.up, ["outside"] = new(8, 8) };
                if (overlap)
                {
                    var extra = Unit("extra").Character;
                    extra.AvailabilityState = CharacterAvailabilityState.Available;
                    game.TryRegisterCharacter(extra);
                    Assert.That(game.TryAddCharacterToActiveParty("extra"), Is.True);
                    players["extra"] = Vector2Int.zero;
                }
                var enemyCell = overlap ? Vector2Int.up : Vector2Int.right;
                var enemies = new Dictionary<string, Vector2Int> { ["enemy"] = enemyCell };
                Assert.That(new CombatSessionFactory().TryCreateSeamless(game, encounter,
                    Combat("one").DungeonContext, players, enemies, out var session), Is.True);
                Assert.That(session.Units.Count, Is.EqualTo(overlap ? 3 : 2));
                Assert.That(session.Units[0].Character, Is.SameAs(game.CharactersById["inside"]));
                Assert.That(session.Units[0].Position, Is.EqualTo(new GridPosition(0, overlap ? 0 : 1)));
                Assert.That(session.Units[session.Units.Count - 1].Position,
                    Is.EqualTo(new GridPosition(enemyCell.x, enemyCell.y)));
                if (overlap) Assert.That(session.Units[1].Position, Is.EqualTo(new GridPosition(1, 0)));
                Assert.That(game.HasCombat, Is.False, "Factory does not commit a partially constructed session.");
                var fullRoom = new DungeonCombatContext("dungeon", 0, "one", "one",
                    Vector2Int.zero, Vector2Int.zero, new[] { Vector2Int.zero }, null, null);
                players["inside"] = Vector2Int.zero;
                enemies["enemy"] = Vector2Int.zero;
                Assert.That(new CombatSessionFactory().TryCreateSeamless(game, encounter,
                    fullRoom, players, enemies, out var rejected, out var placementFailed), Is.False);
                Assert.That(placementFailed, Is.True);
                Assert.That(rejected, Is.Null);
                Assert.That(game.HasCombat, Is.False);
                Assert.That(players["inside"], Is.EqualTo(Vector2Int.zero));

            }
            finally
            {
                Object.DestroyImmediate(definition); Object.DestroyImmediate(classData);
                Object.DestroyImmediate(encounterDefinition);
            }
        }

        [Test]
        public void BorrowAndRelease_UsesCellCenterEvenWhenAnimationHasNotFinished()
        {
            var root = new GameObject("test-grid", typeof(Grid));
            var tileObject = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
            tileObject.transform.SetParent(root.transform);
            var holder = new GameObject("holder");
            var characterObject = new GameObject("character");
            try
            {
                var map = holder.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView)
                    .GetField("floorTilemap", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(map, tileObject.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var spawner = holder.AddComponent<Godmist.Views.Combat.CombatUnitViewSpawner>();
                spawner.ConfigureDungeon(Game(), map);
                var view = characterObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var combat = Combat("one");
                var unit = Unit("test");
                view.SetPosition(new Vector2(0.2f, 0.8f));
                spawner.Borrow(combat, unit, view);
                Assert.That(view.Position, Is.EqualTo(map.CellToWorldCenter(Vector2Int.zero)));
                unit.Position = new GridPosition(-1, 0);
                view.SetPosition(new Vector2(0.2f, 0.5f));
                spawner.Release(unit);
                Assert.That(view.Position, Is.EqualTo(map.CellToWorldCenter(Vector2Int.left)));
                Assert.That(view.CombatUnit, Is.Null);
                unit.Position = new GridPosition(1, 1);
                spawner.Borrow(combat, unit, view);
                spawner.Release(unit);
                Assert.That(view.Position, Is.EqualTo(map.CellToWorldCenter(Vector2Int.one)));
            }
            finally
            {
                Object.DestroyImmediate(characterObject);
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(root);
            }
        }

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void BoundaryDoor_IsIncludedWithoutAddingAdjacentRoomToArena()
        {
            var doorDefinition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "one"); map.TryAddFloorCell(Vector2Int.right, "two");
                floor.TrySetMap(map);
                floor.TryAddObject(new DoorDungeonObjectInstance("door", doorDefinition,
                    new DungeonCellEdge(Vector2Int.zero, Vector2Int.right)));
                var encounter = new DungeonEncounterInstance("one", encounterDefinition, "one", new RectInt(0, 0, 1, 1));
                floor.TryAddEncounter(encounter);
                var dungeon = new DungeonInstance("dungeon", 1); dungeon.TryAddFloor(floor);
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context), Is.True);
                Assert.That(context.MapObjects.Count, Is.EqualTo(1));
                Assert.That(context.WorldCells.Count, Is.EqualTo(1));
                Assert.That(context.ContainsWorldCell(Vector2Int.right), Is.False);
            }
            finally { Object.DestroyImmediate(doorDefinition); Object.DestroyImmediate(encounterDefinition); }
        }

        [Test]
        public void BorrowedCharacter_SurvivesLeavingAndAnotherCombat_WithSameObject()
        {
            var root = new GameObject("shared world test");
            try
            {
                var game = Game(); var first = Combat("one"); var second = Combat("two");
                var unit = Unit("a");
                var view = root.AddComponent<ExplorationCharacterView>();
                var map = root.AddComponent<DungeonFloorMapView>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var objectId = view.gameObject.GetInstanceID();
                spawner.ConfigureDungeon(game, map);
                spawner.Borrow(first, unit, view);
                Assert.That(view.CombatUnit, Is.SameAs(unit));
                Assert.That(spawner.TryGetWorldPosition(unit, out _), Is.True);
                spawner.Release(unit);
                Assert.That(view.CombatUnit, Is.Null);
                spawner.Borrow(second, unit, view);
                Assert.That(view.gameObject.GetInstanceID(), Is.EqualTo(objectId));
                Assert.That(view.CombatUnit, Is.SameAs(unit));
                spawner.Release(unit);
                Assert.That(view, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReleasedEncounter_KeepsSurvivorObjectsAndReplacesDeadChaser()
        {
            var root = new GameObject("encounter reuse test");
            var dead = new GameObject("dead enemy");
            var alive = new GameObject("survivor");
            try
            {
                var trigger = root.AddComponent<EncounterTriggerView>();
                var deadView = dead.AddComponent<ExplorationCharacterView>();
                var survivor = alive.AddComponent<ExplorationCharacterView>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(EncounterTriggerView).GetField("enemyViews", flags).SetValue(trigger,
                    new[] { deadView, survivor });
                typeof(EncounterTriggerView).GetField("chaserView", flags).SetValue(trigger, deadView);
                dead.SetActive(false);
                trigger.ReleaseEncounter();
                Assert.That(trigger.EnemyViews, Is.EqualTo(new[] { survivor }));
                Assert.That(typeof(EncounterTriggerView).GetField("chaserView", flags).GetValue(trigger), Is.SameAs(survivor));
                trigger.ReleaseEncounter();
                Assert.That(trigger.EnemyViews[0], Is.SameAs(survivor));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(dead); Object.DestroyImmediate(alive); }
        }

        [Test]
        public void ObserverReceivesBothSessionsFeedback_AcrossFocusChanges_WithoutDuplicates()
        {
            var root = new GameObject("observer feedback test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", flags).SetValue(host, true);
                typeof(CombatSandboxBootstrap).GetField("unitViewSpawner", flags).SetValue(host, spawner);
                var first = Combat("one"); var second = Combat("two");
                host.TryBegin(first); host.TryBegin(second);
                var a = host.GetDriver(first).FeedbackBus; var b = host.GetDriver(second).FeedbackBus;
                int received = 0;
                host.DungeonFeedback.Published += _ => received++;
                host.Focus(first); host.Focus(null); host.Focus(second); host.Focus(null);
                a.Publish(new CombatFeedbackEvent(Unit("a"), CombatFeedbackType.Damage, 1));
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(2));
                host.End(first);
                a.Publish(new CombatFeedbackEvent(Unit("a"), CombatFeedbackType.Damage, 1));
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(3));
                host.End(second);
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BorrowedMovementContinues_WhenObserverFocusIsExploration()
        {
            var root = new GameObject("background movement test");
            var actor = new GameObject("borrowed actor");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var map = root.AddComponent<DungeonFloorMapView>();
                typeof(CombatUnitViewSpawner).GetField("sandboxBootstrap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, host);
                typeof(CombatUnitViewSpawner).GetField("battleUnitPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, root.AddComponent<Godmist.Views.Characters.CharacterView>());
                var view = actor.AddComponent<ExplorationCharacterView>();
                var game = Game(); var session = Combat("one"); var unit = Unit("a");
                session.Units.Add(unit); game.TryRegisterDungeonCombat(session);
                spawner.ConfigureDungeon(game, map); spawner.Borrow(session, unit, view);
                game.TryFocusCombat(null); host.Focus(null);
                spawner.TryBeginPresentationMovement(unit);
                spawner.TryMovePresentationStep(unit, new GridPosition(1, 0));
                typeof(CombatUnitViewSpawner).GetMethod("SynchronizeDungeon", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(spawner, new object[] { .1f });
                Assert.That(view.Position.x, Is.EqualTo(.6f).Within(.001f));
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.True);
                typeof(CombatUnitViewSpawner).GetMethod("SynchronizeDungeon", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(spawner, new object[] { .1f });
                Assert.That(view.Position.x, Is.EqualTo(1f).Within(.001f));
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [TestCase("camera")]
        [TestCase("zoom")]
        [TestCase("unit")]
        [TestCase("removed_unit")]
        public void FloatingFeedback_ReprojectsWorldAnchor_WithoutResettingAnimation(string change)
        {
            var cameraObject = new GameObject("feedback camera");
            var root = new GameObject("feedback root", typeof(RectTransform), typeof(Canvas));
            var label = new GameObject("feedback", typeof(RectTransform));
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.pixelRect = new Rect(0, 0, 1000, 800);
                camera.transform.position = new Vector3(0, 0, -10);
                var rect = (RectTransform)root.transform;
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                label.transform.SetParent(rect, false);
                var view = label.AddComponent<Godmist.Views.UI.CombatFloatingFeedbackView>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = typeof(Godmist.Views.UI.CombatFloatingFeedbackView);
                Vector3? world = new Vector3(2, 1, 0);
                var layout = new Vector2(12, 20); var drift = new Vector2(5, 9);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                    camera.WorldToScreenPoint(world.Value), null, out var initial);
                type.GetField("startPosition", flags).SetValue(view, initial + layout);
                type.GetField("introStartPosition", flags).SetValue(view, initial + layout - Vector2.up * 18f);
                type.GetField("startTime", flags).SetValue(view, 123f);
                ((RectTransform)label.transform).anchoredPosition = initial + layout + drift;
                var lastWorld = world.Value;
                view.BindWorldAnchor(camera, rect, () => world, world.Value, layout);
                if (change == "camera" || change == "removed_unit") camera.transform.position += Vector3.right;
                if (change == "zoom") camera.orthographicSize = 3f;
                if (change == "unit") world += Vector3.up;
                if (change == "removed_unit") world = null;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                    camera.WorldToScreenPoint(world ?? lastWorld), null, out var expected);
                var refresh = type.GetMethod("RefreshWorldAnchor", flags);
                refresh.Invoke(view, null);
                refresh.Invoke(view, null); // Multiple Canvas callbacks must not accumulate offsets.
                Assert.That(Vector2.Distance(view.LayoutPosition, expected + layout), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(((RectTransform)label.transform).anchoredPosition,
                    expected + layout + drift), Is.LessThan(.001f));
                Assert.That(type.GetField("startTime", flags).GetValue(view), Is.EqualTo(123f));
            }
            finally { Object.DestroyImmediate(label); Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void FailedHostStart_DoesNotCreateDriverOrChangeFocus()
        {
            var root = new GameObject("invalid host test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, true);
                Assert.That(host.TryBegin(Combat("one")), Is.False);
                Assert.That(host.Drivers, Is.Empty);
                Assert.That(host.Session, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MultipleDrivers_FocusAndDispose_DoNotDestroyAnotherSession()
        {
            var root = new GameObject("combat host test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, true);
                typeof(CombatSandboxBootstrap).GetField("unitViewSpawner", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, spawner);
                var first = Combat("one"); var second = Combat("two");
                Assert.That(host.TryBegin(first), Is.True);
                Assert.That(host.TryBegin(second), Is.True);
                Assert.That(host.TryBegin(first), Is.False);
                host.Focus(second);
                var secondDispatcher = second.PassiveEffectEventDispatcher;
                host.End(first);
                Assert.That(host.Session, Is.SameAs(second));
                Assert.That(second.PassiveEffectEventDispatcher, Is.SameAs(secondDispatcher));
                host.End(second);
                Assert.That(host.Drivers, Is.Empty);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.TryBegin(Combat("third")), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
