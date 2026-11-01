using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class WarriorContentTests
    {
        [TestCase(0,0)][TestCase(50,25)][TestCase(100,50)]
        public void FocusUsesCurrentRageAndRestoresNestedSnapshots(int rage,float bonus)
        {
            var u=Unit();Rank(u,false,"t5_1",1);u.Character.ResourcePool.MaxValue=100;u.Character.ResourcePool.CurrentValue=rage;
            var skill=Skill("Cut");var before=PassiveEffectCombatExecutionSystem.ResolveParameter(u,skill,"accuracy",skill.SkillAccuracy);
            Assert.That(before,Is.EqualTo(skill.SkillAccuracy+bonus));
            using(new WarriorCombatSystem.CastScope(u,skill))
            {
                u.Character.ResourcePool.CurrentValue=0;
                Assert.That(WarriorCombatSystem.AccuracyBonus(u,skill),Is.EqualTo(bonus));
                using(new WarriorCombatSystem.CastScope(u,skill))Assert.That(WarriorCombatSystem.AccuracyBonus(u,skill),Is.Zero);
                Assert.That(WarriorCombatSystem.AccuracyBonus(u,skill),Is.EqualTo(bonus));
            }
            Assert.That(WarriorCombatSystem.AccuracyBonus(u,skill),Is.Zero);
        }

        [Test] public void DischargeExecutionRetainsPrePaymentAccuracyUntilActionFinishes()
        {
            var u=Unit();Rank(u,false,"t5_1",1);Rank(u,false,"bleeding_destruction",1);
            var foe=Unit(false,2);var session=Session(u,foe);float observed=-1;
            var skill=Load<SkillData>("Skills/SD_WarriorDischarge.asset");
            u.Character.Skills.Add(skill);u.Character.ActiveSkillLoadout.TryRemoveSkill(u.Character.Skills[0].SkillId);
            u.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId,u.Character.GetKnownSkills().Select(s=>s.SkillId));
            session.SkillResolved+=(caster,used,id)=>observed=PassiveEffectCombatExecutionSystem.ResolveParameter(caster,used,"accuracy",used.SkillAccuracy);
            Assert.That(new SkillExecutionSystem().TryExecute(session,u,skill,foe),Is.True);
            Assert.That(observed,Is.EqualTo(skill.SkillAccuracy+25));
            Assert.That(u.WarriorState.AccuracyRageSnapshot,Is.Null);
        }

        [TestCase(1,99,.3f)][TestCase(1,100,0)][TestCase(2,199,.5f)][TestCase(2,200,0)]
        public void BattleFrenzyUsesStrictRankSpecificHealthThreshold(int rank,int health,float expected)
        {
            var u=Unit();Rank(u,false,"t5_2",rank);u.Character.CurrentHealth=health;
            Assert.That(WarriorCombatSystem.LowHealthDamageBonus(u),Is.EqualTo(expected));
        }

        [TestCase(1,2.15f)][TestCase(2,2.25f)]
        public void CriticalFuryTracksMissingHealthWithoutPersistentStatMutation(int rank,float expected)
        {
            var u=Unit();Rank(u,false,"t5_3",rank);
            Assert.That(WarriorCombatSystem.CriticalDamage(u),Is.EqualTo(expected).Within(.0001));
            u.Character.CurrentHealth=1000;
            Assert.That(WarriorCombatSystem.CriticalDamage(u),Is.EqualTo(2));
        }

        [Test] public void PreviewProjectsPaymentHealingBeforeLowHealthAndCriticalBonuses()
        {
            var u=Unit();Rank(u,false,"t5_2",2);Rank(u,false,"t5_3",2);u.Character.CurrentHealth=199;
            var skill=Load<SkillData>("Skills/SD_WarriorDischarge.asset");
            var projected=WarriorCombatSystem.CriticalDamage(u,skill);
            Assert.That(WarriorCombatSystem.LowHealthDamageBonus(u),Is.EqualTo(.5f));
            Assert.That(WarriorCombatSystem.LowHealthDamageBonus(u,skill),Is.Zero);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(199),"Preview is read-only.");
            WarriorCombatSystem.Paid(u,skill,50,50,new CombatRecoverySystem());
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(203));
            Assert.That(WarriorCombatSystem.CriticalDamage(u),Is.EqualTo(projected).Within(.0001));
        }

        [TestCase(1,"0,003x","0,005x")][TestCase(2,"0,005x","0,003x")]
        public void CriticalFuryTooltipSelectsOnlyItsOwnRank(int rank,string included,string excluded)
        {
            var node=Unit().Character.ClassData.SkillTrees.Single(t=>t.Id==WarriorCombatSystem.Brawler)
                .Nodes.Single(n=>n.Id=="node.warrior.brawler.t5_3");
            var text=Godmist.Views.UI.Workspace.SkillTreeNodeView.BuildRankDescription(node,rank);
            Assert.That(text,Does.Contain(included));Assert.That(text,Does.Not.Contain(excluded));
        }

        [Test] public void DuelRequiresExactlyOneReachableAdjacentEnemy()
        {
            var u=Unit();Rank(u,true,"t5_3",2);var foe=Unit(false,2);var other=Unit(false,1);
            other.Position=new GridPosition(1,1);var session=Session(u,foe);
            Assert.That(WarriorCombatSystem.DuelCriticalChance(session,u,foe),Is.EqualTo(.2f));
            session.Units.Add(other);Assert.That(WarriorCombatSystem.DuelCriticalChance(session,u,foe),Is.Zero);
            other.Character.LifeState=CharacterLifeState.Dead;
            Assert.That(WarriorCombatSystem.DuelCriticalChance(session,u,foe),Is.EqualTo(.2f));
        }

        [TestCase(DamageType.Physical)][TestCase(DamageType.Magical)]
        public void DuelPreviewUsesTheSameBonusForPhysicalAndMagicalContacts(DamageType type)
        {
            var u=Unit();Rank(u,true,"t5_3",2);var foe=Unit(false,2);var s=Session(u,foe);
            Assert.That(CombatDamageSystem.PreviewCriticalChance(s,u,foe,Skill("Cut"),type),Is.EqualTo(.2f).Within(.0001));
        }

        [Test] public void FencingCountsActionsCapsAtFiveAndClearsOnMultipleTargets()
        {
            var u=Unit();Rank(u,true,"t5_4",2);var foe=Unit(false,2);var other=Unit(false,3);Session(u,foe,other);
            var skill=Skill("Cut");var context=new PassiveEffectCombatContext(source:u,target:foe,skill:skill,skillId:skill.SkillId);
            Assert.That(WarriorCombatSystem.FencingMultiplier(context,true),Is.EqualTo(1.04f).Within(.0001));
            Assert.That(u.WarriorState.FencingStacks,Is.Zero);
            for(var i=1;i<=7;i++)
            {
                WarriorCombatSystem.BeginSkill(u,skill,new[]{foe});
                Assert.That(u.WarriorState.FencingStacks,Is.EqualTo(System.Math.Min(i,5)));
                Assert.That(WarriorCombatSystem.FencingMultiplier(context,false),Is.EqualTo(1+.04f*System.Math.Min(i,5)).Within(.0001));
            }
            WarriorCombatSystem.BeginSkill(u,skill,new[]{other});Assert.That(u.WarriorState.FencingStacks,Is.EqualTo(1));
            WarriorCombatSystem.BeginSkill(u,skill,new[]{foe,other});Assert.That(u.WarriorState.FencingStacks,Is.Zero);
        }

        [TestCase(false,true,50,10,50)][TestCase(false,true,51,10,10)]
        [TestCase(true,true,1000,10,20)][TestCase(false,false,50,10,10)]
        public void ExecutionHasPreHitThresholdCriticalGateAndBossException(bool boss,bool critical,int health,int damage,int expected)
        {
            var u=Unit();Rank(u,true,"t5_2",1);var foe=Unit(false,2);foe.IsBoss=boss;foe.Character.CurrentHealth=health;
            var s=Session(u,foe);var context=new PassiveEffectCombatContext(source:u,target:foe,damageType:DamageType.Physical);
            Assert.That(WarriorCombatSystem.ResolveExecution(s,context,critical,damage,()=>0),Is.EqualTo(expected));
            Assert.That(WarriorCombatSystem.ResolveExecution(s,context,critical,damage,()=>1),Is.EqualTo(damage));
        }

        [TestCase(1,.15f)][TestCase(2,.3f)] public void AegisBuffsTheRecipientWithoutStrategy(int rank,float bonus)
        {
            var u=Unit();Rank(u,true,"t5_1",rank);var ally=Unit(true,2);var s=Session(u,ally);
            Assert.That(new SkillExecutionSystem().TryExecute(s,u,Skill("Regroup"),ally),Is.True);
            Assert.That(new CombatVariableSystem().GetMultiplier(ally.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1+bonus).Within(.0001));
            Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1));
        }

        [TestCase(1,25)][TestCase(2,50)] public void DeathWishBanksOnlyPreventedDamageAndPaysOnce(int rank,int heal)
        {
            var u=Unit();Rank(u,false,"t5_4",rank);Rank(u,false,"t5_5",1);
            u.Character.CurrentHealth=101;u.Character.ResourcePool.CurrentValue=0;
            u.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish);
            var foe=Unit(false,2);var s=Session(u,foe);var damage=new CombatDamageSystem();
            damage.ApplyDamage(s,foe,u,200,DamageType.Physical);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(1));
            Assert.That(u.WarriorState.DeathWishPreventedDamage,Is.EqualTo(100));
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            u.ActiveToggleSkillIds.Remove(WarriorCombatSystem.DeathWish);
            WarriorCombatSystem.EndDeathWish(u,new CombatRecoverySystem());
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(1+heal));
            WarriorCombatSystem.EndDeathWish(u,new CombatRecoverySystem());Assert.That(u.Character.CurrentHealth,Is.EqualTo(1+heal));
        }

        [Test] public void FullShieldDoesNotBankDeathWishDamageOrGrantRage()
        {
            var u=Unit();Rank(u,false,"t5_4",2);Rank(u,false,"t5_5",1);
            u.Character.CurrentHealth=1;u.Character.ResourcePool.CurrentValue=0;u.Character.ShieldPool.AddLayer(100);
            u.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish);var foe=Unit(false,2);var s=Session(u,foe);
            new CombatDamageSystem().ApplyDamage(s,foe,u,100,DamageType.Physical);
            Assert.That(u.WarriorState.DeathWishPreventedDamage,Is.Zero);Assert.That(u.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [TestCase(false)][TestCase(true)] public void ActualToggleDeactivationPaysBankOnManualAndFailedUpkeep(bool upkeep)
        {
            var u=Unit();Rank(u,false,"death_wish",1);Rank(u,false,"t5_4",2);var skill=Skill("DeathWish");
            u.Character.Skills.Add(skill);u.Character.ActiveSkillLoadout.TryRemoveSkill(u.Character.Skills[0].SkillId);
            u.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId,u.Character.GetKnownSkills().Select(x=>x.SkillId));
            u.Character.CurrentHealth=1;u.Character.ResourcePool.CurrentValue=0;
            u.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish);u.WarriorState.DeathWishPreventedDamage=100;
            var s=Session(u);var execution=new SkillExecutionSystem();
            if(upkeep)execution.ProcessToggleUpkeep(s,u);else Assert.That(execution.TryExecute(s,u,skill,u),Is.True);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(51));
            Assert.That(u.WarriorState.DeathWishPreventedDamage,Is.Zero);
            Assert.That(WarriorCombatSystem.DeathProtected(u),Is.False);
        }

        [TestCase(0,100)][TestCase(20,80)][TestCase(100,0)][TestCase(200,0)]
        public void DeathWishAccountsForShieldBeforeCalculatingPreventedDamage(int shield,int expected)
        {
            var u=Unit();Rank(u,false,"t5_4",2);u.Character.CurrentHealth=101;
            u.Character.ShieldPool.AddLayer(shield);u.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish);
            var foe=Unit(false,2);var s=Session(u,foe);
            new CombatDamageSystem().ApplyDamage(s,foe,u,200,DamageType.Physical);
            Assert.That(u.WarriorState.DeathWishPreventedDamage,Is.EqualTo(expected));
        }

        [Test] public void RemovingTalentsRemovesConditionalBonusesAndDiscardsDeathWishBank()
        {
            var u=Unit();Rank(u,false,"t5_1",1);Rank(u,false,"t5_2",2);Rank(u,false,"t5_3",2);Rank(u,false,"t5_4",2);
            Rank(u,true,"t5_4",2);u.Character.CurrentHealth=1;u.WarriorState.DeathWishPreventedDamage=100;
            u.Character.SkillTreeProgress.Clear();WarriorCombatSystem.EndDeathWish(u,new CombatRecoverySystem());
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(1));
            Assert.That(WarriorCombatSystem.CriticalDamage(u),Is.EqualTo(2));
            Assert.That(WarriorCombatSystem.LowHealthDamageBonus(u),Is.Zero);
            Assert.That(WarriorCombatSystem.AccuracyBonus(u,Skill("Cut")),Is.Zero);
        }

        [Test] public void OrderRangeDoesNotExpandAreaOrUnrelatedSkills()
        {
            var u=Unit();Rank(u,true,"t5_5",1);
            foreach(var name in new[]{"Regroup","Protection","Destruction"})
            {
                var skill=Skill(name);var mode=skill.TargetModes[0];
                var resolved=PassiveEffectCombatExecutionSystem.ResolveMode(u,skill,mode);
                Assert.That(resolved.AbsoluteRange.MaxRange,Is.EqualTo(mode.AbsoluteRange.MaxRange+2));
                Assert.That(resolved.RelativeRange.MaxRange,Is.EqualTo(mode.RelativeRange.MaxRange));
            }
            Assert.That(WarriorCombatSystem.OrderRangeBonus(u,Skill("Cut")),Is.Zero);
        }
    }
}
