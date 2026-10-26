using Godmist.Views.UI.Workspace;
using System.Linq;
using UnityEngine.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class GalduryteExtractionPrefabTests
    {
        [Test]
        public void ReforgePreviewBeforeAnyAttemptDoesNotPromiseOrMutateTheHigherRarity()
        {
            var root = new GameObject("reforge", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<EquipmentUpgradeWindowView>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var item = new Godmist.Runtime.Items.EquipmentItemInstance
                { CurrentRarity = Godmist.Core.Enums.RarityTier.Common, UpgradeProgress = 1f };
                typeof(EquipmentUpgradeWindowView).GetField("isReforge", flags).SetValue(view, true);
                typeof(EquipmentUpgradeWindowView).GetField("selected", flags).SetValue(view, item);
                var result = typeof(EquipmentUpgradeWindowView).GetMethod("BuildPreviewItem", flags).Invoke(view,
                    new object[] { new Godmist.Systems.Items.EquipmentUpgradePreview { TargetRarity = Godmist.Core.Enums.RarityTier.Uncommon } });
                Assert.That(result, Is.SameAs(item));
                Assert.That(item.CurrentRarity, Is.EqualTo(Godmist.Core.Enums.RarityTier.Common));
                Assert.That(item.UpgradeProgress, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpgradeProjectionRefreshesFromCurrentItemInsteadOfPreviousResult(bool isReforge)
        {
            var root = new GameObject("upgrade", typeof(RectTransform));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Items.EquipmentItemDefinition>();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Godmist.Data.Items.ItemDefinition).GetField("<ItemType>k__BackingField", flags).SetValue(definition, Godmist.Core.Enums.ItemType.Weapon);
                var item = new Godmist.Runtime.Items.EquipmentItemInstance
                {
                    Definition = definition, UpgradeProgress = .2f, CurrentRarity = Godmist.Core.Enums.RarityTier.Common,
                    BaseStatModifiers = new() { new Godmist.Core.Types.StatModifier { StatType = Godmist.Core.Enums.CharacterStatType.MinAttack, Value = 15 } }
                };
                item.RecalculateStatModifiers();
                var view = root.AddComponent<EquipmentUpgradeWindowView>();
                typeof(EquipmentUpgradeWindowView).GetField("selected", flags).SetValue(view, item);
                typeof(EquipmentUpgradeWindowView).GetField("isReforge", flags).SetValue(view, isReforge);
                typeof(EquipmentUpgradeWindowView).GetField("executionPreviewText", flags).SetValue(view, "Previous attempt");
                var result = (Godmist.Runtime.Items.EquipmentItemInstance)typeof(EquipmentUpgradeWindowView).GetMethod("BuildPreviewItem", flags).Invoke(view,
                    new object[] { new Godmist.Systems.Items.EquipmentUpgradePreview { TargetProgress = .4f } });
                if (isReforge) Assert.That(result, Is.SameAs(item));
                else
                {
                    Assert.That(result.UpgradeProgress, Is.EqualTo(.4f));
                    Assert.That(result.StatModifiers[0].Value, Is.EqualTo(18f).Within(.0001f));
                    Assert.That(result.SocketCapacity, Is.EqualTo(2));
                    Assert.That(item.UpgradeProgress, Is.EqualTo(.2f));
                    Assert.That(item.StatModifiers[0].Value, Is.EqualTo(16.5f).Within(.0001f));
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void UpgradeRangeLabels_AreWiredBelowTheirSlider()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view = new SerializedObject(prefab.GetComponent<NpcServicesWindowView>()).FindProperty("equipmentUpgradeService").objectReferenceValue;
            var data = new SerializedObject(view);
            foreach (var prefix in new[] { "target", "chance" })
            {
                var slider = (UnityEngine.UI.Slider)data.FindProperty(prefix == "target" ? "targetProgressSlider" : "successChanceSlider").objectReferenceValue;
                var minimum = (TMPro.TextMeshProUGUI)data.FindProperty(prefix + "MinimumText").objectReferenceValue;
                var maximum = (TMPro.TextMeshProUGUI)data.FindProperty(prefix + "MaximumText").objectReferenceValue;
                Assert.That(minimum, Is.Not.Null);
                Assert.That(maximum, Is.Not.Null);
                Assert.That(minimum.transform.parent, Is.EqualTo(maximum.transform.parent));
                Assert.That(minimum.transform.parent.parent, Is.EqualTo(slider.transform.parent));
                Assert.That(minimum.transform.parent.GetSiblingIndex(), Is.GreaterThan(slider.transform.GetSiblingIndex()));
                Assert.That(minimum.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Left));
                Assert.That(maximum.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Right));
            }
            Assert.That(data.FindProperty("costHeadingText").objectReferenceValue, Is.Not.Null);
        }

        [TestCase("targetProgressSlider", 0f)]
        [TestCase("targetProgressSlider", .5f)]
        [TestCase("targetProgressSlider", 1f)]
        [TestCase("successChanceSlider", 0f)]
        [TestCase("successChanceSlider", .5f)]
        [TestCase("successChanceSlider", 1f)]
        public void UpgradeSlider_HandleStaysCenteredInsideTrack(string field, float value)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var shell = prefab.GetComponent<NpcServicesWindowView>();
            var view = new SerializedObject(shell).FindProperty("equipmentUpgradeService").objectReferenceValue;
            var source = (UnityEngine.UI.Slider)new SerializedObject(view).FindProperty(field).objectReferenceValue;
            var slider = Object.Instantiate(source);
            try
            {
                slider.gameObject.SetActive(true);
                var root = (RectTransform)slider.transform;
                root.anchorMin = root.anchorMax = Vector2.zero;
                slider.minValue = 0;
                slider.maxValue = 1;
                foreach (var width in new[] { 452f, 500f })
                {
                    root.sizeDelta = new Vector2(width, 32);
                    slider.normalizedValue = value;
                    slider.Rebuild(UnityEngine.UI.CanvasUpdate.PostLayout);
                    var handle = slider.handleRect;
                    var track = (RectTransform)slider.transform.Find("GothicBarFrame");
                    var center = root.InverseTransformPoint(handle.TransformPoint(handle.rect.center));
                    var trackCenter = root.InverseTransformPoint(track.TransformPoint(track.rect.center));
                    Assert.That(handle.rect.width, Is.EqualTo(16).Within(.01f));
                    Assert.That(handle.rect.height, Is.EqualTo(32).Within(.01f));
                    Assert.That(center.y, Is.EqualTo(trackCenter.y).Within(.01f));
                    Assert.That(center.x - 8, Is.GreaterThanOrEqualTo(root.rect.xMin - .01f));
                    Assert.That(center.x + 8, Is.LessThanOrEqualTo(root.rect.xMax + .01f));
                    var fillEnd = root.InverseTransformPoint(slider.fillRect.TransformPoint(new Vector3(slider.fillRect.rect.xMax, 0)));
                    Assert.That(fillEnd.x, Is.EqualTo(center.x).Within(.01f));
                }
            }
            finally { Object.DestroyImmediate(slider.gameObject); }
        }

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
                var titleText = title.GetComponent<TMPro.TMP_Text>();
                // Accepted crafting layouts also use a bounded ellipsis title.
                // Both policies prevent a long name from displacing its caption;
                // the layout test below verifies that relationship explicitly.
                if (titleText == null || titleText.overflowMode != TMPro.TextOverflowModes.Ellipsis)
                    Assert.That(le.preferredHeight, Is.EqualTo(-1), view.name);
                else
                    Assert.That(le.preferredHeight, Is.GreaterThanOrEqualTo(-1), view.name);
                Assert.That(le.minWidth, Is.LessThanOrEqualTo(0), view.name);
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
        public void AlchemyMaterials_ThreeColumnsFitAndExtraRowsRemainScrollable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                foreach (var view in root.GetComponentsInChildren<AlchemyCraftingWindowView>(true))
                {
                    var so = new SerializedObject(view);
                    var content = (RectTransform)so.FindProperty("materialRequirementsContent").objectReferenceValue;
                    var grid = content.GetComponent<GridLayoutGroup>();
                    var scroll = content.GetComponentInParent<ScrollRect>(true);
                    Assert.That(grid.constraintCount, Is.EqualTo(3));
                    Assert.That(scroll, Is.Not.Null);
                    Assert.That(scroll.content, Is.SameAs(content));
                    Assert.That(scroll.vertical, Is.True);
                    Assert.That(scroll.horizontal, Is.False);
                    Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                    var column = content.GetComponentsInParent<LayoutElement>(true).First(layout => layout.preferredWidth > 0);
                    Assert.That(3 * grid.cellSize.x + 2 * grid.spacing.x + grid.padding.horizontal + 24,
                        Is.LessThanOrEqualTo(column.preferredWidth));
                    for (var node = content.transform; node != null; node = node.parent) node.gameObject.SetActive(true);
                    var template = (Component)so.FindProperty("materialSlotTemplate").objectReferenceValue;
                    for (var index = 0; index < 5; index++)
                        Object.Instantiate(template.gameObject, content).SetActive(true);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                    Assert.That(LayoutUtility.GetPreferredHeight(content),
                        Is.EqualTo(2 * grid.cellSize.y + grid.spacing.y + grid.padding.vertical));
                    Assert.That(content.GetComponent<ContentSizeFitter>().verticalFit,
                        Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                }
            }
            finally { Object.DestroyImmediate(root); }
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
        public void UpgradeSlider_ReconfiguringBoundsDoesNotEraseExecutionResult()
        {
            var root = new GameObject("upgrade", typeof(RectTransform));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Items.EquipmentItemDefinition>();
            try
            {
                var view = root.AddComponent<EquipmentUpgradeWindowView>();
                var sliderRoot = new GameObject("slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
                sliderRoot.transform.SetParent(root.transform);
                var slider = sliderRoot.GetComponent<UnityEngine.UI.Slider>();
                slider.value = .4f;
                var so = new SerializedObject(view);
                so.FindProperty("targetProgressSlider").objectReferenceValue = slider;
                so.ApplyModifiedPropertiesWithoutUndo();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(EquipmentUpgradeWindowView).GetField("selected", flags).SetValue(view,
                    new Godmist.Runtime.Items.EquipmentItemInstance { Definition = definition, UpgradeProgress = 0f });
                typeof(EquipmentUpgradeWindowView).GetField("executionPreviewText", flags).SetValue(view, "Wynik niepowodzenia");
                slider.onValueChanged.AddListener(value => typeof(EquipmentUpgradeWindowView).GetMethod("HandleSliderChanged", flags).Invoke(view, new object[] { value }));
                typeof(EquipmentUpgradeWindowView).GetMethod("ConfigureTargetProgressSlider", flags).Invoke(view, new object[] { .4f });
                Assert.That(slider.value, Is.EqualTo(.2f).Within(.000001f));
                Assert.That(typeof(EquipmentUpgradeWindowView).GetField("executionPreviewText", flags).GetValue(view), Is.EqualTo("Wynik niepowodzenia"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
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
        public void ExtractionCatalogsAndSelectedPreview_HaveIndependentMasksAndDedicatedTemplates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view = prefab.GetComponentInChildren<GalduryteExtractionWindowView>(true);
            var data = new SerializedObject(view);
            foreach (var field in new[] { "equipmentScroll", "socketsScroll", "detailsScroll" })
            {
                var scroll = (UnityEngine.UI.ScrollRect)data.FindProperty(field).objectReferenceValue;
                Assert.That(scroll, Is.Not.Null, field);
                Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null, field);
                Assert.That(scroll.content.IsChildOf(scroll.viewport), Is.True, field);
            }
            var selected = (GalduryteSocketRowView)data.FindProperty("selectedRow").objectReferenceValue;
            var grid = (Transform)data.FindProperty("socketList").objectReferenceValue;
            Assert.That(selected.transform.IsChildOf(grid), Is.False, "Selected description is independent of the socket catalog.");
            Assert.That(grid.GetComponent<UnityEngine.UI.GridLayoutGroup>().constraintCount, Is.EqualTo(5));
            foreach (var field in new[] { "socketRowTemplate", "socketSlotTemplate", "equipmentSlotTemplate" })
            {
                var template = (Component)data.FindProperty(field).objectReferenceValue;
                Assert.That(template.transform.parent.gameObject.activeSelf, Is.False, field);
                Assert.That(template.transform.IsChildOf(view.transform), Is.True, field);
            }
            var button = (UnityEngine.UI.Button)data.FindProperty("extractButton").objectReferenceValue;
            Assert.That(button.transition, Is.EqualTo(UnityEngine.UI.Selectable.Transition.SpriteSwap));
            Assert.That(button.spriteState.disabledSprite, Is.Not.Null);
            Assert.That(button.GetComponent<UnityEngine.UI.Image>().sprite, Is.Not.Null);
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
            var reforge = (EquipmentUpgradeWindowView)window.FindProperty("equipmentReforgeService").objectReferenceValue;
            var reforgeData = new SerializedObject(reforge);
            foreach (var field in new[] { "equipmentCountText", "previewStatsText", "riskText", "availableGoldText", "missingGoldText", "costHeadingText", "reforgeOutcomesRoot", "reforgeIncreaseText", "reforgeUnchangedText", "reforgeDecreaseText", "reforgeIncreaseChanceText", "reforgeUnchangedChanceText", "reforgeDecreaseChanceText", "reforgeResultRoot", "reforgeResultNameText", "reforgeResultStatsText" })
            {
                var reference = reforgeData.FindProperty(field).objectReferenceValue;
                Assert.That(reference, Is.Not.Null, field);
                var transform = reference is GameObject go ? go.transform : ((Component)reference).transform;
                Assert.That(transform.IsChildOf(reforge.transform), Is.True, field);
            }
            var trade = new SerializedObject(prefab.GetComponentInChildren<TradeServiceWindowView>(true));
            foreach (var field in new[] { "partialSaleConfirmationRoot", "partialSaleText", "partialSaleConfirmButton", "partialSaleCancelButton", "playerGoldText", "traderGoldText" })
                Assert.That(trade.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(((GameObject)trade.FindProperty("partialSaleConfirmationRoot").objectReferenceValue).activeSelf, Is.False);
            foreach (var slot in prefab.GetComponentsInChildren<TradeItemSlotView>(true))
                Assert.That(new SerializedObject(slot).FindProperty("countBackground").objectReferenceValue, Is.Not.Null);
        }
    }
}
