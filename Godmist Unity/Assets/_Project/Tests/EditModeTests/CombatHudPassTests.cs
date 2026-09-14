using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Godmist.Views.UI;
namespace Godmist.Tests.EditMode
{
 public class CombatHudPassTests
 {
  [Test] public void TimelineTooltipRebindPreservesExitAndInvalidatesChangedContent()
  {
   var go=new GameObject("Tooltip",typeof(RectTransform),typeof(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView));
   var source=new GameObject("Source",typeof(CombatSimpleTooltipTrigger));
   try {
    var tooltip=go.GetComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>();
    var trigger=source.GetComponent<CombatSimpleTooltipTrigger>();
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var hovered=tooltip.GetType().GetField("sourceHovered",flags);
    var pending=tooltip.GetType().GetField("pendingSource",flags);
    trigger.Initialize(tooltip,"Jerzy","Nadchodząca tura");
    trigger.OnPointerEnter(null);
    trigger.Initialize(null,"Jerzy","Nadchodząca tura");
    Assert.That(hovered.GetValue(tooltip),Is.True);
    trigger.OnPointerExit(null);
    Assert.That(hovered.GetValue(tooltip),Is.False,"Exit must reach the resolved tooltip after per-frame Bind");
    trigger.OnPointerEnter(null);
    trigger.Initialize(null,"Wladimir","Aktualna tura");
    Assert.That(pending.GetValue(tooltip),Is.Null,"Reused portrait must not retain the old pending tooltip");
   }finally{Object.DestroyImmediate(source);Object.DestroyImmediate(go);}
  }
  [TestCase(5, 72f)]
  [TestCase(6, 151f)]
  [TestCase(10, 151f)]
  [TestCase(12, 151f)]
  public void SkillGridKeepsAllSkillsAndLimitsViewportToTwoRows(int count, float height)
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatActionPanel.prefab");
   var data=ScriptableObject.CreateInstance<Godmist.Data.Skills.SkillData>();
   try {
    var bar=root.GetComponentInChildren<CombatSkillBarView>(true);
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance()};
    var list=Enumerable.Repeat(data,count).ToList();
    typeof(CombatSkillBarView).GetMethod("RebuildSlots",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(bar,new object[]{unit,list,"test"});
    var scroll=bar.GetComponent<ScrollRect>();var grid=scroll.content.GetComponent<GridLayoutGroup>();
    Assert.That(grid.constraintCount,Is.EqualTo(5));Assert.That(scroll.horizontal,Is.False);
    Assert.That(scroll.content.GetComponentsInChildren<CombatSkillSlotView>().Length,Is.EqualTo(count));
    Assert.That(bar.GetComponent<LayoutElement>().preferredHeight,Is.EqualTo(height));
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    Assert.That(scroll.viewport.rect.height,Is.GreaterThanOrEqualTo(height));
    if(count>10)Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
    if(count>=10){var tenth=scroll.content.GetComponentsInChildren<CombatSkillSlotView>()[9];var text=new SerializedObject(tenth).FindProperty("shortcutText").objectReferenceValue;Assert.That(new SerializedObject(text).FindProperty("m_text").stringValue,Is.EqualTo("0"));}
   }finally{PrefabUtility.UnloadPrefabContents(root);Object.DestroyImmediate(data);}
  }
  [Test] public void DungeonHudHasOneActionPanelAndNonWrappingAp()
  {
   var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity",OpenSceneMode.Additive);
   try {
    var hud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CombatHudView>(true)).Single();
    foreach(var field in new[]{"sandboxBootstrap","movement","targeting","inspect","skills","targetPanel"})
        Assert.That(new SerializedObject(hud).FindProperty(field).objectReferenceValue,Is.Not.Null,field);
    Assert.That(PrefabUtility.IsPartOfPrefabInstance(hud),Is.True);
    foreach(var view in hud.GetComponentsInChildren<MonoBehaviour>(true))
    {
        if(view==null)continue;
        var serialized=new SerializedObject(view);var owner=serialized.FindProperty("sandboxBootstrap");
        if(owner!=null)Assert.That(owner.objectReferenceValue,Is.Not.Null,view.GetType().Name);
    }
    var bottom=hud.transform.Find("BottomPanel");
    Assert.That(bottom.GetComponent<HorizontalLayoutGroup>(),Is.Not.Null);
    var active=hud.GetComponentInChildren<CombatActiveUnitView>(true);
    var so=new SerializedObject(active);var ap=(Transform)so.FindProperty("actionPointPipRoot").objectReferenceValue;
    Assert.That(ap.GetComponent<GridLayoutGroup>(),Is.Null);
    Assert.That(ap.GetComponent<HorizontalLayoutGroup>(),Is.Not.Null);
    Assert.That(active.transform.parent,Is.EqualTo(bottom));
    Assert.That(hud.GetComponentInChildren<CombatSkillBarView>(true).transform.parent,Is.EqualTo(bottom));
    Assert.That(hud.GetComponentInChildren<CombatActionButtonsView>(true).transform.parent,Is.EqualTo(bottom));
    var slot=hud.GetComponentInChildren<CombatSkillSlotView>(true);var ss=new SerializedObject(slot);
    foreach(var f in new[]{"actionCostText","hotkeyText","selectedFrame"}) Assert.That(ss.FindProperty(f).objectReferenceValue,Is.Not.Null,f);
   } finally {EditorSceneManager.CloseScene(scene,true);}
  }

  [Test] public void SharedVitalsPreviewDoesNotSpendResources()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab");
   try {
    var view=root.GetComponent<CombatVitalsView>();
    var character=new Godmist.Runtime.Characters.CharacterInstance{CurrentHealth=70,Stats=new Godmist.Runtime.Characters.CharacterStats(),ResourcePool=new ResourcePool(Godmist.Core.Enums.ResourceType.Mana,20,50,0)};
    character.Stats.MaxHealth.BaseValue=100;
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=character,CurrentActionPoints=12,MaxActionPoints=12};
    view.Bind(unit,8);
    var label=new SerializedObject(view).FindProperty("actionValueText").objectReferenceValue;
    string ReadLabel()=>new SerializedObject(label).FindProperty("m_text").stringValue;
    Assert.That(ReadLabel(),Is.EqualTo("12 → 4 AP"));
    Assert.That(unit.CurrentActionPoints,Is.EqualTo(12));Assert.That(character.CurrentHealth,Is.EqualTo(70));Assert.That(character.ResourcePool.CurrentValue,Is.EqualTo(20));
    view.Bind(unit);Assert.That(ReadLabel(),Is.EqualTo("12/12 AP"));
    view.Bind(unit,20);Assert.That(ReadLabel(),Does.Contain("potrzeba 20"));
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  [Test] public void TimelineHoverClearsOnDisableNotification()
  {
   var go=new GameObject("Hud",typeof(CombatHudView));var entry=new GameObject("Entry",typeof(RectTransform),typeof(CombatTurnPortraitView));
   try {var hud=go.GetComponent<CombatHudView>();var unit=new Godmist.Runtime.Combat.BattleUnit();hud.SetTimelineHover(entry.GetComponent<CombatTurnPortraitView>(),unit);Assert.That(hud.HighlightedUnit,Is.SameAs(unit));typeof(CombatHudView).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hud,null);Assert.That(hud.HighlightedUnit,Is.Null);}
   finally{Object.DestroyImmediate(entry);Object.DestroyImmediate(go);}
  }

  [Test] public void FutureTurnPortraitDimsButHoverRestoresReadability()
  {
   var g=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab");
   try{var view=g.GetComponent<CombatTurnPortraitView>();var unit=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance()};var image=(Image)new SerializedObject(view).FindProperty("portrait").objectReferenceValue;view.Bind(unit,false,2);Assert.That(image.color.a,Is.LessThan(.5f));view.Bind(unit,false,2,highlighted:true);Assert.That(image.color.a,Is.EqualTo(1f));}
   finally{PrefabUtility.UnloadPrefabContents(g);}
  }
  [Test] public void UnavailableSkillCannotInvokeItsAction()
  {
   var go=new GameObject("Slot",typeof(RectTransform),typeof(Image),typeof(Button),typeof(CombatSkillSlotView));
   try {var slot=go.GetComponent<CombatSkillSlotView>();var so=new SerializedObject(slot);so.FindProperty("button").objectReferenceValue=go.GetComponent<Button>();so.ApplyModifiedPropertiesWithoutUndo();var invoked=0;slot.Initialize(null,null,()=>invoked++);slot.RefreshAvailability(false,3,false,"",reason:"Za mało punktów akcji.");Assert.That(slot.TryActivate(),Is.False);Assert.That(invoked,Is.Zero);slot.RefreshAvailability(true,3,false,"");Assert.That(slot.TryActivate(),Is.True);Assert.That(invoked,Is.EqualTo(1));Assert.That(go.transform.childCount,Is.Zero,"No runtime static UI fallback");}finally{Object.DestroyImmediate(go);}
  }
 }
}
