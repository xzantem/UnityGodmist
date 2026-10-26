using System;
using System.IO;
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
using Godmist.Views.Characters;
using Godmist.Views.Exploration;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CharacterCardPresentationTests
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject canvasObject, eventsObject;
        CharacterInfoWindowView view;
        CharacterInstance character;
        GameSession session;
        static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Fields).GetValue(owner);
        static void Invoke(object owner,string name)=>owner.GetType().GetMethod(name,Fields).Invoke(owner,null);
        [SetUp] public void Setup()
        {
            canvasObject=new GameObject("Card test",typeof(RectTransform),typeof(Canvas));
            canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            canvasObject.AddComponent<UIWorkspaceWindowManager>();
            eventsObject=new GameObject("Card events",typeof(EventSystem));Invoke(eventsObject.GetComponent<EventSystem>(),"OnEnable");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab");
            view=Object.Instantiate(prefab,canvasObject.transform).GetComponent<CharacterInfoWindowView>();
            Invoke(view.GetComponent<UIWorkspaceWindow>(),"Awake");Invoke(view,"Awake");
            character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            session=new GameSession(GameModeState.Exploration);session.TryRegisterCharacter(character);
            view.Initialize(session,character.CharacterId);Invoke(view,"OnEnable");
        }
        [TearDown] public void Cleanup()
        {Invoke(eventsObject.GetComponent<EventSystem>(),"OnDisable");Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(eventsObject);}
        [Test] public void EmptyInventoryHasAnExplanationAndTheViewportHasUsableArea()
        {
            Assert.That(Get<TMP_Text>(view,"inventoryEmptyText").text,Is.EqualTo("Plecak jest pusty"));
            Assert.That(Get<TMP_Text>(view,"inventoryEmptyText").gameObject.activeSelf,Is.True);
            Canvas.ForceUpdateCanvases();
            var viewport=view.GetComponentInChildren<ScrollRect>(true).viewport;
            Assert.That(viewport.rect.width,Is.GreaterThan(300));Assert.That(viewport.rect.height,Is.GreaterThan(300));
            Assert.That(Get<TextMeshProUGUI>(view,"headerTitleText").text,Is.EqualTo(character.Definition.DisplayName));
        }
        [Test] public void SummaryUsesPolishNamesAndExperienceAtMaximumIsFull()
        {
            Assert.That(Get<TextMeshProUGUI>(view,"identityText").text,Does.Contain("Zwiadowca"));
            Assert.That(Get<TextMeshProUGUI>(view,"healthResourceText").text,Does.Contain("Impet"));
            Assert.That(Get<TextMeshProUGUI>(view,"honorAvailabilityText").text,Does.Contain("Dostępny").And.Not.Contain("Alive"));
            character.Level=CharacterProgressionSystem.MaximumLevel;view.Refresh();
            Assert.That(Get<Image>(view,"experienceFill").fillAmount,Is.EqualTo(1));
            Assert.That(Get<TextMeshProUGUI>(view,"experienceValueText").text,Is.EqualTo("maksimum"));
        }
        [Test] public void InventoryScrollbarHandleFitsItsTrackAndStillScrollsTheList()
        {
            var def=AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:EquipmentItemDefinition")[0]));
            for(var i=0;i<40;i++)character.Inventory.Items.Add(new EquipmentItemInstance{Definition=def,InstanceId="scroll item "+i});
            view.Refresh();
            var scroll=Get<RectTransform>(view,"inventoryScrollRect").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.Rebuild(CanvasUpdate.PostLayout);Invoke(scroll,"LateUpdate");Canvas.ForceUpdateCanvases();
            var bar=scroll.verticalScrollbar;
            Assert.That(bar.gameObject.activeInHierarchy,Is.True);
            Assert.That(bar.handleRect.rect.width,Is.LessThanOrEqualTo(((RectTransform)bar.transform).rect.width+.01f));
            var top=scroll.content.anchoredPosition.y;
            bar.value=0;Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(top+100));
        }
        [Test] public void EquippedItemRequiresExplicitUnequipAndCannotBeRemovedDuringCombat()
        {
            var slot=EquipForPresentation();
            var item=(EquipmentItemInstance)slot.Item;
            Get<Button>(slot,"button").onClick.Invoke();
            Assert.That(character.Equipment.GetItem(slot.Slot),Is.SameAs(item));
            Assert.That(Get<Image>(slot,"selectionBorder").enabled,Is.True);
            slot.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right,position=Vector2.zero});
            var menu=Get<PartyRailMenuView>(Get<ItemActionPopupView>(view,"itemActionPopup"),"menu");Assert.That(menu,Is.Not.Null);
            var action=menu.GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text=="Zdejmij");
            Assert.That(action.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Zdejmij"));
            var combat=new CombatSession{IsSeamless=true};combat.Units.Add(new BattleUnit{Character=character,IsPlayerSide=true});Get<List<CombatSession>>(session,"combats").Add(combat);
            action.onClick.Invoke();Assert.That(character.Equipment.GetItem(slot.Slot),Is.SameAs(item));
            view.ShowEquipmentMenu(slot,Vector2.zero);Assert.That(menu.GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text=="Zdejmij").interactable,Is.False);
        }
        [Test] public void RightButtonDragDoesNotStartUnequippingAndClosingCardClosesItsMenu()
        {
            var slot=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().First();
            var item=new EquipmentItemInstance{Definition=DefinitionFor(slot.Slot),InstanceId="card equipped"};character.Equipment.SetItem(slot.Slot,item);slot.Bind(item);
            slot.OnBeginDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});Assert.That(Get<ItemInstance>(view,"draggedItem"),Is.Null);
            typeof(CharacterInfoWindowView).GetField("dragGhostTemplate",Fields).SetValue(view,null);
            slot.OnBeginDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
            slot.OnEndDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});Assert.That(Get<ItemInstance>(view,"draggedItem"),Is.SameAs(item));view.EndDrag();
            view.ShowEquipmentMenu(slot,Vector2.zero);var menu=Get<PartyRailMenuView>(Get<ItemActionPopupView>(view,"itemActionPopup"),"menu");
            Invoke(view,"OnDisable");Assert.That(menu.gameObject.activeSelf,Is.False);
        }
        [Test] public void ExplicitMenuActionMovesTheItemIntoInventory()
        {
            var def=AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).First();
            var item=new EquipmentItemInstance{Definition=def,InstanceId="explicit unequip"};
            var slot=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().Single(s=>s.Slot==item.EquipmentSlot);
            character.Equipment.SetItem(slot.Slot,item);slot.Bind(item);view.ShowEquipmentMenu(slot,Vector2.zero);
            Get<PartyRailMenuView>(Get<ItemActionPopupView>(view,"itemActionPopup"),"menu").GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text=="Zdejmij").onClick.Invoke();
            Assert.That(character.Equipment.GetItem(slot.Slot),Is.Null);Assert.That(character.Inventory.Items,Does.Contain(item));
            Assert.That(slot.Item,Is.Null);
        }
        [Test] public void AnOpenMenuCannotRemoveANewItemInTheSameSlot()
        {
            var slot=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().First();
            var def=DefinitionFor(slot.Slot);
            var old=new EquipmentItemInstance{Definition=def,InstanceId="old"};var replacement=new EquipmentItemInstance{Definition=def,InstanceId="replacement"};
            character.Equipment.SetItem(slot.Slot,old);slot.Bind(old);view.ShowEquipmentMenu(slot,Vector2.zero);
            character.Equipment.SetItem(slot.Slot,replacement);
            Get<PartyRailMenuView>(Get<ItemActionPopupView>(view,"itemActionPopup"),"menu").GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text=="Zdejmij").onClick.Invoke();
            Assert.That(character.Equipment.GetItem(slot.Slot),Is.SameAs(replacement));Assert.That(character.Inventory.Items,Is.Empty);
        }
        [Test] public void DraggingEquippedItemIntoInventoryStillUnequipsIt()
        {
            var def=AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).First();
            var item=new EquipmentItemInstance{Definition=def,InstanceId="drag unequip"};
            var slot=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().Single(s=>s.Slot==item.EquipmentSlot);
            character.Equipment.SetItem(slot.Slot,item);slot.Bind(item);
            typeof(CharacterInfoWindowView).GetField("dragGhostTemplate",Fields).SetValue(view,null);
            slot.OnBeginDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
            view.TryHandleInventoryDrop();Assert.That(character.Equipment.GetItem(slot.Slot),Is.Null);Assert.That(character.Inventory.Items,Does.Contain(item));
        }
        [Test] public void CurrentVitalsRefreshWithoutRebuildingInventory()
        {
            character.CurrentHealth=50;character.ResourcePool.CurrentValue=15;Invoke(view,"LateUpdate");
            Assert.That(Get<TextMeshProUGUI>(view,"healthResourceText").text,Does.Contain("50/").And.Contain("15/"));
            Assert.That(Get<Image>(view,"healthFill").fillAmount,Is.EqualTo(50/character.Stats.MaxHealth.GetValue()).Within(.001f));
            Assert.That(Get<Image>(view,"resourceFill").fillAmount,Is.EqualTo(15f/character.ResourcePool.MaxValue).Within(.001f));
        }
        [Test] public void UnequipMenuExplainsWhenInventoryHasNoRoom()
        {
            var def=AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).First(d=>d.Weight>0);
            var item=new EquipmentItemInstance{Definition=def,InstanceId="heavy item"};character.Inventory.MaxWeight=0;
            var slot=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().Single(s=>s.Slot==item.EquipmentSlot);
            character.Equipment.SetItem(slot.Slot,item);slot.Bind(item);view.ShowEquipmentMenu(slot,Vector2.zero);
            var menu=Get<PartyRailMenuView>(Get<ItemActionPopupView>(view,"itemActionPopup"),"menu");
            Assert.That(menu.GetComponentsInChildren<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>().text=="Zdejmij").interactable,Is.False);
            Assert.That(Get<TMP_Text>(menu,"hintText").text,Does.Contain("Brak miejsca"));
        }
        [Test] public void FilteringDistinguishesNoMatchesFromAnEmptyInventoryAndCanBeCleared()
        {
            var def=AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).First();
            character.Inventory.Items.Add(new EquipmentItemInstance{Definition=def,InstanceId="search item"});
            var search=Get<TMP_InputField>(view,"inventorySearchInput");search.text="xyz-no-match";
            Assert.That(Get<TMP_Text>(view,"inventoryEmptyText").text,Is.EqualTo("Brak pasujących przedmiotów"));
            Assert.That(Get<Button>(view,"clearSearchButton").gameObject.activeSelf,Is.True);
            view.ClearInventorySearch();Assert.That(search.text,Is.Empty);
            Assert.That(Get<TMP_Text>(view,"inventoryEmptyText").gameObject.activeSelf,Is.False);
            Assert.That(Get<Transform>(view,"inventoryRoot").GetComponentsInChildren<CharacterInfoItemView>().Length,Is.EqualTo(1));
        }
        [Test] public void ClickingEquippedItemPinsDetailsAndClosingRestoresInventory()
        {
            var slot=EquipForPresentation();
            Get<Button>(slot,"button").onClick.Invoke();
            Assert.That(Get<GameObject>(view,"itemDetailsPanel").activeSelf,Is.True);
            var itemTitle=Get<TMP_Text>(view,"itemDetailsTitle");itemTitle.ForceMeshUpdate(true,true);
            Assert.That(itemTitle.GetParsedText(),Is.EqualTo(slot.Item.DisplayName));
            Assert.That(Get<RectTransform>(view,"inventoryScrollRect").offsetMin.y,Is.EqualTo(406));
            Get<Button>(view,"closeItemDetailsButton").onClick.Invoke();
            Assert.That(Get<GameObject>(view,"itemDetailsPanel").activeSelf,Is.False);
            Assert.That(Get<RectTransform>(view,"inventoryScrollRect").offsetMin.y,Is.EqualTo(86));
            Assert.That(Get<Image>(slot,"selectionBorder").enabled,Is.False);
        }
        [Test] public void ReplacingPinnedItemClearsDetailsAndEmptySlotsHaveGhostIcons()
        {
            var slot=EquipForPresentation();
            view.SelectEquipmentSlot(slot);character.Equipment.SetItem(slot.Slot,null);view.Refresh();
            Assert.That(Get<GameObject>(view,"itemDetailsPanel").activeSelf,Is.False);
            Assert.That(Get<Image>(slot,"emptySlotIcon").enabled,Is.True);
            Assert.That(Get<Image>(slot,"emptySlotIcon").sprite,Is.Not.Null);
            Assert.That(Get<Image>(slot,"icon").enabled,Is.False);
        }
        [TestCase("CD_Jerzy.asset")]
        [TestCase("CD_Wladimir_Test.asset")]
        [TestCase("CD_Mage.asset")]
        [TestCase("CD_Paladin.asset")]
        public void PaperDollUsesCurrentWorldFrontIdleWithoutChangingLayoutOrWorldScale(string asset)
        {
            var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/"+asset);
            var next=new CharacterInstanceFactory().Create(definition);session.TryRegisterCharacter(next);
            var paper=Get<Image>(view,"portrait");var size=paper.rectTransform.sizeDelta;
            var position=paper.rectTransform.anchoredPosition;var scale=paper.rectTransform.localScale;
            var face=definition.UiPortraitSprite;
            view.Initialize(session,next.CharacterId);
            Assert.That(CharacterAnimationResolver.TryResolveCharacter(definition.Id,Vector2.down,CharacterAnimationState.Idle,out var idle),Is.True);
            Assert.That(paper.sprite,Is.SameAs(idle.Frames[0]));
            Assert.That(paper.sprite,Is.Not.SameAs(definition.VisualSprite),"The legacy VS22 illustration is not the current world animation frame.");
            Assert.That(paper.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(paper.sprite.pixelsPerUnit,Is.EqualTo(32));
            Assert.That(paper.preserveAspect,Is.True);Assert.That(paper.enabled,Is.True);
            Assert.That(paper.rectTransform.sizeDelta,Is.EqualTo(size));
            Assert.That(paper.rectTransform.anchoredPosition,Is.EqualTo(position));
            Assert.That(paper.rectTransform.localScale,Is.EqualTo(scale));
            Assert.That(definition.UiPortraitSprite,Is.SameAs(face));
            var world=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"));
            try
            {
                world.Bind(next);world.FaceTowards(world.Position+Vector2.down);Invoke(world,"PlayIdleAnimation");
                Assert.That(world.BodyRenderer.sprite,Is.SameAs(paper.sprite));
                Assert.That(world.BodyRenderer.transform.lossyScale.x,Is.EqualTo(.7f).Within(.001f));
                view.Refresh();Assert.That(paper.sprite,Is.SameAs(idle.Frames[0]));
            }
            finally { Object.DestroyImmediate(world.gameObject); }
        }
        [TestCase(false)] [TestCase(true)]
        public void PaperDollRetainsWorldFallbackOrderForCharactersWithoutImportedPack(bool hasIdle)
        {
            var original=character.Definition;
            var definition=ScriptableObject.CreateInstance<CharacterDefinition>();
            try
            {
                typeof(CharacterDefinition).GetField("<Id>k__BackingField",Fields).SetValue(definition,"paperdoll.legacy");
                typeof(CharacterDefinition).GetField("<VisualSprite>k__BackingField",Fields).SetValue(definition,original.VisualSprite);
                CharacterAnimationResolver.TryResolveCharacter(original.Id,Vector2.down,CharacterAnimationState.Idle,out var idle);
                if(hasIdle)definition.IdleAnimationFrames.Add(idle.Frames[0]);
                character.Definition=definition;view.Refresh();
                Assert.That(Get<Image>(view,"portrait").sprite,Is.SameAs(hasIdle?idle.Frames[0]:original.VisualSprite));
            }
            finally { character.Definition=original;Object.DestroyImmediate(definition); }
        }
        [Test] public void PaperDollDoesNotRetainPreviousCharactersSpriteWhenNoVisualIsAvailable()
        {
            character.Definition=null;view.Refresh();
            Assert.That(Get<Image>(view,"portrait").sprite,Is.Null);
            Assert.That(Get<Image>(view,"portrait").enabled,Is.False);
        }
        [Test] public void RenderCurrentPaperDollsForAllFourHeroes()
        {
            var cameraObject=new GameObject("Paperdoll render camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=450;camera.transform.position=new Vector3(0,0,-10);
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.035f,.04f);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var target=new RenderTexture(1280,900,24);var pixels=new Texture2D(1280,900,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                target.Create();camera.targetTexture=target;
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/character-card-model-pass-20261015"));Directory.CreateDirectory(output);
                foreach(var asset in new[]{"CD_Jerzy.asset","CD_Wladimir_Test.asset","CD_Mage.asset","CD_Paladin.asset"})
                {
                    var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/"+asset);
                    var next=new CharacterInstanceFactory().Create(definition);session.TryRegisterCharacter(next);view.Initialize(session,next.CharacterId);
                    foreach(var child in canvasObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,900),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,asset.Replace(".asset","-card.png")),pixels.EncodeToPNG());
                    var colors=pixels.GetPixels32();Assert.That(colors.Count(c=>c.r>100 && c.g>80),Is.GreaterThan(1000),"The rendered card must contain visible UI and character pixels.");
                }
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;target.Release();
                Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(cameraObject);
            }
        }

        static EquipmentItemDefinition DefinitionFor(EquipmentSlot slot) => AssetDatabase.FindAssets("t:EquipmentItemDefinition")
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>)
            .First(def => new EquipmentItemInstance { Definition=def }.EquipmentSlot == slot);
        CharacterInfoEquipmentSlotView EquipForPresentation()
        {
            var def=AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:EquipmentItemDefinition")[0]));
            var item=new EquipmentItemInstance{Definition=def,InstanceId="pinned native fixture"};character.Equipment.SetItem(item.EquipmentSlot,item);view.Refresh();
            return view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().Single(s=>s.Slot==item.EquipmentSlot);
        }
    }
}
