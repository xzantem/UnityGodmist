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
        [Test, Explicit("Actual Mage asset and game tooltip prefab; transient status/talent setup only.")]
        public void ExportChilledMageSpeedBreakdown()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            var character = Character("Mage");
            var progress = character.SkillTreeProgress.FirstOrDefault(p => p.TreeId == MageCombatSystem.Fire);
            if (progress == null) { progress = new SkillTreeProgress { TreeId = MageCombatSystem.Fire }; character.SkillTreeProgress.Add(progress); }
            progress.NodeInvestments.RemoveAll(n => n.NodeId == "node.mage.fire.adrenaline");
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.mage.fire.adrenaline", Ranks = 1 });
            var unit = new BattleUnit { Character = character };
            var system = new StatusEffectSystem();
            Assert.That(system.ApplyStatus(unit, unit, StatusEffectIds.Burn, 1, 3, 1, 0, 0, "preview", sharedRoll: 0f), Is.True);
            Assert.That(system.ApplyStatus(unit, unit, StatusEffectIds.Chill, 1, 3, 1, 0, 0, "preview", sharedRoll: 0f), Is.True);
            Assert.That(character.Stats.Speed.GetValue(), Is.EqualTo(20));
            var body = tooltip.BuildCharacterStatDetails(character, CharacterStatType.Speed, "20");
            var folder = Path.GetFullPath("../LocalHandoffs/root-repairs-20261029"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "mage-breakdown.txt"), Plain(body));
            Assert.That(Plain(body).IndexOf(UiTerms.Status(StatusEffectIds.Chill)), Is.GreaterThan(Plain(body).IndexOf("Adrenalina")));
            var canvasRoot = new GameObject("Chill speed proof", typeof(RectTransform), typeof(Canvas));
            var cameraRoot = new GameObject("Chill speed camera", typeof(Camera));
            var render = new RenderTexture(700, 450, 24); var previousRender = RenderTexture.active;
            try
            {
                var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10); camera.targetTexture = render;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .075f, .09f, 1); camera.cullingMask = 1 << 31;
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 32760;
                tooltipRoot.transform.SetParent(canvasRoot.transform, false);
                foreach (var transform in tooltipRoot.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                typeof(UIWorkspaceTooltipView).GetMethod("SetContent", Hidden).Invoke(tooltip, new object[] { null, "Szybkość", body, "", false });
                var rect = Field<RectTransform>(tooltip, "tooltipRect");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -25);
                Canvas.ForceUpdateCanvases(); typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent", Hidden).Invoke(tooltip, null); Canvas.ForceUpdateCanvases();
                rect.anchoredPosition = new Vector2(0, -25);
                camera.Render(); RenderTexture.active = render;
                var pixels = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
                try { pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(folder, "mage-speed-chill.png"), pixels.EncodeToPNG()); }
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
