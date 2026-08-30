using System;
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
    public class DungeonSemanticAnchorTests
    {
        [Test]
        public void Select_RespectsConditionalAnchorChain()
        {
            var catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            var template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();

            try
            {
                SetAutoProperty(catalog, "Id", "catalog.test");
                SetAutoProperty(template, "Id", "template.normal");
                SetAutoProperty(template, "RoomType", DungeonRoomType.Normal);
                SetAutoProperty(template, "SizeInCells", new Vector2Int(4, 4));

                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "trap.a",
                        DungeonSemanticAnchorType.Trap,
                        new Vector2Int(0, 0),
                        Vector2Int.one,
                        spawnChance: 1f));
                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "trap.b",
                        DungeonSemanticAnchorType.Trap,
                        new Vector2Int(1, 0),
                        Vector2Int.one,
                        spawnChance: 1f,
                        requiresAnchorId: "trap.a"));
                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "trap.c",
                        DungeonSemanticAnchorType.Trap,
                        new Vector2Int(2, 0),
                        Vector2Int.one,
                        spawnChance: 1f,
                        requiresAnchorId: "trap.b"));

                catalog.Templates.Add(template);
                var floor = CreateFloor(template, "room.normal");

                var resolver = new DungeonSemanticAnchorResolver();
                var candidates = resolver.GetCandidates(
                    floor,
                    catalog,
                    new[] { DungeonSemanticAnchorType.Trap },
                    roomType => roomType == DungeonRoomType.Normal);

                var selected = resolver.Select(
                    candidates,
                    minimumCount: 0,
                    maximumCount: 3,
                    new System.Random(123));

                var selectedIds = new HashSet<string>();

                foreach (var placement in selected)
                {
                    selectedIds.Add(placement.Anchor.Id);
                }

                Assert.That(selectedIds, Does.Contain("trap.a"));
                Assert.That(selectedIds, Does.Contain("trap.b"));
                Assert.That(selectedIds, Does.Contain("trap.c"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void Select_DoesNotSpawnConditionalChainWhenRootRollFails()
        {
            var catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            var template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();

            try
            {
                SetAutoProperty(catalog, "Id", "catalog.test");
                SetAutoProperty(template, "Id", "template.normal");
                SetAutoProperty(template, "RoomType", DungeonRoomType.Normal);
                SetAutoProperty(template, "SizeInCells", new Vector2Int(4, 4));

                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "trap.root",
                        DungeonSemanticAnchorType.Trap,
                        Vector2Int.zero,
                        Vector2Int.one,
                        spawnChance: 0f));
                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "trap.child",
                        DungeonSemanticAnchorType.Trap,
                        new Vector2Int(1, 0),
                        Vector2Int.one,
                        spawnChance: 1f,
                        requiresAnchorId: "trap.root"));

                catalog.Templates.Add(template);
                var floor = CreateFloor(template, "room.normal");
                var resolver = new DungeonSemanticAnchorResolver();
                var candidates = resolver.GetCandidates(
                    floor,
                    catalog,
                    new[] { DungeonSemanticAnchorType.Trap },
                    roomType => roomType == DungeonRoomType.Normal);

                var selected = resolver.Select(
                    candidates,
                    minimumCount: 0,
                    maximumCount: 2,
                    new System.Random(123));

                Assert.That(selected, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void ObjectPlacement_UsesAuthoredAnchorCell()
        {
            var catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            var template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
            var obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var dungeonDefinition = ScriptableObject.CreateInstance<DungeonDefinition>();

            try
            {
                SetAutoProperty(catalog, "Id", "catalog.test");
                SetAutoProperty(template, "Id", "template.normal");
                SetAutoProperty(template, "RoomType", DungeonRoomType.Normal);
                SetAutoProperty(template, "SizeInCells", new Vector2Int(5, 4));
                template.SemanticAnchors.Add(
                    new DungeonSemanticAnchorData(
                        "obstacle.choke",
                        DungeonSemanticAnchorType.Obstacle,
                        new Vector2Int(4, 3),
                        Vector2Int.one,
                        isRequired: true));
                catalog.Templates.Add(template);

                SetAutoProperty(obstacleDefinition, "Id", "obstacle.test");
                SetAutoProperty(dungeonDefinition, "Id", "dungeon.test");
                SetAutoProperty(dungeonDefinition, "RoomTemplateCatalog", catalog);
                dungeonDefinition.ObjectDefinitions.Add(obstacleDefinition);
                SetAutoProperty(dungeonDefinition, "MinObjectCountPerFloor", 1);
                SetAutoProperty(dungeonDefinition, "MaxObjectCountPerFloor", 1);

                var dungeon = new DungeonInstance(dungeonDefinition, 123);
                var floor = CreateFloor(template, "room.normal");
                Assert.That(dungeon.TryAddFloor(floor), Is.True);

                new DungeonObjectPlacementSystem().Place(dungeon);

                Assert.That(floor.Objects, Has.Count.EqualTo(1));
                Assert.That(
                    floor.Objects[0].Position,
                    Is.EqualTo(new Vector2Int(4, 3)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(obstacleDefinition);
                UnityEngine.Object.DestroyImmediate(dungeonDefinition);
            }
        }

        private static DungeonFloorInstance CreateFloor(
            DungeonRoomTemplateDefinition template,
            string roomId)
        {
            var floor = new DungeonFloorInstance(0);
            Assert.That(
                floor.TryAddRoom(
                    new DungeonRoomInstance(
                        roomId,
                        DungeonRoomType.Normal,
                        template.Id,
                        Vector2Int.zero)),
                Is.True);

            var map = new DungeonFloorMap();

            for (var x = 0; x < template.SizeInCells.x; x++)
            {
                for (var y = 0; y < template.SizeInCells.y; y++)
                {
                    Assert.That(
                        map.TryAddFloorCell(new Vector2Int(x, y), roomId),
                        Is.True);
                }
            }

            Assert.That(floor.TrySetMap(map), Is.True);
            return floor;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            FieldInfo field = null;

            for (var currentType = target.GetType();
                currentType != null && field == null;
                currentType = currentType.BaseType)
            {
                field = currentType.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}
