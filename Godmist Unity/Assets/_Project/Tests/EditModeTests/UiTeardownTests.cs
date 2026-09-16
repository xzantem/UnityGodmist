using System.Reflection;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;
public class UiTeardownTests
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Make<T>(string name) where T:Component { var go=new GameObject(name);go.SetActive(false);return go.AddComponent<T>(); }
 static void Assign(object owner,string field,object value)=>owner.GetType().GetField(field,Flags).SetValue(owner,value);
 static void Cleanup(object owner,string callback)=>owner.GetType().GetMethod(callback,Flags).Invoke(owner,null);
 [TestCase(true)] [TestCase(false)]
 public void ObstacleTooltipCleanupToleratesEitherDestructionOrder(bool viewFirst)
 {
  var owner=Make<DungeonTacticalTooltip>("owner");var menu=Make<PartyRailMenuView>("menu");var tooltip=Make<UIWorkspaceTooltipView>("tooltip");
  Assign(owner,"obstacleMenu",menu);Assign(owner,"tooltip",tooltip);
  try {
   if(viewFirst){Object.DestroyImmediate(menu.gameObject);Object.DestroyImmediate(tooltip.gameObject);}
   Assert.DoesNotThrow(()=>Cleanup(owner,"OnDisable"));
   Assert.DoesNotThrow(()=>Cleanup(owner,"OnDisable"));
  } finally {Object.DestroyImmediate(owner.gameObject);if(menu!=null)Object.DestroyImmediate(menu.gameObject);if(tooltip!=null)Object.DestroyImmediate(tooltip.gameObject);}
 }
 [TestCase(true)] [TestCase(false)]
 public void InspectCleanupToleratesEitherDestructionOrder(bool viewFirst)
 {
  var owner=Make<CombatUnitInspectController>("owner");var panel=Make<CombatUnitInspectPanel>("panel");
  Assign(owner,"inspectPanel",panel);
  try {if(viewFirst)Object.DestroyImmediate(panel.gameObject);Assert.DoesNotThrow(()=>Cleanup(owner,"OnDisable"));}
  finally{Object.DestroyImmediate(owner.gameObject);if(panel!=null)Object.DestroyImmediate(panel.gameObject);}
 }
 [TestCase(true)] [TestCase(false)]
 public void BootstrapCleanupToleratesEitherDestructionOrder(bool viewFirst)
 {
  var owner=Make<CombatSandboxBootstrap>("owner");var feedback=Make<CombatFloatingFeedbackController>("feedback");
  feedback.Bind(new Godmist.Runtime.Combat.CombatFeedbackBus());Assign(owner,"floatingFeedback",feedback);
  try {if(viewFirst)Object.DestroyImmediate(feedback.gameObject);Assert.DoesNotThrow(()=>Cleanup(owner,"OnDestroy"));Assert.DoesNotThrow(()=>Cleanup(owner,"OnDestroy"));}
  finally {Object.DestroyImmediate(owner.gameObject);if(feedback!=null)Object.DestroyImmediate(feedback.gameObject);}
 }
}