using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRuntimeRestoreTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void RestoreState_RestoresDungeonRoomAndObjectDeltas()
        {
            var dungeon = CreateDungeon(
                out var floor,
                out var room,
                out var door,
                out var obstacle);

            var result = dungeon.TryRestoreState(
                currentFloorIndex: 0,
                isCompleted: true,
                hasPlayerPosition: true,
                playerPosition: new Vector2(8.5f, 4.5f),
                restoredClearedEncounterIds: new[]
                {
                    "encounter.restore.test"
                });

            Assert.That(result, Is.True);
            Assert.That(dungeon.CurrentFloorIndex, Is.EqualTo(0));
            Assert.That(dungeon.IsCompleted, Is.True);
            Assert.That(dungeon.HasPlayerPosition, Is.True);
            Assert.That(
                dungeon.PlayerPosition,
                Is.EqualTo(new Vector2(8.5f, 4.5f)));

            Assert.That(
                dungeon.IsEncounterCleared("encounter.restore.test"),
                Is.True);

            Assert.That(
                floor.TryRestoreRoomVisited(room.RoomId, true),
                Is.True);

            Assert.That(room.IsVisited, Is.True);

            Assert.That(
                floor.TryRestoreObjectState(
                    door.InstanceId,
                    isActive: true,
                    isDoor: true,
                    isOpen: true),
                Is.True);

            Assert.That(door.IsOpen, Is.True);

            Assert.That(
                floor.Map.CanMoveBetween(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0)),
                Is.True);

            Assert.That(
                floor.TryRestoreObjectState(
                    obstacle.InstanceId,
                    isActive: false,
                    isDoor: false,
                    isOpen: false),
                Is.True);

            Assert.That(obstacle.IsActive, Is.False);
        }

        [Test]
        public void TryRestoreObjectState_RejectsIncorrectDoorMarker()
        {
            var dungeon = CreateDungeon(
                out var floor,
                out _,
                out var door,
                out _);

            var result = floor.TryRestoreObjectState(
                door.InstanceId,
                isActive: true,
                isDoor: false,
                isOpen: false);

            Assert.That(result, Is.False);
        }

        private DungeonInstance CreateDungeon(
            out DungeonFloorInstance floor,
            out DungeonRoomInstance room,
            out DoorDungeonObjectInstance door,
            out ObstacleDungeonObjectInstance obstacle)
        {
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();
            var doorDefinition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var obstacleDefinition = ScriptableObject.CreateInstance<
                ObstacleDungeonObjectDefinition>();

            createdObjects.Add(definition);
            createdObjects.Add(doorDefinition);
            createdObjects.Add(obstacleDefinition);

            SetAutoProperty(definition, "Id", "dungeon.restore.test");
            SetAutoProperty(doorDefinition, "Id", "door.restore.test");
            SetAutoProperty(
                obstacleDefinition,
                "Id",
                "obstacle.restore.test");

            var dungeon = new DungeonInstance(definition, 12345);
            floor = new DungeonFloorInstance(0);

            room = new DungeonRoomInstance(
                "room.restore",
                Godmist.Core.Enums.DungeonRoomType.Normal,
                "template.restore",
                Vector2Int.zero);

            Assert.That(floor.TryAddRoom(room), Is.True);

            var map = new DungeonFloorMap();

            Assert.That(
                map.TryAddFloorCell(
                    new Vector2Int(0, 0),
                    room.RoomId),
                Is.True);

            Assert.That(
                map.TryAddFloorCell(
                    new Vector2Int(1, 0),
                    room.RoomId),
                Is.True);

            Assert.That(
                map.TryAddFloorCell(
                    new Vector2Int(2, 0),
                    room.RoomId),
                Is.True);

            Assert.That(floor.TrySetMap(map), Is.True);

            door = new DoorDungeonObjectInstance(
                "door.restore",
                doorDefinition,
                new DungeonCellEdge(
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0)));

            obstacle = new ObstacleDungeonObjectInstance(
                "obstacle.restore",
                obstacleDefinition,
                new Vector2Int(2, 0));

            Assert.That(floor.TryAddObject(door), Is.True);
            Assert.That(floor.TryAddObject(obstacle), Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            return dungeon;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = FindBackingField(
                target.GetType(),
                propertyName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

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