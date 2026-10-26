using System.Collections.Generic;
using Godmist.Views.Dungeons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Views.Lighting
{
    // One batched floor overlay. No texture blur or additional light sources.
    internal sealed class DungeonContactShadows
    {
        private const float Pixel = 1f / 32;
        private static readonly Unity.Profiling.ProfilerMarker RebuildSample = new("Godmist.Lighting.ContactShadows");
        private static readonly Unity.Profiling.ProfilerMarker[] PhaseSamples = {
            new("Godmist.Lighting.Contact.Walls"), new("Godmist.Lighting.Contact.Objects"),
            new("Godmist.Lighting.Contact.Ground"), new("Godmist.Lighting.Contact.Forest"),
            new("Godmist.Lighting.Contact.Transitions"), new("Godmist.Lighting.Contact.Reuse"),
            new("Godmist.Lighting.Contact.Project"), new("Godmist.Lighting.Contact.Upload") };
        // Optional diagnostic clock. Disabled during normal gameplay; timings
        // are disjoint phases of one call, never sums from separate frames.
        public bool MeasurePhaseTimings { get; set; }
        public long[] LastPhaseTicks { get; } = new long[8];
        public long LastTotalTicks { get; private set; }
        private readonly struct PhaseTiming : System.IDisposable
        {
            private readonly DungeonContactShadows owner;
            private readonly int phase;
            private readonly long started;
            private readonly Unity.Profiling.ProfilerMarker.AutoScope marker;
            public PhaseTiming(DungeonContactShadows owner, int phase)
            {
                this.owner = owner; this.phase = phase;
                started = owner.MeasurePhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                marker = phase >= 0 ? PhaseSamples[phase].Auto() : default;
            }
            public void Dispose()
            {
                if (phase >= 0) marker.Dispose();
                if (started == 0) return;
                var ticks = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                if (phase < 0) owner.LastTotalTicks = ticks;
                else owner.LastPhaseTicks[phase] += ticks;
            }
        }
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly MeshRenderer renderer;
        private readonly List<Vector3> vertices = new();
        private readonly List<Color32> colors = new();
        private readonly List<int> triangles = new();
        private readonly HashSet<Rect> emitted = new();
        private readonly HashSet<Rect> projectedBands = new();
        public int BandProjectionCount { get; private set; }
        public int NativeCornerProjectionCount { get; private set; }
        public int UnitCornerProjectionCount { get; private set; }
        private bool unitWorldCellProjection;
        private readonly Dictionary<Vector2Int, (Vector3 Center, bool Visible)> contactCells = new();
        private readonly HashSet<(Vector2 Foot, float Width)> forestContactSources = new();
        private sealed class ForestSourceBands
        {
            public Vector2 Foot;
            public float Width;
            public int Seen;
            public readonly List<(Rect Rect, byte Opacity)> Bands = new();
        }
        private readonly Dictionary<SpriteRenderer, ForestSourceBands> forestSourceBands = new();
        private readonly List<SpriteRenderer> expiredForestSources = new();
        private int forestSourceRevision;
        public int ForestBandTranslationCount { get; private set; }
        internal System.Func<SpriteRenderer, Bounds> BoundsSnapshot { get; set; }
        public int ForestContactSourceCount => forestContactSources.Count;
        private readonly Dictionary<float, List<(Rect Rect, byte Opacity)>> forestBandTemplates = new();
        public int CellVisibilityQueryCount { get; private set; }
        public int ForestTemplateBuildCount { get; private set; }
        private readonly Dictionary<DungeonObjectView, SpriteRenderer[]> objectRenderers = new();
        private readonly List<SpriteRenderer> localWalls = new();
        private readonly List<KeyValuePair<SpriteRenderer, Vector2>> localForestContacts = new();
        private DungeonFloorMapView terrain;
        private Rect viewport;
        private readonly List<(Rect Rect, byte Opacity)> bands = new();
        private readonly List<(Rect Rect, byte Opacity)> previousBands = new();
        private DungeonFloorMapView previousMap;
        private Rect previousViewport;
        private int previousPresentation = -1;
        private int previousWallBandCount;
        private Godmist.Runtime.Dungeons.DungeonFloorMap previousTopology;
        private int previousTopologyRevision = -1;
        public int RebuildCount { get; private set; }
        public int ContactVisibilityReuseCount { get; private set; }
        private static readonly Unity.Profiling.ProfilerMarker VisibilityReuseSample = new("Godmist.Lighting.ContactVisibilityReuse");
        public int QuadCount => vertices.Count / 4;
        public int WallQuadCount { get; private set; }

        public DungeonContactShadows(Transform owner, Shader shader)
        {
            root = new GameObject("Pixel contact shadows", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(owner, false);
            // Mesh vertices are world coordinates, independent of owner transform.
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            mesh = new Mesh { name = "Pixel contact shadows", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            material = new Material(shader);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Rebuild(DungeonFloorMapView map, Renderer floor, Rect cameraBounds,
            IEnumerable<DungeonObjectView> objects, IEnumerable<SpriteRenderer> transitions, Sprite pillar,
            IEnumerable<SpriteRenderer> groundObjects = null)
        {
            using var sample = RebuildSample.Auto();
            if (MeasurePhaseTimings) { System.Array.Clear(LastPhaseTicks, 0, LastPhaseTicks.Length); LastTotalTicks = 0; }
            using var totalTiming = new PhaseTiming(this, -1);
            using (new PhaseTiming(this, 0))
            {
                terrain = map; viewport = cameraBounds;
                unitWorldCellProjection = terrain.HasUnitWorldCellProjection();
                bands.Clear();
                forestContactSources.Clear();
                forestSourceRevision++;
                // Visible() includes a one-tenth-unit fringe; query conservatively
                // so a contact at a viewport/bucket edge is never omitted.
                IEnumerable<SpriteRenderer> walls = map.LightingWallRenderers;
                if (map.UsesVegetationPresentation)
                {
                    map.CollectLightingWallRenderers(Rect.MinMaxRect(cameraBounds.xMin - .1f,
                        cameraBounds.yMin - .1f, cameraBounds.xMax + .1f, cameraBounds.yMax + .1f), localWalls);
                    walls = localWalls;
                }
                foreach (var sprite in walls)
                {
                    // Joins/forest crowns have no wall profile. Reject them before
                    // native renderer state/bounds queries; their contacts have
                    // a separate source pass below.
                    if (sprite == null || !sprite.TryGetComponent<DungeonWallSegmentView>(out var wall)) continue;
                    if (!Visible(sprite, out var b)) continue;
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side)
                    {
                        Add(new Rect(b.min.x-Pixel,b.min.y,Pixel,b.size.y), 80);
                        Add(new Rect(b.min.x-2*Pixel,b.min.y,Pixel,b.size.y), 35);
                        Add(new Rect(b.max.x,b.min.y,Pixel,b.size.y), 80);
                        Add(new Rect(b.max.x+Pixel,b.min.y,Pixel,b.size.y), 35);
                    }
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North)
                    {
                        Add(new Rect(b.min.x,b.min.y-Pixel,b.size.x,Pixel), 90);
                        Add(new Rect(b.min.x,b.min.y-2*Pixel,b.size.x,Pixel), 48);
                    }
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South)
                    {
                        Add(new Rect(b.min.x,b.max.y,b.size.x,Pixel), 80);
                        Add(new Rect(b.min.x,b.max.y+Pixel,b.size.x,Pixel), 35);
                    }
                }
            }
            var wallBandCount = bands.Count;
            using (new PhaseTiming(this, 1))
            {
                if (objects != null && (pillar != null || map.UsesVegetationPresentation))
                    foreach (var view in objects)
                    {
                        if (view == null || (!map.UsesVegetationPresentation && view is not DungeonObstacleView)
                            || !(map.UsesVegetationPresentation ? view.IsLightingPresentationVisible : view.IsElevationVisible)) continue;
                        if (!objectRenderers.TryGetValue(view,out var renderers))
                            objectRenderers.Add(view, renderers = view.GetComponentsInChildren<SpriteRenderer>());
                        foreach (var sprite in renderers)
                        {
                            if (!Visible(sprite, out var b)) continue;
                            if (map.UsesVegetationPresentation)
                            {
                                AddForestContactForSource(sprite, (Vector2)view.transform.position, b.size.x);
                                continue;
                            }
                            if (sprite.sprite != pillar) continue;
                            // Native column alpha footprint: x=9..23, bottom=5, base=5 px.
                            var foot = new Rect(b.min.x+9*Pixel,b.min.y+5*Pixel,15*Pixel,5*Pixel);
                            Add(new Rect(foot.xMin,foot.yMin-Pixel,foot.width,Pixel), 77);
                            Add(new Rect(foot.xMin,foot.yMin-2*Pixel,foot.width,Pixel), 35);
                            Add(new Rect(foot.xMin-Pixel,foot.yMin,Pixel,foot.height), 35);
                            Add(new Rect(foot.xMax,foot.yMin,Pixel,foot.height), 35);
                        }
                    }
            }
            using (new PhaseTiming(this, 2))
            {
                if (groundObjects != null)
                    foreach (var sprite in groundObjects)
                    {
                        if (!Visible(sprite, out var b)) continue;
                        if (map.UsesVegetationPresentation)
                        {
                            AddForestContactForSource(sprite, (Vector2)sprite.transform.position, b.size.x);
                            continue;
                        }
                        // A low floor prop needs only a one-pixel contact band at
                        // its authored base. Do not project its full silhouette;
                        // that would make a lever read as a tall wall object.
                        float width = Mathf.Clamp(b.size.x * .55f, 6f * Pixel, 22f * Pixel);
                        float left = b.center.x - width * .5f;
                        Add(new Rect(left, b.min.y - Pixel, width, Pixel), 52);
                        Add(new Rect(left + Pixel, b.min.y - 2f * Pixel,
                            Mathf.Max(Pixel, width - 2f * Pixel), Pixel), 22);
                    }
            }
            using (new PhaseTiming(this, 3))
            {
                if (map.UsesVegetationPresentation)
                {
                    map.CollectForestPropContacts(Rect.MinMaxRect(cameraBounds.xMin - .1f, cameraBounds.yMin - .1f,
                        cameraBounds.xMax + .1f, cameraBounds.yMax + .1f), localForestContacts);
                    foreach (var prop in localForestContacts)
                        if (Visible(prop.Key, out var b)) AddForestContactForSource(prop.Key, prop.Value, b.size.x);
                }
            }
            using (new PhaseTiming(this, 4))
            {
                foreach (var sprite in transitions)
                {
                    if (!Visible(sprite, out var b)) continue;
                    // Ground stairs occupy 32x28 px inside their 32x32 canvas.
                    var lip = new Rect(b.min.x,b.min.y+2*Pixel,b.size.x,b.size.y-4*Pixel);
                    Add(new Rect(lip.xMin,lip.yMin-Pixel,lip.width,Pixel), 66);
                    // Only the near lip touches here: the recessed opening tapers
                    // inward at the far end, so a full rectangle would float beside it.
                    Add(new Rect(lip.xMin-Pixel,lip.yMin,Pixel,4*Pixel), 30);
                    Add(new Rect(lip.xMax,lip.yMin,Pixel,4*Pixel), 30);
                }
            }
            bool unchanged;
            using (new PhaseTiming(this, 5))
            {
                renderer.sortingLayerID = floor.sortingLayerID;
                // Opaque litter on the forest fog fringe must be drawn before
                // contacts. Equal orders made mesh bounds decide which erased which.
                renderer.sortingOrder = floor.sortingOrder + map.GroundContactSortingOffset;
                expiredForestSources.Clear();
                foreach (var pair in forestSourceBands)
                    if (pair.Key == null || pair.Value.Seen != forestSourceRevision) expiredForestSources.Add(pair.Key);
                foreach (var source in expiredForestSources) forestSourceBands.Remove(source);
                // Torch flicker does not change contact geometry. Compare the actual
                // visible source bands, including moving/disappearing props, before
                // clipping them to traversal cells or uploading a new mesh.
                unchanged = previousMap == map && previousViewport.Equals(viewport)
                    && bands.Count == previousBands.Count
                    && wallBandCount == previousWallBandCount
                    && ReferenceEquals(previousTopology, map.LightingMap)
                    && previousTopologyRevision == (map.LightingMap?.TraversalRevision ?? -1);
                for (var i = 0; unchanged && i < bands.Count; i++)
                    unchanged = bands[i].Equals(previousBands[i]);
                if (unchanged && previousPresentation != map.PresentationRevision)
                {
                    using var visibilitySample = VisibilityReuseSample.Auto();
                    unchanged = ContactVisibilityIsCurrent();
                    if (unchanged) ContactVisibilityReuseCount++;
                }
            }
            if (unchanged) { previousPresentation = map.PresentationRevision; return; }
            using (new PhaseTiming(this, 6))
            {
                contactCells.Clear();
                vertices.Clear(); colors.Clear(); triangles.Clear(); emitted.Clear(); projectedBands.Clear();
                for (var i = 0; i < wallBandCount; i++) Emit(bands[i].Rect, bands[i].Opacity);
                WallQuadCount = QuadCount;
                for (var i = wallBandCount; i < bands.Count; i++) Emit(bands[i].Rect, bands[i].Opacity);
            }
            using (new PhaseTiming(this, 7))
            {
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
                mesh.RecalculateBounds();
                previousBands.Clear(); previousBands.AddRange(bands);
                previousMap = map; previousViewport = viewport; previousPresentation = map.PresentationRevision;
                previousWallBandCount = wallBandCount;
                previousTopology = map.LightingMap; previousTopologyRevision = map.LightingMap?.TraversalRevision ?? -1;
                RebuildCount++;
            }
        }

        private bool ContactVisibilityIsCurrent()
        {
            // Every projected cell, including previously invisible ones, was
            // retained at the last build. Revalidate them before reusing quads:
            // unrelated fog changes need not reproject all source scanlines.
            foreach (var pair in contactCells)
            {
                var state = ReadCellState(pair.Key);
                if (!pair.Value.Center.Equals(state.Center) || state.Visible != pair.Value.Visible) return false;
            }
            return true;
        }

        private bool Visible(SpriteRenderer sprite, out Bounds bounds)
        {
            bounds = default;
            if (sprite == null || !sprite.enabled || sprite.forceRenderingOff
                || !sprite.gameObject.activeInHierarchy || sprite.color.a <= 0) return false;
            bounds = BoundsSnapshot != null ? BoundsSnapshot(sprite) : sprite.bounds;
            return viewport.Overlaps(new Rect(bounds.min.x-.1f,bounds.min.y-.1f,bounds.size.x+.2f,bounds.size.y+.2f));
        }

        private void AddForestContact(Vector2 foot, float artWidth)
            => AppendForestContact(null, foot, artWidth);

        private void AddForestContactForSource(SpriteRenderer source, Vector2 foot, float artWidth)
            => AppendForestContact(source, foot, artWidth);

        private void AppendForestContact(SpriteRenderer source, Vector2 foot, float artWidth)
        {
            float width = Mathf.Clamp(artWidth * .72f, .25f, 2.5f);
            // Identical source footprints emit identical ordered bands. Keep
            // the first source, as the later snapped-rectangle dedupe does.
            if (!forestContactSources.Add((foot, width))) return;
            ForestSourceBands prepared = null;
            if (source != null && forestSourceBands.TryGetValue(source, out prepared))
            {
                prepared.Seen = forestSourceRevision;
                if (prepared.Foot.Equals(foot) && prepared.Width == width)
                { bands.AddRange(prepared.Bands); return; }
                prepared.Bands.Clear();
            }
            else if (source != null)
            {
                prepared = new ForestSourceBands { Seen = forestSourceRevision };
                forestSourceBands.Add(source, prepared);
            }
            if (!forestBandTemplates.TryGetValue(width, out var template))
            {
                // Static art has a small finite set of widths. Bound retention
                // for a runtime prop whose scale changes continuously.
                if (forestBandTemplates.Count >= 128) forestBandTemplates.Clear();
                template = new List<(Rect, byte)>();
                forestBandTemplates.Add(width, template); ForestTemplateBuildCount++;
                // Rounded stepped bands stay attached to the base and fade
                // into the litter, with the original scanline arithmetic.
                for(var layer=0;layer<4;layer++)
                {
                    var spread=layer*2*Pixel;var halfWidth=width*.5f+spread;var halfHeight=.12f+spread*.65f;
                    var rows=Mathf.CeilToInt(halfHeight/Pixel);
                    for(var row=-rows;row<rows;row++)
                    {
                        var y=(row+.5f)*Pixel;var normalized=y/halfHeight;if(Mathf.Abs(normalized)>=1) continue;
                        var x=halfWidth*Mathf.Sqrt(1-normalized*normalized);
                        template.Add((new Rect(-x,row*Pixel,2*x,Pixel),(byte)(layer==0?72:13)));
                    }
                }
            }
            foreach (var band in template)
            {
                var rect = new Rect(foot.x + band.Rect.x, foot.y + band.Rect.y, band.Rect.width, band.Rect.height);
                Add(rect, band.Opacity); prepared?.Bands.Add((rect, band.Opacity)); ForestBandTranslationCount++;
            }
            if (prepared != null) { prepared.Foot = foot; prepared.Width = width; }
        }

        private void Add(Rect rect, byte opacity)
            => bands.Add((rect, opacity));

        private void Emit(Rect rect, byte opacity)
        {
            rect = Rect.MinMaxRect(Snap(rect.xMin),Snap(rect.yMin),Snap(rect.xMax),Snap(rect.yMax));
            if (!viewport.Overlaps(rect)) return;
            // Equal snapped rectangles clip to equal quads in this synchronous
            // build. Keep the first opacity, just like emitted quad deduplication.
            if (!projectedBands.Add(rect)) return;
            BandProjectionCount++;
            var first = ProjectCorner(new Vector2(rect.xMin+.0001f,rect.yMin+.0001f));
            var last = ProjectCorner(new Vector2(rect.xMax-.0001f,rect.yMax-.0001f));
            for (var y=first.y; y<=last.y; y++)
            for (var x=first.x; x<=last.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!contactCells.TryGetValue(cell, out var state))
                {
                    state = ReadCellState(cell);
                    contactCells.Add(cell, state); CellVisibilityQueryCount++;
                }
                if (!state.Visible) continue;
                var center = state.Center;
                var clipped = Rect.MinMaxRect(Mathf.Max(rect.xMin,center.x-.5f),Mathf.Max(rect.yMin,center.y-.5f),
                    Mathf.Min(rect.xMax,center.x+.5f),Mathf.Min(rect.yMax,center.y+.5f));
                if (clipped.width <= 0 || clipped.height <= 0 || !emitted.Add(clipped)) continue;
                int start = vertices.Count;
                vertices.Add(new Vector3(clipped.xMin,clipped.yMin)); vertices.Add(new Vector3(clipped.xMax,clipped.yMin));
                vertices.Add(new Vector3(clipped.xMax,clipped.yMax)); vertices.Add(new Vector3(clipped.xMin,clipped.yMax));
                for (int i=0;i<4;i++) colors.Add(new Color32(0,0,0,opacity));
                triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
                triangles.Add(start); triangles.Add(start+2); triangles.Add(start+3);
            }
        }

        private Vector2Int ProjectCorner(Vector2 point)
        {
            // Bound the shortcut to finite, safely representable cell indices.
            if (unitWorldCellProjection && Mathf.Abs(point.x) < 1000000f && Mathf.Abs(point.y) < 1000000f)
            { UnitCornerProjectionCount++; return Vector2Int.FloorToInt(point); }
            NativeCornerProjectionCount++;
            return terrain.WorldToCell(point);
        }

        private (Vector3 Center, bool Visible) ReadCellState(Vector2Int cell)
        {
            // The guarded unit grid has its anchor exactly at (.5,.5). Reuse
            // the known cell for visibility, retaining the same elevation/fog
            // owner; avoid a native cell->world->cell round trip per patch.
            if (unitWorldCellProjection && Mathf.Abs((float)cell.x) < 1000000f && Mathf.Abs((float)cell.y) < 1000000f)
            {
                var center = new Vector3(cell.x + .5f, cell.y + .5f);
                if (terrain.UsesVegetationPresentation)
                    return (center, !terrain.IsSurfaceOccluded(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(cell)));
                return (center, terrain.TryGetVisibleCellTraversal(cell, out var position) && terrain.IsTraversalVisible(position));
            }
            var at = terrain.CellToWorldCenter(cell);
            // These are contacts of already-rendered scenery, never actor FOV.
            // Visible trees keep their bases when the party looks elsewhere;
            // undiscovered props are rejected by Visible() before this pass.
            if (terrain.UsesVegetationPresentation)
                return (at, !terrain.IsSurfaceOccluded(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(cell)));
            return (at, terrain.TryGetVisibleTraversal(at, out var fallback) && terrain.IsTraversalVisible(fallback));
        }
        private static float Snap(float value) => Mathf.Round(value*32)/32;
        public void Dispose()
        {
            if (Application.isPlaying)
            { Object.Destroy(root); Object.Destroy(mesh); Object.Destroy(material); }
            else
            { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }
    }
}
