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
  [TestCase("PF_UIW_CombatVitals", typeof(CombatVitalsView))]
  [TestCase("PF_UIW_CombatActionPanel", typeof(CombatActiveUnitView))]
  [TestCase("PF_UIW_CombatUnitInspectPanel", typeof(CombatUnitInspectWindowView))]
  public void ActionPointTemplateHasInheritedFillComponent(string prefabName, System.Type viewType)
  {
   var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/" + prefabName + ".prefab");
   try
   {
    var view = root.GetComponentInChildren(viewType, true);
    Assert.That(view, Is.Not.Null);
    var reference = new SerializedObject(view).FindProperty("actionPointPipTemplate").objectReferenceValue;
    var template = reference is GameObject go ? go : ((Component)reference).gameObject;
    Assert.That(template.GetComponents<ActionPointPipFill>(), Has.Length.EqualTo(1));
    Assert.That(template.GetComponent<ActionPointPipFill>().enabled, Is.True);
    Assert.That(template.GetComponent<Image>(), Is.Not.Null);
    Assert.That(template.activeSelf, Is.False);
   }
   finally { PrefabUtility.UnloadPrefabContents(root); }
  }

  [Test] public void FractionalApPreservesSixtyPercentCostAndMovementBudget()
  {
   var unit = new Godmist.Runtime.Combat.BattleUnit { CurrentActionPoints = 600, MaxActionPoints = 600 };
   var cost = Godmist.Systems.Combat.CombatActionCostCalculator.CalculatePercentageCost(unit.MaxActionPoints, 60);
   Assert.That(cost, Is.EqualTo(360));
   unit.CurrentActionPoints -= cost;
   Assert.That(unit.CurrentActionPoints, Is.EqualTo(240));
   Assert.That(unit.CurrentActionPoints, Is.LessThan(cost));
   Assert.That(Godmist.Systems.Combat.CombatActionCostCalculator.CalculateMovementCost(unit), Is.EqualTo(100));
   Assert.That(Godmist.Systems.Combat.CombatMovementSystem.CalculatePathCost(unit, 2), Is.EqualTo(300));
   Assert.That(Godmist.Systems.Combat.CombatActionCostCalculator.CalculatePercentageCost(1, 1), Is.EqualTo(1));
   Assert.That(Godmist.Systems.Combat.CombatActionCostCalculator.CalculatePercentageCost(600, 0), Is.Zero);
   Assert.That(Godmist.Runtime.Combat.ActionPointUnits.PipCount(240), Is.EqualTo(3));
   Assert.That(Godmist.Runtime.Combat.ActionPointUnits.PipFill(240, 2), Is.EqualTo(.4f).Within(.00001f));
  }

  [Test] public void FractionalSpeedAndMomentumKeepTheirOriginalScale()
  {
   var character = new Godmist.Runtime.Characters.CharacterInstance { Stats = new Godmist.Runtime.Characters.CharacterStats() };
   character.Stats.Speed.BaseValue = 21;
   Assert.That(new Godmist.Systems.Combat.CombatTurnSystem().CalculateMaxActionPoints(character, .25f), Is.EqualTo(525));
   var pool = new ResourcePool(Godmist.Core.Enums.ResourceType.Momentum, 0, 200, 0);
   var rule = new Godmist.Systems.Combat.MomentumResourceRule();
   Assert.That(rule.ProcessMovement(pool, 25), Is.Zero);
   Assert.That(rule.ProcessMovement(pool, 25), Is.EqualTo(1));
   Assert.That(rule.ProcessMovement(pool, 100), Is.EqualTo(2));
  }

  [Test] public void PipMeshSeparatesRemainingPreviewAndSpentIncludingSpriteLessImages()
  {
   var go = new GameObject("pip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ActionPointPipFill));
   try
   {
    var image = go.GetComponent<Image>();
    ActionPointPipPresentation.Apply(image, 260, 2, Color.green, Color.gray, 240, Color.yellow);
    Assert.That(image.type, Is.EqualTo(Image.Type.Simple));
    using var mesh = new VertexHelper();
    var vertices = new UIVertex[4];
    for (var i = 0; i < 4; i++) vertices[i] = UIVertex.simpleVert;
    vertices[0].position = new Vector3(0,0); vertices[1].position = new Vector3(0,10);
    vertices[2].position = new Vector3(10,10); vertices[3].position = new Vector3(10,0);
    mesh.AddUIVertexQuad(vertices);
    go.GetComponent<ActionPointPipFill>().ModifyMesh(mesh);
    var stream = new System.Collections.Generic.List<UIVertex>(); mesh.GetUIVertexStream(stream);
    float Area(Color color)
    {
     var area=0f;
     for(var i=0;i<stream.Count;i+=3) if(stream[i].color.Equals((Color32)color))
      area += Vector3.Cross(stream[i+1].position-stream[i].position, stream[i+2].position-stream[i].position).magnitude / 2;
     return area;
    }
    Assert.That(Area(Color.green), Is.EqualTo(40).Within(.001));
    Assert.That(Area(Color.yellow), Is.EqualTo(20).Within(.001));
    Assert.That(Area(Color.gray), Is.EqualTo(40).Within(.001));
   }
   finally { Object.DestroyImmediate(go); }
  }

  [Test] public void SavedActionPointBonusKeepsWholePointMeaningAndFractions()
  {
   var legacy = JsonUtility.FromJson<Godmist.Core.Serialization.PassiveEffectPayloadSaveData>("{\"ActionPointModifier\":2}");
   Assert.That(legacy.ActionPointModifier, Is.EqualTo(2f));
   legacy.ActionPointModifier = .6f;
   var restored = JsonUtility.FromJson<Godmist.Core.Serialization.PassiveEffectPayloadSaveData>(JsonUtility.ToJson(legacy));
   Assert.That(restored.ActionPointModifier, Is.EqualTo(.6f));
  }

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
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=character,CurrentActionPoints=1200,MaxActionPoints=1200};
    view.Bind(unit,800);
    var label=new SerializedObject(view).FindProperty("actionValueText").objectReferenceValue;
    string ReadLabel()=>new SerializedObject(label).FindProperty("m_text").stringValue;
    Assert.That(ReadLabel(),Is.EqualTo("12 → 4 AP"));
    Assert.That(unit.CurrentActionPoints,Is.EqualTo(1200));Assert.That(character.CurrentHealth,Is.EqualTo(70));Assert.That(character.ResourcePool.CurrentValue,Is.EqualTo(20));
    view.Bind(unit);Assert.That(ReadLabel(),Is.EqualTo("12/12 AP"));
    view.Bind(unit,2000);Assert.That(ReadLabel(),Does.Contain("potrzeba 20"));
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
