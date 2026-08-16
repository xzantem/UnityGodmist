using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public class CombatSimpleTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CombatTooltipView tooltip;
        private string title;
        private string description;

        public void Initialize(CombatTooltipView sourceTooltip, string sourceTitle, string sourceDescription)
        {
            tooltip = sourceTooltip;
            title = sourceTitle;
            description = sourceDescription;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= CombatTooltipView.ActiveInstance
                ?? Object.FindAnyObjectByType<CombatTooltipView>(FindObjectsInactive.Include);
            tooltip?.ShowText(title, description, this);
        }

        public void OnPointerExit(PointerEventData eventData) => tooltip?.NotifySourceExit();
    }
}
