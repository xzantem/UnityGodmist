using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private static readonly Vector2Int[] ForestPathNeighbours = {Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
        public static float ForestPathSegmentDistance(Vector2 sample, Vector2 a, Vector2 b)
        {
            var delta = b-a;
            var t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(sample-a, delta)/delta.sqrMagnitude) : 0;
            return Vector2.Distance(sample, a + delta*t);
        }
        private static Vector2 ForestPathPoint(Vector2Int cell, Vector2Int min)
        {
            var noise = VegetationHash(cell);
            return (Vector2)(cell-min) + Vector2.one*.5f
                + new Vector2(((noise & 255u)/255f-.5f)*.2f, (((noise>>8)&255u)/255f-.5f)*.2f);
        }
        private static float ForestPathDistance(Vector2 point, Vector2 sample, Vector2[] neighbours, int count)
        {
            var distance = Vector2.Distance(point,sample);
            for (var i = 0; i < count; i++) distance = Mathf.Min(distance,
                ForestPathSegmentDistance(sample,point,neighbours[i]));
            return distance;
        }
        private readonly HashSet<Vector2Int> forestVisualPathCells = new();
        private Texture2D forestSurfaceMask;
        private Texture2D forestDifficultTerrainMask;
        private Vector4 forestSurfaceBounds;
        private int forestSurfaceMaskBuildCount;

        private readonly HashSet<Vector2Int> forestPathObstacles = new();

        public void SetForestPathObstacles(IEnumerable<Vector2Int> cells)
        {
            var next = cells == null ? new HashSet<Vector2Int>() : cells.ToHashSet();
            if (forestPathObstacles.SetEquals(next) && forestSurfaceMask != null) return;
            forestPathObstacles.Clear(); forestPathObstacles.UnionWith(next);
            if (boundMap != null && vegetationPresentation != null)
            {
                BuildForestSurfaceMask();
                RebuildPresentationDecorations();
            }
        }

        public void ConfigureForestGroundMaterial(Material material)
        {
            if (!material.HasProperty("_ForestSurfaceEnabled")) return;
            var enabled = forestSurfaceMask != null && vegetationPresentation?.SoilSprite != null
                && vegetationPresentation.GroundTile is Tile;
            material.SetFloat("_ForestSurfaceEnabled", enabled ? 1 : 0);
            if (enabled) material.SetColor("_ForestGroundTint", vegetationPresentation.GroundTint);
            var difficultMask=enabled && forestDifficultTerrainMask!=null ? forestDifficultTerrainMask : Texture2D.blackTexture;
            if(material.GetTexture("_ForestDifficultTerrainMask")!=difficultMask)material.SetTexture("_ForestDifficultTerrainMask",difficultMask);
            if (!enabled || material.GetTexture("_ForestSurfaceMask") == forestSurfaceMask) return;
            material.SetTexture("_ForestSurfaceMask", forestSurfaceMask);
            material.SetVector("_ForestSurfaceBounds", forestSurfaceBounds);
            material.SetTexture("_ForestLitter", ((Tile)vegetationPresentation.GroundTile).sprite.texture);
            material.SetTexture("_ForestSoil", vegetationPresentation.SoilSprite.texture);
            // Lighting may configure this material after the first fog mesh
            // was built. Bind the litter composite in that same frame.
            if (fogBlendRenderer != null && UpdateFogSorting()) UploadFogColors();
        }

        private void BuildForestSurfaceMask()
        {
            if (vegetationPresentation == null || boundMap.FloorCells.Count == 0) return;
            forestSurfaceMaskBuildCount++;
            ReleaseForestDifficultTerrainMask();
            if (forestSurfaceMask != null)
            {
                if (Application.isPlaying) Destroy(forestSurfaceMask); else DestroyImmediate(forestSurfaceMask);
                forestSurfaceMask = null;
            }
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var paths = forestVisualPathCells;
            paths.Clear();
            if (boundMap.VisualPathCells.Count > 0)
                paths.UnionWith(boundMap.VisualPathCells.Where(c => !forestPathObstacles.Contains(c)));
            var rooms = boundMap.FloorCells.GroupBy(c => { boundMap.TryGetRoomId(c, out var id); return id; });
            foreach (var room in boundMap.VisualPathCells.Count > 0 ? Enumerable.Empty<IGrouping<string, Vector2Int>>() : rooms)
            {
                var cells = room.Where(c => !forestPathObstacles.Contains(c)).ToHashSet();
                if (cells.Count == 0) continue;
                var average = new Vector2(cells.Average(c => (float)c.x), cells.Average(c => (float)c.y));
                var center = cells.OrderBy(c => ((Vector2)c - average).sqrMagnitude).ThenBy(c => c.y).ThenBy(c => c.x).First();
                var parents = new Dictionary<Vector2Int, Vector2Int> { [center] = center };
                var queue = new Queue<Vector2Int>(); queue.Enqueue(center);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    // Stable, position-dependent order breaks identical right-angle routes.
                    var turn = (int)(VegetationHash(cell) % 4);
                    for (var i = 0; i < 4; i++)
                    {
                        var next = cell + directions[(i + turn) % 4];
                        if (cells.Contains(next) && boundMap.CanMoveBetween(cell, next, true)
                            && parents.TryAdd(next, cell)) queue.Enqueue(next);
                    }
                }
                var portals = cells.Where(c => directions.Any(d => boundMap.IsWalkable(c + d)
                    && boundMap.CanMoveBetween(c, c + d, true)
                    && !forestPathObstacles.Contains(c + d)
                    && boundMap.TryGetRoomId(c + d, out var neighbourRoom) && neighbourRoom != room.Key)).ToHashSet();
                while (portals.Count > 0)
                {
                    // Treat a wide doorway as one entrance, otherwise one path
                    // per doorway cell fans into a bare rectangular clearing.
                    var first = portals.OrderBy(c => c.y).ThenBy(c => c.x).First();
                    var component = new List<Vector2Int>();
                    var portalQueue = new Queue<Vector2Int>(); portalQueue.Enqueue(first); portals.Remove(first);
                    while (portalQueue.Count > 0)
                    {
                        var cell = portalQueue.Dequeue(); component.Add(cell);
                        foreach (var direction in directions)
                            if (portals.Remove(cell + direction)) portalQueue.Enqueue(cell + direction);
                    }
                    var portalCenter = new Vector2(component.Average(c => (float)c.x), component.Average(c => (float)c.y));
                    var entrance = component.OrderBy(c => ((Vector2)c - portalCenter).sqrMagnitude)
                        .ThenBy(c => c.y).ThenBy(c => c.x).First();
                    var step = entrance;
                    while (parents.TryGetValue(step, out var parent))
                    {
                        paths.Add(step);
                        if (step == parent) break;
                        step = parent;
                    }
                }
            }
            var min = new Vector2Int(boundMap.FloorCells.Min(c => c.x) - 6, boundMap.FloorCells.Min(c => c.y) - 6);
            var max = new Vector2Int(boundMap.FloorCells.Max(c => c.x) + 6, boundMap.FloorCells.Max(c => c.y) + 6);
            var cellWidth = Vector2.Distance(CellToWorldCenter(Vector2Int.zero), CellToWorldCenter(Vector2Int.right));
            var lower = CellToWorldCenter(min) - Vector2.one * cellWidth * .5f;
            var span = (Vector2)(max - min + Vector2Int.one) * cellWidth;
            forestSurfaceBounds = new Vector4(lower.x, lower.y, span.x, span.y);
            if(boundMap.DifficultTerrainCells.Count>0)
            {
                // One texel per logical cell, built only with the static floor
                // mask. The fragment samples this exact gameplay footprint.
                var columns=max.x-min.x+1;var rows=max.y-min.y+1;
                var terrainPixels=new Color32[columns*rows];
                foreach(var cell in boundMap.DifficultTerrainCells)
                    terrainPixels[(cell.y-min.y)*columns+cell.x-min.x]=new Color32(255,0,0,255);
                forestDifficultTerrainMask=new Texture2D(columns,rows,TextureFormat.RGBA32,false,true)
                    {name="Forest difficult terrain (logical cells)",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                forestDifficultTerrainMask.SetPixels32(terrainPixels);forestDifficultTerrainMask.Apply(false,true);
            }
            var resolution = Mathf.Min(8f, 2048f / Mathf.Max(max.x - min.x + 1, max.y - min.y + 1));
            var width = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x + 1) * resolution));
            var height = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y + 1) * resolution));
            var pixels = new Color32[width * height];
            // Bake broad vegetation and ragged edge variation once, not per pixel per frame.
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var point = lower + new Vector2((x + .5f) / width * span.x, (y + .5f) / height * span.y);
                var patch = (byte)(255 * Mathf.PerlinNoise(point.x * .24f + 41.7f, point.y * .24f + 73.2f));
                var edge = (byte)(255 * Mathf.PerlinNoise(point.x * 1.4f + 19.3f, point.y * 1.4f + 37.5f));
                pixels[y * width + x] = new Color32(0, patch, edge, 255);
            }
            // Geometry is constant throughout this bake. Resolve clearance per
            // cell/segment once, rather than repeating map queries per texel.
            var blockedEdges = new Dictionary<Vector2Int, byte>();
            foreach (var cell in boundMap.FloorCells)
            {
                byte bits = 0;
                for (var i = 0; i < directions.Length; i++)
                    if (!boundMap.CanMoveBetween(cell, cell + directions[i], true)) bits |= (byte)(1 << i);
                blockedEdges.Add(cell, bits);
            }
            var nearbyRoots = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (var obstacle in forestPathObstacles)
                for (var oy = -1; oy <= 1; oy++) for (var ox = -1; ox <= 1; ox++)
                {
                    var sampleCell = obstacle + new Vector2Int(ox, oy);
                    if (!blockedEdges.ContainsKey(sampleCell)) continue;
                    if (!nearbyRoots.TryGetValue(sampleCell, out var roots)) nearbyRoots[sampleCell] = roots = new();
                    roots.Add((Vector2)(obstacle - min) + Vector2.one * .5f);
                }
            var neighbourPoints = new Vector2[4];
            foreach (var cell in paths)
            {
                var neighbourCount = 0;
                foreach (var direction in ForestPathNeighbours)
                    if (paths.Contains(cell + direction) && boundMap.CanMoveBetween(cell, cell + direction, true))
                        neighbourPoints[neighbourCount++] = ForestPathPoint(cell + direction, min);
                var noise = VegetationHash(cell);
                var point = (Vector2)(cell - min) + Vector2.one * .5f
                    + new Vector2(((noise & 255u) / 255f - .5f) * .2f,
                        (((noise >> 8) & 255u) / 255f - .5f) * .2f);
                var widthNoise = Mathf.PerlinNoise(cell.x * .37f + 13.2f, cell.y * .37f + 29.1f);
                var radius = .36f + widthNoise * .22f;
                if (directions.Count(d => paths.Contains(cell + d)) > 2) radius += .1f;
                var x0 = Mathf.Max(0, Mathf.FloorToInt((point.x - 1.1f) * resolution));
                var x1 = Mathf.Min(width - 1, Mathf.CeilToInt((point.x + 1.1f) * resolution));
                var y0 = Mathf.Max(0, Mathf.FloorToInt((point.y - 1.1f) * resolution));
                var y1 = Mathf.Min(height - 1, Mathf.CeilToInt((point.y + 1.1f) * resolution));
                for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++)
                {
                    var sample = new Vector2((x + .5f) / resolution, (y + .5f) / resolution);
                    var value = (byte)(255 * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * .4f, radius + .16f, ForestPathDistance(point, sample, neighbourPoints, neighbourCount)))));
                    var sampleCell = min + new Vector2Int(Mathf.FloorToInt(sample.x), Mathf.FloorToInt(sample.y));
                    // Underlay extends beyond walkable ground. Never advertise
                    // a route across an invisible boundary or a solid wall.
                    if (!blockedEdges.TryGetValue(sampleCell, out var blocked)) continue;
                    // Fade around roots in circles instead of cutting a square
                    // hole from the path at the occupied cell boundary.
                    var clearance = 1f;
                    var localSample = sample - (Vector2)(sampleCell-min) - Vector2.one*.5f;
                    for (var i = 0; i < directions.Length; i++)
                        if ((blocked & (1 << i)) != 0)
                            clearance = Mathf.Min(clearance, Mathf.SmoothStep(0, 1,
                                Mathf.InverseLerp(.08f, .32f, .5f - Vector2.Dot(localSample, directions[i]))));
                    if (nearbyRoots.TryGetValue(sampleCell, out var roots))
                        foreach (var root in roots)
                            clearance = Mathf.Min(clearance, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, .8f, Vector2.Distance(sample, root))));
                    value = (byte)(value * clearance);
                    var index = y * width + x;
                    if (value > pixels[index].r) pixels[index].r = value;
                }
            }
            // Alpha is a static contact shade, independent of path clearance.
            // Small elliptical bases ground props without extra sprite renderers.
            foreach (var cell in forestPathObstacles)
            {
                var point = (Vector2)(cell - min) + new Vector2(.5f, .58f);
                var x0 = Mathf.Max(0, Mathf.FloorToInt((point.x - .65f) * resolution));
                var x1 = Mathf.Min(width - 1, Mathf.CeilToInt((point.x + .65f) * resolution));
                var y0 = Mathf.Max(0, Mathf.FloorToInt((point.y - .32f) * resolution));
                var y1 = Mathf.Min(height - 1, Mathf.CeilToInt((point.y + .32f) * resolution));
                for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++)
                {
                    var delta = new Vector2((x + .5f) / resolution, (y + .5f) / resolution) - point;
                    var distance = new Vector2(delta.x / .65f, delta.y / .32f).magnitude;
                    var shade = (byte)(255 * Mathf.Lerp(.7f, 1, Mathf.SmoothStep(0, 1, distance)));
                    var index = y * width + x;
                    // Overlapping bases cannot accumulate into black holes.
                    if (shade < pixels[index].a) pixels[index].a = shade;
                }
            }
            forestSurfaceMask = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { name = "Forest paths (presentation only)", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            forestSurfaceMask.SetPixels32(pixels); forestSurfaceMask.Apply(false, true);
            BuildForestBackdrop();
        }
        private void ReleaseForestDifficultTerrainMask()
        {
            if(forestDifficultTerrainMask==null)return;
            if(Application.isPlaying)Destroy(forestDifficultTerrainMask);else DestroyImmediate(forestDifficultTerrainMask);
            forestDifficultTerrainMask=null;
        }
    }
}
