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
        private Image tickShade;
        private bool hasExplorationTick;

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

            SetCounter(stackText, showCounters ? source.StackCount : 0);
            SetCounter(durationText, showCounters ? source.Duration : 0);
            hasExplorationTick = false;
            if (source.ExplorationCharacter != null)
                foreach (var active in source.Effects)
                    if (active?.Lifetime == Godmist.Core.Enums.PassiveEffectLifetime.Timed && active.RemainingDuration > 0)
                    { hasExplorationTick = true; break; }
            RefreshTickShade();
        }

        private void Update() => RefreshTickShade();

        private void RefreshTickShade()
        {
            var show = hasExplorationTick && icon != null && icon.enabled && icon.sprite != null;
            if (show && tickShade == null)
            {
                var overlay = new GameObject("StatusTickShade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.layer = icon.gameObject.layer;
                overlay.transform.SetParent(icon.transform, false);
                overlay.transform.SetAsFirstSibling();
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                tickShade = overlay.GetComponent<Image>();
                tickShade.color = new Color(0f, 0f, 0f, .55f);
                tickShade.raycastTarget = false;
                tickShade.type = Image.Type.Filled;
                tickShade.fillMethod = Image.FillMethod.Radial360;
                tickShade.fillOrigin = (int)Image.Origin360.Top;
                // A shrinking counterclockwise filled region reveals clockwise from twelve o'clock.
                tickShade.fillClockwise = false;
            }
            if (tickShade == null) return;
            if (tickShade.gameObject.activeSelf != show) tickShade.gameObject.SetActive(show);
            if (!show) return;
            if (tickShade.sprite != icon.sprite) tickShade.sprite = icon.sprite;
            tickShade.preserveAspect = icon.preserveAspect;
            tickShade.fillAmount = 1f - Mathf.Clamp01(presentation.ExplorationCharacter.ExplorationStatusElapsedSeconds
                / Godmist.Systems.Combat.ExplorationStatusEffectSystem.ExplorationTickSeconds);
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
