using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonGeneratorServiceTests
    {
        [Test]
        public void Generate_SameSeed_CreatesSameDungeonLayout()
        {
            var definition = CreateDefinition(6, 9);
            var generator = new DungeonGeneratorService();

            var firstDungeon = generator.Generate(definition, 12345);
            var secondDungeon = generator.Generate(definition, 12345);

            Assert.That(
                BuildSignature(firstDungeon),
                Is.EqualTo(BuildSignature(secondDungeon)));
        }

        [Test]
        public void Generate_CreatesReachableStartAndFinal()
        {
            var definition = CreateDefinition(6, 9);
            var generator = new DungeonGeneratorService();

            var dungeon = generator.Generate(definition, 4567);

            Assert.That(dungeon.Floors, Has.Count.EqualTo(1));

            var floor = dungeon.Floors[0];

            Assert.That(
                floor.Rooms.Count,
                Is.InRange(
                    definition.MinRoomCountPerFloor,
                    definition.MaxRoomCountPerFloor));

            var startRoom = FindRoom(floor, DungeonRoomType.Start);
            var finalRoom = FindRoom(floor, DungeonRoomType.Final);

            Assert.That(startRoom, Is.Not.Null);
            Assert.That(finalRoom, Is.Not.Null);
            Assert.That(
                CanReach(floor, startRoom.RoomId, finalRoom.RoomId),
                Is.True);

            foreach (var room in floor.Rooms)
            {
                Assert.That(room.TemplateId, Is.Not.Empty);
            }
        }

        [Test]
        public void Generate_OptionalRoomsHaveOneConnection()
        {
            var definition = CreateDefinition(9, 9);
            var generator = new DungeonGeneratorService();

            var dungeon = generator.Generate(definition, 9876);
            var floor = dungeon.Floors[0];

            foreach (var room in floor.Rooms)
            {
                if (room.RoomType != DungeonRoomType.Optional) continue;

                var connectionCount = 0;

                foreach (var connection in floor.Connections)
                {
                    if (connection.FirstRoomId == room.RoomId
                        || connection.SecondRoomId == room.RoomId)
                    {
                        connectionCount++;
                    }
                }

                Assert.That(connectionCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void Generate_RejectsNonProceduralDefinition()
        {
            var definition = CreateDefinition(6, 6);
            SetAutoProperty(definition, "IsProcedural", false);

            var generator = new DungeonGeneratorService();

            Assert.That(
                () => generator.Generate(definition, 1),
                Throws.TypeOf<System.InvalidOperationException>());
        }

        private static DungeonDefinition CreateDefinition(
            int minRoomCount,
            int maxRoomCount)
        {
            var catalog = ScriptableObject.CreateInstance<
                DungeonRoomTemplateCatalog>();

            SetAutoProperty(
                catalog,
                "Id",
                "dungeon.test.templates");

            SetAutoProperty(
                catalog,
                "Templates",
                new List<DungeonRoomTemplateDefinition>
                {
                    CreateTemplate(
                        "room.start",
                        DungeonRoomType.Start,
                        new Vector2Int(8, 8)),
                    CreateTemplate(
                        "room.normal.small",
                        DungeonRoomType.Normal,
                        new Vector2Int(8, 8)),
                    CreateTemplate(
                        "room.normal.large",
                        DungeonRoomType.Normal,
                        new Vector2Int(12, 8)),
                    CreateTemplate(
                        "room.corridor",
                        DungeonRoomType.Corridor,
                        new Vector2Int(6, 2)),
                    CreateTemplate(
                        "room.optional",
                        DungeonRoomType.Optional,
                        new Vector2Int(8, 8)),
                    CreateTemplate(
                        "room.final",
                        DungeonRoomType.Final,
                        new Vector2Int(10, 10))
                });

            var definition = ScriptableObject.CreateInstance<
                DungeonDefinition>();

            SetAutoProperty(
                definition,
                "Id",
                "dungeon.test.procedural");

            SetAutoProperty(
                definition,
                "IsProcedural",
                true);

            SetAutoProperty(
                definition,
                "MinFloorCount",
                1);

            SetAutoProperty(
                definition,
                "MaxFloorCount",
                1);

            SetAutoProperty(
                definition,
                "MinRoomCountPerFloor",
                minRoomCount);

            SetAutoProperty(
                definition,
                "MaxRoomCountPerFloor",
                maxRoomCount);

            SetAutoProperty(
                definition,
                "RoomTemplateCatalog",
                catalog);

            return definition;
        }

        private static DungeonRoomTemplateDefinition CreateTemplate(
            string id,
            DungeonRoomType roomType,
            Vector2Int sizeInCells)
        {
            var template = ScriptableObject.CreateInstance<
                DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", id);
            SetAutoProperty(template, "RoomType", roomType);
            SetAutoProperty(template, "SizeInCells", sizeInCells);

            return template;
        }

        private static DungeonRoomInstance FindRoom(
            DungeonFloorInstance floor,
            DungeonRoomType roomType)
        {
            foreach (var room in floor.Rooms)
            {
                if (room.RoomType == roomType) return room;
            }

            return null;
        }

        private static bool CanReach(
            DungeonFloorInstance floor,
            string startRoomId,
            string targetRoomId)
        {
            var visited = new HashSet<string> { startRoomId };
            var queue = new Queue<string>();

            queue.Enqueue(startRoomId);

            while (queue.Count > 0)
            {
                var roomId = queue.Dequeue();

                if (roomId == targetRoomId) return true;

                foreach (var connection in floor.Connections)
                {
                    var connectedRoomId = GetConnectedRoomId(
                        connection,
                        roomId);

                    if (connectedRoomId == null
                        || !visited.Add(connectedRoomId))
                    {
                        continue;
                    }

                    queue.Enqueue(connectedRoomId);
                }
            }

            return false;
        }

        private static string GetConnectedRoomId(
            DungeonRoomConnection connection,
            string roomId)
        {
            if (connection.FirstRoomId == roomId)
            {
                return connection.SecondRoomId;
            }

            if (connection.SecondRoomId == roomId)
            {
                return connection.FirstRoomId;
            }

            return null;
        }

        private static string BuildSignature(DungeonInstance dungeon)
        {
            var builder = new StringBuilder();

            foreach (var floor in dungeon.Floors)
            {
                builder.Append($"floor:{floor.FloorIndex}|");

                foreach (var room in floor.Rooms)
                {
                    builder.Append(
                        $"{room.RoomId}:{room.RoomType}:{room.TemplateId}:"
                        + $"{room.LayoutPosition.x},{room.LayoutPosition.y}|");
                }

                foreach (var connection in floor.Connections)
                {
                    builder.Append(
                        $"{connection.FirstRoomId}>"
                        + $"{connection.SecondRoomId}|");
                }
            }

            return builder.ToString();
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