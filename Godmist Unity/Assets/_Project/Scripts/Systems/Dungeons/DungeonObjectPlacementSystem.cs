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

            var objectDefinitions = GetValidObjectDefinitions(
                dungeon.Definition.ObjectDefinitions);

            var doorDefinitions = GetValidDoorDefinitions(
                dungeon.Definition.ObjectDefinitions);

            if (objectDefinitions.Count == 0
                && doorDefinitions.Count == 0)
            {
                return;
            }

            var random = new System.Random(
                dungeon.Seed ^ ObjectPlacementSeedSalt);

            var lootTableSystem = new LootTableSystem(
                () => (float)random.NextDouble());

            foreach (var floor in dungeon.Floors)
            {
                if (floor == null
                    || floor.Map == null
                    || floor.Objects.Count > 0)
                {
                    continue;
                }

                PlaceDoorsOnFloor(
                    dungeon,
                    floor,
                    doorDefinitions,
                    random);

                PlaceObjectsOnFloor(
                    dungeon,
                    floor,
                    objectDefinitions,
                    lootTableSystem,
                    random);
            }
        }

        private static void PlaceDoorsOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DoorDungeonObjectDefinition> definitions,
            System.Random random)
        {
            if (definitions.Count == 0) return;

            var candidates = GetDoorAnchorCandidates(
                floor,
                dungeon.Definition.RoomTemplateCatalog);

            candidates.Sort(CompareDoorCandidates);
            Shuffle(candidates, random);

            for (var index = 0; index < candidates.Count; index++)
            {
                var definition = SelectDefinition(definitions, random);

                if (definition == null) continue;

                var candidate = candidates[index];

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}.door_{index:00}";

                var door = new DoorDungeonObjectInstance(
                    instanceId,
                    definition,
                    candidate.Edge);

                if (!floor.TryAddObject(door))
                {
                    throw new InvalidOperationException(
                        $"Nie udalo sie dodac drzwi '{instanceId}' do pietra "
                        + $"{floor.FloorIndex}.");
                }
            }
        }

        private static List<DoorAnchorCandidate> GetDoorAnchorCandidates(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog)
        {
            var result = new List<DoorAnchorCandidate>();

            if (catalog == null) return result;

            var templatesById = BuildTemplateLookup(catalog);
            var registeredEdges = new HashSet<DungeonCellEdge>();

            foreach (var room in floor.Rooms)
            {
                if (!templatesById.TryGetValue(
                        room.TemplateId,
                        out var template))
                {
                    continue;
                }

                foreach (var anchor in template.StructuralAnchors)
                {
                    if (anchor == null
                        || anchor.Type != DungeonStructuralAnchorType.Door)
                    {
                        continue;
                    }

                    var firstCell = room.LayoutPosition + anchor.LocalCell;
                    var secondCell = firstCell + anchor.GetDirectionOffset();

                    if (!floor.Map.IsWalkable(firstCell)
                        || !floor.Map.IsWalkable(secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "wskazuje pole poza pokojem.");
                    }

                    if (!floor.Map.TryGetRoomId(firstCell, out var firstRoomId)
                        || !floor.Map.TryGetRoomId(secondCell, out var secondRoomId)
                        || firstRoomId != room.RoomId
                        || secondRoomId != room.RoomId)
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "nie moze wychodzic poza granice pokoju.");
                    }

                    var edge = new DungeonCellEdge(firstCell, secondCell);

                    if (floor.Map.HasWallBetween(firstCell, secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "naklada sie na statyczna sciane.");
                    }

                    if (!floor.Map.CanMoveBetween(firstCell, secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "nie wskazuje dostepnej krawedzi.");
                    }

                    if (!registeredEdges.Add(edge))
                    {
                        throw new InvalidOperationException(
                            $"Powtorzona kotwica drzwi w szablonie "
                            + $"'{template.Id}'.");
                    }

                    result.Add(new DoorAnchorCandidate(edge));
                }
            }

            return result;
        }

        private static void PlaceObjectsOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions,
            LootTableSystem lootTableSystem,
            System.Random random)
        {
            if (definitions.Count == 0) return;

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

        private static List<DungeonObjectDefinition> GetValidObjectDefinitions(
            List<DungeonObjectDefinition> objectDefinitions)
        {
            var result = new List<DungeonObjectDefinition>();

            if (objectDefinitions == null) return result;

            foreach (var definition in objectDefinitions)
            {
                if (definition == null
                    || definition is DoorDungeonObjectDefinition
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

        private static List<DoorDungeonObjectDefinition> GetValidDoorDefinitions(
            List<DungeonObjectDefinition> objectDefinitions)
        {
            var result = new List<DoorDungeonObjectDefinition>();

            if (objectDefinitions == null) return result;

            foreach (var definition in objectDefinitions)
            {
                if (definition is not DoorDungeonObjectDefinition door
                    || string.IsNullOrWhiteSpace(door.Id)
                    || door.SpawnWeight <= 0)
                {
                    continue;
                }

                result.Add(door);
            }

            return result;
        }

        private static Dictionary<string, DungeonRoomTemplateDefinition>
            BuildTemplateLookup(DungeonRoomTemplateCatalog catalog)
        {
            var result =
                new Dictionary<string, DungeonRoomTemplateDefinition>();

            foreach (var template in catalog.Templates)
            {
                if (template == null
                    || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                if (!result.TryAdd(template.Id, template))
                {
                    throw new InvalidOperationException(
                        $"Powtorzony RoomTemplateId '{template.Id}'.");
                }
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
                if (IsOccupiedByDungeonObject(floor, cell)) continue;

                result.Add(cell);
            }

            return result;
        }

        private static bool IsEligibleRoomType(DungeonRoomType roomType)
        {
            return roomType == DungeonRoomType.Normal
                || roomType == DungeonRoomType.Optional;
        }

        private static bool IsOccupiedByDungeonObject(
            DungeonFloorInstance floor,
            Vector2Int cell)
        {
            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject is DoorDungeonObjectInstance door)
                {
                    if (door.Edge.FirstCell == cell
                        || door.Edge.SecondCell == cell)
                    {
                        return true;
                    }

                    continue;
                }

                if (dungeonObject != null
                    && dungeonObject.Position == cell)
                {
                    return true;
                }
            }

            return false;
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

        private static T SelectDefinition<T>(
            List<T> definitions,
            System.Random random)
            where T : DungeonObjectDefinition
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

        private static int ComparePositions(
            Vector2Int left,
            Vector2Int right)
        {
            var yComparison = left.y.CompareTo(right.y);

            return yComparison != 0
                ? yComparison
                : left.x.CompareTo(right.x);
        }

        private static int CompareDoorCandidates(
            DoorAnchorCandidate left,
            DoorAnchorCandidate right)
        {
            var firstCellComparison = ComparePositions(
                left.Edge.FirstCell,
                right.Edge.FirstCell);

            return firstCellComparison != 0
                ? firstCellComparison
                : ComparePositions(
                    left.Edge.SecondCell,
                    right.Edge.SecondCell);
        }

        private static void Shuffle<T>(
            List<T> items,
            System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);

                (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
            }
        }

        private readonly struct DoorAnchorCandidate
        {
            public DungeonCellEdge Edge { get; }

            public DoorAnchorCandidate(DungeonCellEdge edge)
            {
                Edge = edge;
            }
        }
    }
}