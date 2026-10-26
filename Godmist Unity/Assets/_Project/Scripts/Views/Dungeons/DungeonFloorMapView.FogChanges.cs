using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Flat forest boundaries depend only on endpoint knowledge. Layered
        // cutaways, geometry changes and debug overrides retain the full path.
        private readonly Dictionary<Vector2Int, List<int>> fogWallsByCell = new();
        private readonly Dictionary<Vector2Int, List<DungeonSurfaceEdge>> fogGroundEdgesByCell = new();
        private readonly Dictionary<Vector2Int, List<int>> fogDecorationsByCell = new();
        private readonly HashSet<int> changedFogDecorations = new();
        private bool fogDecorationIndexReady;
        private readonly HashSet<int> changedFogWalls = new();
        private readonly List<int> orderedFogWalls = new();
        private readonly HashSet<DungeonSurfaceEdge> changedFogGroundEdges = new();
        private readonly HashSet<DungeonSurfaceEdge> fogPresentedClosedGates = new();
        private bool incrementalForestFog;
        private int fogBoundaryRevision = -1;
        private Dictionary<Vector2Int, DungeonKnowledge> boundaryKnownFog = new();
        private Dictionary<Vector2Int, DungeonKnowledge> nextBoundaryKnownFog = new();
        private HashSet<Vector2Int> boundaryVisibleCells = new(), nextBoundaryVisibleCells = new();
        private DungeonFloorInstance boundaryFogFloor;
        private int boundaryDiscoveredCount = -1, boundaryDebugRevision = -1;
        private static readonly Unity.Profiling.ProfilerMarker BoundaryKnowledgeSample = new("Godmist.Dungeon.Fog.BoundaryKnowledge");
        public int FogBoundaryDiscoveryScanCount { get; private set; }
        public int FogGeometryIndexBuildCount { get; private set; }
        private DungeonFloorMap fogGeometryIndexMap;
        private int fogGeometryIndexRevision = -1, fogGeometryWallCount = -1, fogGeometryGroundCount = -1;

        private void InvalidateFogGeometryIndices()
        {
            fogGeometryIndexMap = null; fogGeometryIndexRevision = -1;
            fogWallsByCell.Clear(); fogGroundEdgesByCell.Clear();
        }

        private void EnsureFogGeometryIndices()
        {
            if (ReferenceEquals(fogGeometryIndexMap, boundMap)
                && fogGeometryIndexRevision == boundMap.TraversalRevision
                && fogGeometryWallCount == wallRenderers.Count && fogGeometryGroundCount == vegetationGroundByEdge.Count) return;
            using var timing = new FogPhaseTiming(this, 9);
            fogWallsByCell.Clear(); fogGroundEdgesByCell.Clear();
            for (var i = 0; i < wallRenderers.Count; i++)
            {
                var edge = wallRenderers[i].Edge;
                AddWall(edge.First.Cell, i); AddWall(edge.Second.Cell, i);
                foreach (var renderer in wallRenderers[i].Renderers)
                    if (renderer != null && forestBoundaryOwners.TryGetValue(renderer, out var owners))
                        foreach (var owner in owners)
                        { AddWall(owner.First.Cell, i); AddWall(owner.Second.Cell, i); }
            }
            foreach (var edge in vegetationGroundByEdge.Keys)
            { AddGround(edge.First.Cell, edge); AddGround(edge.Second.Cell, edge); }
            fogGeometryIndexMap = boundMap; fogGeometryIndexRevision = boundMap.TraversalRevision;
            fogGeometryWallCount = wallRenderers.Count; fogGeometryGroundCount = vegetationGroundByEdge.Count;
            FogGeometryIndexBuildCount++;
        }

        private void PrepareForestFogChanges()
        {
            changedFogWalls.Clear(); changedFogGroundEdges.Clear(); orderedFogWalls.Clear(); changedFogDecorations.Clear();
            if (!fogDecorationIndexReady && UsesVegetationPresentation && !boundMap.HasElevatedSurfaces)
                RebuildFogDecorationIndex();
            if (!incrementalForestFog)
            {
                if (!UsesVegetationPresentation || boundMap.HasElevatedSurfaces) { InvalidateFogGeometryIndices(); return; }
                EnsureFogGeometryIndices();
                RememberFogClosedGates();
                if (presentationCoverageReady) RefreshBoundaryKnownFog(false);
                return;
            }
            // A crown can overhang into the camera from an owner outside the
            // clipped floor projection. Track global known endpoint changes;
            // floor mesh clipping must never delay those tree/underlay tints.
            if (presentationCoverageReady) RefreshBoundaryKnownFog(true);
            else
            {
                foreach (var pair in fogProjection)
                    if (!previousFogProjection.TryGetValue(pair.Key, out var previous) || previous != pair.Value) Change(pair.Key);
                foreach (var pair in previousFogProjection)
                    if (!fogProjection.ContainsKey(pair.Key)) Change(pair.Key);
            }
            // A remembered gate can change independently of endpoint knowledge.
            if (FogFloor != null)
            {
                foreach (var edge in FogFloor.Fog.ClosedGates)
                    if (!fogPresentedClosedGates.Contains(edge)) { Change(edge.First.Cell); Change(edge.Second.Cell); }
                foreach (var edge in fogPresentedClosedGates)
                    if (!FogFloor.Fog.ClosedGates.Contains(edge)) { Change(edge.First.Cell); Change(edge.Second.Cell); }
            }
            RememberFogClosedGates();
            orderedFogWalls.AddRange(changedFogWalls); orderedFogWalls.Sort();
        }
        private void RefreshBoundaryKnownFog(bool markChanges)
        {
            using var sample = BoundaryKnowledgeSample.Auto();
            var count = FogFloor?.Fog.Discovered.Count ?? 0;
            var debugRevision = FogFloor?.Fog.DebugRevision ?? 0;
            // Discovery grows monotonically between debug resets. Preserve the
            // remembered entries and visit old discovery only when it grows;
            // visibility can then update from the small old/new visible sets.
            if (markChanges && ReferenceEquals(boundaryFogFloor, FogFloor)
                && boundaryDebugRevision == debugRevision && count >= boundaryDiscoveredCount)
            {
                nextBoundaryVisibleCells.Clear();
                if (FogFloor != null)
                {
                    if (count != boundaryDiscoveredCount)
                        foreach (var position in FogFloor.Fog.Discovered)
                        {
                            FogBoundaryDiscoveryScanCount++;
                            if (position.IsOnStairs || boundaryKnownFog.ContainsKey(position.Surface.Cell)) continue;
                            boundaryKnownFog.Add(position.Surface.Cell, DungeonKnowledge.Remembered);
                            Change(position.Surface.Cell);
                        }
                    foreach (var position in FogFloor.Fog.Visible)
                        if (!position.IsOnStairs) nextBoundaryVisibleCells.Add(position.Surface.Cell);
                }
                foreach (var cell in boundaryVisibleCells)
                {
                    if (nextBoundaryVisibleCells.Contains(cell)) continue;
                    var state = FogFloor?.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(cell))) ?? DungeonKnowledge.Unexplored;
                    if (state == DungeonKnowledge.Unexplored) boundaryKnownFog.Remove(cell);
                    else boundaryKnownFog[cell] = state;
                    Change(cell);
                }
                foreach (var cell in nextBoundaryVisibleCells)
                    if (!boundaryKnownFog.TryGetValue(cell, out var old) || old != DungeonKnowledge.Visible)
                    { boundaryKnownFog[cell] = DungeonKnowledge.Visible; Change(cell); }
                var visibleSwap = boundaryVisibleCells; boundaryVisibleCells = nextBoundaryVisibleCells; nextBoundaryVisibleCells = visibleSwap;
                boundaryDiscoveredCount = count;
                return;
            }
            nextBoundaryKnownFog.Clear();
            boundaryVisibleCells.Clear();
            if (FogFloor != null)
            {
                foreach (var position in FogFloor.Fog.Discovered)
                {
                    FogBoundaryDiscoveryScanCount++;
                    if (!position.IsOnStairs) nextBoundaryKnownFog[position.Surface.Cell] = DungeonKnowledge.Remembered;
                }
                foreach (var position in FogFloor.Fog.Visible)
                    if (!position.IsOnStairs)
                    { nextBoundaryKnownFog[position.Surface.Cell] = DungeonKnowledge.Visible; boundaryVisibleCells.Add(position.Surface.Cell); }
            }
            if (markChanges)
            {
                foreach (var pair in nextBoundaryKnownFog)
                    if (!boundaryKnownFog.TryGetValue(pair.Key, out var old) || old != pair.Value) Change(pair.Key);
                foreach (var pair in boundaryKnownFog)
                    if (!nextBoundaryKnownFog.ContainsKey(pair.Key)) Change(pair.Key);
            }
            var swap = boundaryKnownFog; boundaryKnownFog = nextBoundaryKnownFog; nextBoundaryKnownFog = swap;
            boundaryFogFloor = FogFloor; boundaryDiscoveredCount = count; boundaryDebugRevision = debugRevision;
        }
        private void RememberFogClosedGates()
        {
            fogPresentedClosedGates.Clear();
            if (FogFloor != null) fogPresentedClosedGates.UnionWith(FogFloor.Fog.ClosedGates);
        }
        private void AddWall(Vector2Int cell, int index)
        {
            if (!fogWallsByCell.TryGetValue(cell, out var list)) fogWallsByCell.Add(cell, list = new List<int>());
            if (!list.Contains(index)) list.Add(index);
        }
        private void AddGround(Vector2Int cell, DungeonSurfaceEdge edge)
        {
            if (!fogGroundEdgesByCell.TryGetValue(cell, out var list)) fogGroundEdgesByCell.Add(cell, list = new List<DungeonSurfaceEdge>());
            list.Add(edge);
        }
        private void Change(Vector2Int cell)
        {
            if (fogWallsByCell.TryGetValue(cell, out var walls)) foreach (var i in walls) changedFogWalls.Add(i);
            if (fogGroundEdgesByCell.TryGetValue(cell, out var edges)) foreach (var edge in edges) changedFogGroundEdges.Add(edge);
            if (fogDecorationsByCell.TryGetValue(cell, out var decorations)) foreach (var i in decorations) changedFogDecorations.Add(i);
        }
        private void AddFogDecoration(Vector2Int cell, int index)
        {
            if (!fogDecorationsByCell.TryGetValue(cell, out var list)) fogDecorationsByCell.Add(cell, list = new List<int>());
            list.Add(index);
        }
        private void RebuildFogDecorationIndex()
        {
            fogDecorationsByCell.Clear();
            for (int i = 0; i < presentationDecorations.Count; i++)
            {
                var decoration = presentationDecorations[i];
                if (decoration == null) continue;
                if (decoration.IsWall)
                { AddFogDecoration(decoration.Edge.First.Cell, i); AddFogDecoration(decoration.Edge.Second.Cell, i); }
                else AddFogDecoration(decoration.Cell, i);
            }
            fogDecorationIndexReady = true;
        }
        private IEnumerable<int> FogDecorationIndices()
        {
            if (incrementalForestFog && fogDecorationIndexReady)
            { foreach (var index in changedFogDecorations) yield return index; }
            else for (int i = 0; i < presentationDecorations.Count; i++) yield return i;
        }
        private IEnumerable<int> FogWallIndices()
        {
            if (incrementalForestFog) { foreach (var i in orderedFogWalls) yield return i; }
            else for (var i = 0; i < wallRenderers.Count; i++) yield return i;
        }
        private IEnumerable<DungeonSurfaceEdge> FogVegetationEdges()
            => incrementalForestFog ? changedFogGroundEdges : vegetationGroundByEdge.Keys;
    }
}
