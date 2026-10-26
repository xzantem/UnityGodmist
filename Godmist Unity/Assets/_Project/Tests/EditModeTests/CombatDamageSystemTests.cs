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

        [TestCase(DamageType.Physical)]
        [TestCase(DamageType.Direct)]
        [TestCase(DamageType.Bleed)]
        [TestCase(DamageType.Poison)]
        [TestCase(DamageType.Burn)]
        public void DamageHitMissAndPeriodicDamageCannotGenerateBaseRage(DamageType type)
        {
            var session = CreateSession(out var source, out var target);
            source.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 0, 50, 0);
            var damage = new CombatDamageSystem();
            damage.ResolveAndApplyDamage(session, source, target, 10, type, wasHit: false);
            damage.ResolveAndApplyDamage(session, source, target, 10, type, wasHit: true);
            damage.ApplyDamage(session, source, target, 10, type);
            Assert.That(source.Character.ResourcePool.CurrentValue, Is.Zero,
                "Base Rage belongs to a committed offensive skill, never to its individual damage events.");
            Assert.That(source.Character.ResourcePool.HasGainedResourceSinceLastTurn, Is.False);
        }
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
        public void ApplyDamage_DebugInvulnerableTarget_DoesNotChangeHealth()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.DebugIsInvulnerable = true;

            var damageSystem = new CombatDamageSystem();

            var appliedDamage = damageSystem.ApplyDamage(
                session,
                null,
                enemy,
                40,
                DamageType.Physical);

            Assert.That(appliedDamage, Is.EqualTo(0));
            Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(100));
            Assert.That(
                enemy.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Alive));
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
        public void DamageFeedbackCarriesSourceAndImmutableImpactPositions()
        {
            var session = CreateSession(out var source, out var target);
            source.Position = new GridPosition(1, 1); target.Position = new GridPosition(2, 1);
            var bus = new CombatFeedbackBus(); CombatFeedbackEvent? damage = null;
            bus.Published += feedback => { if (feedback.Type == CombatFeedbackType.Damage) damage = feedback; };
            new CombatDamageSystem(feedbackBus: bus).ApplyDamage(session, source, target, 20, DamageType.Physical);
            Assert.That(damage.HasValue, Is.True);
            source.Position = new GridPosition(3,3); target.Position = new GridPosition(0,0);
            Assert.That(damage.Value.Source, Is.SameAs(source));
            Assert.That(damage.Value.SourcePosition, Is.EqualTo(new GridPosition(1,1)));
            Assert.That(damage.Value.Position, Is.EqualTo(new GridPosition(2,1)));
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
        public void ApplyDamage_RedirectsConfiguredPortionAndDamagesProtector()
        {
            var session = CreateSession(out var protectedUnit, out var protector);
            protector.IsPlayerSide = protectedUnit.IsPlayerSide;
            protector.Character.CharacterId = "protector";
            protectedUnit.DamageRedirectProtectorCharacterId = "protector";
            protectedUnit.DamageRedirectPercent = 0.4f;

            var damage = new CombatDamageSystem().ApplyDamage(
                session, null, protectedUnit, 50, DamageType.Physical);

            Assert.That(protectedUnit.Character.CurrentHealth, Is.EqualTo(70));
            Assert.That(protector.Character.CurrentHealth, Is.EqualTo(80));
            Assert.That(damage, Is.EqualTo(50));
        }

        [Test]
        public void UniqueBuff_ConsumesOnlyRequestedBuff()
        {
            var unit = CreateUnit(health: 100, isPlayerSide: true);
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                IsUniqueCombatBuff = true,
                UniqueBuffType = UniqueBuffType.GuaranteedCriticalHit,
                Stacks = 1
            });
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                IsUniqueCombatBuff = true,
                UniqueBuffType = UniqueBuffType.MaximumDamage,
                Stacks = 1
            });

            var consumed = CombatDamageSystem.TryConsumeUniqueBuff(
                unit, UniqueBuffType.GuaranteedCriticalHit);

            Assert.That(consumed, Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(
                unit, UniqueBuffType.GuaranteedCriticalHit), Is.False);
            Assert.That(CombatDamageSystem.HasUniqueBuff(
                unit, UniqueBuffType.MaximumDamage), Is.True);
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
                    new List<PassiveEffectSourceRuntime>(),
                    1,
                    4,
                    120,
                    PassiveEffectLifetime.Timed,
                    PassiveEffectTickPhase.TurnStart));

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
        public void ApplyDamage_EnemyDeath_TracksDefeatedEnemyBeforeRemoval()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.CharacterId = "encounter.enemy.01";
            enemy.Position = new GridPosition(2, 3);

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
                session.DefeatedEnemyInstanceIds,
                Does.Contain("encounter.enemy.01"));

            Assert.That(
                session.TryGetEnemyDeathPosition(
                    "encounter.enemy.01",
                    out var deathPosition),
                Is.True);
            Assert.That(deathPosition, Is.EqualTo(new GridPosition(2, 3)));

            Assert.That(removedUnit, Is.SameAs(enemy));
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
                    new List<PassiveEffectSourceRuntime>(),
                    1,
                    4,
                    120,
                    PassiveEffectLifetime.Timed,
                    PassiveEffectTickPhase.TurnStart));

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
