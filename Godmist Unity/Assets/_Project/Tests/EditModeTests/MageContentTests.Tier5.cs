using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class MageContentTests
    {
        static SkillData Tier5Skill(string slot)=>Load<SkillData>("Skills/SD_MageT5_"+slot+".asset");
        static void Guarantee(BattleUnit unit,UniqueBuffType buff)=>unit.Character.PassiveEffects.Add(
            new PassiveEffectRuntime{UniqueBuffType=buff,Stacks=1});
        [Test] public void MageTierFiveHasTenNodesEighteenRanksTwoSkillsAndMatchingNativeIcons()
        {
            var nodes=Unit().Character.ClassData.SkillTrees.Where(t=>t.Id!=MageCombatSystem.Frost).SelectMany(t=>t.Nodes).Where(n=>n.Tier==5).ToArray();
            Assert.That(nodes.Length,Is.EqualTo(10));Assert.That(nodes.Sum(n=>n.MaxRanks),Is.EqualTo(18));
            Assert.That(nodes.Count(n=>n.GrantedSkill!=null),Is.EqualTo(2));
            foreach(var node in nodes)
            {Assert.That(node.Icon,Is.Not.Null);Assert.That(node.Icon.rect.width,Is.EqualTo(32));if(node.GrantedSkill!=null)Assert.That(node.GrantedSkill.Icon,Is.SameAs(node.Icon));}
            Assert.That(nodes.Single(n=>n.Id=="node.mage.warlock.t5_2").MaxRanks,Is.EqualTo(1));
        }
        [Test] public void CriticalPowerAddsHalfGloballyAndRefreshOrResetDoesNotCompound()
        {
            var u=Unit();new CharacterStatPipelineSystem().Refresh(u.Character);var initial=u.Character.Stats.CritDamage.GetValue();
            Rank(u,false,"t5_2",1);new CharacterStatPipelineSystem().Refresh(u.Character);
            Assert.That(u.Character.Stats.CritDamage.GetValue(),Is.EqualTo(initial+.5f).Within(.00001));
            new CharacterStatPipelineSystem().Refresh(u.Character);Assert.That(u.Character.Stats.CritDamage.GetValue(),Is.EqualTo(initial+.5f).Within(.00001));
            u.Character.SkillTreeProgress.Clear();new CharacterStatPipelineSystem().Refresh(u.Character);Assert.That(u.Character.Stats.CritDamage.GetValue(),Is.EqualTo(initial));
        }
        [TestCase(0,199,200)][TestCase(1,149,150)][TestCase(2,99,100)]
        public void AdaptationThresholdUsesActualHealthLossAtNewBoundaries(int rank,int below,int at)
        {
            var source=Unit(false);var target=Unit();Rank(target,false,"adaptation",2);if(rank>0)Rank(target,false,"t5_1",rank);
            MageCombatSystem.DamageTaken(source,target,below);Assert.That(target.MageState.Adaptation,Is.Empty);
            MageCombatSystem.DamageTaken(source,target,at);Assert.That(target.MageState.Adaptation[source.Character.CharacterId].RemainingHits,Is.EqualTo(3));
        }
        [TestCase(0,-.15f)][TestCase(1,-.25f)][TestCase(2,-.4f)]
        public void FireArrowDamageReductionUsesResolvedTalentValue(int rank,float value)
        {
            var u=Unit();if(rank>0)Rank(u,true,"t5_1",rank);var skill=Skill("FireArrow");var original=skill.Effects.Single(e=>e.EffectType==EffectType.ModifyVar);
            Assert.That(MageCombatSystem.ResolveEffect(u,skill,original).ModifierValue,Is.EqualTo(value).Within(.00001));
            Assert.That(original.ModifierValue,Is.EqualTo(-.15f).Within(.00001));
        }
        [Test] public void MinimumAttackRollOverridesMaximumButDoesNotChangeAverageDotPower()
        {
            var a=Unit();var b=Unit(false,x:2);Rank(a,true,"t5_2",1);MageCombatSystem.ApplyMinimumRoll(a,b);
            b.Character.Stats.MinAttack=new Stat(20);b.Character.Stats.MaxAttack=new Stat(100);b.Character.Stats.CritChance=new Stat(0);
            Guarantee(b,UniqueBuffType.MageMaximumDamage);var session=Session(b,a);var skill=Skill("EnergyOrb");
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(b,a,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(20));Assert.That(preview.Damage.Maximum,Is.EqualTo(20));
            var hp=a.Character.CurrentHealth;Assert.That(new SkillExecutionSystem().TryExecute(session,b,skill,a),Is.True);
            Assert.That(hp-a.Character.CurrentHealth,Is.EqualTo(preview.Damage.Minimum));
            Assert.That(CombatDamageValueCalculator.CalculateDamageValue(b,DamageValueSource.AverageAttack,1),Is.EqualTo(60));
        }
        [Test] public void RealFireArrowAppliesMinimumOnlyAfterContactAndDebuffExpiresNormally()
        {
            var a=Unit();var b=Unit(false,x:2);var s=Session(a,b);Rank(a,true,"t5_2",1);
            Guarantee(b,UniqueBuffType.GuaranteedDodge);var exec=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());
            Assert.That(exec.TryExecute(s,a,Skill("FireArrow"),b),Is.True);Assert.That(StatusEffectSystem.HasStatus(b,MageCombatSystem.MinimumRoll),Is.False);
            a.CurrentActionPoints=a.MaxActionPoints=10000;a.Character.ResourcePool.CurrentValue=100;
            Assert.That(exec.TryExecute(s,a,Skill("FireArrow"),b),Is.True);Assert.That(StatusEffectSystem.HasStatus(b,MageCombatSystem.MinimumRoll),Is.True);
            Assert.That(b.Character.PassiveEffects.Single(e=>e.EffectId==MageCombatSystem.MinimumRoll).RemainingDuration,Is.EqualTo(4));
            new StatusEffectSystem().RemoveStatus(b,MageCombatSystem.MinimumRoll);Assert.That(MageCombatSystem.HasMinimumRoll(b),Is.False);
        }
        [TestCase(1,.1f)][TestCase(2,.35f)][TestCase(3,.6f)]
        public void AbsorptionSnapshotsDefenseBeforeShieldWithoutExtraCritRollOrConsumption(int rank,float coefficient)
        {
            var a=Unit(false);var b=Unit();new CharacterStatPipelineSystem().Refresh(b.Character);Rank(b,false,"t5_3",rank);
            b.Character.Stats.PhysicalDefense=new Stat(100);b.Character.ShieldPool.AddLayer(1000,"test",3);
            Guarantee(a,UniqueBuffType.GuaranteedCriticalHit);var s=Session(a,b);var ctx=new PassiveEffectCombatContext(s,a,b,damageType:DamageType.Physical);
            var hp=b.Character.CurrentHealth;var rng=UnityEngine.Random.state;
            var hypothetical=CombatDamageSystem.CalculateDodgedDamage(s,a,b,100,DamageType.Physical,ctx);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(1000));
            MageCombatSystem.AbsorbDodgedDamage(s,a,b,100,DamageType.Physical,ctx,roll:0);
            Assert.That(b.MageState.AbsorbedDamage,Is.EqualTo(Mathf.FloorToInt(hypothetical*coefficient)));
            Assert.That(b.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(1000));
            Assert.That(CombatDamageSystem.HasUniqueBuff(a,UniqueBuffType.GuaranteedCriticalHit),Is.True);
        }
        [Test] public void AbsorptionDoesNotProcFromDotsAlliesReactionsOrFailedChance()
        {
            var a=Unit(false);var b=Unit();var s=Session(a,b);Rank(b,false,"t5_3",3);
            MageCombatSystem.AbsorbDodgedDamage(s,a,b,100,DamageType.Burn,new PassiveEffectCombatContext(s,a,b),roll:0);
            MageCombatSystem.AbsorbDodgedDamage(s,b,b,100,DamageType.Physical,new PassiveEffectCombatContext(s,b,b),roll:0);
            MageCombatSystem.AbsorbDodgedDamage(s,a,b,100,DamageType.Physical,new PassiveEffectCombatContext(s,a,b,isPassiveReaction:true),roll:0);
            MageCombatSystem.AbsorbDodgedDamage(s,a,b,100,DamageType.Physical,new PassiveEffectCombatContext(s,a,b),roll:1);
            Assert.That(b.MageState.AbsorbedDamage,Is.Zero);
        }
        [Test] public void AbsorbedPowerIsAddedOnceAndDamagePreviewAgreesWithRealCast()
        {
            var a=Unit();var b=Unit(false,x:2);var s=Session(a,b);Rank(a,false,"t5_3",3);a.MageState.AbsorbedDamage=50;
            var skill=Skill("EnergyOrb");var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(a,b,skill).Damage.Minimum;
            Assert.That(a.MageState.AbsorbedDamage,Is.EqualTo(50));var hp=b.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem().TryExecute(s,a,skill,b),Is.True);
            Assert.That(hp-b.Character.CurrentHealth,Is.EqualTo(preview));Assert.That(a.MageState.AbsorbedDamage,Is.Zero);Assert.That(a.MageState.AttackBonus,Is.Zero);
        }
        [Test] public void AbsorptionCastScopePreservesBankForUtilityAndDuplicateButSpendsItOnFullMiss()
        {
            var a=Unit();var b=Unit(false,x:2);var s=Session(a,b);a.MageState.AbsorbedDamage=50;
            using(new MageCombatSystem.Tier5CastScope(s,a,Skill("Shield"),"heal",false))Assert.That(a.MageState.AbsorbedDamage,Is.EqualTo(50));
            using(new MageCombatSystem.Tier5CastScope(s,a,Skill("EnergyOrb"),"copy",true))Assert.That(a.MageState.AttackBonus,Is.Zero);
            Assert.That(a.MageState.AbsorbedDamage,Is.EqualTo(50));
            using(new MageCombatSystem.Tier5CastScope(s,a,Skill("EnergyOrb"),"miss",false))new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Magical,false);
            Assert.That(a.MageState.AbsorbedDamage,Is.Zero);Assert.That(a.MageState.AttackBonus,Is.Zero);
        }
        [Test] public void AbsorptionIsNotMultipliedAcrossHitsOfOneUse()
        {
            var a=Unit();var b=Unit(false,x:2);var s=Session(a,b);a.MageState.AbsorbedDamage=50;var hp=b.Character.CurrentHealth;
            using(new MageCombatSystem.Tier5CastScope(s,a,Skill("EnergyOrb"),"multi",false))
            {var damage=new CombatDamageSystem();damage.ResolveAndApplyDamage(s,a,b,100,DamageType.Magical,true);damage.ResolveAndApplyDamage(s,a,b,100,DamageType.Magical,true);}
            Assert.That(hp-b.Character.CurrentHealth,Is.EqualTo(250));
        }
        [Test] public void BurnExtensionAddsOneFullTickFromAnotherOwnerAndOnlyOneAttemptPerUse()
        {
            var a=Unit();var owner=Unit();var b=Unit(false,x:2);var s=Session(a,owner,b);Rank(a,true,"t5_3",2);Rank(a,true,"arcana",3);
            var burn=new PassiveEffectRuntime(StatusEffectIds.Burn,new(){PassiveEffectSourceFactory.FromRuntimeSystem(owner,"test.burn")},1,4,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnStart);
            burn.SetPayload(new DamageOverTimePayload{RemainingTotalDamage=360,DamagePerTurn=90});b.Character.PassiveEffects.Add(burn);
            var ctx=new PassiveEffectCombatContext(s,a,b,skill:Skill("EnergyOrb"),actionId:"multi",damageType:DamageType.Magical);
            MageCombatSystem.ExtendBurnOnContact(a,b,ctx,0);MageCombatSystem.ExtendBurnOnContact(a,b,ctx,0);
            Assert.That(burn.RemainingDuration,Is.EqualTo(5));Assert.That(burn.GetPayload<DamageOverTimePayload>().RemainingTotalDamage,Is.EqualTo(450));
            Assert.That(burn.Sources[0].SourceCharacterId,Is.EqualTo(owner.Character.CharacterId));
            var failed=new PassiveEffectCombatContext(s,a,b,skill:Skill("EnergyOrb"),actionId:"failed",damageType:DamageType.Magical);
            MageCombatSystem.ExtendBurnOnContact(a,b,failed,1);MageCombatSystem.ExtendBurnOnContact(a,b,failed,0);Assert.That(burn.RemainingDuration,Is.EqualTo(5));
        }
        [TestCase(0,1f)][TestCase(1,1.15f)][TestCase(2,1.3f)][TestCase(3,1.5f)]
        public void LavaAffectsBothFirePillarDamageAndItsBurnButDoesNotMutateAsset(int rank,float multiplier)
        {
            var a=Unit();if(rank>0)Rank(a,true,"t5_4",rank);var skill=Tier5Skill("f5");
            Assert.That(MageCombatSystem.ResolveEffect(a,skill,skill.Effects[0]).DamageMultiplier,Is.EqualTo(multiplier).Within(.00001));
            Assert.That(MageCombatSystem.ResolveEffect(a,skill,skill.Effects[1]).StatusDamageMultiplier,Is.EqualTo(.9f*multiplier).Within(.00001));
            Assert.That(skill.Effects[0].DamageMultiplier,Is.EqualTo(1));Assert.That(skill.Effects[1].StatusDamageMultiplier,Is.EqualTo(.9f));
        }
        [Test] public void FirePillarUsesRealSquareThreeGeometryAndOnlyEnemies()
        {
            var a=Unit(x:1,y:1);var b=Unit(false,x:3,y:3);var c=Unit(false,x:4,y:4);var far=Unit(false,x:5,y:5);var ally=Unit(x:3,y:4);var s=Session(a,b,c,far,ally);
            var skill=Tier5Skill("f5");var center=new GridPosition(3,3);
            var targets=new CombatTargetingService().GetTargets(s,a,skill.TargetModes[0],center);
            Assert.That(targets,Is.EquivalentTo(new[]{b,c}));
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,a,skill,
                new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}},null,null,new Dictionary<int,GridPosition?>{{0,center}}),Is.True);
            Assert.That(b.Character.CurrentHealth,Is.LessThan(500));Assert.That(c.Character.CurrentHealth,Is.LessThan(500));
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(500));Assert.That(far.Character.CurrentHealth,Is.EqualTo(500));
        }
        [Test] public void TeleportRejectsInvalidRangeBeforeCostAndAdvancesMovedAllysNextTurn()
        {
            var a=Unit();var ally=Unit(x:2);var foe=Unit(false,x:8);var s=Session(a,ally,foe);Rank(a,false,"t5_5",2);var skill=Tier5Skill("w4");
            var exec=new SkillExecutionSystem();var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{ally}}};
            Assert.That(exec.TryExecute(s,a,skill,targets,new GridPosition(12,2)),Is.False);Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            ally.CurrentActionValue=1000;var expected=1000-.5f*CombatTurnSystem.CalculateBaseActionValue(ally);
            Assert.That(exec.TryExecute(s,a,skill,targets,new GridPosition(5,2)),Is.True);Assert.That(ally.Position,Is.EqualTo(new GridPosition(5,2)));
            Assert.That(ally.CurrentActionValue,Is.EqualTo(expected).Within(.00001));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(25));
        }
        [Test] public void SelfTeleportAdvanceIsDeferredAndInvalidTargetsDoNotSpendMana()
        {
            var a=Unit();var foe=Unit(false,x:2);var s=Session(a,foe);Rank(a,false,"t5_5",1);var skill=Tier5Skill("w4");var exec=new SkillExecutionSystem();
            Assert.That(exec.TryExecute(s,a,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{foe}}},new GridPosition(4,2)),Is.False);
            Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(100));var expected=.25f*CombatTurnSystem.CalculateBaseActionValue(a);
            Assert.That(exec.TryExecute(s,a,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{a}}},new GridPosition(4,2)),Is.True);
            Assert.That(a.PendingEquipmentActionValueReduction,Is.EqualTo(expected).Within(.00001));
        }
    }
}
