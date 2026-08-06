using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class ObstacleDungeonObjectInstance : DungeonObjectInstance
    {
        public bool BlocksMovement { get; }
        public bool BlocksLineOfSight { get; }

        public ObstacleDungeonObjectInstance(
            string instanceId,
            ObstacleDungeonObjectDefinition definition,
            Vector2Int position)
            : base(instanceId, definition, position)
        {
            BlocksMovement = definition.BlocksMovement;
            BlocksLineOfSight = definition.BlocksLineOfSight;
        }
    }
}