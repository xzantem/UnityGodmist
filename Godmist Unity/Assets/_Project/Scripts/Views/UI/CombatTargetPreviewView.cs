using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Runtime-created, non-interactive damage/hit preview shown above the unit
    /// currently hovered while a skill is being targeted.
    /// </summary>
    public sealed class CombatTargetPreviewView : MonoBehaviour
    {
        private RectTransform rectTransform;
        private TextMeshProUGUI label;
        private CanvasGroup canvasGroup;
        private RectTransform parentRect;

        public static CombatTargetPreviewView Create(Transform parent)
        {
            if (parent == null) return null;

            var gameObject = new GameObject(
                "Combat Target Preview",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(CombatTargetPreviewView));
            gameObject.transform.SetParent(parent, false);

            var view = gameObject.GetComponent<CombatTargetPreviewView>();
            view.Initialize();
            return view;
        }

        public void Show(Vector2 screenPosition, string text, Color color)
        {
            if (rectTransform == null) Initialize();
            if (parentRect == null || label == null || canvasGroup == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    GetCanvasCamera(),
                    out var localPosition))
            {
                return;
            }

            rectTransform.anchoredPosition = localPosition + new Vector2(0f, 44f);
            label.text = text ?? string.Empty;
            label.color = color;
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            else gameObject.SetActive(false);
        }

        private void Initialize()
        {
            rectTransform ??= transform as RectTransform;
            parentRect ??= transform.parent as RectTransform;
            canvasGroup ??= GetComponent<CanvasGroup>();

            var background = GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.04f, 0.06f, 0.11f, 0.92f);
                background.raycastTarget = false;
            }

            if (label == null)
            {
                var labelObject = new GameObject(
                    "Preview Text",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(transform, false);
                label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = TMP_Settings.defaultFontAsset;
                label.fontSize = 15f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.outlineWidth = 0.2f;
                label.outlineColor = Color.black;

                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 4f);
                labelRect.offsetMax = new Vector2(-8f, -4f);
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(190f, 42f);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private Camera GetCanvasCamera()
        {
            var canvas = parentRect?.GetComponentInParent<Canvas>()?.rootCanvas;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }
    }
}
