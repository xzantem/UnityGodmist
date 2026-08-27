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
        private const int MaxPlacementFallbackGap = 12;
        private const double CorridorRoomChance = 0.4;
        private const int CorridorRoutingPadding = 6;
        private const int MaxFloorGenerationAttempts = 32;
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private enum LayoutDirection
        {
            Right,
            Up,
            Left,
            Down
        }

        private readonly struct PlacedRoom
        {
            public DungeonRoomInstance Room { get; }
            public Vector2Int Size { get; }

            public PlacedRoom(
                DungeonRoomInstance room,
                Vector2Int size)
            {
                Room = room;
                Size = size;
            }
        }
        private readonly struct DoorwayReservation
        {
            public LayoutDirection Direction { get; }
            public int StartCell { get; }
            public int Width { get; }

            public DoorwayReservation(
                LayoutDirection direction,
                int startCell,
                int width)
            {
                Direction = direction;
                StartCell = startCell;
                Width = width;
            }

            public bool Overlaps(
                LayoutDirection direction,
                int startCell,
                int width)
            {
                if (Direction != direction)
                {
                    return false;
                }

                var endCell = StartCell + Width;
                var otherEndCell = startCell + width;

                return StartCell < otherEndCell
                    && endCell > startCell;
            }
        }
        
        private sealed class LayoutAttemptFailedException : Exception
        {
            public LayoutAttemptFailedException(string message) : base(message) {}
        }

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
            for (var attempt = 1; attempt <= MaxFloorGenerationAttempts; attempt++)
            {
                try
                {
                    return GenerateFloorAttempt(
                        definition,
                        floorIndex,
                        random);
                }
                catch (LayoutAttemptFailedException)
                {
                    // Ten wariant ułożenia pokoi nie pozwala spełnić wszystkich ograniczeń.
                }
            }

            throw new InvalidOperationException(
                $"Nie udalo sie wygenerowac poprawnego ukladu pietra {floorIndex} "
                + $"po {MaxFloorGenerationAttempts} probach.");
        }

        private static DungeonFloorInstance GenerateFloorAttempt(
            DungeonDefinition definition,
            int floorIndex,
            System.Random random)
        {
            var floor = new DungeonFloorInstance(floorIndex);

            var targetRoomCount = random.Next(
                definition.MinRoomCountPerFloor,
                definition.MaxRoomCountPerFloor + 1);

            var optionalRoomCount = GetOptionalRoomCount(
                definition,
                targetRoomCount,
                random);

            var mainPathRoomCount = targetRoomCount - optionalRoomCount;
            var loopCandidateRooms = new List<DungeonRoomInstance>();
            var optionalAnchorRooms = new List<PlacedRoom>();
            var occupiedAreas = new List<RectInt>();
            var roomTemplatesById = new Dictionary<string, DungeonRoomTemplateDefinition>();

            DungeonRoomInstance previousRoom = null;
            var previousRoomSize = Vector2Int.zero;
            var previousDirection = LayoutDirection.Right;

            for (var roomIndex = 0; roomIndex < mainPathRoomCount; roomIndex++)
            {
                var roomType = GetMainPathRoomType(
                    roomIndex,
                    mainPathRoomCount,
                    definition.LayoutProfile,
                    random);

                var template = SelectTemplate(
                    definition.RoomTemplateCatalog,
                    roomType,
                    random);

                Vector2Int roomPosition;
                var chosenDirection = previousDirection;

                if (previousRoom == null)
                {
                    roomPosition = Vector2Int.zero;
                }
                else if (!TryFindMainPathPosition(
                    previousRoom.LayoutPosition,
                    previousRoomSize,
                    template.SizeInCells,
                    previousDirection,
                    definition.LayoutProfile,
                    occupiedAreas,
                    random,
                    out roomPosition,
                    out chosenDirection))
                {
                    throw new LayoutAttemptFailedException(
                        "Nie udalo sie znalezc wolnej pozycji dla pokoju glownej trasy.");
                }

                var room = new DungeonRoomInstance(
                    $"floor.{floorIndex}.room.{roomIndex:00}",
                    roomType,
                    template.Id,
                    roomPosition);

                AddRoomOrThrow(floor, room);
                roomTemplatesById.Add(room.RoomId, template);
                occupiedAreas.Add(
                    new RectInt(roomPosition, template.SizeInCells));

                loopCandidateRooms.Add(room);

                if (previousRoom != null
                    && !floor.TryAddConnection(
                        previousRoom.RoomId,
                        room.RoomId))
                {
                    throw new InvalidOperationException(
                        "Nie udalo sie znalezc wolnej pozycji dla pokoju opcjonalnego.");
                }

                if (roomType == DungeonRoomType.Normal || roomType == DungeonRoomType.Corridor)
                {
                    optionalAnchorRooms.Add(
                        new PlacedRoom(room, template.SizeInCells));
                }

                previousRoom = room;
                previousRoomSize = template.SizeInCells;
                previousDirection = chosenDirection;
            }

            for (var optionalIndex = 0; optionalIndex < optionalRoomCount; optionalIndex++)
            {
                var template = SelectTemplate(
                    definition.RoomTemplateCatalog,
                    DungeonRoomType.Optional,
                    random);

                if (!TryFindOptionalRoomPosition(
                    optionalAnchorRooms,
                    template.SizeInCells,
                    definition.LayoutProfile,
                    occupiedAreas,
                    random,
                    out var anchorRoom,
                    out var roomPosition))
                {
                    throw new LayoutAttemptFailedException(
                        "Nie udalo sie znalezc wolnej pozycji dla pokoju opcjonalnego.");
                }

                var room = new DungeonRoomInstance(
                    $"floor.{floorIndex}.optional.{optionalIndex:00}",
                    DungeonRoomType.Optional,
                    template.Id,
                    roomPosition);

                AddRoomOrThrow(floor, room);
                roomTemplatesById.Add(room.RoomId, template);
                occupiedAreas.Add(
                    new RectInt(roomPosition, template.SizeInCells));

                optionalAnchorRooms.Add(
                    new PlacedRoom(room, template.SizeInCells));

                loopCandidateRooms.Add(room);

                if (!floor.TryAddConnection(
                    anchorRoom.Room.RoomId,
                    room.RoomId))
                {
                    throw new InvalidOperationException(
                        "Nie udalo sie utworzyc polaczenia pokoju opcjonalnego.");
                }
            }

            BuildConnectionGeometry(
                floor,
                roomTemplatesById,
                definition.LayoutProfile,
                random);

            var floorMapBuilder = new DungeonFloorMapBuilder();

            if (!floor.TrySetMap(
                    floorMapBuilder.Build(
                        floor,
                        definition.RoomTemplateCatalog)))
            {
                throw new InvalidOperationException(
                    "Nie udalo sie przypisac mapy do pietra lochu.");
            }

            ValidateFloorReachability(floor);

            return floor;
        }

        private static void BuildConnectionGeometry(
            DungeonFloorInstance floor,
            Dictionary<string, DungeonRoomTemplateDefinition> roomTemplatesById,
            DungeonLayoutProfile layoutProfile,
            System.Random random)
        {
            var reservationsByRoomId = new Dictionary<string, List<DoorwayReservation>>();

            foreach (var connection in floor.Connections)
            {
                if (!floor.TryGetRoom(connection.FirstRoomId, out var firstRoom)
                    || !floor.TryGetRoom(connection.SecondRoomId, out var secondRoom))
                {
                    throw new InvalidOperationException(
                        "Polaczenie odwoluje sie do brakujacego pokoju.");
                }

                if (!roomTemplatesById.TryGetValue(firstRoom.RoomId, out var firstTemplate)
                    || !roomTemplatesById.TryGetValue(secondRoom.RoomId, out var secondTemplate))
                {
                    throw new InvalidOperationException(
                        "Brakuje szablonu pokoju podczas generowania polaczenia.");
                }

                var firstRoomArea = new RectInt(
                    firstRoom.LayoutPosition,
                    firstTemplate.SizeInCells);

                var secondRoomArea = new RectInt(
                    secondRoom.LayoutPosition,
                    secondTemplate.SizeInCells);

                if (TrySetDirectPassageGeometry(
                        connection,
                        firstRoom.RoomId,
                        secondRoom.RoomId,
                        firstTemplate,
                        secondTemplate,
                        firstRoomArea,
                        secondRoomArea,
                        layoutProfile,
                        random,
                        reservationsByRoomId))
                {
                    continue;
                }

                throw new LayoutAttemptFailedException(
                    $"Pokoje '{firstRoom.RoomId}' i '{secondRoom.RoomId}' "
                    + "nie maja kompatybilnych zewnetrznych krawedzi przejscia.");
            }
        }

        private static void ValidateFloorReachability(
            DungeonFloorInstance floor)
        {
            if (floor == null || floor.Map == null)
            {
                throw new LayoutAttemptFailedException(
                    "Nie mozna zweryfikowac osiagalnosci pietra bez mapy.");
            }

            var startRoomId = string.Empty;

            foreach (var room in floor.Rooms)
            {
                if (room.RoomType != DungeonRoomType.Start)
                {
                    continue;
                }

                startRoomId = room.RoomId;
                break;
            }

            if (string.IsNullOrWhiteSpace(startRoomId))
            {
                throw new LayoutAttemptFailedException(
                    "Nie znaleziono pokoju Start podczas walidacji osiagalnosci.");
            }

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var hasStartCell = false;

            foreach (var cell in floor.Map.FloorCells)
            {
                if (!floor.Map.TryGetRoomId(cell, out var roomId)
                    || roomId != startRoomId)
                {
                    continue;
                }

                visited.Add(cell);
                queue.Enqueue(cell);
                hasStartCell = true;
                break;
            }

            if (!hasStartCell)
            {
                throw new LayoutAttemptFailedException(
                    "Pokoj Start nie ma zadnej komorki footprintu.");
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();

                foreach (var direction in CardinalDirections)
                {
                    var neighbour = cell + direction;

                    if (!floor.Map.CanMoveBetween(cell, neighbour)
                        || !visited.Add(neighbour))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbour);
                }
            }

            if (visited.Count != floor.Map.FloorCells.Count)
            {
                throw new LayoutAttemptFailedException(
                    "Nie wszystkie komorki footprintow sa osiagalne "
                    + "z pokoju Start.");
            }
        }

        private static bool TrySetDirectPassageGeometry(
            DungeonRoomConnection connection,
            string firstRoomId,
            string secondRoomId,
            DungeonRoomTemplateDefinition firstTemplate,
            DungeonRoomTemplateDefinition secondTemplate,
            RectInt firstRoomArea,
            RectInt secondRoomArea,
            DungeonLayoutProfile layoutProfile,
            System.Random random,
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId)
        {
            if (firstRoomArea.xMax == secondRoomArea.xMin)
            {
                return TrySetVerticalWallPassage(
                    connection,
                    firstRoomId,
                    secondRoomId,
                    firstTemplate,
                    secondTemplate,
                    firstRoomArea,
                    secondRoomArea,
                    true,
                    layoutProfile,
                    random,
                    reservationsByRoomId);
            }

            if (secondRoomArea.xMax == firstRoomArea.xMin)
            {
                return TrySetVerticalWallPassage(
                    connection,
                    firstRoomId,
                    secondRoomId,
                    firstTemplate,
                    secondTemplate,
                    firstRoomArea,
                    secondRoomArea,
                    false,
                    layoutProfile,
                    random,
                    reservationsByRoomId);
            }

            if (firstRoomArea.yMax == secondRoomArea.yMin)
            {
                return TrySetHorizontalWallPassage(
                    connection,
                    firstRoomId,
                    secondRoomId,
                    firstTemplate,
                    secondTemplate,
                    firstRoomArea,
                    secondRoomArea,
                    true,
                    layoutProfile,
                    random,
                    reservationsByRoomId);
            }

            if (secondRoomArea.yMax == firstRoomArea.yMin)
            {
                return TrySetHorizontalWallPassage(
                    connection,
                    firstRoomId,
                    secondRoomId,
                    firstTemplate,
                    secondTemplate,
                    firstRoomArea,
                    secondRoomArea,
                    false,
                    layoutProfile,
                    random,
                    reservationsByRoomId);
            }

            return false;
        }

        private static bool TrySetVerticalWallPassage(
            DungeonRoomConnection connection,
            string firstRoomId,
            string secondRoomId,
            DungeonRoomTemplateDefinition firstTemplate,
            DungeonRoomTemplateDefinition secondTemplate,
            RectInt firstRoomArea,
            RectInt secondRoomArea,
            bool firstRoomIsLeft,
            DungeonLayoutProfile layoutProfile,
            System.Random random,
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId)
        {
            var overlapStart = Mathf.Max(
                firstRoomArea.yMin,
                secondRoomArea.yMin);

            var overlapEnd = Mathf.Min(
                firstRoomArea.yMax,
                secondRoomArea.yMax);

            var doorwayWidth = Mathf.Min(
                layoutProfile.MinDoorwayWidth,
                overlapEnd - overlapStart);

            if (doorwayWidth < layoutProfile.MinDoorwayWidth)
            {
                return false;
            }

            var firstDoorDirection = firstRoomIsLeft
                ? LayoutDirection.Right
                : LayoutDirection.Left;

            var secondDoorDirection = firstRoomIsLeft
                ? LayoutDirection.Left
                : LayoutDirection.Right;

            foreach (var doorwayStart in GetDoorwayStarts(
                        overlapStart,
                        overlapEnd,
                        doorwayWidth,
                        layoutProfile,
                        random))
            {
                var firstDoorCell = firstRoomIsLeft
                    ? new Vector2Int(firstRoomArea.xMax - 1, doorwayStart)
                    : new Vector2Int(firstRoomArea.xMin, doorwayStart);

                var secondDoorCell = firstRoomIsLeft
                    ? new Vector2Int(secondRoomArea.xMin, doorwayStart)
                    : new Vector2Int(secondRoomArea.xMax - 1, doorwayStart);

                if (!AreDoorwayCellsInFootprints(
                        firstTemplate,
                        secondTemplate,
                        firstRoomArea,
                        secondRoomArea,
                        firstDoorCell,
                        secondDoorCell,
                        doorwayWidth,
                        true))
                {
                    continue;
                }

                if (!TryReserveDoorways(
                        reservationsByRoomId,
                        firstRoomId,
                        firstDoorDirection,
                        doorwayStart,
                        secondRoomId,
                        secondDoorDirection,
                        doorwayStart,
                        doorwayWidth))
                {
                    continue;
                }

                return connection.TrySetDirectPassage(
                    firstDoorCell,
                    secondDoorCell,
                    doorwayWidth);
            }

            return false;
        }

        private static bool TrySetHorizontalWallPassage(
            DungeonRoomConnection connection,
            string firstRoomId,
            string secondRoomId,
            DungeonRoomTemplateDefinition firstTemplate,
            DungeonRoomTemplateDefinition secondTemplate,
            RectInt firstRoomArea,
            RectInt secondRoomArea,
            bool firstRoomIsBelow,
            DungeonLayoutProfile layoutProfile,
            System.Random random,
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId)
        {
            var overlapStart = Mathf.Max(
                firstRoomArea.xMin,
                secondRoomArea.xMin);

            var overlapEnd = Mathf.Min(
                firstRoomArea.xMax,
                secondRoomArea.xMax);

            var doorwayWidth = Mathf.Min(
                layoutProfile.MinDoorwayWidth,
                overlapEnd - overlapStart);

            if (doorwayWidth < layoutProfile.MinDoorwayWidth)
            {
                return false;
            }

            var firstDoorDirection = firstRoomIsBelow
                ? LayoutDirection.Up
                : LayoutDirection.Down;

            var secondDoorDirection = firstRoomIsBelow
                ? LayoutDirection.Down
                : LayoutDirection.Up;

            foreach (var doorwayStart in GetDoorwayStarts(
                        overlapStart,
                        overlapEnd,
                        doorwayWidth,
                        layoutProfile,
                        random))
            {
                var firstDoorCell = firstRoomIsBelow
                    ? new Vector2Int(doorwayStart, firstRoomArea.yMax - 1)
                    : new Vector2Int(doorwayStart, firstRoomArea.yMin);

                var secondDoorCell = firstRoomIsBelow
                    ? new Vector2Int(doorwayStart, secondRoomArea.yMin)
                    : new Vector2Int(doorwayStart, secondRoomArea.yMax - 1);

                if (!AreDoorwayCellsInFootprints(
                        firstTemplate,
                        secondTemplate,
                        firstRoomArea,
                        secondRoomArea,
                        firstDoorCell,
                        secondDoorCell,
                        doorwayWidth,
                        false))
                {
                    continue;
                }

                if (!TryReserveDoorways(
                        reservationsByRoomId,
                        firstRoomId,
                        firstDoorDirection,
                        doorwayStart,
                        secondRoomId,
                        secondDoorDirection,
                        doorwayStart,
                        doorwayWidth))
                {
                    continue;
                }

                return connection.TrySetDirectPassage(
                    firstDoorCell,
                    secondDoorCell,
                    doorwayWidth);
            }

            return false;
        }

        private static bool AreDoorwayCellsInFootprints(
            DungeonRoomTemplateDefinition firstTemplate,
            DungeonRoomTemplateDefinition secondTemplate,
            RectInt firstRoomArea,
            RectInt secondRoomArea,
            Vector2Int firstDoorCell,
            Vector2Int secondDoorCell,
            int doorwayWidth,
            bool isVertical)
        {
            var firstFootprint = firstTemplate.Footprint
                                 ?? new DungeonRoomFootprint();
            var secondFootprint = secondTemplate.Footprint
                                  ?? new DungeonRoomFootprint();

            firstFootprint.Validate(
                firstTemplate.SizeInCells,
                firstTemplate.Id);
            secondFootprint.Validate(
                secondTemplate.SizeInCells,
                secondTemplate.Id);

            var lateral = isVertical
                ? Vector2Int.up
                : Vector2Int.right;

            for (var offset = 0; offset < doorwayWidth; offset++)
            {
                var firstCell = firstDoorCell + lateral * offset;
                var secondCell = secondDoorCell + lateral * offset;

                if (!firstFootprint.Contains(
                        firstCell - firstRoomArea.position,
                        firstTemplate.SizeInCells)
                    || !secondFootprint.Contains(
                        secondCell - secondRoomArea.position,
                        secondTemplate.SizeInCells))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<int> GetDoorwayStarts(
            int overlapStart,
            int overlapEnd,
            int doorwayWidth,
            DungeonLayoutProfile layoutProfile,
            System.Random random)
        {
            var maximumStart = overlapEnd - doorwayWidth;
            var centredStart = overlapStart
                            + (maximumStart - overlapStart) / 2;

            var starts = new List<int>();

            for (var start = overlapStart; start <= maximumStart; start++)
            {
                starts.Add(start);
            }

            if (layoutProfile.DoorPlacementStyle
                == DungeonDoorPlacementStyle.Random)
            {
                ShuffleOffsets(starts, random);
                return starts;
            }

            starts.Sort((firstStart, secondStart) =>
            {
                var firstDistance = Mathf.Abs(firstStart - centredStart);
                var secondDistance = Mathf.Abs(secondStart - centredStart);

                if (layoutProfile.DoorPlacementStyle
                    == DungeonDoorPlacementStyle.Offset)
                {
                    var firstIsPreferred = firstDistance
                                        <= layoutProfile.MaxLateralOffsetCells;

                    var secondIsPreferred = secondDistance
                                            <= layoutProfile.MaxLateralOffsetCells;

                    if (firstIsPreferred != secondIsPreferred)
                    {
                        return firstIsPreferred ? -1 : 1;
                    }
                }

                return firstDistance.CompareTo(secondDistance);
            });

            return starts;
        }

        private static bool TryReserveDoorways(
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId,
            string firstRoomId,
            LayoutDirection firstDirection,
            int firstStartCell,
            string secondRoomId,
            LayoutDirection secondDirection,
            int secondStartCell,
            int doorwayWidth)
        {
            if (!IsDoorwayAvailable(
                    reservationsByRoomId,
                    firstRoomId,
                    firstDirection,
                    firstStartCell,
                    doorwayWidth)
                || !IsDoorwayAvailable(
                    reservationsByRoomId,
                    secondRoomId,
                    secondDirection,
                    secondStartCell,
                    doorwayWidth))
            {
                return false;
            }

            AddDoorwayReservation(
                reservationsByRoomId,
                firstRoomId,
                firstDirection,
                firstStartCell,
                doorwayWidth);

            AddDoorwayReservation(
                reservationsByRoomId,
                secondRoomId,
                secondDirection,
                secondStartCell,
                doorwayWidth);

            return true;
        }

        private static bool IsDoorwayAvailable(
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId,
            string roomId,
            LayoutDirection direction,
            int startCell,
            int doorwayWidth)
        {
            if (!reservationsByRoomId.TryGetValue(
                    roomId,
                    out var reservations))
            {
                return true;
            }

            foreach (var reservation in reservations)
            {
                if (reservation.Overlaps(
                        direction,
                        startCell,
                        doorwayWidth))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddDoorwayReservation(
            Dictionary<string, List<DoorwayReservation>> reservationsByRoomId,
            string roomId,
            LayoutDirection direction,
            int startCell,
            int doorwayWidth)
        {
            if (!reservationsByRoomId.TryGetValue(
                    roomId,
                    out var reservations))
            {
                reservations = new List<DoorwayReservation>();
                reservationsByRoomId.Add(roomId, reservations);
            }

            reservations.Add(
                new DoorwayReservation(
                    direction,
                    startCell,
                    doorwayWidth));
        }

        private static LayoutDirection[] GetOptionalRoomDirections(
            System.Random random)
        {
            var directions = new[]
            {
                LayoutDirection.Up,
                LayoutDirection.Down,
                LayoutDirection.Right,
                LayoutDirection.Left
            };

            var firstDirectionIndex = random.Next(directions.Length);
            var result = new LayoutDirection[directions.Length];

            for (var index = 0; index < directions.Length; index++)
            {
                result[index] = directions[
                    (firstDirectionIndex + index) % directions.Length];
            }

            return result;
        }

        private static LayoutDirection TurnLeft(
            LayoutDirection direction)
        {
            return direction switch
            {
                LayoutDirection.Right => LayoutDirection.Up,
                LayoutDirection.Up => LayoutDirection.Left,
                LayoutDirection.Left => LayoutDirection.Down,
                LayoutDirection.Down => LayoutDirection.Right,
                _ => LayoutDirection.Right
            };
        }

        private static LayoutDirection TurnRight(
            LayoutDirection direction)
        {
            return direction switch
            {
                LayoutDirection.Right => LayoutDirection.Down,
                LayoutDirection.Down => LayoutDirection.Left,
                LayoutDirection.Left => LayoutDirection.Up,
                LayoutDirection.Up => LayoutDirection.Right,
                _ => LayoutDirection.Right
            };
        }

        private static LayoutDirection GetReverseDirection(
            LayoutDirection direction)
        {
            return direction switch
            {
                LayoutDirection.Right => LayoutDirection.Left,
                LayoutDirection.Left => LayoutDirection.Right,
                LayoutDirection.Up => LayoutDirection.Down,
                LayoutDirection.Down => LayoutDirection.Up,
                _ => LayoutDirection.Right
            };
        }

        private static bool TryFindMainPathPosition(
            Vector2Int previousPosition,
            Vector2Int previousSize,
            Vector2Int nextSize,
            LayoutDirection previousDirection,
            DungeonLayoutProfile layoutProfile,
            List<RectInt> occupiedAreas,
            System.Random random,
            out Vector2Int position,
            out LayoutDirection chosenDirection)
        {
            foreach (var direction in GetMainPathDirections(
                previousDirection,
                layoutProfile,
                random))
            {
                foreach (var lateralOffset in GetLateralOffsets(
                    previousSize,
                    nextSize,
                    direction,
                    layoutProfile,
                    random))
                    {
                        var candidatePosition = GetConnectedPosition(
                            previousPosition,
                            previousSize,
                            nextSize,
                            direction,
                            lateralOffset);

                        if (!IsAreaFree(
                                candidatePosition,
                                nextSize,
                                occupiedAreas))
                        {
                            continue;
                        }

                        position = candidatePosition;
                        chosenDirection = direction;
                        return true;
                    }
            }

            position = default;
            chosenDirection = previousDirection;

            return false;
        }

        private static bool TryFindOptionalRoomPosition(
            List<PlacedRoom> optionalAnchorRooms,
            Vector2Int optionalRoomSize,
            DungeonLayoutProfile layoutProfile,
            List<RectInt> occupiedAreas,
            System.Random random,
            out PlacedRoom anchorRoom,
            out Vector2Int position)
        {
            var firstAnchorIndex = random.Next(optionalAnchorRooms.Count);

            for (var offset = 0; offset < optionalAnchorRooms.Count; offset++)
            {
                var anchorIndex = (firstAnchorIndex + offset)
                                % optionalAnchorRooms.Count;

                var candidateAnchor = optionalAnchorRooms[anchorIndex];

                foreach (var direction in GetOptionalRoomDirections(random))
                {
                    foreach (var lateralOffset in GetLateralOffsets(
                        candidateAnchor.Size,
                        optionalRoomSize,
                        direction,
                        layoutProfile,
                        random))
                    {
                        var candidatePosition = GetConnectedPosition(
                            candidateAnchor.Room.LayoutPosition,
                            candidateAnchor.Size,
                            optionalRoomSize,
                            direction,
                            lateralOffset);

                        if (!IsAreaFree(
                                candidatePosition,
                                optionalRoomSize,
                                occupiedAreas))
                        {
                            continue;
                        }

                        anchorRoom = candidateAnchor;
                        position = candidatePosition;
                        return true;
                    }
                }
            }

            anchorRoom = default;
            position = default;

            return false;
        }

        private static LayoutDirection[] GetMainPathDirections(
            LayoutDirection previousDirection,
            DungeonLayoutProfile layoutProfile,
            System.Random random)
        {
            var turnLeft = TurnLeft(previousDirection);
            var turnRight = TurnRight(previousDirection);
            var reverseDirection = GetReverseDirection(previousDirection);

            if (random.NextDouble() >= layoutProfile.MainPathTurnChance)
            {
                return new[]
                {
                    previousDirection,
                    turnLeft,
                    turnRight,
                    reverseDirection
                };
            }

            return random.Next(0, 2) == 0
                ? new[]
                {
                    turnLeft,
                    turnRight,
                    previousDirection,
                    reverseDirection
                }
                : new[]
                {
                    turnRight,
                    turnLeft,
                    previousDirection,
                    reverseDirection
                };
        }

        private static List<int> GetLateralOffsets(
            Vector2Int anchorSize,
            Vector2Int nextSize,
            LayoutDirection direction,
            DungeonLayoutProfile layoutProfile,
            System.Random random)
        {
            var isHorizontalConnection = direction == LayoutDirection.Left
                                        || direction == LayoutDirection.Right;

            var anchorLength = isHorizontalConnection
                ? anchorSize.y
                : anchorSize.x;

            var nextLength = isHorizontalConnection
                ? nextSize.y
                : nextSize.x;

            var minimumOverlap = layoutProfile.MinDoorwayWidth;

            var minimumOffset = minimumOverlap - nextLength;
            var maximumOffset = anchorLength - minimumOverlap;

            var centredOffset = Mathf.Clamp(
                (anchorLength - nextLength) / 2,
                minimumOffset,
                maximumOffset);

            var offsets = new List<int>();

            if (layoutProfile.DoorPlacementStyle
                == DungeonDoorPlacementStyle.Centered)
            {
                offsets.Add(centredOffset);

                for (var distance = 1;
                    centredOffset - distance >= minimumOffset
                    || centredOffset + distance <= maximumOffset;
                    distance++)
                {
                    var leftOffset = centredOffset - distance;
                    var rightOffset = centredOffset + distance;

                    if (leftOffset >= minimumOffset)
                    {
                        offsets.Add(leftOffset);
                    }

                    if (rightOffset <= maximumOffset)
                    {
                        offsets.Add(rightOffset);
                    }
                }

                return offsets;
            }

            var allowedMinimumOffset = Mathf.Max(
                minimumOffset,
                centredOffset - layoutProfile.MaxLateralOffsetCells);

            var allowedMaximumOffset = Mathf.Min(
                maximumOffset,
                centredOffset + layoutProfile.MaxLateralOffsetCells);

            for (var offset = allowedMinimumOffset;
                offset <= allowedMaximumOffset;
                offset++)
            {
                offsets.Add(offset);
            }

            ShuffleOffsets(offsets, random);

            return offsets;
        }

        private static void ShuffleOffsets(
            List<int> offsets,
            System.Random random)
        {
            for (var index = offsets.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);

                (offsets[index], offsets[swapIndex])
                    = (offsets[swapIndex], offsets[index]);
            }
        }

        private static Vector2Int GetConnectedPosition(
            Vector2Int anchorPosition,
            Vector2Int anchorSize,
            Vector2Int nextSize,
            LayoutDirection direction,
            int lateralOffset)
        {
            return direction switch
            {
                LayoutDirection.Right => new Vector2Int(
                    anchorPosition.x + anchorSize.x,
                    anchorPosition.y + lateralOffset),

                LayoutDirection.Left => new Vector2Int(
                    anchorPosition.x - nextSize.x,
                    anchorPosition.y + lateralOffset),

                LayoutDirection.Up => new Vector2Int(
                    anchorPosition.x + lateralOffset,
                    anchorPosition.y + anchorSize.y),

                LayoutDirection.Down => new Vector2Int(
                    anchorPosition.x + lateralOffset,
                    anchorPosition.y - nextSize.y),

                _ => anchorPosition
            };
        }

        private static bool IsAreaFree(
            Vector2Int position,
            Vector2Int size,
            List<RectInt> occupiedAreas)
        {
            var candidateArea = new RectInt(position, size);

            foreach (var occupiedArea in occupiedAreas)
            {
                if (candidateArea.Overlaps(occupiedArea))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetOptionalRoomCount(
            DungeonDefinition definition,
            int targetRoomCount,
            System.Random random)
        {
            var maximumAllowedOptionalRooms = Math.Min(
                definition.MaxOptionalRoomCount,
                targetRoomCount - 3);

            return random.Next(
                definition.MinOptionalRoomCount,
                maximumAllowedOptionalRooms + 1);
        }

        private static DungeonRoomType GetMainPathRoomType(
            int roomIndex,
            int mainPathRoomCount,
            DungeonLayoutProfile layoutProfile,
            System.Random random)
        {
            if (roomIndex == 0) return DungeonRoomType.Start;
            if (roomIndex == mainPathRoomCount - 1)
            {
                return DungeonRoomType.Final;
            }

            return random.NextDouble() < layoutProfile.CorridorRoomChance
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

            if (definition.LayoutProfile == null)
            {
                throw new InvalidOperationException(
                    "Proceduralny loch wymaga DungeonLayoutProfile.");
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

            if (definition.MinOptionalRoomCount < 0
                || definition.MaxOptionalRoomCount
                    < definition.MinOptionalRoomCount)
            {
                throw new InvalidOperationException(
                    "Zakres liczby pokoi opcjonalnych jest nieprawidlowy.");
            }

            if (definition.MinOptionalRoomCount
                > definition.MinRoomCountPerFloor - 3)
            {
                throw new InvalidOperationException(
                    "Minimalna liczba pokoi opcjonalnych nie zostawia "
                    + "miejsca na podstawowa trase lochu.");
            }

            if (definition.MinLoopConnections < 0
                || definition.MaxLoopConnections
                    < definition.MinLoopConnections)
            {
                throw new InvalidOperationException(
                    "Zakres liczby polaczen petli jest nieprawidlowy.");
            }

            if (definition.MaxLoopConnections > 0)
            {
                throw new InvalidOperationException(
                    "Polaczenia petli wymagaja passu layoutu lochu v2.");
            }
        }
    }
}
