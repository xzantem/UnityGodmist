using System;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Native styling scoped to the shared exploration/combat inspection window.</summary>
public static class GodmistInspectWindowSetup
{
    const string Path="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatUnitInspectPanel.prefab";
    static UIWorkspaceStylePalette palette;
    static readonly Color TextColor=new(.91f,.875f,.78f);
    static T Ref<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object=>new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
    static void Wire(UnityEngine.Object owner,string name,UnityEngine.Object value){var so=new SerializedObject(owner);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Stretch(RectTransform t,float l=0,float b=0,float r=0,float top=0){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=new Vector2(l,b);t.offsetMax=new Vector2(-r,-top);}
    static void Top(RectTransform t,float y,float height){t.anchorMin=new Vector2(0,1);t.anchorMax=Vector2.one;t.pivot=new Vector2(.5f,1);t.anchoredPosition=new Vector2(0,-y);t.sizeDelta=new Vector2(0,height);}
    static RectTransform Node(Transform parent,string name){var t=parent.Find(name) as RectTransform;if(t==null){t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);}t.gameObject.layer=parent.gameObject.layer;return t;}
    static void Text(TMP_Text t,float size,TextAlignmentOptions align=TextAlignmentOptions.TopLeft){t.font=palette.Font;t.fontSharedMaterial=palette.Font.material;t.fontSize=size;t.color=TextColor;t.fontStyle=FontStyles.Normal;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.alignment=align;t.margin=Vector4.zero;t.raycastTarget=false;t.enabled=true;}
    static TMP_Text Label(Transform parent,string name,string value,float size){var rect=Node(parent,name);var t=rect.GetComponent<TextMeshProUGUI>()??rect.gameObject.AddComponent<TextMeshProUGUI>();Text(t,size);t.text=value;return t;}
    static void Paint(Image i,string name,bool hollow=false){i.sprite=palette.GetArtwork(name)??throw new Exception("Missing UI art "+name);i.overrideSprite=null;i.color=Color.white;i.type=i.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;i.fillCenter=!hollow;}
    static Image Art(Transform parent,string name,string artwork,bool hollow=false){var t=Node(parent,name);var i=t.GetComponent<Image>()??t.gameObject.AddComponent<Image>();Paint(i,artwork,hollow);i.raycastTarget=false;return i;}
    static LayoutElement Layout(Component node,float height){var l=node.GetComponent<LayoutElement>()??node.gameObject.AddComponent<LayoutElement>();l.minHeight=height;l.preferredHeight=height;l.flexibleHeight=0;return l;}
    static void TableText(TMP_Text text,float left,float right){Text(text,16);var fit=text.GetComponent<ContentSizeFitter>();if(fit!=null)fit.enabled=false;var l=text.GetComponent<LayoutElement>();if(l!=null)l.enabled=false;var t=text.rectTransform;t.anchorMin=new Vector2(left,0);t.anchorMax=new Vector2(right,1);t.offsetMin=Vector2.zero;t.offsetMax=new Vector2(0,-30);}
    static void Bar(Slider slider,TMP_Text value,float y,Color color)
    {
        slider.interactable=false;Top((RectTransform)slider.transform,y,12);
        var track=slider.transform.Find("Background").GetComponent<Image>();Paint(track,"slider_track");track.raycastTarget=false;
        var fill=slider.fillRect.GetComponent<Image>();Paint(fill,"fill_runtime");fill.type=Image.Type.Simple;fill.color=color;fill.raycastTarget=false;
        Stretch((RectTransform)slider.transform.Find("Fill Area"),6,3,6,3);
        // The track artwork is opaque; using it above the fill hides the actual value.
        slider.transform.Find("GothicBarFrame").GetComponent<Image>().enabled=false;
        Text(value,16,TextAlignmentOptions.TopRight);Top(value.rectTransform,-26,22);
    }
    [MenuItem("Godmist/UI/Restyle inspection window")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)){style.enabled=false;Wire(style,"palette",null);}
            var view=root.GetComponent<CombatUnitInspectWindowView>();var window=root.GetComponent<UIWorkspaceWindow>();
            var ws=new SerializedObject(window);ws.FindProperty("minimumSize").vector2Value=new Vector2(500,550);ws.FindProperty("maximumSize").vector2Value=new Vector2(640,900);ws.ApplyModifiedPropertiesWithoutUndo();
            ((RectTransform)root.transform).sizeDelta=new Vector2(500,650);
            root.GetComponent<Image>().enabled=false;
            var surface=Art(root.transform,"WindowSurface","surface_solid");Stretch(surface.rectTransform,5,5,5,5);surface.raycastTarget=true;surface.transform.SetAsFirstSibling();Wire(view,"background",surface);
            Paint(root.transform.Find("Border").GetComponent<Image>(),"tooltip_frame",true);
            var header=root.transform.Find("Header");Paint(header.GetComponent<Image>(),"button_normal");
            var title=Ref<TextMeshProUGUI>(view,"titleText");Text(title,26,TextAlignmentOptions.MidlineLeft);title.gameObject.SetActive(true);Stretch(title.rectTransform,18,0,56,0);
            var close=Ref<Button>(view,"closeButton");Paint(close.GetComponent<Image>(),"button_normal");close.transition=Selectable.Transition.None;close.spriteState=default;Text(close.GetComponentInChildren<TMP_Text>(true),22,TextAlignmentOptions.Center);
            Stretch((RectTransform)root.transform.Find("Content"),16,16,16,60);
            var scroll=Ref<ScrollRect>(view,"contentScroll");scroll.GetComponent<Image>().color=Color.clear;scroll.transform.Find("Border").GetComponent<Image>().enabled=false;scroll.inertia=false;scroll.scrollSensitivity=24;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            Stretch(scroll.viewport,0,0,18,0);scroll.viewport.GetComponent<Image>().color=Color.clear;
            var scrollbar=scroll.verticalScrollbar;Paint(scrollbar.GetComponent<Image>(),"scrollbar_track");Paint(scrollbar.handleRect.GetComponent<Image>(),"scrollbar_thumb_normal");scrollbar.transition=Selectable.Transition.None;
            var content=Ref<RectTransform>(view,"contentRoot");var group=content.GetComponent<VerticalLayoutGroup>();group.padding=new RectOffset(4,4,8,8);group.spacing=12;group.childControlWidth=true;group.childControlHeight=true;group.childForceExpandWidth=true;group.childForceExpandHeight=false;
            var identity=Ref<TextMeshProUGUI>(view,"identityText");Text(identity,18);Layout(identity,24);
            var portrait=Ref<Image>(view,"portrait");portrait.preserveAspect=true;var portraitLayout=Layout(portrait,112);portraitLayout.minWidth=88;portraitLayout.preferredWidth=88;portraitLayout.flexibleWidth=0;
            var overview=portrait.transform.parent;var overviewGroup=overview.GetComponent<HorizontalLayoutGroup>();overviewGroup.spacing=16;overviewGroup.childControlWidth=true;overviewGroup.childControlHeight=true;overviewGroup.childForceExpandWidth=false;overviewGroup.childForceExpandHeight=false;overviewGroup.childAlignment=TextAnchor.UpperLeft;Wire(view,"overviewLayout",Layout(overview,112));
            var bars=Ref<CombatVitalsView>(view,"vitals");Layout(bars,140).flexibleWidth=1;
            var barsGroup=bars.transform.parent.GetComponent<VerticalLayoutGroup>();barsGroup.childControlWidth=true;barsGroup.childControlHeight=true;barsGroup.childForceExpandWidth=true;barsGroup.childForceExpandHeight=false;bars.transform.parent.GetComponent<LayoutElement>().flexibleWidth=1;
            var health=Ref<Slider>(view,"healthSlider");Bar(health,Ref<TextMeshProUGUI>(view,"healthValueText"),28,new Color(.7f,.12f,.1f));
            var resource=Ref<Slider>(view,"resourceSlider");Bar(resource,Ref<TextMeshProUGUI>(view,"resourceValueText"),76,Color.white);
            var healthLabel=Label(bars.transform,"HealthLabel","Zdrowie",16);Top(healthLabel.rectTransform,0,22);
            var resourceLabel=Label(bars.transform,"ResourceLabel","Zasób",16);Top(resourceLabel.rectTransform,48,22);Wire(view,"resourceLabelText",resourceLabel);
            var action=Ref<TextMeshProUGUI>(bars,"actionValueText");Text(action,16);Top(action.rectTransform,96,22);Wire(view,"actionValueText",action);
            var pips=(RectTransform)Ref<Transform>(view,"actionPointPipRoot");Top(pips,122,14);
            var left=Ref<TextMeshProUGUI>(view,"statsLeftText");var right=Ref<TextMeshProUGUI>(view,"statsRightText");var stats=left.transform.parent;
            stats.GetComponent<HorizontalLayoutGroup>().enabled=false;stats.GetComponent<ContentSizeFitter>().enabled=false;Wire(view,"statsLayout",Layout(stats,202));
            TableText(left,0,.43f);TableText(right,.62f,.9f);
            var leftValues=Label(stats,"CombatValues","",16);TableText(leftValues,.43f,.58f);leftValues.alignment=TextAlignmentOptions.TopRight;Wire(view,"statsLeftValues",leftValues);
            var rightValues=Label(stats,"ResistanceValues","",16);TableText(rightValues,.9f,1);rightValues.alignment=TextAlignmentOptions.TopRight;Wire(view,"statsRightValues",rightValues);
            var combatHeader=Label(stats,"CombatHeader","Walka",20);Top(combatHeader.rectTransform,0,26);
            var resistanceHeader=Label(stats,"ResistanceHeader","Odporności",20);Top(resistanceHeader.rectTransform,0,26);resistanceHeader.rectTransform.anchorMin=new Vector2(.62f,1);resistanceHeader.rectTransform.anchorMax=Vector2.one;
            foreach(var name in new[]{"statusesHeaderText","skillsHeaderText"}){var t=Ref<TextMeshProUGUI>(view,name);Text(t,20);Layout(t,26);}
            foreach(var name in new[]{"emptyStatuses","emptySkills"}){var t=Ref<GameObject>(view,name).GetComponent<TMP_Text>();Text(t,16);t.color=new Color(.64f,.62f,.58f);Layout(t,22);}
            foreach(var list in new[]{Ref<RectTransform>(view,"statusRoot"),Ref<RectTransform>(view,"skillRoot")}){var layout=list.GetComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;}
            var skill=Ref<CombatSkillInspectEntryView>(view,"skillTemplate");Layout(skill,58);skill.GetComponent<Image>().color=Color.clear;skill.GetComponent<Image>().raycastTarget=true;skill.transform.Find("Border").GetComponent<Image>().enabled=false;
            var frame=Art(skill.transform,"SkillFrame","item_frame",true);var fr=frame.rectTransform;fr.anchorMin=fr.anchorMax=new Vector2(0,.5f);fr.pivot=new Vector2(0,.5f);fr.anchoredPosition=Vector2.zero;fr.sizeDelta=new Vector2(50,50);
            var icon=Ref<Image>(skill,"icon");GodmistSkillTreeSetup.ConfigureIconMask(frame.transform,ref icon);Wire(skill,"icon",icon);
            var label=Ref<TextMeshProUGUI>(skill,"label");Text(label,18,TextAlignmentOptions.MidlineLeft);Stretch((RectTransform)label.transform.parent,64,0,0,0);Stretch(label.rectTransform);
            label.transform.parent.Find("Detail").gameObject.SetActive(false);
            var status=Ref<CombatStatusInspectEntryView>(view,"statusTemplate");Layout(status,48);status.GetComponent<Image>().color=Color.clear;status.transform.Find("Border").GetComponent<Image>().enabled=false;
            var statusLabel=Ref<TextMeshProUGUI>(status,"label");Text(statusLabel,18,TextAlignmentOptions.MidlineLeft);Stretch((RectTransform)statusLabel.transform.parent,56,0,0,0);Stretch(statusLabel.rectTransform);statusLabel.transform.parent.Find("Detail").gameObject.SetActive(false);
            DetailStyling(view);
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    static void DetailStyling(CombatUnitInspectWindowView view)
    {
        var status=Ref<CombatStatusInspectEntryView>(view,"statusTemplate");
        Wire(status,"labelRoot",Ref<TextMeshProUGUI>(status,"label").transform.parent);
        var bars=Ref<CombatVitalsView>(view,"vitals");
        var colors=new SerializedObject(bars);
        colors.FindProperty("availableColor").colorValue=new Color(.9f,.94f,1f,1);
        colors.FindProperty("spentColor").colorValue=new Color(.23f,.27f,.32f,1);
        colors.ApplyModifiedPropertiesWithoutUndo();
        var pips=Ref<Transform>(view,"actionPointPipRoot");
        Top((RectTransform)pips,122,16);
        var group=pips.GetComponent<HorizontalLayoutGroup>();
        group.childControlWidth=true;group.childControlHeight=true;
        group.childForceExpandWidth=false;group.childForceExpandHeight=false;
        group.childAlignment=TextAnchor.MiddleLeft;group.spacing=3;
        var image=Ref<Image>(bars,"actionPointPipTemplate");
        image.sprite=null;image.overrideSprite=null;image.color=Color.white;
        image.type=Image.Type.Simple;image.raycastTarget=false;
        var layout=Layout(image,16);layout.minWidth=0;layout.preferredWidth=16;layout.flexibleWidth=0;
        var fill=new SerializedObject(image.GetComponent<ActionPointPipFill>());
        fill.FindProperty("facetedGem").boolValue=true;fill.ApplyModifiedPropertiesWithoutUndo();
        image.transform.Find("GothicPipFrame").GetComponent<Image>().enabled=false;
    }

    [MenuItem("Godmist/UI/Restyle inspection AP and effects")]
    public static void ConfigureDetails()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{DetailStyling(root.GetComponent<CombatUnitInspectWindowView>());PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
}
