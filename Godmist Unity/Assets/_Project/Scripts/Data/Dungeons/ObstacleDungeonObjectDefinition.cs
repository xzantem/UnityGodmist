using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "ObstacleDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Obstacle")]
    public class ObstacleDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField]
        public bool BlocksMovement { get; private set; } = true;

        [field: SerializeField]
        public bool BlocksLineOfSight { get; private set; }

        [field: SerializeField] public bool IsDestructible { get; private set; }
        [field: SerializeField, Min(1)] public int MaxHits { get; private set; } = 3;

        public override DungeonObjectType ObjectType => DungeonObjectType.Obstacle;
    }
}