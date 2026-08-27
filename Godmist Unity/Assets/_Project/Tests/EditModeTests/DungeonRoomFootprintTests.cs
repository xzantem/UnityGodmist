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
