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
        private readonly List<(DungeonTraversalPosition Position, SpriteRenderer Renderer)> stairStripes = new();
        public int StairPresentationElementCount => stairStripes.Count;

        private void BuildStairPresentation()
        {
            if (boundMap == null) return;
            var tileRenderer = floorTilemap.GetComponent<TilemapRenderer>();
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
                    for (var bar = 0; bar < 4; bar++)
                    {
                        var location = center + direction * ((bar - 1.5f) * .17f * cellSize);
                        AddStairStripe(position, location, perpendicular, (.35f + bar * .1f) * cellSize, .055f * cellSize, tileRenderer);
                    }
                }
            }
        }

        private void BuildLedgePresentation()
        {
            var tiles = floorTilemap.GetComponent<TilemapRenderer>();
            var size = Vector2.Distance(CellToWorldCenter(Vector2Int.zero), CellToWorldCenter(Vector2Int.right));
            foreach (var surface in boundMap.Surfaces)
            {
                if (surface.Level <= 0) continue;
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    if (boundMap.IsWalkable(new DungeonSurfacePosition(surface.Cell + direction, surface.Level))) continue;
                    var center = CellToWorldCenter(surface.Cell) + (Vector2)direction * (size * .46f);
                    var tangent = new Vector2(-direction.y, direction.x);
                    AddStairStripe(new DungeonTraversalPosition(surface), center, tangent, size, size * .085f, tiles);
                    var face = stairStripes[stairStripes.Count - 1].Renderer;
                    face.name = "Balcony ledge"; face.color = new Color(.23f, .19f, .15f);
                    AddStairStripe(new DungeonTraversalPosition(surface), center - (Vector2)direction * (size * .045f), tangent, size, size * .025f, tiles);
                    var rim = stairStripes[stairStripes.Count - 1].Renderer;
                    rim.name = "Balcony rim"; rim.color = new Color(.63f, .55f, .40f);
                }
            }
        }

        private void AddStairStripe(DungeonTraversalPosition position, Vector2 center, Vector2 direction,
            float length, float width, TilemapRenderer tiles)
        {
            if (stairStripeSprite == null)
                stairStripeSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            // Geometry is generated from the saved route, like the existing runtime combat path markers.
            var element = new GameObject("Dungeon stair tread", typeof(SpriteRenderer));
            element.transform.SetParent(transform, false);
            element.transform.position = new Vector3(center.x, center.y, floorTilemap.transform.position.z - .01f);
            element.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            element.transform.localScale = new Vector3(length / Mathf.Max(.0001f, transform.lossyScale.x),
                width / Mathf.Max(.0001f, transform.lossyScale.y), 1);
            var renderer = element.GetComponent<SpriteRenderer>();
            renderer.sprite = stairStripeSprite;
            renderer.color = new Color(.88f, .8f, .55f);
            if (tiles != null) { renderer.sortingLayerID = tiles.sortingLayerID; renderer.sortingOrder = tiles.sortingOrder + 1; }
            stairStripes.Add((position, renderer));
        }

        private void RefreshCutawayPresentation()
        {
            var used = 0;
            var tileRenderer = floorTilemap.GetComponent<TilemapRenderer>();
            var size = Vector2.Distance(CellToWorldCenter(Vector2Int.zero), CellToWorldCenter(Vector2Int.right));
            foreach (var surface in elevation.CutawaySurfaces)
            foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                if (Knowledge(new DungeonTraversalPosition(surface)) == DungeonKnowledge.Unexplored) continue;
                var center = CellToWorldCenter(surface.Cell) + direction * (size * .46f);
                var tangent = new Vector2(-direction.y, direction.x);
                if (used == cutawayLines.Count)
                {
                    AddStairStripe(new DungeonTraversalPosition(surface), center, tangent, size * .9f, size * .018f, tileRenderer);
                    var renderer = stairStripes[stairStripes.Count - 1].Renderer;
                    stairStripes.RemoveAt(stairStripes.Count - 1);
                    renderer.name = "Balcony cutaway outline";
                    renderer.color = new Color(.72f, .68f, .54f, .35f);
                    cutawayLines.Add(renderer);
                }
                var line = cutawayLines[used++];
                line.transform.position = new Vector3(center.x, center.y, floorTilemap.transform.position.z - .02f);
                line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                line.forceRenderingOff = false;
            }
            for (var i = used; i < cutawayLines.Count; i++) cutawayLines[i].forceRenderingOff = true;
        }

        private void RefreshStairPresentation()
        {
            foreach (var stripe in stairStripes)
                if (stripe.Renderer != null)
                {
                    var knowledge = Knowledge(stripe.Position);
                    stripe.Renderer.forceRenderingOff = !elevation.IsVisible(stripe.Position) || knowledge == DungeonKnowledge.Unexplored;
                    TintFog(stripe.Renderer, knowledge);
                }
        }

        private void ClearStairPresentation()
        {
            foreach (var stripe in stairStripes)
                if (stripe.Renderer != null)
                {
                    if (Application.isPlaying) Destroy(stripe.Renderer.gameObject);
                    else DestroyImmediate(stripe.Renderer.gameObject);
                }
            stairStripes.Clear();
            foreach (var line in cutawayLines) if (line != null)
            { if (Application.isPlaying) Destroy(line.gameObject); else DestroyImmediate(line.gameObject); }
            cutawayLines.Clear();
        }

        private void ReleaseStairSprite()
        {
            if (stairStripeSprite == null) return;
            if (Application.isPlaying) Destroy(stairStripeSprite); else DestroyImmediate(stairStripeSprite);
            stairStripeSprite = null;
        }
    }
}
