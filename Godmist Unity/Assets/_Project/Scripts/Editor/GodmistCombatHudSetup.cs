using System;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit native authoring for the existing combat HUD and its templates.</summary>
public static class GodmistCombatHudSetup
{
    [MenuItem("Godmist/Content/UI/Configure Combat Consumable Popover")]
    public static void ConfigureConsumablePopover()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Configure the consumable popover outside Play Mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ConsumableSelectionWindow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var slot in root.GetComponentsInChildren<CombatSkillSlotView>(true))
            {
                var count = slot.transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
                if (count == null) throw new InvalidOperationException("Consumable item template is missing its authored CountLabel.");
                Wire(slot, "hotkeyText", count);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var scenePath in new[] { "City/Arungard", "Sandbox/DungeonSandbox", "Sandbox/CombatSandbox" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + scenePath + ".unity", OpenSceneMode.Single);
                var targets = new System.Collections.Generic.List<Godmist.Views.Combat.CombatTargetSelectionController>();
                var huds = new System.Collections.Generic.List<CombatHudView>();
                foreach (var sceneRoot in scene.GetRootGameObjects())
                {
                    targets.AddRange(sceneRoot.GetComponentsInChildren<Godmist.Views.Combat.CombatTargetSelectionController>(true));
                    huds.AddRange(sceneRoot.GetComponentsInChildren<CombatHudView>(true));
                }
                if (huds.Count == 0) continue;
                if (targets.Count != 1) throw new InvalidOperationException("Expected one explicitly owned combat target controller in " + scenePath);
                foreach (var hud in huds) Wire(hud, "targeting", targets[0]);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (Array.Exists(setup, state => state.isLoaded && state.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Consumable popover references configured: item counts and existing HUD target controllers.");
    }

    public static void ConfigureVitalsText()
    {
        foreach(var name in new[]{"PF_UIW_CombatVitals","PF_UIW_CombatActionPanel","PF_UIW_CombatHud"})
        {
            var path="Assets/_Project/Prefabs/UI/Workspace/"+name+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var vitals in root.GetComponentsInChildren<CombatVitalsView>(true))
                    foreach(var field in new[]{"healthValueText","resourceValueText","actionValueText"})
                    {
                        var text=Ref<TextMeshProUGUI>(vitals,field);
                        text.fontSize=18;
                        var size=text.rectTransform.sizeDelta;size.y=Mathf.Max(size.y,22);text.rectTransform.sizeDelta=size;
                    }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
    }
    const string UI="Assets/_Project/Prefabs/UI/";
    static UIWorkspaceStylePalette palette;
    static T Ref<T>(UnityEngine.Object o,string name) where T:UnityEngine.Object => new SerializedObject(o).FindProperty(name).objectReferenceValue as T;
    static void Wire(UnityEngine.Object o,string name,UnityEngine.Object value){var so=new SerializedObject(o);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static RectTransform Node(Transform parent,string name){var rect=parent.Find(name) as RectTransform;if(rect==null){rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);}rect.gameObject.layer=parent.gameObject.layer;return rect;}
    static void Stretch(RectTransform t,float l=0,float b=0,float r=0,float top=0){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=new Vector2(l,b);t.offsetMax=new Vector2(-r,-top);}
    static void Place(RectTransform t,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot){t.anchorMin=t.anchorMax=anchor;t.pivot=pivot;t.anchoredPosition=pos;t.sizeDelta=size;}
    static void Top(RectTransform t,float y,float height){t.anchorMin=new Vector2(0,1);t.anchorMax=Vector2.one;t.pivot=new Vector2(.5f,1);t.anchoredPosition=new Vector2(0,-y);t.sizeDelta=new Vector2(0,height);}
    static void Paint(Image i,string art,bool hollow=false){i.sprite=palette.GetArtwork(art)??throw new Exception("Missing art "+art);i.overrideSprite=null;i.color=Color.white;i.type=i.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;i.fillCenter=!hollow;i.raycastTarget=false;}
    static Image Art(Transform parent,string name,string art,bool hollow=false){var t=Node(parent,name);var i=t.GetComponent<Image>();if(i==null)i=t.gameObject.AddComponent<Image>();Paint(i,art,hollow);return i;}
    static LayoutElement Layout(Component c,float width,float height){var l=c.GetComponent<LayoutElement>();if(l==null)l=c.gameObject.AddComponent<LayoutElement>();l.minWidth=width;l.preferredWidth=width;l.flexibleWidth=0;l.minHeight=height;l.preferredHeight=height;l.flexibleHeight=0;return l;}
    static void Text(TMP_Text t,float size,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft){t.font=palette.Font;t.fontSharedMaterial=palette.Font.material;t.fontSize=size;t.enableAutoSizing=false;t.fontStyle=FontStyles.Normal;t.color=new Color(.91f,.875f,.78f);t.alignment=alignment;t.margin=Vector4.zero;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;}
    static TextMeshProUGUI Label(Transform parent,string name,float size){var t=Node(parent,name);var label=t.GetComponent<TextMeshProUGUI>();if(label==null)label=t.gameObject.AddComponent<TextMeshProUGUI>();Text(label,size);return label;}
    static void DisableStyle(Transform parent){foreach(var style in parent.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)){style.enabled=false;Wire(style,"palette",null);}}
    static void FramedSurface(Transform root)
    {
        var image=root.GetComponent<Image>();if(image!=null)image.enabled=false;
        var surface=Art(root,"HudSurface","surface_solid");Stretch(surface.rectTransform,5,5,5,5);Layout(surface,0,0).ignoreLayout=true;surface.transform.SetAsFirstSibling();
        var border=Art(root,"HudBorder","tooltip_frame",true);Stretch(border.rectTransform);Layout(border,0,0).ignoreLayout=true;border.transform.SetAsLastSibling();
    }
    static void Bar(Slider slider,TMP_Text value,float y,Color tint)
    {
        Top((RectTransform)slider.transform,y,12);slider.interactable=false;
        StyleVitalBar(slider,1,tint);
        Text(value,18,TextAlignmentOptions.TopRight);Top(value.rectTransform,-22,22);
    }
    static void StyleVitalBar(Slider slider,float scale,Color tint)
    {
        Paint(slider.transform.Find("Background").GetComponent<Image>(),"slider_track");
        var background=slider.transform.Find("Background").GetComponent<Image>();
        background.pixelsPerUnitMultiplier=1/scale;
        Stretch(background.rectTransform);
        var fill=slider.fillRect.GetComponent<Image>();Paint(fill,"fill_runtime");fill.type=Image.Type.Simple;fill.color=tint;
        fill.pixelsPerUnitMultiplier=1;
        Stretch((RectTransform)slider.transform.Find("Fill Area"),6*scale,3*scale,6*scale,3*scale);
        slider.transform.Find("GothicBarFrame").GetComponent<Image>().enabled=false;
    }
    static void Vitals(GameObject root)
    {
        DisableStyle(root.transform);var v=root.GetComponent<CombatVitalsView>();
        Layout(v,240,118);Bar(Ref<Slider>(v,"healthSlider"),Ref<TextMeshProUGUI>(v,"healthValueText"),20,new Color(.7f,.12f,.1f));
        Bar(Ref<Slider>(v,"resourceSlider"),Ref<TextMeshProUGUI>(v,"resourceValueText"),58,Color.white);
        var action=Ref<TextMeshProUGUI>(v,"actionValueText");Text(action,18);Top(action.rectTransform,76,24);
        var pips=(RectTransform)Ref<Transform>(v,"actionPointPipRoot");Top(pips,102,16);pips.GetComponent<Image>().enabled=false;
        var group=pips.GetComponent<HorizontalLayoutGroup>();group.spacing=3;group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=group.childForceExpandHeight=false;group.childAlignment=TextAnchor.MiddleLeft;
        var pip=Ref<Image>(v,"actionPointPipTemplate");pip.sprite=null;pip.overrideSprite=null;pip.color=Color.white;pip.type=Image.Type.Simple;pip.raycastTarget=false;
        var layout=Layout(pip,16,16);layout.minWidth=0;
        var so=new SerializedObject(pip.GetComponent<ActionPointPipFill>());so.FindProperty("facetedGem").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();pip.transform.Find("GothicPipFrame").GetComponent<Image>().enabled=false;
        so=new SerializedObject(v);so.FindProperty("availableColor").colorValue=new Color(.9f,.94f,1f);so.FindProperty("spentColor").colorValue=new Color(.23f,.27f,.32f);so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SkillSlot(CombatSkillSlotView slot)
    {
        DisableStyle(slot.transform);var root=slot.transform;Paint(root.GetComponent<Image>(),"item_frame",true);root.GetComponent<Image>().raycastTarget=true;
        var button=Ref<Button>(slot,"button");button.transition=Selectable.Transition.None;
        var icon=Ref<Image>(slot,"icon");var unavailable=Ref<Image>(slot,"unavailableOverlay");GodmistSkillTreeSetup.ConfigureIconMask(root,ref icon,ref unavailable);Wire(slot,"icon",icon);Wire(slot,"unavailableOverlay",unavailable);
        var border=root.Find("Border").GetComponent<Image>();Paint(border,"item_frame",true);Stretch(border.rectTransform);border.transform.SetAsLastSibling();
        var selected=Ref<Image>(slot,"selectedFrame");Paint(selected,"slot_selected",true);Stretch(selected.rectTransform);selected.enabled=false;selected.transform.SetAsLastSibling();
        var shortcut=Ref<TextMeshProUGUI>(slot,"shortcutText");Text(shortcut,16,TextAlignmentOptions.TopRight);Place(shortcut.rectTransform,new Vector2(1,1),new Vector2(-6,-4),new Vector2(22,20),new Vector2(1,1));shortcut.transform.SetAsLastSibling();
        var knives=Ref<TextMeshProUGUI>(slot,"hotkeyText");Text(knives,12,TextAlignmentOptions.BottomRight);Place(knives.rectTransform,new Vector2(1,0),new Vector2(-6,4),new Vector2(36,16),new Vector2(1,0));knives.transform.SetAsLastSibling();
        Ref<TextMeshProUGUI>(slot,"actionCostText").enabled=false;
    }
    static void Bottom(Transform root)
    {
        DisableStyle(root);FramedSurface(root);
        var fitter=root.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var group=root.GetComponent<HorizontalLayoutGroup>();group.padding=new RectOffset(16,16,14,14);group.spacing=18;group.childAlignment=TextAnchor.MiddleCenter;group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=group.childForceExpandHeight=false;
        var active=root.GetComponentInChildren<CombatActiveUnitView>(true);Layout(active,350,168);active.GetComponent<Image>().enabled=false;
        var title=Ref<TextMeshProUGUI>(active,"unitNameText");Text(title,20);Top(title.rectTransform,0,24);title.rectTransform.offsetMin=new Vector2(92,title.rectTransform.offsetMin.y);
        var portrait=Ref<Image>(active,"portrait");Place(portrait.rectTransform,new Vector2(0,1),new Vector2(12,-30),new Vector2(72,72),new Vector2(0,1));portrait.preserveAspect=true;
        var frame=Art(active.transform,"PortraitFrame","item_frame",true);Place(frame.rectTransform,new Vector2(0,1),new Vector2(8,-26),new Vector2(80,80),new Vector2(0,1));
        var accent=Ref<Image>(active,"sideFrame");accent.enabled=false;
        var vitals=Ref<CombatVitalsView>(active,"vitals");Place((RectTransform)vitals.transform,new Vector2(0,1),new Vector2(98,-26),new Vector2(240,118),new Vector2(0,1));
        var statuses=Ref<CombatStatusCollectionView>(active,"statusCollection");Place((RectTransform)statuses.transform,new Vector2(0,1),new Vector2(98,-148),new Vector2(240,20),new Vector2(0,1));
        var bar=root.GetComponentInChildren<CombatSkillBarView>(true);var grid=Ref<GridLayoutGroup>(bar,"skillGrid");grid.cellSize=new Vector2(72,72);grid.spacing=new Vector2(7,7);grid.padding=new RectOffset(0,0,0,0);var barLayout=Layout(bar,388,72);barLayout.minHeight=0;
        var viewport=Ref<ScrollRect>(bar,"skillScroll").viewport;viewport.GetComponent<Image>().color=Color.clear;
        SkillSlot(Ref<CombatSkillSlotView>(bar,"skillSlotTemplate"));
        var buttons=root.GetComponentInChildren<CombatActionButtonsView>(true);Layout(buttons,128,64);buttons.GetComponent<Image>().enabled=false;
        var index=0;foreach(var button in buttons.GetComponentsInChildren<Button>(true)){Place((RectTransform)button.transform,new Vector2(0,.5f),new Vector2(index++*64,0),new Vector2(56,56),new Vector2(0,.5f));Paint(button.GetComponent<Image>(),"button_normal");button.GetComponent<Image>().raycastTarget=true;button.transition=Selectable.Transition.None;}
    }
    static void Turn(GameObject root)
    {
        var view=root.GetComponent<CombatTurnPortraitView>();Layout(view,68,68);((RectTransform)root.transform).sizeDelta=new Vector2(68,68);Paint(root.GetComponent<Image>(),"item_frame",true);
        TimelineHealthBar(Ref<Slider>(view,"healthSlider"));
        var portrait=Ref<Image>(view,"portrait");GodmistSkillTreeSetup.ConfigureIconMask(root.transform,ref portrait);Wire(view,"portrait",portrait);
        var normal=Art(root.transform,"PortraitFrame","item_frame",true);Stretch(normal.rectTransform);
        var active=Ref<Image>(view,"activeHighlight");Paint(active,"slot_selected",true);Stretch(active.rectTransform);active.transform.SetAsLastSibling();active.enabled=false;
        var so=new SerializedObject(view);var edges=so.FindProperty("activeEdges");for(var i=0;i<edges.arraySize;i++)((Image)edges.GetArrayElementAtIndex(i).objectReferenceValue).enabled=false;edges.arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();
        Wire(view,"playerHighlightSprite",palette.GetArtwork("slot_selected"));Wire(view,"enemyHighlightSprite",palette.GetArtwork("slot_selected"));
        var corners=Node(root.transform,"HoverCorners");Stretch(corners,-3,-3,-3,-3);var hover=corners.GetComponent<SkillTreeActiveNodeFrame>();if(hover==null)hover=corners.gameObject.AddComponent<SkillTreeActiveNodeFrame>();hover.Configure(false,true,2);hover.color=new Color(.8f,.94f,1f);hover.enabled=false;so=new SerializedObject(hover);so.FindProperty("cornerCut").floatValue=0;so.ApplyModifiedPropertiesWithoutUndo();Wire(view,"hoverHighlight",hover);
        var unknown=Ref<TextMeshProUGUI>(view,"unknownLabel");Text(unknown,26,TextAlignmentOptions.Center);Stretch(unknown.rectTransform,6,6,6,6);unknown.transform.SetAsLastSibling();
    }
    static void TimelineHealthBar(Slider slider)
    {
        if (slider == null || slider.fillRect == null)
            throw new InvalidOperationException("Timeline portrait is missing its authored health slider.");
        // The HUD reserves a separate bottom row for this strip; it must not
        // overlap the opaque bottom edge of HudBorder.
        Place((RectTransform)slider.transform,new Vector2(.5f,0),new Vector2(0,-2),new Vector2(56,6),new Vector2(.5f,1));
        slider.interactable=false;slider.transition=Selectable.Transition.None;
        slider.navigation=new Navigation{mode=Navigation.Mode.None};
        slider.direction=Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
        slider.SetValueWithoutNotify(1);
        StyleVitalBar(slider,.5f,new Color(.7f,.12f,.1f));
        slider.transform.Find("GothicBarFrame")?.gameObject.SetActive(false);
        slider.gameObject.SetActive(true);
    }

    static void TimelineHealthLayout(Transform top)
    {
        ((RectTransform)top).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,108);
        Stretch((RectTransform)top.Find("TurnOrderPanel"),14,26,14,14);
    }

    [MenuItem("Godmist/UI/Configure timeline health strips")]
    public static void ConfigureTimelineHealthStrips()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var path=UI+"CombatHUD/PF_CombatTurnPortrait.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            TimelineHealthBar(Ref<Slider>(root.GetComponent<CombatTurnPortraitView>(),"healthSlider"));
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var hudPath=UI+"Workspace/PF_UIW_CombatHud.prefab";
        var hud=PrefabUtility.LoadPrefabContents(hudPath);
        try
        {
            TimelineHealthLayout(hud.transform.Find("TopBar"));
            PrefabUtility.SaveAsPrefabAsset(hud,hudPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(hud);}
        AssetDatabase.SaveAssets();
        Debug.Log("TIMELINE_HEALTH_STRIPS_CONFIGURED: shared bottom HUD artwork, 56x6 track, 50x3 fill; 108 px panel with a reserved strip row above its bottom border.");
    }

    static void Hud(GameObject root)
    {
        var top=root.transform.Find("TopBar");DisableStyle(top);FramedSurface(top);((RectTransform)top).sizeDelta=new Vector2(640,108);
        TimelineHealthLayout(top);top.Find("TurnOrderPanel/TurnOrderText").gameObject.SetActive(false);
        var turns=top.GetComponentInChildren<HorizontalLayoutGroup>(true);turns.childAlignment=TextAnchor.MiddleCenter;turns.spacing=6;turns.childControlWidth=turns.childControlHeight=true;turns.childForceExpandWidth=turns.childForceExpandHeight=false;
        Bottom(root.transform.Find("BottomPanel"));
        foreach(var drawer in root.GetComponentsInChildren<CombatEdgeDrawer>(true))
        {
            var chevron=Ref<RectTransform>(drawer,"chevronRect");if(chevron!=null)Text(chevron.GetComponent<TMP_Text>(),16,TextAlignmentOptions.Center);
        }
        var hud=root.GetComponent<CombatHudView>();var target=Ref<GameObject>(hud,"targetPanel");DisableStyle(target.transform);Paint(target.GetComponent<Image>(),"tooltip_frame");Text(Ref<TextMeshProUGUI>(hud,"targetName"),18);Text(Ref<TextMeshProUGUI>(hud,"targetHealth"),16);target.SetActive(false);
        var template=Node(root.transform.Find("Overlay/TooltipLayer"),"TargetPreviewTemplate");var bg=template.GetComponent<Image>();if(bg==null)bg=template.gameObject.AddComponent<Image>();Paint(bg,"surface_solid");
        var cg=template.GetComponent<CanvasGroup>();if(cg==null)cg=template.gameObject.AddComponent<CanvasGroup>();cg.alpha=0;cg.interactable=false;cg.blocksRaycasts=false;
        var canvas=template.GetComponent<Canvas>();if(canvas==null)canvas=template.gameObject.AddComponent<Canvas>();var so=new SerializedObject(canvas);so.FindProperty("m_OverrideSorting").boolValue=true;so.FindProperty("m_SortingOrder").intValue=180;so.ApplyModifiedPropertiesWithoutUndo();
        var view=template.GetComponent<CombatTargetPreviewView>();if(view==null)view=template.gameObject.AddComponent<CombatTargetPreviewView>();
        Place(template,new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,136),new Vector2(.5f,.5f));
        var title=Label(template,"Title",20);Top(title.rectTransform,12,26);title.rectTransform.offsetMin=new Vector2(16,title.rectTransform.offsetMin.y);title.rectTransform.offsetMax=new Vector2(-16,title.rectTransform.offsetMax.y);Wire(view,"titleText",title);
        var details=Label(template,"Details",16);Stretch(details.rectTransform,16,40,16,44);details.alignment=TextAlignmentOptions.TopLeft;Wire(view,"label",details);
        var status=Art(template,"Status","fill_runtime");Place(status.rectTransform,new Vector2(0,0),new Vector2(16,12),new Vector2(24,24),Vector2.zero);status.preserveAspect=true;Wire(view,"statusIcon",status);
        var chance=Label(template,"StatusChance",16);Place(chance.rectTransform,Vector2.zero,new Vector2(48,12),new Vector2(100,24),Vector2.zero);Wire(view,"statusChanceLabel",chance);
        var border=Art(template,"Border","tooltip_frame",true);Stretch(border.rectTransform);template.gameObject.SetActive(false);Wire(hud,"targetPreviewTemplate",view);
    }
    [MenuItem("Godmist/UI/Restyle combat HUD")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        foreach(var name in new[]{"Workspace/PF_UIW_CombatVitals","Workspace/PF_UIW_CombatSkillSlot","CombatHUD/PF_CombatTurnPortrait","Workspace/PF_UIW_CombatActionPanel","Workspace/PF_UIW_CombatHud"})
        {
            var path=UI+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{switch(name){case "Workspace/PF_UIW_CombatVitals":Vitals(root);break;case "Workspace/PF_UIW_CombatSkillSlot":SkillSlot(root.GetComponent<CombatSkillSlotView>());break;case "CombatHUD/PF_CombatTurnPortrait":Turn(root);break;case "Workspace/PF_UIW_CombatActionPanel":Bottom(root.transform);break;default:Hud(root);break;}PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
    }
}
