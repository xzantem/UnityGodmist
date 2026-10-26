using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class MainMenuSaveBrowserTests
    {
        private Scene scene;
        private SaveLoadPanelView view;
        private static GameSaveSummary Summary(string id,int minute,SaveSlotType type=SaveSlotType.Quick,string campaign="fixture")
        {
            var data=new GameSaveData{Gold=321,TrackedQuestId="quest.test"};
            data.Party.ActiveMemberIds.Add("jerzy");data.Characters.Add(new CharacterSaveData{CharacterId="jerzy",Level=7});
            return new GameSaveSummary("fixture",id,campaign,"Nazwa "+id,DateTime.MinValue,new DateTime(2026,10,4,5,minute,0,DateTimeKind.Utc),type,new GameSavePreview(data));
        }
        [SetUp] public void Open()
        {
            scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            var canvas=scene.GetRootGameObjects().Single(root=>root.name=="Canvas").GetComponent<Canvas>();
            // Headless EditMode has no Game View surface for an overlay canvas.
            // Preserve authoring units so ScrollRect can calculate world bounds.
            canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one;
            view=canvas.transform.Find("MainMenuRoot").GetComponent<SaveLoadPanelView>();
        }
        [TearDown] public void Close(){if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        private T Ref<T>(string field)where T:UnityEngine.Object=>new SerializedObject(view).FindProperty(field).objectReferenceValue as T;

        [Test] public void OpeningLoadSelectsNewestAndKeyboardSelectionClampsWithoutLoading()
        {
            view.Open(SaveLoadPanelMode.Load);
            view.ShowSummaries(new[]{Summary("old",1),Summary("new",50),Summary("middle",20)});
            Assert.That(view.SelectedSave.SaveId,Is.EqualTo("new"));Assert.That(Ref<Button>("loadButton").interactable,Is.True);
            view.MoveSelection(1);Assert.That(view.SelectedSave.SaveId,Is.EqualTo("middle"));
            view.MoveSelection(99);Assert.That(view.SelectedSave.SaveId,Is.EqualTo("old"));
            view.MoveSelection(-99);Assert.That(view.SelectedSave.SaveId,Is.EqualTo("new"));
            Assert.That(Ref<TMP_Text>("detailsText").text,Does.Contain("321").And.Not.Contain("kampania"));
            var captions=new SerializedObject(view).FindProperty("partyCaptionTexts");
            Assert.That(((TMP_Text)captions.GetArrayElementAtIndex(0).objectReferenceValue).text,Does.Contain("Poz. 7"));
        }
        [Test] public void RefreshPreservesSelectionByIdAndEmptyListClearsActionsAndPortraits()
        {
            view.Open(SaveLoadPanelMode.Load);view.ShowSummaries(new[]{Summary("new",50),Summary("old",1)});view.MoveSelection(1);
            view.ShowSummaries(new[]{Summary("new",55),Summary("old",2)});Assert.That(view.SelectedSave.SaveId,Is.EqualTo("old"));
            view.ShowSummaries(Array.Empty<GameSaveSummary>());Assert.That(view.SelectedSave,Is.Null);Assert.That(Ref<Button>("loadButton").interactable,Is.False);
            Assert.That(Ref<TMP_Text>("detailsText").text,Does.Contain("Brak zapisów"));Assert.That(Ref<Button>("deleteButton").gameObject.activeSelf,Is.False);
        }
        [Test] public void ArrowSelectionScrollsToLastEntryAndBackWithoutClippingFirstRow()
        {
            view.Open(SaveLoadPanelMode.Load);view.ShowSummaries(Enumerable.Range(0,12).Select(index=>Summary(index.ToString(),index)).ToArray());
            // Preview scenes do not run the normal canvas update loop.
            var scroll=Ref<ScrollRect>("saveScroll");
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.transform.parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            view.MoveSelection(99);Assert.That(Ref<ScrollRect>("saveScroll").verticalNormalizedPosition,Is.LessThan(1));
            view.MoveSelection(-99);Assert.That(Ref<ScrollRect>("saveScroll").verticalNormalizedPosition,Is.EqualTo(1).Within(.001));
        }
        [Test] public void SharedSaveModeDoesNotPreselectAnExistingSaveAndOnlyManualSaveCanBeDeleted()
        {
            view.Open(SaveLoadPanelMode.Save);view.ShowSummaries(new[]{Summary("manual",50,SaveSlotType.Manual),Summary("auto",1)});
            Assert.That(view.SelectedSave,Is.Null);view.MoveSelection(1);Assert.That(Ref<Button>("deleteButton").gameObject.activeSelf,Is.True);
            view.MoveSelection(1);Assert.That(Ref<Button>("deleteButton").gameObject.activeSelf,Is.False);
        }
        [Test] public void SharedBrowserEscapeReturnsToOwnerOnceAndClearsSelection()
        {
            var closed = 0; view.Closed += () => closed++;
            view.Open(SaveLoadPanelMode.Load); view.ShowSummaries(new[] { Summary("new", 50) });
            Assert.That(view.HandlesKeyboardNavigation, Is.True);
            view.HandleEscape(); view.HandleEscape();
            Assert.That(view.IsOpen, Is.False); Assert.That(view.SelectedSave, Is.Null);
            Assert.That(closed, Is.EqualTo(1));
        }
        [Test] public void SwitchingSharedBrowserModesRestoresMatchingActionsWithoutKeepingTypedName()
        {
            view.Open(SaveLoadPanelMode.Save); Ref<TMP_InputField>("saveNameInput").text = "Nowy zapis";
            view.Open(SaveLoadPanelMode.Load); view.ShowSummaries(new[] { Summary("manual", 50, SaveSlotType.Manual) });
            Assert.That(Ref<GameObject>("saveNameGroup").activeSelf, Is.False);
            Assert.That(Ref<Button>("saveButton").gameObject.activeSelf, Is.False);
            Assert.That(Ref<Button>("loadButton").gameObject.activeSelf, Is.True);
            view.Open(SaveLoadPanelMode.Save);
            Assert.That(Ref<TMP_InputField>("saveNameInput").text, Is.Empty);
            Assert.That(Ref<GameObject>("saveNameGroup").activeSelf, Is.True);
            Assert.That(Ref<Button>("loadButton").gameObject.activeSelf, Is.False);
        }
        [Test] public void ScrollbarThumbHasMinimumHeightAndExpandedRaycastArea()
        {
            var scrollbar=Ref<ScrollRect>("saveScroll").verticalScrollbar;
            var graphic=scrollbar.targetGraphic;
            Assert.That(scrollbar.handleRect.sizeDelta.y,Is.GreaterThanOrEqualTo(24));
            Assert.That(graphic.raycastTarget,Is.True);
            Assert.That(graphic.raycastPadding.x,Is.LessThan(0));
            Assert.That(graphic.raycastPadding.z,Is.LessThan(0));
        }
        [Test] public void SaveRowSelectionKeepsArtworkUntintedAndClickSelectsAnotherRow()
        {
            view.Open(SaveLoadPanelMode.Load);view.ShowSummaries(new[]{Summary("new",50),Summary("old",1)});
            var entries=Ref<Transform>("saveListContent").GetComponentsInChildren<SaveListEntryView>();
            var selectedImage=new SerializedObject(entries[0]).FindProperty("background").objectReferenceValue as Image;
            Assert.That(selectedImage.sprite.name,Is.EqualTo("list_row_selected"));Assert.That(selectedImage.color,Is.EqualTo(Color.white));
            entries[1].GetComponent<Button>().onClick.Invoke();Assert.That(view.SelectedSave.SaveId,Is.EqualTo("old"));
            Assert.That(selectedImage.sprite.name,Is.EqualTo("list_row_normal"));Assert.That(selectedImage.color,Is.EqualTo(Color.white));
        }
        [Test] public void FirstDeleteClickRequiresConfirmationAndChangingSelectionCancelsIt()
        {
            // Invoke only this view's callback setup; SendMessage("Awake") is a native
            // lifecycle operation Unity forbids in an EditMode preview scene.
            typeof(SaveLoadPanelView).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,null);
            view.Open(SaveLoadPanelMode.Load);
            view.ShowSummaries(new[]{Summary("manual",50,SaveSlotType.Manual),Summary("auto",1)});
            Ref<Button>("deleteButton").onClick.Invoke();
            Assert.That(Ref<TMP_Text>("messageText").text,Does.Contain("Usunąć zapis"));
            Assert.That(Ref<Button>("deleteButton").GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Potwierdź usunięcie"));
            Assert.That(view.SelectedSave.SaveId,Is.EqualTo("manual"));
            view.MoveSelection(1);view.MoveSelection(-1);
            Assert.That(Ref<Button>("deleteButton").GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Usuń zapis"));
        }
        [Test] public void PreviewCopiesOnlyActivePartyAndRetainsLeaderWithoutMutatingSnapshot()
        {
            var data=new GameSaveData{Gold=99};data.Party.ActiveLeaderId="b";data.Party.ActiveMemberIds.AddRange(new[]{"a","b","a","missing"});
            data.Characters.AddRange(new[]{new CharacterSaveData{CharacterId="a",Level=2},new CharacterSaveData{CharacterId="b",Level=8},new CharacterSaveData{CharacterId="bench",Level=40}});
            var preview=new GameSavePreview(data);Assert.That(preview.Party.Select(member=>member.CharacterId),Is.EqualTo(new[]{"b","a"}));
            data.Characters[1].Level=30;data.Gold=1;Assert.That(preview.Party[0].Level,Is.EqualTo(8));Assert.That(preview.Gold,Is.EqualTo(99));
            Assert.That(new GameSavePreview(new GameSaveData{Party=null,Characters=null}).Party,Is.Empty);
        }
        [Test] public void LegacySummaryWithoutPreviewAndUserRichTextNamesRemainReadable()
        {
            var old=new GameSaveSummary("file","id","legacy","<color=red>name</color>",DateTime.MinValue,DateTime.MinValue,SaveSlotType.Manual);
            Assert.That(SaveSummaryPresentation.Details(old,null),Does.Contain("nie zawiera danych podglądu").And.Not.Contain("<color=red>"));
        }
        [Test] public void CampaignSelectionIsolatedAndHistoryKeepsAllManualAndQuickSaves()
        {
            view.Open(SaveLoadPanelMode.Load);
            var saves=new[]{Summary("a-new",50,SaveSlotType.Autosave,"a"),Summary("a-old",20,SaveSlotType.Autosave,"a"),Summary("a-manual",10,SaveSlotType.Manual,"a"),Summary("a-quick",15,SaveSlotType.Quick,"a"),Summary("b",1,SaveSlotType.Manual,"b")};
            view.ShowSummaries(saves);
            Assert.That(Ref<TMP_Dropdown>("campaignDropdown").options.Count,Is.EqualTo(2));
            Assert.That(view.SelectedSave.SaveId,Is.EqualTo("a-new"));
            Assert.That(Ref<Transform>("saveListContent").childCount,Is.EqualTo(3));
            view.ToggleHistory();Assert.That(Ref<Transform>("saveListContent").childCount,Is.EqualTo(4));
            view.MoveSelection(1);Assert.That(view.SelectedSave.SaveId,Is.EqualTo("a-old"));
            view.ToggleHistory();Assert.That(view.SelectedSave.SaveId,Is.EqualTo("a-new"));
            Ref<TMP_Dropdown>("campaignDropdown").onValueChanged.AddListener(view.SelectCampaign);
            Ref<TMP_Dropdown>("campaignDropdown").value=1;
            Assert.That(view.SelectedSave.SaveId,Is.EqualTo("b"));Assert.That(Ref<Transform>("saveListContent").childCount,Is.EqualTo(1));
            Assert.That(Ref<Button>("historyButton").gameObject.activeSelf,Is.False);
            view.ShowSummaries(saves);Assert.That(view.SelectedSave.SaveId,Is.EqualTo("b"));
        }
        [Test] public void MissingCampaignIdsStaySeparateAndEmptyStateClearsStructuredPreview()
        {
            var a=Summary("a",1,SaveSlotType.Manual,null);var b=Summary("b",2,SaveSlotType.Manual,null);
            Assert.That(SaveSummaryPresentation.Campaigns(new[]{a,b},null).Count,Is.EqualTo(2));
            view.Open(SaveLoadPanelMode.Load);view.ShowSummaries(new[]{a});view.ShowSummaries(Array.Empty<GameSaveSummary>());
            Assert.That(Ref<TMP_Text>("previewDateText").text,Is.Empty);
            Assert.That(Ref<TMP_Text>("partyHeadingText").text,Is.Empty);
            Assert.That(Ref<Button>("historyButton").gameObject.activeSelf,Is.False);
            Assert.That(Ref<TMP_Dropdown>("campaignDropdown").interactable,Is.False);
        }
        [Test] public void CampaignLabelsDisambiguateSameLeaderAndMinuteWithoutShowingIds()
        {
            var campaigns=SaveSummaryPresentation.Campaigns(new[]{Summary("a",1,SaveSlotType.Manual,"secret-a"),Summary("b",1,SaveSlotType.Manual,"secret-b")},null);
            Assert.That(campaigns.Select(campaign=>campaign.Label).Distinct().Count(),Is.EqualTo(2));
            Assert.That(campaigns.All(campaign=>!campaign.Label.Contains("secret-")),Is.True);
            Assert.That(campaigns.All(campaign=>campaign.Label.Contains("jerzy")),Is.True);
        }
        [Test] public void SharedPausePanelWithoutCampaignControlsRetainsFlatList()
        {
            var settings=new SerializedObject(view);settings.FindProperty("campaignDropdown").objectReferenceValue=null;settings.FindProperty("previewLocationText").objectReferenceValue=null;settings.ApplyModifiedPropertiesWithoutUndo();
            view.Open(SaveLoadPanelMode.Load);view.ShowSummaries(new[]{Summary("a",1,SaveSlotType.Autosave,"a"),Summary("b",2,SaveSlotType.Autosave,"b")});
            Assert.That(Ref<Transform>("saveListContent").childCount,Is.EqualTo(2));
            Assert.That(Ref<TMP_Text>("detailsText").text,Does.Contain("Drużyna w chwili zapisu"));
        }
    }
}
