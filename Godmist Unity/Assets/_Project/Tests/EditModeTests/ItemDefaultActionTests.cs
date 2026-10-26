using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ItemDefaultActionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject canvas, events;
        CharacterInfoWindowView window;
        CharacterInstance character;
        GameSession session;
        readonly List<ScriptableObject> definitions = new();

        [SetUp] public void Setup()
        {
            canvas = new GameObject("Default item actions", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            canvas.AddComponent<UIWorkspaceWindowManager>();
            events = new GameObject("Default action events", typeof(EventSystem));
            Invoke(events.GetComponent<EventSystem>(), "OnEnable");
            window = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab"),
                canvas.transform).GetComponent<CharacterInfoWindowView>();
            Invoke(window.GetComponent<UIWorkspaceWindow>(), "Awake"); Invoke(window, "Awake");
            character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            session = new GameSession(GameModeState.Exploration);
            session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
            window.Initialize(session, character.CharacterId); Invoke(window, "OnEnable");
        }
        [TearDown] public void Cleanup()
        {
            Invoke(window, "OnDisable"); Invoke(events.GetComponent<EventSystem>(), "OnDisable");
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events);
            foreach (var definition in definitions) Object.DestroyImmediate(definition);
            definitions.Clear();
        }

        [Test] public void SingleClickSelectsInventoryButtonWithoutEquipping()
        {
            var equipment = Equipment(); var row = Add(equipment);
            var data = ClickData(1);
            ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.pointerDownHandler);
            Click(row, data);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));
            Assert.That(character.Inventory.Items, Does.Contain(equipment));
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.Null);
        }
        [Test] public void DoubleClickEquipsExactlyOnceThroughTheRealButtonAndView()
        {
            var equipment = Equipment(); var row = Add(equipment);
            Click(row, ClickData(1)); Click(row, ClickData(2));
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.SameAs(equipment));
            Assert.That(character.Inventory.Items, Has.No.Member(equipment));
        }
        [Test] public void KeyboardSubmitEquipsWithoutRequiringPointerDoubleClick()
        {
            var equipment = Equipment(); var row = Add(equipment);
            ExecuteEvents.Execute(row.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.SameAs(equipment));
        }
        [Test] public void EquippedSlotSingleClickInspectsAndDoubleClickUnequips()
        {
            var equipment = Equipment(); character.Equipment.SetItem(equipment.EquipmentSlot, equipment); window.Refresh();
            var slot = Slot(equipment);
            Click(slot, ClickData(1));
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.SameAs(equipment));
            Assert.That(Get<GameObject>(window, "itemDetailsPanel").activeSelf, Is.True);
            Click(slot, ClickData(2));
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.Null);
            Assert.That(character.Inventory.Items, Does.Contain(equipment));
        }
        [Test] public void KeyboardSubmitUnequipsSelectedSlot()
        {
            var equipment = Equipment(); character.Equipment.SetItem(equipment.EquipmentSlot, equipment); window.Refresh();
            ExecuteEvents.Execute(Slot(equipment).gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.Null);
            Assert.That(character.Inventory.Items, Does.Contain(equipment));
        }
        [Test] public void PotionDoubleClickConsumesOneChargeAndTripleClickDoesNotConsumeAnother()
        {
            character.CurrentHealth = 1;
            var potion = Potion(); var row = Add(potion);
            Click(row, ClickData(1)); Assert.That(potion.CurrentCharges, Is.EqualTo(3));
            Click(row, ClickData(2)); Assert.That(potion.CurrentCharges, Is.EqualTo(2));
            Assert.That(character.CurrentHealth, Is.GreaterThan(1));
            var refreshed = Rows().Single(v => v.Item == potion);
            Click(refreshed, ClickData(3)); Assert.That(potion.CurrentCharges, Is.EqualTo(2));
        }
        [Test] public void CombatOnlyBombDoesNotLoseChargesOrAutoSelectATarget()
        {
            var bomb = Potion(ConsumableUseType.Bomb); var row = Add(bomb);
            Click(row, ClickData(2)); Assert.That(bomb.CurrentCharges, Is.EqualTo(3));
            Assert.That(character.Inventory.Items, Does.Contain(bomb));
        }
        [Test] public void LootBagOpensOnDoubleClickAndNotSingleClick()
        {
            var bag = new LootBagItemInstance(Definition<LootBagItemDefinition>("default.bag"), "bag");
            var reward = new ItemInstance(Definition<ItemDefinition>("default.reward"), "reward");
            bag.Contents.Add(reward); var row = Add(bag);
            Click(row, ClickData(1)); Assert.That(character.Inventory.Items, Does.Contain(bag));
            Click(row, ClickData(2));
            Assert.That(character.Inventory.Items, Has.No.Member(bag));
            Assert.That(character.Inventory.Items, Does.Contain(reward));
        }
        [Test] public void CampingKitDoubleClickUsesExistingCampActionOnce()
        {
            var kit = new ItemInstance(Definition<ItemDefinition>("CampingKit"), "kit"); var row = Add(kit);
            var calls = 0; window.CampAction = (owner, id) =>
            {
                Assert.That(owner, Is.EqualTo(character.CharacterId)); Assert.That(id, Is.EqualTo(kit.InstanceId));
                calls++; character.Inventory.Items.Remove(kit); return string.Empty;
            };
            Click(row, ClickData(1)); Assert.That(calls, Is.Zero);
            Click(row, ClickData(2)); Assert.That(calls, Is.EqualTo(1));
            Assert.That(character.Inventory.Items, Has.No.Member(kit));
        }
        [TestCase("equipment")]
        [TestCase("potion")]
        [TestCase("bag")]
        [TestCase("camp")]
        public void CombatBlocksDefaultActions(string kind)
        {
            ItemInstance item = kind switch {
                "equipment" => Equipment(), "potion" => Potion(),
                "bag" => new LootBagItemInstance(Definition<LootBagItemDefinition>("blocked.bag"), "bag"),
                _ => new ItemInstance(Definition<ItemDefinition>("CampingKit"), "kit") };
            var row = Add(item); var calls = 0; window.CampAction = (_, _) => { calls++; return string.Empty; };
            var combat = new CombatSession { IsSeamless = true };
            combat.Units.Add(new BattleUnit { Character = character, IsPlayerSide = true });
            Get<List<CombatSession>>(session, "combats").Add(combat);
            Click(row, ClickData(2));
            Assert.That(character.Inventory.Items, Does.Contain(item));
            Assert.That(calls, Is.Zero);
            if (item is ConsumableItemInstance potion) Assert.That(potion.CurrentCharges, Is.EqualTo(3));
        }
        [TestCase("dragging")]
        [TestCase("disabled")]
        [TestCase("uninteractable")]
        [TestCase("button-disabled")]
        [TestCase("window-disabled")]
        [TestCase("removed")]
        public void DragAndStaleOrDisabledRowsCannotActivate(string state)
        {
            var equipment = Equipment(); var row = Add(equipment); var data = ClickData(2);
            if (state == "dragging") data.dragging = true;
            if (state == "disabled") row.enabled = false;
            if (state == "uninteractable") row.GetComponent<Button>().interactable = false;
            if (state == "button-disabled") row.GetComponent<Button>().enabled = false;
            if (state == "window-disabled") window.enabled = false;
            if (state == "removed") character.Inventory.Items.Remove(equipment);
            row.OnPointerClick(data);
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.Null);
        }
        [Test] public void RightClickKeepsContextMenuWithoutPerformingDefaultAction()
        {
            var equipment = Equipment(); var row = Add(equipment); var data = ClickData(2);
            data.button = PointerEventData.InputButton.Right; Click(row, data);
            Assert.That(character.Inventory.Items, Does.Contain(equipment));
            var popup = Get<ItemActionPopupView>(window, "itemActionPopup");
            var menu = Get<PartyRailMenuView>(popup, "menu");
            Assert.That(menu.gameObject.activeSelf, Is.True);
            Assert.That(menu.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Załóż"), Is.True);
        }
        [Test] public void StaleEquippedSlotCannotUnequipItsReplacement()
        {
            var equipment = Equipment(); character.Equipment.SetItem(equipment.EquipmentSlot, equipment); window.Refresh();
            var slot = Slot(equipment); var replacement = new EquipmentItemInstance { Definition = equipment.Definition, InstanceId = "replacement" };
            character.Equipment.SetItem(equipment.EquipmentSlot, replacement);
            slot.OnPointerClick(ClickData(2));
            Assert.That(character.Equipment.GetItem(equipment.EquipmentSlot), Is.SameAs(replacement));
        }
        EquipmentItemInstance Equipment() => AssetDatabase.FindAssets("t:EquipmentItemDefinition")
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>)
            .Select(d => new EquipmentItemInstance { Definition = d, InstanceId = "default.equipment" })
            .First(e => EquipmentService.MeetsRequirements(character, e));
        ConsumableItemInstance Potion(ConsumableUseType type = ConsumableUseType.Potion)
        {
            var def = Definition<ConsumableItemDefinition>("default.potion");
            Set(def, "<MaxCharges>k__BackingField", 3); Set(def, "<UseType>k__BackingField", type);
            var potion = new ConsumableItemInstance(def, "potion");
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RestoreHealth, Value = .25f });
            return potion;
        }
        T Definition<T>(string id) where T : ItemDefinition
        {
            var def = ScriptableObject.CreateInstance<T>(); definitions.Add(def);
            typeof(ItemDefinition).GetField("<Id>k__BackingField", Private).SetValue(def, id); return def;
        }
        CharacterInfoItemView Add(ItemInstance item)
        { character.Inventory.Items.Add(item); window.Refresh(); return Rows().Single(v => v.Item == item); }
        List<CharacterInfoItemView> Rows() => Get<List<CharacterInfoItemView>>(window, "inventoryViews");
        CharacterInfoEquipmentSlotView Slot(EquipmentItemInstance item) => window.GetComponentsInChildren<CharacterInfoEquipmentSlotView>()
            .Single(s => s.Slot == item.EquipmentSlot);
        static PointerEventData ClickData(int count) => new(EventSystem.current)
            { button = PointerEventData.InputButton.Left, clickCount = count, eligibleForClick = true };
        static void Click(Component target, PointerEventData data) => ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler);
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
