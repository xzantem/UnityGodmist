using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using System.Linq;

namespace Godmist.Tests.EditMode
{
    public class CombatLineOfSightServiceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RangeSightBatchMatchesIndividualQueriesAcrossDoorsOccupantsAndIgnoredObjects(bool ignoreTargetObject)
        {
            var session = new CombatSession { Grid = new BattleGrid(12, 12) };
            var random = new System.Random(417);
            for (var i = 0; i < 30; i++)
            {
                var position = new GridPosition(random.Next(12), random.Next(12));
                session.MapObjects.Add(new ObstacleCombatMapObject("blocker-" + i, position, true, i % 3 != 0,
                    occupiedPositions: new[] { position, new GridPosition(position.X, (position.Y + 1) % 12) }));
            }
            session.MapObjects.Add(null);
            var door = new DoorCombatMapObject("door", new GridPosition(5, 5), new GridPosition(6, 5), DoorInteractionMode.DirectAndLever);
            session.MapObjects.Add(door);
            var occupant = new BattleUnit { Character = new Godmist.Runtime.Characters.CharacterInstance(), Position = session.MapObjects[0].Position };
            session.Units.Add(occupant);
            session.EnvironmentBlocksLineOfSight = (a, b) => a.X == 3 && b.X == 4 && b.Y == 2;
            var service = new CombatLineOfSightService();
            var positions = session.Grid.Positions.Concat(new[] { new GridPosition(11, 11), new GridPosition(-1, 0) }).ToArray();
            CombatMapObject Ignored(GridPosition position) => ignoreTargetObject ? session.GetMapObjectAt(position) : null;
            for (var phase = 0; phase < 3; phase++)
            {
                foreach (var origin in new[] { new GridPosition(0, 0), new GridPosition(5, 5), new GridPosition(11, 11), new GridPosition(5, 4) })
                {
                    var expected = positions.Where(p => service.HasLineOfSight(session, origin, p, Ignored(p))).ToArray();
                    Assert.That(service.FilterVisiblePositions(session, origin, positions, Ignored), Is.EqualTo(expected),
                        $"Origin {origin.X},{origin.Y}, phase {phase}; order and duplicate destinations must be retained.");
                }
                door.IsOpen = !door.IsOpen;
                occupant.Character.LifeState = phase == 0 ? CharacterLifeState.Downed : CharacterLifeState.Dead;
                session.Grid.SetWalkable(new GridPosition(8, 8), phase != 0);
                session.MapObjects.RemoveAt(1);
            }
        }

        [Test]
        public void RangeSightBatchPreservesElevatedGeometryAndWallRules()
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 4; x++) for (var y = 0; y < 4; y++)
            {
                map.TryAddFloorCell(new UnityEngine.Vector2Int(x, y), "ground");
                map.TryAddFloorCell(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(new UnityEngine.Vector2Int(x, y), 1), "upper");
            }
            map.TryAddWall(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(new UnityEngine.Vector2Int(1, 1), 1),
                new Godmist.Runtime.Dungeons.DungeonSurfacePosition(new UnityEngine.Vector2Int(2, 1), 1));
            var context = new DungeonCombatContext("test", 0, "enc", "room", UnityEngine.Vector2Int.zero,
                UnityEngine.Vector2Int.one, map.FloorCells, null, null, surfaceMap: map);
            var session = new CombatSession { Grid = new BattleGrid(4, 4), DungeonContext = context };
            foreach (var surface in map.Surfaces) session.Grid.SetWalkable(new GridPosition(surface.Cell.x, surface.Cell.y, surface.Level));
            session.MapObjects.Add(new ObstacleCombatMapObject("upper obstacle", new GridPosition(2, 2, 1), true, true));
            session.MapObjects.Add(new DoorCombatMapObject("ground door", new GridPosition(1, 1), new GridPosition(2, 1), DoorInteractionMode.DirectAndLever));
            var service = new CombatLineOfSightService();
            var positions = session.Grid.Positions.ToArray();
            foreach (var origin in positions)
                Assert.That(service.FilterVisiblePositions(session, origin, positions),
                    Is.EqualTo(positions.Where(p => service.HasLineOfSight(session, origin, p)).ToArray()));
        }

        [Test]
        public void LargeRangeSightBatchMeasuresEquivalentVisibleCells()
        {
            var session = new CombatSession { Grid = new BattleGrid(32, 32) };
            for (var i = 0; i < 500; i++)
                session.MapObjects.Add(new ObstacleCombatMapObject("distant-" + i, new GridPosition(i, 50), true, true));
            var origin = new GridPosition(16, 16);
            var positions = session.Grid.Positions.ToArray();
            var service = new CombatLineOfSightService();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var expected = positions.Where(p => service.HasLineOfSight(session, origin, p)).ToArray();
            var original = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            var actual = service.FilterVisiblePositions(session, origin, positions);
            TestContext.WriteLine($"1024 range cells / 500 blockers: individual={original:F3}ms, indexed batch={timer.Elapsed.TotalMilliseconds:F3}ms");
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Count, Is.EqualTo(1024));
        }

        [Test]
        public void IndexedGroundSightStepsMatchExhaustiveWallsAndLiveGateState()
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 8; x++) for (var y = 0; y < 8; y++)
            {
                var cell = new UnityEngine.Vector2Int(x, y);
                map.TryAddFloorCell(cell, "room");
                map.TryAddWall(cell, cell + UnityEngine.Vector2Int.right);
                if (y % 2 == 0) map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(cell,
                    cell + UnityEngine.Vector2Int.right), DungeonWallType.Low);
                if (x % 2 == 0) map.TryAddWall(cell, cell + UnityEngine.Vector2Int.up);
            }
            var gate = new Godmist.Runtime.Dungeons.DungeonCellEdge(new UnityEngine.Vector2Int(2, 2), new UnityEngine.Vector2Int(3, 2));
            foreach (var open in new[] { false, true, false })
            {
                map.TrySetProgressionGateState(gate, open);
                for (var x = -1; x < 9; x++) for (var y = -1; y < 9; y++)
                for (var dx = -2; dx <= 2; dx++) for (var dy = -2; dy <= 2; dy++)
                {
                    var from = new UnityEngine.Vector2Int(x, y);
                    var to = from + new UnityEngine.Vector2Int(dx, dy);
                    var expected = false;
                    bool Crosses(Godmist.Runtime.Dungeons.DungeonCellEdge edge) => CombatLineOfSightService.CrossesEdge(
                        new GridPosition(from.x, from.y), new GridPosition(to.x, to.y),
                        new BattleCellEdge(new GridPosition(edge.FirstCell.x, edge.FirstCell.y),
                            new GridPosition(edge.SecondCell.x, edge.SecondCell.y)));
                    foreach (var wall in map.WallEdges)
                        if (map.GetWallType(wall) == DungeonWallType.Full && Crosses(wall)) { expected = true; break; }
                    foreach (var closed in map.ClosedProgressionGateEdges) if (Crosses(closed)) expected = true;
                    Assert.That(map.BlocksGroundSightStep(from, to), Is.EqualTo(expected), $"{from} -> {to}; gateOpen={open}");
                }
            }
        }

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
