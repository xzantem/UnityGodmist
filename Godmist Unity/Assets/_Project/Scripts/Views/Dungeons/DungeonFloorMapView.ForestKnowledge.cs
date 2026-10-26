using Godmist.Runtime.Dungeons;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Linear-light transmission: remembered woodland must read distinctly
        // darker than current LOS, while preserving the underlying scenery.
        private const float ForestRememberedTransmission = .35f;
        // One scene-owned filter above woodland scenery. The continuous litter
        // backdrop fills geometry gaps; it must not bypass discovery.
        private SpriteRenderer forestKnowledgeRenderer;
        private Texture2D forestKnowledgeTexture;
        private Sprite forestKnowledgeSprite;
        private Color32[] forestKnowledgePixels;
        private float[] forestKnowledgeTargets;
        private Vector2Int forestKnowledgeOrigin;
        private DungeonFogState forestKnowledgeState;
        private int forestKnowledgeRevision = -1, forestKnowledgeDebugRevision = -1;
        private bool forestKnowledgeAnimating;
        private float[] forestRememberedTargets;
        private bool[] forestKnowledgeWalkable, forestKnowledgeDirtyMask;
        private readonly HashSet<DungeonTraversalPosition> forestRememberedSources = new(), forestVisibleSources = new();
        private Dictionary<int, float> forestVisibleTargets = new(), nextForestVisibleTargets = new();
        private readonly List<(Vector2Int Offset, float Weight)> forestKnowledgeStencil = new();
        private readonly List<int> forestKnowledgeDirty = new(), forestKnowledgeFinished = new();
        private readonly HashSet<int> forestKnowledgeActive = new();
        private DungeonFloorMap forestKnowledgeMap;
        private int forestKnowledgeMapRevision = -1, forestKnowledgeDepth = -1, forestKnowledgeDiscoveredCount = -1;
        private static readonly Unity.Profiling.ProfilerMarker ForestKnowledgeRetargetSample = new("Godmist.Dungeon.Fog.ForestKnowledge.Retarget");
        private static readonly Unity.Profiling.ProfilerMarker ForestKnowledgeAnimateSample = new("Godmist.Dungeon.Fog.ForestKnowledge.Animate");
        public int ForestKnowledgeDiscoveryScanCount { get; private set; }
        public int ForestKnowledgeStencilSampleCount { get; private set; }
        public int ForestKnowledgeAnimationPixelCount { get; private set; }

        private void RefreshForestKnowledge()
        {
            using var sample = ForestKnowledgeRetargetSample.Auto();
            if (!UsesVegetationPresentation || forestBackdrop == null) return;
            var fog = FogFloor?.Fog;
            bool show = fog != null && !fog.DebugFogDisabled;
            if (forestKnowledgeRenderer != null) forestKnowledgeRenderer.enabled = show;
            if (!show) return;
            if (forestKnowledgeTexture == null) CreateForestKnowledge();
            forestKnowledgeRenderer.enabled = true;
            forestKnowledgeRenderer.sortingLayerID = FloorFeedbackSortingLayerID;
            int depth = ForestBoundaryDepthInCells;
            bool rebuild = !ReferenceEquals(forestKnowledgeState, fog) || !ReferenceEquals(forestKnowledgeMap, boundMap)
                || forestKnowledgeMapRevision != boundMap.TraversalRevision || forestKnowledgeDepth != depth
                || forestKnowledgeDebugRevision != fog.DebugRevision || fog.Discovered.Count < forestKnowledgeDiscoveredCount;
            if (!rebuild && forestKnowledgeRevision == fog.Revision && forestKnowledgeDiscoveredCount == fog.Discovered.Count) return;
            bool snap = snapFogAnimation || rebuild;
            forestKnowledgeState = fog;
            forestKnowledgeRevision = fog.Revision;
            forestKnowledgeDebugRevision = fog.DebugRevision;
            if (rebuild) ResetForestKnowledgeCache(depth);
            // Discovery is monotonic between debug resets. Only new sources
            // paint the remembered layer; walking over old ground never repaints it.
            if (rebuild || forestKnowledgeDiscoveredCount != fog.Discovered.Count)
                foreach (var position in fog.Discovered)
                {
                    ForestKnowledgeDiscoveryScanCount++;
                    if (forestRememberedSources.Add(position)) PaintForestKnowledge(position, true);
                }
            forestKnowledgeDiscoveredCount = fog.Discovered.Count;
            // Line of sight can extend beyond the permanent discovery radius.
            // Rebuild only its small stencil. Old visible contributions must be
            // removed so overlapping crowns fade back to the remembered maximum.
            if (rebuild || !forestVisibleSources.SetEquals(fog.Visible))
            {
                nextForestVisibleTargets.Clear();
                foreach (var position in fog.Visible) PaintForestKnowledge(position, false);
                foreach (var index in forestVisibleTargets.Keys) MarkForestKnowledgeDirty(index);
                var swap = forestVisibleTargets; forestVisibleTargets = nextForestVisibleTargets; nextForestVisibleTargets = swap;
                forestVisibleSources.Clear(); forestVisibleSources.UnionWith(fog.Visible);
            }
            foreach (var index in forestKnowledgeDirty)
            {
                forestVisibleTargets.TryGetValue(index, out var visible);
                var target = Mathf.Max(forestRememberedTargets[index], visible);
                if (forestKnowledgeTargets[index] != target)
                { forestKnowledgeTargets[index] = target; forestKnowledgeActive.Add(index); }
                forestKnowledgeDirtyMask[index] = false;
            }
            forestKnowledgeDirty.Clear();
            forestKnowledgeAnimating = forestKnowledgeActive.Count > 0;
            AdvanceForestKnowledge(snap ? 1 : 0);
        }

        private void ResetForestKnowledgeCache(int depth)
        {
            forestKnowledgeMap = boundMap; forestKnowledgeMapRevision = boundMap.TraversalRevision; forestKnowledgeDepth = depth;
            forestRememberedSources.Clear(); forestVisibleSources.Clear(); forestVisibleTargets.Clear(); nextForestVisibleTargets.Clear();
            System.Array.Clear(forestRememberedTargets, 0, forestRememberedTargets.Length);
            System.Array.Clear(forestKnowledgeWalkable, 0, forestKnowledgeWalkable.Length);
            int width = forestKnowledgeTexture.width, height = forestKnowledgeTexture.height;
            foreach (var cell in boundMap.FloorCells)
            {
                int x = cell.x - forestKnowledgeOrigin.x, y = cell.y - forestKnowledgeOrigin.y;
                if (x >= 0 && y >= 0 && x < width && y < height) forestKnowledgeWalkable[y * width + x] = true;
            }
            forestKnowledgeStencil.Clear();
            for (int y = -depth; y <= depth; y++) for (int x = -depth; x <= depth; x++)
            {
                float weight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1f, depth, Mathf.Sqrt(x*x + y*y)));
                if (weight > 0) forestKnowledgeStencil.Add((new Vector2Int(x, y), weight));
            }
            // Also clear stale targets after clearmap, another floor or geometry changes.
            for (int i = 0; i < forestKnowledgeTargets.Length; i++) MarkForestKnowledgeDirty(i);
        }

        private void MarkForestKnowledgeDirty(int index)
        {
            if (forestKnowledgeDirtyMask[index]) return;
            forestKnowledgeDirtyMask[index] = true; forestKnowledgeDirty.Add(index);
        }

        private void PaintForestKnowledge(DungeonTraversalPosition position, bool remembered)
        {
            if (!boundMap.TryGetTraversalCell(position, out var cell)) return;
            int width = forestKnowledgeTexture.width, height = forestKnowledgeTexture.height;
            int sx = cell.x - forestKnowledgeOrigin.x, sy = cell.y - forestKnowledgeOrigin.y;
            foreach (var entry in forestKnowledgeStencil)
            {
                ForestKnowledgeStencilSampleCount++;
                int x = sx + entry.Offset.x, y = sy + entry.Offset.y;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                int index = y * width + x;
                // Decorative fringe cannot illuminate another playable tile.
                if (entry.Offset != Vector2Int.zero && forestKnowledgeWalkable[index]) continue;
                if (remembered) forestRememberedTargets[index] = Mathf.Max(forestRememberedTargets[index], ForestRememberedTransmission * entry.Weight);
                else
                {
                    nextForestVisibleTargets.TryGetValue(index, out var old);
                    nextForestVisibleTargets[index] = Mathf.Max(old, entry.Weight);
                }
                MarkForestKnowledgeDirty(index);
            }
        }

        private void CreateForestKnowledge()
        {
            var bounds = forestBackdrop.bounds;
            forestKnowledgeOrigin = WorldToCell(bounds.min);
            var end = WorldToCell(bounds.max) + Vector2Int.one;
            var size = end - forestKnowledgeOrigin;
            forestKnowledgeTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, true)
            { name = "Forest discovery filter", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            forestKnowledgePixels = new Color32[size.x * size.y];
            forestKnowledgeTargets = new float[forestKnowledgePixels.Length];
            forestRememberedTargets = new float[forestKnowledgePixels.Length];
            forestKnowledgeWalkable = new bool[forestKnowledgePixels.Length];
            forestKnowledgeDirtyMask = new bool[forestKnowledgePixels.Length];
            for (int i = 0; i < forestKnowledgePixels.Length; i++) forestKnowledgePixels[i] = new Color32(0, 0, 0, 255);
            forestKnowledgeTexture.SetPixels32(forestKnowledgePixels); forestKnowledgeTexture.Apply(false, false);
            var cellSize = CellToWorldCenter(Vector2Int.one) - CellToWorldCenter(Vector2Int.zero);
            forestKnowledgeSprite = Sprite.Create(forestKnowledgeTexture, new Rect(0, 0, size.x, size.y),
                Vector2.zero, 1, 0, SpriteMeshType.FullRect);
            var root = new GameObject("Forest discovery filter");
            root.layer = floorTilemap.gameObject.layer;
            root.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
            root.transform.position = CellToWorldCenter(forestKnowledgeOrigin) - cellSize * .5f;
            root.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1);
            forestKnowledgeRenderer = root.AddComponent<SpriteRenderer>();
            forestKnowledgeRenderer.sprite = forestKnowledgeSprite;
            forestKnowledgeRenderer.sortingLayerID = FloorFeedbackSortingLayerID;
            // World depth is reserved below this boundary; keep HUD lanes free.
            forestKnowledgeRenderer.sortingOrder = DepthSortingBandTopOrderExclusive;
            spawnedWalls.Add(root);
        }

        private void AdvanceForestKnowledge(float deltaTime)
        {
            if (!forestKnowledgeAnimating || forestKnowledgeTexture == null) return;
            using var sample = ForestKnowledgeAnimateSample.Auto();
            bool changed = false;
            forestKnowledgeFinished.Clear();
            float step = Mathf.Max(0, deltaTime) / DungeonFogTransition.Duration;
            foreach (int i in forestKnowledgeActive)
            {
                ForestKnowledgeAnimationPixelCount++;
                float target = 1 - forestKnowledgeTargets[i];
                float alpha = Mathf.MoveTowards(forestKnowledgePixels[i].a / 255f, target, step);
                byte value = (byte)Mathf.RoundToInt(alpha * 255);
                changed |= forestKnowledgePixels[i].a != value;
                forestKnowledgePixels[i].a = value;
                if (Mathf.Abs(value / 255f - target) <= 1f / 255) forestKnowledgeFinished.Add(i);
            }
            foreach (int i in forestKnowledgeFinished) forestKnowledgeActive.Remove(i);
            if (changed)
            {
                forestKnowledgeTexture.SetPixels32(forestKnowledgePixels); forestKnowledgeTexture.Apply(false, false);
            }
            forestKnowledgeAnimating = forestKnowledgeActive.Count > 0;
        }

        private void ClearForestKnowledge()
        {
            if (Application.isPlaying) { Destroy(forestKnowledgeSprite); Destroy(forestKnowledgeTexture); }
            else { DestroyImmediate(forestKnowledgeSprite); DestroyImmediate(forestKnowledgeTexture); }
            forestKnowledgeRenderer = null; forestKnowledgeSprite = null; forestKnowledgeTexture = null;
            forestKnowledgePixels = null; forestKnowledgeTargets = null; forestKnowledgeState = null;
            forestKnowledgeRevision = forestKnowledgeDebugRevision = -1; forestKnowledgeAnimating = false;
            forestRememberedTargets = null; forestKnowledgeWalkable = null; forestKnowledgeDirtyMask = null;
            forestRememberedSources.Clear(); forestVisibleSources.Clear(); forestVisibleTargets.Clear(); nextForestVisibleTargets.Clear();
            forestKnowledgeDirty.Clear(); forestKnowledgeFinished.Clear(); forestKnowledgeActive.Clear(); forestKnowledgeStencil.Clear();
            forestKnowledgeMap = null; forestKnowledgeMapRevision = forestKnowledgeDepth = forestKnowledgeDiscoveredCount = -1;
        }
    }
}
