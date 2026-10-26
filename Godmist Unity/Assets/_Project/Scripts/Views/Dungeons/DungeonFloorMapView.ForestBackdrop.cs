using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private SpriteRenderer forestBackdrop;

        // Soil patches overlap with different tints and feathered alpha. Equal
        // orders let sprite batching reorder that non-commutative blend when an
        // unrelated actor changes animation frame. Reserve distinct lanes below
        // the playable floor, with continuous litter behind every patch.
        private int ForestUnderlaySortingOrder(int slot)
            => Mathf.Max(short.MinValue, GetFloorFeedbackDepthSortingOrder() - 2 - slot);

        private void RefreshForestUnderlaySorting()
        {
            int slot = 0;
            foreach (var ground in vegetationGround)
                if (ground.Renderer != null && ground.Renderer != forestBackdrop)
                    ground.Renderer.sortingOrder = ForestUnderlaySortingOrder(slot++);
            if (forestBackdrop != null) forestBackdrop.sortingOrder = ForestUnderlaySortingOrder(slot);
        }

        private void BuildForestBackdrop()
        {
            if (vegetationPresentation?.GroundTile is not Tile tile || tile.sprite == null) return;
            if (forestBackdrop == null)
            {
                var root = new GameObject("Uncharted woodland litter");
                root.layer = floorTilemap.gameObject.layer;
                root.transform.SetParent(wallsRoot != null ? wallsRoot : transform, false);
                forestBackdrop = root.AddComponent<SpriteRenderer>();
                spawnedWalls.Add(root);
                forestBackdrop.sprite = tile.sprite;
                vegetationGroundRenderers.Add(forestBackdrop);
                vegetationGround.Add((forestBackdrop, Array.Empty<Godmist.Runtime.Dungeons.DungeonSurfaceEdge>()));
                RegisterGroundLightingPresentation(forestBackdrop);
            }
            // This contains no map routes, contact imprints or objects. A single
            // continuous litter layer prevents missing unknown tiles from exposing
            // the camera's rectangular black background between visible trees.
            var size = new Vector2(forestSurfaceBounds.z, forestSurfaceBounds.w) + Vector2.one * 8;
            var lower = new Vector2(forestSurfaceBounds.x, forestSurfaceBounds.y) - Vector2.one * 4;
            var center = lower + size * .5f;
            forestBackdrop.transform.localScale = new Vector3(
                size.x / tile.sprite.bounds.size.x, size.y / tile.sprite.bounds.size.y, 1);
            forestBackdrop.transform.position = center;
            forestBackdrop.transform.position += (Vector3)center - forestBackdrop.bounds.center;
            var tint = vegetationPresentation.GroundTint;
            forestBackdrop.color = tint;
            forestBackdrop.sortingLayerID = FloorFeedbackSortingLayerID;
            forestBackdrop.sortingOrder = ForestUnderlaySortingOrder(vegetationGround.Count - 1);
            var block = new MaterialPropertyBlock();
            block.SetFloat("_ForestFogLitter", 1);
            block.SetFloat("_ForestBackdrop", 1);
            block.SetVector("_ForestBackdropBounds", new Vector4(lower.x, lower.y, size.x, size.y));
            forestBackdrop.SetPropertyBlock(block);
            InvalidateForestLightingCoverage();
        }
    }
}
