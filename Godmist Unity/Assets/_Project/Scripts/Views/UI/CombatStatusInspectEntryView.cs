using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public sealed class CombatStatusInspectEntryView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMPro.TextMeshProUGUI label;
        [SerializeField] private RectTransform labelRoot;
        [SerializeField] private float iconLabelInset = 56f;
        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        public void Initialize(
            CombatStatusPresentation sourcePresentation,
            UIWorkspaceTooltipView sourceTooltip)
        {
            presentation = sourcePresentation;
            var effect = presentation.Effect;
            if (icon != null) { icon.sprite = sourceTooltip?.GetStatusIcon(presentation); icon.enabled = icon.sprite != null; }
            if (labelRoot != null)
            {
                var inset = labelRoot.offsetMin;
                inset.x = icon != null && icon.enabled ? iconLabelInset : 0f;
                labelRoot.offsetMin = inset;
            }
            if (label != null) label.text = !string.IsNullOrWhiteSpace(effect.DisplayName) ? effect.DisplayName
                : sourceTooltip?.GetGlossaryTitle(effect.EffectId) ?? effect.EffectId;
            tooltip = sourceTooltip;
        }

        private void OnDisable() => tooltip?.NotifySourceExit(this);

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
