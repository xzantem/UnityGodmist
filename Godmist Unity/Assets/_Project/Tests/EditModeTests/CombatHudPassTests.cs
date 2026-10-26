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
  [Test] public void ExplorationKeepsNativeSkillsVisibleAndClickableWithoutCombatControlsOrSession()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
   var runtime=new GameObject("exploration HUD test runtime");runtime.SetActive(false);
   try
   {
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var bootstrap=runtime.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
    var definition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
    var actor=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition),IsPlayerSide=true,CurrentActionPoints=1300,MaxActionPoints=1300,AvailableKnives=5,MaxKnives=5};
    var hud=root.GetComponent<CombatHudView>();var bar=root.GetComponentInChildren<CombatSkillBarView>(true);
    var actions=root.GetComponentInChildren<CombatActionButtonsView>(true);var active=root.GetComponentInChildren<CombatActiveUnitView>(true);
    foreach(var view in new MonoBehaviour[]{hud,bar,actions}){var state=new SerializedObject(view);state.FindProperty("sandboxBootstrap").objectReferenceValue=bootstrap;state.ApplyModifiedPropertiesWithoutUndo();}
    Godmist.Data.Skills.SkillData selected=null;bar.BindExploration(()=>actor,skill=>selected=skill);
    void Refresh(){typeof(CombatSkillBarView).GetMethod("Update",flags).Invoke(bar,null);typeof(CombatHudView).GetMethod("LateUpdate",flags).Invoke(hud,null);typeof(CombatActionButtonsView).GetMethod("Update",flags).Invoke(actions,null);}
    Refresh();var slots=bar.GetComponentsInChildren<CombatSkillSlotView>();Assert.That(slots.Length,Is.GreaterThan(0));
    Assert.That(slots.Any(slot=>slot.TryActivate()),Is.True);Assert.That(selected,Is.Not.Null);
    Assert.That(actor.Character.GetActiveSkills(),Does.Contain(selected));Assert.That(bootstrap.Session,Is.Null);Assert.That(bootstrap.Drivers,Is.Empty);
    Assert.That(active.gameObject.activeSelf,Is.False);
    Assert.That(root.transform.Find("TopBar").gameObject.activeSelf,Is.False);
    Assert.That(root.transform.Find("TimelineDrawerHandle").gameObject.activeSelf,Is.False);
    var actionState=new SerializedObject(actions);
    foreach(var field in new[]{"endTurnButton","escapeButton","consumablesButton"})
     Assert.That(((Button)actionState.FindProperty(field).objectReferenceValue).gameObject.activeSelf,Is.False);
    for(int frame=0;frame<10;frame++)Refresh();
    Assert.That(bar.GetComponentsInChildren<CombatSkillSlotView>(),Is.EqualTo(slots),"Idle frames reuse existing slots.");
    var mage=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset");
    actor=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(mage),IsPlayerSide=true,CurrentActionPoints=1300,MaxActionPoints=1300};
    selected=null;Refresh();Assert.That(bar.GetComponentsInChildren<CombatSkillSlotView>().Any(slot=>slot.TryActivate()),Is.True);
    Assert.That(actor.Character.GetActiveSkills(),Does.Contain(selected),"Changing leader displays and activates the new leader's loadout.");
   }
   finally{PrefabUtility.UnloadPrefabContents(root);Object.DestroyImmediate(runtime);}
  }

  [Test] public void EnemyTurnConcealsUnknownSkillsWithoutCollapsingAndRestoresKnownAndPlayerControls()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
   var runtime=new GameObject("HUD test runtime");runtime.SetActive(false);
   try
   {
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var bootstrap=runtime.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
    var spawner=runtime.AddComponent<Godmist.Views.Combat.CombatUnitViewSpawner>();
    var map=runtime.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();spawner.ConfigureDungeon(null,map);
    var visible=true;spawner.ExplorationPresentationVisible=_=>visible;
    var bootstrapState=new SerializedObject(bootstrap);bootstrapState.FindProperty("unitViewSpawner").objectReferenceValue=spawner;bootstrapState.ApplyModifiedPropertiesWithoutUndo();
    var driver=(Godmist.Views.Combat.CombatExecutionDriver)typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetMethod("CreateDriver",flags).Invoke(bootstrap,null);
    var definition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition),CurrentActionPoints=1300,MaxActionPoints=1300};
    var session=new Godmist.Runtime.Combat.CombatSession();session.Units.Add(unit);session.TurnParticipants.Add(new Godmist.Runtime.Combat.CombatTurnParticipant(unit));session.ActiveUnit=unit;
    typeof(Godmist.Views.Combat.CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
    typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetField("focused",flags).SetValue(bootstrap,driver);
    var hud=root.GetComponent<CombatHudView>();var bar=root.GetComponentInChildren<CombatSkillBarView>(true);
    var actions=root.GetComponentInChildren<CombatActionButtonsView>(true);var active=root.GetComponentInChildren<CombatActiveUnitView>(true);
    foreach(var view in new MonoBehaviour[]{hud,bar,actions}){var state=new SerializedObject(view);state.FindProperty("sandboxBootstrap").objectReferenceValue=bootstrap;state.ApplyModifiedPropertiesWithoutUndo();}
    void Refresh(){typeof(CombatSkillBarView).GetMethod("Update",flags).Invoke(bar,null);typeof(CombatHudView).GetMethod("LateUpdate",flags).Invoke(hud,null);typeof(CombatActionButtonsView).GetMethod("Update",flags).Invoke(actions,null);}
    var activeState=new SerializedObject(active);var vitals=(CombatVitalsView)activeState.FindProperty("vitals").objectReferenceValue;
    var portrait=(Image)activeState.FindProperty("portrait").objectReferenceValue;var title=(TMPro.TMP_Text)activeState.FindProperty("unitNameText").objectReferenceValue;
    var width=active.GetComponent<LayoutElement>().preferredWidth;
    var actionState=new SerializedObject(actions);var end=(Button)actionState.FindProperty("endTurnButton").objectReferenceValue;
    var items=(Button)actionState.FindProperty("consumablesButton").objectReferenceValue;
    Refresh();var skillCount=bar.GetComponentsInChildren<CombatSkillSlotView>().Length;
    Assert.That(skillCount,Is.GreaterThan(0));Assert.That(vitals.gameObject.activeSelf,Is.True);Assert.That(portrait.sprite,Is.Not.Null);
    Assert.That(end.gameObject.activeSelf,Is.False);Assert.That(items.gameObject.activeSelf,Is.False);
    visible=false;Refresh();
    Assert.That(active.gameObject.activeSelf,Is.True);Assert.That(active.GetComponent<LayoutElement>().preferredWidth,Is.EqualTo(width));
    Assert.That(vitals.gameObject.activeSelf,Is.False);Assert.That(portrait.sprite,Is.Null);Assert.That(title.text,Is.EqualTo("Nieznany przeciwnik"));
    Assert.That(bar.GetComponentsInChildren<CombatSkillSlotView>(),Is.Empty);Assert.That(bar.HoveredSkill,Is.Null);
    visible=true;Refresh();Assert.That(vitals.gameObject.activeSelf,Is.True);Assert.That(portrait.sprite,Is.Not.Null);
    Assert.That(portrait.color,Is.EqualTo(Color.white));Assert.That(title.text,Is.EqualTo(definition.DisplayName));
    Assert.That(bar.GetComponentsInChildren<CombatSkillSlotView>().Length,Is.EqualTo(skillCount));
    unit.IsPlayerSide=true;typeof(CombatHudView).GetMethod("LateUpdate",flags).Invoke(hud,null);typeof(CombatActionButtonsView).GetMethod("Update",flags).Invoke(actions,null);
    Assert.That(end.gameObject.activeSelf,Is.True);Assert.That(items.gameObject.activeSelf,Is.True);
   }finally{PrefabUtility.UnloadPrefabContents(root);Object.DestroyImmediate(runtime);}
  }

  [Test] public void PixelTargetsHaveNoFillAndPathUsesPointFilteredGems()
  {
   var root=new GameObject("pixel markers");
   try
   {
    var view=root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
    var cells=new[]{new Godmist.Runtime.Combat.GridPosition(0,0),new Godmist.Runtime.Combat.GridPosition(0,1)};
    view.ShowOutline(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillTarget,cells,Color.green);
    view.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillTarget,cells,Color.green);
    var batch=root.GetComponentsInChildren<MeshRenderer>().Single();
    var mesh=batch.GetComponent<MeshFilter>().sharedMesh;
    Assert.That(mesh.vertexCount,Is.EqualTo(8));
    Assert.That(mesh.triangles.Length,Is.EqualTo(12));
    var texture=(Texture2D)batch.sharedMaterial.mainTexture;
    Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
    Assert.That(texture.GetPixel(16,16).a,Is.Zero,"The target center must stay clear.");
    for(var i=0;i<4;i++)view.RefreshVisibility();
    Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1),"Refresh must reuse the target batch.");
    view.ClearAll();
    view.ShowMovementPreview(new[]{new Godmist.Runtime.Combat.GridPosition(1,0),new Godmist.Runtime.Combat.GridPosition(1,1)},cells[0],300,Color.yellow,showCost:false);
    var gems=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Combat pixel path gem").ToArray();
    Assert.That(gems.Length,Is.EqualTo(8));foreach(var gem in gems)Assert.That(gem.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
    view.IsPositionVisible=_=>false;view.RefreshVisibility();Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty);
   }finally{Object.DestroyImmediate(root);}
  }

  [Test] public void FloorFeedbackTracksNegativeActorBandAndRestoresBorrowedSortingGroup()
  {
   var root=new GameObject("floor map");var grid=new GameObject("Grid",typeof(Grid));grid.transform.SetParent(root.transform);
   var floor=new GameObject("Floor",typeof(UnityEngine.Tilemaps.Tilemap),typeof(UnityEngine.Tilemaps.TilemapRenderer));floor.transform.SetParent(grid.transform);
   var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();var feedbackRoot=new GameObject("Feedback");
   try
   {
    var view=root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();var so=new SerializedObject(view);
    so.FindProperty("floorTilemap").objectReferenceValue=floor.GetComponent<UnityEngine.Tilemaps.Tilemap>();so.FindProperty("floorTile").objectReferenceValue=tile;
    so.FindProperty("wallSegmentPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();so.ApplyModifiedPropertiesWithoutUndo();
    var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();for(var y=0;y<40;y++){map.TryAddFloorCell(new Vector2Int(0,y),"room");map.TryAddFloorCell(new Vector2Int(1,y),"room");map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(new Vector2Int(0,y),new Vector2Int(1,y)),Godmist.Core.Enums.DungeonWallType.Full);}view.Rebuild(map);
    var group=feedbackRoot.AddComponent<UnityEngine.Rendering.SortingGroup>();group.sortingOrder=401;group.enabled=false;
    var feedback=feedbackRoot.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();feedback.SetFloorSorting(view.FloorFeedbackSortingLayerID,view.GetFloorFeedbackDepthSortingOrder());
    Assert.That(group.sortingOrder,Is.LessThan(0));Assert.That(group.enabled,Is.True);
    Assert.That(floor.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingOrder,Is.LessThan(group.sortingOrder));
    Assert.That(group.sortingOrder,Is.LessThan(view.GetDungeonObjectDepthSortingOrder()));
    for(var y=-1;y<42;y++)Assert.That(group.sortingOrder,Is.LessThan(view.GetActorDepthSortingOrder(y)));
    // EditMode does not dispatch MonoBehaviour's runtime destruction callbacks.
    typeof(Godmist.Views.Combat.CombatGridFeedbackRenderer).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(feedback,null);
    typeof(Godmist.Views.Combat.CombatGridFeedbackRenderer).GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(feedback,null);
    Object.DestroyImmediate(feedback);Assert.That(group.sortingOrder,Is.EqualTo(401));Assert.That(group.enabled,Is.False);
   }finally{Object.DestroyImmediate(feedbackRoot);Object.DestroyImmediate(root);Object.DestroyImmediate(tile);}
  }

  [Test] public void SoftFloorJoinsTilesAndFogRebuildsTheExposedBoundary()
  {
   var root=new GameObject("soft floor");
   try
   {
    var view=root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
    var cells=new[]{new Godmist.Runtime.Combat.GridPosition(0,0),new Godmist.Runtime.Combat.GridPosition(1,0)};
    view.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange,cells,Color.blue);
    var mesh=root.GetComponentInChildren<MeshFilter>();var texture=(Texture2D)mesh.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
    var uv=mesh.sharedMesh.uv;
    float Alpha(Vector2 point)=>texture.GetPixelBilinear(point.x,point.y).a;
    Assert.That(Alpha(Vector2.Lerp(uv[2],uv[3],.5f)),Is.GreaterThan(.8f),"Joined edge must not become a dark seam.");
    Assert.That(Alpha(Vector2.Lerp(uv[0],uv[1],.5f)),Is.LessThan(.02f),"Exposed outer edge must fade out.");
    Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty,"No hard rectangular outlines remain.");
    view.IsPositionVisible=p=>p.X==0;view.RefreshVisibility();uv=mesh.sharedMesh.uv;
    Assert.That(mesh.sharedMesh.vertexCount,Is.EqualTo(4));
    Assert.That(Alpha(Vector2.Lerp(uv[2],uv[3],.5f)),Is.LessThan(.02f),"Hidden neighbour must not keep a joined boundary.");
    var corner=uv[0]+(uv[2]-uv[0])*.125f;
    var side=uv[0]+Vector2.Scale(uv[2]-uv[0],new Vector2(.125f,.5f));
    Assert.That(Alpha(corner),Is.LessThan(Alpha(side)),"Convex corners must round off instead of preserving a square inset.");
    Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Bilinear));
    view.ShowOutline(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillTarget,cells,Color.green);
    var count=root.transform.childCount;for(var i=0;i<5;i++)view.RefreshVisibility();Assert.That(root.transform.childCount,Is.EqualTo(count));
    view.ClearAll();Assert.That(root.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),Is.True);
   }finally{Object.DestroyImmediate(root);}
  }

  [Test] public void TargetPreviewFollowsPointerAndFlipsAtTheScreenEdge()
  {
   var root=new GameObject("preview parent",typeof(RectTransform));var parent=(RectTransform)root.transform;parent.sizeDelta=new Vector2(800,600);
   try
   {
    var preview=CombatTargetPreviewView.Create(parent);var rect=(RectTransform)preview.transform;
    var pointer=RectTransformUtility.WorldToScreenPoint(null,parent.TransformPoint(Vector3.zero));
    preview.Show(pointer,"Preview",Color.white);
    var corners=new Vector3[4];rect.GetWorldCorners(corners);var topLeft=parent.InverseTransformPoint(corners[1]);
    Assert.That(topLeft.x,Is.EqualTo(8).Within(.1f));Assert.That(topLeft.y,Is.EqualTo(-8).Within(.1f));
    pointer=RectTransformUtility.WorldToScreenPoint(null,parent.TransformPoint(new Vector3(380,-280)));
    preview.Show(pointer,"Preview",Color.white);rect.GetWorldCorners(corners);
    foreach(var corner in corners){var p=parent.InverseTransformPoint(corner);Assert.That(p.x,Is.InRange(-400f,400f));Assert.That(p.y,Is.InRange(-300f,300f));}
    Assert.That(rect.anchoredPosition.x,Is.LessThan(380));Assert.That(rect.anchoredPosition.y,Is.GreaterThan(-280));
   }finally{Object.DestroyImmediate(root);}
  }

  [Test] public void ImportedHudTimelinePrefabCreatesProjectedTurnPortraits()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
   var bootstrapObject=new GameObject("Bootstrap");bootstrapObject.SetActive(false);
   try
   {
    var hud=root.GetComponent<CombatHudView>();var so=new SerializedObject(hud);
    Assert.That(so.FindProperty("turnOrderEntryPrefab").objectReferenceValue,Is.Not.Null,"Imported HUD must resolve its external portrait prefab reference.");
    so.FindProperty("sandboxBootstrap").objectReferenceValue=bootstrapObject.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();so.ApplyModifiedPropertiesWithoutUndo();
    var unit=new Godmist.Runtime.Combat.BattleUnit{IsPlayerSide=true,Character=new Godmist.Runtime.Characters.CharacterInstance{LifeState=Godmist.Core.Enums.CharacterLifeState.Alive,Stats=new Godmist.Runtime.Characters.CharacterStats{Speed=new Godmist.Core.Types.Stat(55)}}};
    var session=new Godmist.Runtime.Combat.CombatSession();session.Units.Add(unit);session.TurnParticipants.Add(new Godmist.Runtime.Combat.CombatTurnParticipant(unit));session.ActiveUnit=unit;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(CombatHudView).GetMethod("RefreshTurnOrder",flags).Invoke(hud,new object[]{session});
    var container=(Transform)so.FindProperty("turnOrderContainer").objectReferenceValue;
    Assert.That(container.GetComponentsInChildren<CombatTurnPortraitView>().Length,Is.EqualTo(8));
   }finally{PrefabUtility.UnloadPrefabContents(root);Object.DestroyImmediate(bootstrapObject);}
  }
  [Test] public void DestroyedHudPreviewCanBeClearedByWorldControllerOnSceneExit()
  {
   var root=new GameObject("Tooltip layer",typeof(RectTransform));
   var controllerObject=new GameObject("Targeting",typeof(Godmist.Views.Combat.CombatTargetSelectionController));
   try
   {
    var preview=CombatTargetPreviewView.Create(root.transform);
    var controller=controllerObject.GetComponent<Godmist.Views.Combat.CombatTargetSelectionController>();
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetField("targetPreview",flags).SetValue(controller,preview);
    Object.DestroyImmediate(root);
    Assert.DoesNotThrow(()=>typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetMethod("OnDisable",flags).Invoke(controller,null));
   }finally{if(root!=null)Object.DestroyImmediate(root);Object.DestroyImmediate(controllerObject);}
  }
  [Test] public void TimelineActiveFrameAndHoverAreIndependentAndUnknownPortraitIsAnonymous()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab");
   try
   {
    var view=root.GetComponent<CombatTurnPortraitView>();var so=new SerializedObject(view);
    var frame=(Image)so.FindProperty("activeHighlight").objectReferenceValue;
    var hover=(Graphic)so.FindProperty("hoverHighlight").objectReferenceValue;
    var portrait=(Image)so.FindProperty("portrait").objectReferenceValue;
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance(),IsPlayerSide=true};
    view.Bind(unit,true,highlighted:true);Assert.That(frame.enabled,Is.True);Assert.That(hover.enabled,Is.True);var gold=frame.sprite;
    view.Bind(unit,true);Assert.That(frame.sprite,Is.SameAs(gold));Assert.That(hover.enabled,Is.False);
    view.Bind(unit,false,highlighted:true);Assert.That(frame.enabled,Is.False);Assert.That(hover.enabled,Is.True);
    view.Bind(unit,false,highlighted:true,known:false);Assert.That(hover.enabled,Is.False);Assert.That(portrait.sprite,Is.Null);
    Assert.That(((Component)so.FindProperty("unknownLabel").objectReferenceValue).gameObject.activeSelf,Is.True);
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }

  [Test] public void PartySelectionAndCurrentTurnHaveDifferentAccents()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab");
   try
   {
    var view=root.GetComponent<Godmist.Views.UI.Workspace.CharacterInfoPartyPortraitView>();
    var unit=new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance(),IsPlayerSide=true};
    var so=new SerializedObject(view);var title=(TMPro.TMP_Text)so.FindProperty("nameText").objectReferenceValue;
    view.RefreshPresentation(unit,true,false);var selected=title.color;
    view.RefreshPresentation(unit,false,true);var turn=title.color;
    Assert.That(selected.b,Is.GreaterThan(selected.r));Assert.That(turn.r,Is.GreaterThan(turn.b));
    view.RefreshPresentation(unit,true,true);Assert.That(title.color,Is.EqualTo(turn));
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }

  [TestCase(1,false)] [TestCase(1,true)] [TestCase(64,false)]
  public void RangeOpacitySurvivesRepeatedVisibilityRefresh(int count,bool individualTint)
  {
   var root=new GameObject("range");var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
   try
   {
    var view=root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();var so=new SerializedObject(view);so.FindProperty("feedbackRoot").objectReferenceValue=root.transform;so.FindProperty("feedbackSprite").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();
    var cells=Enumerable.Range(0,count).Select(i=>new Godmist.Runtime.Combat.GridPosition(i,0)).ToArray();
    view.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange,cells,new Color(0,0,1,.6f),individualTint? _=>new Color(0,0,1,.6f):null);
    for(var i=0;i<3;i++)view.RefreshVisibility();
    var rendered=root.GetComponentsInChildren<SpriteRenderer>();
    if(rendered.Length>0)foreach(var item in rendered)Assert.That(item.color.a,Is.EqualTo(.108f).Within(.001f));
    else foreach(var color in root.GetComponentInChildren<MeshFilter>().sharedMesh.colors)Assert.That(color.a,Is.EqualTo(.108f).Within(.005f));
    view.ClearFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange);
    view.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.ChosenSkillTargets,cells,Color.green);
    foreach(var item in root.GetComponentsInChildren<SpriteRenderer>())Assert.That(item.color.a,Is.EqualTo(1));
    view.IsPositionVisible=_=>false;view.RefreshVisibility();Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty);
    Assert.That(root.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),Is.True);
   }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}
  }

  [TestCase("PF_UIW_CombatHud")] [TestCase("PF_UIW_CombatActionPanel")]
  public void SkillCostsStayInTooltipAndIconsUseShapeMask(string name)
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/"+name+".prefab");
   try
   {
    var slot=root.GetComponentInChildren<CombatSkillSlotView>(true);var so=new SerializedObject(slot);
    slot.RefreshAvailability(false,750,false,"",selected:true);slot.RefreshAvailability(true,200,false,"");
    Assert.That(((TMPro.TMP_Text)so.FindProperty("actionCostText").objectReferenceValue).enabled,Is.False);
    var icon=(Image)so.FindProperty("icon").objectReferenceValue;Assert.That(icon.GetComponentInParent<Mask>(true),Is.Not.Null);
    var panel=name=="PF_UIW_CombatHud"?root.transform.Find("BottomPanel"):root.transform;
    Assert.That(panel.GetComponent<Image>().enabled,Is.False);
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }

  [Test] public void DungeonHudTargetPreviewIsAuthoredAndNonInteractive()
  {
   var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
   try
   {
    var template=(CombatTargetPreviewView)new SerializedObject(root.GetComponent<CombatHudView>()).FindProperty("targetPreviewTemplate").objectReferenceValue;
    Assert.That(template,Is.Not.Null);Assert.That(template.gameObject.activeSelf,Is.False);
    Assert.That(template.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
    Assert.That(template.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
    var canvas=template.GetComponent<Canvas>();Assert.That(canvas.overrideSorting,Is.True);Assert.That(canvas.sortingOrder,Is.EqualTo(180));
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
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

  [TestCase(0, 0)]
  [TestCase(50, 25)]
  [TestCase(100, 60)]
  public void FacetedPipKeepsExactFractionalAndPreviewBoundaries(int current, int remaining)
  {
   var go=new GameObject("gem",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(ActionPointPipFill));
   try
   {
    ((RectTransform)go.transform).sizeDelta=new Vector2(16,16);
    var fill=go.GetComponent<ActionPointPipFill>();
    var so=new SerializedObject(fill);so.FindProperty("facetedGem").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
    ActionPointPipPresentation.Apply(go.GetComponent<Image>(),current,0,Color.green,Color.blue,remaining,Color.red);
    using var mesh=new VertexHelper();fill.ModifyMesh(mesh);
    var stream=new System.Collections.Generic.List<UIVertex>();mesh.GetUIVertexStream(stream);
    Assert.That(stream.Count,Is.GreaterThan(0));
    float boundary1=-8+16*remaining/100f, boundary2=-8+16*current/100f;
    foreach(var vertex in stream)
    {
     if(vertex.color.g>0) Assert.That(vertex.position.x,Is.LessThanOrEqualTo(boundary1+.001f));
     if(vertex.color.r>0) Assert.That(vertex.position.x,Is.InRange(boundary1-.001f,boundary2+.001f));
     if(vertex.color.b>0) Assert.That(vertex.position.x,Is.GreaterThanOrEqualTo(boundary2-.001f));
    }
    Assert.That(stream.Any(v=>v.color.r==0 && v.color.g==0 && v.color.b==0),Is.True,"Black gem outline");
    Assert.That(stream.Where(v=>v.color.r+v.color.g+v.color.b>0).Select(v=>v.color).Distinct().Count(),Is.GreaterThan(2),"Facets retain shading");
   }
   finally{Object.DestroyImmediate(go);}
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
    Assert.That(bar.GetComponent<LayoutElement>().preferredHeight,Is.EqualTo(height+grid.padding.vertical));
    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    Assert.That(scroll.viewport.rect.height,Is.EqualTo(height+grid.padding.vertical).Within(.1f));
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

  sealed class ConsumablePopupFixture : System.IDisposable
  {
   public readonly GameObject root;
   public readonly CombatConsumableSelectionView view;
   public readonly Godmist.Views.Combat.CombatTargetSelectionController targeting;
   public readonly Godmist.Runtime.Combat.CombatSession session;
   public readonly RectTransform anchor;
   public readonly Godmist.Runtime.Items.ConsumableItemInstance item;
   public readonly Godmist.Data.Items.ConsumableItemDefinition itemDefinition;
   public ConsumablePopupFixture(int count=2)
   {
    root=new GameObject("Popup fixture",typeof(RectTransform),typeof(Canvas));root.SetActive(false);
    root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
    ((RectTransform)root.transform).sizeDelta=new Vector2(800,600);
    var bootstrap=root.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
    targeting=root.AddComponent<Godmist.Views.Combat.CombatTargetSelectionController>();
    var state=new SerializedObject(targeting);state.FindProperty("sandboxBootstrap").objectReferenceValue=bootstrap;state.ApplyModifiedPropertiesWithoutUndo();
    var definition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
    var unit=new Godmist.Runtime.Combat.BattleUnit {Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition),IsPlayerSide=true,CurrentActionPoints=1300,MaxActionPoints=1300};
    session=new Godmist.Runtime.Combat.CombatSession {ActiveUnit=unit};session.Units.Add(unit);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var driver=(Godmist.Views.Combat.CombatExecutionDriver)typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetMethod("CreateDriver",flags).Invoke(bootstrap,null);
    typeof(Godmist.Views.Combat.CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
    typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetField("focused",flags).SetValue(bootstrap,driver);
    var potion=AssetDatabase.LoadAssetAtPath<Godmist.Data.Items.ConsumableItemDefinition>("Assets/_Project/ScriptableObjects/Items/Consumables/CID_Bomb.asset");
    itemDefinition=Object.Instantiate(potion);
    var itemData=new SerializedObject(itemDefinition);itemData.FindProperty("<IsStackable>k__BackingField").boolValue=true;
    itemData.FindProperty("<MaxCharges>k__BackingField").intValue=1;itemData.ApplyModifiedPropertiesWithoutUndo();
    for(var i=0;i<count;i++) {
     var candidate=new Godmist.Runtime.Items.ConsumableItemInstance(itemDefinition,"popup."+i);
     candidate.RuntimeEffects.Add(new Godmist.Runtime.Items.ConsumableEffectRuntime {EffectType=Godmist.Core.Enums.ConsumableRuntimeEffectType.ApplyBleed,Value=7,DurationTurns=4});
     if(i==0)item=candidate;unit.Character.Inventory.Items.Add(candidate);
    }
    anchor=new GameObject("Items icon",typeof(RectTransform)).GetComponent<RectTransform>();anchor.SetParent(root.transform,false);anchor.sizeDelta=new Vector2(48,48);anchor.anchoredPosition=new Vector2(0,-250);
    var prefab=AssetDatabase.LoadAssetAtPath<CombatConsumableSelectionView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ConsumableSelectionWindow.prefab");
    view=CombatConsumableSelectionView.Create(root.transform,prefab);view.Configure(bootstrap,targeting,null);
    // The authoring command wires the existing item-slot CountLabel; simulate that explicit setup locally.
    var template=(CombatSkillSlotView)new SerializedObject(view).FindProperty("itemTemplate").objectReferenceValue;
    var slotData=new SerializedObject(template);slotData.FindProperty("hotkeyText").objectReferenceValue=template.transform.Find("CountLabel").GetComponent<TMPro.TextMeshProUGUI>();slotData.ApplyModifiedPropertiesWithoutUndo();
    // Keep gameplay components inactive; activate only the authored UI subtree for layout.
    bootstrap.enabled=false;targeting.enabled=false;root.SetActive(true);
   }
   public void Open()=>view.Open(anchor);
   public CombatSkillSlotView[] Slots=>view.GetComponentsInChildren<CombatSkillSlotView>().Where(s=>s.gameObject.activeSelf).ToArray();
   public void Dispose(){Object.DestroyImmediate(root);Object.DestroyImmediate(itemDefinition);}
  }
  [Test] public void ConsumablePopupUsesItemIconCountsAndDoesNotChangeAuthoredPrefab()
  {
   using(var f=new ConsumablePopupFixture()) {
    f.Open();Assert.That(f.view.IsOpen,Is.True);Assert.That(f.Slots.Length,Is.EqualTo(1),"Identical bombs should form one stack");
    var slot=f.Slots.Single();var state=new SerializedObject(slot);
    var icon=(Image)state.FindProperty("icon").objectReferenceValue;
    Assert.That(icon.sprite,Is.SameAs(Godmist.Runtime.Items.ItemIconPresentation.GetIcon(f.item)));Assert.That(icon.enabled,Is.True);
    var count=(TMPro.TMP_Text)state.FindProperty("hotkeyText").objectReferenceValue;Assert.That(count.text,Is.EqualTo("2"));
    Assert.That(count.isActiveAndEnabled,Is.True);
    var cost=(TMPro.TMP_Text)state.FindProperty("actionCostText").objectReferenceValue;Assert.That(cost==null||!cost.gameObject.activeSelf,Is.True);
    Assert.That(f.view.transform.Find("Header").gameObject.activeSelf,Is.False);
    Assert.That(f.view.GetComponentsInChildren<Godmist.Views.UI.Workspace.UIWorkspaceWindowResizeHandle>(),Is.Empty);
    Assert.That(((RectTransform)f.view.transform).rect.height,Is.LessThan(150));
   }
  }
  [Test] public void ConsumablePopupSelectionClosesBeforeTargetingWithoutSpendingCharge()
  {
   using(var f=new ConsumablePopupFixture()) {
    f.Open();var charges=f.item.CurrentCharges;
    Assert.That(f.Slots.Single().TryActivate(),Is.True);
    Assert.That(f.view.IsOpen,Is.False);Assert.That(f.targeting.SelectedSkill,Is.Not.Null);
    Assert.That(f.item.CurrentCharges,Is.EqualTo(charges),"Choosing is not consuming");
   }
  }
  [Test] public void ConsumablePopupUsedTypeCannotBeSelectedAndClosesOnActiveUnitChange()
  {
   using(var f=new ConsumablePopupFixture()) {
    f.session.ActiveUnit.ConsumableUseTypesUsedThisTurn.Add(f.item.ConsumableDefinition.UseType);
    f.Open();Assert.That(f.Slots.Single().TryActivate(),Is.False);Assert.That(f.view.IsOpen,Is.True);
    f.session.ActiveUnit=new Godmist.Runtime.Combat.BattleUnit {IsPlayerSide=true};
    typeof(CombatConsumableSelectionView).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.view,null);
    Assert.That(f.view.IsOpen,Is.False);
   }
  }
  [TestCase(-370,-250)] [TestCase(370,-250)] [TestCase(370,250)]
  public void ConsumablePopupTracksAnchorAndFitsScreen(float x,float y)
  {
   using(var f=new ConsumablePopupFixture()) {
    f.anchor.anchoredPosition=new Vector2(x,y);f.Open();
    var corners=new Vector3[4];((RectTransform)f.view.transform).GetWorldCorners(corners);
    foreach(var corner in corners){var point=f.root.transform.InverseTransformPoint(corner);Assert.That(point.x,Is.InRange(-400.01f,400.01f));Assert.That(point.y,Is.InRange(-300.01f,300.01f));}
    var before=f.view.transform.position;f.anchor.anchoredPosition=Vector2.zero;
    typeof(CombatConsumableSelectionView).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.view,null);
    Assert.That(Vector3.Distance(before,f.view.transform.position),Is.GreaterThan(20));
   }
  }
  [Test] public void ConsumablePopupEmptyStateIsCompactAndReturnScrollIsExcluded()
  {
   using(var f=new ConsumablePopupFixture(0)) {
    var definition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Items.ConsumableItemDefinition>("Assets/_Project/ScriptableObjects/Items/Consumables/CID_ReturnScroll.asset");
    f.session.ActiveUnit.Character.Inventory.Items.Add(new Godmist.Runtime.Items.ConsumableItemInstance(definition));
    f.Open();Assert.That(f.Slots,Is.Empty);Assert.That(f.view.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Brak przedmiotów"),Is.True);
    Assert.That(((RectTransform)f.view.transform).rect.height,Is.EqualTo(64).Within(.01f));
   }
  }
  [TestCase(true,false,false)] [TestCase(false,true,false)] [TestCase(false,false,true)]
  public void ConsumablePopupDismissesWithoutStartingAnAction(bool escape,bool secondary,bool primary)
  {
   using(var f=new ConsumablePopupFixture()) {
    f.Open();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var method=typeof(CombatConsumableSelectionView).GetMethod("HandleDismissInput",flags);
    Assert.That(method.Invoke(f.view,new object[]{escape,secondary,primary,new Vector2(-9999,-9999),null}),Is.True);
    Assert.That(f.view.IsOpen,Is.False);Assert.That(f.targeting.SelectedSkill,Is.Null);
   }
  }
  [Test] public void ConsumablePopupScrollsLongInventoryAndKeepsClicksInsideOpen()
  {
   using(var f=new ConsumablePopupFixture(18)) {
    for(var i=0;i<f.session.ActiveUnit.Character.Inventory.Items.Count;i++)f.session.ActiveUnit.Character.Inventory.Items[i].CustomName="Unique "+i;
    f.Open();Assert.That(f.Slots.Length,Is.EqualTo(18));
    var scroll=f.view.GetComponentInChildren<ScrollRect>();Assert.That(scroll.vertical,Is.True);
    Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var center=RectTransformUtility.WorldToScreenPoint(null,f.anchor.position);
    Assert.That(typeof(CombatConsumableSelectionView).GetMethod("HandleDismissInput",flags).Invoke(f.view,new object[]{false,false,true,center,null}),Is.False);
    Assert.That(f.view.IsOpen,Is.True);
   }
  }
  [Test] public void ConsumablePopupBlocksLowActionPointsAndDismissalClaimsTheSameFrame()
  {
   var events=new GameObject("Popover input",typeof(UnityEngine.EventSystems.EventSystem));
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var eventSystem=events.GetComponent<UnityEngine.EventSystems.EventSystem>();
   // EventSystem is not ExecuteAlways, so EditMode must invoke its registration lifecycle.
   typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnEnable",flags).Invoke(eventSystem,null);
   UnityEngine.EventSystems.EventSystem.current=eventSystem;
   try {using(var f=new ConsumablePopupFixture()) {
    f.session.ActiveUnit.CurrentActionPoints=0;f.Open();Assert.That(f.Slots.Single().TryActivate(),Is.False);
    Assert.That(typeof(CombatSkillSlotView).GetField("unavailableReason",flags).GetValue(f.Slots.Single()),Is.EqualTo("Za mało punktów akcji."));
    typeof(CombatConsumableSelectionView).GetMethod("HandleDismissInput",flags).Invoke(f.view,new object[]{true,false,false,Vector2.zero,null});
    Assert.That(f.view.IsOpen,Is.False);
    Assert.That(Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedContextMenu(),Is.True,
     "Inactive dismissed popup must still own this input frame; selected="+UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name+
     "; frame="+Time.frameCount+"; dismissed="+typeof(CombatConsumableSelectionView).GetField("dismissedFrame",flags).GetValue(f.view));
   }} finally {typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnDisable",flags).Invoke(eventSystem,null);Object.DestroyImmediate(events);}
  }
  [TestCase(6, 1f)] [TestCase(11, .75f)] [TestCase(18, 1f)]
  public void ConsumablePopupFitsSlotArtworkInsideOneRectangularMask(int count, float scale)
  {
   using(var f=new ConsumablePopupFixture(count)) {
    f.root.transform.localScale=Vector3.one*scale;
    for(var i=0;i<count;i++)f.session.ActiveUnit.Character.Inventory.Items[i].CustomName="Distinct "+i;
    f.Open();Canvas.ForceUpdateCanvases();
    var scroll=f.view.GetComponentInChildren<ScrollRect>();var viewport=scroll.viewport;
    Assert.That(viewport.GetComponent<RectMask2D>().enabled,Is.True);
    Assert.That(viewport.GetComponent<Mask>().enabled,Is.False);
    Assert.That(viewport.GetComponent<Image>().enabled,Is.False);
    Assert.That(scroll.transform.Find("Border").gameObject.activeSelf,Is.False);
    var columns=scroll.content.GetComponent<GridLayoutGroup>().constraintCount;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var corners=new Vector3[4];
    void CheckArtwork(CombatSkillSlotView slot) {
     var icon=(Image)typeof(CombatSkillSlotView).GetField("icon",flags).GetValue(slot);
     Assert.That(icon.rectTransform.rect.width,Is.GreaterThan(0));Assert.That(icon.rectTransform.rect.height,Is.GreaterThan(0));
     ((RectTransform)slot.transform.Find("Border")).GetWorldCorners(corners);
     foreach(var point in corners) {
      var local=viewport.InverseTransformPoint(point);
      Assert.That(local.x,Is.InRange(viewport.rect.xMin+3.99f,viewport.rect.xMax-3.99f));
      Assert.That(local.y,Is.InRange(viewport.rect.yMin+3.99f,viewport.rect.yMax-3.99f));
     }
    }
    foreach(var slot in f.Slots.Take(columns*4))CheckArtwork(slot);
    if(scroll.vertical) {
     scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
     var rows=Mathf.CeilToInt(count/(float)columns);
     foreach(var slot in f.Slots.Skip((rows-4)*columns))CheckArtwork(slot);
    }
   }
  }
  static Godmist.Runtime.Combat.BattleUnit AddSharedConsumableActor(ConsumablePopupFixture f)
  {
   var definition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
   var actor=new Godmist.Runtime.Combat.BattleUnit { Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition),
    IsPlayerSide=true,CurrentActionPoints=1300,MaxActionPoints=1300,Position=new Godmist.Runtime.Combat.GridPosition(2,2) };
   actor.Character.Inventory.Items.Clear();f.session.Units.Add(actor);f.session.ActiveUnit=actor;return actor;
  }

  [Test] public void ConsumablePopupSharesStacksAndRefreshesWhenAnotherInventoryChanges()
  {
   using(var f=new ConsumablePopupFixture()) {
    var owner=f.session.ActiveUnit;
    var actor=AddSharedConsumableActor(f);
    f.Open();Assert.That(f.Slots,Has.Length.EqualTo(1));
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var count=(TMPro.TMP_Text)typeof(CombatSkillSlotView).GetField("hotkeyText",flags).GetValue(f.Slots.Single());
    Assert.That(count.text,Is.EqualTo("2"));
    Assert.That(actor.Character.Inventory.Items,Is.Empty);
    // Move one identical bomb to the active caster: the shared stack remains intact.
    var moved=owner.Character.Inventory.Items.Last();owner.Character.Inventory.Items.Remove(moved);actor.Character.Inventory.Items.Add(moved);
    typeof(CombatConsumableSelectionView).GetMethod("Update",flags).Invoke(f.view,null);
    Assert.That(f.Slots,Has.Length.EqualTo(1));
    Assert.That(((TMPro.TMP_Text)typeof(CombatSkillSlotView).GetField("hotkeyText",flags).GetValue(f.Slots.Single())).text,Is.EqualTo("2"));
    owner.Character.Inventory.Items.Remove(f.item);
    typeof(CombatConsumableSelectionView).GetMethod("Update",flags).Invoke(f.view,null);
    Assert.That(((TMPro.TMP_Text)typeof(CombatSkillSlotView).GetField("hotkeyText",flags).GetValue(f.Slots.Single())).text,Is.EqualTo("1"));
    Assert.That(f.Slots.Single().TryActivate(),Is.True);Assert.That(f.view.IsOpen,Is.False);
    Assert.That(f.targeting.SelectedSkill,Is.Not.Null);
    var action=f.targeting.SelectedSkill;f.targeting.CancelTargetSelection();Object.DestroyImmediate(action);
    Assert.That(((Godmist.Runtime.Items.ConsumableItemInstance)moved).CurrentCharges,Is.EqualTo(1));
   }
  }

  [TestCase(1)] [TestCase(3)]
  public void ConsumablePopupTeammatePotionExecutesWithCasterApAndCarrierCharges(int charges)
  {
   using(var f=new ConsumablePopupFixture(1)) {
    var owner=f.session.ActiveUnit;
    var actor=AddSharedConsumableActor(f);actor.Character.CurrentHealth=1;
    var itemData=new SerializedObject(f.itemDefinition);
    itemData.FindProperty("<UseType>k__BackingField").enumValueIndex=(int)Godmist.Core.Enums.ConsumableUseType.Potion;
    itemData.FindProperty("<MaxCharges>k__BackingField").intValue=charges;itemData.ApplyModifiedPropertiesWithoutUndo();
    f.item.CurrentCharges=charges;f.item.RuntimeEffects.Clear();
    f.item.RuntimeEffects.Add(new Godmist.Runtime.Items.ConsumableEffectRuntime { EffectType=Godmist.Core.Enums.ConsumableRuntimeEffectType.RestoreHealth,Value=.25f });
    f.session.Grid=new Godmist.Runtime.Combat.BattleGrid(8,8);
    Godmist.Systems.Combat.CombatTurnSystem.RefreshMaxActionPoints(f.session,actor,true);
    Godmist.Systems.Combat.CombatTurnSystem.RefreshMaxActionPoints(f.session,owner,true);
    var actorAp=actor.CurrentActionPoints;var ownerAp=owner.CurrentActionPoints;
    var cost=Godmist.Systems.Combat.CombatActionCostCalculator.CalculatePercentageCost(actor.MaxActionPoints,Godmist.Systems.Items.ConsumableRuntimeActionBuilder.ActionCostPercent);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var bootstrap=(Godmist.Views.Combat.CombatSandboxBootstrap)typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetField("sandboxBootstrap",flags).GetValue(f.targeting);
    var driver=(Godmist.Views.Combat.CombatExecutionDriver)typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetField("focused",flags).GetValue(bootstrap);
    typeof(Godmist.Views.Combat.CombatExecutionDriver).GetField("skillExecutionSystem",flags).SetValue(driver,new Godmist.Systems.Combat.SkillExecutionSystem());
    typeof(Godmist.Views.Combat.CombatExecutionDriver).GetField("targetingService",flags).SetValue(driver,new Godmist.Systems.Combat.CombatTargetingService());
    f.Open();Assert.That(f.Slots.Single().TryActivate(),Is.True);Assert.That(f.view.IsOpen,Is.False);
    var action=f.targeting.SelectedSkill;Assert.That(action,Is.Not.Null);
    try {
     var positions=(System.Collections.Generic.Dictionary<int,Godmist.Runtime.Combat.GridPosition?>)typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetField("selectedPositionsByModeIndex",flags).GetValue(f.targeting);
     positions[0]=actor.Position;
     typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetMethod("TryCompleteSelection",flags).Invoke(f.targeting,null);
     Assert.That(actor.Character.CurrentHealth,Is.GreaterThan(1));
     Assert.That(actor.CurrentActionPoints,Is.EqualTo(actorAp-cost));Assert.That(owner.CurrentActionPoints,Is.EqualTo(ownerAp));
     Assert.That(f.item.CurrentCharges,Is.EqualTo(charges-1));
     Assert.That(owner.Character.Inventory.Items.Contains(f.item),Is.EqualTo(charges>1));
     Assert.That(actor.Character.Inventory.Items,Is.Empty);
     Assert.That(actor.ConsumableUseTypesUsedThisTurn,Does.Contain(Godmist.Core.Enums.ConsumableUseType.Potion));
     Assert.That(owner.ConsumableUseTypesUsedThisTurn,Is.Empty);
     Assert.That(f.targeting.IsSelectingTarget,Is.False);
     f.targeting.BeginConsumableTargetSelection(f.item);Assert.That(f.targeting.IsSelectingTarget,Is.False);
    } finally {Object.DestroyImmediate(action);}
   }
  }

  [TestCase(false)] [TestCase(true)]
  public void ConsumablePopupRejectsStaleOwnershipAndTurnBeforeExecuting(bool changeTurn)
  {
   using(var f=new ConsumablePopupFixture()) {
    var owner=f.session.ActiveUnit;var actor=AddSharedConsumableActor(f);
    f.Open();f.Slots.Single().TryActivate();var action=f.targeting.SelectedSkill;
    Assert.That(action,Is.Not.Null);
    try {
     if(changeTurn)f.session.ActiveUnit=owner;else owner.Character.Inventory.Items.Remove(f.item);
     var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
     var result=(bool)typeof(Godmist.Views.Combat.CombatTargetSelectionController).GetMethod("TryExecuteSelectedAction",flags).Invoke(f.targeting,new object[]{null,null});
     Assert.That(result,Is.False);Assert.That(f.item.CurrentCharges,Is.EqualTo(1));
     Assert.That(actor.CurrentActionPoints,Is.EqualTo(1300));Assert.That(owner.ConsumableUseTypesUsedThisTurn,Is.Empty);
     Assert.That(actor.ConsumableUseTypesUsedThisTurn,Is.Empty);
     f.targeting.CancelTargetSelection();
    } finally {Object.DestroyImmediate(action);}
   }
  }

 }
}
