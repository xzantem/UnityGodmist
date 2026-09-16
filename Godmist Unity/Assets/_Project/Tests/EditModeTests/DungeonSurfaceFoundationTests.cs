using System.Linq;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonVisibilityModelTests
    {
        private readonly Godmist.Systems.Dungeons.DungeonVisibilitySystem system = new();
        private static DungeonTraversalPosition At(int x, int y = 0, int level = 0)
            => new(new DungeonSurfacePosition(new Vector2Int(x, y), level));
        private static DungeonFloorInstance Floor()
        {
            var map = new DungeonFloorMap();
            for (var x = -11; x <= 11; x++) for (var y = -2; y <= 2; y++)
                map.TryAddFloorCell(new Vector2Int(x, y), x < 0 ? "left" : "right");
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map); return floor;
        }

        [Test]
        public void UnlimitedPartyVisibilitySeesBeyondEnemyRangeButRespectsOcclusionAndUnion()
        {
            var floor = Floor();
            var visible = new System.Collections.Generic.HashSet<DungeonTraversalPosition>();
            system.CollectPartyVisible(floor, new[] { At(-11) }, visible);
            Assert.That(visible, Does.Contain(At(11)));
            Assert.That(system.CanDetect(floor, At(-11), Vector2.right, 10, 120, At(11)), Is.False);
            floor.Map.SetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right)),
                Godmist.Core.Enums.DungeonWallType.Full);
            system.CollectPartyVisible(floor, new[] { At(-11) }, visible);
            Assert.That(visible, Has.No.Member(At(11)));
            system.CollectPartyVisible(floor, new[] { At(-11), At(10) }, visible);
            Assert.That(visible, Does.Contain(At(11)));
            system.CollectPartyVisible(floor, System.Array.Empty<DungeonTraversalPosition>(), visible);
            Assert.That(visible, Is.Empty);
        }

        [Test]
        public void SightCannotLeakThroughJoinedWallCornerDueToRounding()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = -12; x <= -1; x++) for (var y = -12; y <= -1; y++)
                map.TryAddFloorCell(new Vector2Int(x, y), "corner");
            floor.TrySetMap(map);
            // Ray passes exactly through (-5,-5), where these two walls meet.
            map.TryAddWall(new Vector2Int(-6, -5), new Vector2Int(-5, -5));
            map.TryAddWall(new Vector2Int(-5, -6), new Vector2Int(-5, -5));
            Assert.That(system.HasLineOfSight(floor, At(-12, -12), At(-1, -1)), Is.False);
            Assert.That(system.HasLineOfSight(floor, At(-1, -1), At(-12, -12)), Is.False);
        }

        [TestCase(8, true)]
        [TestCase(10, true)]
        [TestCase(11, false)]
        public void EnemyRangeUsesXYDistanceAndIncludesBoundary(int distance, bool expected)
            => Assert.That(system.CanDetect(Floor(), At(0), Vector2.right, 10, 120, At(distance)), Is.EqualTo(expected));

        [Test]
        public void CombatObservationSeesBehindObserverButRespectsRangeWallsAndDoors()
        {
            var floor = Floor();
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(-2)), Is.False);
            Assert.That(system.CanObserveCombat(floor, At(0), 10, At(-2)), Is.True);
            Assert.That(system.CanObserveCombat(floor, At(0), 10, At(-11)), Is.False);
            var edge = new DungeonCellEdge(new Vector2Int(-1, 0), Vector2Int.zero);
            floor.Map.SetWallType(new DungeonSurfaceEdge(edge), Godmist.Core.Enums.DungeonWallType.Full);
            Assert.That(system.CanObserveCombat(floor, At(0), 10, At(-2)), Is.False);
            floor.Map.SetWallType(new DungeonSurfaceEdge(edge), Godmist.Core.Enums.DungeonWallType.Railing);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
            try
            {
                var door = new DoorDungeonObjectInstance("door", definition, edge);
                floor.TryAddObject(door);
                Assert.That(system.CanObserveCombat(floor, At(0), 10, At(-2)), Is.False);
                door.TrySetOpen(true);
                Assert.That(system.CanObserveCombat(floor, At(0), 10, At(-2)), Is.True);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ConeDependsOnFacingAndSeesAcrossRoomBoundary()
        {
            var floor = Floor();
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(1, 1)), Is.True);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(1, 2)), Is.False);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(-1)), Is.False);
            Assert.That(system.CanDetect(floor, At(0), Vector2.left, 10, 120, At(-1)), Is.True);
            Assert.That(system.CanDetect(floor, At(0), Vector2.up, 10, 120, At(0, 2)), Is.True);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 90, At(1, 1)), Is.True);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 8, 120, At(9)), Is.False);
        }

        [Test]
        public void WallsBlockSightButRailingsAndLowWallsDoNot()
        {
            var floor = Floor(); var edge = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
            floor.Map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.Full);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.False);
            foreach (var type in new[] { Godmist.Core.Enums.DungeonWallType.Low, Godmist.Core.Enums.DungeonWallType.Railing })
            {
                floor.Map.SetWallType(edge, type);
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.True);
            }
        }

        [Test]
        public void OpeningDoorChangesVisibilityWithoutMovingOrRebuildingMap()
        {
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
            try
            {
                var floor = Floor();
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
                floor.TryAddObject(door);
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.False);
                door.TrySetOpen(true);
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.True);
                door.TrySetOpen(false);
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.False);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CeilingBlocksSameXYButOpenEdgeAllowsCrossHeightSight()
        {
            var floor = Floor(); floor.Map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "upper");
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 360, At(0, 0, 1)), Is.False);
            Assert.That(system.CanDetect(floor, At(2), Vector2.left, 10, 120, At(0, 0, 1)), Is.True);
        }

        [Test]
        public void OccludingObstacleRemainsObservableButHidesCellsBehindIt()
        {
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
            try
            {
                typeof(Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition).GetProperty("BlocksLineOfSight").SetValue(definition, true);
                var floor = Floor(); floor.TryAddObject(new ObstacleDungeonObjectInstance("pillar", definition, Vector2Int.right));
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(1)), Is.True);
                Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(2)), Is.False);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void PartyVisibilityIncludesIntermediateStairPositions()
        {
            var floor = Floor(); var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            floor.Map.TryAddFloorCell(upper, "upper");
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            floor.Map.TryAddStairs(first, upper, new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            var step = new DungeonTraversalPosition(new DungeonStairConnection(first, upper), 1);
            var visible = new System.Collections.Generic.HashSet<DungeonTraversalPosition>();
            system.CollectPartyVisible(floor, new[] { At(0) }, 10, visible);
            Assert.That(visible.Contains(step), Is.True);
        }

        [Test]
        public void PartyVisibilityIsOmnidirectionalUnionAndClearsPreviousFrame()
        {
            var floor = Floor(); var visible = new System.Collections.Generic.HashSet<DungeonTraversalPosition>();
            system.CollectPartyVisible(floor, new[] { At(-5), At(5) }, 2, visible);
            Assert.That(visible.Contains(At(-7)), Is.True);
            Assert.That(visible.Contains(At(7)), Is.True);
            Assert.That(visible.Contains(At(0)), Is.False);
            system.CollectPartyVisible(floor, new[] { At(5) }, 2, visible);
            Assert.That(visible.Contains(At(-7)), Is.False, "Current visibility is not discovery memory.");
            Assert.That(visible.Contains(At(5, -2)), Is.True);
        }

        [Test]
        public void CameraLayerDoesNotAffectLogicalSight()
        {
            var floor = Floor(); floor.Map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "upper");
            var view = new DungeonElevationViewState(); view.Bind(floor.Map);
            var before = system.CanDetect(floor, At(-2), Vector2.right, 10, 120, At(0));
            view.Step(-1); view.Focus(At(0), true); view.Step(1);
            Assert.That(system.CanDetect(floor, At(-2), Vector2.right, 10, 120, At(0)), Is.EqualTo(before).And.True);
        }

        [Test]
        public void InvalidConfigurationCannotRevealTargets()
        {
            var floor = Floor();
            Assert.That(system.CanDetect(floor, At(0), Vector2.zero, 10, 120, At(1)), Is.False);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, float.NaN, 120, At(1)), Is.False);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 361, At(1)), Is.False);
            Assert.That(system.CanDetect(floor, At(0), Vector2.right, 10, 120, At(99)), Is.False);
        }

        [Test]
        public void FacingRetainsVerticalMovementEvenWithoutDirectionalSprite()
        {
            var root = new GameObject("Vision direction test");
            try
            {
                var view = root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var data = new Godmist.Data.Combat.EncounterEnemyData(); view.ConfigureEnemyVision(data);
                Assert.That(view.VisionRange, Is.EqualTo(10)); Assert.That(view.VisionAngle, Is.EqualTo(120));
                view.Move(Vector2.up);
                Assert.That(view.FacingDirection, Is.EqualTo(Vector2.up));
                view.Move(Vector2.zero);
                Assert.That(view.FacingDirection, Is.EqualTo(Vector2.up));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonSurfaceFoundationTests
    {
        [Test]
        public void StackedSurfacesHaveIndependentIdentityAndRoomOwnership()
        {
            var map = new DungeonFloorMap();
            var lower = new DungeonSurfacePosition(Vector2Int.zero);
            var upper = new DungeonSurfacePosition(Vector2Int.zero, 1);
            Assert.That(map.TryAddFloorCell(lower, "below"), Is.True);
            Assert.That(map.TryAddFloorCell(upper, "above"), Is.True);
            Assert.That(map.TryAddFloorCell(upper, "duplicate"), Is.False);
            Assert.That(map.Surfaces.Count(), Is.EqualTo(2));
            Assert.That(map.FloorCells.Count, Is.EqualTo(1));
            map.TryGetRoomId(upper, out var room);
            Assert.That(room, Is.EqualTo("above"));
            Assert.That(map.GetStairDestinations(lower), Is.Empty);
        }

        [Test]
        public void StairsRequireExistingDistinctLevelsAndAreBidirectional()
        {
            var map = new DungeonFloorMap();
            var lower = new DungeonSurfacePosition(Vector2Int.zero);
            var upper = new DungeonSurfacePosition(Vector2Int.right, 1);
            map.TryAddFloorCell(lower, "r");
            Assert.That(map.TryAddStairs(lower, upper), Is.False);
            map.TryAddFloorCell(upper, "r");
            Assert.That(map.TryAddStairs(lower, lower), Is.False);
            Assert.That(map.TryAddStairs(lower, upper), Is.True);
            Assert.That(map.TryAddStairs(upper, lower), Is.False);
            Assert.That(map.GetStairDestinations(lower), Is.EquivalentTo(new[] { upper }));
            Assert.That(map.GetStairDestinations(upper), Is.EquivalentTo(new[] { lower }));
        }

        [Test]
        public void LegacyMovementCannotTreatUpperSurfaceAsGround()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, 1), "r");
            Assert.That(map.IsWalkable(Vector2Int.right), Is.False);
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.right), Is.False);
            Assert.That(map.TryAddFloorCell(Vector2Int.right, "r"), Is.True);
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.right), Is.True);
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonLayeredGeometryTests
    {
        private readonly System.Collections.Generic.List<ScriptableObject> assets = new();
        private T Asset<T>(string id = null) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset);
            if (id != null) Set(asset, "Id", id);
            return asset;
        }
        private static void Set(object target, string field, object value)
            => target.GetType().GetField("<" + field + ">k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        private Godmist.Data.Dungeons.DungeonRoomTemplateDefinition Template(string id, params int[] levels)
        {
            var t = Asset<Godmist.Data.Dungeons.DungeonRoomTemplateDefinition>(id);
            Set(t, "SizeInCells", new Vector2Int(2, 2));
            Set(t, "BoundaryEdgesConfigured", false);
            foreach (var level in levels)
                for (var x = 0; x < 2; x++) for (var y = 0; y < 2; y++)
                    t.Surfaces.Add(new Godmist.Data.Dungeons.DungeonRoomSurfaceData(new Vector2Int(x, y), level));
            return t;
        }
        [TearDown] public void TearDown() { foreach (var a in assets) Object.DestroyImmediate(a); assets.Clear(); }

        [Test]
        public void PassageRejectsDifferentLevelsWithoutReplacingValidGeometry()
        {
            var c = new DungeonRoomConnection("a", "b");
            Assert.That(c.TrySetDirectPassage(new DungeonSurfacePosition(Vector2Int.zero, 1), new DungeonSurfacePosition(Vector2Int.right, 1), 1), Is.True);
            Assert.That(c.TrySetDirectPassage(new DungeonSurfacePosition(Vector2Int.zero), new DungeonSurfacePosition(Vector2Int.right, 1), 1), Is.False);
            Assert.That(c.DoorwayLevel, Is.EqualTo(1));
            Assert.That(c.TrySetDirectPassage(Vector2Int.zero, Vector2Int.one, 1), Is.False);
        }

        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void MatchingRespectsRotationLevelAndEveryDoorwayCell(int rotation)
        {
            var first = Template("a", 1);
            var second = Template("b", 0);
            Assert.That(Match(first, second, rotation, 2, out _), Is.False);
            second.Surfaces.Clear();
            second.Surfaces.AddRange(first.Surfaces);
            Assert.That(Match(first, second, rotation, 2, out var level), Is.True);
            Assert.That(level, Is.EqualTo(1));
            var local = Godmist.Data.Dungeons.DungeonRoomTransformUtility.InverseTransformCell(new Vector2Int(1, 1), first.SizeInCells, rotation);
            first.Surfaces.RemoveAll(s => s.LocalCell == local);
            first.Surfaces.Add(new Godmist.Data.Dungeons.DungeonRoomSurfaceData(local, 0));
            Assert.That(Match(first, second, rotation, 2, out _), Is.False);
        }
        private static bool Match(Godmist.Data.Dungeons.DungeonRoomTemplateDefinition a, Godmist.Data.Dungeons.DungeonRoomTemplateDefinition b, int rotation, int width, out int level)
            => Godmist.Systems.Dungeons.DungeonPassageGeometry.TryFindCommonLevel(a, b, rotation, 0,
                Vector2Int.right, Vector2Int.zero, width, Vector2Int.up,
                Godmist.Core.Enums.DungeonEdgeDirection.Right, Godmist.Core.Enums.DungeonEdgeDirection.Left, out level);

        [Test]
        public void UpperDoorwayLeavesGroundWallAndGateIndependent()
        {
            var a = Template("a", 0, 1); var b = Template("b", 0, 1);
            var catalog = Asset<Godmist.Data.Dungeons.DungeonRoomTemplateCatalog>(); catalog.Templates.AddRange(new[] { a, b });
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("a", Godmist.Core.Enums.DungeonRoomType.Normal, "a", Vector2Int.zero));
            floor.TryAddRoom(new DungeonRoomInstance("b", Godmist.Core.Enums.DungeonRoomType.Normal, "b", new Vector2Int(2, 0)));
            floor.TryAddConnection("a", "b");
            var c = floor.Connections[0];
            c.TrySetDirectPassage(new DungeonSurfacePosition(Vector2Int.right, 1), new DungeonSurfacePosition(new Vector2Int(2, 0), 1), 2);
            var map = new Godmist.Systems.Dungeons.DungeonFloorMapBuilder().Build(floor, catalog);
            var edge = new DungeonCellEdge(Vector2Int.right, new Vector2Int(2, 0));
            Assert.That(map.SurfaceWallEdges.Contains(new DungeonSurfaceEdge(edge, 1)), Is.False);
            Assert.That(map.WallEdges.Contains(edge), Is.True);
            var upper = new DungeonSurfaceEdge(edge, 1);
            Assert.That(map.SetWallType(upper, Godmist.Core.Enums.DungeonWallType.Railing), Is.True);
            Assert.That(map.GetWallType(edge), Is.EqualTo(Godmist.Core.Enums.DungeonWallType.Full));
            Assert.That(map.GetWallType(upper), Is.EqualTo(Godmist.Core.Enums.DungeonWallType.Railing));
            Assert.That(map.TrySetProgressionGateState(upper, false), Is.True);
            Assert.That(map.ClosedProgressionGateEdges, Is.Empty);
            b.Surfaces.RemoveAll(s => s.Level == 1);
            Assert.Throws<System.InvalidOperationException>(() => new Godmist.Systems.Dungeons.DungeonFloorMapBuilder().Build(floor, catalog));
        }

        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void BuilderRotatesStairsAndStructuralWallsWithoutChangingLevels(int rotation)
        {
            var t = Template("a", 0, 1);
            var first = new Godmist.Data.Dungeons.DungeonRoomSurfaceData(Vector2Int.zero);
            var second = new Godmist.Data.Dungeons.DungeonRoomSurfaceData(Vector2Int.one, 1);
            t.Stairs.Add(new Godmist.Data.Dungeons.DungeonRoomStairData(first, second, new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.one }));
            t.StructuralAnchors.Add(new Godmist.Data.Dungeons.DungeonStructuralAnchorData(Godmist.Core.Enums.DungeonStructuralAnchorType.Wall,
                Vector2Int.zero, Godmist.Core.Enums.DungeonEdgeDirection.Right, Godmist.Core.Enums.DungeonWallType.Low, 1));
            var catalog = Asset<Godmist.Data.Dungeons.DungeonRoomTemplateCatalog>(); catalog.Templates.Add(t);
            var floor = new DungeonFloorInstance(0); var position = new Vector2Int(5, 7);
            floor.TryAddRoom(new DungeonRoomInstance("a", Godmist.Core.Enums.DungeonRoomType.Normal, "a", position, rotationDegrees: rotation));
            var map = new Godmist.Systems.Dungeons.DungeonFloorMapBuilder().Build(floor, catalog);
            Vector2Int Transform(Vector2Int p) => position + Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformCell(p, t.SizeInCells, rotation);
            var lower = new DungeonSurfacePosition(Transform(Vector2Int.zero));
            var upper = new DungeonSurfacePosition(Transform(Vector2Int.one), 1);
            Assert.That(map.GetStairDestinations(lower), Is.EquivalentTo(new[] { upper }));
            Assert.That(map.TryGetStairPath(new DungeonStairConnection(lower, upper), out var route), Is.True);
            Assert.That(route, Is.EqualTo(new[] { Transform(Vector2Int.zero), Transform(Vector2Int.up), Transform(Vector2Int.one) }));
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(lower), new DungeonTraversalPosition(upper), out var traversal), Is.True);
            Assert.That(traversal.Count - 1, Is.EqualTo(2));
            var edge = new DungeonCellEdge(Transform(Vector2Int.zero), Transform(Vector2Int.right));
            Assert.That(map.GetWallType(new DungeonSurfaceEdge(edge, 1)), Is.EqualTo(Godmist.Core.Enums.DungeonWallType.Low));
            Assert.That(map.WallEdges.Contains(edge), Is.False);
        }

        [Test]
        public void AuthoredExitAndBlockingWallAreMatchedOnTheirOwnLevelAfterRotation()
        {
            var t = Template("a", 0, 1);
            Set(t, "BoundaryEdgesConfigured", true); t.AllowedBoundaryEdges.Clear();
            t.AllowedBoundaryEdges.Add(new Godmist.Data.Dungeons.DungeonRoomBoundaryEdgeData(Vector2Int.zero, Godmist.Core.Enums.DungeonEdgeDirection.Left, 1));
            var cell = Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformCell(Vector2Int.zero, t.SizeInCells, 90);
            var direction = Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformDirection(Godmist.Core.Enums.DungeonEdgeDirection.Left, 90);
            bool Allows(int level) => Godmist.Data.Dungeons.DungeonRoomTransformUtility.AllowsTransformedBoundaryEdge(t, cell, direction, 90, level);
            Assert.That(Allows(0), Is.False);
            Assert.That(Allows(1), Is.True);
            t.StructuralAnchors.Add(new Godmist.Data.Dungeons.DungeonStructuralAnchorData(Godmist.Core.Enums.DungeonStructuralAnchorType.Wall,
                Vector2Int.zero, Godmist.Core.Enums.DungeonEdgeDirection.Left));
            Assert.That(Allows(1), Is.True);
            t.StructuralAnchors.Add(new Godmist.Data.Dungeons.DungeonStructuralAnchorData(Godmist.Core.Enums.DungeonStructuralAnchorType.Wall,
                Vector2Int.zero, Godmist.Core.Enums.DungeonEdgeDirection.Left, level: 1));
            Assert.That(Allows(1), Is.False);
        }

        [Test]
        public void GroundProjectionsUpdateAfterMutationsAndNeverIncludeUpperEdges()
        {
            var map = new DungeonFloorMap();
            foreach (var level in new[] { 0, 1 })
            {
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, level), "a");
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, level), "a");
            }
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            Assert.That(map.WallEdges, Is.Empty); Assert.That(map.WallTypes, Is.Empty);
            map.SetWallType(new DungeonSurfaceEdge(edge, 1), Godmist.Core.Enums.DungeonWallType.Railing);
            Assert.That(map.WallEdges, Is.Empty); Assert.That(map.WallTypes, Is.Empty);
            map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.Low);
            Assert.That(map.WallEdges, Does.Contain(edge));
            Assert.That(map.WallTypes[edge], Is.EqualTo(Godmist.Core.Enums.DungeonWallType.Low));
            Assert.That(map.ClosedProgressionGateEdges, Is.Empty);
            map.TrySetProgressionGateState(edge, false);
            Assert.That(map.ClosedProgressionGateEdges, Does.Contain(edge));
            map.TrySetProgressionGateState(edge, true);
            Assert.That(map.ClosedProgressionGateEdges, Is.Empty);
        }

        [Test]
        public void BuilderOpensUpperLandingEdgeAcrossMissingUpperFloor()
        {
            var t = Template("a", 0, 1);
            t.Surfaces.RemoveAll(surface => surface.Level == 1 && surface.LocalCell != Vector2Int.one);
            t.Stairs.Add(new Godmist.Data.Dungeons.DungeonRoomStairData(
                new Godmist.Data.Dungeons.DungeonRoomSurfaceData(Vector2Int.zero),
                new Godmist.Data.Dungeons.DungeonRoomSurfaceData(Vector2Int.one, 1),
                new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.one }));
            var catalog = Asset<Godmist.Data.Dungeons.DungeonRoomTemplateCatalog>(); catalog.Templates.Add(t);
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("a", Godmist.Core.Enums.DungeonRoomType.Normal, "a", Vector2Int.zero));
            var map = new Godmist.Systems.Dungeons.DungeonFloorMapBuilder().Build(floor, catalog);
            var entry = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.up, Vector2Int.one), 1);
            Assert.That(map.SurfaceWallEdges.Contains(entry), Is.False);
            Assert.That(map.SurfaceWallEdges.Contains(new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.one, new Vector2Int(2, 1)), 1)), Is.True);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)),
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.one, 1)), out var route), Is.True);
            Assert.That(route.Count - 1, Is.EqualTo(2));
        }

        [Test]
        public void TemplateRejectsDuplicateSurfacesMissingStairEndsAndMissingExitLevel()
        {
            var t = Template("a", 1);
            t.Surfaces.Add(t.Surfaces[0]);
            Assert.Throws<System.InvalidOperationException>(() => t.ValidateSurfaceGeometry());
            t.Surfaces.RemoveAt(t.Surfaces.Count - 1);
            t.Stairs.Add(new Godmist.Data.Dungeons.DungeonRoomStairData(t.Surfaces[0], new Godmist.Data.Dungeons.DungeonRoomSurfaceData(Vector2Int.zero, 2)));
            Assert.Throws<System.InvalidOperationException>(() => t.ValidateSurfaceGeometry());
            t.Stairs.Clear(); Set(t, "BoundaryEdgesConfigured", true); t.AllowedBoundaryEdges.Clear();
            t.AllowedBoundaryEdges.Add(new Godmist.Data.Dungeons.DungeonRoomBoundaryEdgeData(Vector2Int.zero, Godmist.Core.Enums.DungeonEdgeDirection.Left, 0));
            Assert.Throws<System.InvalidOperationException>(() => t.ValidateSurfaceGeometry());
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonStairTraversalTests
    {
        private static readonly Vector2Int[] Path = { new(0, 0), new(1, 0), new(1, 1), new(2, 1), new(2, 2) };
        private static DungeonSurfacePosition First => new(Path[0]);
        private static DungeonSurfacePosition Second => new(Path[Path.Length - 1], 1);
        private static DungeonFloorMap MakeMap()
        {
            var map = new DungeonFloorMap(); map.TryAddFloorCell(First, "r"); map.TryAddFloorCell(Second, "r");
            Assert.That(map.TryAddStairs(First, Second, Path), Is.True); return map;
        }

        [Test]
        public void RouteIncludesEveryStepAndCanEndAtInteriorCell()
        {
            var map = MakeMap(); var stair = new DungeonStairConnection(First, Second);
            var start = new DungeonTraversalPosition(First); var end = new DungeonTraversalPosition(Second);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map, start, end, out var path), Is.True);
            Assert.That(path.Count - 1, Is.EqualTo(4));
            Assert.That(path[2], Is.EqualTo(new DungeonTraversalPosition(stair, 2)));
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map, start, path[2], out var partial), Is.True);
            Assert.That(partial.Count - 1, Is.EqualTo(2));
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map, path[2], end, out var rest), Is.True);
            Assert.That(rest.Count - 1, Is.EqualTo(2));
            Assert.That(map.GetTraversalNeighbours(start).Contains(end), Is.False);
        }

        [Test]
        public void InteriorCannotJumpOntoFloorBeneathOrBypassOccupiedStep()
        {
            var map = MakeMap(); var stair = new DungeonStairConnection(First, Second);
            var step = new DungeonTraversalPosition(stair, 2);
            var below = new DungeonSurfacePosition(Path[2]); map.TryAddFloorCell(below, "r");
            Assert.That(map.TryGetTraversalCell(step, out var cell), Is.True); Assert.That(cell, Is.EqualTo(below.Cell));
            Assert.That(step, Is.Not.EqualTo(new DungeonTraversalPosition(below)));
            Assert.That(map.GetTraversalNeighbours(step).Contains(new DungeonTraversalPosition(below)), Is.False);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(First), new DungeonTraversalPosition(Second), out _, p => p != step), Is.False);
        }

        [Test]
        public void ReverseAuthoredRouteHasStableIdentityAndDefensiveCopy()
        {
            var map = new DungeonFloorMap(); map.TryAddFloorCell(First, "r"); map.TryAddFloorCell(Second, "r");
            var authored = Path.Reverse().ToArray();
            Assert.That(map.TryAddStairs(Second, First, authored), Is.True);
            authored[1] = new Vector2Int(99, 99);
            Assert.That(map.TryGetStairPath(new DungeonStairConnection(First, Second), out var path), Is.True);
            Assert.That(path, Is.EqualTo(Path));
            Assert.That(map.TryAddStairs(First, Second, Path), Is.False);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(Second), new DungeonTraversalPosition(First), out var reverse), Is.True);
            Assert.That(reverse.Count, Is.EqualTo(Path.Length));
        }

        [Test]
        public void LegacyLongEndpointsRemainStoredButCannotTeleport()
        {
            var map = new DungeonFloorMap(); map.TryAddFloorCell(First, "r"); map.TryAddFloorCell(Second, "r");
            Assert.That(map.TryAddStairs(First, Second), Is.True);
            Assert.That(map.Stairs.Count(), Is.EqualTo(1));
            Assert.That(map.TryGetStairPath(new DungeonStairConnection(First, Second), out _), Is.False);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(First), new DungeonTraversalPosition(Second), out _), Is.False);
        }

        [Test]
        public void InvalidExplicitRouteIsRejectedAtomically()
        {
            var map = new DungeonFloorMap(); map.TryAddFloorCell(First, "r"); map.TryAddFloorCell(Second, "r");
            Assert.That(map.TryAddStairs(First, Second, new[] { First.Cell, Second.Cell }), Is.False);
            Assert.That(map.Stairs, Is.Empty);
            Assert.That(map.TryAddStairs(First, Second, Path), Is.True);
            Assert.That(map.TryGetTraversalCell(new DungeonTraversalPosition(new DungeonStairConnection(First, Second), 99), out _), Is.False);
        }

        [Test]
        public void ExplicitWallAtStairEntranceBlocksBothDirections()
        {
            var map = MakeMap();
            map.SetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(Path[0], Path[1]), First.Level), Godmist.Core.Enums.DungeonWallType.Railing);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(First), new DungeonTraversalPosition(Second), out _), Is.False);
            Assert.That(Godmist.Systems.Dungeons.DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(Second), new DungeonTraversalPosition(First), out _), Is.False);
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonElevationViewTests
    {
        [Test]
        public void StandingUnderStairsRevealsGroundAndEnteringStairsRestoresTreads()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "r");
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            map.TryAddFloorCell(second, "r");
            map.TryAddStairs(first, second, new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            var state = new DungeonElevationViewState(); state.Bind(map);
            var ground = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
            state.Focus(ground);
            Assert.That(state.IsVisible(ground), Is.True);
            var step = new DungeonTraversalPosition(new DungeonStairConnection(first, second), 1);
            state.Focus(step);
            Assert.That(state.IsVisible(step), Is.True);
            Assert.That(state.IsVisible(ground), Is.False);
        }

        [Test]
        public void AutomaticProjectionStartsAtWholeMapCeilingAndCutsOnlyAboveFocus()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 6; x++)
            {
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x, 0)), "r");
                if (x > 0) map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x, 0), 1), "r");
            }
            map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(10, 0), 3), "remote");
            var state = new DungeonElevationViewState(); state.Bind(map);
            Assert.That(state.ProjectionLevel, Is.EqualTo(3), "Full height is visible immediately after Bind, before the first focus tick.");
            Assert.That(state.IsVisible(new DungeonSurfacePosition(new Vector2Int(10, 0), 3)), Is.True);
            state.Focus(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)));
            Assert.That(state.ProjectionLevel, Is.EqualTo(3));
            Assert.That(state.IsVisible(new DungeonSurfacePosition(Vector2Int.right, 1)), Is.True);
            Assert.That(state.IsSelectable(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right, 1))), Is.False);
            state.Focus(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right)));
            Assert.That(state.IsVisible(new DungeonSurfacePosition(Vector2Int.right)), Is.True);
            Assert.That(state.IsVisible(new DungeonSurfacePosition(new Vector2Int(5, 0), 1)), Is.True);
            Assert.That(state.CutawaySurfaces.Count(), Is.EqualTo(2));
            Assert.That(state.Step(-1), Is.True);
            Assert.That(state.ProjectionLevel, Is.EqualTo(1));
            Assert.That(state.Step(-1), Is.True);
            Assert.That(state.ProjectionLevel, Is.Zero);
            Assert.That(state.CutawaySurfaces, Is.Empty);
            Assert.That(state.Follow(0, true), Is.True, "Returning to automatic mode must refresh even on the same level.");
            Assert.That(state.ProjectionLevel, Is.EqualTo(3));
            Assert.That(state.IsVisible(new DungeonSurfacePosition(Vector2Int.right)), Is.True);
        }

        [Test]
        public void ManualLayerPersistsUntilMovementOrExplicitFocusResumesFollowing()
        {
            var state = new DungeonElevationViewState(); var map = Stacked(); state.Bind(map);
            var focus = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
            state.Focus(focus); state.Step(1); state.Focus(focus);
            Assert.That(state.IsManual, Is.True);
            Assert.That(state.Level, Is.EqualTo(2));
            state.Focus(focus, true);
            Assert.That(state.IsManual, Is.False);
            Assert.That(state.Level, Is.Zero);
            Assert.That(state.ProjectionLevel, Is.EqualTo(2));
        }

        private static DungeonFloorMap Stacked()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero), "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 2), "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right), "r");
            return map;
        }

        [Test]
        public void ManualSelectionSkipsMissingLevelsAndDoesNotWrap()
        {
            var state = new DungeonElevationViewState(); state.Bind(Stacked());
            Assert.That(state.Step(1), Is.True);
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(state.Step(1), Is.False);
            Assert.That(state.Step(-1), Is.True);
            Assert.That(state.Level, Is.Zero);
            Assert.That(state.Step(-1), Is.False);
        }

        [Test]
        public void ManualPlanningSurvivesIdleFollowAndSameMapRebind()
        {
            var map = Stacked(); var state = new DungeonElevationViewState(); state.Bind(map);
            state.Step(1); state.Follow(0); state.Bind(map);
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(state.IsManual, Is.True);
            state.Follow(0, true);
            Assert.That(state.Level, Is.Zero);
            Assert.That(state.IsManual, Is.False);
        }

        [Test]
        public void BalconyOccludesOnlyItsFootprintAndLowerSelectionRevealsGround()
        {
            var state = new DungeonElevationViewState(); state.Bind(Stacked()); state.Step(1);
            Assert.That(state.TryGetVisibleSurface(Vector2Int.zero, out var balcony), Is.True);
            Assert.That(balcony.Level, Is.EqualTo(2));
            Assert.That(state.TryGetVisibleSurface(Vector2Int.right, out var openGround), Is.True);
            Assert.That(openGround.Level, Is.Zero);
            Assert.That(state.IsVisible(new DungeonSurfacePosition(Vector2Int.zero)), Is.False);
            state.Step(-1);
            Assert.That(state.IsVisible(new DungeonSurfacePosition(Vector2Int.zero)), Is.True);
            Assert.That(state.IsVisible(balcony), Is.False);
        }

        [Test]
        public void SelectionDoesNotMutateMapOrOpenBlockedTraversal()
        {
            var map = Stacked(); var edge = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
            map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.Full);
            var surfaces = map.Surfaces.ToArray();
            var walls = map.SurfaceWallEdges.ToArray();
            var state = new DungeonElevationViewState(); state.Bind(map); state.Step(1); state.Step(-1);
            Assert.That(map.Surfaces, Is.EquivalentTo(surfaces));
            Assert.That(map.SurfaceWallEdges, Is.EquivalentTo(walls));
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.right), Is.False);
        }

        [Test]
        public void NewMapResetsManualFocusAndSupportsNegativeElevations()
        {
            var state = new DungeonElevationViewState(); state.Bind(Stacked()); state.Step(1);
            var next = new DungeonFloorMap();
            next.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, -3), "r");
            next.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, -1), "r");
            state.Bind(next);
            Assert.That(state.Level, Is.EqualTo(-3));
            Assert.That(state.IsManual, Is.False);
            Assert.That(state.Step(1), Is.True);
            Assert.That(state.Level, Is.EqualTo(-1));
            state.Bind(null);
            Assert.That(state.VisibleSurfaces, Is.Empty);
            Assert.That(state.Step(1), Is.False);
        }

        [Test]
        public void UnknownFollowLevelDoesNotCancelManualSelection()
        {
            var state = new DungeonElevationViewState(); state.Bind(Stacked()); state.Step(1);
            Assert.That(state.Follow(99, true), Is.False);
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(state.IsManual, Is.True);
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonElevationPresentationTests
    {
        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void LayeredExplorationMovesContinuouslyDiagonallyAndRestoresFractionalPosition()
        {
            var root = new GameObject("Free roam test");
            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(root.transform);
            var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(grid.transform);
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                SetField(view, "floorTilemap", tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>()); SetField(view, "floorTile", tile);
                var wall = new GameObject("Wall source").AddComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>(); wall.transform.SetParent(root.transform);
                SetField(view, "wallSegmentPrefab", wall);
                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++) for (var y = 0; y < 3; y++) map.TryAddFloorCell(new Vector2Int(x, y), "r");
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "r");
                view.Rebuild(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var a = new GameObject("Leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); a.transform.SetParent(root.transform);
                a.SetPosition(new Vector2(.5f, .5f)); a.ConfigureTerrain(map, view, floor, "player:p");
                a.Move(new Vector2(.13f, .17f));
                Assert.That(a.Position.x, Is.EqualTo(.63f).Within(.0001f));
                Assert.That(a.Position.y, Is.EqualTo(.67f).Within(.0001f));
                a.Move(new Vector2(.6f, .6f));
                Assert.That(a.Position.x, Is.EqualTo(1.23f).Within(.0001f));
                Assert.That(a.Position.y, Is.EqualTo(1.27f).Within(.0001f));
                var b = new GameObject("Restored").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); b.transform.SetParent(root.transform);
                b.ConfigureTerrain(map, view, floor, "player:p");
                Assert.That(Vector2.Distance(a.Position, b.Position), Is.LessThan(.0001f));
                var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                party.ConfigureTraversal(floor, view, null);
                Assert.That(party.CanEnterTraversal(b, a.DungeonPosition), Is.True, "A party member must not reserve an exploration tile.");
                party.SeparateForCombat();
                Assert.That(party.IsSeparated, Is.True);
                party.RejoinAfterCombat();
                Assert.That(party.IsSeparated, Is.False);
                var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
                map.TryAddFloorCell(upper, "r");
                Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                    new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) }), Is.True);
                view.Rebuild(map);
                a.SetPosition(new Vector2(.5f, .5f));
                a.PreferredTraversalDestination = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
                a.BindCombat(new Godmist.Runtime.Combat.BattleUnit { Character = new Godmist.Runtime.Characters.CharacterInstance() });
                a.ReleaseCombat();
                Assert.That(a.PreferredTraversalDestination, Is.Null);
                a.Move(new Vector2(.6f, 0));
                Assert.That(a.DungeonPosition.IsOnStairs, Is.True, "Stair entry must work after combat while the camera is still on ground level.");
                a.Move(new Vector2(1f, 0));
                Assert.That(a.DungeonPosition.Surface, Is.EqualTo(upper));
                for (var pass = 0; pass < 3; pass++)
                {
                    view.FocusElevation(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(1, 1))), true);
                    Assert.That(view.IsSurfaceVisible(upper), Is.True);
                    view.FocusElevation(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)), true);
                    Assert.That(view.IsSurfaceVisible(new DungeonSurfacePosition(Vector2Int.zero)), Is.True);
                    Assert.That(view.IsSurfaceVisible(upper), Is.True, "Repeated cutaway must retain the balcony outside the local opening without PgUp/PgDown.");
                }
                var circle = a.gameObject.AddComponent<CircleCollider2D>(); circle.radius = .28f;
                SetField(a, "movementCollider", circle); circle.enabled = false;
                map.SetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(2, 1))), Godmist.Core.Enums.DungeonWallType.Full);
                a.SetPosition(new Vector2(1.5f, 1.5f));
                a.Move(new Vector2(.45f, 0));
                Assert.That(a.Position.x, Is.LessThanOrEqualTo(1.72f), "The disabled authored collider still needs clearance from a wall.");
                var beforeSlide = a.Position;
                a.Move(new Vector2(0, .1f));
                Assert.That(a.Position.y, Is.GreaterThan(beforeSlide.y), "Wall clearance must allow sliding along it.");

            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void ChangingViewedLevelReusesWallsAndDoesNotToggleColliders()
        {
            var root = new GameObject("Elevation presentation test");
            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(root.transform);
            var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(grid.transform);
            var prefab = new GameObject("Wall source", typeof(SpriteRenderer), typeof(BoxCollider2D));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var wall = prefab.AddComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
                SetField(wall, "movementCollider", prefab.GetComponent<BoxCollider2D>());
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var tilemap = tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                SetField(view, "floorTilemap", tilemap); SetField(view, "floorTile", tile); SetField(view, "wallSegmentPrefab", wall);
                var map = new DungeonFloorMap();
                foreach (var level in new[] { 0, 2 })
                {
                    map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, level), "r");
                    map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, level), "r");
                    map.SetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), level), Godmist.Core.Enums.DungeonWallType.Full);
                }
                view.Rebuild(map);
                var actorObject = new GameObject("Actor", typeof(SpriteRenderer)); actorObject.transform.SetParent(root.transform);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.ConfigureTerrain(map, view); actor.SetPosition(new Vector2(.5f, .5f));
                view.FocusElevation(actor.DungeonPosition, true);
                var actorRenderer = actorObject.GetComponent<SpriteRenderer>();
                actorRenderer.enabled = false;
                void RefreshActor() => typeof(Godmist.Views.Exploration.ExplorationCharacterView)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(actor, null);
                RefreshActor();
                Assert.That(actorRenderer.forceRenderingOff, Is.False);
                Assert.That(actorRenderer.enabled, Is.False, "Elevation must not reveal a renderer hidden by another system.");
                var walls = root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonWallSegmentView>();
                Assert.That(walls.Length, Is.EqualTo(2));
                var ground = walls.Single(w => w.GetComponent<BoxCollider2D>().enabled);
                var upper = walls.Single(w => !w.GetComponent<BoxCollider2D>().enabled);
                Assert.That(ground.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
                Assert.That(upper.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True);
                Assert.That(view.StepViewedLevel(1), Is.True);
                RefreshActor();
                Assert.That(actorRenderer.forceRenderingOff, Is.True);
                Assert.That(root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonWallSegmentView>(), Is.EquivalentTo(walls));
                Assert.That(ground.GetComponent<BoxCollider2D>().enabled, Is.True);
                Assert.That(upper.GetComponent<BoxCollider2D>().enabled, Is.False);
                Assert.That(ground.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True);
                Assert.That(upper.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
                Assert.That(view.TryGetVisibleSurface(new Vector3(.5f, .5f), out var visible), Is.True);
                Assert.That(visible.Level, Is.EqualTo(2));
                view.FollowElevation(0);
                Assert.That(view.ViewedLevel, Is.EqualTo(2));
                view.FollowElevation(0, true);
                Assert.That(view.ViewedLevel, Is.Zero);
                RefreshActor();
                Assert.That(actorRenderer.forceRenderingOff, Is.False);
                Assert.That(actorRenderer.enabled, Is.False);
                Assert.That(ground.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(tile); }
        }
    }
}

