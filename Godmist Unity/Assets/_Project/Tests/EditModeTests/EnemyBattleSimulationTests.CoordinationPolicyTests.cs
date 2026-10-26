using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string CoordinationFolder=>Path.GetFullPath("../LocalHandoffs/simulator-coordination-20261019");
        [TestCase(1)][TestCase(4)]
        [Explicit("Read-only replay of initial v7 failures; exact live decisions, support and positions.")]
        public void AuditCoordinationStress(int seed)
        {
            var folder=Path.Combine(CoordinationFolder,"stress-initial");Directory.CreateDirectory(folder);
            var report=new ForestDecisionAudit();
            RunRealisticExpeditions(48,0,"forest",DungeonLength.Normal,0,seed+1,$"stress-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,auditEngine:(engine,battle)=>
                {
                    if(battle!=15)return;
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        report.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                        if(!after)report.details.Add(engine.CaptureForestDetail(actor,battle));
                    };
                    engine.Session.SkillResolved+=(actor,skill,id)=>{if(actor.IsPlayerSide)report.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,$"decisions-S{seed}.json"),JsonUtility.ToJson(report,true));
        }
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Same 24 full L48 EQ0 real-tree routes; v7 player decisions only.")]
        public void AuditCoordinationExpeditions(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,firstSeed+4,$"v7-L48-U0-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:Path.Combine(CoordinationFolder,"after"),fullOnly:true,traceKnockdowns:true);

        [TestCase("forest",6)][TestCase("catacombs",7)]
        [Explicit("Exact archived v6 controls, including its previously worst full routes.")]
        public void AuditCoordinationHistoricalControl(string biome,int seed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"v6-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(CoordinationFolder,"control"),fullOnly:true,traceKnockdowns:true,previousCoordinationPolicy:true);

        [Test]
        public void CoordinationFullTurnDoesNotPermitLethalMageSetupWithoutShot()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            mage.Character.CurrentHealth=1;mage.CurrentActionPoints=1125;enemy.CurrentActionValue=1;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);
            var rng=Random.state;var start=mage.Position;
            Assert.That(engine.SafeCoordinatedSetup(mage,new GridPosition(4,4)),Is.False);
            engine.PlayerAction(mage);
            Assert.That(mage.Position,Is.EqualTo(start));Assert.That(engine.Result.damage,Is.Zero);
            Assert.That(Random.state,Is.EqualTo(rng));
            engine.UsePreviousCoordinationPolicy=true;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.Position,Is.Not.EqualTo(start));
        }

        [Test]
        public void CoordinationHealthyMageCanPrepareAndExposedMageCanReduceRisk()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);enemy.CurrentActionValue=1;
            enemy.Character.Stats.MinAttack=new Stat(1);enemy.Character.Stats.MaxAttack=new Stat(1);
            Assert.That(engine.SafeCoordinatedSetup(mage,new GridPosition(4,4)),Is.True);
            mage.Position=new GridPosition(4,4);mage.Character.CurrentHealth=1;
            Assert.That(engine.SafeCoordinatedSetup(mage,new GridPosition(0,4)),Is.True);
        }

        [Test]
        public void CoordinationMovesAndExecutesAffordableFocusKillInsteadOfHittingNearbyTank()
        {
            var warrior=PolicyHero(0,true,2,4);var tank=PolicyHero(0,false,2,5);var weak=PolicyHero(0,false,4,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){warrior,tank,weak}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,warrior);
            tank.Character.CurrentHealth=100000;tank.Character.Stats.MaxHealth=new Stat(100000);
            weak.Character.CurrentHealth=1;weak.Character.Stats.Dodge=new Stat(0);warrior.Character.Stats.WeaponAccuracy=new Stat(1);
            BattleUnit selected=null;engine.AuditChosenPlan=(actor,skill,targets,score)=>selected=targets.FirstOrDefault();
            var start=warrior.Position;var focus=engine.UpdatePlayerFocus(warrior);Assert.That(focus,Is.SameAs(weak));
            Assert.That(engine.PlayerAction(warrior),Is.True);Assert.That(warrior.Position,Is.Not.EqualTo(start));
            Assert.That(weak.Character.CurrentHealth,Is.EqualTo(1),"Movement itself cannot deal a free hit.");
            Assert.That(engine.PlayerAction(warrior),Is.True);
            Assert.That(selected,Is.SameAs(weak),"The committed cast must target the projected focus, even if the real hit roll misses.");
            Assert.That(engine.Result.heroActions,Is.EqualTo(1));
            Assert.That(tank.Character.CurrentHealth,Is.EqualTo(100000));
        }

        [Test]
        public void CoordinationWaitsBeforeExposedOneHpReviveButKeepsEmergencyFallback()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var enemy=PolicyHero(0,false,4,4);var scout=PolicyHero(1,true,0,9);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,scout,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.DownedTurnsRemaining=3;
            mage.CurrentActionValue=10;enemy.CurrentActionValue=1;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
            engine.PlayerAction(helper);Assert.That(engine.Result.heroRevives,Is.Zero);
            mage.DownedTurnsRemaining=1;PolicyActivate(session,helper);helper.Position=new GridPosition(2,4);
            Assert.That(engine.PlayerAction(helper),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));
        }

        [Test]
        public void CoordinationMeleeUsesAffordableAlternativeWhenSharedFocusCannotBeReached()
        {
            var warrior=PolicyHero(0,true,2,4);var near=PolicyHero(0,false,4,4);var distant=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){warrior,near,distant}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,warrior);
            near.Character.CurrentHealth=100000;near.Character.Stats.MaxHealth=new Stat(100000);distant.Character.CurrentHealth=1;
            Assert.That(engine.UpdatePlayerFocus(warrior),Is.SameAs(distant));
            BattleUnit target=null;engine.AuditChosenPlan=(actor,skill,targets,score)=>target=targets.FirstOrDefault();
            Assert.That(engine.PlayerAction(warrior),Is.True);Assert.That(target,Is.Null,"A movement action cannot grant a free attack.");
            Assert.That(engine.PlayerAction(warrior),Is.True);Assert.That(target,Is.SameAs(near));
        }

        [Test]
        public void CoordinationNearestRangedRescuerStillStagesBeforeUnsafeReviveDeadline()
        {
            var scout=PolicyHero(1,true,2,4);var mage=PolicyHero(2,true,6,4);var warrior=PolicyHero(0,true,0,9);var enemy=PolicyHero(0,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){scout,mage,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,scout);
            mage.Character.CurrentHealth=0;mage.Character.LifeState=CharacterLifeState.Downed;mage.DownedTurnsRemaining=2;mage.CurrentActionValue=1;
            enemy.CurrentActionValue=1;
            Assert.That(engine.SafePlayerHelpUp(scout,mage),Is.False);
            Assert.That(engine.DeferPlayerRescue(scout,mage),Is.False,"The designated helper must not delegate to nobody.");
            var before=scout.Position;
            Assert.That(engine.PlayerAction(scout),Is.True);Assert.That(scout.Position.X,Is.GreaterThan(before.X));
            Assert.That(engine.Result.heroRevives,Is.Zero);Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void CoordinationWoundedHealerCanWithdrawAndAvoidLethalStaging()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var warrior=PolicyHero(0,true,2,5);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);healer.Character.CurrentHealth=1;
            healer.Character.ResourcePool.CurrentValue=0;enemy.CurrentActionValue=1;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);
            Assert.That(engine.SafeCoordinatedSetup(healer,new GridPosition(5,4)),Is.False);
            var before=engine.MatchupIncoming(healer,healer.Position);
            Assert.That(engine.ProtectRangedPlayer(healer),Is.True);
            Assert.That(engine.MatchupIncoming(healer,healer.Position),Is.LessThan(before));
            Assert.That(healer.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void CoordinationExposedMageCanRetreatWhileUnfundedRescueWaits()
        {
            var mage=PolicyHero(2,true,3,4);var fallen=PolicyHero(0,true,2,5);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);
            var enemy=PolicyHero(2,false,7,4);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,fallen,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=25;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;fallen.DownedTurnsRemaining=3;fallen.CurrentActionValue=50;
            healer.CurrentActionValue=1;healer.Character.ResourcePool.CurrentValue=0;
            Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.True);
            Assert.That(engine.ProtectRangedPlayer(mage),Is.True);
            Assert.That(engine.Result.heroRevives,Is.Zero);Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void CoordinationRecoveryPreviewRequiresRealEarlierAffordableHealAndRestoresState()
        {
            var helper=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,3,4);var healer=PolicyHero(3,true,3,5);var enemy=PolicyHero(0,false,4,4);
            RescueHealer(healer);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){helper,mage,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,helper);
            mage.Character.LifeState=CharacterLifeState.Downed;mage.Character.CurrentHealth=0;mage.CurrentActionValue=20;
            enemy.CurrentActionValue=10;healer.CurrentActionValue=1;healer.CurrentActionPoints=0;healer.Character.ResourcePool.CurrentValue=100;
            enemy.Character.Stats.MinAttack=new Stat(10);enemy.Character.Stats.MaxAttack=new Stat(10);
            var rng=Random.state;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.True);
            Assert.That(mage.Character.CurrentHealth,Is.Zero);Assert.That(mage.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
            Assert.That(healer.CurrentActionPoints,Is.Zero);Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            Assert.That(session.ActiveUnit,Is.SameAs(helper));Assert.That(Random.state,Is.EqualTo(rng));
            healer.Character.ResourcePool.CurrentValue=0;Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
            healer.Character.ResourcePool.CurrentValue=100;healer.CurrentActionValue=11;
            Assert.That(engine.SafePlayerHelpUp(helper,mage),Is.False);
        }
    }
}
