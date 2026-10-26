using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
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
        public void RenderNativeChestWindowBeforeAndAfterIndependentRolls()
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTableData>("Assets/_Project/ScriptableObjects/Dungeons/Forest/Loot/LT_Forest_Chest_1_10_Production.asset");
            var legacyMethod = typeof(LootTableSystem).GetMethod("RollLoot", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ItemInstance> before = null, after = null; var selectedSeed = 0;
            bool Equipment(ItemInstance item) => item.Definition.ItemType is ItemType.Armor or ItemType.Weapon;
            for (var seed = 1; seed <= 500; seed++)
            {
                var first = new System.Random(seed); var second = new System.Random(seed);
                var legacy = (List<ItemInstance>)legacyMethod.Invoke(new LootTableSystem(() => (float)first.NextDouble()),
                    new object[] { table, 5f / (3 * .04f * 3.55f * 2), false });
                before = legacy.SelectMany(i => i is LootBagItemInstance bag ? (IEnumerable<ItemInstance>)bag.Contents : new[] { i }).ToList();
                after = new LootTableSystem(() => (float)second.NextDouble()).RollChestLoot(table, 3);
                if (before.Count(Equipment) < 4 || after.Count(Equipment) != 1) continue;
                selectedSeed = seed; break;
            }
            Assert.That(selectedSeed, Is.GreaterThan(0));
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/loot-window-sample.txt", $"Illustrative first chest, three reward chests per expedition; seed={selectedSeed}; before equipment={before.Count(Equipment)}; after equipment={after.Count(Equipment)}. All resource items included. Not a guarantee for each chest.");
            var root = new GameObject("Native loot proof");
            var texture = new RenderTexture(900, 640, 24);
            var image = new Texture2D(900, 640, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Loot camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 320;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .065f, .05f); camera.targetTexture = texture; camera.cullingMask = 1 << 30;
                var canvasObject = new GameObject("Native loot HUD", typeof(Canvas)); canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var prefab = AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab");
                var view = Object.Instantiate(prefab, canvas.transform);
                var rect = (RectTransform)view.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                var refresh = typeof(DungeonLootWindowView).GetMethod("RefreshItems", BindingFlags.NonPublic | BindingFlags.Instance);
                for (var phase = 0; phase < 2; phase++)
                {
                    refresh.Invoke(view, new object[] { phase == 0 ? before : after, "Łup ze skrzyni", "Skrzynia jest pusta.", 0, true });
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                    Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rect); Canvas.ForceUpdateCanvases();
                    camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 900, 640), 0, 0); image.Apply();
                    File.WriteAllBytes(Output + (phase == 0 ? "/loot-before.png" : "/loot-after.png"), image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(image);
            }
        }
    }
}
