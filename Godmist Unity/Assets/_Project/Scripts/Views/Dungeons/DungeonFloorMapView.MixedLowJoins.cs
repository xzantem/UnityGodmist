using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Reduced Side profiles need a standing height transition. A South
        // profile instead shares the low wall's projected roof height, and
        // must participate in that roof's corner without adding a taller face.
        private void ConfigureMixedMasonryJunctions()
        {
            var full = wallRenderers.Where(pair => pair.View != null
                && boundMap.GetWallType(pair.Edge) == DungeonWallType.Full).ToDictionary(pair => pair.Edge, pair => pair.View);
            foreach (var pair in full)
                if (progressionWallRefresh == null || progressionWallRefresh.Contains(pair.Value))
                    pair.Value.BeginMixedLowWallJunctions();
            var vertices = new HashSet<(int Level, Vector2Int Point)>();
            foreach (var pair in wallRenderers)
            {
                if (boundMap.GetWallType(pair.Edge) != DungeonWallType.Low) continue;
                GetEdgeEndpoints(pair.Edge.CellEdge, out var first, out var second);
                vertices.Add((pair.Edge.Level, first)); vertices.Add((pair.Edge.Level, second));
            }
            foreach (var vertex in vertices)
            {
                var adjacent = WallsAtEndpoint(vertex.Level, vertex.Point).ToArray();
                var candidates = adjacent.Where(edge => full.ContainsKey(edge)).ToArray();
                if (candidates.Length == 0) continue;
                var lowArms = 0; var sideArms = 0; var southArms = 0; var northArms = 0;
                foreach (var edge in adjacent)
                {
                    GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
                    var other = first == vertex.Point ? second : first;
                    var arm = other.x < vertex.Point.x ? 1 : other.x > vertex.Point.x ? 2 : other.y < vertex.Point.y ? 4 : 8;
                    if (boundMap.GetWallType(edge) == DungeonWallType.Low) lowArms |= arm;
                    else if (full.ContainsKey(edge))
                        switch (ResolveWallSurfaceProfile(edge))
                        {
                            case DungeonWallSurfaceProfile.Side: sideArms |= arm; break;
                            case DungeonWallSurfaceProfile.South: southArms |= arm; break;
                            case DungeonWallSurfaceProfile.North: northArms |= arm; break;
                        }
                }
                // A standing North profile without South keeps its existing
                // native join. Other mixtures are composed by projected roof
                // level, not discarded because their full profiles differ.
                if (northArms != 0 && southArms == 0) continue;
                var clipMinX = float.NegativeInfinity;
                var clipMaxX = float.PositiveInfinity;
                foreach (var northEdge in candidates.Where(edge => ResolveWallSurfaceProfile(edge) == DungeonWallSurfaceProfile.North))
                {
                    var faceBounds = full[northEdge].GetComponent<SpriteRenderer>().bounds;
                    GetEdgeEndpoints(northEdge.CellEdge, out var a, out var b);
                    var other = a == vertex.Point ? b : a;
                    if (other.x < vertex.Point.x) clipMinX = Mathf.Max(clipMinX, faceBounds.max.x);
                    else clipMaxX = Mathf.Min(clipMaxX, faceBounds.min.x);
                    foreach (var southEdge in candidates.Where(edge => ResolveWallSurfaceProfile(edge) == DungeonWallSurfaceProfile.South))
                        if (progressionWallRefresh == null || progressionWallRefresh.Contains(full[southEdge]))
                            full[southEdge].TrimSouthOcclusionAtStandingFace(faceBounds);
                }
                var point = (Vector2)floorTilemap.CellToWorld(new Vector3Int(vertex.Point.x, vertex.Point.y, 0));
                // Preserve the taller wall's south-facing height transition.
                // Its roof starts above the full face, not at the low roof.
                foreach (var edge in candidates)
                {
                    GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
                    var other = first == vertex.Point ? second : first;
                    if (other.y > vertex.Point.y && (progressionWallRefresh == null || progressionWallRefresh.Contains(full[edge])))
                        full[edge].ClipSideAtRaisedLowWall(point.y + DungeonWallSegmentView.NativeFullWallCapCenterHeight);
                }
                var mixedSouth = southArms != 0 && (sideArms != 0 || northArms != 0);
                var owner = candidates.OrderBy(edge => mixedSouth && depthSortOrders.TryGetValue(full[edge], out var order) ? -order : 0)
                    .ThenBy(edge =>
                    {
                        GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
                        return (first == vertex.Point ? second : first).y > vertex.Point.y ? 0 : 1;
                    }).ThenBy(edge => edge.FirstCell.x).ThenBy(edge => edge.FirstCell.y)
                    .ThenBy(edge => edge.SecondCell.x).ThenBy(edge => edge.SecondCell.y).First();
                if (progressionWallRefresh != null && !progressionWallRefresh.Contains(full[owner])) continue;
                full[owner].ConfigureMixedLowWallJunction(wallSegmentPrefab.SideWallSprite, wallSegmentPrefab.NorthWallSprite,
                    point, lowArms, sideArms, southArms, northArms, clipMinX, clipMaxX);
            }
        }
    }
}
