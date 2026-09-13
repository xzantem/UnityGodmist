using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class GalduryteExtractionPrefabTests
    {
        [Test]
        public void ExtractionPrefab_HasLocalReferencesAndInteractiveMaskedSelection()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view = prefab.GetComponentInChildren<GalduryteExtractionWindowView>(true);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.transform.parent.name, Is.EqualTo("Content"));
            var serialized = new SerializedObject(view);
            foreach (var name in new[] { "equipmentGrid", "socketList", "equipmentSlotTemplate", "socketRowTemplate", "previewSlot", "previewNameText", "previewSummaryText", "costText", "chanceText", "warningText", "statusText", "extractButton" })
            {
                var component = serialized.FindProperty(name).objectReferenceValue as Component;
                Assert.That(component, Is.Not.Null, name);
                Assert.That(component.transform.IsChildOf(view.transform), Is.True, name);
            }
            var row = (Component)serialized.FindProperty("socketRowTemplate").objectReferenceValue;
            var icon = (Component)new SerializedObject(row).FindProperty("iconSlot").objectReferenceValue;
            Assert.That(new SerializedObject(icon.GetComponent("Button")).FindProperty("m_Interactable").boolValue, Is.True);
            Assert.That(row.gameObject.activeSelf, Is.False);
            var grid = (Transform)serialized.FindProperty("equipmentGrid").objectReferenceValue;
            Assert.That(grid.GetComponent("GridLayoutGroup"), Is.Not.Null);
            var scroll = grid.parent;
            while (scroll != view.transform && scroll.GetComponent("ScrollRect") == null) scroll = scroll.parent;
            Assert.That(scroll, Is.Not.EqualTo(view.transform));
            var scrollData = new SerializedObject(scroll.GetComponent("ScrollRect"));
            Assert.That(scrollData.FindProperty("m_Content").objectReferenceValue, Is.EqualTo(grid));
            var viewport = (RectTransform)scrollData.FindProperty("m_Viewport").objectReferenceValue;
            Assert.That(viewport, Is.Not.Null);
            Assert.That(viewport.GetComponent("RectMask2D") != null || viewport.GetComponent("Mask") != null, Is.True);
            var window = new SerializedObject(prefab.GetComponentInChildren<NpcServicesWindowView>(true));
            Assert.That(window.FindProperty("galduryteExtractionService").objectReferenceValue, Is.EqualTo(view));
        }

        [Test]
        public void GalduryteLayouts_UseContentHeightAndSeparateWrappedWarning()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            foreach (var view in new Component[] { prefab.GetComponentInChildren<GalduryteSocketingWindowView>(true), prefab.GetComponentInChildren<GalduryteExtractionWindowView>(true) })
            {
                var so = new SerializedObject(view);
                var title = (Component)so.FindProperty("previewNameText").objectReferenceValue;
                var layout = new SerializedObject(title.GetComponent("LayoutElement"));
                Assert.That(layout.FindProperty("m_PreferredHeight").floatValue, Is.EqualTo(-1));
                Assert.That(layout.FindProperty("m_MinHeight").floatValue, Is.EqualTo(-1));
                var measure = title.GetType().GetMethod("GetPreferredValues", new[] { typeof(string), typeof(float), typeof(float) });
                var shortSize = (Vector2)measure.Invoke(title, new object[] { "Miecz", 220f, float.PositiveInfinity });
                var longSize = (Vector2)measure.Invoke(title, new object[] { "Doskonały Żelazny Miecz i Sztylet zwiadowcy", 220f, float.PositiveInfinity });
                Assert.That(longSize.y, Is.GreaterThan(shortSize.y), "Długa nazwa musi zwiększać wysokość layoutu.");
                var status = (Component)so.FindProperty("statusText").objectReferenceValue;
                Assert.That(status.gameObject.activeSelf, Is.False);
                if (view is not GalduryteExtractionWindowView) continue;
                var warning = (Component)so.FindProperty("warningText").objectReferenceValue;
                var warningData = new SerializedObject(warning);
                Assert.That(warningData.FindProperty("m_TextWrappingMode").intValue, Is.EqualTo(1));
                Assert.That(new SerializedObject(warning.GetComponent("LayoutElement")).FindProperty("m_PreferredHeight").floatValue, Is.EqualTo(-1));
                var chance = (Component)so.FindProperty("chanceText").objectReferenceValue;
                var cost = (Component)so.FindProperty("costText").objectReferenceValue;
                Assert.That(chance.transform.parent, Is.EqualTo(cost.transform.parent));
                Assert.That(chance, Is.Not.EqualTo(warning));
            }
        }
    }
}
