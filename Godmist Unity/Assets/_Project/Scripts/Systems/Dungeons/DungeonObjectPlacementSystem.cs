using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonObjectPlacementSystem
    {
        private const int ObjectPlacementSeedSalt =
            unchecked((int)0x5F3759DF);

        public void Place(
            DungeonInstance dungeon,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null)
        {
            if (dungeon == null || dungeon.Definition == null) return;

            var objectDefinitions = GetValidObjectDefinitions(
                dungeon.Definition.ObjectDefinitions);

            var doorDefinitions = GetValidDoorDefinitions(
                dungeon.Definition.ObjectDefinitions);

            var random = new System.Random(
                dungeon.Seed ^ ObjectPlacementSeedSalt);

            var lootTableSystem = new LootTableSystem(
                random,
                equipmentParts);

            foreach (var floor in dungeon.Floors)
            {
                if (floor == null
                    || floor.Map == null
                    || floor.Objects.Count > 0)
                {
                    continue;
                }

                if (doorDefinitions.Count > 0)
                {
                    PlaceDoorsOnFloor(
                        dungeon,
                        floor,
                        doorDefinitions,
                        random);
                }

                if (objectDefinitions.Count > 0)
                {
                    PlaceObjectsOnFloor(
                        dungeon,
                        floor,
                        objectDefinitions,
                        lootTableSystem,
                        random);
                }

                if (!BindProgressionObjects(floor))
                {
                    throw new InvalidOperationException(
                        $"Nie udalo sie przypisac wszystkich obiektow progresji "
                        + $"na pietrze {floor.FloorIndex}.");
                }
            }
        }

        private static bool BindProgressionObjects(DungeonFloorInstance floor)
        {
            if (floor == null) return false;

            foreach (var connection in floor.Connections)
            {
                if (connection == null
                    || !connection.HasProgressionGate
                    || string.IsNullOrWhiteSpace(connection.RequiredBranchRoomId))
                {
                    continue;
                }

                var sourceIds = new List<string>();

                foreach (var dungeonObject in floor.Objects)
                {
                    var isProgressionSource =
                        dungeonObject is LeverDungeonObjectInstance
                        || (dungeonObject is ConditionDungeonObjectInstance
                            && dungeonObject.IsProgressionRequirement);

                    if (!isProgressionSource
                        || !floor.Map.TryGetRoomId(
                            dungeonObject.Position,
                            out var roomId)
                        || !string.Equals(
                            roomId,
                            connection.RequiredBranchRoomId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var bound = dungeonObject switch
                    {
                        LeverDungeonObjectInstance lever =>
                            lever.TryBindCondition(
                                connection.ProgressionConditionId),
                        ConditionDungeonObjectInstance condition =>
                            condition.TryBindCondition(
                                connection.ProgressionConditionId),
                        _ => false
                    };

                    if (!bound)
                    {
                        return false;
                    }

                    sourceIds.Add(dungeonObject.InstanceId);
                }

                if (!connection.TrySetProgressionRequirementSources(sourceIds))
                {
                    return false;
                }

                foreach (var dungeonObject in floor.Objects)
                {
                    var isSatisfied = dungeonObject switch
                    {
                        LeverDungeonObjectInstance lever => lever.IsActivated,
                        ConditionDungeonObjectInstance condition =>
                            condition.IsSatisfied,
                        _ => false
                    };

                    if (!isSatisfied
                        || !sourceIds.Contains(dungeonObject.InstanceId))
                    {
                        continue;
                    }

                    floor.TrySatisfyProgressionCondition(
                        connection.ProgressionConditionId,
                        dungeonObject.InstanceId);
                }
            }

            return true;
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

                    var firstCell = room.LayoutPosition
                                    + DungeonRoomTransformUtility.TransformCell(
                                        anchor.LocalCell,
                                        template.SizeInCells,
                                        room.RotationDegrees);
                    var secondCell = firstCell
                                     + DungeonRoomTransformUtility.GetDirectionOffset(
                                         DungeonRoomTransformUtility.TransformDirection(
                                             anchor.Direction,
                                             room.RotationDegrees));

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

            var semanticResolver = new DungeonSemanticAnchorResolver();
            var semanticCandidates = semanticResolver.GetCandidates(
                floor,
                dungeon.Definition.RoomTemplateCatalog,
                new[]
                {
                    DungeonSemanticAnchorType.Chest,
                    DungeonSemanticAnchorType.Obstacle,
                    DungeonSemanticAnchorType.Trap,
                    DungeonSemanticAnchorType.Lever,
                    DungeonSemanticAnchorType.Condition,
                    DungeonSemanticAnchorType.Reward
                },
                roomType => IsEligibleRoomType(roomType)
                    || roomType == DungeonRoomType.Corridor);

            semanticCandidates = semanticCandidates.FindAll(
                IsEligibleSemanticCandidate);

            semanticCandidates = semanticCandidates.FindAll(candidate =>
                GetDefinitionsForAnchorType(
                    definitions,
                    candidate.Anchor.Type).Count > 0);

            var reservedEnemyCells = new DungeonEnemyAnchorResolver()
                .GetReservedCells(
                    floor,
                    dungeon.Definition.RoomTemplateCatalog);

            semanticCandidates = semanticCandidates.FindAll(candidate =>
                candidate.Cells.All(cell => !reservedEnemyCells.Contains(cell)));

            if (semanticCandidates.Count == 0)
            {
                if (dungeon.Definition.MinObjectCountPerFloor > 0)
                {
                    throw new InvalidOperationException(
                        $"Pietro {floor.FloorIndex} nie ma uzytecznych kotew "
                        + "semantycznych dla skonfigurowanych obiektow.");
                }

                return;
            }

            PlaceAnchoredObjects(
                dungeon,
                floor,
                definitions,
                lootTableSystem,
                random,
                semanticResolver,
                semanticCandidates);
        }

        private static void PlaceAnchoredObjects(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions,
            LootTableSystem lootTableSystem,
            System.Random random,
            DungeonSemanticAnchorResolver semanticResolver,
            List<DungeonSemanticAnchorPlacement> candidates)
        {
            var maximumCount = Mathf.Min(
                dungeon.Definition.MaxObjectCountPerFloor,
                candidates.Count);

            if (maximumCount <= 0) return;

            var minimumCount = Mathf.Min(
                dungeon.Definition.MinObjectCountPerFloor,
                maximumCount);

            var selectedAnchors = semanticResolver.Select(
                candidates,
                minimumCount,
                maximumCount,
                random);

            for (var index = 0; index < selectedAnchors.Count; index++)
            {
                var selectedAnchor = selectedAnchors[index];
                var matchingDefinitions = GetDefinitionsForAnchorType(
                    definitions,
                    selectedAnchor.Anchor.Type);

                if (matchingDefinitions.Count == 0) continue;

                var position = GetAvailableAnchorPosition(
                    floor,
                    selectedAnchor.Cells,
                    random);

                if (!position.HasValue) continue;

                var definition = SelectDefinition(matchingDefinitions, random);

                if (definition == null) continue;

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}."
                    + $"object_{index:00}";

                var dungeonObject = CreateObjectInstance(
                    instanceId,
                    definition,
                    position.Value,
                    lootTableSystem,
                    selectedAnchor.Anchor.ActivationGroupId,
                    selectedAnchor.Room.IsRequiredProgressionRoom
                    && (selectedAnchor.Anchor.Type
                        == DungeonSemanticAnchorType.Lever
                        || selectedAnchor.Anchor.Type
                           == DungeonSemanticAnchorType.Condition));

                if (dungeonObject != null)
                {
                    floor.TryAddObject(dungeonObject);
                }
            }
        }

        private static List<DungeonObjectDefinition> GetDefinitionsForAnchorType(
            List<DungeonObjectDefinition> definitions,
            DungeonSemanticAnchorType anchorType)
        {
            var result = new List<DungeonObjectDefinition>();

            foreach (var definition in definitions)
            {
                if ((anchorType == DungeonSemanticAnchorType.Chest
                     && definition is ChestDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Obstacle
                        && definition is ObstacleDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Trap
                        && definition is SpikeTrapDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Lever
                        && definition is LeverDungeonObjectDefinition)
                    || ((anchorType == DungeonSemanticAnchorType.Condition
                         || anchorType == DungeonSemanticAnchorType.Reward)
                        && definition is ConditionDungeonObjectDefinition))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        private static Vector2Int? GetAvailableAnchorPosition(
            DungeonFloorInstance floor,
            IReadOnlyList<Vector2Int> cells,
            System.Random random)
        {
            var candidates = new List<Vector2Int>(cells);
            candidates.Sort(ComparePositions);
            Shuffle(candidates, random);

            foreach (var candidate in candidates)
            {
                if (!IsOccupiedByDungeonObject(floor, candidate))
                {
                    return candidate;
                }
            }

            return null;
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

        private static bool IsEligibleRoomType(DungeonRoomType roomType)
        {
            return roomType == DungeonRoomType.Normal
                || roomType == DungeonRoomType.Optional;
        }

        private static bool IsEligibleSemanticCandidate(
            DungeonSemanticAnchorPlacement candidate)
        {
            if (candidate?.Room == null) return false;

            return IsEligibleRoomType(candidate.Room.RoomType)
                || candidate.Anchor.Type == DungeonSemanticAnchorType.Trap
                   && candidate.Room.RoomType == DungeonRoomType.Corridor;
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
            LootTableSystem lootTableSystem,
            string activationGroupId,
            bool isProgressionRequirement = false)
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

            if (definition is SpikeTrapDungeonObjectDefinition spikeTrap)
            {
                return new SpikeTrapDungeonObjectInstance(
                    instanceId,
                    spikeTrap,
                    position,
                    activationGroupId);
            }

            if (definition is LeverDungeonObjectDefinition lever)
            {
                return new LeverDungeonObjectInstance(
                    instanceId,
                    lever,
                    position,
                    isProgressionRequirement);
            }

            if (definition is ConditionDungeonObjectDefinition condition)
            {
                return new ConditionDungeonObjectInstance(
                    instanceId,
                    condition,
                    position,
                    isProgressionRequirement);
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
