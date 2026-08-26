using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public sealed class CombatStatusInspectEntryView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        public void Initialize(
            CombatStatusPresentation sourcePresentation,
            UIWorkspaceTooltipView sourceTooltip)
        {
            presentation = sourcePresentation;
            tooltip = sourceTooltip;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowStatus(presentation, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip?.NotifySourceExit(this);
        }
    }
}
