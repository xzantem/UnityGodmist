using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class SkeletonSlayerBossTests
    {
        UnityEngine.Random.State randomState;
        [SetUp]public void Seed(){randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(91234);}
        [TearDown]public void RestoreSeed(){UnityEngine.Random.state=randomState;}
        static CharacterDefinition Definition=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.SlayerPath);
        static BattleUnit Unit(bool hero=false,int x=7,int y=7)
        {
            var d=hero?AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset"):Definition;
            Assert.That(d,Is.Not.Null,"Run Configure Skeleton Slayer before integration tests.");
            var c=new CharacterInstanceFactory().Create(d,d.ClassData,10);c.CharacterId+=Guid.NewGuid();
            c.Stats.CritChance=new Stat(0);c.Stats.Dodge=new Stat(0);c.Stats.MaxHealth=new Stat(10000);c.CurrentHealth=10000;c.ResourcePool.CurrentValue=c.ResourcePool.MaxValue;
            return new BattleUnit{Character=c,IsPlayerSide=hero,Position=new GridPosition(x,y),MaxActionPoints=10000,CurrentActionPoints=10000};
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var s=new CombatSession{CombatId="slayer.tests",Grid=new BattleGrid(15,15),Units=units.ToList(),ActiveUnit=units[0]};
            foreach(var u in units)u.WarriorState.Session=s;return s;
        }
        static SkeletonSlayerBossSystem Owner(CombatSession s)=>new(s,new CombatDamageSystem(),new StatusEffectSystem());
        static void Phase(SkeletonSlayerBossSystem owner,BattleUnit boss){boss.Character.CurrentHealth=5000;owner.CheckPhases();owner.BeginTurn(boss);boss.CurrentActionPoints=boss.MaxActionPoints=10000;}
        static SkillData Skill(BattleUnit b,string id)=>b.Character.Skills.Single(s=>s.SkillId==id);
        static bool Cast(CombatSession s,BattleUnit b,string id,BattleUnit target)
            =>new SkillExecutionSystem().TryExecute(s,b,Skill(b,id),new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}});
        [Test]
        public void ThresholdLatchesAndWaitsForOwnTurnIncludingRebindAndHealing()
        {
            var b=Unit();var h=Unit(true,2,2);var s=Session(b,h);var feedback=new CombatFeedbackBus();var count=0;
            feedback.Published+=e=>{if(e.EffectName=="Zemsta Zabójcy")count++;};
            var owner=new SkeletonSlayerBossSystem(s,new CombatDamageSystem(),new StatusEffectSystem(),feedback:feedback);
            b.Character.CurrentHealth=5000;s.SkillResolutionDepth=1;owner.CheckPhases();Assert.That(b.SkeletonSlayerState.PhaseTwoPending,Is.False);
            s.SkillResolutionDepth=0;owner.CheckPhases();owner.BeginTurn(h);Assert.That(b.Character.Definition,Is.SameAs(Definition));
            b.Character.CurrentHealth=10000;owner.Dispose(true);
            using var rebound=new SkeletonSlayerBossSystem(s,new CombatDamageSystem(),new StatusEffectSystem(),feedback:feedback);
            rebound.BeginTurn(b);rebound.BeginTurn(b);Assert.That(count,Is.EqualTo(1));Assert.That(b.SkeletonSlayerState.PhaseTwo,Is.True);Assert.That(b.Character.CurrentHealth,Is.EqualTo(10000));
            Assert.That(b.Character.Skills.Select(k=>k.DisplayName),Is.EqualTo(new[]{"Kościany Rozgrom","Młot Zniszczenia","Wykonać Zadanie"}));
        }
        [TestCase(DamageType.Physical,65)] [TestCase(DamageType.Magical,100)]
        public void PhaseOneArmorIsPhysicalOnly(DamageType type,int expected)
        {
            var b=Unit();var h=Unit(true);var s=Session(b,h);using var o=Owner(s);
            new CombatDamageSystem().ApplyDamage(s,h,b,100,type);Assert.That(10000-b.Character.CurrentHealth,Is.EqualTo(expected));
        }
        [Test]
        public void DefenseCountsPreDefenseDamageThenEmpowersExactlyNextAttack()
        {
            var b=Unit();var h=Unit(true,7,8);var s=Session(b,h);using var o=Owner(s);
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.DefenseId,b),Is.True);
            h.Character.Stats.MinAttack=h.Character.Stats.MaxAttack=new Stat(100);b.Character.Stats.PhysicalDefense=new Stat(1000);
            new CombatDamageSystem().ResolveAndApplyDamage(s,h,b,100,DamageType.Physical,true);
            Assert.That(b.SkeletonSlayerState.DefenseDamage,Is.EqualTo(100));Assert.That(10000-b.Character.CurrentHealth,Is.LessThan(100));
            o.BeginTurn(b);Assert.That(b.SkeletonSlayerState.NextAttackBonus,Is.Zero);
            o.BeginTurn(b);Assert.That(b.SkeletonSlayerState.NextAttackBonus,Is.EqualTo(100));
            var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{h}}};
            SkeletonSlayerBossSystem.BeforeSkill(b,Skill(b,SkeletonSlayerBossSystem.CrushingId),targets);
            Assert.That(SkeletonSlayerBossSystem.AttackBonus(b),Is.EqualTo(100));Assert.That(b.SkeletonSlayerState.NextAttackBonus,Is.Zero);
            s.SkillResolved.Invoke(b,Skill(b,SkeletonSlayerBossSystem.CrushingId),"test");Assert.That(SkeletonSlayerBossSystem.AttackBonus(b),Is.Zero);
        }
        [Test]
        public void DefenseWithoutDamageStunsAndPhaseChangeClearsOwnDefense()
        {
            var b=Unit();var s=Session(b);using var o=Owner(s);Cast(s,b,SkeletonSlayerBossSystem.DefenseId,b);
            o.BeginTurn(b);o.BeginTurn(b);Assert.That(StatusEffectSystem.IsStunned(b),Is.True);
            b.SkeletonSlayerState.DefenseTurns=2;b.SkeletonSlayerState.NextAttackBonus=100;Phase(o,b);
            Assert.That(b.SkeletonSlayerState.DefenseTurns,Is.Zero);Assert.That(b.SkeletonSlayerState.NextAttackBonus,Is.Zero);
        }
        [Test]
        public void RevengeRefillsPastBaseCapAndSurvivesNonRefillRefresh()
        {
            var b=Unit();var h=Unit(true);var s=Session(b,h);using var o=Owner(s);Phase(o,b);
            var d=new CombatDamageSystem();for(var i=0;i<30;i++)d.ApplyDamage(s,h,b,1,DamageType.Magical);
            Assert.That(b.SkeletonSlayerState.RevengeHits,Is.EqualTo(20));
            CombatTurnSystem.RefreshMaxActionPoints(s,b,true);Assert.That(b.CurrentActionPoints,Is.EqualTo(Mathf.RoundToInt(b.MaxActionPoints*1.6f)));
            var ap=b.CurrentActionPoints;CombatTurnSystem.RefreshMaxActionPoints(s,b);Assert.That(b.CurrentActionPoints,Is.EqualTo(ap));
        }
        [Test]
        public void HammerUsesChosenTargetCellBlocksMovementAndExplodesAfterTwoBossTurns()
        {
            var b=Unit();var near=Unit(true,7,8);var selected=Unit(true,8,7);var s=Session(b,near,selected);using var o=Owner(s);Phase(o,b);
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.HammerId,selected),Is.True);
            Assert.That(b.SkeletonSlayerState.HammerCenter,Is.EqualTo(selected.Position));Assert.That(b.SkeletonSlayerState.HammerFields.Count,Is.EqualTo(9));
            Assert.That(new CombatMovementSystem().CalculateReachablePaths(s,b),Is.Empty);
            Assert.That(s.CanUseBossSkill(b,Skill(b,SkeletonSlayerBossSystem.HammerId)),Is.False);
            Assert.That(s.CanUseBossSkill(b,Skill(b,SkeletonSlayerBossSystem.TaskId)),Is.True);
            var hits=0;s.DamageApplied+=(a,t,n,ty,id)=>{if(a==b && n>0)hits++;};
            o.BeginTurn(b);Assert.That(hits,Is.Zero);o.BeginTurn(b);Assert.That(hits,Is.InRange(1,10));Assert.That(b.SkeletonSlayerState.HammerTurns,Is.Zero);Assert.That(s.SummonedObjects,Is.Empty);
        }
        [Test]
        public void HammerCancelsAtTenPercentCumulativeDamageAndCleansMarkers()
        {
            var b=Unit();var h=Unit(true,7,8);var s=Session(b,h);using var o=Owner(s);Phase(o,b);Cast(s,b,SkeletonSlayerBossSystem.HammerId,h);
            var d=new CombatDamageSystem();d.ApplyDamage(s,h,b,999,DamageType.Magical);Assert.That(b.SkeletonSlayerState.HammerTurns,Is.EqualTo(2));
            Assert.That(b.SkeletonSlayerState.HammerDamage,Is.EqualTo(999));
            d.ApplyDamage(s,h,b,1,DamageType.Magical);Assert.That(b.SkeletonSlayerState.HammerTurns,Is.Zero,$"Accum={b.SkeletonSlayerState.HammerDamage}; MaxHP={b.Character.Stats.MaxHealth.GetValue()}; HP={b.Character.CurrentHealth}");Assert.That(s.SummonedObjects,Is.Empty);
        }
        [Test]
        public void IgnoringDefenseStillRollsHitsButBypassesBothDefenseStats()
        {
            var b=Unit();var h=Unit(true,7,8);var s=Session(b,h);using var o=Owner(s);Phase(o,b);
            h.Character.Stats.PhysicalDefense=new Stat(100000);var d=new CombatDamageSystem();
            d.ResolveAndApplyDamage(s,b,h,100,DamageType.Physical,true,ignoreDefense:true);Assert.That(10000-h.Character.CurrentHealth,Is.EqualTo(100));
            var hp=h.Character.CurrentHealth;d.ResolveAndApplyDamage(s,b,h,100,DamageType.Physical,false,ignoreDefense:true);Assert.That(h.Character.CurrentHealth,Is.EqualTo(hp));
        }
        [Test]
        public void TaskHasZeroAPCostCannotSpamAndAcceleratesOnlyOneTurn()
        {
            var b=Unit();var s=Session(b);using var o=Owner(s);Phase(o,b);CombatTurnSystem.RefreshMaxActionPoints(s,b,true);var ap=b.CurrentActionPoints;
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.TaskId,b),Is.True);Assert.That(b.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.TaskId,b),Is.False);
            Assert.That(SkeletonSlayerBossSystem.ApplyTurnDelay(b,100,true),Is.Zero);Assert.That(SkeletonSlayerBossSystem.ApplyTurnDelay(b,100,true),Is.EqualTo(100));
        }
        [Test]
        public void TaskActuallyAdvancesNextTurnAndPreviewDoesNotConsumeIt()
        {
            var b=Unit();var h=Unit(true,1,1);var s=Session(b,h);using var o=Owner(s);Phase(o,b);
            var turns=new CombatTurnSystem(onUnitTurnBegan:o.BeginTurn);turns.InitializeTurnOrder(s);
            Assert.That(s.ActiveUnit,Is.SameAs(b));Assert.That(Cast(s,b,SkeletonSlayerBossSystem.TaskId,b),Is.True);
            var upcoming=CombatTurnSystem.GetUpcomingUnitTurns(s,3);
            Assert.That(b.SkeletonSlayerState.AccelerateNextTurn,Is.True);
            turns.EndActiveTurn(s);Assert.That(s.ActiveUnit,Is.SameAs(b));Assert.That(b.SkeletonSlayerState.AccelerateNextTurn,Is.False);
            turns.EndActiveTurn(s);Assert.That(s.ActiveUnit,Is.SameAs(h));
        }
        [TestCase(2,false)]
        [TestCase(5,true)]
        public void TaskGrantedTurnCanBuyAnotherOnlyWithEnoughRage(int resourceGains,bool canRenew)
        {
            var b=Unit();var h=Unit(true,1,1);var s=Session(b,h);using var owner=Owner(s);Phase(owner,b);
            h.Character.Stats.Speed=new Stat(b.Character.Stats.Speed.GetValue()*.9f);
            var turns=new CombatTurnSystem(onUnitTurnBegan:owner.BeginTurn);turns.InitializeTurnOrder(s);
            var task=Skill(b,SkeletonSlayerBossSystem.TaskId);
            Assert.That(task.ResourceType,Is.EqualTo(ResourceType.Rage));
            Assert.That(task.ResourceCost,Is.EqualTo(50));
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.TaskId,b),Is.True);
            Assert.That(b.Character.ResourcePool.CurrentValue,Is.Zero);
            CombatTurnSystem.GetUpcomingUnitTurns(s,4);
            Assert.That(b.SkeletonSlayerState.AccelerateNextTurn,Is.True,"Preview cannot consume acceleration.");
            turns.EndActiveTurn(s);Assert.That(s.ActiveUnit,Is.SameAs(b));
            Assert.That(s.CanUseBossSkill(b,task),Is.True,"A granted turn must have no special recast gate.");
            var resources=new ResourceRuleSystem();
            for(var hit=0;hit<resourceGains;hit++)resources.ProcessOffensiveSkillUsed(b);
            Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(resourceGains*10));
            Assert.That(Cast(s,b,SkeletonSlayerBossSystem.TaskId,b),Is.EqualTo(canRenew));
            Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(canRenew?0:20));
            turns.EndActiveTurn(s);
            Assert.That(s.ActiveUnit,Is.SameAs(canRenew?b:h));
        }
        [Test]
        public void EveryImportedPngRetainsNativeDimensionsPointFilterAnd32PPU()
        {
            var config=JsonUtility.FromJson<DeadKingArtSetup.Config>(System.IO.File.ReadAllText("Assets/_Project/Scripts/Editor/SkeletonSlayerArtBindings.json"));
            Assert.That(config.imports.Length,Is.EqualTo(325));
            foreach(var item in config.imports)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(item.path);Assert.That(importer,Is.Not.Null,item.path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(32));
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
                var bytes=System.IO.File.ReadAllBytes(item.path);var width=BitConverter.ToInt32(bytes.Skip(16).Take(4).Reverse().ToArray(),0);var height=BitConverter.ToInt32(bytes.Skip(20).Take(4).Reverse().ToArray(),0);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(item.path);Assert.That(texture.width,Is.EqualTo(width));Assert.That(texture.height,Is.EqualTo(height));
                using var hash=System.Security.Cryptography.SHA256.Create();Assert.That(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),Is.EqualTo(item.sha256));
            }
        }
        [Test]
        public void DeathAndDisposeCancelPendingPhaseTerrainAndRestoreDefinition()
        {
            var b=Unit();var h=Unit(true,7,8);var s=Session(b,h);var o=Owner(s);Phase(o,b);Cast(s,b,SkeletonSlayerBossSystem.HammerId,h);
            b.Character.CurrentHealth=0;b.Character.LifeState=CharacterLifeState.Dead;o.BeginTurn(b);Assert.That(s.SummonedObjects,Is.Empty);Assert.That(b.SkeletonSlayerState.HammerTurns,Is.Zero);
            o.Dispose();Assert.That(b.SkeletonSlayerState,Is.Null);Assert.That(b.Character.Definition,Is.SameAs(Definition));Assert.That(s.CanUseBossSkill,Is.Null);
        }
        [Test]
        public void PassivesAndCountdownsAreVisibleWithoutMutatingCharacterEffects()
        {
            var b=Unit();var s=Session(b);using var o=Owner(s);var count=b.Character.PassiveEffects.Count;
            var first=Godmist.Views.UI.CombatStatusPresentationFactory.Create(b.Character.PassiveEffects,null,null,b);
            Assert.That(first.Any(e=>e.Effect.EffectId==SkeletonSlayerBossSystem.ArmorStatusId),Is.True);
            Phase(o,b);var second=Godmist.Views.UI.CombatStatusPresentationFactory.Create(b.Character.PassiveEffects,null,null,b);
            Assert.That(second.Any(e=>e.Effect.EffectId==SkeletonSlayerBossSystem.RevengeStatusId),Is.True);Assert.That(b.Character.PassiveEffects.Count,Is.EqualTo(count));
        }
        [TestCase(1)] [TestCase(2)]
        public void AllDirectionsMotionReactionsAndSkillPosesResolveAt32PPU(int phase)
        {
            var id=phase==1?SkeletonSlayerBossSystem.BossId:SkeletonSlayerBossSystem.PhaseTwoId;
            for(var i=0;i<8;i++)
            {
                var angle=i*Mathf.PI/4;var facing=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                foreach(var state in new[]{CharacterAnimationState.Idle,CharacterAnimationState.Walk})
                {
                    Assert.That(CharacterAnimationResolver.TryResolveCharacter(id,facing,state,out var clip),Is.True);
                    Assert.That(clip.FrameCount,Is.EqualTo(4));Assert.That(clip.GetFrameDuration(0),Is.EqualTo(state==CharacterAnimationState.Idle?.3f:.18f).Within(.001));
                    foreach(var frame in clip.Frames){Assert.That(frame.pixelsPerUnit,Is.EqualTo(32));Assert.That(frame.pivot,Is.EqualTo(new Vector2(80,16)));}
                }
                foreach(var action in (phase==1?new[]{"crushing-blow","tremors","unyielding-defense","hit","dodge"}:new[]{"bone-crush","destruction-charge","destruction-impact","execute-task","hit","dodge"}))
                {Assert.That(CombatPoseResolver.TryResolveAction(id,action,facing,out var pose),Is.True,action);Assert.That(pose.Actor.pixelsPerUnit,Is.EqualTo(32));Assert.That(pose.Actor.pivot,Is.EqualTo(action is "hit" or "dodge"?new Vector2(80,16):new Vector2(128,64)));}
            }
            Assert.That(CharacterAnimationResolver.TryResolveCorpse(id,out var corpse),Is.True);Assert.That(corpse.pivot,Is.EqualTo(new Vector2(96,96)));
        }
        [Test]
        public void ImportedDefinitionsHaveDedicatedArtAndWorkbookStats()
        {
            Assert.That(Definition.TransformationFrames.Count,Is.EqualTo(6));Assert.That(Definition.TransformationFramesPerSecond,Is.EqualTo(5));
            foreach(var d in new[]{Definition,Definition.SecondPhaseDefinition})
            {Assert.That(d.DeadSprite,Is.Not.Null);Assert.That(d.PortraitSprite,Is.Not.Null);Assert.That(d.StartingSkills.All(k=>k.Icon!=null),Is.True);Assert.That(d.Tags,Contains.Item(CharacterTag.Giant));Assert.That(d.LootTable,Is.Null);}
            Assert.That(Definition.BaseStats.MaxHealth,Is.EqualTo(700));Assert.That(Definition.BaseStats.MinAttack,Is.EqualTo(50));
            Assert.That(Definition.DeadSprite,Is.Not.SameAs(Definition.SecondPhaseDefinition.DeadSprite));
        }
    }
}
