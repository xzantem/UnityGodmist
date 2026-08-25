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

        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        private void Awake()
        {
            // Status templates are authored in several HUD contexts. Give the root a
            // transparent raycast surface so pointer events work even when the visible
            // sprite is a child with Raycast Target disabled.
            var graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var raycastSurface = gameObject.AddComponent<Image>();
                raycastSurface.color = Color.clear;
                graphic = raycastSurface;
            }

            graphic.raycastTarget = true;
        }

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

            SetCounter(stackText, effect.Stacks);
            SetCounter(durationText, effect.RemainingDuration);
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

        private static void SetCounter(TextMeshProUGUI label, int value)
        {
            if (label == null) return;
            label.gameObject.SetActive(value > 1);
            if (value > 1) label.text = value.ToString();
        }
    }
}
