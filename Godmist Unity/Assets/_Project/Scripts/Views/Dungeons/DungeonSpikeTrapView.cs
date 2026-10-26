using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonSpikeTrapView : DungeonObjectView
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Color armedColor = new(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color triggeredColor = new(0.45f, 0.45f, 0.45f, 1f);

        private SpikeTrapDungeonObjectInstance trap;
        private Sprite armedSprite;
        private Sprite triggeredSprite;
        private SpriteRenderer runtimePresentationRenderer;

        public void ConfigureRuntimeSprites(Sprite nextArmedSprite, Sprite nextTriggeredSprite = null, float nativeScale = 1f)
        {
            armedSprite = nextArmedSprite;
            triggeredSprite = nextTriggeredSprite ?? nextArmedSprite;
            visualRenderer = EnsureRuntimePresentationRenderer();
            var parentScale=transform.lossyScale;
            visualRenderer.transform.localScale=new Vector3(InverseScale(parentScale.x)*nativeScale,InverseScale(parentScale.y)*nativeScale,InverseScale(parentScale.z));
            RefreshVisual();
        }

        /// <summary>
        /// Applies a presentation-only state palette to a runtime replacement
        /// sprite.  Production trap prefabs have authored state tints for
        /// their original art; Catacombs audit sprites already carry their
        /// own palette and must not inherit those saturated tints.
        /// </summary>
        public void ConfigureRuntimePresentationColors(
            Color nextArmedColor,
            Color nextTriggeredColor)
        {
            armedColor = nextArmedColor;
            triggeredColor = nextTriggeredColor;
            RefreshVisual();
        }

        private SpriteRenderer EnsureRuntimePresentationRenderer()
        {
            if (runtimePresentationRenderer != null) return runtimePresentationRenderer;

            var sourceRenderer = visualRenderer ?? GetComponentInChildren<SpriteRenderer>(true);
            var presentation = new GameObject("DungeonTrapPresentation");
            presentation.layer = gameObject.layer;
            presentation.transform.SetParent(transform, false);
            var parentWorldScale = transform.lossyScale;
            presentation.transform.localScale = new Vector3(
                InverseScale(parentWorldScale.x),
                InverseScale(parentWorldScale.y),
                InverseScale(parentWorldScale.z));
            presentation.transform.localRotation = Quaternion.identity;
            presentation.transform.localPosition = Vector3.zero;

            var renderer = presentation.AddComponent<SpriteRenderer>();
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.size = Vector2.one;
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.flipX = sourceRenderer.flipX;
                renderer.flipY = sourceRenderer.flipY;
            }

            foreach (var staleRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (staleRenderer != renderer) staleRenderer.enabled = false;

            runtimePresentationRenderer = renderer;
            return renderer;
        }

        private static float InverseScale(float value)
            => Mathf.Abs(value) > .0001f ? 1f / value : 1f;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            SpikeTrapDungeonObjectInstance nextTrap,
            Tilemap nextFloorTilemap,
            Vector3 worldPosition)
        {
            if (!TryBind(nextTrap, worldPosition)) return;

            trap = nextTrap;
            RefreshVisual();
        }

        private void Update() => RefreshVisual();

        private void RefreshVisual()
        {
            if (visualRenderer != null)
            {
                var isTriggered = PresentationState(trap != null && trap.IsTriggered);
                if (armedSprite != null)
                {
                    visualRenderer.sprite = isTriggered ? triggeredSprite : armedSprite;
                }
                visualRenderer.color = isTriggered
                    ? triggeredColor
                    : armedColor;
            }
        }
        protected override void RefreshFogVisual() => RefreshVisual();

    }
}
