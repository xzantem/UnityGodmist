using System;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Native authoring of the development workspace variant.</summary>
public static class GodmistSkillTreeSetup
{
    const string Path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillTreeWindow Variant.prefab";
    static UIWorkspaceStylePalette palette;
    static void Wire(UnityEngine.Object owner, string field, UnityEngine.Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static RectTransform Node(Transform parent, string name)
    {
        var node = parent.Find(name);
        if (node == null) { node = new GameObject(name, typeof(RectTransform)).transform; node.SetParent(parent, false); }
        node.gameObject.layer = parent.gameObject.layer;
        return (RectTransform)node;
    }
    static void Stretch(RectTransform r, float l=0, float b=0, float right=0, float top=0)
    { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=new Vector2(l,b); r.offsetMax=new Vector2(-right,-top); }
    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot=null)
    { r.anchorMin=r.anchorMax=anchor; r.pivot=pivot??new Vector2(.5f,.5f); r.sizeDelta=size; r.anchoredPosition=pos; }
    static void Top(RectTransform r, float y, float height, float right=0)
    { r.anchorMin=new Vector2(0,1); r.anchorMax=Vector2.one; r.pivot=new Vector2(.5f,1); r.sizeDelta=new Vector2(-right,height); r.anchoredPosition=new Vector2(-right*.5f,-y); }
    static void RemoveLayout(Transform t)
    {
        foreach(var c in t.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in t.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in t.GetComponents<LayoutElement>()) UnityEngine.Object.DestroyImmediate(c);
    }
    static void Paint(Image image, string art, bool hollow=false)
    { image.sprite=palette.GetArtwork(art)??throw new Exception("Missing art "+art); image.overrideSprite=null; image.color=Color.white; image.type=image.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple; image.fillCenter=!hollow; }
    static Image Art(Transform parent, string name, string art, bool hollow=false)
    { var r=Node(parent,name); var image=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>(); Paint(image,art,hollow); image.raycastTarget=false; return image; }
    static TMP_Text Label(Transform parent, string name, string text, float size=20, TextAlignmentOptions alignment=TextAlignmentOptions.TopLeft)
    {
        var r=Node(parent,name); var label=r.GetComponent<TextMeshProUGUI>()??r.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=palette.Font; label.fontSharedMaterial=palette.Font.material; label.fontSize=size;
        label.color=new Color(.91f,.875f,.78f); label.enableAutoSizing=false; label.textWrappingMode=TextWrappingModes.Normal;
        label.overflowMode=TextOverflowModes.Overflow; label.alignment=alignment; label.raycastTarget=false; label.margin=Vector4.zero; label.fontStyle=FontStyles.Normal; label.text=text;
        return label;
    }
    static void ButtonStyle(Button button, string caption)
    {
        Paint(button.GetComponent<Image>(),"button_normal"); button.targetGraphic=button.GetComponent<Image>();
        button.transition=Selectable.Transition.ColorTint; button.spriteState=default;
        button.colors=new ColorBlock{normalColor=Color.white,highlightedColor=new Color(1.12f,1.12f,1.12f),selectedColor=Color.white,pressedColor=new Color(.75f,.75f,.75f),disabledColor=new Color(.5f,.5f,.5f),colorMultiplier=1,fadeDuration=0};
        var label=Label(button.transform,"Label",caption,20,TextAlignmentOptions.Center); Stretch(label.rectTransform,8,2,8,2);
    }
    static ScrollRect Scroll(Transform parent, string name)
    {
        var r=Node(parent,name); var scroll=r.GetComponent<ScrollRect>()??r.gameObject.AddComponent<ScrollRect>();
        var vp=Node(r,"Viewport"); Stretch(vp,0,0,14,0);
        var im=vp.GetComponent<Image>()??vp.gameObject.AddComponent<Image>(); im.sprite=null; im.color=Color.clear; im.raycastTarget=true;
        if(vp.GetComponent<RectMask2D>()==null)vp.gameObject.AddComponent<RectMask2D>();
        scroll.viewport=vp; scroll.horizontal=false; scroll.vertical=true; scroll.inertia=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=28;
        var track=Art(r,"Scrollbar","scrollbar_track"); track.raycastTarget=true;
        var tr=track.rectTransform; tr.anchorMin=new Vector2(1,0);tr.anchorMax=Vector2.one;tr.pivot=new Vector2(1,.5f);tr.sizeDelta=new Vector2(8,0);tr.anchoredPosition=Vector2.zero;
        var bar=tr.GetComponent<Scrollbar>()??tr.gameObject.AddComponent<Scrollbar>();
        var area=Node(tr,"SlidingArea");Stretch(area,2,2,2,2);var handle=Art(area,"Handle","scrollbar_thumb_normal");Stretch(handle.rectTransform);handle.raycastTarget=true;
        bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    static void ConfigureTierGates(GameObject root)
    {
        var view=root.GetComponent<SkillTreeProgressionPanelView>();
        var treeArea=((ScrollRect)new SerializedObject(view).FindProperty("treeScroll").objectReferenceValue).content;
        var so=new SerializedObject(view);var fills=so.FindProperty("tierGateFills");fills.arraySize=3;
        var names=new[]{"Gate10Outline","Gate20Outline","Gate30Outline"};
        for(var i=0;i<names.Length;i++)
        {
            var gate=treeArea.Find(names[i]);gate.GetComponent<Image>().enabled=false;
            var le=gate.GetComponent<LayoutElement>()??gate.gameObject.AddComponent<LayoutElement>();le.ignoreLayout=true;
            var rule=Art(gate,"Rule","separator_horizontal");rule.sprite=null;rule.color=new Color(.6f,.5f,.3f);
            rule.rectTransform.anchorMin=new Vector2(0,.5f);rule.rectTransform.anchorMax=new Vector2(1,.5f);rule.rectTransform.sizeDelta=new Vector2(0,2);rule.rectTransform.anchoredPosition=Vector2.zero;rule.transform.SetAsFirstSibling();
            var back=(RectTransform)gate.Find("GateLabelBackground");Place(back,new Vector2(.5f,.5f),Vector2.zero,new Vector2(280,24));Paint(back.GetComponent<Image>(),"surface_solid");
            var track=Art(back,"ProgressTrack","slider_track");Place(track.rectTransform,new Vector2(0,.5f),new Vector2(6,0),new Vector2(202,12),new Vector2(0,.5f));
            var fill=Art(track.transform,"Fill","fill_runtime");Stretch(fill.rectTransform,5,3,5,3);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;fill.color=new Color(.88f,.68f,.28f);
            fills.GetArrayElementAtIndex(i).objectReferenceValue=fill;
            var label=Label(back,"GateLabel",$"0/{(i+1)*10}",18,TextAlignmentOptions.Center);Place(label.rectTransform,new Vector2(1,.5f),new Vector2(-6,0),new Vector2(60,24),new Vector2(1,.5f));label.color=new Color(1f,.82f,.45f);label.transform.SetAsLastSibling();
            foreach(var graphic in gate.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("Godmist/UI/Restyle development tier gates")]
    public static void ConfigureGateProgress()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{ConfigureTierGates(root);PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Godmist/UI/Restyle development workspace")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)){style.enabled=false;Wire(style,"palette",null);}
            var view=root.GetComponent<SkillTreeProgressionPanelView>(); var window=root.GetComponent<UIWorkspaceWindow>();
            var so=new SerializedObject(window);so.FindProperty("minimumSize").vector2Value=new Vector2(1060,700);so.FindProperty("maximumSize").vector2Value=new Vector2(1500,1000);so.ApplyModifiedPropertiesWithoutUndo();
            Place((RectTransform)root.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1200,820));
            root.GetComponent<Image>().enabled=false;
            var surface=Art(root.transform,"WindowSurface","surface_solid");surface.raycastTarget=true;Stretch(surface.rectTransform,5,5,5,5);surface.transform.SetAsFirstSibling();
            Paint(root.transform.Find("Border").GetComponent<Image>(),"tooltip_frame",true);
            var header=(RectTransform)root.transform.Find("Header");Top(header,0,48);Paint(header.GetComponent<Image>(),"button_normal");
            var title=Label(header,"Title","Rozwój",27,TextAlignmentOptions.MidlineLeft);Stretch(title.rectTransform,20,0,62,0);title.enabled=true;
            var close=header.Find("CloseButton").GetComponent<Button>();ButtonStyle(close,"×");Place((RectTransform)close.transform,new Vector2(1,.5f),new Vector2(-10,0),new Vector2(36,36),new Vector2(1,.5f));
            Wire(view,"windowTitleText",title); Wire(view,"workspaceWindow",window);
            var content=(RectTransform)root.transform.Find("Content");RemoveLayout(content);Stretch(content,18,18,18,64);
            var progression=Label(content,"ProgressionText","Poziom 1 • XP: 0",18);RemoveLayout(progression.transform);Top(progression.rectTransform,0,26,394);
            var xpTrack=Art(content,"ExperienceBar","slider_track");Place(xpTrack.rectTransform,new Vector2(0,1),new Vector2(0,-28),new Vector2(320,10),new Vector2(0,1));
            var xpFill=Art(xpTrack.transform,"Fill","fill_runtime");Stretch(xpFill.rectTransform,6,3,6,3);xpFill.type=Image.Type.Filled;xpFill.fillMethod=Image.FillMethod.Horizontal;xpFill.fillOrigin=0;xpFill.fillAmount=0;xpFill.color=new Color(.45f,.85f,.95f);Wire(view,"experienceFill",xpFill);
            var points=Label(content,"AvailablePoints","Punkty rozwoju: 0",23,TextAlignmentOptions.TopRight);Top(points.rectTransform,0,30);points.color=new Color(1f,.8f,.35f);Wire(view,"availablePointsText",points);
            var tabs=(RectTransform)content.Find("TreeTabs");Top(tabs,42,44,394);var hl=tabs.GetComponent<HorizontalLayoutGroup>();hl.childAlignment=TextAnchor.UpperLeft;hl.childControlHeight=hl.childControlWidth=true;hl.childForceExpandHeight=false;hl.childForceExpandWidth=true;hl.spacing=12;
            var tab=tabs.Find("TreeTabTemplate").GetComponent<Button>();ButtonStyle(tab,"Drzewko");var tabLayout=tab.GetComponent<LayoutElement>();tabLayout.minWidth=180;tabLayout.preferredWidth=240;tabLayout.flexibleWidth=1;tabLayout.minHeight=tabLayout.preferredHeight=44;tabLayout.flexibleHeight=0;
            var oldBorder=tab.transform.Find("Border").GetComponent<Image>();oldBorder.enabled=false;
            var tabSelection=Art(tab.transform,"SelectionBorder","button_hover",true);Stretch(tabSelection.rectTransform);tabSelection.enabled=false;
            // Decorative state frames must stay behind the label, including hover/selection.
            tab.transform.Find("Label").SetAsLastSibling();
            var row=(RectTransform)content.Find("TreeHeaderRow");RemoveLayout(row);Top(row,102,80,394);
            row.Find("SelectedTreeHeader").gameObject.SetActive(false);
            var passive=(RectTransform)row.Find("GlobalPassivePanel");RemoveLayout(passive);Stretch(passive);passive.GetComponent<Image>().enabled=false;
            var passiveText=Label(passive,"GlobalPassiveText","Pasyw drzewka",18);Stretch(passiveText.rectTransform,2,0,2,0);passiveText.lineSpacing=3;
            var separator=Art(content,"HeaderSeparator","separator_horizontal");Top(separator.rectTransform,186,2,394);Wire(view,"headerSeparator",separator.rectTransform);
            var treeArea=(RectTransform)(content.Find("TreeArea")??content.Find("TreeScroll/Viewport/TreeArea"));
            RemoveLayout(treeArea);treeArea.GetComponent<Image>().enabled=false;
            var scroll=Scroll(content,"TreeScroll");Stretch((RectTransform)scroll.transform,0,0,394,200);Wire(view,"treeScroll",scroll);
            treeArea.SetParent(scroll.viewport,false);treeArea.anchorMin=new Vector2(0,1);treeArea.anchorMax=Vector2.one;treeArea.pivot=new Vector2(.5f,1);treeArea.sizeDelta=Vector2.zero;treeArea.anchoredPosition=Vector2.zero;scroll.content=treeArea;
            var areaLayout=treeArea.gameObject.AddComponent<VerticalLayoutGroup>();areaLayout.padding=new RectOffset(8,8,0,12);areaLayout.childControlHeight=areaLayout.childControlWidth=true;areaLayout.childForceExpandHeight=false;areaLayout.childForceExpandWidth=true;
            var areaFit=treeArea.gameObject.AddComponent<ContentSizeFitter>();areaFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var rows=(RectTransform)treeArea.Find("TreeRows");var vl=rows.GetComponent<VerticalLayoutGroup>();vl.padding=new RectOffset(0,0,8,0);vl.spacing=42;vl.childControlWidth=vl.childControlHeight=true;vl.childForceExpandWidth=true;vl.childForceExpandHeight=false;vl.childAlignment=TextAnchor.UpperCenter;
            for(var i=1;i<=7;i++)
            {
                var tier=rows.Find("Tier"+i);var h=tier.GetComponent<HorizontalLayoutGroup>();h.childAlignment=TextAnchor.UpperCenter;h.spacing=14;h.childControlWidth=h.childControlHeight=true;h.childForceExpandWidth=h.childForceExpandHeight=false;
                var le=tier.GetComponent<LayoutElement>();le.minHeight=le.preferredHeight=96;le.flexibleHeight=0;
            }
            var template=(Button)new SerializedObject(view).FindProperty("nodeTemplate").objectReferenceValue;
            var card=(RectTransform)template.transform;Paint(template.GetComponent<Image>(),"item_frame");template.transition=Selectable.Transition.None;
            var leCard=card.GetComponent<LayoutElement>()??card.gameObject.AddComponent<LayoutElement>();leCard.minWidth=leCard.preferredWidth=96;leCard.minHeight=leCard.preferredHeight=96;leCard.flexibleHeight=leCard.flexibleWidth=0;
            var nodeView=card.GetComponent<SkillTreeNodeView>();
            var nodeSo=new SerializedObject(nodeView);
            var icon=(Image)nodeSo.FindProperty("icon").objectReferenceValue;Place(icon.rectTransform,new Vector2(.5f,.5f),new Vector2(0,5),new Vector2(72,72));icon.preserveAspect=true;icon.raycastTarget=false;
            var rank=(TMP_Text)nodeSo.FindProperty("rankText").objectReferenceValue;
            var rankBack=Art(card,"RankBackground","surface_solid");Place(rankBack.rectTransform,new Vector2(1,0),new Vector2(-5,5),new Vector2(42,20),new Vector2(1,0));
            rank.transform.SetAsLastSibling();Label(card,rank.name,"0/2",17,TextAlignmentOptions.Center);Place(rank.rectTransform,new Vector2(1,0),new Vector2(-5,5),new Vector2(42,20),new Vector2(1,0));
            var name=(TMP_Text)nodeSo.FindProperty("nameText").objectReferenceValue;if(name!=null)name.gameObject.SetActive(false);
            var lockText=(TMP_Text)nodeSo.FindProperty("lockText").objectReferenceValue;if(lockText!=null)lockText.enabled=false;
            var overlay=(Image)nodeSo.FindProperty("disabledOverlay").objectReferenceValue;overlay.sprite=null;Stretch(overlay.rectTransform,6,6,6,6);overlay.transform.SetSiblingIndex(icon.transform.GetSiblingIndex()+1);
            var border=card.Find("Border").GetComponent<Image>();border.enabled=false;
            var accent=Art(card,"RankAccent","slot_hover",true);Stretch(accent.rectTransform);accent.enabled=true;Wire(nodeView,"rankAccent",accent);
            var selected=Art(card,"SelectionBorder","slot_selected",true);Stretch(selected.rectTransform,-4,-4,-4,-4);selected.sprite=null;selected.color=Color.clear;selected.type=Image.Type.Simple;selected.enabled=true;Wire(nodeView,"selectionBorder",selected);
            foreach(var corner in new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,0),new Vector2(1,1)})
            {
                var cornerName=$"Corner{corner.x}{corner.y}";
                var horizontal=Art(selected.transform,cornerName+"H","surface_solid");horizontal.sprite=null;horizontal.color=new Color(.85f,.95f,1f);Place(horizontal.rectTransform,corner,Vector2.zero,new Vector2(12,2),corner);
                var vertical=Art(selected.transform,cornerName+"V","surface_solid");vertical.sprite=null;vertical.color=horizontal.color;Place(vertical.rectTransform,corner,Vector2.zero,new Vector2(2,12),corner);
            }
            selected.gameObject.SetActive(false);
            var hover=Art(card,"HoverBorder","slot_hover",true);Stretch(hover.rectTransform);hover.color=new Color(.7f,.9f,1f);hover.enabled=false;Wire(nodeView,"hoverBorder",hover);
            var holdViewport=Node(card,"HoldFillViewport");holdViewport.anchorMin=Vector2.zero;holdViewport.anchorMax=new Vector2(1,0);holdViewport.pivot=new Vector2(.5f,0);holdViewport.sizeDelta=Vector2.zero;holdViewport.anchoredPosition=Vector2.zero;
            if(holdViewport.GetComponent<RectMask2D>()==null)holdViewport.gameObject.AddComponent<RectMask2D>();
            var holdBorder=Art(holdViewport,"FillBorder","slot_hover",true);holdBorder.rectTransform.anchorMin=Vector2.zero;holdBorder.rectTransform.anchorMax=new Vector2(1,0);holdBorder.rectTransform.pivot=new Vector2(.5f,0);holdBorder.rectTransform.sizeDelta=new Vector2(0,96);holdBorder.rectTransform.anchoredPosition=Vector2.zero;holdBorder.color=new Color(.35f,1f,1f);holdBorder.enabled=false;
            Wire(nodeView,"holdFillViewport",holdViewport);Wire(nodeView,"holdFillBorder",holdBorder);
            ConfigureNodeShapes(root);
            ConfigureRankMarkers(root);
            ConfigureNodeIcons(root);
            var connections=(RectTransform)treeArea.Find("Connections");Stretch(connections);var connectionLayout=connections.GetComponent<LayoutElement>()??connections.gameObject.AddComponent<LayoutElement>();connectionLayout.ignoreLayout=true;
            var line=connections.Find("ConnectionTemplate").GetComponent<Image>();line.sprite=null;line.color=new Color(.44f,.48f,.43f);line.rectTransform.sizeDelta=new Vector2(100,2);line.raycastTarget=false;
            ConfigureTierGates(root);
            var details=Node(content,"NodeDetails");details.anchorMin=new Vector2(1,0);details.anchorMax=Vector2.one;details.pivot=new Vector2(1,.5f);details.sizeDelta=new Vector2(370,-42);details.anchoredPosition=new Vector2(0,-21);
            var divider=Art(content,"DetailsDivider","separator_vertical");divider.rectTransform.anchorMin=new Vector2(1,0);divider.rectTransform.anchorMax=Vector2.one;divider.rectTransform.pivot=new Vector2(.5f,.5f);divider.rectTransform.sizeDelta=new Vector2(2,-42);divider.rectTransform.anchoredPosition=new Vector2(-382,-21);
            var di=Art(details,"Icon","item_frame");di.sprite=null;di.preserveAspect=true;Place(di.rectTransform,new Vector2(0,1),Vector2.zero,new Vector2(64,64),new Vector2(0,1));
            var dt=Label(details,"Title","Wybierz węzeł",25);Top(dt.rectTransform,0,58);dt.rectTransform.offsetMin=new Vector2(80,dt.rectTransform.offsetMin.y);
            var dr=Label(details,"Rank","",18);Top(dr.rectTransform,66,26);
            var ds=Scroll(details,"BodyScroll");Stretch((RectTransform)ds.transform,0,126,0,108);
            var body=Label(ds.viewport,"Body","",21);body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=Vector2.one;body.rectTransform.pivot=new Vector2(.5f,1);body.rectTransform.sizeDelta=Vector2.zero;body.rectTransform.anchoredPosition=Vector2.zero;body.lineSpacing=4;body.paragraphSpacing=10;
            var fitter=body.GetComponent<ContentSizeFitter>()??body.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;ds.content=body.rectTransform;
            var block=Label(details,"BlockReason","",18);block.rectTransform.anchorMin=new Vector2(0,0);block.rectTransform.anchorMax=new Vector2(1,0);block.rectTransform.pivot=new Vector2(.5f,0);block.rectTransform.sizeDelta=new Vector2(0,72);block.rectTransform.anchoredPosition=new Vector2(0,54);block.color=new Color(.8f,.77f,.67f);
            var bi=Art(details,"Invest","button_normal");bi.raycastTarget=true;var invest=bi.GetComponent<Button>()??bi.gameObject.AddComponent<Button>();ButtonStyle(invest,"Rozwiń — 1 pkt");Place(bi.rectTransform,new Vector2(1,0),Vector2.zero,new Vector2(250,42),new Vector2(1,0));
            Wire(view,"detailsIcon",di);Wire(view,"detailsTitle",dt);Wire(view,"detailsRank",dr);Wire(view,"detailsBody",body);Wire(view,"detailsScroll",ds);Wire(view,"investmentBlockText",block);Wire(view,"investButton",invest);
            ResourcePanelIconSetup.ConfigureTree(root);
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    static SkillTreeActiveNodeFrame Shape(Transform parent, string name, bool surface, bool corners, float width, Color tint)
    {
        var rect=Node(parent,name);
        if(rect.GetComponent<CanvasRenderer>()==null)rect.gameObject.AddComponent<CanvasRenderer>();
        var shape=rect.GetComponent<SkillTreeActiveNodeFrame>()??rect.gameObject.AddComponent<SkillTreeActiveNodeFrame>();
        shape.Configure(surface,corners,width);shape.color=tint;Stretch(rect);shape.enabled=false;
        return shape;
    }
    static void ConfigureNodeShapes(GameObject root)
    {
        var view=root.GetComponent<SkillTreeProgressionPanelView>();
        var template=(Button)new SerializedObject(view).FindProperty("nodeTemplate").objectReferenceValue;
        var card=template.transform;var node=card.GetComponent<SkillTreeNodeView>();
        Wire(node,"nodeSurface",template.GetComponent<Image>());Wire(node,"rankBackground",card.Find("RankBackground"));
        var surface=Shape(card,"ActiveSurface",true,false,4,new Color(.28f,.32f,.29f));surface.transform.SetAsFirstSibling();Wire(node,"activeSurface",surface);
        Wire(node,"activeRankAccent",Shape(card,"ActiveRankAccent",false,false,2,new Color(.7f,.78f,.8f)));
        Wire(node,"activeHoverBorder",Shape(card,"ActiveHoverBorder",false,false,3,new Color(.7f,.78f,.8f)));
        var selection=Shape(card,"ActiveSelectionBorder",false,true,2,new Color(.85f,.95f,1f));Stretch(selection.rectTransform,-4,-4,-4,-4);Wire(node,"activeSelectionBorder",selection);
        var hold=Shape(card.Find("HoldFillViewport"),"ActiveFillBorder",false,false,2,new Color(.35f,1f,1f));
        hold.rectTransform.anchorMin=Vector2.zero;hold.rectTransform.anchorMax=new Vector2(1,0);hold.rectTransform.pivot=new Vector2(.5f,0);hold.rectTransform.sizeDelta=new Vector2(0,96);hold.rectTransform.anchoredPosition=Vector2.zero;Wire(node,"activeHoldBorder",hold);
        card.Find("RankBackground").SetAsLastSibling();
        ((TMP_Text)new SerializedObject(node).FindProperty("rankText").objectReferenceValue).transform.SetAsLastSibling();
        hold.transform.parent.SetAsLastSibling();
        card.Find("RankMarkers")?.SetAsLastSibling();
    }
    [MenuItem("Godmist/UI/Restyle development node shapes")]
    public static void ConfigureActiveNodeShapes()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{ConfigureNodeShapes(root);PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    static void ConfigureRankMarkers(GameObject root)
    {
        var view=root.GetComponent<SkillTreeProgressionPanelView>();
        var template=(Button)new SerializedObject(view).FindProperty("nodeTemplate").objectReferenceValue;
        var card=template.transform;var node=card.GetComponent<SkillTreeNodeView>();
        var rect=Node(card,"RankMarkers");
        Place(rect,new Vector2(.5f,0),Vector2.zero,new Vector2(80,16),new Vector2(.5f,.5f));
        if(rect.GetComponent<CanvasRenderer>()==null)rect.gameObject.AddComponent<CanvasRenderer>();
        var markers=rect.GetComponent<SkillTreeRankMarkers>()??rect.gameObject.AddComponent<SkillTreeRankMarkers>();
        markers.SetRanks(0,2,new Color(.25f,1f,.4f),Color.white);
        Wire(node,"rankMarkers",markers);
        ((TMP_Text)new SerializedObject(node).FindProperty("rankText").objectReferenceValue).enabled=false;
        card.Find("RankBackground").gameObject.SetActive(false);
        card.Find("HoldFillViewport").SetAsLastSibling();rect.SetAsLastSibling();
    }
    [MenuItem("Godmist/UI/Restyle development rank markers")]
    public static void ConfigureNodeRankMarkers()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{ConfigureRankMarkers(root);PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }

    public static SkillIconShapeMask ConfigureIconMask(Transform card, ref Image icon)
    {
        Image overlay=null;
        return ConfigureIconMask(card,ref icon,ref overlay);
    }
    public static SkillIconShapeMask ConfigureIconMask(Transform card, ref Image icon, ref Image overlay)
    {
        var rect=Node(card,"IconMask");Stretch(rect,4,4,4,4);
        if(rect.GetComponent<CanvasRenderer>()==null)rect.gameObject.AddComponent<CanvasRenderer>();
        var shape=rect.GetComponent<SkillIconShapeMask>()??rect.gameObject.AddComponent<SkillIconShapeMask>();shape.SetCornerCut(2);
        var mask=rect.GetComponent<Mask>()??rect.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
        icon=MaskedImage(rect,icon,"Icon");
        if(overlay!=null)overlay=MaskedImage(rect,overlay,"Overlay");
        rect.SetAsFirstSibling();
        return shape;
    }
    static Image MaskedImage(RectTransform mask, Image source, string name)
    {
        var image=source;
        if(source.transform.parent!=mask)
        {
            // An inherited prefab child cannot retain a reparent override. Keep it disabled
            // and author a local masked image with an explicit view reference instead.
            var rect=Node(mask,name);
            image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
            image.sprite=source.sprite;image.overrideSprite=source.overrideSprite;image.color=source.color;
            image.material=source.material;image.type=source.type;image.fillCenter=source.fillCenter;
            image.fillMethod=source.fillMethod;image.fillOrigin=source.fillOrigin;
            image.fillClockwise=source.fillClockwise;image.fillAmount=source.fillAmount;
            image.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
            image.enabled=source.enabled;source.enabled=false;
        }
        Stretch(image.rectTransform);image.preserveAspect=false;image.raycastTarget=false;image.maskable=true;
        return image;
    }
    static void ConfigureNodeIcons(GameObject root)
    {
        var view=root.GetComponent<SkillTreeProgressionPanelView>();
        var template=(Button)new SerializedObject(view).FindProperty("nodeTemplate").objectReferenceValue;
        var node=template.GetComponent<SkillTreeNodeView>();var so=new SerializedObject(node);
        var icon=(Image)so.FindProperty("icon").objectReferenceValue;
        var overlay=(Image)so.FindProperty("disabledOverlay").objectReferenceValue;
        var shape=ConfigureIconMask(template.transform,ref icon,ref overlay);
        shape.transform.SetSiblingIndex(1);Wire(node,"iconMask",shape);Wire(node,"icon",icon);Wire(node,"disabledOverlay",overlay);
        ConfigureRankMarkers(root);
    }
    [MenuItem("Godmist/UI/Restyle development icon masks")]
    public static void ConfigureNodeIconMasks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{ConfigureNodeIcons(root);PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
}
