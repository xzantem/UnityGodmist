using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string MageRecoveryFolder=>Path.GetFullPath("../LocalHandoffs/simulator-mage-recovery-20261019");
        static BattleUnit RecoveryArcher(int x,int y)
        {
            var definition=PointEnemyDefinitions.Single(d=>d.Id=="enemy.forest.elf_archer");
            var unit=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,1),false);
            unit.Position=new GridPosition(x,y);return unit;
        }
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("v6 same 24 cached L48 U0 full routes, actual trees and resources; no statistic/mechanic changes.")]
        public void AuditMageRecoveryExpeditions(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,firstSeed+4,$"v6-L48-U0-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:Path.Combine(MageRecoveryFolder,"after"),fullOnly:true,traceKnockdowns:true);

        [TestCase("forest",1)][TestCase("catacombs",6)]
        [Explicit("Exact v5 historical control after integrating v6 decisions.")]
        public void AuditMageRecoveryHistoricalControl(string biome,int seed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"v5-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(MageRecoveryFolder,"control"),fullOnly:true,traceKnockdowns:true,previousMagePolicy:true);

        [Test]
        public void MageRecoveryIdentifiesLethalReentryReducedExposureAndHistoricalControl()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            var rng=Random.state;var ap=mage.CurrentActionPoints;
            Assert.That(engine.SafeMageApproach(mage,new GridPosition(1,4)),Is.False);
            mage.Position=new GridPosition(3,4);Assert.That(engine.SafeMageApproach(mage,new GridPosition(0,4)),Is.True);
            mage.Position=new GridPosition(0,4);engine.UsePreviousMagePolicy=true;
            Assert.That(engine.SafeMageApproach(mage,new GridPosition(1,4)),Is.True);
            Assert.That(mage.CurrentActionPoints,Is.EqualTo(ap));Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));Assert.That(Random.state,Is.EqualTo(rng));
        }

        [Test]
        public void MageRecoveryMovementDependsOnActualRangeAdvantageRatherThanDungeonTheme()
        {
            var mage=PolicyHero(2,true,0,4);var equalRange=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,equalRange}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;
            equalRange.Character.Stats.MinAttack=new Stat(1000);equalRange.Character.Stats.MaxAttack=new Stat(1000);equalRange.CurrentActionValue=1;
            Assert.That(engine.SafeMageApproach(mage,new GridPosition(1,4)),Is.True);
            var archer=RecoveryArcher(10,4);session.Units.Add(archer);archer.CurrentActionValue=1;
            Assert.That(engine.SafeMageApproach(mage,new GridPosition(1,4)),Is.False);
        }

        [Test]
        public void MageRecoveryActualMoveDoesNotLeaveRefugeForAnUnaffordableShot()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;mage.CurrentActionPoints=100;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            engine.PlayerAction(mage);
            Assert.That(engine.PlayerNeedsProtection(mage),Is.False,"A future firing lane is not worth entering known lethal exposure without a shot.");
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));Assert.That(engine.Result.damage,Is.Zero);
        }

        [Test]
        public void MageRecoveryExecutesPlannedMoveAndShotInsteadOfUndoingItsOwnApproach()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;mage.CurrentActionPoints=2000;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.Position,Is.Not.EqualTo(new GridPosition(0,4)));
            var position=mage.Position;var points=mage.CurrentActionPoints;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.Position,Is.EqualTo(position));
            Assert.That(engine.Result.skills.Any(s=>s.id.StartsWith("hero:")),Is.True);
            Assert.That(mage.CurrentActionPoints,Is.LessThan(points));
        }

        [Test]
        public void MageRecoveryDoesNotReserveSupportManaForAHealthyMageWithoutDot()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.holy_regeneration");
            var mage=PolicyHero(2,true,4,4);var warrior=PolicyHero(0,true,3,5);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);healer.Character.ResourcePool.CurrentValue=63;
            enemy.CurrentActionValue=1;mage.CurrentActionValue=50;
            Assert.That(engine.PreservesMageRecoveryMana(healer,SupportSkill(healer,"skill.paladin.holy_regeneration"),
                new(){{0,new[]{warrior}}}),Is.True);
        }

        [Test]
        public void MageRecoveryKeepsUtilityReserveAfterHealingMakesThePartyLookHealthy()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.blessing");
            var mage=PolicyHero(2,true,4,4);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            healer.Character.ResourcePool.CurrentValue=45;mage.CurrentActionValue=50;enemy.CurrentActionValue=1;
            var bless=SupportSkill(healer,"skill.paladin.blessing");
            Assert.That(engine.PreservesSupportMana(healer,bless),Is.False);
            engine.UsePreviousMagePolicy=true;Assert.That(engine.PreservesSupportMana(healer,bless),Is.True);
            Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(45));
        }

        [Test]
        public void MageRecoveryCommittedShotStillRequiresActualRemainingPoints()
        {
            var mage=PolicyHero(2,true,0,4);var enemy=RecoveryArcher(10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;mage.CurrentActionPoints=2000;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            Assert.That(engine.PlayerAction(mage),Is.True);mage.CurrentActionPoints=0;
            Assert.That(engine.PlayerAction(mage),Is.False);Assert.That(engine.Result.skills,Is.Empty);
            Assert.That(mage.CurrentActionPoints,Is.Zero);
        }

        [Test]
        public void MageRecoveryHistoricalV6CanPrepareAgainstLongerRangeWithoutFreezingOutsideItsOwnRange()
        {
            var mage=PolicyHero(2,true,0,5);var definition=PointEnemyDefinitions.Single(d=>d.Id=="enemy.forest.elf_archer");
            var enemy=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,1),false);enemy.Position=new GridPosition(10,5);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,enemy}};
            using var engine=new Engine(session,new Result());engine.UsePreviousCoordinationPolicy=true;PolicyActivate(session,mage);mage.Character.CurrentHealth=1;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            Assert.That(engine.PlayerAction(mage),Is.True);Assert.That(mage.Position.X,Is.GreaterThan(0));
            Assert.That(mage.CurrentActionPoints,Is.GreaterThan(0));
        }

        [Test]
        public void MageRecoveryRefugeIsNotBlockedByAnUnfundedHealerAnchor()
        {
            var mage=PolicyHero(2,true,2,4);var scout=PolicyHero(1,true,1,5);var healer=PolicyHero(3,true,6,3);RescueHealer(healer);
            var enemy=RecoveryArcher(10,4);var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,scout,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;healer.Character.ResourcePool.CurrentValue=0;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;
            Assert.That(engine.ProtectRangedPlayer(mage),Is.True);Assert.That(engine.PlayerNeedsProtection(mage),Is.False);
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));Assert.That(healer.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void MageRecoveryHealsKnownLethalThreatAboveHalfHealthBeforeMaintenance()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var mage=PolicyHero(2,true,4,4);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue()*.6f);var before=mage.Character.CurrentHealth;
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);enemy.CurrentActionValue=1;mage.CurrentActionValue=50;
            healer.Character.ResourcePool.CurrentValue=30;
            Assert.That(engine.PlayerNeedsProtection(mage),Is.True);Assert.That(engine.PlayerAction(healer),Is.True);
            Assert.That(mage.Character.CurrentHealth,Is.GreaterThan(before));Assert.That(healer.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void MageRecoveryCountsTheTurnAMageWillLoseToKnownCrowdControl()
        {
            var healer=PolicyHero(3,true,3,4);RescueHealer(healer);var mage=PolicyHero(2,true,4,4);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);
            mage.Character.CurrentHealth=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue()*.6f);var hp=mage.Character.CurrentHealth;
            mage.CurrentActionValue=1;enemy.CurrentActionValue=2;mage.Character.Resistances.SuppressionResistance=0;
            Assert.That(new StatusEffectSystem().ApplyStatus(enemy,mage,"stun",1,2,1,0,0,"mage.control"),Is.True);
            enemy.Character.Stats.MinAttack=new Stat(1000);enemy.Character.Stats.MaxAttack=new Stat(1000);healer.Character.ResourcePool.CurrentValue=30;
            Assert.That(engine.PlayerNeedsProtection(mage),Is.True);
            engine.UsePreviousMagePolicy=true;Assert.That(engine.PlayerNeedsProtection(mage),Is.False);
            engine.UsePreviousMagePolicy=false;Assert.That(engine.PlayerAction(healer),Is.True);Assert.That(mage.Character.CurrentHealth,Is.GreaterThan(hp));
        }

        [Test]
        public void MageRecoveryMaintenancePreservesImmediateHealButHealingMageCanSpendIt()
        {
            var healer=PolicyHero(3,true,3,4);SupportLoadout(healer,"skill.paladin.heal","skill.paladin.holy_regeneration");
            var mage=PolicyHero(2,true,4,4);var warrior=PolicyHero(0,true,3,5);var enemy=PolicyHero(2,false,7,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){healer,mage,warrior,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,healer);healer.Character.ResourcePool.CurrentValue=63;
            mage.Character.CurrentHealth=Mathf.RoundToInt(mage.Character.Stats.MaxHealth.GetValue()*.6f);
            enemy.CurrentActionValue=1;mage.CurrentActionValue=50;warrior.Character.CurrentHealth=1;
            var regen=SupportSkill(healer,"skill.paladin.holy_regeneration");var heal=SupportSkill(healer,"skill.paladin.heal");
            var others=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{warrior}}};
            Assert.That(engine.PreservesMageRecoveryMana(healer,regen,others),Is.False);
            mage.Character.CurrentHealth=1;healer.Character.ResourcePool.CurrentValue=30;
            Assert.That(engine.PreservesMageRecoveryMana(healer,heal,new(){{0,new[]{mage}}}),Is.True);
            engine.UsePreviousMagePolicy=true;Assert.That(engine.PreservesMageRecoveryMana(healer,regen,others),Is.True);
        }

        [Test]
        public void MageRecoveryReachRestoresProjectionAndRejectsUnfundedRecovery()
        {
            var mage=PolicyHero(2,true,2,4);var healer=PolicyHero(3,true,3,5);RescueHealer(healer);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,healer,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,mage);mage.Character.CurrentHealth=1;
            var position=mage.Position;var points=healer.CurrentActionPoints;var rng=Random.state;
            healer.Character.ResourcePool.CurrentValue=0;Assert.That(engine.MageRecoveryReach(healer,mage,new GridPosition(1,4)),Is.False);
            healer.Character.ResourcePool.CurrentValue=100;Assert.That(engine.MageRecoveryReach(healer,mage,new GridPosition(1,4)),Is.True);
            Assert.That(mage.Position,Is.EqualTo(position));Assert.That(session.ActiveUnit,Is.SameAs(mage));Assert.That(healer.CurrentActionPoints,Is.EqualTo(points));
            Assert.That(healer.Character.ResourcePool.CurrentValue,Is.EqualTo(100));Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));Assert.That(Random.state,Is.EqualTo(rng));
        }
    }
}
