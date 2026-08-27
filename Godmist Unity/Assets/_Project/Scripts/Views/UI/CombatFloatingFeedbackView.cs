using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatFloatingFeedbackView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0f)] private float holdDuration = 3f;
        [SerializeField, Min(0f)] private float fadeDuration = 3f;
        [SerializeField, Min(0f)] private float introDriftDuration = 0.1f;
        [SerializeField, Min(0f)] private float introDriftDistance = 18f;
        [SerializeField] private Vector2 travel = new(0f, 42f);
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.18f;
        [SerializeField, Range(0f, 1f)] private float emphasizedOutlineWidth = 0.3f;

        private RectTransform rectTransform;
        private Material runtimeFontMaterial;
        private Vector2 startPosition;
        private Vector2 introStartPosition;
        private Vector2 activeTravel;
        private float startTime;
        private float delay;

        public Vector2 LayoutPosition => startPosition;

        public void ConfigureIntroDrift(float duration, float distance)
        {
            introDriftDuration = Mathf.Max(0f, duration);
            introDriftDistance = Mathf.Max(0f, distance);
        }

        public void ConfigureLifetime(float linger, float fade)
        {
            holdDuration = Mathf.Max(0f, linger);
            fadeDuration = Mathf.Max(0f, fade);
        }

        public Vector2 GetLayoutSize(string text, bool emphasized)
        {
            var minimumHeight = 48f;
            if (label == null)
            {
                return new Vector2(emphasized ? 72f : 56f, minimumHeight);
            }

            var originalFontSize = label.fontSize;
            var originalFontStyle = label.fontStyle;
            label.fontSize = emphasized ? 40f : 36f;
            label.fontStyle = FontStyles.Bold;
            var preferredSize = label.GetPreferredValues(text);
            label.fontSize = originalFontSize;
            label.fontStyle = originalFontStyle;

            return new Vector2(
                Mathf.Max(36f, preferredSize.x),
                Mathf.Max(minimumHeight, preferredSize.y));
        }

        public void Show(
            string text,
            Color color,
            Vector2 position,
            float startDelay,
            bool emphasized,
            float horizontalDrift = 0f)
        {
            rectTransform ??= transform as RectTransform;
            startPosition = position;
            activeTravel = new Vector2(horizontalDrift, travel.y);
            delay = startDelay;
            startTime = Time.unscaledTime;
            var introDirection = Mathf.Abs(horizontalDrift) > 0.001f
                ? new Vector2(Mathf.Sign(horizontalDrift), 0f)
                : Vector2.up;
            introStartPosition = position
                - introDirection * introDriftDistance
                + Vector2.down * (introDriftDistance * 0.45f);
            rectTransform.anchoredPosition = introStartPosition;
            canvasGroup.alpha = 0f;

            // The template is inactive when instantiated. TMP initializes its
            // shared material on enable, so activate before touching outline
            // properties (otherwise SetOutlineThickness can dereference a
            // missing material instance).
            gameObject.SetActive(true);

            label.text = text;
            label.color = color;
            label.fontSize = emphasized ? 40f : 36f;
            label.fontStyle = FontStyles.Bold;
            ApplyOutline(emphasized ? emphasizedOutlineWidth : outlineWidth);
        }

        private void OnDestroy()
        {
            if (runtimeFontMaterial != null)
            {
                Destroy(runtimeFontMaterial);
            }
        }

        private void ApplyOutline(float width)
        {
            if (label == null)
            {
                return;
            }

            var sourceMaterial = label.fontSharedMaterial ?? label.font?.material;
            if (sourceMaterial == null)
            {
                return;
            }

            if (runtimeFontMaterial == null)
            {
                runtimeFontMaterial = new Material(sourceMaterial)
                {
                    name = $"{sourceMaterial.name} Material Instance"
                };
                label.fontSharedMaterial = runtimeFontMaterial;
            }

            if (runtimeFontMaterial.HasProperty("_OutlineColor"))
            {
                runtimeFontMaterial.EnableKeyword("OUTLINE_ON");
                runtimeFontMaterial.SetColor("_OutlineColor", outlineColor);
            }

            if (runtimeFontMaterial.HasProperty("_OutlineWidth"))
            {
                runtimeFontMaterial.SetFloat("_OutlineWidth", width);
            }

            label.UpdateMeshPadding();
            label.SetVerticesDirty();
        }

        private void Update()
        {
            var elapsed = Time.unscaledTime - startTime - delay;
            if (elapsed < 0f) return;

            var effectiveHoldDuration = Mathf.Max(0f, holdDuration);
            var effectiveFadeDuration = Mathf.Max(0.01f, fadeDuration);
            var effectiveIntroDuration = Mathf.Max(0f, introDriftDuration);
            if (elapsed < effectiveIntroDuration)
            {
                var introProgress = Mathf.Clamp01(elapsed / effectiveIntroDuration);
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    introStartPosition,
                    startPosition,
                    introProgress);
                canvasGroup.alpha = 1f;
                return;
            }

            var holdElapsed = elapsed - effectiveIntroDuration;
            if (holdElapsed <= effectiveHoldDuration)
            {
                rectTransform.anchoredPosition = startPosition;
                canvasGroup.alpha = 1f;
                return;
            }

            var progress = Mathf.Clamp01(
                (elapsed - effectiveIntroDuration - effectiveHoldDuration)
                / effectiveFadeDuration);
            var effectiveTravel = activeTravel;
            effectiveTravel.y = Mathf.Max(72f, effectiveTravel.y);
            rectTransform.anchoredPosition = startPosition + effectiveTravel * progress;
            canvasGroup.alpha = 1f - progress;
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
