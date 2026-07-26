using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatDamageSystemTests
    {
        private readonly List<CharacterDefinition> createdDefinitions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var definition in createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            createdDefinitions.Clear();
        }

        [Test]
        public void ApplyDamage_UsesCurrentHealth()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.CurrentExperience = 0;

            var damageSystem = new CombatDamageSystem();

            var damage = damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                20,
                DamageType.Physical);

            Assert.That(damage, Is.EqualTo(20));
            Assert.That(
                enemy.Character.CurrentHealth,
                Is.EqualTo(80));
        }

        [Test]
        public void ApplyDamage_ConsumesShieldBeforeHealth()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.ShieldPool.AddLayer(10, "test");

            var damageSystem = new CombatDamageSystem();

            damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                25,
                DamageType.Physical);

            Assert.That(
                enemy.Character.ShieldPool.TotalShield,
                Is.EqualTo(0));

            Assert.That(
                enemy.Character.CurrentHealth,
                Is.EqualTo(85));
        }

        [Test]
        public void ApplyDamage_BleedIsAbsorbedByShield()
        {
            var session = CreateSession(out _, out var enemy);
            enemy.Character.ShieldPool.AddLayer(10, "test");

            var damageSystem = new CombatDamageSystem();

            damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                25,
                DamageType.Bleed);

            Assert.That(enemy.Character.ShieldPool.TotalShield, Is.EqualTo(0));
            Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(85));
        }

        [Test]
        public void ApplyDamage_PlayerAtZeroHealth_BecomesDowned()
        {
            var session = CreateSession(
                out var player,
                out _);

            var damageSystem = new CombatDamageSystem();

            damageSystem.ApplyDamage(
                session,
                null,
                player,
                100,
                DamageType.Physical);

            Assert.That(
                player.Character.CurrentHealth,
                Is.EqualTo(0));

            Assert.That(
                player.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Downed));

            Assert.That(
                player.DownedTurnsRemaining,
                Is.EqualTo(BattleUnit.DownedTurnLimit));
        }

        [Test]
        public void ApplyDamage_EnemyAtZeroHealth_BecomesDead()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            var damageSystem = new CombatDamageSystem();

            damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                100,
                DamageType.Physical);

            Assert.That(
                enemy.Character.CurrentHealth,
                Is.EqualTo(0));

            Assert.That(
                enemy.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Dead));
        }

        [Test]
        public void ApplyDamage_EnemyDeath_InvokesRemovalCallback()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            BattleUnit removedUnit = null;

            var damageSystem = new CombatDamageSystem(
                removeUnitFromCombat: unit => removedUnit = unit);

            damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                100,
                DamageType.Physical);

            Assert.That(
                removedUnit,
                Is.SameAs(enemy));
        }

        [Test]
        public void ApplyDamage_LastEnemyDead_EndsCombatWithVictory()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            var outcomeSystem = new CombatOutcomeSystem();

            var damageSystem = new CombatDamageSystem(
                evaluateOutcome:
                    outcomeSystem.EvaluateCombatOutcome);

            damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                100,
                DamageType.Physical);

            Assert.That(
                session.Outcome,
                Is.EqualTo(CombatOutcomeState.Victory));
        }

        [Test]
        public void BleedAtFiftyPercentResistance_DealsExpectedDamage()
        {
            var session = CreateSession(
                out var player,
                out _);

            player.Character.CurrentHealth = 200;
            player.Character.Resistances.BleedResistance = 0.5f;

            player.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    "bleed",
                    new List<string>(),
                    1,
                    4,
                    120));

            var statusSystem = new StatusEffectSystem();

            statusSystem.ProcessTurnStart(
                session,
                player);

            Assert.That(
                player.Character.CurrentHealth,
                Is.EqualTo(170));

            Assert.That(
                player.Character.PassiveEffects[0].TotalDamage,
                Is.EqualTo(90));

            Assert.That(
                player.Character.PassiveEffects[0].RemainingDuration,
                Is.EqualTo(3));
        }

        [Test]
        public void BleedAtZeroResistance_DoublesDamage()
        {
            var session = CreateSession(
                out var player,
                out _);

            player.Character.CurrentHealth = 200;
            player.Character.Resistances.BleedResistance = 0f;

            player.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    "bleed",
                    new List<string>(),
                    1,
                    4,
                    120));

            var statusSystem = new StatusEffectSystem();

            statusSystem.ProcessTurnStart(
                session,
                player);

            Assert.That(
                player.Character.CurrentHealth,
                Is.EqualTo(140));
        }

        [Test]
        public void ApplyDamage_DownedTarget_AcceleratesDeathWithoutHealthDamage()
        {
            var session = CreateSession(
                out var player,
                out _);

            player.Character.CurrentHealth = 0;
            player.Character.LifeState =
                CharacterLifeState.Downed;
            player.DownedTurnsRemaining = 3;

            var damageSystem = new CombatDamageSystem();

            var damage = damageSystem.ApplyDamage(
                session,
                null,
                player,
                20,
                DamageType.Physical);

            Assert.That(damage, Is.EqualTo(0));
            Assert.That(player.Character.CurrentHealth, Is.EqualTo(0));
            Assert.That(player.DownedTurnsRemaining, Is.EqualTo(2));
            Assert.That(
                player.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Downed));
        }   

        private CombatSession CreateSession(
            out BattleUnit player,
            out BattleUnit enemy)
        {
            player = CreateUnit(
                health: 100,
                isPlayerSide: true);

            enemy = CreateUnit(
                health: 100,
                isPlayerSide: false);

            var session = new CombatSession
            {
                CombatId = "test.combat",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit>
                {
                    player,
                    enemy
                }
            };

            return session;
        }

        private BattleUnit CreateUnit(
            int health,
            bool isPlayerSide)
        {
            var definition =
                ScriptableObject.CreateInstance<CharacterDefinition>();

            createdDefinitions.Add(definition);

            var character = new CharacterInstance
            {
                Definition = definition,
                CurrentHealth = health,
                LifeState = CharacterLifeState.Alive,
                Resistances = new ResistanceSet(),
                ShieldPool = new ShieldPool(),
                PassiveEffects = new List<PassiveEffectRuntime>()
            };

            return new BattleUnit
            {
                Character = character,
                IsPlayerSide = isPlayerSide
            };
        }
    }
}
