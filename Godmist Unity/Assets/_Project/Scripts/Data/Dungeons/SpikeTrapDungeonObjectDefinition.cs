using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "SpikeTrapDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Spike Trap")]
    public class SpikeTrapDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField, Min(1)]
        public int Damage { get; private set; } = 10;

        public override DungeonObjectType ObjectType => DungeonObjectType.SpikeTrap;
    }
}
