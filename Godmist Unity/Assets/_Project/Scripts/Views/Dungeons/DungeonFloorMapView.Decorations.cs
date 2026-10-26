using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private const string PresentationDecorResources = "DungeonDecor/";
        private const string PresentationFloorVariantResources = "DungeonFloorVariants/";
        private const string PresentationFloorVariantName = "floor-aged-tile-";
        private const int PresentationDecorSeed = 0x4D554E;
        private readonly List<PresentationDecoration> presentationDecorations = new();
        private readonly HashSet<Vector2Int> presentationOccupiedCells = new();
        private readonly HashSet<Vector2Int> presentationTorchMountCells = new();
        private readonly Dictionary<string, Sprite> presentationSprites = new();
        private readonly HashSet<SpriteRenderer> runtimeGroundPresentations = new();
        private readonly HashSet<SpriteRenderer> runtimePropGroundPresentations = new();
        private readonly HashSet<SpriteRenderer> depthSortedGroundPresentations = new();
        private readonly HashSet<SpriteRenderer> runtimeActorPresentations = new();
        private readonly HashSet<SpriteRenderer> groundOutlinePresentations = new();
        private readonly HashSet<SpriteRenderer> floorSortedPresentations = new();
        private Vector2Int? catacombsAuditDaggerCell;
        private bool catacombsAuditDiagnostics;
        private bool catacombsArtPresentationEnabled;
        private bool catacombsAuditVisibilityLogged;
        private TileBase[] presentationFloorVariantTiles;
        private bool presentationFloorVariantLoaded;

        private sealed class PresentationDecoration
        {
            public GameObject Object;
            public SpriteRenderer Renderer;
            public Vector2Int Cell;
            public DungeonSurfaceEdge Edge;
            public bool IsWall;
            public Color BaseColor;
        }

        /// <summary>
        /// Supplies cells that already have a gameplay or traversal presentation.
        /// The list is presentation-only; it never changes map walkability.
        /// </summary>
        public void SetPresentationOccupancy(IEnumerable<Vector2Int> cells)
        {
            presentationOccupiedCells.Clear();
            if (cells != null)
                foreach (var cell in cells)
                    presentationOccupiedCells.Add(cell);

            if (boundMap != null)
                RebuildPresentationDecorations();
        }

        /// <summary>
        /// Adds the Catacombs-only ground dagger presentation owned by the
        /// asset-audit fixture. This is presentation state and never changes
        /// map traversal or combat object ownership.
        /// </summary>
        public void ConfigureCatacombsAuditDagger(Vector2Int cell)
        {
            catacombsAuditDaggerCell = cell;
            if (boundMap != null)
                RebuildPresentationDecorations();
        }

        public void ConfigureCatacombsAuditDiagnostics(bool enabled)
        {
            catacombsAuditDiagnostics = enabled;
            catacombsAuditVisibilityLogged = false;
        }

        /// <summary>
        /// Enables the versioned Catacombs structural presentation for every
        /// Catacombs map. The audit room adds structural visibility separately;
        /// ordinary Catacombs gameplay keeps its normal fog and traversal.
        /// </summary>
        public void ConfigureCatacombsArtPresentation(bool enabled)
        {
            catacombsArtPresentationEnabled = enabled;
        }

        private void LogCatacombsAuditVisibilitySummary()
        {
            if (!catacombsAuditDiagnostics || catacombsAuditVisibilityLogged || FogFloor == null || boundMap == null)
                return;

            var visibleUpperSurfaces = elevation.VisibleSurfaces.Count(surface => surface.Level > 0);
            var visibleStairPositions = elevation.VisiblePositions.Count(position => position.IsOnStairs);
            var fogMode = FogFloor.Fog.DebugFogDisabled ? "disabled" : "normal";
            Debug.Log(
                $"Catacombs audit structural preview={elevation.IsStructuralPreview} fog={fogMode} "
                + $"visibleUpperSurfaces={visibleUpperSurfaces} visibleStairPositions={visibleStairPositions} "
                + $"cutawaySurfaces={elevation.CutawaySurfaces.Count()}",
                this);
            catacombsAuditVisibilityLogged = true;
        }

        /// <summary>
        /// Registers an explicitly owned runtime ground renderer, such as a
        /// thrown knife, with this floor's lighting pass without searching
        /// the scene hierarchy.
        /// </summary>
        public void RegisterLightingPresentation(SpriteRenderer renderer)
            => RegisterGroundLightingPresentation(renderer);

        public void RegisterGroundLightingPresentation(SpriteRenderer renderer)
            => RegisterGroundLightingPresentation(renderer, false);

        public void RegisterGroundLightingPresentation(
            SpriteRenderer renderer,
            bool reservesOutlineBand, bool preserveDepthSorting = false)
        {
            if (renderer == null)
                return;

            runtimeActorPresentations.Remove(renderer);
            runtimeGroundPresentations.Add(renderer);
            if (!vegetationGroundRenderers.Contains(renderer)) runtimePropGroundPresentations.Add(renderer);
            if (preserveDepthSorting) depthSortedGroundPresentations.Add(renderer); else depthSortedGroundPresentations.Remove(renderer);
            if (reservesOutlineBand)
                groundOutlinePresentations.Add(renderer);
            else
                groundOutlinePresentations.Remove(renderer);
        }

        // Combat summons are upright actors: they need the same body lighting
        // and shadow projection as borrowed party/enemy views, not floor shading.
        public void RegisterActorLightingPresentation(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            runtimeGroundPresentations.Remove(renderer);
            runtimePropGroundPresentations.Remove(renderer);
            depthSortedGroundPresentations.Remove(renderer);
            groundOutlinePresentations.Remove(renderer);
            runtimeActorPresentations.Add(renderer);
        }

        public IEnumerable<SpriteRenderer> ActorLightingPresentationRenderers => runtimeActorPresentations;

        public void UnregisterLightingPresentation(SpriteRenderer renderer)
        {
            if (renderer != null)
            {
                runtimeGroundPresentations.Remove(renderer);
                runtimePropGroundPresentations.Remove(renderer);
                depthSortedGroundPresentations.Remove(renderer);
                runtimeActorPresentations.Remove(renderer);
                groundOutlinePresentations.Remove(renderer);
            }
        }

        public bool IsGroundLightingPresentation(SpriteRenderer renderer)
            => renderer != null
               && (runtimeGroundPresentations.Contains(renderer)
                   || floorSortedPresentations.Contains(renderer));

        public bool ReservesGroundOutlineBand(SpriteRenderer renderer)
            // The lighting pass uses this opt-in to keep a dedicated lane
            // between the floor tilemap and the outlined source sprite.
            => renderer != null && groundOutlinePresentations.Contains(renderer);

        private void RebuildPresentationDecorations()
        {
            ClearPresentationDecorations();
            if (boundMap == null)
                return;

            BuildPresentationTorchMountCells();
            if (vegetationPresentation == null) BuildWallDecorations();
            else { BuildForestGroundCover(); BuildForestEdgeDetails(); BuildEntArenaDetails(); }
            BuildDifficultTerrainDecorations();
            BuildCatacombsAuditDagger();
            // Wall decoration renderers are consumed by the scene-owned
            // lighting pass. Refresh the cache before the first visibility
            // pass so a rebuild made after lighting initialization is safe.
            RefreshWallRendererCache();
            RefreshPresentationDecorations();
        }

        private void BuildCatacombsAuditDagger()
        {
            if (!catacombsAuditDaggerCell.HasValue)
                return;

            var sprite = LoadPresentationSprite("catacombs-ground-dagger-v2");
            if (sprite == null || !boundMap.IsWalkable(catacombsAuditDaggerCell.Value))
                return;

            var decoration = CreateDecoration(
                "Catacombs audit ground dagger",
                sprite,
                transform,
                CellToWorldCenter(catacombsAuditDaggerCell.Value)
                    + (Vector2)DungeonCatacombsObjectVisuals.GroundDaggerPresentationOffset,
                GetFloorFeedbackDepthSortingOrder(),
                Color.white,
                DungeonCatacombsObjectVisuals.GroundDaggerPresentationScale,
                FloorFeedbackSortingLayerID);
            decoration.Cell = catacombsAuditDaggerCell.Value;
            presentationDecorations.Add(decoration);
            floorSortedPresentations.Add(decoration.Renderer);
        }

        private void BuildWallDecorations()
        {
            // These panels are transparent 24x16 insets at 32 PPU. Their
            // canvas contains no stone surround; the native wall remains the
            // only source of trim, face, collision, fog and depth geometry.
            var grate = LoadPresentationSprite("wall-grate-inset-24x16");
            var ossuary = LoadPresentationSprite("wall-ossuary-inset-24x16");
            if (grate == null && ossuary == null)
                return;

            // Keep focal panels on a deterministic, ordered wall run. The
            // first two eligible segments provide stable grate/ossuary
            // anchors. Remaining segments stay clear so the authored wall
            // face and its native trim remain readable. Corner segments,
            // occupied interaction rings, and actual torch mounts stay clear.
            var northPairs = wallRenderers
                .Where(pair => pair.View != null
                    && pair.View.SurfaceProfile == DungeonWallSurfaceProfile.North
                    && boundMap.GetWallType(pair.Edge) == Godmist.Core.Enums.DungeonWallType.Full)
                .Where(pair => !IsWallPresentationBlocked(pair.Edge)
                    && !IsTorchMountEdge(pair.Edge)
                    && !IsWallPresentationCorner(pair.Edge))
                .OrderBy(pair => pair.Edge.CellEdge.FirstCell.y)
                .ThenBy(pair => pair.Edge.CellEdge.FirstCell.x)
                .ToArray();
            var panelEndpoints = new List<Vector2Int>();
            var placedGrate = false;
            var placedOssuary = false;

            for (var index = 0; index < northPairs.Length; index++)
            {
                var pair = northPairs[index];
                var sourceRenderer = pair.View.GetComponent<SpriteRenderer>();
                if (sourceRenderer == null || !sourceRenderer.enabled)
                    continue;

                var hash = PresentationHash(pair.Edge, PresentationDecorSeed + 17);
                var panelNearExisting = IsWallPanelNear(pair.Edge, panelEndpoints);
                Sprite sprite = null;
                if (!panelNearExisting || !placedGrate || !placedOssuary)
                {
                    if (!placedGrate && grate != null && index == 0)
                        sprite = grate;
                    else if (!placedOssuary && ossuary != null
                        && index >= Mathf.Max(1, northPairs.Length / 2))
                        sprite = ossuary;
                    else if (grate != null && hash % 31u < 3u)
                        sprite = grate;
                    else if (ossuary != null && hash % 47u == 0u)
                        sprite = ossuary;
                }
                if (sprite == null)
                    continue;

                var bounds = sourceRenderer.bounds;
                var faceCenter = new Vector3(
                    bounds.center.x,
                    bounds.min.y + DungeonWallSegmentView.NativeNorthWallFaceDepthWorld * .5f,
                    bounds.center.z);
                var decoration = CreateDecoration(
                    "Dungeon wall " + sprite.name,
                    sprite,
                    // Keep the overlay in the native wall SortingGroup so
                    // the source renderer's local order remains meaningful.
                    // CreateDecoration preserves world position/rotation and
                    // compensates the wall transform scale.
                    pair.View.transform,
                    faceCenter,
                    sourceRenderer.sortingOrder + 2,
                    Color.white,
                    1f,
                    sourceRenderer.sortingLayerID);
                decoration.IsWall = true;
                decoration.Edge = pair.Edge;
                presentationDecorations.Add(decoration);
                GetEdgeEndpoints(pair.Edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
                panelEndpoints.Add(firstEndpoint);
                panelEndpoints.Add(secondEndpoint);
                if (sprite == grate)
                    placedGrate = true;
                if (sprite == ossuary)
                    placedOssuary = true;
            }

            // The cobweb source includes a slate corner in its transparent
            // canvas. A vertical Side wall anchor would expose that canvas
            // over the floor, so leave it unused until a North-corner anchor
            // can be derived from the concrete wall profile.
        }

        private PresentationDecoration CreateDecoration(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector3 position,
            int sortingOrder,
            Color baseColor,
            float worldScale,
            int sourceSortingLayerId)
        {
            var gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            var parentScale = parent != null ? parent.lossyScale : Vector3.one;
            gameObject.transform.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > .0001f ? worldScale / parentScale.x : worldScale,
                Mathf.Abs(parentScale.y) > .0001f ? worldScale / parentScale.y : worldScale,
                Mathf.Abs(parentScale.z) > .0001f ? worldScale / parentScale.z : worldScale);

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingLayerID = sourceSortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.color = baseColor;

            return new PresentationDecoration
            {
                Object = gameObject,
                Renderer = renderer,
                BaseColor = renderer.color
            };
        }

        public IEnumerable<SpriteRenderer> LightingPresentationRenderers
        {
            get
            {
                foreach (var decoration in presentationDecorations)
                    if (decoration != null && !decoration.IsWall && decoration.Renderer != null)
                        yield return decoration.Renderer;
                foreach (var renderer in runtimeGroundPresentations)
                    if (renderer != null)
                        yield return renderer;
            }
        }

        public int PresentationDecorationCount => presentationDecorations.Count;
        public int PresentationFloorDecorationCount => presentationDecorations.Count(decoration =>
            decoration != null && !decoration.IsWall && decoration.Renderer != null);
        public bool HasCatacombsAuditDaggerPresentation => presentationDecorations.Any(decoration =>
            decoration != null && decoration.Renderer != null
            && decoration.Renderer.sprite != null
            && decoration.Renderer.sprite.name.IndexOf("catacombs-ground-dagger-v2", StringComparison.Ordinal) >= 0);
        public int PresentationWallDecorationCount => presentationDecorations.Count(decoration =>
            decoration != null && decoration.IsWall && decoration.Renderer != null);
        public int PresentationFloorVariantTileCount => presentationFloorVariantTiles == null
            ? 0
            : presentationFloorVariantTiles.Count(tile => tile != null);
        public IEnumerable<SpriteRenderer> PresentationRenderers
        {
            get
            {
                foreach (var decoration in presentationDecorations)
                    if (decoration != null && decoration.Renderer != null)
                        yield return decoration.Renderer;
            }
        }
        public IEnumerable<Vector2Int> PresentationOccupiedCells => presentationOccupiedCells;
        public string PresentationLayoutSignature => string.Join(";", presentationDecorations
            .Where(decoration => decoration != null && decoration.Renderer != null)
            .OrderBy(decoration => decoration.Renderer.sprite != null ? decoration.Renderer.sprite.name : string.Empty)
            .ThenBy(decoration => decoration.Renderer.transform.position.y)
            .ThenBy(decoration => decoration.Renderer.transform.position.x)
            .Select(decoration => (decoration.Renderer.sprite != null ? decoration.Renderer.sprite.name : "null")
                + ":" + Mathf.RoundToInt(decoration.Renderer.transform.position.x * 32f)
                + ":" + Mathf.RoundToInt(decoration.Renderer.transform.position.y * 32f)));

        private IEnumerable<SpriteRenderer> LightingPresentationWallRenderers
        {
            get
            {
                foreach (var decoration in presentationDecorations)
                    if (decoration != null && decoration.IsWall && decoration.Renderer != null)
                        yield return decoration.Renderer;
            }
        }

        private bool IsWallPresentationCorner(DungeonSurfaceEdge edge)
        {
            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            foreach (var pair in wallRenderers)
            {
                if (pair.View == null
                    || pair.View.SurfaceProfile == DungeonWallSurfaceProfile.North
                    || boundMap.GetWallType(pair.Edge) != Godmist.Core.Enums.DungeonWallType.Full)
                    continue;
                if ((pair.Edge.CellEdge.FirstCell == edge.CellEdge.FirstCell
                    && pair.Edge.CellEdge.SecondCell == edge.CellEdge.SecondCell)
                    || (pair.Edge.CellEdge.FirstCell == edge.CellEdge.SecondCell
                        && pair.Edge.CellEdge.SecondCell == edge.CellEdge.FirstCell))
                    continue;
                GetEdgeEndpoints(pair.Edge.CellEdge, out var first, out var second);
                if (first == firstEndpoint || first == secondEndpoint
                    || second == firstEndpoint || second == secondEndpoint)
                    return true;
            }
            return false;
        }

        private bool IsWallPanelNear(DungeonSurfaceEdge edge, List<Vector2Int> panelEndpoints)
        {
            GetEdgeEndpoints(edge.CellEdge, out var firstEndpoint, out var secondEndpoint);
            foreach (var panelEndpoint in panelEndpoints)
                if (Mathf.Abs(panelEndpoint.x - firstEndpoint.x) <= 1
                    && Mathf.Abs(panelEndpoint.y - firstEndpoint.y) <= 1
                    || Mathf.Abs(panelEndpoint.x - secondEndpoint.x) <= 1
                    && Mathf.Abs(panelEndpoint.y - secondEndpoint.y) <= 1)
                    return true;
            return false;
        }

        private bool IsWallPresentationBlocked(DungeonSurfaceEdge edge)
        {
            GetEdgeEndpoints(edge.CellEdge, out var first, out var second);
            foreach (var occupied in presentationOccupiedCells)
                if (Mathf.Abs(occupied.x - first.x) <= 1 && Mathf.Abs(occupied.y - first.y) <= 1
                    || Mathf.Abs(occupied.x - second.x) <= 1 && Mathf.Abs(occupied.y - second.y) <= 1)
                    return true;
            return false;
        }

        private bool IsTorchMountEdge(DungeonSurfaceEdge edge)
        {
            if (edge.Level != 0 || edge.CellEdge.FirstCell == edge.CellEdge.SecondCell)
                return false;
            foreach (var cell in presentationTorchMountCells)
            {
                if ((edge.CellEdge.FirstCell == cell && edge.CellEdge.SecondCell == cell + Vector2Int.up)
                    || (edge.CellEdge.SecondCell == cell && edge.CellEdge.FirstCell == cell + Vector2Int.up))
                    return true;
            }
            return false;
        }

        private void BuildPresentationTorchMountCells()
        {
            presentationTorchMountCells.Clear();
            var torchPositions = new List<Vector2>();
            foreach (var cell in boundMap.FloorCells.OrderBy(value => value.y).ThenBy(value => value.x))
            {
                if (boundMap.IsWalkable(cell + Vector2Int.up))
                    continue;

                var position = CellToWorldCenter(cell) + Vector2.up * .35f;
                if (torchPositions.Any(torch => Vector2.Distance(torch, position) < 5.5f))
                    continue;
                torchPositions.Add(position);
                presentationTorchMountCells.Add(cell);
            }
        }

        private void RefreshPresentationDecorations()
        {
            foreach (var decorationIndex in FogDecorationIndices())
            {
                var decoration = presentationDecorations[decorationIndex];
                if (decoration == null || decoration.Renderer == null)
                    continue;

                var knowledge = decoration.IsWall
                    ? PresentationEdgeKnowledge(decoration.Edge)
                    : PresentationKnowledge(new DungeonTraversalPosition(new DungeonSurfacePosition(decoration.Cell)));
                var hiddenClosedGate = decoration.IsWall
                    && FogFloor != null
                    && boundMap.SurfaceClosedProgressionGateEdges.Contains(decoration.Edge)
                    && !FogFloor.Fog.ClosedGates.Contains(decoration.Edge);
                var visible = decoration.IsWall
                    ? (IsStructuralPreviewEdgeVisible(decoration.Edge)
                            || knowledge != DungeonKnowledge.Unexplored)
                        && IsFogEdgeVisible(decoration.Edge)
                        && !hiddenClosedGate
                    : UsesVegetationPresentation
                        ? knowledge != DungeonKnowledge.Unexplored
                            && IsTraversalProjected(new DungeonTraversalPosition(new DungeonSurfacePosition(decoration.Cell)))
                        : IsSurfaceVisible(new DungeonSurfacePosition(decoration.Cell));
                var remembered = knowledge == DungeonKnowledge.Remembered;
                PresentFogSprite(decoration.Renderer, decoration.BaseColor, visible,
                    remembered ? RememberedFogBrightness : 1);
            }
        }

        private void ClearPresentationDecorations()
        {
            InvalidateForestLightingCoverage();
            fogDecorationIndexReady = false;
            foreach (var decoration in presentationDecorations)
            {
                if (decoration?.Object == null)
                    continue;
                if (decoration.Renderer != null)
                {
                    forestPropContacts.Remove(decoration.Renderer);
                    floorSortedPresentations.Remove(decoration.Renderer);
                    decoration.Renderer.forceRenderingOff = true;
                    decoration.Renderer.enabled = false;
                }
                if (Application.isPlaying)
                    Destroy(decoration.Object);
                else
                    DestroyImmediate(decoration.Object);
            }

            presentationDecorations.Clear();
            floorSortedPresentations.Clear();
            presentationTorchMountCells.Clear();
        }

        private Sprite LoadPresentationSprite(string resourceName)
        {
            if (presentationSprites.TryGetValue(resourceName, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(PresentationDecorResources + resourceName)
                ?? Resources.Load<Sprite>("CatacombsAudit/" + resourceName);
            if (sprite == null
                && resourceName.StartsWith("catacombs-", StringComparison.Ordinal)
                && DungeonCatacombsObjectVisuals.TryLoadPresentationSprite(
                    resourceName,
                    resourceName == "catacombs-ground-dagger-v2"
                        || resourceName == "catacombs-pillar"
                        || resourceName == "catacombs-lever"
                        || resourceName == "catacombs-lever-v4"
                        ? new Vector2(.5f, 0f)
                        : new Vector2(.5f, .5f),
                    out var runtimeSprite))
            {
                sprite = runtimeSprite;
            }
            presentationSprites[resourceName] = sprite;
            return sprite;
        }

        public static int ResolvePresentationFloorVariant(Vector2Int cell)
        {
            var groupX = FloorDivide(cell.x, 4);
            var groupY = FloorDivide(cell.y, 4);
            // Keep contiguous 4x4 modules together. Variant zero remains the
            // approved production module; authored alternatives are selected
            // sparsely so the room reads as a grouped floor rather than a
            // checkerboard of per-tile noise.
            var hash = PresentationHash(new Vector2Int(groupX, groupY), PresentationDecorSeed + 43);
            return hash % 7u < 2u ? 1 : 0;
        }

        private TileBase ResolvePresentationFloorTile(Vector3Int cell)
        {
            if (ResolvePresentationFloorVariant(new Vector2Int(cell.x, cell.y)) == 0)
                return null;

            EnsurePresentationFloorVariantLoaded();
            if (presentationFloorVariantTiles == null)
                return null;

            var column = PositiveModulo(cell.x, 4);
            var row = PositiveModulo(3 - cell.y, 4);
            return presentationFloorVariantTiles[row * 4 + column];
        }

        private void EnsurePresentationFloorVariantLoaded()
        {
            if (presentationFloorVariantLoaded)
                return;
            presentationFloorVariantLoaded = true;

            var tiles = new TileBase[16];
            for (var index = 0; index < tiles.Length; index++)
            {
                var sprite = Resources.Load<Sprite>(PresentationFloorVariantResources
                    + PresentationFloorVariantName + index.ToString("00"));
                if (sprite == null)
                {
                    DestroyPresentationFloorVariantTiles(tiles);
                    presentationFloorVariantTiles = null;
                    return;
                }

                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = PresentationFloorVariantName + index.ToString("00") + " (runtime)";
                tile.sprite = sprite;
                tiles[index] = tile;
            }

            presentationFloorVariantTiles = tiles;
        }

        private void DestroyPresentationFloorVariantTiles(TileBase[] tiles)
        {
            if (tiles == null)
                return;
            foreach (var tile in tiles)
                if (tile != null)
                {
                    if (Application.isPlaying)
                        Destroy(tile);
                    else
                        DestroyImmediate(tile);
                }
        }

        private static int FloorDivide(int value, int divisor)
        {
            var quotient = value / divisor;
            if (value < 0 && value % divisor != 0)
                quotient--;
            return quotient;
        }

        private static uint PresentationHash(Vector2Int cell, int salt)
        {
            unchecked
            {
                var hash = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663) ^ (uint)salt;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        private static uint PresentationHash(DungeonSurfaceEdge edge, int salt)
        {
            var hash = PresentationHash(edge.FirstCell, salt);
            return PresentationHash(edge.SecondCell, unchecked((int)(hash ^ (uint)(edge.Level * 83492791))));
        }
    }
}
