using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;
using Godmist.Runtime.Combat;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Runtime-created, non-interactive damage/hit preview shown above the unit
    /// currently hovered while a skill is being targeted.
    /// </summary>
    public sealed class CombatTargetPreviewView : MonoBehaviour
    {
        private RectTransform rectTransform;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private TextMeshProUGUI titleText;
        private CanvasGroup canvasGroup;
        private RectTransform parentRect;
        [SerializeField] private Image statusIcon;
        [SerializeField] private TextMeshProUGUI statusChanceLabel;

        public static CombatTargetPreviewView Create(Transform parent, CombatTargetPreviewView template = null)
        {
            if (parent == null) return null;
            if (template != null)
            {
                var clone = Instantiate(template, parent, false);
                clone.Initialize();
                return clone;
            }

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

        public void Show(Vector2 screenPosition, string text, Color color,
            Sprite statusSprite = null, float statusChance = 0f, BattleUnit target = null)
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

            var hasStatus = statusSprite != null;
            var hasText = !string.IsNullOrEmpty(text);
            var authored = titleText != null;
            rectTransform.sizeDelta = authored ? new Vector2(300f, hasStatus ? 136f : 112f)
                : new Vector2(hasStatus ? hasText ? 262f : 72f : 190f, 42f);
            var half = rectTransform.sizeDelta * .5f;
            // Match ordinary hover tooltips: top-left beside the pointer, then
            // flip at the screen edge instead of anchoring to the target's tile.
            var position = localPosition + new Vector2(half.x + 8f, -half.y - 8f);
            if (position.x + half.x > parentRect.rect.xMax)
                position.x = localPosition.x - half.x - 8f;
            if (position.y - half.y < parentRect.rect.yMin)
                position.y = localPosition.y + half.y + 8f;
            position.x = Mathf.Clamp(position.x, parentRect.rect.xMin + half.x, parentRect.rect.xMax - half.x);
            position.y = Mathf.Clamp(position.y, parentRect.rect.yMin + half.y, parentRect.rect.yMax - half.y);
            rectTransform.anchoredPosition = position;
            if (titleText != null) titleText.text = target?.Character?.Definition?.DisplayName ?? target?.Character?.CharacterId ?? string.Empty;
            var health = authored && target?.Character != null ? $"HP: {CombatHealthPresentation.Format(target.Character)}\n" : string.Empty;
            label.text = health + (text ?? string.Empty);
            label.color = authored ? new Color(.91f,.875f,.78f) : color;
            if (statusIcon != null)
            {
                statusIcon.gameObject.SetActive(hasStatus);
                statusIcon.sprite = statusSprite;
            }
            if (statusChanceLabel != null)
            {
                statusChanceLabel.gameObject.SetActive(hasStatus);
                statusChanceLabel.text = $"{Mathf.RoundToInt(statusChance * 100f)}%";
            }
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = authored ? new Vector2(16f, hasStatus ? 40f : 12f) : new Vector2(8f, 4f);
            labelRect.offsetMax = authored ? new Vector2(-16f, -44f) : new Vector2(hasStatus && hasText ? -72f : -8f, -4f);
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            // The HUD can be destroyed before the world targeting controller on scene exit.
            if (this == null) return;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            else gameObject.SetActive(false);
        }

        private void Initialize()
        {
            rectTransform ??= transform as RectTransform;
            parentRect ??= transform.parent as RectTransform;
            canvasGroup ??= GetComponent<CanvasGroup>();

            if (label != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                return;
            }

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

            if (statusIcon == null)
            {
                var iconObject = new GameObject("Status Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                statusIcon = iconObject.GetComponent<Image>();
                statusIcon.raycastTarget = false;
                statusIcon.preserveAspect = true;
                var iconRect = statusIcon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(1f, 0.5f);
                iconRect.pivot = new Vector2(1f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-42f, 0f);
                iconRect.sizeDelta = new Vector2(24f, 24f);

                var chanceObject = new GameObject("Status Chance", typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                chanceObject.transform.SetParent(transform, false);
                statusChanceLabel = chanceObject.GetComponent<TextMeshProUGUI>();
                statusChanceLabel.font = TMP_Settings.defaultFontAsset;
                statusChanceLabel.fontSize = 14f;
                statusChanceLabel.fontStyle = FontStyles.Bold;
                statusChanceLabel.alignment = TextAlignmentOptions.MidlineRight;
                statusChanceLabel.raycastTarget = false;
                statusChanceLabel.color = Color.white;
                var chanceRect = statusChanceLabel.rectTransform;
                chanceRect.anchorMin = chanceRect.anchorMax = new Vector2(1f, 0.5f);
                chanceRect.pivot = new Vector2(1f, 0.5f);
                chanceRect.anchoredPosition = new Vector2(-6f, 0f);
                chanceRect.sizeDelta = new Vector2(38f, 26f);
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
