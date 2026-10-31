using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class GodmistMainMenuSetup
{
    [MenuItem("Godmist/UI/Configure main menu settings")]
    public static void ConfigureSettings()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before authoring.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var menu=FindMenuCanvas(scene).Find("MainMenuRoot");
        ConfigureSettings(menu);
        foreach(var component in menu.GetComponentsInChildren<Component>(true))
            if(component!=null&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("GODMIST_SETTINGS_CONFIGURED");
    }

    private static void ConfigureSettings(Transform menu)
    {
        var settings=menu.GetComponent<MainMenuSettingsView>();
        var panel=Reference<GameObject>(settings,"panelRoot").transform;
        WithoutLayout(panel);
        var column=panel.Find("SettingsColumn");if(column!=null)WithoutLayout(column);
        Fixed(panel,new Vector2(.65f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(660,540));
        Paint(panel.GetComponent<Image>(),"surface_solid");
        var title=panel.GetComponentsInChildren<TMP_Text>(true).Single(text=>text.name=="SettingsTitle");
        var volumeLabel=panel.GetComponentsInChildren<TMP_Text>(true).Single(text=>text.name=="VolumeLabel");
        var resolutionLabel=panel.GetComponentsInChildren<TMP_Text>(true).Single(text=>text.name=="ResolutionLabel");
        foreach(var label in new[]{title,volumeLabel,resolutionLabel})label.transform.SetParent(panel,false);
        title.font=volumeLabel.font=resolutionLabel.font=palette.Font;
        title.text="Ustawienia";title.fontSize=32;Top(title.transform,24,42,28,28);
        volumeLabel.text="Głośność ogólna";volumeLabel.fontSize=20;Top(volumeLabel.transform,120,30,28,110);
        resolutionLabel.text="Rozdzielczość";resolutionLabel.fontSize=20;Top(resolutionLabel.transform,270,30,28,28);
        foreach(var label in new[]{title,volumeLabel,resolutionLabel}){label.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);label.raycastTarget=false;}
        Top(Text(panel,"GothicAudioHeading","Dźwięk",24).transform,78,32,28,28);
        Top(Text(panel,"GothicDisplayHeading","Obraz",24).transform,228,32,28,28);
        var separator=Image(panel,"GothicSettingsSeparator","separator_horizontal");Top(separator.transform,206,6,28,28);
        var volumeValue=Text(panel,"GothicVolumeValue","100%",20);Top(volumeValue.transform,120,30,550,28);volumeValue.alignment=TextAlignmentOptions.Right;Assign(settings,"volumeValueText",volumeValue);

        var slider=Reference<Slider>(settings,"volumeSlider");slider.transform.SetParent(panel,false);Top(slider.transform,158,32,28,28);
        slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;slider.direction=Slider.Direction.LeftToRight;
        var track=slider.transform.Find("Background");if(track!=null){Paint(track.GetComponent<Image>(),"slider_track");Stretch(track,0,6,0,6);}
        var fillArea=slider.fillRect.parent;Stretch(fillArea,14,9,14,9);
        var fill=slider.fillRect.GetComponent<Image>();Paint(fill,"fill_runtime");fill.color=new Color(.64f,.72f,.60f,1);
        // Slider.UpdateVisuals stretches the handle on the non-moving axis each
        // time its value changes. A zero-height track keeps the handle at 32.
        var handleArea=(RectTransform)slider.handleRect.parent;
        handleArea.anchorMin=new Vector2(0,.5f);handleArea.anchorMax=new Vector2(1,.5f);handleArea.pivot=new Vector2(.5f,.5f);handleArea.anchoredPosition=Vector2.zero;handleArea.sizeDelta=new Vector2(-28,0);
        var knob=slider.handleRect.GetComponent<Image>();Paint(knob,"slider_knob_normal");knob.preserveAspect=true;knob.raycastTarget=true;slider.targetGraphic=knob;
        var handleRect=slider.handleRect;handleRect.anchorMin=new Vector2(handleRect.anchorMin.x,.5f);handleRect.anchorMax=new Vector2(handleRect.anchorMax.x,.5f);handleRect.pivot=new Vector2(.5f,.5f);handleRect.anchoredPosition=Vector2.zero;handleRect.sizeDelta=new Vector2(28,32);slider.transition=Selectable.Transition.SpriteSwap;
        slider.spriteState=new SpriteState{highlightedSprite=Art("slider_knob_hover"),pressedSprite=Art("slider_knob_pressed"),selectedSprite=Art("slider_knob_hover"),disabledSprite=Art("slider_knob_disabled")};
        WhiteInteraction(slider);

        var dropdown=Reference<TMP_Dropdown>(settings,"resolutionDropdown");dropdown.transform.SetParent(panel,false);Top(dropdown.transform,308,44,28,28);
        Paint(dropdown.targetGraphic as Image,"list_row_normal");dropdown.transition=Selectable.Transition.SpriteSwap;WhiteInteraction(dropdown);
        dropdown.spriteState=new SpriteState{highlightedSprite=Art("list_row_hover"),pressedSprite=Art("list_row_pressed"),selectedSprite=Art("list_row_selected"),disabledSprite=Art("list_row_disabled")};
        foreach(var label in dropdown.GetComponentsInChildren<TMP_Text>(true)){label.font=palette.Font;label.fontSize=20;label.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);}
        Stretch(dropdown.captionText.transform,14,0,48,0);dropdown.captionText.textWrappingMode=TextWrappingModes.NoWrap;dropdown.captionText.overflowMode=TextOverflowModes.Ellipsis;
        Paint(dropdown.template.GetComponent<Image>(),"dropdown_popup");dropdown.template.sizeDelta=new Vector2(0,208);dropdown.alphaFadeSpeed=0;
        var arrow=dropdown.transform.Find("Arrow");Paint(arrow.GetComponent<Image>(),"glyph_next");Fixed(arrow,new Vector2(1,.5f),new Vector2(.5f,.5f),new Vector2(-24,0),new Vector2(16,16));arrow.localRotation=Quaternion.Euler(0,0,-90);
        var popupScroll=dropdown.template.GetComponent<ScrollRect>();
        if(popupScroll!=null&&popupScroll.verticalScrollbar!=null)
        {
            var bar=popupScroll.verticalScrollbar;Paint(bar.GetComponent<Image>(),"scrollbar_track");Paint(bar.targetGraphic as Image,"scrollbar_thumb_normal");
            bar.transition=Selectable.Transition.SpriteSwap;WhiteInteraction(bar);bar.spriteState=new SpriteState{highlightedSprite=Art("scrollbar_thumb_hover"),pressedSprite=Art("scrollbar_thumb_pressed"),selectedSprite=Art("scrollbar_thumb_hover"),disabledSprite=Art("scrollbar_thumb_disabled")};
        }
        var item=dropdown.template.GetComponentInChildren<Toggle>(true);var itemRect=(RectTransform)item.transform;itemRect.sizeDelta=new Vector2(itemRect.sizeDelta.x,36);
        Paint(item.targetGraphic as Image,"list_row_normal");item.transition=Selectable.Transition.SpriteSwap;WhiteInteraction(item);
        item.spriteState=new SpriteState{highlightedSprite=Art("list_row_hover"),pressedSprite=Art("list_row_pressed"),selectedSprite=Art("list_row_selected"),disabledSprite=Art("list_row_disabled")};Paint(item.graphic as Image,"checkmark");

        var fullscreen=Reference<Toggle>(settings,"fullscreenToggle");fullscreen.transform.SetParent(panel,false);Top(fullscreen.transform,368,32,28,28);
        var checkBackground=fullscreen.targetGraphic as Image;Paint(checkBackground,"checkbox_unchecked");Fixed(checkBackground.transform,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(28,28));
        Paint(fullscreen.graphic as Image,"checkmark");Stretch(fullscreen.graphic.transform,6,6,6,6);fullscreen.transition=Selectable.Transition.SpriteSwap;WhiteInteraction(fullscreen);
        fullscreen.spriteState=new SpriteState{highlightedSprite=Art("checkbox_hover"),pressedSprite=Art("checkbox_hover"),selectedSprite=Art("checkbox_hover"),disabledSprite=Art("checkbox_disabled")};
        var toggleLabel=fullscreen.GetComponentInChildren<TMP_Text>(true);toggleLabel.font=palette.Font;toggleLabel.fontSize=20;toggleLabel.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);toggleLabel.text="Pełny ekran (bez ramek)";Stretch(toggleLabel.transform,42,0,0,0);
        var apply=Reference<Button>(settings,"applyButton");var close=Reference<Button>(settings,"closeButton");
        foreach(var button in new[]{apply,close}){button.transform.SetParent(panel,false);Button(button,"button");var label=button.GetComponentInChildren<TMP_Text>(true);label.font=palette.Font;label.fontSize=24;label.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);}
        Fixed(close.transform,Vector2.zero,Vector2.zero,new Vector2(28,24),new Vector2(180,44));
        Fixed(apply.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,24),new Vector2(220,44));Paint(apply.targetGraphic as Image,"button_selected");
        var settingsData=new SerializedObject(settings);settingsData.FindProperty("applyDisabledTextColor").colorValue=palette.GetTextColor(UIWorkspaceTextStyle.Disabled);settingsData.FindProperty("volumeFillColor").colorValue=fill.color;settingsData.ApplyModifiedPropertiesWithoutUndo();
        var message=Reference<TMP_Text>(settings,"messageText");message.transform.SetParent(panel,false);Top(message.transform,414,46,28,28);message.font=palette.Font;message.fontSize=18;message.color=palette.GetTextColor(UIWorkspaceTextStyle.Body);message.text="";
        var outer=Image(panel,"GothicSettingsFrame","window_frame");Stretch(outer.transform);outer.fillCenter=false;outer.transform.SetAsLastSibling();

        var modal=Image(panel,"GothicDisplayConfirmation","scrim");Stretch(modal.transform);modal.color=new Color(1,1,1,.75f);modal.raycastTarget=true;
        var dialog=Image(modal.transform,"Dialog","surface_solid");Fixed(dialog.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,240));dialog.raycastTarget=true;
        var frame=Image(dialog.transform,"Frame","window_frame");Stretch(frame.transform);frame.fillCenter=false;
        var confirmation=Text(dialog.transform,"Message","Zachować ustawienia obrazu?",22);Top(confirmation.transform,24,126,28,28);Assign(settings,"confirmationMessageText",confirmation);
        var revert=MakeSettingsButton(dialog.transform,"Revert","Przywróć",false);Fixed(revert.transform,Vector2.zero,Vector2.zero,new Vector2(28,24),new Vector2(220,44));
        var keep=MakeSettingsButton(dialog.transform,"Keep","Zachowaj",true);Fixed(keep.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,24),new Vector2(220,44));
        Assign(settings,"confirmationRoot",modal.gameObject);Assign(settings,"keepDisplayButton",keep);Assign(settings,"revertDisplayButton",revert);modal.gameObject.SetActive(false);panel.gameObject.SetActive(false);
        // Confirmation navigation cannot escape into disabled settings controls.
        var keepNav=keep.navigation;keepNav.mode=Navigation.Mode.Explicit;keepNav.selectOnLeft=revert;keepNav.selectOnRight=revert;keep.navigation=keepNav;
        var revertNav=revert.navigation;revertNav.mode=Navigation.Mode.Explicit;revertNav.selectOnLeft=keep;revertNav.selectOnRight=keep;revert.navigation=revertNav;
    }
    private static void WhiteInteraction(Selectable control)
    {var colors=control.colors;colors.normalColor=colors.highlightedColor=colors.pressedColor=colors.selectedColor=colors.disabledColor=Color.white;colors.colorMultiplier=1;control.colors=colors;}
    private static Button MakeSettingsButton(Transform parent,string name,string label,bool primary)
    {var image=Image(parent,name,"button_normal");image.raycastTarget=true;var button=image.GetComponent<Button>()??image.gameObject.AddComponent<Button>();button.targetGraphic=image;Button(button,"button");if(primary)Paint(image,"button_selected");var text=Text(image.transform,"Label",label,24);Stretch(text.transform,8,0,8,0);text.alignment=TextAlignmentOptions.Center;return button;}
}
