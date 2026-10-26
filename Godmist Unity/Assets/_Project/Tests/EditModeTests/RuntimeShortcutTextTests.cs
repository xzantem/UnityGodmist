using System.Reflection;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class RuntimeShortcutTextTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject canvasObject;
        private GameObject owner;
        private Material authoredMaterial;

        [SetUp]
        public void SetUp()
        {
            canvasObject = new GameObject("Shortcut test canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            owner = new GameObject("Inactive view owner");
            owner.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(canvasObject);
            if (authoredMaterial != null) Object.DestroyImmediate(authoredMaterial);
        }

        [TestCase("endTurnButton", "E")]
        [TestCase("consumablesButton", "Q")]
        public void ShippedIconButtonsCreateRenderableShortcutLabels(string field, string shortcut)
        {
            var prefab = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
            try
            {
                var actions = prefab.GetComponentInChildren<CombatActionButtonsView>(true);
                var source = (Button)new SerializedObject(actions).FindProperty(field).objectReferenceValue;
                Assert.That(source, Is.Not.Null);
                var button = Object.Instantiate(source, canvasObject.transform);
                button.gameObject.SetActive(true);
                Assert.That(button.GetComponentInChildren<TMP_Text>(true), Is.Null, "These production buttons only contain icons.");
                var label = CreateCombatShortcut(button, shortcut);
                AssertRenderable(label, shortcut);
                label.gameObject.SetActive(false);
                label.gameObject.SetActive(true);
                AssertRenderable(label, shortcut);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CombatShortcutUsesInactiveFontAndPreservesValidMaterial(bool missingMaterial)
        {
            var buttonObject = new GameObject("Styled button", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var source = CreateInactiveStyle(buttonObject.transform, missingMaterial);
            var label = CreateCombatShortcut(buttonObject.GetComponent<Button>(), "E");
            Assert.That(label.font, Is.SameAs(source.font));
            Assert.That(label.fontSharedMaterial, Is.SameAs(missingMaterial ? source.font.material : authoredMaterial));
            AssertRenderable(label, "E");
        }

        [Test]
        public void SaveShortcutWithoutSourceLabelUsesDefaultFontMaterial()
        {
            var label = CreateSaveShortcut();
            Assert.That(label.font, Is.SameAs(TMP_Settings.defaultFontAsset));
            AssertRenderable(label, "F5");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveShortcutUsesInactiveFontAndPreservesValidMaterial(bool missingMaterial)
        {
            var source = CreateInactiveStyle(canvasObject.transform, missingMaterial);
            var label = CreateSaveShortcut();
            Assert.That(label.font, Is.SameAs(source.font));
            Assert.That(label.fontSharedMaterial, Is.SameAs(missingMaterial ? source.font.material : authoredMaterial));
            AssertRenderable(label, "F9");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContextualHintsUseInactiveFontAndPreserveValidMaterial(bool missingMaterial)
        {
            var source = CreateInactiveStyle(canvasObject.transform, missingMaterial);
            var view = ContextualInputHintsView.Ensure(canvasObject.GetComponent<Canvas>());
            var label = (TextMeshProUGUI)typeof(ContextualInputHintsView).GetField("text", PrivateInstance).GetValue(view);
            Assert.That(label.font, Is.SameAs(source.font));
            Assert.That(label.fontSharedMaterial, Is.SameAs(missingMaterial ? source.font.material : authoredMaterial));
            AssertRenderable(label, "Tab");
        }

        private TextMeshProUGUI CreateInactiveStyle(Transform parent, bool missingMaterial)
        {
            var sourceObject = new GameObject("Inactive authored label", typeof(RectTransform), typeof(TextMeshProUGUI));
            sourceObject.transform.SetParent(parent, false);
            var source = sourceObject.GetComponent<TextMeshProUGUI>();
            source.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Fonts/GodmistGothic SDF.asset");
            Assert.That(source.font, Is.Not.Null);
            authoredMaterial = new Material(source.font.material);
            sourceObject.SetActive(false);
            source.fontSharedMaterial = missingMaterial ? null : authoredMaterial;
            return source;
        }

        private TextMeshProUGUI CreateSaveShortcut()
        {
            var view = owner.AddComponent<SaveLoadPanelView>();
            var state = new SerializedObject(view);
            state.FindProperty("panelRoot").objectReferenceValue = canvasObject;
            state.ApplyModifiedPropertiesWithoutUndo();
            return (TextMeshProUGUI)typeof(SaveLoadPanelView).GetMethod("CreateQuickShortcutHint", PrivateInstance).Invoke(view, null);
        }

        private static TextMeshProUGUI CreateCombatShortcut(Button button, string shortcut)
        {
            return (TextMeshProUGUI)typeof(CombatActionButtonsView)
                .GetMethod("CreateShortcutLabel", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { button, shortcut });
        }

        private static void AssertRenderable(TextMeshProUGUI label, string text)
        {
            label.text = text;
            label.gameObject.SetActive(true);
            Assert.DoesNotThrow(() => label.ForceMeshUpdate(false, true), "Creating TMP's MaterialReference must have a non-null material.");
            Assert.That(label.fontSharedMaterial, Is.Not.Null);
            Assert.That(label.textInfo.characterCount, Is.EqualTo(text.Length));
            Assert.That(label.mesh.vertexCount, Is.GreaterThan(0));
        }
    }
}
