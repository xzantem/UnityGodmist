using System;
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
        static string TeamPolicyFolder=>Path.GetFullPath("../LocalHandoffs/simulator-team-policy-20261019");
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("L48 U0 real trees, original routes: historical v4 before shared decisions.")]
        public void AuditTeamPolicyBefore(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,firstSeed+4,$"before-L48-U0-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:Path.Combine(TeamPolicyFolder,"before"),fullOnly:true,traceKnockdowns:true,previousTeamPolicy:true);

        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("L48 U0 real trees, original routes: shared recovery/protection/focus decisions only.")]
        public void AuditTeamPolicyAfter(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,firstSeed+4,$"after-L48-U0-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:Path.Combine(TeamPolicyFolder,"after"),fullOnly:true,traceKnockdowns:true,previousMagePolicy:true);

        [Test,Explicit("Historical v4 L48 U0 replay control after integrating the shared policy.")]
        public void AuditTeamPolicyHistoricalControl()
            =>RunRealisticExpeditions(48,0,"forest",DungeonLength.Normal,0,7,"control-L48-U0-forest-S6",firstSeed:6,
                outputFolder:Path.Combine(TeamPolicyFolder,"control"),fullOnly:true,traceKnockdowns:true,previousTeamPolicy:true);

        [TestCase(1,9)][TestCase(4,12)][TestCase(6,3)]
        [Explicit("Read-only exact replay of first permanent-loss battles in the initial shared policy.")]
        public void AuditTeamPolicyRegressionDecisions(int seed,int selectedBattle)
        {
            var folder=Path.Combine(TeamPolicyFolder,"regression-trace");Directory.CreateDirectory(folder);var observations=new SeedDecisionReport();
            RunRealisticExpeditions(48,0,"forest",DungeonLength.Normal,0,seed+1,$"regression-forest-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousMagePolicy:true,auditEngine:(engine,battle)=>
                {
                    if(battle!=selectedBattle)return;
                    engine.AuditPlayerDecision=(actor,after,played)=>observations.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                    engine.Session.SkillResolved+=(actor,skill,id)=>{if(actor.IsPlayerSide)observations.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,$"decisions-S{seed}.json"),JsonUtility.ToJson(observations,true));
        }

        [Test]
        public void TeamPolicyExposedRangedHeroWaitsForConcreteAffordableRecovery()
        {
            var mage=PolicyHero(2,true,2,4);var fallen=PolicyHero(0,true,3,4);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);
            var enemy=PolicyHero(0,false,2,5);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,fallen,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=25;
            fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;fallen.DownedTurnsRemaining=3;fallen.CurrentActionValue=50;
            healer.CurrentActionValue=1;healer.Character.ResourcePool.CurrentValue=100;
            var rng=UnityEngine.Random.state;var ap=healer.CurrentActionPoints;
            Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.True);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));Assert.That(session.ActiveUnit,Is.SameAs(mage));
            Assert.That(healer.CurrentActionPoints,Is.EqualTo(ap));Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            Assert.That(fallen.Character.CurrentHealth,Is.Zero);Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
            engine.PlayerAction(mage);Assert.That(engine.Result.heroRevives,Is.Zero);Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void TeamPolicyRejectsUnfundedLateAndEmergencyDeferrals()
        {
            var mage=PolicyHero(2,true,2,4);var fallen=PolicyHero(0,true,3,4);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,fallen,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;fallen.DownedTurnsRemaining=3;fallen.CurrentActionValue=50;
            healer.CurrentActionValue=1;healer.Character.ResourcePool.CurrentValue=0;Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.False);
            healer.Character.ResourcePool.CurrentValue=100;healer.CurrentActionValue=10000;Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.False);
            healer.CurrentActionValue=1;fallen.DownedTurnsRemaining=1;Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.False);
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(fallen.Character.CurrentHealth,Is.EqualTo(1));Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
        }

        [Test]
        public void TeamPolicyDoesNotPromiseOneHealToTwoDownedPatients()
        {
            var mage=PolicyHero(2,true,2,4);var fallen=PolicyHero(0,true,3,4);var urgent=PolicyHero(1,true,2,5);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);
            var enemy=PolicyHero(0,false,10,4);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,fallen,urgent,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            foreach(var u in new[]{fallen,urgent}){u.Character.LifeState=CharacterLifeState.Downed;u.Character.CurrentHealth=0;}
            fallen.DownedTurnsRemaining=3;fallen.CurrentActionValue=50;urgent.DownedTurnsRemaining=1;urgent.CurrentActionValue=1;
            healer.CurrentActionValue=1;healer.Character.ResourcePool.CurrentValue=30;
            Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.False,"The healer's actual highest-priority recovery belongs to another victim.");
        }

        [Test]
        public void TeamPolicyProtectsExposedRangedHeroAboveHalfHealth()
        {
            var mage=PolicyHero(2,true,3,4);var ally=PolicyHero(0,true,2,5);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,ally,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);
            mage.Character.CurrentHealth=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue()*.75f);
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            Assert.That(engine.PlayerNeedsProtection(mage),Is.True);var start=mage.Position;var hp=mage.Character.CurrentHealth;
            Assert.That(engine.ProtectRangedPlayer(mage),Is.True);Assert.That(mage.Position,Is.Not.EqualTo(start));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(mage.CurrentActionPoints,Is.LessThan(mage.MaxActionPoints));
        }

        [Test]
        public void TeamPolicyProtectsSharedFocusFromNearEqualOscillation()
        {
            var hero=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,2,5);var first=PolicyHero(0,false,7,4);var second=PolicyHero(0,false,7,5);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){hero,mage,first,second}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,hero);
            var focus=engine.UpdatePlayerFocus(hero);second.Character.CurrentHealth-=1;
            Assert.That(engine.UpdatePlayerFocus(mage),Is.SameAs(focus));
            focus.Character.LifeState=CharacterLifeState.Dead;Assert.That(engine.UpdatePlayerFocus(hero),Is.Not.SameAs(focus));
        }

        [Test]
        public void TeamPolicyHistoricalV6RetreatCannotBypassUnfundedNonEmergencyRescue()
        {
            var mage=PolicyHero(2,true,3,4);var fallen=PolicyHero(0,true,2,5);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);
            var enemy=PolicyHero(2,false,7,4);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,fallen,healer,enemy}};
            using var engine=new Engine(session,new Result());engine.UsePreviousCoordinationPolicy=true;PolicyActivate(session,mage);mage.Character.CurrentHealth=25;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;fallen.DownedTurnsRemaining=3;fallen.CurrentActionValue=50;
            healer.CurrentActionValue=1;healer.Character.ResourcePool.CurrentValue=0;
            Assert.That(engine.PlayerNeedsProtection(mage),Is.True);Assert.That(engine.DeferPlayerRescue(mage,fallen),Is.False);
            var start=mage.Position;var ap=mage.CurrentActionPoints;
            Assert.That(engine.ProtectRangedPlayer(mage),Is.False);Assert.That(mage.Position,Is.EqualTo(start));Assert.That(mage.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(engine.Result.heroRevives,Is.EqualTo(1));
            Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
        }

        [Test]
        public void TeamPolicyUnfundedHealerDoesNotChaseAwayFromCriticalPatient()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var mage=PolicyHero(2,true,1,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=1;healer.Character.ResourcePool.CurrentValue=0;
            Assert.That(engine.KeepsTeamSupportReach(healer,new GridPosition(4,4)),Is.False);
            Assert.That(engine.KeepsTeamSupportReach(healer,new GridPosition(2,4)),Is.True);
            engine.PlayerAction(healer);
            Assert.That(Math.Abs(healer.Position.X-mage.Position.X)+Math.Abs(healer.Position.Y-mage.Position.Y),Is.LessThanOrEqualTo(2));
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));Assert.That(healer.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void TeamPolicyWithdrawalKeepsHealerRatherThanDistantRangedBuddyAsAnchor()
        {
            var mage=PolicyHero(2,true,3,4);var scout=PolicyHero(1,true,2,4);var healer=PolicyHero(3,true,7,3);RescueHealer(healer);
            var enemy=PolicyHero(2,false,7,4);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,scout,healer,enemy}};
            using var engine=new Engine(session,new Result());engine.UsePreviousMagePolicy=true; // Historical v5 anchor; v6 permits a safer refuge.
            PolicyActivate(session,mage);mage.Character.CurrentHealth=25;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            var distance=Math.Abs(mage.Position.X-healer.Position.X)+Math.Abs(mage.Position.Y-healer.Position.Y);
            Assert.That(engine.PlayerNeedsProtection(mage),Is.True);engine.ProtectRangedPlayer(mage);
            Assert.That(Math.Abs(mage.Position.X-healer.Position.X)+Math.Abs(mage.Position.Y-healer.Position.Y),Is.LessThanOrEqualTo(Math.Max(3,distance)));
        }
    }
}
