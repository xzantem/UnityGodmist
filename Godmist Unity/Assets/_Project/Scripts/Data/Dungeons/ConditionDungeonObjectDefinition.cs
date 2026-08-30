using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "ConditionDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Condition")]
    public class ConditionDungeonObjectDefinition : DungeonObjectDefinition
    {
        public override DungeonObjectType ObjectType => DungeonObjectType.Condition;
    }
}
