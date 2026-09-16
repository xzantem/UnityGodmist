using System.Reflection;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
 public class ExplorationZoomTests
 {
  GameObject root; Camera camera; ExplorationCameraFollow exploration; CombatCameraFramer combat;
  [SetUp] public void Setup(){root=new GameObject("zoom");camera=root.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;root.transform.position=new Vector3(0,0,-10);exploration=root.AddComponent<ExplorationCameraFollow>();combat=root.AddComponent<CombatCameraFramer>();foreach(var owner in new MonoBehaviour[]{exploration,combat})owner.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owner,null);}
  [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);
  [Test] public void ScrollIsSmoothAndAccumulatesBeforeNextFrame(){exploration.Zoom(1);exploration.Zoom(1);Assert.That(camera.orthographicSize,Is.EqualTo(5));exploration.TickZoom(.02f);Assert.That(camera.orthographicSize,Is.LessThan(5).And.GreaterThan(CombatCameraFramer.ScrollZoom(5,2)));exploration.TickZoom(10);Assert.That(camera.orthographicSize,Is.EqualTo(CombatCameraFramer.ScrollZoom(5,2)).Within(.001f));}
  [TestCase(100,2.5f)] [TestCase(-100,10f)] public void ExplorationUsesCombatLimits(float steps,float expected){exploration.Zoom(steps);exploration.TickZoom(10);Assert.That(camera.orthographicSize,Is.EqualTo(expected));}
  [Test] public void CombatHandoffPreservesCurrentZoomAndStopsOldAnimation(){exploration.Zoom(2);exploration.TickZoom(.02f);var before=camera.orthographicSize;exploration.SetCombatSuspended(true);exploration.TickZoom(10);Assert.That(camera.orthographicSize,Is.EqualTo(before));camera.orthographicSize=7;exploration.SetCombatSuspended(false);exploration.TickZoom(10);Assert.That(camera.orthographicSize,Is.EqualTo(7));}
  [Test] public void FirstSeamlessCombatKeepsZoomAndWheelMatchesExploration(){camera.orthographicSize=4;var session=new CombatSession{IsSeamless=true};var bounds=new Bounds(Vector3.zero,new Vector3(100,100,0));combat.Focus(session,bounds,bounds,_=>Vector3.zero);combat.TickTransition(10);Assert.That(camera.orthographicSize,Is.EqualTo(4));combat.Zoom(2);combat.TickTransition(10);var combatZoom=camera.orthographicSize;combat.Focus(null,default,default,null);camera.orthographicSize=4;exploration.Zoom(2);exploration.TickZoom(10);Assert.That(camera.orthographicSize,Is.EqualTo(combatZoom).Within(.001f));}
 }
}
