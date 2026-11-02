using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public partial class MageContentTests
    {
        static BattleUnit FrostUnit(bool player=true,int x=1,int y=2)
        {
            var u=Unit(player,x,y);u.Character.Stats.Speed=new Stat(100);u.Character.Stats.CritChance=new Stat(0);u.Character.Stats.CritDamage=new Stat(2);return u;
        }
        static void IceRank(BattleUnit u,string slot,int rank=1)
        {
            var p=u.Character.SkillTreeProgress.FirstOrDefault(p=>p.TreeId==MageCombatSystem.Frost);
            if(p==null){p=new SkillTreeProgress{TreeId=MageCombatSystem.Frost};u.Character.SkillTreeProgress.Add(p);}
            var id="node.mage.frost."+slot;var investment=p.NodeInvestments.FirstOrDefault(n=>n.NodeId==id);
            if(investment==null)p.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId=id,Ranks=rank});else investment.Ranks=rank;
        }
        static SkillData IceSkill(string slot)=>Load<SkillData>("Skills/SD_MageFrost_"+slot+".asset");
        static void ColdStatus(BattleUnit unit,string id,int turns)=>unit.Character.PassiveEffects.Add(new PassiveEffectRuntime(id,new(),1,turns,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd));
        static void ColdSeed(float chance)
        {for(int i=0;i<1000;i++){UnityEngine.Random.InitState(i);if(UnityEngine.Random.value<chance){UnityEngine.Random.InitState(i);return;}}Assert.Fail("Seed");}
        static PassiveEffectCombatContext ColdContext(CombatSession s,BattleUnit a,BattleUnit b,string action="cold")=>new(s,a,b,skill:Skill("EnergyOrb"),skillId:Skill("EnergyOrb").SkillId,actionId:action,damageType:DamageType.Magical);
        [Test] public void FrostProductionTreeHasTwentyFiveNodesFortyFourRanksAndThreeValidSpells()
        {
            var c=FrostUnit().Character;Assert.That(c.ClassData.SkillTrees.Count,Is.EqualTo(3));
            var tree=c.ClassData.SkillTrees.Single(t=>t.Id==MageCombatSystem.Frost);
            Assert.That(tree.Nodes.Count,Is.EqualTo(25));Assert.That(tree.Nodes.Sum(n=>n.MaxRanks),Is.EqualTo(44));
            Assert.That(tree.Nodes.Count(n=>n.GrantedSkill!=null),Is.EqualTo(3));Assert.That(tree.Nodes.Select(n=>n.DisplayName).Distinct().Count(),Is.EqualTo(25));
            foreach(var node in tree.Nodes)
            {Assert.That(node.Icon,Is.Not.Null,node.Id);Assert.That(node.Icon.rect.width,Is.EqualTo(32));foreach(var req in node.Requirements)Assert.That(tree.Nodes.Any(n=>n.Id==req.NodeId),Is.True);if(node.GrantedSkill!=null)Assert.That(CombatDataValidator.ValidateSkill(node.GrantedSkill).Errors,Is.Empty);}
            Assert.That(tree.Nodes.Single(n=>n.Id.EndsWith("t2_3")).MaxRanks,Is.EqualTo(3));
            Assert.That(tree.Nodes.Single(n=>n.Id.EndsWith("t3_3")).DisplayName,Is.EqualTo("Trwała Aura"));
            foreach(var pair in new[]{("t1_3","t1_4"),("t2_4","t1_4"),("t4_2","t3_1"),("t5_5","t5_4")})
                Assert.That(tree.Nodes.Single(n=>n.Id=="node.mage.frost."+pair.Item1).Requirements.Select(r=>r.NodeId),Has.Member("node.mage.frost."+pair.Item2));
        }
        [Test] public void FrostResistanceIsDerivedOnceAndConditionalCriticalWorksBeforeFreezeBreaks()
        {
            var a=FrostUnit();var b=FrostUnit(false,2);IceRank(a,"t1_1",3);var s=Session(a,b);MageCombatSystem.BindFrostSession(s);
            var pipeline=new CharacterStatPipelineSystem();pipeline.Refresh(a.Character);pipeline.Refresh(a.Character);
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(a,ResistanceType.Suppression),Is.EqualTo(.015f).Within(.00001));
            Assert.That(MageCombatSystem.FrozenCriticalChance(a,b),Is.Zero);
            ColdStatus(b,StatusEffectIds.Freeze,3);Assert.That(MageCombatSystem.FrozenCriticalChance(a,b),Is.EqualTo(.045f).Within(.00001));
            Assert.That(CombatDamageSystem.PreviewCriticalChance(s,a,b,Skill("EnergyOrb"),DamageType.Magical),Is.EqualTo(.045f).Within(.00001));
            ColdSeed(.045f);
            var hit=new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Magical,true,passiveContext:ColdContext(s,a,b));
            Assert.That(hit.IsCritical,Is.True);Assert.That(StatusEffectSystem.IsFrozen(b),Is.False);
        }
        [TestCase(1,65)][TestCase(2,25)] public void HypothermiaReducesImmediateAndPeriodicHealingFromOtherSources(int rank,int expected)
        {
            var mage=FrostUnit();var weaker=FrostUnit(x:3);var target=FrostUnit(false,2);IceRank(mage,"t1_2",rank);IceRank(weaker,"t1_2",1);
            var s=Session(mage,weaker,target);MageCombatSystem.BindFrostSession(s);ColdStatus(target,StatusEffectIds.Freeze,3);
            var before=target.Character.CurrentHealth;Assert.That(new CombatRecoverySystem().Heal(target,100),Is.EqualTo(expected));Assert.That(target.Character.CurrentHealth-before,Is.EqualTo(expected));
            Assert.That(new StatusEffectSystem().ApplyPeriodicRecovery(target,target,Skill("Ritual"),Skill("Ritual").Effects[0].WithPercentageValue(.1f),0,false),Is.True);
            before=target.Character.CurrentHealth;new StatusEffectSystem().ProcessTurnEnd(target);Assert.That(target.Character.CurrentHealth-before,Is.EqualTo(expected));
            mage.Character.LifeState=CharacterLifeState.Dead;weaker.Character.LifeState=CharacterLifeState.Dead;
            Assert.That(new CombatRecoverySystem().Heal(target,100),Is.EqualTo(100));
        }
        [TestCase(0,1.25f)][TestCase(1,1.35f)][TestCase(2,1.45f)][TestCase(3,1.6f)]
        public void IcicleModifiersAreResolvedWithoutMutatingSkillAsset(int rank,float multiplier)
        {
            var a=FrostUnit();IceRank(a,"t2_3",rank);IceRank(a,"t2_4");var skill=IceSkill("t1_4");
            Assert.That(MageCombatSystem.ResolveEffect(a,skill,skill.Effects[0]).DamageMultiplier,Is.EqualTo(multiplier).Within(.00001));
            Assert.That(MageCombatSystem.ResolveEffect(a,skill,skill.Effects[1]).DurationTurns,Is.EqualTo(3));
            Assert.That(skill.Effects[0].DamageMultiplier,Is.EqualTo(1.25f));Assert.That(skill.Effects[1].DurationTurns,Is.EqualTo(2));
        }
        [Test] public void FreezeRetentionKeepsOriginalDamageBonusAndGlacierSupportsNearbyAllies()
        {
            var owner=FrostUnit();var ally=FrostUnit(x:3);var enemy=FrostUnit(false,4);var s=Session(owner,ally,enemy);IceRank(owner,"t3_5");ColdStatus(enemy,StatusEffectIds.Freeze,3);
            ColdSeed(.3f);Assert.That(MageCombatSystem.ConsumeFrostFreeze(s,ally,enemy,3),Is.EqualTo(2.5f));
            Assert.That(enemy.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Freeze).RemainingDuration,Is.EqualTo(2));
            ally.Position=new GridPosition(8,2);Assert.That(MageCombatSystem.ConsumeFrostFreeze(s,ally,enemy,2),Is.EqualTo(2));Assert.That(StatusEffectSystem.IsFrozen(enemy),Is.False);
        }
        [Test] public void ConversionRetainsStunDurationAndOnlyAttemptsOncePerActionTarget()
        {
            var a=FrostUnit();var b=FrostUnit(false,2);var s=Session(a,b);IceRank(a,"t1_5",2);ColdStatus(b,StatusEffectIds.Stun,4);
            var ctx=ColdContext(s,a,b);var contact=MageCombatSystem.FrostContact(a,b,ctx);var statuses=new StatusEffectSystem();
            MageCombatSystem.FrostAfterContact(s,a,b,ctx,contact,statuses);
            Assert.That(StatusEffectSystem.HasStatus(b,StatusEffectIds.Stun),Is.False);Assert.That(b.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Freeze).RemainingDuration,Is.EqualTo(4));
            var count=a.MageState.Frost.Contacts.Count;MageCombatSystem.FrostAfterContact(s,a,b,ctx,contact,statuses);Assert.That(a.MageState.Frost.Contacts.Count,Is.EqualTo(count));
            MageCombatSystem.CompleteFrostAction(a,ctx.ActionId);Assert.That(a.MageState.Frost.Contacts,Is.Empty);
        }
        [Test] public void FrostProcsIgnorePeriodicReactionsAndDuplicatesWithoutConsumingRandom()
        {
            var a=FrostUnit();var b=FrostUnit(false,2);var s=Session(a,b);IceRank(a,"t1_1",3);
            foreach(var ctx in new[]{new PassiveEffectCombatContext(s,a,b,skill:Skill("EnergyOrb"),isPassiveReaction:true),new PassiveEffectCombatContext(s,a,b,skill:Skill("EnergyOrb"),isPassiveDuplicate:true),new PassiveEffectCombatContext(s,a,b,skill:Skill("EnergyOrb"),damageType:DamageType.Burn)})
            {
                var random=UnityEngine.Random.state;MageCombatSystem.FrostAfterContact(s,a,b,ctx,MageCombatSystem.FrostContact(a,b,ctx),new StatusEffectSystem());
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(b.Character.PassiveEffects,Is.Empty);
            }
        }
        [Test] public void ShatteringUsesPreContactBossThresholdAndOneEligibleAttempt()
        {
            var a=FrostUnit();var b=FrostUnit(false,2);IceRank(a,"t4_3",2);IceRank(a,"t5_3",2);b.IsBoss=true;b.Character.CurrentHealth=200;
            Assert.That(MageCombatSystem.FrostExecution(a,b,new MageFrostContact(3,0,200,true,"one"),10,0),Is.EqualTo(10));
            b.Character.CurrentHealth=199;
            Assert.That(MageCombatSystem.FrostExecution(a,b,new MageFrostContact(3,0,199,true,"one"),10,0),Is.GreaterThan(199));
            Assert.That(MageCombatSystem.FrostExecution(a,b,new MageFrostContact(3,0,199,true,"one"),10,0),Is.EqualTo(10));
        }
        [Test] public void EmergencyShieldTracksItsOwnLayerAndAppliesBreakPenaltyOnlyForThatLayer()
        {
            var a=FrostUnit();IceRank(a,"t3_1");a.Character.Stats.PhysicalDefense=new Stat(100);a.Character.Stats.MagicalDefense=new Stat(100);
            MageCombatSystem.FrostShieldResolved(a,249);Assert.That(a.Character.ShieldPool.TotalShield,Is.Zero);
            MageCombatSystem.FrostShieldResolved(a,1);Assert.That(a.Character.ShieldPool.TotalShield,Is.EqualTo(300));Assert.That(a.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(130).Within(.001));
            a.Character.ShieldPool.AddLayer(50,"other",3);a.Character.ShieldPool.AbsorbDamage(50);MageCombatSystem.FrostShieldResolved(a,0);
            Assert.That(StatusEffectSystem.HasStatus(a,MageCombatSystem.BrokenIce),Is.False);
            a.Character.ShieldPool.AbsorbDamage(300);MageCombatSystem.FrostShieldResolved(a,0);
            Assert.That(StatusEffectSystem.HasStatus(a,MageCombatSystem.BrokenIce),Is.True);Assert.That(a.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(70).Within(.001));
            Assert.That(a.MageState.Frost.ShieldCooldown,Is.EqualTo(10));Assert.That(a.Character.ShieldPool.TotalShield,Is.Zero);
            var cooldown=a.Character.PassiveEffects.Single(e=>e.EffectId=="mage.frost.shield_cooldown");
            Assert.That(Godmist.Views.UI.HeroCombatStatusText.Describe(cooldown,a),Does.Contain("10"));
            for(int i=0;i<10;i++)MageCombatSystem.AdvanceFrostShieldClock(a);
            Assert.That(StatusEffectSystem.HasStatus(a,"mage.frost.shield_cooldown"),Is.False);
        }
        [Test] public void EmergencyShieldCountsDotHealthLossAndNeverSavesAnAlreadyFatalHit()
        {
            var a=FrostUnit();var enemy=FrostUnit(false,2);IceRank(a,"t3_1");IceRank(a,"t3_2");IceRank(a,"t4_2",3);a.Character.CurrentHealth=1000;var s=Session(enemy,a);
            new CombatDamageSystem().ApplyDamage(s,enemy,a,150,DamageType.Burn);Assert.That(a.Character.ShieldPool.TotalShield,Is.EqualTo(300));Assert.That(a.MageState.Frost.ShieldCooldown,Is.EqualTo(7));
            MageCombatSystem.FrostTurnStart(s,a,new StatusEffectSystem());Assert.That(a.MageState.Frost.HealthLost,Is.Zero);Assert.That(a.MageState.Frost.ShieldCooldown,Is.EqualTo(6));
            var fatal=FrostUnit();IceRank(fatal,"t3_1");fatal.Character.CurrentHealth=100;var other=Session(enemy,fatal);
            new CombatDamageSystem().ApplyDamage(other,enemy,fatal,100,DamageType.Burn);Assert.That(fatal.Character.ShieldPool.TotalShield,Is.Zero);Assert.That(fatal.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }
        [Test] public void IceFieldUsesDynamicRangeAndEmptyGroundWithoutChargingInvalidSelection()
        {
            var a=FrostUnit();var enemy=FrostUnit(false,3);var s=Session(a,enemy);IceRank(a,"t5_5",2);var skill=IceSkill("t5_4");var exec=new SkillExecutionSystem();
            var groups=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,Array.Empty<BattleUnit>()}};
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveMode(a,skill,skill.TargetModes[0]).AbsoluteRange.MaxRange,Is.EqualTo(11));
            Assert.That(exec.TryExecute(s,a,skill,groups,null,null,new Dictionary<int,GridPosition?>{{0,new GridPosition(13,11)}}),Is.False);Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            Assert.That(exec.TryExecute(s,a,skill,groups,null,null,new Dictionary<int,GridPosition?>{{0,new GridPosition(4,4)}}),Is.True);
            Assert.That(a.MageState.Frost.Areas.Single().Turns,Is.EqualTo(6));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
        }
        [Test] public void FrostFieldsUseSharedMovementCostsAndRemoveModifiersAfterLeavingOrCombatCleanup()
        {
            var a=FrostUnit();var enemy=FrostUnit(false,4,4);var s=Session(a,enemy);MageCombatSystem.BindFrostSession(s);
            a.MageState.Frost.Areas.Add(new MageFrostArea{Center=new GridPosition(4,4),Turns=4,Snowstorm=true,MovementPenalty=true});MageCombatSystem.CreateIceField(s,a,new GridPosition(4,4));
            Assert.That(enemy.Character.Stats.Speed.GetValue(),Is.EqualTo(75));Assert.That(MageCombatSystem.FrostAccuracyPenalty(enemy),Is.EqualTo(30));
            var path=new[]{new GridPosition(5,4)};Assert.That(CombatMovementSystem.CalculatePathCost(s,enemy.Position,0,path,actor:enemy),Is.EqualTo(2*ActionPointUnits.PerPoint));
            enemy.Position=new GridPosition(8,4);s.NotifyUnitEnteredCell(enemy);Assert.That(enemy.Character.Stats.Speed.GetValue(),Is.EqualTo(100));Assert.That(MageCombatSystem.FrostAccuracyPenalty(enemy),Is.Zero);
            enemy.Position=new GridPosition(4,4);s.NotifyUnitEnteredCell(enemy);enemy.Character.ShieldPool.AddLayer(100,MageCombatSystem.EmergencyShield);
            new PassiveEffectLifecycleSystem().RemoveCombatScopedEffects(s);Assert.That(enemy.Character.Stats.Speed.GetValue(),Is.EqualTo(100));Assert.That(enemy.Character.ShieldPool.TotalShield,Is.Zero);
        }
        [Test] public void SnowstormsSpawnFromAnotherAllysKillAndOverlapsDoNotMultiplyTicks()
        {
            var a=FrostUnit();var killer=FrostUnit(x:2);var dead=FrostUnit(false,4,4);var enemy=FrostUnit(false,5,4);IceRank(a,"t5_1",2);var s=Session(a,killer,dead,enemy);dead.Character.CurrentHealth=10;
            var damage=new CombatDamageSystem();damage.ApplyDamage(s,killer,dead,10,DamageType.Physical);
            var area=a.MageState.Frost.Areas.Single();Assert.That(area.Center,Is.EqualTo(dead.Position));Assert.That(area.Turns,Is.EqualTo(4));
            MageCombatSystem.FrostEnemyDied(s,dead);Assert.That(a.MageState.Frost.Areas.Count,Is.EqualTo(1));a.MageState.Frost.Areas.Add(area.Copy());
            MageCombatSystem.FrostTurnEnd(s,enemy,damage);Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(440));
            for(int i=0;i<4;i++)MageCombatSystem.FrostTurnEnd(s,a,damage);Assert.That(a.MageState.Frost.Areas,Is.Empty);
        }
        [Test] public void DetachedPlanningDoesNotRebindOrMutateUnownedLiveUnitsOrAreas()
        {
            var a=FrostUnit();var enemy=FrostUnit(false,4,4);var s=Session(a,enemy);MageCombatSystem.BindFrostSession(s);a.MageState.Frost.Areas.Add(new MageFrostArea{Center=new GridPosition(4,4),Turns=4,Snowstorm=true});
            var copy=(CombatSession)typeof(SimpleCombatAiSystem).GetMethod("CopyPlanningWorld",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,s,a,new HashSet<int>{0}});
            copy.Units[0].MageState.Frost.Areas[0].Turns=1;MageCombatSystem.CreateIceField(copy,copy.Units[0],new GridPosition(4,4));
            Assert.That(a.MageState.Frost.Areas.Single().Turns,Is.EqualTo(4));Assert.That(enemy.MageState.Frost.Session,Is.SameAs(s));Assert.That(enemy.Character.Stats.Speed.GetValue(),Is.EqualTo(100));
        }
        [Test] public void CombatOnlyIceShieldIsExcludedFromSaveWhileOtherShieldsRemain()
        {
            var c=FrostUnit().Character;var game=new GameSession(GameModeState.Exploration);Assert.That(game.TryRegisterCharacter(c),Is.True);game.TryAddCharacterToActiveParty(c.CharacterId);
            c.ShieldPool.AddLayer(300,MageCombatSystem.EmergencyShield);c.ShieldPool.AddLayer(40,CombatShieldSystem.MageShieldSourceId,3);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"frost");var layers=save.Characters.Single().ShieldLayers;
            Assert.That(layers.Select(l=>l.SourceId),Does.Not.Contain(MageCombatSystem.EmergencyShield));Assert.That(layers.Single().CurrentValue,Is.EqualTo(40));Assert.That(layers.Single().RemainingTurns,Is.EqualTo(3));
        }
        [Test] public void FrostInvestmentAndOldMageBranchesSurviveSaveRestore()
        {
            var c=new CharacterInstanceFactory().Create(Load<Godmist.Data.Characters.CharacterDefinition>("Characters/CD_Mage.asset"));new CharacterProgressionSystem().TrySetLevel(c,25,out _);var progression=new SkillTreeProgressionSystem();
            Assert.That(progression.TryInvest(c,MageCombatSystem.Warlock,"node.mage.warlock.mastery"),Is.True,"Warlock: "+progression.GetInvestmentBlockReason(c,MageCombatSystem.Warlock,"node.mage.warlock.mastery"));
            Assert.That(progression.TryInvest(c,MageCombatSystem.Fire,"node.mage.fire.fire_aura"),Is.True,"Fire: "+progression.GetInvestmentBlockReason(c,MageCombatSystem.Fire,"node.mage.fire.fire_aura"));
            Assert.That(progression.TryInvest(c,MageCombatSystem.Frost,"node.mage.frost.t1_4"),Is.True,"Frost: "+progression.GetInvestmentBlockReason(c,MageCombatSystem.Frost,"node.mage.frost.t1_4"));
            var game=new GameSession(GameModeState.Exploration);game.TryRegisterCharacter(c);game.TryAddCharacterToActiveParty(c.CharacterId);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"frost");Assert.That(new GameSessionRestoreFactory().TryCreate(save,Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"),out var restored),Is.True);
            var mage=restored.CharactersById[c.CharacterId];Assert.That(mage.ClassData.SkillTrees.Count,Is.EqualTo(3));Assert.That(mage.GetKnownSkills(),Has.Member(IceSkill("t1_4")));Assert.That(mage.GetKnownSkills(),Has.Member(Skill("FireAura")));
        }

        [TestCase(false,90)][TestCase(true,85)]
        public void ForceFrostUsesFlatSpeedPenaltyAndSharesOneCastBudget(bool improved,int speed)
        {
            var a=FrostUnit();var b=FrostUnit(false,2);IceRank(a,"t1_1",3);if(improved)IceRank(a,"t2_1");
            var session=Session(a,b);var context=ColdContext(session,a,b);var contact=MageCombatSystem.FrostContact(a,b,context);
            ColdSeed(.2f);MageCombatSystem.FrostAfterContact(session,a,b,context,contact,new StatusEffectSystem());
            Assert.That(b.Character.Stats.Speed.GetValue(),Is.EqualTo(speed));
            var random=UnityEngine.Random.state;MageCombatSystem.FrostAfterContact(session,a,b,context,contact,new StatusEffectSystem());
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test] public void FrostAuraPulsesFollowRecipientAndNeverChillAndFreezeInSamePulse()
        {
            var source=FrostUnit(x:1);var recipient=FrostUnit(x:4);var enemy=FrostUnit(false,5);
            IceRank(source,"t4_4",3);IceRank(source,"t4_5",2);var session=Session(source,recipient,enemy);var statuses=new StatusEffectSystem();
            ColdSeed(.4f);Assert.That(MageCombatSystem.ApplyFrostAura(session,source,recipient,5,statuses),Is.True);
            Assert.That(StatusEffectSystem.HasStatus(enemy,StatusEffectIds.Chill),Is.True);Assert.That(StatusEffectSystem.IsFrozen(enemy),Is.False);
            ColdSeed(.01f);MageCombatSystem.PulseFrostAura(session,recipient,statuses);
            Assert.That(enemy.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Freeze).RemainingDuration,Is.EqualTo(3));
            statuses.RemoveStatus(enemy,StatusEffectIds.Freeze);statuses.RemoveStatus(enemy,StatusEffectIds.Chill);
            recipient.Position=new GridPosition(10,2);ColdSeed(.4f);MageCombatSystem.PulseFrostAura(session,recipient,statuses);
            Assert.That(StatusEffectSystem.HasStatus(enemy,StatusEffectIds.Chill),Is.False);
        }

        [Test] public void HypothermiaForecastExpiresWithControlAndMatchesActualTickRounding()
        {
            var a=FrostUnit();var b=FrostUnit(false,2);IceRank(a,"t1_2",2);var session=Session(a,b);MageCombatSystem.BindFrostSession(session);
            ColdStatus(b,StatusEffectIds.Chill,1);
            var effect=new PassiveEffectRuntime{EffectId=PaladinCombatSystem.HolyRegenerationId,RemainingDuration=4,StateValues=new(){PassiveEffectRuntimeStateValue.Integer("remaining",400)}};
            Assert.That(PaladinCombatSystem.ForecastRegenerationTicks(effect,2,500,recipient:b),Is.EqualTo(125));
            Assert.That(effect.GetStateValue("remaining").IntegerValue,Is.EqualTo(400));
            Assert.That(CombatRecoverySystem.IncomingHealing(b,3),Is.Zero);
        }

        [Test] public void FrostGroundVfxLoadsFromIndependentResourceCatalog()
        {
            Assert.That(Godmist.Views.Characters.CombatTargetVfxResolver.TryResolve("frost-floor",out var clip),Is.True);
            Assert.That(clip.Frames.Length,Is.EqualTo(1));Assert.That(clip.Frames[0].rect.width,Is.EqualTo(32));
        }
    }
}
