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