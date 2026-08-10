using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DoorDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Door")]
    public class DoorDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField]
        public DoorInteractionMode InteractionMode { get; private set; }
            = DoorInteractionMode.DirectOnly;

        [field: SerializeField]
        public bool StartsOpen { get; private set; }

        public override DungeonObjectType ObjectType => DungeonObjectType.Door;
    }
}