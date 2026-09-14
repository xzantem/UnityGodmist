using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public class DungeonStructuralAnchorData
    {
        [field: SerializeField]
        public DungeonStructuralAnchorType Type { get; private set; }

        [field: SerializeField]
        public Vector2Int LocalCell { get; private set; }

        [field: SerializeField]
        public DungeonEdgeDirection Direction { get; private set; }

        [field: SerializeField]
        public DungeonWallType WallType { get; private set; } = DungeonWallType.Full;

        [field: SerializeField] public int Level { get; private set; }

        public DungeonStructuralAnchorData()
        {
        }

        public DungeonStructuralAnchorData(
            DungeonStructuralAnchorType type,
            Vector2Int localCell,
            DungeonEdgeDirection direction, DungeonWallType wallType = DungeonWallType.Full, int level = 0)
        {
            Type = type;
            WallType = wallType;
            LocalCell = localCell;
            Level = level;
            Direction = direction;
        }

        public Vector2Int GetDirectionOffset()
        {
            return Direction switch
            {
                DungeonEdgeDirection.Up => Vector2Int.up,
                DungeonEdgeDirection.Right => Vector2Int.right,
                DungeonEdgeDirection.Down => Vector2Int.down,
                DungeonEdgeDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }
    }
}