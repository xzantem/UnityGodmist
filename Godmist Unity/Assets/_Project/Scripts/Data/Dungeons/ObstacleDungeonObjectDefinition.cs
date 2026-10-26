using Godmist.Core.Enums;
using System;
using System.Collections.Generic;
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

        [field: SerializeField] public Sprite[] PresentationVariants { get; private set; } = Array.Empty<Sprite>();
        [field: SerializeField] public bool NativePixelScale { get; private set; }
        [field: SerializeField] public Sprite PresentationSprite { get; private set; }
        [field: SerializeField] public Color PresentationTint { get; private set; } = Color.white;
        [field: SerializeField, Min(.1f)] public float PresentationWidth { get; private set; } = 1.5f;
        // Zero preserves the existing prefab footprint. Positive values opt
        // into a grounded circular trunk instead of a full-cell physical box.
        [field: SerializeField, Min(0)] public float TrunkRadius { get; private set; }

        [field: SerializeField] public bool IsDestructible { get; private set; }
        [field: SerializeField, Min(1)] public int MaxHits { get; private set; } = 3;

        public override DungeonObjectType ObjectType => DungeonObjectType.Obstacle;

        // The anchor is the south-west cell; occupied cells, navigation and
        // the physical collider agree on the full grounded footprint.
        public Vector2Int SizeInCells => string.Equals(Id, "object.obstacle.sarcophagus", StringComparison.Ordinal)
            ? new Vector2Int(1, 2)
            : string.Equals(Id, "object.forest.tree.prototype", StringComparison.Ordinal)
                ? new Vector2Int(2, 2) : Vector2Int.one;

        public Vector2Int PlacementReservationSizeInCells => SizeInCells;

        public IEnumerable<Vector2Int> EnumeratePlacementReservationCells(Vector2Int position)
        {
            var size = PlacementReservationSizeInCells;
            for (var y = 0; y < size.y; y++)
                for (var x = 0; x < size.x; x++)
                    yield return position + new Vector2Int(x, y);
        }

        public IEnumerable<Vector2Int> EnumerateOccupiedCells(Vector2Int position)
        {
            var size = SizeInCells;
            for (var y = 0; y < size.y; y++)
                for (var x = 0; x < size.x; x++)
                    yield return position + new Vector2Int(x, y);
        }
    }
}
