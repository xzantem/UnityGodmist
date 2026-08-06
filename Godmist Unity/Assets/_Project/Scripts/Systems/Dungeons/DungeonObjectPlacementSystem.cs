using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonObjectPlacementSystem
    {
        private const int ObjectPlacementSeedSalt =
            unchecked((int)0x5F3759DF);

        private static readonly Vector2Int[] cardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public void Place(DungeonInstance dungeon)
        {
            if (dungeon == null || dungeon.Definition == null) return;

            var definitions = GetValidDefinitions(
                dungeon.Definition.ObjectDefinitions);

            if (definitions.Count == 0) return;

            var random = new System.Random(
                dungeon.Seed ^ ObjectPlacementSeedSalt);

            var lootTableSystem = new LootTableSystem(
                () => (float)random.NextDouble());

            foreach (var floor in dungeon.Floors)
            {
                PlaceObjectsOnFloor(
                    dungeon,
                    floor,
                    definitions,
                    lootTableSystem,
                    random);
            }
        }

        private static void PlaceObjectsOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions,
            LootTableSystem lootTableSystem,
            System.Random random)
        {
            if (floor == null || floor.Map == null || floor.Objects.Count > 0)
            {
                return;
            }

            var candidatePositions = GetCandidatePositions(floor);

            if (candidatePositions.Count == 0) return;

            candidatePositions.Sort(ComparePositions);
            Shuffle(candidatePositions, random);

            var maximumCount = Mathf.Min(
                dungeon.Definition.MaxObjectCountPerFloor,
                candidatePositions.Count);

            if (maximumCount <= 0) return;

            var minimumCount = Mathf.Min(
                dungeon.Definition.MinObjectCountPerFloor,
                maximumCount);

            var objectCount = minimumCount == maximumCount
                ? minimumCount
                : random.Next(minimumCount, maximumCount + 1);

            for (var index = 0; index < objectCount; index++)
            {
                var definition = SelectDefinition(definitions, random);

                if (definition == null) continue;

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}.object_{index:00}";

                var dungeonObject = CreateObjectInstance(
                    instanceId,
                    definition,
                    candidatePositions[index],
                    lootTableSystem);

                if (dungeonObject == null) continue;

                floor.TryAddObject(dungeonObject);
            }
        }

        private static List<DungeonObjectDefinition> GetValidDefinitions(
            List<DungeonObjectDefinition> objectDefinitions)
        {
            var result = new List<DungeonObjectDefinition>();

            if (objectDefinitions == null) return result;

            foreach (var definition in objectDefinitions)
            {
                if (definition == null
                    || string.IsNullOrWhiteSpace(definition.Id)
                    || definition.SpawnWeight <= 0)
                {
                    continue;
                }

                if (definition is ChestDungeonObjectDefinition chest
                    && chest.LootTable == null)
                {
                    continue;
                }

                result.Add(definition);
            }

            return result;
        }

        private static List<Vector2Int> GetCandidatePositions(
            DungeonFloorInstance floor)
        {
            var result = new List<Vector2Int>();
            var encounterRoomIds = new HashSet<string>();

            foreach (var encounter in floor.Encounters)
            {
                if (encounter != null)
                {
                    encounterRoomIds.Add(encounter.RoomId);
                }
            }

            foreach (var cell in floor.Map.FloorCells)
            {
                if (!floor.Map.TryGetRoomId(cell, out var roomId)) continue;
                if (encounterRoomIds.Contains(roomId)) continue;
                if (!floor.TryGetRoom(roomId, out var room)) continue;
                if (!IsEligibleRoomType(room.RoomType)) continue;
                if (IsPassageCell(floor.Map, cell)) continue;

                result.Add(cell);
            }

            return result;
        }

        private static bool IsEligibleRoomType(DungeonRoomType roomType)
        {
            return roomType == DungeonRoomType.Normal
                || roomType == DungeonRoomType.Optional;
        }

        private static bool IsPassageCell(
            DungeonFloorMap map,
            Vector2Int cell)
        {
            if (!map.TryGetRoomId(cell, out var roomId)) return false;

            foreach (var direction in cardinalDirections)
            {
                var neighbour = cell + direction;

                if (!map.TryGetRoomId(neighbour, out var neighbourRoomId)
                    || string.Equals(
                        roomId,
                        neighbourRoomId,
                        StringComparison.Ordinal)
                    || map.HasWallBetween(cell, neighbour))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static DungeonObjectDefinition SelectDefinition(
            List<DungeonObjectDefinition> definitions,
            System.Random random)
        {
            var totalWeight = 0;

            foreach (var definition in definitions)
            {
                totalWeight += definition.SpawnWeight;
            }

            if (totalWeight <= 0) return null;

            var selectedWeight = random.Next(totalWeight);

            foreach (var definition in definitions)
            {
                selectedWeight -= definition.SpawnWeight;

                if (selectedWeight < 0)
                {
                    return definition;
                }
            }

            return definitions[^1];
        }

        private static DungeonObjectInstance CreateObjectInstance(
            string instanceId,
            DungeonObjectDefinition definition,
            Vector2Int position,
            LootTableSystem lootTableSystem)
        {
            if (definition is ChestDungeonObjectDefinition chest)
            {
                var items = lootTableSystem.RollLoot(chest.LootTable);

                return new ChestDungeonObjectInstance(
                    instanceId,
                    chest,
                    position,
                    items);
            }

            if (definition is ObstacleDungeonObjectDefinition obstacle)
            {
                return new ObstacleDungeonObjectInstance(
                    instanceId,
                    obstacle,
                    position);
            }

            return null;
        }

        private static int ComparePositions(Vector2Int left, Vector2Int right)
        {
            var yComparison = left.y.CompareTo(right.y);

            return yComparison != 0 ? yComparison : left.x.CompareTo(right.x);
        }

        private static void Shuffle<T>(List<T> items, System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);

                (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
            }
        }
    }
}