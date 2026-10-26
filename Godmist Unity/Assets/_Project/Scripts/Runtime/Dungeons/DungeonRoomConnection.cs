using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonRoomConnection
    {
        public string FirstRoomId { get; }
        public string SecondRoomId { get; }

        public bool HasGeometry { get; private set; }
        public Vector2Int FirstDoorCell { get; private set; }
        public Vector2Int SecondDoorCell { get; private set; }
        public int DoorwayWidth { get; private set; }
        public int DoorwayLevel { get; private set; }
        public DungeonSurfacePosition FirstDoorSurface => new(FirstDoorCell, DoorwayLevel);
        public DungeonSurfacePosition SecondDoorSurface => new(SecondDoorCell, DoorwayLevel);
        private readonly List<Vector2Int> corridorCells = new();
        public IReadOnlyList<Vector2Int> CorridorCells => corridorCells;
        public Vector2Int EndDoorCell { get; private set; }
        public Vector2Int GetEntranceCell(string roomId) => roomId == FirstRoomId
            ? FirstDoorCell : corridorCells.Count > 0 ? EndDoorCell : SecondDoorCell;

        public bool HasProgressionGate { get; private set; }
        public string ProgressionGateId { get; private set; } = string.Empty;
        public string ProgressionConditionId { get; private set; } = string.Empty;
        public string RequiredBranchRoomId { get; private set; } = string.Empty;
        public bool IsProgressionGateUnlocked { get; private set; }

        private readonly HashSet<string> requiredProgressionSourceIds = new(
            StringComparer.Ordinal);
        private readonly HashSet<string> satisfiedProgressionSourceIds = new(
            StringComparer.Ordinal);

        public IReadOnlyCollection<string> RequiredProgressionSourceIds =>
            requiredProgressionSourceIds;

        public DungeonRoomConnection(
            string firstRoomId,
            string secondRoomId)
        {
            if (string.IsNullOrWhiteSpace(firstRoomId))
            {
                throw new ArgumentException(
                    "FirstRoomId musi byc niepuste.",
                    nameof(firstRoomId));
            }

            if (string.IsNullOrWhiteSpace(secondRoomId))
            {
                throw new ArgumentException(
                    "SecondRoomId musi byc niepuste.",
                    nameof(secondRoomId));
            }

            if (string.Equals(
                    firstRoomId,
                    secondRoomId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Polaczenie nie moze prowadzic do tego samego pokoju.");
            }

            FirstRoomId = firstRoomId;
            SecondRoomId = secondRoomId;
        }

        public bool TrySetDirectPassage(Vector2Int firstDoorCell, Vector2Int secondDoorCell, int doorwayWidth)
            => TrySetDirectPassage(new DungeonSurfacePosition(firstDoorCell), new DungeonSurfacePosition(secondDoorCell), doorwayWidth);

        public bool TrySetDirectPassage(DungeonSurfacePosition first, DungeonSurfacePosition second, int doorwayWidth)
        {
            if (doorwayWidth < 1 || first.Level != second.Level
                || Math.Abs((long)first.Cell.x - second.Cell.x) + Math.Abs((long)first.Cell.y - second.Cell.y) != 1) return false;
            FirstDoorCell = first.Cell;
            SecondDoorCell = second.Cell;
            DoorwayLevel = first.Level;
            DoorwayWidth = doorwayWidth;
            HasGeometry = true;
            corridorCells.Clear();
            EndDoorCell = second.Cell;
            return true;
        }

        public bool TrySetRoutedPassage(Vector2Int first, Vector2Int firstApproach,
            Vector2Int endDoor, int width, IEnumerable<Vector2Int> cells)
        {
            if (cells == null || width < 1 || width > 8) return false;
            var footprint = new HashSet<Vector2Int>();
            foreach (var cell in cells)
                if (footprint.Count >= 4096 || !footprint.Add(cell)) return false;
            if (footprint.Count == 0 || footprint.Count > 4096 || footprint.Contains(first)
                || footprint.Contains(endDoor) || !footprint.Contains(firstApproach)) return false;
            var normal = firstApproach - first;
            if (Math.Abs(normal.x) + Math.Abs(normal.y) != 1) return false;
            var lateral = normal.x != 0 ? Vector2Int.up : Vector2Int.right;
            for (var i = 0; i < width; i++)
                if (!footprint.Contains(firstApproach + lateral * i)
                    || !footprint.Contains(endDoor - normal + lateral * i)
                    || footprint.Contains(first + lateral * i) || footprint.Contains(endDoor + lateral * i)) return false;
            var visited = new HashSet<Vector2Int> { firstApproach };
            var pending = new Queue<Vector2Int>(); pending.Enqueue(firstApproach);
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (footprint.Contains(next) && visited.Add(next)) pending.Enqueue(next);
                }
            }
            if (visited.Count != footprint.Count || !TrySetDirectPassage(first, firstApproach, width)) return false;
            EndDoorCell = endDoor;
            corridorCells.AddRange(footprint);
            corridorCells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return true;
        }

        public bool TrySetProgressionGate(
            string gateId,
            string conditionId,
            string requiredBranchRoomId)
        {
            if (HasProgressionGate
                || string.IsNullOrWhiteSpace(gateId)
                || string.IsNullOrWhiteSpace(conditionId)
                || string.IsNullOrWhiteSpace(requiredBranchRoomId))
            {
                return false;
            }

            ProgressionGateId = gateId;
            ProgressionConditionId = conditionId;
            RequiredBranchRoomId = requiredBranchRoomId;
            HasProgressionGate = true;
            IsProgressionGateUnlocked = false;

            return true;
        }

        public bool TrySatisfyProgressionCondition(string conditionId)
        {
            return TrySatisfyProgressionCondition(conditionId, null);
        }

        public bool TrySatisfyProgressionCondition(
            string conditionId,
            string sourceId)
        {
            if (!HasProgressionGate
                || IsProgressionGateUnlocked
                || !string.Equals(
                    ProgressionConditionId,
                    conditionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (requiredProgressionSourceIds.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(sourceId)
                    || !requiredProgressionSourceIds.Contains(sourceId))
                {
                    return false;
                }

                satisfiedProgressionSourceIds.Add(sourceId);

                if (satisfiedProgressionSourceIds.Count
                    < requiredProgressionSourceIds.Count)
                {
                    return false;
                }
            }

            IsProgressionGateUnlocked = true;
            return true;
        }

        public bool TrySetProgressionRequirementSources(
            IEnumerable<string> sourceIds)
        {
            if (!HasProgressionGate || sourceIds == null)
            {
                return false;
            }

            var normalizedSourceIds = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var sourceId in sourceIds)
            {
                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    normalizedSourceIds.Add(sourceId);
                }
            }

            if (normalizedSourceIds.Count == 0)
            {
                return false;
            }

            requiredProgressionSourceIds.Clear();
            satisfiedProgressionSourceIds.Clear();

            foreach (var sourceId in normalizedSourceIds)
            {
                requiredProgressionSourceIds.Add(sourceId);
            }

            IsProgressionGateUnlocked = false;
            return true;
        }

        public bool TryRestoreProgressionGateUnlocked(bool isUnlocked)
        {
            if (!HasProgressionGate)
            {
                return false;
            }

            IsProgressionGateUnlocked = isUnlocked;

            satisfiedProgressionSourceIds.Clear();
            if (isUnlocked)
            {
                foreach (var sourceId in requiredProgressionSourceIds)
                {
                    satisfiedProgressionSourceIds.Add(sourceId);
                }
            }

            return true;
        }

        public bool Connects(string firstRoomId, string secondRoomId)
        {
            return string.Equals(
                       FirstRoomId,
                       firstRoomId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       SecondRoomId,
                       secondRoomId,
                       StringComparison.Ordinal)
                   || string.Equals(
                       FirstRoomId,
                       secondRoomId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       SecondRoomId,
                       firstRoomId,
                       StringComparison.Ordinal);
        }
    }
}