namespace Godmist.Tests.EditMode
{
    public class DungeonHeightCombatIntegrationTests
    {
        private static DungeonTraversalPosition Node(int x, int y, int level = 0)
            => new(new DungeonSurfacePosition(new Vector2Int(x, y), level));

        private static Godmist.Runtime.Combat.DungeonCombatContext Context(DungeonFloorMap map)
            => new("d", 0, "enc", "r", Vector2Int.zero, Vector2Int.right,
                map.Surfaces.Select(s => s.Cell).Distinct(), null, null, surfaceMap: map);

        [Test]
        public void BalconyCeilingBlocksBothDirectionsIncludingSeam()
        {
            var map = new DungeonFloorMap();
            foreach (var x in new[] { 0, 1 }) foreach (var level in new[] { 0, 1 })
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x, 0), level), "r");
            Assert.That(map.BlocksStructuralSight(Node(0, 0), Node(0, 0, 1)), Is.True);
            Assert.That(map.BlocksStructuralSight(Node(0, 0, 1), Node(0, 0)), Is.True);
            Assert.That(map.BlocksStructuralSight(Node(0, 0), Node(1, 0, 1)), Is.True);
            Assert.That(map.BlocksStructuralSight(Node(1, 0, 1), Node(0, 0)), Is.True);
        }

        [Test]
        public void OpenBalconyEdgeAllowsCrossHeightSight()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(new Vector2Int(0, 0), "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(1, 0), 1), "r");
            Assert.That(map.BlocksStructuralSight(Node(0, 0), Node(1, 0, 1)), Is.False);
            Assert.That(map.BlocksStructuralSight(Node(1, 0, 1), Node(0, 0)), Is.False);
        }

        [Test]
        public void LayeredGridKeepsGroundAndStairOccupancySeparateAndUsesEveryStep()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "r");
            var end = new DungeonSurfacePosition(new Vector2Int(3, 0), 1);
            map.TryAddFloorCell(end, "r");
            Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), end,
                Enumerable.Range(0, 4).Select(x => new Vector2Int(x, 0)).ToArray()), Is.True);
            var context = Context(map);
            Assert.That(new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var stair = map.Stairs.Single();
            var first = Godmist.Systems.Combat.DungeonBattleGridFactory.ToBattleTraversalPosition(context, new DungeonTraversalPosition(stair, 1));
            var second = Godmist.Systems.Combat.DungeonBattleGridFactory.ToBattleTraversalPosition(context, new DungeonTraversalPosition(stair, 2));
            Assert.That(grid.GetPositionsAt(1, 0).Count(), Is.EqualTo(2));
            grid.SetBlocked(new Godmist.Runtime.Combat.GridPosition(1, 0));
            Assert.That(grid.IsBlocked(first), Is.False);
            Assert.That(grid.CanMoveBetween(new Godmist.Runtime.Combat.GridPosition(0, 0), first), Is.True);
            Assert.That(grid.CanMoveBetween(first, second), Is.True);
            Assert.That(grid.CanMoveBetween(first, new Godmist.Runtime.Combat.GridPosition(2, 0)), Is.False);
            Assert.That(grid.GetMovementSurcharge(first, second), Is.Zero);
            Assert.That(Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(context, second),
                Is.EqualTo(new DungeonTraversalPosition(stair, 2)));
        }

        [Test]
        public void TargetingUsesProjectedRangeButCannotHitThroughFloor()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, 1), "r");
            var context = Context(map);
            new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(context, out var grid);
            var session = new Godmist.Runtime.Combat.CombatSession { Grid = grid, DungeonContext = context };
            Godmist.Runtime.Combat.BattleUnit Unit(int x, int level, bool player) => new()
            {
                Character = new Godmist.Runtime.Characters.CharacterInstance { CurrentHealth = 10 },
                Position = new Godmist.Runtime.Combat.GridPosition(x, 0, level), IsPlayerSide = player
            };
            var caster = Unit(0, 0, true); var blocked = Unit(0, 1, false); var edge = Unit(1, 1, false);
            session.Units.AddRange(new[] { caster, blocked, edge });
            var mode = new Godmist.Data.Skills.SkillTargetModeData();
            var targeting = new Godmist.Systems.Combat.CombatTargetingService();
            Assert.That(targeting.GetTargets(session, caster, mode, blocked.Position), Is.Empty);
            // Remove the tile directly above the caster in a second map, opening the balcony edge.
            var open = new DungeonFloorMap(); open.TryAddFloorCell(Vector2Int.zero, "r");
            open.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, 1), "r");
            session.DungeonContext = Context(open);
            new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(session.DungeonContext, out var openGrid); session.Grid = openGrid;
            session.Units.Remove(blocked);
            Assert.That(targeting.GetTargets(session, caster, mode, edge.Position), Does.Contain(edge));
            Assert.That(targeting.GetSelectablePositions(session, caster, mode), Does.Contain(edge.Position));
        }

        [Test]
        public void IntermediateStairHeightCanBeFollowedWithoutFloorAtThatLevel()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "r");
            var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 2); map.TryAddFloorCell(upper, "r");
            map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper, new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            var state = new DungeonElevationViewState(); state.Bind(map);
            Assert.That(state.Follow(1, true), Is.True);
            Assert.That(state.TryGetVisiblePosition(Vector2Int.right, out var position), Is.True);
            Assert.That(position.IsOnStairs, Is.True);
        }

        [Test]
        public void CombatExecutionCommitsStairNodesAndChargesNormalSuccessiveAp()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "r");
            var end = new DungeonSurfacePosition(new Vector2Int(3, 0), 1);
            map.TryAddFloorCell(end, "r");
            map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), end,
                Enumerable.Range(0, 4).Select(x => new Vector2Int(x, 0)).ToArray());
            var context = Context(map);
            new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(context, out var grid);
            var session = new Godmist.Runtime.Combat.CombatSession { Grid = grid, DungeonContext = context };
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Characters.CharacterDefinition>();
            try
            {
                var unit = new Godmist.Runtime.Combat.BattleUnit
                {
                    Character = new Godmist.Runtime.Characters.CharacterInstance { Definition = definition, CharacterId = "p", CurrentHealth = 10 },
                    Position = new Godmist.Runtime.Combat.GridPosition(0, 0), CurrentActionPoints = 20, IsPlayerSide = true
                };
                session.Units.Add(unit);
                var stair = map.Stairs.Single();
                var path = new[] { new DungeonTraversalPosition(stair, 1), new DungeonTraversalPosition(stair, 2), new DungeonTraversalPosition(end) }
                    .Select(p => Godmist.Systems.Combat.DungeonBattleGridFactory.ToBattleTraversalPosition(context, p)).ToArray();
                Assert.That(new Godmist.Systems.Combat.CombatMovementSystem().TryMoveAlongPath(session, unit, path), Is.True);
                Assert.That(unit.Position, Is.EqualTo(path[2]));
                Assert.That(unit.MovementStepsThisTurn, Is.EqualTo(3));
                Assert.That(unit.CurrentActionPoints, Is.EqualTo(14));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void BoundaryWallWithMissingAdjacentFloorDoesNotBreakCombatGrid()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "r");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "r");
            map.SetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.left), 1), Godmist.Core.Enums.DungeonWallType.Full);
            Assert.That(new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(Context(map), out _), Is.True);
        }
    }
}
