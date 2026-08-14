using System;
using Godmist.Data.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatSkillSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI hotkeyText;

        private SkillData skill;
        private CombatTooltipView tooltipView;
        private int actionCost;

        public void Initialize(
            SkillData sourceSkill,
            CombatTooltipView sourceTooltipView,
            Action onClick)
        {
            skill = sourceSkill;
            tooltipView = sourceTooltipView;

            if (icon != null)
            {
                icon.sprite = skill?.Icon;
                icon.enabled = skill?.Icon != null;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void RefreshAvailability(bool isAvailable, int calculatedActionCost)
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
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltipView?.ShowSkill(skill, actionCost, Input.mousePosition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltipView?.Hide();
        }
    }
}
