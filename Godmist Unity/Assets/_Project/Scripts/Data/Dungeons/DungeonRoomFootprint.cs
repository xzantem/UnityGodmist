using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public class DungeonRoomFootprint
    {
        [field: SerializeField]
        public List<Vector2Int> FloorCellOffsets { get; private set; } = new();

        public bool UsesFullBoundingBox =>
            FloorCellOffsets == null || FloorCellOffsets.Count == 0;

        public void Validate(Vector2Int sizeInCells, string templateId)
        {
            if (sizeInCells.x < 1 || sizeInCells.y < 1)
            {
                throw new InvalidOperationException(
                    $"Szablon '{templateId}' ma nieprawidlowy rozmiar pokoju.");
            }

            if (UsesFullBoundingBox) return;

            var uniqueOffsets = new HashSet<Vector2Int>();

            foreach (var offset in FloorCellOffsets)
            {
                if (offset.x < 0
                    || offset.y < 0
                    || offset.x >= sizeInCells.x
                    || offset.y >= sizeInCells.y)
                {
                    throw new InvalidOperationException(
                        $"Footprint szablonu '{templateId}' zawiera komorke "
                        + $"({offset.x},{offset.y}) poza rozmiarem "
                        + $"{sizeInCells.x}x{sizeInCells.y}.");
                }

                if (!uniqueOffsets.Add(offset))
                {
                    throw new InvalidOperationException(
                        $"Footprint szablonu '{templateId}' zawiera "
                        + $"zduplikowana komorke ({offset.x},{offset.y}).");
                }
            }
        }

        public bool Contains(
            Vector2Int localCell,
            Vector2Int sizeInCells)
        {
            if (localCell.x < 0
                || localCell.y < 0
                || localCell.x >= sizeInCells.x
                || localCell.y >= sizeInCells.y)
            {
                return false;
            }

            return UsesFullBoundingBox || FloorCellOffsets.Contains(localCell);
        }

        public IEnumerable<Vector2Int> Enumerate(
            Vector2Int sizeInCells)
        {
            if (UsesFullBoundingBox)
            {
                for (var x = 0; x < sizeInCells.x; x++)
                {
                    for (var y = 0; y < sizeInCells.y; y++)
                    {
                        yield return new Vector2Int(x, y);
                    }
                }

                yield break;
            }

            foreach (var offset in FloorCellOffsets)
            {
                yield return offset;
            }
        }
    }
}
