using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatFloatingFeedbackView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.1f)] private float lifetime = 1.1f;
        [SerializeField] private Vector2 travel = new(0f, 42f);

        private RectTransform rectTransform;
        private Vector2 startPosition;
        private float startTime;
        private float delay;

        public void Show(string text, Color color, Vector2 position, float startDelay, bool emphasized)
        {
            rectTransform ??= transform as RectTransform;
            startPosition = position;
            delay = startDelay;
            startTime = Time.unscaledTime;
            rectTransform.anchoredPosition = position;
            label.text = text;
            label.color = color;
            label.fontSize = emphasized ? 48f : 36f;
            label.fontStyle = FontStyles.Bold;
            label.outlineColor = new Color(0.035f, 0.05f, 0.08f, 1f);
            label.outlineWidth = emphasized ? 0.36f : 0.28f;
            canvasGroup.alpha = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            var elapsed = Time.unscaledTime - startTime - delay;
            if (elapsed < 0f) return;

            var effectiveLifetime = Mathf.Max(1.1f, lifetime);
            var progress = Mathf.Clamp01(elapsed / effectiveLifetime);
            var effectiveTravel = travel;
            effectiveTravel.y = Mathf.Max(72f, effectiveTravel.y);
            rectTransform.anchoredPosition = startPosition + effectiveTravel * progress;
            canvasGroup.alpha = progress < 0.55f
                ? 1f
                : 1f - (progress - 0.55f) / 0.45f;
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
