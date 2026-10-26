using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Use the rendered trunk anchor, never the crown bounds or the logical
        // wall centre. Enumerated once when the lighting rig is built.
        public IEnumerable<(Vector2 position, DungeonSurfacePosition cell, SpriteRenderer trunk)> ForestTorchAnchors()
        {
            if (!UsesVegetationPresentation || boundMap == null) yield break;
            foreach (var pair in wallRenderers.OrderBy(p => p.Edge.FirstCell.y).ThenBy(p => p.Edge.FirstCell.x))
            {
                var edge = pair.Edge;
                if (edge.Level != 0 || pair.View == null || !vegetationRenderers.TryGetValue(pair.View, out var trees)) continue;
                var cell = boundMap.IsWalkable(edge.FirstCell) ? edge.FirstCell : edge.SecondCell;
                if (!boundMap.IsWalkable(cell)) continue;
                var centre = CellToWorldCenter(cell);
                var trunk = trees.OfType<SpriteRenderer>().Where(r => r != null && r.sprite != null)
                    .OrderBy(r => ((Vector2)r.transform.position - centre).sqrMagnitude).FirstOrDefault();
                if (trunk != null) yield return ((Vector2)trunk.transform.position, new DungeonSurfacePosition(cell), trunk);
            }
        }
    }
}
