using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "ChestDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Chest")]
    public class ChestDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField]
        public LootTableData LootTable { get; private set; }

        public override DungeonObjectType ObjectType => DungeonObjectType.Chest;
    }
}