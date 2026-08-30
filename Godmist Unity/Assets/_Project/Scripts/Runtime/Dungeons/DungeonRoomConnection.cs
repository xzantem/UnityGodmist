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

        public bool TrySetDirectPassage(
            Vector2Int firstDoorCell,
            Vector2Int secondDoorCell,
            int doorwayWidth)
        {
            if (doorwayWidth < 1)
            {
                return false;
            }

            FirstDoorCell = firstDoorCell;
            SecondDoorCell = secondDoorCell;
            DoorwayWidth = doorwayWidth;
            HasGeometry = true;

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
