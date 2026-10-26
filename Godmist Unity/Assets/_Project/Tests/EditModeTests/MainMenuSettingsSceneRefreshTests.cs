using System.Linq;
using System.Reflection;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Godmist.Tests.EditMode
{
    public sealed class MainMenuSettingsSceneRefreshTests
    {
        private Scene scene;
        private MainMenuSettingsView view;
        private static readonly string[] NewFields =
        {
            "volumeValueText", "confirmationRoot", "confirmationMessageText", "keepDisplayButton", "revertDisplayButton"
        };

        [SetUp]
        public void OpenScene()
        {
            scene = EditorSceneManager.OpenPreviewScene(GodmistMainMenuSetup.ScenePath);
            view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuSettingsView>(true)).Single();
        }

        [TearDown]
        public void RestoreScene() => EditorSceneManager.ClosePreviewScene(scene);

        private void SimulateOldInMemoryScene()
        {
            // Model the open scene surviving script reload without reimporting its new objects.
            foreach (var field in NewFields)
                typeof(MainMenuSettingsView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, null);
        }

        [Test]
        public void ReportsTheExactMissingFields()
        {
            SimulateOldInMemoryScene();
            Assert.That(view.GetMissingReferences(), Is.EquivalentTo(NewFields));
        }

        [Test]
        public void DirtyStaleSceneIsPreservedForManualRecovery()
        {
            SimulateOldInMemoryScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(GodmistSettingsSceneRefresh.RefreshIfStale(scene, out var message), Is.False);
            Assert.That(message, Does.Contain("Nie nadpisuj"));
            Assert.That(view.GetMissingReferences(), Is.EquivalentTo(NewFields));
            Assert.That(scene.isDirty, Is.True);
        }

        [Test]
        public void CompleteSceneDoesNotReloadOrDiscardEdits()
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(GodmistSettingsSceneRefresh.RefreshIfStale(scene, out var message), Is.True);
            Assert.That(message, Is.Empty);
            Assert.That(view.GetMissingReferences(), Is.Empty);
            Assert.That(scene.isDirty, Is.True);
        }

    }
}
