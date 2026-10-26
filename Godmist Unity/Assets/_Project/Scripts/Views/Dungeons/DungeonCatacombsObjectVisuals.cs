using System;
using System.Collections.Generic;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    /// <summary>
    /// Runtime-only presentation resolver for the Catacombs object definitions.
    /// The sandbox scene still supplies the normal prefabs; this resolver only
    /// replaces their stale art and supplies explicit views for the two object
    /// types whose scene references are intentionally empty.
    /// </summary>
    internal static class DungeonCatacombsObjectVisuals
    {
        private const string ResourceRoot = "CatacombsAudit/";
        private const float PixelsPerUnit = 32f;
        // The authored v2 canvas is 32px, but the embedded blade is a small
        // ground prop beside the 32px/0.7x exploration character. Keep audit
        // and combat presenters on one measured world scale and sink the tip
        // just below the floor baseline so it reads as planted.
        public const float GroundDaggerPresentationScale = 0.45f;
        public static readonly Vector2 GroundDaggerPresentationOffset = new(0f, -.06f);

        private readonly struct SpriteSpec
        {
            public SpriteSpec(
                string resourceName,
                Vector2 pivot,
                bool alignBaseToBlockingCollider = false,
                Vector2 colliderWorldSize = default,
                int transparentTopPaddingPixels = 0)
            {
                ResourceName = resourceName;
                Pivot = pivot;
                AlignBaseToBlockingCollider = alignBaseToBlockingCollider;
                ColliderWorldSize = colliderWorldSize;
                TransparentTopPaddingPixels = transparentTopPaddingPixels;
            }

            public string ResourceName { get; }
            public Vector2 Pivot { get; }
            public bool AlignBaseToBlockingCollider { get; }
            public Vector2 ColliderWorldSize { get; }
            public int TransparentTopPaddingPixels { get; }
            public bool HasColliderGeometry => ColliderWorldSize.x > 0f && ColliderWorldSize.y > 0f;
        }

        private static readonly Dictionary<string, SpriteSpec> Specs =
            new(StringComparer.Ordinal)
            {
                ["object.spike_trap.debug"] = new("catacombs-debug-trap", new(.5f, .5f)),
                ["object.trap.spikes"] = new("catacombs-spikes", new(.5f, .5f)),
                ["object.trap.gas"] = new("catacombs-gas-vent", new(.5f, .5f)),
                ["object.trap.graveseal"] = new("catacombs-grave-seal", new(.5f, .5f)),
                // The pivot includes the measured transparent rows below each
                // authored silhouette. Every obstacle is then placed with its
                // visible base on the prefab collider's lower edge; this keeps
                // the cell/root contract identical across the three footprints.
                ["object.obstacle.pillar"] = new(
                    "catacombs-pillar",
                    new(.5f, .03125f),
                    alignBaseToBlockingCollider: true,
                    transparentTopPaddingPixels: 3),
                ["object.obstacle.rubble"] = new(
                    "catacombs-rubble",
                    new(.5f, .15625f),
                    alignBaseToBlockingCollider: true,
                    colliderWorldSize: new(1f, .5f), transparentTopPaddingPixels: 7),
                ["object.obstacle.debug"] = new(
                    "catacombs-rubble",
                    new(.5f, .15625f),
                    alignBaseToBlockingCollider: true,
                    colliderWorldSize: new(1f, .5f), transparentTopPaddingPixels: 7),
                ["object.obstacle.sarcophagus"] = new(
                    "catacombs-sarcophagus",
                    new(.5f, .0625f),
                    alignBaseToBlockingCollider: true,
                    colliderWorldSize: new(1f, 2f), transparentTopPaddingPixels: 3),
                ["object.lever.debug"] = new("catacombs-lever-v4", new(.5f, 0f)),
                ["object.condition.debug"] = new("catacombs-condition", new(.5f, .5f))
            };

        private static readonly Dictionary<string, Sprite> LoadedSprites =
            new(StringComparer.Ordinal);

        public static bool TryApplyTrap(
            DungeonSpikeTrapView view,
            SpikeTrapDungeonObjectDefinition definition)
        {
            if (view == null || definition == null
                || !Specs.TryGetValue(definition.Id, out var spec)) return false;

            var armed = Load(spec);
            if (armed == null) return false;

            var triggered = string.Equals(
                definition.Id,
                "object.trap.gas",
                StringComparison.Ordinal)
                ? Load(new SpriteSpec("catacombs-gas-base", spec.Pivot))
                : armed;
            view.ConfigureRuntimeSprites(armed, triggered ?? armed);
            view.ConfigureRuntimePresentationColors(
                new Color(.82f, .82f, .78f, 1f),
                new Color(.45f, .45f, .42f, 1f));
            return true;
        }

        /// <summary>Returns a cached Catacombs presentation sprite for another runtime owner.</summary>
        public static bool TryLoadPresentationSprite(
            string resourceName,
            Vector2 pivot,
            out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(resourceName)) return false;
            sprite = Load(new SpriteSpec(resourceName, pivot));
            return sprite != null;
        }

        public static bool TryApplyObstacle(
            DungeonObstacleView view,
            ObstacleDungeonObjectDefinition definition)
        {
            if (view == null || definition == null
                || !Specs.TryGetValue(definition.Id, out var spec)) return false;

            if (spec.HasColliderGeometry
                && !view.ConfigureRuntimeColliderGeometry(
                    spec.ColliderWorldSize,
                    preserveBottomEdge: true))
            {
                return false;
            }

            return view.ConfigureRuntimeSprite(
                Load(spec),
                spec.AlignBaseToBlockingCollider,
                spec.TransparentTopPaddingPixels);
        }

        public static DungeonLeverView CreateLeverView(
            Transform parent,
            string objectName,
            LeverDungeonObjectDefinition definition)
            => CreateExplicitView<DungeonLeverView>(
                parent,
                objectName,
                definition?.Id);

        public static DungeonConditionView CreateConditionView(
            Transform parent,
            string objectName,
            ConditionDungeonObjectDefinition definition)
            => CreateExplicitView<DungeonConditionView>(
                parent,
                objectName,
                definition?.Id);

        private static T CreateExplicitView<T>(
            Transform parent,
            string objectName,
            string definitionId)
            where T : DungeonObjectView
        {
            if (string.IsNullOrWhiteSpace(definitionId)
                || !Specs.TryGetValue(definitionId, out var spec)) return null;

            var sprite = Load(spec);
            if (sprite == null) return null;

            var gameObject = new GameObject(objectName);
            if (parent != null) gameObject.transform.SetParent(parent, false);

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 6;
            var view = gameObject.AddComponent<T>();
            if (view is DungeonLeverView leverView)
            {
                leverView.ConfigureRuntimePresentation(
                    // The authored source already carries the muted iron and
                    // stone palette. Keep both states neutral so activation
                    // does not wash the floor base brown; the asymmetric
                    // handle position and runtime flipX provide the cue.
                    Color.white,
                    Color.white,
                    true);
            }
            else if (view is DungeonConditionView conditionView)
            {
                conditionView.ConfigureRuntimePresentation(
                    new Color(.62f, .62f, .58f, 1f),
                    new Color(.46f, .70f, .50f, 1f));
            }
            return view;
        }

        private static Sprite Load(SpriteSpec spec)
        {
            var cacheKey = spec.ResourceName + ":" + spec.Pivot.x + ":" + spec.Pivot.y;
            if (LoadedSprites.TryGetValue(cacheKey, out var cached) && cached != null && cached.texture != null)
            {
                cached.texture.filterMode = FilterMode.Point;
                cached.texture.wrapMode = TextureWrapMode.Clamp;
                return cached;
            }

            var texture = Resources.Load<Texture2D>(ResourceRoot + spec.ResourceName);
            if (texture == null)
            {
                Debug.LogError(
                    "DungeonCatacombsObjectVisuals: missing resource "
                    + ResourceRoot + spec.ResourceName);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                spec.Pivot,
                PixelsPerUnit);
            sprite.name = spec.ResourceName + " Runtime Sprite";
            LoadedSprites[cacheKey] = sprite;
            return sprite;
        }
    }
}

