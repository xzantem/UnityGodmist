using System.Linq;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Tests.EditMode
{
    public class MainMenuSceneConfigurationTests
    {
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
        private Scene scene;
        private Transform canvas;

        [SetUp]
        public void OpenPreview()
        {
            scene = EditorSceneManager.OpenPreviewScene(BootstrapPath);
            canvas = scene.GetRootGameObjects().Single(x => x.name == "Canvas").transform;
        }

        [TearDown]
        public void ClosePreview() { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test]
        public void ProductionMenuHasCompleteSerializedReferencesAndNoMissingScripts()
        {
            var root = canvas.Find("MainMenuRoot");
            Assert.That(root, Is.Not.Null);
            foreach (var view in new Component[] { root.GetComponent<MainMenuView>(), root.GetComponent<SaveLoadPanelView>(), root.GetComponent<MainMenuSettingsView>() })
            {
                Assert.That(view, Is.Not.Null);
                var iterator = new SerializedObject(view).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, view.GetType().Name + ": " + iterator.name);
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            Assert.That(canvas.Find("StartCombatButton").gameObject.activeSelf, Is.False);
            Assert.That(canvas.Find("StartExplorationButton").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void VolumeControlHasInteractiveHandleRatherThanDisplayOnlyBar()
        {
            var settings = new SerializedObject(canvas.Find("MainMenuRoot").GetComponent<MainMenuSettingsView>());
            var slider = new SerializedObject(settings.FindProperty("volumeSlider").objectReferenceValue);
            Assert.That(slider.FindProperty("m_Interactable").boolValue, Is.True);
            Assert.That(slider.FindProperty("m_HandleRect").objectReferenceValue, Is.Not.Null);
            Assert.That(slider.FindProperty("m_TargetGraphic").objectReferenceValue, Is.Not.Null);
            Assert.That(slider.FindProperty("m_MinValue").floatValue, Is.Zero);
            Assert.That(slider.FindProperty("m_MaxValue").floatValue, Is.EqualTo(1));
        }

        [Test]
        public void BuildStartsAtMenuAndContainsBothGameplayDestinations()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(scenes.First(), Is.EqualTo(BootstrapPath));
            Assert.That(scenes, Does.Contain("Assets/_Project/Scenes/City/Arungard.unity"));
            Assert.That(scenes, Does.Contain("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity"));
        }
    }
}
