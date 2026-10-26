using System;
using System.IO;
using Godmist.Views.UI.Workspace;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Views.UI;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scoped native prefab authoring; does not modify the shared window or scenes.</summary>
public static class GodmistCharacterCardSetup
{
    const string Root="Assets/_Project/Prefabs/UI/Workspace/";
    static UIWorkspaceStylePalette palette;
    static Material ghostMaterial;
    static Material godlyMaterial;
    static Material rarityMaterial;
    static readonly Color TextColor=new(.91f,.875f,.78f);
    static T Ref<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object => new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
    static void Wire(UnityEngine.Object owner,string name,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Place(RectTransform t,Vector2 anchor,Vector2 pos,Vector2 size,Vector2? pivot=null)
    {t.anchorMin=t.anchorMax=anchor;t.pivot=pivot??new Vector2(.5f,.5f);t.sizeDelta=size;t.anchoredPosition=pos;}
    static void Stretch(RectTransform t,float left=0,float bottom=0,float right=0,float top=0)
    {t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=new Vector2(left,bottom);t.offsetMax=new Vector2(-right,-top);}
    static void Paint(Image image,string art,bool hollow=false)
    {image.sprite=palette.GetArtwork(art)??throw new Exception("Missing art: "+art);image.overrideSprite=null;image.color=Color.white;image.type=image.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;image.fillCenter=!hollow;}
    static Image ImageAt(Transform parent,string name,string art,bool hollow=false)
    {var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(Image)).transform;t.SetParent(parent,false);}t.gameObject.layer=parent.gameObject.layer;var i=t.GetComponent<Image>();Paint(i,art,hollow);i.raycastTarget=false;return i;}
    [MenuItem("Godmist/UI/Fit character window surface to frame")]
    public static void FitWindowSurfaceToFrame()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var path=Root+"PF_UIW_CharacterInfoWindow Variant.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try{FitWindowSurface(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    static void FitWindowSurface(GameObject root)
    {
        root.GetComponent<Image>().enabled=false;
        var surface=ImageAt(root.transform,"WindowSurface","surface_solid");
        Stretch(surface.rectTransform,5,5,5,5);surface.raycastTarget=true;
        surface.transform.SetAsFirstSibling();
    }
    static void TextStyle(TMP_Text t,float size=18,TextAlignmentOptions alignment=TextAlignmentOptions.TopLeft)
    {t.font=palette.Font;t.fontSharedMaterial=palette.Font.material;t.fontSize=size;t.color=TextColor;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.alignment=alignment;t.raycastTarget=false;t.margin=Vector4.zero;t.fontStyle=FontStyles.Normal;}
    static TextMeshProUGUI Label(Transform parent,string name,string text,float size=20)
    {var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).transform;t.SetParent(parent,false);}t.gameObject.layer=parent.gameObject.layer;var l=t.GetComponent<TextMeshProUGUI>();TextStyle(l,size);l.text=text;return l;}
    static void AtTop(RectTransform t,float y,float height,float inset=12)
    {t.anchorMin=new Vector2(0,1);t.anchorMax=Vector2.one;t.pivot=new Vector2(.5f,1);t.sizeDelta=new Vector2(-2*inset,height);t.anchoredPosition=new Vector2(0,-y);}
    static Image Bar(Transform parent,string name,float y,float height,Color color)
    {
        var track=ImageAt(parent,name,"slider_track");AtTop(track.rectTransform,y,height);
        var fill=ImageAt(track.transform,"Fill","fill_runtime");Stretch(fill.rectTransform,6,4,6,4);
        fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=.4f;fill.color=color;return fill;
    }
    static void Heading(Transform parent,string name,string text,float y)
    {AtTop(Label(parent,name,text,20).rectTransform,y,26);}
    static void ButtonStyle(Button button,string caption)
    {
        Paint(button.GetComponent<Image>(),"button_normal");button.transition=Selectable.Transition.ColorTint;
        button.colors=new ColorBlock{normalColor=Color.white,highlightedColor=new Color(1.15f,1.15f,1.15f),selectedColor=new Color(1.15f,1.15f,1.15f),pressedColor=new Color(.75f,.75f,.75f),disabledColor=new Color(.55f,.55f,.55f),colorMultiplier=1,fadeDuration=0};
        button.spriteState=default;
        var label=button.GetComponentInChildren<TMP_Text>(true);TextStyle(label,20,TextAlignmentOptions.Center);label.text=caption;Stretch(label.rectTransform,6,2,6,2);
        foreach(var image in button.GetComponentsInChildren<Image>(true))if(image!=button.GetComponent<Image>())image.enabled=false;
    }
    static Image PlainImage(Transform parent,string name,Color color)
    {
        var image=ImageAt(parent,name,"surface_solid");image.sprite=null;image.overrideSprite=null;
        image.type=Image.Type.Simple;image.color=color;return image;
    }
    static void CenterEmptySlotShape(Image image)
    {
        var sprite=image.sprite;
        if(sprite==null)return;
        var path=AssetDatabase.GetAssetPath(sprite);
        if(!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
        // Read the authored alpha without changing its importer/readability or
        // sprite pivot. Only this decorative placeholder's local position changes.
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!source.LoadImage(File.ReadAllBytes(path)))return;
            var rect=sprite.rect;
            if(source.width!=sprite.texture.width||source.height!=sprite.texture.height)
                throw new InvalidOperationException("Empty-slot source and imported texture dimensions differ: "+path);
            var pixels=source.GetPixels32();
            var minX=(int)rect.xMax;var minY=(int)rect.yMax;var maxX=-1;var maxY=-1;
            for(var y=(int)rect.yMin;y<rect.yMax;y++)for(var x=(int)rect.xMin;x<rect.xMax;x++)
            {
                if(pixels[y*source.width+x].a==0)continue;
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
            }
            if(maxX<minX||maxY<minY)return;
            var visibleCenter=new Vector2((minX+maxX+1)*.5f,(minY+maxY+1)*.5f);
            var area=image.rectTransform.rect.size;
            var scale=Mathf.Min(area.x/rect.width,area.y/rect.height);
            image.rectTransform.anchoredPosition=-(visibleCenter-rect.center)*scale;
        }
        finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    static void ItemStyle(Component item)
    {
        Paint(item.GetComponent<Image>(),"item_frame");
        var button=Ref<Button>(item,"button");if(button!=null){button.transition=Selectable.Transition.ColorTint;button.spriteState=default;button.targetGraphic=item.GetComponent<Image>();button.colors=new ColorBlock{normalColor=Color.white,highlightedColor=new Color(.94f,.92f,.82f),selectedColor=new Color(.94f,.92f,.82f),pressedColor=new Color(.75f,.72f,.62f),disabledColor=new Color(.65f,.65f,.65f),colorMultiplier=1,fadeDuration=0};}
        var backing=PlainImage(item.transform,"IconBacking",new Color(.19f,.18f,.20f));
        Stretch(backing.rectTransform,5,5,5,5);backing.transform.SetAsFirstSibling();
        var icon=Ref<Image>(item,"icon");Place(icon.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,64));icon.preserveAspect=true;icon.raycastTarget=false;
        var border=item.transform.Find("Border");if(border!=null)border.gameObject.SetActive(false);
    }
    static void StatHint(Transform parent,string name,float y,string title,string description)
    {
        var hit=PlainImage(parent,name,Color.clear);AtTop(hit.rectTransform,y,22);hit.raycastTarget=true;
        var trigger=hit.GetComponent<CombatSimpleTooltipTrigger>()??hit.gameObject.AddComponent<CombatSimpleTooltipTrigger>();
        Wire(trigger,"tooltip",null);var so=new SerializedObject(trigger);so.FindProperty("title").stringValue=title;so.FindProperty("description").stringValue=description;so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void DetailsDivider(TMP_Text body)
    {
        var divider=ImageAt(body.transform,"MetadataDivider","separator_horizontal");
        divider.color=new Color(.7f,.67f,.57f,.55f);divider.enabled=false;
        var layout=divider.GetComponent<LayoutElement>()??divider.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
        var view=body.GetComponent<ItemDetailsDividerView>()??body.gameObject.AddComponent<ItemDetailsDividerView>();
        Wire(view,"detailsText",body);Wire(view,"divider",divider);
    }
    static void DetailsPanel(CharacterInfoWindowView view,RectTransform right,RectTransform inventoryScroll)
    {
        var panel=ImageAt(right,"PinnedItemDetails","tooltip_frame");Place(panel.rectTransform,new Vector2(.5f,0),new Vector2(0,78),new Vector2(396,312),new Vector2(.5f,0));
        var label=Label(panel.transform,"PinnedLabel","Szczegóły wyposażenia",16);AtTop(label.rectTransform,10,22,14);
        var icon=PlainImage(panel.transform,"ItemIcon",Color.white);Place(icon.rectTransform,new Vector2(0,1),new Vector2(14,-40),new Vector2(52,52),new Vector2(0,1));icon.preserveAspect=true;
        var title=Label(panel.transform,"ItemTitle","Przedmiot",21);AtTop(title.rectTransform,40,54,14);title.rectTransform.offsetMin=new Vector2(78,title.rectTransform.offsetMin.y);title.rectTransform.offsetMax=new Vector2(-46,title.rectTransform.offsetMax.y);title.textWrappingMode=TextWrappingModes.Normal;
        var close=ImageAt(panel.transform,"CloseDetails","button_normal");Place(close.rectTransform,Vector2.one,new Vector2(-10,-8),new Vector2(26,26),Vector2.one);var button=close.GetComponent<Button>()??close.gameObject.AddComponent<Button>();close.raycastTarget=true;Label(close.transform,"Label","×");ButtonStyle(button,"×");
        var scrollNode=PlainImage(panel.transform,"DetailsScroll",Color.clear);Stretch(scrollNode.rectTransform,14,54,14,102);scrollNode.raycastTarget=true;
        var scroll=scrollNode.GetComponent<ScrollRect>()??scrollNode.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=PlainImage(scroll.transform,"Viewport",Color.white);Stretch(viewport.rectTransform,0,0,12,0);var mask=viewport.GetComponent<Mask>()??viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;scroll.viewport=viewport.rectTransform;
        var body=Label(viewport.transform,"DetailsBody","",17);body.textWrappingMode=TextWrappingModes.Normal;body.overflowMode=TextOverflowModes.Overflow;AtTop(body.rectTransform,0,0,0);scroll.content=body.rectTransform;
        DetailsDivider(body);
        var fit=body.GetComponent<ContentSizeFitter>()??body.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var track=ImageAt(scroll.transform,"Scrollbar","scrollbar_track");track.raycastTarget=true;track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=Vector2.one;track.rectTransform.offsetMin=new Vector2(-8,0);track.rectTransform.offsetMax=Vector2.zero;
        var bar=track.GetComponent<Scrollbar>()??track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var handle=ImageAt(track.transform,"Handle","scrollbar_thumb_normal");Stretch(handle.rectTransform);bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        var meta=Label(panel.transform,"DetailsMeta","",15);Place(meta.rectTransform,new Vector2(0,0),new Vector2(14,10),new Vector2(368,36),Vector2.zero);meta.textWrappingMode=TextWrappingModes.Normal;
        Wire(view,"itemDetailsPanel",panel.gameObject);Wire(view,"inventoryScrollRect",inventoryScroll);Wire(view,"itemDetailsTitle",title);Wire(view,"itemDetailsBody",body);Wire(view,"itemDetailsIcon",icon);Wire(view,"itemDetailsMeta",meta);Wire(view,"closeItemDetailsButton",button);Wire(view,"itemDetailsScroll",scroll);panel.gameObject.SetActive(false);
    }
    static void ConfigureTooltip()
    {
        var path=Root+"PF_UIW_Tooltip.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)){style.enabled=false;Wire(style,"palette",null);}
            var view=root.GetComponent<UIWorkspaceTooltipView>();var icon=Ref<Image>(view,"icon");icon.preserveAspect=true;
            var layout=icon.GetComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=64;layout.minHeight=layout.preferredHeight=64;
            var header=icon.transform.parent.GetComponent<HorizontalLayoutGroup>();header.spacing=12;
            var title=Ref<TMP_Text>(view,"titleText");TextStyle(title,28,TextAlignmentOptions.MidlineLeft);title.textWrappingMode=TextWrappingModes.Normal;title.overflowMode=TextOverflowModes.Overflow;
            var content=header.transform.parent.GetComponent<VerticalLayoutGroup>();content.spacing=8;
            var body=Ref<TMP_Text>(view,"bodyText");TextStyle(body,18);body.textWrappingMode=TextWrappingModes.Normal;body.overflowMode=TextOverflowModes.Overflow;
            DetailsDivider(body);
            var meta=Ref<TMP_Text>(view,"metaText");TextStyle(meta,16);meta.textWrappingMode=TextWrappingModes.Normal;meta.overflowMode=TextOverflowModes.Overflow;meta.color=new Color(.72f,.69f,.61f);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Godmist/UI/Restyle character card")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        const string ghostPath="Assets/_Project/UI/EmptySlotSilhouette.mat";
        ghostMaterial=AssetDatabase.LoadAssetAtPath<Material>(ghostPath);
        if(ghostMaterial==null){ghostMaterial=new Material(Shader.Find("Godmist/UI/Empty Slot Silhouette"));AssetDatabase.CreateAsset(ghostMaterial,ghostPath);}
        ghostMaterial.shader=Shader.Find("Godmist/UI/Empty Slot Silhouette");EditorUtility.SetDirty(ghostMaterial);
        const string godlyPath="Assets/_Project/UI/DiamondRarity.mat";
        godlyMaterial=AssetDatabase.LoadAssetAtPath<Material>(godlyPath);
        var godlyShader=Shader.Find("Godmist/UI/Diamond Rarity")??throw new Exception("Missing diamond rarity shader");
        if(godlyMaterial==null){godlyMaterial=new Material(godlyShader);AssetDatabase.CreateAsset(godlyMaterial,godlyPath);}
        godlyMaterial.shader=godlyShader;godlyMaterial.SetFloat("_ShinePhase",-1);godlyMaterial.SetFloat("_Frame",1);godlyMaterial.SetFloat("_Diamond",1);godlyMaterial.DisableKeyword("_FRAME_ON");godlyMaterial.DisableKeyword("_DIAMOND_ON");EditorUtility.SetDirty(godlyMaterial);
        const string rarityPath="Assets/_Project/UI/RarityOutline.mat";
        rarityMaterial=AssetDatabase.LoadAssetAtPath<Material>(rarityPath);
        if(rarityMaterial==null){rarityMaterial=new Material(godlyShader);AssetDatabase.CreateAsset(rarityMaterial,rarityPath);}
        rarityMaterial.shader=godlyShader;rarityMaterial.SetFloat("_Frame",1);rarityMaterial.SetFloat("_Diamond",0);rarityMaterial.DisableKeyword("_FRAME_ON");rarityMaterial.DisableKeyword("_DIAMOND_ON");EditorUtility.SetDirty(rarityMaterial);
        var path=Root+"PF_UIW_CharacterInfoWindow Variant.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            // These are variant-specific overrides. Shared chrome remains unchanged.
            foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)){style.enabled=false;Wire(style,"palette",null);}
            var view=root.GetComponent<CharacterInfoWindowView>();var window=root.GetComponent<UIWorkspaceWindow>();
            var ws=new SerializedObject(window);ws.FindProperty("minimumSize").vector2Value=new Vector2(1120,800);ws.FindProperty("maximumSize").vector2Value=new Vector2(1400,900);ws.ApplyModifiedPropertiesWithoutUndo();
            Place((RectTransform)root.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1180,800));
            FitWindowSurface(root);Paint(root.transform.Find("Border").GetComponent<Image>(),"tooltip_frame",true);
            var header=(RectTransform)root.transform.Find("Header");AtTop(header,0,52,0);Paint(header.GetComponent<Image>(),"button_normal");
            var title=Ref<TextMeshProUGUI>(view,"headerTitleText");title.enabled=true;title.gameObject.SetActive(true);TextStyle(title,28,TextAlignmentOptions.MidlineLeft);Stretch(title.rectTransform,20,0,62,0);title.text="Postać";
            var close=header.Find("CloseButton").GetComponent<Button>();ButtonStyle(close,"×");Place((RectTransform)close.transform,new Vector2(1,.5f),new Vector2(-10,0),new Vector2(36,36),new Vector2(1,.5f));
            var content=(RectTransform)root.transform.Find("Content");Stretch(content,18,20,18,66);
            var left=(RectTransform)content.Find("LeftColumn");left.anchorMin=new Vector2(0,0);left.anchorMax=new Vector2(0,1);left.pivot=new Vector2(0,.5f);left.sizeDelta=new Vector2(280,0);left.anchoredPosition=Vector2.zero;left.GetComponent<Image>().enabled=false;
            var center=(RectTransform)content.Find("CharacterCenter");center.anchorMin=Vector2.zero;center.anchorMax=Vector2.one;center.offsetMin=new Vector2(300,0);center.offsetMax=new Vector2(-440,0);center.GetComponent<Image>().enabled=false;
            var right=(RectTransform)content.Find("InventoryColumn");right.anchorMin=new Vector2(1,0);right.anchorMax=Vector2.one;right.pivot=new Vector2(1,.5f);right.sizeDelta=new Vector2(420,0);right.anchoredPosition=Vector2.zero;right.GetComponent<Image>().enabled=false;
            foreach(var f in new[]{"identityText","progressionText","healthResourceText","statsText","resistancesText","honorAvailabilityText","inventoryFooterText"})TextStyle(Ref<TextMeshProUGUI>(view,f));
            AtTop(Ref<TextMeshProUGUI>(view,"identityText").rectTransform,4,28);
            AtTop(Ref<TextMeshProUGUI>(view,"progressionText").rectTransform,38,48);
            Wire(view,"experienceFill",Bar(left,"ExperienceBar",90,16,new Color(.58f,.76f,.80f)));
            var vitals=Ref<TextMeshProUGUI>(view,"healthResourceText");AtTop(vitals.rectTransform,117,102);vitals.lineSpacing=0;
            Wire(view,"healthFill",Bar(left,"HealthBar",141,16,new Color(.70f,.14f,.12f)));
            Wire(view,"resourceFill",Bar(left,"ResourceBar",185,16,new Color(.25f,.78f,.55f)));
            Heading(left,"CombatHeading","Walka",219);
            AtTop(Ref<TextMeshProUGUI>(view,"statsText").rectTransform,252,176);
            Heading(left,"ResistanceHeading","Odporności",443);AtTop(Ref<TextMeshProUGUI>(view,"resistancesText").rectTransform,475,132);
            Heading(left,"StateHeading","Stan postaci",616);AtTop(Ref<TextMeshProUGUI>(view,"honorAvailabilityText").rectTransform,648,66);
            StatHint(left,"DodgeHint",318,"Unik", "Punkty uniku, nie procent. Obniżają szansę trafienia przeciwnika; wynik zależy też od jego celności broni i umiejętności.");
            StatHint(left,"AccuracyHint",406,"Celność", "Punkty celności broni, nie gotowa szansa trafienia. W walce uwzględniana jest również celność umiejętności oraz unik celu.");
            StatHint(left,"ControlHint",563,"Odporność na kontrolę", "Odporność na ogłuszenie, zamrożenie, sen, paraliż, unieruchomienie, wychłodzenie i prowokację.");
            StatHint(left,"DisplacementHint",585,"Odporność na przemieszczenie", "Odporność na wymuszone przesunięcie postaci przez umiejętności i efekty przeciwnika.");
            Heading(center,"EquipmentHeading","Wyposażenie",4);
            var portrait=Ref<Image>(view,"portrait");Place(portrait.rectTransform,new Vector2(.5f,1),new Vector2(0,-280),new Vector2(224,336));portrait.raycastTarget=false;
            var slots=view.GetComponentsInChildren<CharacterInfoEquipmentSlotView>(true);
            foreach(var slot in slots)
            {
                var index=(int)slot.Slot;var x=index%2==0?-126:126;var y=-145-(index/2)*130;
                Place((RectTransform)slot.transform,new Vector2(.5f,1),new Vector2(x,y),new Vector2(72,72));ItemStyle(slot);
                var ghost=PlainImage(slot.transform,"EmptySlotIcon",new Color(.65f,.63f,.59f,.55f));Place(ghost.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,64));ghost.preserveAspect=true;ghost.material=ghostMaterial;
                var iconNames=new[]{"long-sword-32-v1","","necklace-neutral-32","ring-neutral-32","bracelet-neutral-32","earrings-neutral-32"};
                ghost.sprite=index!=1?AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/Items/VS22/"+iconNames[index]+".png"):
                    AssetDatabase.FindAssets("t:EquipmentItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>).Where(d=>d.EquipmentSlot==slot.Slot && d.Icon!=null).Select(d=>d.Icon).First();
                CenterEmptySlotShape(ghost);
                Wire(slot,"emptySlotIcon",ghost);
                var count=slot.transform.Find("CountLabel");if(count!=null)count.gameObject.SetActive(false);
                var border=Ref<Image>(slot,"feedbackBorder");Paint(border,"slot_valid",true);Stretch(border.rectTransform);border.enabled=false;border.raycastTarget=false;
                var selection=ImageAt(slot.transform,"SelectionBorder","slot_selected",true);Stretch(selection.rectTransform);selection.enabled=false;Wire(slot,"selectionBorder",selection);
                var name=index switch{0=>"Broń",1=>"Pancerz",2=>"Naszyjnik",3=>"Pierścień",4=>"Bransoleta",_=>"Kolczyki"};
                var label=Label(slot.transform,"SlotName",name,16);label.alignment=TextAlignmentOptions.Center;Place(label.rectTransform,new Vector2(.5f,0),new Vector2(0,-18),new Vector2(112,26));
            }
            var buttons=(RectTransform)center.Find("ButtonRoot");AtTop(buttons,492,42,4);
            var fitter=buttons.GetComponent<ContentSizeFitter>();fitter.horizontalFit=fitter.verticalFit=ContentSizeFitter.FitMode.Unconstrained;
            var layout=buttons.GetComponent<HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;
            ButtonStyle(Ref<Button>(view,"skillLoadoutButton"),"Umiejętności");ButtonStyle(Ref<Button>(view,"skillTreeButton"),"Rozwój");
            foreach(var button in new[]{Ref<Button>(view,"skillLoadoutButton"),Ref<Button>(view,"skillTreeButton")})
            {var element=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();element.minWidth=0;element.preferredWidth=150;element.flexibleWidth=1;element.minHeight=element.preferredHeight=42;}
            var help=Label(center,"EquipmentHint","LPM: szczegóły • PPM: opcje\nPrzeciągnij do plecaka, aby zdjąć",15);help.alignment=TextAlignmentOptions.Center;help.textWrappingMode=TextWrappingModes.Normal;AtTop(help.rectTransform,550,42,0);
            Heading(right,"InventoryHeading","Plecak",4);
            var search=Ref<TMP_InputField>(view,"inventorySearchInput");AtTop((RectTransform)search.transform,40,42);Paint(search.GetComponent<Image>(),"button_normal");search.customCaretColor=true;search.caretColor=TextColor;search.selectionColor=new Color(.5f,.45f,.25f,.6f);
            var area=(RectTransform)search.transform.Find("Text Area");Stretch(area,12,5,48,5);
            TextStyle(search.textComponent,18,TextAlignmentOptions.MidlineLeft);var placeholder=(TMP_Text)search.placeholder;TextStyle(placeholder,18,TextAlignmentOptions.MidlineLeft);placeholder.text="Szukaj w plecaku…";placeholder.color=new Color(.64f,.61f,.54f);
            var clearNode=ImageAt(search.transform,"ClearSearch","button_normal").gameObject;
            var clear=clearNode.GetComponent<Button>()??clearNode.AddComponent<Button>();clear.GetComponent<Image>().raycastTarget=true;Place((RectTransform)clear.transform,new Vector2(1,.5f),new Vector2(-5,0),new Vector2(32,32),new Vector2(1,.5f));Label(clear.transform,"Label","×");ButtonStyle(clear,"×");Wire(view,"clearSearchButton",clear);clear.gameObject.SetActive(false);
            var scroll=right.Find("InventoryScroll").GetComponent<ScrollRect>();Stretch((RectTransform)scroll.transform,12,86,12,100);scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var scrollSurface=scroll.GetComponent<Image>();scrollSurface.sprite=null;scrollSurface.overrideSprite=null;scrollSurface.type=Image.Type.Simple;scrollSurface.color=new Color(.115f,.108f,.124f,1);
            var viewport=(RectTransform)scroll.transform.Find("Viewport");Stretch(viewport,0,0,18,0);scroll.viewport=viewport;viewport.GetComponent<Image>().color=Color.white;viewport.GetComponent<Mask>().showMaskGraphic=false;
            var grid=Ref<Transform>(view,"inventoryRoot").GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(72,72);grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(8,8,8,8);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;grid.childAlignment=TextAnchor.UpperLeft;
            var gridRect=(RectTransform)grid.transform;gridRect.anchorMin=new Vector2(0,1);gridRect.anchorMax=Vector2.one;gridRect.pivot=new Vector2(.5f,1);gridRect.sizeDelta=new Vector2(0,0);gridRect.anchoredPosition=Vector2.zero;scroll.content=gridRect;
            var gridFit=grid.GetComponent<ContentSizeFitter>();gridFit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;gridFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var template=Ref<CharacterInfoItemView>(view,"inventoryItemTemplate");ItemStyle(template);
            var rarity=PlainImage(template.transform,"RarityAccent",new Color(.54f,.52f,.47f));Stretch(rarity.rectTransform,4,4,4,4);rarity.material=rarityMaterial;rarity.transform.SetAsLastSibling();Wire(template,"rarityAccent",rarity);Wire(template,"rarityAccentMaterial",rarityMaterial);
            rarity.color=ItemRarityPresentation.Color(RarityTier.Common);Wire(template,"godlyAccentMaterial",godlyMaterial);
            DetailsPanel(view,right,(RectTransform)scroll.transform);
            var empty=Label(scroll.transform,"EmptyInventory","Plecak jest pusty",20);empty.alignment=TextAlignmentOptions.Center;Stretch(empty.rectTransform,20,0,20,0);Wire(view,"inventoryEmptyText",empty);
            var bar=scroll.verticalScrollbar;Place((RectTransform)bar.transform,new Vector2(1,.5f),Vector2.zero,new Vector2(8,0),new Vector2(1,.5f));var br=(RectTransform)bar.transform;br.anchorMin=new Vector2(1,0);br.anchorMax=Vector2.one;br.offsetMin=new Vector2(-8,0);br.offsetMax=Vector2.zero;
            var barTrack=bar.GetComponent<Image>();barTrack.sprite=null;barTrack.overrideSprite=null;barTrack.type=Image.Type.Simple;barTrack.color=new Color(.25f,.27f,.25f);
            Stretch((RectTransform)bar.transform.Find("Sliding Area"));Stretch(bar.handleRect);var barHandle=bar.handleRect.GetComponent<Image>();barHandle.sprite=null;barHandle.overrideSprite=null;barHandle.type=Image.Type.Simple;barHandle.color=new Color(.52f,.58f,.49f);
            bar.transition=Selectable.Transition.ColorTint;bar.spriteState=default;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var footer=Ref<TextMeshProUGUI>(view,"inventoryFooterText");Place(footer.rectTransform,new Vector2(0,0),new Vector2(12,8),new Vector2(396,58),new Vector2(0,0));
            Wire(view,"equipmentMenuPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PF_UIW_PartyContextMenu.prefab").GetComponent<PartyRailMenuView>());
            foreach(var col in new[]{left,center}){var line=ImageAt(content,col.name+"Separator","separator_vertical");Place(line.rectTransform,new Vector2(col==left?0:1,.5f),new Vector2(col==left?290:-430,0),new Vector2(2,660));line.rectTransform.localRotation=Quaternion.identity;}
            // Keep authored card elements visible after native validation/import.
            title.transform.SetAsLastSibling();close.transform.SetAsLastSibling();root.transform.Find("Border").SetAsLastSibling();
            ResourcePanelIconSetup.ConfigureCharacter(root);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        ConfigureTooltip();
        AssetDatabase.SaveAssets();
    }
}
