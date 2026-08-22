using TMPro;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Shared visual tokens for every UI Workspace window and its reusable UGUI templates.
    /// The asset is presentation-only: it must never carry gameplay or window-state logic.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIWorkspaceStylePalette",
        menuName = "Godmist/UI Workspace/Style Palette")]
    public sealed class UIWorkspaceStylePalette : ScriptableObject
    {
        [Header("Typography")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(8f)] private float titleFontSize = 24f;
        [SerializeField, Min(8f)] private float headingFontSize = 18f;
        [SerializeField, Min(8f)] private float bodyFontSize = 15f;
        [SerializeField, Min(8f)] private float detailFontSize = 13f;
        [SerializeField, Min(8f)] private float captionFontSize = 11f;

        [Header("Surfaces")]
        [SerializeField] private Color windowColor = new(0.08f, 0.12f, 0.18f, 0.98f);
        [SerializeField] private Color headerColor = new(0.12f, 0.19f, 0.29f, 1f);
        [SerializeField] private Color sectionColor = new(0.10f, 0.15f, 0.21f, 0.96f);
        [SerializeField] private Color raisedColor = new(0.14f, 0.20f, 0.27f, 1f);
        [SerializeField] private Color tooltipColor = new(0.04f, 0.06f, 0.09f, 0.98f);
        [SerializeField] private Color scrimColor = new(0f, 0f, 0f, 0.55f);

        [Header("Text")]
        [SerializeField] private Color primaryTextColor = new(0.93f, 0.95f, 0.98f, 1f);
        [SerializeField] private Color secondaryTextColor = new(0.69f, 0.75f, 0.82f, 1f);
        [SerializeField] private Color disabledTextColor = new(0.42f, 0.47f, 0.53f, 1f);

        [Header("Accents and feedback")]
        [SerializeField] private Color neutralBorderColor = new(0.25f, 0.32f, 0.40f, 1f);
        [SerializeField] private Color hoverColor = new(0.49f, 0.65f, 0.78f, 1f);
        [SerializeField] private Color selectedColor = new(0.82f, 0.62f, 0.22f, 1f);
        [SerializeField] private Color positiveColor = new(0.24f, 0.62f, 0.39f, 1f);
        [SerializeField] private Color negativeColor = new(0.78f, 0.29f, 0.27f, 1f);
        [SerializeField] private Color warningColor = new(0.91f, 0.64f, 0.20f, 1f);

        [Header("Layout")]
        [SerializeField, Min(0f)] private float spacingUnit = 4f;
        [SerializeField, Min(0f)] private float windowPadding = 16f;
        [SerializeField, Min(0f)] private float sectionPadding = 12f;
        [SerializeField, Min(0f)] private float controlHeight = 36f;
        [SerializeField, Min(0f)] private float iconSize = 24f;
        [SerializeField, Min(0f)] private float slotIconSize = 48f;
        [SerializeField, Min(0f)] private float borderWidth = 2f;

        public TMP_FontAsset Font => font;
        public float SpacingUnit => spacingUnit;
        public float WindowPadding => windowPadding;
        public float SectionPadding => sectionPadding;
        public float ControlHeight => controlHeight;
        public float IconSize => iconSize;
        public float SlotIconSize => slotIconSize;
        public float BorderWidth => borderWidth;

        public Color GetSurfaceColor(UIWorkspaceSurfaceStyle style)
        {
            return style switch
            {
                UIWorkspaceSurfaceStyle.Window => windowColor,
                UIWorkspaceSurfaceStyle.Header => headerColor,
                UIWorkspaceSurfaceStyle.Section => sectionColor,
                UIWorkspaceSurfaceStyle.Raised => raisedColor,
                UIWorkspaceSurfaceStyle.Tooltip => tooltipColor,
                UIWorkspaceSurfaceStyle.Scrim => scrimColor,
                _ => Color.white
            };
        }

        public Color GetTextColor(UIWorkspaceTextStyle style)
        {
            return style == UIWorkspaceTextStyle.Disabled
                ? disabledTextColor
                : style == UIWorkspaceTextStyle.Detail || style == UIWorkspaceTextStyle.Caption
                    ? secondaryTextColor
                    : primaryTextColor;
        }

        public float GetFontSize(UIWorkspaceTextStyle style)
        {
            return style switch
            {
                UIWorkspaceTextStyle.Title => titleFontSize,
                UIWorkspaceTextStyle.Heading => headingFontSize,
                UIWorkspaceTextStyle.Body => bodyFontSize,
                UIWorkspaceTextStyle.Detail => detailFontSize,
                UIWorkspaceTextStyle.Caption => captionFontSize,
                _ => bodyFontSize
            };
        }

        public Color GetAccentColor(UIWorkspaceAccentStyle style)
        {
            return style switch
            {
                UIWorkspaceAccentStyle.Neutral => neutralBorderColor,
                UIWorkspaceAccentStyle.Hover => hoverColor,
                UIWorkspaceAccentStyle.Selected => selectedColor,
                UIWorkspaceAccentStyle.Positive => positiveColor,
                UIWorkspaceAccentStyle.Negative => negativeColor,
                UIWorkspaceAccentStyle.Warning => warningColor,
                _ => Color.clear
            };
        }
    }

    public enum UIWorkspaceSurfaceStyle
    {
        None,
        Window,
        Header,
        Section,
        Raised,
        Tooltip,
        Scrim
    }

    public enum UIWorkspaceTextStyle
    {
        Title,
        Heading,
        Body,
        Detail,
        Caption,
        Disabled
    }

    public enum UIWorkspaceAccentStyle
    {
        None,
        Neutral,
        Hover,
        Selected,
        Positive,
        Negative,
        Warning
    }
}
