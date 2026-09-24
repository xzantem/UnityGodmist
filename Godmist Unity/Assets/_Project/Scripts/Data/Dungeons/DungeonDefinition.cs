using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using System.Collections.Generic;
using Godmist.Data.Combat;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DungeonDefinition",
        menuName = "Godmist/Dungeons/Dungeon Definition")]
    public class DungeonDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public bool EnvironmentConfigured { get; private set; }
        [field: SerializeField] public bool IsIndoor { get; private set; }
        [field: SerializeField] public bool IsCold { get; private set; }
        [field: SerializeField] public bool IsWarm { get; private set; }

        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public DungeonTheme Theme { get; private set; }
        [field: SerializeField] public bool IsProcedural { get; private set; }
        [field: SerializeField] public bool IsStoryDungeon { get; private set; }
        [field: Tooltip("Only authored expedition definitions appear on the city offer board. Test and demo dungeons remain loadable by ID.")]
        [field: SerializeField] public bool IsAvailableAsOffer { get; private set; }
        [field: Tooltip("Optional fixed levels for offers of this expedition. Empty uses the party level.")]
        [field: SerializeField] public List<int> FixedOfferLevels { get; private set; } = new();
        [field: SerializeField] public DungeonLayoutProfile LayoutProfile { get; private set; }

        [field: Header("Rewards")]
        [field: SerializeField, Range(1, 50)]
        public int DungeonLevel { get; private set; } = 1;

        [field: SerializeField]
        public DungeonLootTableCatalog LootTableCatalog { get; private set; }

        [field: Header("Procedural generation")]
        [field: SerializeField, Min(1)] public int MinFloorCount { get; private set; } = 1;

        [field: SerializeField, Min(1)] public int MaxFloorCount { get; private set; } = 1;

        [field: SerializeField, Min(3)] public int MinRoomCountPerFloor { get; private set; } = 6;

        [field: SerializeField, Min(3)] public int MaxRoomCountPerFloor { get; private set; } = 9;

        [field: SerializeField, Min(0)] public int MinOptionalRoomCount { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MaxOptionalRoomCount { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MinLoopConnections { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MaxLoopConnections { get; private set; } = 0;

        [field: Header("Progression gates")]
        [field: SerializeField, Range(0, 3)]
        public int MinProgressionGateCount { get; private set; } = 0;

        [field: SerializeField, Range(0, 3)]
        public int MaxProgressionGateCount { get; private set; } = 0;

        [field: Header("Room templates")]
        [field: SerializeField]
        public DungeonRoomTemplateCatalog RoomTemplateCatalog { get; private set; }
        
        [field: SerializeField]
        public List<EncounterDefinition> EncounterDefinitions { get; private set; } = new();

        [field: SerializeField]
        public EncounterDefinition BossEncounterDefinition { get; private set; }

        [field: Header("Encounters")]
        [field: SerializeField, Min(0)] public int MinEncounterCountPerFloor { get; private set; } = 1;

        [field: SerializeField, Min(0)] public int MaxEncounterCountPerFloor { get; private set; } = 3;

        [field: SerializeField] public bool AllowEncounterDefinitionRepeats { get; private set; } = true;

        [field: Header("Dungeon objects")]
        [field: SerializeField]
        public List<DungeonObjectDefinition> ObjectDefinitions { get; private set; } = new();

        [field: Tooltip("Legacy save reconstruction only. New dungeons use room anchors and content groups.")]
        [field: SerializeField, Min(0)]
        public int MinObjectCountPerFloor { get; private set; } = 1;

        [field: Tooltip("Legacy save reconstruction only. New dungeons use room anchors and content groups.")]
        [field: SerializeField, Min(0)]
        public int MaxObjectCountPerFloor { get; private set; } = 3;

        private void OnValidate()
        {
            DungeonLevel = Mathf.Clamp(DungeonLevel, 1, 50);

            MinFloorCount = Mathf.Max(1, MinFloorCount);
            MaxFloorCount = Mathf.Max(MinFloorCount, MaxFloorCount);

            MinRoomCountPerFloor = Mathf.Max(3, MinRoomCountPerFloor);
            MaxRoomCountPerFloor = Mathf.Max(
                MinRoomCountPerFloor,
                MaxRoomCountPerFloor);

            MinOptionalRoomCount = Mathf.Max(0, MinOptionalRoomCount);
            MaxOptionalRoomCount = Mathf.Max(
                MinOptionalRoomCount,
                MaxOptionalRoomCount);

            MinLoopConnections = Mathf.Max(0, MinLoopConnections);
            MaxLoopConnections = Mathf.Max(
                MinLoopConnections,
                MaxLoopConnections);

            MinProgressionGateCount = Mathf.Clamp(
                MinProgressionGateCount,
                0,
                3);
            MaxProgressionGateCount = Mathf.Clamp(
                MaxProgressionGateCount,
                MinProgressionGateCount,
                3);

            MinEncounterCountPerFloor = Mathf.Max(0, MinEncounterCountPerFloor);
            MaxEncounterCountPerFloor = Mathf.Max(
                MinEncounterCountPerFloor,
                MaxEncounterCountPerFloor);

            MinObjectCountPerFloor = Mathf.Max(0, MinObjectCountPerFloor);
            MaxObjectCountPerFloor = Mathf.Max(
                MinObjectCountPerFloor,
                MaxObjectCountPerFloor);
        }
    }
}
