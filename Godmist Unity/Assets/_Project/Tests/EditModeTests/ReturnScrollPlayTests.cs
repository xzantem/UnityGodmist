using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public class ReturnScrollPlayTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Output => Path.GetFullPath(Application.dataPath + "/../../LocalHandoffs/return-scroll-20261103");
        [Serializable] class Scenes { public string[] paths; public bool[] loaded, active; }
        static void Stage(string value) => File.AppendAllText(Path.Combine(Output, "play-stages.txt"), DateTime.UtcNow.ToString("O") + " " + value + "\n");
        static T View<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).First(v => v.gameObject.scene.IsValid());
        static IEnumerator Loaded(GameBootstrap game)
        {
            var deadline = Time.realtimeSinceStartup + 90;
            while (game.IsSceneLoading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.IsSceneLoading, Is.False);
            for (var i = 0; i < 15; i++) yield return null;
        }
        static IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            yield return BackgroundGameCapture.Capture(Path.Combine(Output, name + ".png"), beforeRender: () =>
            {
                // Capture switches overlay canvases to a 1920x1080 render camera.
                // Reflow/reclamp the existing tooltip in that coordinate space.
                var tooltip = UIWorkspaceTooltipView.ActiveInstance;
                if (tooltip == null || !(bool)typeof(UIWorkspaceTooltipView).GetField("visible", Private).GetValue(tooltip)) return;
                typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent", Private).Invoke(tooltip, null);
                typeof(UIWorkspaceTooltipView).GetMethod("PlaceAt", Private).Invoke(tooltip, new object[] { new Vector2(1200, 700) });
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)typeof(UIWorkspaceTooltipView).GetField("tooltipRect", Private).GetValue(tooltip);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var camera = rect.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
                foreach (var corner in corners)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    Assert.That(point.x, Is.InRange(-1, 1921));
                    Assert.That(point.y, Is.InRange(-1, 1081));
                }
                var meta = (TMP_Text)typeof(UIWorkspaceTooltipView).GetField("metaText", Private).GetValue(tooltip);
                if (meta.text.Contains("<sprite name=\"gold\">"))
                {
                    meta.ForceMeshUpdate(true, true);
                    Stage("Tooltip currency: " + tooltip.name + "; library=" + meta.spriteAsset?.name
                        + "; richText=" + meta.richText + "; sprites=" + meta.textInfo.spriteCount
                        + "; parsed=" + meta.GetParsedText());
                    Assert.That(meta.spriteAsset, Is.Not.Null);
                    Assert.That(meta.spriteAsset.spriteCharacterTable.Any(c => c.name == "gold"), Is.True);
                    Assert.That(meta.GetParsedText(), Does.Not.Contain("<sprite"), "The price must not expose raw markup.");
                    Assert.That(meta.GetParsedText().Any(c => char.GetUnicodeCategory(c)
                        == System.Globalization.UnicodeCategory.PrivateUse), Is.True,
                        "The production TMP parser must resolve the tag to its currency glyph.");
                }
            });
        }

        [UnityTest]
        public IEnumerator BuyUseReturnResumeAndSaveBothProductionDungeons()
        {
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), Is.False);
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            File.WriteAllText(Path.Combine(Output, "previous-scenes.json"), JsonUtility.ToJson(new Scenes {
                paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(), active = setup.Select(s => s.isActive).ToArray() }));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            Stage("Entering Play Mode");
            yield return new EnterPlayMode();
            yield return VerifyRunningGame();
        }

        static IEnumerator VerifyRunningGame()
        {
            for (var i = 0; i < 10; i++) yield return null;
            var game = GameBootstrap.Instance;
            Assert.That(game, Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(game,
                new object[] { new SaveManager(Path.Combine(Output, "isolated-saves")) });
            Assert.That(game.TryStartNewGame(out var error), Is.True, error);
            yield return Loaded(game);
            Stage("City ready");
            game.Session.TryAddGold(200);
            var ownerId = game.Session.PlayerParty.ActiveLeaderId;
            var owner = game.Session.CharactersById[ownerId];
            var merchant = game.Session.NpcsById.Values.Single(n => n.Definition.GetRole<EnchanterRoleData>() != null);
            var scroll = (ConsumableItemInstance)merchant.ShopInventory.Items.Single(i => i.Definition.Id == "item.consumable.return_scroll");

            var services = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<NpcServicesWindowView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab"), UIWorkspaceWindowManager.Instance.WorkspaceRoot);
            Assert.That(services.Open(merchant, ownerId), Is.True);
            var trade = services.GetComponentInChildren<TradeServiceWindowView>(true);
            var slot = trade.GetComponentsInChildren<TradeItemSlotView>(true).Single(s => s.Item == scroll && s.Source == TradeItemSlotSource.TraderOffer);
            slot.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return Capture("01-enchanter-scroll");
            var gold = game.Session.Gold;
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = slot.transform.position };
            slot.OnPointerClick(click); slot.OnPointerClick(click);
            Assert.That(owner.Inventory.Items, Does.Contain(scroll));
            Assert.That(gold - game.Session.Gold, Is.EqualTo(200));
            services.GetComponent<UIWorkspaceWindow>().Close();
            Assert.That(game.TryReturnToArungardFromDungeon(), Is.False, "Cannot use a return scroll in town.");
            Stage("Purchased through trade UI");

            foreach (var theme in new[] { "forest", "catacombs" })
            {
                if (theme == "catacombs")
                {
                    // Use a real seeded chest roll for the second scroll.
                    var table = AssetDatabase.LoadAssetAtPath<LootTableData>(ReturnScrollSetup.ChestPaths[0]);
                    for (var seed = 0; seed < 1000 && (scroll == null || !scroll.HasCharges); seed++)
                        scroll = new LootTableSystem(new System.Random(seed), game.ContentCatalog.EquipmentPartDefinitions)
                            .RollChestLoot(table).OfType<ConsumableItemInstance>().FirstOrDefault(i => i.Definition.Id == "item.consumable.return_scroll");
                    Assert.That(scroll, Is.Not.Null);
                    Assert.That(new InventoryService().TryAddItem(owner.Inventory, scroll), Is.True);
                }
                var definition = game.ContentCatalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + theme + ".expedition");
                Assert.That(game.TryEnterDungeon(definition, 231023, offeredLevel: 1, length: DungeonLength.Short), Is.True);
                yield return Loaded(game);
                var dungeon = game.Session.ActiveDungeon;
                Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
                // A controlled cleared encounter proves that resuming does not regenerate progress.
                var clearedId = floor.Encounters.First().InstanceId;
                Assert.That(dungeon.TryMarkEncounterCleared(clearedId), Is.True);
                var roomIds = floor.Rooms.Select(r => r.RoomId).ToArray();
                View<CharacterInfoPartyRailView>().OpenControllerCharacterPanel(ControllerCharacterPanel.Inventory, ownerId);
                var card = View<CharacterInfoWindowView>();
                Assert.That(card.GetComponent<UIWorkspaceWindow>().IsOpen, Is.True);
                UIWorkspaceTooltipView.ActiveInstance.ShowItem(scroll, owner, card);
                yield return Capture(theme + "-scroll-inventory");

                Assert.That(game.Session.TrySetActiveCombat(new CombatSession { CombatId = "test.scroll.block" }), Is.True);
                Assert.That(game.TryReturnToArungardFromDungeon(), Is.False);
                Assert.That(game.Session.TryClearActiveCombat(), Is.True);
                var destination = typeof(GameBootstrap).GetField("explorationSceneName", Private);
                var original = destination.GetValue(game);
                destination.SetValue(game, "MissingScrollTestScene");
                LogAssert.Expect(LogType.Error, "SceneLoader: scena 'MissingScrollTestScene' nie jest dodana do Build Settings.");
                card.TryUseConsumable(scroll);
                destination.SetValue(game, original);
                Assert.That(scroll.CurrentCharges, Is.EqualTo(1));
                Assert.That(game.Session.ActiveDungeon, Is.SameAs(dungeon));

                var completed = game.Session.DungeonOffers.CompletedExpeditions;
                card.TryUseConsumable(scroll);
                Assert.That(game.IsSceneLoading, Is.True);
                Assert.That(scroll.CurrentCharges, Is.EqualTo(1), "Accepted loading alone must not consume the scroll.");
                card.TryUseConsumable(scroll);
                yield return Loaded(game);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Arungard"));
                Assert.That(owner.Inventory.Items, Has.No.Member(scroll));
                Assert.That(scroll.CurrentCharges, Is.Zero);
                Assert.That(game.Session.ActiveDungeon, Is.SameAs(dungeon));
                Assert.That(dungeon.IsCompleted, Is.False);
                Assert.That(game.Session.DungeonOffers.CompletedExpeditions, Is.EqualTo(completed));
                Assert.That(dungeon.IsEncounterCleared(clearedId), Is.True);

                var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(game.Session, "scroll-return");
                Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, game.ContentCatalog, out var restored, out var failure), Is.True, failure?.ToString());
                Assert.That(restored.ActiveDungeon.IsEncounterCleared(clearedId), Is.True);
                Assert.That(restored.CharactersById[ownerId].Inventory.Items.Any(i => i.InstanceId == scroll.InstanceId), Is.False);

                var selector = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonSelectorWindowView>(
                    "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonSelector.prefab"), UIWorkspaceWindowManager.Instance.WorkspaceRoot);
                selector.Open(game, definition);
                Assert.That(string.Join(" ", selector.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)), Does.Contain("Wznów"));
                yield return Capture(theme + "-resume");
                selector.GetComponent<UIWorkspaceWindow>().Close();
                Assert.That(game.TryEnterDungeon(definition, 999999), Is.True);
                yield return Loaded(game);
                Assert.That(game.Session.ActiveDungeon, Is.SameAs(dungeon));
                Assert.That(dungeon.Seed, Is.EqualTo(231023));
                Assert.That(dungeon.TryGetCurrentFloor(out var resumed), Is.True);
                CollectionAssert.AreEqual(roomIds, resumed.Rooms.Select(r => r.RoomId));
                Assert.That(dungeon.IsEncounterCleared(clearedId), Is.True);
                Stage(theme + ": returned; consumed once; persisted; resumed original instance PASS");
                Assert.That(game.TryRetreatFromDungeon(), Is.True);
                yield return Loaded(game);
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            var file = Path.Combine(Output, "previous-scenes.json");
            if (!File.Exists(file)) yield break;
            var previous = JsonUtility.FromJson<Scenes>(File.ReadAllText(file));
            if (previous.paths.Length == 0 || previous.paths.Any(string.IsNullOrEmpty))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p, i) => new SceneSetup {
                path = p, isLoaded = previous.loaded[i], isActive = previous.active[i] }).ToArray());
        }
    }
}
