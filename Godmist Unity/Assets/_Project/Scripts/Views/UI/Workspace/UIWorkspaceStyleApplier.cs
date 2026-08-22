using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Applies a single palette role to one reusable UGUI template element.
    /// Compose template hierarchies from several instances instead of duplicating colors locally.
    /// </summary>
    [ExecuteAlways]
    public sealed class UIWorkspaceStyleApplier : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceStylePalette palette;
        [SerializeField] private UIWorkspaceSurfaceStyle surfaceStyle;
        [SerializeField] private UIWorkspaceAccentStyle borderStyle = UIWorkspaceAccentStyle.Neutral;
        [SerializeField] private UIWorkspaceTextStyle textStyle = UIWorkspaceTextStyle.Body;
        [SerializeField] private UIWorkspaceAccentStyle interactionStyle = UIWorkspaceAccentStyle.Hover;

        [Header("Targets")]
        [SerializeField] private Image surfaceImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Selectable selectable;

        public UIWorkspaceStylePalette Palette => palette;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply UI Workspace style")]
        public void Apply()
        {
            if (palette == null)
            {
                return;
            }

            if (surfaceImage != null && surfaceStyle != UIWorkspaceSurfaceStyle.None)
            {
                surfaceImage.color = palette.GetSurfaceColor(surfaceStyle);
            }

            if (borderImage != null && borderStyle != UIWorkspaceAccentStyle.None)
            {
                borderImage.color = palette.GetAccentColor(borderStyle);
            }

            if (text != null)
            {
                if (palette.Font != null)
                {
                    text.font = palette.Font;
                }

                text.fontSize = palette.GetFontSize(textStyle);
                text.color = palette.GetTextColor(textStyle);
            }

            if (selectable != null && interactionStyle != UIWorkspaceAccentStyle.None)
            {
                var colors = selectable.colors;
                var normalColor = surfaceStyle == UIWorkspaceSurfaceStyle.None
                    ? Color.white
                    : palette.GetSurfaceColor(surfaceStyle);
                var interactionColor = palette.GetAccentColor(interactionStyle);
                colors.normalColor = normalColor;
                colors.highlightedColor = interactionColor;
                colors.pressedColor = Color.Lerp(interactionColor, Color.black, 0.2f);
                colors.selectedColor = interactionColor;
                colors.disabledColor = palette.GetTextColor(UIWorkspaceTextStyle.Disabled);
                colors.colorMultiplier = 1f;
                selectable.colors = colors;
            }
        }
    }
}
