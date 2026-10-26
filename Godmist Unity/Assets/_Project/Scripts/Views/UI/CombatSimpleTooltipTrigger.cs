using UnityEngine;
using UnityEngine.EventSystems;
using Godmist.Runtime.Combat;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatSimpleTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UIWorkspaceTooltipView tooltip;
        [SerializeField] private string title;
        [SerializeField] private string description;
        private BattleUnit richUnit;
        private bool showRichUnit;
        private bool pointerHovered;
        private bool suppressResourceHover;

        public void Initialize(UIWorkspaceTooltipView sourceTooltip, string sourceTitle, string sourceDescription)
        {
            suppressResourceHover = false;
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
            richUnit = null;
            showRichUnit = false;
        }

        public void InitializeResource(UIWorkspaceTooltipView sourceTooltip, ResourcePool pool)
        {
            // Resource bars currently show their value without an additional hover card.
            // Keep the authored trigger so this temporary UX choice needs no prefab migration.
            if (!suppressResourceHover) tooltip?.InvalidateSource(this);
            tooltip = sourceTooltip != null ? sourceTooltip : tooltip;
            suppressResourceHover = true;
            pointerHovered = false;
            showRichUnit = false;
            richUnit = null;
        }

        public void InitializeUnitTooltip(
            UIWorkspaceTooltipView sourceTooltip,
            BattleUnit unit,
            string sourceTitle,
            string sourceDescription,
            bool known = true)
        {
            Initialize(sourceTooltip, sourceTitle, sourceDescription);
            richUnit = unit;
            showRichUnit = known && unit?.Character != null;
        }

        public void UpdateDescription(string value)
        {
            if (description == value) return;
            description = value;
            tooltip?.InvalidateSource(this);
            if (pointerHovered) tooltip?.ShowText(title, description, this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (suppressResourceHover) return;
            pointerHovered = true;
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            if (showRichUnit) tooltip?.ShowUnit(richUnit, this, true);
            else tooltip?.ShowText(title, description, this);
        }

        private void OnDisable()
        {
            pointerHovered = false;
            tooltip?.InvalidateSource(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerHovered = false;
            tooltip?.NotifySourceExit(this);
        }
    }
}
