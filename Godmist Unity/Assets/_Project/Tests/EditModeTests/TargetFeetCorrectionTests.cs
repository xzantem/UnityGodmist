using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class TargetFeetCorrectionTests
    {
        [Test]
        public void PointerFindsFeetAcrossCellBoundaryButKeepsGridTargetIdentity()
        {
            var unit=new BattleUnit{Position=new GridPosition(2,0)};var feet=new Vector2(1.55f,.4f);
            var pointer=new Vector2(1.12f,.4f);
            Assert.That(Mathf.RoundToInt(pointer.x),Is.EqualTo(1));
            var found=CombatTargetSelectionController.FindUnitAtFeet(new[]{unit},u=>feet,pointer,1);
            Assert.That(found,Is.SameAs(unit));Assert.That(found.Position,Is.EqualTo(new GridPosition(2,0)));
            Assert.That(CombatTargetSelectionController.FindUnitAtFeet(new[]{unit},u=>feet,new Vector2(.9f,.4f),1),Is.Null);
        }
        [Test]
        public void HiddenUnitIsNotPickedAndOverlappingMarkersUseNearestFeet()
        {
            var hidden=new BattleUnit();var a=new BattleUnit();var b=new BattleUnit();
            var feet=new Dictionary<BattleUnit,Vector2>{{a,new Vector2(.1f,0)},{b,new Vector2(.6f,0)}};
            var found=CombatTargetSelectionController.FindUnitAtFeet(new[]{hidden,a,b},u=>feet.TryGetValue(u,out var p)?p:null,new Vector2(.55f,0),1);
            Assert.That(found,Is.SameAs(b));
            Assert.That(CombatTargetSelectionController.FindUnitAtFeet(new[]{hidden},u=>null,Vector2.zero,1),Is.Null);
        }
        [TestCase(RangeShape.Square)][TestCase(RangeShape.Radial)][TestCase(RangeShape.Ring)]
        public void FeetResolverCannotChangeRangeGeometryOrColors(RangeShape shape)
        {
            var root=new GameObject("Original range comparison");
            try {
                var view=root.AddComponent<CombatGridFeedbackRenderer>();
                typeof(CombatGridFeedbackRenderer).GetField("feedbackRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,root.transform);
                view.SetGridMapping(Vector2.zero,1);var range=new SkillRangeData(shape,shape==RangeShape.Ring?2:0,4);
                var cells=new List<GridPosition>();
                for(var y=-5;y<=5;y++)for(var x=-5;x<=5;x++) {
                    var p=new GridPosition(x,y);if(CombatTargetingService.IsPositionWithinRange(new GridPosition(0,0),p,range))cells.Add(p);
                }
                void Draw(){view.ShowOutline(CombatGridFeedbackLayer.SkillRange,cells,Color.cyan);view.ShowFill(CombatGridFeedbackLayer.SkillRange,cells,new Color(.2f,.4f,.8f,.24f));}
                Draw();var meshes=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
                var vertices=meshes.Select(m=>m.vertices).ToArray();var colors=meshes.Select(m=>m.colors32).ToArray();
                view.ResolveExplorationMarker=p=>new Vector2(p.X+.37f,p.Y+.24f);Draw();
                for(var i=0;i<meshes.Length;i++) {
                    Assert.That(meshes[i].vertices,Is.EqualTo(vertices[i]));Assert.That(meshes[i].colors32,Is.EqualTo(colors[i]));
                }
                Assert.That(root.GetComponentsInChildren<Transform>().Any(t=>t.name.Contains("Continuous exploration")),Is.False);
                Assert.That(range.Shape,Is.EqualTo(shape));Assert.That(range.MaxRange,Is.EqualTo(4));
            } finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void RenderRestoredHighlights()
        {
            new ExplorationPresentationPolishTests().RenderOriginalRangeWithFeetTarget();
            File.Copy("../LocalHandoffs/exploration-presentation-20261026/exploration-targeting.png","../LocalHandoffs/target-feet-correction-20261026/targeting-restored.png",true);
        }
    }
}
