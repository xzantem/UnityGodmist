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
        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        public void Initialize(
            CombatStatusPresentation sourcePresentation,
            UIWorkspaceTooltipView sourceTooltip)
        {
            presentation = sourcePresentation;
            var effect = presentation.Effect;
            if (icon != null) { icon.sprite = sourceTooltip?.GetGlossaryIcon(effect.EffectId); icon.enabled = icon.sprite != null; }
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
