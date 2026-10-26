using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class DirectLootPresentationTests
    {
        const string Output = "../LocalHandoffs/loot-portraits-walls-20261026";
        static void Set(object target, string property, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("<" + property + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) continue;
                field.SetValue(target, value); return;
            }
            throw new InvalidOperationException(property);
        }
        static void Field(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        [Test]
        public void IndependentEquipmentCanDropOneWhileActualBagStillGuaranteesTwo()
        {
            var equipment = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            var bag = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            var table = ScriptableObject.CreateInstance<LootTableData>();
            try
            {
                Set(equipment, "Id", "test.armor"); Set(equipment, "ItemType", ItemType.Armor);
                Set(bag, "Id", "test.bag"); Set(bag, "ItemType", ItemType.Lootbag);
                var bracket = new LootBagBracketData();
                for (var i = 0; i < 2; i++)
                {
                    var entry = new LootBagPoolEntryData(); Set(entry, "ItemDefinition", equipment); Set(entry, "Amount", new IntRange(1, 1));
                    var pool = new LootBagPoolData(); pool.Entries.Add(entry); bracket.Pools.Add(pool);
                }
                bag.Brackets.Add(bracket);
                var source = new LootEntryData(); Set(source, "ItemDefinition", bag); Set(source, "DropChance", .5f); Set(source, "Amount", new IntRange(1, 1));
                table.Entries.Add(source);
                var rolls = new Queue<float>(new[] { 0f, .1f, 0f, .9f });
                var direct = new LootTableSystem(() => rolls.Dequeue()).RollDirectLoot(table);
                Assert.That(direct.Count, Is.EqualTo(1), "No common package gate may couple both drops.");
                Assert.That(direct[0], Is.TypeOf<EquipmentItemInstance>());
                var original = new LootTableSystem(() => 0f).CreateLootBag(bag);
                Assert.That(original.Contents.Count, Is.EqualTo(2), "Actual lootbag contents remain unchanged.");
            }
            finally { Object.DestroyImmediate(equipment); Object.DestroyImmediate(bag); Object.DestroyImmediate(table); }
        }

        [TestCase("forest", 1)] [TestCase("forest", 3)] [TestCase("forest", 5)]
        [TestCase("catacombs", 1)] [TestCase("catacombs", 3)] [TestCase("catacombs", 5)]
        public void ProductionChestsKeepFiveMeanItemsWithoutPackageBursts(string theme, int chests)
        {
            var path = theme == "forest" ? "Dungeons/Forest/Loot/LT_Forest_Chest_1_10_Production.asset"
                : "Catacombs/Loot/LT_Catacombs_Chest_1_10_Production.asset";
            var table = AssetDatabase.LoadAssetAtPath<LootTableData>("Assets/_Project/ScriptableObjects/" + path);
            var rng = new System.Random(959042448);
            var loot = new LootTableSystem(() => (float)rng.NextDouble());
            var oldRng = new System.Random(959042448);
            var oldLoot = new LootTableSystem(() => (float)oldRng.NextDouble());
            var oldMethod = typeof(LootTableSystem).GetMethod("RollLoot", BindingFlags.Instance | BindingFlags.NonPublic);
            // These production tables have one armor and one weapon source,
            // each 4% * (1 + 1 + .8 + .5 + .25), amount one.
            var oldScale = 5f / (chests * .04f * 3.55f * 2f);
            const int samples = 5000;
            var total = 0; var singles = 0; var bursts = 0;
            var oldTotal = 0; var oldSingles = 0; var oldBursts = 0;
            for (var i = 0; i < samples; i++)
                for (var c = 0; c < chests; c++)
                {
                    var count = loot.RollChestLoot(table, chests).Count(item => item.Definition.ItemType is ItemType.Armor or ItemType.Weapon);
                    total += count; if (count == 1) singles++; if (count >= 4) bursts++;
                    var legacy = (List<ItemInstance>)oldMethod.Invoke(oldLoot, new object[] { table, oldScale, false });
                    var oldCount = legacy.OfType<LootBagItemInstance>().SelectMany(b => b.Contents)
                        .Count(item => item.Definition.ItemType is ItemType.Armor or ItemType.Weapon);
                    oldTotal += oldCount; if (oldCount == 1) oldSingles++; if (oldCount >= 4) oldBursts++;
                }
            Assert.That(total / (float)samples, Is.EqualTo(5).Within(.2f));
            Assert.That(singles, Is.GreaterThan(0));
            Directory.CreateDirectory(Output);
            File.WriteAllText($"{Output}/loot-{theme}-{chests}.txt", $"seed=959042448; expeditions={samples}; chests={chests}\nBEFORE mean={oldTotal / (float)samples}; single-item chests={oldSingles}; 4+ item chests={oldBursts}\nAFTER mean={total / (float)samples}; single-item chests={singles}; 4+ item chests={bursts}");
        }

        [Test]
        public void DowningNotificationIsSuppressedButDamageRemainsVisible()
        {
            var root = new GameObject("Floating policy");
            try
            {
                var view = root.AddComponent<CombatFloatingFeedbackController>();
                var method = typeof(CombatFloatingFeedbackController).GetMethod("ShouldShowFeedback", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method.Invoke(view, new object[] { new CombatFeedbackEvent(null, CombatFeedbackType.Downed) }), Is.False);
                Assert.That(method.Invoke(view, new object[] { new CombatFeedbackEvent(null, CombatFeedbackType.Damage, 14, DamageType.Bleed) }), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RenderPartitionAndActivePortraitComparison()
        {
            Directory.CreateDirectory(Output);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var root = new GameObject("Partition proof", typeof(Grid));
            var target = new RenderTexture(768, 896, 24);
            var pixels = new Texture2D(768, 896, TextureFormat.RGBA32, false);
            var old = RenderTexture.active;
            try
            {
                var authored = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DungeonFloorMapView>(true)).Single();
                var view = root.AddComponent<DungeonFloorMapView>();
                var tilesObject = new GameObject("Native floor", typeof(Tilemap), typeof(TilemapRenderer)); tilesObject.transform.SetParent(root.transform, false);
                Field(view, "floorTilemap", tilesObject.GetComponent<Tilemap>());
                foreach (var field in new[] { "floorTile", "wallSegmentPrefab", "lowWallPrefab", "railingPrefab" }) Field(view, field, Field<object>(authored, field));
                view.ConfigureCatacombsArtPresentation(true);
                var map = new DungeonFloorMap();
                for (var y = 0; y < 10; y++) for (var x = 0; x < 8; x++)
                    if (x >= 3 || y < 9) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                for (var y = 0; y < 10; y++)
                    if (y < 3 || y > 4) map.SetWallType(new DungeonCellEdge(new Vector2Int(2, y), new Vector2Int(3, y)), DungeonWallType.Full);
                for (var x = 0; x < 3; x++) map.SetWallType(new DungeonCellEdge(new Vector2Int(x, 8), new Vector2Int(x, 9)), DungeonWallType.Full);
                view.Rebuild(map);
                var vertical = root.GetComponentsInChildren<DungeonWallSegmentView>()
                    .Where(w => w.SurfaceProfile == DungeonWallSurfaceProfile.Side).ToArray();
                Assert.That(vertical.Length, Is.EqualTo(8));
                var faceEnds = vertical.Where(w => w.GetComponent<SpriteRenderer>().sprite.name == "RuntimeDungeonMixedSideFace").ToArray();
                Assert.That(faceEnds.Length, Is.EqualTo(2), "Only the two free southern ends use standing face art.");
                Assert.That(faceEnds.Select(w => w.transform.position.y), Is.EquivalentTo(new[] { .5f, 5.5f }));
                Assert.That(vertical.Except(faceEnds).All(w => w.GetComponent<SpriteRenderer>().sprite.name == "DungeonWallSide32"), Is.True,
                    "The run is cap/cap/cap/face at its southern end, never face/cap/cap.");
                var caps = root.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => r.name is "SideFreeUpperCap" or "SideTerminalFaceCap").ToArray();
                Assert.That(caps.Count(r => r.name == "SideFreeUpperCap"), Is.EqualTo(1));
                Assert.That(caps.Count(r => r.name == "SideTerminalFaceCap"), Is.EqualTo(2));
                Assert.That(caps.All(r => r.enabled && r.gameObject.activeInHierarchy), Is.True);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = 30;
                var cameraObject = new GameObject("Native comparison camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.7f;
                camera.transform.position = new Vector3(3.7f, 4.8f, -10); camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 768, 896), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + "/partition-current.png", pixels.EncodeToPNG());
                File.WriteAllText(Output + "/partition-renderers.txt", string.Join("\n", root.GetComponentsInChildren<DungeonWallSegmentView>()
                    .Where(w => w.SurfaceProfile == DungeonWallSurfaceProfile.Side).Select(w => $"{w.transform.position}: {w.GetComponent<SpriteRenderer>().sprite?.name}")));
            }
            finally
            {
                RenderTexture.active = old; Object.DestroyImmediate(root); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
