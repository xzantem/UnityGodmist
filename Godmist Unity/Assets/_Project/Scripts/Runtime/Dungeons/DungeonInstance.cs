using System;
using System.Collections.Generic;
using UnityEngine;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Combat;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonInstance
    {
        public DungeonDefinition Definition { get; }
        public string DungeonId { get; }
        public int Seed { get; }
        public int DungeonLevel { get; }
        public int CurrentFloorIndex { get; private set; }
        public int HighestReachedFloorIndex { get; private set; }
        public int GuildProgressCreditedFloorCount { get; private set; }
        public bool IsCompleted { get; private set; }

        private readonly HashSet<string> clearedEncounterIds = new();
        private readonly Dictionary<string, CombatRewardResult> rewardsByEncounterId = new();
        private readonly Dictionary<string, Vector2> pendingPlayerPartyPositions = new();
        private readonly List<DungeonFloorInstance> floors = new();

        public Vector2 PlayerPosition { get; private set; }
        public bool HasPlayerPosition { get; private set; }
        public IReadOnlyCollection<string> ClearedEncounterIds => clearedEncounterIds;
        public IReadOnlyDictionary<string, Vector2> PendingPlayerPartyPositions =>
            pendingPlayerPartyPositions;
        public bool HasPendingPlayerPartyPositions =>
            pendingPlayerPartyPositions.Count > 0;
        public IReadOnlyList<DungeonFloorInstance> Floors => floors;

        public DungeonInstance(DungeonDefinition definition, int seed)
            : this(definition, seed, definition?.DungeonLevel ?? 1)
        {
        }

        public DungeonInstance(
            DungeonDefinition definition,
            int seed,
            int dungeonLevel)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("DungeonDefinition musi miec niepuste Id.", nameof(definition));
            }

            Definition = definition;
            DungeonId = definition.Id;
            Seed = seed;
            DungeonLevel = Mathf.Clamp(dungeonLevel, 1, 50);
        }

        public DungeonInstance(
            string dungeonId,
            int seed,
            int dungeonLevel = 1)
        {
            if (string.IsNullOrWhiteSpace(dungeonId))
            {
                throw new ArgumentException("DungeonId musi byc niepuste.", nameof(dungeonId));
            }

            DungeonId = dungeonId;
            Seed = seed;
            DungeonLevel = Mathf.Clamp(dungeonLevel, 1, 50);
        }

        public bool TrySetCurrentFloorIndex(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex == CurrentFloorIndex) return false;
            if (!TryGetFloor(floorIndex, out _)) return false;

            CurrentFloorIndex = floorIndex;
            if (floorIndex > HighestReachedFloorIndex)
            {
                HighestReachedFloorIndex = floorIndex;
            }

            return true;
        }

        public bool TryMarkCompleted()
        {
            if (IsCompleted) return false;

            IsCompleted = true;
            return true;
        }

        public void SetPlayerPosition(Vector2 position)
        {
            PlayerPosition = position;
            HasPlayerPosition = true;
        }

        public bool TrySetPendingPlayerPartyPositions(
            IReadOnlyDictionary<string, Vector2> positions)
        {
            if (positions == null) return false;

            var validatedPositions = new Dictionary<string, Vector2>();

            foreach (var pair in positions)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || float.IsNaN(pair.Value.x)
                    || float.IsNaN(pair.Value.y)
                    || float.IsInfinity(pair.Value.x)
                    || float.IsInfinity(pair.Value.y))
                {
                    return false;
                }

                validatedPositions[pair.Key] = pair.Value;
            }

            pendingPlayerPartyPositions.Clear();

            foreach (var pair in validatedPositions)
            {
                pendingPlayerPartyPositions.Add(pair.Key, pair.Value);
            }

            return true;
        }

        public void ClearPendingPlayerPartyPositions()
        {
            pendingPlayerPartyPositions.Clear();
        }

        public bool TryRestoreState(
            int currentFloorIndex,
            bool isCompleted,
            bool hasPlayerPosition,
            Vector2 playerPosition,
            IEnumerable<string> restoredClearedEncounterIds,
            int highestReachedFloorIndex = -1,
            int guildProgressCreditedFloorCount = 0)
        {
            if (!TryGetFloor(currentFloorIndex, out _))
            {
                return false;
            }

            var normalizedHighestReachedFloorIndex = highestReachedFloorIndex < 0
                ? currentFloorIndex
                : highestReachedFloorIndex;

            var completedFloorCount = isCompleted
                ? floors.Count
                : normalizedHighestReachedFloorIndex;

            if (!TryGetFloor(normalizedHighestReachedFloorIndex, out _)
                || normalizedHighestReachedFloorIndex < currentFloorIndex
                || guildProgressCreditedFloorCount < 0
                || guildProgressCreditedFloorCount > completedFloorCount)
            {
                return false;
            }

            var validatedClearedEncounterIds = new HashSet<string>();

            if (restoredClearedEncounterIds != null)
            {
                foreach (var encounterId in restoredClearedEncounterIds)
                {
                    if (string.IsNullOrWhiteSpace(encounterId))
                    {
                        return false;
                    }

                    validatedClearedEncounterIds.Add(encounterId);
                }
            }

            CurrentFloorIndex = currentFloorIndex;
            HighestReachedFloorIndex = normalizedHighestReachedFloorIndex;
            GuildProgressCreditedFloorCount = guildProgressCreditedFloorCount;
            IsCompleted = isCompleted;

            clearedEncounterIds.Clear();

            foreach (var encounterId in validatedClearedEncounterIds)
            {
                clearedEncounterIds.Add(encounterId);
            }

            HasPlayerPosition = hasPlayerPosition;
            PlayerPosition = hasPlayerPosition
                ? playerPosition
                : default;

            return true;
        }

        public int GetCompletedFloorCount()
        {
            if (floors.Count == 0) return 0;
            if (IsCompleted) return floors.Count;

            return Mathf.Clamp(HighestReachedFloorIndex, 0, floors.Count);
        }

        public bool TrySetGuildProgressCreditedFloorCount(int floorCount)
        {
            if (floorCount < GuildProgressCreditedFloorCount
                || floorCount > GetCompletedFloorCount())
            {
                return false;
            }

            GuildProgressCreditedFloorCount = floorCount;
            return true;
        }

        public bool IsEncounterCleared(string encounterId)
        {
            return !string.IsNullOrWhiteSpace(encounterId) && clearedEncounterIds.Contains(encounterId);
        }

        public bool TryMarkEncounterCleared(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId)) return false;

            return clearedEncounterIds.Add(encounterId);
        }

        public bool TryGetPreRolledRewards(
            string encounterId,
            out CombatRewardResult rewards)
        {
            rewards = null;

            return !string.IsNullOrWhiteSpace(encounterId)
                && rewardsByEncounterId.TryGetValue(encounterId, out rewards);
        }

        public bool TryStorePreRolledRewards(
            string encounterId,
            CombatRewardResult rewards)
        {
            if (string.IsNullOrWhiteSpace(encounterId) || rewards == null) return false;
            if (rewardsByEncounterId.ContainsKey(encounterId)) return false;

            rewardsByEncounterId.Add(encounterId, rewards);
            return true;
        }

        public bool TryAddFloor(DungeonFloorInstance floor)
        {
            if (floor == null
                || floors.Exists(
                    existing => existing.FloorIndex == floor.FloorIndex))
            {
                return false;
            }

            floors.Add(floor);
            floors.Sort(
                (left, right) => left.FloorIndex.CompareTo(right.FloorIndex));

            return true;
        }

        public bool TryGetFloor(
            int floorIndex,
            out DungeonFloorInstance floor)
        {
            floor = floors.Find(
                candidate => candidate.FloorIndex == floorIndex);

            return floor != null;
        }

        public bool TryGetCurrentFloor(out DungeonFloorInstance floor)
        {
            return TryGetFloor(CurrentFloorIndex, out floor);
        }

        public bool TryGetEnemyBody(
            string bodyInstanceId,
            out DungeonEnemyBodyInstance body)
        {
            body = null;

            if (string.IsNullOrWhiteSpace(bodyInstanceId)) return false;

            foreach (var floor in floors)
            {
                if (floor != null
                    && floor.TryGetEnemyBody(bodyInstanceId, out body))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
