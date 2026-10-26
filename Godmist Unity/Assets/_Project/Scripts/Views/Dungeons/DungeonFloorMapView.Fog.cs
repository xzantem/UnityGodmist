using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        public DungeonFloorInstance FogFloor { get; private set; }
        private readonly Dictionary<SpriteRenderer, Color> fogBaseColors = new();
        private readonly Dictionary<SpriteRenderer, (DungeonKnowledge Knowledge, bool Show)> presentedFogTints = new();
        private readonly Dictionary<DungeonSurfaceEdge, DungeonWallSegmentView> rememberedGates = new();
        private readonly HashSet<DungeonSurfaceEdge> presentationClosedGateEdges = new();
        public int FogElevationQueryCount { get; private set; }
        private HashSet<DungeonSurfaceEdge> ReadPresentationClosedGates()
        {
            presentationClosedGateEdges.Clear();
            presentationClosedGateEdges.UnionWith(boundMap.SurfaceClosedProgressionGateEdges);
            return presentationClosedGateEdges;
        }
        public void ApplyFog(DungeonFloorInstance floor)
        {
            var changedFloor = !ReferenceEquals(FogFloor, floor);
            FogFloor = floor;
            if (changedFloor && vegetationPresentation != null && boundMap == floor?.Map)
                RebuildPresentationDecorations();
            using (new FogPhaseTiming(this, 14))
                elevation.SetFogVisibility(floor?.Fog.DebugFogDisabled == true ? null : floor?.Fog.Visible);
            RefreshElevationPresentation();
        }
        public DungeonKnowledge Knowledge(DungeonTraversalPosition node) => FogFloor?.Fog.DebugFogDisabled == true ? DungeonKnowledge.Visible : FogFloor?.Fog.Get(node) ?? DungeonKnowledge.Visible;
        public bool IsCurrentlySeen(DungeonTraversalPosition node) => Knowledge(node) == DungeonKnowledge.Visible
            || IsStructuralPreviewPosition(node);
        public bool IsObjectCurrentlySeen(DungeonObjectInstance obj) => FogFloor == null || FogFloor.Fog.DebugFogDisabled || DungeonFogSystem.IsObjectVisible(FogFloor, obj);
        public DungeonObjectKnowledge KnownObject(DungeonObjectInstance obj) => FogFloor != null
            && FogFloor.Fog.Objects.TryGetValue(obj.InstanceId, out var known) ? known : null;
        public DungeonKnowledge EdgeKnowledge(DungeonSurfaceEdge edge)
        {
            var a = Knowledge(new DungeonTraversalPosition(edge.First));
            var b = Knowledge(new DungeonTraversalPosition(edge.Second));
            return (DungeonKnowledge)Mathf.Max((int)a, (int)b);
        }

        private bool IsFogEdgeVisible(DungeonSurfaceEdge edge)
        {
            FogElevationQueryCount++;
            // Static woodland remembers its silhouette after discovery. FOV
            // gates knowledge, while projection alone controls occlusion.
            if (UsesVegetationPresentation)
                return !IsSurfaceOccluded(edge.First) || !IsSurfaceOccluded(edge.Second);
            return elevation.IsVisible(edge);
        }

        private DungeonKnowledge PresentationKnowledge(DungeonTraversalPosition node)
            => IsStructuralPreviewPosition(node) ? DungeonKnowledge.Visible : Knowledge(node);

        private DungeonKnowledge PresentationEdgeKnowledge(DungeonSurfaceEdge edge)
            => IsStructuralPreviewEdgeVisible(edge) ? DungeonKnowledge.Visible : EdgeKnowledge(edge);

        private bool IsStructuralPreviewPosition(DungeonTraversalPosition position)
        {
            if (!elevation.IsStructuralPreview) return false;
            if (position.IsOnStairs) return true;
            return position.Surface.Level > 0;
        }

        private bool IsStructuralPreviewEdgeVisible(DungeonSurfaceEdge edge)
            => elevation.IsStructuralPreview && edge.Level > 0 && elevation.IsVisible(edge);
        public static Color Dim(Color color) => new(color.r * .55f, color.g * .55f, color.b * .55f, color.a);
        // Forest knowledge is composited once above the entire scenery. A
        // second per-sprite tint would darken props but leave litter brighter.
        public float RememberedFogBrightness => UsesVegetationPresentation ? 1f : .55f;
        private void TintFog(Renderer renderer, DungeonKnowledge knowledge)
        {
            if (renderer is not SpriteRenderer sprite) return;
            var state = (knowledge, !renderer.forceRenderingOff);
            // Static wall/underlay sprites retain their target; the animation
            // tick owns intermediate colors. Most of a long floor is unchanged
            // when the party crosses one cell, so don't rewrite those renderers.
            if (presentedFogTints.TryGetValue(sprite, out var previous) && previous == state) return;
            presentedFogTints[sprite] = state;
            if (!fogBaseColors.TryGetValue(sprite, out var original)) fogBaseColors.Add(sprite, original = sprite.color);
            PresentFogSprite(sprite, original, !renderer.forceRenderingOff,
                knowledge == DungeonKnowledge.Remembered ? RememberedFogBrightness : 1);
        }
        private void RefreshRememberedGates()
        {
            if(UsesVegetationPresentation){RefreshForestProgressionGates();return;}
            if (FogFloor == null || wallSegmentPrefab == null) return;
            var closed = ReadPresentationClosedGates();
            foreach (var edge in FogFloor.Fog.ClosedGates)
            {
                if (closed.Contains(edge) || rememberedGates.ContainsKey(edge)) continue;
                var view = Instantiate(wallSegmentPrefab, wallsRoot != null ? wallsRoot : transform);
                view.name = "Remembered progression gate";
                view.Bind(edge.CellEdge, floorTilemap, false);
                rememberedGates.Add(edge, view);
            }
            foreach (var pair in rememberedGates)
                foreach (var renderer in pair.Value.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.forceRenderingOff = FogFloor.Fog.DebugFogDisabled || closed.Contains(pair.Key) || !FogFloor.Fog.ClosedGates.Contains(pair.Key)
                        || EdgeKnowledge(pair.Key) == DungeonKnowledge.Unexplored || !elevation.IsVisible(pair.Key);
                    TintFog(renderer, DungeonKnowledge.Remembered);
                }
        }
    }
}
