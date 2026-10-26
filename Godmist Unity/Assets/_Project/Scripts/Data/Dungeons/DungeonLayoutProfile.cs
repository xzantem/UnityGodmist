using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DungeonLayoutProfile",
        menuName = "Godmist/Dungeons/Layout Profile")]
    public class DungeonLayoutProfile : ScriptableObject, IIdentifiable
    {
        [field: SerializeField]
        public string Id { get; private set; } = string.Empty;

        [field: Header("Room placement")]
        [field: SerializeField]
        public DungeonDoorPlacementStyle DoorPlacementStyle
        {
            get;
            private set;
        } = DungeonDoorPlacementStyle.Centered;

        [field: SerializeField, Min(0)]
        public int MaxLateralOffsetCells { get; private set; }

        [field: SerializeField, Range(0f, 1f)]
        public float MainPathTurnChance { get; private set; } = 0.5f;

        [field: SerializeField]
        public bool PreferCompactMainPath { get; private set; }

        [field: Header("Doorways")]
        [field: SerializeField, Min(1)]
        public int MinDoorwayWidth { get; private set; } = 1;

        [field: SerializeField, Min(1)]
        public int MaxDoorwayWidth { get; private set; } = 1;

        [field: Header("Corridors")]
        [field: SerializeField, Range(0f, 1f)]
        public float CorridorRoomChance { get; private set; } = 0.4f;

        private void OnValidate()
        {
            MainPathTurnChance = Mathf.Clamp01(MainPathTurnChance);

            MaxLateralOffsetCells = Mathf.Max(0, MaxLateralOffsetCells);

            MinDoorwayWidth = Mathf.Max(1, MinDoorwayWidth);
            MaxDoorwayWidth = Mathf.Max(
                MinDoorwayWidth,
                MaxDoorwayWidth);

            CorridorRoomChance = Mathf.Clamp01(CorridorRoomChance);
        }
    }
}
