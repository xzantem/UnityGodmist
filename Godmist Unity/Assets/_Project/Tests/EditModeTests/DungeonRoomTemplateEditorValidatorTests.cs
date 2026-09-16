using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRoomEditorContentEditsTests
    {
        private DungeonRoomTemplateDefinition template;
        private SerializedObject serialized;

        [SetUp]
        public void Setup()
        {
            template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("a", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.zero, Vector2Int.one));
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("b", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.right, Vector2Int.one, requiresAnchorId: "a"));
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("c", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.up, Vector2Int.one));
            serialized = new SerializedObject(template);
        }

        [TearDown]
        public void Cleanup()
        {
            Undo.ClearUndo(template);
            serialized.Dispose();
            Object.DestroyImmediate(template);
        }

        [Test]
        public void RenameUpdatesDependencyAndGroupAndSupportsUndoRedo()
        {
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }));
            serialized.Update();
            Undo.IncrementCurrentGroup();
            Assert.That(DungeonRoomEditorContentEdits.RenameAnchor(serialized, "a", "renamed", out var error), Is.True, error);
            Assert.That(template.SemanticAnchors[1].RequiresAnchorId, Is.EqualTo("renamed"));
            Assert.That(template.ContentGroups[0].AnchorIds[0], Is.EqualTo("renamed"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(template.SemanticAnchors[0].Id, Is.EqualTo("a"));
            Assert.That(template.SemanticAnchors[1].RequiresAnchorId, Is.EqualTo("a"));
            Assert.That(template.ContentGroups[0].AnchorIds[0], Is.EqualTo("a"));
            Undo.PerformRedo();
            Assert.That(template.SemanticAnchors[0].Id, Is.EqualTo("renamed"));
            Assert.That(template.SemanticAnchors[1].RequiresAnchorId, Is.EqualTo("renamed"));
        }

        [TestCase("")]
        [TestCase("b")]
        public void InvalidRenameDoesNotChangeData(string name)
        {
            var before = EditorJsonUtility.ToJson(template);
            Assert.That(DungeonRoomEditorContentEdits.RenameAnchor(serialized, "a", name, out _), Is.False);
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
        }

        [Test]
        public void DeletionRejectsExternalDependencyAndRemovesEmptyGroupWhenComplete()
        {
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }));
            serialized.Update();
            Assert.That(DungeonRoomEditorContentEdits.DeleteAnchors(serialized, new[] { "a" }, out var error), Is.False);
            Assert.That(error, Does.Contain("b").And.Contain("a"));
            Assert.That(template.SemanticAnchors.Count, Is.EqualTo(3));
            Assert.That(DungeonRoomEditorContentEdits.DeleteAnchors(serialized, new[] { "a", "b" }, out error), Is.True, error);
            Assert.That(template.ContentGroups, Is.Empty);
            Assert.That(template.SemanticAnchors.Count, Is.EqualTo(1));
            Assert.That(template.SemanticAnchors[0].Id, Is.EqualTo("c"));
        }

        [Test]
        public void CreateGroupNormalizesMembersAndUndoRestoresOriginalSettings()
        {
            template.SemanticAnchors[0] = new DungeonSemanticAnchorData("a", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.zero, Vector2Int.one, .2f, true);
            serialized.Update();
            Undo.IncrementCurrentGroup();
            Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "pair", new[] { "a", "b" }, true, .33f, out var error), Is.True, error);
            Assert.That(template.ContentGroups.Count, Is.EqualTo(1));
            Assert.That(template.ContentGroups[0].IsRequired, Is.True);
            Assert.That(template.ContentGroups[0].SpawnChance, Is.EqualTo(.33f));
            Assert.That(template.SemanticAnchors[0].IsRequired, Is.False);
            Assert.That(template.SemanticAnchors[0].SpawnChance, Is.EqualTo(1f));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(template.ContentGroups, Is.Empty);
            Assert.That(template.SemanticAnchors[0].IsRequired, Is.True);
            Assert.That(template.SemanticAnchors[0].SpawnChance, Is.EqualTo(.2f));
        }

        [Test]
        public void CreatingGroupThatContractsDependencyCycleIsRejected()
        {
            template.SemanticAnchors[2] = new DungeonSemanticAnchorData("c", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.up, Vector2Int.one, requiresAnchorId: "b");
            serialized.Update();
            Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "cycle", new[] { "a", "c" }, false, 1f, out var error), Is.False);
            Assert.That(error, Does.Contain("cykl"));
            Assert.That(template.ContentGroups, Is.Empty);
        }

        [Test]
        public void EnemyAndExistingMembershipCannotBeGroupedAgain()
        {
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("enemy", DungeonSemanticAnchorType.Enemy,
                Vector2Int.one, Vector2Int.one, encounterEnemyIndex: 0));
            serialized.Update();
            Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "bad", new[] { "a", "enemy" }, false, 1, out _), Is.False);
            Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "pair", new[] { "a", "b" }, false, 1, out _), Is.True);
            Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "again", new[] { "a", "c" }, false, 1, out _), Is.False);
        }

        [Test]
        public void MoveMemberRemovesEmptySourceAndJoiningSameGroupIsIdempotent()
        {
            template.ContentGroups.Add(new DungeonContentGroupData("source", new[] { "a" }, true));
            template.ContentGroups.Add(new DungeonContentGroupData("target", new[] { "b", "c" }, false, .33f));
            serialized.Update();
            Assert.That(DungeonRoomEditorContentEdits.JoinGroup(serialized, "a", 1, out var error), Is.True, error);
            Assert.That(template.ContentGroups.Count, Is.EqualTo(1));
            Assert.That(template.ContentGroups[0].AnchorIds, Is.EquivalentTo(new[] { "a", "b", "c" }));
            Assert.That(DungeonRoomEditorContentEdits.JoinGroup(serialized, "a", 0, out error), Is.True, error);
            Assert.That(template.ContentGroups[0].AnchorIds.Count, Is.EqualTo(3));
        }

        [Test]
        public void DetachAndDissolvePreserveGroupSettingsOnIndependentAnchors()
        {
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, true, .33f, "test.pair"));
            serialized.Update();
            Assert.That(DungeonRoomEditorContentEdits.DetachAnchor(serialized, "a", out var error), Is.True, error);
            Assert.That(template.SemanticAnchors[0].IsRequired, Is.True);
            Assert.That(template.SemanticAnchors[0].SpawnChance, Is.EqualTo(.33f));
            Assert.That(template.SemanticAnchors[0].DistributionKey, Is.EqualTo("test.pair"));
            Assert.That(template.ContentGroups[0].AnchorIds, Is.EqualTo(new[] { "b" }));
            DungeonRoomEditorContentEdits.DissolveGroup(serialized, 0);
            Assert.That(template.ContentGroups, Is.Empty);
            Assert.That(template.SemanticAnchors[1].IsRequired, Is.True);
            Assert.That(template.SemanticAnchors[1].RequiresAnchorId, Is.EqualTo("a"));
        }

        [Test]
        public void SelectionNeverCreatesAnchorsAndCtrlClickTogglesSelection()
        {
            var window = ScriptableObject.CreateInstance<DungeonRoomEditorWindow>();
            var previousSelection = Selection.activeObject;
            try
            {
                Invoke(window, "SetTemplate", template);
                Invoke(window, "EnsureSerializedObject");
                var before = EditorJsonUtility.ToJson(template);
                Click(window, new Vector2(5, 75), false);
                Click(window, new Vector2(15, 75), true);
                var selected = (HashSet<string>)typeof(DungeonRoomEditorWindow).GetField("selectedAnchorIds",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                Assert.That(selected, Is.EquivalentTo(new[] { "a", "b" }));
                Click(window, new Vector2(5, 75), true);
                Assert.That(selected, Is.EquivalentTo(new[] { "b" }));
                Click(window, new Vector2(25, 75), false);
                Assert.That(selected, Is.Empty);
                Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(window);
                Selection.activeObject = previousSelection;
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator EditorPanelsRenderAtMinimumAndLargeWindowSizes()
        {
            var window = ScriptableObject.CreateInstance<DungeonRoomEditorWindow>();
            var previousSelection = Selection.activeObject;
            try
            {
                Invoke(window, "SetTemplate", template);
                window.Show();
                foreach (var size in new[] { new Vector2(960, 560), new Vector2(1600, 900) })
                {
                    window.position = new Rect(new Vector2(30, 30), size);
                    window.Repaint();
                    yield return null;
                    yield return null;
                    Invoke(window, "EnsureSerializedObject");
                    Invoke(window, "SelectSemantic", 0, false);
                    window.Repaint();
                    yield return null;
                    Assert.That(DungeonRoomEditorContentEdits.CreateGroup(serialized, "render.group", new[] { "a", "b" }, false, .33f, out var error), Is.True, error);
                    // The group was authored through another SerializedObject, as with a separate Inspector.
                    ((SerializedObject)typeof(DungeonRoomEditorWindow).GetField("serializedTemplate",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window)).Update();
                    Invoke(window, "SelectGroup", 0);
                    Assert.That((int)typeof(DungeonRoomEditorWindow).GetField("selectedGroup",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window), Is.EqualTo(0));
                    window.Repaint();
                    yield return null;
                    DungeonRoomEditorContentEdits.DissolveGroup(serialized, 0);
                    Invoke(window, "ClearAnchorSelection");
                }
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                Selection.activeObject = previousSelection;
            }
        }

        private static object Invoke(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void Click(DungeonRoomEditorWindow window, Vector2 point, bool control)
        {
            var input = new Event { type = EventType.MouseDown, button = 0, mousePosition = point,
                modifiers = control ? EventModifiers.Control : EventModifiers.None };
            Invoke(window, "ProcessGridInput", input, new Rect(0, 0, 80, 80), 10f, new Vector2Int(8, 8));
        }
    }

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

namespace Godmist.Tests.EditMode
{
    public class DungeonRoomSurfaceEditorTests
    {
        private DungeonRoomTemplateDefinition template;
        private DungeonRoomEditorWindow window;
        private Object previousSelection;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup()
        {
            previousSelection = Selection.activeObject;
            template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
            using (var data = new SerializedObject(template))
            {
                data.FindProperty("<SizeInCells>k__BackingField").vector2IntValue = new Vector2Int(3, 3);
                data.FindProperty("<Id>k__BackingField").stringValue = "height.test";
                data.FindProperty("<DisplayName>k__BackingField").stringValue = "Height test";
                data.FindProperty("<AllowedBoundaryEdges>k__BackingField").arraySize = 0;
                data.FindProperty("<BoundaryEdgesConfigured>k__BackingField").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            window = ScriptableObject.CreateInstance<DungeonRoomEditorWindow>();
            Invoke("SetTemplate", template); Invoke("EnsureSerializedObject");
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(window); Undo.ClearUndo(template); Object.DestroyImmediate(template);
            Selection.activeObject = previousSelection;
        }
        private object Invoke(string name, params object[] args) => typeof(DungeonRoomEditorWindow).GetMethod(name, Flags).Invoke(window, args);
        private void Set(string name, object value) => typeof(DungeonRoomEditorWindow).GetField(name, Flags).SetValue(window, value);
        private SerializedObject Data => (SerializedObject)typeof(DungeonRoomEditorWindow).GetField("serializedTemplate", Flags).GetValue(window);
        private bool Paint(Vector2Int cell, int level, bool add)
        {
            var args = new object[] { cell, level, add, null };
            return (bool)Invoke("TryPaintSurface", args);
        }
        private static Godmist.Runtime.Dungeons.DungeonSurfacePosition Surface(int x, int y, int level)
            => new(new Vector2Int(x, y), level);
        private bool Stair(Godmist.Runtime.Dungeons.DungeonSurfacePosition a, Godmist.Runtime.Dungeons.DungeonSurfacePosition b)
        {
            var cells = new List<Vector2Int> { a.Cell }; var current = a.Cell;
            while (current.x != b.Cell.x) { current.x += current.x < b.Cell.x ? 1 : -1; cells.Add(current); }
            while (current.y != b.Cell.y) { current.y += current.y < b.Cell.y ? 1 : -1; cells.Add(current); }
            return (bool)Invoke("TrySaveStairRoute", new object[] { a, b, cells, -1, null });
        }

        [Test] public void PaintingUpperLevelPreservesLegacyGroundAndUndoRedo()
        {
            Assert.That(template.Surfaces, Is.Empty);
            Undo.IncrementCurrentGroup();
            Assert.That(Paint(Vector2Int.one, 1, true), Is.True);
            Assert.That(template.Surfaces.Count, Is.EqualTo(10));
            Assert.That(template.ContainsSurface(Vector2Int.one, 0), Is.True);
            Assert.That(template.ContainsSurface(Vector2Int.one, 1), Is.True);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Data.Update();
            Assert.That(template.Surfaces, Is.Empty);
            Assert.That(template.ContainsSurface(Vector2Int.one, 1), Is.False);
            Undo.PerformRedo(); Data.Update();
            Assert.That(template.ContainsSurface(Vector2Int.one, 1), Is.True);
        }

        [Test] public void DeletingUpperSurfaceDoesNotDeleteGroundOrGroundContent()
        {
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("content", DungeonSemanticAnchorType.Obstacle, Vector2Int.one, Vector2Int.one)); Data.Update();
            Paint(Vector2Int.one, 1, true);
            Assert.That(Paint(Vector2Int.one, 1, false), Is.True);
            var before = EditorJsonUtility.ToJson(template);
            Assert.That(Paint(Vector2Int.one, 0, false), Is.False);
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
        }

        [Test] public void StairRequiresDifferentExistingLevelsRejectsDuplicateAndProtectsEndpoints()
        {
            Paint(Vector2Int.one, 1, true);
            var a = Surface(0, 0, 0); var b = Surface(1, 1, 1);
            Assert.That(Stair(a, Surface(1, 1, 0)), Is.False);
            Assert.That(Stair(a, Surface(1, 1, 2)), Is.False);
            Assert.That(Stair(a, b), Is.True);
            Assert.That(Stair(b, a), Is.False);
            var before = EditorJsonUtility.ToJson(template);
            Assert.That(Paint(Vector2Int.one, 1, false), Is.False);
            Assert.That(Paint(Vector2Int.zero, 0, false), Is.False);
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
            Assert.DoesNotThrow(() => template.ValidateSurfaceGeometry());
        }

        [Test] public void StairAdditionCanBeUndoneAndRedoneWithoutLosingSurfaces()
        {
            Paint(Vector2Int.one, 1, true);
            Undo.IncrementCurrentGroup();
            Stair(Surface(0, 0, 0), Surface(1, 1, 1));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Data.Update();
            Assert.That(template.Stairs, Is.Empty);
            Assert.That(template.ContainsSurface(Vector2Int.one, 1), Is.True);
            Undo.PerformRedo(); Data.Update();
            Assert.That(template.Stairs.Count, Is.EqualTo(1));
            Assert.DoesNotThrow(() => template.ValidateSurfaceGeometry());
        }

        [Test] public void ValidationRejectsGroundContentAndTerrainOnUpperOnlySurface()
        {
            Paint(Vector2Int.one, 1, true); Paint(Vector2Int.one, 0, false);
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("upper.only", DungeonSemanticAnchorType.Obstacle, Vector2Int.one, Vector2Int.one));
            template.DifficultTerrainCells.Add(Vector2Int.one);
            var report = DungeonRoomTemplateEditorValidator.Validate(template);
            Assert.That(report.Errors, Has.Some.Contains("upper.only"));
            Assert.That(report.Errors, Has.Some.Contains("Difficult terrain"));
        }

        [Test] public void LevelSwitchKeepsPendingEndpointButClearsSelection()
        {
            Paint(Vector2Int.one, 1, true);
            Invoke("PlaceStairEndpoint", Vector2Int.zero);
            Invoke("PlaceStairEndpoint", Vector2Int.right);
            Invoke("ChangeLevel", 1);
            Invoke("PlaceStairEndpoint", Vector2Int.one);
            Assert.That(template.Stairs.Count, Is.EqualTo(1));
            Assert.That(template.Stairs[0].First.Level, Is.EqualTo(0));
            Assert.That(template.Stairs[0].Second.Level, Is.EqualTo(1));
        }

        [Test] public void StructuralAnchorsAtSameXYRemainIndependent()
        {
            Paint(Vector2Int.one, 1, true);
            Invoke("SelectOrAddStructuralAnchor", Vector2Int.one, new Vector2Int(3, 3)); Data.ApplyModifiedProperties();
            Invoke("ChangeLevel", 1);
            Invoke("SelectOrAddStructuralAnchor", Vector2Int.one, new Vector2Int(3, 3)); Data.ApplyModifiedProperties();
            Assert.That(template.StructuralAnchors.Count, Is.EqualTo(2));
            Assert.That(template.StructuralAnchors[0].Level, Is.EqualTo(0));
            Assert.That(template.StructuralAnchors[1].Level, Is.EqualTo(1));
            Assert.That(Paint(Vector2Int.one, 1, false), Is.False);
        }

        [Test] public void BoundaryOperationsAffectOnlyActiveLevel()
        {
            Paint(Vector2Int.zero, 1, true);
            Invoke("SetAllBoundaryEdges", true); Data.ApplyModifiedProperties();
            var groundCount = template.AllowedBoundaryEdges.Count;
            Invoke("ChangeLevel", 1);
            Invoke("SetAllBoundaryEdges", true); Data.ApplyModifiedProperties();
            Assert.That(template.AllowedBoundaryEdges.Count, Is.EqualTo(groundCount + 2));
            Invoke("SetAllBoundaryEdges", false); Data.ApplyModifiedProperties();
            Assert.That(template.AllowedBoundaryEdges.Count, Is.EqualTo(groundCount));
            Assert.That(template.AllowedBoundaryEdges.TrueForAll(edge => edge.Level == 0), Is.True);
        }

        [Test] public void ResizeShiftsSurfacesStairsAnchorsAndTerrainAndUndoRestoresEverything()
        {
            Paint(Vector2Int.one, 1, true); Stair(Surface(0, 0, 0), Surface(1, 1, 1));
            template.DifficultTerrainCells.Add(Vector2Int.right); Data.Update();
            var before = EditorJsonUtility.ToJson(template);
            Undo.IncrementCurrentGroup();
            Invoke("ResizeRoom", DungeonEdgeDirection.Left, true);
            Assert.That(template.SizeInCells.x, Is.EqualTo(4));
            Assert.That(template.ContainsSurface(new Vector2Int(2, 1), 1), Is.True);
            Assert.That(template.Stairs[0].First.LocalCell, Is.EqualTo(Vector2Int.right));
            Assert.That(template.Stairs[0].Second.LocalCell, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(template.DifficultTerrainCells[0], Is.EqualTo(new Vector2Int(2, 0)));
            Assert.DoesNotThrow(() => template.ValidateSurfaceGeometry());
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Data.Update();
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
        }

        [Test] public void ResizeCannotClipStairEndpoint()
        {
            Paint(Vector2Int.one, 1, true); Stair(Surface(0, 0, 0), Surface(1, 1, 1));
            var before = EditorJsonUtility.ToJson(template);
            Invoke("ResizeRoom", DungeonEdgeDirection.Left, false);
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
        }

        [Test] public void StairDraftRejectsSkippedCellAndCanTurnBeforeChangingLevel()
        {
            Paint(Vector2Int.one, 1, true);
            Invoke("PlaceStairEndpoint", Vector2Int.zero);
            Invoke("PlaceStairEndpoint", new Vector2Int(2, 0));
            Assert.That(((List<Vector2Int>)typeof(DungeonRoomEditorWindow).GetField("pendingStairPath", Flags).GetValue(window)).Count, Is.EqualTo(1));
            Invoke("PlaceStairEndpoint", Vector2Int.right);
            Invoke("ChangeLevel", 1); Invoke("PlaceStairEndpoint", Vector2Int.one);
            Assert.That(template.Stairs[0].PathCells, Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.one }));
        }

        [Test] public void EditingAndCancelingRoutePreservesOriginalUntilSave()
        {
            Paint(Vector2Int.one, 1, true); Stair(Surface(0, 0, 0), Surface(1, 1, 1));
            var before = EditorJsonUtility.ToJson(template);
            Set("selectedStair", 0); Invoke("BeginStairRouteEdit");
            Invoke("PlaceStairEndpoint", Vector2Int.up); Invoke("CancelStairDraft");
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
            Set("selectedStair", 0); Invoke("BeginStairRouteEdit");
            Invoke("PlaceStairEndpoint", Vector2Int.up); Invoke("ChangeLevel", 1); Invoke("PlaceStairEndpoint", Vector2Int.one);
            Assert.That(template.Stairs.Count, Is.EqualTo(1));
            Assert.That(template.Stairs[0].PathCells, Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.one }));
        }

        [Test] public void InteriorStairCellKeepsFootprintWithoutFloorBelowAndPreventsClipping()
        {
            Paint(Vector2Int.one, 1, true);
            var path = new[] { Vector2Int.right, Vector2Int.zero, Vector2Int.up, Vector2Int.one };
            Assert.That(Invoke("TrySaveStairRoute", new object[] { Surface(1, 0, 0), Surface(1, 1, 1), path, -1, null }), Is.True);
            Assert.That(Paint(Vector2Int.zero, 0, false), Is.True);
            Assert.That(template.ContainsSurface(Vector2Int.zero, 0), Is.False);
            Assert.That(template.Footprint.Contains(Vector2Int.zero, template.SizeInCells), Is.True);
            Assert.That(DungeonRoomTemplateEditorValidator.Validate(template).IsValid, Is.True);
            var before = EditorJsonUtility.ToJson(template);
            Invoke("ResizeRoom", DungeonEdgeDirection.Left, false);
            Assert.That(EditorJsonUtility.ToJson(template), Is.EqualTo(before));
        }

        [Test] public void GroundAnchorCannotBeSelectedOrCreatedOnUpperLevel()
        {
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("content", DungeonSemanticAnchorType.Obstacle, Vector2Int.one, Vector2Int.one)); Data.Update();
            Paint(Vector2Int.one, 1, true); Invoke("ChangeLevel", 1);
            Assert.That(Invoke("FindSemanticAnchorAt", Vector2Int.one, new Vector2Int(3, 3)), Is.EqualTo(-1));
            Invoke("SelectOrAddSemanticAnchor", Vector2Int.one, new Vector2Int(3, 3)); Data.ApplyModifiedProperties();
            Assert.That(template.SemanticAnchors.Count, Is.EqualTo(1));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator LayeredEditorRendersAtMinimumAndLargeSizes()
        {
            Paint(Vector2Int.one, 1, true); Stair(Surface(0, 0, 0), Surface(1, 1, 1));
            window.Show();
            foreach (var size in new[] { new Vector2(960, 560), new Vector2(1600, 900) })
            {
                window.position = new Rect(new Vector2(30, 30), size);
                Invoke("ChangeLevel", 1); Set("selectedStair", 0); window.Repaint();
                yield return null; yield return null;
                Invoke("ClearAnchorSelection"); Set("selectedSurface", (Godmist.Runtime.Dungeons.DungeonSurfacePosition?)Surface(1, 1, 1));
                window.Repaint(); yield return null;
                Assert.That(DungeonRoomTemplateEditorValidator.Validate(template).IsValid, Is.True);
            }
        }
    }
}
