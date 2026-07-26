using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class ResourceRuleSystemTests
    {
        [Test]
        public void Mana_RegeneratesAtTurnStart()
        {
            var unit = CreateUnit(
                ResourceType.Mana,
                current: 4,
                max: 20,
                regeneration: 5);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(9));
        }

        [Test]
        public void Momentum_GainsTwoPerSpentActionPoint()
        {
            var unit = CreateUnit(
                ResourceType.Momentum,
                current: 0,
                max: 200);

            var system = new ResourceRuleSystem();

            system.ProcessUnitMoved(
                unit,
                spentActionPoints: 3);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(6));
        }

        [Test]
        public void Momentum_AboveOneHundred_GainsAtHalfEfficiency()
        {
            var unit = CreateUnit(
                ResourceType.Momentum,
                current: 100,
                max: 200);

            var system = new ResourceRuleSystem();

            system.ProcessUnitMoved(
                unit,
                spentActionPoints: 3);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(103));
        }

        [Test]
        public void Rage_GainsTenForSuccessfulDamage()
        {
            var attacker = CreateUnit(
                ResourceType.Rage,
                current: 0,
                max: 50);

            var target = CreateUnit(
                ResourceType.None,
                current: 0,
                max: 0);

            var session = CreateSession(
                attacker,
                target);

            var resourceSystem = new ResourceRuleSystem();

            var damageSystem = new CombatDamageSystem(
                resourceRuleSystem: resourceSystem);

            damageSystem.ApplyDamage(
                session,
                attacker,
                target,
                10,
                DamageType.Physical);

            Assert.That(
                attacker.Character.ResourcePool.CurrentValue,
                Is.EqualTo(10));
        }

        [Test]
        public void Rage_DecaysAfterTurnWithoutAttack()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 20,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(10));
        }

        [Test]
        public void Rage_SuccessfulHitPreventsNextTurnDecay()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 20,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessDamageDealt(unit);
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(30));
        }

        [Test]
        public void Rage_AtMaximumStillPreventsNextTurnDecay()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 50,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessDamageDealt(unit);
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(50));
        }

        private static BattleUnit CreateUnit(
            ResourceType resourceType,
            int current,
            int max,
            int regeneration = 0)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(
                        resourceType,
                        current,
                        max,
                        regeneration)
                },
                IsPlayerSide = true,
                Position = new GridPosition(0, 0)
            };
        }

        private static CombatSession CreateSession(
            BattleUnit first,
            BattleUnit second)
        {
            return new CombatSession
            {
                CombatId = "test.resource.rules",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit>
                {
                    first,
                    second
                }
            };
        }
    }
}