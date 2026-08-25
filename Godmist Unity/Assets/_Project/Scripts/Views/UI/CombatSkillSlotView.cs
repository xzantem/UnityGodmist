using System;
using Godmist.Data.Skills;
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

        private SkillData skill;
        private UIWorkspaceTooltipView tooltipView;
        private int actionCost;
        private Color defaultIconColor = Color.white;
        private static readonly Color ActiveToggleColor = new(0.35f, 1f, 0.65f, 1f);

        public void Initialize(
            SkillData sourceSkill,
            UIWorkspaceTooltipView sourceTooltipView,
            Action onClick)
        {
            skill = sourceSkill;
            tooltipView = sourceTooltipView;

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
            bool isActiveToggle)
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
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltipView?.ShowSkill(skill, actionCost, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltipView?.NotifySourceExit(this);
        }
    }
}
