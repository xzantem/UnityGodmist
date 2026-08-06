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
    public class DungeonEncounterPlacementSystemTests
    {
        [Test]
        public void Place_UsesOnlyNormalOptionalAndCorridorRooms()
        {
            var definition = CreateDungeonDefinition(
                minEncounterCount: 3,
                maxEncounterCount: 3,
                allowRepeats: true);

            var dungeon = CreateDungeonWithAllRoomTypes(definition);
            var placementSystem = new DungeonEncounterPlacementSystem();

            placementSystem.Place(dungeon);

            var floor = dungeon.Floors[0];

            Assert.That(floor.Encounters, Has.Count.EqualTo(3));

            foreach (var encounter in floor.Encounters)
            {
                floor.TryGetRoom(encounter.RoomId, out var room);

                Assert.That(
                    room.RoomType == DungeonRoomType.Normal
                    || room.RoomType == DungeonRoomType.Optional
                    || room.RoomType == DungeonRoomType.Corridor,
                    Is.True,
                    $"Encounter trafil do niedozwolonego pokoju: {room.RoomType}.");
            }
        }

        [Test]
        public void Place_AllowsRepeatedDefinitionsButKeepsUniqueInstanceIds()
        {
            var definition = CreateDungeonDefinition(
                minEncounterCount: 3,
                maxEncounterCount: 3,
                allowRepeats: true);

            var dungeon = CreateDungeonWithAllRoomTypes(definition);
            var placementSystem = new DungeonEncounterPlacementSystem();

            placementSystem.Place(dungeon);

            var floor = dungeon.Floors[0];
            var instanceIds = new HashSet<string>();

            Assert.That(floor.Encounters, Has.Count.EqualTo(3));

            foreach (var encounter in floor.Encounters)
            {
                Assert.That(
                    encounter.Definition.Id,
                    Is.EqualTo("encounter.test"));

                Assert.That(
                    instanceIds.Add(encounter.InstanceId),
                    Is.True,
                    $"Powtorzone InstanceId: {encounter.InstanceId}");
            }
        }

        [Test]
        public void Place_SameSeedCreatesSameEncounterPlacement()
        {
            var firstDefinition = CreateDungeonDefinition(
                minEncounterCount: 2,
                maxEncounterCount: 2,
                allowRepeats: true);

            var secondDefinition = CreateDungeonDefinition(
                minEncounterCount: 2,
                maxEncounterCount: 2,
                allowRepeats: true);

            var firstDungeon = CreateDungeonWithAllRoomTypes(
                firstDefinition,
                seed: 12345);

            var secondDungeon = CreateDungeonWithAllRoomTypes(
                secondDefinition,
                seed: 12345);

            var placementSystem = new DungeonEncounterPlacementSystem();

            placementSystem.Place(firstDungeon);
            placementSystem.Place(secondDungeon);

            Assert.That(
                BuildEncounterSignature(firstDungeon.Floors[0]),
                Is.EqualTo(BuildEncounterSignature(secondDungeon.Floors[0])));
        }

        [Test]
        public void Place_DoesNotAddEncountersAgainWhenCalledTwice()
        {
            var definition = CreateDungeonDefinition(
                minEncounterCount: 2,
                maxEncounterCount: 2,
                allowRepeats: true);

            var dungeon = CreateDungeonWithAllRoomTypes(definition);
            var placementSystem = new DungeonEncounterPlacementSystem();

            placementSystem.Place(dungeon);

            var firstCount = dungeon.Floors[0].Encounters.Count;

            placementSystem.Place(dungeon);

            Assert.That(
                dungeon.Floors[0].Encounters,
                Has.Count.EqualTo(firstCount));
        }

        private static DungeonDefinition CreateDungeonDefinition(
            int minEncounterCount,
            int maxEncounterCount,
            bool allowRepeats)
        {
            var catalog = ScriptableObject.CreateInstance<
                DungeonRoomTemplateCatalog>();

            SetAutoProperty(catalog, "Id", "catalog.test");

            SetAutoProperty(
                catalog,
                "Templates",
                new List<DungeonRoomTemplateDefinition>
                {
                    CreateRoomTemplate(
                        "room.start",
                        DungeonRoomType.Start),

                    CreateRoomTemplate(
                        "room.normal",
                        DungeonRoomType.Normal),

                    CreateRoomTemplate(
                        "room.optional",
                        DungeonRoomType.Optional),

                    CreateRoomTemplate(
                        "room.corridor",
                        DungeonRoomType.Corridor),

                    CreateRoomTemplate(
                        "room.final",
                        DungeonRoomType.Final)
                });

            var encounter = ScriptableObject.CreateInstance<
                EncounterDefinition>();

            SetAutoProperty(encounter, "Id", "encounter.test");

            SetAutoProperty(
                encounter,
                "Enemies",
                new List<EncounterEnemyData>
                {
                    new()
                });

            var definition = ScriptableObject.CreateInstance<
                DungeonDefinition>();

            SetAutoProperty(definition, "Id", "dungeon.test");
            SetAutoProperty(definition, "RoomTemplateCatalog", catalog);

            definition.EncounterDefinitions.Add(encounter);

            SetAutoProperty(
                definition,
                "MinEncounterCountPerFloor",
                minEncounterCount);

            SetAutoProperty(
                definition,
                "MaxEncounterCountPerFloor",
                maxEncounterCount);

            SetAutoProperty(
                definition,
                "AllowEncounterDefinitionRepeats",
                allowRepeats);

            return definition;
        }

        private static DungeonInstance CreateDungeonWithAllRoomTypes(
            DungeonDefinition definition,
            int seed = 12345)
        {
            var dungeon = new DungeonInstance(definition, seed);
            var floor = new DungeonFloorInstance(0);

            floor.TryAddRoom(new DungeonRoomInstance(
                "start",
                DungeonRoomType.Start,
                "room.start",
                new Vector2Int(0, 0)));

            floor.TryAddRoom(new DungeonRoomInstance(
                "normal",
                DungeonRoomType.Normal,
                "room.normal",
                new Vector2Int(8, 0)));

            floor.TryAddRoom(new DungeonRoomInstance(
                "optional",
                DungeonRoomType.Optional,
                "room.optional",
                new Vector2Int(16, 0)));

            floor.TryAddRoom(new DungeonRoomInstance(
                "corridor",
                DungeonRoomType.Corridor,
                "room.corridor",
                new Vector2Int(24, 0)));

            floor.TryAddRoom(new DungeonRoomInstance(
                "final",
                DungeonRoomType.Final,
                "room.final",
                new Vector2Int(32, 0)));

            dungeon.TryAddFloor(floor);

            return dungeon;
        }

        private static DungeonRoomTemplateDefinition CreateRoomTemplate(
            string id,
            DungeonRoomType roomType)
        {
            var template = ScriptableObject.CreateInstance<
                DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", id);
            SetAutoProperty(template, "RoomType", roomType);

            SetAutoProperty(
                template,
                "SizeInCells",
                new Vector2Int(8, 8));

            return template;
        }

        private static string BuildEncounterSignature(
            DungeonFloorInstance floor)
        {
            var result = string.Empty;

            foreach (var encounter in floor.Encounters)
            {
                result += $"{encounter.InstanceId}|";
            }

            return result;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}