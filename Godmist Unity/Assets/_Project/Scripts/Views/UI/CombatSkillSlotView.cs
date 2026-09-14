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

        [SerializeField] private TextMeshProUGUI actionCostText;
        [SerializeField] private Image selectedFrame;
        private string unavailableReason;
        [SerializeField] private TextMeshProUGUI shortcutText;
        private Action<bool> hoverChanged;
        public void SetShortcut(int index) { if(shortcutText!=null)shortcutText.text=index>=0 && index<10?((index+1)%10).ToString():""; }
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
            Func<BattleUnit> sourceCasterProvider = null,
            Action<bool> onHoverChanged = null)
        {
            hoverChanged = onHoverChanged;
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
            string knifeCount,
            bool selected = false,
            string reason = null)
        {
            actionCost = calculatedActionCost;
            unavailableReason = reason;
            if (actionCostText != null) actionCostText.text = $"{actionCost} AP";
            if (selectedFrame != null) selectedFrame.enabled = selected;
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverChanged?.Invoke(true);
            tooltipView?.ShowSkill(skill, actionCost, this, casterProvider?.Invoke(), unavailableReason);
        }

        private void OnDisable(){hoverChanged?.Invoke(false);tooltipView?.NotifySourceExit(this);}

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverChanged?.Invoke(false);
            tooltipView?.NotifySourceExit(this);
        }
    }
}
