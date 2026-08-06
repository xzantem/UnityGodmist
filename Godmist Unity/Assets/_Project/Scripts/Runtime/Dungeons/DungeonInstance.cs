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
        public int CurrentFloorIndex { get; private set; }
        public bool IsCompleted { get; private set; }

        private readonly HashSet<string> clearedEncounterIds = new();
        private readonly Dictionary<string, CombatRewardResult> rewardsByEncounterId = new();
        private readonly List<DungeonFloorInstance> floors = new();

        public Vector2 PlayerPosition { get; private set; }
        public bool HasPlayerPosition { get; private set; }
        public IReadOnlyCollection<string> ClearedEncounterIds => clearedEncounterIds;
        public IReadOnlyList<DungeonFloorInstance> Floors => floors;

        public DungeonInstance(DungeonDefinition definition, int seed)
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
        }

        public DungeonInstance(string dungeonId, int seed)
        {
            if (string.IsNullOrWhiteSpace(dungeonId))
            {
                throw new ArgumentException("DungeonId musi byc niepuste.", nameof(dungeonId));
            }

            DungeonId = dungeonId;
            Seed = seed;
        }

        public bool TrySetCurrentFloorIndex(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex == CurrentFloorIndex) return false;
            if (!TryGetFloor(floorIndex, out _)) return false;

            CurrentFloorIndex = floorIndex;
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
    }
}
