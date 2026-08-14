using System.Text;
using Godmist.Data.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Vector2 screenOffset = new(18f, -18f);

        private bool isVisible;

        private void Awake()
        {
            Hide();
        }

        public void ShowSkill(SkillData skill, int actionCost, Vector2 screenPosition)
        {
            if (skill == null)
            {
                Hide();
                return;
            }

            if (icon != null)
            {
                icon.sprite = skill.Icon;
                icon.enabled = skill.Icon != null;
            }

            if (titleText != null)
            {
                titleText.text = skill.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = BuildDescription(skill);
            }

            if (costText != null)
            {
                costText.text = $"AP {actionCost}  •  RES {skill.ResourceCost}";
            }

            gameObject.SetActive(true);
            isVisible = true;
            PlaceAt(screenPosition);
        }

        public void Hide()
        {
            isVisible = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isVisible)
            {
                PlaceAt(Input.mousePosition);
            }
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (tooltipRect == null || tooltipRect.parent is not RectTransform parentRect)
            {
                return;
            }

            var canvas = tooltipRect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    camera,
                    out var pointerPosition))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            var size = tooltipRect.rect.size;
            var horizontalOffset = Mathf.Abs(screenOffset.x);
            var verticalOffset = Mathf.Abs(screenOffset.y);

            // The tooltip's top-left corner follows the pointer by default. Only the axis
            // that would leave the viewport is flipped, which keeps one tooltip corner
            // attached to the cursor instead of sliding the whole tooltip along an edge.
            var pivot = new Vector2(0f, 1f);
            var offset = new Vector2(horizontalOffset, -verticalOffset);

            if (pointerPosition.x + horizontalOffset + size.x > parentRect.rect.xMax)
            {
                pivot.x = 1f;
                offset.x = -horizontalOffset;
            }

            if (pointerPosition.y - verticalOffset - size.y < parentRect.rect.yMin)
            {
                pivot.y = 0f;
                offset.y = verticalOffset;
            }

            tooltipRect.pivot = pivot;

            var anchorReference = new Vector2(
                Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, tooltipRect.anchorMin.x),
                Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, tooltipRect.anchorMin.y));
            var requestedPivotPosition = pointerPosition + offset;
            var minimumPivotPosition = parentRect.rect.min + Vector2.Scale(size, tooltipRect.pivot);
            var maximumPivotPosition = parentRect.rect.max
                - Vector2.Scale(size, Vector2.one - tooltipRect.pivot);
            var clampedPivotPosition = new Vector2(
                Mathf.Clamp(
                    requestedPivotPosition.x,
                    minimumPivotPosition.x,
                    maximumPivotPosition.x),
                Mathf.Clamp(
                    requestedPivotPosition.y,
                    minimumPivotPosition.y,
                    maximumPivotPosition.y));

            tooltipRect.anchoredPosition = clampedPivotPosition - anchorReference;
        }

        private static string BuildDescription(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill.TooltipDescription))
            {
                return skill.TooltipDescription;
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < skill.Effects.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(" • ");
                }

                builder.Append(skill.Effects[index].EffectType);
            }

            return builder.ToString();
        }
    }
}
