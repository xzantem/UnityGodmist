using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class ScoutContentTests
    {
        static void G(BattleUnit u,string slot,int rank)=>Rank(u,"guardian",slot,rank);
        static CombatSession GS(params BattleUnit[] units)
        {var s=Session(units);foreach(var u in units)u.WarriorState.Session=s;return s;}
        static SkillData GuardianSkill(string slot)=>Skill("ScoutGuardian_"+slot);
        static void PrepareCounter(BattleUnit u)=>GuardianCombatSystem.Execute(u,u,GuardianSkill("t5_4"),new StatusEffectSystem(),new CombatShieldSystem());
        static UnityEngine.Random.State ContactRoll(float min=.2f,float max=.8f)
        {
            for(int seed=1;seed<1000;seed++)
            {UnityEngine.Random.InitState(seed);var state=UnityEngine.Random.state;var roll=UnityEngine.Random.value;if(roll>min&&roll<max){UnityEngine.Random.state=state;return state;}}
            throw new InvalidOperationException("No deterministic contact roll");
        }
        [Test]
        public void GuardianStaticStatsAndBreakdownUseInvestedPointsWithoutCompoundingRefresh()
        {
            var u=Unit(true,1);new CharacterStatPipelineSystem().Refresh(u.Character);
            var dodge=u.Character.Stats.Dodge.GetValue();var phys=u.Character.Stats.PhysicalDefense.GetValue();var magic=u.Character.Stats.MagicalDefense.GetValue();
            G(u,"t1_1",2);G(u,"t1_5",2);new CharacterStatPipelineSystem().Refresh(u.Character);
            Assert.That(u.Character.Stats.Dodge.GetValue(),Is.EqualTo(dodge+16));
            Assert.That(u.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(phys*1.02f).Within(.001));
            Assert.That(u.Character.Stats.MagicalDefense.GetValue(),Is.EqualTo(magic*1.02f*1.3f).Within(.001));
            var once=u.Character.Stats.MagicalDefense.GetValue();new CharacterStatPipelineSystem().Refresh(u.Character);
            Assert.That(u.Character.Stats.MagicalDefense.GetValue(),Is.EqualTo(once));
        }
        [Test]
        public void GuardianShieldUsesRecipientHealthRefreshesAndSharesOnlyWhenProtectingAnother()
        {
            var a=Unit(true,1);var b=Unit(true,2);GS(a,b);G(a,"t2_3",2);G(a,"t3_3",1);
            b.Character.Stats.MaxHealth=new Stat(20000);b.Character.CurrentHealth=20000;
            var shields=new CombatShieldSystem();
            Assert.That(GuardianCombatSystem.Execute(a,b,GuardianSkill("t1_3"),new StatusEffectSystem(),shields),Is.True);
            Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(3000));
            Assert.That(b.Character.ShieldPool.Layers.Single().RemainingTurns,Is.EqualTo(5));
            Assert.That(a.Character.ShieldPool.TotalShield,Is.EqualTo(500));
            GuardianCombatSystem.Execute(a,b,GuardianSkill("t1_3"),new StatusEffectSystem(),shields);
            Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(3000));Assert.That(a.Character.ShieldPool.TotalShield,Is.EqualTo(500));
            a.Character.ShieldPool.RemoveLayersBySourceId("scout.guardian.front");
            GuardianCombatSystem.Execute(a,a,GuardianSkill("t1_3"),new StatusEffectSystem(),shields);
            Assert.That(a.Character.ShieldPool.Layers.Any(l=>l.SourceId=="scout.guardian.front"),Is.False);
        }
        [Test]
        public void GuardianMultiHitPreviewConsumesFirstHitProtectionOnlyOnFirstHitWithoutMutatingState()
        {
            var a=Unit(true,1);var b=Unit(false,2);GS(a,b);G(b,"t1_2",2);
            var skill=Skill("ScoutQuickStrikes");
            var protectedPreview=CombatSkillPreviewCalculator.CalculateTargetPreview(a,b,skill).Damage;
            Assert.That(b.GuardianState.FirstHitUsed,Is.False);
            b.GuardianState.FirstHitUsed=true;
            var unprotectedPreview=CombatSkillPreviewCalculator.CalculateTargetPreview(a,b,skill).Damage;
            Assert.That(protectedPreview.Minimum,Is.EqualTo(unprotectedPreview.Minimum-10));
            Assert.That(protectedPreview.Maximum,Is.EqualTo(unprotectedPreview.Maximum-20));
        }
        [Test]
        public void GuardianRealCastPaysDiscountOnceAndInvalidCastDoesNotConsumeIt()
        {
            var a=Unit(true,1);var ally=Unit(true,2);var enemy=Unit(false,8);var s=GS(a,ally,enemy);
            a.Character.ResourcePool.CurrentValue=100;G(a,"t5_1",2);
            a.Character.Stats.Speed=new Stat(10000f/(s.ActionPointsPerSpeed*ActionPointUnits.PerPoint));
            var skill=GuardianSkill("t1_3");var exec=new SkillExecutionSystem();
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(a,skill),Is.EqualTo(28));
            Assert.That(exec.TryExecute(s,a,skill,enemy),Is.False);Assert.That(a.GuardianState.DiscountUsed,Is.False);
            Assert.That(exec.TryExecute(s,a,skill,ally),Is.True);
            Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(72));Assert.That(a.CurrentActionPoints,Is.EqualTo(6000));
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(a,skill),Is.EqualTo(35));
            GuardianCombatSystem.TurnStart(a);Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(a,skill),Is.EqualTo(28));
        }
        [TestCase("t1_4",55,3000)][TestCase("t5_4",70,6000)]
        public void GuardianOtherActivesUseRealExecutionCostsAndTargets(string slot,int cost,int apLeft)
        {
            var a=Unit(true,1);var foe=Unit(false,2);var session=GS(a,foe);a.Character.ResourcePool.CurrentValue=100;
            a.Character.Stats.Speed=new Stat(10000f/(session.ActionPointsPerSpeed*ActionPointUnits.PerPoint));
            var target=slot=="t1_4"?foe:a;
            Assert.That(new SkillExecutionSystem().TryExecute(session,a,GuardianSkill(slot),target),Is.True);
            Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(100-cost));Assert.That(a.CurrentActionPoints,Is.EqualTo(apLeft));
        }
        [Test]
        public void GuardianInvalidRangeConsumesNeitherDiscountNorCosts()
        {
            var a=Unit(true,1);var ally=Unit(true,8);var session=GS(a,ally);G(a,"t5_1",2);
            Assert.That(new SkillExecutionSystem().TryExecute(session,a,GuardianSkill("t1_3"),ally),Is.False);
            Assert.That(a.GuardianState.DiscountUsed,Is.False);Assert.That(a.CurrentActionPoints,Is.EqualTo(10000));
            Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(200));
        }
        [TestCase(1,27)][TestCase(2,55)]
        public void FailedTauntRefundsActualSpentImpetusWithoutRefundingAp(int rank,int refund)
        {
            var a=Unit(true,1);var b=Unit(false,2);GS(a,b);G(a,"t4_4",rank);a.Character.ResourcePool.CurrentValue=20;
            a.GuardianState.PaidResourceThisAction=55;
            Assert.That(GuardianCombatSystem.Execute(a,b,GuardianSkill("t1_4"),new StatusEffectSystem(),new CombatShieldSystem(),1f),Is.False);
            Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(20+refund));Assert.That(a.CurrentActionPoints,Is.EqualTo(10000));
        }
        [Test]
        public void GuardianTauntAccuracyAndReductionFollowItsLivingOwnerAndDuration()
        {
            var a=Unit(true,1);var b=Unit(false,2);var s=GS(a,b);G(a,"t2_4",3);G(a,"t3_4",2);
            Assert.That(GuardianCombatSystem.Execute(a,b,GuardianSkill("t1_4"),new StatusEffectSystem(),new CombatShieldSystem(),0),Is.True);
            Assert.That(GuardianCombatSystem.AccuracyPenalty(b,s),Is.EqualTo(15));
            Assert.That(GuardianCombatSystem.IncomingMultiplier(s,b,a,DamageType.Physical),Is.EqualTo(.8f).Within(.0001));
            new StatusEffectSystem().RemoveStatus(b,StatusEffectIds.Taunt);
            Assert.That(GuardianCombatSystem.AccuracyPenalty(b,s),Is.Zero);Assert.That(GuardianCombatSystem.IncomingMultiplier(s,b,a,DamageType.Physical),Is.EqualTo(1));
        }
        [TestCase(StatusEffectIds.Stun)][TestCase(StatusEffectIds.Immobilize)][TestCase(StatusEffectIds.Sleep)]
        [TestCase(StatusEffectIds.Paralysis)][TestCase(StatusEffectIds.Taunt)][TestCase("fear")]
        public void NeutralizationReducesBaseControlChanceBeforeResistanceAndClampsAtZero(string status)
        {
            var a=Unit(true,1);G(a,"t2_5",2);
            Assert.That(GuardianCombatSystem.StatusBaseChance(a,status,.6f),Is.EqualTo(.4f).Within(.0001));
            Assert.That(GuardianCombatSystem.StatusBaseChance(a,status,.1f),Is.Zero);
            Assert.That(GuardianCombatSystem.StatusBaseChance(a,StatusEffectIds.Poison,.6f),Is.EqualTo(.6f));
        }
        [Test]
        public void CautionConsumesFirstDirectHitIncludingShieldButNotDamageOverTime()
        {
            var a=Unit(false,1);var b=Unit(true,2);var s=GS(a,b);G(b,"t1_2",2);var damage=new CombatDamageSystem();
            b.Character.ShieldPool.AddLayer(1000);
            damage.ApplyDamage(s,a,b,100,DamageType.Poison);Assert.That(b.GuardianState.FirstHitUsed,Is.False);
            damage.ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,true);
            Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(820));Assert.That(b.GuardianState.FirstHitUsed,Is.True);
            damage.ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,true);
            Assert.That(b.Character.ShieldPool.TotalShield,Is.EqualTo(720));
            GuardianCombatSystem.TurnStart(b);Assert.That(b.GuardianState.FirstHitUsed,Is.False);
        }
        [Test]
        public void StatisticalDodgeRewardsOnceAndOrdinaryAccuracyMissDoesNot()
        {
            var a=Unit(false,1);var b=Unit(true,3);var s=GS(a,b);G(b,"t2_2",1);G(b,"t4_2",2);b.Character.ResourcePool.CurrentValue=50;
            a.Character.WeaponAccuracy=100;b.Character.Stats.Dodge=new Stat(10000);
            ContactRoll();Assert.That(new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,100f).Hit,Is.False);
            Assert.That(GuardianCombatSystem.HasFreeStep(b),Is.True);Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(60));
            ContactRoll();new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,100f);
            Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(60));
            GuardianCombatSystem.TurnStart(b);b.GuardianState.HasFreeStep=false;new StatusEffectSystem().RemoveStatus(b,GuardianCombatSystem.FreeStepMarker);
            b.Character.Stats.Dodge=new Stat(0);a.Character.WeaponAccuracy=1;
            ContactRoll();new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,100f);
            Assert.That(b.GuardianState.DodgeRewardUsed,Is.False);Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(60));
        }
        [Test]
        public void FreeStepHasNoApOrMomentumCostAndDoesNotIncreaseNextPaidStepOrdinal()
        {
            var a=Unit(true,1);var enemy=Unit(false,8);var s=GS(a,enemy);G(a,"t2_2",1);G(a,"t3_2",1);
            a.Character.ResourcePool.CurrentValue=50;GuardianCombatSystem.Recovered(a,1);
            var movement=new CombatMovementSystem(resourceRuleSystem:new ResourceRuleSystem());
            Assert.That(movement.TryMoveOneStep(s,a,1,0),Is.True);
            Assert.That(a.CurrentActionPoints,Is.EqualTo(10000));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(50));
            Assert.That(a.MovementStepsThisTurn,Is.EqualTo(1));Assert.That(GuardianCombatSystem.PaidSteps(a),Is.Zero);
            Assert.That(CombatActionCostCalculator.CalculateMovementCost(a),Is.EqualTo(100));
            Assert.That(movement.TryMoveOneStep(s,a,1,0),Is.True);
            Assert.That(a.CurrentActionPoints,Is.EqualTo(9900));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(52));
            GuardianCombatSystem.Recovered(a,1);Assert.That(GuardianCombatSystem.HasFreeStep(a),Is.False);
        }
        [Test]
        public void PathPreviewPredictsFreeStepEarnedByRecoveringKnifeInsideThePath()
        {
            var a=Unit(true,1);var enemy=Unit(false,8);var s=GS(a,enemy);G(a,"t2_2",1);G(a,"t3_2",1);
            a.Character.ResourcePool.CurrentValue=50;a.AvailableKnives=4;
            var path=new[]{new GridPosition(2,2),new GridPosition(3,2)};
            s.MapObjects.Add(new KnifeCombatMapObject("recovery",path[0],a.Character.CharacterId,1));
            var preview=CombatMovementSystem.CalculatePathCost(s,a.Position,0,path,0,0,0,a);
            Assert.That(preview,Is.EqualTo(100));
            Assert.That(new CombatMovementSystem(resourceRuleSystem:new ResourceRuleSystem()).TryMoveAlongPath(s,a,path),Is.True);
            Assert.That(10000-a.CurrentActionPoints,Is.EqualTo(preview));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(60));
        }
        [Test]
        public void FreeStepSharesRecoveryAndDodgeLimitExpiresAndCannotBypassImmobilization()
        {
            var a=Unit(true,1);var enemy=Unit(false,8);var s=GS(a,enemy);G(a,"t2_2",1);G(a,"t3_2",1);
            GuardianCombatSystem.Recovered(a,1);GuardianCombatSystem.Dodged(s,a,enemy,new PassiveEffectCombatContext(s,enemy,a),new CombatDamageSystem());
            Assert.That(a.GuardianState.StepAwarded,Is.True);
            new StatusEffectSystem().ApplyStatus(enemy,a,StatusEffectIds.Immobilize,1,3,1,0,0,"test",sharedRoll:0);
            Assert.That(new CombatMovementSystem().TryMoveOneStep(s,a,1,0),Is.False);Assert.That(GuardianCombatSystem.HasFreeStep(a),Is.True);
            GuardianCombatSystem.TurnStart(a);GuardianCombatSystem.TurnEnd(a);Assert.That(GuardianCombatSystem.HasFreeStep(a),Is.False);
        }
        [Test]
        public void SureStepAppliesOnlyAfterPaidMovementAndEndsAtNextOwnStartOrCombatCleanup()
        {
            var a=Unit(true,1);var enemy=Unit(false,8);var s=GS(a,enemy);G(a,"t2_1",2);new CharacterStatPipelineSystem().Refresh(a.Character);
            var baseline=a.Character.Stats.Dodge.GetValue();a.Character.ResourcePool.CurrentValue=50;
            var movement=new CombatMovementSystem(resourceRuleSystem:new ResourceRuleSystem());movement.TryMoveOneStep(s,a,1,0);
            Assert.That(a.Character.Stats.Dodge.GetValue(),Is.EqualTo(baseline+8));movement.TryMoveOneStep(s,a,1,0);
            Assert.That(a.Character.Stats.Dodge.GetValue(),Is.EqualTo(baseline+8));
            GuardianCombatSystem.TurnStart(a);Assert.That(a.Character.Stats.Dodge.GetValue(),Is.EqualTo(baseline));
            GuardianCombatSystem.Moved(a,false,100);new PassiveEffectLifecycleSystem().RemoveCombatScopedEffects(s);
            Assert.That(StatusEffectSystem.HasStatus(a,GuardianCombatSystem.SureStep),Is.False);
        }
        [Test]
        public void PartyPositionBonusesAreLiveAndDoNotIncludeSummons()
        {
            var owner=Unit(true,1);var ally=Unit(true,2);var enemy=Unit(false,8);var s=GS(owner,ally,enemy);G(owner,"t4_3",2);G(owner,"t5_3",2);G(owner,"t4_5",1);
            Assert.That(GuardianCombatSystem.DodgeBonus(owner),Is.EqualTo(10));Assert.That(GuardianCombatSystem.DodgeBonus(ally),Is.EqualTo(10));
            Assert.That(GuardianCombatSystem.DefenseMultiplier(owner),Is.EqualTo(1.2f).Within(.001));
            Assert.That(GuardianCombatSystem.IncomingMultiplier(s,enemy,ally,DamageType.Physical),Is.EqualTo(.9f).Within(.001));
            ally.IsAiControlledSummon=true;
            Assert.That(GuardianCombatSystem.DodgeBonus(owner),Is.Zero);Assert.That(GuardianCombatSystem.DodgeBonus(ally),Is.Zero);
            Assert.That(GuardianCombatSystem.DefenseMultiplier(owner),Is.EqualTo(1));
        }
        [Test]
        public void GroundKnifeProtectionTakesStrongestOwnerAndStopsImmediatelyAfterPickupOrOwnerDeath()
        {
            var a=Unit(true,1);var b=Unit(true,4);var ally=Unit(true,2);var foe=Unit(false,8);var s=GS(a,b,ally,foe);G(a,"t5_2",3);G(b,"t5_2",2);
            s.MapObjects.Add(new KnifeCombatMapObject("a",ally.Position,a.Character.CharacterId,1));s.MapObjects.Add(new KnifeCombatMapObject("b",ally.Position,b.Character.CharacterId,1));
            Assert.That(GuardianCombatSystem.IncomingMultiplier(s,foe,ally,DamageType.Physical),Is.EqualTo(.9f).Within(.001));
            a.Character.LifeState=CharacterLifeState.Dead;
            Assert.That(GuardianCombatSystem.IncomingMultiplier(s,foe,ally,DamageType.Physical),Is.EqualTo(.92f).Within(.001));
            s.MapObjects.Clear();Assert.That(GuardianCombatSystem.IncomingMultiplier(s,foe,ally,DamageType.Physical),Is.EqualTo(1));
            Assert.That(GuardianCombatSystem.IncomingMultiplier(s,foe,ally,DamageType.Poison),Is.EqualTo(1));
        }
        [Test]
        public void MagicDodgeUsesHalfDodgeAndAppliesToAlwaysHitsButNotInevitableDamage()
        {
            var a=Unit(false,1);var b=Unit(true,2);var s=GS(a,b);b.Character.Stats.Dodge=new Stat(200);
            Assert.That(CombatDamageSystem.ContactChance(a,b,DamageType.Magical,1,null,s),Is.EqualTo(1));G(b,"t3_5",1);
            Assert.That(CombatDamageSystem.ContactChance(a,b,DamageType.Magical,1,null,s),Is.EqualTo(.5f).Within(.001));
            ContactRoll(.6f,.9f);Assert.That(new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Magical,true).Hit,Is.False);
            Assert.That(CombatDamageSystem.ContactChance(a,b,DamageType.Inevitable,1,null,s),Is.EqualTo(1));
        }
        [TestCase(DamageType.Physical)][TestCase(DamageType.Magical)]
        public void CounterUsesPreCriticalValueOnceEvenWhenAttackMisses(DamageType type)
        {
            var a=Unit(false,1);var b=Unit(true,2);var s=GS(a,b);PrepareCounter(b);G(b,"t5_5",2);
            a.Character.Stats.MagicalDefense=new Stat(0);
            var damage=new CombatDamageSystem();var hp=a.Character.CurrentHealth;
            damage.ResolveAndApplyDamage(s,a,b,100,type,false);
            Assert.That(a.Character.CurrentHealth,Is.EqualTo(hp-250));Assert.That(b.Character.CurrentHealth,Is.EqualTo(5000));
            damage.ResolveAndApplyDamage(s,a,b,100,type,false);Assert.That(a.Character.CurrentHealth,Is.EqualTo(hp-250));
        }
        [Test]
        public void CounterReturnsCapturedValueWithoutMultiplyingByDefendersOutgoingBuffs()
        {
            var a=Unit(false,1);var b=Unit(true,2);var session=GS(a,b);PrepareCounter(b);
            new ModifyVarSystem().ApplyFromSummonedObject(b,b,GameplayVariableType.DamageDealtPercent,StatModifierStacking.Additive,1f,3,"test.guardian.offense");
            new CombatDamageSystem().ResolveAndApplyDamage(session,a,b,100,DamageType.Physical,false);
            Assert.That(a.Character.CurrentHealth,Is.EqualTo(4900));
        }
        [TestCase(0)][TestCase(100)]
        public void CounterReportsActualPreDefenseValueToArmoredMitigationBank(int defense)
        {
            var a=Unit(false,1);var b=Unit(true,2);var session=GS(a,b);PrepareCounter(b);
            a.Character.SkillTreeProgress.Add(new Godmist.Runtime.Characters.SkillTreeProgress{TreeId=WarriorCombatSystem.ManAtArms,
                NodeInvestments=new(){new Godmist.Runtime.Characters.SkillTreeNodeInvestment{NodeId="node.warrior.man_at_arms.t1_1",Ranks=1}}});
            a.Character.Stats.PhysicalDefense=new Stat(defense);
            new ModifyVarSystem().ApplyFromSummonedObject(b,b,GameplayVariableType.DamageDealtPercent,StatModifierStacking.Additive,1f,3,"test.guardian.offense");
            var hp=a.Character.CurrentHealth;
            new CombatDamageSystem().ResolveAndApplyDamage(session,a,b,100,DamageType.Physical,false);
            Assert.That(a.WarriorState.MitigatedBank,Is.EqualTo((100-(hp-a.Character.CurrentHealth))*.02).Within(.00001));
        }
        [Test]
        public void CounterDoesNotReduceIncomingDamageAndCannotChainAnotherCounter()
        {
            var a=Unit(false,1);var b=Unit(true,2);var s=GS(a,b);PrepareCounter(a);PrepareCounter(b);
            new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,true);
            Assert.That(a.Character.CurrentHealth,Is.EqualTo(4900));Assert.That(b.Character.CurrentHealth,Is.EqualTo(4900));
            Assert.That(a.GuardianState.CounterTurns,Is.EqualTo(2));Assert.That(b.GuardianState.CounterTurns,Is.Zero);
        }
        [Test]
        public void CounterSurvivesOneOwnEndExpiresAtNextEndAndDispelCancelsIt()
        {
            var a=Unit(false,1);var b=Unit(true,2);var s=GS(a,b);PrepareCounter(b);GuardianCombatSystem.TurnEnd(b);
            Assert.That(b.GuardianState.CounterTurns,Is.EqualTo(1));GuardianCombatSystem.TurnEnd(b);Assert.That(b.GuardianState.CounterTurns,Is.Zero);
            PrepareCounter(b);new StatusEffectSystem().RemoveStatus(b,GuardianCombatSystem.CounterMarker);
            new CombatDamageSystem().ResolveAndApplyDamage(s,a,b,100,DamageType.Physical,false);Assert.That(a.Character.CurrentHealth,Is.EqualTo(5000));
        }
        [Test]
        public void RetaliationIsOncePerIntervalPaysOnlyKnifeAndDoesNotConsumeRollOrRecoveredStrength()
        {
            var a=Unit(true,1);var foe=Unit(false,3);var s=GS(a,foe);G(a,"t3_1",2);Rank(a,"assassin","t5_4",1);Rank(a,"assassin","throat_cut",3);Rank(a,"blade_dancer","t5_2",3);
            a.ScoutState.RecoveredStacks=3;ScoutCombatSystem.GrantRollCritical(a);a.Character.ResourcePool.CurrentValue=50;
            var context=new PassiveEffectCombatContext(s,foe,a);ContactRoll();GuardianCombatSystem.Dodged(s,a,foe,context,new CombatDamageSystem());
            Assert.That(a.AvailableKnives,Is.EqualTo(4));Assert.That(a.CurrentActionPoints,Is.EqualTo(10000));Assert.That(a.Character.ResourcePool.CurrentValue,Is.EqualTo(50));
            Assert.That(ScoutCombatSystem.HasRollCritical(a),Is.True);Assert.That(a.ScoutState.RecoveredStacks,Is.EqualTo(3));
            Assert.That(StatusEffectSystem.HasStatus(foe,StatusEffectIds.Stun),Is.False);
            GuardianCombatSystem.Dodged(s,a,foe,context,new CombatDamageSystem());Assert.That(a.AvailableKnives,Is.EqualTo(4));
        }
    }
}
