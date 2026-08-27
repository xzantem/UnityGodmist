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

            SetAutoProperty(
                definition,
                "MinOptionalRoomCount",
                1);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                1);
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
        public void Generate_SeveralSeeds_DoesNotOverlapRoomAreas()
        {
            var definition = CreateDefinition(6, 9);

            SetAutoProperty(
                definition,
                "MinOptionalRoomCount",
                2);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                2);
            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 50; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                for (var firstIndex = 0;
                    firstIndex < floor.Rooms.Count;
                    firstIndex++)
                {
                    var firstBounds = GetRoomBounds(
                        floor.Rooms[firstIndex],
                        definition.RoomTemplateCatalog);

                    for (var secondIndex = firstIndex + 1;
                        secondIndex < floor.Rooms.Count;
                        secondIndex++)
                    {
                        var secondBounds = GetRoomBounds(
                            floor.Rooms[secondIndex],
                            definition.RoomTemplateCatalog);

                        Assert.That(
                            firstBounds.Overlaps(secondBounds),
                            Is.False,
                            $"Seed {seed}: pokoje '{floor.Rooms[firstIndex].RoomId}' "
                            + $"i '{floor.Rooms[secondIndex].RoomId}' nakladaja sie.");
                    }
                }
            }
        }

        [Test]
        public void Generate_CreatesWalkableMapForEveryRoomCell()
        {
            var definition = CreateDefinition(6, 6);
            var generator = new DungeonGeneratorService();

            var dungeon = generator.Generate(definition, 12345);
            var floor = dungeon.Floors[0];

            Assert.That(floor.Map, Is.Not.Null);

            foreach (var room in floor.Rooms)
            {
                var bounds = GetRoomBounds(
                    room,
                    definition.RoomTemplateCatalog);

                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (var y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        Assert.That(
                            floor.Map.IsWalkable(new Vector2Int(x, y)),
                            Is.True,
                            $"Komorka {x},{y} pokoju '{room.RoomId}' nie jest przechodnia.");
                    }
                }
            }
        }

        [Test]
        public void Generate_ConnectionsCreatePassableDoorways()
        {
            var definition = CreateDefinition(6, 9);
            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 50; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                foreach (var connection in floor.Connections)
                {
                    Assert.That(
                        floor.Map.CanMoveBetween(
                            connection.FirstDoorCell,
                            connection.SecondDoorCell),
                        Is.True,
                        $"Seed {seed}: drzwi polaczenia nie sa przechodnie.");
                }
            }
        }

        [Test]
        public void Generate_IrregularFootprints_CreateReachablePassages()
        {
            var definition = CreateDefinition(6, 9);

            foreach (var template in definition.RoomTemplateCatalog.Templates)
            {
                SetAutoProperty(
                    template,
                    "Footprint",
                    CreatePerimeterFootprint(template.SizeInCells));
            }

            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 50; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                foreach (var connection in floor.Connections)
                {
                    Assert.That(
                        floor.Map.CanMoveBetween(
                            connection.FirstDoorCell,
                            connection.SecondDoorCell),
                        Is.True,
                        $"Seed {seed}: footprint nie utworzyl przechodnich drzwi.");
                }

                Assert.That(
                    floor.Map.FloorCells.Count,
                    Is.LessThan(
                        GetTotalBoundingBoxCellCount(
                            floor,
                            definition.RoomTemplateCatalog)),
                    $"Seed {seed}: generator nie uzyl footprintu.");
            }
        }

        [Test]
        public void Generate_Connections_CreateAdjacentDoorways()
        {
            var definition = CreateDefinition(6, 9);
            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 50; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                foreach (var connection in floor.Connections)
                {
                    floor.TryGetRoom(
                        connection.FirstRoomId,
                        out var firstRoom);

                    floor.TryGetRoom(
                        connection.SecondRoomId,
                        out var secondRoom);

                    var firstRoomBounds = GetRoomBounds(
                        firstRoom,
                        definition.RoomTemplateCatalog);

                    var secondRoomBounds = GetRoomBounds(
                        secondRoom,
                        definition.RoomTemplateCatalog);

                    Assert.That(connection.HasGeometry, Is.True);

                    Assert.That(
                        firstRoomBounds.Contains(connection.FirstDoorCell),
                        Is.True);

                    Assert.That(
                        secondRoomBounds.Contains(connection.SecondDoorCell),
                        Is.True);

                    var cellDistance = Mathf.Abs(
                                        connection.FirstDoorCell.x
                                        - connection.SecondDoorCell.x)
                                    + Mathf.Abs(
                                        connection.FirstDoorCell.y
                                        - connection.SecondDoorCell.y);

                    Assert.That(
                        cellDistance,
                        Is.EqualTo(1),
                        $"Seed {seed}: drzwi polaczenia nie sa sasiednie.");
                }
            }
        }

        [Test]
        public void Generate_DoesNotReuseDoorwayCellOnSameRoomWall()
        {
            var definition = CreateDefinition(9, 9);

            SetAutoProperty(
                definition,
                "MinOptionalRoomCount",
                3);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                3);

            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 100; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var usedDoorways = new HashSet<string>();

                foreach (var connection in dungeon.Floors[0].Connections)
                {
                    Assert.That(
                        usedDoorways.Add(
                            BuildDoorwayKey(
                                connection.FirstRoomId,
                                connection.FirstDoorCell,
                                connection.SecondDoorCell)),
                        Is.True,
                        $"Seed {seed}: powtorzone drzwi pierwszego pokoju.");

                    Assert.That(
                        usedDoorways.Add(
                            BuildDoorwayKey(
                                connection.SecondRoomId,
                                connection.SecondDoorCell,
                                connection.FirstDoorCell)),
                        Is.True,
                        $"Seed {seed}: powtorzone drzwi drugiego pokoju.");
                }
            }
        }

        [Test]
        public void Generate_RejectsLoopConnectionsUntilLayoutPassV2()
        {
            var definition = CreateDefinition(6, 6);

            SetAutoProperty(
                definition,
                "MinLoopConnections",
                1);

            SetAutoProperty(
                definition,
                "MaxLoopConnections",
                1);

            var generator = new DungeonGeneratorService();

            Assert.That(
                () => generator.Generate(definition, 12345),
                Throws.TypeOf<System.InvalidOperationException>());
        }

        [Test]
        public void Generate_ConfiguredOptionalRoomRange_IsRespected()
        {
            var definition = CreateDefinition(6, 9);

            SetAutoProperty(
                definition,
                "MinOptionalRoomCount",
                1);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                2);

            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 50; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                var optionalRoomCount = 0;

                foreach (var room in floor.Rooms)
                {
                    if (room.RoomType == DungeonRoomType.Optional)
                    {
                        optionalRoomCount++;
                    }
                }

                Assert.That(optionalRoomCount, Is.InRange(1, 2));
            }
        }

        [Test]
        public void Generate_SeveralSeeds_CanAttachOptionalRoomToOptionalRoom()
        {
            var definition = CreateDefinition(9, 9);

            SetAutoProperty(
                definition,
                "MinOptionalRoomCount",
                2);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                2);

            var generator = new DungeonGeneratorService();

            for (var seed = 0; seed < 200; seed++)
            {
                var dungeon = generator.Generate(definition, seed);
                var floor = dungeon.Floors[0];

                foreach (var connection in floor.Connections)
                {
                    floor.TryGetRoom(
                        connection.FirstRoomId,
                        out var firstRoom);

                    floor.TryGetRoom(
                        connection.SecondRoomId,
                        out var secondRoom);

                    if (firstRoom.RoomType == DungeonRoomType.Optional
                        && secondRoom.RoomType == DungeonRoomType.Optional)
                    {
                        Assert.Pass(
                            $"Seed {seed} utworzyl branching pokoi opcjonalnych.");
                    }
                }
            }

            Assert.Fail(
                "Zaden z testowanych seedow nie utworzyl branching pokoi opcjonalnych.");
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

            var layoutProfile = ScriptableObject.CreateInstance<
                DungeonLayoutProfile>();

            SetAutoProperty(
                layoutProfile,
                "Id",
                "layout.test");

            SetAutoProperty(
                layoutProfile,
                "DoorPlacementStyle",
                DungeonDoorPlacementStyle.Centered);

            SetAutoProperty(
                layoutProfile,
                "MaxLateralOffsetCells",
                0);

            SetAutoProperty(
                layoutProfile,
                "MainPathTurnChance",
                0.5f);

            SetAutoProperty(
                layoutProfile,
                "CorridorRoomChance",
                0.4f);

            SetAutoProperty(
                layoutProfile,
                "MinDoorwayWidth",
                1);

            SetAutoProperty(
                layoutProfile,
                "MaxDoorwayWidth",
                1);

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
                "MinOptionalRoomCount",
                0);

            SetAutoProperty(
                definition,
                "MaxOptionalRoomCount",
                0);

            SetAutoProperty(
                definition,
                "MinLoopConnections",
                0);

            SetAutoProperty(
                definition,
                "MaxLoopConnections",
                0);

            SetAutoProperty(
                definition,
                "RoomTemplateCatalog",
                catalog);

            SetAutoProperty(
                definition,
                "LayoutProfile",
                layoutProfile);

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

        private static DungeonRoomFootprint CreatePerimeterFootprint(
            Vector2Int sizeInCells)
        {
            var offsets = new List<Vector2Int>();

            for (var x = 0; x < sizeInCells.x; x++)
            {
                for (var y = 0; y < sizeInCells.y; y++)
                {
                    var isInterior = x > 0
                                     && y > 0
                                     && x < sizeInCells.x - 1
                                     && y < sizeInCells.y - 1;

                    if (!isInterior)
                    {
                        offsets.Add(new Vector2Int(x, y));
                    }
                }
            }

            var footprint = new DungeonRoomFootprint();

            SetAutoProperty(
                footprint,
                "FloorCellOffsets",
                offsets);

            return footprint;
        }

        private static int GetTotalBoundingBoxCellCount(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog)
        {
            var total = 0;

            foreach (var room in floor.Rooms)
            {
                foreach (var template in catalog.Templates)
                {
                    if (template != null && template.Id == room.TemplateId)
                    {
                        total += template.SizeInCells.x * template.SizeInCells.y;
                        break;
                    }
                }
            }

            return total;
        }

        private static RectInt GetRoomBounds(
            DungeonRoomInstance room,
            DungeonRoomTemplateCatalog catalog)
        {
            foreach (var template in catalog.Templates)
            {
                if (template != null && template.Id == room.TemplateId)
                {
                    return new RectInt(
                        room.LayoutPosition,
                        template.SizeInCells);
                }
            }

            Assert.Fail(
                $"Nie znaleziono szablonu '{room.TemplateId}' "
                + $"dla pokoju '{room.RoomId}'.");

            return default;
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

        private static string BuildDoorwayKey(
            string roomId,
            Vector2Int doorwayCell,
            Vector2Int connectedDoorwayCell)
        {
            var direction = connectedDoorwayCell - doorwayCell;

            return $"{roomId}:{direction.x}:{direction.y}:"
                + $"{doorwayCell.x}:{doorwayCell.y}";
        }
    }
}
