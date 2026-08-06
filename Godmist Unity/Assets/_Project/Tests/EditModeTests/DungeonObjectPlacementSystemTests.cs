using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonObjectPlacementSystemTests
    {
        [Test]
        public void Place_UsesOnlyEligibleCellsOutsideEncounterRooms()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 2,
                maxObjectCount: 2);

            var dungeon = CreateDungeon(
                definition,
                seed: 12345,
                includeEncounterInNormalRoom: true);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(dungeon);

            var floor = dungeon.Floors[0];

            Assert.That(floor.Objects, Has.Count.EqualTo(2));

            foreach (var dungeonObject in floor.Objects)
            {
                Assert.That(
                    floor.Map.TryGetRoomId(
                        dungeonObject.Position,
                        out var roomId),
                    Is.True);

                Assert.That(roomId, Is.EqualTo("optional"));
                Assert.That(dungeonObject, Is.TypeOf<ObstacleDungeonObjectInstance>());
            }
        }

        [Test]
        public void Place_SameSeedCreatesSameObjectPlacement()
        {
            var firstDefinition = CreateDungeonDefinition(
                minObjectCount: 3,
                maxObjectCount: 3);

            var secondDefinition = CreateDungeonDefinition(
                minObjectCount: 3,
                maxObjectCount: 3);

            var firstDungeon = CreateDungeon(
                firstDefinition,
                seed: 54321,
                includeEncounterInNormalRoom: false);

            var secondDungeon = CreateDungeon(
                secondDefinition,
                seed: 54321,
                includeEncounterInNormalRoom: false);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(firstDungeon);
            placementSystem.Place(secondDungeon);

            Assert.That(
                BuildObjectSignature(firstDungeon.Floors[0]),
                Is.EqualTo(BuildObjectSignature(secondDungeon.Floors[0])));
        }

        [Test]
        public void Place_DoesNotAddObjectsAgainWhenCalledTwice()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 2,
                maxObjectCount: 2);

            var dungeon = CreateDungeon(
                definition,
                seed: 12345,
                includeEncounterInNormalRoom: false);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(dungeon);

            var firstObjectCount = dungeon.Floors[0].Objects.Count;

            placementSystem.Place(dungeon);

            Assert.That(
                dungeon.Floors[0].Objects,
                Has.Count.EqualTo(firstObjectCount));
        }

        private static DungeonDefinition CreateDungeonDefinition(
            int minObjectCount,
            int maxObjectCount)
        {
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();

            SetAutoProperty(definition, "Id", "dungeon.test");
            SetAutoProperty(
                definition,
                "MinObjectCountPerFloor",
                minObjectCount);

            SetAutoProperty(
                definition,
                "MaxObjectCountPerFloor",
                maxObjectCount);

            var obstacle = ScriptableObject.CreateInstance<
                ObstacleDungeonObjectDefinition>();

            SetAutoProperty(obstacle, "Id", "obstacle.test");

            definition.ObjectDefinitions.Add(obstacle);

            return definition;
        }

        private static DungeonInstance CreateDungeon(
            DungeonDefinition definition,
            int seed,
            bool includeEncounterInNormalRoom)
        {
            var dungeon = new DungeonInstance(definition, seed);
            var floor = new DungeonFloorInstance(0);

            AddRoom(
                floor,
                "start",
                DungeonRoomType.Start,
                new RectInt(0, 0, 2, 2));

            AddRoom(
                floor,
                "normal",
                DungeonRoomType.Normal,
                new RectInt(4, 0, 2, 2));

            AddRoom(
                floor,
                "optional",
                DungeonRoomType.Optional,
                new RectInt(8, 0, 3, 2));

            AddRoom(
                floor,
                "final",
                DungeonRoomType.Final,
                new RectInt(13, 0, 2, 2));

            var map = new DungeonFloorMap();

            AddMapCells(map, "start", new RectInt(0, 0, 2, 2));
            AddMapCells(map, "normal", new RectInt(4, 0, 2, 2));
            AddMapCells(map, "optional", new RectInt(8, 0, 3, 2));
            AddMapCells(map, "final", new RectInt(13, 0, 2, 2));

            Assert.That(floor.TrySetMap(map), Is.True);

            if (includeEncounterInNormalRoom)
            {
                var encounterDefinition =
                    ScriptableObject.CreateInstance<EncounterDefinition>();

                SetAutoProperty(
                    encounterDefinition,
                    "Id",
                    "encounter.test");

                var encounter = new DungeonEncounterInstance(
                    "encounter.test",
                    encounterDefinition,
                    "normal",
                    new RectInt(4, 0, 2, 2));

                Assert.That(floor.TryAddEncounter(encounter), Is.True);
            }

            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            return dungeon;
        }

        private static void AddRoom(
            DungeonFloorInstance floor,
            string roomId,
            DungeonRoomType roomType,
            RectInt area)
        {
            var room = new DungeonRoomInstance(
                roomId,
                roomType,
                $"template.{roomType}",
                area.position);

            Assert.That(floor.TryAddRoom(room), Is.True);
        }

        private static void AddMapCells(
            DungeonFloorMap map,
            string roomId,
            RectInt area)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    Assert.That(
                        map.TryAddFloorCell(
                            new Vector2Int(x, y),
                            roomId),
                        Is.True);
                }
            }
        }

        private static string BuildObjectSignature(
            DungeonFloorInstance floor)
        {
            var result = string.Empty;

            foreach (var dungeonObject in floor.Objects)
            {
                result +=
                    $"{dungeonObject.InstanceId}|"
                    + $"{dungeonObject.Definition.Id}|"
                    + $"{dungeonObject.Position.x},{dungeonObject.Position.y};";
            }

            return result;
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