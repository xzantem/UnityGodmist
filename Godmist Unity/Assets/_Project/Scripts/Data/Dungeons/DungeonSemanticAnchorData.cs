using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public class DungeonSemanticAnchorData
    {
        [field: SerializeField]
        public string Id { get; private set; } = string.Empty;

        [field: SerializeField]
        public DungeonSemanticAnchorType Type { get; private set; }

        [field: SerializeField]
        public Vector2Int LocalCell { get; private set; }

        [field: SerializeField]
        public Vector2Int AreaSize { get; private set; } = Vector2Int.one;

        [field: SerializeField, Range(0f, 1f)]
        public float SpawnChance { get; private set; } = 1f;

        [field: SerializeField]
        public bool IsRequired { get; private set; }

        [field: SerializeField]
        public string RequiresAnchorId { get; private set; } = string.Empty;

        [field: SerializeField]
        public string ActivationGroupId { get; private set; } = string.Empty;

        public DungeonSemanticAnchorData()
        {
        }

        public DungeonSemanticAnchorData(
            string id,
            DungeonSemanticAnchorType type,
            Vector2Int localCell,
            Vector2Int areaSize,
            float spawnChance = 1f,
            bool isRequired = false,
            string requiresAnchorId = null,
            string activationGroupId = null)
        {
            Id = id ?? string.Empty;
            Type = type;
            LocalCell = localCell;
            AreaSize = areaSize;
            SpawnChance = spawnChance;
            IsRequired = isRequired;
            RequiresAnchorId = requiresAnchorId ?? string.Empty;
            ActivationGroupId = activationGroupId ?? string.Empty;
        }

        public void Normalize()
        {
            AreaSize = new Vector2Int(
                Mathf.Max(1, AreaSize.x),
                Mathf.Max(1, AreaSize.y));
            SpawnChance = Mathf.Clamp01(SpawnChance);

            if (Type != DungeonSemanticAnchorType.Trap)
            {
                ActivationGroupId = string.Empty;
            }
        }
    }
}
