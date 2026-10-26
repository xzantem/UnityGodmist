using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Editor;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class QuestJournalWindowTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject panel, owner;
        QuestJournalPanelView view;
        GameSession session;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static T Ref<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        static QuestInstance Quest(string name, QuestStatus status) => new(AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/ScriptableObjects/Quests/" + name + ".asset"), status);

        [SetUp] public void Setup()
        {
            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuestJournalUiSetup.WindowPath));
            owner = new GameObject("Journal fixture"); view = owner.AddComponent<QuestJournalPanelView>();
            QuestJournalUiSetup.WireController(view, panel); Invoke(view, "Awake"); panel.SetActive(true);
            session = new GameSession(GameModeState.Exploration); Set(view, "gameSession", session); Set(view, "questService", new QuestService(session));
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(owner); Object.DestroyImmediate(panel); }

        [TestCase("Assets/_Project/Scenes/City/Arungard.unity")]
        [TestCase("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity")]
        public void BothScenesUseSharedJournalAndAllReferences(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<QuestJournalPanelView>(true)).Single();
                var so = new SerializedObject(controller);
                foreach (var field in new[] { "panelRoot", "window", "visibility", "activeTab", "completedTab", "activeQuestsContent", "completedQuestsContent", "objectivePrefab", "questButtonPrefab", "rewardsRoot", "rewardItem", "detailsContent", "trackButton" })
                    Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                var root = (GameObject)so.FindProperty("panelRoot").objectReferenceValue;
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root), Is.EqualTo(QuestJournalUiSetup.WindowPath));
                Assert.That(root.activeSelf, Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test] public void CompletedTabNeverSelectsTrackedActiveQuest()
        {
            var active = Quest("QD_Catacombs_Radiant_Kill", QuestStatus.Active);
            var completed = Quest("QD_Catacombs_Radiant_Expedition", QuestStatus.Completed);
            session.TryRegisterQuest(active); session.TryRegisterQuest(completed); session.TrySetTrackedQuest(active.QuestId);
            Invoke(view, "ShowCompleted");
            Assert.That(Ref<string>(view, "selectedQuestId"), Is.EqualTo(completed.QuestId));
            Assert.That(Ref<GameObject>(view, "activeListRoot").activeSelf, Is.False);
            Assert.That(Ref<Button>(view, "trackButton").gameObject.activeSelf, Is.False);
        }

        [Test] public void EmptyCategoryClearsOldDetailsAndTrackingAction()
        {
            session.TryRegisterQuest(Quest("QD_Catacombs_Radiant_Kill", QuestStatus.Active));
            Invoke(view, "RefreshAll"); Invoke(view, "ShowCompleted");
            Assert.That(Ref<string>(view, "selectedQuestId"), Is.Empty);
            Assert.That(Ref<GameObject>(view, "detailsContent").activeSelf, Is.False);
            Assert.That(Ref<TextMeshProUGUI>(view, "emptyListText").gameObject.activeSelf, Is.True);
            Assert.That(Ref<Button>(view, "trackButton").gameObject.activeSelf, Is.False);
        }

        [Test] public void TrackingToggleUsesSessionAndUpdatesRowMarker()
        {
            var quest = Quest("QD_Catacombs_Radiant_Kill", QuestStatus.Active); session.TryRegisterQuest(quest);
            Invoke(view, "BindServices"); Invoke(view, "RefreshAll"); Ref<Button>(view, "trackButton").onClick.Invoke();
            Assert.That(session.TrackedQuestId, Is.EqualTo(quest.QuestId));
            Assert.That(Ref<Transform>(view, "activeQuestsContent").GetComponentInChildren<QuestJournalQuestButtonView>().transform.Find("Tracked").gameObject.activeSelf, Is.True);
            Ref<Button>(view, "trackButton").onClick.Invoke(); Assert.That(session.TrackedQuestId, Is.Empty);
        }

        [Test] public void RewardPreviewUsesExactOfferWithoutGrantingAnything()
        {
            var quest = Quest("QD_Catacombs_Radiant_Kill", QuestStatus.Active); quest.TrySetRadiantRewardOffer(new RadiantRewardOffer(8, 12345, 234, 7, null)); session.TryRegisterQuest(quest);
            var gold = session.Gold; Invoke(view, "RefreshAll");
            Assert.That(Ref<TextMeshProUGUI>(view, "experienceText").text, Is.EqualTo(12345.ToString("N0")));
            Assert.That(Ref<TextMeshProUGUI>(view, "goldText").text, Is.EqualTo(234.ToString("N0")));
            Assert.That(session.Gold, Is.EqualTo(gold)); Assert.That(quest.ClaimedRewardKeys, Is.Empty);
        }

        [Test] public void StoryExperiencePreviewUsesStandardMissionExperience()
        {
            session.TryRegisterQuest(Quest("QD_InvestigateCatacombs", QuestStatus.Active)); Invoke(view, "RefreshAll");
            Assert.That(Ref<TextMeshProUGUI>(view, "experienceText").text, Is.EqualTo(CharacterProgressionSystem.GetStandardMissionExperience(1).ToString("N0")));
        }

        [Test] public void RadiantGoalAppearsOnlyInProgressRow()
        {
            var quest = Quest("QD_Catacombs_Radiant_Kill", QuestStatus.Active); session.TryRegisterQuest(quest); quest.TryAddObjectiveProgress("objective.contract", 6); Invoke(view, "RefreshAll");
            Assert.That(Ref<TextMeshProUGUI>(view, "descriptionText").text, Does.Not.Contain("Pokonaj 12"));
            Assert.That(Ref<GameObject>(view, "stageRoot").activeSelf, Is.False);
            Assert.That(Ref<Transform>(view, "objectivesContent").GetComponentInChildren<QuestJournalObjectiveView>().transform.Find("Counter").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("6/12"));
            Assert.That(QuestJournalPresentation.ProgressSummary(quest), Is.EqualTo("6/12"));
        }
    }
}
