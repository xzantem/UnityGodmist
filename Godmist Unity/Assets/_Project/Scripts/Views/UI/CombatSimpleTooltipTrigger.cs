using UnityEngine;
using UnityEngine.EventSystems;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatSimpleTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UIWorkspaceTooltipView tooltip;
        [SerializeField] private string title;
        [SerializeField] private string description;

        public void Initialize(UIWorkspaceTooltipView sourceTooltip, string sourceTitle, string sourceDescription)
        {
            // Null means keep the resolved view (timeline binds every frame).
            // Clearing it would prevent pointer exit from notifying that view.
            var nextTooltip = sourceTooltip != null ? sourceTooltip : tooltip;
            if (tooltip != nextTooltip || title != sourceTitle || description != sourceDescription)
            {
                tooltip?.InvalidateSource(this);
            }
            tooltip = nextTooltip;
            title = sourceTitle;
            description = sourceDescription;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowText(title, description, this);
        }

        private void OnDisable() => tooltip?.InvalidateSource(this);

        public void OnPointerExit(PointerEventData eventData) => tooltip?.NotifySourceExit(this);
    }
}
