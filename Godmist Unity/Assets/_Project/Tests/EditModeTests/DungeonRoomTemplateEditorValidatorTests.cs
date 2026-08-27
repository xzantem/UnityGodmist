using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRoomTemplateEditorValidatorTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Validate_DefaultRectangle_IsValid()
        {
            var template = CreateTemplate();

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        [Test]
        public void Validate_DisconnectedFootprint_ReturnsError()
        {
            var template = CreateTemplate();
            SetAutoProperty(
                template,
                "Footprint",
                CreateFootprint(
                    new Vector2Int(0, 0),
                    new Vector2Int(2, 2)));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("spojny"));
        }

        [Test]
        public void Validate_SemanticAnchorOutsideFootprint_ReturnsError()
        {
            var template = CreateTemplate();
            SetAutoProperty(
                template,
                "Footprint",
                CreateFootprint(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)));
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "chest.test",
                    DungeonSemanticAnchorType.Chest,
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1)));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("poza footprintem"));
        }

        [Test]
        public void Validate_SemanticAnchorDependencyCycle_ReturnsError()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "trap.a",
                    DungeonSemanticAnchorType.Trap,
                    new Vector2Int(0, 0),
                    Vector2Int.one,
                    requiresAnchorId: "trap.b"));
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "trap.b",
                    DungeonSemanticAnchorType.Trap,
                    new Vector2Int(1, 0),
                    Vector2Int.one,
                    requiresAnchorId: "trap.a"));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("cykl"));
        }

        private DungeonRoomTemplateDefinition CreateTemplate()
        {
            var template = ScriptableObject.CreateInstance<
                DungeonRoomTemplateDefinition>();
            createdObjects.Add(template);

            SetAutoProperty(template, "Id", "room.template.test");
            SetAutoProperty(template, "DisplayName", "Test room template");
            SetAutoProperty(template, "RoomType", DungeonRoomType.Normal);
            SetAutoProperty(template, "SizeInCells", new Vector2Int(4, 4));
            return template;
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
