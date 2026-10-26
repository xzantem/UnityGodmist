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

        /// <summary>
        /// Allows runtime-composed panels to reuse the authored slider prefab
        /// with the same palette treatment as serialized workspace sliders.
        /// </summary>
        public void SetStyle(UIWorkspaceSliderStyle style)
        {
            sliderStyle = style;
            Apply();
        }

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
                backgroundImage.color = palette.IsArtwork(backgroundImage.sprite)
                    ? Color.white : palette.GetSurfaceColor(UIWorkspaceSurfaceStyle.Raised);
            }

            if (fillImage != null)
            {
                fillImage.color = palette.IsArtwork(fillImage.sprite) && fillImage.sprite.name == "fill_gold"
                    ? Color.white : palette.GetAccentColor(GetFillAccentStyle(sliderStyle));
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
