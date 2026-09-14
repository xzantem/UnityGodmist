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

        [TestCase(DungeonWallType.Full)]
        [TestCase(DungeonWallType.Low)]
        [TestCase(DungeonWallType.Railing)]
        public void Validate_ExternalWallEdge_IsValid(DungeonWallType type)
        {
            var template = CreateTemplate();
            template.StructuralAnchors.Add(new DungeonStructuralAnchorData(
                DungeonStructuralAnchorType.Wall, Vector2Int.zero, DungeonEdgeDirection.Left, type));
            var report = DungeonRoomTemplateEditorValidator.Validate(template);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
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

        [Test]
        public void Validate_BoundaryEdgeInsideFootprint_ReturnsError()
        {
            var template = CreateTemplate();
            SetAutoProperty(template, "BoundaryEdgesConfigured", true);
            template.AllowedBoundaryEdges.Add(
                new DungeonRoomBoundaryEdgeData(
                    new Vector2Int(1, 1),
                    DungeonEdgeDirection.Up));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("zewnetrznej krawedzi"));
        }

        [Test]
        public void Validate_BoundaryEdgeCanBeDisabledForSpecificSide()
        {
            var template = CreateTemplate();
            SetAutoProperty(template, "BoundaryEdgesConfigured", true);
            template.AllowedBoundaryEdges.Add(
                new DungeonRoomBoundaryEdgeData(
                    new Vector2Int(0, 0),
                    DungeonEdgeDirection.Left));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
            Assert.That(
                template.AllowsBoundaryEdge(
                    new Vector2Int(0, 0),
                    DungeonEdgeDirection.Left),
                Is.True);
            Assert.That(
                template.AllowsBoundaryEdge(
                    new Vector2Int(0, 3),
                    DungeonEdgeDirection.Left),
                Is.False);
        }

        [Test]
        public void Validate_ConcaveBoundaryEdge_IsValid()
        {
            var template = CreateTemplate();
            SetAutoProperty(
                template,
                "Footprint",
                CreateFootprint(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(0, 2),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 2)));
            SetAutoProperty(template, "BoundaryEdgesConfigured", true);
            template.AllowedBoundaryEdges.Add(
                new DungeonRoomBoundaryEdgeData(
                    new Vector2Int(0, 1),
                    DungeonEdgeDirection.Right));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
            Assert.That(
                template.AllowsBoundaryEdge(
                    new Vector2Int(0, 1),
                    DungeonEdgeDirection.Right),
                Is.True);
        }

        [Test]
        public void Validate_EnclosedHoleBoundaryEdge_ReturnsError()
        {
            var template = CreateTemplate();
            SetAutoProperty(
                template,
                "SizeInCells",
                new Vector2Int(3, 3));
            SetAutoProperty(
                template,
                "Footprint",
                CreateFootprint(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(0, 2),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 2)));
            SetAutoProperty(template, "BoundaryEdgesConfigured", true);
            template.AllowedBoundaryEdges.Add(
                new DungeonRoomBoundaryEdgeData(
                    new Vector2Int(0, 1),
                    DungeonEdgeDirection.Right));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("zewnetrznej krawedzi"));
        }

        [Test]
        public void Validate_ProgressionControlOutsideRequiredBranch_ReturnsError()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "lever.test",
                    DungeonSemanticAnchorType.Lever,
                    new Vector2Int(1, 1),
                    Vector2Int.one));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("typu Lever albo Condition"));
        }

        [Test]
        public void Validate_RequiredProgressionBranchWithControls_IsValid()
        {
            var template = CreateTemplate();
            SetAutoProperty(
                template,
                "RoomType",
                DungeonRoomType.Optional);
            SetAutoProperty(
                template,
                "CanBeRequiredProgressionBranch",
                true);
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "lever.test",
                    DungeonSemanticAnchorType.Lever,
                    new Vector2Int(1, 1),
                    Vector2Int.one));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        [Test]
        public void Validate_EnemyAnchorWithEncounterInstanceId_IsValid()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "enemy.anchor.01",
                    DungeonSemanticAnchorType.Enemy,
                    new Vector2Int(1, 1),
                    Vector2Int.one,
                    encounterEnemyIndex: 0));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        [Test]
        public void Validate_EnemyAnchorWithoutEncounterInstanceId_ReturnsError()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "enemy.anchor.01",
                    DungeonSemanticAnchorType.Enemy,
                    new Vector2Int(1, 1),
                    Vector2Int.one));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("indeks"));
        }

        [Test]
        public void Validate_EnemyAnchorsCannotReuseTargetOrCell()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "enemy.anchor.01",
                    DungeonSemanticAnchorType.Enemy,
                    new Vector2Int(1, 1),
                    Vector2Int.one,
                    encounterEnemyIndex: 0));
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "enemy.anchor.02",
                    DungeonSemanticAnchorType.Enemy,
                    new Vector2Int(1, 1),
                    Vector2Int.one,
                    encounterEnemyIndex: 0));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("wiecej niz jednego anchora"));
            Assert.That(
                report.Errors,
                Has.Some.Contains("dzielic komorki"));
        }

        [Test]
        public void Validate_EnemyAnchorCannotOverlapOtherSemanticAnchor()
        {
            var template = CreateTemplate();
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "enemy.anchor.01",
                    DungeonSemanticAnchorType.Enemy,
                    new Vector2Int(1, 1),
                    Vector2Int.one,
                    encounterEnemyIndex: 0));
            template.SemanticAnchors.Add(
                new DungeonSemanticAnchorData(
                    "obstacle.anchor.01",
                    DungeonSemanticAnchorType.Obstacle,
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1)));

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("koliduje"));
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
