using System;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonStructuralAnchorTests
    {
        private DungeonRoomTemplateCatalog catalog;
        private DungeonRoomTemplateDefinition template;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            if (template != null)
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void Build_WallAnchor_BlocksMovementBetweenItsCells()
        {
            var floor = CreateFloorWithSingleRoom();
            var builder = new DungeonFloorMapBuilder();

            var map = builder.Build(floor, catalog);

            var firstCell = new Vector2Int(1, 1);
            var secondCell = new Vector2Int(2, 1);

            Assert.That(map.HasWallBetween(firstCell, secondCell), Is.True);
            Assert.That(map.CanMoveBetween(firstCell, secondCell), Is.False);
        }

        [Test]
        public void Build_WallAnchor_DoesNotBlockUnrelatedCells()
        {
            var floor = CreateFloorWithSingleRoom();
            var builder = new DungeonFloorMapBuilder();

            var map = builder.Build(floor, catalog);

            Assert.That(
                map.CanMoveBetween(
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1)),
                Is.True);
        }

        [TestCase(DungeonWallType.Low, true)]
        [TestCase(DungeonWallType.Railing, false)]
        public void Build_TypedInternalEdge_UsesSameExplorationRules(DungeonWallType type, bool walk)
        {
            var floor = CreateFloorWithSingleRoom();
            template.StructuralAnchors.Clear();
            template.StructuralAnchors.Add(new DungeonStructuralAnchorData(
                DungeonStructuralAnchorType.Wall, new(1, 1), DungeonEdgeDirection.Right, type));
            var map = new DungeonFloorMapBuilder().Build(floor, catalog);
            Assert.That(map.CanMoveBetween(new(1, 1), new(2, 1)), Is.EqualTo(walk));
            Assert.That(map.GetWallType(new DungeonCellEdge(new(1, 1), new(2, 1))), Is.EqualTo(type));
        }

        [TestCase(DungeonWallType.Low)]
        [TestCase(DungeonWallType.Railing)]
        public void Build_TypedExternalEdge_PreservesVoidAndExcludesDoorway(DungeonWallType type)
        {
            var floor = CreateFloorWithSingleRoom();
            template.StructuralAnchors.Clear();
            template.StructuralAnchors.Add(new DungeonStructuralAnchorData(
                DungeonStructuralAnchorType.Wall, Vector2Int.zero, DungeonEdgeDirection.Left, type));
            var map = new DungeonFloorMapBuilder().Build(floor, catalog);
            Assert.That(map.GetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.left)), Is.EqualTo(type));
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.left), Is.False);
            Assert.That(template.AllowsBoundaryEdge(Vector2Int.zero, DungeonEdgeDirection.Left), Is.False);
        }

        [TestCase(0)]
        [TestCase(90)]
        [TestCase(180)]
        [TestCase(270)]
        public void Build_RotatesTypedExternalEdges(int rotation)
        {
            CreateFloorWithSingleRoom();
            template.StructuralAnchors.Clear();
            template.StructuralAnchors.Add(new DungeonStructuralAnchorData(
                DungeonStructuralAnchorType.Wall, Vector2Int.zero, DungeonEdgeDirection.Left, DungeonWallType.Low));
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("rotated", DungeonRoomType.Normal, template.Id, Vector2Int.zero,
                rotationDegrees: rotation));
            var map = new DungeonFloorMapBuilder().Build(floor, catalog);
            var cell = DungeonRoomTransformUtility.TransformCell(Vector2Int.zero, template.SizeInCells, rotation);
            var offset = DungeonRoomTransformUtility.GetDirectionOffset(
                DungeonRoomTransformUtility.TransformDirection(DungeonEdgeDirection.Left, rotation));
            Assert.That(map.GetWallType(new DungeonCellEdge(cell, cell + offset)), Is.EqualTo(DungeonWallType.Low));
            Assert.That(map.CanMoveBetween(cell, cell + offset), Is.False);
        }

        [Test]
        public void SerializedWallType_RoundTripsAndLegacyDefaultsToFullWall()
        {
            var legacy = JsonUtility.FromJson<DungeonStructuralAnchorData>("{}");
            Assert.That(legacy.WallType, Is.EqualTo(DungeonWallType.Full));
            var low = new DungeonStructuralAnchorData(DungeonStructuralAnchorType.Wall,
                new Vector2Int(1, 2), DungeonEdgeDirection.Left, DungeonWallType.Low);
            var restored = JsonUtility.FromJson<DungeonStructuralAnchorData>(JsonUtility.ToJson(low));
            Assert.That(restored.WallType, Is.EqualTo(DungeonWallType.Low));
            Assert.That(restored.LocalCell, Is.EqualTo(low.LocalCell));
            Assert.That(restored.Direction, Is.EqualTo(low.Direction));
        }

        private DungeonFloorInstance CreateFloorWithSingleRoom()
        {
            catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", "template.test.room");
            SetAutoProperty(template, "SizeInCells", new Vector2Int(4, 4));

            template.StructuralAnchors.Add(
                new DungeonStructuralAnchorData(
                    DungeonStructuralAnchorType.Wall,
                    new Vector2Int(1, 1),
                    DungeonEdgeDirection.Right));

            catalog.Templates.Add(template);

            var floor = new DungeonFloorInstance(0);

            var room = new DungeonRoomInstance(
                "room.test.01",
                DungeonRoomType.Normal,
                template.Id,
                Vector2Int.zero);

            Assert.That(floor.TryAddRoom(room), Is.True);

            return floor;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = FindBackingField(target.GetType(), propertyName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono backing field dla {propertyName}.");

            field.SetValue(target, value);
        }

        private static FieldInfo FindBackingField(
            Type type,
            string propertyName)
        {
            for (var currentType = type;
                currentType != null;
                currentType = currentType.BaseType)
            {
                var field = currentType.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}