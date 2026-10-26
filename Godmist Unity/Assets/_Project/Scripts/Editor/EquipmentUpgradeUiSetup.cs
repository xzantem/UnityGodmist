using System;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Godmist.Editor
{
    /// <summary>Authors the upgrade and reforge service presentations independently.</summary>
    public static class EquipmentUpgradeUiSetup
    {
        const string Path=BlacksmithCraftingUiSetup.ServicesPath;
        static UIWorkspaceStylePalette palette;
        static readonly Color Cream=new Color32(223,215,184,255);
        [MenuItem("Godmist/Content/UI/Configure Equipment Reforge")]
        public static void ConfigureReforge()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var view=Get<EquipmentUpgradeWindowView>(root.GetComponent<NpcServicesWindowView>(),"equipmentReforgeService");
                foreach(var style in view.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
                { style.enabled=false; Wire(style,"text",null); }
                var body=Get<GameObject>(view,"serviceRoot").transform;
                Rect(body,Vector2.zero,Vector2.one,new Vector2(16,0),new Vector2(-16,0));
                body.GetComponent<HorizontalLayoutGroup>().spacing=28;
                var assembly=body.Find("AssemblyColumn");var summary=body.Find("SummaryColumn");
                Element(assembly,452,-1,0,1);assembly.GetComponent<LayoutElement>().preferredWidth=500;
                Element(summary,330,-1,1,1);
                assembly.GetComponent<VerticalLayoutGroup>().spacing=16;
                summary.GetComponent<VerticalLayoutGroup>().spacing=12;
                var equipment=Section(assembly.Find("EquipmentSection"),"Wyposażenie",120,1);
                var count=Node(equipment.parent,"EquipmentCount",typeof(TextMeshProUGUI));
                Text(count.GetComponent<TextMeshProUGUI>(),17);count.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.Right;
                Rect(count,new Vector2(1,1),Vector2.one,new Vector2(-80,-28),Vector2.zero);
                Wire(view,"equipmentCountText",count.GetComponent<TextMeshProUGUI>());
                var scroll=equipment.GetComponentInChildren<ScrollRect>(true);
                DisableStyles(scroll.gameObject);scroll.GetComponent<Image>().color=new Color32(37,34,40,255);
                scroll.transform.Find("Border").gameObject.SetActive(false);
                Rect(scroll.viewport,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-16,-4));
                var grid=Get<Transform>(view,"equipmentGrid").GetComponent<GridLayoutGroup>();
                grid.cellSize=new Vector2(80,80);grid.spacing=new Vector2(8,8);grid.constraintCount=5;grid.childAlignment=TextAnchor.UpperCenter;
                var template=Get<BlacksmithCraftingSlotView>(view,"equipmentSlotTemplate");StyleSlot(template,64);
                var badge=Get<TextMeshProUGUI>(template,"countText");Text(badge,15);badge.alignment=TextAlignmentOptions.Right;
                Rect(badge.transform,Vector2.zero,new Vector2(1,0),new Vector2(4,3),new Vector2(-4,23));
                var risk=Node(assembly,"ReforgeRisk",typeof(TextMeshProUGUI),typeof(LayoutElement));
                Text(risk.GetComponent<TextMeshProUGUI>(),17);risk.GetComponent<TextMeshProUGUI>().color=new Color32(211,181,130,255);
                Element(risk,-1,50,1,0);Wire(view,"riskText",risk.GetComponent<TextMeshProUGUI>());
                var divider=Node(summary,"ColumnDivider",typeof(Image),typeof(LayoutElement));
                divider.GetComponent<LayoutElement>().ignoreLayout=true;
                Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));Line(divider);
                var preview=Section(summary.Find("PreviewSection"),"Podgląd",260,0);
                var layout=preview.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=8;
                Element(preview.Find("ResultHeader"),-1,80,1,0);
                var slot=Get<BlacksmithCraftingSlotView>(view,"previewSlot");StyleSlot(slot,64);Element(slot.transform,80,80,0,0);
                var name=Get<TextMeshProUGUI>(view,"previewNameText");Text(name,23);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;Element(name.transform,0,-1,1,0);
                var description=Get<TextMeshProUGUI>(view,"previewText");Text(description,17);Element(description.transform,-1,38,1,0);
                description.overflowMode=TextOverflowModes.Ellipsis;
                var heading=Node(preview,"ReforgeOutcomesHeading",typeof(TextMeshProUGUI),typeof(LayoutElement));
                Text(heading.GetComponent<TextMeshProUGUI>(),21);Element(heading,-1,28,1,0);Wire(view,"previewStatsText",heading.GetComponent<TextMeshProUGUI>());
                var outcomes=Node(preview,"ReforgeOutcomes",typeof(VerticalLayoutGroup),typeof(LayoutElement));
                Element(outcomes,-1,96,1,0);var rows=outcomes.GetComponent<VerticalLayoutGroup>();rows.spacing=0;
                rows.childControlWidth=rows.childControlHeight=true;rows.childForceExpandWidth=true;rows.childForceExpandHeight=false;
                Wire(view,"reforgeOutcomesRoot",outcomes.gameObject);
                foreach(var outcome in new[]{"Increase","Unchanged","Decrease"})
                {
                    var row=Node(outcomes,outcome,typeof(HorizontalLayoutGroup),typeof(LayoutElement));Element(row,-1,32,1,0);
                    var rowLayout=row.GetComponent<HorizontalLayoutGroup>();rowLayout.spacing=8;
                    rowLayout.childControlWidth=rowLayout.childControlHeight=true;rowLayout.childForceExpandWidth=false;rowLayout.childForceExpandHeight=false;
                    var label=Node(row,"Label",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(label.GetComponent<TextMeshProUGUI>(),18);Element(label,0,32,1,0);
                    var chance=Node(row,"Chance",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(chance.GetComponent<TextMeshProUGUI>(),18);Element(chance,52,32,0,0);
                    chance.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.TopRight;
                    chance.GetComponent<TextMeshProUGUI>().color=outcome=="Increase"?new Color32(112,199,135,255):outcome=="Decrease"?new Color32(224,108,117,255):Cream;
                    Wire(view,"reforge"+outcome+"Text",label.GetComponent<TextMeshProUGUI>());
                    Wire(view,"reforge"+outcome+"ChanceText",chance.GetComponent<TextMeshProUGUI>());
                }
                var cost=Section(summary.Find("CostSection"),"Koszt próby",128,0);
                Wire(view,"costHeadingText",cost.parent.Find("SectionTitle").GetComponent<TextMeshProUGUI>());
                var price=Get<TextMeshProUGUI>(view,"costText");Text(price,25);price.alignment=TextAlignmentOptions.Left;
                Rect(price.transform,new Vector2(0,1),Vector2.one,new Vector2(36,-34),Vector2.zero);
                var coin=Node(cost,"GoldIcon",typeof(Image));coin.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
                coin.GetComponent<Image>().preserveAspect=true;coin.GetComponent<Image>().raycastTarget=false;
                Rect(coin,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-32),new Vector2(28,-4));
                var available=Node(cost,"AvailableGold",typeof(TextMeshProUGUI));Text(available.GetComponent<TextMeshProUGUI>(),17);
                Rect(available,new Vector2(0,1),Vector2.one,new Vector2(0,-62),new Vector2(0,-36));Wire(view,"availableGoldText",available.GetComponent<TextMeshProUGUI>());
                var missing=Node(cost,"MissingGold",typeof(TextMeshProUGUI));Text(missing.GetComponent<TextMeshProUGUI>(),17);
                missing.GetComponent<TextMeshProUGUI>().color=new Color32(224,108,117,255);
                Rect(missing,new Vector2(0,1),Vector2.one,new Vector2(0,-88),new Vector2(0,-62));Wire(view,"missingGoldText",missing.GetComponent<TextMeshProUGUI>());
                var spacer=Node(summary,"SummarySpacer",typeof(LayoutElement));spacer.SetSiblingIndex(summary.Find("StatusText").GetSiblingIndex());Element(spacer,-1,0,1,1);
                var status=Get<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,64,1,0);
                var button=Get<Button>(view,"actionButton");StyleButton(button);Element(button.transform,-1,44,1,0);
                button.transform.Find("Border").gameObject.SetActive(false);Text(button.GetComponentInChildren<TextMeshProUGUI>(true),21);
                ConfigureReforgeResult(view);
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Godmist/Content/UI/Configure Equipment Reforge Result")]
        public static void ConfigureReforgeResult()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                ConfigureReforgeResult(Get<EquipmentUpgradeWindowView>(root.GetComponent<NpcServicesWindowView>(),"equipmentReforgeService"));
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static void ConfigureReforgeResult(EquipmentUpgradeWindowView view)
        {
            var summary=Get<GameObject>(view,"serviceRoot").transform.Find("SummaryColumn");
            summary.GetComponent<VerticalLayoutGroup>().spacing=8;
            var preview=summary.Find("PreviewSection");Element(preview,-1,316,1,0);
            preview.Find("Content").GetComponent<VerticalLayoutGroup>().spacing=4;
            Element(Get<TextMeshProUGUI>(view,"previewStatsText").transform,-1,24,1,0);
            var outcomes=Get<GameObject>(view,"reforgeOutcomesRoot").transform;Element(outcomes,-1,72,1,0);
            foreach(Transform row in outcomes)
            {
                Element(row,-1,24,1,0);
                foreach(Transform cell in row) { Text(cell.GetComponent<TextMeshProUGUI>(),17);Element(cell,cell.name=="Chance"?52:0,24,cell.name=="Chance"?0:1,0); }
            }
            Element(summary.Find("CostSection"),-1,104,1,0);
            Element(Get<TextMeshProUGUI>(view,"statusText").transform,-1,48,1,0);
            var existing=Get<GameObject>(view,"reforgeResultRoot");
            var result=existing!=null?(RectTransform)existing.transform:Node(preview.Find("Content"),"ReforgeResult",typeof(VerticalLayoutGroup),typeof(LayoutElement));
            result.SetParent(preview.Find("Content"),false);
            result.SetSiblingIndex(preview.Find("Content/ResultHeader").GetSiblingIndex()+1);Element(result,-1,92,1,0);
            var layout=result.GetComponent<VerticalLayoutGroup>();layout.spacing=0;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var name=Node(result,"ResultName",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(name.GetComponent<TextMeshProUGUI>(),18);Element(name,0,24,1,0);
            name.GetComponent<TextMeshProUGUI>().textWrappingMode=TextWrappingModes.NoWrap;name.GetComponent<TextMeshProUGUI>().overflowMode=TextOverflowModes.Ellipsis;
            var stats=Node(result,"StatChanges",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(stats.GetComponent<TextMeshProUGUI>(),17);Element(stats,0,68,1,0);
            Wire(view,"reforgeResultRoot",result.gameObject);Wire(view,"reforgeResultNameText",name.GetComponent<TextMeshProUGUI>());Wire(view,"reforgeResultStatsText",stats.GetComponent<TextMeshProUGUI>());
            result.gameObject.SetActive(false);
        }
        [MenuItem("Godmist/Content/UI/Configure Equipment Upgrade")]
        public static void Configure()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var shell=root.GetComponent<NpcServicesWindowView>();
                var view=Get<EquipmentUpgradeWindowView>(shell,"equipmentUpgradeService");
                var body=Get<GameObject>(view,"serviceRoot").transform;
                foreach(var style in view.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
                {
                    style.enabled=false;
                    // OnValidate runs for disabled styles when Unity loads the prefab.
                    // These text nodes now have explicit sizes authored by this setup.
                    Wire(style,"text",null);
                }
                Rect(body,Vector2.zero,Vector2.one,new Vector2(16,0),new Vector2(-16,0));
                body.GetComponent<HorizontalLayoutGroup>().spacing=28;
                var assembly=body.Find("AssemblyColumn");var summary=body.Find("SummaryColumn");
                Element(assembly,452,-1,0,1);assembly.GetComponent<LayoutElement>().preferredWidth=500;
                Element(summary,330,-1,1,1);assembly.GetComponent<VerticalLayoutGroup>().spacing=16;summary.GetComponent<VerticalLayoutGroup>().spacing=16;
                var equipment=Section(assembly.Find("EquipmentSection"),"Wyposażenie",120,1);
                var count=Node(equipment.parent,"EquipmentCount",typeof(TextMeshProUGUI));Text(count.GetComponent<TextMeshProUGUI>(),17);count.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.Right;
                Rect(count,new Vector2(1,1),Vector2.one,new Vector2(-80,-28),Vector2.zero);Wire(view,"equipmentCountText",count.GetComponent<TextMeshProUGUI>());
                var scroll=equipment.GetComponentInChildren<ScrollRect>(true);DisableStyles(scroll.gameObject);scroll.GetComponent<Image>().color=new Color32(37,34,40,255);scroll.transform.Find("Border").gameObject.SetActive(false);
                Rect(scroll.viewport,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-16,-4));
                var grid=Get<Transform>(view,"equipmentGrid").GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(80,80);grid.spacing=new Vector2(8,8);grid.constraintCount=5;grid.childAlignment=TextAnchor.UpperCenter;
                var template=Get<BlacksmithCraftingSlotView>(view,"equipmentSlotTemplate");StyleSlot(template,64);
                var badge=Get<TextMeshProUGUI>(template,"countText");Text(badge,15);Rect(badge.transform,new Vector2(0,0),new Vector2(1,0),new Vector2(4,3),new Vector2(-4,23));badge.alignment=TextAlignmentOptions.Right;
                var settings=Section(assembly.Find("SettingsSection"),"Parametry ulepszenia",188,0);
                var horizontal=settings.GetComponent<HorizontalLayoutGroup>();if(horizontal)Object.DestroyImmediate(horizontal);
                var options=settings.GetComponent<VerticalLayoutGroup>() ?? settings.gameObject.AddComponent<VerticalLayoutGroup>();options.spacing=12;options.childControlWidth=options.childControlHeight=true;options.childForceExpandWidth=true;options.childForceExpandHeight=false;
                foreach(var field in new[]{"targetProgressSlider","successChanceSlider"})
                {
                    var slider=Get<Slider>(view,field);var group=slider.transform.parent;Element(group,-1,66,1,0);group.GetComponent<VerticalLayoutGroup>().spacing=6;
                    var label=group.GetComponentInChildren<TextMeshProUGUI>(true);Text(label,18);Element(label.transform,-1,26,1,0);Element(slider.transform,-1,32,1,0);
                    var style=slider.GetComponent<UIWorkspaceSliderStyleApplier>();if(style)style.enabled=false;
                    var fill=slider.fillRect.GetComponent<Image>();fill.sprite=palette.GetArtwork("fill_gold");fill.color=Color.white;fill.type=Image.Type.Sliced;
                    Rect(slider.transform.Find("Background"),Vector2.zero,Vector2.one,new Vector2(0,6),new Vector2(0,-6));
                    Rect(slider.transform.Find("GothicBarFrame"),Vector2.zero,Vector2.one,new Vector2(0,6),new Vector2(0,-6));
                    ConfigureSliderGeometry(slider);
                }
                var divider=Node(summary,"ColumnDivider",typeof(Image),typeof(LayoutElement));divider.GetComponent<LayoutElement>().ignoreLayout=true;Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));Line(divider);
                var preview=Section(summary.Find("PreviewSection"),"Podgląd",208,0);
                var layout=preview.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=8;
                Element(preview.Find("ResultHeader"),-1,80,1,0);
                var result=Get<BlacksmithCraftingSlotView>(view,"previewSlot");StyleSlot(result,64);Element(result.transform,80,80,0,0);
                var name=Get<TextMeshProUGUI>(view,"previewNameText");Text(name,23);name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;Element(name.transform,0,-1,1,0);
                var description=Get<TextMeshProUGUI>(view,"previewText");Text(description,17);Element(description.transform,-1,38,1,0);description.overflowMode=TextOverflowModes.Ellipsis;
                var stats=Node(preview,"UpgradeStats",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(stats.GetComponent<TextMeshProUGUI>(),19);Element(stats,-1,84,1,0);stats.GetComponent<TextMeshProUGUI>().lineSpacing=6;Wire(view,"previewStatsText",stats.GetComponent<TextMeshProUGUI>());
                var risk=Node(assembly,"UpgradeRisk",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(risk.GetComponent<TextMeshProUGUI>(),17);risk.GetComponent<TextMeshProUGUI>().color=new Color32(211,181,130,255);Element(risk,-1,76,1,0);Wire(view,"riskText",risk.GetComponent<TextMeshProUGUI>());
                var cost=Section(summary.Find("CostSection"),"Koszt próby",142,0);
                var price=Get<TextMeshProUGUI>(view,"costText");Text(price,25);price.alignment=TextAlignmentOptions.Left;
                Rect(price.transform,new Vector2(0,1),Vector2.one,new Vector2(36,-34),Vector2.zero);
                var coin=Node(cost,"GoldIcon",typeof(Image));coin.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");coin.GetComponent<Image>().preserveAspect=true;coin.GetComponent<Image>().raycastTarget=false;Rect(coin,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-32),new Vector2(28,-4));
                var available=Node(cost,"AvailableGold",typeof(TextMeshProUGUI));Text(available.GetComponent<TextMeshProUGUI>(),17);Rect(available,new Vector2(0,1),Vector2.one,new Vector2(0,-66),new Vector2(0,-40));Wire(view,"availableGoldText",available.GetComponent<TextMeshProUGUI>());
                var missing=Node(cost,"MissingGold",typeof(TextMeshProUGUI));Text(missing.GetComponent<TextMeshProUGUI>(),17);missing.GetComponent<TextMeshProUGUI>().color=new Color32(224,108,117,255);Rect(missing,new Vector2(0,1),Vector2.one,new Vector2(0,-94),new Vector2(0,-68));Wire(view,"missingGoldText",missing.GetComponent<TextMeshProUGUI>());
                var spacer=Node(summary,"SummarySpacer",typeof(LayoutElement));spacer.SetSiblingIndex(summary.Find("StatusText").GetSiblingIndex());Element(spacer,-1,0,1,1);
                var status=Get<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,64,1,0);
                var button=Get<Button>(view,"actionButton");StyleButton(button);Element(button.transform,-1,44,1,0);button.transform.Find("Border").gameObject.SetActive(false);Text(button.GetComponentInChildren<TextMeshProUGUI>(true),21);
                ConfigureRangeLabels(view);
                ConfigureStatChanges(view);
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Godmist/Content/UI/Fix Equipment Upgrade Slider Geometry")]
        public static void ConfigureSliderGeometry()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                var view=Get<EquipmentUpgradeWindowView>(root.GetComponent<NpcServicesWindowView>(),"equipmentUpgradeService");
                foreach(var field in new[]{"targetProgressSlider","successChanceSlider"})
                    ConfigureSliderGeometry(Get<Slider>(view,field));
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static void ConfigureSliderGeometry(Slider slider)
        {
            Rect(slider.transform.Find("Fill Area"),Vector2.zero,Vector2.one,new Vector2(8,6),new Vector2(-8,-6));
            // Slider.UpdateVisuals stretches the handle on its non-moving axis.
            // A zero-height, centred area preserves a fixed 16 x 32 thumb.
            Rect(slider.handleRect.parent,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(8,0),new Vector2(-8,0));
            slider.handleRect.pivot=new Vector2(.5f,.5f);
            slider.handleRect.anchoredPosition=Vector2.zero;
            slider.handleRect.sizeDelta=new Vector2(16,32);
            slider.handleRect.GetComponent<Image>().preserveAspect=true;
        }
        [MenuItem("Godmist/Content/UI/Configure Equipment Upgrade Range Labels")]
        public static void ConfigureRangeLabels()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var view=Get<EquipmentUpgradeWindowView>(root.GetComponent<NpcServicesWindowView>(),"equipmentUpgradeService");
                ConfigureRangeLabels(view);
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static void ConfigureRangeLabels(EquipmentUpgradeWindowView view)
        {
            var body=Get<GameObject>(view,"serviceRoot").transform;
            Element(body.Find("AssemblyColumn/SettingsSection"),-1,228,1,0);
            Element(Get<TextMeshProUGUI>(view,"riskText").transform,-1,50,1,0);
            foreach(var field in new[]{"targetProgressSlider","successChanceSlider"})
            {
                var slider=Get<Slider>(view,field);
                var group=slider.transform.parent;
                Element(group,-1,92,1,0);
                var range=Node(group,"RangeLabels",typeof(HorizontalLayoutGroup),typeof(LayoutElement));
                Element(range,-1,22,1,0);
                var layout=range.GetComponent<HorizontalLayoutGroup>();
                layout.childControlWidth=layout.childControlHeight=true;
                layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var minimum=Node(range,"Minimum",typeof(TextMeshProUGUI),typeof(LayoutElement));
                var maximum=Node(range,"Maximum",typeof(TextMeshProUGUI),typeof(LayoutElement));
                Text(minimum.GetComponent<TextMeshProUGUI>(),17);Text(maximum.GetComponent<TextMeshProUGUI>(),17);
                Element(minimum,0,22,1,0);Element(maximum,0,22,1,0);
                minimum.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.Left;
                maximum.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.Right;
                minimum.GetComponent<TextMeshProUGUI>().text="0%";
                maximum.GetComponent<TextMeshProUGUI>().text=field=="targetProgressSlider" ? "20%" : "99%";
                var prefix=field=="targetProgressSlider" ? "target" : "chance";
                Wire(view,prefix+"MinimumText",minimum.GetComponent<TextMeshProUGUI>());
                Wire(view,prefix+"MaximumText",maximum.GetComponent<TextMeshProUGUI>());
            }
            Wire(view,"costHeadingText",body.Find("SummaryColumn/CostSection/SectionTitle").GetComponent<TextMeshProUGUI>());
        }
        [MenuItem("Godmist/Content/UI/Configure Equipment Upgrade Stat Changes")]
        public static void ConfigureStatChanges()
        {
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                ConfigureStatChanges(Get<EquipmentUpgradeWindowView>(root.GetComponent<NpcServicesWindowView>(),"equipmentUpgradeService"));
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static void ConfigureStatChanges(EquipmentUpgradeWindowView view)
        {
            var body=Get<GameObject>(view,"serviceRoot").transform;
            Element(body.Find("SummaryColumn/PreviewSection"),-1,234,1,0);
            var stats=Get<TextMeshProUGUI>(view,"previewStatsText");
            Text(stats,18);stats.lineSpacing=0;Element(stats.transform,-1,110,1,0);
            Element(body.Find("SummaryColumn/CostSection"),-1,128,1,0);
        }
        static Transform Section(Transform section, string title, float height, float flex)
        {
            DisableStyles(section.gameObject); section.GetComponent<Image>().enabled = false;
            section.Find("Border").gameObject.SetActive(false); Element(section, -1, height, 1, flex);
            var heading = section.Find("SectionTitle").GetComponent<TextMeshProUGUI>(); Text(heading, 23); heading.text = title;
            Rect(heading.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
            var content = section.Find("Content") ?? section.Find("EquipmentContent");
            if (content == null) throw new InvalidOperationException("Section content is missing: " + section.name);
            Rect(content, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32)); return content;
        }
        static void StyleSlot(BlacksmithCraftingSlotView slot, float iconSize)
        {
            var button = Get<Button>(slot, "button"); DisableStyles(button.gameObject); button.transition = Selectable.Transition.None;
            var surface = button.GetComponent<Image>(); surface.sprite = palette.GetArtwork("item_frame"); surface.type = Image.Type.Sliced; surface.color = Color.white;
            var border = Get<Image>(slot, "border"); DisableStyles(border.gameObject); Rect(border.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); border.raycastTarget = false;
            var icon = Get<Image>(slot, "icon"); Rect(icon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-iconSize/2, -iconSize/2), new Vector2(iconSize/2, iconSize/2)); icon.preserveAspect = true; icon.raycastTarget = false;
            Get<TextMeshProUGUI>(slot, "countText").gameObject.SetActive(false);
        }
        static void StyleButton(Button button)
        {
            DisableStyles(button.gameObject); var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork("tab_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            button.targetGraphic = image; image.overrideSprite = null; button.transition = Selectable.Transition.SpriteSwap; var state = button.spriteState;
            state.highlightedSprite = palette.GetArtwork("tab_hover"); state.selectedSprite = image.sprite; state.pressedSprite = palette.GetArtwork("tab_pressed"); state.disabledSprite = palette.GetArtwork("tab_disabled"); button.spriteState = state;
        }
        static void DisableStyles(GameObject go) { foreach(var style in go.GetComponents<UIWorkspaceStyleApplier>())style.enabled = false; }
        static void Text(TMP_Text text, int size) { DisableStyles(text.gameObject); text.font = palette.Font; text.fontSize = size; text.color = Cream; text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        static void Element(Transform t, float width, float height, float flexWidth, float flexHeight) { var e=t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth=width; e.preferredWidth=width; e.minHeight=height; e.preferredHeight=height; e.flexibleWidth=flexWidth; e.flexibleHeight=flexHeight; }
        static RectTransform Node(Transform parent,string name,params Type[] types) { var t=parent.Find(name) as RectTransform; if(t)return t; var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); foreach(var type in types)go.AddComponent(type); return (RectTransform)go.transform; }
        static void Rect(Transform t,Vector2 min,Vector2 max,Vector2 leftBottom,Vector2 rightTop) { var r=(RectTransform)t; r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f); r.offsetMin=leftBottom; r.offsetMax=rightTop; }
        static void Line(RectTransform t) { var im=t.GetComponent<Image>(); im.color=new Color32(107,109,87,255); im.raycastTarget=false; }
        static T Get<T>(Object owner,string field) where T:Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        static void Wire(Object owner,string field,Object value) { var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
