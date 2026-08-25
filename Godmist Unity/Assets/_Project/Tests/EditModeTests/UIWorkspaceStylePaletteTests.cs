using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class UIWorkspaceStylePaletteTests
    {
        [Test]
        public void DefaultPalette_ExposesDistinctSemanticAccentTokens()
        {
            var palette = ScriptableObject.CreateInstance<UIWorkspaceStylePalette>();

            Assert.That(
                palette.GetAccentColor(UIWorkspaceAccentStyle.Positive),
                Is.Not.EqualTo(palette.GetAccentColor(UIWorkspaceAccentStyle.Negative)));
            Assert.That(
                palette.GetAccentColor(UIWorkspaceAccentStyle.Selected),
                Is.Not.EqualTo(palette.GetAccentColor(UIWorkspaceAccentStyle.Hover)));

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void DefaultPalette_UsesReadableTypographyHierarchy()
        {
            var palette = ScriptableObject.CreateInstance<UIWorkspaceStylePalette>();

            Assert.That(
                palette.GetFontSize(UIWorkspaceTextStyle.Title),
                Is.GreaterThan(palette.GetFontSize(UIWorkspaceTextStyle.Heading)));
            Assert.That(
                palette.GetFontSize(UIWorkspaceTextStyle.Heading),
                Is.GreaterThan(palette.GetFontSize(UIWorkspaceTextStyle.Body)));
            Assert.That(
                palette.GetFontSize(UIWorkspaceTextStyle.Body),
                Is.GreaterThan(palette.GetFontSize(UIWorkspaceTextStyle.Detail)));

            Object.DestroyImmediate(palette);
        }
    }
}
