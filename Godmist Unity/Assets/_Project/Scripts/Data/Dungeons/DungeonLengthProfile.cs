using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public sealed class DungeonLengthProfile
    {
        [field: SerializeField] public DungeonLength Length { get; private set; }
        [field: SerializeField] public int FloorCount { get; private set; }
        [field: SerializeField] public int MinRoomsPerFloor { get; private set; }
        [field: SerializeField] public int MaxRoomsPerFloor { get; private set; }
        [field: SerializeField] public int MinEncountersPerFloor { get; private set; }
        [field: SerializeField] public int MaxEncountersPerFloor { get; private set; }
        [field: Tooltip("Branch destinations across the expedition, separate from additional transit rooms. Zero retains legacy generation.")]
        [field: SerializeField] public int MinBranchCount { get; private set; }
        [field: SerializeField] public int MaxBranchCount { get; private set; }
        [field: SerializeField] public int MinBranchTransitRoomCount { get; private set; }
        [field: SerializeField] public int MaxBranchTransitRoomCount { get; private set; }
        [field: Tooltip("Interior rooms in EACH arm between a loop fork and join; these use the base room budget.")]
        [field: SerializeField] public int MinLoopArmRoomCount { get; private set; }
        [field: SerializeField] public int MaxLoopArmRoomCount { get; private set; }
        public int CampingKits => (int)Length - 1;
        public int RewardMultiplier => (int)Length;
    }
}
