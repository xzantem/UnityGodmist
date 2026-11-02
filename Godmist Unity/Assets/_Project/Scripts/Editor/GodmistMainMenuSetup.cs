using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class GodmistMenuBackgroundImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if(assetPath!="Assets/_Project/UI/Backgrounds/GodmistMenuRuins-v1.png")return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
    }
}

/// <summary>Explicit native authoring for the menu scene and shared save-row prefab.</summary>
public static partial class GodmistMainMenuSetup
{
    public const string ScenePath="Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
    private const string RowPath="Assets/_Project/Prefabs/UI/PF_SaveListEntry.prefab";
    private static UIWorkspaceStylePalette palette;
    private static Sprite Art(string name)=>palette.GetArtwork(name)??throw new InvalidOperationException("Missing art "+name);
    private static T Reference<T>(UnityEngine.Object owner,string property)where T:UnityEngine.Object => new SerializedObject(owner).FindProperty(property).objectReferenceValue as T;
    private static void Assign(UnityEngine.Object owner,string property,UnityEngine.Object value)
    {var data=new SerializedObject(owner);data.FindProperty(property).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
    private static void Bool(UnityEngine.Object owner,string property,bool value)
    {var data=new SerializedObject(owner);data.FindProperty(property).boolValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
    private static void Fixed(Transform node,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {var rect=(RectTransform)node;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;}
    private static void Stretch(Transform node,float left=0,float bottom=0,float right=0,float top=0)
    {var rect=(RectTransform)node;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
    private static void Top(Transform node,float y,float height,float left=0,float right=0)
    {var rect=(RectTransform)node;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2((left-right)*.5f,-y);rect.sizeDelta=new Vector2(-left-right,height);}
    private static Image Image(Transform parent,string name,string sprite)
    {
        var node=parent.Find(name);
        if(node==null){node=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).transform;node.SetParent(parent,false);node.gameObject.layer=5;}
        var image=node.GetComponent<Image>();Paint(image,sprite);image.raycastTarget=false;return image;
    }
    private static void Paint(Image image,string sprite)
    {if(image==null)return;image.sprite=Art(sprite);image.overrideSprite=null;image.color=Color.white;image.type=image.sprite.border.sqrMagnitude>0?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.fillCenter=true;}
    private static TMP_Text Text(Transform parent,string name,string value,float size)
    {
        var node=parent.Find(name);
        if(node==null){node=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI)).transform;node.SetParent(parent,false);node.gameObject.layer=5;}
        var text=node.GetComponent<TMP_Text>();text.font=palette.Font;text.fontSize=size;text.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);text.text=value;text.raycastTarget=false;
        return text;
    }
    private static void Button(Button button,string family)
    {
        var image=button.targetGraphic as Image;Paint(image,family+"_normal");
        button.transition=Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=Art(family+"_hover"),pressedSprite=Art(family+"_pressed"),selectedSprite=Art(family+"_selected"),disabledSprite=Art(family+"_disabled")};
        var colors=button.colors;colors.normalColor=colors.highlightedColor=colors.pressedColor=colors.selectedColor=colors.disabledColor=Color.white;colors.colorMultiplier=1;button.colors=colors;
        image?.CrossFadeColor(Color.white,0,true,true);
        foreach(var style in button.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
        {var data=new SerializedObject(style);data.FindProperty("textStyle").enumValueIndex=(int)UIWorkspaceTextStyle.Heading;data.ApplyModifiedPropertiesWithoutUndo();style.Apply();}
    }
    private static void WithoutLayout(Transform node)
    {foreach(var layout in node.GetComponents<LayoutGroup>())layout.enabled=false;foreach(var fitter in node.GetComponents<ContentSizeFitter>())fitter.enabled=false;}
    private static Image Selection(Button button,string name)
    {var frame=Image(button.transform,name,"slot_selected");Stretch(frame.transform);frame.fillCenter=false;frame.enabled=false;return frame;}

