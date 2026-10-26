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
        [SerializeField] private UIWorkspaceAccentStyle textAccentStyle = UIWorkspaceAccentStyle.None;
        [SerializeField] private UIWorkspaceAccentStyle interactionStyle = UIWorkspaceAccentStyle.Hover;

        [Header("Targets")]
        [SerializeField] private Image surfaceImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Selectable selectable;

        public UIWorkspaceStylePalette Palette => palette;

        public void ConfigureRuntime(
            UIWorkspaceStylePalette nextPalette,
            UIWorkspaceSurfaceStyle nextSurfaceStyle,
            UIWorkspaceAccentStyle nextBorderStyle,
            UIWorkspaceTextStyle nextTextStyle,
            UIWorkspaceAccentStyle nextTextAccentStyle,
            Image nextSurfaceImage = null,
            Image nextBorderImage = null,
            TMP_Text nextText = null,
            Selectable nextSelectable = null)
        {
            palette = nextPalette;
            surfaceStyle = nextSurfaceStyle;
            borderStyle = nextBorderStyle;
            textStyle = nextTextStyle;
            textAccentStyle = nextTextAccentStyle;
            surfaceImage = nextSurfaceImage;
            borderImage = nextBorderImage;
            text = nextText;
            selectable = nextSelectable;
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

        [ContextMenu("Apply UI Workspace style")]
        public void Apply()
        {
            if (palette == null)
            {
                return;
            }

            if (surfaceImage != null && surfaceStyle != UIWorkspaceSurfaceStyle.None)
            {
                surfaceImage.color = palette.IsArtwork(surfaceImage.sprite)
                    ? new Color(1, 1, 1, surfaceStyle == UIWorkspaceSurfaceStyle.Scrim ? palette.GetSurfaceColor(surfaceStyle).a : 1)
                    : palette.GetSurfaceColor(surfaceStyle);
            }

            if (borderImage != null && borderStyle != UIWorkspaceAccentStyle.None)
            {
                borderImage.color = palette.IsArtwork(borderImage.sprite)
                    ? Color.white : palette.GetAccentColor(borderStyle);
            }

            if (text != null)
            {
                if (palette.Font != null)
                {
                    text.font = palette.Font;
                }

                text.fontSize = palette.GetFontSize(textStyle);
                text.color = textAccentStyle != UIWorkspaceAccentStyle.None
                    ? palette.GetAccentColor(textAccentStyle)
                    : palette.GetTextColor(textStyle);
            }

            if (selectable != null && interactionStyle != UIWorkspaceAccentStyle.None)
            {
                var colors = selectable.colors;
                var artworkTarget = selectable.targetGraphic is Image image && palette.IsArtwork(image.sprite);
                if (artworkTarget) selectable.targetGraphic.CrossFadeColor(Color.white, 0, true, true);
                var normalColor = artworkTarget || surfaceStyle == UIWorkspaceSurfaceStyle.None
                    ? Color.white
                    : palette.GetSurfaceColor(surfaceStyle);
                var interactionColor = artworkTarget ? Color.white : palette.GetAccentColor(interactionStyle);
                colors.normalColor = normalColor;
                colors.highlightedColor = interactionColor;
                colors.pressedColor = artworkTarget ? Color.white : Color.Lerp(interactionColor, Color.black, 0.2f);
                colors.selectedColor = interactionColor;
                colors.disabledColor = artworkTarget ? Color.white : palette.GetTextColor(UIWorkspaceTextStyle.Disabled);
                colors.colorMultiplier = 1f;
                selectable.colors = colors;
            }
        }
    }
}
