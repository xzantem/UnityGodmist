using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly List<(SpriteRenderer Tree, DungeonSurfaceEdge Edge)> entFireTrees = new();
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> entFireOverlays = new();
        private readonly HashSet<string> entBurningRooms = new();
        private readonly HashSet<string> nextEntBurningRooms = new();
        private readonly HashSet<SpriteRenderer> entFireRendererSet = new();
        private readonly Dictionary<SpriteRenderer, DungeonSurfaceEdge> entFireVisibilityEdges = new();
        private Material entFireMaterial;
        private float nextEntFireRefresh;
        public int BurningArenaTreeCount => entFireOverlays.Values.Count(r => r != null && r.enabled);
        // Match the yellow-green embers in the approved phase-II Ent art.
        public static readonly Color ArenaFireCore = new(.85f, .78f, .14f, 1);
        public static readonly Color ArenaFireLow = new(.13f, .19f, .025f, 1);
        public static readonly Color ArenaFireLightColor = new(.95f, .88f, .20f, 1);
        public IEnumerable<(Vector2 position, DungeonSurfacePosition cell, SpriteRenderer flame)> ArenaFireLightSources()
        {
            // Ordinary exploration has no fire overlays. Avoid visiting every
            // boundary tree in the generated forest just to miss this lookup.
            if (entFireOverlays.Count == 0) yield break;
            foreach (var entry in entFireTrees)
                if (entFireOverlays.TryGetValue(entry.Tree, out var flame) && flame != null && flame.enabled
                    && !flame.forceRenderingOff && entry.Tree.enabled && !entry.Tree.forceRenderingOff && entry.Tree.gameObject.activeInHierarchy)
                {
                    var cell = boundMap.IsWalkable(entry.Edge.FirstCell) ? entry.Edge.FirstCell : entry.Edge.SecondCell;
                    yield return ((Vector2)entry.Tree.transform.position, new DungeonSurfacePosition(cell, entry.Edge.Level), flame);
                }
        }
        public bool IsArenaFireRenderer(Renderer renderer) => renderer is SpriteRenderer sprite
            && entFireRendererSet.Contains(sprite);

        // Only the phase/room set is sampled. Tree renderers are touched once per transition.
        public void RefreshEntArenaFire(IEnumerable<CombatSession> combats)
        {
            if (Time.unscaledTime < nextEntFireRefresh) return;
            nextEntFireRefresh = Time.unscaledTime + .2f;
            nextEntBurningRooms.Clear();
            if (vegetationPresentation != null && boundMap != null && combats != null)
                foreach (var combat in combats)
                {
                    if (combat?.DungeonContext == null || !ReferenceEquals(combat.DungeonContext.SurfaceMap, boundMap) || combat.Outcome != Godmist.Core.Enums.CombatOutcomeState.Ongoing) continue;
                    foreach (var unit in combat.Units)
                        if (unit?.ForestEntState?.PhaseTwo == true
                            && unit.ForestEntState.OriginalDefinition?.Id == ForestEntBossSystem.BossId
                            && unit.ForestEntState.ArenaRoomId != null)
                            nextEntBurningRooms.Add(unit.ForestEntState.ArenaRoomId);
                }
            SetEntBurningRooms(nextEntBurningRooms);
        }

        private void SetEntBurningRooms(HashSet<string> rooms)
        {
            if (entBurningRooms.SetEquals(rooms)) return;
            entBurningRooms.Clear(); entBurningRooms.UnionWith(rooms);
            foreach (var entry in entFireTrees)
            {
                var burning = IsEntBurningEdge(entry.Edge);
                if (!entFireOverlays.TryGetValue(entry.Tree, out var overlay) && burning)
                    overlay = CreateEntFireOverlay(entry.Tree, entry.Edge);
                if (overlay != null) overlay.enabled = burning;
            }
            RefreshEntFireVisibility();
        }

        private void RefreshEntFireVisibility()
        {
            // Decorative woodland can extend beyond discovered cells. Active
            // fire is dynamic combat information and must keep its own owner.
            foreach (var pair in entFireVisibilityEdges)
            {
                if (pair.Key == null) continue;
                var knowledge = PresentationEdgeKnowledge(pair.Value);
                pair.Key.forceRenderingOff = knowledge == DungeonKnowledge.Unexplored;
                TintFog(pair.Key, knowledge);
            }
        }

        private bool IsEntBurningEdge(DungeonSurfaceEdge edge) =>
            boundMap.TryGetRoomId(new DungeonSurfacePosition(edge.FirstCell, edge.Level), out var first)
                && entBurningRooms.Contains(first)
            || boundMap.TryGetRoomId(new DungeonSurfacePosition(edge.SecondCell, edge.Level), out var second)
                && entBurningRooms.Contains(second);

        private SpriteRenderer CreateEntFireOverlay(SpriteRenderer tree, DungeonSurfaceEdge edge)
        {
            if (tree == null || tree.sprite == null) return null;
            if (entFireMaterial == null)
            {
                var shader = Resources.Load<Shader>("Godmist/EntArenaFire");
                if (shader == null) { Debug.LogError("Missing Ent Arena Fire shader", this); return null; }
                entFireMaterial = new Material(shader);
                entFireMaterial.SetColor("_FireLow", ArenaFireLow);
                entFireMaterial.SetColor("_FireCore", ArenaFireCore);
            }
            var go = new GameObject("Ent green arena fire");
            go.transform.SetParent(tree.transform, false);
            var overlay = go.AddComponent<SpriteRenderer>();
            overlay.sprite = tree.sprite; overlay.flipX = tree.flipX;
            overlay.sharedMaterial = entFireMaterial;
            overlay.sortingLayerID = tree.sortingLayerID; overlay.sortingOrder = tree.sortingOrder + 1;
            overlay.color = new Color(1, 1, 1, .9f);
            overlay.forceRenderingOff = tree.forceRenderingOff;
            var bounds = tree.sprite.bounds;
            var block = new MaterialPropertyBlock();
            block.SetVector("_SpriteBounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            block.SetFloat("_Phase", (VegetationHash(edge.FirstCell + edge.SecondCell * 31) & 255u) / 255f * 6.28f);
            overlay.SetPropertyBlock(block);
            entFireOverlays.Add(tree, overlay); entFireRendererSet.Add(overlay);
            entFireVisibilityEdges.Add(overlay, edge);
            InvalidateWorldPresentation();
            // Share precisely the same fog/elevation owner as the original tree.
            for (var i = 0; i < wallRenderers.Count; i++)
            {
                var wall = wallRenderers[i];
                if (!wall.Edge.Equals(edge)) continue;
                var renderers = wall.Renderers.Concat(new Renderer[] { overlay }).ToArray();
                wallRenderers[i] = (wall.Edge, wall.View, renderers);
                if (wall.View != null) vegetationRenderers[wall.View] = renderers;
                break;
            }
            return overlay;
        }

        private void ClearEntArenaFire()
        {
            entFireTrees.Clear(); entFireOverlays.Clear(); entFireRendererSet.Clear(); entBurningRooms.Clear(); nextEntBurningRooms.Clear();
            entFireVisibilityEdges.Clear();
            nextEntFireRefresh = 0;
            if (entFireMaterial != null)
            {
                if (Application.isPlaying) Destroy(entFireMaterial); else DestroyImmediate(entFireMaterial);
                entFireMaterial = null;
            }
        }
    }
}
