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
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ItemPortraitTransferTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject canvas, events;
        CharacterInfoWindowView window;
        CharacterInfoPartyPortraitView portrait;
        ItemActionPopupView popup;
        UIWorkspaceTooltipView tooltip;
        CharacterInstance owner, recipient;
        GameSession session;
        ItemDefinition material;
        readonly List<Object> assets = new();

        [SetUp] public void Setup()
        {
            canvas = new GameObject("Transfer test", typeof(RectTransform), typeof(Canvas));
            canvas.AddComponent<UIWorkspaceWindowManager>();
            events = new GameObject("Transfer events", typeof(EventSystem));
            Invoke(events.GetComponent<EventSystem>(), "OnEnable");
            window = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab"), canvas.transform)
                .GetComponent<CharacterInfoWindowView>();
            Invoke(window.GetComponent<UIWorkspaceWindow>(), "Awake"); Invoke(window, "Awake");
            owner = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            recipient = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset"));
            session = new GameSession(GameModeState.Exploration);
            foreach (var character in new[]{owner,recipient})
            { session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId); }
            window.Initialize(session, owner.CharacterId); Invoke(window,"OnEnable");
            popup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemActionPopupView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ItemActionPopup.prefab"),canvas.transform);
            Invoke(popup,"Awake"); Set(window,"itemActionPopup",popup);
            tooltip=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIWorkspaceTooltipView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),canvas.transform);
            Invoke(tooltip,"Awake"); Invoke(tooltip,"OnEnable");
            var rail = new GameObject("Rail",typeof(RectTransform)).AddComponent<CharacterInfoPartyRailView>();
            rail.transform.SetParent(canvas.transform); Set(rail,"session",session);
            portrait = new GameObject("Recipient",typeof(RectTransform)).AddComponent<CharacterInfoPartyPortraitView>();
            portrait.transform.SetParent(rail.transform); Set(portrait,"character",recipient); Set(portrait,"rail",rail);
            material=ScriptableObject.CreateInstance<ItemDefinition>(); assets.Add(material);
            Set(material,"<Id>k__BackingField","transfer.material"); Set(material,"<IsStackable>k__BackingField",true);
            Set(material,"<Weight>k__BackingField",1f);
        }
        [TearDown] public void Cleanup()
        {
            Invoke(window,"OnDisable"); Invoke(tooltip,"OnDisable"); Invoke(events.GetComponent<EventSystem>(),"OnDisable");
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events);
            foreach(var asset in assets) Object.DestroyImmediate(asset); assets.Clear();
        }
        ItemInstance Add(string id) { var item=new ItemInstance(material,id); owner.Inventory.Items.Add(item); return item; }
        PointerEventData Drag(ItemInstance item)
        {
            window.Refresh();
            var row=Get<List<CharacterInfoItemView>>(window,"inventoryViews").Single(v=>v.Item==item);
            var data=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,pointerDrag=row.gameObject,dragging=true};
            row.OnBeginDrag(data); return data;
        }
        void Confirm(int count)
        { Get<TMP_InputField>(popup,"quantityInput").text=count.ToString(); Get<Button>(popup,"confirmButton").onClick.Invoke(); }
        void Combat(CharacterInstance character)
        { var combat=new CombatSession { IsSeamless=true }; combat.Units.Add(new BattleUnit { Character=character,IsPlayerSide=true }); Get<List<CombatSession>>(session,"combats").Add(combat); }

        [Test] public void RecipientPreviewRequiresRoomForOneUnitWithoutMovingTheStack()
        {
            var a=Add("a"); Add("b"); Add("c"); Drag(a);
            recipient.Inventory.MaxWeight=1;
            Assert.That(window.CanPreviewRecipientDrop(session,recipient.CharacterId,a,out var reason),Is.True);
            Assert.That(reason,Does.Contain("liczbę sztuk"));
            recipient.Inventory.MaxWeight=.5f;
            Assert.That(window.CanPreviewRecipientDrop(session,recipient.CharacterId,a,out reason),Is.False);
            Assert.That(reason,Does.Contain("Brak miejsca"));
            Assert.That(owner.Inventory.Items,Has.Count.EqualTo(3));Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [TestCase("dead")] [TestCase("combat")] [TestCase("self")] [TestCase("foreign")]
        public void RecipientPreviewRejectsUnavailableDestinations(string state)
        {
            var a=Add("a"); Drag(a);
            if(state=="dead") recipient.LifeState=CharacterLifeState.Dead;
            if(state=="combat") Combat(recipient);
            var destination=state=="foreign"?new GameSession():session;
            var id=state=="self"?owner.CharacterId:recipient.CharacterId;
            Assert.That(window.CanPreviewRecipientDrop(destination,id,a,out var reason),Is.False);
            Assert.That(reason,Is.Not.Empty);Assert.That(owner.Inventory.Items,Does.Contain(a));
        }
        [Test] public void LeavingPortraitAndCancellingClearOnlyTheRecipientPreview()
        {
            var image=new GameObject("Portrait",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(portrait.transform,false);Set(portrait,"portrait",image);
            var a=Add("a");var data=Drag(a);
            data.pointerCurrentRaycast=new RaycastResult {gameObject=portrait.gameObject};window.UpdateDrag(data);
            var border=Get<ItemRarityBorderGraphic>(portrait,"itemDropBorder");
            Assert.That(border,Is.Not.Null);Assert.That(border.enabled,Is.True);
            Assert.That(border.color,Is.EqualTo(Godmist.Views.HighlightStyle.Friendly));
            data.pointerCurrentRaycast=new RaycastResult();window.UpdateDrag(data);
            Assert.That(border.enabled,Is.False);
            data.pointerCurrentRaycast=new RaycastResult {gameObject=portrait.gameObject};window.UpdateDrag(data);
            Assert.That(border.enabled,Is.True);window.EndDrag();Assert.That(border.enabled,Is.False);
            Assert.That(window.CanPreviewRecipientDrop(session,recipient.CharacterId,a,out _),Is.False);
        }

        [Test] public void QuantityPreviewShowsWeightDisablesOverflowAndDoesNotClampTheChoice()
        {
            var a=Add("a"); Add("b"); Add("c"); recipient.Inventory.MaxWeight=1;
            portrait.OnDrop(Drag(a));
            Assert.That(Get<TMP_Text>(popup,"quantityHint").text,Does.Contain("Waga odbiorcy"));
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.True);
            Get<TMP_InputField>(popup,"quantityInput").text="2";
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.False);
            Assert.That(Get<TMP_InputField>(popup,"quantityInput").text,Is.EqualTo("2"));
            Assert.That(Get<TMP_Text>(popup,"quantityError").text,Does.Contain("limit wagi"));
            Assert.That(owner.Inventory.Items,Has.Count.EqualTo(3)); Assert.That(recipient.Inventory.Items,Is.Empty);
            recipient.Inventory.MaxWeight=null;
            Confirm(2);
            Assert.That(recipient.Inventory.Items,Has.Count.EqualTo(2));Assert.That(owner.Inventory.Items,Has.Count.EqualTo(1));
        }
        [Test] public void PreviewAndTransferShareWeightAndStaleSelectionValidation()
        {
            var a=Add("a");var b=Add("b");var ids=new[]{a.InstanceId,b.InstanceId};
            var actions=new Godmist.Systems.Items.ItemOwnershipActions();
            recipient.Inventory.MaxWeight=1;
            Assert.That(actions.PreviewTransfer(session,owner,a,ids,2,recipient,false,true,out var reason,out var weight),Is.False);
            Assert.That(weight,Is.EqualTo(2));
            Assert.That(actions.TryTransfer(session,owner,a,ids,2,recipient,false,true,out var actualReason),Is.False);
            Assert.That(actualReason,Is.EqualTo(reason)); Assert.That(owner.Inventory.Items,Has.Count.EqualTo(2));
            owner.Inventory.Items.Remove(b);recipient.Inventory.MaxWeight=null;
            Assert.That(actions.PreviewTransfer(session,owner,a,ids,2,recipient,false,true,out _,out weight),Is.False);
            Assert.That(weight,Is.Null);Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [TestCase(0f)] [TestCase(-2f)] public void ZeroAndNegativeDefinitionWeightUseTheExistingZeroFloor(float weight)
        {
            Set(material,"<Weight>k__BackingField",weight);
            var a=Add("a");Add("b");recipient.Inventory.MaxWeight=0;
            portrait.OnDrop(Drag(a));Get<TMP_InputField>(popup,"quantityInput").text="2";
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.True);Confirm(2);
            Assert.That(recipient.Inventory.Items,Has.Count.EqualTo(2));
        }

        [Test] public void PortraitStackDropRequestsQuantityAndPreservesExactInstances()
        {
            var a=Add("a"); var b=Add("b"); var c=Add("c");
            portrait.OnDrop(Drag(a));
            Assert.That(owner.Inventory.Items,Does.Contain(a)); Assert.That(recipient.Inventory.Items,Is.Empty);
            Assert.That(popup.IsModalOpen,Is.True); Assert.That(Get<ItemInstance>(window,"draggedItem"),Is.Null);
            Confirm(2);
            Assert.That(recipient.Inventory.Items,Is.EqualTo(new[]{a,b})); Assert.That(owner.Inventory.Items,Does.Contain(c));
            Assert.That(popup.IsModalOpen,Is.False);
        }
        [Test] public void CancelQuantityKeepsEveryItemAtSource()
        {
            var a=Add("a"); Add("b"); portrait.OnDrop(Drag(a));
            Get<Button>(popup,"cancelButton").onClick.Invoke();
            Assert.That(owner.Inventory.Items,Has.Count.EqualTo(2)); Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [TestCase("weight")] [TestCase("removed")] [TestCase("combat")] [TestCase("dead")]
        public void ConfirmationRevalidatesLiveStateAndFailureIsVisibleAndAtomic(string change)
        {
            var a=Add("a"); var b=Add("b"); portrait.OnDrop(Drag(a));
            if(change=="weight") recipient.Inventory.MaxWeight=1;
            if(change=="removed") owner.Inventory.Items.Remove(b);
            if(change=="combat") Combat(recipient);
            if(change=="dead") recipient.LifeState=CharacterLifeState.Dead;
            Confirm(2);
            Assert.That(recipient.Inventory.Items,Is.Empty); Assert.That(owner.Inventory.Items,Does.Contain(a));
            Assert.That(Get<TMP_Text>(popup,"quantityError").text,Is.Not.Empty);
            Assert.That(popup.IsModalOpen,Is.True);
        }
        [Test] public void EquippedItemGoesToRecipientBackpackWithoutAutoEquip()
        {
            var definition=AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).First(d=>d!=null);
            var item=new EquipmentItemInstance { Definition=definition, InstanceId="worn" };
            owner.Equipment.SetItem(item.EquipmentSlot,item); window.Refresh();
            var slot=window.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().Single(s=>s.Item==item);
            var data=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,pointerDrag=slot.gameObject,dragging=true};
            slot.OnBeginDrag(data); portrait.OnDrop(data); slot.OnEndDrag(data);
            Assert.That(owner.Equipment.GetItem(item.EquipmentSlot),Is.Null);
            Assert.That(recipient.Inventory.Items,Does.Contain(item)); Assert.That(recipient.Equipment.GetEquippedItems(),Is.Empty);
        }
        [TestCase("self")] [TestCase("foreign")] [TestCase("right")] [TestCase("cancelled")]
        public void InvalidDropDoesNotTransfer(string kind)
        {
            var a=Add("a"); var data=Drag(a);
            if(kind=="self") Set(portrait,"character",owner);
            if(kind=="foreign") Set(Get<CharacterInfoPartyRailView>(portrait,"rail"),"session",new GameSession());
            if(kind=="right") data.button=PointerEventData.InputButton.Right;
            if(kind=="cancelled") window.EndDrag();
            portrait.OnDrop(data);
            Assert.That(owner.Inventory.Items,Does.Contain(a)); Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [Test] public void QuantityUsesDragSnapshotAndIgnoresItemsAddedLater()
        {
            var a=Add("a"); Add("b"); var data=Drag(a); Add("late"); portrait.OnDrop(data);
            Assert.That(Get<int>(popup,"maximum"),Is.EqualTo(2)); Confirm(2);
            Assert.That(recipient.Inventory.Items,Has.Count.EqualTo(2)); Assert.That(owner.Inventory.Items.Any(i=>i.InstanceId=="late"),Is.True);
        }
        [Test] public void RebindingSourceWindowCancelsDragAndCannotTransferNewOwnersItems()
        {
            var a=Add("a"); var data=Drag(a); window.Initialize(session,recipient.CharacterId);
            portrait.OnDrop(data);
            Assert.That(owner.Inventory.Items,Does.Contain(a)); Assert.That(recipient.Inventory.Items,Is.Empty);
            Assert.That(Get<ItemInstance>(window,"draggedItem"),Is.Null);
        }
        [Test] public void DropIntoAnotherOpenInventoryUsesSameQuantityContract()
        {
            var a=Add("a"); Add("b"); var data=Drag(a);
            var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab"),canvas.transform)
                .GetComponent<CharacterInfoWindowView>();
            Invoke(target.GetComponent<UIWorkspaceWindow>(),"Awake"); Invoke(target,"Awake");
            target.Initialize(session,recipient.CharacterId);
            target.TryReceiveInventoryTransfer(window,a);
            Assert.That(popup.IsModalOpen,Is.True);Confirm(2);
            Assert.That(recipient.Inventory.Items.Any(i=>i==a),Is.True);
            Assert.That(owner.Inventory.Items.Any(i=>i.InstanceId=="a" || i.InstanceId=="b"),Is.False);
        }
        [Test] public void SelectingInventoryItemNeverCreatesDetailsOrChangesInventorySize()
        {
            var item=Add("selected");window.Refresh();
            var scroll=Get<RectTransform>(window,"inventoryScrollRect");var before=scroll.offsetMin;
            window.SelectInventoryItem(item);
            Assert.That(window.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="PinnedItemDetails"),Is.False);
            Assert.That(scroll.offsetMin,Is.EqualTo(before));Assert.That(owner.Inventory.Items,Does.Contain(item));
        }
        [Test, Explicit("Scoped Editor API authoring: remove duplicate item details, recover inventory viewport, and update the existing hint.")]
        public void ApplyPanelRemoval()
        {
            const string path="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel=root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="PinnedItemDetails");
                if(panel!=null)Object.DestroyImmediate(panel.gameObject);
                var view=root.GetComponent<CharacterInfoWindowView>();
                var scroll=Get<RectTransform>(view,"inventoryScrollRect");
                scroll.offsetMin=new Vector2(scroll.offsetMin.x,86);
                root.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="EquipmentHint").text="LPM: zaznacz • dwuklik: akcja\nPPM: opcje";
                Assert.That(new SerializedObject(view).FindProperty("itemDetailsPanel"),Is.Null);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void Invoke(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
    }
}
