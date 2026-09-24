using System.Linq;
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
            Assert.That(row.Find("TextRoot").GetComponent<RectTransform>().sizeDelta.x,
                Is.EqualTo(-24f));
            var board = prefab.GetComponent<CatacombsMissionBoardView>();
            Assert.That(board, Is.Not.Null);
            var boardFields = new SerializedObject(board);
            Assert.That(boardFields.FindProperty("questList").objectReferenceValue, Is.EqualTo(content));
            Assert.That(boardFields.FindProperty("questButtonTemplate").objectReferenceValue,
                Is.EqualTo(row.GetComponent<Button>()));

            var details = detailsSection.Find("Content");
            Assert.That(details, Is.Not.Null);
            var title = details.Find("MissionTitle").GetComponent<RectTransform>();
            var description = details.Find("MissionDescription").GetComponent<RectTransform>();
            var reward = details.Find("MissionReward").GetComponent<RectTransform>();
            var message = details.Find("MissionMessage").GetComponent<RectTransform>();
            var action = details.Find("MissionActionButton").GetComponent<RectTransform>();
            Assert.That(title.anchorMin.y, Is.GreaterThan(description.anchorMax.y));
            Assert.That(description.anchorMin.y, Is.GreaterThan(reward.anchorMax.y));
            Assert.That(reward.anchorMin.y, Is.GreaterThan(message.anchorMax.y));
            Assert.That(message.anchorMin.y, Is.GreaterThan(action.anchorMax.y));
        }
    }
}
