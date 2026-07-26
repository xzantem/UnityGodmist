using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatDamageResolutionTests
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
        public void CalculateHitChance_EqualAccuracyAndDodge_ReturnsFiftyPercent()
        {
            var chance = CombatDamageSystem.CalculateHitChance(
                weaponAccuracy: 100f,
                skillAccuracy: 100f,
                dodge: 100f);

            Assert.That(chance, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void CalculateHitChance_ZeroDodge_ReturnsOneHundredPercent()
        {
            var chance = CombatDamageSystem.CalculateHitChance(
                weaponAccuracy: 100f,
                skillAccuracy: 100f,
                dodge: 0f);

            Assert.That(chance, Is.EqualTo(1f));
        }

        [Test]
        public void ResolveAndApplyDamage_PhysicalDamage_UsesPhysicalDefense()
        {
            var session = CreateSession(
                out var source,
                out var target);

            source.Character.WeaponAccuracy = 100f;
            source.Character.Stats.CritChance = new Stat(0f);

            target.Character.Stats.PhysicalDefense = new Stat(20f);

            var damageSystem = new CombatDamageSystem();

            var result = damageSystem.ResolveAndApplyDamage(
                session,
                source,
                target,
                attackValue: 20,
                damageType: DamageType.Physical,
                skillAccuracy: 100f);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.IsCritical, Is.False);
            Assert.That(result.HealthDamage, Is.EqualTo(10));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void ResolveAndApplyDamage_PhysicalDamageWithZeroAccuracy_Misses()
        {
            var session = CreateSession(
                out var source,
                out var target);

            source.Character.WeaponAccuracy = 0f;

            var damageSystem = new CombatDamageSystem();

            var result = damageSystem.ResolveAndApplyDamage(
                session,
                source,
                target,
                attackValue: 20,
                damageType: DamageType.Physical,
                skillAccuracy: 0f);

            Assert.That(result.Hit, Is.False);
            Assert.That(result.IsCritical, Is.False);
            Assert.That(result.HealthDamage, Is.EqualTo(0));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void ResolveAndApplyDamage_MagicalDamage_AlwaysHitsAndUsesMagicalDefense()
        {
            var session = CreateSession(
                out var source,
                out var target);

            source.Character.WeaponAccuracy = 0f;
            source.Character.Stats.CritChance = new Stat(1f);

            target.Character.Stats.MagicalDefense = new Stat(20f);

            var damageSystem = new CombatDamageSystem();

            var result = damageSystem.ResolveAndApplyDamage(
                session,
                source,
                target,
                attackValue: 20,
                damageType: DamageType.Magical,
                skillAccuracy: 0f);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.IsCritical, Is.False);
            Assert.That(result.HealthDamage, Is.EqualTo(10));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void ResolveAndApplyDamage_InevitableDamage_CanCritAndIgnoresDefense()
        {
            var session = CreateSession(
                out var source,
                out var target);

            source.Character.Stats.CritChance = new Stat(1f);
            source.Character.Stats.CritDamage = new Stat(1.5f);

            target.Character.Stats.PhysicalDefense = new Stat(999f);
            target.Character.Stats.MagicalDefense = new Stat(999f);

            var damageSystem = new CombatDamageSystem();

            var result = damageSystem.ResolveAndApplyDamage(
                session,
                source,
                target,
                attackValue: 20,
                damageType: DamageType.Inevitable,
                skillAccuracy: 0f);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.IsCritical, Is.True);
            Assert.That(result.HealthDamage, Is.EqualTo(30));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void ResolveAndApplyDamage_DirectDamage_CannotCritAndIgnoresDefense()
        {
            var session = CreateSession(
                out var source,
                out var target);

            source.Character.Stats.CritChance = new Stat(1f);
            source.Character.Stats.CritDamage = new Stat(1.5f);

            target.Character.Stats.PhysicalDefense = new Stat(999f);
            target.Character.Stats.MagicalDefense = new Stat(999f);

            var damageSystem = new CombatDamageSystem();

            var result = damageSystem.ResolveAndApplyDamage(
                session,
                source,
                target,
                attackValue: 20,
                damageType: DamageType.Direct,
                skillAccuracy: 0f);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.IsCritical, Is.False);
            Assert.That(result.HealthDamage, Is.EqualTo(20));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(80));
        }

        private CombatSession CreateSession(
            out BattleUnit source,
            out BattleUnit target)
        {
            source = CreateUnit(isPlayerSide: true);
            target = CreateUnit(isPlayerSide: false);

            return new CombatSession
            {
                CombatId = "test.damage-resolution",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit>
                {
                    source,
                    target
                }
            };
        }

        private BattleUnit CreateUnit(bool isPlayerSide)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdDefinitions.Add(definition);

            var character = new CharacterInstance
            {
                Definition = definition,
                CurrentHealth = 100,
                WeaponAccuracy = 100f,
                LifeState = CharacterLifeState.Alive,
                Resistances = new ResistanceSet(),
                ShieldPool = new ShieldPool(),
                PassiveEffects = new List<PassiveEffectRuntime>(),
                Stats = new CharacterStats
                {
                    MinAttack = new Stat(10f),
                    MaxAttack = new Stat(20f),
                    PhysicalDefense = new Stat(0f),
                    MagicalDefense = new Stat(0f),
                    Dodge = new Stat(0f),
                    CritChance = new Stat(0f),
                    CritDamage = new Stat(1.5f)
                }
            };

            return new BattleUnit
            {
                Character = character,
                IsPlayerSide = isPlayerSide
            };
        }
    }
}