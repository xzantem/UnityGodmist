using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit authoring pass for existing UI prefabs; never runs automatically.</summary>
public static class GodmistGothicUIPrefabSetup
{
    private const string PrefabRoot = "Assets/_Project/Prefabs/UI/";
    private const string PalettePath = "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset";
    private static UIWorkspaceStylePalette palette;
    private static readonly List<string> report = new();

    [MenuItem("Godmist/UI/Configure enemy awareness indicator")]
    public static void ConfigureAwarenessIndicator()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring UI prefabs.");
        const string path = "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab";
        var skin = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(PalettePath);
        if (skin == null || skin.GetArtwork("icon_button_normal") == null || skin.Font == null)
            throw new InvalidDataException("Godmist awareness requires the existing Gothic frame and font.");
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = root.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            if (view == null) throw new InvalidDataException("Exploration character view is missing.");
            var data = new SerializedObject(view);
            data.FindProperty("awarenessStyle").objectReferenceValue = skin;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Godmist/UI/Apply Gothic skin to UI prefabs")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring UI prefabs.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(PalettePath);
        if (palette == null) throw new InvalidDataException("Workspace style palette is missing.");
        var manifest = GodmistGothicUISpriteImport.ReadManifest();
        var serializedPalette = new SerializedObject(palette);
        var artwork = serializedPalette.FindProperty("artwork");
        artwork.arraySize = manifest.sprites.Length;
        for (var i = 0; i < artwork.arraySize; i++)
            artwork.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(GodmistGothicUISpriteImport.Root + manifest.sprites[i].name + ".png");
        ColorField(serializedPalette,"windowColor",new Color(29/255f,27/255f,33/255f,1));
        ColorField(serializedPalette,"headerColor",new Color(36/255f,33/255f,39/255f,1));
        ColorField(serializedPalette,"sectionColor",new Color(29/255f,27/255f,33/255f,1));
        ColorField(serializedPalette,"raisedColor",new Color(45/255f,41/255f,46/255f,1));
        ColorField(serializedPalette,"tooltipColor",new Color(21/255f,20/255f,26/255f,1));
        ColorField(serializedPalette,"primaryTextColor",new Color(.91f,.875f,.78f,1));
        ColorField(serializedPalette,"secondaryTextColor",new Color(.72f,.72f,.66f,1));
        ColorField(serializedPalette,"disabledTextColor",new Color(.46f,.47f,.44f,1));
        serializedPalette.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();

        var paths = AssetDatabase.FindAssets("t:Prefab",new[]{PrefabRoot.TrimEnd('/')})
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
        var scope = new HashSet<string>(paths,StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        void Visit(string path)
        {
            if (!visited.Add(path)) return;
            foreach (var dependency in AssetDatabase.GetDependencies(path,false))
                if (dependency != path && scope.Contains(dependency)) Visit(dependency);
            ordered.Add(path);
        }
        foreach(var path in paths) Visit(path);
        report.Clear();
        foreach(var path in ordered)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = CaptureBehaviourWiring(root);
                var icons = CaptureContentIcons(root);
                StylePrefab(root);
                var after = CaptureBehaviourWiring(root);
                if (before != after) throw new InvalidDataException("Styling changed nonvisual wiring: " + path);
                if (icons != CaptureContentIcons(root)) throw new InvalidDataException("Styling replaced a content icon/portrait: " + path);
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("TestResults");
        File.WriteAllLines("TestResults/Gothic-UI-prefabs.txt",report);
        Debug.Log("GODMIST_GOTHIC_PREFABS_PASS: " + report.Count + " prefabs; gameplay wiring preserved.");
    }

