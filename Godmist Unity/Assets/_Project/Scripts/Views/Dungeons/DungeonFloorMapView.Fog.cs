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
        private readonly Dictionary<DungeonSurfaceEdge, DungeonWallSegmentView> rememberedGates = new();
        public void ApplyFog(DungeonFloorInstance floor)
        {
            FogFloor = floor;
            elevation.SetFogVisibility(floor?.Fog.DebugFogDisabled == true ? null : floor?.Fog.Visible);
            RefreshElevationPresentation();
        }
        public DungeonKnowledge Knowledge(DungeonTraversalPosition node) => FogFloor?.Fog.DebugFogDisabled == true ? DungeonKnowledge.Visible : FogFloor?.Fog.Get(node) ?? DungeonKnowledge.Visible;
        public bool IsCurrentlySeen(DungeonTraversalPosition node) => Knowledge(node) == DungeonKnowledge.Visible;
        public bool IsObjectCurrentlySeen(DungeonObjectInstance obj) => FogFloor == null || FogFloor.Fog.DebugFogDisabled || DungeonFogSystem.IsObjectVisible(FogFloor, obj);
        public DungeonObjectKnowledge KnownObject(DungeonObjectInstance obj) => FogFloor != null
            && FogFloor.Fog.Objects.TryGetValue(obj.InstanceId, out var known) ? known : null;
        public DungeonKnowledge EdgeKnowledge(DungeonSurfaceEdge edge)
        {
            var a = Knowledge(new DungeonTraversalPosition(edge.First));
            var b = Knowledge(new DungeonTraversalPosition(edge.Second));
            return (DungeonKnowledge)Mathf.Max((int)a, (int)b);
        }
        public static Color Dim(Color color) => new(color.r * .55f, color.g * .55f, color.b * .55f, color.a);
        private void TintFog(Renderer renderer, DungeonKnowledge knowledge)
        {
            if (renderer is not SpriteRenderer sprite) return;
            if (!fogBaseColors.TryGetValue(sprite, out var original)) fogBaseColors.Add(sprite, original = sprite.color);
            sprite.color = knowledge == DungeonKnowledge.Remembered ? Dim(original) : original;
        }
        private void RefreshRememberedGates()
        {
            if (FogFloor == null || wallSegmentPrefab == null) return;
            var closed = new HashSet<DungeonSurfaceEdge>(boundMap.SurfaceClosedProgressionGateEdges);
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
