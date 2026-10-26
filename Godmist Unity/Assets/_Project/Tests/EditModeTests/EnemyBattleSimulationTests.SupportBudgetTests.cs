using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string SupportBudgetFolder=>Path.GetFullPath("../LocalHandoffs/simulator-support-budget-20261019");
        static void SupportLoadout(BattleUnit healer,params string[] selected)
        {
            // Isolated policy fixtures expose selected authored skills without
            // importing unrelated tree passives. Expedition builds still invest
            // every point through the real progression system.
            foreach(var id in selected.Where(id=>!healer.Character.GetKnownSkills().Any(s=>s.SkillId==id)))
                healer.Character.Skills.Add(healer.Character.ClassData.SkillTrees.SelectMany(t=>t.Nodes)
                    .Select(n=>n.GrantedSkill).Single(s=>s!=null&&s.SkillId==id));
            var basic=healer.Character.GetBasicAttackSkill().SkillId;
            Assert.That(healer.Character.ActiveSkillLoadout.TryInitialize(healer.Character.GetKnownSkills().Select(s=>s.SkillId),basic,
                new[]{basic}.Concat(selected).ToArray()),Is.True);
        }
        static SkillData SupportSkill(BattleUnit healer,string id)=>healer.Character.GetActiveSkills().Single(s=>s.SkillId==id);

        [Test]
        public void SupportBudgetReservesActualHealCostForExposedWoundedAlly()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.blessing");
            var mage=PolicyHero(2,true,4,4);var enemy=PolicyHero(0,false,5,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue()*.6f);enemy.CurrentActionValue=1;
            healer.Character.ResourcePool.CurrentValue=50;
            var bless=SupportSkill(healer,"skill.paladin.blessing");var heal=SupportSkill(healer,"skill.paladin.heal");
            var rng=UnityEngine.Random.state;var hp=mage.Character.CurrentHealth;var ap=healer.CurrentActionPoints;
            Assert.That(engine.SupportManaReserve(healer),Is.EqualTo(Math.Max(0,PassiveEffectCombatExecutionSystem.ResolveResourceCost(healer,heal)-healer.Character.ResourcePool.Regeneration)));
            Assert.That(engine.PreservesSupportMana(healer,bless),Is.False);
            Assert.That(engine.PreservesSupportMana(healer,heal),Is.True,"A needed heal may spend the reserved mana.");
            Assert.That(engine.PreservesSupportMana(healer,healer.Character.GetBasicAttackSkill()),Is.True);
            Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(50));Assert.That(healer.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
            engine.UsePreviousSupportPolicy=true;Assert.That(engine.PreservesSupportMana(healer,bless),Is.True,"Historical v3 replay remains available.");
        }

        [Test]
        public void SupportBudgetAllowsUtilityWithoutThreatAndWithoutHealingLoadout()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.blessing");
            var ally=PolicyHero(2,true,4,4);var enemy=PolicyHero(0,false,11,9);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,ally,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);healer.Character.ResourcePool.CurrentValue=50;
            var bless=SupportSkill(healer,"skill.paladin.blessing");
            Assert.That(engine.PreservesSupportMana(healer,bless),Is.True);
            ally.Character.CurrentHealth=1;SupportLoadout(healer,"skill.paladin.blessing");
            Assert.That(engine.SupportManaReserve(healer),Is.Zero);Assert.That(engine.PreservesSupportMana(healer,bless),Is.True);
        }

        [Test]
        public void SupportBudgetCriticalHealingCanSpendLastManaAndActuallyRevive()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var mage=PolicyHero(2,true,4,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=0;mage.Character.LifeState=CharacterLifeState.Downed;mage.DownedTurnsRemaining=1;
            var heal=SupportSkill(healer,"skill.paladin.heal");healer.Character.ResourcePool.CurrentValue=PassiveEffectCombatExecutionSystem.ResolveResourceCost(healer,heal);
            Assert.That(engine.PlayerAction(healer),Is.True);Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(mage.Character.CurrentHealth,Is.GreaterThan(1));Assert.That(healer.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void SupportBudgetCountsAllGroupRecipientsOnceAndUsesDiscountedApPlusMana()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.group_heal");
            var mage=PolicyHero(2,true,4,4);var warrior=PolicyHero(0,true,4,5);var enemy=PolicyHero(0,false,11,9);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=1;warrior.Character.CurrentHealth=1;
            var group=SupportSkill(healer,"skill.paladin.group_heal");var single=SupportSkill(healer,"skill.paladin.heal");
            var one=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{mage}}};var both=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{mage,warrior}}};
            var rng=UnityEngine.Random.state;var hp=mage.Character.CurrentHealth;
            var first=engine.GroupSupportPriority(healer,group,one);var total=engine.GroupSupportPriority(healer,group,both);
            Assert.That(first,Is.GreaterThan(0));Assert.That(total,Is.GreaterThan(first));
            Assert.That(engine.GroupSupportPriority(healer,group,new(){{0,new[]{warrior,mage}}}),Is.EqualTo(total));
            var full=engine.SupportOpportunityCost(healer,single,one);
            healer.Character.SkillTreeProgress.Add(new SkillTreeProgress{TreeId=PaladinCombatSystem.Priest,
                NodeInvestments=new(){new SkillTreeNodeInvestment{NodeId="node.paladin.priest.persistent_healer",Ranks=3}}});
            mage.PaladinState.HealingDiscount=true;
            var discounted=engine.SupportOpportunityCost(healer,single,one);
            Assert.That(discounted,Is.LessThan(full));Assert.That(discounted,Is.GreaterThan(20),"Free AP still costs real mana.");
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
        }

        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Same original 24 L48 EQ50 full routes; only player support decisions change.")]
        public void AuditSupportBudgetExpeditions(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,.5f,biome,DungeonLength.Normal,0,firstSeed+4,$"v4-L48-U50-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:SupportBudgetFolder,fullOnly:true,traceKnockdowns:true,previousTeamPolicy:true);

        [Test,Explicit("Targeted seed6 check before full paired matrix.")]
        public void AuditSupportBudgetSeed6()
            =>RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,7,"probe-forest-S6",firstSeed:6,
                outputFolder:Path.Combine(SupportBudgetFolder,"probe"),fullOnly:true,traceKnockdowns:true,previousTeamPolicy:true);

        [Test]
        public void SupportBudgetExecutesGroupHealForSeveralCriticalAllies()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.group_heal");
            var mage=PolicyHero(2,true,4,4);var warrior=PolicyHero(0,true,4,5);var scout=PolicyHero(1,true,3,5);var enemy=PolicyHero(0,false,11,9);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,warrior,scout,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            foreach(var ally in new[]{mage,warrior,scout})ally.Character.CurrentHealth=1;
            string chosen=null;session.SkillResolved+=(actor,skill,id)=>chosen=skill.SkillId;
            Assert.That(engine.PlayerAction(healer),Is.True);
            Assert.That(chosen,Is.EqualTo("skill.paladin.group_heal"));
            foreach(var ally in new[]{mage,warrior,scout})Assert.That(ally.Character.CurrentHealth,Is.GreaterThan(1));
        }

        [Test,Explicit("Historical v3 control must match archived seed6 exactly, except generated item GUIDs and elapsed time.")]
        public void AuditSupportBudgetHistoricalControl()
            =>RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,7,"control-forest-S6",firstSeed:6,
                outputFolder:Path.Combine(SupportBudgetFolder,"control"),fullOnly:true,traceKnockdowns:true,previousSupportPolicy:true);
    }
}
