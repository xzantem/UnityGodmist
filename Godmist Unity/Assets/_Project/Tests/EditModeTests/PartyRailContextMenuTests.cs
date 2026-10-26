using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class PartyRailContextMenuTests
    {
        const string Root = "Assets/_Project/Prefabs/UI/Workspace/";
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject canvasObject, eventsObject, cameraObject;
        Canvas canvas;
        PartyRailMenuView menu;
        static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Fields).SetValue(owner, value);
        static Button[] Buttons(PartyRailMenuView view) => view.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
        static string Label(Button button) => button.GetComponentInChildren<TMP_Text>().text;
        [SetUp] public void Setup()
        {
            eventsObject = new GameObject("Menu test events", typeof(EventSystem));
            typeof(EventSystem).GetMethod("OnEnable", Fields).Invoke(eventsObject.GetComponent<EventSystem>(), null);
            canvasObject = new GameObject("Menu test canvas", typeof(RectTransform), typeof(Canvas));
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(800, 600);
            cameraObject = new GameObject("Menu camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 300;
            camera.transform.position = new Vector3(0, 0, -100); canvas.worldCamera = camera;
            menu = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PF_UIW_PartyContextMenu.prefab"), canvas.transform).GetComponent<PartyRailMenuView>();
            menu.Hide();
        }
        [TearDown] public void Cleanup()
        {
            typeof(EventSystem).GetMethod("OnDisable", Fields).Invoke(eventsObject.GetComponent<EventSystem>(), null);
            Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(eventsObject);
        }
        [Test] public void SubmenuDoesNotRunAnActionUntilDestinationIsChosenAndBackRestoresRoot()
        {
            var calls = 0;
            var actions = new[] { new PartyRailMenuView.Entry("Karta"),
                new PartyRailMenuView.Entry("Dołącz", children: new[] { new PartyRailMenuView.Entry("Grupa B", () => calls++) }) };
            menu.Show(Vector2.zero, actions, "Jerzy");
            Buttons(menu)[1].onClick.Invoke();
            Assert.That(calls, Is.Zero); Assert.That(Buttons(menu).Select(Label), Is.EqualTo(new[] { "Wróć", "Grupa B" }));
            Buttons(menu)[0].onClick.Invoke();
            Assert.That(menu.gameObject.activeSelf, Is.True); Assert.That(Buttons(menu).Select(Label), Is.EqualTo(new[] { "Karta", "Dołącz" }));
            Buttons(menu)[1].onClick.Invoke(); Buttons(menu)[1].onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1)); Assert.That(menu.gameObject.activeSelf, Is.False);
        }
        [Test] public void InputRouterIgnoresDestroyedSelectedObject()
        {
            var selected = new GameObject("Destroyed focused card");
            var events = eventsObject.GetComponent<EventSystem>();
            events.SetSelectedGameObject(selected);
            Object.DestroyImmediate(selected);
            Assert.That(ReferenceEquals(events.currentSelectedGameObject, null), Is.False,
                "The EventSystem retains a managed wrapper for its destroyed selection.");
            Assert.That(events.currentSelectedGameObject == null, Is.True);
            Assert.That(UIWorkspaceInputRouter.HasFocusedTextInput(), Is.False);
            Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.False);
        }
        [Test] public void DisabledActionCannotDispatchAndKeyboardNavigationSkipsIt()
        {
            var calls = 0;
            menu.Show(Vector2.zero, new[] { new PartyRailMenuView.Entry("Karta"),
                new PartyRailMenuView.Entry("Odłącz", () => calls++, false, true), new PartyRailMenuView.Entry("Kamera") }, "Jerzy", "Postać porusza się samodzielnie.");
            var buttons = Buttons(menu); buttons[1].onClick.Invoke();
            Assert.That(calls, Is.Zero); Assert.That(menu.gameObject.activeSelf, Is.True);
            Assert.That(buttons.All(b => b.navigation.mode == Navigation.Mode.None), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(buttons[0].gameObject));
            menu.MoveFocus(1); Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(buttons[2].gameObject));
            menu.MoveFocus(1); Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(buttons[0].gameObject));
            menu.MoveFocus(-1); Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(buttons[2].gameObject));
            Assert.That(Get<TMP_Text>(menu, "hintText").text, Does.Contain("samodzielnie"));
        }
        [Test] public void LegacyWorldMenuStillRunsItsActionAndClearsRailDecorations()
        {
            menu.Show(Vector2.zero, new[] { new PartyRailMenuView.Entry("Odłącz", separatorBefore: true) }, "Jerzy", "Samodzielnie");
            var calls = 0;
            menu.Show(Vector2.zero, new[] { ("Inspektuj", (System.Action)(() => calls++), true) }, "Wróg");
            Assert.That(Get<TMP_Text>(menu, "hintText").gameObject.activeSelf, Is.False);
            Assert.That(Get<List<RectTransform>>(menu, "separators").All(s => !s.gameObject.activeSelf), Is.True);
            Buttons(menu)[0].onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
        }
        [TestCase(-296f, 0f)] [TestCase(-296f, -280f)] [TestCase(390f, 280f)] [TestCase(-390f, 280f)]
        public void PopupUsesClickPositionAndClampsAtCanvasEdges(float x, float y)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, new Vector3(x, y, 0));
            menu.Show(screen, new[] { new PartyRailMenuView.Entry("Karta"), new PartyRailMenuView.Entry("Efekty"),
                new PartyRailMenuView.Entry("Kamera"), new PartyRailMenuView.Entry("Odłącz", separatorBefore: true) }, "Jerzy");
            var panel = Get<RectTransform>(menu, "panel"); var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Assert.That(panel.localPosition.x, Is.EqualTo(Mathf.Clamp(x, -400, 400 - panel.rect.width)).Within(.01f));
            Assert.That(panel.localPosition.y, Is.EqualTo(Mathf.Clamp(y, -300 + panel.rect.height, 300)).Within(.01f));
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(-400f - .01f));
            Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(-300f - .01f)); Assert.That(corners[2].y, Is.LessThanOrEqualTo(300f + .01f));
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(400f + .01f));
        }
        [Test] public void ClosingPopupRestoresPreviousSelection()
        {
            var previous = new GameObject("previous", typeof(RectTransform), typeof(Button)); previous.transform.SetParent(canvas.transform, false);
            EventSystem.current.SetSelectedGameObject(previous);
            menu.Show(Vector2.zero, new[] { new PartyRailMenuView.Entry("Karta") }, "Jerzy"); menu.Hide();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(previous));
        }
        [Test] public void OpenMenuKeepsMovementAvailableWithoutBecomingATextInput()
        {
            var partyObject = new GameObject("party", typeof(RectTransform)); partyObject.transform.SetParent(canvas.transform, false);
            partyObject.SetActive(false);
            var party = partyObject.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
            party.Initialize(new GameSession(GameModeState.Exploration), new Godmist.Views.Exploration.ExplorationCharacterView[0]);
            Assert.That(party.CanAcceptExplorationInput, Is.True);
            menu.Show(Vector2.zero, new[] { new PartyRailMenuView.Entry("Karta") }, "Jerzy");
            Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.True);
            Assert.That(UIWorkspaceInputRouter.HasFocusedTextInput(), Is.False);
            Assert.That(party.CanAcceptExplorationInput, Is.True);
            menu.Hide(); Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.False);
            Assert.That(party.CanAcceptExplorationInput, Is.True);
        }
        [Test] public void DismissalKeepsTheSameEnterOrEscapeFromReachingWorldInput()
        {
            var manager = canvasObject.AddComponent<UIWorkspaceWindowManager>();
            typeof(UIWorkspaceWindowManager).GetMethod("Awake", Fields).Invoke(manager, null);
            menu.Show(Vector2.zero, new[] { new PartyRailMenuView.Entry("Karta") }, "Jerzy"); menu.Hide();
            Assert.That(menu.gameObject.activeSelf, Is.False);
            Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.True);
            Set(manager, "contextMenuInputFrame", Time.frameCount - 1);
            Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.False);
            typeof(UIWorkspaceWindowManager).GetMethod("OnDestroy", Fields).Invoke(manager, null);
        }
        [Test] public void RightClickMenuActsOnClickedCharacterWithoutChangingActiveLeader()
        {
            var session = new GameSession(GameModeState.Exploration);
            foreach (var id in new[] { "a", "b" }) { session.TryRegisterCharacter(new CharacterInstance { CharacterId = id }); session.PlayerParty.TryAddActiveMember(id); }
            session.PlayerParty.TrySetActiveLeader("a");
            var manager = canvasObject.AddComponent<UIWorkspaceWindowManager>();
            var railObject = new GameObject("rail", typeof(RectTransform)); railObject.transform.SetParent(canvas.transform, false);
            var rail = railObject.AddComponent<CharacterInfoPartyRailView>(); Set(rail, "session", session); Set(rail, "workspaceManager", manager);
            Set(rail, "menuTemplate", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PF_UIW_PartyContextMenu.prefab").GetComponent<PartyRailMenuView>());
            var row = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PF_UIW_PartyMember.prefab"), rail.transform).GetComponent<CharacterInfoPartyPortraitView>();
            row.Bind(session.CharactersById["b"], null); rail.ShowMenu(row, Vector2.zero);
            var popup = Get<PartyRailMenuView>(rail, "menu");
            var click = canvas.transform.InverseTransformPoint(canvas.worldCamera.ScreenToWorldPoint(new Vector3(0, 0, 100)));
            var panel = Get<RectTransform>(popup, "panel");
            Assert.That(panel.localPosition.x, Is.EqualTo(Mathf.Clamp(click.x, -400, 400 - panel.rect.width)).Within(.01f));
            Assert.That(Buttons(popup).Select(Label), Is.EqualTo(new[] { "Karta postaci", "Statystyki i efekty", "Wycentruj kamerę", "Odłącz od grupy" }));
            Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo("a"));
            Buttons(popup)[3].onClick.Invoke();
            Assert.That(session.PlayerParty.GetGroup(session.PlayerParty.GetGroupIndex("b")), Is.EqualTo(new[] { "b" }));
            Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo("a"));
            rail.ShowMenu(row, Vector2.zero);
            Assert.That(Buttons(popup)[3].interactable, Is.False); Assert.That(Label(Buttons(popup)[4]), Does.StartWith("Dołącz"));
        }
        [Test] public void ShortcutCanOpenAndCloseCardInCombatWithoutEnablingProgression()
        {
            var session = new GameSession(GameModeState.Combat);
            var character = new CharacterInstance { CharacterId = "a", Level = 1, Stats = new CharacterStats { MaxHealth = new Stat(350) }, CurrentHealth = 350 };
            session.TryRegisterCharacter(character); session.PlayerParty.TryAddActiveMember("a"); session.PlayerParty.TrySetActiveLeader("a");
            var combat = new CombatSession(); combat.Units.Add(new BattleUnit { Character = character, IsPlayerSide = true });
            Get<List<CombatSession>>(session, "combats").Add(combat);
            var manager = canvasObject.AddComponent<UIWorkspaceWindowManager>();
            var railObject = new GameObject("rail", typeof(RectTransform)); railObject.transform.SetParent(canvas.transform, false);
            var rail = railObject.AddComponent<CharacterInfoPartyRailView>(); Set(rail, "session", session); Set(rail, "workspaceManager", manager);
            Set(rail, "characterInfoWindowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PF_UIW_CharacterInfoWindow Variant.prefab").GetComponent<CharacterInfoWindowView>());
            var toggle = typeof(CharacterInfoPartyRailView).GetMethod("ToggleActiveCharacterInfo", Fields);
            toggle.Invoke(rail, null);
            var card = Get<Dictionary<string, CharacterInfoWindowView>>(rail, "windows")["a"];
            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Assert.That(Get<Button>(card, "skillLoadoutButton").gameObject.activeSelf, Is.False);
            Assert.That(Get<Button>(card, "skillTreeButton").gameObject.activeSelf, Is.False);
            toggle.Invoke(rail, null); Assert.That(card.gameObject.activeSelf, Is.False);
        }
        [TestCase(true)] [TestCase(false)] public void InventoryTransferIsBlockedIfSourceOrDestinationIsInCombat(bool sourceInCombat)
        {
            var session = new GameSession(); var a = new CharacterInstance { CharacterId = "a" }; var b = new CharacterInstance { CharacterId = "b" };
            var combat = new CombatSession { IsSeamless = true };
            combat.Units.Add(new BattleUnit { Character = sourceInCombat ? a : b, IsPlayerSide = true }); Get<List<CombatSession>>(session, "combats").Add(combat);
            var sourceObject = new GameObject("source", typeof(RectTransform)); sourceObject.transform.SetParent(canvas.transform, false);
            var targetObject = new GameObject("target", typeof(RectTransform)); targetObject.transform.SetParent(canvas.transform, false);
            var source = sourceObject.AddComponent<CharacterInfoWindowView>(); var target = targetObject.AddComponent<CharacterInfoWindowView>();
            Set(source, "session", session); Set(source, "character", a); Set(target, "session", session); Set(target, "character", b);
            var item = new ItemInstance { InstanceId = "test item" }; a.Inventory.Items.Add(item);
            target.TryReceiveInventoryTransfer(source, item);
            Assert.That(a.Inventory.Items, Does.Contain(item)); Assert.That(b.Inventory.Items, Is.Empty);
            var fightingCard = sourceInCombat ? source : target; fightingCard.BeginDrag(item, new PointerEventData(EventSystem.current));
            Assert.That(Get<ItemInstance>(fightingCard, "draggedItem"), Is.Null);
        }
    }
}
