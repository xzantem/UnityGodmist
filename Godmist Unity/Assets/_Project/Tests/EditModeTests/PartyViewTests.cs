using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
 public sealed class PartyViewTests
 {
  [Test] public void AuthoredColumnsReuseProductionPresentersWithOneWindowOwner()
  {
   var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyView.prefab");Assert.That(root,Is.Not.Null);
   Assert.That(root.GetComponentsInChildren<UIWorkspaceWindow>(true).Length,Is.EqualTo(1));
   Assert.That(root.GetComponentsInChildren<ControllerNavigationScope>(true).Length,Is.EqualTo(1));
   var column=root.GetComponentInChildren<PartyViewColumnView>(true);Assert.That(column,Is.Not.Null);
   foreach(var name in new[]{"equipment","stats","skills","development","portrait","identity","vitals","individualButton","blockedReason"})Assert.That(new SerializedObject(column).FindProperty(name).objectReferenceValue,Is.Not.Null,name);
   Assert.That(new SerializedObject(column).FindProperty("modeButtons").arraySize,Is.EqualTo(4));
  }
  [Test] public void PlayableTreeRowsFitTheMinimumColumnWidth()
  {
   var trees=AssetDatabase.FindAssets("t:SkillTreeDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Godmist.Data.Skills.SkillTreeDefinition>).Where(t=>t!=null);
   foreach(var tree in trees)foreach(var tier in tree.Nodes.Where(n=>n!=null).GroupBy(n=>n.Tier))
    Assert.That(tier.Count()*72+(tier.Count()-1)*8+16+28,Is.LessThanOrEqualTo(PartyViewWindowView.MinimumColumnWidth),tree.name+" tier "+tier.Key);
  }
  [Test] public void ProductionRailsReferencePartyViewWithoutReplacingIndividualCard()
  {
   foreach(var name in new[]{"PF_UIW_PartyRail.prefab","PF_UIW_CombatPartyRail.prefab"})
   {
    var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/"+name);
    foreach(var rail in root.GetComponentsInChildren<CharacterInfoPartyRailView>(true))
    {
     var s=new SerializedObject(rail);Assert.That(s.FindProperty("partyViewPrefab").objectReferenceValue,Is.Not.Null,name);
     Assert.That(s.FindProperty("characterInfoWindowPrefab").objectReferenceValue,Is.Not.Null,name);
    }
   }
  }
 }
}
