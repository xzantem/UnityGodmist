using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class StatusEffectSystemTests
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
        public void ApplyBleed_CreatesRuntimeEffect()
        {
            var session = CreateSession(
                out var player,
                out var enemy);

            var effect = CreateBleedEffect(
                duration: 3,
                damagePerTurn: 10);

            var statusSystem = new StatusEffectSystem();

            var applied = statusSystem.ApplyStatus(player,
                enemy,
                effect);

            Assert.That(applied, Is.True);
            Assert.That(
                enemy.Character.PassiveEffects,
                Has.Count.EqualTo(1));

            var runtimeEffect =
                enemy.Character.PassiveEffects[0];

            Assert.That(runtimeEffect.EffectId, Is.EqualTo("bleed"));
            Assert.That(
                runtimeEffect.RemainingDuration,
                Is.EqualTo(3));
            Assert.That(
                runtimeEffect.TotalDamage,
                Is.EqualTo(30));
            Assert.That(runtimeEffect.Stacks, Is.EqualTo(1));
        }

        [Test]
        public void ApplyBleed_RefreshesDurationAndAddsTotalDamage()
        {
            var session = CreateSession(
                out var player,
                out var enemy);

            var firstEffect = CreateBleedEffect(
                duration: 2,
                damagePerTurn: 34);

            var secondEffect = CreateBleedEffect(
                duration: 5,
                damagePerTurn: 26);

            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyStatus(
                    player,
                    enemy,
                    firstEffect),
                Is.True);

            Assert.That(
                statusSystem.ApplyStatus(
                    player,
                    enemy,
                    secondEffect),
                Is.True);

            Assert.That(
                enemy.Character.PassiveEffects,
                Has.Count.EqualTo(1));

            var runtimeEffect =
                enemy.Character.PassiveEffects[0];

            Assert.That(
                runtimeEffect.RemainingDuration,
                Is.EqualTo(5));

            Assert.That(
                runtimeEffect.TotalDamage,
                Is.EqualTo(198));

            Assert.That(runtimeEffect.Stacks, Is.EqualTo(1));
        }

        [Test]
        public void ProcessTurnStart_CombinedBleedDealsTotalDamageOverFiveTurns()
        {
            var session = CreateSession(
                out var player,
                out var enemy);

            player.Character.CurrentHealth = 1000;
            player.Character.Resistances.BleedResistance = 0.5f;

            var firstEffect = CreateBleedEffect(
                duration: 2,
                damagePerTurn: 34);

            var secondEffect = CreateBleedEffect(
                duration: 5,
                damagePerTurn: 26);

            var statusSystem = new StatusEffectSystem();

            statusSystem.ApplyStatus(
                enemy,
                player,
                firstEffect);

            statusSystem.ApplyStatus(
                enemy,
                player,
                secondEffect);

            for (var turn = 0; turn < 5; turn++)
            {
                statusSystem.ProcessTurnStart(
                    session,
                    player);
            }

            Assert.That(
                player.Character.CurrentHealth,
                Is.EqualTo(802));

            Assert.That(
                player.Character.PassiveEffects,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void ProcessTurnStart_DoesNotProcessDownedUnit()
        {
            var session = CreateSession(
                out var player,
                out _);

            player.Character.LifeState =
                CharacterLifeState.Downed;

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
                Is.EqualTo(100));

            Assert.That(
                player.Character.PassiveEffects[0]
                    .RemainingDuration,
                Is.EqualTo(4));
        }

        [Test]
        public void LethalBleed_SetsPlayerToDowned()
        {
            var session = CreateSession(
                out var player,
                out _);

            player.Character.CurrentHealth = 10;
            player.Character.Resistances.BleedResistance = 0.5f;

            player.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    "bleed",
                    new List<string>(),
                    1,
                    1,
                    10));

            var statusSystem = new StatusEffectSystem();

            statusSystem.ProcessTurnStart(
                session,
                player);

            Assert.That(
                player.Character.CurrentHealth,
                Is.EqualTo(0));

            Assert.That(
                player.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void LethalBleed_SetsEnemyToDead()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.CurrentHealth = 10;
            enemy.Character.Resistances.BleedResistance = 0.5f;

            enemy.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    "bleed",
                    new List<string>(),
                    1,
                    1,
                    10));

            var statusSystem = new StatusEffectSystem();

            statusSystem.ProcessTurnStart(
                session,
                enemy);

            Assert.That(
                enemy.Character.CurrentHealth,
                Is.EqualTo(0));

            Assert.That(
                enemy.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Dead));
        }

        private SkillEffectData CreateBleedEffect(
            int duration,
            int damagePerTurn)
        {
            var effect = new SkillEffectData();

            SetPrivateField(
                effect,
                "statusId",
                "bleed");

            SetPrivateField(
                effect,
                "durationTurns",
                duration);

            SetPrivateField(
                effect,
                "statusDamageMultiplier",
                damagePerTurn / 10f);

            SetPrivateField(
                effect,
                "baseChance",
                1f);

            return effect;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance
                | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola {fieldName}.");

            field.SetValue(target, value);
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

            return new CombatSession
            {
                CombatId = "test.status",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit>
                {
                    player,
                    enemy
                }
            };
        }

        private BattleUnit CreateUnit(
            int health,
            bool isPlayerSide)
        {
            var definition =
                ScriptableObject.CreateInstance
                <CharacterDefinition>();

            createdDefinitions.Add(definition);

            var character = new CharacterInstance
            {
                Definition = definition,
                CurrentHealth = health,
                LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats
                {
                    MinAttack = new Stat(10f),
                    MaxAttack = new Stat(10f)
                },
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
        [Test]
        public void ProcessTurnEnd_StunExpiresAfterAffectedUnitTurn()
        {
            var session = CreateSession(
                out _,
                out var enemy);

            enemy.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    StatusEffectSystem.StunId,
                    new List<string>(),
                    1,
                    1,
                    0));

            var statusSystem = new StatusEffectSystem();

            statusSystem.ProcessTurnStart(session, enemy);

            Assert.That(
                StatusEffectSystem.IsStunned(enemy),
                Is.True);

            statusSystem.ProcessTurnEnd(enemy);

            Assert.That(
                StatusEffectSystem.IsStunned(enemy),
                Is.False);
        }
    }
}