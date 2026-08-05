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
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public DungeonTheme Theme { get; private set; }
        [field: SerializeField] public bool IsProcedural { get; private set; }
        [field: SerializeField] public bool IsStoryDungeon { get; private set; }
        [field: SerializeField] public DungeonLayoutProfile LayoutProfile { get; private set; }

        [field: Header("Procedural generation")]
        [field: SerializeField, Min(1)] public int MinFloorCount { get; private set; } = 1;

        [field: SerializeField, Min(1)] public int MaxFloorCount { get; private set; } = 1;

        [field: SerializeField, Min(3)] public int MinRoomCountPerFloor { get; private set; } = 6;

        [field: SerializeField, Min(3)] public int MaxRoomCountPerFloor { get; private set; } = 9;

        [field: SerializeField, Min(0)] public int MinOptionalRoomCount { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MaxOptionalRoomCount { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MinLoopConnections { get; private set; } = 0;

        [field: SerializeField, Min(0)] public int MaxLoopConnections { get; private set; } = 0;

        [field: Header("Room templates")]
        [field: SerializeField]
        public DungeonRoomTemplateCatalog RoomTemplateCatalog { get; private set; }
        
        [field: SerializeField]
        public List<EncounterDefinition> EncounterDefinitions { get; private set; } = new();

        private void OnValidate()
        {
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
        }
    }
}