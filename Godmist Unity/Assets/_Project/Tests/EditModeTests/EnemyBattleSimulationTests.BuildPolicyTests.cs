using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string BuildPolicyFolder=>Path.GetFullPath("../LocalHandoffs/simulator-build-policy-20261019");
        static BattleUnit BuildHero(int index,int level,int profile,int x=2,int y=4)
        {
            var gear=PointInput.rows.Single(r=>r.level==level&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
            var hero=PointHero(gear,profile,PointBudget(level),0,PointEnemies(level));
            hero.CharacterId=$"build-{index}-{profile}-{x}-{y}";SetBuildLoadout(hero,profile);
            return new BattleUnit{Character=hero,IsPlayerSide=true,Position=new GridPosition(x,y)};
        }
        static IEnumerable<TestCaseData> BuildRoleCases()
        {foreach(var level in new[]{10,48})foreach(var profile in new[]{0,1,2})foreach(var hero in Enumerable.Range(0,4))yield return new TestCaseData(level,profile,hero);}
        [TestCaseSource(nameof(BuildRoleCases))]
        public void BuildRoleLoadoutsKeepLegalRecoveryOffenseAndSpecialty(int level,int profile,int index)
        {
            var hero=BuildHero(index,level,profile).Character;var ids=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray();
            Assert.That(ids,Does.Contain(hero.GetBasicAttackSkill().SkillId));Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));
            Assert.That(ids.Length,Is.LessThanOrEqualTo(hero.ActiveSkillLoadout.Capacity));
            Assert.That(hero.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks)),Is.EqualTo(PointBudget(level)));
            void Keeps(string id){if(hero.GetKnownSkills().Any(s=>s.SkillId==id))Assert.That(ids,Does.Contain(id));}
            if(index==0)Keeps(profile==0?"skill.warrior.destruction":"skill.warrior.shout");
            if(index==1)Keeps(profile==1?"skill.scout.poisoned_dagger":"skill.scout.ricochet");
            if(index==2){Keeps("skill.mage.shield");Keeps("skill.mage.fireball");}
            if(index==3){Keeps("skill.paladin.heal");Keeps(profile==0?"skill.paladin.holy_regeneration":"skill.paladin.judgement_field");}
        }
        [TestCase(4,2)][TestCase(5,0)][TestCase(6,1)][TestCase(7,3)]
        public void BuildSingleSwitchChangesOnlyItsOwner(int profile,int owner)
        {foreach(var index in Enumerable.Range(0,4))Assert.That(RealisticProfile(index,profile),Is.EqualTo(index==owner?1-RealisticProfile(index,0):RealisticProfile(index,0)));}

        sealed partial class Engine
        {
            internal (string skill,float score,GridPosition? placement,GridPosition? aim)[] BuildTestPlans(BattleUnit actor)
                =>BuildPlayerPlans(actor).Select(p=>(p.skill.SkillId,p.score,p.objectPosition,p.positions.Values.FirstOrDefault())).ToArray();
            internal float BuildTestCost(BattleUnit actor,Godmist.Data.Skills.SkillData skill)=>BuildPlanCost(actor,skill);
            internal void BuildTestFieldTick()=>objects.ExecuteTurnEffects(Session,Session.SummonedObjects.Single(PaladinCombatSystem.IsField));
            internal string BuildTestFingerprint()=>DecisionStateFingerprint();
            internal string[] BuildTestPlanKeys(BattleUnit actor)
            {
                string Key(PlayerPlan p)=>p.skill.SkillId+"|"+p.score.ToString("R")+"|"+p.objectPosition+"|"
                    +string.Join(",",p.targets.Values.SelectMany(t=>t).Select(t=>t.Character.CharacterId).Distinct().OrderBy(s=>s,StringComparer.Ordinal));
                return BuildPlayerPlans(actor).GroupBy(Key).Select(g=>g.Key+"|"+string.Join(",",g.First().positions.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value))).ToArray();
            }
        }
        static void BuildEnableUtility(Engine engine)
        {engine.AdditionalAuditScore=(a,s,m,t)=>engine.BuildUtility(a,s,m,t,new HashSet<string>(),false);}

        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void BuildResourceCostRespectsFuryMomentumAndMana(int index)
        {
            var actor=BuildHero(index,48,1);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            var skill=actor.Character.GetKnownSkills().First(s=>PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s)>0);
            var ordinary=Math.Max(20,CombatActionCostCalculator.CalculateSkillCost(actor,skill));
            if(actor.Character.ResourcePool.ResourceType==ResourceType.Mana)Assert.That(engine.BuildTestCost(actor,skill),Is.GreaterThan(ordinary));
            else Assert.That(engine.BuildTestCost(actor,skill),Is.EqualTo(ordinary));
            engine.UsePreviousBuildPolicy=true;Assert.That(engine.BuildTestCost(actor,skill),Is.EqualTo(ordinary));
        }
        [Test]
        public void BuildShieldDoesNotReplaceOffenseForLowHealthWithoutIncomingDamage()
        {
            var mage=BuildHero(2,48,1);var ally=BuildHero(0,48,1,2,3);var enemy=PolicyHero(0,false,6,4);
            AssertBuildLoadout(mage.Character,new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.shield","skill.mage.fireball"});
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};var result=new Result();
            using var engine=new Engine(session,result);PolicyActivate(session,mage);ally.Character.CurrentHealth=1;enemy.CurrentActionValue=1;ally.CurrentActionValue=100;
            Assert.That(engine.MatchupIncoming(ally,ally.Position),Is.Zero);
            Assert.That(engine.PlayerAction(mage),Is.True);
            Assert.That(result.skills.Any(s=>s.id=="hero:skill.mage.shield"),Is.False);
            Assert.That(enemy.Character.CurrentHealth,Is.LessThan(enemy.Character.Stats.MaxHealth.GetValue()));
        }
        [Test]
        public void BuildShieldStillUsesRealManaToPreventAnImminentDowning()
        {
            var mage=BuildHero(2,48,1);var ally=BuildHero(0,48,1,3,4);var enemy=PolicyHero(0,false,4,4);
            AssertBuildLoadout(mage.Character,new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.shield","skill.mage.fireball"});
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};var result=new Result();
            using var engine=new Engine(session,result);PolicyActivate(session,mage);ally.Character.CurrentHealth=1;enemy.CurrentActionValue=1;ally.CurrentActionValue=100;
            enemy.Character.Stats.MinAttack=new Stat(100);enemy.Character.Stats.MaxAttack=new Stat(100);
            enemy.Character.Stats.MaxHealth=new Stat(100000);enemy.Character.CurrentHealth=100000;
            Assert.That(engine.MatchupIncoming(ally,ally.Position),Is.GreaterThan(1));var mana=mage.Character.ResourcePool.CurrentValue;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(result.skills.Any(s=>s.id=="hero:skill.mage.shield"),Is.True);
            Assert.That(ally.Character.ShieldPool.TotalShield,Is.GreaterThan(0));Assert.That(ally.Character.CurrentHealth,Is.EqualTo(1));
            Assert.That(mage.Character.ResourcePool.CurrentValue,Is.LessThan(mana));
        }
        [Test]
        public void BuildCrusaderPlansLegalFieldWithoutMutatingStateAndUsesRealTick()
        {
            var paladin=BuildHero(3,48,1,3,4);var foes=new[]{PolicyHero(0,false,6,3),PolicyHero(0,false,6,4),PolicyHero(0,false,7,5)};
            foreach(var foe in foes){foe.Character.Stats.MaxHealth=new Stat(100000);foe.Character.CurrentHealth=100000;}
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){paladin}};session.Units.AddRange(foes);
            using var engine=new Engine(session,new Result());PolicyActivate(session,paladin);BuildEnableUtility(engine);
            // Isolate field execution; a move plus Verdict is a separate legal plan.
            paladin.CurrentActionPoints=CombatActionCostCalculator.CalculateSkillCost(paladin,paladin.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.paladin.judgement_field"));
            var rng=UnityEngine.Random.state;var fingerprint=engine.BuildTestFingerprint();
            var plans=engine.BuildTestPlans(paladin);Assert.That(plans.Any(p=>p.skill=="skill.paladin.judgement_field"&&p.placement.HasValue&&p.score>0),Is.True);
            Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(fingerprint));Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
            engine.UsePreviousBuildPolicy=true;Assert.That(engine.BuildTestPlans(paladin).Any(p=>p.skill=="skill.paladin.judgement_field"),Is.False);engine.UsePreviousBuildPolicy=false;
            var ap=paladin.CurrentActionPoints;var mana=paladin.Character.ResourcePool.CurrentValue;
            Assert.That(engine.PlayerAction(paladin),Is.True);Assert.That(session.SummonedObjects.Count(PaladinCombatSystem.IsField),Is.EqualTo(1));
            Assert.That(paladin.CurrentActionPoints,Is.LessThan(ap));Assert.That(paladin.Character.ResourcePool.CurrentValue,Is.LessThan(mana));
            Assert.That(engine.BuildTestPlans(paladin).Any(p=>p.skill=="skill.paladin.judgement_field"),Is.False,"Do not erase an active useful field to refresh it.");
            var health=foes.Sum(f=>f.Character.CurrentHealth);engine.BuildTestFieldTick();Assert.That(foes.Sum(f=>f.Character.CurrentHealth),Is.LessThan(health));
        }
        [Test]
        public void BuildCommanderValuesAdvancingAnActualAttackerButNotAnActiveTurn()
        {
            var warrior=BuildHero(0,48,0);var mage=BuildHero(2,48,0,3,4);var enemy=PolicyHero(0,false,6,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){warrior,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,warrior);
            mage.CurrentActionValue=100;var skill=warrior.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.warrior.regroup");
            Assert.That(engine.BuildUtility(warrior,skill,0,new[]{mage},new(),false),Is.GreaterThan(0));
            mage.CurrentActionValue=0;Assert.That(engine.BuildUtility(warrior,skill,0,new[]{mage},new(),false),Is.Zero);
        }
        [Test]
        public void BuildWarlockRejectsRitualBeforeLethalPressureAndDoesNotRefreshActiveRecovery()
        {
            var mage=BuildHero(2,48,0);var ally=BuildHero(0,48,1,3,4);var enemy=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);enemy.CurrentActionValue=1;ally.CurrentActionValue=100;
            var skill=mage.Character.GetKnownSkills().Single(s=>s.SkillId=="skill.mage.ritual");
            Assert.That(engine.MatchupIncoming(ally,ally.Position),Is.GreaterThan(0));
            ally.Character.CurrentHealth=1;Assert.That(engine.BuildUtility(mage,skill,0,new[]{mage,ally},new(),false),Is.Zero);
            enemy.Character.LifeState=CharacterLifeState.Dead;ally.Character.CurrentHealth=(int)(ally.Character.Stats.MaxHealth.GetValue()*.5f);
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{ally},new(),false),Is.GreaterThan(0));
            var effect=skill.Effects.Single();Assert.That(new StatusEffectSystem().ApplyPeriodicRecovery(mage,ally,skill,effect.WithPercentageValue(effect.PercentageValue/effect.DurationTurns),0,false),Is.True);
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{ally},new(),false),Is.Zero);
        }
        [Test]
        public void BuildWarlockValuesDonorCostAndDoesNotRefreshAnExistingTransfer()
        {
            var mage=BuildHero(2,48,0);var donor=BuildHero(3,48,0,0,9);var enemy=PolicyHero(0,false,6,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,donor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            var skill=mage.Character.GetKnownSkills().Single(s=>s.SkillId=="skill.mage.sacrifice");
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{donor},new(),false),Is.GreaterThan(0));
            WarriorCombatSystem.Variable(mage,mage,GameplayVariableType.DamageDealtPercent,.2f,3,"mage.sacrifice.gain.test","test");
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{donor},new(),false),Is.Zero);
        }
        [Test]
        public void BuildPaladinPreservesOneCriticalHealBeforeOptionalFieldSpending()
        {
            var paladin=BuildHero(3,48,1);var mage=BuildHero(2,48,0,3,4);var enemy=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){paladin,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,paladin);mage.Character.CurrentHealth=1;enemy.CurrentActionValue=1;
            var heal=paladin.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.paladin.heal");
            var reserve=PassiveEffectCombatExecutionSystem.ResolveResourceCost(paladin,heal);
            paladin.Character.ResourcePool.Drain(paladin.Character.ResourcePool.CurrentValue-reserve);
            var field=paladin.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.paladin.judgement_field");
            Assert.That(engine.SupportManaReserve(paladin),Is.GreaterThan(0));Assert.That(engine.PreservesSupportMana(paladin,field),Is.False);
            Assert.That(engine.PreservesSupportMana(paladin,paladin.Character.GetBasicAttackSkill()),Is.True);
        }
        [Test]
        public void BuildPaladinSpendsHealingReserveOnlyForAnActualLethalDamageOpportunity()
        {
            var paladin=BuildHero(3,48,1);var mage=BuildHero(2,48,0,3,4);var enemy=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){paladin,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,paladin);
            mage.Character.CurrentHealth=1;mage.CurrentActionValue=100;enemy.CurrentActionValue=1;
            enemy.Character.Stats.MaxHealth=new Stat(100000);enemy.Character.CurrentHealth=100000;
            var skill=paladin.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.paladin.blinding_light");
            var reserve=engine.SupportManaReserve(paladin);var cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(paladin,skill);
            Assert.That(reserve,Is.GreaterThan(0));paladin.Character.ResourcePool.Drain(paladin.Character.ResourcePool.CurrentValue-(reserve+cost-1));
            Assert.That(paladin.Character.ResourcePool.CurrentValue,Is.GreaterThanOrEqualTo(cost));
            Assert.That(engine.PreservesSupportMana(paladin,skill),Is.False);
            enemy.Character.CurrentHealth=1;Assert.That(engine.PreservesSupportMana(paladin,skill),Is.True);
        }
        [Test]
        public void BuildWarlockDropsOfferingBeforeImminentLethalPressure()
        {
            var mage=BuildHero(2,48,0);var enemy=PolicyHero(0,false,3,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            var offering=mage.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.mage.offering");
            Assert.That(engine.BuildUtility(mage,offering,0,new[]{mage},new(),false),Is.GreaterThan(0));
            Assert.That(new SkillExecutionSystem().TryExecute(session,mage,offering,mage),Is.True);
            Assert.That(mage.ActiveToggleSkillIds,Does.Contain(offering.SkillId));
            mage.Character.CurrentHealth=1;enemy.CurrentActionValue=1;var hp=mage.Character.CurrentHealth;
            Assert.That(engine.MatchupIncoming(mage,mage.Position),Is.GreaterThan(0));
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.ActiveToggleSkillIds,Does.Not.Contain(offering.SkillId));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(hp));
        }
        [Test]
        public void BuildWarlockPlansDirectionalEnergySplitAndExecutesAllThreeRays()
        {
            var mage=BuildHero(2,48,0);var ids=new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.energy_split"};
            AssertBuildLoadout(mage.Character,ids);
            var foes=new[]{PolicyHero(0,false,6,4),PolicyHero(0,false,5,7),PolicyHero(0,false,5,1)};
            foreach(var enemy in foes){enemy.Character.Stats.MaxHealth=new Stat(50000);enemy.Character.CurrentHealth=50000;enemy.Character.Stats.MagicalDefense=new Stat(0);}
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage}};session.Units.AddRange(foes);
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);BuildEnableUtility(engine);
            var rng=UnityEngine.Random.state;var fingerprint=engine.BuildTestFingerprint();
            Assert.That(engine.BuildTestPlans(mage).Any(p=>p.skill=="skill.mage.energy_split"&&p.aim.HasValue),Is.True);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(fingerprint));
            engine.UsePreviousBuildPolicy=true;Assert.That(engine.BuildTestPlans(mage).Any(p=>p.skill=="skill.mage.energy_split"),Is.False);engine.UsePreviousBuildPolicy=false;
            var mana=mage.Character.ResourcePool.CurrentValue;var ap=mage.CurrentActionPoints;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(engine.Result.skills.Any(s=>s.id=="hero:skill.mage.energy_split"),Is.True);
            Assert.That(foes.All(e=>e.Character.CurrentHealth<50000),Is.True);
            Assert.That(mage.Character.ResourcePool.CurrentValue,Is.LessThan(mana));Assert.That(mage.CurrentActionPoints,Is.LessThan(ap));
        }
        [Test]
        public void BuildWarlockHealthForManaRequiresActualNeedAndSafePayment()
        {
            var mage=BuildHero(2,48,1);var enemy=PolicyHero(0,false,8,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            var skill=mage.Character.GetKnownSkills().Single(s=>s.SkillId=="skill.mage.focus");
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{mage},new(),false),Is.Zero);
            mage.Character.ResourcePool.Drain(mage.Character.ResourcePool.CurrentValue);mage.Character.CurrentHealth=1;
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{mage},new(),false),Is.Zero);
            mage.Character.CurrentHealth=(int)mage.Character.Stats.MaxHealth.GetValue();
            Assert.That(engine.BuildUtility(mage,skill,0,new[]{mage},new(),false),Is.GreaterThan(0));
            var hp=mage.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem().TryExecute(session,mage,skill,mage),Is.True);
            Assert.That(mage.Character.CurrentHealth,Is.LessThan(hp));Assert.That(mage.Character.ResourcePool.CurrentValue,Is.GreaterThan(0));
        }
        [TestCase(2,4)][TestCase(5,5)][TestCase(0,0)][TestCase(11,9)]
        public void BuildWarlockDirectionDedupKeepsScoresTargetsAndFirstAim(int x,int y)
        {
            var mage=BuildHero(2,48,0,x,y);AssertBuildLoadout(mage.Character,new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.energy_split"});
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,PolicyHero(0,false,6,4),PolicyHero(0,false,5,7),PolicyHero(0,false,5,1)}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);BuildEnableUtility(engine);
            var rng=UnityEngine.Random.state;var state=engine.BuildTestFingerprint();
            var optimized=engine.BuildTestPlanKeys(mage);engine.AuditExhaustiveDirections=true;var exhaustive=engine.BuildTestPlanKeys(mage);
            Assert.That(optimized,Is.EqualTo(exhaustive));Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(state));Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
        }
    }
}
