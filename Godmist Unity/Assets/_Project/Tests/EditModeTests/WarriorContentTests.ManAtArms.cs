using System;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class WarriorContentTests
    {
        [TestCase("t1_2")][TestCase("t2_3")][TestCase("t4_1")][TestCase("t5_3")]
        public void ManAtArmsCastPosesResolve(string slot)
        {
            foreach(var facing in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right,
                new Vector2(1,1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(-1,-1)})
            {
                Assert.That(Godmist.Views.Characters.CombatPoseResolver.TryResolveSkill("wladimir",
                    "skill.warrior.man_at_arms."+slot,facing,AttackType.Melee,false,out var pose),Is.True,slot+facing);
                Assert.That(pose.Actor,Is.Not.Null,slot+facing);
            }
        }
        static void ArmoredRank(BattleUnit u,string slot,int rank=1)
        {
            var p=u.Character.SkillTreeProgress.Find(p=>p.TreeId==WarriorCombatSystem.ManAtArms);
            if(p==null){p=new SkillTreeProgress{TreeId=WarriorCombatSystem.ManAtArms};u.Character.SkillTreeProgress.Add(p);}
            p.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId="node.warrior.man_at_arms."+slot,Ranks=rank});
        }
        static SkillData ArmoredTransient(string id)
        {
            var s=ScriptableObject.CreateInstance<SkillData>();
            typeof(SkillData).GetField("<SkillId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,id);
            return s;
        }
        [Test] public void ManAtArmsStatsCombinePerPointHealthAndRankedDefenses()
        {
            var u=Unit();ArmoredRank(u,"t1_1",3);ArmoredRank(u,"t4_3",2);
            u.Character.Stats.PhysicalDefense=new Stat(100);
            WarriorCombatSystem.ApplyStats(u.Character);
            Assert.That(u.Character.MaximumHealth,Is.EqualTo(1290));
            Assert.That(u.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(140).Within(.01));
        }
        [Test] public void ManAtArmsBankExcludesShieldAbsorptionAndIncludesDefense()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t1_1");
            u.Character.ShieldPool.AddLayer(100);
            new CombatDamageSystem().ApplyDamage(Session(u,foe),foe,u,50,DamageType.Physical,preMitigationDamage:100);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(500));
            Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(50));
            Assert.That(u.WarriorState.MitigatedBank,Is.EqualTo(1).Within(.001));
        }
        [Test] public void ManAtArmsBankIsSpentOnAttackUseEvenWithoutAHitAndNeverTwice()
        {
            var u=Unit();ArmoredRank(u,"t1_1");u.WarriorState.MitigatedBank=12.75;
            using(new WarriorCombatSystem.ArmoredAttackScope(u,Skill("Cut"),false))
            {
                Assert.That(WarriorCombatSystem.ConsumeArmoredAttack(u),Is.EqualTo(12));
                Assert.That(WarriorCombatSystem.ConsumeArmoredAttack(u),Is.Zero);
            }
            Assert.That(u.WarriorState.MitigatedBank,Is.Zero);
            u.WarriorState.MitigatedBank=30;
            using(new WarriorCombatSystem.ArmoredAttackScope(u,Skill("Cut"),false)){}
            Assert.That(u.WarriorState.MitigatedBank,Is.Zero);
            Assert.That(u.WarriorState.AttackBank,Is.Zero);
        }
        [Test] public void ManAtArmsBankUsesAttackerBonusesBeforeMeasuringDefenseReduction()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t1_1");u.Character.ShieldPool.AddLayer(200);
            WarriorCombatSystem.Variable(foe,foe,GameplayVariableType.DamageDealtPercent,1,3,"test.outgoing","Premia");
            new CombatDamageSystem().ApplyDamage(Session(u,foe),foe,u,50,DamageType.Physical,preMitigationDamage:100);
            Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(100));
            Assert.That(u.WarriorState.MitigatedBank,Is.EqualTo(2).Within(.001));
        }
        [TestCase(0f,30)][TestCase(1f,100)]
        public void ManAtArmsPhysicalBlockReducesSeventyPercent(float roll,int expected)
        {
            var u=Unit();ArmoredRank(u,"t2_1",2);
            Assert.That(WarriorCombatSystem.ArmoredIncoming(null,u,100,DamageType.Physical,"skill.test",roll),Is.EqualTo(expected));
        }
        [Test] public void ManAtArmsMagicAvoidDoesNotRollForPhysicalOrPeriodicDamage()
        {
            var u=Unit();ArmoredRank(u,"t2_5",2);
            Assert.That(WarriorCombatSystem.ArmoredAvoidMagic(u,DamageType.Magical,0),Is.True);
            Assert.That(WarriorCombatSystem.ArmoredAvoidMagic(u,DamageType.Physical,0),Is.False);
            Assert.That(WarriorCombatSystem.ArmoredAvoidMagic(u,DamageType.Burn,0),Is.False);
        }
        [Test] public void ManAtArmsVampirismUsesLostHealthAndOverhealRefreshesOnlyItsOwnShield()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t4_5",2);ArmoredRank(u,"t4_4");
            u.Character.CurrentHealth=995;u.Character.ShieldPool.AddLayer(50,"unrelated",2);
            var damage=new CombatDamageSystem();var s=Session(u,foe);
            damage.ApplyDamage(s,u,foe,100,DamageType.Physical);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(1000));
            Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(55));
            damage.ApplyDamage(s,u,foe,100,DamageType.Physical);
            Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(65));
            Assert.That(u.Character.ShieldPool.Layers.Single(l=>l.SourceId=="unrelated").RemainingTurns,Is.EqualTo(2));
            Assert.That(u.Character.ShieldPool.Layers.Last().RemainingTurns,Is.EqualTo(5));
        }
        [Test] public void ManAtArmsVampirismRejectsShieldDamageAndReactionDamage()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t4_5",2);
            foe.Character.ShieldPool.AddLayer(100);
            var damage=new CombatDamageSystem();var s=Session(u,foe);
            damage.ApplyDamage(s,u,foe,100,DamageType.Physical);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(500));
            damage.ApplyDamage(s,u,foe,100,DamageType.Physical,allowWarriorProcs:false,allowVampirism:false);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(500));
        }
        [TestCase(499,519)][TestCase(500,510)]
        public void ManAtArmsTasteOfBloodUsesStrictPreHealingThreshold(int health,int expected)
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t4_5",2);ArmoredRank(u,"t5_5",2);
            u.Character.CurrentHealth=health;
            new CombatDamageSystem().ApplyDamage(Session(u,foe),u,foe,100,DamageType.Physical);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(expected));
        }
        [Test] public void ManAtArmsRevivePreservesLifeAndOnlySuccessfulRollStartsCooldown()
        {
            var u=Unit();ArmoredRank(u,"t5_4");u.Character.CurrentHealth=0;
            Assert.That(WarriorCombatSystem.TryArmoredRevive(u,1),Is.False);
            Assert.That(u.WarriorState.ReviveCooldown,Is.Zero);
            Assert.That(WarriorCombatSystem.TryArmoredRevive(u,0),Is.True);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(250));
            Assert.That(u.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(WarriorCombatSystem.TryArmoredRevive(u,0),Is.False);
            for(var i=0;i<5;i++)WarriorCombatSystem.ArmoredTurnEnd(u);
            u.Character.CurrentHealth=0;
            Assert.That(WarriorCombatSystem.TryArmoredRevive(u,0),Is.True);
        }
        [Test] public void ManAtArmsAdaptationOnlyProtectsAgainstTheSameSkillInNextTurn()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t3_5",2);
            u.WarriorState.PendingAdaptedSkills.Add("skill.test");
            Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Physical,"skill.test",1),Is.EqualTo(100));
            WarriorCombatSystem.ArmoredTurnStart(u);
            Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Physical,"skill.test",1),Is.EqualTo(70));
            Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Physical,"skill.other",1),Is.EqualTo(100));
            WarriorCombatSystem.ArmoredTurnStart(u);
            Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Physical,"skill.test",1),Is.EqualTo(100));
        }
        [Test] public void ManAtArmsRenewalHealsEachSleepingTurnAndDamageInterruptsIt()
        {
            var u=Unit();var foe=Unit(false,2);var s=Session(u,foe);
            var skill=ArmoredTransient(WarriorCombatSystem.EnergyRenewal);
            try
            {
                var status=new StatusEffectSystem();
                Assert.That(WarriorCombatSystem.ExecuteArmored(u,u,skill,status),Is.True);
                status.ProcessTurnEnd(u);Assert.That(u.Character.CurrentHealth,Is.EqualTo(700));
                new CombatDamageSystem().ApplyDamage(s,foe,u,10,DamageType.Physical);
                Assert.That(StatusEffectSystem.IsSleeping(u),Is.False);
                status.ProcessTurnEnd(u);Assert.That(u.Character.CurrentHealth,Is.EqualTo(690));
            }
            finally{UnityEngine.Object.DestroyImmediate(skill);}
        }
        [Test] public void ManAtArmsDotDurationRemovesOneTickWithoutIncreasingRemainingTicks()
        {
            var u=Unit();var foe=Unit(false,2);ArmoredRank(u,"t1_4");Session(u,foe);
            var status=new StatusEffectSystem();
            Assert.That(status.ApplyStatus(foe,u,StatusEffectIds.Burn,1,3,1,1,0,"test",sharedRoll:0),Is.True);
            var effect=u.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Burn);
            Assert.That(effect.RemainingDuration,Is.EqualTo(2));
            Assert.That(effect.TotalDamage,Is.EqualTo(200));
        }
        [Test] public void ManAtArmsVigorIsGrantedOnceAndDifferentShieldSourcesRemainIndependent()
        {
            var u=Unit();ArmoredRank(u,"t5_1",2);
            WarriorCombatSystem.InitializeArmored(u);WarriorCombatSystem.InitializeArmored(u);
            Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(200));
            Assert.That(u.Character.ShieldPool.Layers.Single().RemainingTurns,Is.EqualTo(5));
        }
        [Test] public void ManAtArmsCopiedAiStateDoesNotShareAdaptationSets()
        {
            var u=Unit();var copy=new WarriorCombatState();u.WarriorState.PendingAdaptedSkills.Add("skill.test");
            copy.CopyArmoredFrom(u.WarriorState);copy.PendingAdaptedSkills.Clear();
            Assert.That(u.WarriorState.PendingAdaptedSkills,Does.Contain("skill.test"));
        }

        [TestCase("t1_2",50)][TestCase("t2_3",40)][TestCase("t4_1",50)][TestCase("t5_3",50)]
        public void ManAtArmsRealSkillExecutionPaysOnceAndUsesApprovedEffect(string slot,int spent)
        {
            var u=Unit();var foe=Unit(false,2);var session=Session(u,foe);ArmoredRank(u,slot);
            var skill=Load<SkillData>("Skills/SD_WarriorManAtArms_"+slot+".asset");
            u.Character.Skills.Add(skill);
            var removable=u.Character.GetActiveSkills().First(s=>s.SkillId!=u.Character.GetBasicAttackSkill().SkillId);
            Assert.That(u.Character.ActiveSkillLoadout.TryRemoveSkill(removable.SkillId),Is.True);
            Assert.That(u.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId,u.Character.GetKnownSkills().Select(s=>s.SkillId)),Is.True);
            new CharacterStatPipelineSystem().Refresh(u.Character);
            CombatTurnSystem.RefreshMaxActionPoints(session);
            var previousPoints=u.CurrentActionPoints;
            var price=CombatActionCostCalculator.CalculateSkillCost(u,skill);
            Assert.That(new SkillExecutionSystem().TryExecute(session,u,skill,slot=="t2_3"?foe:u),Is.True);
            Assert.That(u.LastSkillResourceSpent,Is.EqualTo(spent));
            Assert.That(u.CurrentActionPoints,Is.EqualTo(previousPoints-price));
            if(slot=="t1_2")Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(u.Character.MaximumHealth/5));
            if(slot=="t2_3")Assert.That(foe.Character.CurrentHealth,Is.EqualTo(500));
            if(slot=="t4_1")Assert.That(WarriorCombatSystem.PredictionDodge(foe,u),Is.EqualTo(5));
            if(slot=="t5_3")Assert.That(StatusEffectSystem.IsSleeping(u),Is.True);
        }

        [Test] public void ManAtArmsPeriodicDamageHealsItsOriginalOwner()
        {
            var u=Unit();var foe=Unit(false,2);var s=Session(u,foe);ArmoredRank(u,"t4_5",2);
            var statuses=new StatusEffectSystem();
            Assert.That(statuses.ApplyStatus(u,foe,StatusEffectIds.Burn,1,3,1,1,0,"skill.test.effect.main",sharedRoll:0),Is.True);
            statuses.ProcessTurnStart(s,foe);statuses.ProcessTurnEnd(foe);
            Assert.That(foe.Character.CurrentHealth,Is.EqualTo(300));
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(520));
        }

        [Test] public void ManAtArmsPredictionUsesDistanceAndEndsWhenItsBuffIsRemoved()
        {
            var u=Unit();var foe=Unit(false,4);ArmoredRank(u,"t4_2",2);
            var skill=ArmoredTransient(WarriorCombatSystem.Prediction);var statuses=new StatusEffectSystem();
            try
            {
                WarriorCombatSystem.ExecuteArmored(u,u,skill,statuses);
                Assert.That(WarriorCombatSystem.PredictionDodge(foe,u),Is.EqualTo(15));
                Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Magical,"skill.test",1),Is.EqualTo(91));
                statuses.RemoveStatus(u,WarriorCombatSystem.Prediction);
                Assert.That(WarriorCombatSystem.PredictionDodge(foe,u),Is.Zero);
                Assert.That(WarriorCombatSystem.ArmoredIncoming(foe,u,100,DamageType.Magical,"skill.test",1),Is.EqualTo(100));
            }
            finally{UnityEngine.Object.DestroyImmediate(skill);}
        }
    }
}
