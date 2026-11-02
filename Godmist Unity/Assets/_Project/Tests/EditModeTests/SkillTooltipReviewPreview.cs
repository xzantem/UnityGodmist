using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    /// <summary>Explicit read-only contact sheet export using the shipped tooltip prefab.</summary>
    public sealed class SkillTooltipReviewPreview
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> owned = new();
        readonly List<Card> cards = new();
        List<SkillData> skills;
        CharacterClassData previewClass;
        [Serializable] sealed class Card
        {
            public string group, scenario, file, title, body, meta, kind;
            [NonSerialized] public CombatStatusPresentation presentation;
        }
        [Serializable] sealed class Gallery { public Card[] cards; }
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        static object Get(object value, string field) => value.GetType().GetField(field, Hidden).GetValue(value);
        static void Invoke(object value, string method, params object[] args) => value.GetType().GetMethod(method, Hidden).Invoke(value, args);
        [Test, Explicit("Exports base skill tooltip with the actual shipped tooltip prefab.")]
        public void Export()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            var output = Path.GetFullPath("../LocalHandoffs/skill-tooltip-review-20261027"); Directory.CreateDirectory(output);
            var oldActive = UIWorkspaceTooltipView.ActiveInstance;
            var oldRender = RenderTexture.active;
            Camera renderCamera = null;
            try
            {
                var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Paladin_Crusader.asset");
                var manager = new Godmist.Systems.SaveLoad.SaveManager();
                var catalog = AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                Assert.That(manager.TryGetSaveSummaries(out var summaries, out var loadMessage), Is.True, loadMessage);
                CharacterInstance character = null; string savePath = null;
                foreach (var summary in summaries)
                {
                    if (!manager.TryLoadSave(summary, catalog, out var session, out _)) continue;
                    character = session.CharactersById.Values.FirstOrDefault(c => c.ClassData?.Id == "class.paladin");
                    if (character == null) continue;
                    savePath = summary.FilePath; break;
                }
                Assert.That(character, Is.Not.Null, "No saved Paladin found.");
                var ranks = Godmist.Systems.Characters.SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id);
                File.WriteAllText(Path.Combine(output, "189-character.txt"), $"{character.Definition.DisplayName}, poziom {character.Level}\nSave: {savePath}\nAttack: {character.Stats.MinAttack.GetValue()}–{character.Stats.MaxAttack.GetValue()}\n" + JsonUtility.ToJson(character.SkillTreeProgress));
                cards.Add(new Card { scenario = $"{character.Definition.DisplayName}, poziom {character.Level}; wyposażenie i talenty z zapisu" });
                var canvasRoot = Own(new GameObject("Hero status preview canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
                canvasRoot.hideFlags = HideFlags.HideAndDontSave; var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                var cameraRoot = Own(new GameObject("Hero status preview camera", typeof(Camera))); cameraRoot.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraRoot.GetComponent<Camera>(); renderCamera = camera; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .075f, .09f, 1); camera.cullingMask = 1 << 31;
                canvas.worldCamera = camera; canvas.planeDistance = 1; canvasRoot.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                var render = Own(new RenderTexture(512, 1200, 24)); camera.targetTexture = render;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillTreeWindow Variant.prefab"); Assert.That(prefab, Is.Not.Null);
                var index = 0;
                foreach (var card in cards)
                {
                    var root = Object.Instantiate(prefab, canvasRoot.transform); root.SetActive(true);
                    try
                    {
                        var view = root.GetComponentInChildren<SkillTreeProgressionPanelView>(true); Assert.That(view, Is.Not.Null);
                        view.GetType().GetField("character", Hidden).SetValue(view, character);
                        view.GetType().GetField("selectedTree", Hidden).SetValue(view, tree);
                        Invoke(view, "RefreshSelectedTreeHeader");
                        var label = (TMP_Text)Get(view, "globalPassiveText");
                        var rect = (RectTransform)Get(view, "globalPassivePanel");
                        rect.SetParent(canvasRoot.transform, false); rect.gameObject.SetActive(true);
                        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
                        rect.sizeDelta = new Vector2(480, 200); rect.anchoredPosition = new Vector2(0, -20);
                        foreach (var transform in canvasRoot.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                        Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(80, label.GetPreferredValues(label.text, 460, Mathf.Infinity).y + 24));
                        Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
                        card.title = tree.DisplayName; card.body = label.text; card.meta = "Wydane punkty: " + ranks; card.kind = "SkillTreeGlobalPassive";
                        camera.Render(); RenderTexture.active = render;
                        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                        var low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                        var x = Mathf.Max(0, Mathf.FloorToInt(low.x) - 8); var y = Mathf.Max(0, Mathf.FloorToInt(low.y) - 8);
                        var width = Mathf.Min(512 - x, Mathf.CeilToInt(high.x - low.x) + 16); var height = Mathf.Min(1200 - y, Mathf.CeilToInt(high.y - low.y) + 16);
                        var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        try { pixels.ReadPixels(new Rect(x, y, width, height), 0, 0); pixels.Apply(); card.file = "189.png"; File.WriteAllBytes(Path.Combine(output, card.file), pixels.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(pixels); }
                        Object.DestroyImmediate(rect.gameObject);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                File.WriteAllText(Path.Combine(output, "189-gallery.json"), JsonUtility.ToJson(new Gallery { cards = cards.ToArray() }, true));
                Assert.That(cards.Count, Is.EqualTo(1)); Debug.Log("Exported " + cards.Count + " native hero status tooltip previews: " + output);
            }
            finally
            {
                RenderTexture.active = oldRender;
                if (renderCamera != null) renderCamera.targetTexture = null;
                foreach (var item in owned.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
                typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null, oldActive);
            }
        }
    }
}
