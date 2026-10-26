using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ExplorationPresentationPolishTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Private).SetValue(o,v);
        [Test,Explicit("Import approved hedge artwork and rebuild tracked HUD through Editor API.")]
        public void ApplyApprovedPresentation()=>ExplorationPresentationSetup.Apply();
        [TestCase(DamageValueSource.MinAttack,"20")][TestCase(DamageValueSource.MaxAttack,"40")]
        [TestCase(DamageValueSource.AverageAttack,"30")][TestCase(DamageValueSource.AttackRange,"20–40")]
        public void SkillDescriptionAndActualTargetPreviewShareCollapsedDamageText(DamageValueSource source,string expected)
        {
            var skill=ScriptableObject.CreateInstance<SkillData>();var root=new GameObject("Damage formatter");
            try {
                var effect=new SkillEffectData();Set(effect,"effectType",EffectType.DealDamage);Set(effect,"damageValueSource",source);Set(effect,"damageMultiplier",1f);
                Set(skill,"effects",new List<SkillEffectData>{effect});
                var actor=new BattleUnit {Character=new CharacterInstance {CharacterId="caster",WeaponAccuracy=100,
                    Stats=new CharacterStats{MinAttack=new Stat(20),MaxAttack=new Stat(40),CritChance=new Stat(0),CritDamage=new Stat(1.5f)}}};
                var target=new BattleUnit {Character=new CharacterInstance {CharacterId="target",Stats=new CharacterStats()}};
                Assert.That(CombatSkillPreviewCalculator.CalculateDamageRange(actor,skill).ToDisplayString(),Is.EqualTo(expected));
                Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(actor,target,skill).Damage.ToDisplayString(),Is.EqualTo(expected));
                var view=root.AddComponent<UIWorkspaceTooltipView>();
                var text=(string)typeof(UIWorkspaceTooltipView).GetMethod("DescribeDamage",Private).Invoke(view,new object[]{skill,actor,effect});
                Assert.That(text,Does.Contain(expected));if(source!=DamageValueSource.AttackRange)Assert.That(text,Does.Not.Contain("–"));
            } finally {Object.DestroyImmediate(skill);Object.DestroyImmediate(root);}
        }
        [TestCase(CombatGridFeedbackLayer.SkillTarget)][TestCase(CombatGridFeedbackLayer.ChosenSkillTargets)]
        public void OpeningMarkersFollowActualFeetThenReturnToCombatGrid(CombatGridFeedbackLayer layer)
        {
            var root=new GameObject("Free target marker");
            try {
                var view=root.AddComponent<CombatGridFeedbackRenderer>();Set(view,"feedbackRoot",root.transform);view.SetGridMapping(Vector2.zero,1);
                var cell=new GridPosition(2,3);var feet=new Vector2(2.37f,3.42f);view.ResolveExplorationMarker=p=>p==cell?feet:null;
                view.ShowOutline(layer,new[]{cell},Color.green);
                var renderer=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.enabled);
                Assert.That((Vector2)renderer.bounds.center,Is.EqualTo(feet));
                feet+=new Vector2(.1f,-.13f);typeof(CombatGridFeedbackRenderer).GetMethod("LateUpdate",Private).Invoke(view,null);
                Assert.That((Vector2)renderer.bounds.center,Is.EqualTo(feet));
                view.ResolveExplorationMarker=null;view.ClearOutline(layer);view.ShowOutline(CombatGridFeedbackLayer.SkillTarget,new[]{cell},Color.green);
                renderer=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.enabled&&r.gameObject.name.Contains("corner"));
                Assert.That((Vector2)renderer.bounds.center,Is.EqualTo(new Vector2(2,3)));
            } finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void NativeHedgeAtlasAndGodmistSectionAreAssigned()
        {
            var sprites=AssetDatabase.LoadAllAssetsAtPath(ExplorationPresentationSetup.HedgePath).OfType<Sprite>().ToArray();
            Assert.That(sprites.Length,Is.EqualTo(16));Assert.That(sprites.All(s=>s.pixelsPerUnit==32),Is.True);
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(CombatEconomyPolishSetup.TrackerPath);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(root.transform.Find("TrackedQuestSection").gameObject),Is.Not.Null);
            Assert.That(root.transform.Find("TrackedQuestSection/Border").GetComponent<UnityEngine.UI.Image>().sprite,Is.Not.Null);
        }
        [TestCase(4,10)][TestCase(5,6)][TestCase(6,5)][TestCase(7,9)]
        [TestCase(8,11)][TestCase(9,14)][TestCase(10,7)][TestCase(11,13)][TestCase(12,15)]
        public void HedgeJunctionPixelsCannotGrowAnUnconnectedArm(int index,int arms)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                image.LoadImage(File.ReadAllBytes(ExplorationPresentationSetup.HedgePath));
                var x=index%4*32;var y=(3-index/4)*32;
                bool Any(int bit)=>Enumerable.Range(0,32).Any(i=>image.GetPixel(
                    x+(bit==1?0:bit==2?31:i),y+(bit==4?0:bit==8?31:i)).a>.5f);
                foreach(var bit in new[]{1,2,4,8})Assert.That(Any(bit),Is.EqualTo((arms&bit)!=0),"Physical hedge arm "+bit);
            } finally {Object.DestroyImmediate(image);}
        }
        [Test]
        public void RenderNativeTracker()
        {
            new CombatEconomyPolishTests().RenderTrackedQuestWithProductionFontAndPrefab();
            File.Copy("../LocalHandoffs/combat-economy-ui-20261026/tracked-quest-hud.png",Path.Combine(ExplorationPresentationSetup.Output,"tracked-quest-hud.png"),true);
        }
        [Test]
        public void RenderOriginalRangeWithFeetTarget()
        {
            var root=new GameObject("Exploration targeting render");var cameraObject=new GameObject("Exploration targeting camera",typeof(Camera));
            var texture=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGBA32,false);var active=RenderTexture.active;
            try {
                var feedback=root.AddComponent<CombatGridFeedbackRenderer>();feedback.SetGridMapping(Vector2.zero,1);
                var center=new Vector2(.3f,.22f);var targetFeet=new Vector2(1.62f,.36f);var targetCell=new GridPosition(2,0);
                feedback.ResolveExplorationMarker=p=>p==targetCell?targetFeet:null;
                var cells=new List<GridPosition>();
                var range=new SkillRangeData(RangeShape.Square,0,2);
                for(var y=-2;y<=2;y++)for(var x=-2;x<=2;x++) {
                    var cell=new GridPosition(x,y);
                    if(CombatTargetingService.IsPositionWithinRange(new GridPosition(0,0),cell,range))cells.Add(cell);
                }
                feedback.ShowOutline(CombatGridFeedbackLayer.SkillRange,cells,new Color(.7f,.85f,1f,.85f));
                feedback.ShowFill(CombatGridFeedbackLayer.SkillRange,cells,new Color(.7f,.85f,1f,.24f));
                feedback.ShowOutline(CombatGridFeedbackLayer.SkillTarget,new[]{targetCell},new Color(1,.7f,.2f));
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png");
                foreach(var feet in new[]{center,targetFeet}) {
                    var actor=new GameObject("Unsnapped character",typeof(SpriteRenderer));actor.transform.SetParent(root.transform,false);
                    var view=actor.GetComponent<SpriteRenderer>();view.sprite=sprite;view.sortingOrder=500;
                    actor.transform.position=new Vector3(feet.x,feet.y-sprite.bounds.min.y,0);
                }
                foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3.3f;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.065f,.04f);
                camera.transform.position=new Vector3(.3f,.22f,-10);camera.targetTexture=texture;texture.Create();camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(ExplorationPresentationSetup.Output,"exploration-targeting.png"),image.EncodeToPNG());
                Assert.That(image.GetPixels32().Count(c=>c.r>150&&c.g>80),Is.GreaterThan(200));
                camera.targetTexture=null;
            } finally {RenderTexture.active=active;Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(texture);Object.DestroyImmediate(image);}
        }
    }
}
