using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        sealed partial class Engine
        {
            internal bool JointTestFinish(BattleUnit actor)=>TryJointLethalAttack(actor);
        }
        [TestCaseSource(nameof(BuildRoleCases))]
        public void JointLoadoutsPreserveInvestmentAndUseOnlyKnownSkillsWithinCapacity(int level,int profile,int index)
        {
            var hero=BuildHero(index,level,profile).Character;
            string Ranks()=>string.Join("|",hero.SkillTreeProgress.SelectMany(t=>t.NodeInvestments.Select(n=>$"{t.TreeId}/{n.NodeId}:{n.Ranks}")));
            var before=Ranks();var known=hero.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
            foreach(var variant in new[]{0,1,2,3})
            {
                SetJointLoadout(hero,profile,variant);var ids=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray();
                Assert.That(Ranks(),Is.EqualTo(before));Assert.That(ids.Length,Is.LessThanOrEqualTo(hero.ActiveSkillLoadout.Capacity));
                Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));Assert.That(ids.All(known.Contains),Is.True);
                Assert.That(ids,Does.Contain(hero.GetBasicAttackSkill().SkillId));
                if(index==2){Assert.That(ids,Does.Contain("skill.mage.fireball"));Assert.That(ids,Does.Contain("skill.mage.shield"));}
                if(index==3)Assert.That(ids,Does.Contain("skill.paladin.heal"));
            }
        }
        [Test]
        public void JointFinishRemovesImminentAttackerWithRealCostsInsteadOfShielding()
        {
            var mage=BuildHero(2,48,0);var ally=BuildHero(0,48,0,3,4);var enemy=PolicyHero(0,false,4,4);
            AssertBuildLoadout(mage.Character,new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.shield","skill.mage.fireball"});
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};var result=new Result();
            using var engine=new Engine(session,result);PolicyActivate(session,mage);enemy.Character.CurrentHealth=1;ally.Character.CurrentHealth=1;
            enemy.Character.Stats.MinAttack=new Stat(100);enemy.Character.Stats.MaxAttack=new Stat(100);enemy.Character.Stats.Dodge=new Stat(0);
            enemy.CurrentActionValue=1;ally.CurrentActionValue=100;
            var fingerprint=engine.BuildTestFingerprint();var random=UnityEngine.Random.state;
            engine.UsePreviousPlanningPolicy=true;Assert.That(engine.JointTestFinish(mage),Is.False);
            Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(fingerprint));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            engine.UsePreviousPlanningPolicy=false;var points=mage.CurrentActionPoints;
            try
            {
                UnityEngine.Random.InitState(13);
                Assert.That(engine.JointTestFinish(mage),Is.True);Assert.That(result.heroActions,Is.EqualTo(1));
                Assert.That(mage.CurrentActionPoints,Is.LessThan(points));Assert.That(ally.Character.ShieldPool.TotalShield,Is.Zero);
                Assert.That(enemy.Character.CurrentHealth,Is.Zero);
            }
            finally{UnityEngine.Random.state=random;}
        }
        [Test]
        public void JointFinishDoesNotIgnoreRemainingLethalPressureOrConsumePreviewRng()
        {
            var mage=BuildHero(2,48,0);var ally=BuildHero(0,48,0,3,4);var weak=PolicyHero(0,false,5,4);var strong=PolicyHero(0,false,3,3);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,weak,strong}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);ally.Character.CurrentHealth=1;weak.Character.CurrentHealth=1;
            strong.Character.Stats.MaxHealth=new Stat(1000000);strong.Character.CurrentHealth=1000000;
            strong.Character.Stats.MinAttack=new Stat(1000);strong.Character.Stats.MaxAttack=new Stat(1000);
            weak.CurrentActionValue=1;strong.CurrentActionValue=1;ally.CurrentActionValue=100;
            var fingerprint=engine.BuildTestFingerprint();var random=UnityEngine.Random.state;
            Assert.That(engine.JointTestFinish(mage),Is.False);
            Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(fingerprint));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test]
        public void JointFinishCountsEnemyShieldInsteadOfAssumingOneHpMeansLethal()
        {
            var mage=BuildHero(2,48,0);var ally=BuildHero(0,48,0,3,4);var enemy=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);ally.Character.CurrentHealth=1;enemy.Character.CurrentHealth=1;
            enemy.Character.ShieldPool.AddLayer(1000000,"joint-test");enemy.CurrentActionValue=1;ally.CurrentActionValue=100;
            var fingerprint=engine.BuildTestFingerprint();var random=UnityEngine.Random.state;
            Assert.That(engine.JointTestFinish(mage),Is.False);
            Assert.That(engine.BuildTestFingerprint(),Is.EqualTo(fingerprint));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test]
        public void JointScoutDoesNotUndoDefensiveWithdrawalWithAnotherNoAttackApproach()
        {
            var scout=BuildHero(1,48,1);
            var definition=PointCatalog.CharacterDefinitions.Single(d=>d.Id=="enemy.forest.elf_archer");
            var enemy=new BattleUnit{Character=new CharacterInstanceFactory().Create(definition,definition.ClassData,48),Position=new GridPosition(10,4)};
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){scout,enemy}};var result=new Result{turns=1};
            using var engine=new Engine(session,result);PolicyActivate(session,scout);scout.Character.CurrentHealth=1;enemy.CurrentActionValue=1;
            enemy.Character.Stats.MaxHealth=new Stat(1000000);enemy.Character.CurrentHealth=1000000;var start=scout.Position;
            Assert.That(engine.MatchupIncoming(scout,start),Is.GreaterThan(1));Assert.That(engine.PlayerAction(scout),Is.True);
            Assert.That(scout.Position,Is.Not.EqualTo(start));var refuge=scout.Position;
            Assert.That(engine.PlayerAction(scout),Is.False);Assert.That(scout.Position,Is.EqualTo(refuge));
        }
        [Test]
        public void JointApproachKeepsAnAffordableAttackAheadOfOutOfRangeBuffPreparation()
        {
            var mage=BuildHero(2,48,0);var enemy=PolicyHero(0,false,9,4);
            AssertBuildLoadout(mage.Character,new[]{mage.Character.GetBasicAttackSkill().SkillId,"skill.mage.magic_focus","skill.mage.fireball"});
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};var result=new Result();
            using var engine=new Engine(session,result);PolicyActivate(session,mage);BuildEnableUtility(engine);
            enemy.Character.Stats.MaxHealth=new Stat(1000000);enemy.Character.CurrentHealth=1000000;enemy.CurrentActionValue=10000;
            var position=mage.Position;Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.Position,Is.Not.EqualTo(position));
            Assert.That(result.skills.Any(s=>s.id=="hero:skill.mage.magic_focus"),Is.False);
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(enemy.Character.CurrentHealth,Is.LessThan(1000000));
        }
        [TestCase(10)][TestCase(48)]
        public void JointWarlockWaitsForGroupTransferBeforeReplacingTheFunctionalBar(int level)
        {
            var mage=BuildHero(2,level,0).Character;SetJointLoadout(mage,0,3);
            var ids=mage.GetActiveSkills().Select(s=>s.SkillId).ToArray();
            if(level==10)
            {
                Assert.That(MageCombatSystem.Rank(mage,MageCombatSystem.Warlock,"mass_sacrifice"),Is.Zero);
                Assert.That(ids,Does.Not.Contain("skill.mage.sacrifice"));
                if(mage.GetKnownSkills().Any(s=>s.SkillId=="skill.mage.magic_focus"))Assert.That(ids,Does.Contain("skill.mage.magic_focus"));
            }
            else
            {
                Assert.That(MageCombatSystem.Rank(mage,MageCombatSystem.Warlock,"mass_sacrifice"),Is.GreaterThan(0));
                Assert.That(ids,Does.Contain("skill.mage.sacrifice"));Assert.That(ids,Does.Contain("skill.mage.ritual"));
            }
        }
    }
}