    [MenuItem("Godmist/UI/Configure main menu and save browser")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        AssetDatabase.ImportAsset("Assets/_Project/UI/Backgrounds/GodmistMenuRuins-v1.png",ImportAssetOptions.ForceUpdate);
        var row=PrefabUtility.LoadPrefabContents(RowPath);
        try
        {
            var view=row.GetComponent<SaveListEntryView>();var button=Reference<Button>(view,"button");Button(button,"list_row");
            Assign(view,"normalBackground",Art("list_row_normal"));Assign(view,"selectedBackground",Art("list_row_selected"));
            var title=Reference<TMP_Text>(view,"titleText");var details=Reference<TMP_Text>(view,"detailsText");
            Top(title.transform,10,28,16,16);Top(details.transform,42,44,16,16);
            title.font=details.font=palette.Font;title.fontSize=20;details.fontSize=18;
            title.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);details.color=palette.GetTextColor(UIWorkspaceTextStyle.Detail);
            title.textWrappingMode=TextWrappingModes.NoWrap;title.overflowMode=TextOverflowModes.Ellipsis;
            details.textWrappingMode=TextWrappingModes.NoWrap;details.overflowMode=TextOverflowModes.Ellipsis;
            var element=row.GetComponent<LayoutElement>()??row.AddComponent<LayoutElement>();element.minHeight=element.preferredHeight=94;element.flexibleHeight=0;
            var navigation=button.navigation;navigation.mode=Navigation.Mode.Automatic;button.navigation=navigation;
            PrefabUtility.SaveAsPrefabAsset(row,RowPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(row);}

        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var canvas=FindMenuCanvas(scene);
        var menu=canvas.Find("MainMenuRoot");var viewMenu=menu.GetComponent<MainMenuView>();var saves=menu.GetComponent<SaveLoadPanelView>();
        Reference<TMP_Text>(viewMenu,"messageText").text=string.Empty;
        var background=Image(menu,"GothicMenuBackdrop","surface_solid");
        background.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Backgrounds/GodmistMenuRuins-v1.png");background.type=UnityEngine.UI.Image.Type.Simple;Stretch(background.transform);background.transform.SetAsFirstSibling();
        var shade=Image(menu,"GothicMenuShade","scrim");shade.color=new Color(1,1,1,.24f);Stretch(shade.transform);shade.transform.SetSiblingIndex(1);
        var column=menu.Find("MainMenuColumn");Fixed(column,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(84,0),new Vector2(300,470));
        foreach(var button in column.GetComponentsInChildren<Button>(true))
        {
            Button(button,"button");
            var primary=button==Reference<Button>(viewMenu,"continueButton");
            Paint(button.targetGraphic as Image,primary?"button_selected":"list_row_normal");
            var height=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();height.preferredHeight=primary?56:44; height.minHeight=height.preferredHeight;
            foreach(var style in button.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
            {var data=new SerializedObject(style);data.FindProperty("textStyle").enumValueIndex=(int)(primary?UIWorkspaceTextStyle.Title:UIWorkspaceTextStyle.Heading);data.ApplyModifiedPropertiesWithoutUndo();style.Apply();}
        }
        Assign(viewMenu,"loadSelectionFrame",Selection(Reference<Button>(viewMenu,"loadButton"),"GothicActiveSection"));
        Assign(viewMenu,"settingsSelectionFrame",Selection(Reference<Button>(viewMenu,"settingsButton"),"GothicActiveSection"));

        var panel=Reference<GameObject>(saves,"panelRoot").transform;WithoutLayout(panel);
        Fixed(panel,new Vector2(.65f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1040,650));Paint(panel.GetComponent<Image>(),"surface_solid");
        var outerFrame=Image(panel,"GothicOuterFrame","window_frame");Stretch(outerFrame.transform);outerFrame.fillCenter=false;outerFrame.transform.SetAsLastSibling();
        var divider=Image(panel,"GothicColumnDivider","surface_solid");divider.sprite=null;divider.color=new Color(.28f,.31f,.28f,.65f);
        var dividerRect=divider.rectTransform;dividerRect.anchorMin=new Vector2(0,0);dividerRect.anchorMax=new Vector2(0,1);dividerRect.pivot=new Vector2(.5f,.5f);dividerRect.anchoredPosition=new Vector2(473,6);dividerRect.sizeDelta=new Vector2(2,-180);
        var titleText=Reference<TMP_Text>(saves,"titleText");var close=Reference<Button>(saves,"closeButton");var header=titleText.transform.parent;WithoutLayout(header);Top(header,16,48,24,24);
        Top(titleText.transform,4,36,0,180);titleText.font=palette.Font;titleText.fontSize=26;titleText.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);
        Fixed(close.transform,new Vector2(1,.5f),new Vector2(1,.5f),Vector2.zero,new Vector2(136,40));Button(close,"button");
        var content=Reference<Transform>(saves,"saveListContent");var scroll=content.GetComponentInParent<ScrollRect>(true);var listPanel=scroll.transform.parent;WithoutLayout(listPanel);
        Fixed(listPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-84),new Vector2(438,480));Paint(listPanel.GetComponent<Image>(),"surface_solid");
        var listLabel=listPanel.Find("ListLabelText");if(listLabel!=null){Top(listLabel,4,28,14,14);listLabel.GetComponent<TMP_Text>().fontSize=20;listLabel.GetComponent<TMP_Text>().text="Kampania";}
        var selectorNode=listPanel.Find("GothicCampaignSelector");
        if(selectorNode==null)
        {
            var settings=menu.GetComponent<MainMenuSettingsView>();
            var selectorCopy=UnityEngine.Object.Instantiate(Reference<TMP_Dropdown>(settings,"resolutionDropdown"),listPanel);
            selectorNode=selectorCopy.transform;selectorNode.name="GothicCampaignSelector";
        }
        selectorNode.gameObject.SetActive(true);Top(selectorNode,36,44,14,14);
        var selector=selectorNode.GetComponent<TMP_Dropdown>();selector.onValueChanged.RemoveAllListeners();selector.ClearOptions();selector.AddOptions(new System.Collections.Generic.List<string>{"Wybierz kampanię"});
        // Dropdown artwork already contains a baked arrow. Use a plain frame so
        // the separately positioned arrow remains legible in every state.
        Paint(selector.targetGraphic as Image,"list_row_normal");selector.transition=Selectable.Transition.SpriteSwap;
        selector.spriteState=new SpriteState{highlightedSprite=Art("list_row_hover"),pressedSprite=Art("list_row_pressed"),selectedSprite=Art("list_row_selected"),disabledSprite=Art("list_row_disabled")};
        foreach(var text in selector.GetComponentsInChildren<TMP_Text>(true)){text.font=palette.Font;text.fontSize=20;text.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);}
        Stretch(selector.captionText.transform,14,0,48,0);selector.captionText.textWrappingMode=TextWrappingModes.NoWrap;selector.captionText.overflowMode=TextOverflowModes.Ellipsis;
        Paint(selector.template.GetComponent<Image>(),"dropdown_popup");selector.template.sizeDelta=new Vector2(0,260);
        selector.alphaFadeSpeed=0;
        var selectorScroll=selector.template.GetComponent<ScrollRect>();
        if(selectorScroll!=null&&selectorScroll.verticalScrollbar!=null)
        {
            var bar=selectorScroll.verticalScrollbar;Paint(bar.GetComponent<Image>(),"scrollbar_track");Paint(bar.targetGraphic as Image,"scrollbar_thumb_normal");
            bar.transition=Selectable.Transition.SpriteSwap;bar.spriteState=new SpriteState{highlightedSprite=Art("scrollbar_thumb_hover"),pressedSprite=Art("scrollbar_thumb_pressed"),selectedSprite=Art("scrollbar_thumb_hover"),disabledSprite=Art("scrollbar_thumb_disabled")};
        }
        var arrow=selectorNode.Find("Arrow");if(arrow!=null)
        {
            Paint(arrow.GetComponent<Image>(),"glyph_next");
            Fixed(arrow,new Vector2(1,.5f),new Vector2(.5f,.5f),new Vector2(-24,0),new Vector2(16,16));
            arrow.localRotation=Quaternion.Euler(0,0,-90);arrow.GetComponent<Image>().preserveAspect=true;
        }
        var itemToggle=selector.template.GetComponentInChildren<Toggle>(true);
        if(itemToggle!=null)
        {
            var itemRect=(RectTransform)itemToggle.transform;itemRect.sizeDelta=new Vector2(itemRect.sizeDelta.x,36);
            Paint(itemToggle.targetGraphic as Image,"list_row_normal");itemToggle.transition=Selectable.Transition.SpriteSwap;
            itemToggle.spriteState=new SpriteState{highlightedSprite=Art("list_row_hover"),pressedSprite=Art("list_row_pressed"),selectedSprite=Art("list_row_selected"),disabledSprite=Art("list_row_disabled")};
            if(itemToggle.graphic is Image mark)Paint(mark,"checkmark");
        }
        Assign(saves,"campaignDropdown",selector);
        var status=Text(listPanel,"GothicCampaignStatus","",18);Top(status.transform,86,34,14,14);Assign(saves,"campaignStatusText",status);
        var history=Image(listPanel,"GothicHistoryButton","button_normal");Fixed(history.transform,Vector2.zero,Vector2.zero,new Vector2(14,6),new Vector2(390,36));history.raycastTarget=true;
        var historyControl=history.GetComponent<Button>()??history.gameObject.AddComponent<Button>();historyControl.targetGraphic=history;Button(historyControl,"button");
        var historyLabel=Text(history.transform,"Label","Starsze autozapisy",18);Stretch(historyLabel.transform,8,0,8,0);historyLabel.alignment=TextAlignmentOptions.Center;Assign(saves,"historyButton",historyControl);
        Stretch(scroll.transform,14,52,14,132);var scrollImage=scroll.GetComponent<Image>();if(scrollImage!=null)scrollImage.color=Color.clear;
        Stretch(scroll.viewport,0,0,20,0);
        var viewportImage=scroll.viewport.GetComponent<Image>();var mask=scroll.viewport.GetComponent<Mask>();if(mask!=null)mask.showMaskGraphic=false;if(viewportImage!=null)viewportImage.color=mask!=null?Color.white:Color.clear;
        var grid=content.GetComponent<VerticalLayoutGroup>();if(grid!=null){grid.spacing=8;grid.childControlWidth=true;grid.childControlHeight=true;grid.childForceExpandHeight=false;}
        var thumbRoot=scroll.transform.Find("GothicVerticalScrollbar");
        if(thumbRoot==null){thumbRoot=new GameObject("GothicVerticalScrollbar",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Scrollbar)).transform;thumbRoot.SetParent(scroll.transform,false);thumbRoot.gameObject.layer=5;}
        var thumbRect=(RectTransform)thumbRoot;thumbRect.anchorMin=new Vector2(1,0);thumbRect.anchorMax=Vector2.one;thumbRect.pivot=new Vector2(1,.5f);thumbRect.offsetMin=new Vector2(-14,0);thumbRect.offsetMax=Vector2.zero;
        Paint(thumbRoot.GetComponent<Image>(),"scrollbar_track");
        var slidingArea=thumbRoot.Find("SlidingArea");if(slidingArea==null){slidingArea=new GameObject("SlidingArea",typeof(RectTransform)).transform;slidingArea.SetParent(thumbRoot,false);slidingArea.gameObject.layer=5;}
        Stretch(slidingArea,0,12,0,12);var handle=Image(slidingArea,"Handle","scrollbar_thumb_normal");Stretch(handle.transform);handle.rectTransform.sizeDelta=new Vector2(0,24);handle.raycastPadding=new Vector4(-8,0,-8,0);handle.raycastTarget=true;
        var oldHandle=thumbRoot.Find("Handle");if(oldHandle!=null)UnityEngine.Object.DestroyImmediate(oldHandle.gameObject);
        var scrollbar=thumbRoot.GetComponent<Scrollbar>();scrollbar.handleRect=handle.rectTransform;scrollbar.targetGraphic=handle;scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scrollbar.transition=Selectable.Transition.SpriteSwap;scrollbar.spriteState=new SpriteState{highlightedSprite=Art("scrollbar_thumb_hover"),pressedSprite=Art("scrollbar_thumb_pressed"),selectedSprite=Art("scrollbar_thumb_hover"),disabledSprite=Art("scrollbar_thumb_disabled")};
        var thumbColors=scrollbar.colors;thumbColors.normalColor=thumbColors.highlightedColor=thumbColors.pressedColor=thumbColors.selectedColor=thumbColors.disabledColor=Color.white;scrollbar.colors=thumbColors;
        scroll.vertical=true;scroll.horizontal=false;scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.scrollSensitivity=30;
        Assign(saves,"saveScroll",scroll);Bool(saves,"keyboardNavigation",true);

        var detailsText=Reference<TMP_Text>(saves,"detailsText");var detailsPanel=detailsText.transform.parent;WithoutLayout(detailsPanel);
        Fixed(detailsPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(484,-84),new Vector2(532,480));Paint(detailsPanel.GetComponent<Image>(),"surface_solid");
        var detailHeading=Text(detailsPanel,"GothicPreviewHeading","",18);detailHeading.gameObject.SetActive(false);
        var location=Text(detailsPanel,"GothicLocation","",28);Top(location.transform,8,48,20,20);location.overflowMode=TextOverflowModes.Ellipsis;location.textWrappingMode=TextWrappingModes.NoWrap;Assign(saves,"previewLocationText",location);
        var date=Text(detailsPanel,"GothicSaveDate","",20);Top(date.transform,62,54,20,20);date.overflowMode=TextOverflowModes.Ellipsis;date.textWrappingMode=TextWrappingModes.NoWrap;Assign(saves,"previewDateText",date);
        var partyHeading=Text(detailsPanel,"GothicPartyHeading","Drużyna w chwili zapisu",24);Top(partyHeading.transform,128,30,20,20);Assign(saves,"partyHeadingText",partyHeading);
        var strip=detailsPanel.Find("GothicPartyStrip");
        if(strip==null){strip=new GameObject("GothicPartyStrip",typeof(RectTransform),typeof(HorizontalLayoutGroup)).transform;strip.SetParent(detailsPanel,false);strip.gameObject.layer=5;}
        Top(strip,166,84,20,20);var portraitLayout=strip.GetComponent<HorizontalLayoutGroup>();portraitLayout.spacing=14;portraitLayout.childAlignment=TextAnchor.UpperCenter;portraitLayout.childControlWidth=portraitLayout.childControlHeight=true;portraitLayout.childForceExpandWidth=portraitLayout.childForceExpandHeight=false;
        var partySeparator=Image(detailsPanel,"GothicPartySeparator","separator_horizontal");Top(partySeparator.transform,320,6,20,20);
        Top(detailsText.transform,338,132,20,20);detailsText.font=palette.Font;detailsText.fontSize=20;detailsText.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);detailsText.overflowMode=TextOverflowModes.Ellipsis;
        var portraits=new SerializedObject(saves);var images=portraits.FindProperty("partyPortraits");images.arraySize=5;var captions=portraits.FindProperty("partyCaptionTexts");captions.arraySize=5;
        for(int i=0;i<5;i++)
        {
            var existingSlot=detailsPanel.Find("GothicPartyPortrait"+i);if(existingSlot!=null)existingSlot.SetParent(strip,false);
            var slot=Image(strip,"GothicPartyPortrait"+i,"icon_button_normal");var portraitElement=slot.GetComponent<LayoutElement>()??slot.gameObject.AddComponent<LayoutElement>();portraitElement.minWidth=portraitElement.preferredWidth=84;portraitElement.minHeight=portraitElement.preferredHeight=84;portraitElement.flexibleWidth=portraitElement.flexibleHeight=0;
            var portrait=Image(slot.transform,"Portrait","surface_solid");Stretch(portrait.transform,8,8,8,8);portrait.sprite=null;portrait.enabled=false;portrait.preserveAspect=true;
            images.GetArrayElementAtIndex(i).objectReferenceValue=portrait;
            var caption=Text(slot.transform,"Caption","",20);Top(caption.transform,92,54,-5,-5);caption.alignment=TextAlignmentOptions.Top;caption.textWrappingMode=TextWrappingModes.NoWrap;caption.overflowMode=TextOverflowModes.Ellipsis;captions.GetArrayElementAtIndex(i).objectReferenceValue=caption;
        }
        portraits.ApplyModifiedPropertiesWithoutUndo();
        Godmist.Views.Game.GameBootstrap bootstrap=null;foreach(var sceneRoot in scene.GetRootGameObjects()){bootstrap=sceneRoot.GetComponentInChildren<Godmist.Views.Game.GameBootstrap>(true);if(bootstrap!=null)break;}
        Assign(saves,"previewCatalog",new SerializedObject(bootstrap).FindProperty("contentCatalog").objectReferenceValue);
        var load=Reference<Button>(saves,"loadButton");Fixed(load.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(220,44));Button(load,"button");
        var delete=Reference<Button>(saves,"deleteButton");Fixed(delete.transform,new Vector2(0,0),Vector2.zero,new Vector2(24,24),new Vector2(236,44));Button(delete,"button");
        var message=Reference<TMP_Text>(saves,"messageText");Top(message.transform,562,20,28,28);message.fontSize=16;
        var help=Text(panel,"GothicKeyboardHelp","Strzałki: wybór | Enter: wczytaj | Esc: wróć",18);Fixed(help.transform,Vector2.zero,Vector2.zero,new Vector2(280,24),new Vector2(490,44));help.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);
        ConfigureSaveAction(saves);
        panel.gameObject.SetActive(false);
        ConfigureSettings(menu);
        ConfigureReleaseVersionLabel(menu, viewMenu);
        // Direct native property changes need explicit overrides on scene prefab instances.
        foreach(var component in menu.GetComponentsInChildren<Component>(true))
            if(component!=null&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("GODMIST_MAIN_MENU_CONFIGURED: native scene and save row saved.");
    }
}
