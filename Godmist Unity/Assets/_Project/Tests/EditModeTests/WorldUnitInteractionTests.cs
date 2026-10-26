using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
 public class WorldUnitInteractionTests
 {
  static BattleUnit InvisibleUnit(bool player)
  {
   var unit=new BattleUnit {IsPlayerSide=player,Character=new CharacterInstance(),Position=new GridPosition(2,2)};
   unit.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
    EffectId=Godmist.Core.Constants.StatusEffectIds.Invisibility,RemainingDuration=2,
    Lifetime=PassiveEffectLifetime.Timed});
   return unit;
  }

  [TestCase(false)] [TestCase(true)]
  public void InvisibilityGatesPresentationAndInspectionWithoutDisablingAllies(bool player)
  {
   var root=new GameObject("invisibility gates");
   try {
    var unit=InvisibleUnit(player);
    var spawner=root.AddComponent<CombatUnitViewSpawner>();
    var bootstrap=root.AddComponent<CombatSandboxBootstrap>();
    Assert.That(spawner.IsUnitVisible(unit),Is.EqualTo(player));
    Assert.That(spawner.IsUnitKnown(unit),Is.EqualTo(player));
    Assert.That(bootstrap.IsUnitVisible(unit),Is.EqualTo(player));
    Assert.That(bootstrap.IsUnitKnown(unit),Is.EqualTo(player));
    var inspect=root.AddComponent<CombatUnitInspectController>();var session=new CombatSession();session.Units.Add(unit);
    inspect.IsPositionVisible=_=>true;
    Assert.That(Call(inspect,"FindUnitAtPosition",session,unit.Position),Is.EqualTo(player?unit:null));
    unit.Character.PassiveEffects.Clear();
    Assert.That(spawner.IsUnitVisible(unit)&&spawner.IsUnitKnown(unit)&&bootstrap.IsUnitKnown(unit),Is.True);
    Assert.That(Call(inspect,"FindUnitAtPosition",session,unit.Position),Is.SameAs(unit));
   } finally {Object.DestroyImmediate(root);}
  }

  [TestCase(false)] [TestCase(true)]
  public void InvisibilityHidesStandaloneBodyAndPreservesFogAfterReveal(bool player)
  {
   var root=new GameObject("standalone invisibility");root.SetActive(false);
   var definition=ScriptableObject.CreateInstance<Godmist.Data.Characters.CharacterDefinition>();
   try {
    var body=root.AddComponent<SpriteRenderer>();
    var view=root.AddComponent<Godmist.Views.Characters.CharacterView>();
    var data=new SerializedObject(view);data.FindProperty("bodyRenderer").objectReferenceValue=body;data.ApplyModifiedPropertiesWithoutUndo();
    var unit=InvisibleUnit(player);unit.Character.Definition=definition;view.Bind(unit);
    view.SetElevationVisible(true);
    Assert.That(body.forceRenderingOff,Is.EqualTo(!player));
    Assert.That(body.color.a,Is.EqualTo(player ? .45f : 1f).Within(.001f));
    unit.Character.PassiveEffects.Clear();view.Refresh();
    Assert.That(body.forceRenderingOff,Is.False);Assert.That(body.color.a,Is.EqualTo(1f));
    view.SetElevationVisible(false);view.Refresh();Call(view,"LateUpdate");
    Assert.That(body.forceRenderingOff,Is.True,"Reveal must never override fog/elevation visibility.");
   } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(definition);}
  }

  [TestCase(false)] [TestCase(true)]
  public void InvisibilityPersistsAcrossBorrowedCombatReleaseAndRestoresAlpha(bool player)
  {
   var root=new GameObject("exploration invisibility");root.SetActive(false);
   try {
    var body=root.AddComponent<SpriteRenderer>();body.color=new Color(.6f,.5f,.4f,.8f);
    var view=root.AddComponent<ExplorationCharacterView>();
    var data=new SerializedObject(view);data.FindProperty("bodyRenderer").objectReferenceValue=body;data.ApplyModifiedPropertiesWithoutUndo();
    root.SetActive(true);
    var unit=InvisibleUnit(player);view.Bind(unit.Character);if(!player)view.SetEnemyInstanceId("enemy");
    view.BindCombat(unit);Call(view,"LateUpdate");
    Assert.That(body.forceRenderingOff,Is.EqualTo(!player));
    Assert.That(view.IsVisibleForInspection,Is.EqualTo(player));
    Assert.That(body.color.a,Is.EqualTo(player ? .36f : .8f).Within(.001f));
    for(var i=0;i<5;i++)Call(view,"LateUpdate");
    Assert.That(body.color.a,Is.EqualTo(player ? .36f : .8f).Within(.001f),"Alpha must not compound every frame.");
    view.ReleaseCombat();Call(view,"LateUpdate");
    Assert.That(body.forceRenderingOff,Is.EqualTo(!player));
    unit.Character.PassiveEffects.Clear();Call(view,"LateUpdate");
    Assert.That(body.forceRenderingOff,Is.False);Assert.That(view.IsVisibleForInspection,Is.True);
    Assert.That(body.color.a,Is.EqualTo(.8f).Within(.001f));
   } finally {Object.DestroyImmediate(root);}
  }

  [Test]
  public void InvisibleEnemyCannotLeaveFootMarkerOrOutlineEvenWithStaleHover()
  {
   var root=new GameObject("hidden accents");var texture=new Texture2D(2,2);
   var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
   try {
    var body=root.AddComponent<SpriteRenderer>();body.sprite=sprite;
    var visual=root.AddComponent<CombatUnitVisualStateView>();var unit=InvisibleUnit(false);
    unit.Character.PassiveEffects.Clear();visual.Refresh(unit,true,true,true);
    Assert.That(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),Is.True);
    unit.Character.PassiveEffects.Add(InvisibleUnit(false).Character.PassiveEffects[0]);
    visual.Refresh(unit,true,true,true);Call(visual,"LateUpdate");
    Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r!=body).All(r=>!r.enabled),Is.True);
    Assert.That(root.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),Is.True);
   } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}
  }

  [Test]
  public void GroundAreaHitsInvisibleEnemyWithoutExposingItAsEligibleTarget()
  {
   var caster=InvisibleUnit(true);caster.Character.PassiveEffects.Clear();caster.Position=new GridPosition(1,2);
   var enemy=InvisibleUnit(false);var session=new CombatSession {Grid=new BattleGrid(6,6)};
   session.Units.Add(caster);session.Units.Add(enemy);
   var mode=new Godmist.Data.Skills.SkillTargetModeData();
   void Set(string name,object value)=>typeof(Godmist.Data.Skills.SkillTargetModeData).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mode,value);
   Set("absoluteRange",new Godmist.Data.Skills.SkillRangeData(RangeShape.Square,0,4));
   var service=new Godmist.Systems.Combat.CombatTargetingService();
   Assert.That(service.GetTargets(session,caster,mode,enemy.Position),Is.Empty);
   Set("targetType",TargetType.AreaAroundTile);Set("relativeRange",new Godmist.Data.Skills.SkillRangeData(RangeShape.Square,0,1));
   Assert.That(service.GetTargets(session,caster,mode,enemy.Position),Does.Contain(enemy));
   Assert.That(service.GetEligibleTargetPositions(session,caster,mode),Has.No.Member(enemy.Position));
   Assert.That(service.GetSelectablePositions(session,caster,mode),Does.Contain(enemy.Position),"Terrain remains selectable without revealing its occupant.");
  }
  static object Call(object owner,string method,params object[] args) => owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);
  [Test]
  public void NpcHoverPreservesBodyAndTracksAnimatedSpriteWithoutDuplicateOutlines()
  {
   var root=new GameObject("NPC hover"); root.SetActive(false);
   var texture=new Texture2D(2,2);
   var first=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
   var second=Sprite.Create(texture,new Rect(0,0,1,2),Vector2.one*.5f);
   try {
    var body=new GameObject("Body").AddComponent<SpriteRenderer>();body.transform.SetParent(root.transform);
    body.sprite=first;body.color=new Color(.6f,.5f,.4f,.7f);
    var npc=root.AddComponent<Godmist.Views.NPC.NpcView>();
    var state=new SerializedObject(npc);state.FindProperty("bodyRenderer").objectReferenceValue=body;state.ApplyModifiedPropertiesWithoutUndo();
    root.SetActive(true);var original=body.color;
    npc.SetInspectionHighlight(true);Call(npc,"LateUpdate");
    var visual=body.GetComponent<CombatUnitVisualStateView>();Assert.That(visual,Is.Not.Null);
    var outlines=body.GetComponentsInChildren<SpriteRenderer>().Where(x=>x!=body).ToArray();
    Assert.That(outlines.Length,Is.EqualTo(8));Assert.That(outlines.All(x=>x.enabled&&x.sprite==first),Is.True);
    Assert.That(body.color,Is.EqualTo(original),"Hover must not tint the NPC sprite.");
    body.sprite=second;body.flipX=true;body.sortingOrder=42;Call(npc,"LateUpdate");
    Assert.That(outlines.All(x=>x.sprite==second&&x.flipX&&x.sortingOrder==41),Is.True);
    npc.SetInspectionHighlight(false);Assert.That(outlines.All(x=>!x.enabled),Is.True);
    npc.SetInspectionHighlight(true);Call(npc,"LateUpdate");
    Assert.That(body.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(9));
    body.forceRenderingOff=true;Call(npc,"LateUpdate");Assert.That(outlines.All(x=>!x.enabled),Is.True);
    body.forceRenderingOff=false;Call(npc,"LateUpdate");
    Call(npc,"OnDisable");Assert.That(outlines.All(x=>!x.enabled),Is.True);
    Assert.That(body.color,Is.EqualTo(original));
   } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(texture);}
  }
  [Test]
  public void CombatPickingRejectsFogHiddenAndDeadUnitsButAllowsDownedUnits()
  {
   var go=new GameObject("inspect");var owner=go.AddComponent<CombatUnitInspectController>();
   try {
    var unit=new BattleUnit { Character=new CharacterInstance(), Position=new GridPosition(2,3) };
    var session=new CombatSession();session.Units.Add(unit);
    owner.IsPositionVisible=_=>false;
    Assert.That(Call(owner,"FindUnitAtPosition",session,unit.Position),Is.Null);
    owner.IsPositionVisible=_=>true;
    Assert.That(Call(owner,"FindUnitAtPosition",session,unit.Position),Is.SameAs(unit));
    unit.Character.LifeState=CharacterLifeState.Dead;
    Assert.That(Call(owner,"FindUnitAtPosition",session,unit.Position),Is.Null);
    unit.Character.LifeState=CharacterLifeState.Downed;
    Assert.That(Call(owner,"FindUnitAtPosition",session,unit.Position),Is.SameAs(unit));
   } finally {Object.DestroyImmediate(go);}
  }
  [Test]
  public void ExplorationInspectionRejectsInactiveAndDeadCharacters()
  {
   var go=new GameObject("view");var view=go.AddComponent<ExplorationCharacterView>();
   var character=new CharacterInstance();
   try {
    view.Bind(character);Assert.That(view.IsVisibleForInspection,Is.True);
    go.SetActive(false);Assert.That(view.IsVisibleForInspection,Is.False);
    go.SetActive(true);character.LifeState=CharacterLifeState.Dead;Assert.That(view.IsVisibleForInspection,Is.False);
   } finally {Object.DestroyImmediate(go);}
  }
  [TestCase("City/Arungard")]
  [TestCase("Sandbox/DungeonSandbox")]
  [TestCase("Sandbox/CombatSandbox")]
  public void SceneUsesOneExplicitlyWiredWorldInteractionOwner(string path)
  {
   var setup=EditorSceneManager.GetSceneManagerSetup();
   try {
    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+path+".unity");
    var owners=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CombatUnitInspectController>(true)).ToArray();
    Assert.That(owners.Length,Is.EqualTo(1));var owner=owners[0];var data=new SerializedObject(owner);
    foreach(var field in new[]{"worldCamera","contextMenu","worldTooltip","inspectWindowPrefab"})
     Assert.That(data.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
    if(path=="City/Arungard") {
     var entry=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonEntranceView>(true)).Single();
     var entryData=new SerializedObject(entry);
     foreach(var field in new[]{"partyController","selector","hoverOutline","tooltip"})
      Assert.That(entryData.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
     Assert.That(entry.GetComponent<Collider2D>().enabled,Is.True);
     var label=entry.transform.Find("ExpeditionsLabel");
     Assert.That(label,Is.Not.Null);
     if(entry.UsesGateExit) {
      Assert.That(label.gameObject.activeSelf,Is.False);
      Assert.That(entry.GetComponent<SpriteRenderer>().enabled,Is.False);
      Assert.That(entryData.FindProperty("gateArrow").objectReferenceValue,Is.Not.Null);
     } else Assert.That(label.gameObject.activeSelf,Is.True);
    }
    var menu=(PartyRailMenuView)data.FindProperty("contextMenu").objectReferenceValue;
    Assert.That(menu.gameObject.activeSelf,Is.False);
    if(path=="Sandbox/CombatSandbox")
     Assert.That(data.FindProperty("targetSelectionController").objectReferenceValue,Is.Not.Null);
    else {
     var party=(ExplorationPartyController)data.FindProperty("explorationParty").objectReferenceValue;
     Assert.That(party,Is.Not.Null);
     Assert.That(new SerializedObject(party).FindProperty("unitInteractions").objectReferenceValue,Is.SameAs(owner));
    }
   } finally {if(setup.Any(s=>s.isLoaded && s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test]
  public void NpcApproachTargetsReachableInteractionRangeAndCanBeCancelled()
  {
   var root=new GameObject("npc-route");
   try {
    var party=root.AddComponent<ExplorationPartyController>();
    var actor=new GameObject("actor").AddComponent<ExplorationCharacterView>();actor.transform.SetParent(root.transform);
    actor.Bind(new CharacterInstance { CharacterId="a" });
    var session=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
    session.PlayerParty.TryAddActiveMember("a");session.PlayerParty.TrySetActiveLeader("a");party.Initialize(session,new[]{actor});
    var npc=new GameObject("npc").AddComponent<Godmist.Views.NPC.NpcView>();npc.transform.SetParent(root.transform);npc.transform.position=new Vector2(4,0);
    var collider=npc.gameObject.AddComponent<BoxCollider2D>();collider.size=Vector2.one;Physics2D.SyncTransforms();
    Assert.That(party.RequestNpcInteraction(npc),Is.True);
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var goal=(Vector2)typeof(ExplorationPartyController).GetField("movementDestination",flags).GetValue(party);
    Assert.That(Vector2.Distance(goal,npc.transform.position),Is.LessThan(npc.InteractionRadius));
    Assert.That(actor.HasClearMovementSegment(goal,goal),Is.True);
    Assert.That(typeof(ExplorationPartyController).GetField("pendingNpc",flags).GetValue(party),Is.SameAs(npc));
    party.CancelMovement();
    Assert.That(typeof(ExplorationPartyController).GetField("pendingNpc",flags).GetValue(party),Is.Null);
    Assert.That(typeof(ExplorationPartyController).GetField("movementDestination",flags).GetValue(party),Is.Null);
   } finally {Object.DestroyImmediate(root);Physics2D.SyncTransforms();}
  }
  [Test]
  public void EntranceApproachTargetsInteractionRangeAndCanBeCancelled()
  {
   var root=new GameObject("npc-route");
   try {
    var party=root.AddComponent<ExplorationPartyController>();
    var actor=new GameObject("actor").AddComponent<ExplorationCharacterView>();actor.transform.SetParent(root.transform);
    actor.Bind(new CharacterInstance { CharacterId="a" });
    var session=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
    session.PlayerParty.TryAddActiveMember("a");session.PlayerParty.TrySetActiveLeader("a");party.Initialize(session,new[]{actor});
    var npc=new GameObject("npc").AddComponent<Godmist.Views.Dungeons.DungeonEntranceView>();npc.transform.SetParent(root.transform);npc.transform.position=new Vector2(4,0);
    var collider=npc.gameObject.AddComponent<BoxCollider2D>();collider.size=Vector2.one;Physics2D.SyncTransforms();
    Assert.That(party.RequestEntranceInteraction(npc),Is.True);
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var goal=(Vector2)typeof(ExplorationPartyController).GetField("movementDestination",flags).GetValue(party);
    Assert.That(Vector2.Distance(goal,npc.transform.position),Is.LessThan(npc.InteractionRadius));
    Assert.That(actor.HasClearMovementSegment(goal,goal),Is.True);
    Assert.That(typeof(ExplorationPartyController).GetField("pendingEntrance",flags).GetValue(party),Is.SameAs(npc));
    party.CancelMovement();
    Assert.That(typeof(ExplorationPartyController).GetField("pendingEntrance",flags).GetValue(party),Is.Null);
    Assert.That(typeof(ExplorationPartyController).GetField("movementDestination",flags).GetValue(party),Is.Null);
   } finally {Object.DestroyImmediate(root);Physics2D.SyncTransforms();}
  }
  [Test]
  public void AutomaticEntranceDoesNotRepeatUntilLeaderLeavesRange()
  {
   var root=new GameObject("automatic-entrance");
   try {
    var party=root.AddComponent<ExplorationPartyController>();
    var actor=new GameObject("actor").AddComponent<ExplorationCharacterView>();actor.transform.SetParent(root.transform);
    actor.Bind(new CharacterInstance { CharacterId="a" });
    var session=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
    session.PlayerParty.TryAddActiveMember("a");session.PlayerParty.TrySetActiveLeader("a");party.Initialize(session,new[]{actor});
    var entrance=new GameObject("entrance").AddComponent<Godmist.Views.Dungeons.DungeonEntranceView>();entrance.transform.SetParent(root.transform);
    var data=new SerializedObject(entrance);data.FindProperty("partyController").objectReferenceValue=party;data.ApplyModifiedPropertiesWithoutUndo();
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var poll=entrance.GetType().GetMethod("CanAutomaticallyOpen",flags);
    Assert.That(poll.Invoke(entrance,null),Is.True);
    entrance.GetType().GetField("openedDuringVisit",flags).SetValue(entrance,true);
    Assert.That(poll.Invoke(entrance,null),Is.False);
    actor.transform.position=Vector2.right*(entrance.InteractionRadius+.1f);
    Assert.That(poll.Invoke(entrance,null),Is.False);
    actor.transform.position=Vector2.zero;
    Assert.That(poll.Invoke(entrance,null),Is.False,"Boundary jitter must not rearm entry");
    actor.transform.position=Vector2.right*(entrance.InteractionRadius+.3f);
    Assert.That(poll.Invoke(entrance,null),Is.False);
    actor.transform.position=Vector2.zero;
    Assert.That(poll.Invoke(entrance,null),Is.True,"Returning after leaving allows another opening");
   } finally {Object.DestroyImmediate(root);}
  }
  [TestCase(0f,-1.3f,false)]
  [TestCase(0f,-.01f,false)]
  [TestCase(0f,0f,true)]
  [TestCase(0f,.3f,true)]
  [TestCase(1.11f,.3f,false)]
  [TestCase(-1.11f,.3f,false)]
  public void GateRequiresCrossingExitLineInsidePassage(float x,float y,bool expected)
  {
   var root=new GameObject("gate-test");
   try {
    var entrance=CreateGate(root,out var party,out var actor);
    entrance.transform.position=new Vector3(16,24.7f,0);entrance.transform.localScale=new Vector3(2,1,1);
    actor.transform.position=entrance.GateExitPosition+new Vector2(x,y);
    Assert.That(entrance.GateExitPosition.y,Is.EqualTo(26f).Within(.001f));
    Assert.That(Call(entrance,"CanAutomaticallyOpen"),Is.EqualTo(expected));
    Assert.That(entrance.CanInteract(),Is.EqualTo(expected));
   } finally {Object.DestroyImmediate(root);}
  }
  [Test]
  public void GateOnlyRearmsAfterReturningToCityAndApproachCrossesExitLine()
  {
   var root=new GameObject("gate-test");
   try {
    var entrance=CreateGate(root,out var party,out var actor);
    Assert.That(party.RequestEntranceInteraction(entrance),Is.True);
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var destination=(Vector2)typeof(ExplorationPartyController).GetField("movementDestination",flags).GetValue(party);
    Assert.That(destination,Is.EqualTo(entrance.GateApproachPosition));
    Assert.That(destination.y,Is.GreaterThan(entrance.GateExitPosition.y));
    entrance.GetType().GetField("openedDuringVisit",flags).SetValue(entrance,true);
    foreach(var y in new[]{.1f,5f,-.1f,-.49f,.1f}) {
     actor.transform.position=entrance.GateExitPosition+Vector2.up*y;
     Assert.That(Call(entrance,"CanAutomaticallyOpen"),Is.False,"Outside travel and boundary jitter must not rearm");
     Assert.That(entrance.CanInteract(),Is.False,"E and click must not bypass the visit latch");
    }
    actor.transform.position=entrance.GateExitPosition-Vector2.up*.6f;
    Assert.That(Call(entrance,"CanAutomaticallyOpen"),Is.False);
    actor.transform.position=entrance.GateExitPosition+Vector2.up*.1f;
    Assert.That(Call(entrance,"CanAutomaticallyOpen"),Is.True);
    party.Session.TrySetMode(GameModeState.Combat);
    Assert.That(Call(entrance,"CanAutomaticallyOpen"),Is.False);
   } finally {Object.DestroyImmediate(root);}
  }
  [Test]
  public void GateArrowUsesTransparentHardAlphaAtNativePixelDensity()
  {
   var root=new GameObject("gate-test");
   try {
    var entrance=CreateGate(root,out _,out _);
    var arrow=new GameObject("arrow").AddComponent<SpriteRenderer>();arrow.transform.SetParent(root.transform);
    var data=new SerializedObject(entrance);data.FindProperty("gateArrow").objectReferenceValue=arrow;data.ApplyModifiedPropertiesWithoutUndo();
    Call(entrance,"CreateGateArrow");
    Assert.That(arrow.sprite.pixelsPerUnit,Is.EqualTo(32));
    var texture=arrow.sprite.texture;Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));
    var pixels=texture.GetPixels32();
    Assert.That(pixels.Any(p=>p.a==0),Is.True);
    Assert.That(pixels.Any(p=>p.a==255),Is.True);
    Assert.That(pixels.All(p=>p.a==0||p.a==255),Is.True);
    Assert.That(texture.GetPixel(0,0).a,Is.Zero);
    Assert.That(texture.GetPixel(12,26).a,Is.EqualTo(1));
   } finally {Object.DestroyImmediate(root);}
  }
  static Godmist.Views.Dungeons.DungeonEntranceView CreateGate(GameObject root,out ExplorationPartyController party,out ExplorationCharacterView actor)
  {
   party=root.AddComponent<ExplorationPartyController>();
   actor=new GameObject("actor").AddComponent<ExplorationCharacterView>();actor.transform.SetParent(root.transform);
   actor.Bind(new CharacterInstance {CharacterId="gate-actor"});
   var session=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
   session.PlayerParty.TryAddActiveMember("gate-actor");session.PlayerParty.TrySetActiveLeader("gate-actor");party.Initialize(session,new[]{actor});
   var gate=new GameObject("entrance");gate.transform.SetParent(root.transform);
   var entrance=gate.AddComponent<Godmist.Views.Dungeons.DungeonEntranceView>();
   var data=new SerializedObject(entrance);data.FindProperty("partyController").objectReferenceValue=party;
   data.FindProperty("useGateExit").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
   return entrance;
  }
  [Test]
  public void ContextMenuHeaderIsAuthoredTextWithoutButtonDecoration()
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyContextMenu.prefab");
   var data=new SerializedObject(prefab.GetComponent<PartyRailMenuView>());
   var header=data.FindProperty("header").objectReferenceValue as Component;
   Assert.That(header,Is.Not.Null);
   Assert.That(header.GetComponent<UnityEngine.UI.Button>(),Is.Null);
   Assert.That(header.GetComponent<UnityEngine.UI.Image>(),Is.Null);
  }
  [Test]
  public void DestroyedContextMenuDoesNotBreakRepeatedCleanup()
  {
   var go=new GameObject("owner");var owner=go.AddComponent<CombatUnitInspectController>();
   var menuGo=new GameObject("menu");var menu=menuGo.AddComponent<PartyRailMenuView>();
   var data=new SerializedObject(owner);data.FindProperty("contextMenu").objectReferenceValue=menu;data.ApplyModifiedPropertiesWithoutUndo();
   Object.DestroyImmediate(menuGo);
   try {Assert.DoesNotThrow(()=>Call(owner,"ClearWorldInteraction"));Assert.DoesNotThrow(()=>Call(owner,"ClearWorldInteraction"));}
   finally {Object.DestroyImmediate(go);}
  }
 }
}
