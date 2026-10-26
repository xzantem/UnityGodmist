using System;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonObstacleView : DungeonObjectView
    {
        [SerializeField] private Collider2D blockingCollider;

        private SpriteRenderer[] hitRenderers;
        private int previousHits;
        private float hitFlashUntil;
        private SpriteRenderer runtimePresentationRenderer;
        private int presentationTopPaddingPixels;
        public bool HasBlockingCollider => blockingCollider != null;
        public bool IsGroundPresentation { get; private set; }
        public bool TryGetPresentationBounds(out Bounds bounds)
        {
            bounds = default;
            if (runtimePresentationRenderer != null && runtimePresentationRenderer.enabled
                && runtimePresentationRenderer.sprite != null)
            {
                bounds = runtimePresentationRenderer.bounds;
                var topInset = presentationTopPaddingPixels / runtimePresentationRenderer.sprite.pixelsPerUnit
                    * Mathf.Abs(runtimePresentationRenderer.transform.lossyScale.y);
                bounds.SetMinMax(bounds.min, bounds.max - Vector3.up * Mathf.Min(topInset, bounds.size.y));
                return true;
            }
            var found = false;
            if (hitRenderers == null) return false;
            foreach (var renderer in hitRenderers)
            {
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }
        public Vector2Int OccupiedSizeInCells => DungeonObject is ObstacleDungeonObjectInstance obstacle
            ? obstacle.OccupiedSizeInCells : Vector2Int.one;

        public bool ConfigureRuntimeColliderGeometry(
            Vector2 worldSize,
            bool preserveBottomEdge = true)
        {
            if (blockingCollider is not BoxCollider2D box
                || worldSize.x <= 0f || worldSize.y <= 0f)
            {
                return false;
            }

            var currentBounds = blockingCollider.bounds;
            var targetBottom = preserveBottomEdge
                ? currentBounds.min.y
                : currentBounds.center.y - worldSize.y * .5f;
            var targetCenter = new Vector3(
                currentBounds.center.x,
                targetBottom + worldSize.y * .5f,
                currentBounds.center.z);
            var colliderTransform = blockingCollider.transform;
            var scale = colliderTransform.lossyScale;
            if (Mathf.Abs(scale.x) <= .0001f || Mathf.Abs(scale.y) <= .0001f)
                return false;

            box.size = new Vector2(
                worldSize.x / Mathf.Abs(scale.x),
                worldSize.y / Mathf.Abs(scale.y));
            var localCenter = colliderTransform.InverseTransformPoint(targetCenter);
            box.offset = new Vector2(localCenter.x, localCenter.y);
            return true;
        }

        public bool ConfigureRuntimeSprite(
            Sprite sprite,
            bool alignBaseToBlockingCollider = false,
            int transparentTopPaddingPixels = 0)
        {
            if (sprite == null) return false;
            presentationTopPaddingPixels = Mathf.Max(0, transparentTopPaddingPixels);

            if (runtimePresentationRenderer != null)
            {
                runtimePresentationRenderer.sprite = sprite;
                runtimePresentationRenderer.drawMode = SpriteDrawMode.Simple;
                runtimePresentationRenderer.size = Vector2.one;
                ApplyRuntimePresentationAnchor(
                    runtimePresentationRenderer.transform,
                    alignBaseToBlockingCollider);
                return true;
            }

            var sourceRenderer = GetComponentInChildren<SpriteRenderer>(true);
            var presentation = new GameObject("CatacombsObstaclePresentation");
            presentation.transform.SetParent(transform, false);
            var parentWorldScale = transform.lossyScale;
            presentation.transform.localScale = new Vector3(
                InverseScale(parentWorldScale.x),
                InverseScale(parentWorldScale.y),
                InverseScale(parentWorldScale.z));
            presentation.transform.localRotation = Quaternion.identity;

            var renderer = presentation.AddComponent<SpriteRenderer>();
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.size = Vector2.one;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.flipX = sourceRenderer.flipX;
                renderer.flipY = sourceRenderer.flipY;
            }

            ApplyRuntimePresentationAnchor(
                presentation.transform,
                alignBaseToBlockingCollider);

            foreach (var staleRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (staleRenderer != renderer) staleRenderer.enabled = false;

            runtimePresentationRenderer = renderer;
            return true;
        }

        private void ApplyRuntimePresentationAnchor(
            Transform presentation,
            bool alignBaseToBlockingCollider)
        {
            if (presentation == null) return;

            var presentationWorldOffset = Vector3.zero;
            if (alignBaseToBlockingCollider && blockingCollider != null)
            {
                // Catacombs obstacle sprites encode their transparent bottom
                // padding in the runtime Sprite pivot. The shared contract is
                // therefore the visible art base at the collider's lower edge.
                // The child is re-anchored after each sprite bind; changing a
                // Catacombs footprint never introduces a visual offset drift.
                presentationWorldOffset = new Vector3(
                    0f,
                    blockingCollider.bounds.min.y - transform.position.y,
                    0f);
            }

            presentation.localPosition = transform.InverseTransformVector(
                presentationWorldOffset);
        }

        private static float InverseScale(float value)
            => Mathf.Abs(value) > .0001f ? 1f / value : 1f;

        protected override int ResolveDepthSortingOrder(DungeonFloorMapView view)
        {
            // Flat decorations stay on the floor. Standing obstacles instead interleave
            // with actors at their grounded base, not their visual top.
            if (DungeonObject == null || IsGroundPresentation)
                return base.ResolveDepthSortingOrder(view);

            var baseline = runtimePresentationRenderer != null
                ? runtimePresentationRenderer.transform.position.y
                : blockingCollider is BoxCollider2D box
                    ? box.transform.TransformPoint(box.offset - Vector2.up * box.size.y * .5f).y
                    : transform.position.y;
            return view.GetStandingObjectDepthSortingOrder(baseline);
        }

        public void Initialize(
            ObstacleDungeonObjectInstance obstacle,
            Vector3 worldPosition)
        {
            if (!TryBind(obstacle, worldPosition)) return;

            IsGroundPresentation = string.Equals(
                obstacle.Definition?.Id,
                "object.obstacle.rubble",
                StringComparison.Ordinal)
                || string.Equals(
                    obstacle.Definition?.Id,
                    "object.obstacle.debug",
                    StringComparison.Ordinal)
                || string.Equals(obstacle.Definition?.Id, "object.forest.bedroll.prototype", StringComparison.Ordinal);
            if (obstacle.Definition is Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition definition
                && definition.PresentationSprite != null)
            {
                uint spriteNoise;
                unchecked { spriteNoise = (uint)(obstacle.Position.x * 73856093 ^ obstacle.Position.y * 19349663 ^ 13007); }
                var presentationSprite = Godmist.Data.Dungeons.DungeonVegetationPresentation.SelectTreeSprite(
                    definition.PresentationSprite, definition.PresentationVariants, spriteNoise);
                // Tree rarity is deliberately weighted. Other props share
                // their base and authored variants evenly at stable positions.
                if (!string.Equals(definition.Id, "object.forest.tree.prototype", StringComparison.Ordinal)
                    && definition.PresentationVariants is { Length: > 0 } variants)
                {
                    var slot = (int)(spriteNoise % (uint)(variants.Length + 1));
                    presentationSprite = slot == 0 ? definition.PresentationSprite
                        : variants[slot - 1] ?? definition.PresentationSprite;
                }
                ConfigureRuntimeSprite(presentationSprite);
                runtimePresentationRenderer.color = definition.PresentationTint;
                var artScale = definition.NativePixelScale ? 1f : definition.PresentationWidth / presentationSprite.bounds.size.x;
                var parentScale = transform.lossyScale;
                runtimePresentationRenderer.transform.localScale = new Vector3(
                    InverseScale(parentScale.x) * artScale, InverseScale(parentScale.y) * artScale, 1);
                if (definition.SizeInCells == new Vector2Int(2, 2) && blockingCollider != null)
                {
                    // Four genuinely occupied cells, not a single circular
                    // collider hidden under a large sprite. Anchor is SW.
                    blockingCollider.enabled = false;
                    var box = blockingCollider as BoxCollider2D ?? blockingCollider.gameObject.AddComponent<BoxCollider2D>();
                    var scale = box.transform.lossyScale;
                    box.size = new Vector2(2f / Mathf.Abs(scale.x), 2f / Mathf.Abs(scale.y));
                    var center = box.transform.InverseTransformPoint(worldPosition + new Vector3(.5f, .5f));
                    box.offset = new Vector2(center.x, center.y);
                    blockingCollider = box;
                    runtimePresentationRenderer.transform.position = worldPosition + new Vector3(.5f, -.5f);
                }
                else if (definition.TrunkRadius > 0 && blockingCollider != null)
                {
                    // Stable art variation is bound once; physical roots and
                    // the tactical footprint remain definition-owned.
                    uint noise;
                    unchecked { noise = (uint)(obstacle.Position.x * 73856093 ^ obstacle.Position.y * 19349663 ^ 13007); }
                    var widthVariation = definition.NativePixelScale ? 1f : .9f + (noise & 255u) / 255f * .2f;
                    var heightVariation = definition.NativePixelScale ? 1f : .9f + ((noise >> 8) & 255u) / 255f * .2f;
                    runtimePresentationRenderer.transform.localScale = new Vector3(
                        InverseScale(parentScale.x) * artScale * widthVariation,
                        InverseScale(parentScale.y) * artScale * heightVariation, 1);
                    runtimePresentationRenderer.flipX = (noise & 256u) != 0;
                    var shade = .88f + ((noise >> 16) & 255u) / 255f * .12f;
                    var tint = definition.PresentationTint;
                    runtimePresentationRenderer.color = new Color(tint.r * shade, tint.g * shade, tint.b * shade, tint.a);
                    var oldCollider = blockingCollider;
                    oldCollider.enabled = false;
                    var trunk = oldCollider as CircleCollider2D ?? oldCollider.gameObject.AddComponent<CircleCollider2D>();
                    var scale = trunk.transform.lossyScale;
                    trunk.radius = definition.TrunkRadius / Mathf.Max(.0001f, Mathf.Abs(scale.x));
                    var center = trunk.transform.InverseTransformPoint(worldPosition + Vector3.up * definition.TrunkRadius);
                    trunk.offset = new Vector2(center.x, center.y);
                    blockingCollider = trunk;
                }
            }
            hitRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            previousHits = obstacle.RemainingHits;
            if (blockingCollider == null) return;

            blockingCollider.enabled = obstacle.IsActive && obstacle.BlocksMovement;
            blockingCollider.isTrigger = false;
        }
        private static readonly Unity.Profiling.ProfilerMarker ObstacleLateSample = new("Godmist.Dungeon.ObstacleLate");
        protected override void LateUpdate()
        {
            using var sample = ObstacleLateSample.Auto();
            if (DungeonObject is ObstacleDungeonObjectInstance obstacle)
            {
                if (obstacle.RemainingHits < previousHits) hitFlashUntil = Time.time + .12f;
                previousHits = obstacle.RemainingHits;
                if (blockingCollider != null && blockingCollider.enabled != (obstacle.IsActive && obstacle.BlocksMovement))
                    blockingCollider.enabled = obstacle.IsActive && obstacle.BlocksMovement;
            }
            base.LateUpdate();
            if (DungeonObject is { IsActive: false } && IsElevationVisible && hitRenderers != null)
                foreach (var renderer in hitRenderers)
                    if (renderer != null) renderer.forceRenderingOff = true;
        }
        protected override void RefreshFogVisual()
        {
            if (IsElevationVisible && Time.time < hitFlashUntil && hitRenderers != null)
                foreach (var renderer in hitRenderers) if (renderer != null) renderer.color = Color.white;
        }
    }
}
