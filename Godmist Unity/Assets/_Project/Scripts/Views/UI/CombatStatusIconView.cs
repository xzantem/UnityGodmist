using Godmist.Runtime.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatStatusIconView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI stackText;
        [SerializeField] private TextMeshProUGUI durationText;

        [SerializeField] private bool showCounters = true;
        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        public void Bind(
            CombatStatusPresentation source,
            UIWorkspaceTooltipView sourceTooltip,
            Sprite sourceIcon)
        {
            presentation = source;
            tooltip = sourceTooltip;
            var effect = source.Effect;

            if (icon != null)
            {
                icon.sprite = sourceIcon;
                icon.enabled = icon.sprite != null;
            }

            SetCounter(stackText, showCounters ? effect.Stacks : 0);
            SetCounter(durationText, showCounters ? effect.RemainingDuration : 0);
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

        private void OnDisable() => tooltip?.NotifySourceExit(this);

        private static void SetCounter(TextMeshProUGUI label, int value)
        {
            if (label == null) return;
            label.gameObject.SetActive(value > 1);
            if (value > 1) label.text = value.ToString();
        }
    }
}
