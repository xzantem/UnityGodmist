using System;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Godmist.Editor
{
    /// <summary>Authors the material-crafting tab while preserving service and recipe references.</summary>
    public static class MaterialCraftingUiSetup
    {
        public const string PrefabPath = BlacksmithCraftingUiSetup.ServicesPath;
        static UIWorkspaceStylePalette palette;
        static readonly Color Cream = new Color32(223,215,184,255);
        [MenuItem("Godmist/Content/UI/Configure Material Crafting")]
        public static void Configure()
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var view=root.GetComponentInChildren<MaterialCraftingWindowView>(true);
                var body=Get<GameObject>(view,"serviceRoot").transform;
                Rect(body,Vector2.zero,Vector2.one,new Vector2(16,0),new Vector2(-16,0));
                body.GetComponent<HorizontalLayoutGroup>().spacing=28;
                var assembly=body.Find("AssemblyColumn"); var summary=body.Find("SummaryColumn");
                Element(assembly,452,-1,0,1); assembly.GetComponent<LayoutElement>().preferredWidth=500;
                Element(summary,330,-1,1,1);
                assembly.GetComponent<VerticalLayoutGroup>().spacing=12;
                summary.GetComponent<VerticalLayoutGroup>().spacing=12;
                var recipe=Section(assembly.Find("RecipeSection"),"Receptury",-1,1);
                var heading=assembly.Find("RecipeSection/SectionTitle").GetComponent<TextMeshProUGUI>();
                Rect(heading.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-28),new Vector2(-125,0));
                Wire(view,"recipeSectionLabel",heading);
                var counter=Node(heading.transform.parent,"RecipeCount",typeof(TextMeshProUGUI));
                Text(counter.GetComponent<TextMeshProUGUI>(),17); counter.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.Right;
                Rect(counter,new Vector2(1,1),Vector2.one,new Vector2(-125,-28),Vector2.zero);
                Wire(view,"recipeCountText",counter.GetComponent<TextMeshProUGUI>());
                var input=recipe.Find("RecipeSearch")?.GetComponent<TMP_InputField>();
                if(!input)
                {
                    var original=root.GetComponentInChildren<BlacksmithCraftingWindowView>(true);
                    input=Object.Instantiate(Get<TMP_InputField>(original,"customNameInput"),recipe,false);
                    input.name="RecipeSearch";
                }
                DisableStyles(input.gameObject); input.enabled=true; input.interactable=true;
                input.SetTextWithoutNotify(string.Empty); input.characterLimit=80;
                input.onValueChanged=new TMP_InputField.OnChangeEvent(); input.onEndEdit=new TMP_InputField.SubmitEvent();
                Rect(input.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-40),Vector2.zero);
                Text(input.textComponent,18); input.textComponent.raycastTarget=true;
                Text((TMP_Text)input.placeholder,17); ((TMP_Text)input.placeholder).text="Szukaj materiału…";
                Wire(view,"recipeSearchInput",input);
                var scroll=recipe.GetComponentInChildren<ScrollRect>(true);
                Rect(scroll.transform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-52));
                DisableStyles(scroll.gameObject); scroll.GetComponent<Image>().color=new Color32(37,34,40,255);
                scroll.transform.Find("Border").gameObject.SetActive(false);
                Rect(scroll.viewport,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-16,-4));
                var recipeGrid=Get<Transform>(view,"recipeGrid");
                recipeGrid.SetParent(scroll.viewport,false);scroll.content=(RectTransform)recipeGrid;
                Rect(recipeGrid,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero); ((RectTransform)recipeGrid).pivot=new Vector2(.5f,1);
                var grid=recipeGrid.GetComponent<GridLayoutGroup>(); if(grid)Object.DestroyImmediate(grid);
                var groups=recipeGrid.GetComponent<VerticalLayoutGroup>() ?? recipeGrid.gameObject.AddComponent<VerticalLayoutGroup>();
                groups.spacing=16;groups.childControlWidth=groups.childControlHeight=true;groups.childForceExpandWidth=true;groups.childForceExpandHeight=false;
                var groupTemplate=Node(recipeGrid,"RecipeGroupTemplate",typeof(MaterialRecipeGroupView),typeof(VerticalLayoutGroup));
                var groupLayout=groupTemplate.GetComponent<VerticalLayoutGroup>();groupLayout.spacing=8;groupLayout.childControlWidth=groupLayout.childControlHeight=true;groupLayout.childForceExpandWidth=true;groupLayout.childForceExpandHeight=false;
                var groupTitle=Node(groupTemplate,"Heading",typeof(TextMeshProUGUI),typeof(LayoutElement));Text(groupTitle.GetComponent<TextMeshProUGUI>(),19);Element(groupTitle,-1,26,1,0);
                var groupContent=Node(groupTemplate,"Recipes",typeof(GridLayoutGroup));var tiles=groupContent.GetComponent<GridLayoutGroup>();
                tiles.cellSize=new Vector2(80,80);tiles.spacing=new Vector2(8,8);tiles.constraint=GridLayoutGroup.Constraint.FixedColumnCount;tiles.constraintCount=5;tiles.childAlignment=TextAnchor.UpperCenter;
                var groupView=groupTemplate.GetComponent<MaterialRecipeGroupView>();Wire(groupView,"heading",groupTitle.GetComponent<TextMeshProUGUI>());Wire(groupView,"content",groupContent);Wire(view,"recipeGroupTemplate",groupView);
                Get<BlacksmithCraftingSlotView>(view,"recipeSlotTemplate").transform.SetParent(groupContent,false);groupTemplate.gameObject.SetActive(false);
                var fitter=recipeGrid.GetComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var recipeSlot=Get<BlacksmithCraftingSlotView>(view,"recipeSlotTemplate");StyleSlot(recipeSlot,64);
                var output=Get<TextMeshProUGUI>(recipeSlot,"countText");Text(output,16);
                Rect(output.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-34,3),new Vector2(-5,24));
                var divider=Node(summary,"ColumnDivider",typeof(Image),typeof(LayoutElement));divider.GetComponent<LayoutElement>().ignoreLayout=true;
                Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));Line(divider);
                var preview=Section(summary.Find("PreviewSection"),"Wynik",168,0);
                var layout=preview.GetComponent<VerticalLayoutGroup>();layout.spacing=6;layout.padding=new RectOffset();
                Element(preview.Find("ResultHeader"),-1,80,1,0);
                var result=Get<BlacksmithCraftingSlotView>(view,"previewTooltipSlot");StyleSlot(result,64);Element(result.transform,80,80,0,0);
                var name=Get<TextMeshProUGUI>(view,"previewNameText");Text(name,23);Element(name.transform,-1,30,1,0);name.overflowMode=TextOverflowModes.Ellipsis;
                var inheritedInput=name.GetComponent<TMP_InputField>();if(inheritedInput)inheritedInput.enabled=false;
                var info=Get<TextMeshProUGUI>(view,"previewSummaryText");Text(info,17);Element(info.transform,-1,22,1,0);
                var description=Node(preview,"ResultDescription",typeof(TextMeshProUGUI),typeof(LayoutElement));
                var descriptionText=description.GetComponent<TextMeshProUGUI>();Text(descriptionText,17);descriptionText.overflowMode=TextOverflowModes.Ellipsis;
                Element(description,-1,44,1,0);Wire(view,"previewDescriptionText",descriptionText);
                var variants=Get<GameObject>(view,"variantSection").transform;Section(variants,"Wariant składników",80,0);
                foreach(var field in new[]{"firstVariantDropdown","secondVariantDropdown"})
                {
                    var dropdown=Get<TMP_Dropdown>(view,field);StyleQuality(dropdown);
                    var r=(RectTransform)dropdown.transform;r.offsetMin=new Vector2(r.offsetMin.x,0);r.offsetMax=new Vector2(r.offsetMax.x,40);
                    dropdown.template.sizeDelta=new Vector2(dropdown.template.sizeDelta.x,180);
                    dropdown.captionText.overflowMode=TextOverflowModes.Ellipsis;
                }
                var materials=Section(summary.Find("MaterialsSection"),"Wymagane materiały",100,1);
                Wire(view,"materialsSectionLabel",summary.Find("MaterialsSection/SectionTitle").GetComponent<TextMeshProUGUI>());
                var materialGrid=Get<Transform>(view,"materialsGrid");
                var materialScrollNode=Node(materials,"MaterialsScroll",typeof(ScrollRect));Rect(materialScrollNode,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                var viewport=Node(materialScrollNode,"Viewport",typeof(Image),typeof(RectMask2D));viewport.GetComponent<Image>().color=Color.clear;
                Rect(viewport,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-14,0));materialGrid.SetParent(viewport,false);
                Rect(materialGrid,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);((RectTransform)materialGrid).pivot=new Vector2(.5f,1);
                var materialScroll=materialScrollNode.GetComponent<ScrollRect>();materialScroll.content=(RectTransform)materialGrid;materialScroll.viewport=viewport;
                materialScroll.horizontal=false;materialScroll.vertical=true;materialScroll.movementType=ScrollRect.MovementType.Clamped;materialScroll.scrollSensitivity=24;
                var scrollbar=materialScrollNode.Find("Scrollbar")?.GetComponent<Scrollbar>();
                if(!scrollbar){scrollbar=Object.Instantiate(scroll.verticalScrollbar,materialScrollNode,false);scrollbar.name="Scrollbar";}
                Rect(scrollbar.transform,new Vector2(1,0),Vector2.one,new Vector2(-10,0),Vector2.zero);
                materialScroll.verticalScrollbar=scrollbar;materialScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                var materialFitter=materialGrid.GetComponent<ContentSizeFitter>();materialFitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;materialFitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var costs=materialGrid.GetComponent<GridLayoutGroup>();costs.constraint=GridLayoutGroup.Constraint.FixedColumnCount;costs.constraintCount=3;costs.cellSize=new Vector2(96,142);costs.spacing=new Vector2(10,12);costs.childAlignment=TextAnchor.UpperCenter;
                var material=Get<BlacksmithCraftingSlotView>(view,"materialSlotTemplate");
                var slot=Get<Button>(material,"button"); Rect(slot.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-42,-84),new Vector2(42,0));StyleSlot(material,64);
                var count=Get<TextMeshProUGUI>(material,"countText");count.transform.SetParent(material.transform,false);Text(count,18);count.alignment=TextAlignmentOptions.Center;count.raycastTarget=true;
                if(!count.GetComponent<MaterialCraftingCountTooltipView>())count.gameObject.AddComponent<MaterialCraftingCountTooltipView>();
                Rect(count.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-110),new Vector2(0,-86));
                var buy=Get<Button>(material,"buyButton");StyleButton(buy);Rect(buy.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,30));
                var coin=buy.transform.Find("Icon").GetComponent<Image>();DisableStyles(coin.gameObject);coin.gameObject.SetActive(true);coin.enabled=true;
                coin.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");coin.color=Color.white;coin.preserveAspect=true;
                Rect(coin.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(8,-8),new Vector2(24,8));
                var price=Get<TextMeshProUGUI>(material,"buyPriceText");Text(price,18);Rect(price.transform,Vector2.zero,Vector2.one,new Vector2(28,2),new Vector2(-4,-2));
                var status=Get<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,40,1,0);
                var craft=Get<Button>(view,"craftButton");StyleButton(craft);Element(craft.transform,-1,44,1,0);craft.transform.Find("Border").gameObject.SetActive(false);
                Text(craft.GetComponentInChildren<TextMeshProUGUI>(true),21);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
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
        static void StyleQuality(TMP_Dropdown dropdown)
        {
            Element(dropdown.transform, 170, 40, 0, 0); Text((TextMeshProUGUI)dropdown.captionText, 18);
            Text((TextMeshProUGUI)dropdown.itemText, 18);
            dropdown.itemText.color = Cream;
            var template = dropdown.template; Rect(template, new Vector2(0, 1), Vector2.one, new Vector2(0, 4), new Vector2(0, 126));
            template.pivot = new Vector2(.5f, 0); template.anchoredPosition = new Vector2(0, 4);
            var item = dropdown.itemText.transform.parent.GetComponent<RectTransform>(); item.sizeDelta = new Vector2(item.sizeDelta.x, 36);
            var content = item.parent.GetComponent<RectTransform>(); content.sizeDelta = new Vector2(content.sizeDelta.x, 44);
            var background = item.Find("Item Background").GetComponent<Image>(); DisableStyles(background.gameObject); background.sprite = palette.GetArtwork("button_normal"); background.type = Image.Type.Sliced;
            var toggle = item.GetComponent<Toggle>(); toggle.transition = Selectable.Transition.ColorTint; var colors = toggle.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f,1.12f,1.12f); colors.selectedColor = Color.white; toggle.colors = colors;
            Rect(dropdown.itemText.transform, Vector2.zero, Vector2.one, new Vector2(26, 2), new Vector2(-6, -2));
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
