using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Data.Items;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Systems.NPC;
using Godmist.Runtime.NPC;
using Godmist.Data.NPC;
using Godmist.Views.NPC;
using TMPro;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Godmist.Views.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class VS17MissionWindowTests
    {
        [Test]
        public void DungeonJournal_HasOwnWiredPanelUnderExplorationUi()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity", OpenSceneMode.Additive);
            try
            {
                var journal = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<QuestJournalPanelView>(true))
                    .Single();
                Assert.That(journal.transform.parent.name, Is.EqualTo("ExplorationUI"));
                var fields = new SerializedObject(journal);
                foreach (var name in new[] { "panelRoot", "closeButton", "activeQuestsContent",
                             "completedQuestsContent", "questButtonPrefab", "titleText",
                             "descriptionText", "stageText", "objectivesText", "trackButton",
                             "trackButtonText" })
                    Assert.That(fields.FindProperty(name).objectReferenceValue,
                        Is.Not.Null, name);
                var panel = (GameObject)fields.FindProperty("panelRoot").objectReferenceValue;
                Assert.That(panel.scene, Is.EqualTo(scene));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void SkillCastBanner_RendersFullNameAboveFloatingText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
            Assert.That(prefab, Is.Not.Null);
            var banner = prefab.transform.Find("SkillCastBanner");
            Assert.That(banner, Is.Not.Null);
            var canvas = banner.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.EqualTo(100));
            var label = banner.Find("SkillCastText").GetComponent("TextMeshProUGUI");
            Assert.That(label, Is.Not.Null);
            var textState = new SerializedObject(label);
            Assert.That(textState.FindProperty("m_enableAutoSizing").boolValue, Is.True);
            Assert.That(textState.FindProperty("m_TextWrappingMode").intValue, Is.Zero);
            Assert.That(textState.FindProperty("m_overflowMode").intValue, Is.Zero);
        }

        [Test]
        public void Arungard_UsesGuardMarkerAndMissionBoardWithoutIntroductionBanner()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/City/Arungard.unity",
                OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                Assert.That(objects.Any(item => item.name == "CampaignIntroduction"), Is.False);
                Assert.That(objects.Any(item => item.name == "MissionBoardButton"), Is.True);
                Assert.That(objects.Any(item => item.name == "QuestOfferMarker"), Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MissionWindow_UsesWorkspaceVariantAndSeparatedDetails()
        {
            const string folder = "Assets/_Project/Prefabs/UI/Workspace/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PF_UIW_CatacombsMissions.prefab");
            var baseWindow = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PF_UIW_Window.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(prefab), Is.EqualTo(baseWindow));
            Assert.That(prefab.GetComponent<UIWorkspaceWindow>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UIWorkspaceStyleApplier>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<UIWorkspaceWindowResizeHandle>(true), Is.Not.Null);

            var listSection = prefab.transform.Find("Content/MissionListSection");
            var detailsSection = prefab.transform.Find("Content/MissionDetailsSection");
            Assert.That(listSection, Is.Not.Null);
            Assert.That(detailsSection, Is.Not.Null);
            Assert.That(listSection.GetComponent<UIWorkspaceStyleApplier>(), Is.Not.Null);
            Assert.That(detailsSection.GetComponent<UIWorkspaceStyleApplier>(), Is.Not.Null);
            var listRight = listSection.GetComponent<RectTransform>().anchorMax.x;
            var detailsLeft = detailsSection.GetComponent<RectTransform>().anchorMin.x;
            Assert.That(listRight, Is.LessThan(detailsLeft));

            var scroll = listSection.Find("Content/MissionListScroll");
            Assert.That(scroll, Is.Not.Null, "Lista nie zawiera widoku przewijania UIW.");
            Assert.That(scroll.GetComponent<ScrollRect>(), Is.Not.Null);
            var content = scroll.GetComponent<ScrollRect>().content;
            Assert.That(content, Is.Not.Null);
            Assert.That(content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            var row = content.Find("MissionRowTemplate");
            Assert.That(row, Is.Not.Null, "Brak szablonu wiersza UIW.");
            Assert.That(row.GetComponent<Button>(), Is.Not.Null);
            Assert.That(row.Find("Icon").gameObject.activeSelf, Is.False);
            Assert.That(row.Find("TextRoot/Title"), Is.Not.Null);
            Assert.That(row.Find("TextRoot/Detail"), Is.Not.Null);
            var board = prefab.GetComponent<CatacombsMissionBoardView>();
            Assert.That(board, Is.Not.Null);
            var boardFields = new SerializedObject(board);
            Assert.That(boardFields.FindProperty("questList").objectReferenceValue, Is.EqualTo(content));
            Assert.That(boardFields.FindProperty("questButtonTemplate").objectReferenceValue,
                Is.EqualTo(row.GetComponent<Button>()));

            foreach(var field in new[]{"windowTitleText","titleText","metadataText","objectiveText","detailsText","progressRoot","progressText","progressFill","rewardsRoot","experienceText","goldText","honorText","rewardItem","messageText","actionButton","actionButtonText"})
                Assert.That(boardFields.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
            Assert.That(prefab.transform.Find("Header/Title").GetComponent<TextMeshProUGUI>().text,Does.Contain("Katakumby"));
            var action=(Button)boardFields.FindProperty("actionButton").objectReferenceValue;
            Assert.That(action.GetComponent<Image>().sprite.name,Is.EqualTo("button_normal"));
            Assert.That(action.transition,Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(detailsSection.Find("Content/MissionDetailScroll").GetComponent<ScrollRect>().content.GetComponent<VerticalLayoutGroup>(),Is.Not.Null);

        }

        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Ref<T>(CatacombsMissionBoardView view,string field) where T:UnityEngine.Object
            =>new SerializedObject(view).FindProperty(field).objectReferenceValue as T;
        static void Invoke(CatacombsMissionBoardView view,string method)=>typeof(CatacombsMissionBoardView).GetMethod(method,Private).Invoke(view,null);

        static QuestInstance RegisterMission(GameSession session, string name, QuestStatus status)
        {
            var definition=AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/ScriptableObjects/Quests/QD_Catacombs_Radiant_"+name+".asset");
            var quest=new QuestInstance(definition,status);
            quest.TrySetRadiantRewardOffer(new RadiantRewardOffer(1,1200,40,2,null));
            Assert.That(session.TryRegisterQuest(quest),Is.True);
            return quest;
        }
        static NpcInstance Guard()=>new NpcInstance {
            NpcId="npc.arungard.questgiver_01",
            Definition=AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_QuestGiver.asset")};

        [Test]
        public void GuardMarkerIgnoresLegacyIntroductionAfterAcceptingAllCurrentOffers()
        {
            var session=new GameSession(GameModeState.Exploration);var npc=Guard();
            var legacy=AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/ScriptableObjects/Quests/QD_InvestigateCatacombs.asset");
            session.TryRegisterQuest(new QuestInstance(legacy,QuestStatus.Available));
            Assert.That(QuestOfferAvailability.HasOffer(session,npc.Definition.BaseDialogue),Is.True,"Reproduce the old permanent marker.");
            RegisterMission(session,"Kill",QuestStatus.Available);RegisterMission(session,"Expedition",QuestStatus.Available);
            RegisterMission(session,"Boss",QuestStatus.Available);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.Offer));
            var service=new QuestService(session);
            Assert.That(service.TryStartQuest(CatacombsMissionBoard.KillQuestId),Is.True);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.Offer),"Another job is still available.");
            Assert.That(service.TryStartQuest(CatacombsMissionBoard.ExpeditionQuestId),Is.True);
            Assert.That(CatacombsMissionBoard.HasBossOffer(session),Is.False);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.None),"Active jobs and unavailable boss jobs must not show an offer marker.");
            Assert.That(QuestOfferAvailability.GetMarkerState(null,npc),Is.EqualTo(NpcQuestMarkerState.None));
        }

        [Test]
        public void GuardRewardMarkerTakesPriorityOverOffersAndClearsAfterClaim()
        {
            var session=new GameSession(GameModeState.Exploration);var npc=Guard();
            RegisterMission(session,"Kill",QuestStatus.Completed);
            RegisterMission(session,"Expedition",QuestStatus.Available);
            Assert.That(CatacombsMissionBoard.HasOffer(session),Is.True);
            Assert.That(CatacombsMissionBoard.HasReward(session),Is.True);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.Reward));
            var service=new QuestService(session);
            Assert.That(service.TryTurnInRadiantQuest(CatacombsMissionBoard.KillQuestId),Is.True);
            Assert.That(CatacombsMissionBoard.HasReward(session),Is.False);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.Offer),"The other unaccepted job remains available.");
            Assert.That(service.TryStartQuest(CatacombsMissionBoard.KillQuestId),Is.False,"The claimed slot waits for a completed expedition.");
            Assert.That(service.TryStartQuest(CatacombsMissionBoard.ExpeditionQuestId),Is.True);
            Assert.That(QuestOfferAvailability.GetMarkerState(session,npc),Is.EqualTo(NpcQuestMarkerState.None));
        }

        [TestCase(1f)][TestCase(1.75f)]
        public void GuardMarkerUsesDistinctRewardSymbolAndStaysAboveWholeSprite(float scale)
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/City/Arungard.unity",OpenSceneMode.Additive);
            try
            {
                var view=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<NpcView>(true)).Single(x=>x.name=="QuestGiver");
                typeof(NpcView).GetMethod("Awake",Private).Invoke(view,null);
                view.Initialize(null,Guard());view.transform.localScale*=scale;
                var fields=new SerializedObject(view);
                var marker=(GameObject)fields.FindProperty("questMarker").objectReferenceValue;
                var body=(SpriteRenderer)fields.FindProperty("bodyRenderer").objectReferenceValue;
                var text=marker.GetComponent<TextMeshPro>();
                var session=new GameSession(GameModeState.Exploration);
                var quest=RegisterMission(session,"Kill",QuestStatus.Available);
                var refresh=typeof(NpcView).GetMethod("RefreshQuestMarker",Private);
                var position=typeof(NpcView).GetMethod("PositionQuestMarker",Private);
                refresh.Invoke(view,new object[]{session});position.Invoke(view,null);
                Assert.That(marker.activeSelf,Is.True);Assert.That(text.text,Is.EqualTo("!"));var offerColour=text.color;
                quest.TrySetStatus(QuestStatus.Completed);refresh.Invoke(view,new object[]{session});position.Invoke(view,null);
                Assert.That(text.text,Is.EqualTo("?"));Assert.That(text.color,Is.Not.EqualTo(offerColour));
                var lower=text.transform.TransformPoint(new Vector3(text.textBounds.center.x,text.textBounds.min.y,0));
                Assert.That(lower.y,Is.EqualTo(body.bounds.max.y+.2f).Within(.001f));
                Assert.That(lower.x,Is.EqualTo(body.bounds.center.x).Within(.001f));
                var previous=marker.transform.position;position.Invoke(view,null);
                Assert.That(Vector3.Distance(marker.transform.position,previous),Is.LessThan(.001f),"Repeated layout must not drift.");
                quest.TrySetStatus(QuestStatus.Active);refresh.Invoke(view,new object[]{session});
                Assert.That(marker.activeSelf,Is.False);
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }

        [Test]
        public void MissionBoard_AcceptsTracksClaimsAndPreservesExactRewardPreview()
        {
            var root=new GameObject("Mission canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CatacombsMissions.prefab"),root.transform);panel.SetActive(true);
                var view=panel.GetComponent<CatacombsMissionBoardView>();var session=new GameSession(GameModeState.Exploration);var service=new QuestService(session,nextRandom:new System.Random(123));
                var definition=AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/ScriptableObjects/Quests/QD_Catacombs_Radiant_Kill.asset");
                var quest=new QuestInstance(definition,QuestStatus.Available);var offer=new RadiantRewardOffer(1,1200,40,2,null);quest.TrySetRadiantRewardOffer(offer);session.TryRegisterQuest(quest);
                foreach(var pair in new[]{("session",(object)session),("service",service),("selectedQuestId",quest.QuestId)})typeof(CatacombsMissionBoardView).GetField(pair.Item1,Private).SetValue(view,pair.Item2);
                Invoke(view,"Awake");Invoke(view,"Refresh");
                Assert.That(Ref<TextMeshProUGUI>(view,"experienceText").text,Is.EqualTo(offer.Experience.ToString("N0")));
                Assert.That(Ref<GameObject>(view,"progressRoot").activeSelf,Is.False);
                Assert.That(Ref<MissionRewardItemView>(view,"rewardItem").gameObject.activeSelf,Is.False);
                var action=Ref<Button>(view,"actionButton");action.onClick.Invoke();
                Assert.That(quest.Status,Is.EqualTo(QuestStatus.Active));Assert.That(action.interactable,Is.False);
                Assert.That(Ref<TextMeshProUGUI>(view,"messageText").text,Is.Empty);
                Assert.That(quest.RadiantRewardOffer,Is.SameAs(offer));
                service.TryAddObjectiveProgress(quest.QuestId,"objective.contract",6);Invoke(view,"Refresh");
                Assert.That(Ref<Image>(view,"progressFill").fillAmount,Is.EqualTo(.5f).Within(.001));
                Assert.That(Ref<TextMeshProUGUI>(view,"progressText").text,Does.Contain("6/12"));
                service.TryAddObjectiveProgress(quest.QuestId,"objective.contract",6);service.TryAdvanceQuestStage(quest.QuestId);Invoke(view,"Refresh");
                Assert.That(quest.Status,Is.EqualTo(QuestStatus.Completed));Assert.That(action.interactable,Is.True);
                Assert.That(Ref<TextMeshProUGUI>(view,"actionButtonText").text,Is.EqualTo("Odbierz nagrodę"));
                action.onClick.Invoke();Assert.That(session.Gold,Is.EqualTo(offer.Gold));Assert.That(quest.Status,Is.EqualTo(QuestStatus.TurnedIn));
                Assert.That(Ref<Transform>(view,"questList").GetComponentsInChildren<Button>().Length,Is.EqualTo(0),"Claimed slots wait for the next completed expedition; no duplicate or instant replacement row.");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void MissionReward_UsesExactItemRarityAndClearsPreviewWhenAbsent()
        {
            var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CatacombsMissions.prefab"));
            try
            {
                var view=Ref<MissionRewardItemView>(panel.GetComponent<CatacombsMissionBoardView>(),"rewardItem");
                var definition=AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/EID_Start_ScoutArmor.asset");
                var item=new EquipmentItemInstance(definition){CurrentRarity=RarityTier.Rare,ItemLevel=1};view.Bind(item);
                Assert.That(view.Item,Is.SameAs(item));var fields=new SerializedObject(view);
                Assert.That(((TextMeshProUGUI)fields.FindProperty("nameText").objectReferenceValue).text,Is.EqualTo(ItemRarityPresentation.Name(item)));
                Assert.That(((Image)fields.FindProperty("rarityAccent").objectReferenceValue).color,Is.EqualTo(ItemRarityPresentation.Color(item.CurrentRarity)));
                view.Bind(null);Assert.That(view.Item,Is.Null);Assert.That(view.gameObject.activeSelf,Is.False);
            }
            finally{Object.DestroyImmediate(panel);}
        }
    }
}
