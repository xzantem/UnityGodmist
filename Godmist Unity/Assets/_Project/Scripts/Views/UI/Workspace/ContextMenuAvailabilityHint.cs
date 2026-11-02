using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    // Disabled buttons remain disabled; only their explanation can receive focus.
    public sealed class ContextMenuAvailabilityHint : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private string title, reason;
        private UIWorkspaceTooltipView tooltip;
        private bool hovered, selected;
        public bool CanInspect => isActiveAndEnabled && !string.IsNullOrWhiteSpace(reason);

        public void Bind(string nextTitle, string nextReason)
        {
            tooltip?.InvalidateSource(this);
            hovered = selected = false;
            title = nextTitle;
            reason = nextReason;
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; Show(data.position); }
        public void OnPointerExit(PointerEventData data)
        {
            hovered = false;
            if (!selected || !ControllerInput.UiActive) tooltip?.NotifySourceExit(this);
        }
        public void OnSelect(BaseEventData data)
        {
            selected = true;
            if (ControllerInput.UiActive && transform is RectTransform rect)
            {
                var canvas = GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Show(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)));
            }
        }
        public void OnDeselect(BaseEventData data)
        {
            selected = false;
            if (!hovered || ControllerInput.UiActive) tooltip?.NotifySourceExit(this);
        }
        private void Show(Vector2 position)
        {
            if (!CanInspect) return;
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowCompactText(title, reason, this, position);
        }
        private void OnDisable()
        {
            hovered = selected = false;
            tooltip?.InvalidateSource(this);
        }
    }
}
