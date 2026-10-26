using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Godmist.Tests.EditMode
{
    public class SceneLoadingPresentationTests
    {
        private GameObject root;
        private SceneLoader loader;
        private GameSession session;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        [SetUp] public void Setup()
        {
            root = new GameObject("loading test");
            loader = root.AddComponent<SceneLoader>();
            session = new GameSession(GameModeState.Exploration);
            loader.Initialize(session);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        private IEnumerator Routine(Func<bool> prepare, Action failed = null) =>
            (IEnumerator)typeof(SceneLoader).GetMethod("LoadSceneRoutine", Flags).Invoke(loader,
                new object[] { "unused", GameModeState.Exploration, prepare, "Generowanie lochu…", null, failed });
        private GameObject Overlay => (GameObject)typeof(SceneLoader).GetField("loadingOverlay", Flags).GetValue(loader);
        private Slider Progress => Overlay.GetComponentInChildren<Slider>(true);

        [TestCase(false)][TestCase(true)]
        public void OverlayPrecedesGenerationAndFailureRestoresModeAndUnblocksInput(bool throws)
        {
            var prepared = 0; var rolledBack = 0;
            var routine = Routine(() => { prepared++; if (throws) throw new InvalidOperationException("generation test failure"); return false; }, () => rolledBack++);
            try
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(loader.IsLoading, Is.True);
                Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Loading));
                Assert.That(prepared, Is.Zero);
                Assert.That(Overlay.activeSelf, Is.True);
                Assert.That(Overlay.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(Overlay.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(30000));
                Assert.That(Overlay.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Generowanie lochu…"));
                Assert.That(Overlay.GetComponentInChildren<Image>().raycastTarget, Is.True);
                Assert.That(Progress.gameObject.activeSelf, Is.True);
                Assert.That(Progress.value, Is.Zero);
                Assert.That(loader.TryLoadScene("duplicate must not start", GameModeState.Exploration), Is.False);
                Assert.That(routine.MoveNext(), Is.True); Assert.That(prepared, Is.Zero);
                if (throws) LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: generation test failure"));
                Assert.That(routine.MoveNext(), Is.True); Assert.That(prepared, Is.EqualTo(1));
                Assert.That(routine.Current, Is.TypeOf<WaitForSecondsRealtime>());
                Assert.That(Overlay.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("Nie udało"));
                Assert.That(Progress.gameObject.activeSelf, Is.False);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(rolledBack, Is.EqualTo(1));
                Assert.That(loader.IsLoading, Is.False);
                Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Exploration));
                Assert.That(Overlay.activeSelf, Is.False);
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        [Test]
        public void CancellationBeforePreparationClosesOverlayWithoutGenerating()
        {
            var prepared = false; var failed = false;
            var routine = Routine(() => { prepared = true; return true; }, () => failed = true);
            Assert.That(routine.MoveNext(), Is.True);
            (routine as IDisposable).Dispose();
            Assert.That(prepared, Is.False); Assert.That(failed, Is.True);
            Assert.That(loader.IsLoading, Is.False); Assert.That(Overlay.activeSelf, Is.False);
            Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Exploration));
        }

        [Test]
        public void RollbackExceptionStillClosesOverlayAndRestoresMode()
        {
            var routine = Routine(() => false, () => throw new InvalidOperationException("rollback failure"));
            Assert.That(routine.MoveNext(), Is.True);
            Assert.Throws<InvalidOperationException>(() => (routine as IDisposable).Dispose());
            Assert.That(loader.IsLoading, Is.False); Assert.That(Overlay.activeSelf, Is.False);
            Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Exploration));
        }

        [Test]
        public void ProgressUsesGothicArtworkNeverRegressesAndResetsForTheNextLoad()
        {
            var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            var state = new SerializedObject(loader);
            state.FindProperty("loadingStyle").objectReferenceValue = palette;
            state.ApplyModifiedPropertiesWithoutUndo();
            void Show(string message) => typeof(SceneLoader).GetMethod("ShowLoading", Flags).Invoke(loader, new object[] { message });
            void Report(float progress) => typeof(SceneLoader).GetMethod("ReportLoadingProgress", Flags).Invoke(loader, new object[] { progress });
            Show("Wczytywanie…");
            var bar = Progress;
            Assert.That(bar.interactable, Is.False);
            Assert.That(bar.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(bar.fillRect.GetComponent<Image>().sprite, Is.SameAs(palette.GetArtwork("fill_runtime")));
            Assert.That(bar.transform.Find("Track").GetComponent<Image>().sprite, Is.SameAs(palette.GetArtwork("slider_track")));
            Assert.That(Overlay.GetComponentInChildren<TextMeshProUGUI>().font, Is.SameAs(palette.Font));
            foreach (var image in bar.GetComponentsInChildren<Image>()) Assert.That(image.raycastTarget, Is.False);
            Report(.65f); Assert.That(bar.value, Is.EqualTo(.65f));
            Report(.2f); Report(-1); Assert.That(bar.value, Is.EqualTo(.65f));
            Report(2); Assert.That(bar.value, Is.EqualTo(1));
            Show("Generowanie lochu…");
            Assert.That(Progress, Is.SameAs(bar));
            Assert.That(bar.value, Is.Zero);
            Assert.That(Overlay.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void PersistentBootstrapLoaderHasTheSharedLoadingStyleAssigned()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            try
            {
                var found = 0;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                    foreach (var sceneLoader in sceneRoot.GetComponentsInChildren<SceneLoader>(true))
                    {
                        var palette = (UIWorkspaceStylePalette)new SerializedObject(sceneLoader).FindProperty("loadingStyle").objectReferenceValue;
                        Assert.That(palette, Is.Not.Null);
                        Assert.That(palette.GetArtwork("slider_track"), Is.Not.Null);
                        Assert.That(palette.GetArtwork("fill_runtime"), Is.Not.Null);
                        found++;
                    }
                Assert.That(found, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
