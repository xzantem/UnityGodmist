using System;
using System.Linq;
using Godmist.Views.NPC;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Creates the mission board from the shared UI Workspace prefabs.</summary>
    public static class VS17CatacombsMissionUiSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/City/Arungard.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CatacombsMissions.prefab";
        private const string WindowPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Window.prefab";
        private const string SectionPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Section.prefab";
        private const string ScrollPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ScrollView.prefab";
        private const string RowPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ListRow.prefab";
        private const string ButtonPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Button.prefab";

        [MenuItem("Godmist/Content/VS-17/Configure Catacombs Mission UI")]
        public static void Configure()
        {
            var prefab = BuildPrefab();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
                throw new InvalidOperationException("VS-17: scena Arungard ma niezapisane zmiany.");

            try
            {
                var dialogue = FindInScene<NpcDialogueController>(scene);
                if (dialogue == null)
                    throw new InvalidOperationException("VS-17: brak kontrolera dialogu w Arungard.");
                var so = new SerializedObject(dialogue);
                var source = so.FindProperty("servicesButton").objectReferenceValue as Button;
                if (source == null)
                    throw new InvalidOperationException("VS-17: brak przycisku usług.");
                var missionButton = so.FindProperty("missionBoardButton").objectReferenceValue as Button;
                if (missionButton == null)
                {
                    var buttonObject = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                    buttonObject.name = "MissionBoardButton";
                    missionButton = buttonObject.GetComponent<Button>();
                    missionButton.onClick.RemoveAllListeners();
                    var rect = buttonObject.GetComponent<RectTransform>();
                    rect.anchoredPosition = source.GetComponent<RectTransform>().anchoredPosition
                        + new Vector2(-380f, 0f);
                    foreach (var label in buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true))
                        label.text = "Zlecenia";
                }
                so.FindProperty("missionBoardButton").objectReferenceValue = missionButton;
                so.FindProperty("missionBoardPrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dialogue);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("VS-17: zapisano okno UIW zleceń i przycisk u strażnika.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Godmist/Content/VS-17/Rebuild Mission Window UIW")]
        public static void RepairLayout()
        {
            BuildPrefab();
            Debug.Log("VS-17: przebudowano okno zleceń z prefabów UIW.");
        }

    const string Folder="Assets/_Project/Prefabs/UI/Workspace/";
    static UIWorkspaceStylePalette palette;
    static readonly Color Cream=new Color32(232,222,191,255);
    static void Wire(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static RectTransform Rect(Transform t,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
    {var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return r;}
    static RectTransform Node(Transform parent,string name,params Type[] components)
    {var types=new[]{typeof(RectTransform)}.Concat(components).ToArray();var go=new GameObject(name,types);go.layer=5;go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
    static void Height(GameObject go,float h)
    {var le=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=h;le.flexibleHeight=0;}
    static TextMeshProUGUI Text(Transform parent,string name,int size,string value="")
    {var node=Node(parent,name,typeof(TextMeshProUGUI));var t=node.GetComponent<TextMeshProUGUI>();t.font=palette.Font;t.fontSize=size;t.color=Cream;t.text=value;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;}
    static void StyleText(TextMeshProUGUI text,int size)
    {text.font=palette.Font;text.enableAutoSizing=false;text.fontSize=size;text.color=Cream;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;}
    static void Paint(Image image,string name)
    {image.sprite=palette.GetArtwork(name);image.type=Image.Type.Sliced;image.color=Color.white;}
    static void ButtonArt(Button button,string prefix)
    {
        var image=button.GetComponent<Image>();Paint(image,prefix+"_normal");button.targetGraphic=image;
        button.transition=Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=palette.GetArtwork(prefix+"_hover"),pressedSprite=palette.GetArtwork(prefix+"_pressed"),selectedSprite=palette.GetArtwork(prefix+"_selected"),disabledSprite=palette.GetArtwork(prefix+"_disabled")};
    }
    static void VGroup(RectTransform node,int spacing)
    {var g=node.gameObject.AddComponent<VerticalLayoutGroup>();g.spacing=spacing;g.childControlWidth=g.childControlHeight=true;g.childForceExpandWidth=true;g.childForceExpandHeight=false;}
    static void Divider(Transform parent,string name,bool vertical=false)
    {var n=Node(parent,name,typeof(Image));var img=n.GetComponent<Image>();img.color=new Color(.6f,.59f,.47f,.4f);img.raycastTarget=false;if(!vertical)Height(n.gameObject,1);}
    static GameObject Instance(string name,Transform parent)
    {return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"),parent);}
    static ScrollRect Scroll(Transform parent,string name)
    {
        var go=Instance("PF_UIW_ScrollView",parent);go.name=name;
        foreach(var style in go.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))style.enabled=false;
        foreach(var img in go.GetComponents<Image>())img.enabled=false;
        var border=go.transform.Find("Border");if(border!=null)border.gameObject.SetActive(false);
        var sr=go.GetComponent<ScrollRect>();sr.horizontal=false;sr.vertical=true;sr.movementType=ScrollRect.MovementType.Clamped;sr.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        Rect(sr.content, new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);sr.content.pivot=new Vector2(.5f,1);
        return sr;
    }
    static void Metric(Transform parent,string name,string label,string sprite,out TextMeshProUGUI value)
    {
        var box=Node(parent,name,typeof(LayoutElement),typeof(Godmist.Views.UI.CombatSimpleTooltipTrigger));
        var layout=box.GetComponent<LayoutElement>();layout.minWidth=120;layout.preferredWidth=140;layout.flexibleWidth=0;
        box.GetComponent<Godmist.Views.UI.CombatSimpleTooltipTrigger>().Initialize(null,label,string.Empty);
        var icon=Node(box,"Icon",typeof(Image));Rect(icon,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,-16),new Vector2(32,16));
        var img=icon.GetComponent<Image>();img.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(sprite);img.preserveAspect=true;img.color=Color.white;img.raycastTarget=true;
        value=Text(box,"Amount",23);Rect(value.transform,Vector2.zero,Vector2.one,new Vector2(40,0),Vector2.zero);
        value.alignment=TextAlignmentOptions.MidlineLeft;value.raycastTarget=true;
    }
    private static CatacombsMissionBoardView BuildPrefab()
    {
        const string path=Folder+"PF_UIW_CatacombsMissions.prefab";
        try
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                palette=root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                foreach(var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))style.enabled=false;
                var rootRect=(RectTransform)root.transform;rootRect.sizeDelta=new Vector2(1080,680);
                var board=root.GetComponent<CatacombsMissionBoardView>();var window=root.GetComponent<UIWorkspaceWindow>();
                var ws=new SerializedObject(window);ws.FindProperty("minimumSize").vector2Value=new Vector2(850,570);ws.FindProperty("maximumSize").vector2Value=new Vector2(1400,900);ws.ApplyModifiedPropertiesWithoutUndo();
                var header=root.transform.Find("Header");var head=header.Find("Title").GetComponent<TextMeshProUGUI>();StyleText(head,28);head.text="Zlecenia — Katakumby";
                Rect(head.transform,Vector2.zero,Vector2.one,new Vector2(22,6),new Vector2(-66,-6));
                var close=header.GetComponentInChildren<Button>(true);ButtonArt(close,"icon_button");
                var closeRect=(RectTransform)close.transform;closeRect.anchorMin=closeRect.anchorMax=Vector2.one;closeRect.pivot=new Vector2(1,1);closeRect.sizeDelta=new Vector2(44,44);closeRect.anchoredPosition=new Vector2(-7,-6);
                foreach(var text in close.GetComponentsInChildren<TextMeshProUGUI>(true))StyleText(text,24);
                var content=root.transform.Find("Content");
                var left=content.Find("MissionListSection");var right=content.Find("MissionDetailsSection");
                Rect(left,Vector2.zero,new Vector2(.30f,1),new Vector2(12,14),new Vector2(-14,-10));
                Rect(right,new Vector2(.32f,0),Vector2.one,new Vector2(12,14),new Vector2(-16,-10));
                foreach(var section in new[]{left,right})
                {
                    foreach(var img in section.GetComponents<Image>())img.enabled=false;
                    var border=section.Find("Border");if(border!=null)border.gameObject.SetActive(false);
                    section.Find("SectionTitle").gameObject.SetActive(false);
                    Rect(section.Find("Content"),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                }
                var oldSeparator=content.Find("MissionSeparator");if(oldSeparator!=null)UnityEngine.Object.DestroyImmediate(oldSeparator.gameObject);
                var separator=Node(content,"MissionSeparator",typeof(Image));separator.GetComponent<Image>().color=new Color(.6f,.59f,.47f,.4f);separator.GetComponent<Image>().raycastTarget=false;
                Rect(separator,new Vector2(.31f,0),new Vector2(.31f,1),new Vector2(0,16),new Vector2(1,-12));
                var listScroll=left.Find("Content/MissionListScroll").GetComponent<ScrollRect>();
                foreach(var img in listScroll.GetComponents<Image>())img.enabled=false;
                var listBorder=listScroll.transform.Find("Border");if(listBorder!=null)listBorder.gameObject.SetActive(false);
                Rect(listScroll.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);listScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                var row=listScroll.content.Find("MissionRowTemplate").GetComponent<Button>();Height(row.gameObject,96);ButtonArt(row,"tab");
                Rect(row.transform.Find("TextRoot"),Vector2.zero,Vector2.one,new Vector2(14,8),new Vector2(-14,-8));
                var rowTitle=row.transform.Find("TextRoot/Title").GetComponent<TextMeshProUGUI>();var rowDetail=row.transform.Find("TextRoot/Detail").GetComponent<TextMeshProUGUI>();
                StyleText(rowTitle,21);StyleText(rowDetail,17);Rect(rowTitle.transform,new Vector2(0,.35f),Vector2.one,Vector2.zero,Vector2.zero);Rect(rowDetail.transform,Vector2.zero,new Vector2(1,.35f),Vector2.zero,Vector2.zero);
                var detail=right.Find("Content");for(var i=detail.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(detail.GetChild(i).gameObject);
                var scroll=Scroll(detail,"MissionDetailScroll");Rect(scroll.transform,Vector2.zero,Vector2.one,new Vector2(0,94),Vector2.zero);
                var body=scroll.content;VGroup(body,14);var fitter=body.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var title=Text(body,"MissionTitle",30);var meta=Text(body,"MissionMetadata",19);Divider(body,"TitleDivider");
                var objective=Text(body,"MissionObjective",23);var description=Text(body,"MissionDescription",18);
                var progress=Node(body,"MissionProgress");Height(progress.gameObject,70);
                var progressLabel=Text(progress,"ProgressText",18);Rect(progressLabel.transform,new Vector2(0,.35f),Vector2.one,Vector2.zero,Vector2.zero);
                var track=Node(progress,"Track",typeof(Image));Rect(track,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,12));track.GetComponent<Image>().color=new Color(.14f,.14f,.16f,1);
                var fill=Node(track,"Fill",typeof(Image));Rect(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var fillImage=fill.GetComponent<Image>();fillImage.sprite=palette.GetArtwork("fill_runtime");fillImage.type=Image.Type.Filled;fillImage.fillMethod=Image.FillMethod.Horizontal;fillImage.fillOrigin=0;fillImage.raycastTarget=false;
                var rewards=Node(body,"MissionRewards");VGroup(rewards,12);var rewardHeading=Text(rewards,"RewardHeading",23,"Nagrody");
                var metrics=Node(rewards,"RewardAmounts",typeof(HorizontalLayoutGroup));Height(metrics.gameObject,40);var metricGroup=metrics.GetComponent<HorizontalLayoutGroup>();metricGroup.spacing=24;metricGroup.childControlWidth=metricGroup.childControlHeight=true;metricGroup.childForceExpandWidth=false;metricGroup.childForceExpandHeight=true;
                Metric(metrics,"Experience","Doświadczenie","Assets/_Project/Art/Icons/ResourceRewards/experience-32.png",out var xp);
                Metric(metrics,"Gold","Złoto","Assets/_Project/Art/Icons/ResourceRewards/gold-32.png",out var gold);
                Metric(metrics,"Honor","Honor","Assets/_Project/Art/Icons/ResourceRewards/honor-32.png",out var honor);
                var item=Node(rewards,"RewardItem",typeof(MissionRewardItemView));Height(item.gameObject,80);
                var slot=Node(item,"ItemSlot",typeof(Image));Rect(slot,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(76,76));Paint(slot.GetComponent<Image>(),"item_frame");
                var icon=Node(slot,"Icon",typeof(Image));Rect(icon,Vector2.zero,Vector2.one,new Vector2(8,8),new Vector2(-8,-8));icon.GetComponent<Image>().preserveAspect=true;
                var accent=Node(slot,"RarityAccent",typeof(Image));Rect(accent,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4));accent.GetComponent<Image>().raycastTarget=false;
                var itemName=Text(item,"ItemName",21);Rect(itemName.transform,Vector2.zero,Vector2.one,new Vector2(92,16),Vector2.zero);
                var itemView=item.GetComponent<MissionRewardItemView>();Wire(itemView,"icon",icon.GetComponent<Image>());Wire(itemView,"rarityAccent",accent.GetComponent<Image>());Wire(itemView,"nameText",itemName);Wire(itemView,"rarityMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/UI/RarityOutline.mat"));Wire(itemView,"godlyMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/UI/DiamondRarity.mat"));
                var message=Text(detail,"MissionMessage",18);Rect(message.transform,Vector2.zero,new Vector2(1,0),new Vector2(0,60),new Vector2(0,86));
                var actionObject=Instance("PF_UIW_Button",detail);actionObject.name="MissionActionButton";foreach(var style in actionObject.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))style.enabled=false;
                var action=actionObject.GetComponent<Button>();ButtonArt(action,"button");var actionRect=(RectTransform)action.transform;actionRect.anchorMin=actionRect.anchorMax=new Vector2(1,0);actionRect.pivot=new Vector2(1,0);actionRect.sizeDelta=new Vector2(320,50);actionRect.anchoredPosition=Vector2.zero;
                var actionLabel=actionObject.transform.Find("Label").GetComponent<TextMeshProUGUI>();StyleText(actionLabel,23);actionLabel.alignment=TextAlignmentOptions.Center;actionLabel.text="Przyjmij zlecenie";
                var borderAction=actionObject.transform.Find("Border");if(borderAction!=null)borderAction.gameObject.SetActive(false);
                Wire(board,"windowTitleText",head);Wire(board,"titleText",title);Wire(board,"metadataText",meta);Wire(board,"objectiveText",objective);Wire(board,"detailsText",description);Wire(board,"progressRoot",progress.gameObject);Wire(board,"progressText",progressLabel);Wire(board,"progressFill",fillImage);Wire(board,"rewardsRoot",rewards.gameObject);Wire(board,"experienceText",xp);Wire(board,"goldText",gold);Wire(board,"honorText",honor);Wire(board,"rewardItem",itemView);Wire(board,"messageText",message);Wire(board,"actionButton",action);Wire(board,"actionButtonText",actionLabel);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CatacombsMissionBoardView>();
        }
        catch{throw;}
    }
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
