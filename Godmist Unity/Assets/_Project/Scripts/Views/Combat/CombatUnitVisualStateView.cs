using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatUnitVisualStateView : MonoBehaviour
    {
        [SerializeField] private Color activeTurnColor = new(1f, .78f, .22f, 1f);
        [SerializeField] private Color inspectColor = new(.5f, .86f, 1f, 1f);
        [SerializeField] private Color targetColor = new(1f, .34f, .28f, 1f);
        [SerializeField, Min(0.001f)] private float outlineWidth = .06f;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
        private SpriteRenderer[] renderers;
        private Color[] defaults;
        private SpriteRenderer[] outlineRenderers;
        private Material outlineMaterial;
        private MaterialPropertyBlock[] outlineProperties;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            defaults = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) defaults[i] = renderers[i].color;
            if (renderers.Length == 0) return;

            var outlineShader = Shader.Find("Godmist/Combat/Solid Sprite Silhouette");
            if (outlineShader == null)
            {
                Debug.LogError("Combat visual-state outline shader was not found.", this);
                return;
            }

            outlineMaterial = new Material(outlineShader);
            var offsets = new[]
            {
                Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                new Vector2(-1f, -1f), new Vector2(-1f, 1f),
                new Vector2(1f, -1f), new Vector2(1f, 1f)
            };
            outlineRenderers = new SpriteRenderer[offsets.Length];
            outlineProperties = new MaterialPropertyBlock[offsets.Length];
            for (var index = 0; index < offsets.Length; index++)
            {
                var outline = new GameObject("Visual State Outline", typeof(SpriteRenderer));
                outline.transform.SetParent(renderers[0].transform, false);
                outline.transform.localPosition = offsets[index] * outlineWidth;
                var renderer = outline.GetComponent<SpriteRenderer>();
                renderer.sortingLayerID = renderers[0].sortingLayerID;
                renderer.sortingOrder = renderers[0].sortingOrder - 1;
                renderer.sharedMaterial = outlineMaterial;
                outlineRenderers[index] = renderer;
                outlineProperties[index] = new MaterialPropertyBlock();
            }
        }

        private void OnDestroy()
        {
            if (outlineMaterial != null) Destroy(outlineMaterial);
        }

        public void Refresh(BattleUnit unit, bool isActive, bool isHovered, bool isTargeting)
        {
            if (renderers == null) Awake();
            var accent = isHovered ? (isTargeting ? targetColor : inspectColor) : isActive ? activeTurnColor : Color.clear;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].color = defaults[i];
            }
            if (outlineRenderers == null) return;
            // The colour deliberately reaches the unmodified accent.  The easing keeps it
            // there longer than at the black end, so the unit reads as gold/blue/red rather
            // than as a permanently dim outline.
            var rawPulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            var pulse = Mathf.Pow(rawPulse, .45f);
            var outlineColor = Color.Lerp(Color.black, accent, pulse);
            if (StatusEffectSystem.IsHardCrowdControlled(unit))
                outlineColor = Color.Lerp(Color.black, outlineColor, .55f);
            for (var index = 0; index < outlineRenderers.Length; index++)
            {
                var outlineRenderer = outlineRenderers[index];
                outlineRenderer.sprite = renderers[0].sprite;
                outlineRenderer.flipX = renderers[0].flipX;
                outlineRenderer.flipY = renderers[0].flipY;
                outlineRenderer.enabled = accent.a > 0f;
                if (!outlineRenderer.enabled) continue;

                var properties = outlineProperties[index];
                properties.SetColor("_OutlineColor", outlineColor);
                outlineRenderer.SetPropertyBlock(properties);
            }
        }
    }
}
