using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string PlayerPolicyFolder=>Path.GetFullPath("../LocalHandoffs/simulator-player-policy-20261019");
        static BattleUnit PolicyHero(int index,bool player,int x,int y)
        {
            var definition=PointHeroDefinition(index);var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,1);
            hero.CharacterId=$"policy-{index}-{player}-{x}-{y}";
            var basic=hero.GetBasicAttackSkill();
            Assert.That(hero.ActiveSkillLoadout.TryInitialize(hero.GetKnownSkills().Select(s=>s.SkillId),basic.SkillId,new[]{basic.SkillId}),Is.True);
            return new BattleUnit{Character=hero,IsPlayerSide=player,Position=new GridPosition(x,y)};
        }
        static void PolicyActivate(CombatSession session,BattleUnit actor)
        {
            session.ActiveUnit=actor;session.ActiveParticipant=session.TurnParticipants.Single(p=>p.Unit==actor);
            CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
        }
        [Test]
        public void PlayerPolicyCanReviveSameAllyTwiceWithoutSpendingResources()
        {
            var actor=PolicyHero(0,true,2,4);var ally=PolicyHero(2,true,3,4);var enemy=PolicyHero(0,false,9,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,ally,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            var resource=actor.Character.ResourcePool.CurrentValue;
            for(var i=0;i<2;i++)
            {
                PolicyActivate(session,actor); // Two separate actor turns, one encounter/Engine.
                ally.Character.LifeState=CharacterLifeState.Downed;ally.Character.CurrentHealth=0;ally.DownedTurnsRemaining=3;
                Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(ally.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            }
            Assert.That(engine.Result.heroRevives,Is.EqualTo(2));Assert.That(actor.Character.ResourcePool.CurrentValue,Is.EqualTo(resource));
        }
        [Test]
        public void PlayerPolicyHealsDownedAllyBeforeRegeneratingAnotherWoundedAlly()
        {
            var actor=PolicyHero(3,true,3,4);var fallen=PolicyHero(2,true,4,4);var wounded=PolicyHero(0,true,3,5);var enemy=PolicyHero(0,false,9,4);
            actor.Character=EnemyScalingBalanceTests.Hero(3,48);actor.Character.CharacterId="policy-healer";
            var ids=new[]{actor.Character.GetBasicAttackSkill().SkillId,"skill.paladin.heal","skill.paladin.holy_regeneration"};
            Assert.That(actor.Character.ActiveSkillLoadout.TryInitialize(actor.Character.GetKnownSkills().Select(s=>s.SkillId),ids[0],ids),Is.True);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,fallen,wounded,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            fallen.Character.LifeState=CharacterLifeState.Downed;fallen.Character.CurrentHealth=0;fallen.DownedTurnsRemaining=3;wounded.Character.CurrentHealth=1;
            Assert.That(engine.PlayerAction(actor),Is.True);
            Assert.That(fallen.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(engine.Result.skills.Any(s=>s.id=="hero:skill.paladin.heal"),Is.True);
            Assert.That(wounded.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration"),Is.False);
        }
        [Test]
        public void PlayerPolicyFocusIsSharedStableAndReplacedAfterDeath()
        {
            var actor=PolicyHero(0,true,2,4);var mage=PolicyHero(2,true,2,5);var weak=PolicyHero(0,false,7,4);var other=PolicyHero(0,false,7,5);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,mage,weak,other}};
            using var engine=new Engine(session,new Result());weak.Character.CurrentHealth=1;
            Assert.That(engine.UpdatePlayerFocus(actor),Is.SameAs(weak));
            other.Character.CurrentHealth=1;Assert.That(engine.UpdatePlayerFocus(mage),Is.SameAs(weak));
            weak.Character.LifeState=CharacterLifeState.Dead;
            Assert.That(engine.UpdatePlayerFocus(mage),Is.SameAs(other));
        }
        [Test]
        public void PlayerPolicyFocusNeverSelectsInvisibleEnemy()
        {
            var actor=PolicyHero(0,true,2,4);var hidden=PolicyHero(0,false,7,4);var visible=PolicyHero(0,false,7,5);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,hidden,visible}};
            using var engine=new Engine(session,new Result());hidden.Character.CurrentHealth=1;hidden.Character.Resistances.SuppressionResistance=0;
            Assert.That(engine.UpdatePlayerFocus(actor),Is.SameAs(hidden));
            Assert.That(new StatusEffectSystem().ApplyStatus(hidden,hidden,"invisibility",1,3,1,0,0,"policy.hidden"),Is.True);
            Assert.That(engine.UpdatePlayerFocus(actor),Is.SameAs(visible));
        }
        [Test]
        public void PlayerPolicyRangedMoveReservesRealAttackCost()
        {
            var actor=PolicyHero(2,true,1,4);var enemy=PolicyHero(0,false,8,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            var firstHp=enemy.Character.CurrentHealth;
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(actor.Position,Is.Not.EqualTo(new GridPosition(1,4)));
            Assert.That(actor.CurrentActionPoints,Is.GreaterThanOrEqualTo(CombatActionCostCalculator.CalculateSkillCost(actor,actor.Character.GetBasicAttackSkill())));
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(firstHp),"Planning must not deal damage.");
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(engine.Result.heroActions,Is.EqualTo(1));
        }
        [Test]
        public void PlayerPolicyRangedDoesNotChargeAfterSpendingAttackPoints()
        {
            var actor=PolicyHero(2,true,2,4);var enemy=PolicyHero(0,false,6,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            actor.CurrentActionPoints=CombatActionCostCalculator.CalculateSkillCost(actor,actor.Character.GetBasicAttackSkill())-1;
            var points=actor.CurrentActionPoints;
            Assert.That(engine.PlayerAction(actor),Is.False);Assert.That(actor.Position,Is.EqualTo(new GridPosition(2,4)));Assert.That(actor.CurrentActionPoints,Is.EqualTo(points));
        }
        [Test]
        public void PlayerPolicyProjectionRestoresPositionPointsAndResource()
        {
            var actor=PolicyHero(2,true,2,4);var enemy=PolicyHero(0,false,6,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);engine.UpdatePlayerFocus(actor);
            var position=actor.Position;var points=actor.CurrentActionPoints;var resource=actor.Character.ResourcePool.CurrentValue;var hp=actor.Character.CurrentHealth;
            Assert.That(engine.CanProjectPlayerAttack(actor,new GridPosition(3,4),actor.MaxActionPoints),Is.True);
            Assert.That(actor.Position,Is.EqualTo(position));Assert.That(actor.CurrentActionPoints,Is.EqualTo(points));
            Assert.That(actor.Character.ResourcePool.CurrentValue,Is.EqualTo(resource));Assert.That(actor.Character.CurrentHealth,Is.EqualTo(hp));
            Assert.That(engine.CanProjectPlayerAttack(actor,new GridPosition(3,4),0),Is.False);
            Assert.That(actor.Position,Is.EqualTo(position));Assert.That(actor.CurrentActionPoints,Is.EqualTo(points));
            actor.Character.ResourcePool.CurrentValue=0;
            Assert.That(engine.CanProjectPlayerAttack(actor,new GridPosition(3,4),actor.MaxActionPoints),Is.False,"Projection cannot fund a mana-cost spell.");
            Assert.That(actor.Character.ResourcePool.CurrentValue,Is.Zero);Assert.That(actor.Position,Is.EqualTo(position));Assert.That(actor.CurrentActionPoints,Is.EqualTo(points));
        }
        [Test]
        public void PlayerPolicyMomentumRepositionPreservesFiringLane()
        {
            var actor=PolicyHero(1,true,2,4);var basic=actor.Character.GetBasicAttackSkill();
            var special=actor.Character.GetKnownSkills().First(s=>s.ResourceCost>0&&s.Effects.Any(e=>e.EffectType==EffectType.DealDamage));
            Assert.That(actor.Character.ActiveSkillLoadout.TryInitialize(actor.Character.GetKnownSkills().Select(s=>s.SkillId),basic.SkillId,new[]{basic.SkillId,special.SkillId}),Is.True);
            var range=basic.TargetModes[0].AbsoluteRange.MaxRange;var enemy=PolicyHero(0,false,2+range,4);
            var session=new CombatSession{Grid=new BattleGrid(14,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            actor.CurrentActionPoints=CombatActionCostCalculator.CalculateSkillCost(actor,basic)-1;actor.Character.ResourcePool.CurrentValue=0;
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(actor.Character.ResourcePool.CurrentValue,Is.GreaterThan(0));
            Assert.That(CombatTargetingService.IsPositionWithinRange(actor.Position,enemy.Position,basic.TargetModes[0].AbsoluteRange),Is.True);
            Assert.That(SkillData.IsPointBlank(actor.Position.X-enemy.Position.X,actor.Position.Y-enemy.Position.Y),Is.False);
        }
        [Test]
        public void PlayerPolicyEmptyScoutApproachesDaggerBeyondCurrentReach()
        {
            var actor=PolicyHero(1,true,2,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(14,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);actor.AvailableKnives=0;
            actor.CurrentActionPoints=ActionPointUnits.FromPoints(3);
            session.MapObjects.Add(new KnifeCombatMapObject("policy-knife",new GridPosition(8,4),actor.Character.CharacterId,1));
            Assert.That(engine.PlayerAction(actor),Is.True);
            Assert.That(Math.Abs(actor.Position.X-8)+Math.Abs(actor.Position.Y-4),Is.LessThan(6));
            Assert.That(actor.AvailableKnives,Is.Zero,"A distant dagger is not granted by planning.");
        }
        [Test]
        public void PlayerPolicyFocusAffectsExecutedPlanWithoutForbiddingOtherLegalTargets()
        {
            var actor=PolicyHero(2,true,2,4);var focus=PolicyHero(0,false,6,4);var other=PolicyHero(0,false,6,5);
            var session=new CombatSession{Grid=new BattleGrid(14,10),Units=new(){actor,focus,other}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);
            focus.Character.CurrentHealth=200;other.Character.CurrentHealth=250;
            Assert.That(engine.UpdatePlayerFocus(actor),Is.SameAs(focus));other.Character.CurrentHealth=175;
            IReadOnlyList<BattleUnit> chosen=null;engine.AuditChosenPlan=(a,s,t,score)=>chosen=t;
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(chosen,Does.Contain(focus));
            PolicyActivate(session,actor);focus.Position=new GridPosition(13,4);chosen=null;
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(chosen,Does.Contain(other),"An out-of-range focus is not a veto on legal attacks.");
        }
        [Test]
        public void PlayerPolicyEmptyScoutActuallyRecoversNearbyOwnedDagger()
        {
            var actor=PolicyHero(1,true,2,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(14,10),Units=new(){actor,enemy}};
            using var engine=new Engine(session,new Result());PolicyActivate(session,actor);actor.AvailableKnives=0;
            actor.CurrentActionPoints=ActionPointUnits.FromPoints(3);
            session.MapObjects.Add(new KnifeCombatMapObject("policy-near-knife",new GridPosition(4,4),actor.Character.CharacterId,1));
            Assert.That(engine.PlayerAction(actor),Is.True);Assert.That(actor.AvailableKnives,Is.EqualTo(1));
            Assert.That(session.MapObjects.OfType<KnifeCombatMapObject>(),Is.Empty);Assert.That(engine.Result.heroActions,Is.Zero);
        }
        [Test,Explicit("Legacy control verifies exact archived replay before comparing new default decisions.")]
        public void AuditPlayerPolicyLegacyReplay()
            =>RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,1,"legacy-proof",outputFolder:PlayerPolicyFolder,fullOnly:true,legacyPolicy:true);

        [TestCaseSource(nameof(PlayerPolicyCases)),Explicit("Paired L48 full clear on original twelve routes, gear and drops; new default policy only.")]
        public void AuditPlayerPolicyPairedExpeditions(float upgrade,string biome,int firstSeed)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,0,firstSeed+4,
                $"v2-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}-S{firstSeed}",firstSeed:firstSeed,outputFolder:PlayerPolicyFolder,fullOnly:true,previousRescuePolicy:true);
        static IEnumerable<TestCaseData> PlayerPolicyCases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var first in new[]{0,4,8})
                yield return new TestCaseData(upgrade,biome,first);
        }
    }
}
