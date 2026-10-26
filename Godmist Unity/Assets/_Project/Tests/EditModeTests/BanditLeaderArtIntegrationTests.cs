using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class BanditLeaderArtIntegrationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        sealed class FeedbackForwarder
        {
            readonly CombatExecutionDriver driver;
            public FeedbackForwarder(CombatExecutionDriver value)=>driver=value;
            public void Handle(CombatFeedbackEvent e)=>typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback",Private).Invoke(driver,new object[]{e});
        }
        static T Asset<T>(string path)where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static CharacterDefinition Definition=>Asset<CharacterDefinition>(VS16CatacombsBossSetup.BanditPath);
        static readonly Vector2[] Directions={Vector2.down,new(1,-1),Vector2.right,Vector2.one,Vector2.up,new(-1,1),Vector2.left,new(-1,-1)};
        static BattleUnit Unit(CharacterDefinition d,int x=2,int y=2,bool player=false)=>new(){Character=new CharacterInstanceFactory().Create(d,d.ClassData,10),Position=new GridPosition(x,y),IsPlayerSide=player};
        static BanditLeaderBossSystem Owner(CombatSession s,CombatFeedbackBus bus)=>new(s,new CombatDamageSystem(),new StatusEffectSystem(),new CombatRecoverySystem(),new SummonedObjectSystem(),feedback:bus);

        [Test]
        public void EveryImportedFileIsExactAndEveryDefinitionBindingUsesItsNativeProfile()
        {
            var config=JsonUtility.FromJson<DeadKingArtSetup.Config>(File.ReadAllText(BanditLeaderArtSetup.ConfigPath));
            Assert.That(config.imports.Length,Is.EqualTo(271));
            foreach(var entry in config.imports)
            {
                using var hash=SHA256.Create();Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(entry.path))).Replace("-","").ToLowerInvariant(),Is.EqualTo(entry.sha256),entry.path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(entry.path);
                Assert.That(importer.textureType,Is.EqualTo(entry.texture?TextureImporterType.Default:TextureImporterType.Sprite),entry.path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.GetPlatformTextureSettings("Standalone").format,Is.EqualTo(TextureImporterFormat.RGBA32));
                if(!entry.texture)
                {var sprite=Asset<Sprite>(entry.path);Assert.That(sprite,Is.Not.Null);Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.pivot.x,Is.EqualTo(sprite.rect.width*entry.pivotX).Within(.001));Assert.That(sprite.pivot.y,Is.EqualTo(sprite.rect.height*entry.pivotY).Within(.001));}
            }
            foreach(var entry in config.bindings)
            {
                var data=new SerializedObject(AssetDatabase.LoadMainAssetAtPath(entry.asset));var field=data.FindProperty("<"+entry.field+">k__BackingField")??data.FindProperty(entry.field);
                Assert.That(field,Is.Not.Null,entry.asset+entry.field);if(field.isArray)Assert.That(field.arraySize,Is.EqualTo(entry.sprites.Length));
                for(var i=0;i<entry.sprites.Length;i++)Assert.That((field.isArray?field.GetArrayElementAtIndex(i):field).objectReferenceValue,Is.SameAs(Asset<Sprite>(entry.sprites[i])));
            }
            var glossary=Asset<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            foreach(var entry in config.glossary){Assert.That(glossary.TryGet(entry.id,out var found),Is.True);Assert.That(found.Icon,Is.SameAs(Asset<Sprite>(entry.sprite)));}
            foreach(var d in new[]{Definition,Definition.SecondPhaseDefinition})
            {Assert.That(d.PortraitSprite.rect.size,Is.EqualTo(new Vector2(128,128)));Assert.That(d.StartingSkills.All(k=>k.Icon!=null && k.Icon.rect.size==new Vector2(32,32)),Is.True);Assert.That(d.DeadSprite,Is.Not.Null);}
        }

        [Test]
        public void BothPhasesResolveAllDirectionsRegisteredEffectsAndGroundedCorpses()
        {
            foreach(var definition in new[]{Definition,Definition.SecondPhaseDefinition})foreach(var direction in Directions)
            {
                foreach(var state in new[]{CharacterAnimationState.Idle,CharacterAnimationState.Walk})
                {
                    Assert.That(CharacterAnimationResolver.TryResolveCharacter(definition.Id,direction,state,out var clip),Is.True);
                    Assert.That(clip.FrameCount,Is.EqualTo(4));Assert.That(clip.Frames.Select(s=>s.GetInstanceID()).Distinct().Count(),Is.EqualTo(4));
                    foreach(var frame in clip.Frames){Assert.That(frame.rect.size,Is.EqualTo(new Vector2(96,96)));Assert.That(frame.pivot,Is.EqualTo(new Vector2(48,8)));Assert.That(frame.pixelsPerUnit,Is.EqualTo(32));}
                    Assert.That(clip.GetFrameDuration(0),Is.EqualTo(state==CharacterAnimationState.Idle?.3f:.18f).Within(.0001));
                }
                foreach(var skill in definition.StartingSkills)
                {
                    Assert.That(CombatPoseResolver.TryResolveSkill(definition.Id,skill.SkillId,direction,skill.AttackType,false,out var pose),Is.True,skill.SkillId);
                    Assert.That(pose.Actor.pivot,Is.EqualTo(new Vector2(64,24)));Assert.That(pose.Actor.rect.size,Is.EqualTo(new Vector2(128,128)));
                    Assert.That(CombatPoseResolver.TryGetVisibleBounds(pose.Actor,out var bounds),Is.True);Assert.That(bounds.size.x,Is.GreaterThan(0));
                    if(skill.SkillId==BanditLeaderBossSystem.OrderId){Assert.That(pose.TargetHeadEffects.Length,Is.EqualTo(1));Assert.That(pose.TargetHeadDuration,Is.EqualTo(.18f));Assert.That(pose.TargetHeadOffsets[0],Is.EqualTo(new Vector2(0,2.03125f)));}
                    else{Assert.That(pose.Effects.Length,Is.EqualTo(1));Assert.That(pose.EffectDurations[0],Is.EqualTo(.18f));}
                    if(skill.SkillId==BanditLeaderBossSystem.FireId)
                    {var d=CharacterAnimationResolver.DirectionFor(direction);Assert.That(pose.EffectOrders[0],Is.EqualTo(d is "ne"or"n"or"nw"?-1:1));}
                }
                foreach(var reaction in new[]{"hit","dodge"})Assert.That(CombatPoseResolver.TryResolveAction(definition.Id,reaction,direction,out _),Is.True);
                Assert.That(CharacterAnimationResolver.TryResolveCorpse(definition.Id,out var corpse),Is.True);Assert.That(corpse.rect.size,Is.EqualTo(new Vector2(128,128)));Assert.That(corpse.pivot,Is.EqualTo(new Vector2(64,64)));
            }
            Assert.That(Definition.TransformationFrames.Count,Is.EqualTo(6));Assert.That(Definition.TransformationFramesPerSecond,Is.EqualTo(5));
            Assert.That(Definition.SecondPhaseDefinition.TransformationFrames,Is.Empty);
        }

        [Test]
        public void RegisteredFlashSharesFullActorPoseDeadline()
        {
            Assert.That(CombatPoseResolver.TryResolveAction(Definition.SecondPhaseDefinition.Id,"fire",Vector2.down,out var pose),Is.True);
            var root=new GameObject("Bandit pose flash lifetime");
            try
            {
                var renderer=root.AddComponent<SpriteRenderer>();using var player=new CombatPosePlayer(renderer);player.Play(pose,2);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r=>r!=renderer && r.enabled),Is.EqualTo(1));
                Assert.That(player.Tick(Time.time+.19f),Is.True);Assert.That(renderer.sprite,Is.SameAs(pose.Actor));
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r=>r!=renderer && r.enabled),Is.EqualTo(1));
                Assert.That(player.Tick(Time.time+2.01f),Is.False);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r=>r!=renderer && r.enabled),Is.Zero);
                player.Clear();Assert.That(player.IsPlaying,Is.False);
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void OrderUsesMeasuredTargetHeadAnchorRatherThanTransparentCanvasTop()
        {
            Assert.That(CombatPoseResolver.TryResolveAction(Definition.Id,"order",Vector2.down,out var pose),Is.True);
            var root=new GameObject("Registered order head effect");
            try
            {
                root.transform.position=new Vector3(3,4,0);var body=root.AddComponent<SpriteRenderer>();body.sprite=pose.Actor;
                var player=CombatTargetEffectPlayer.Play(body,Unit(Definition),pose.TargetHeadEffects,pose.TargetHeadDuration,pose.TargetHeadOffsets);
                Assert.That(player.GetComponentsInChildren<SpriteRenderer>().Single().transform.position,Is.EqualTo(root.transform.position+new Vector3(0,2.03125f,0)));
                player.Clear();
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void ConcealCueIsOneShotAndNeverReplacesTheTransitionNotification()
        {
            var boss=Unit(Definition);var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){boss}};var bus=new CombatFeedbackBus();var seen=new List<CombatFeedbackEvent>();bus.Published+=seen.Add;
            using var owner=Owner(session,bus);owner.BeginTurn(boss);
            Assert.That(seen.Count(e=>e.Type==CombatFeedbackType.ProcTriggered),Is.EqualTo(1));Assert.That(seen.Any(e=>e.Type==CombatFeedbackType.VisualCue),Is.False);
            boss.Character.PassiveEffects.RemoveAll(e=>e.EffectId==Godmist.Core.Constants.StatusEffectIds.Invisibility);owner.BeginTurn(boss);
            Assert.That(seen.Count(e=>e.Type==CombatFeedbackType.VisualCue),Is.EqualTo(1));owner.BeginTurn(boss);Assert.That(seen.Count(e=>e.Type==CombatFeedbackType.VisualCue),Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GuardDeathsQueueSixFrameTransitionForOwnTurnAndPreserveLogicalFacing()
        {
            yield return new EnterPlayMode();var root=new GameObject("Bandit transition test");CombatExecutionDriver driver=null;BanditLeaderBossSystem owner=null;
            try
            {
                var boss=Unit(Definition);var guard=Unit(Asset<CharacterDefinition>(VS16CatacombsBossSetup.BanditRoot+"CD_Forest_BanditThug.asset"),4,4);
                var go=new GameObject("Boss");go.transform.SetParent(root.transform);var renderer=go.AddComponent<SpriteRenderer>();var view=go.AddComponent<CharacterView>();
                var data=new SerializedObject(view);data.FindProperty("bodyRenderer").objectReferenceValue=renderer;data.ApplyModifiedPropertiesWithoutUndo();view.Bind(boss);
                var spawner=root.AddComponent<CombatUnitViewSpawner>();spawner.enabled=false;((Dictionary<BattleUnit,CharacterView>)typeof(CombatUnitViewSpawner).GetField("views",Private).GetValue(spawner)).Add(boss,view);
                var host=root.AddComponent<CombatSandboxBootstrap>();host.enabled=false;driver=new CombatExecutionDriver(host,spawner,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){boss,guard},ActiveUnit=boss};typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
                var bus=new CombatFeedbackBus();var forwarder=new FeedbackForwarder(driver);bus.Published+=forwarder.Handle;owner=Owner(session,bus);
                Assert.That(CombatPoseResolver.TryResolveAction(Definition.Id,"hit",Vector2.left,out var oldPose),Is.True);view.PlayCombatPose(oldPose,Vector2.left,3);
                boss.Character.CurrentHealth=1;owner.CheckPhases();Assert.That(boss.BanditLeaderState.PhasePending,Is.False);
                guard.Character.CurrentHealth=0;guard.Character.LifeState=CharacterLifeState.Dead;owner.CheckPhases();Assert.That(view.IsTransforming,Is.False);Assert.That(boss.Character.Definition,Is.SameAs(Definition));
                owner.BeginTurn(boss);Assert.That(renderer.sprite,Is.SameAs(Definition.TransformationFrames[0]));Assert.That(view.HasCombatPose,Is.False);
                var seen=new HashSet<Sprite>();var finish=Time.time+1.35f;view.PlayCombatPose(oldPose,Vector2.right,3);
                while(Time.time<finish){if(view.IsTransforming){seen.Add(renderer.sprite);Assert.That(view.FacingDirection,Is.EqualTo(Vector2.left));Assert.That(driver.IsActionInProgress,Is.True);}yield return null;}
                Assert.That(seen.Count,Is.EqualTo(6));Assert.That(view.IsTransforming,Is.False);Assert.That(driver.IsActionInProgress,Is.False);Assert.That(renderer.sprite.name,Does.StartWith("bandit-leader-p2-w-idle"));
                owner.CheckPhases();yield return null;Assert.That(view.IsTransforming,Is.False);
            }
            finally{owner?.Dispose();driver?.Dispose();Object.Destroy(root);}
            yield return new ExitPlayMode();
        }

        [TestCase("bear")]
        [TestCase("mine")]
        public void HiddenTrapActivationPublishesOneLocatedEffectAndCleanupDoesNotReplayIt(string key)
        {
            var boss=Unit(Definition);var hero=Unit(Definition.SecondPhaseDefinition,3,3,true);var definition=Asset<SummonedObjectDefinition>(VS16CatacombsBossSetup.BanditRoot+"SO_BanditLeader_"+key+".asset");
            var trap=new SummonedCombatMapObject("test.trap",hero.Position,boss.Character.CharacterId,BanditLeaderBossSystem.Prefix+key,0,definition);
            var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){boss,hero},SummonedObjects=new(){trap},MapObjects=new(){trap}};
            var bus=new CombatFeedbackBus();var seen=new List<CombatFeedbackEvent>();bus.Published+=e=>{if(e.Type==CombatFeedbackType.MapObjectTriggered)seen.Add(e);};var effects=new SummonedObjectEffectExecutor(feedbackBus:bus);
            Assert.That(definition.HiddenUntilTriggered,Is.True);Assert.That(definition.WorldSprite,Is.Not.Null);Assert.That(definition.TriggerSprite,Is.Not.Null);
            Assert.That(effects.ExecuteEntry(session,trap,hero),Is.True);Assert.That(seen.Count,Is.EqualTo(1));Assert.That(seen[0].MapObject,Is.SameAs(trap));Assert.That(seen[0].Position,Is.EqualTo(hero.Position));
            new SummonedObjectSystem(effectExecutor:effects).Remove(session,trap);Assert.That(seen.Count,Is.EqualTo(1));
            Assert.That(effects.ExecuteEntry(session,trap,hero),Is.False);Assert.That(seen.Count,Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActivationShowsRegisteredFootprintBrieflyThenReleasesBothRenderers()
        {
            yield return new EnterPlayMode();var root=new GameObject("Bandit trap visual lifecycle");
            try
            {
                var spawner=root.AddComponent<CombatUnitViewSpawner>();spawner.enabled=false;var definition=Asset<SummonedObjectDefinition>(VS16CatacombsBossSetup.BanditRoot+"SO_BanditLeader_bear.asset");
                var trap=new SummonedCombatMapObject("trap",new GridPosition(3,4),"boss",BanditLeaderBossSystem.BearId,0,definition);var session=new CombatSession{Grid=new BattleGrid(8,8),SummonedObjects=new(){trap}};
                typeof(CombatUnitViewSpawner).GetMethod("EnsureSummonedObjectViews",Private).Invoke(spawner,new object[]{session});typeof(CombatUnitViewSpawner).GetMethod("SynchronizeSummonedObjectViews",Private).Invoke(spawner,new object[]{session});
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Single().forceRenderingOff,Is.True);
                Assert.That(spawner.TryPlayMapObjectTrigger(session,trap),Is.True);var visible=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>!r.forceRenderingOff).ToArray();Assert.That(visible.Length,Is.EqualTo(2));
                spawner.TryGetWorldPosition(trap.Position,out var center);Assert.That(visible.Single(r=>r.sprite==definition.WorldSprite).transform.position,Is.EqualTo(center+(Vector3)definition.WorldSpriteOffset));
                // EditMode runner drives this IEnumerator even after EnterPlayMode;
                // wait on the game clock explicitly rather than yielding WaitForSeconds.
                var finish=Time.time+.25f;var deadline=Time.realtimeSinceStartup+3;
                while(Time.time<finish && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(Time.time,Is.GreaterThanOrEqualTo(finish));
                typeof(CombatUnitViewSpawner).GetMethod("TickWorldEffects",Private).Invoke(spawner,null);yield return null;
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r=>!r.forceRenderingOff),Is.Zero);
            }
            finally{Object.Destroy(root);}
            yield return new ExitPlayMode();
        }
    }
}
