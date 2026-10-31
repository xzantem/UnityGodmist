using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView : MonoBehaviour
    {
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase[] floorModuleTiles;
        public float WallHalfThickness => UsesVegetationPresentation ? .03f
            : wallSegmentPrefab != null ? wallSegmentPrefab.Thickness * .5f : 0;
        [SerializeField] private TileBase difficultTerrainTile;
        [SerializeField] private DungeonWallSegmentView wallSegmentPrefab;
        [SerializeField] private Transform wallsRoot;
        [SerializeField] private DungeonWallSegmentView lowWallPrefab;
        [SerializeField] private DungeonWallSegmentView railingPrefab;

        private readonly List<GameObject> spawnedWalls = new();
        private readonly List<(DungeonSurfaceEdge Edge, DungeonWallSegmentView View, Renderer[] Renderers)> wallRenderers = new();
        private readonly List<float> depthSortBaselines = new();
        private readonly List<float> doorDepthSortBaselines = new();
        private readonly List<float> standingObjectDepthSortBaselines = new();
        private readonly Dictionary<DungeonWallSegmentView, int> depthSortOrders = new();
        // Each baseline gets one deterministic lane per wall role and one
        // actor lane between adjacent baselines.  The finite integer sorting
        // band stays below the HUD; the floor TilemapRenderer is moved below
        // its lower edge at runtime so large maps cannot cover distant walls.
        private const int DepthSortingBandTopOrderExclusive = 100;
        private const int DepthSortingSlotsPerBaseline = 4;
        // Keep ordinary dungeon objects between the floor and the actor lane.
        // These lanes move with the wall band so a large floor never pushes
        // actors below the existing DungeonObjectView presentation.
        private const int DepthSortingObjectLaneOffset = -2;
        private const int DepthSortingFloorLaneOffset = -4;
        private const float SideVoidBoundaryEpsilon = .001f;
        private const float DepthSortingJoinOverlapEpsilon = .0001f;
        private int depthSortingBandBaseOrder;
        private TilemapRenderer depthSortingFloorRenderer;
        private int depthSortingFloorOriginalOrder;
        private bool depthSortingFloorStateCaptured;
        private const float DepthSortingEpsilon = .0001f;
        private bool reserveDepthBaselines;
        private bool pendingDepthBaselines;
        private int depthSortingBuildCount;

        public void ReserveObjectDepthBaselines(IEnumerable<DungeonObjectView> objects)
        {
            var previous = reserveDepthBaselines;
            reserveDepthBaselines = true;
            try
            {
                foreach (var item in objects) if (item != null) item.ReserveDepthBaseline(this);
            }
            finally
            {
                reserveDepthBaselines = previous;
                if (!reserveDepthBaselines && pendingDepthBaselines)
                {
                    pendingDepthBaselines = false;
                    ConfigureWallDepthSorting();
                }
            }
        }

        private void DepthBaselineAdded()
        {
            if (reserveDepthBaselines) pendingDepthBaselines = true;
            else ConfigureWallDepthSorting();
        }
        private readonly Dictionary<(int Level, Vector2Int Vertex), List<DungeonSurfaceEdge>> endpointWalls = new();
        private DungeonFloorMap endpointMap;
        private int endpointRevision = -1;

        private IEnumerable<DungeonSurfaceEdge> WallsAtEndpoint(int level, Vector2Int vertex)
        {
            if (!ReferenceEquals(endpointMap, boundMap) || endpointRevision != boundMap.TraversalRevision)
            {
                endpointWalls.Clear();
                void Add(DungeonSurfaceEdge edge)
                {
                    GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
                    foreach (var endpoint in new[] { first, second })
                    {
                        var key = (edge.Level, endpoint);
                        if (!endpointWalls.TryGetValue(key, out var walls)) endpointWalls[key] = walls = new();
                        walls.Add(edge);
                    }
                }
                // Keep wall-before-gate order: corner direction selection uses
                // the first matching edge, exactly as the previous full scan.
                foreach (var edge in boundMap.SurfaceWallEdges) Add(edge);
                foreach (var edge in boundMap.SurfaceClosedProgressionGateEdges) Add(edge);
                endpointMap = boundMap; endpointRevision = boundMap.TraversalRevision;
            }
            return endpointWalls.TryGetValue((level, vertex), out var result)
                ? result : System.Array.Empty<DungeonSurfaceEdge>();
        }
        private readonly DungeonElevationViewState elevation = new();
        public int PresentationRevision { get; private set; }
        public int ViewedLevel => elevation.Level;
        public bool IsAssetAuditStructuralPreview => elevation.IsStructuralPreview;
        public void ConfigureAssetAuditStructuralPreview(bool enabled)
        {
            elevation.ConfigureStructuralPreview(enabled);
            catacombsAuditVisibilityLogged = false;
        }
        public void FocusElevation(DungeonTraversalPosition position, bool resumeFollowing = false)
        { if (elevation.Focus(position, resumeFollowing)) RefreshElevationPresentation(); }
        public bool IsTraversalSelectable(DungeonTraversalPosition position) => elevation.IsSelectable(position);
        public bool IsManualElevation => elevation.IsManual;

        public bool StepViewedLevel(int direction)
        {
            if (!elevation.Step(direction)) return false;
            RefreshElevationPresentation();
            return true;
        }

        public void FollowElevation(int level, bool resumeFollowing = false)
        {
            if (elevation.Follow(level, resumeFollowing)) RefreshElevationPresentation();
        }

        public bool TryGetVisibleSurface(Vector3 worldPosition, out DungeonSurfacePosition surface)
            => elevation.TryGetVisibleSurface(WorldToCell(worldPosition), out surface);

        public bool TryGetVisibleTraversal(Vector3 worldPosition, out DungeonTraversalPosition position)
            => elevation.TryGetVisiblePosition(WorldToCell(worldPosition), out position);
        public bool TryGetVisibleCellTraversal(Vector2Int cell, out DungeonTraversalPosition position)
            => elevation.TryGetVisiblePosition(cell, out position);
        public bool IsTraversalVisible(DungeonTraversalPosition position) => elevation.IsVisible(position)
            && (IsCurrentlySeen(position) || IsStructuralPreviewPosition(position));
        // Own party members may move ahead of an asynchronous FOV result.
        // Projection/cutaway still applies; logical fog still gates enemies.
        public bool IsTraversalProjected(DungeonTraversalPosition position) => elevation.IsVisible(position);
        public bool IsSurfaceVisible(DungeonSurfacePosition surface) => IsTraversalVisible(new DungeonTraversalPosition(surface));
        public bool IsSurfaceOccluded(DungeonSurfacePosition surface) => surface.Level > elevation.ProjectionLevel
            || (elevation.TryGetVisibleSurface(surface.Cell, out var covering) && covering.Level > surface.Level);

        public int GetActorDepthSortingOrder(float groundedFeetY)
            => ResolveDepthSortingOrder(groundedFeetY, true);

        public bool TryGetLowWallCrossing(Vector2 fromWorld, Vector2 toWorld, int level, out DungeonSurfaceEdge edge)
        {
            edge = default;
            if (boundMap == null || floorTilemap == null) return false;
            var from = WorldToCell(fromWorld);
            var to = WorldToCell(toWorld);
            var delta = to - from;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return false;
            edge = new DungeonSurfaceEdge(new DungeonCellEdge(from, to), level);
            return boundMap.GetWallType(edge) == DungeonWallType.Low;
        }

        public int GetLowWallCrossingDepthSortingOrder(float groundedFeetY, DungeonSurfaceEdge edge)
        {
            var order = GetActorDepthSortingOrder(groundedFeetY);
            if (boundMap == null || floorTilemap == null || boundMap.GetWallType(edge) != DungeonWallType.Low)
                return order;

            var first = CellToWorldCenter(edge.FirstCell);
            var second = CellToWorldCenter(edge.SecondCell);
            var baseline = (first.y + second.y) * .5f;
            if (edge.FirstCell.x != edge.SecondCell.x)
            {
                // A side segment is atomic and sorted at its south endpoint.
                // A crossing actor must clear it, including the raised cap of
                // its immediate southern continuation, rather than pass under it.
                baseline -= Vector2.Distance(first, second) * .5f;
                var south = new DungeonSurfaceEdge(new DungeonCellEdge(
                    edge.FirstCell + Vector2Int.down, edge.SecondCell + Vector2Int.down), edge.Level);
                if (boundMap.GetWallType(south) == DungeonWallType.Low)
                    baseline -= Mathf.Abs(CellToWorldCenter(edge.FirstCell).y - CellToWorldCenter(south.FirstCell).y);
            }
            // Use the existing actor lane immediately south of the obstacle;
            // never move actors to a global foreground/HUD sorting layer.
            return Mathf.Max(order, GetActorDepthSortingOrder(baseline - DepthSortingEpsilon * 2));
        }

        /// <summary>
        /// Returns the lane for a wall-mounted prop whose contact point is on
        /// the supplied wall baseline. It stays immediately behind an actor
        /// on that baseline while retaining the map's Y-aware wall ordering.
        /// </summary>
        public int GetWallMountedDepthSortingOrder(float groundedBaseY)
            => ResolveDepthSortingOrder(groundedBaseY, true) - 1;

        public int GetDoorDepthSortingOrder(float groundedBaseY)
        {
            // Door thresholds can lie between wall baselines. Give them a real
            // baseline so actors immediately on either side never share a lane.
            if (RegisterDepthBaseline(doorDepthSortBaselines, groundedBaseY))
                DepthBaselineAdded();
            // Above wall faces at the same baseline, below actors south of it.
            return ResolveDepthSortingOrder(groundedBaseY, false) + 2;
        }

        public int GetDungeonObjectDepthSortingOrder()
            => depthSortingBandBaseOrder + DepthSortingObjectLaneOffset;

        public int GetStandingObjectDepthSortingOrder(float groundedBaseY)
        {
            // A pillar can stand between wall baselines. Reserve its own lane
            // so actors on opposite sides of its base never share its order.
            if (RegisterDepthBaseline(standingObjectDepthSortBaselines, groundedBaseY))
                DepthBaselineAdded();
            return ResolveDepthSortingOrder(groundedBaseY, false) + 2;
        }

        // Reserve a floor-feedback lane below objects and every actor baseline.
        public int GetFloorFeedbackDepthSortingOrder()
            => depthSortingBandBaseOrder - 3;
        public int FloorFeedbackSortingLayerID
        {
            get
            {
                var renderer=floorTilemap != null ? floorTilemap.GetComponent<TilemapRenderer>() : null;
                return renderer != null ? renderer.sortingLayerID : SortingLayer.NameToID("Default");
            }
        }

        private int ResolveDepthSortingOrder(float baselineY, bool actor)
        {
            if (depthSortBaselines.Count == 0)
                return depthSortingBandBaseOrder + (actor ? 1 : 0);

            var insertion = LowerBound(depthSortBaselines, baselineY);
            var exact = insertion < depthSortBaselines.Count
                        && Mathf.Abs(depthSortBaselines[insertion] - baselineY) <= DepthSortingEpsilon;
            var wallOrder = depthSortingBandBaseOrder
                + (depthSortBaselines.Count - insertion - 1) * DepthSortingSlotsPerBaseline;
            if (actor)
                return exact
                    ? wallOrder - 1
                    : wallOrder + DepthSortingSlotsPerBaseline - 1;
            return wallOrder;
        }

        // These registration lists stay sorted. Existing static objects query
        // every frame: avoid a captured predicate allocation and a linear scan.
        private static bool RegisterDepthBaseline(List<float> baselines, float value)
        {
            var index = LowerBound(baselines, value);
            if (index < baselines.Count && Mathf.Abs(baselines[index] - value) <= DepthSortingEpsilon
                || index > 0 && Mathf.Abs(baselines[index - 1] - value) <= DepthSortingEpsilon)
                return false;
            baselines.Insert(index, value);
            return true;
        }

        private static int LowerBound(List<float> values, float value)
        {
            var low = 0;
            var high = values.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (values[middle] < value - DepthSortingEpsilon)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        public int GroundContactSortingOffset => UsesVegetationPresentation ? 2 : 1;
        public int GroundPresentationSortingOffset(bool outlined) => UsesVegetationPresentation
            ? outlined ? 4 : 3 : outlined ? 2 : 1;
        private readonly Dictionary<Vector3Int, (TileBase Tile, Color Color)> presentedFloorCells = new();
        private readonly HashSet<Vector3Int> visibleFloorCells = new();
        private readonly List<Vector3Int> removedFloorCells = new();
        private static readonly Unity.Profiling.ProfilerMarker FogTilesSample = new("Godmist.Dungeon.Fog.Tiles");
        private static readonly Unity.Profiling.ProfilerMarker FogWallsSample = new("Godmist.Dungeon.Fog.Walls");
        private static readonly Unity.Profiling.ProfilerMarker FogGroundSample = new("Godmist.Dungeon.Fog.Ground");
        private static readonly Unity.Profiling.ProfilerMarker SoftFogSample = new("Godmist.Dungeon.Fog.Soft");

        private void RefreshElevationPresentation()
        {
            var ownsBatch = !batchingFloorColors;
            if (ownsBatch) { pendingFloorColors.Clear(); batchingFloorColors = true; }
            try { RefreshElevationPresentationCore(); }
            finally
            {
                if (ownsBatch)
                {
                    batchingFloorColors = false;
                    using (new FogPhaseTiming(this, 6)) FlushFloorPresentationColors();
                }
            }
        }

        private void RefreshElevationPresentationCore()
        {
            if (floorTilemap == null || boundMap == null) return;
            var setupTiming = new FogPhaseTiming(this, 0);
            bool coverageOnly = refreshingCameraPresentation && WorldPresentationIsCurrent();
            // Retain the original lighting invalidation/cadence on camera
            // changes, including carried light positions between regular ticks.
            PresentationRevision++;
            PreparePresentationCoverage();
            BeginFogAnimationRefresh();
            var forestGroundTile = vegetationPresentation != null ? vegetationPresentation.GroundTile : null;
            var hasForestGroundTile = forestGroundTile != null;
            var forestGroundTint = vegetationPresentation != null ? vegetationPresentation.GroundTint : Color.white;
            setupTiming.Dispose();
            using (new FogPhaseTiming(this, 1))
            using (FogTilesSample.Auto())
            {
            visibleFloorCells.Clear();
            fogProjection.Clear();
            foreach (var position in FogPresentationPositions())
            {
                // Flat coverage positions already carry their projected cell.
                // Layered/stair projection keeps the regular topology lookup.
                Vector2Int worldCell;
                if (presentationCoverageReady && !position.IsOnStairs) worldCell = position.Surface.Cell;
                else boundMap.TryGetTraversalCell(position, out worldCell);
                if (!IsFogCellInCoverage(worldCell)) continue;
                DungeonKnowledge knowledge;
                if (coverageOnly && previousFogProjection.TryGetValue(worldCell, out knowledge)) FogCoverageKnowledgeReuseCount++;
                else knowledge = PresentationKnowledge(position);
                fogProjection[worldCell] = knowledge;
                var cell = new Vector3Int(worldCell.x, worldCell.y, 0);
                if (knowledge == DungeonKnowledge.Unexplored)
                {
                    if (presentationCoverageReady && presentedFloorCells.Remove(cell))
                    { floorTilemap.SetTile(cell, null); pendingFloorColors.Remove(cell); }
                    continue;
                }
                visibleFloorCells.Add(cell);
                var difficult = !hasForestGroundTile && !position.IsOnStairs && position.Surface.Level == 0 && boundMap.IsDifficultTerrain(worldCell);
                var tile = hasForestGroundTile
                    ? forestGroundTile
                    : difficult && catacombsArtPresentationEnabled
                    ? ResolvePresentationFloorTile(cell) ?? ResolveFloorTile(cell)
                    : difficult && difficultTerrainTile != null
                    ? difficultTerrainTile
                    : ResolvePresentationFloorTile(cell) ?? ResolveFloorTile(cell);
                var color = vegetationPresentation != null ? forestGroundTint
                    : difficult ? (catacombsArtPresentationEnabled ? new Color(.72f,.66f,.52f,1) : difficultTerrainColor) : Color.white;
                if (knowledge == DungeonKnowledge.Remembered && !UsesVegetationPresentation) color = Dim(color);
                var existing = presentedFloorCells.TryGetValue(cell, out var previous);
                if (existing && previous.Tile == tile && previous.Color == color) continue;
                // Fog tint changes do not change the underlying tile or its
                // forest transform; avoid invalidating native tile geometry.
                if (!existing || previous.Tile != tile)
                {
                    floorTilemap.SetTile(cell, tile);
                    floorTilemap.SetTileFlags(cell, TileFlags.None);
                    if (vegetationPresentation != null && tile is Tile ground && ground.sprite != null)
                        floorTilemap.SetTransformMatrix(cell, ForestGroundTransform(ground.sprite, worldCell));
                }
                SetFloorPresentationColor(cell, color);
                presentedFloorCells[cell] = (tile, color);
            }
            removedFloorCells.Clear();
            // Flat topology only grows. Within camera coverage the loop above
            // removes newly hidden cells immediately; offscreen tiles catch up
            // before that region can enter the viewport. Full/layered paths
            // retain their original geometry-removal sweep.
            if (!presentationCoverageReady)
                foreach (var cell in presentedFloorCells.Keys)
                    if (!visibleFloorCells.Contains(cell)) removedFloorCells.Add(cell);
            foreach (var cell in removedFloorCells)
            { floorTilemap.SetTile(cell, null); presentedFloorCells.Remove(cell); pendingFloorColors.Remove(cell); }
            }
            if (coverageOnly)
            {
                // World sprite knowledge was already applied globally. Only
                // tiles/soft-fog coverage changed; preserve their exact refresh.
                using (new FogPhaseTiming(this, 5)) using (SoftFogSample.Auto()) RefreshSoftFog();
                CoverageOnlyRefreshCount++;
                return;
            }
            var indexTiming = new FogPhaseTiming(this, 2);
            PrepareForestFogChanges();
            RefreshStairPresentation();
            RefreshCutawayPresentation();
            var closedGates = ReadPresentationClosedGates();
            indexTiming.Dispose();
            var forestWalls = UsesVegetationPresentation;
            using (new FogPhaseTiming(this, 3))
            {
                using (FogWallsSample.Auto()) foreach (var wallIndex in FogWallIndices())
                {
                    var wall = wallRenderers[wallIndex];
                    if (forestWalls)
                    {
                        // Neighbouring discovered edges share a static crown;
                        // the arbitrary BFS owner must not punch holes in it.
                        var hideActualGate = FogFloor != null && closedGates.Contains(wall.Edge)
                            && !FogFloor.Fog.ClosedGates.Contains(wall.Edge);
                        foreach (var renderer in wall.Renderers)
                            if (renderer != null)
                            {
                                if(IsForestProgressionGateRenderer(renderer))continue;
                                if (hideActualGate)
                                {
                                    if (!renderer.forceRenderingOff) renderer.forceRenderingOff = true;
                                    TintFog(renderer, DungeonKnowledge.Unexplored);
                                }
                                else RefreshForestBoundary(renderer, wall.Edge);
                            }
                        continue;
                    }
                    var knowledge = PresentationEdgeKnowledge(wall.Edge);
                    // Unknown geometry is hidden regardless of its projected
                    // surface; avoid querying elevation for those boundaries.
                    var hidden = (knowledge == DungeonKnowledge.Unexplored && !IsStructuralPreviewEdgeVisible(wall.Edge))
                        || !IsFogEdgeVisible(wall.Edge);
                    if (FogFloor != null && closedGates.Contains(wall.Edge)
                        && !FogFloor.Fog.ClosedGates.Contains(wall.Edge)) hidden = true;
                    foreach (var renderer in wall.Renderers)
                        if (renderer != null)
                        {
                            if (renderer.forceRenderingOff != hidden) renderer.forceRenderingOff = hidden;
                            TintFog(renderer, knowledge);
                        }
                }
                RefreshRememberedGates();
            }
            using (new FogPhaseTiming(this, 4))
            using (FogGroundSample.Auto())
            { RefreshVegetationGround(); RefreshPresentationDecorations(); }
            RefreshEntFireVisibility();
            using (new FogPhaseTiming(this, 5)) using (SoftFogSample.Auto()) RefreshSoftFog();
            RememberWorldPresentation();
            LogCatacombsAuditVisibilitySummary();
        }

        public DungeonFloorMap LightingMap => boundMap;
        public IEnumerable<SpriteRenderer> LightingWallRenderers
        {
            get { foreach (var wall in wallRenderers) foreach (var renderer in wall.Renderers)
                if (renderer is SpriteRenderer sprite && !IsArenaFireRenderer(sprite)) yield return sprite;
                foreach (var renderer in LightingPresentationWallRenderers)
                    yield return renderer;
                foreach (var renderer in balconyFaceRenderers)
                    if (renderer != null) yield return renderer; }
        }
        private DungeonFloorMap boundMap;
        private readonly HashSet<DungeonSurfaceEdge> presentedProgressionGates = new();
        public int MapRebuildCount { get; private set; }
        public int ProgressionGateLocalRefreshCount { get; private set; }
        public long[] ProgressionGatePhaseTicks { get; } = new long[6];
        public int LastGateWallReplacementCount { get; private set; }
        private HashSet<DungeonWallSegmentView> progressionWallRefresh;
        private readonly Dictionary<DungeonWallSegmentView,List<Bounds>> progressionWallVisualBounds = new();
        private readonly Dictionary<DungeonWallSegmentView,Collider2D[]> navigationWallColliders = new();
        private readonly HashSet<Collider2D> navigationColliderOwners = new();
        private readonly Dictionary<DungeonWallSegmentView,List<Bounds>> cachedNavigationWallBounds = new();
        private readonly Dictionary<int,List<Bounds>> cachedVegetationNavigationBounds = new();
        private readonly Dictionary<Collider2D,Bounds> cachedVegetationGateBounds = new();
        private readonly Dictionary<int,List<Bounds>> cachedNativeNavigationBounds = new();
        private bool nativeNavigationBoundsDirty = true;
        private Matrix4x4 navigationBoundsRootMatrix;
        private bool hasNavigationBoundsSnapshot;
        public int NavigationBoundsCaptureCount { get; private set; }
        private Bounds worldBounds;
        private bool hasWorldBounds;

        [SerializeField] private Color difficultTerrainColor = new(.55f, .43f, .3f, 1f);

        public void Rebuild(DungeonFloorMap map, bool deferForestGround = false)
        {
            MapRebuildCount++;
            if (!ReferenceEquals(boundMap, map))
            {
                if (boundMap != null)
                {
                    boundMap.ProgressionGateStateChanged
                        -= HandleProgressionGateStateChanged;
                }

                boundMap = map;

                if (boundMap != null)
                {
                    boundMap.ProgressionGateStateChanged
                        += HandleProgressionGateStateChanged;
                }
            }

            elevation.Bind(map);
            Clear();

            catacombsAuditDaggerCell = map != null && map.HasAssetAuditDagger
                ? map.AssetAuditDaggerCell
                : (Vector2Int?)null;

            if (map == null
                || floorTilemap == null
                || floorTile == null
                || wallSegmentPrefab == null)
            {
                Debug.LogError(
                    "DungeonFloorMapView: brak mapy, Tilemapy, Tile lub prefabu sciany.");

                return;
            }

            foreach (var edge in map.SurfaceWallEdges)
                SpawnWallSegment(edge, map.GetWallType(edge));
            foreach (var edge in map.SurfaceClosedProgressionGateEdges)
                SpawnWallSegment(edge);
            presentedProgressionGates.UnionWith(map.SurfaceClosedProgressionGateEdges);
            ConfigureWallDepthSorting();
            if (!UsesVegetationPresentation) ConfigureSouthCornerMiterOwners();
            ConfigureCatacombsRailingPresentations();
            ConfigureCatacombsLowWallPresentations();
            if (!UsesVegetationPresentation) BuildSideVoidOccluders();
            BuildVegetationBoundary();
            // Scene startup supplies final prop/transition occupancy after
            // spawning. Avoid baking a provisional forest mask/decor set that
            // is immediately discarded; ordinary rebuilds remain immediate.
            if (!deferForestGround || !UsesVegetationPresentation)
            {
                BuildForestSurfaceMask();
                RebuildPresentationDecorations();
            }
            RefreshNavigationColliderIndex();
            RefreshWallRendererCache();
            BuildStairPresentation();
            BuildLedgePresentation();
            RefreshElevationPresentation();

            RebuildWorldBounds(map);
            // Capture immutable terrain while loading/rebuilding its geometry,
            // before a later gate interaction can request a navigation bake.
            Physics2D.SyncTransforms();
            foreach(var level in map.Surfaces.Select(s=>s.Level).Distinct())
                foreach(var ignored in CachedNavigationWallBounds(level)) { }
        }

        private void ConfigureCatacombsRailingPresentations()
        {
            var only = progressionWallRefresh;
            if (!catacombsArtPresentationEnabled || boundMap == null)
                return;
            var horizontalSprite = LoadPresentationSprite("catacombs-railing-v3");
            var verticalSprite = LoadPresentationSprite("catacombs-railing-side-v3");
            if (horizontalSprite == null || verticalSprite == null)
                return;

            foreach (var pair in wallRenderers)
                if (pair.View != null
                    && (only == null || only.Contains(pair.View))
                    && boundMap.GetWallType(pair.Edge) == DungeonWallType.Railing)
                    pair.View.ConfigureCatacombsAuditRailing(
                        pair.Edge.CellEdge,
                        horizontalSprite,
                        verticalSprite);
        }

        private void ConfigureCatacombsLowWallPresentations()
        {
            if(!catacombsArtPresentationEnabled || boundMap==null || wallSegmentPrefab==null)return;
            foreach(var pair in wallRenderers)
            {
                if(pair.View==null || boundMap.GetWallType(pair.Edge)!=DungeonWallType.Low
                    || progressionWallRefresh!=null && !progressionWallRefresh.Contains(pair.View))continue;
                var horizontal=pair.Edge.FirstCell.x==pair.Edge.SecondCell.x;
                var sprite=horizontal?wallSegmentPrefab.NorthWallSprite:wallSegmentPrefab.SideWallSprite;
                GetEdgeEndpoints(pair.Edge.CellEdge,out var first,out var second);
                bool FullSide(Vector2Int vertex)=>WallsAtEndpoint(pair.Edge.Level,vertex).Any(e=>boundMap.GetWallType(e)==DungeonWallType.Full && e.FirstCell.x!=e.SecondCell.x);
                var left=first.x<=second.x?first:second;var right=first.x>=second.x?first:second;
                var north=first.y>=second.y?first:second;
                var clipMin=horizontal&&FullSide(left)?floorTilemap.CellToWorld(new Vector3Int(left.x,left.y,0)).x+DungeonWallSegmentView.NativeWallWidthWorld*.5f:float.NegativeInfinity;
                var clipMax=horizontal&&FullSide(right)?floorTilemap.CellToWorld(new Vector3Int(right.x,right.y,0)).x-DungeonWallSegmentView.NativeWallWidthWorld*.5f:float.PositiveInfinity;
                bool FullFaceAtNorth(DungeonSurfaceEdge edge)
                {
                    if (boundMap.GetWallType(edge) != DungeonWallType.Full) return false;
                    if (ResolveWallSurfaceProfile(edge) == DungeonWallSurfaceProfile.North) return true;
                    GetEdgeEndpoints(edge.CellEdge, out var a, out var b);
                    return (a == north ? b : a).y > north.y;
                }
                var clipTop=!horizontal&&WallsAtEndpoint(pair.Edge.Level,north).Any(FullFaceAtNorth)
                    ?floorTilemap.CellToWorld(new Vector3Int(north.x,north.y,0)).y:float.PositiveInfinity;
                pair.View.ConfigureCroppedLowWall(pair.Edge.CellEdge,sprite,
                    (CellToWorldCenter(pair.Edge.FirstCell)+CellToWorldCenter(pair.Edge.SecondCell))*.5f,clipMin,clipMax,clipTop);
                var firstArms=ResolveWallArmsAtVertex(pair.Edge.Level,first);
                var secondArms=ResolveWallArmsAtVertex(pair.Edge.Level,second);
                pair.View.ConfigureNativeLowWallJoins(wallSegmentPrefab.SideWallSprite,wallSegmentPrefab.NorthWallSprite,
                    new DungeonWallSegmentView.LowWallJoinEndpoint(floorTilemap.CellToWorld(new Vector3Int(first.x,first.y,0)),
                        firstArms,ResolveWallArmsAtVertex(pair.Edge.Level,first,DungeonWallType.Low),OwnsLowWallVertex(pair.Edge,first),
                        WallsAtEndpoint(pair.Edge.Level,first).Any(e=>boundMap.GetWallType(e)==DungeonWallType.Full)),
                    new DungeonWallSegmentView.LowWallJoinEndpoint(floorTilemap.CellToWorld(new Vector3Int(second.x,second.y,0)),
                        secondArms,ResolveWallArmsAtVertex(pair.Edge.Level,second,DungeonWallType.Low),OwnsLowWallVertex(pair.Edge,second),
                        WallsAtEndpoint(pair.Edge.Level,second).Any(e=>boundMap.GetWallType(e)==DungeonWallType.Full)));
            }
            ConfigureMixedMasonryJunctions();
        }

        // Shared endpoint index also drives full-wall caps and miters. Bits
        // describe physical arms LEFT / RIGHT / DOWN / UP at the grid vertex.
        private int ResolveWallArmsAtVertex(int level,Vector2Int vertex,DungeonWallType? type=null)
        {
            var arms=0;
            foreach(var edge in WallsAtEndpoint(level,vertex))
            {
                if(type.HasValue&&boundMap.GetWallType(edge)!=type.Value)continue;
                GetEdgeEndpoints(edge.CellEdge,out var first,out var second);
                var other=first==vertex?second:first;
                arms|=other.x<vertex.x?1:other.x>vertex.x?2:other.y<vertex.y?4:8;
            }
            return arms;
        }

        private bool OwnsLowWallVertex(DungeonSurfaceEdge owner,Vector2Int vertex)
        {
            DungeonSurfaceEdge? selected=null;
            foreach(var edge in WallsAtEndpoint(owner.Level,vertex))
            {
                if(boundMap.GetWallType(edge)==DungeonWallType.Full)return false;
                if(boundMap.GetWallType(edge)!=DungeonWallType.Low)continue;
                // A horizontal low wall owns the standing face beneath a
                // turn and sits above the vertical low-wall depth lane.
                // Choosing only by coordinates can hand the turn to its
                // vertical neighbour and leave half the corner without a face.
                var horizontal=edge.FirstCell.x==edge.SecondCell.x;
                var selectedHorizontal=selected.HasValue&&selected.Value.FirstCell.x==selected.Value.SecondCell.x;
                if(!selected.HasValue){selected=edge;continue;}
                if(horizontal!=selectedHorizontal){if(horizontal)selected=edge;continue;}
                if(edge.FirstCell.x<selected.Value.FirstCell.x
                    || edge.FirstCell.x==selected.Value.FirstCell.x && (edge.FirstCell.y<selected.Value.FirstCell.y
                    || edge.FirstCell.y==selected.Value.FirstCell.y && (edge.SecondCell.x<selected.Value.SecondCell.x
                    || edge.SecondCell.x==selected.Value.SecondCell.x && edge.SecondCell.y<selected.Value.SecondCell.y)))selected=edge;
            }
            return selected.HasValue&&selected.Value.Equals(owner);
        }

        private void ConfigureSouthCornerMiterOwners()
        {
            var only = progressionWallRefresh;
            if (boundMap == null || floorTilemap == null)
                return;

            foreach (var pair in wallRenderers)
            {
                var side = pair.View;
                if (side == null
                    || only != null && !only.Contains(side)
                    || side.SurfaceProfile != DungeonWallSurfaceProfile.Side
                    || !side.UsesNativeWallCrossSection
                    || pair.Edge.FirstCell.x == pair.Edge.SecondCell.x
                    || boundMap.GetWallType(pair.Edge) != DungeonWallType.Full)
                    continue;

                GetEdgeEndpoints(pair.Edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
                ConfigureSouthCornerMiterOwner(pair.Edge, side, firstEndpoint, secondEndpoint);
                ConfigureSouthCornerMiterOwner(pair.Edge, side, secondEndpoint, firstEndpoint);
            }
        }

        private void ConfigureSouthCornerMiterOwner(
            DungeonSurfaceEdge sideEdge,
            DungeonWallSegmentView side,
            Vector2Int endpoint,
            Vector2Int otherEndpoint)
        {
            if (!HasSouthWallAtEndpoint(sideEdge, endpoint)
                || !TryGetSouthCornerOwner(sideEdge, endpoint, out var south))
                return;

            var horizontalDirection = FindCornerHorizontalDirection(
                sideEdge,
                endpoint,
                DungeonWallSurfaceProfile.South);
            if (horizontalDirection.sqrMagnitude <= .0001f)
                return;

            var sourceRenderer = south.GetComponent<SpriteRenderer>();
            var sideRenderer = side.GetComponent<SpriteRenderer>();
            if (sourceRenderer == null || !sourceRenderer.enabled
                || sideRenderer == null || !sideRenderer.enabled)
                return;

            var endpointWorld = floorTilemap.CellToWorld(
                new Vector3Int(endpoint.x, endpoint.y, 0));
            var otherEndpointWorld = floorTilemap.CellToWorld(
                new Vector3Int(otherEndpoint.x, otherEndpoint.y, 0));
            var southVisualOffsetWorld = sourceRenderer.bounds.min.y
                                         - endpointWorld.y
                                         + DungeonWallSegmentView.NativeWallDepthWorld;
            if (!depthSortOrders.TryGetValue(side, out var sideOrder)
                || !depthSortOrders.TryGetValue(south, out var southOrder))
                return;

            // Always rehome the matched South join overlay under the South
            // wall. Its source role (face or cap) is preserved by the target
            // renderer; leaving either source in the Side group can make a
            // short mixed span render twice when Side wins the depth lane.
            var owner = sideOrder >= southOrder ? side : south;
            var southFaceRehomed = south.ConfigureSideSouthJoinOverlayAt(
                side,
                endpointWorld,
                southVisualOffsetWorld);
            if (southFaceRehomed)
                side.DisableSouthSideJoinOverlayAt(
                    endpointWorld,
                    southVisualOffsetWorld);
            if (!owner.ConfigureSouthCornerMiterAt(
                    endpointWorld,
                    otherEndpointWorld,
                    horizontalDirection,
                    southVisualOffsetWorld,
                    sourceRenderer.bounds,
                    sideRenderer.bounds,
                    sideRenderer.sprite))
                return;

            // A South-owned miter replaces any bind-time Side pair, including
            // an exterior South join that has no rehomed face overlay.
            if (owner != side)
                side.DisableSouthCornerMiterAt(endpointWorld, southVisualOffsetWorld);
        }

        private bool TryGetSouthCornerOwner(
            DungeonSurfaceEdge sideEdge,
            Vector2Int endpoint,
            out DungeonWallSegmentView south)
        {
            foreach (var pair in wallRenderers)
            {
                if (pair.View == null
                    || pair.View.SurfaceProfile != DungeonWallSurfaceProfile.South
                    || !pair.View.UsesNativeWallCrossSection
                    || boundMap.GetWallType(pair.Edge) != DungeonWallType.Full
                    || !IsPerpendicularWallAtEndpoint(sideEdge, pair.Edge, endpoint))
                    continue;

                south = pair.View;
                return true;
            }

            south = null;
            return false;
        }

        private void OnDestroy()
        {
            ReleasePresentationCamera();
            ClearEntArenaFire();
            ReleaseSoftFog();
            RestoreDepthSortingFloorOrder();
            ReleaseStairSprite();
            ReleaseForestExitRailSprite();
            DestroyPresentationFloorVariantTiles(presentationFloorVariantTiles);
            presentationFloorVariantTiles = null;
            if (boundMap != null)
            {
                boundMap.ProgressionGateStateChanged
                    -= HandleProgressionGateStateChanged;
            }
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            if (floorTilemap == null)
            {
                return Vector2Int.zero;
            }

            var cell = floorTilemap.WorldToCell(worldPosition);

            return new Vector2Int(cell.x, cell.y);
        }

        // A contact rebuild projects thousands of pixel-band corners. The
        // common, untransformed unit rectangle has an exact floor projection;
        // every other layout/transform retains Unity's native WorldToCell path.
        public bool HasUnitWorldCellProjection()
        {
            if (floorTilemap == null) return false;
            var grid = floorTilemap.layoutGrid;
            if (grid == null) return false;
            var size = grid.cellSize;
            return grid.cellLayout == GridLayout.CellLayout.Rectangle
                && grid.cellSwizzle == GridLayout.CellSwizzle.XYZ
                && size.x == 1f && size.y == 1f && (size.z == 0f || size.z == 1f)
                && grid.cellGap.Equals(Vector3.zero)
                && floorTilemap.tileAnchor.Equals(new Vector3(.5f, .5f, 0))
                && grid.transform.localToWorldMatrix.Equals(Matrix4x4.identity)
                && floorTilemap.transform.localToWorldMatrix.Equals(Matrix4x4.identity);
        }

        public IEnumerable<Bounds> NavigationWallBounds(int level)
        {
            foreach (var boundary in vegetationColliders)
                if (boundary.Level == level && boundary.Collider != null && boundary.Collider.enabled)
                    yield return boundary.Collider.bounds;
            foreach (var wall in wallRenderers)
            {
                if (wall.Edge.Level != level || wall.View == null) continue;
                if (!navigationWallColliders.TryGetValue(wall.View,out var colliders)) continue;
                foreach (var collider in colliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger) yield return collider.bounds;
            }
        }

        public bool OwnsNavigationCollider(Collider2D collider) => navigationColliderOwners.Contains(collider);

        // Generated terrain is static between rebuilds; gate replacements drop
        // only their own entries. Physical route validation still checks live
        // colliders, and a failed query explicitly refreshes this snapshot.
        public IEnumerable<Bounds> CachedNavigationWallBounds(int level, bool refresh = false)
        {
            var matrix=(wallsRoot!=null?wallsRoot:transform).localToWorldMatrix;
            if(refresh || !hasNavigationBoundsSnapshot || !navigationBoundsRootMatrix.Equals(matrix))
            {
                cachedNavigationWallBounds.Clear(); cachedVegetationNavigationBounds.Clear();cachedVegetationGateBounds.Clear();
                nativeNavigationBoundsDirty=true;
                var gates=new HashSet<Collider2D>(forestGateMovementBoundaries.Values);
                foreach(var boundary in vegetationColliders)
                {
                    var collider=boundary.Collider;
                    if(collider==null || !collider.gameObject.activeInHierarchy)continue;
                    NavigationBoundsCaptureCount++;
                    if(gates.Contains(collider)) { cachedVegetationGateBounds[collider]=collider.bounds;continue; }
                    if(!collider.enabled || collider.isTrigger)continue;
                    if(!cachedVegetationNavigationBounds.TryGetValue(boundary.Level,out var bounds))
                        cachedVegetationNavigationBounds.Add(boundary.Level,bounds=new List<Bounds>());
                    bounds.Add(collider.bounds);
                }
                navigationBoundsRootMatrix=matrix;hasNavigationBoundsSnapshot=true;
            }
            if(cachedVegetationNavigationBounds.TryGetValue(level,out var vegetation))
                foreach(var bounds in vegetation)yield return bounds;
            foreach(var gate in forestGateMovementBoundaries)
                if(gate.Key.Level==level && gate.Value!=null && gate.Value.enabled
                    && cachedVegetationGateBounds.TryGetValue(gate.Value,out var bounds))yield return bounds;
            if(nativeNavigationBoundsDirty)
            {
                cachedNativeNavigationBounds.Clear();
                foreach(var wall in wallRenderers)
                {
                    if(wall.View==null)continue;
                    if(!cachedNavigationWallBounds.TryGetValue(wall.View,out var bounds))
                    {
                        cachedNavigationWallBounds.Add(wall.View,bounds=new List<Bounds>());
                        if(navigationWallColliders.TryGetValue(wall.View,out var colliders))
                            foreach(var collider in colliders)
                                if(collider!=null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy)
                                { bounds.Add(collider.bounds);NavigationBoundsCaptureCount++; }
                    }
                    if(bounds.Count==0)continue;
                    if(!cachedNativeNavigationBounds.TryGetValue(wall.Edge.Level,out var combined))
                        cachedNativeNavigationBounds.Add(wall.Edge.Level,combined=new List<Bounds>());
                    combined.AddRange(bounds);
                }
                nativeNavigationBoundsDirty=false;
            }
            if(cachedNativeNavigationBounds.TryGetValue(level,out var nativeBounds))
                foreach(var b in nativeBounds)yield return b;
        }

        private void RemoveNavigationWallColliders(DungeonWallSegmentView wall)
        {
            if (!navigationWallColliders.TryGetValue(wall,out var colliders)) return;
            foreach (var collider in colliders) navigationColliderOwners.Remove(collider);
            navigationWallColliders.Remove(wall);
            cachedNavigationWallBounds.Remove(wall);
            nativeNavigationBoundsDirty=true;
        }

        public Vector2 CellToWorldCenter(Vector2Int cell)
        {
            if (floorTilemap == null)
            {
                return cell;
            }

            return floorTilemap.GetCellCenterWorld(
                new Vector3Int(cell.x, cell.y, 0));
        }

        private TileBase ResolveFloorTile(Vector3Int cell)
        {
            if (floorModuleTiles != null && floorModuleTiles.Length == 16)
            {
                var column = PositiveModulo(cell.x, 4);
                // The approved module manifest is top-left/row-major while
                // Unity world Y grows upward. Flip the row so the module keeps
                // its authored orientation when it repeats in world space.
                var row = PositiveModulo(3 - cell.y, 4);
                var tile = floorModuleTiles[row * 4 + column];
                if (tile != null)
                    return tile;
            }

            return floorTile;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            var remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        private void Clear()
        {
            ClearEntArenaFire();
            vegetationRenderers.Clear();
            ClearVegetationGround();
            ResetFogAnimations();
            InvalidateFogGeometryIndices();
            ClearSoftFog();
            ClearPresentationDecorations();
            ClearStairPresentation();
            if (floorTilemap != null)
            {
                floorTilemap.ClearAllTiles();
                presentedFloorCells.Clear();
            }

            foreach (var wall in wallRenderers)
                if (wall.View != null)
                    wall.View.ClearNorthSideJoinOverlays();

            foreach (var spawnedWall in spawnedWalls)
            {
                if (spawnedWall != null)
                {
                    if (Application.isPlaying) Destroy(spawnedWall); else DestroyImmediate(spawnedWall);
                }
            }

            spawnedWalls.Clear();
            foreach (var gate in rememberedGates.Values)
                if (gate != null) { if (Application.isPlaying) Destroy(gate.gameObject); else DestroyImmediate(gate.gameObject); }
            rememberedGates.Clear(); fogBaseColors.Clear();
            wallRenderers.Clear();
            navigationWallColliders.Clear(); navigationColliderOwners.Clear();
            cachedNavigationWallBounds.Clear();cachedVegetationNavigationBounds.Clear();cachedVegetationGateBounds.Clear();
            cachedNativeNavigationBounds.Clear();nativeNavigationBoundsDirty=true;
            hasNavigationBoundsSnapshot=false;
            progressionWallVisualBounds.Clear();
            presentedProgressionGates.Clear();
            depthSortBaselines.Clear();
            depthSortOrders.Clear();
            RestoreDepthSortingFloorOrder();
            doorDepthSortBaselines.Clear();
            standingObjectDepthSortBaselines.Clear();
            depthSortingBandBaseOrder = 0;
            hasWorldBounds = false;
        }

        private void SpawnWallSegment(DungeonSurfaceEdge edge, DungeonWallType type = DungeonWallType.Full)
        {
            var parent = wallsRoot != null ? wallsRoot : transform;

            var prefab = type == DungeonWallType.Low ? lowWallPrefab
                : type == DungeonWallType.Railing ? railingPrefab : wallSegmentPrefab;
            if (prefab == null) { Debug.LogError("Missing wall prefab: " + type); return; }
            var wallSegment = Instantiate(
                prefab,
                parent);
            wallSegment.IsVerticalLowWall = type == DungeonWallType.Low
                && edge.CellEdge.FirstCell.x != edge.CellEdge.SecondCell.x;

            // Presentation focus never toggles physics: existing ground movement keeps its ground colliders.
            var surfaceProfile = ResolveWallSurfaceProfile(edge);
            if (UsesVegetationPresentation)
            {
                // Forest owns its tree sprites and physical boundary colliders.
                // Keep only the wall owner/baseline used by fog and depth lanes;
                // do not construct hidden native masonry caps and textures.
                wallSegment.BindVegetationBoundary(edge.CellEdge, floorTilemap, surfaceProfile);
                wallRenderers.Add((edge, wallSegment, System.Array.Empty<Renderer>()));
                spawnedWalls.Add(wallSegment.gameObject);
                return;
            }
            var endpointExtensions = ResolveWallEndpointExtensions(edge, surfaceProfile);
            var northSideCapEndpoints = ResolveNorthSideCapEndpoints(
                edge,
                type,
                surfaceProfile);
            var northSideMiterEndpoints = ResolveNorthSideMiterEndpoints(
                edge,
                type,
                surfaceProfile);
            var northCornerHorizontalDirections = ResolveCornerHorizontalDirections(
                edge,
                type,
                surfaceProfile,
                DungeonWallSurfaceProfile.North);
            var southSideCapEndpoints = ResolveSouthSideCapEndpoints(
                edge,
                type,
                surfaceProfile);
            var southSideFaceEndpoints = ResolveSouthSideFaceEndpoints(
                edge,
                type,
                surfaceProfile);
            var southSideMiterEndpoints = ResolveSouthSideMiterEndpoints(
                edge,
                type,
                surfaceProfile);
            var southCornerHorizontalDirections = ResolveCornerHorizontalDirections(
                edge,
                type,
                surfaceProfile,
                DungeonWallSurfaceProfile.South);
            var southSideVisualOffsetEndpoints = ResolveSouthSideVisualOffsetEndpoints(
                edge,
                type,
                surfaceProfile);
            wallSegment.Bind(edge.CellEdge, floorTilemap, edge.Level == 0 && (type != DungeonWallType.Low
                || !boundMap.IsWalkable(edge.First) || !boundMap.IsWalkable(edge.Second)),
                endpointExtensions.ExtendFirst,
                endpointExtensions.ExtendSecond,
                false,
                surfaceProfile,
                northSideCapEndpoints.ExtendFirst,
                northSideCapEndpoints.ExtendSecond,
                southSideCapEndpoints.ExtendFirst,
                southSideCapEndpoints.ExtendSecond,
                southSideFaceEndpoints.ExtendFirst,
                southSideFaceEndpoints.ExtendSecond,
                southSideVisualOffsetEndpoints.ExtendFirst,
                southSideVisualOffsetEndpoints.ExtendSecond,
                northSideMiterEndpoints.ExtendFirst,
                northSideMiterEndpoints.ExtendSecond,
                southSideMiterEndpoints.ExtendFirst,
                southSideMiterEndpoints.ExtendSecond,
                northCornerHorizontalDirections.First,
                northCornerHorizontalDirections.Second,
                southCornerHorizontalDirections.First,
                southCornerHorizontalDirections.Second,
                false); // Vertical Side runs always keep continuous cap art.
            if (type == DungeonWallType.Full && surfaceProfile == DungeonWallSurfaceProfile.Side
                && edge.FirstCell.x != edge.SecondCell.x
                && boundMap.IsWalkable(edge.First) && boundMap.IsWalkable(edge.Second))
            {
                GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
                bool Free(Vector2Int vertex) => !WallsAtEndpoint(edge.Level, vertex).Any(other =>
                    !other.Equals(edge) && (boundMap.GetWallType(other) == DungeonWallType.Full
                        || catacombsArtPresentationEnabled && boundMap.GetWallType(other) == DungeonWallType.Low
                        || boundMap.SurfaceClosedProgressionGateEdges.Contains(other)));
                wallSegment.ConfigureSideFreeEndCaps(Free(first), Free(second));
            }
            wallRenderers.Add((edge, wallSegment, wallSegment.GetComponentsInChildren<Renderer>(true)));
            spawnedWalls.Add(wallSegment.gameObject);
        }

        private void ConfigureWallDepthSorting()
        {
            depthSortingBuildCount++;
            var depthWalls = new List<DungeonWallSegmentView>();
            foreach (var wall in wallRenderers)
            {
                if (wall.View == null)
                    continue;

                depthWalls.Add(wall.View);
            }

            var parent = wallsRoot != null ? wallsRoot : transform;
            depthSortBaselines.Clear();
            depthSortOrders.Clear();
            if (depthWalls.Count == 0 && doorDepthSortBaselines.Count == 0
                && standingObjectDepthSortBaselines.Count == 0)
            {
                // Keep the same floor/object/actor contract on an open floor
                // without any wall or standing object. Without a
                // wall baseline there is still a real floor render band.
                depthSortingBandBaseOrder = DepthSortingBandTopOrderExclusive - 1
                    - (DepthSortingSlotsPerBaseline - 1);
                ApplyDepthSortingFloorOrder();
                return;
            }

            foreach (var wall in depthWalls)
            {
                if (!depthSortBaselines.Exists(
                        value => Mathf.Abs(value - wall.DepthSortBaselineY) <= DepthSortingEpsilon))
                    depthSortBaselines.Add(wall.DepthSortBaselineY);
            }
            foreach (var baseline in doorDepthSortBaselines)
                if (!depthSortBaselines.Exists(value => Mathf.Abs(value - baseline) <= DepthSortingEpsilon))
                    depthSortBaselines.Add(baseline);
            foreach (var baseline in standingObjectDepthSortBaselines)
                if (!depthSortBaselines.Exists(value => Mathf.Abs(value - baseline) <= DepthSortingEpsilon))
                    depthSortBaselines.Add(baseline);
            depthSortBaselines.Sort();
            // A baseline needs one lane per wall role (North, Side, South)
            // plus the actor lane between adjacent baselines.  The role lanes
            // live in the role lanes; sortingOrder remains below the HUD.
            depthSortingBandBaseOrder = DepthSortingBandTopOrderExclusive - 1
                - (depthSortBaselines.Count - 1) * DepthSortingSlotsPerBaseline
                - (DepthSortingSlotsPerBaseline - 1);
            ApplyDepthSortingFloorOrder();

            // Keep each wall as a sibling of actor groups. A single parent
            // SortingGroup would make all walls atomic and prevent interleaving.
            foreach (var wall in depthWalls)
            {
                if (wall.transform.parent != parent)
                    wall.transform.SetParent(parent, true);
            }

            var originalWallOrder = new Dictionary<DungeonWallSegmentView, int>();
            for (var i = 0; i < wallRenderers.Count; i++)
                if (wallRenderers[i].View != null) originalWallOrder[wallRenderers[i].View] = i;
            depthWalls.Sort((first, second) =>
            {
                var baselineCompare = first.DepthSortBaselineY.CompareTo(second.DepthSortBaselineY);
                if (baselineCompare != 0)
                    return baselineCompare;

                var roleCompare = first.DepthSortRole.CompareTo(second.DepthSortRole);
                return roleCompare != 0
                    ? roleCompare
                    : originalWallOrder[first].CompareTo(originalWallOrder[second]);
            });

            for (var index = 0; index < depthWalls.Count; index++)
            {
                var wall = depthWalls[index];
                var baselineIndex = LowerBound(depthSortBaselines, wall.DepthSortBaselineY);
                var sortingOrder = depthSortingBandBaseOrder
                    + (depthSortBaselines.Count - baselineIndex - 1)
                    * DepthSortingSlotsPerBaseline
                    + wall.DepthSortRole;
                if (wall.IsVerticalLowWall) sortingOrder = depthSortingBandBaseOrder - 2;
                depthSortOrders[wall] = sortingOrder;
                wall.SetDepthSortingOrder(sortingOrder);
            }

            if (!UsesVegetationPresentation) ConfigureNorthSideJoinSorting(depthWalls);
            RefreshForestCanopyDepthOrders();
            RefreshForestExitRailDepthOrders();
            RefreshForestLowHedgeDepthOrders();
        }

        private void ConfigureNorthSideJoinSorting(List<DungeonWallSegmentView> nativeWalls)
        {
            if (boundMap == null || nativeWalls == null || nativeWalls.Count == 0)
                return;

            var nativeWallEdges = new Dictionary<DungeonWallSegmentView, DungeonSurfaceEdge>();
            var nativeWallBounds = new Dictionary<DungeonWallSegmentView, List<Bounds>>();
            var selected = new HashSet<DungeonWallSegmentView>(nativeWalls);
            foreach (var pair in wallRenderers)
            {
                if (pair.View == null || !selected.Contains(pair.View) || !pair.View.UsesNativeWallCrossSection)
                    continue;

                nativeWallEdges[pair.View] = pair.Edge;
                var bounds = new List<Bounds>();
                pair.View.CollectWallVisualBounds(bounds);
                nativeWallBounds[pair.View] = bounds;
            }

            // A North join can only touch the lower endpoint of a Side wall
            // on the same level. Retain the original native-wall order among
            // these candidates; it also orders overlapping native overlays.
            var sideAtEndpoint = new Dictionary<(int Level, Vector2Int Vertex), List<DungeonWallSegmentView>>();
            var nativeOrder = new Dictionary<DungeonWallSegmentView, int>();
            for (var i = 0; i < nativeWalls.Count; i++)
            {
                var side = nativeWalls[i]; nativeOrder[side] = i;
                if (side.SurfaceProfile != DungeonWallSurfaceProfile.Side
                    || !nativeWallEdges.TryGetValue(side, out var edge)
                    || boundMap.GetWallType(edge) != DungeonWallType.Full) continue;
                GetEdgeEndpoints(edge.CellEdge, out var lower, out _);
                var key = (edge.Level, lower);
                if (!sideAtEndpoint.TryGetValue(key, out var list)) sideAtEndpoint.Add(key, list = new());
                list.Add(side);
            }

            var overlapBounds = new List<Bounds>();
            var candidates = new List<DungeonWallSegmentView>();
            foreach (var north in nativeWalls)
            {
                if (north.SurfaceProfile != DungeonWallSurfaceProfile.North
                    || !nativeWallEdges.TryGetValue(north, out var northEdge)
                    || !nativeWallBounds.TryGetValue(north, out var northBounds)
                    || boundMap.GetWallType(northEdge) != DungeonWallType.Full)
                    continue;

                candidates.Clear();
                GetEdgeEndpoints(northEdge.CellEdge, out var firstEndpoint, out var secondEndpoint);
                if (sideAtEndpoint.TryGetValue((northEdge.Level, firstEndpoint), out var firstSides)) candidates.AddRange(firstSides);
                if (sideAtEndpoint.TryGetValue((northEdge.Level, secondEndpoint), out var secondSides)) candidates.AddRange(secondSides);
                candidates.Sort((first, second) => nativeOrder[first].CompareTo(nativeOrder[second]));
                foreach (var side in candidates)
                {
                    if (side.SurfaceProfile != DungeonWallSurfaceProfile.Side
                        || !nativeWallEdges.TryGetValue(side, out var sideEdge)
                        || !nativeWallBounds.TryGetValue(side, out var sideBounds)
                        || northEdge.Level != sideEdge.Level
                        || boundMap.GetWallType(sideEdge) != DungeonWallType.Full
                        || !HasSharedPerpendicularEndpoint(northEdge, sideEdge)
                        || !HasLowerSideEndpoint(northEdge, sideEdge)
                        || !CollectVisualBoundsOverlap(northBounds, sideBounds, overlapBounds))
                        continue;

                    if (!depthSortOrders.TryGetValue(side, out var sideOrder))
                        continue;

                    // Raise North only for a real visual join with this Side
                    // segment. Keep the actor lane between the Side baseline
                    // and the next baseline above both walls.
                    var desiredOrder = sideOrder + 1;
                    var actorLane = GetActorBetweenLane(side.DepthSortBaselineY);
                    if (desiredOrder >= actorLane)
                        continue;

                    var sideRenderer = side.GetComponent<SpriteRenderer>();
                    if (sideRenderer == null || !sideRenderer.enabled)
                        continue;

                    if (TrySelectNorthSideJoinBounds(overlapBounds, out var joinBounds))
                        north.ConfigureNorthSideJoinOverlay(
                            joinBounds,
                            desiredOrder,
                            sideRenderer.bounds,
                            side);
                }
            }
        }

        private static bool TrySelectNorthSideJoinBounds(
            List<Bounds> overlaps,
            out Bounds selected)
        {
            selected = default;
            if (overlaps == null || overlaps.Count == 0)
                return false;

            // Several North and Side child renderers occupy the same contact
            // (the source wall, cap, and miter). They describe one visual join,
            // not three independent overlays. Keep the highest contact band;
            // the dedicated miter closes that band above the crop.
            var found = false;
            foreach (var overlap in overlaps)
            {
                if (!found
                    || overlap.max.y > selected.max.y + DepthSortingJoinOverlapEpsilon
                    || overlap.max.y >= selected.max.y - DepthSortingJoinOverlapEpsilon
                        && overlap.size.y > selected.size.y)
                {
                    selected = overlap;
                    found = true;
                }
            }

            return found;
        }

        private int GetActorBetweenLane(float baselineY)
        {
            if (depthSortBaselines.Count == 0)
                return depthSortingBandBaseOrder + DepthSortingSlotsPerBaseline - 1;

            var baselineIndex = LowerBound(depthSortBaselines, baselineY);
            var wallOrder = depthSortingBandBaseOrder
                + (depthSortBaselines.Count - baselineIndex - 1) * DepthSortingSlotsPerBaseline;
            return wallOrder + DepthSortingSlotsPerBaseline - 1;
        }

        private static bool HasSharedPerpendicularEndpoint(
            DungeonSurfaceEdge first,
            DungeonSurfaceEdge second)
        {
            if (first.CellEdge.Equals(second.CellEdge)
                || (first.FirstCell.x != first.SecondCell.x)
                   == (second.FirstCell.x != second.SecondCell.x))
                return false;

            GetEdgeEndpoints(first.CellEdge, out var firstEndpoint, out var secondEndpoint);
            GetEdgeEndpoints(second.CellEdge, out var otherFirstEndpoint, out var otherSecondEndpoint);
            return firstEndpoint == otherFirstEndpoint
                   || firstEndpoint == otherSecondEndpoint
                   || secondEndpoint == otherFirstEndpoint
                   || secondEndpoint == otherSecondEndpoint;
        }

        private static bool HasLowerSideEndpoint(
            DungeonSurfaceEdge north,
            DungeonSurfaceEdge side)
        {
            GetEdgeEndpoints(side.CellEdge, out var lowerEndpoint, out _);
            GetEdgeEndpoints(north.CellEdge, out var northFirstEndpoint, out var northSecondEndpoint);
            // The Side edge is canonicalized to consecutive world-Y endpoints;
            // only its lower endpoint may receive a North face join overlay.
            return lowerEndpoint == northFirstEndpoint
                   || lowerEndpoint == northSecondEndpoint;
        }

        private static bool CollectVisualBoundsOverlap(
            List<Bounds> first,
            List<Bounds> second,
            List<Bounds> overlaps)
        {
            overlaps.Clear();
            foreach (var firstBounds in first)
                foreach (var secondBounds in second)
                {
                    var overlapWidth = Mathf.Min(firstBounds.max.x, secondBounds.max.x)
                                       - Mathf.Max(firstBounds.min.x, secondBounds.min.x);
                    var overlapHeight = Mathf.Min(firstBounds.max.y, secondBounds.max.y)
                                        - Mathf.Max(firstBounds.min.y, secondBounds.min.y);
                    if (overlapWidth > DepthSortingJoinOverlapEpsilon
                        && overlapHeight > DepthSortingJoinOverlapEpsilon)
                    {
                        overlaps.Add(new Bounds(
                            new Vector3(
                                (Mathf.Max(firstBounds.min.x, secondBounds.min.x)
                                    + Mathf.Min(firstBounds.max.x, secondBounds.max.x)) * .5f,
                                (Mathf.Max(firstBounds.min.y, secondBounds.min.y)
                                    + Mathf.Min(firstBounds.max.y, secondBounds.max.y)) * .5f,
                                (firstBounds.center.z + secondBounds.center.z) * .5f),
                            new Vector3(overlapWidth, overlapHeight,
                                Mathf.Max(firstBounds.size.z, secondBounds.size.z))));
                    }
                }

            return overlaps.Count > 0;
        }

        private void ApplyDepthSortingFloorOrder()
        {
            var renderer = floorTilemap != null
                ? floorTilemap.GetComponent<TilemapRenderer>()
                : null;
            if (renderer == null)
                return;

            if (!depthSortingFloorStateCaptured || depthSortingFloorRenderer != renderer)
            {
                depthSortingFloorRenderer = renderer;
                depthSortingFloorOriginalOrder = renderer.sortingOrder;
                depthSortingFloorStateCaptured = true;
            }

            renderer.sortingOrder = Mathf.Max(
                short.MinValue + 1,
                depthSortingBandBaseOrder + DepthSortingFloorLaneOffset);
            // Reserving a new grounded baseline moves the entire floor band.
            // Underlays must follow it too; their old order can otherwise put
            // opaque soil in front of actors on a large forest floor.
            RefreshForestUnderlaySorting();
            foreach (var ground in floorSortedPresentations)
                if (ground != null) ground.sortingOrder = renderer.sortingOrder + GroundPresentationSortingOffset(IsForestReadableProp(ground));
            foreach (var ground in runtimeGroundPresentations)
                if (ground != null && !PreservesGroundPresentationSorting(ground))
                    ground.sortingOrder = renderer.sortingOrder + GroundPresentationSortingOffset(ReservesGroundOutlineBand(ground) || IsForestReadableProp(ground));
            if (fogBlendRenderer != null)
            {
                fogBlendRenderer.sortingLayerID = renderer.sortingLayerID;
                fogBlendRenderer.sortingOrder = renderer.sortingOrder + 1;
            }
        }

        private void RestoreDepthSortingFloorOrder()
        {
            if (!depthSortingFloorStateCaptured || depthSortingFloorRenderer == null)
                return;

            depthSortingFloorRenderer.sortingOrder = depthSortingFloorOriginalOrder;
            depthSortingFloorRenderer = null;
            depthSortingFloorStateCaptured = false;
        }

        private void RefreshNavigationColliderIndex()
        {
            if (progressionWallRefresh == null)
            {
                navigationWallColliders.Clear(); navigationColliderOwners.Clear();
                hasNavigationBoundsSnapshot=false;
                foreach (var boundary in vegetationColliders)
                    if (boundary.Collider != null) navigationColliderOwners.Add(boundary.Collider);
            }
            for (var index = 0; index < wallRenderers.Count; index++)
            {
                var pair = wallRenderers[index];
                if (progressionWallRefresh != null && !progressionWallRefresh.Contains(pair.View)) continue;
                if (pair.View != null)
                {
                    RemoveNavigationWallColliders(pair.View);
                    // Include inactive colliders so toggling a door/wall never
                    // requires another hierarchy search during movement.
                    var colliders=pair.View.GetComponentsInChildren<Collider2D>(true);
                    navigationWallColliders[pair.View]=colliders;
                    foreach(var collider in colliders) navigationColliderOwners.Add(collider);
                }
            }
        }

        private void RefreshWallRendererCache()
        {
            InvalidateWorldPresentation();
            for (var index=0;index<wallRenderers.Count;index++)
            {
                var pair=wallRenderers[index];
                if (progressionWallRefresh!=null && !progressionWallRefresh.Contains(pair.View)) continue;
                wallRenderers[index] = (
                    pair.Edge,
                    pair.View,
                    pair.View != null && vegetationRenderers.TryGetValue(pair.View, out var vegetation)
                        ? vegetation
                        : pair.View != null
                        ? pair.View.GetComponentsInChildren<Renderer>(true)
                        : System.Array.Empty<Renderer>());
            }
        }

        private void BuildSideVoidOccluders()
        {
            var only = progressionWallRefresh;
            if (boundMap == null)
                return;

            foreach (var pair in wallRenderers)
                if (pair.View != null && (only == null || only.Contains(pair.View)) && pair.View.SurfaceProfile == DungeonWallSurfaceProfile.Side)
                    pair.View.ClearSideVoidOcclusionRenderers();

            // Collect only authored wall geometry before creating any new black
            // renderers. The black corner fill must not subtract another black
            // fill, and must never cover an existing wall in a neighbouring
            // SortingGroup.
            if (only == null) progressionWallVisualBounds.Clear();
            var wallVisualBoundsByLevel = new Dictionary<int, List<Bounds>>();
            foreach (var pair in wallRenderers)
            {
                if (pair.View == null)
                    continue;

                if (!wallVisualBoundsByLevel.TryGetValue(pair.Edge.Level, out var levelBounds))
                {
                    levelBounds = new List<Bounds>();
                    wallVisualBoundsByLevel.Add(pair.Edge.Level, levelBounds);
                }

                bool uncached = !progressionWallVisualBounds.TryGetValue(pair.View,out var cached);
                if (uncached)
                { cached=new List<Bounds>();progressionWallVisualBounds.Add(pair.View,cached); }
                if (uncached || only == null || only.Contains(pair.View))
                { cached.Clear();pair.View.CollectWallVisualBounds(cached); }
                levelBounds.AddRange(cached);
            }

            var occlusionPieces = new List<Bounds>();
            var scratchPieces = new List<Bounds>();
            var sideCandidates = only == null ? wallRenderers : wallRenderers.Where(w=>only.Contains(w.View)).ToList();

            foreach (var pair in wallRenderers)
            {
                var edge = pair.Edge;
                var wall = pair.View;
                if (wall == null || wall.SurfaceProfile != DungeonWallSurfaceProfile.South)
                    continue;

                if (!TryGetWalkableCell(edge, out _))
                    continue;
                foreach (var sidePair in sideCandidates)
                {
                    var sideEdge = sidePair.Edge;
                    var sideWall = sidePair.View;
                    if (sideWall == null
                        || only != null && !only.Contains(sideWall)
                        || sideWall.SurfaceProfile != DungeonWallSurfaceProfile.Side
                        || sideEdge.Level != edge.Level
                        || !TryGetWalkableCell(sideEdge, out _)
                        || !TryGetVoidCell(sideEdge, out var sideVoidCell))
                        continue;

                    GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
                    var touchesSouthEndpoint = IsPerpendicularWallAtEndpoint(
                        edge,
                        sideEdge,
                        firstEndpoint)
                        || IsPerpendicularWallAtEndpoint(
                            edge,
                            sideEdge,
                            secondEndpoint);
                    if (!touchesSouthEndpoint
                        || !sideWall.TryGetVisualBounds(out var sideBounds))
                        continue;

                    var voidCellMin = floorTilemap.CellToWorld(
                        new Vector3Int(sideVoidCell.x, sideVoidCell.y, 0));
                    var voidCellMax = floorTilemap.CellToWorld(
                        new Vector3Int(sideVoidCell.x + 1, sideVoidCell.y + 1, 0));
                    var cornerMinY = Mathf.Max(sideBounds.min.y, voidCellMin.y);
                    var cornerMaxY = Mathf.Min(sideBounds.max.y, voidCellMax.y);
                    if (cornerMaxY <= cornerMinY + .0001f)
                        continue;

                    var cornerMinX = Mathf.Min(sideBounds.min.x, voidCellMin.x);
                    var cornerMaxX = Mathf.Max(sideBounds.max.x, voidCellMax.x);
                    var cornerBounds = new Bounds(
                        new Vector3(
                            (cornerMinX + cornerMaxX) * .5f,
                            (cornerMinY + cornerMaxY) * .5f,
                            sideBounds.center.z),
                        new Vector3(
                            cornerMaxX - cornerMinX,
                            cornerMaxY - cornerMinY,
                            sideBounds.size.z));

                    occlusionPieces.Clear();
                    occlusionPieces.Add(cornerBounds);
                    if (wallVisualBoundsByLevel.TryGetValue(edge.Level, out var levelVisualBounds))
                        foreach (var wallBounds in levelVisualBounds)
                            SubtractSideVoidBounds(occlusionPieces, scratchPieces, wallBounds);

                    foreach (var occlusionPiece in occlusionPieces)
                        sideWall.ConfigureSideVoidOcclusion(occlusionPiece);
                }
            }
        }

        private static void SubtractSideVoidBounds(
            List<Bounds> pieces,
            List<Bounds> scratch,
            Bounds blocker)
        {
            scratch.Clear();
            foreach (var piece in pieces)
            {
                var overlapMinX = Mathf.Max(piece.min.x, blocker.min.x);
                var overlapMaxX = Mathf.Min(piece.max.x, blocker.max.x);
                var overlapMinY = Mathf.Max(piece.min.y, blocker.min.y);
                var overlapMaxY = Mathf.Min(piece.max.y, blocker.max.y);
                if (overlapMaxX - overlapMinX <= SideVoidBoundaryEpsilon
                    || overlapMaxY - overlapMinY <= SideVoidBoundaryEpsilon)
                {
                    scratch.Add(piece);
                    continue;
                }

                AddSideVoidPiece(
                    scratch,
                    piece,
                    piece.min.x,
                    piece.max.x,
                    piece.min.y,
                    overlapMinY);
                AddSideVoidPiece(
                    scratch,
                    piece,
                    piece.min.x,
                    piece.max.x,
                    overlapMaxY,
                    piece.max.y);
                AddSideVoidPiece(
                    scratch,
                    piece,
                    piece.min.x,
                    overlapMinX,
                    overlapMinY,
                    overlapMaxY);
                AddSideVoidPiece(
                    scratch,
                    piece,
                    overlapMaxX,
                    piece.max.x,
                    overlapMinY,
                    overlapMaxY);
            }

            pieces.Clear();
            pieces.AddRange(scratch);
        }

        private static void AddSideVoidPiece(
            List<Bounds> pieces,
            Bounds source,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (maxX - minX <= SideVoidBoundaryEpsilon
                || maxY - minY <= SideVoidBoundaryEpsilon)
                return;

            pieces.Add(new Bounds(
                new Vector3(
                    (minX + maxX) * .5f,
                    (minY + maxY) * .5f,
                    source.center.z),
                new Vector3(
                    maxX - minX,
                    maxY - minY,
                    source.size.z)));
        }

        private bool TryGetWalkableCell(DungeonSurfaceEdge edge, out Vector2Int floorCell)
        {
            if (boundMap.IsWalkable(edge.First))
            {
                floorCell = edge.FirstCell;
                return true;
            }

            if (boundMap.IsWalkable(edge.Second))
            {
                floorCell = edge.SecondCell;
                return true;
            }

            floorCell = default;
            return false;
        }

        private bool TryGetVoidCell(DungeonSurfaceEdge edge, out Vector2Int voidCell)
        {
            var firstWalkable = boundMap.IsWalkable(edge.First);
            var secondWalkable = boundMap.IsWalkable(edge.Second);
            if (firstWalkable == secondWalkable)
            {
                voidCell = default;
                return false;
            }

            voidCell = firstWalkable ? edge.SecondCell : edge.FirstCell;
            return true;
        }

        private DungeonWallSurfaceProfile ResolveWallSurfaceProfile(DungeonSurfaceEdge edge)
        {
            if (boundMap == null
                || floorTilemap == null
                || edge.FirstCell.y == edge.SecondCell.y)
                return DungeonWallSurfaceProfile.Side;

            var firstWalkable = boundMap.IsWalkable(edge.First);
            var secondWalkable = boundMap.IsWalkable(edge.Second);
            if (firstWalkable == secondWalkable)
                return DungeonWallSurfaceProfile.Side;

            var floorCell = firstWalkable ? edge.FirstCell : edge.SecondCell;
            var otherCell = firstWalkable ? edge.SecondCell : edge.FirstCell;
            var floorY = floorTilemap.GetCellCenterWorld(
                new Vector3Int(floorCell.x, floorCell.y, 0)).y;
            var otherY = floorTilemap.GetCellCenterWorld(
                new Vector3Int(otherCell.x, otherCell.y, 0)).y;
            return floorY < otherY
                ? DungeonWallSurfaceProfile.North
                : DungeonWallSurfaceProfile.South;
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveWallEndpointExtensions(
            DungeonSurfaceEdge edge,
            DungeonWallSurfaceProfile profile)
        {
            if (profile != DungeonWallSurfaceProfile.North
                && profile != DungeonWallSurfaceProfile.South)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var extendAtFirstEndpoint = HasPerpendicularWallAtEndpoint(edge, firstEndpoint);
            var extendAtSecondEndpoint = HasPerpendicularWallAtEndpoint(edge, secondEndpoint);

            // Bind's tangent is +Y for vertical cell pairs and -X for horizontal
            // cell pairs. Horizontal North/South segments therefore receive the
            // right endpoint as extendFirstEnd.
            return edge.FirstCell.x != edge.SecondCell.x
                ? (extendAtFirstEndpoint, extendAtSecondEndpoint)
                : (extendAtSecondEndpoint, extendAtFirstEndpoint);
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveNorthSideCapEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            // The extended cap belongs only to the native full-height wall
            // material. Low walls and railings can share the same topological
            // edge, but they must keep their legacy art and posts untouched.
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasNorth = HasNorthWallAtEndpoint(edge, firstEndpoint);
            var secondHasNorth = HasNorthWallAtEndpoint(edge, secondEndpoint);
            var firstWorldY = floorTilemap.CellToWorld(
                new Vector3Int(firstEndpoint.x, firstEndpoint.y, 0)).y;
            var secondWorldY = floorTilemap.CellToWorld(
                new Vector3Int(secondEndpoint.x, secondEndpoint.y, 0)).y;

            // DungeonCellEdge canonicalizes a side edge left-to-right. Bind's
            // tangent is then +Y, so its first/second ends are the same
            // lower/upper order returned by GetEdgeEndpoints.
            if (firstWorldY > secondWorldY)
                return (firstHasNorth, false);

            return (false, secondHasNorth);
        }

        private bool HasNorthWallAtEndpoint(
            DungeonSurfaceEdge edge,
            Vector2Int endpoint)
        {
            foreach (var candidate in WallsAtEndpoint(edge.Level, endpoint))
                if (IsNorthWallAtEndpoint(edge, candidate, endpoint))
                    return true;

            return false;
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveSouthSideCapEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasSouth = HasSouthWallAtEndpoint(edge, firstEndpoint);
            var secondHasSouth = HasSouthWallAtEndpoint(edge, secondEndpoint);
            var firstHasNorth = HasNorthWallAtEndpoint(edge, firstEndpoint);
            var secondHasNorth = HasNorthWallAtEndpoint(edge, secondEndpoint);
            var firstWorldY = floorTilemap.CellToWorld(
                new Vector3Int(firstEndpoint.x, firstEndpoint.y, 0)).y;
            var secondWorldY = floorTilemap.CellToWorld(
                new Vector3Int(secondEndpoint.x, secondEndpoint.y, 0)).y;

            if (firstWorldY > secondWorldY)
                return (firstHasSouth && !firstHasNorth, false);

            return (false, secondHasSouth && !secondHasNorth);
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveNorthSideMiterEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasNorth = HasNorthWallAtEndpoint(edge, firstEndpoint);
            var secondHasNorth = HasNorthWallAtEndpoint(edge, secondEndpoint);

            // Miter geometry follows the actual North wall endpoint.  This is
            // deliberately independent from the established cap flags: a
            // stepped/concave join can meet the lower Side endpoint while the
            // legacy cap contract remains upper-only.
            return (firstHasNorth, secondHasNorth);
        }

        private (Vector2 First, Vector2 Second) ResolveCornerHorizontalDirections(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile,
            DungeonWallSurfaceProfile perpendicularProfile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (Vector2.zero, Vector2.zero);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            return (
                FindCornerHorizontalDirection(edge, firstEndpoint, perpendicularProfile),
                FindCornerHorizontalDirection(edge, secondEndpoint, perpendicularProfile));
        }

        private Vector2 FindCornerHorizontalDirection(
            DungeonSurfaceEdge edge,
            Vector2Int endpoint,
            DungeonWallSurfaceProfile perpendicularProfile)
        {
            foreach (var candidate in WallsAtEndpoint(edge.Level, endpoint))
            {
                var direction = TryGetCornerHorizontalDirection(
                    edge,
                    candidate,
                    endpoint,
                    perpendicularProfile);
                if (direction.sqrMagnitude > .0001f)
                    return direction;
            }

            return Vector2.zero;
        }

        private Vector2 TryGetCornerHorizontalDirection(
            DungeonSurfaceEdge edge,
            DungeonSurfaceEdge candidate,
            Vector2Int endpoint,
            DungeonWallSurfaceProfile perpendicularProfile)
        {
            if (!IsPerpendicularWallAtEndpoint(edge, candidate, endpoint)
                || boundMap.GetWallType(candidate) != DungeonWallType.Full
                || ResolveWallSurfaceProfile(candidate) != perpendicularProfile)
                return Vector2.zero;

            GetEdgeEndpoints(candidate.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var otherEndpoint = endpoint == firstEndpoint ? secondEndpoint
                : endpoint == secondEndpoint ? firstEndpoint
                : default;
            if (otherEndpoint == default && endpoint != firstEndpoint && endpoint != secondEndpoint)
                return Vector2.zero;

            var endpointWorld = floorTilemap.CellToWorld(
                new Vector3Int(endpoint.x, endpoint.y, 0));
            var otherWorld = floorTilemap.CellToWorld(
                new Vector3Int(otherEndpoint.x, otherEndpoint.y, 0));
            var direction = (Vector2)(otherWorld - endpointWorld);
            return Mathf.Abs(direction.x) > .0001f
                ? direction.normalized
                : Vector2.zero;
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveSouthSideFaceEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasSouth = HasSouthWallAtEndpoint(edge, firstEndpoint);
            var secondHasSouth = HasSouthWallAtEndpoint(edge, secondEndpoint);
            var firstHasNorth = HasNorthWallAtEndpoint(edge, firstEndpoint);
            var secondHasNorth = HasNorthWallAtEndpoint(edge, secondEndpoint);
            var firstWorldY = floorTilemap.CellToWorld(
                new Vector3Int(firstEndpoint.x, firstEndpoint.y, 0)).y;
            var secondWorldY = floorTilemap.CellToWorld(
                new Vector3Int(secondEndpoint.x, secondEndpoint.y, 0)).y;

            // A mixed junction keeps both visual roles at the shared endpoint:
            // the side cap supplies the upper 24 px and the South face supplies
            // the dark 8 px strip below it. Ordinary South-only corners remain
            // caps through ResolveSouthSideCapEndpoints.
            if (firstWorldY > secondWorldY)
                return (firstHasSouth && firstHasNorth, false);

            return (false, secondHasSouth && secondHasNorth);
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveSouthSideMiterEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasSouth = HasSouthWallAtEndpoint(edge, firstEndpoint);
            var secondHasSouth = HasSouthWallAtEndpoint(edge, secondEndpoint);

            // Miter geometry is independent from the legacy South cap/face
            // flags.  Follow whichever endpoint actually meets South; this
            // covers both convex lower joins and mirrored stepped upper joins.
            return (firstHasSouth, secondHasSouth);
        }

        private (bool ExtendFirst, bool ExtendSecond) ResolveSouthSideVisualOffsetEndpoints(
            DungeonSurfaceEdge edge,
            DungeonWallType type,
            DungeonWallSurfaceProfile profile)
        {
            if (type != DungeonWallType.Full
                || profile != DungeonWallSurfaceProfile.Side
                || edge.FirstCell.x == edge.SecondCell.x)
                return (false, false);

            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            var firstHasSouth = HasSouthWallAtEndpoint(edge, firstEndpoint);
            var secondHasSouth = HasSouthWallAtEndpoint(edge, secondEndpoint);
            var firstWorldY = floorTilemap.CellToWorld(
                new Vector3Int(firstEndpoint.x, firstEndpoint.y, 0)).y;
            var secondWorldY = floorTilemap.CellToWorld(
                new Vector3Int(secondEndpoint.x, secondEndpoint.y, 0)).y;

            // Bind's tangent is +Y for a vertical Side segment. Preserve the
            // endpoint order even when a caller supplies a reversed cell pair so
            // each end receives the offset belonging to its world-space vertex.
            return firstWorldY <= secondWorldY
                ? (firstHasSouth, secondHasSouth)
                : (secondHasSouth, firstHasSouth);
        }

        private bool HasSouthWallAtEndpoint(
            DungeonSurfaceEdge edge,
            Vector2Int endpoint)
        {
            foreach (var candidate in WallsAtEndpoint(edge.Level, endpoint))
                if (IsSouthWallAtEndpoint(edge, candidate, endpoint))
                    return true;

            return false;
        }

        private bool IsSouthWallAtEndpoint(
            DungeonSurfaceEdge edge,
            DungeonSurfaceEdge candidate,
            Vector2Int endpoint)
        {
            return IsPerpendicularWallAtEndpoint(edge, candidate, endpoint)
                   && boundMap.GetWallType(candidate) == DungeonWallType.Full
                   && ResolveWallSurfaceProfile(candidate) == DungeonWallSurfaceProfile.South;
        }

        private bool IsNorthWallAtEndpoint(
            DungeonSurfaceEdge edge,
            DungeonSurfaceEdge candidate,
            Vector2Int endpoint)
        {
            return IsPerpendicularWallAtEndpoint(edge, candidate, endpoint)
                   && boundMap.GetWallType(candidate) == DungeonWallType.Full
                   && ResolveWallSurfaceProfile(candidate) == DungeonWallSurfaceProfile.North;
        }

        private bool HasPerpendicularWallAtEndpoint(
            DungeonSurfaceEdge edge,
            Vector2Int endpoint)
        {
            foreach (var candidate in WallsAtEndpoint(edge.Level, endpoint))
                if (IsPerpendicularWallAtEndpoint(edge, candidate, endpoint))
                    return true;

            return false;
        }

        private static bool IsPerpendicularWallAtEndpoint(
            DungeonSurfaceEdge edge,
            DungeonSurfaceEdge candidate,
            Vector2Int endpoint)
        {
            if (edge.Level != candidate.Level
                || edge.CellEdge.Equals(candidate.CellEdge)
                || (edge.FirstCell.x != edge.SecondCell.x)
                   == (candidate.FirstCell.x != candidate.SecondCell.x))
                return false;

            GetEdgeEndpoints(candidate.CellEdge, out var candidateFirst, out var candidateSecond);
            return endpoint == candidateFirst || endpoint == candidateSecond;
        }

        private static void GetEdgeEndpoints(
            DungeonCellEdge edge,
            out Vector2Int firstEndpoint,
            out Vector2Int secondEndpoint)
        {
            if (edge.FirstCell.x != edge.SecondCell.x)
            {
                var boundaryX = Mathf.Max(edge.FirstCell.x, edge.SecondCell.x);
                var minimumY = Mathf.Min(edge.FirstCell.y, edge.SecondCell.y);
                firstEndpoint = new Vector2Int(boundaryX, minimumY);
                secondEndpoint = new Vector2Int(boundaryX, minimumY + 1);
                return;
            }

            var boundaryY = Mathf.Max(edge.FirstCell.y, edge.SecondCell.y);
            var minimumX = Mathf.Min(edge.FirstCell.x, edge.SecondCell.x);
            firstEndpoint = new Vector2Int(minimumX, boundaryY);
            secondEndpoint = new Vector2Int(minimumX + 1, boundaryY);
        }

        private void HandleProgressionGateStateChanged()
        {
            if (boundMap == null) return;
            var closed = new HashSet<DungeonSurfaceEdge>(boundMap.SurfaceClosedProgressionGateEdges);
            var changed = new HashSet<DungeonSurfaceEdge>(presentedProgressionGates);
            changed.SymmetricExceptWith(closed);
            if (changed.Count == 0) return;
            if (UsesVegetationPresentation)
            {
                // Normal progression changes whole log barriers. New/partially
                // split barriers alter authored grouping and retain full setup.
                if (closed.Any(edge => !forestGateMovementBoundaries.ContainsKey(edge))
                    || forestProgressionGates.Any(g => !g.Remembered && g.Edges.Any(closed.Contains) && g.Edges.Any(e => !closed.Contains(e))))
                { Rebuild(boundMap); return; }
                foreach (var pair in forestGateMovementBoundaries)
                    if (pair.Value != null) pair.Value.enabled = closed.Contains(pair.Key);
            }
            else
            {
                var replacements = RefreshNativeProgressionGates(changed, closed);
                foreach (var wall in wallRenderers)
                {
                    if (!replacements.Contains(wall.View)) continue;
                    var knowledge = PresentationEdgeKnowledge(wall.Edge);
                    bool hidden = (knowledge == DungeonKnowledge.Unexplored && !IsStructuralPreviewEdgeVisible(wall.Edge))
                        || !IsFogEdgeVisible(wall.Edge)
                        || FogFloor != null && closed.Contains(wall.Edge) && !FogFloor.Fog.ClosedGates.Contains(wall.Edge);
                    foreach (var renderer in wall.Renderers)
                        if (renderer != null) { renderer.forceRenderingOff = hidden; TintFog(renderer,knowledge); }
                }
            }
            presentedProgressionGates.Clear(); presentedProgressionGates.UnionWith(closed);
            ProgressionGateLocalRefreshCount++;
            if (UsesVegetationPresentation)
            {
                // Opening a gate changes no floor cells or tree ownership.
                // Preserve those caches instead of invalidating a whole forest.
                if (ReferenceEquals(fogGeometryIndexMap,boundMap)) fogGeometryIndexRevision=boundMap.TraversalRevision;
                fogBoundaryRevision=boundMap.TraversalRevision;
                if (ReferenceEquals(forestKnowledgeMap,boundMap)) forestKnowledgeMapRevision=boundMap.TraversalRevision;
            }
            else InvalidateFogGeometryIndices();
            RefreshRememberedGates(); RememberFogClosedGates();
            PresentationRevision++;
            // Only gate geometry changed here; the next logical fog update
            // still computes new LOS and publishes its own revision normally.
            RememberWorldPresentation();
        }

        private HashSet<DungeonWallSegmentView> RefreshNativeProgressionGates(HashSet<DungeonSurfaceEdge> changed, HashSet<DungeonSurfaceEdge> closed)
        {
            long measured = MeasureFogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            void Phase(int index)
            {
                if (measured == 0) return;
                long now = System.Diagnostics.Stopwatch.GetTimestamp();ProgressionGatePhaseTicks[index] += now-measured;measured=now;
            }
            var staticWalls = new HashSet<DungeonSurfaceEdge>(boundMap.SurfaceWallEdges);
            // Only walls sharing the changed gate's corners can change caps,
            // miters or void masks. The second ring includes owners of joins.
            var region = new HashSet<DungeonSurfaceEdge>(changed);
            var vertices = new HashSet<(int Level, Vector2Int Vertex)>();
            for (int ring = 0; ring < 2; ring++)
            {
                foreach (var edge in region)
                { GetEdgeEndpoints(edge.CellEdge, out var a, out var b); vertices.Add((edge.Level,a)); vertices.Add((edge.Level,b)); }
                foreach (var wall in wallRenderers)
                {
                    GetEdgeEndpoints(wall.Edge.CellEdge, out var a, out var b);
                    if (vertices.Contains((wall.Edge.Level,a)) || vertices.Contains((wall.Edge.Level,b))) region.Add(wall.Edge);
                }
            }
            var replacements = new HashSet<DungeonWallSegmentView>();
            Phase(0);
            bool needsNewDepthLane = false;
            for (int i = 0; i < wallRenderers.Count; i++)
            {
                var old = wallRenderers[i];
                if (!region.Contains(old.Edge)) continue;
                if (old.View != null)
                {
                    old.View.ClearNorthSideJoinOverlays(); depthSortOrders.Remove(old.View);
                    RemoveNavigationWallColliders(old.View);
                    progressionWallVisualBounds.Remove(old.View);
                    old.View.gameObject.SetActive(false); spawnedWalls.Remove(old.View.gameObject);
                    foreach (var renderer in old.View.GetComponentsInChildren<SpriteRenderer>(true))
                    { fogBaseColors.Remove(renderer); presentedFogTints.Remove(renderer); fogSpriteVisuals.Remove(renderer); UnregisterLightingPresentation(renderer); }
                    if (Application.isPlaying) Destroy(old.View.gameObject); else DestroyImmediate(old.View.gameObject);
                }
                if (!staticWalls.Contains(old.Edge) && !closed.Contains(old.Edge))
                { wallRenderers.RemoveAt(i--); continue; }
                SpawnWallSegment(old.Edge,boundMap.GetWallType(old.Edge));
                wallRenderers[i] = wallRenderers[wallRenderers.Count-1]; wallRenderers.RemoveAt(wallRenderers.Count-1);
                replacements.Add(wallRenderers[i].View);
            }
            foreach (var edge in closed)
                if (!presentedProgressionGates.Contains(edge) && !wallRenderers.Any(w=>w.Edge.Equals(edge)))
                { SpawnWallSegment(edge); replacements.Add(wallRenderers[wallRenderers.Count-1].View); }
            Phase(1);
            // Preserve existing depth lanes, including now-empty gate lanes,
            // so unrelated renderers and actors never need a global re-sort.
            foreach (var wall in replacements)
            {
                int index = LowerBound(depthSortBaselines,wall.DepthSortBaselineY);
                if (index >= depthSortBaselines.Count || Mathf.Abs(depthSortBaselines[index]-wall.DepthSortBaselineY)>DepthSortingEpsilon)
                { needsNewDepthLane = true; break; }
                int order=depthSortingBandBaseOrder+(depthSortBaselines.Count-index-1)*DepthSortingSlotsPerBaseline+wall.DepthSortRole;
                if (wall.IsVerticalLowWall) order = depthSortingBandBaseOrder - 2;
                depthSortOrders[wall]=order; wall.SetDepthSortingOrder(order);
            }
            if (needsNewDepthLane) ConfigureWallDepthSorting();
            else ConfigureNorthSideJoinSorting(wallRenderers.Where(w=>replacements.Contains(w.View)).Select(w=>w.View).ToList());
            Phase(2);
            progressionWallRefresh = replacements;
            try
            {
                ConfigureSouthCornerMiterOwners();
                ConfigureCatacombsRailingPresentations();
                ConfigureCatacombsLowWallPresentations();
                Phase(3);
                BuildSideVoidOccluders();
                Phase(4);
                RefreshNavigationColliderIndex();
                RefreshWallRendererCache();
                Phase(5);
            }
            finally { progressionWallRefresh = null; }
            LastGateWallReplacementCount = replacements.Count;
            return replacements;
        }

        private void RebuildWorldBounds(DungeonFloorMap map)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            void Include(Vector2Int cell)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            foreach (var surface in map.Surfaces) Include(surface.Cell);
            foreach (var stair in map.Stairs)
                if (map.TryGetStairPath(stair, out var path))
                    foreach (var cell in path) Include(cell);

            if (minX == int.MaxValue)
            {
                return;
            }

            var minimum = floorTilemap.CellToWorld(
                new Vector3Int(minX, minY, 0));

            var maximum = floorTilemap.CellToWorld(
                new Vector3Int(maxX + 1, maxY + 1, 0));

            worldBounds.SetMinMax(minimum, maximum);
            hasWorldBounds = true;
        }
    }
}
