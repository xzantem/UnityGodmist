using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private readonly List<SpriteRenderer> cutawayLines = new();
        private Sprite stairStripeSprite;
        private Sprite balconyMasonrySource, balconyMasonryFace, balconyMasonryCap;
        private readonly List<SpriteRenderer> balconyFaceRenderers = new();
        private readonly List<(DungeonTraversalPosition Position, SpriteRenderer Renderer)> stairStripes = new();
        public int StairPresentationElementCount => stairStripes.Count;

        private void BuildStairPresentation()
        {
            if (boundMap == null) return;
            var tileRenderer = floorTilemap.GetComponent<TilemapRenderer>();
            var useCatacombsArt = catacombsArtPresentationEnabled;
            // The route is assembled from a terminal start, open middle cells,
            // and a terminal end.  A full closed sprite on every cell made a
            // continuous flight read as five detached platforms.
            var startSprite = useCatacombsArt ? LoadPresentationSprite("catacombs-stairs-start-v4") : null;
            var middleSprite = useCatacombsArt ? LoadPresentationSprite("catacombs-stairs-middle-v4") : null;
            var endSprite = useCatacombsArt ? LoadPresentationSprite("catacombs-stairs-end-v4") : null;
            foreach (var stairs in boundMap.Stairs)
            {
                if (!boundMap.TryGetStairPath(stairs, out var path)) continue;
                for (var i = 0; i < path.Count; i++)
                {
                    var position = i == 0 ? new DungeonTraversalPosition(stairs.First)
                        : i == path.Count - 1 ? new DungeonTraversalPosition(stairs.Second) : new DungeonTraversalPosition(stairs, i);
                    var center = CellToWorldCenter(path[i]);
                    var next = CellToWorldCenter(i == path.Count - 1 ? path[i - 1] : path[i + 1]);
                    var direction = (i == path.Count - 1 ? center - next : next - center).normalized;
                    var cellSize = Vector2.Distance(center, next);
                    var perpendicular = new Vector2(-direction.y, direction.x);
                    if (!useCatacombsArt)
                    {
                        // Keep the original generic dungeon stair marker for
                        // non-Catacombs expeditions.  The v4 art is owned by
                        // the explicit Catacombs presentation gate below.
                        for (var bar = 0; bar < 4; bar++)
                        {
                            var location = center + direction * ((bar - 1.5f) * .17f * cellSize);
                            AddStairStripe(position, location, perpendicular,
                                (.35f + bar * .1f) * cellSize,
                                .055f * cellSize,
                                tileRenderer);
                        }
                        continue;
                    }
                    var sprite = path.Count == 1 ? startSprite
                        : i == 0 ? startSprite
                        : i == path.Count - 1 ? endSprite
                        : middleSprite;
                    // One full-size sprite per authored route cell gives the
                    // stair a continuous stone bed with pixel-aligned joints;
                    // only the two route terminals contain a transverse cap.
                    AddStairStripe(
                        position,
                        center,
                        perpendicular,
                        cellSize,
                        cellSize,
                        tileRenderer,
                        sprite);
                }
            }
        }

        private void BuildLedgePresentation()
        {
            var tiles = floorTilemap.GetComponent<TilemapRenderer>();
            var useCatacombsArt = catacombsArtPresentationEnabled;
            PrepareBalconyMasonry(useCatacombsArt ? LoadPresentationSprite("catacombs-balcony-face-v4") : null);
            var size = Vector2.Distance(CellToWorldCenter(Vector2Int.zero), CellToWorldCenter(Vector2Int.right));
            foreach (var surface in boundMap.Surfaces)
            {
                if (surface.Level <= 0) continue;
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    if (boundMap.IsWalkable(new DungeonSurfacePosition(surface.Cell + direction, surface.Level))) continue;
                    if (!useCatacombsArt)
                    {
                        var legacyCenter = CellToWorldCenter(surface.Cell) + (Vector2)direction * (size * .46f);
                        var legacyTangent = new Vector2(-direction.y, direction.x);
                        AddStairStripe(new DungeonTraversalPosition(surface), legacyCenter, legacyTangent,
                            size, size * .085f, tiles);
                        var legacyFace = stairStripes[stairStripes.Count - 1].Renderer;
                        legacyFace.name = "Balcony ledge";
                        legacyFace.color = new Color(.23f, .19f, .15f);
                        AddStairStripe(new DungeonTraversalPosition(surface),
                            legacyCenter - (Vector2)direction * (size * .045f), legacyTangent,
                            size, size * .025f, tiles);
                        var legacyRim = stairStripes[stairStripes.Count - 1].Renderer;
                        legacyRim.name = "Balcony rim";
                        legacyRim.color = new Color(.63f, .55f, .40f);
                        continue;
                    }
                    // Only the edge where the authored stair path arrives is open.
                    // Other front edges remain visible masonry so a balcony does
                    // not become a floating outline or lose its readable height.
                    if (IsStairArrivalEdge(surface, direction)) continue;
                    var position = new DungeonTraversalPosition(surface);
                    var center = CellToWorldCenter(surface.Cell);
                    if (direction == Vector2Int.down)
                    {
                        // A standing face is projected downward from the front
                        // boundary. Never rotate/compress that face into side caps.
                        var height = size * .60f;
                        AddStairStripe(position, center + Vector2.down * (size * .5f + height * .5f),
                            Vector2.right,size,height,tiles,balconyMasonryFace,tiled:true,standingFace:true);
                        stairStripes[stairStripes.Count - 1].Renderer.name = "Catacombs balcony stone face";
                    }
                    var capDepth = size * .125f;
                    var tangent = direction.x == 0 ? Vector2.right : Vector2.up;
                    AddStairStripe(position, center + (Vector2)direction * (size * .5f - capDepth * .5f),
                        tangent,size,capDepth,tiles,balconyMasonryCap);
                    stairStripes[stairStripes.Count - 1].Renderer.name = "Catacombs balcony stone cap";
                }
            }
        }

        private void AddStairStripe(DungeonTraversalPosition position, Vector2 center, Vector2 direction,
            float length, float width, TilemapRenderer tiles, Sprite overrideSprite = null, bool tiled = false, bool standingFace = false)
        {
            if (overrideSprite == null && stairStripeSprite == null)
                stairStripeSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            // Geometry is generated from the saved route, like the existing runtime combat path markers.
            var element = new GameObject("Dungeon stair tread", typeof(SpriteRenderer));
            element.transform.SetParent(transform, false);
            element.transform.position = new Vector3(center.x, center.y, floorTilemap.transform.position.z - .01f);
            element.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var renderer = element.GetComponent<SpriteRenderer>();
            renderer.sprite = overrideSprite ?? stairStripeSprite;
            var spriteSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.one;
            element.transform.localScale = new Vector3(
                (tiled ? 1 : length / Mathf.Max(.0001f,spriteSize.x)) / Mathf.Max(.0001f,transform.lossyScale.x),
                (tiled ? 1 : width / Mathf.Max(.0001f,spriteSize.y)) / Mathf.Max(.0001f,transform.lossyScale.y),
                1);
            if (tiled)
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(length,width);
            }
            renderer.color = overrideSprite != null
                ? Color.white
                : new Color(.48f, .46f, .41f, 1f);
            if (tiles != null) { renderer.sortingLayerID = tiles.sortingLayerID; renderer.sortingOrder = tiles.sortingOrder + 1; }
            if (tiles != null) element.layer = tiles.gameObject.layer;
            if (catacombsArtPresentationEnabled)
            {
                if (standingFace) balconyFaceRenderers.Add(renderer);
                else RegisterGroundLightingPresentation(renderer, false, preserveDepthSorting:true);
            }
            stairStripes.Add((position, renderer));
        }

        private void RefreshCutawayPresentation()
        {
            var used = 0;
            var tileRenderer = floorTilemap.GetComponent<TilemapRenderer>();
            var size = Vector2.Distance(CellToWorldCenter(Vector2Int.zero), CellToWorldCenter(Vector2Int.right));
            var cutawaySprite = catacombsArtPresentationEnabled ? balconyMasonryCap : null;
            foreach (var surface in elevation.CutawaySurfaces)
            foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                if (PresentationKnowledge(new DungeonTraversalPosition(surface)) == DungeonKnowledge.Unexplored) continue;
                // A cutaway edge is only a perimeter edge.  Testing the explicit
                // same-level surface avoids the old level-0 overload and removes
                // the four repeated internal rectangles from adjacent balcony cells.
                if (boundMap.IsWalkable(new DungeonSurfacePosition(
                        surface.Cell + Vector2Int.RoundToInt(direction), surface.Level))) continue;
                var center = CellToWorldCenter(surface.Cell) + direction * (size * .46f);
                var tangent = new Vector2(-direction.y, direction.x);
                if (used == cutawayLines.Count)
                {
                    AddStairStripe(new DungeonTraversalPosition(surface), center, tangent, size * .9f, size * .018f, tileRenderer, cutawaySprite);
                    var renderer = stairStripes[stairStripes.Count - 1].Renderer;
                    stairStripes.RemoveAt(stairStripes.Count - 1);
                    renderer.name = "Balcony cutaway stone cap";
                    renderer.color = cutawaySprite != null
                        ? Color.white
                        : new Color(.72f, .68f, .54f, .35f);
                    cutawayLines.Add(renderer);
                }
                var line = cutawayLines[used++];
                line.transform.position = new Vector3(center.x, center.y, floorTilemap.transform.position.z - .02f);
                line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                line.forceRenderingOff = false;
            }
            for (var i = used; i < cutawayLines.Count; i++) cutawayLines[i].forceRenderingOff = true;
        }

        private bool IsStairArrivalEdge(DungeonSurfacePosition surface, Vector2Int direction)
        {
            foreach (var stairs in boundMap.Stairs)
            {
                if (stairs.Second.Level != surface.Level
                    || !boundMap.TryGetStairPath(stairs, out var path)
                    || path.Count < 2)
                    continue;

                var arrival = path[path.Count - 1];
                var previous = path[path.Count - 2];
                if (arrival == surface.Cell && arrival + direction == previous)
                    return true;
            }

            return false;
        }

        private void RefreshStairPresentation()
        {
            foreach (var stripe in stairStripes)
                if (stripe.Renderer != null)
                {
                    var knowledge = PresentationKnowledge(stripe.Position);
                    stripe.Renderer.forceRenderingOff = !elevation.IsVisible(stripe.Position) || knowledge == DungeonKnowledge.Unexplored;
                    TintFog(stripe.Renderer, knowledge);
                }
        }

        private void ClearStairPresentation()
        {
            foreach (var stripe in stairStripes)
                if (stripe.Renderer != null)
                {
                    UnregisterLightingPresentation(stripe.Renderer);
                    if (Application.isPlaying) Destroy(stripe.Renderer.gameObject);
                    else DestroyImmediate(stripe.Renderer.gameObject);
                }
            stairStripes.Clear();
            balconyFaceRenderers.Clear();
            foreach (var line in cutawayLines) if (line != null)
            { UnregisterLightingPresentation(line); if (Application.isPlaying) Destroy(line.gameObject); else DestroyImmediate(line.gameObject); }
            cutawayLines.Clear();
        }

        private void ReleaseStairSprite()
        {
            PrepareBalconyMasonry(null);
            if (stairStripeSprite == null) return;
            if (Application.isPlaying) Destroy(stairStripeSprite); else DestroyImmediate(stairStripeSprite);
            stairStripeSprite = null;
        }

        private void PrepareBalconyMasonry(Sprite source)
        {
            if (source == balconyMasonrySource) return;
            foreach (var sprite in new[] { balconyMasonryFace, balconyMasonryCap })
                if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            balconyMasonrySource = source;
            balconyMasonryFace = source == null ? null : DungeonWallSegmentView.CreateMasonryFaceSprite(source);
            balconyMasonryCap = source == null ? null : DungeonWallSegmentView.CreateMasonryCapSprite(source);
        }
    }
}
