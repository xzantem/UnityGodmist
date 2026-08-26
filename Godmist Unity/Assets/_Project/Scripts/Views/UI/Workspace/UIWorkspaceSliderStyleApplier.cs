using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Applies the shared Workspace treatment to a reusable slider.
    /// Resource sliders intentionally use a neutral fallback here: combat code
    /// replaces their fill with the colour of the unit's ResourceType.
    /// </summary>
    [ExecuteAlways]
    public sealed class UIWorkspaceSliderStyleApplier : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceStylePalette palette;
        [SerializeField] private UIWorkspaceSliderStyle sliderStyle;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image fillImage;

        public UIWorkspaceStylePalette Palette => palette;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply UI Workspace slider style")]
        public void Apply()
        {
            if (palette == null)
            {
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = palette.GetSurfaceColor(UIWorkspaceSurfaceStyle.Raised);
            }

            if (fillImage != null)
            {
                fillImage.color = palette.GetAccentColor(GetFillAccentStyle(sliderStyle));
            }
        }

        private static UIWorkspaceAccentStyle GetFillAccentStyle(
            UIWorkspaceSliderStyle style)
        {
            return style switch
            {
                UIWorkspaceSliderStyle.Health => UIWorkspaceAccentStyle.Negative,
                UIWorkspaceSliderStyle.Shield => UIWorkspaceAccentStyle.Hover,
                UIWorkspaceSliderStyle.Resource => UIWorkspaceAccentStyle.Neutral,
                _ => UIWorkspaceAccentStyle.Neutral
            };
        }
    }

    public enum UIWorkspaceSliderStyle
    {
        Neutral,
        Health,
        Shield,
        Resource
    }
}
