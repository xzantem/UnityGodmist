using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRoomFootprintTests
    {
        private DungeonRoomTemplateCatalog catalog;
        private DungeonRoomTemplateDefinition template;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null)
            {
                Object.DestroyImmediate(catalog);
            }

            if (template != null)
            {
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void Build_OFootprint_LeavesInteriorVoidAndWalls()
        {
            catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", "template.test.o");
            SetAutoProperty(template, "SizeInCells", new Vector2Int(3, 3));
            SetAutoProperty(
                template,
                "Footprint",
                CreateFootprint(
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(0, 2),
                    new Vector2Int(1, 0),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 0),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2)));

            catalog.Templates.Add(template);

            var floor = new DungeonFloorInstance(0);
            Assert.That(
                floor.TryAddRoom(
                    new DungeonRoomInstance(
                        "room.test.o",
                        DungeonRoomType.Normal,
                        template.Id,
                        Vector2Int.zero)),
                Is.True);

            var map = new DungeonFloorMapBuilder().Build(floor, catalog);

            Assert.That(map.FloorCells, Has.Count.EqualTo(8));
            Assert.That(map.IsWalkable(new Vector2Int(1, 1)), Is.False);
            Assert.That(
                map.HasWallBetween(
                    new Vector2Int(1, 0),
                    new Vector2Int(1, 1)),
                Is.True);
            Assert.That(
                map.HasWallBetween(
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)),
                Is.True);
        }

        [Test]
        public void Build_EmptyFootprint_UsesFullBoundingBox()
        {
            catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", "template.test.rect");
            SetAutoProperty(template, "SizeInCells", new Vector2Int(4, 2));
            catalog.Templates.Add(template);

            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(
                new DungeonRoomInstance(
                    "room.test.rect",
                    DungeonRoomType.Normal,
                    template.Id,
                    Vector2Int.zero));

            var map = new DungeonFloorMapBuilder().Build(floor, catalog);

            Assert.That(map.FloorCells, Has.Count.EqualTo(8));
            Assert.That(map.IsWalkable(new Vector2Int(3, 1)), Is.True);
        }

        [Test]
        public void TransformCell_RotatesRectangularRoomAndSwapsSize()
        {
            var sourceSize = new Vector2Int(4, 2);

            Assert.That(
                DungeonRoomTransformUtility.GetRotatedSize(sourceSize, 90),
                Is.EqualTo(new Vector2Int(2, 4)));
            Assert.That(
                DungeonRoomTransformUtility.TransformCell(
                    new Vector2Int(0, 0),
                    sourceSize,
                    90),
                Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(
                DungeonRoomTransformUtility.TransformCell(
                    new Vector2Int(3, 1),
                    sourceSize,
                    90),
                Is.EqualTo(new Vector2Int(0, 3)));
        }

        [Test]
        public void InverseTransformCell_RestoresAuthoredCell()
        {
            var sourceSize = new Vector2Int(5, 3);
            var authoredCell = new Vector2Int(2, 1);

            for (var rotation = 0; rotation < 360; rotation += 90)
            {
                var transformedCell = DungeonRoomTransformUtility.TransformCell(
                    authoredCell,
                    sourceSize,
                    rotation);

                Assert.That(
                    DungeonRoomTransformUtility.InverseTransformCell(
                        transformedCell,
                        sourceSize,
                        rotation),
                    Is.EqualTo(authoredCell));
            }
        }

        [Test]
        public void TransformDirection_RotatesCounterClockwiseInWorldCoordinates()
        {
            Assert.That(
                DungeonRoomTransformUtility.TransformDirection(
                    DungeonEdgeDirection.Up,
                    90),
                Is.EqualTo(DungeonEdgeDirection.Left));
            Assert.That(
                DungeonRoomTransformUtility.TransformDirection(
                    DungeonEdgeDirection.Right,
                    180),
                Is.EqualTo(DungeonEdgeDirection.Left));
            Assert.That(
                DungeonRoomTransformUtility.InverseTransformDirection(
                    DungeonEdgeDirection.Left,
                    90),
                Is.EqualTo(DungeonEdgeDirection.Up));
        }

        [Test]
        public void TransformArea_RotatesAnchorAreaAndKeepsItsCellsInBounds()
        {
            var area = DungeonRoomTransformUtility.TransformArea(
                new Vector2Int(1, 0),
                new Vector2Int(2, 1),
                new Vector2Int(4, 3),
                90);

            Assert.That(area.position, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(area.size, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(
                new RectInt(Vector2Int.zero, new Vector2Int(3, 4)).Contains(area.position),
                Is.True);
        }

        private static DungeonRoomFootprint CreateFootprint(
            params Vector2Int[] offsets)
        {
            var footprint = new DungeonRoomFootprint();
            SetAutoProperty(
                footprint,
                "FloorCellOffsets",
                new List<Vector2Int>(offsets));

            return footprint;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
