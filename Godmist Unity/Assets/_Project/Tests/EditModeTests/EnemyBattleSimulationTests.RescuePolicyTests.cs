using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string RescueFolder=>Path.GetFullPath("../LocalHandoffs/rescue-mage-policy-20261019");
        static void RescueBurn(BattleUnit victim,int damage=10)
        {
            victim.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId="burn",RemainingDuration=1,
                Lifetime=PassiveEffectLifetime.Timed,TickPhase=PassiveEffectTickPhase.TurnStart,TotalDamage=damage,
                Sources=new(){new PassiveEffectSourceRuntime{SourceCharacterId=victim.Character.CharacterId}}});
        }
        static void RescueHealer(BattleUnit unit)
        {
            var ids=new[]{unit.Character.GetBasicAttackSkill().SkillId,"skill.paladin.heal"};
            Assert.That(unit.Character.ActiveSkillLoadout.TryInitialize(unit.Character.GetKnownSkills().Select(s=>s.SkillId),ids[0],ids),Is.True);
        }
        [Test]
        public void RescuePolicyDefersLethalDotButAllowsEmergencyHelpUp()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var healer=PolicyHero(3,true,3,5);var enemy=PolicyHero(0,false,10,4);RescueHealer(healer);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;RescueBurn(mage);
            mage.CurrentActionValue=1;healer.CurrentActionValue=2;healer.Character.ResourcePool.CurrentValue=100;
            var random=UnityEngine.Random.state;var resource=helper.Character.ResourcePool.CurrentValue;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
            Assert.That(engine.CanWaitForRecovery(mage),Is.True);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(mage.Character.CurrentHealth,Is.Zero);
            engine.PlayerAction(helper);Assert.That(engine.Result.heroRevives,Is.Zero);
            PolicyActivate(session,helper);helper.Position=new GridPosition(2,4);mage.DownedTurnsRemaining=1;
            Assert.That(engine.PlayerAction(helper),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
            Assert.That(engine.Result.riskRescues,Is.EqualTo(1));Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));
            Assert.That(helper.Character.ResourcePool.CurrentValue,Is.EqualTo(resource));
        }
        [Test]
        public void RescuePolicyHistoricalV6KeepsAllyAliveWhenNoSafeRecoveryPlanExists()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var scout=PolicyHero(1,true,1,7);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());engine.UsePreviousCoordinationPolicy=true;PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;RescueBurn(mage);
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);Assert.That(engine.CanWaitForRecovery(mage),Is.False);
            Assert.That(engine.PlayerAction(helper),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));
        }
        [Test]
        public void RescuePolicyHealsImminentDeadlineBeforeLargerDownedAlly()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var mage=PolicyHero(2,true,4,4);var warrior=PolicyHero(0,true,3,5);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);healer.Character.ResourcePool.CurrentValue=100;
            foreach(var fallen in new[]{mage,warrior}){fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;}
            mage.DownedTurnsRemaining=1;mage.CurrentActionValue=1;warrior.DownedTurnsRemaining=3;warrior.CurrentActionValue=20;
            Assert.That(engine.PlayerAction(healer),Is.True);Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(warrior.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }
        [Test]
        public void RescuePolicyRecognizesAffordableHealingBeforeDotAndRejectsEmptyMana()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var healer=PolicyHero(3,true,3,5);var enemy=PolicyHero(0,false,10,4);RescueHealer(healer);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;RescueBurn(mage);
            mage.CurrentActionValue=10;healer.CurrentActionValue=1;enemy.CurrentActionValue=20;
            healer.Character.ResourcePool.CurrentValue=100;healer.CurrentActionPoints=0;
            var random=UnityEngine.Random.state;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.True);
            Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));Assert.That(mage.Character.CurrentHealth,Is.Zero);
            Assert.That(healer.CurrentActionPoints,Is.Zero);Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            Assert.That(session.ActiveUnit,Is.SameAs(helper));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            healer.Character.ResourcePool.CurrentValue=0;Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
            healer.Character.ResourcePool.CurrentValue=100;healer.CurrentActionValue=11;Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
        }
        [Test]
        public void RescuePolicyPrioritizesExposedMageOverSafeMaintenanceHealing()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);
            var mage=PolicyHero(2,true,4,4);var scout=PolicyHero(1,true,2,5);var enemy=PolicyHero(0,false,5,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=1;scout.Character.CurrentHealth=Mathf.RoundToInt(scout.Character.Stats.MaxHealth.GetValue()*.5f);
            enemy.CurrentActionValue=1;mage.CurrentActionValue=10;
            Assert.That(engine.PlayerAction(healer),Is.True);Assert.That(mage.Character.CurrentHealth,Is.GreaterThan(1));
            Assert.That(engine.Result.heroRevives,Is.Zero);
        }
        [Test]
        public void RescuePolicyStagesHelperBeforeDeadlineWithoutRevivingIntoLethalDot()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,6,4);var scout=PolicyHero(1,true,1,7);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;RescueBurn(mage);
            Assert.That(engine.PlayerAction(helper),Is.True);
            Assert.That(helper.Position.X,Is.GreaterThan(2));Assert.That(engine.Result.heroRevives,Is.Zero);
            Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
            PolicyActivate(session,helper);helper.Position=new GridPosition(5,4);mage.DownedTurnsRemaining=1;
            Assert.That(engine.PlayerAction(helper),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
        }
        [Test]
        public void RescuePolicyRecoversFreeEmbeddedDaggerBeforeHelpUpEndsTurn()
        {
            var scout=PolicyHero(1,true,2,4);var mage=PolicyHero(2,true,3,4);var enemy=PolicyHero(0,false,2,5);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){scout,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,scout);
            scout.AvailableKnives=0;enemy.ScoutState.Knives.Add(new EmbeddedScoutKnife{Owner=scout,BleedMultiplier=.6f});
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;
            var ap=scout.CurrentActionPoints;
            Assert.That(engine.PlayerAction(scout),Is.True);Assert.That(scout.AvailableKnives,Is.EqualTo(1));
            Assert.That(engine.Result.heroRevives,Is.Zero);Assert.That(scout.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(engine.PlayerAction(scout),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
        }
        [Test]
        public void RescuePolicyHistoricalV6DoesNotTreatPossibleEnemyShotAsCertainVictimChoice()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var scout=PolicyHero(1,true,3,3);var enemy=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());engine.UsePreviousCoordinationPolicy=true;PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;
            enemy.CurrentActionValue=1;mage.CurrentActionValue=10;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.True,"No known DoT or committed attack: do not assume the enemy will choose the revived target.");
        }
        [Test]
        public void RescuePolicyUsesAvDeadlineForFastDownedAlly()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var scout=PolicyHero(1,true,1,7);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=2;
            mage.Character.Stats.Speed=new Stat(1000);mage.CurrentActionValue=1;RescueBurn(mage);
            Assert.That(engine.RescueDeadline(mage),Is.LessThan(CombatTurnSystem.CalculateBaseActionValue(helper)));
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
            Assert.That(engine.PlayerAction(helper),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
        }
        [Test]
        public void RescuePolicyObservesActualHealingOnceWithAndWithoutFeedback()
        {
            var mage=PolicyHero(2,true,3,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            var result=new Result();var engine=new Engine(session,result);var max=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue());
            mage.Character.CurrentHealth=max-25;
            Assert.That(engine.KnockdownTestRecovery.Heal(mage,10),Is.EqualTo(10));
            Assert.That(new CombatRecoverySystem().Heal(mage,100,reportingSourceId:"mage.fire_feeding"),Is.EqualTo(15));
            Assert.That(result.heroHealing,Is.EqualTo(25));Assert.That(result.heroHealingPreviouslyUnreported,Is.EqualTo(15));
            Assert.That(result.heroFireFeedingHealing,Is.EqualTo(15));
            Assert.That(new CombatRecoverySystem().Heal(mage,100),Is.Zero);Assert.That(result.heroHealing,Is.EqualTo(25));
            engine.Dispose();mage.Character.CurrentHealth-=5;new CombatRecoverySystem().Heal(mage,5);
            Assert.That(result.heroHealing,Is.EqualTo(25),"Disposed combat must not observe exploration or next-combat healing.");
        }
        [Test]
        public void RescuePolicyReportsFireFeedingWithoutChangingPayoutOrPayingTwice()
        {
            var mage=PolicyHero(2,true,3,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            var result=new Result();using var engine=new Engine(session,result);
            mage.Character.SkillTreeProgress.Add(new SkillTreeProgress{TreeId=MageCombatSystem.Fire,
                NodeInvestments=new(){new SkillTreeNodeInvestment{NodeId="node.mage.fire.fire_feeding",Ranks=2}}});
            mage.Character.CurrentHealth-=40;
            var burn=new PassiveEffectRuntime{EffectId="burn"};var hp=mage.Character.CurrentHealth;
            MageCombatSystem.AfterBurnTick(session,burn,100,true);MageCombatSystem.AfterBurnTick(session,burn,100,true);
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(hp+20));Assert.That(result.heroHealing,Is.EqualTo(20));
            Assert.That(result.heroFireFeedingHealing,Is.EqualTo(20));
        }
        [TestCase("catacombs")][TestCase("forest")]
        [Explicit("Reporting-only control keeps previous decisions; proves HP/results unchanged by complete heal observation.")]
        public void AuditRescueReportingControl(string biome)
            =>RunRealisticExpeditions(48,.5f,biome,DungeonLength.Normal,0,1,$"reporting-control-{biome}",
                outputFolder:Path.Combine(RescueFolder,"control"),fullOnly:true,traceKnockdowns:true,previousRescuePolicy:true);

        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Paired original normal L48 EQ50 routes; rescue/mage decisions only, full real healing reporting.")]
        public void AuditRescuePolicyExpeditions(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,.5f,biome,DungeonLength.Normal,0,firstSeed+4,$"v3-L48-U50-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:RescueFolder,fullOnly:true,traceKnockdowns:true,previousSupportPolicy:true);

        [TestCase("forest",0)][TestCase("forest",1)][TestCase("forest",5)][TestCase("forest",8)][TestCase("catacombs",1)]
        [Explicit("Targeted replay of rescue deadline regressions before the final paired matrix.")]
        public void AuditRescuePolicyProbe(string biome,int seed)
            =>RunRealisticExpeditions(48,.5f,biome,DungeonLength.Normal,0,seed+1,$"probe-{biome}-S{seed}",
                firstSeed:seed,outputFolder:Path.Combine(RescueFolder,"probe"),fullOnly:true,traceKnockdowns:true,previousSupportPolicy:true);
    }
}
