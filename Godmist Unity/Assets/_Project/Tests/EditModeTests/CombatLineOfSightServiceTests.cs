using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CombatLineOfSightServiceTests
    {
        [TestCase(1, 1, 1)]
        [TestCase(2, 1, 2)]
        [TestCase(2, 2, 3)]
        [TestCase(3, 1, 3)]
        [TestCase(3, 2, 4)]
        [TestCase(4, 4, 6)]
        [TestCase(0, 0, 0)]
        public void RoundedRadialAndRingDistanceHasConsistentInnerAndOuterBounds(int x, int y, int distance)
        {
            foreach (var shape in new[] { RangeShape.Radial, RangeShape.Ring })
            foreach (var sign in new[] { -1, 1 })
            {
                var origin = new GridPosition(10, 10);
                var target = new GridPosition(10 + sign * x, 10 + sign * y);
                Assert.That(CombatTargetingService.IsPositionWithinRange(origin, target,
                    new Godmist.Data.Skills.SkillRangeData(shape, distance, distance)), Is.True);
                Assert.That(CombatTargetingService.IsPositionWithinRange(origin, target,
                    new Godmist.Data.Skills.SkillRangeData(shape, distance + 1, distance + 2)), Is.False);
                if (distance > 0)
                    Assert.That(CombatTargetingService.IsPositionWithinRange(origin, target,
                        new Godmist.Data.Skills.SkillRangeData(shape, 0, distance - 1)), Is.False);
            }
        }

        [Test]
        public void BoundedRelativeRangesMatchFullGridGeometry()
        {
            var session=new CombatSession{Grid=new BattleGrid(18,18)};
            var caster=new BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance(),Position=new GridPosition(3,3)};
            var service=new CombatTargetingService();
            foreach(var shape in new[]{RangeShape.Square,RangeShape.Radial,RangeShape.Ring,RangeShape.None})
            foreach(var center in new[]{new GridPosition(1,1),new GridPosition(9,9)})
            for(var size=1;size<=5;size++)for(var rotation=0;rotation<4;rotation++)
            {
                var range=new Godmist.Data.Skills.SkillRangeData(shape,1,size);
                var expected=new System.Collections.Generic.List<GridPosition>();
                foreach(var position in session.Grid.Positions)
                    if(CombatTargetingService.IsPositionWithinRelativeRange(caster.Position,center,position,range,rotation))expected.Add(position);
                Assert.That(service.GetRelativeRangePositions(session,caster,range,center,rotation),Is.EquivalentTo(expected),$"{shape}, {size}, {rotation}");
            }
        }

        [Test]
        public void MeleeRadialRangeIncludesAllEightNeighboursButNotOriginOrDistanceTwo()
        {
            var range = new Godmist.Data.Skills.SkillRangeData(RangeShape.Radial, 1, 1);
            var origin = new GridPosition(3, 3);
            for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++)
                Assert.That(CombatTargetingService.IsPositionWithinRange(origin, new GridPosition(3+x,3+y), range), Is.EqualTo(x != 0 || y != 0));
            Assert.That(CombatTargetingService.IsPositionWithinRange(origin,new GridPosition(5,3),range),Is.False);
            Assert.That(CombatTargetingService.IsPositionWithinRange(origin,new GridPosition(5,5),new Godmist.Data.Skills.SkillRangeData(RangeShape.Radial,1,2)),Is.False);
        }

        [Test]
        public void IndexedColumnsPreserveMultipleLevelsAndWalkabilityChanges()
        {
            var grid = new BattleGrid(200,200,true);
            grid.TraversalNeighbours = _ => System.Array.Empty<GridPosition>();
            for(var x=0;x<200;x++)for(var y=0;y<200;y++)grid.SetWalkable(new GridPosition(x,y));
            var upper = new GridPosition(4,4,1);grid.SetWalkable(upper);grid.SetWalkable(upper);
            Assert.That(grid.GetPositionsAt(4,4),Is.EquivalentTo(new[]{new GridPosition(4,4),upper}));
            grid.SetWalkable(upper,false);Assert.That(grid.GetPositionsAt(4,4),Is.EquivalentTo(new[]{new GridPosition(4,4)}));
            grid.SetWalkable(new GridPosition(4,4),false);Assert.That(grid.GetPositionsAt(4,4),Is.Empty);
            var watch=System.Diagnostics.Stopwatch.StartNew();var count=0;
            for(var i=0;i<100;i++)for(var x=90;x<=106;x++)for(var y=90;y<=106;y++)foreach(var cell in grid.GetPositionsAt(x,y))count++;
            watch.Stop();TestContext.WriteLine($"100 range-8 column queries on 40,000 cells: {watch.Elapsed.TotalMilliseconds:F2} ms; {count} results");
            Assert.That(count,Is.EqualTo(28900));
        }

        [Test]
        public void IndexedWallsPreserveDiagonalCornersAndLowWallVisibility()
        {
            var first=new UnityEngine.Vector2Int(10,20);var second=new UnityEngine.Vector2Int(11,20);
            var edge=new Godmist.Runtime.Dungeons.DungeonCellEdge(first,second);
            foreach(var type in new[]{DungeonWallType.Full,DungeonWallType.Low})
            {
                var context=new DungeonCombatContext("test",0,"enc","room",first,second,
                    new[]{first,second,new UnityEngine.Vector2Int(10,21),new UnityEngine.Vector2Int(11,21)},new[]{edge},null,
                    nextWallTypes:new System.Collections.Generic.Dictionary<Godmist.Runtime.Dungeons.DungeonCellEdge,DungeonWallType>{{edge,type}});
                var session=new CombatSession{Grid=new BattleGrid(2,2),DungeonContext=context};
                Assert.That(new CombatLineOfSightService().HasLineOfSight(session,new GridPosition(0,0),new GridPosition(1,1)),Is.EqualTo(type==DungeonWallType.Low));
                Assert.That(new CombatLineOfSightService().HasLineOfSight(session,new GridPosition(1,1),new GridPosition(0,0)),Is.EqualTo(type==DungeonWallType.Low));
            }
        }

        [Test]
        public void HasLineOfSight_WithoutBlocker_ReturnsTrue()
        {
            var session = CreateSession();
            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.True);
        }

        [Test]
        public void HasLineOfSight_HighObstacleBetweenUnits_ReturnsFalse()
        {
            var session = CreateSession();

            session.MapObjects.Add(
                new ObstacleCombatMapObject(
                    "obstacle.wall.01",
                    new GridPosition(3, 1),
                    blocksMovement: true,
                    blocksLineOfSight: true));

            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.False);
        }

        [Test]
        public void HasLineOfSight_LowObstacleBetweenUnits_ReturnsTrue()
        {
            var session = CreateSession();

            session.MapObjects.Add(
                new ObstacleCombatMapObject(
                    "obstacle.rock.01",
                    new GridPosition(3, 1),
                    blocksMovement: true,
                    blocksLineOfSight: false));

            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.True);
        }

        [Test]
        public void HasLineOfSight_ClosedDoorBlocksButOpenDoorDoesNot()
        {
            var session = CreateSession();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(3, 1),
                new GridPosition(4, 1),
                DoorInteractionMode.DirectAndLever);

            session.MapObjects.Add(door);

            var service = new CombatLineOfSightService();

            var closedDoorResult = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            door.IsOpen = true;

            var openDoorResult = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(closedDoorResult, Is.False);
            Assert.That(openDoorResult, Is.True);
        }

        private static CombatSession CreateSession()
        {
            return new CombatSession
            {
                Grid = new BattleGrid(8, 8)
            };
        }
    }
}