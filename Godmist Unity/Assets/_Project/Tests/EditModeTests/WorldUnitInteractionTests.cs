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
  static object Call(object owner,string method,params object[] args) => owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);
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
