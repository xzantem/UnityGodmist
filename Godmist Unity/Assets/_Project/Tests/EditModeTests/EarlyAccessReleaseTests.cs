using System.IO;
using System.Linq;
using Godmist.Editor;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class EarlyAccessReleaseTests
    {
        [Test]
        public void CandidateVersionAndExistingSaveLocationArePreserved()
        {
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(Godmist.Core.Constants.GameVersion.Bundle));
            Assert.That(PlayerSettings.companyName, Is.EqualTo("DefaultCompany"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("Godmist Unity"));
            Assert.That(new SaveManager().SaveDirectoryPath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "Saves")));
            Assert.That(Application.persistentDataPath.Replace('\\', '/'), Does.EndWith("/DefaultCompany/Godmist Unity"));
        }

        [Test]
        public void NativeMenuShowsCandidateVersionWithoutInterceptingInput()
        {
            var scene = EditorSceneManager.OpenPreviewScene(GodmistMainMenuSetup.ScenePath);
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            var old = RenderTexture.active;
            GameObject cameraObject = null;
            GameObject menuCopy = null;
            try
            {
                var authoredCanvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Single(canvas => canvas.name == "Canvas").gameObject;
                menuCopy = Object.Instantiate(authoredCanvas);
                var canvas = menuCopy.GetComponent<Canvas>();
                menuCopy.SetActive(true); canvas.enabled = true;
                var view = canvas.transform.Find("MainMenuRoot").GetComponent<MainMenuView>();
                view.gameObject.SetActive(true);
                var label = new SerializedObject(view).FindProperty("versionLabel").objectReferenceValue as TextMeshProUGUI;
                Assert.That(label, Is.Not.Null);
                Assert.That(label.text, Is.EqualTo(Godmist.Core.Constants.GameVersion.MenuLabel));
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.rectTransform.anchorMin, Is.EqualTo(Vector2.right));
                Assert.That(label.rectTransform.anchorMax, Is.EqualTo(Vector2.right));
                var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
                Assert.That(label.font, Is.SameAs(palette.Font));
                Assert.That(view.GetComponentsInChildren<Button>(true).Count(), Is.GreaterThanOrEqualTo(5));
                cameraObject = new GameObject("Release menu camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 360; camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
                foreach (var t in menuCopy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                camera.cullingMask = 1 << 30;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixels32().Count(p => p.r > 30 || p.g > 30 || p.b > 30), Is.GreaterThan(10000),
                    "The native menu must actually render, not a black preview surface.");
                Directory.CreateDirectory(EarlyAccessReleaseBuild.Output);
                File.WriteAllBytes(Path.Combine(EarlyAccessReleaseBuild.Output, "main-menu.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old;
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                if (menuCopy != null) Object.DestroyImmediate(menuCopy);
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
