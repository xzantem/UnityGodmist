using System;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatSkillSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI hotkeyText;

        private TextMeshProUGUI runtimeKnifeCountText;
        private SkillData skill;
        private UIWorkspaceTooltipView tooltipView;
        private Func<BattleUnit> casterProvider;
        private int actionCost;
        private Color defaultIconColor = Color.white;
        private static readonly Color ActiveToggleColor = new(0.35f, 1f, 0.65f, 1f);

        public void Initialize(
            SkillData sourceSkill,
            UIWorkspaceTooltipView sourceTooltipView,
            Action onClick,
            Func<BattleUnit> sourceCasterProvider = null)
        {
            skill = sourceSkill;
            tooltipView = sourceTooltipView;
            casterProvider = sourceCasterProvider;

            if (icon != null)
            {
                defaultIconColor = icon.color;
                icon.sprite = skill?.Icon;
                icon.enabled = skill?.Icon != null;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void RefreshAvailability(
            bool isAvailable,
            int calculatedActionCost,
            bool isActiveToggle,
            string knifeCount)
        {
            actionCost = calculatedActionCost;
            if (button != null)
            {
                button.interactable = isAvailable;
            }

            if (unavailableOverlay != null)
            {
                unavailableOverlay.enabled = !isAvailable;
            }

            if (icon != null)
            {
                icon.color = isActiveToggle
                    ? ActiveToggleColor
                    : defaultIconColor;
            }

            if (hotkeyText != null)
            {
                hotkeyText.text = knifeCount ?? string.Empty;
                hotkeyText.enabled = !string.IsNullOrWhiteSpace(knifeCount);
                return;
            }

            if (string.IsNullOrWhiteSpace(knifeCount))
            {
                if (runtimeKnifeCountText != null)
                {
                    runtimeKnifeCountText.enabled = false;
                }

                return;
            }

            var counterText = GetKnifeCountText();
            if (counterText != null)
            {
                counterText.text = knifeCount;
                counterText.enabled = true;
            }
        }

        public bool TryActivate()
        {
            if (button == null || !button.interactable)
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        private TextMeshProUGUI GetKnifeCountText()
        {
            if (hotkeyText != null)
            {
                return hotkeyText;
            }

            if (runtimeKnifeCountText != null)
            {
                return runtimeKnifeCountText;
            }

            var counterObject = new GameObject("Knife Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            counterObject.transform.SetParent(transform, false);
            counterObject.transform.SetAsLastSibling();

            var counterRect = (RectTransform)counterObject.transform;
            counterRect.anchorMin = new Vector2(1f, 0f);
            counterRect.anchorMax = new Vector2(1f, 0f);
            counterRect.pivot = new Vector2(1f, 0f);
            counterRect.anchoredPosition = new Vector2(-3f, 3f);
            counterRect.sizeDelta = new Vector2(40f, 18f);

            runtimeKnifeCountText = counterObject.GetComponent<TextMeshProUGUI>();
            runtimeKnifeCountText.alignment = TextAlignmentOptions.BottomRight;
            runtimeKnifeCountText.fontSize = 14f;
            runtimeKnifeCountText.fontStyle = FontStyles.Bold;
            runtimeKnifeCountText.color = Color.white;
            runtimeKnifeCountText.outlineWidth = 0.2f;
            runtimeKnifeCountText.outlineColor = Color.black;
            runtimeKnifeCountText.raycastTarget = false;
            runtimeKnifeCountText.enabled = false;
            return runtimeKnifeCountText;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltipView?.ShowSkill(skill, actionCost, this, casterProvider?.Invoke());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltipView?.NotifySourceExit(this);
        }
    }
}
