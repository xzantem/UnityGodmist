using System;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Systems.Characters;
using UnityEditor;
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
    public partial class SeamlessDungeonCombatTests
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
            CurrentActionPoints = 700, AvailableKnives = 2,
            Character = new CharacterInstance
            {
                CharacterId = id, LifeState = CharacterLifeState.Alive,
                CurrentHealth = 12, Stats = new CharacterStats { Speed = new Stat(20f) }
            }
        };

        [Test]
        public void WideSessions_CanShareRoomWithoutSharingParticipants()
        {
            var game = Game();
            var first = Combat("a", "same"); first.IsDungeonWide = true; first.Units.Add(Unit("p1"));
            var second = Combat("b", "same"); second.IsDungeonWide = true; second.Units.Add(Unit("p2"));
            Assert.That(game.TryRegisterDungeonCombat(first), Is.True);
            Assert.That(game.TryRegisterDungeonCombat(second), Is.True);
            var duplicate = Combat("c", "same"); duplicate.IsDungeonWide = true; duplicate.Units.Add(Unit("p1"));
            Assert.That(game.TryRegisterDungeonCombat(duplicate), Is.False);
        }

        [Test]
        public void Merge_PreservesRemainingAV_AP_AndResumesWithoutTurnStart()
        {
            var first = Combat("first"); first.IsDungeonWide = true;
            var second = Combat("second"); second.IsDungeonWide = true;
            var a = Unit("a"); var b = Unit("b"); var waiting = Unit("waiting");
            a.CurrentActionValue = b.CurrentActionValue = 0;
            a.CurrentActionPoints = 200; b.CurrentActionPoints = 300; waiting.CurrentActionValue = 123;
            first.Units.Add(a); second.Units.AddRange(new[] { b, waiting });
            var pa = new CombatTurnParticipant(a); var pb = new CombatTurnParticipant(b);
            first.TurnParticipants.Add(pa); first.ActiveParticipant = pa; first.ActiveUnit = a;
            second.TurnParticipants.AddRange(new[] { pb, new CombatTurnParticipant(waiting) });
            second.ActiveParticipant = pb; second.ActiveUnit = b;
            b.BanditLeaderState = new BanditLeaderCombatState {Session=second};
            var delayed = new ScheduledCombatEffect("b", null, null, 0, 2, "a", null);
            second.ScheduledEffects.Add(delayed);
            Assert.That(DungeonCombatMembershipSystem.TryMerge(first, second), Is.True);
            Assert.That(first.ActiveUnit, Is.SameAs(a));
            Assert.That(b.WarriorState.Session,Is.SameAs(first));
            Assert.That(b.BanditLeaderState.Session,Is.SameAs(first));
            Assert.That(b.CurrentActionPoints, Is.EqualTo(300));
            Assert.That(waiting.CurrentActionValue, Is.EqualTo(123));
            Assert.That(first.ScheduledEffects[0], Is.SameAs(delayed));
            Assert.That(first.SuspendedTurns, Does.Contain(pb));
            a.CurrentActionValue = 500;
            first.ActiveParticipant = null; first.ActiveUnit = null;
            var starts = 0; var resumes = 0;
            first.ResumeTurn = participant => { Assert.That(participant, Is.SameAs(pb)); resumes++; };
            new CombatTurnSystem(onUnitTurnBegan: unit => starts++).AdvanceTurn(first);
            Assert.That(starts, Is.Zero); Assert.That(resumes, Is.EqualTo(1));
            Assert.That(b.CurrentActionPoints, Is.EqualTo(300));
            Assert.That(waiting.CurrentActionValue, Is.EqualTo(123));
            Assert.That(first.SuspendedTurns, Is.Empty);
        }

        [Test]
        public void Merge_RejectsDifferentFloorsAndDuplicateUnitsWithoutMutation()
        {
            var first = Combat("first"); first.IsDungeonWide = true; first.Units.Add(Unit("a"));
            var second = Combat("second"); second.IsDungeonWide = true; second.Units.Add(Unit("a"));
            Assert.That(DungeonCombatMembershipSystem.TryMerge(first, second), Is.False);
            second.Units[0].Character.CharacterId = "b";
            second.DungeonContext = new DungeonCombatContext("dungeon", 1, "second", "second", Vector2Int.zero,
                Vector2Int.zero, first.DungeonContext.WorldCells, null, null);
            Assert.That(DungeonCombatMembershipSystem.TryMerge(first, second), Is.False);
            Assert.That(first.Units.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnemyIdentity_QualifiesRepeatedAuthoredIds_WithoutMutatingPrerolledLoot()
        {
            var first = Combat("first"); first.IsDungeonWide = true;
            var second = Combat("second"); second.IsDungeonWide = true;
            var loot = new CombatRewardResult { EnemyRewards = new List<CombatEnemyReward>
                { new() { EnemyInstanceId = "guard", Gold = 17, ExperiencePerActiveCharacter = 3 } } };
            var a = Unit("guard", false); var b = Unit("guard", false);
            first.Units.Add(a); second.Units.Add(b);
            DungeonCombatMembershipSystem.RegisterEnemy(first, "one", "guard", loot, a);
            DungeonCombatMembershipSystem.RegisterEnemy(second, "two", "guard", loot, b);
            Assert.That(a.Character.CharacterId, Is.Not.EqualTo(b.Character.CharacterId));
            second.ExperienceGrantedEnemyIds.Add(b.Character.CharacterId);
            second.TryMarkEnemyDefeated(b.Character.CharacterId, new GridPosition(1, 1, 2));
            var grantsOnMerge = 0;
            first.EnemyDefeated = id => { if (!first.ExperienceGrantedEnemyIds.Contains(id)) grantsOnMerge++; };
            Assert.That(DungeonCombatMembershipSystem.TryMerge(first, second), Is.True);
            Assert.That(first.ExperienceGrantedEnemyIds, Does.Contain(b.Character.CharacterId));
            Assert.That(grantsOnMerge, Is.Zero, "Merging cannot repeat already awarded kill XP.");
            Assert.That(first.Rewards.EnemyRewards.Count, Is.EqualTo(2));
            Assert.That(first.TryGetEnemyDeathPosition(b.Character.CharacterId, out var death), Is.True);
            Assert.That(death, Is.EqualTo(new GridPosition(1, 1, 2)));
            Assert.That(loot.EnemyRewards[0].EnemyInstanceId, Is.EqualTo("guard"));
            Assert.That(first.EnemySources[b.Character.CharacterId].EncounterId, Is.EqualTo("two"));
        }

        [Test]
        public void WholeFloorContext_IncludesOtherRooms_AndHasNoRoomExit()
        {
            var definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "one"); map.TryAddFloorCell(Vector2Int.right, "two");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var dungeon = new DungeonInstance("d", 1); dungeon.TryAddFloor(floor);
                var encounter = new DungeonEncounterInstance("e", definition, "one", new RectInt(0, 0, 1, 1));
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context, true), Is.True);
                Assert.That(context.WorldCells.Count, Is.EqualTo(2));
                new DungeonBattleGridFactory().TryCreate(context, out var grid);
                var combat = new CombatSession { IsSeamless = true, IsDungeonWide = true, Grid = grid, DungeonContext = context };
                DungeonCombatArenaSystem.RefreshExits(combat, floor, new HashSet<Vector2Int>());
                Assert.That(combat.OpenExitEdges, Is.Empty);
                Assert.That(combat.IsExitPosition(new GridPosition(1, 0)), Is.False);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SpottedFactory_AddsOnlyRequestedEnemyAndPartyMember()
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                SetProperty(definition, "Id", "enemy.definition");
                SetProperty(definition, "BaseStats", new CharacterBaseStatsData());
                SetProperty(classData, "Id", "class.warrior"); SetProperty(classData, "ResourceType", ResourceType.Rage);
                var entries = new List<EncounterEnemyData>();
                foreach (var id in new[] { "guard", "hidden" })
                {
                    var data = new EncounterEnemyData(); SetProperty(data, "InstanceId", id);
                    SetProperty(data, "CharacterDefinition", definition); SetProperty(data, "ClassData", classData); entries.Add(data);
                }
                SetProperty(encounterDefinition, "Enemies", entries);
                var encounter = new DungeonEncounterInstance("one", encounterDefinition, "one", new RectInt(0, 0, 2, 2));
                var game = Game();
                foreach (var id in new[] { "spotted", "unseen" })
                {
                    var character = Unit(id).Character; character.AvailabilityState = CharacterAvailabilityState.Available;
                    game.TryRegisterCharacter(character); game.TryAddCharacterToActiveParty(id);
                }
                Assert.That(new CombatSessionFactory().TryCreateSeamless(game, encounter, Combat("one").DungeonContext,
                    new Dictionary<string, Vector2Int> { ["spotted"] = Vector2Int.zero },
                    new Dictionary<string, Vector2Int> { ["guard"] = Vector2Int.right }, out var session, out _, true), Is.True);
                Assert.That(session.Units.Count, Is.EqualTo(2));
                Assert.That(session.Units.Exists(unit => unit.Character.CharacterId == "unseen"), Is.False);
                Assert.That(session.EnemySources.Count, Is.EqualTo(1));
                Assert.That(session.Rewards.EnemyRewards.Exists(reward => reward.EnemyInstanceId == "hidden"), Is.False);
            }
            finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(classData); Object.DestroyImmediate(encounterDefinition); }
        }

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
            enemy.CurrentActionPoints = 200;
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, enemy, player), Is.False);
            enemy.CurrentActionPoints = 300;
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
            unit.CurrentActionPoints = ap * 100;
            unit.MovementStepsThisTurn = 2;
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), DungeonWallType.Low);
            var system = new CombatMovementSystem();
            var paths = system.CalculateReachablePaths(session, unit);
            Assert.That(paths.ContainsKey(new GridPosition(1, 0)), Is.EqualTo(succeeds));
            Assert.That(system.TryMoveOneStep(session, unit, 1, 0), Is.EqualTo(succeeds));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(succeeds ? 0 : ap * 100));
            Assert.That(unit.MovementStepsThisTurn, Is.EqualTo(succeeds ? 3 : 2));
        }

        [Test]
        public void LowWall_ReachabilitySelectsCheaperEqualLengthPath()
        {
            var session = Combat("one");
            var unit = Unit("player");
            session.Units.Add(unit);
            unit.CurrentActionPoints = 300;
            session.Grid.SetWallEdge(new(0, 0), new(1, 0), DungeonWallType.Low);
            var paths = new CombatMovementSystem().CalculateReachablePaths(session, unit);
            Assert.That(paths[new GridPosition(1, 1)], Is.EqualTo(new[] { new GridPosition(0, 1), new GridPosition(1, 1) }));
            Assert.That(CombatMovementSystem.CalculatePathCost(session, unit.Position, 0,
                paths[new GridPosition(1, 1)]), Is.EqualTo(300));
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
            unit.CurrentActionPoints = 300;
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
            Assert.That(player.CurrentActionPoints, Is.EqualTo(700));
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
            Assert.That(joining.CurrentActionPoints, Is.EqualTo(700));
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

        private static DungeonFloorInstance CreateSpawnFloor(DungeonFloorMap map)
        {
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);return floor;
        }

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void ExplorationEnemySpawnHasUniqueRuntimeAtDungeonLevelAndReceivesTrapDamage()
        {
            var root = new GameObject("enemy spawn test");
            var prefab = new GameObject("enemy prefab");
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            var trapDefinition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();
            var spawned = new List<ExplorationCharacterView>();
            try
            {
                SetProperty(definition, "Id", "enemy.definition");
                var baseStats = new CharacterBaseStatsData();
                SetProperty(baseStats, "MaxHealth", 1000);
                SetProperty(definition, "BaseStats", baseStats);
                SetProperty(classData, "Id", "class.warrior");
                SetProperty(classData, "ResourceType", ResourceType.Rage);
                var enemies = new List<EncounterEnemyData>();
                foreach (var id in new[] { "enemy.one", "enemy.two" })
                {
                    var data = new EncounterEnemyData();
                    SetProperty(data, "InstanceId", id);
                    SetProperty(data, "CharacterDefinition", definition);
                    SetProperty(data, "ClassData", classData);
                    enemies.Add(data);
                }
                SetProperty(encounterDefinition, "Enemies", enemies);
                var encounter = new DungeonEncounterInstance("encounter", encounterDefinition, "room", new RectInt(0, 0, 4, 1));
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var mapView = root.AddComponent<DungeonFloorMapView>();
                var spawner = root.AddComponent<DungeonEncounterSpawner>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(DungeonEncounterSpawner).GetField("floorMapView", flags).SetValue(spawner, mapView);
                typeof(DungeonEncounterSpawner).GetField("enemyViewPrefab", flags).SetValue(spawner, prefab.AddComponent<ExplorationCharacterView>());
                spawned = (List<ExplorationCharacterView>)typeof(DungeonEncounterSpawner)
                    .GetMethod("SpawnEnemyViews", flags).Invoke(spawner, new object[] { encounter, CreateSpawnFloor(map), 11 });
                Assert.That(spawned.Count, Is.EqualTo(2));
                for (var i = 0; i < spawned.Count; i++)
                {
                    Assert.That(spawned[i].RuntimeCharacter, Is.Not.Null);
                    Assert.That(spawned[i].RuntimeCharacter.Level, Is.EqualTo(11));
                    Assert.That(spawned[i].RuntimeCharacter.CharacterId, Is.EqualTo(enemies[i].InstanceId));
                    Assert.That(spawned[i].CombatUnit, Is.Null);
                }
                Assert.That(spawned[0].RuntimeCharacter, Is.Not.SameAs(spawned[1].RuntimeCharacter));
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.TryAddObject(new SpikeTrapDungeonObjectInstance("trap", trapDefinition, Vector2Int.zero));
                var unit = new BattleUnit { Character = spawned[0].RuntimeCharacter, IsPlayerSide = false };
                var health = unit.Character.CurrentHealth;
                new Godmist.Systems.Dungeons.DungeonTrapSystem(floor, 11).EnterCell(Vector2Int.zero,
                    new[] { new Godmist.Systems.Dungeons.DungeonTrapOccupant(unit, null, Vector2Int.zero) });
                Assert.That(unit.Character.CurrentHealth, Is.LessThan(health));
                spawned[0].BindCombat(unit);
                Assert.That(spawned[0].CombatUnit.Character, Is.SameAs(unit.Character));
                Assert.That(spawned[0].RuntimeCharacter.CurrentHealth, Is.LessThan(health));
            }
            finally
            {
                foreach (var view in spawned) if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(root); Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(definition); Object.DestroyImmediate(classData);
                Object.DestroyImmediate(encounterDefinition); Object.DestroyImmediate(trapDefinition);
            }
        }

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

        [Test]
        public void BorrowedCombatMovement_IgnoresExplorationCollisionGate()
        {
            var root = new GameObject("borrowed combat movement");
            var actorObject = new GameObject("borrowed combat actor");
            var obstacle = new GameObject("exploration obstacle");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var mapView = root.AddComponent<DungeonFloorMapView>();
                typeof(CombatUnitViewSpawner).GetField("sandboxBootstrap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, host);
                typeof(CombatUnitViewSpawner).GetField("battleUnitPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, root.AddComponent<Godmist.Views.Characters.CharacterView>());
                var terrainMap = new DungeonFloorMap();
                terrainMap.TryAddFloorCell(Vector2Int.zero, "room");
                terrainMap.TryAddFloorCell(Vector2Int.right, "room");
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                actor.ConfigureTerrain(terrainMap, mapView);
                actorObject.transform.position = Vector2.zero;
                var collider = obstacle.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(.2f, 1f);
                obstacle.transform.position = new Vector2(.5f, 0f);
                Physics2D.SyncTransforms();

                Assert.That(actor.HasClearMovementSegment(Vector2.zero, Vector2.right), Is.False,
                    "The fixture must exercise the exploration collision gate.");

                var game = Game();
                var session = Combat("borrowed-movement");
                var unit = Unit("borrowed-movement-unit", player: false);
                session.Units.Add(unit);
                Assert.That(game.TryRegisterDungeonCombat(session), Is.True);
                spawner.ConfigureDungeon(game, mapView);
                spawner.Borrow(session, unit, actor);
                Assert.That(actor.CombatUnit, Is.SameAs(unit));
                Assert.That(spawner.TryBeginPresentationMovement(unit), Is.True);
                Assert.That(spawner.TryMovePresentationStep(unit, new GridPosition(1, 0)), Is.True);

                var synchronize = typeof(CombatUnitViewSpawner).GetMethod(
                    "SynchronizeDungeon", BindingFlags.NonPublic | BindingFlags.Instance);
                synchronize.Invoke(spawner, new object[] { .1f });
                Assert.That(actor.Position.x, Is.EqualTo(.6f).Within(.001f));
                synchronize.Invoke(spawner, new object[] { .1f });

                Assert.That(actor.Position.x, Is.EqualTo(1f).Within(.001f),
                    "The borrowed combat presentation must follow the already validated logical step.");
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(obstacle);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InactiveBorrowedView_DoesNotKeepCombatMovementInProgress()
        {
            var root = new GameObject("inactive borrowed movement");
            var actor = new GameObject("inactive borrowed actor");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var game = Game();
                var session = Combat("inactive-borrowed-movement");
                var unit = Unit("inactive-borrowed-movement-unit", player: false);
                session.Units.Add(unit);
                Assert.That(game.TryRegisterDungeonCombat(session), Is.True);

                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var mapView = root.AddComponent<DungeonFloorMapView>();
                var view = actor.AddComponent<ExplorationCharacterView>();
                typeof(CombatUnitViewSpawner).GetField("sandboxBootstrap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, host);
                spawner.ConfigureDungeon(game, mapView);
                spawner.Borrow(session, unit, view);
                Assert.That(spawner.TryBeginPresentationMovement(unit), Is.True);
                Assert.That(spawner.TryMovePresentationStep(unit, new GridPosition(1, 0)), Is.True);

                view.gameObject.SetActive(false);

                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
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
        private static BattleUnit OpeningMage(string id, bool player = true, int x = 1)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset");
            var character = new CharacterInstanceFactory().Create(definition);
            character.CharacterId = id; character.Stats.Speed = new Stat(50);
            character.Stats.MaxHealth = new Stat(10000); character.CurrentHealth = 10000;
            character.Stats.WeaponAccuracy = new Stat(10000); character.WeaponAccuracy = 10000;
            return new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(x, 2),
                MaxActionPoints = 1250, CurrentActionPoints = 1250 };
        }
        private static SkillData OpeningSkill(string name) => AssetDatabase.LoadAssetAtPath<SkillData>(
            "Assets/_Project/ScriptableObjects/Skills/SD_Mage" + name + ".asset");
        private static CombatSession OpeningSession(params BattleUnit[] units) => new()
        { IsSeamless = true, IsDungeonWide = true, CombatId = Guid.NewGuid().ToString(), Grid = new BattleGrid(12, 8), Units = units.ToList(), ActiveUnit = units[0], ActionPointsPerSpeed = .25f };
        private static CombatSandboxBootstrap OpeningHost(GameObject root, bool real = false)
        {
            var host = root.AddComponent<CombatSandboxBootstrap>();
            typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, true);
            if (real)
            {
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                typeof(CombatSandboxBootstrap).GetField("unitViewSpawner", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, spawner);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab");
                typeof(CombatUnitViewSpawner).GetField("battleUnitPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, prefab.GetComponent<Godmist.Views.Characters.CharacterView>());
                typeof(CombatUnitViewSpawner).GetField("unitsRoot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, root.transform);
            }
            return host;
        }

        [TestCase(0f, 400f)] [TestCase(.25f, 350f)] [TestCase(.75f, 250f)]
        [TestCase(1f, 200f)] [TestCase(-1f, 400f)] [TestCase(2f, 200f)]
        [TestCase(float.NaN, 200f)]
        public void OpeningDelayUsesEachEnemyDetectionAndNeverChangesSpeed(float detection, float expected)
        {
            var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 4); var ally = OpeningMage("ally", true, 2);
            var session = OpeningSession(actor, enemy, ally);
            new CombatTurnSystem().InitializeTurnOrder(session, actor, new Dictionary<BattleUnit, float> { [enemy] = detection });
            Assert.That(session.ActiveUnit, Is.SameAs(actor));
            Assert.That(enemy.CurrentActionValue, Is.EqualTo(expected)); Assert.That(ally.CurrentActionValue, Is.EqualTo(200));
            Assert.That(enemy.Character.Stats.Speed.GetValue(), Is.EqualTo(50));
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(1250));
        }
        [Test]
        public void OpeningMixedDetectionDoesNotDelayLaterArrivalsOrRepeatAfterEnemyTurn()
        {
            var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 4); var alert = OpeningMage("alert", false, 5);
            var session = OpeningSession(actor, enemy, alert); var turns = new CombatTurnSystem();
            turns.InitializeTurnOrder(session, actor, new Dictionary<BattleUnit, float> { [enemy] = 0, [alert] = .75f });
            Assert.That(enemy.CurrentActionValue, Is.EqualTo(400)); Assert.That(alert.CurrentActionValue, Is.EqualTo(250));
            var arrival = OpeningMage("arrival", false, 6);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, arrival, arrival.Position), Is.True);
            Assert.That(arrival.CurrentActionValue, Is.EqualTo(200));
            for (var i = 0; i < 12 && session.ActiveUnit != enemy; i++) turns.EndActiveTurn(session);
            Assert.That(session.ActiveUnit, Is.SameAs(enemy));
            turns.EndActiveTurn(session);
            Assert.That(enemy.CurrentActionValue, Is.LessThanOrEqualTo(200));
        }
        [Test]
        public void OpeningGroupingIncludesRoomAlarmAndVisiblePartyInsteadOfArtificialDuel()
        {
            var actor = OpeningMage("actor"); var ally = OpeningMage("ally", true, 2);
            var third = OpeningMage("third", true, 3); var fourth = OpeningMage("fourth", true, 4);
            var distant = OpeningMage("distant", true, 10); var anchor = OpeningMage("anchor", false, 5);
            var peer = OpeningMage("peer", false, 6); var unrelated = OpeningMage("other-room", false, 11);
            var session = OpeningSession(actor, ally, third, fourth, distant, anchor, peer, unrelated);
            var group = DungeonCombatOpeningSystem.CollectParticipants(session, actor, new[] { anchor }, anchor,
                u => new[] { anchor, peer }, (enemy, player) => player != distant);
            Assert.That(group, Is.EquivalentTo(new[] { actor, ally, third, fourth, anchor, peer }));
            Assert.That(group, Has.No.Member(unrelated)); Assert.That(group, Has.No.Member(distant));
        }
        [Test]
        public void OpeningGroupIncludesSecondaryAreaVictimsAndTheirRoomAlerts()
        {
            var actor = OpeningMage("actor"); var first = OpeningMage("first", false, 3);
            var second = OpeningMage("second", false, 4); var peer = OpeningMage("peer", false, 5);
            var session = OpeningSession(actor, first, second, peer);
            var group = DungeonCombatOpeningSystem.CollectParticipants(session, actor, new[] { first, second }, first,
                u => u == second ? new[] { peer } : Array.Empty<BattleUnit>(), (e, a) => false);
            Assert.That(group, Is.EquivalentTo(session.Units));
        }
        [TestCase("EnergyOrb")] [TestCase("Fireball")] [TestCase("Shield")]
        public void OpeningPreviewUsesProductionSkillTargetingWithoutCostsTurnsOrCombatRegistration(string name)
        {
            var root = new GameObject("opening preview");
            try
            {
                var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 3);
                var session = OpeningSession(actor, enemy); var host = OpeningHost(root);
                var commits = 0; var skill = OpeningSkill(name); var ap = actor.CurrentActionPoints;
                var mana = actor.Character.ResourcePool.CurrentValue; var hp = enemy.Character.CurrentHealth;
                Assert.That(host.BeginOpeningPreview(session, (s,p,t,o,r,c) => { commits++; return false; }, u => true), Is.True);
                var positions = new Dictionary<int, GridPosition?> { [0] = name == "Shield" ? actor.Position : enemy.Position };
                Assert.That(host.OpeningPreview.ValidateOpeningSkill(skill, positions, null, null, null, null, out var targets), Is.True);
                Assert.That(targets.Values.SelectMany(v => v), Has.Member(name == "Shield" ? actor : enemy));
                Assert.That(host.TryExecuteSkill(skill, positions), Is.False);
                Assert.That(commits, Is.EqualTo(1)); Assert.That(host.Drivers, Is.Empty);
                Assert.That(session.TurnParticipants, Is.Empty); Assert.That(session.ActiveTurnIndex, Is.Zero);
                Assert.That(session.PassiveEffectEventDispatcher, Is.Null); Assert.That(session.EnemyHealthChanged, Is.Null);
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap)); Assert.That(actor.Character.ResourcePool.CurrentValue, Is.EqualTo(mana));
                Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(hp));
                Assert.That(host.CanEndTurn(), Is.False);
                host.CancelOpeningPreview(); Assert.That(host.Session, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase("range")] [TestCase("LOS")] [TestCase("AP")] [TestCase("mana")] [TestCase("unknown")]
        public void InvalidOpeningSelectionIsRejectedWithoutCosts(string reason)
        {
            var root = new GameObject("invalid opening");
            try
            {
                var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 3);
                var session = OpeningSession(actor, enemy); var host = OpeningHost(root); var skill = OpeningSkill("EnergyOrb");
                if (reason == "range") enemy.Position = new GridPosition(11, 7);
                if (reason == "LOS") session.EnvironmentBlocksLineOfSight = (a,b) => true;
                if (reason == "AP") actor.CurrentActionPoints = 0;
                if (reason == "mana") actor.Character.ResourcePool.CurrentValue = 0;
                if (reason == "unknown") skill = OpeningSkill("FireAura");
                var ap = actor.CurrentActionPoints; var mana = actor.Character.ResourcePool.CurrentValue;
                host.BeginOpeningPreview(session, (s,p,t,o,r,c) => false, u => true);
                Assert.That(host.OpeningPreview.ValidateOpeningSkill(skill,
                    new Dictionary<int, GridPosition?> { [0] = enemy.Position }, null, null, null, null, out _), Is.False);
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap)); Assert.That(actor.Character.ResourcePool.CurrentValue, Is.EqualTo(mana));
                Assert.That(host.Drivers, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void OpeningMissStillPaysTheRealFirstTurnCostsAndKeepsTheFight()
        {
            var root = new GameObject("opening miss");
            try
            {
                var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 3);
                enemy.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { UniqueBuffType = UniqueBuffType.GuaranteedDodge });
                var session = OpeningSession(actor, enemy); var host = OpeningHost(root, true); var skill = OpeningSkill("EnergyOrb");
                var hp = enemy.Character.CurrentHealth;
                Assert.That(host.TryBegin(session), Is.True);
                var driver = host.GetDriver(session); driver.StartTurns(actor, new Dictionary<BattleUnit, float> { [enemy] = .25f });
                var ap = actor.CurrentActionPoints; var mana = actor.Character.ResourcePool.CurrentValue;
                var cost = CombatActionCostCalculator.CalculateSkillCost(actor, skill);
                var resource = PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor, skill);
                Assert.That(driver.TryExecuteSkill(skill, enemy.Position), Is.True);
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap - cost));
                Assert.That(actor.Character.ResourcePool.CurrentValue, Is.EqualTo(mana - resource));
                Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(hp));
                Assert.That(session.ActiveUnit, Is.SameAs(actor)); Assert.That(session.ActiveTurnIndex, Is.Zero);
                Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Ongoing)); Assert.That(host.Drivers.Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase("MultipleChosen")] [TestCase("Chain")] [TestCase("TwoModes")]
        public void OpeningPreparationKeepsAdvancedTargetModesAndSelections(string kind)
        {
            var root = new GameObject("advanced opening"); var skill = Object.Instantiate(OpeningSkill("EnergyOrb"));
            try
            {
                var actor = OpeningMage("actor"); var first = OpeningMage("first", false, 3); var second = OpeningMage("second", false, 4);
                SetProperty(skill, "SkillId", "skill.test.opening." + kind);
                var mode = JsonUtility.FromJson<SkillTargetModeData>(JsonUtility.ToJson(skill.TargetModes[0]));
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(SkillTargetModeData).GetField("absoluteRange", flags).SetValue(mode, new SkillRangeData(RangeShape.Square, 1, 8));
                typeof(SkillTargetModeData).GetField("chainTargets", flags).SetValue(mode, 3);
                if (kind != "TwoModes")
                {
                    typeof(SkillTargetModeData).GetField("targetType", flags).SetValue(mode,
                        kind == "Chain" ? TargetType.Chain : TargetType.MultipleChosen);
                    typeof(SkillTargetModeData).GetField("relativeRange", flags).SetValue(mode, new SkillRangeData(RangeShape.Square, 0, 3));
                }
                var modes = new List<SkillTargetModeData> { mode };
                if (kind == "TwoModes")
                {
                    var casterMode = new SkillTargetModeData();
                    typeof(SkillTargetModeData).GetField("targetType", flags).SetValue(casterMode, TargetType.Caster);
                    modes.Add(casterMode);
                }
                typeof(SkillData).GetField("targetModes", flags).SetValue(skill, modes);
                actor.Character.Skills.Add(skill);
                Assert.That(actor.Character.ActiveSkillLoadout.TryInitialize(actor.Character.GetKnownSkills().Select(v => v.SkillId),
                    actor.Character.GetBasicAttackSkill().SkillId, new[] { actor.Character.GetBasicAttackSkill().SkillId, skill.SkillId }), Is.True);
                var session = OpeningSession(actor, first, second); var host = OpeningHost(root);
                host.BeginOpeningPreview(session, (s,p,t,o,r,c) => false, u => true);
                var positions = new Dictionary<int, GridPosition?> { [0] = first.Position };
                if (kind == "TwoModes") positions[1] = null;
                var chosen = kind == "MultipleChosen" ? new Dictionary<int, IReadOnlyList<GridPosition>>
                    { [0] = new[] { first.Position, second.Position } } : null;
                Assert.That(host.OpeningPreview.ValidateOpeningSkill(skill, positions, null, null, null, chosen, out var targets), Is.True);
                Assert.That(targets[0], Has.Member(first));
                if (kind == "TwoModes") Assert.That(targets[1], Is.EquivalentTo(new[] { actor }));
                else Assert.That(targets[0], Is.EquivalentTo(new[] { first, second }));
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(1250)); Assert.That(first.Character.CurrentHealth, Is.EqualTo(10000));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(skill); }
        }
        [Test]
        public void OpeningValidationSharesOwnedSummonRequirementWithExecution()
        {
            var root = new GameObject("summon prerequisite"); var skill = Object.Instantiate(OpeningSkill("EnergyOrb"));
            try
            {
                var actor = OpeningMage("actor"); var enemy = OpeningMage("enemy", false, 3);
                SetProperty(skill, "SkillId", "skill.test.opening.summon");
                typeof(Godmist.Data.Effects.SkillEffectData).GetField("damagePerOwnedSummonGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(skill.Effects[0], "required-summon");
                actor.Character.Skills.Add(skill);
                actor.Character.ActiveSkillLoadout.TryInitialize(actor.Character.GetKnownSkills().Select(v => v.SkillId),
                    actor.Character.GetBasicAttackSkill().SkillId, new[] { actor.Character.GetBasicAttackSkill().SkillId, skill.SkillId });
                var session = OpeningSession(actor, enemy); var host = OpeningHost(root);
                host.BeginOpeningPreview(session, (s,p,t,o,r,c) => false, u => true);
                Assert.That(host.OpeningPreview.ValidateOpeningSkill(skill, new Dictionary<int, GridPosition?> { [0] = enemy.Position },
                    null, null, null, null, out _), Is.False);
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(1250)); Assert.That(actor.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(skill); }
        }
        [Test]
        public void SceneOwnerShowsStartingKnivesBeforeFirstCombatAndPreservesRetainedSpentKnives()
        {
            var root=new GameObject("exploration scout HUD"); DungeonCombatCoordinator coordinator=null;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            void Field(object owner,string name,object value)=>owner.GetType().GetField(name,flags).SetValue(owner,value);
            try
            {
                var game=new GameSession(GameModeState.Exploration,"scout.hud");
                var character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
                game.TryRegisterCharacter(character);game.TryAddCharacterToActiveParty(character.CharacterId);game.TrySetActiveLeader(character.CharacterId);
                var view=root.AddComponent<ExplorationCharacterView>();view.Bind(character);
                var party=root.AddComponent<ExplorationPartyController>();party.Initialize(game,new List<ExplorationCharacterView>{view});
                coordinator=root.AddComponent<DungeonCombatCoordinator>();Field(coordinator,"game",game);Field(coordinator,"party",party);Field(coordinator,"initialized",true);
                var retained=new CombatSessionFactory().CreateJoiningUnit(character,new GridPosition(0,0));
                ((Dictionary<string,BattleUnit>)typeof(DungeonCombatCoordinator).GetField("retainedPlayers",flags).GetValue(coordinator)).Add(character.CharacterId,retained);
                var health=character.CurrentHealth;var resource=character.ResourcePool.CurrentValue;
                var display=coordinator.GetExplorationSkillActor();
                Assert.That(display.AvailableKnives,Is.GreaterThan(0));Assert.That(display.AvailableKnives,Is.EqualTo(display.MaxKnives));
                Assert.That(retained.AvailableKnives,Is.Zero);Assert.That(retained.HasInitializedCombatResources,Is.False);
                retained.HasInitializedCombatResources=true;retained.MaxKnives=7;retained.AvailableKnives=2;
                Field(coordinator,"nextExplorationSkillRefresh",0f);
                Assert.That(coordinator.GetExplorationSkillActor(),Is.SameAs(display));
                Assert.That(display.MaxKnives,Is.EqualTo(7));Assert.That(display.AvailableKnives,Is.EqualTo(2));
                Assert.That(character.CurrentHealth,Is.EqualTo(health));Assert.That(character.ResourcePool.CurrentValue,Is.EqualTo(resource));
                Assert.That(game.Combats,Is.Empty);
            }
            finally{if(coordinator!=null)Field(coordinator,"initialized",false);Object.DestroyImmediate(root);}
        }

        [TestCase(false, false, false, false)] [TestCase(true, false, false, false)] [TestCase(true, true, false, false)]
        [TestCase(false, false, true, false)] [TestCase(true, false, true, false)] [TestCase(true, true, true, false)]
        [TestCase(true, false, true, true)]
        public void SceneOwnerPreparesCancelsOrCommitsFullPartyRoomFight(bool commit, bool stackedFollowers, bool useSkillBar, bool differentTargetEncounter)
        {
            var root = new GameObject("opening scene owner"); var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            DungeonCombatCoordinator coordinator = null;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Field(object owner, string name, object value) => owner.GetType().GetField(name, flags).SetValue(owner, value);
            GameObject Child(string name) { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
            try
            {
                root.AddComponent<Camera>().orthographic = true;
                var camera = root.AddComponent<CombatCameraFramer>();
                var host = OpeningHost(root, true); var spawner = root.GetComponent<CombatUnitViewSpawner>();
                var game = new GameSession(GameModeState.Exploration, "opening.scene");
                var dungeon = new DungeonInstance("opening.dungeon", 7); var floor = new DungeonFloorInstance(0); var model = new DungeonFloorMap();
                for (var x = 0; x < 8; x++) for (var y = 0; y < 8; y++) model.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(model); dungeon.TryAddFloor(floor); game.TrySetActiveDungeon(dungeon);
                var map = root.AddComponent<DungeonFloorMapView>();
                var gridRoot = Child("grid"); gridRoot.AddComponent<Grid>();
                var tileObject = new GameObject("tilemap", typeof(UnityEngine.Tilemaps.Tilemap)); tileObject.transform.SetParent(gridRoot.transform);
                Field(map, "floorTilemap", tileObject.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                spawner.ConfigureDungeon(game,map);
                var party = root.AddComponent<ExplorationPartyController>();
                var playerViews = new List<ExplorationCharacterView>();
                for (var n = 0; n < 4; n++)
                {
                    var unit = OpeningMage("player" + n); game.TryRegisterCharacter(unit.Character); game.TryAddCharacterToActiveParty(unit.Character.CharacterId);
                    var view = Child(unit.Character.CharacterId).AddComponent<ExplorationCharacterView>(); view.Bind(unit.Character);
                    view.SetPosition(new Vector2(1.5f, stackedFollowers && n == 1 ? .5f : n + .5f)); playerViews.Add(view);
                }
                party.Initialize(game, playerViews);
                var enemies = new List<ExplorationCharacterView>(); var entries = new List<EncounterEnemyData>();
                foreach (var id in new[] { "guard", "peer" })
                {
                    var unit = OpeningMage(DungeonCombatMembershipSystem.EnemyKey("encounter", id), false);
                    var view = Child(id).AddComponent<ExplorationCharacterView>(); view.Bind(unit.Character); view.SetEnemyInstanceId(id);
                    view.SetPosition(new Vector2(3.5f, id == "guard" ? .5f : 1.5f)); enemies.Add(view);
                    var entry = new EncounterEnemyData(); SetProperty(entry,"InstanceId",id); SetProperty(entry,"CharacterDefinition",unit.Character.Definition);
                    SetProperty(entry,"ClassData",unit.Character.ClassData); entries.Add(entry);
                }
                SetProperty(encounterDefinition,"Enemies",entries);
                var encounter = new DungeonEncounterInstance("encounter",encounterDefinition,"room",new RectInt(0,0,8,8));
                dungeon.TryStorePreRolledRewards("encounter",new CombatRewardResult { EnemyRewards = new()
                    { new CombatEnemyReward { EnemyInstanceId="guard", Gold=3 }, new CombatEnemyReward { EnemyInstanceId="peer", Gold=5 } } });
                var trigger = Child("trigger").AddComponent<EncounterTriggerView>();
                trigger.Initialize(party,model,map,encounter,differentTargetEncounter?new List<ExplorationCharacterView>{enemies[0]}:enemies);
                var encounters = root.AddComponent<DungeonEncounterSpawner>();
                var triggers=(List<EncounterTriggerView>)typeof(DungeonEncounterSpawner).GetField("spawnedTriggers",flags).GetValue(encounters);
                triggers.Add(trigger);
                if(differentTargetEncounter)
                {
                    var other=new DungeonEncounterInstance("other.encounter",encounterDefinition,"room",new RectInt(0,0,8,8));
                    var otherTrigger=Child("other trigger").AddComponent<EncounterTriggerView>();
                    otherTrigger.Initialize(party,model,map,other,new List<ExplorationCharacterView>{enemies[1]});triggers.Add(otherTrigger);
                    dungeon.TryStorePreRolledRewards("other.encounter",new CombatRewardResult { EnemyRewards = new()
                        { new CombatEnemyReward { EnemyInstanceId="peer", Gold=5 } } });
                }
                var targeting = root.AddComponent<CombatTargetSelectionController>(); Field(targeting,"sandboxBootstrap",host);
                var movement = root.AddComponent<CombatMovementInputController>(); var inspect = root.AddComponent<CombatUnitInspectController>();
                var feedback = root.AddComponent<CombatGridFeedbackRenderer>();
                var follow = root.AddComponent<ExplorationCameraFollow>();
                var hud = Child("combat HUD"); hud.SetActive(false);
                coordinator = root.AddComponent<DungeonCombatCoordinator>();
                foreach (var pair in new Dictionary<string,object> { ["game"]=game,["floor"]=floor,["map"]=map,["party"]=party,["encounters"]=encounters,
                    ["combatHost"]=host,["unitViews"]=spawner,["targeting"]=targeting,["movementInput"]=movement,["inspectInput"]=inspect,
                    ["gridFeedback"]=feedback,["combatHudRoot"]=hud,["explorationHudRoot"]=Child("exploration HUD"),["combatCamera"]=camera,
                    ["explorationCamera"]=follow,["initialized"]=true }) Field(coordinator,pair.Key,pair.Value);
                party.SetCombatCoordinator(coordinator);
                var mana = playerViews[0].RuntimeCharacter.ResourcePool.CurrentValue;
                floor.GetEnemyAwareness(DungeonCombatMembershipSystem.EnemyKey("encounter","guard")).TryRestore(.25f);
                floor.GetEnemyAwareness(DungeonCombatMembershipSystem.EnemyKey(differentTargetEncounter?"other.encounter":"encounter","peer")).TryRestore(.75f);
                Godmist.Views.UI.CombatSkillBarView skillBar = null;
                Godmist.Views.UI.CombatSkillSlotView openingSlot = null;
                if (useSkillBar)
                {
                    var barObject = new GameObject("exploration skills", typeof(RectTransform)); barObject.transform.SetParent(hud.transform);
                    skillBar = barObject.AddComponent<Godmist.Views.UI.CombatSkillBarView>();
                    var nativeBar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab")
                        .GetComponentInChildren<Godmist.Views.UI.CombatSkillBarView>(true);
                    Field(skillBar,"skillSlotTemplate",new SerializedObject(nativeBar).FindProperty("skillSlotTemplate").objectReferenceValue);
                    Field(skillBar,"skillBarRoot",barObject.GetComponent<RectTransform>());
                    Field(skillBar,"sandboxBootstrap",host); Field(skillBar,"targetSelectionController",targeting);
                    skillBar.BindExploration(coordinator.GetExplorationSkillActor, skill => coordinator.TrySelectOpeningSkill(skill));
                    typeof(DungeonCombatCoordinator).GetMethod("SetFocus",flags).Invoke(coordinator,new object[]{null,true});
                    var displayActor = coordinator.GetExplorationSkillActor();
                    for(int i=0;i<100;i++) Assert.That(coordinator.GetExplorationSkillActor(),Is.SameAs(displayActor));
                    Assert.That(host.Session,Is.Null); Assert.That(host.Drivers,Is.Empty); Assert.That(game.Combats,Is.Empty);
                    Assert.That(playerViews[0].RuntimeCharacter.ResourcePool.CurrentValue,Is.EqualTo(mana));
                    Assert.That(hud.activeSelf,Is.True);
                    typeof(Godmist.Views.UI.CombatSkillBarView).GetMethod("Update",flags).Invoke(skillBar,null);
                    var slots = skillBar.GetComponentsInChildren<Godmist.Views.UI.CombatSkillSlotView>();
                    openingSlot = slots.First(slot => (SkillData)slot.GetType().GetField("skill",flags).GetValue(slot)==OpeningSkill("EnergyOrb"));
                    Assert.That(openingSlot.TryActivate(),Is.True);
                    Assert.That(targeting.SelectedSkill,Is.SameAs(OpeningSkill("EnergyOrb")));
                    typeof(Godmist.Views.UI.CombatSkillBarView).GetMethod("Update",flags).Invoke(skillBar,null);
                    Assert.That(targeting.SelectedSkill,Is.SameAs(OpeningSkill("EnergyOrb")),"Showing the preview must not rebuild/cancel the selected skill.");
                    Assert.That(skillBar.GetComponentsInChildren<Godmist.Views.UI.CombatSkillSlotView>(),Is.EqualTo(slots));
                    Assert.That(party.CanAcceptExplorationInput,Is.False);
                    var position = playerViews[0].Position;
                    typeof(ExplorationPartyController).GetMethod("FixedUpdateCore",flags).Invoke(party,null);
                    Assert.That(playerViews[0].Position,Is.EqualTo(position));
                }
                else Assert.That(coordinator.TryPrepareOpening(enemies[0]),Is.True);
                Assert.That(host.IsOpeningPreview,Is.True); Assert.That(hud.activeSelf,Is.True);
                Assert.That(game.Combats,Is.Empty); Assert.That(host.Drivers,Is.Empty);
                Assert.That(playerViews[0].RuntimeCharacter.ResourcePool.CurrentValue,Is.EqualTo(mana));
                if (!commit)
                {
                    if(useSkillBar)
                    {
                        targeting.CancelTargetSelection();
                        Field(targeting,"<ConsumedPrimaryClickThisFrame>k__BackingField",true);
                        typeof(DungeonCombatCoordinator).GetMethod("UpdateOpeningPreparation",flags).Invoke(coordinator,null);
                        Assert.That(party.IsWorldInteractionClaimedThisFrame,Is.True,"An attack click must not become a movement/interaction click after cancelling targeting.");
                    }
                    else typeof(DungeonCombatCoordinator).GetMethod("CancelOpeningPreparation",flags).Invoke(coordinator,new object[]{true});
                    Assert.That(host.Session,Is.Null); Assert.That(game.Combats,Is.Empty); Assert.That(host.Drivers,Is.Empty);
                    Assert.That(hud.activeSelf,Is.True); Assert.That(playerViews.All(v=>v.CombatUnit==null),Is.True);
                    if(useSkillBar)
                    {
                        Assert.That(party.CanAcceptExplorationInput,Is.True);
                        Assert.That(openingSlot.TryActivate(),Is.True);
                        Assert.That(host.IsOpeningPreview,Is.True);
                        game.TrySetActiveLeader(playerViews[1].CharacterId); Field(coordinator,"nextOpeningRefresh",0f);
                        typeof(DungeonCombatCoordinator).GetMethod("UpdateOpeningPreparation",flags).Invoke(coordinator,null);
                        Assert.That(host.Session,Is.Null);
                        Assert.That(coordinator.GetExplorationSkillActor().Character,Is.SameAs(playerViews[1].RuntimeCharacter));
                        Assert.That(coordinator.TrySelectOpeningSkill(OpeningSkill("EnergyOrb")),Is.True);
                        game.TrySetMode(GameModeState.Menu);
                        typeof(DungeonCombatCoordinator).GetMethod("UpdateOpeningPreparation",flags).Invoke(coordinator,null);
                        Assert.That(host.Session,Is.Null); Assert.That(targeting.IsSelectingTarget,Is.False);
                        Assert.That(movement.enabled,Is.False); Assert.That(game.CurrentMode,Is.EqualTo(GameModeState.Menu));
                    }
                    return;
                }
                var actor = host.Session.ActiveUnit; var target = host.Session.Units.First(u=>!u.IsPlayerSide && u.Character==enemies[useSkillBar?1:0].RuntimeCharacter);
                Assert.That(host.TryExecuteSkill(OpeningSkill("EnergyOrb"),target.Position),Is.True);
                Assert.That(host.IsOpeningPreview,Is.False); Assert.That(game.Combats.Count,Is.EqualTo(1));
                var fight=game.Combats.Single(); Assert.That(fight.Units.Count,Is.EqualTo(6));
                Assert.That(fight.DungeonContext.EncounterInstanceId,Is.EqualTo(differentTargetEncounter?"other.encounter":"encounter"));
                Assert.That(fight.Units.Select(u=>u.Position).Distinct().Count(),Is.EqualTo(6));
                Assert.That(fight.Units.Count(u=>u.IsPlayerSide),Is.EqualTo(4)); Assert.That(fight.EnemySources.Count,Is.EqualTo(2));
                Assert.That(fight.Rewards.EnemyRewards.Sum(v=>v.Gold),Is.EqualTo(8));
                Assert.That(fight.ActiveUnit.Character,Is.SameAs(actor.Character)); Assert.That(fight.ActiveUnit.CurrentActionPoints,Is.LessThan(fight.ActiveUnit.MaxActionPoints));
                foreach (var enemy in fight.Units.Where(u=>!u.IsPlayerSide))
                {
                    var p=enemy.Character==enemies[0].RuntimeCharacter ? .25f : .75f;
                    Assert.That(enemy.CurrentActionValue,Is.EqualTo(CombatTurnSystem.CalculateBaseActionValue(enemy)*(2-p)).Within(.01f));
                }
            }
            finally
            {
                if(coordinator!=null) Field(coordinator,"initialized",false);
                Object.DestroyImmediate(root); Object.DestroyImmediate(encounterDefinition);
            }
        }
    }
}
