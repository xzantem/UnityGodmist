using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class ObstacleDungeonObjectInstance : DungeonObjectInstance
    {
        public bool IsDestructible { get; private set; }
        public int MaxHits { get; private set; }
        public int RemainingHits { get; private set; }
        public bool ApplyHits(int hits)
        {
            if (!IsActive || !IsDestructible || hits <= 0) return false;
            RemainingHits = Mathf.Max(0, RemainingHits - hits);
            if (RemainingHits == 0) TryDeactivate();
            return true;
        }
        public bool RestoreDurability(bool destructible, int maxHits, int remainingHits)
        {
            if (maxHits < 1 || remainingHits < 0 || remainingHits > maxHits) return false;
            IsDestructible = destructible; MaxHits = maxHits; RemainingHits = remainingHits;
            if (destructible && remainingHits == 0) TryDeactivate();
            return true;
        }
        public bool BlocksMovement { get; }
        public bool BlocksLineOfSight { get; }

        public ObstacleDungeonObjectInstance(
            string instanceId,
            ObstacleDungeonObjectDefinition definition,
            Vector2Int position)
            : base(instanceId, definition, position)
        {
            IsDestructible = definition.IsDestructible;
            MaxHits = Mathf.Max(1, definition.MaxHits); RemainingHits = MaxHits;
            BlocksMovement = definition.BlocksMovement;
            BlocksLineOfSight = definition.BlocksLineOfSight;
        }
    }
}