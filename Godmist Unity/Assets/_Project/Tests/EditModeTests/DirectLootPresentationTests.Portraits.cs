using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class DirectLootPresentationTests
    {
        [Test]
        public void ActiveEnemyUsesSelectionFrameAndNativePortraitRender()
        {
            var root = new GameObject("Native turn portrait comparison");
            var target = new RenderTexture(400, 200, 24);
            var pixels = new Texture2D(400, 200, TextureFormat.RGBA32, false);
            var old = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Portrait render camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.065f, .06f, .055f); camera.targetTexture = target; camera.cullingMask = 1 << 30;
                var canvasObject = new GameObject("Native HUD", typeof(Canvas)); canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var definition = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { "Assets/_Project/ScriptableObjects/Catacombs/Enemies" })
                    .Select(id => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(id)))
                    .First(d => d != null && d.UiPortraitSprite != null && d.name.ToLowerInvariant().Contains("defender"));
                var unit = new BattleUnit { Character = new CharacterInstanceFactory().Create(definition), IsPlayerSide = false };
                var prefab = AssetDatabase.LoadAssetAtPath<CombatTurnPortraitView>("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab");
                var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
                for (var i = 0; i < 2; i++)
                {
                    var view = Object.Instantiate(prefab, canvas.transform);
                    var rect = (RectTransform)view.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(i == 0 ? -100 : 100, 0); rect.localScale = Vector3.one * 2;
                    view.Bind(unit, true);
                    var frame = Field<Image>(view, "activeHighlight");
                    Assert.That(frame.sprite, Is.SameAs(Field<Sprite>(view, "playerHighlightSprite")));
                    Assert.That(frame.color, Is.EqualTo(Field<Color>(view, "enemyActiveColor")));
                    if (i == 0) { frame.sprite = palette.GetArtwork("slot_invalid"); frame.color = Color.white; }
                    view.Bind(unit, false);
                    Assert.That(frame.enabled, Is.False);
                    view.Bind(unit, true);
                    if (i == 0) { frame.sprite = palette.GetArtwork("slot_invalid"); frame.color = Color.white; }
                    Field<Image>(view, "portrait").enabled = true;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 400, 200), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/portrait-before-after.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; Object.DestroyImmediate(root); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
