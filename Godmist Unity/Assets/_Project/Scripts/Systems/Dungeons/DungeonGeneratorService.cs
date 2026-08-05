using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonGeneratorService
    {
        private const int RoomSpacing = 3;
        private const double CorridorChance = 0.4;

        public DungeonInstance Generate(
            DungeonDefinition definition,
            int seed)
        {
            ValidateDefinition(definition);

            var random = new System.Random(seed);
            var dungeon = new DungeonInstance(definition, seed);

            var floorCount = random.Next(
                definition.MinFloorCount,
                definition.MaxFloorCount + 1);

            for (var floorIndex = 0; floorIndex < floorCount; floorIndex++)
            {
                var floor = GenerateFloor(
                    definition,
                    floorIndex,
                    random);

                if (!dungeon.TryAddFloor(floor))
                {
                    throw new InvalidOperationException(
                        $"Nie udalo sie dodac pietra {floorIndex} do lochu.");
                }
            }

            return dungeon;
        }

        private static DungeonFloorInstance GenerateFloor(
            DungeonDefinition definition,
            int floorIndex,
            System.Random random)
        {
            var floor = new DungeonFloorInstance(floorIndex);

            var targetRoomCount = random.Next(
                definition.MinRoomCountPerFloor,
                definition.MaxRoomCountPerFloor + 1);

            var optionalRoomCount = GetOptionalRoomCount(
                targetRoomCount,
                random);

            var mainPathRoomCount = targetRoomCount - optionalRoomCount;
            var normalPathRooms = new List<DungeonRoomInstance>();

            DungeonRoomInstance previousRoom = null;
            var nextX = 0;

            for (var roomIndex = 0;
                 roomIndex < mainPathRoomCount;
                 roomIndex++)
            {
                var roomType = GetMainPathRoomType(
                    roomIndex,
                    mainPathRoomCount,
                    random);

                var template = SelectTemplate(
                    definition.RoomTemplateCatalog,
                    roomType,
                    random);

                var room = new DungeonRoomInstance(
                    $"floor.{floorIndex}.room.{roomIndex:00}",
                    roomType,
                    template.Id,
                    new Vector2Int(nextX, 0));

                AddRoomOrThrow(floor, room);

                if (previousRoom != null
                    && !floor.TryAddConnection(
                        previousRoom.RoomId,
                        room.RoomId))
                {
                    throw new InvalidOperationException(
                        "Nie udalo sie utworzyc polaczenia glownej trasy.");
                }

                if (roomType == DungeonRoomType.Normal)
                {
                    normalPathRooms.Add(room);
                }

                previousRoom = room;
                nextX += template.SizeInCells.x + RoomSpacing;
            }

            for (var optionalIndex = 0;
                 optionalIndex < optionalRoomCount;
                 optionalIndex++)
            {
                var template = SelectTemplate(
                    definition.RoomTemplateCatalog,
                    DungeonRoomType.Optional,
                    random);

                var anchorRoom = normalPathRooms[
                    random.Next(normalPathRooms.Count)];

                var room = new DungeonRoomInstance(
                    $"floor.{floorIndex}.optional.{optionalIndex:00}",
                    DungeonRoomType.Optional,
                    template.Id,
                    new Vector2Int(
                        anchorRoom.LayoutPosition.x,
                        -((optionalIndex + 1)
                          * (template.SizeInCells.y + RoomSpacing))));

                AddRoomOrThrow(floor, room);

                if (!floor.TryAddConnection(
                        anchorRoom.RoomId,
                        room.RoomId))
                {
                    throw new InvalidOperationException(
                        "Nie udalo sie utworzyc polaczenia pokoju opcjonalnego.");
                }
            }

            return floor;
        }

        private static int GetOptionalRoomCount(
            int targetRoomCount,
            System.Random random)
        {
            var maximumOptionalRooms = Math.Min(
                2,
                targetRoomCount - 3);

            return random.Next(0, maximumOptionalRooms + 1);
        }

        private static DungeonRoomType GetMainPathRoomType(
            int roomIndex,
            int mainPathRoomCount,
            System.Random random)
        {
            if (roomIndex == 0) return DungeonRoomType.Start;
            if (roomIndex == mainPathRoomCount - 1) return DungeonRoomType.Final;

            // Pierwszy pokoj wewnetrzny zawsze daje glowna trase
            // co najmniej Start -> Normal -> Final.
            if (roomIndex == 1) return DungeonRoomType.Normal;

            return random.NextDouble() < CorridorChance
                ? DungeonRoomType.Corridor
                : DungeonRoomType.Normal;
        }

        private static DungeonRoomTemplateDefinition SelectTemplate(
            DungeonRoomTemplateCatalog catalog,
            DungeonRoomType roomType,
            System.Random random)
        {
            var candidates = new List<DungeonRoomTemplateDefinition>();

            foreach (var template in catalog.Templates)
            {
                if (template == null
                    || template.RoomType != roomType
                    || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                candidates.Add(template);
            }

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Katalog '{catalog.Id}' nie ma szablonu typu {roomType}.");
            }

            return candidates[random.Next(candidates.Count)];
        }

        private static void AddRoomOrThrow(
            DungeonFloorInstance floor,
            DungeonRoomInstance room)
        {
            if (!floor.TryAddRoom(room))
            {
                throw new InvalidOperationException(
                    $"Nie udalo sie dodac pokoju '{room.RoomId}'.");
            }
        }

        private static void ValidateDefinition(
            DungeonDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.IsProcedural)
            {
                throw new InvalidOperationException(
                    "Generator wymaga proceduralnego DungeonDefinition.");
            }

            if (definition.RoomTemplateCatalog == null)
            {
                throw new InvalidOperationException(
                    "Proceduralny loch wymaga RoomTemplateCatalog.");
            }

            if (definition.MinFloorCount < 1
                || definition.MaxFloorCount < definition.MinFloorCount)
            {
                throw new InvalidOperationException(
                    "Zakres liczby pieter jest nieprawidlowy.");
            }

            if (definition.MinRoomCountPerFloor < 3
                || definition.MaxRoomCountPerFloor
                    < definition.MinRoomCountPerFloor)
            {
                throw new InvalidOperationException(
                    "Zakres liczby pokoi jest nieprawidlowy.");
            }
        }
    }
}