using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "LeverDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Lever")]
    public class LeverDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField]
        public bool IsReversible { get; private set; } = true;

        public override DungeonObjectType ObjectType => DungeonObjectType.Lever;
    }
}
