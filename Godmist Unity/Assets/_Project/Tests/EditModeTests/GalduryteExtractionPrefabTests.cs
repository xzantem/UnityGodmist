using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class GalduryteExtractionPrefabTests
    {
        [Test]
        public void SpecialistPreviews_UseContentHeightAndNonShrinkingSquareIcons()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var count = 0;
            foreach (var view in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (view == null || view is GalduryteSocketingWindowView || view is GalduryteExtractionWindowView) continue;
                var so = new SerializedObject(view);
                var title = so.FindProperty("previewNameText")?.objectReferenceValue as Component;
                if (title == null) continue;
                count++;
                var le = title.GetComponent<UnityEngine.UI.LayoutElement>();
                Assert.That(le.preferredHeight, Is.EqualTo(-1), view.name);
                Assert.That(le.minWidth, Is.EqualTo(0), view.name);
                var status = so.FindProperty("statusText")?.objectReferenceValue as Component;
                var action = (so.FindProperty("identifyButton")?.objectReferenceValue ?? so.FindProperty("craftButton")?.objectReferenceValue ?? so.FindProperty("actionButton")?.objectReferenceValue) as Component;
                if (status != null && action != null && status.transform.parent == action.transform.parent)
                    Assert.That(status.transform.GetSiblingIndex(), Is.LessThan(action.transform.GetSiblingIndex()), view.name);
                var icon = title.transform.parent.parent.Find("ResultIconSlot");
                if (icon == null) continue;
                var iconLayout = icon.GetComponent<UnityEngine.UI.LayoutElement>();
                Assert.That(iconLayout.minWidth, Is.EqualTo(80), view.name);
                Assert.That(iconLayout.minHeight, Is.EqualTo(80), view.name);
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void AlchemyMaterials_FiveCardsFitInTwoRowsInsideColumn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            foreach (var view in prefab.GetComponentsInChildren<AlchemyCraftingWindowView>(true))
            {
                var so = new SerializedObject(view);
                var content = (Component)so.FindProperty("materialRequirementsContent").objectReferenceValue;
                var grid = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Assert.That(grid.constraintCount, Is.EqualTo(3));
                var section = content.transform.parent.parent;
                var column = section.parent.GetComponent<UnityEngine.UI.LayoutElement>();
                Assert.That(3 * grid.cellSize.x + 2 * grid.spacing.x + grid.padding.horizontal + 24,
                    Is.LessThanOrEqualTo(column.preferredWidth));
                Assert.That(2 * grid.cellSize.y + grid.spacing.y + grid.padding.vertical + 52,
                    Is.LessThanOrEqualTo(section.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight));
            }
        }

        [Test]
        public void SpecialistPreview_LongTitleDoesNotOverlapCaptionAfterLayout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            foreach (var view in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (view == null || view is GalduryteSocketingWindowView || view is GalduryteExtractionWindowView) continue;
                var titleRef = new SerializedObject(view).FindProperty("previewNameText")?.objectReferenceValue as Component;
                if (titleRef == null || titleRef.transform.parent.name != "ResultInfo") continue;
                var row = Object.Instantiate(titleRef.transform.parent.parent.gameObject);
                try
                {
                    row.SetActive(true);
                    var rect = (RectTransform)row.transform;
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.sizeDelta = new Vector2(320, 112);
                    var title = row.transform.Find("ResultInfo/" + titleRef.name).GetComponent(titleRef.GetType());
                    var data = new SerializedObject(title);
                    data.FindProperty("m_text").stringValue = "Doskonały Żelazny Długi Miecz";
                    data.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    var caption = title.transform.parent.GetChild(1) as RectTransform;
                    var titleRect = (RectTransform)title.transform;
                    Assert.That(caption.anchoredPosition.y + caption.rect.height * (1 - caption.pivot.y),
                        Is.LessThanOrEqualTo(titleRect.anchoredPosition.y - titleRect.rect.height * titleRect.pivot.y + .01f), view.name);
                    var icon = row.transform.Find("ResultIconSlot") as RectTransform;
                    Assert.That(icon.rect.width, Is.EqualTo(icon.rect.height).Within(.01f), view.name);
                }
                finally { Object.DestroyImmediate(row); }
            }
        }

        [Test]
        public void UpgradeSlider_RoundedTwentyPercentIsActualTwentyPercent()
        {
            var root = new GameObject("upgrade", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<EquipmentUpgradeWindowView>();
                var sliderRoot = new GameObject("slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
                sliderRoot.transform.SetParent(root.transform);
                var slider = sliderRoot.GetComponent<UnityEngine.UI.Slider>();
                var so = new SerializedObject(view);
                so.FindProperty("targetProgressSlider").objectReferenceValue = slider;
                so.ApplyModifiedPropertiesWithoutUndo();
                slider.value = .196f;
                typeof(EquipmentUpgradeWindowView).GetMethod("HandleSliderChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { slider.value });
                Assert.That(slider.value, Is.EqualTo(.2f).Within(.000001f));
                Assert.That(new Godmist.Runtime.Items.EquipmentItemInstance { UpgradeProgress = slider.value }.SocketCapacity, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

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

        [Test]
        public void NpcStaticUi_IsSerializedWithCompleteReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var header = prefab.GetComponentInChildren<NpcLoyaltyHeaderView>(true);
            Assert.That(header, Is.Not.Null);
            Assert.That(MonoScript.FromMonoBehaviour(header).GetClass(), Is.EqualTo(typeof(NpcLoyaltyHeaderView)));
            var serialized = new SerializedObject(header);
            foreach (var field in new[] { "titleText", "levelText", "progressText", "progressSlider", "progressRow" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var window = new SerializedObject(prefab.GetComponentInChildren<NpcServicesWindowView>(true));
            Assert.That(window.FindProperty("loyaltyHeader").objectReferenceValue, Is.EqualTo(header));
            var trade = new SerializedObject(prefab.GetComponentInChildren<TradeServiceWindowView>(true));
            foreach (var field in new[] { "partialSaleConfirmationRoot", "partialSaleText", "partialSaleConfirmButton", "partialSaleCancelButton", "playerGoldText", "traderGoldText" })
                Assert.That(trade.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(((GameObject)trade.FindProperty("partialSaleConfirmationRoot").objectReferenceValue).activeSelf, Is.False);
            foreach (var slot in prefab.GetComponentsInChildren<TradeItemSlotView>(true))
                Assert.That(new SerializedObject(slot).FindProperty("countBackground").objectReferenceValue, Is.Not.Null);
        }
    }
}
