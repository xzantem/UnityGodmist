using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class SkeletonSlayerPresentationTests
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        sealed class FeedbackForwarder
        {
            readonly CombatExecutionDriver driver;
            public FeedbackForwarder(CombatExecutionDriver driver){this.driver=driver;}
            public void Handle(CombatFeedbackEvent e)
            {
                typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback",Private).Invoke(driver,new object[]{e});
            }
        }
        [UnityTest]
        public IEnumerator SixSouthFramesAreProtectedThenRestoreOriginalLogicalFacing()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Slayer transformation test");CombatExecutionDriver driver=null;SkeletonSlayerBossSystem owner=null;
            try
            {
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.SlayerPath);
                var boss=new BattleUnit{Character=new CharacterInstanceFactory().Create(definition,definition.ClassData,10),Position=new GridPosition(2,2)};
                var go=new GameObject("Boss");go.transform.SetParent(root.transform);var renderer=go.AddComponent<SpriteRenderer>();var view=go.AddComponent<CharacterView>();
                var data=new SerializedObject(view);data.FindProperty("bodyRenderer").objectReferenceValue=renderer;data.ApplyModifiedPropertiesWithoutUndo();view.Bind(boss);
                var spawner=root.AddComponent<CombatUnitViewSpawner>();spawner.enabled=false;
                ((Dictionary<BattleUnit,CharacterView>)typeof(CombatUnitViewSpawner).GetField("views",Private).GetValue(spawner)).Add(boss,view);
                var host=root.AddComponent<CombatSandboxBootstrap>();host.enabled=false;
                driver=new CombatExecutionDriver(host,spawner,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){boss},ActiveUnit=boss};
                typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
                var bus=new CombatFeedbackBus();var forwarder=new FeedbackForwarder(driver);bus.Published+=forwarder.Handle;
                owner=new SkeletonSlayerBossSystem(session,new CombatDamageSystem(),new StatusEffectSystem(),feedback:bus);
                Assert.That(CombatPoseResolver.TryResolveAction(definition.Id,"crushing-blow",Vector2.left,out var oldPose),Is.True);view.PlayCombatPose(oldPose,Vector2.left,3);
                boss.Character.CurrentHealth=(int)(boss.Character.Stats.MaxHealth.GetValue()*.5f);owner.CheckPhases();
                Assert.That(view.IsTransforming,Is.False);Assert.That(boss.Character.Definition,Is.SameAs(definition));owner.BeginTurn(boss);
                Assert.That(renderer.sprite,Is.SameAs(definition.TransformationFrames[0]));Assert.That(view.HasCombatPose,Is.False);
                view.PlayCombatPose(oldPose,Vector2.right,3);var seen=new HashSet<Sprite>();var finish=Time.time+1.35f;
                while(Time.time<finish)
                {
                    if(view.IsTransforming){seen.Add(renderer.sprite);Assert.That(view.FacingDirection,Is.EqualTo(Vector2.left));Assert.That(driver.IsActionInProgress,Is.True);}
                    yield return null;
                }
                Assert.That(seen.Count,Is.EqualTo(6));Assert.That(view.IsTransforming,Is.False);Assert.That(driver.IsActionInProgress,Is.False);
                Assert.That(renderer.sprite.name,Does.StartWith("skeleton-slayer-p2-w-idle"));
                owner.CheckPhases();yield return null;Assert.That(view.IsTransforming,Is.False);
            }
            finally{owner?.Dispose();driver?.Dispose();Object.Destroy(root);}
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator DeathCancelsTransformationAndResolvesTheDedicatedCorpse()
        {
            yield return new EnterPlayMode();var root=new GameObject("Slayer corpse precedence");
            try
            {
                var d=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.SlayerPhasePath);
                var p1=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.SlayerPath);
                var boss=new BattleUnit{Character=new CharacterInstanceFactory().Create(d,d.ClassData,10)};
                var renderer=root.AddComponent<SpriteRenderer>();var view=root.AddComponent<CharacterView>();var data=new SerializedObject(view);
                data.FindProperty("bodyRenderer").objectReferenceValue=renderer;data.ApplyModifiedPropertiesWithoutUndo();view.Bind(boss);
                view.PlayTransformation(p1.TransformationFrames,5);Assert.That(view.IsTransforming,Is.True);
                boss.Character.CurrentHealth=0;boss.Character.LifeState=Godmist.Core.Enums.CharacterLifeState.Dead;yield return null;
                Assert.That(view.IsTransforming,Is.False);
                // CharacterView is removed on death; the existing corpse owner uses this resolver.
                Assert.That(CharacterAnimationResolver.TryResolveCorpse(boss.Character.Definition.Id,out var corpse),Is.True);
                Assert.That(corpse.name,Does.Contain("corpse"));Assert.That(corpse.rect.size,Is.EqualTo(new Vector2(192,192)));
                Assert.That(renderer.transform.localRotation,Is.EqualTo(Quaternion.identity));
            }
            finally{Object.Destroy(root);}
            yield return new ExitPlayMode();
        }
    }
}
