using System.IO;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class CharacterStatBreakdownTests
    {
        [Test, Explicit("Actual Mage asset and tooltip prefab; transient fractional HP modifier and full healing.")]
        public void ExportMaximumHealthRoundingBreakdown()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            var character = Character("Mage");
            character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId = "rounding.preview", DisplayName = "Premia wyposażenia",
                Lifetime = PassiveEffectLifetime.Persistent, StatModifiers = new() {
                    new Godmist.Core.Types.StatModifier { StatType = CharacterStatType.MaxHealth,
                        Operation = StatModifierOperation.FlatAbsolute, Value = .5f } } });
            new CharacterStatPipelineSystem().Refresh(character);
            var rawMaximum = character.Stats.MaxHealth.GetValue();
            Assert.That(rawMaximum % 1f, Is.EqualTo(.5f));
            var unit = new BattleUnit { Character = character };
            new CombatRecoverySystem().Heal(unit, 100000);
            Assert.That(character.CurrentHealth, Is.EqualTo(character.MaximumHealth));
            var body = tooltip.BuildCharacterStatDetails(character, CharacterStatType.MaxHealth);
            Assert.That(Plain(body), Does.Contain("Wynik po zaokrągleniu"));
            var meta = "Zdrowie: " + Godmist.Views.UI.CombatHealthPresentation.Format(character);
            var folder = Path.GetFullPath("../LocalHandoffs/maximum-health-rounding-20261029"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "mage-breakdown.txt"), Plain(body) + "\n" + meta);
            var canvasRoot = new GameObject("HP rounding proof", typeof(RectTransform), typeof(Canvas));
            var cameraRoot = new GameObject("HP rounding camera", typeof(Camera));
            var render = new RenderTexture(740, 320, 24); var previousRender = RenderTexture.active;
            try
            {
                var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10); camera.targetTexture = render;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .075f, .09f, 1); camera.cullingMask = 1 << 31;
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 32760;
                tooltipRoot.transform.SetParent(canvasRoot.transform, false);
                foreach (var transform in tooltipRoot.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                typeof(UIWorkspaceTooltipView).GetMethod("SetContent", Hidden).Invoke(tooltip, new object[] { null, UiTerms.Stat(CharacterStatType.MaxHealth), body, meta, false });
                var rect = Field<RectTransform>(tooltip, "tooltipRect");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -25);
                Canvas.ForceUpdateCanvases(); typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent", Hidden).Invoke(tooltip, null); Canvas.ForceUpdateCanvases();
                rect.anchoredPosition = new Vector2(0, -25);
                camera.Render(); RenderTexture.active = render;
                var pixels = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
                try { pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(folder, "mage-health-rounding.png"), pixels.EncodeToPNG()); }
                finally { Object.DestroyImmediate(pixels); }
            }
            finally
            {
                tooltipRoot.transform.SetParent(null, false); cameraRoot.GetComponent<Camera>().targetTexture = null;
                RenderTexture.active = previousRender; Object.DestroyImmediate(render); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(canvasRoot);
            }
        }
    }
}
