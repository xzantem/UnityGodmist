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

        [field: SerializeField]
        public List<EncounterDefinition> EncounterDefinitions { get; private set; } = new();
    }
}