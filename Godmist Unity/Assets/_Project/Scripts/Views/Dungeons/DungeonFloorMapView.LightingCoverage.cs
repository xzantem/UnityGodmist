using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Floor-owned boundary trees, underlays and authored decorations have
        // stable geometry. Runtime props/summons remain in live registration
        // sets; moving a knife never needs these indices rebuilt.
        private readonly ForestLightingCoverage forestLightingCoverage = new();
        private readonly ForestLightingCoverage forestDecorationLightingCoverage = new();
        private readonly ForestLightingCoverage forestContactCoverage = new();
        private readonly List<SpriteRenderer> forestContactCandidates = new();
        private int forestContactCount = -1;
        private int forestLightingDecorationCount = -1;
        private bool forestLightingCoverageReady;
        private Matrix4x4 forestLightingOwnerMatrix;
        public int LightingCoverageQueryBuildCount => forestLightingCoverage.QueryBuildCount
            + forestDecorationLightingCoverage.QueryBuildCount + forestContactCoverage.QueryBuildCount;

        private void InvalidateForestLightingCoverage()
        {
            InvalidateWorldPresentation();
            forestLightingCoverageReady = false;
            forestLightingCoverage.Clear(); forestContactCoverage.Clear(); forestContactCount = -1;
            forestDecorationLightingCoverage.Clear(); forestLightingDecorationCount = -1;
        }

        private void EnsureForestLightingCoverage()
        {
            var matrix = (wallsRoot != null ? wallsRoot : transform).localToWorldMatrix;
            if (forestLightingCoverageReady && matrix == forestLightingOwnerMatrix
                && forestContactCount == forestPropContacts.Count
                && forestLightingDecorationCount == presentationDecorations.Count) return;
            forestLightingCoverage.Clear();
            foreach (var wall in wallRenderers)
                foreach (var renderer in wall.Renderers)
                    if (renderer is SpriteRenderer sprite && !IsArenaFireRenderer(sprite)) forestLightingCoverage.Add(sprite, false);
            foreach (var ground in vegetationGround) forestLightingCoverage.Add(ground.Renderer, true);
            forestDecorationLightingCoverage.Clear();
            foreach (var decoration in presentationDecorations)
                if (decoration != null) forestDecorationLightingCoverage.Add(decoration.Renderer, !decoration.IsWall);
            forestLightingDecorationCount = presentationDecorations.Count;
            forestContactCoverage.Clear();
            foreach (var contact in forestPropContacts) forestContactCoverage.Add(contact.Key, true);
            forestContactCount = forestPropContacts.Count;
            forestLightingOwnerMatrix = matrix;
            forestLightingCoverageReady = true;
        }

        public void CollectLightingWallRenderers(Rect viewport, List<SpriteRenderer> result)
        {
            result.Clear();
            if (!UsesVegetationPresentation) { result.AddRange(LightingWallRenderers); return; }
            EnsureForestLightingCoverage();
            forestLightingCoverage.Collect(viewport, false, result, exactBounds: true);
            // Static authored decorations retain source order within coverage.
            forestDecorationLightingCoverage.Collect(viewport, false, result, exactBounds: true);
        }

        public void CollectLightingGroundRenderers(Rect viewport, List<SpriteRenderer> result)
        {
            result.Clear();
            if (!UsesVegetationPresentation) { result.AddRange(LightingPresentationRenderers); return; }
            EnsureForestLightingCoverage();
            forestDecorationLightingCoverage.Collect(viewport, true, result, exactBounds: true);
            foreach (var renderer in runtimePropGroundPresentations)
                if (renderer != null) result.Add(renderer);
            var firstBoundary = result.Count;
            forestLightingCoverage.Collect(viewport, true, result, exactBounds: true);
            for (var i = result.Count - 1; i >= firstBoundary; i--)
                if (!runtimeGroundPresentations.Contains(result[i])) result.RemoveAt(i);
        }

        public void CollectForestPropContacts(Rect viewport, List<KeyValuePair<SpriteRenderer, Vector2>> result)
        {
            result.Clear();
            EnsureForestLightingCoverage();
            forestContactCandidates.Clear();
            forestContactCoverage.Collect(viewport, true, forestContactCandidates);
            foreach (var renderer in forestContactCandidates)
                if (forestPropContacts.TryGetValue(renderer, out var foot))
                    result.Add(new KeyValuePair<SpriteRenderer, Vector2>(renderer, foot));
        }

        private sealed class ForestLightingCoverage
        {
            private const float BucketSize = 8;
            private readonly List<(SpriteRenderer Renderer, bool Ground, Bounds Bounds)> sources = new();
            private readonly Dictionary<Vector2Int, List<int>> buckets = new();
            private readonly HashSet<int> visited = new();
            private readonly List<int> candidates = new();
            // Lighting and contacts alternate between coverage and coverage
            // plus a .1-unit fringe. Keep both exact queries so neither
            // rebuilds the other's bucket selection on every lighting tick.
            private readonly List<int>[] cachedCandidates = { new(), new(), new(), new(), new(), new(), new(), new() };
            private readonly Rect[] cachedViewports = new Rect[8];
            private readonly bool[] cachedQueries = new bool[8];
            private readonly int[] lastQuerySlots = { 0, 1, 2, 3 };
            public int QueryBuildCount { get; private set; }

            public void Clear()
            {
                sources.Clear(); buckets.Clear(); visited.Clear(); candidates.Clear();
                for (int i = 0; i < cachedQueries.Length; i++) { cachedQueries[i] = false; cachedCandidates[i].Clear(); }
            }
            public void Add(SpriteRenderer renderer, bool ground)
            {
                if (renderer == null) return;
                for (int i = 0; i < cachedQueries.Length; i++) cachedQueries[i] = false;
                var index = sources.Count;
                var bounds = renderer.bounds;
                sources.Add((renderer, ground, bounds));
                var first = Bucket(bounds.min); var last = Bucket(bounds.max);
                for (var y = first.y; y <= last.y; y++)
                for (var x = first.x; x <= last.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!buckets.TryGetValue(cell, out var list)) buckets.Add(cell, list = new List<int>());
                    list.Add(index);
                }
            }
            public void Collect(Rect viewport, bool ground, List<SpriteRenderer> result, bool exactBounds = false)
            {
                int query = (ground ? 1 : 0) + (exactBounds ? 2 : 0);
                int slot;
                if (cachedQueries[query] && cachedViewports[query].Equals(viewport)) slot = query;
                else if (cachedQueries[query + 4] && cachedViewports[query + 4].Equals(viewport)) slot = query + 4;
                else slot = !cachedQueries[query] || lastQuerySlots[query] == query + 4 ? query : query + 4;
                lastQuerySlots[query] = slot;
                var selected = cachedCandidates[slot];
                if (!cachedQueries[slot] || !cachedViewports[slot].Equals(viewport))
                {
                QueryBuildCount++;
                visited.Clear(); candidates.Clear();
                var first = Bucket(viewport.min); var last = Bucket(viewport.max);
                for (var y = first.y; y <= last.y; y++)
                for (var x = first.x; x <= last.x; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, y), out var list))
                        foreach (var index in list)
                            if (sources[index].Ground == ground && visited.Add(index)) candidates.Add(index);
                // Preserve source order, including contact-band precedence.
                candidates.Sort();
                selected.Clear(); selected.AddRange(candidates);
                cachedQueries[slot] = true; cachedViewports[slot] = viewport;
                }
                foreach (var index in selected)
                {
                    var source = sources[index];
                    if (source.Renderer == null) continue;
                    if (exactBounds && (source.Bounds.max.x < viewport.xMin || source.Bounds.min.x > viewport.xMax
                        || source.Bounds.max.y < viewport.yMin || source.Bounds.min.y > viewport.yMax)) continue;
                    result.Add(source.Renderer);
                }
            }
            private static Vector2Int Bucket(Vector2 point) => new(Mathf.FloorToInt(point.x / BucketSize), Mathf.FloorToInt(point.y / BucketSize));
        }
    }
}
