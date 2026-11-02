using System;
using Godmist.Core.Utilities;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Editor
{
 public static partial class PartyViewSetup
 {
  static UIWorkspaceStylePalette palette;
  static T Ref<T>(Object target,string name) where T:Object =>new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
  static void Wire(Object target,string name,Object value){var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
  static void Nodes(Object target,string field,Object[] values){var s=new SerializedObject(target);var p=s.FindProperty(field);p.arraySize=values.Length;for(var i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
  static RectTransform Node(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
  static void Stretch(RectTransform r,float l=0,float b=0,float right=0,float top=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-right,-top);}
  static void Top(RectTransform r,float y,float h,float l=0,float right=0){r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-l-right,h);r.anchoredPosition=new Vector2((l-right)*.5f,-y);}
  static void Bottom(RectTransform r,float y,float h){r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);r.sizeDelta=new Vector2(0,h);r.anchoredPosition=new Vector2(0,y);}
  static Image Art(Transform parent,string name,string art,bool hollow=false){var r=Node(parent,name);var i=r.gameObject.AddComponent<Image>();i.sprite=palette.GetArtwork(art)??throw new Exception("Missing art "+art);i.type=Image.Type.Sliced;i.fillCenter=!hollow;i.raycastTarget=false;return i;}
  static TMP_Text Text(Transform parent,string name,string value,int size=18){var l=Node(parent,name).gameObject.AddComponent<TextMeshProUGUI>();l.font=palette.Font;l.fontSharedMaterial=palette.Font.material;l.fontSize=size;l.text=value;l.color=new Color(.91f,.875f,.78f);l.raycastTarget=false;l.textWrappingMode=TextWrappingModes.Normal;return l;}
  static Button Button(Transform parent,string name,string caption){var i=Art(parent,name,"button_normal");i.raycastTarget=true;var b=i.gameObject.AddComponent<Button>();b.transition=Selectable.Transition.ColorTint;var l=Text(i.transform,"Label",caption,16);l.alignment=TextAlignmentOptions.Center;l.fontSize=14;Stretch(l.rectTransform,2,2,2,2);return b;}
  static void NoLayout(Transform t){foreach(var c in t.GetComponents<LayoutGroup>())Object.DestroyImmediate(c);foreach(var c in t.GetComponents<ContentSizeFitter>())Object.DestroyImmediate(c);foreach(var c in t.GetComponents<LayoutElement>())Object.DestroyImmediate(c);}
  static ScrollRect Scroll(Transform parent,string name,bool horizontal=false)
  {
   var r=Node(parent,name);var s=r.gameObject.AddComponent<ScrollRect>();var vp=Node(r,"Viewport");Stretch(vp);vp.gameObject.AddComponent<RectMask2D>();
   var hit=vp.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
   var content=Node(vp,"Content");content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;
   s.viewport=vp;s.content=content;s.horizontal=horizontal;s.vertical=!horizontal;s.inertia=false;s.movementType=ScrollRect.MovementType.Clamped;s.scrollSensitivity=32;return s;
  }
  static GameObject Clone(string file,Transform parent)
  {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+file)??throw new Exception("Missing "+file);
   var go=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.transform.SetParent(parent,false);return go;
  }
  static void Embedded(GameObject go)
  {
   foreach(var window in go.GetComponentsInChildren<UIWorkspaceWindow>(true))Object.DestroyImmediate(window);
   foreach(var c in go.GetComponentsInChildren<ControllerNavigationScope>(true))Object.DestroyImmediate(c);
   foreach(var c in go.GetComponentsInChildren<UIWorkspaceWindowDragHandle>(true))Object.DestroyImmediate(c);
   foreach(var c in go.GetComponentsInChildren<UIWorkspaceWindowResizeHandle>(true))Object.DestroyImmediate(c);
   foreach(var n in new[]{"Header","Border","WindowSurface","ResizeHandle"}){var t=go.transform.Find(n);if(t!=null)t.gameObject.SetActive(false);}
   var image=go.GetComponent<Image>();if(image!=null)image.enabled=false;
   Stretch((RectTransform)go.transform);
   var content=go.transform.Find("Content") as RectTransform;if(content!=null)Stretch(content,6,6,6,6);
  }
  static void Equipment(GameObject go)
  {
   var view=go.GetComponent<CharacterInfoWindowView>();var content=go.transform.Find("Content");
   var left=content.Find("LeftColumn");left.gameObject.SetActive(false);
   foreach(var n in new[]{"LeftColumnSeparator","CharacterCenterSeparator"}){var s=content.Find(n);if(s!=null)s.gameObject.SetActive(false);}
   var center=(RectTransform)content.Find("CharacterCenter");NoLayout(center);Top(center,0,260);
   foreach(var n in new[]{"ButtonRoot","EquipmentHint","EquipmentHeading","Gold","GoldValue","Honor","HonorValue"}){var t=center.Find(n);if(t!=null)t.gameObject.SetActive(false);}
   var portrait=Ref<Image>(view,"portrait");Top(portrait.rectTransform,24,184,98,98);
   var slots=new SerializedObject(view).FindProperty("equipmentSlots");
   for(var i=0;i<slots.arraySize;i++)
   {
    var slot=(CharacterInfoEquipmentSlotView)slots.GetArrayElementAtIndex(i).objectReferenceValue;var rect=(RectTransform)slot.transform;
    rect.anchorMin=rect.anchorMax=new Vector2(i%2,1);rect.pivot=new Vector2(i%2,1);rect.sizeDelta=new Vector2(58,58);rect.anchoredPosition=new Vector2(i%2==0?12:-12,-(12+i/2*82));
    var icon=Ref<Image>(slot,"icon");icon.rectTransform.sizeDelta=new Vector2(48,48);
    var label=slot.transform.Find("SlotName")?.GetComponent<TMP_Text>();if(label!=null){label.fontSize=12;Top(label.rectTransform,59,18,-10,-10);}
   }
   var right=(RectTransform)content.Find("InventoryColumn");Stretch(right,0,0,0,272);
   var grid=Ref<Transform>(view,"inventoryRoot").GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(58,58);grid.spacing=new Vector2(10,10);grid.constraintCount=4;
   var template=Ref<CharacterInfoItemView>(view,"inventoryItemTemplate");Ref<Image>(template,"icon").rectTransform.sizeDelta=new Vector2(48,48);
   Top((RectTransform)Ref<TMP_InputField>(view,"inventorySearchInput").transform,28,34,6,6);
   var scroll=right.Find("InventoryScroll").GetComponent<ScrollRect>();Stretch((RectTransform)scroll.transform,4,54,4,70);
   var footer=Ref<TMP_Text>(view,"inventoryFooterText");footer.fontSize=16;Bottom(footer.rectTransform,0,24);footer.textWrappingMode=TextWrappingModes.Normal;
   var heading=right.Find("InventoryHeading") as RectTransform;if(heading!=null)Top(heading,0,24,6,6);
  }
  static void Skills(GameObject go)
  {
   var view=go.GetComponent<SkillLoadoutModalView>();var content=go.transform.Find("Content");
   var equipped=(RectTransform)content.Find("EquippedSection");Top(equipped,0,132);
   var basic=equipped.Find("Content/BasicColumn") as RectTransform;basic.sizeDelta=new Vector2(84,0);
   var basicTitle=basic.Find("BasicLabel").GetComponent<TMP_Text>();basicTitle.fontSize=12;
   Stretch((RectTransform)equipped.Find("Content/ActiveColumn"),100,0,0,0);
   foreach(var button in new[]{Ref<Button>(view,"basicSlotCard"),Ref<Button>(view,"activeSlotTemplate"),Ref<Button>(view,"availableSkillTemplate")})
   {
    var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(64,64);
    var itemLayout=button.GetComponent<LayoutElement>();if(itemLayout!=null){itemLayout.minWidth=itemLayout.preferredWidth=64;itemLayout.minHeight=itemLayout.preferredHeight=64;}
   }
   var activeRow=Ref<Transform>(view,"activeSlotsRoot").GetComponent<HorizontalLayoutGroup>();activeRow.spacing=8;
   var grid=Ref<Transform>(view,"availableSkillsRoot").GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(64,64);grid.spacing=new Vector2(10,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;
   var library=(RectTransform)content.Find("Library");Top(library,148,152);
   var divider=content.Find("DetailsDivider");if(divider!=null)divider.gameObject.SetActive(false);
   var details=(RectTransform)content.Find("SkillDetails");Stretch(details,0,0,0,316);
   foreach(var f in new[]{"detailsTitle","detailsBody","detailsCost","detailsState"}){var l=Ref<TMP_Text>(view,f);l.fontSize=f=="detailsTitle"?20:16;}
   Top(Ref<TMP_Text>(view,"detailsCost").rectTransform,72,44);
   Stretch((RectTransform)Ref<ScrollRect>(view,"detailsScroll").transform,0,50,0,122);
   foreach(var field in new[]{"assignButton","removeButton"}){var b=Ref<Button>(view,field);Bottom((RectTransform)b.transform,0,40);}
  }
  static void Development(GameObject go)
  {
   var view=go.GetComponent<SkillTreeProgressionPanelView>();var content=go.transform.Find("Content");
   var divider=content.Find("DetailsDivider");if(divider!=null)divider.gameObject.SetActive(false);
   content.Find("ProgressionText").gameObject.SetActive(false);
   content.Find("ExperienceMetric").gameObject.SetActive(false);
   Top(Ref<TMP_Text>(view,"availablePointsText").rectTransform,0,28);Ref<TMP_Text>(view,"availablePointsText").fontSize=18;
   Top((RectTransform)content.Find("TreeTabs"),34,36);
   var tab=Ref<Button>(view,"treeTabTemplate");var le=tab.GetComponent<LayoutElement>();le.minWidth=0;le.preferredWidth=130;le.minHeight=le.preferredHeight=36;tab.GetComponentInChildren<TMP_Text>(true).fontSize=16;
   Top((RectTransform)content.Find("TreeHeaderRow"),78,66);Top(Ref<RectTransform>(view,"headerSeparator"),150,2);
   var passive=Ref<TMP_Text>(view,"globalPassiveText");passive.fontSize=16;
   var scroll=Ref<ScrollRect>(view,"treeScroll");Stretch((RectTransform)scroll.transform,0,260,0,164);
   var template=Ref<Button>(view,"nodeTemplate");var layout=template.GetComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=72;layout.minHeight=layout.preferredHeight=72;
   var tiers=new SerializedObject(view).FindProperty("tierRoots");for(var i=0;i<tiers.arraySize;i++){var root=(Transform)tiers.GetArrayElementAtIndex(i).objectReferenceValue;if(root==null)continue;var l=root.GetComponent<LayoutElement>();l.minHeight=l.preferredHeight=72;root.GetComponent<HorizontalLayoutGroup>().spacing=8;}
   var details=(RectTransform)content.Find("NodeDetails");Bottom(details,0,246);
   foreach(var field in new[]{"detailsTitle","detailsBody","detailsRank","investmentBlockText"})Ref<TMP_Text>(view,field).fontSize=field=="detailsTitle"?20:16;
   Stretch((RectTransform)Ref<ScrollRect>(view,"detailsScroll").transform,0,100,0,100);
   var xp=content.Find("ExperienceBar");if(xp!=null)xp.gameObject.SetActive(false);
  }
  [MenuItem("Godmist/UI/Author Party View")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
   for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
   palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
   var go=Clone("PF_UIW_Window.prefab",null);
   try
   {
    go.name="PF_UIW_PartyView";var root=(RectTransform)go.transform;root.sizeDelta=new Vector2(1760,940);root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
    foreach(var style in go.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))style.enabled=false;
    var window=go.GetComponent<UIWorkspaceWindow>();var sw=new SerializedObject(window);sw.FindProperty("minimumSize").vector2Value=new Vector2(680,600);sw.FindProperty("maximumSize").vector2Value=new Vector2(4000,2200);sw.ApplyModifiedPropertiesWithoutUndo();window.ConfigureResizeAxes(false,false);
    foreach(var c in go.GetComponentsInChildren<UIWorkspaceWindowDragHandle>(true))Object.DestroyImmediate(c);
    foreach(var c in go.GetComponentsInChildren<UIWorkspaceWindowResizeHandle>(true))Object.DestroyImmediate(c);
    var surface=Art(root,"PartySurface","surface_solid");Stretch(surface.rectTransform,5,5,5,5);surface.raycastTarget=true;surface.transform.SetAsFirstSibling();
    var view=go.AddComponent<PartyViewWindowView>();Wire(view,"window",window);
    var title=go.transform.Find("Header/Title").GetComponent<TMP_Text>();title.text="Drużyna";title.font=palette.Font;title.fontSize=26;title.enabled=true;title.gameObject.SetActive(true);title.fontSharedMaterial=palette.Font.material;title.color=new Color(.91f,.875f,.78f);title.alignment=TextAlignmentOptions.MidlineLeft;Top((RectTransform)title.transform.parent,0,44);Stretch(title.rectTransform,18,0,60,0);
    var sharedGold=Text(title.transform.parent,"PartyGold","",18);sharedGold.alignment=TextAlignmentOptions.MidlineRight;Stretch(sharedGold.rectTransform,260,0,62,0);Wire(view,"goldText",sharedGold);
    var body=go.transform.Find("Content") as RectTransform;foreach(Transform child in body.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);NoLayout(body);Stretch(body,18,44,18,56);
    var scroll=Scroll(body,"PartyColumns",true);Stretch((RectTransform)scroll.transform);scroll.content.anchorMax=new Vector2(0,1);scroll.content.sizeDelta=new Vector2(1724,0);scroll.content.anchorMin=new Vector2(0,0);scroll.content.anchorMax=new Vector2(0,1);
    var horizontal=scroll.content.gameObject.AddComponent<HorizontalLayoutGroup>();horizontal.spacing=10;horizontal.childControlHeight=horizontal.childControlWidth=true;horizontal.childForceExpandHeight=horizontal.childForceExpandWidth=true;
    var col=Node(scroll.content,"CharacterColumnTemplate");col.sizeDelta=new Vector2(420,0);var element=col.gameObject.AddComponent<LayoutElement>();element.minWidth=PartyViewWindowView.MinimumColumnWidth;element.flexibleWidth=1;
    var cv=col.gameObject.AddComponent<PartyViewColumnView>();var back=Art(col,"Surface","surface_solid");Stretch(back.rectTransform);back.raycastTarget=true;
    var selection=Art(col,"ActiveColumn","tooltip_frame",true);Stretch(selection.rectTransform);selection.color=new Color(.85f,.7f,.4f);selection.enabled=false;Wire(cv,"selection",selection);
    var drop=Art(col,"Recipient","slot_valid",true);Stretch(drop.rectTransform);drop.enabled=false;Wire(cv,"dropPreview",drop);
    var head=Node(col,"CharacterHeader");Top(head,8,104,10,10);
    var portrait=Art(head,"Portrait","surface_solid");portrait.preserveAspect=true;portrait.rectTransform.anchorMin=portrait.rectTransform.anchorMax=new Vector2(0,1);portrait.rectTransform.pivot=new Vector2(0,1);portrait.rectTransform.sizeDelta=new Vector2(72,72);portrait.rectTransform.anchoredPosition=Vector2.zero;Wire(cv,"portrait",portrait);
    var button=Button(head,"IndividualCard","Karta postaci ›");Top((RectTransform)button.transform,74,28);Wire(cv,"individualButton",button);
    var name=Text(head,"Name","Postać",23);Top(name.rectTransform,0,28,84,0);Wire(cv,"identity",name);
    var level=Text(head,"ClassLevel","",16);Top(level.rectTransform,30,22,84,0);Wire(cv,"level",level);
    var vitals=Text(head,"Vitals","",16);Top(vitals.rectTransform,54,22,84,0);Wire(cv,"vitals",vitals);
    var tabs=Node(col,"Modes");Top(tabs,120,36,8,8);var tl=tabs.gameObject.AddComponent<HorizontalLayoutGroup>();tl.spacing=3;tl.childControlWidth=tl.childControlHeight=true;tl.childForceExpandWidth=tl.childForceExpandHeight=true;
    var buttons=new[]{Button(tabs,"Equipment","Ekwipunek"),Button(tabs,"Stats","Statystyki"),Button(tabs,"Skills","Umiejętności"),Button(tabs,"Trees",UiTerms.Development)};Nodes(cv,"modeButtons",buttons);
    var panel=Node(col,"Panels");Stretch(panel,8,8,8,166);panel.gameObject.AddComponent<RectMask2D>();
    var eq=Clone("PF_UIW_CharacterInfoWindow Variant.prefab",panel);Embedded(eq);Equipment(eq);Wire(cv,"equipment",eq.GetComponent<CharacterInfoWindowView>());
    var gold=eq.transform.Find("Content/InventoryColumn/GoldMetric");if(gold!=null)gold.gameObject.SetActive(false);
    var stats=Clone("PF_UIW_CombatUnitInspectPanel.prefab",panel);Embedded(stats);Wire(cv,"stats",stats.GetComponent<CombatUnitInspectWindowView>());
    var si=stats.GetComponent<CombatUnitInspectWindowView>();var statsParent=Ref<TMP_Text>(si,"statsLeftText").transform.parent;
    var resistanceHeading=statsParent.Find("ResistanceHeader") as RectTransform;if(resistanceHeading!=null){Top(resistanceHeading,260,26);Wire(si,"embeddedResistanceHeading",resistanceHeading);}
    var overview=Ref<Image>(si,"portrait").transform.parent;overview.gameObject.SetActive(false);
    var sob=new SerializedObject(si);sob.FindProperty("embedded").boolValue=true;sob.ApplyModifiedPropertiesWithoutUndo();
    var skill=Clone("PF_UIW_SkillLoadoutWindow Variant.prefab",panel);Embedded(skill);Skills(skill);Wire(cv,"skills",skill.GetComponent<SkillLoadoutModalView>());
    var dev=Clone("PF_UIW_SkillTreeWindow Variant.prefab",panel);Embedded(dev);Development(dev);Wire(cv,"development",dev.GetComponent<SkillTreeProgressionPanelView>());
    Nodes(cv,"panels",new Object[]{eq,stats,skill,dev});
    var blocked=Text(panel,"BlockedReason","",18);Top(blocked.rectTransform,40,100,12,12);blocked.gameObject.SetActive(false);Wire(cv,"blockedReason",blocked);
    foreach(var child in new[]{stats,skill,dev})child.SetActive(false);
    selection.transform.SetAsLastSibling();drop.transform.SetAsLastSibling();col.gameObject.SetActive(false);
    Wire(view,"memberScroll",scroll);Wire(view,"columnTemplate",cv);
    var scope=go.GetComponent<ControllerNavigationScope>()??go.AddComponent<ControllerNavigationScope>();var ss=new SerializedObject(scope);ss.FindProperty("modal").boolValue=false;ss.ApplyModifiedPropertiesWithoutUndo();UnityEventTools.AddPersistentListener(scope.CancelAction,view.Close);
    RefineRoot(go);
    var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"PF_UIW_PartyView.prefab");WireRails(prefab.GetComponent<PartyViewWindowView>());
   }
   finally{Object.DestroyImmediate(go);}
  }
  static void RefineRoot(GameObject root)
  {
   var view=root.GetComponent<PartyViewWindowView>();var column=root.GetComponentInChildren<PartyViewColumnView>(true);
   Skills(Ref<SkillLoadoutModalView>(column,"skills").gameObject);
   Development(Ref<SkillTreeProgressionPanelView>(column,"development").gameObject);
   var buttons=new SerializedObject(column).FindProperty("modeButtons");
   ((Button)buttons.GetArrayElementAtIndex(3).objectReferenceValue).GetComponentInChildren<TMP_Text>(true).text=UiTerms.Development;
   var header=root.transform.Find("Header");var value=Ref<TMP_Text>(view,"goldText");
   var gold=header.Find("GoldMetric") as RectTransform??Node(header,"GoldMetric");
   gold.anchorMin=gold.anchorMax=new Vector2(1,.5f);gold.pivot=new Vector2(1,.5f);gold.sizeDelta=new Vector2(160,34);gold.anchoredPosition=new Vector2(-64,0);
   Metric(gold,"Gold","gold","Złoto drużyny",value);
   // A centered window title and a separate treasury plate give the header a clear hierarchy.
   var title=header.Find("Title").GetComponent<TMP_Text>();title.fontSize=28;title.alignment=TextAlignmentOptions.Center;title.color=new Color(.93f,.87f,.70f);title.margin=Vector4.zero;Stretch(title.rectTransform,250,0,250,0);
   var close=(RectTransform)header.Find("CloseButton");close.anchorMin=close.anchorMax=new Vector2(1,.5f);close.pivot=new Vector2(1,.5f);close.sizeDelta=new Vector2(28,28);close.anchoredPosition=new Vector2(-12,0);
   gold.anchoredPosition=new Vector2(-62,0);
   var goldLayout=gold.GetComponent<HorizontalLayoutGroup>()??gold.gameObject.AddComponent<HorizontalLayoutGroup>();goldLayout.padding=new RectOffset(12,12,0,0);goldLayout.spacing=8;goldLayout.childAlignment=TextAnchor.MiddleLeft;goldLayout.childControlWidth=goldLayout.childControlHeight=true;goldLayout.childForceExpandWidth=goldLayout.childForceExpandHeight=false;
   var goldSize=gold.GetComponent<ContentSizeFitter>()??gold.gameObject.AddComponent<ContentSizeFitter>();goldSize.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;goldSize.verticalFit=ContentSizeFitter.FitMode.Unconstrained;
   var goldIcon=gold.Find("GoldIcon");var iconLayout=goldIcon.GetComponent<LayoutElement>()??goldIcon.gameObject.AddComponent<LayoutElement>();iconLayout.minWidth=iconLayout.preferredWidth=28;iconLayout.minHeight=iconLayout.preferredHeight=28;
   value.fontSize=20;value.color=new Color(.96f,.82f,.43f);value.alignment=TextAlignmentOptions.MidlineRight;value.margin=Vector4.zero;
   var plate=gold.GetComponent<Image>();plate.sprite=palette.GetArtwork("surface_solid");plate.type=Image.Type.Sliced;plate.color=Color.white;
   var goldFrame=gold.Find("GoldFrame")?.GetComponent<Image>()??Art(gold,"GoldFrame","tooltip_frame",true);
   var ignore=goldFrame.GetComponent<LayoutElement>()??goldFrame.gameObject.AddComponent<LayoutElement>();ignore.ignoreLayout=true;Stretch(goldFrame.rectTransform);goldFrame.transform.SetAsFirstSibling();


   var head=(RectTransform)column.transform.Find("CharacterHeader");Top(head,8,194,10,10);
   Top((RectTransform)Ref<Button>(column,"individualButton").transform,164,28);
   Top((RectTransform)column.transform.Find("Modes"),210,36,8,8);Stretch((RectTransform)column.transform.Find("Panels"),8,8,8,256);
   var xp=head.Find("ExperienceMetric") as RectTransform??Node(head,"ExperienceMetric");Top(xp,94,24,0,210);
   Wire(column,"experience",Metric(xp,"Experience","experience",UiTerms.Experience));
   var honor=head.Find("HonorMetric") as RectTransform??Node(head,"HonorMetric");Top(honor,94,24,210,0);
   Wire(column,"honor",Metric(honor,"Honor","honor",UiTerms.Honor));
   var state=head.Find("Availability")?.GetComponent<TMP_Text>()??Text(head,"Availability","",15);Top(state.rectTransform,136,22);Wire(column,"availability",state);
   var equipment=Ref<CharacterInfoWindowView>(column,"equipment");Equipment(equipment.gameObject);
   var oldSummary=equipment.transform.Find("Content/CharacterCenter/Summary");if(oldSummary!=null)Object.DestroyImmediate(oldSummary.gameObject);
   var healthText=Ref<TMP_Text>(column,"vitals");healthText.fontSize=14;Top(healthText.rectTransform,54,22,84,210);
   var resourceText=head.Find("ResourceValue")?.GetComponent<TMP_Text>()??Text(head,"ResourceValue","",14);Top(resourceText.rectTransform,54,22,230,0);Wire(column,"resourceValue",resourceText);
   Wire(column,"healthFill",ThinBar(head,"HealthBar",Ref<Image>(equipment,"healthFill"),76,84,210));
   var resource=ThinBar(head,"ResourceBar",Ref<Image>(equipment,"resourceFill"),76,230,0);Wire(column,"resourceFill",resource);Wire(column,"resourceTrack",head.Find("ResourceBar").gameObject);
   Wire(column,"experienceFill",ThinBar(head,"ExperienceBar",Ref<Image>(equipment,"experienceFill"),120,0,210));
  }
  static Image ThinBar(RectTransform header,string name,Image source,float y,float left,float right)
  {
   var bar=header.Find(name)?.gameObject;
   if(bar==null){bar=Object.Instantiate(source.GetComponentInParent<UIBarFillMask>(true).gameObject,header,false);bar.name=name;}
   bar.SetActive(true);Top((RectTransform)bar.transform,y,10,left,right);
   var mask=bar.GetComponent<UIBarFillMask>();mask.Apply();
   foreach(var image in bar.GetComponentsInChildren<Image>(true))image.raycastTarget=false;
   return mask.FillArea.GetComponentInChildren<Image>(true);
  }
  static TMP_Text Metric(RectTransform row,string name,string iconName,string title,TMP_Text existing=null)
  {
   var icon=row.Find(name+"Icon")?.GetComponent<Image>();if(icon==null)icon=Node(row,name+"Icon").gameObject.AddComponent<Image>();
   icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/"+iconName+"-32.png")??throw new Exception("Missing resource icon "+iconName);
   icon.preserveAspect=true;icon.raycastTarget=false;icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(0,.5f);icon.rectTransform.pivot=new Vector2(0,.5f);icon.rectTransform.sizeDelta=new Vector2(24,24);icon.rectTransform.anchoredPosition=Vector2.zero;
   var value=existing??row.Find(name+"Value")?.GetComponent<TMP_Text>()??Text(row,name+"Value","",16);value.transform.SetParent(row,false);value.alignment=TextAlignmentOptions.MidlineLeft;value.textWrappingMode=TextWrappingModes.NoWrap;Stretch(value.rectTransform,32,0,0,0);
   var hit=row.GetComponent<Image>()??row.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
   (row.GetComponent<CombatSimpleTooltipTrigger>()??row.gameObject.AddComponent<CombatSimpleTooltipTrigger>()).Initialize(null,title,"");return value;
  }
  [MenuItem("Godmist/UI/Refine Party View presentation")]
  public static void RefinePresentation()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
   palette=AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
   var path=Root+"PF_UIW_PartyView.prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try{RefineRoot(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  public static void RefinePresentationAndExit()
  {
   if(!Application.isBatchMode)throw new InvalidOperationException("Batch verification only.");
   try{RefinePresentation();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static void WireRails(PartyViewWindowView prefab)
  {
   foreach(var path in new[]{Root+"PF_UIW_PartyRail.prefab",Root+"PF_UIW_CombatPartyRail.prefab"})
   {
    var root=PrefabUtility.LoadPrefabContents(path);try{foreach(var rail in root.GetComponentsInChildren<CharacterInfoPartyRailView>(true)){Wire(rail,"partyViewPrefab",prefab);if(Ref<CharacterInfoWindowView>(rail,"characterInfoWindowPrefab")==null)Wire(rail,"characterInfoWindowPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PF_UIW_CharacterInfoWindow Variant.prefab").GetComponent<CharacterInfoWindowView>());}PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
   }
   var setup=EditorSceneManager.GetSceneManagerSetup();
   try
   {
    foreach(var path in new[]{"Assets/_Project/Scenes/Bootstrap/Bootstrap.unity","Assets/_Project/Scenes/City/Arungard.unity","Assets/_Project/Scenes/Sandbox/CombatSandbox.unity","Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity","Assets/_Project/Scenes/Sandbox/ExplorationSandbox.unity"})
    {
     var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var changed=false;
     foreach(var rail in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CharacterInfoPartyRailView>(true)))
      if(Ref<PartyViewWindowView>(rail,"partyViewPrefab")!=prefab){Wire(rail,"partyViewPrefab",prefab);changed=true;}
     if(changed)EditorSceneManager.SaveScene(scene);
    }
   }
   finally{if(setup.Any(s=>s.isActive&&s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
 }
}
