using UnityEngine;
using UnityEngine.EventSystems;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatSimpleTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private UIWorkspaceTooltipView tooltip;
        private string title;
        private string description;

        public void Initialize(UIWorkspaceTooltipView sourceTooltip, string sourceTitle, string sourceDescription)
        {
            tooltip = sourceTooltip;
            title = sourceTitle;
            description = sourceDescription;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowText(title, description, this);
        }

        public void OnPointerExit(PointerEventData eventData) => tooltip?.NotifySourceExit();
    }
}
