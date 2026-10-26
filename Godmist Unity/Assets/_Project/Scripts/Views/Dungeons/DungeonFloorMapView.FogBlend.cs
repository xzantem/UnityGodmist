using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private Dictionary<Vector2Int, DungeonKnowledge> fogProjection = new();
        private readonly DungeonKnowledge[] fogNeighbours = new DungeonKnowledge[8];
        private int forestFogVoidMask;
        private readonly List<Vector3> fogVertices = new();
        private readonly List<Color> fogColors = new();
        private readonly List<Vector2> fogUVs = new();
        private readonly List<int> fogIndices = new();
        private Mesh fogBlendMesh;
        private Material fogBlendMaterial;
        private MeshRenderer fogBlendRenderer;
        private Material forestFogGroundMaterial;
        private const int FogSubdivisions = 8;
        private readonly List<Vector2Int> fogMeshCells = new();
        private readonly Dictionary<Vector2Int, int> fogMeshSlots = new();
        private readonly Stack<int> freeFogMeshSlots = new();
        private readonly Dictionary<int, float[]> fogSamplePatterns = new();
        private readonly Dictionary<Vector2Int, (int Pattern, Color Color)> fogTargetPatterns = new();
        private readonly HashSet<Vector2Int> changedFogPatternCells = new();
        private readonly List<KeyValuePair<Vector2Int, DungeonKnowledge>> fogPatternCandidates = new();
        private readonly HashSet<int> dirtyFogColorSlots = new();
        private readonly List<int> orderedDirtyFogColorSlots = new();
        private readonly List<int> dirtyFogGeometrySlots = new();
        private static readonly Unity.Profiling.ProfilerMarker FogMeshBuildSample = new("Godmist.Dungeon.Fog.BuildMesh");
        private static readonly Unity.Profiling.ProfilerMarker FogGeometryUploadSample = new("Godmist.Dungeon.Fog.UploadGeometry");
        public int FogGeometryVertexUploadCount { get; private set; }
        private static readonly VertexAttributeDescriptor[] FogVertexLayout = {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2) };
        private static readonly Unity.Profiling.ProfilerMarker FogUploadSample = new("Godmist.Dungeon.Fog.UploadColors");
        public int FogColorVertexUploadCount { get; private set; }
        public int FogColorPatchWriteCount { get; private set; }
        public int FogPatternEvaluationCount { get; private set; }
        public int FogColorUploadCount { get; private set; }
        public int FogPatternCandidateCount { get; private set; }
        public int FogColorOnlyRefreshCount { get; private set; }

        private void CollectChangedFogPatterns()
        {
            changedFogPatternCells.Clear();
            if (snapFogAnimation) return;
            foreach (var pair in fogProjection)
                if (!previousFogProjection.TryGetValue(pair.Key, out var old) || old != pair.Value)
                    MarkFogPatternNeighbours(pair.Key);
            foreach (var pair in previousFogProjection)
                if (!fogProjection.ContainsKey(pair.Key)) MarkFogPatternNeighbours(pair.Key);
        }

        private void MarkFogPatternNeighbours(Vector2Int cell)
        {
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                changedFogPatternCells.Add(cell + new Vector2Int(x, y));
        }

        private void RefreshSoftFog()
        {
            RefreshForestKnowledge();
            var retargetTiming = new FogPhaseTiming(this, 10);
            CollectChangedFogPatterns();
            expiredFogCells.Clear();
            foreach (var pair in fogCellVisuals)
                if (!fogProjection.TryGetValue(pair.Key, out var state) || state == DungeonKnowledge.Unexplored)
                    expiredFogCells.Add(pair.Key);
            foreach (var cell in expiredFogCells) fogCellVisuals.Remove(cell);
            foreach (var cell in expiredFogCells) fogTargetPatterns.Remove(cell);
            var changed = expiredFogCells.Count > 0;
            var membershipChanged = changed;
            if (FogFloor != null && !FogFloor.Fog.DebugFogDisabled)
            {
                fogPatternCandidates.Clear();
                if (snapFogAnimation) fogPatternCandidates.AddRange(fogProjection);
                else
                {
                    // Interior cells with unchanged knowledge have no patch
                    // to retarget. Existing patches still need their live
                    // target colour checked, including active transitions.
                    foreach (var cell in changedFogPatternCells)
                        if (fogProjection.TryGetValue(cell, out var state))
                            fogPatternCandidates.Add(new(cell, state));
                    foreach (var cell in fogCellVisuals.Keys)
                        if (!changedFogPatternCells.Contains(cell))
                            fogPatternCandidates.Add(new(cell, fogProjection[cell]));
                }
                FogPatternCandidateCount += fogPatternCandidates.Count;
                foreach (var pair in fogPatternCandidates)
                {
                    if (pair.Value == DungeonKnowledge.Unexplored) continue;
                    var hadCell = previousFogProjection.TryGetValue(pair.Key, out var previous)
                        && previous != DungeonKnowledge.Unexplored;
                    var cached = fogCellVisuals.TryGetValue(pair.Key, out var visual);
                    var color = presentedFloorCells[new Vector3Int(pair.Key.x, pair.Key.y, 0)].Color;
                    // A boundary pattern depends only on this cell and its
                    // eight neighbours. Reuse unchanged patterns during a pan
                    // or a local reveal, including their in-flight transition.
                    if (!snapFogAnimation && !changedFogPatternCells.Contains(pair.Key))
                    {
                        if (cached && fogTargetPatterns.TryGetValue(pair.Key, out var retained)
                            && retained.Color == color) continue;
                        if (!cached && hadCell && previous == pair.Value) continue;
                    }
                    var boundary = ReadFogNeighbours(fogProjection, pair.Key, pair.Value);
                    if (!cached && !boundary && (snapFogAnimation || hadCell && previous == pair.Value)) continue;
                    var patternKey = FogPatternKey(pair.Value);
                    if (cached && !snapFogAnimation && fogTargetPatterns.TryGetValue(pair.Key, out var oldTarget)
                        && oldTarget.Pattern == patternKey && oldTarget.Color == color) continue;
                    fogTargetPatterns[pair.Key] = (patternKey, color);
                    changed = true;
                    var baseline = DungeonFogBlend.Brightness(pair.Value);
                    SampleFogBrightness(pair.Value, fogTargetSamples);
                    if (!cached)
                    {
                        var startBaseline = baseline;
                        if (snapFogAnimation)
                            System.Array.Copy(fogTargetSamples, fogStartSamples, fogTargetSamples.Length);
                        else if (hadCell)
                        {
                            startBaseline = DungeonFogBlend.Brightness(previous);
                            ReadFogNeighbours(previousFogProjection, pair.Key, previous);
                            SampleFogBrightness(previous, fogStartSamples);
                        }
                        else System.Array.Clear(fogStartSamples, 0, fogStartSamples.Length);
                        visual = new FogCellVisual { Transition = new DungeonFogTransition(startBaseline, fogStartSamples) };
                        fogCellVisuals.Add(pair.Key, visual);
                        membershipChanged = true;
                    }
                    visual.Boundary = boundary; visual.TargetBaseline = baseline;
                    visual.TargetColor = color;
                    visual.Transition.Retarget(baseline, fogTargetSamples, snapFogAnimation);
                    visual.ColorsDirty = true;
                    ApplyFogCellColor(pair.Key, visual);
                }
            }
            else
            {
                membershipChanged |= fogCellVisuals.Count > 0;
                changed |= membershipChanged; fogCellVisuals.Clear(); fogTargetPatterns.Clear();
            }
            retargetTiming.Dispose();
            if (membershipChanged || fogMeshSlots.Count != fogCellVisuals.Count
                || changed && (fogBlendMesh == null || fogBlendRenderer == null)) BuildFogMesh();
            else if (changed)
            {
                // Retargeting existing patches changes colours, not membership,
                // vertices or indices. Preserve pending dirty colours as well.
                foreach (var visual in fogCellVisuals.Values)
                    if (visual.ColorsDirty) WriteFogCellColors(visual);
                UpdateFogSorting();
                fogBlendRenderer.enabled = true;
                UploadFogColors(false); FogColorOnlyRefreshCount++;
            }
        }

        private bool ReadFogNeighbours(Dictionary<Vector2Int, DungeonKnowledge> projection, Vector2Int cell, DungeonKnowledge center)
        {
            FogPatternEvaluationCount++;
            var index = 0; var boundary = false;
            forestFogVoidMask = 0;
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var adjacent = cell + new Vector2Int(x, y);
                if (UsesVegetationPresentation && !boundMap.IsWalkable(adjacent))
                    forestFogVoidMask |= 1 << index;
                // The unwalkable woodland has its own remembered trees and
                // overlapping soil. It is not an unknown floor tile: painting
                // fog here creates a stepped black outline at the map boundary.
                // Real unexplored floor still receives the ordinary frontier.
                var state = projection.TryGetValue(adjacent, out var neighbour) ? neighbour
                    : UsesVegetationPresentation && boundMap.IsWalkable(adjacent)
                        ? DungeonKnowledge.Unexplored : center;
                fogNeighbours[index++] = state; boundary |= state < center;
            }
            return boundary;
        }

        private int FogPatternKey(DungeonKnowledge state)
        {
            var key = (int)state;
            for (var i = 0; i < 8; i++) key = key * 3 + (int)fogNeighbours[i];
            return key * 512 + (UsesVegetationPresentation ? 256 | forestFogVoidMask : 0);
        }
        private void SampleFogBrightness(DungeonKnowledge state, float[] samples)
        {
            var key = FogPatternKey(state);
            if (!fogSamplePatterns.TryGetValue(key, out var pattern))
            {
                pattern = new float[81];
                DungeonFogBlend.SampleGridBrightness(state, fogNeighbours, pattern, UsesVegetationPresentation, forestFogVoidMask);
                fogSamplePatterns.Add(key, pattern);
            }
            System.Array.Copy(pattern, samples, 81);
        }

        private void BuildFogMesh()
        {
            using var timing = new FogPhaseTiming(this, 11);
            using var sample = FogMeshBuildSample.Auto();
            if (fogCellVisuals.Count == 0) { ClearSoftFog(); return; }
            dirtyFogGeometrySlots.Clear();
            // Boundary cells change locally as the party walks. Keep stable
            // vertex slots, recycling retired cells instead of rebuilding all
            // 81-vertex patches and their triangles for every changed cell.
            expiredFogCells.Clear();
            foreach (var cell in fogMeshSlots.Keys)
                if (!fogCellVisuals.ContainsKey(cell)) expiredFogCells.Add(cell);
            foreach (var cell in expiredFogCells)
            {
                int slot = fogMeshSlots[cell];
                for (int i = 0; i < 81; i++) fogColors[slot * 81 + i] = Color.clear;
                dirtyFogColorSlots.Add(slot);
                fogMeshSlots.Remove(cell); freeFogMeshSlots.Push(slot);
            }
            bool geometryChanged = false, topologyChanged = false;
            var colorsChanged = expiredFogCells.Count > 0;
            foreach (var pair in fogCellVisuals)
            {
                var newSlot = false;
                if (!fogMeshSlots.TryGetValue(pair.Key, out var slot))
                {
                    newSlot = true;
                    geometryChanged = true;
                    if (freeFogMeshSlots.Count > 0)
                    {
                        slot = freeFogMeshSlots.Pop(); fogMeshCells[slot] = pair.Key;
                        WriteFogBoundaryVertices(pair.Key, slot * 81);
                    }
                    else
                    {
                        slot = fogMeshCells.Count; fogMeshCells.Add(pair.Key);
                        AddFogBoundary(pair.Key, pair.Value); topologyChanged = true;
                    }
                    fogMeshSlots.Add(pair.Key, slot);
                    dirtyFogGeometrySlots.Add(slot);
                }
                pair.Value.FirstVertex = slot * 81;
                if (newSlot || pair.Value.ColorsDirty)
                { WriteFogCellColors(pair.Value); colorsChanged = true; }
            }
            if (fogBlendRenderer == null)
            {
                var root = new GameObject("Soft terrain fog boundary", typeof(MeshFilter), typeof(MeshRenderer));
                root.layer = floorTilemap.gameObject.layer; root.transform.SetParent(floorTilemap.transform, false);
                fogBlendMesh = new Mesh { name = "Soft terrain fog boundary", indexFormat = IndexFormat.UInt32 };
                fogBlendMesh.MarkDynamic(); root.GetComponent<MeshFilter>().sharedMesh = fogBlendMesh;
                fogBlendMaterial = new Material(Shader.Find("Sprites/Default"))
                    { name = "Soft terrain fog boundary", mainTexture = Texture2D.whiteTexture };
                fogBlendRenderer = root.GetComponent<MeshRenderer>(); fogBlendRenderer.sharedMaterial = fogBlendMaterial;
                forestFogGroundMaterial = null;
                fogBlendRenderer.shadowCastingMode = ShadowCastingMode.Off; fogBlendRenderer.receiveShadows = false;
            }
            UpdateFogSorting();
            if (topologyChanged) fogBlendMesh.Clear();
            if (topologyChanged)
            {
                fogBlendMesh.SetVertexBufferParams(fogVertices.Count, FogVertexLayout);
                fogBlendMesh.SetVertexBufferData(fogUVs, 0, 0, fogUVs.Count, 2);
            }
            if (geometryChanged) UploadFogGeometry(topologyChanged);
            if (colorsChanged || geometryChanged) UploadFogColors(topologyChanged);
            if (topologyChanged) fogBlendMesh.SetTriangles(fogIndices, 0, false);
            if (geometryChanged) fogBlendMesh.RecalculateBounds();
            fogBlendRenderer.enabled = true;
        }

        private bool UpdateFogSorting()
        {
            var floorRenderer = floorTilemap.GetComponent<TilemapRenderer>();
            fogBlendRenderer.sortingLayerID = floorRenderer.sortingLayerID;
            fogBlendRenderer.sortingOrder = floorRenderer.sortingOrder + 1;
            var material = floorRenderer.sharedMaterial;
            var ground = UsesVegetationPresentation && material != null
                && material.HasProperty("_ForestFogLitter") && material.GetFloat("_ForestSurfaceEnabled") > .5f
                ? material : null;
            if (forestFogGroundMaterial == ground) return false;
            forestFogGroundMaterial = ground;
            // Over known forest, fog blends the route into continuous litter
            // instead of stamping an opaque black tile against visible woods.
            // This uses the live floor material/light buffers and no new
            // material instance. Unknown tiles/units remain logically hidden.
            fogBlendRenderer.sharedMaterial = ground != null ? ground : fogBlendMaterial;
            if (ground != null)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetTexture("_MainTex", Texture2D.whiteTexture);
                properties.SetColor("_Color", vegetationPresentation.GroundTint);
                properties.SetFloat("_ForestSurfaceEnabled", 1);
                properties.SetFloat("_ForestPropReadability", 0);
                properties.SetFloat("_ForestFogLitter", 1);
                properties.SetFloat("_ForestFogMesh", 1);
                fogBlendRenderer.SetPropertyBlock(properties);
            }
            else fogBlendRenderer.SetPropertyBlock(null);
            foreach (var visual in fogCellVisuals.Values) WriteFogCellColors(visual);
            return true;
        }

        private void UploadFogGeometry(bool full)
        {
            using var timing = new FogPhaseTiming(this, 12);
            using var sample = FogGeometryUploadSample.Auto();
            dirtyFogGeometrySlots.Sort();
            int runs = 0;
            for (int i = 0; i < dirtyFogGeometrySlots.Count; i++)
                if (i == 0 || dirtyFogGeometrySlots[i] != dirtyFogGeometrySlots[i - 1] + 1) runs++;
            full |= dirtyFogGeometrySlots.Count * 81 >= fogVertices.Count / 2 || runs > 8;
            if (full)
            {
                fogBlendMesh.SetVertexBufferData(fogVertices, 0, 0, fogVertices.Count, 0);
                FogGeometryVertexUploadCount += fogVertices.Count;
            }
            else
            {
                for (int i = 0; i < dirtyFogGeometrySlots.Count;)
                {
                    int first = dirtyFogGeometrySlots[i], last = first;
                    while (++i < dirtyFogGeometrySlots.Count && dirtyFogGeometrySlots[i] == last + 1)
                        last = dirtyFogGeometrySlots[i];
                    int start = first * 81, count = (last - first + 1) * 81;
                    fogBlendMesh.SetVertexBufferData(fogVertices, start, start, count, 0);
                    FogGeometryVertexUploadCount += count;
                }
            }
            dirtyFogGeometrySlots.Clear();
        }

        private void UploadFogColors(bool full = false)
        {
            using var timing = new FogPhaseTiming(this, 13);
            using var sample = FogUploadSample.Auto();
            if (fogBlendMesh == null || fogColors.Count == 0) return;
            if (!full)
            {
                orderedDirtyFogColorSlots.Clear(); orderedDirtyFogColorSlots.AddRange(dirtyFogColorSlots);
                orderedDirtyFogColorSlots.Sort();
                int runs = 0;
                for (int i = 0; i < orderedDirtyFogColorSlots.Count; i++)
                    if (i == 0 || orderedDirtyFogColorSlots[i] != orderedDirtyFogColorSlots[i - 1] + 1) runs++;
                // Many sparse writes cost more native calls than a full stream.
                full = dirtyFogColorSlots.Count * 81 >= fogColors.Count / 2 || runs > 8;
            }
            if (full)
            {
                fogBlendMesh.SetVertexBufferData(fogColors, 0, 0, fogColors.Count, 1);
                FogColorVertexUploadCount += fogColors.Count;
            }
            else
            {
                for (int i = 0; i < orderedDirtyFogColorSlots.Count;)
                {
                    int first = orderedDirtyFogColorSlots[i], last = first;
                    while (++i < orderedDirtyFogColorSlots.Count && orderedDirtyFogColorSlots[i] == last + 1)
                        last = orderedDirtyFogColorSlots[i];
                    int start = first * 81, count = (last - first + 1) * 81;
                    fogBlendMesh.SetVertexBufferData(fogColors, start, start, count, 1);
                    FogColorVertexUploadCount += count;
                }
            }
            dirtyFogColorSlots.Clear(); FogColorUploadCount++;
        }

        private void AddFogBoundary(Vector2Int cell, FogCellVisual visual)
        {
            var first = fogVertices.Count; var stride = FogSubdivisions + 1;
            visual.FirstVertex = first;
            for (var y = 0; y <= FogSubdivisions; y++)
            for (var x = 0; x <= FogSubdivisions; x++)
            {
                fogVertices.Add(default);
                fogColors.Add(Color.clear);
                fogUVs.Add(new Vector2(.5f, .5f));
                if (x == FogSubdivisions || y == FogSubdivisions) continue;
                var a = first + y * stride + x;
                fogIndices.Add(a); fogIndices.Add(a + stride); fogIndices.Add(a + stride + 1);
                fogIndices.Add(a); fogIndices.Add(a + stride + 1); fogIndices.Add(a + 1);
            }
            WriteFogBoundaryVertices(cell, first);
        }

        private void WriteFogBoundaryVertices(Vector2Int cell, int first)
        {
            var origin = floorTilemap.CellToLocalInterpolated(new Vector3(cell.x, cell.y, 0));
            var right = floorTilemap.CellToLocalInterpolated(new Vector3(cell.x + 1, cell.y, 0)) - origin;
            var up = floorTilemap.CellToLocalInterpolated(new Vector3(cell.x, cell.y + 1, 0)) - origin;
            var affine = floorTilemap.layoutGrid.cellLayout != GridLayout.CellLayout.Hexagon;
            for (var y = 0; y <= FogSubdivisions; y++)
            for (var x = 0; x <= FogSubdivisions; x++)
            {
                var point = new Vector2((float)x / FogSubdivisions, (float)y / FogSubdivisions);
                var vertex = affine ? origin + right * point.x + up * point.y
                    : floorTilemap.CellToLocalInterpolated(new Vector3(cell.x + point.x, cell.y + point.y, 0));
                vertex.z -= .05f; fogVertices[first + y * (FogSubdivisions + 1) + x] = vertex;
            }
        }

        private void ClearSoftFog()
        {
            fogMeshCells.Clear();
            fogMeshSlots.Clear(); freeFogMeshSlots.Clear();
            fogVertices.Clear(); fogColors.Clear(); fogUVs.Clear(); fogIndices.Clear();
            dirtyFogColorSlots.Clear(); orderedDirtyFogColorSlots.Clear();
            dirtyFogGeometrySlots.Clear();
            if (fogBlendRenderer != null) fogBlendRenderer.enabled = false;
            if (fogBlendMesh != null) fogBlendMesh.Clear();
        }
        private void ReleaseSoftFog()
        {
            ClearForestKnowledge();
            ClearSoftFog();
            forestFogGroundMaterial = null;
            if (Application.isPlaying) { Destroy(fogBlendMesh); Destroy(fogBlendMaterial); }
            else { DestroyImmediate(fogBlendMesh); DestroyImmediate(fogBlendMaterial); }
        }
    }
}
