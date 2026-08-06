using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    public abstract class DungeonObjectDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField]
        public string Id { get; private set; } = string.Empty;

        [field: SerializeField]
        public string DisplayName { get; private set; } = string.Empty;

        [field: SerializeField, Min(1)]
        public int SpawnWeight { get; private set; } = 1;

        public abstract DungeonObjectType ObjectType { get; }
    }
}