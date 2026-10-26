using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class CampingTooltipLifecycleTests
    {
        const string Prefabs = "Assets/_Project/Prefabs/UI/Workspace/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject canvas, events;
        UIWorkspaceTooltipView tooltip;
        CharacterInfoWindowView window;
        CharacterInstance character;
        GameSession session;
        ItemDefinition kit;
        GameContentCatalog catalog;
        DungeonCampService camp;

        [SetUp] public void Setup()
        {
            canvas = new GameObject("Camping tooltip test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            canvas.AddComponent<UIWorkspaceWindowManager>();
            events = new GameObject("Camping events", typeof(EventSystem));
            Invoke(events.GetComponent<EventSystem>(), "OnEnable");
            tooltip = Instantiate<UIWorkspaceTooltipView>("PF_UIW_Tooltip.prefab");
            Invoke(tooltip, "Awake"); Invoke(tooltip, "OnEnable");
            window = Instantiate<CharacterInfoWindowView>("PF_UIW_CharacterInfoWindow Variant.prefab");
            Invoke(window.GetComponent<UIWorkspaceWindow>(), "Awake"); Invoke(window, "Awake");
            character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            character.CurrentHealth = 1;
            session = new GameSession(GameModeState.Exploration);
            session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
            session.TrySetActiveLeader(character.CharacterId);
            var dungeon = new DungeonInstance("catacombs", 1);
            dungeon.TryAddFloor(new DungeonFloorInstance(0)); dungeon.TryAddFloor(new DungeonFloorInstance(1));
            session.TrySetActiveDungeon(dungeon);
            kit = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(kit, "<Id>k__BackingField", DungeonCampService.KitId);
            catalog = ScriptableObject.CreateInstance<GameContentCatalog>(); catalog.ItemDefinitions.Add(kit);
            camp = new DungeonCampService(); camp.TryIssueSupplies(session, catalog);
            window.Initialize(session, character.CharacterId); Invoke(window, "OnEnable");
            window.CampAction = (owner, item) => camp.TryCamp(session, session.ActiveDungeon.Floors[0],
                out var reason, owner, item) ? string.Empty : reason;
        }

        [TearDown] public void Cleanup()
        {
            Invoke(window, "OnDisable");
            Invoke(events.GetComponent<EventSystem>(), "OnDisable");
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events);
            Object.DestroyImmediate(kit); Object.DestroyImmediate(catalog);
        }

        [TestCase("pending")]
        [TestCase("visible")]
        [TestCase("pinned")]
        public void InventoryCampConsumesClickedKitAndLeavesNoSuccessTooltip(string state)
        {
            var clicked = character.Inventory.Items[1];
            var row = Get<List<CharacterInfoItemView>>(window, "inventoryViews").Single(v => v.Item == clicked);
            Prime(row, state);
            window.TryUseCampingKit(clicked);
            Assert.That(character.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(character.Inventory.Items.Contains(clicked), Is.False);
            Assert.That(character.CurrentHealth, Is.GreaterThan(1));
            AssertHidden();
            // The consumed row cannot repeat the action or consume the other kit.
            window.TryUseCampingKit(clicked);
            Assert.That(character.Inventory.Items.Count, Is.EqualTo(1));
            AssertHidden();
        }

        [Test] public void InventoryCampFailureUsesDismissibleNoticeAndKeepsKit()
        {
            var clicked = character.Inventory.Items[0];
            window.ShowItemMenu(clicked, Vector2.zero);
            window.CampAction = (_, _) => "Zbierz żywych członków drużyny przy przywódcy.";
            window.TryUseCampingKit(clicked);
            var popup = Get<ItemActionPopupView>(window, "itemActionPopup");
            Assert.That(Get<TMP_Text>(popup, "quantityHint").text, Does.StartWith("Zbierz"));
            Assert.That(Get<GameObject>(popup, "quantityPanel").activeInHierarchy, Is.True);
            Assert.That(character.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(character.CurrentHealth, Is.EqualTo(1));
            AssertHidden();
            popup.HideAll();
            Assert.That(Get<GameObject>(popup, "quantityPanel").activeInHierarchy, Is.False);
        }

        [Test] public void GroundCampSuccessConsumesOneKitAndLeavesNoResultCard()
        {
            var controller = canvas.AddComponent<ExplorationPartyController>();
            var terrain = canvas.AddComponent<DungeonFloorMapView>();
            var map = new DungeonFloorMap();
            for (var y = 0; y < 5; y++) for (var x = 0; x < 5; x++)
                map.TryAddFloorCell(new Vector2Int(x, y), "camp-room");
            var floor = session.ActiveDungeon.Floors[0]; floor.TrySetMap(map);
            Set(terrain, "boundMap", map);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"), canvas.transform);
            actor.Bind(character); actor.ConfigureTerrain(map, terrain);
            actor.SetPosition(terrain.CellToWorldCenter(new Vector2Int(2, 2)));
            controller.Initialize(session, new[] { actor });
            controller.ConfigureTraversal(floor, terrain, null);
            Assert.That(controller.CanCamp(out var reason), Is.True, reason);
            var hover = canvas.AddComponent<DungeonTacticalTooltip>();
            var menu = Instantiate<PartyRailMenuView>("PF_UIW_PartyContextMenu.prefab"); menu.Hide();
            Set(hover, "partyController", controller); Set(hover, "tooltip", tooltip); Set(hover, "obstacleMenu", menu);
            Prime(hover, "pinned");
            Invoke(hover, "TryCampFromMenu");
            AssertHidden();
            Assert.That(character.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(character.CurrentHealth, Is.GreaterThan(1));
            Assert.That(menu.gameObject.activeSelf, Is.False);
        }

        [Test] public void GroundCampRechecksConditionsAndShowsFailureWithoutTooltip()
        {
            var controller = canvas.AddComponent<ExplorationPartyController>();
            var hover = canvas.AddComponent<DungeonTacticalTooltip>();
            var menu = Instantiate<PartyRailMenuView>("PF_UIW_PartyContextMenu.prefab");
            Set(hover, "partyController", controller); Set(hover, "tooltip", tooltip); Set(hover, "obstacleMenu", menu);
            Prime(hover, "pinned");
            Invoke(hover, "TryCampFromMenu");
            AssertHidden();
            Assert.That(menu.gameObject.activeInHierarchy, Is.True);
            Assert.That(Get<IReadOnlyList<PartyRailMenuView.Entry>>(menu, "rootActions")[0].Label,
                Is.EqualTo("Obóz można rozbić tylko w lochu."));
            menu.Hide(); Assert.That(menu.gameObject.activeSelf, Is.False);
        }

        [TestCase("item", "pending")]
        [TestCase("item", "visible")]
        [TestCase("item", "pinned")]
        [TestCase("equipment", "pending")]
        [TestCase("equipment", "pinned")]
        public void DisabledItemSourceImmediatelyCancelsItsCard(string kind, string state)
        {
            MonoBehaviour source = kind == "item"
                ? Get<List<CharacterInfoItemView>>(window, "inventoryViews")[0]
                : window.GetComponentsInChildren<CharacterInfoEquipmentSlotView>(true)[0];
            Prime(source, state);
            source.gameObject.SetActive(false);
            // EditMode does not dispatch the runtime lifecycle of these views.
            Invoke(source, "OnDisable");
            AssertHidden();
        }

        [TestCase("pending")]
        [TestCase("visible")]
        [TestCase("pinned")]
        public void DisabledBehaviourOnActiveObjectCannotKeepTooltipAlive(string state)
        {
            var source = new GameObject("Generic tooltip source", typeof(CanvasGroup));
            source.transform.SetParent(canvas.transform);
            var behaviour = source.GetComponent<CanvasGroup>();
            Prime(behaviour, state);
            behaviour.enabled = false;
            Assert.That(source.activeInHierarchy, Is.True);
            Invoke(tooltip, "Update");
            AssertHidden();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosingOrRebindingWindowCancelsOwnedPinnedCard(bool rebind)
        {
            Prime(Get<List<CharacterInfoItemView>>(window, "inventoryViews")[0], "pinned");
            if (rebind) window.Initialize(session, character.CharacterId);
            else Invoke(window, "OnDisable");
            AssertHidden();
        }

        [Test] public void OldItemAndWindowInvalidationPreserveUnrelatedPinnedCard()
        {
            var source = new object();
            Prime(source, "pinned");
            Invoke(Get<List<CharacterInfoItemView>>(window, "inventoryViews")[0], "OnDisable");
            Invoke(window, "OnDisable");
            Assert.That(tooltip.IsPinned, Is.True);
            Assert.That(Get<object>(tooltip, "sourceIdentity"), Is.SameAs(source));
        }

        [Test] public void DestroyedSourceClosesPinnedCardOnNextUpdate()
        {
            var source = new GameObject("Destroyed source", typeof(CanvasGroup));
            Prime(source.GetComponent<CanvasGroup>(), "pinned");
            Object.DestroyImmediate(source);
            Invoke(tooltip, "Update");
            AssertHidden();
        }

        T Instantiate<T>(string name) where T : Component => Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name), canvas.transform).GetComponentInChildren<T>(true);
        void Prime(object source, string state)
        {
            tooltip.ShowCompactText("Obóz", "Treść przed akcją", source);
            if (state == "pending") return;
            var show = Get<Action>(tooltip, "pendingShow");
            Assert.That(show, Is.Not.Null); Set(tooltip, "pendingShow", null); show();
            if (state == "pinned") tooltip.PinAtCurrentPosition();
        }
        void AssertHidden()
        {
            Assert.That(Get<bool>(tooltip, "visible"), Is.False);
            Assert.That(Get<Action>(tooltip, "pendingShow"), Is.Null);
            Assert.That(tooltip.IsPinned, Is.False);
        }
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
