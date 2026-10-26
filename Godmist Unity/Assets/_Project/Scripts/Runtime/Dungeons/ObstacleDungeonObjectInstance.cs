using System;
using System.Collections.Generic;
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
        public Vector2Int OccupiedSizeInCells => ((ObstacleDungeonObjectDefinition)Definition).SizeInCells;

        /// <summary>
        /// Selected Catacombs obstacles are authored as one logical combat
        /// cell while their exploration collider uses a presentation
        /// footprint. Tactical occupancy remains cell based; exploration may
        /// use the spawned collider for this footprint.
        /// </summary>
        public bool UsesPhysicalExplorationFootprint
            => (OccupiedSizeInCells == Vector2Int.one && Definition is ObstacleDungeonObjectDefinition { TrunkRadius: > 0 })
                || string.Equals(Definition?.Id, "object.obstacle.rubble", StringComparison.Ordinal)
                || string.Equals(Definition?.Id, "object.obstacle.debug", StringComparison.Ordinal);

        /// <summary>
        /// Returns the footprint shared by placement, exploration and combat.
        /// </summary>
        public IEnumerable<Vector2Int> EnumerateOccupiedCells()
            => ((ObstacleDungeonObjectDefinition)Definition).EnumerateOccupiedCells(Position);

        /// <summary>
        /// Returns cells touched by the exploration collider.
        /// </summary>
        public IEnumerable<Vector2Int> EnumeratePhysicalExplorationCells()
            => EnumerateOccupiedCells();

        public bool OccupiesPhysicalExplorationCell(Vector2Int cell)
            => OccupiesCell(cell);

        public bool OccupiesCell(Vector2Int cell)
        {
            if (!IsActive) return false;
            var offset = cell - Position;
            var size = OccupiedSizeInCells;
            return offset.x >= 0 && offset.y >= 0 && offset.x < size.x && offset.y < size.y;
        }

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