    private static void ColorField(SerializedObject owner,string field,Color value) => owner.FindProperty(field).colorValue=value;
    private static Sprite Art(string name) => palette.GetArtwork(name) ?? throw new InvalidDataException("Missing Gothic UI sprite: " + name);
    private static void Paint(Image image,string name,bool hollow=false)
    {
        if (image == null) return;
        image.sprite=Art(name);image.color=Color.white;image.overrideSprite=null;
        image.type=image.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;
        image.fillCenter=!hollow;
        image.preserveAspect=false;
        image.pixelsPerUnitMultiplier=1;
        // Compact HUD controls need a smaller rim than full-size windows/buttons.
        var height=image.rectTransform.rect.height;
        var width=image.rectTransform.rect.width;
        var border=image.sprite.border;
        if(height>0 && width>0)
            image.pixelsPerUnitMultiplier=Mathf.Max(1,Mathf.Ceil(Mathf.Max((border.y+border.w+2)/height,(border.x+border.z+2)/width)));
        EditorUtility.SetDirty(image);
    }
    private static void States(Selectable selectable,Image image,string family)
    {
        if(selectable==null || image==null) return;
        Paint(image,family+"_normal");
        selectable.transition=Selectable.Transition.SpriteSwap;
        selectable.spriteState=new SpriteState
        {
            highlightedSprite=Art(family+"_hover"),pressedSprite=Art(family+"_pressed"),
            selectedSprite=Art(family+"_selected"),disabledSprite=Art(family+"_disabled")
        };
        var colors=selectable.colors;colors.normalColor=colors.highlightedColor=colors.pressedColor=colors.selectedColor=colors.disabledColor=Color.white;
        colors.colorMultiplier=1;selectable.colors=colors;
        image.CrossFadeColor(Color.white,0,true,true);
        EditorUtility.SetDirty(selectable);
    }
    private static string ButtonFamily(Button button)
    {
        var name=button.name;
        var size=((RectTransform)button.transform).sizeDelta;
        if(name.Contains("Slot") || name.Contains("IconButton") || name.Contains("CloseButton") || name.Contains("ServiceTab") ||
            (size.x>0 && size.y>0 && size.x<=80 && size.x/size.y<1.5f)) return "icon_button";
        if(button.GetComponent<InventoryItemButtonView>()!=null || button.GetComponent<PartyMemberButtonView>()!=null || button.GetComponent<SaveListEntryView>()!=null || button.transform.Find("TextRoot")!=null || name.Contains("ListRow") || name.Contains("Quest")) return "list_row";
        return "button";
    }
    [MenuItem("Godmist/UI/Configure loot window")]
    public static void ConfigureLootWindow()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring UI prefabs.");
        const string path = PrefabRoot + "Workspace/PF_UIW_DungeonLootWindow.prefab";
        var skin = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(PalettePath);
        var gold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
        var frame = skin != null ? skin.GetArtwork("icon_button_normal") : null;
        if (gold == null || frame == null) throw new InvalidDataException("Loot window artwork is missing.");
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(root.GetComponent<DungeonLootWindowView>());
            var title = (TextMeshProUGUI)data.FindProperty("titleText").objectReferenceValue;
            foreach (var style in title.GetComponents<UIWorkspaceStyleApplier>()) style.enabled = false;
            var rect = title.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32, 4); rect.offsetMax = new Vector2(-56, -4);
            title.text = "Łup z ciała";
            title.font = skin.Font; title.fontSize = 26; title.enableAutoSizing = false;
            title.color = skin.GetTextColor(UIWorkspaceTextStyle.Title);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow;

            var goldSlot = (DungeonLootGoldSlotView)data.FindProperty("goldSlot").objectReferenceValue;
            var goldData = new SerializedObject(goldSlot);
            var icon = (Image)goldData.FindProperty("icon").objectReferenceValue;
            icon.sprite = gold; icon.preserveAspect = true; icon.color = Color.white;
            var template = (DungeonLootItemView)data.FindProperty("itemTemplate").objectReferenceValue;
            var background = template.GetComponent<Image>();
            background.sprite = frame; background.type = Image.Type.Sliced; background.color = Color.white;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StylePrefab(GameObject root)
    {
        foreach(var button in root.GetComponentsInChildren<Button>(true))
            if(button.targetGraphic is Image image && !IsContentIcon(image) &&
                (image.transform==button.transform || image.name is "Background" or "Surface"))
                States(button,image,ButtonFamily(button));

        foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
        {
            var data=new SerializedObject(style);
            data.FindProperty("palette").objectReferenceValue=palette;
            var surface=data.FindProperty("surfaceImage").objectReferenceValue as Image;
            var border=data.FindProperty("borderImage").objectReferenceValue as Image;
            var role=(UIWorkspaceSurfaceStyle)data.FindProperty("surfaceStyle").enumValueIndex;
            var surfaceButton=surface!=null?surface.GetComponentInParent<Button>():null;
            if(surface!=null && (surfaceButton==null || surfaceButton.targetGraphic!=surface))
            {
                var scrollbar=surface.GetComponentInParent<Scrollbar>();
                if(scrollbar!=null) Paint(surface,scrollbar.handleRect==surface.rectTransform?"scrollbar_thumb_normal":"scrollbar_track",scrollbar.handleRect!=surface.rectTransform);
                else if(role!=UIWorkspaceSurfaceStyle.None)
                    Paint(surface,role==UIWorkspaceSurfaceStyle.Header?"header":role==UIWorkspaceSurfaceStyle.Scrim?"scrim":"surface_solid");
                if(role==UIWorkspaceSurfaceStyle.Scrim) surface.color=new Color(1,1,1,.55f);
            }
            if(border!=null)
            {
                var parent=border.transform.parent;
                var interactive=parent!=null?parent.GetComponent<Button>():null;
                if(interactive!=null)
                {
                    Paint(border,"item_frame",true);
                    border.enabled=false;
                    data.FindProperty("borderStyle").enumValueIndex=(int)UIWorkspaceAccentStyle.None;
                }
                else
                {
                    var parentStyle=parent!=null?parent.GetComponent<UIWorkspaceStyleApplier>():null;
                    var parentRole=parentStyle!=null?(UIWorkspaceSurfaceStyle)new SerializedObject(parentStyle).FindProperty("surfaceStyle").enumValueIndex:role;
                    Paint(border,parentRole==UIWorkspaceSurfaceStyle.Window?"window_frame":"tooltip_frame",true);
                }
                border.raycastTarget=false;
            }
            data.ApplyModifiedPropertiesWithoutUndo();style.Apply();
        }

        foreach(var input in root.GetComponentsInChildren<TMP_InputField>(true))
            if(input.targetGraphic is Image inputImage)
                ControlStates(input,inputImage,"input_normal","input_focused","input_focused","input_focused","input_disabled");
        foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if(dropdown.targetGraphic is Image dropdownImage)
                ControlStates(dropdown,dropdownImage,"dropdown_normal","dropdown_hover","dropdown_hover","dropdown_hover","dropdown_disabled");
            if(dropdown.template!=null) Paint(dropdown.template.GetComponent<Image>(),"dropdown_popup");
            var arrow=dropdown.transform.Find("Arrow")?.GetComponent<Image>();
            if(arrow!=null) arrow.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);
        }
        foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            if(toggle.targetGraphic is Image toggleImage)
            {
                var rect=toggleImage.rectTransform.rect;
                if(rect.width>rect.height*1.5f)
                    States(toggle,toggleImage,"list_row");
                else
                    ControlStates(toggle,toggleImage,"checkbox_unchecked","checkbox_hover","checkbox_hover","checkbox_hover","checkbox_disabled");
            }
            if(toggle.graphic is Image checkmark) {Paint(checkmark,"glyph_confirm");checkmark.preserveAspect=true;}
        }
        foreach(var slider in root.GetComponentsInChildren<Slider>(true)) StyleSlider(slider);
        foreach(var scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
        {
            var track=scrollbar.GetComponent<Image>();Paint(track,"scrollbar_track",true);
            var handle=scrollbar.handleRect!=null?scrollbar.handleRect.GetComponent<Image>():null;
            Paint(handle,"scrollbar_thumb_normal");
            scrollbar.transition=Selectable.Transition.SpriteSwap;
            scrollbar.spriteState=new SpriteState{highlightedSprite=Art("scrollbar_thumb_hover"),pressedSprite=Art("scrollbar_thumb_pressed"),selectedSprite=Art("scrollbar_thumb_hover"),disabledSprite=Art("scrollbar_thumb_disabled")};
            handle?.CrossFadeColor(Color.white,0,true,true);
        }
        foreach(var inventory in root.GetComponentsInChildren<InventoryItemButtonView>(true))
        {
            var data=new SerializedObject(inventory);
            data.FindProperty("normalBackground").objectReferenceValue=Art("list_row_normal");
            data.FindProperty("selectedBackground").objectReferenceValue=Art("list_row_selected");
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var party in root.GetComponentsInChildren<PartyMemberButtonView>(true))
        {
            var data=new SerializedObject(party);
            data.FindProperty("normalBackground").objectReferenceValue=Art("list_row_normal");
            data.FindProperty("selectedBackground").objectReferenceValue=Art("list_row_selected");
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.name is "Selected" or "SelectedBorder" or "SelectionFrame") {Paint(image,"slot_selected",true);image.raycastTarget=false;}
            if(image.name=="ResizeHandle") {Paint(image,"resize_grip");image.preserveAspect=true;}
            if(image.name is "CountBackground" or "CountBadge") Paint(image,"counter_badge");
            if(image.name.Contains("ActionPointPipTemplate"))
            {
                Paint(image,"fill_runtime");image.type=Image.Type.Simple;
                Decoration(image.transform,"GothicPipFrame","ap_frame",true);
            }
        }
        foreach(var menu in root.GetComponentsInChildren<PartyRailMenuView>(true))
        {
            var image=menu.transform.Find("Menu")?.GetComponent<Image>();
            if(image!=null) Paint(image,"icon_button_normal");
        }
        foreach(var style in root.GetComponentsInChildren<UIWorkspaceSliderStyleApplier>(true)) style.Apply();
    }
    private static void ControlStates(Selectable control,Image image,string normal,string hover,string pressed,string selected,string disabled)
    {
        Paint(image,normal);
        control.transition=Selectable.Transition.SpriteSwap;
        control.spriteState=new SpriteState{highlightedSprite=Art(hover),pressedSprite=Art(pressed),selectedSprite=Art(selected),disabledSprite=Art(disabled)};
        var colors=control.colors;colors.normalColor=colors.highlightedColor=colors.pressedColor=colors.selectedColor=colors.disabledColor=Color.white;
        colors.colorMultiplier=1;control.colors=colors;image.CrossFadeColor(Color.white,0,true,true);
    }
    private static Image Decoration(Transform parent,string name,string sprite,bool hollow)
    {
        var existing=parent.Find(name);
        var image=existing!=null?existing.GetComponent<Image>():null;
        if(image==null)
        {
            var node=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));node.layer=parent.gameObject.layer;
            var rect=node.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            image=node.GetComponent<Image>();
        }
        Paint(image,sprite,hollow);image.raycastTarget=false;return image;
    }
    private static void StyleSlider(Slider slider)
    {
        var background=slider.transform.Find("Background")?.GetComponent<Image>();
        Paint(background,"bar_background");
        var fill=slider.fillRect!=null?slider.fillRect.GetComponent<Image>():null;
        if(fill!=null)
        {
            var previousColor=fill.color;Paint(fill,"fill_runtime");fill.color=previousColor;
        }
        var loyalty=slider.GetComponentInParent<NpcLoyaltyHeaderView>()!=null;
        var progress=loyalty || slider.name.IndexOf("Experience",StringComparison.OrdinalIgnoreCase)>=0 || slider.name.IndexOf("ExpSlider",StringComparison.OrdinalIgnoreCase)>=0;
        Decoration(slider.transform,"GothicBarFrame",progress?"bar_frame":"slider_track",true);
        if(progress && fill!=null)
        {
            Paint(fill,"fill_gold");
            var glint=Decoration(slider.fillRect,"GothicProgressGlint","exp_loyalty_glint",false);
            var rect=glint.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(24,8);rect.anchoredPosition=Vector2.zero;
            var follower=glint.GetComponent<UIWorkspaceProgressGlint>()??glint.gameObject.AddComponent<UIWorkspaceProgressGlint>();follower.Configure(slider,glint);
        }
        if(slider.handleRect!=null)
        {
            var handle=slider.handleRect.GetComponent<Image>();Paint(handle,"slider_knob_normal");
            slider.transition=Selectable.Transition.SpriteSwap;
            slider.spriteState=new SpriteState{highlightedSprite=Art("slider_knob_hover"),pressedSprite=Art("slider_knob_pressed"),selectedSprite=Art("slider_knob_hover"),disabledSprite=Art("slider_knob_disabled")};
            handle?.CrossFadeColor(Color.white,0,true,true);
        }
    }

    // Comparing paths/object references avoids instance IDs from the temporary
    // prefab scene while protecting gameplay links and persistent events.
    private static string CaptureBehaviourWiring(GameObject root)
    {
        string PathOf(Transform transform)
        {
            var result=transform.name;
            while(transform.parent!=null && transform!=root.transform){transform=transform.parent;result=transform.name+"/"+result;}
            return result;
        }
        string Ref(UnityEngine.Object obj)
        {
            if(obj==null)return "null";
            if(obj is Component component)return PathOf(component.transform)+":"+component.GetType().FullName;
            if(obj is GameObject go)return PathOf(go.transform);
            return AssetDatabase.GetAssetPath(obj)+":"+obj.name;
        }
        var lines=new List<string>();
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(behaviour==null)throw new InvalidDataException("Missing script in "+root.name);
            if(behaviour.GetType().Namespace?.StartsWith("Godmist",StringComparison.Ordinal)!=true ||
                behaviour is UIWorkspaceStyleApplier or UIWorkspaceSliderStyleApplier or UIWorkspaceProgressGlint)continue;
            var data=new SerializedObject(behaviour);var iterator=data.GetIterator();
            while(iterator.Next(true))
                if(iterator.propertyType==SerializedPropertyType.ObjectReference && iterator.propertyPath!="m_Script" &&
                    iterator.propertyPath!="normalBackground" && iterator.propertyPath!="selectedBackground")
                    lines.Add(PathOf(behaviour.transform)+":"+behaviour.GetType().FullName+":"+iterator.propertyPath+"="+Ref(iterator.objectReferenceValue));
        }
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            for(var i=0;i<button.onClick.GetPersistentEventCount();i++)
                lines.Add(PathOf(button.transform)+":click="+Ref(button.onClick.GetPersistentTarget(i))+":"+button.onClick.GetPersistentMethodName(i));
        }
        foreach(var slider in root.GetComponentsInChildren<Slider>(true))
        {
            lines.Add(PathOf(slider.transform)+":slider="+slider.minValue+":"+slider.maxValue+":"+slider.value+":"+slider.direction+":"+Ref(slider.fillRect)+":"+Ref(slider.handleRect));
            for(var i=0;i<slider.onValueChanged.GetPersistentEventCount();i++)
                lines.Add(PathOf(slider.transform)+":value="+Ref(slider.onValueChanged.GetPersistentTarget(i))+":"+slider.onValueChanged.GetPersistentMethodName(i));
        }
        lines.Sort(StringComparer.Ordinal);return string.Join("\n",lines);
    }

    private static bool IsContentIcon(Image image)
    {
        if(image.name is "Icon" or "Portrait" or "Avatar" or "ActionIcon" or "MapImage") return true;
        return image.sprite!=null && AssetDatabase.GetAssetPath(image.sprite).Contains("/Art/");
    }
    private static string CaptureContentIcons(GameObject root)
    {
        return string.Join("\n",root.GetComponentsInChildren<Image>(true).Where(IsContentIcon)
            .Select(image=>AnimationUtility.CalculateTransformPath(image.transform,root.transform)+":"+AssetDatabase.GetAssetPath(image.sprite)+":"+(image.sprite!=null?image.sprite.name:"null"))
            .OrderBy(line=>line,StringComparer.Ordinal));
    }
}
