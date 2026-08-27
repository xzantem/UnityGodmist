using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
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

            var skill = CreateSkill("skill.test.bleed");

            var statusSystem = new StatusEffectSystem();

            var applied = statusSystem.ApplyStatus(player,
                enemy,
                skill,
                effect,
                effectIndex: 0);

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
            Assert.That(runtimeEffect.Lifetime, Is.EqualTo(PassiveEffectLifetime.Timed));
            Assert.That(runtimeEffect.Sources[0].SourceType, Is.EqualTo(PassiveEffectSourceType.Skill));
            Assert.That(runtimeEffect.Sources[0].SourceId, Is.EqualTo("skill.test.bleed.effect.0"));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ApplyOnHitStatusBuff_ProcsConfiguredDamageOverTime()
        {
            var session = CreateSession(out var source, out var target);
            var effect = new SkillEffectData();
            SetPrivateField(effect, "onHitStatusId", StatusEffectIds.Bleed);
            SetPrivateField(effect, "onHitStatusChance", 1f);
            SetPrivateField(effect, "onHitStatusDurationTurns", 2);
            SetPrivateField(effect, "statusDamageMultiplier", .5f);
            SetPrivateField(effect, "durationTurns", 3);
            var skill = CreateSkill("skill.test.on_hit");
            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyOnHitStatusBuff(source, source, skill, effect, 0),
                Is.True);

            var onHit = source.Character.PassiveEffects[0]
                .GetPayload<OnHitStatusPayload>();
            Assert.That(onHit, Is.Not.Null);
            Assert.That(onHit.StatusDamagePerTurn, Is.EqualTo(5));

            Assert.That(
                statusSystem.ApplyActiveOnHitStatuses(source, target),
                Is.EqualTo(1));
            var bleed = target.Character.PassiveEffects.Find(
                value => value.EffectId == StatusEffectIds.Bleed);
            Assert.That(bleed, Is.Not.Null);
            Assert.That(bleed.TotalDamage, Is.EqualTo(10));
            Assert.That(bleed.GetPayload<DamageOverTimePayload>().DamagePerTurn, Is.EqualTo(5));
            Assert.That(bleed.RemainingDuration, Is.EqualTo(2));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ApplyActiveOnHitStatuses_UsesPersistedDamagePerTurnWhenMultiplierIsMissing()
        {
            var session = CreateSession(out var source, out var target);
            source.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "skill.test.on_hit_buff.0",
                Lifetime = PassiveEffectLifetime.CombatScoped,
                Payloads = new List<PassiveEffectPayloadRuntime>
                {
                    new OnHitStatusPayload
                    {
                        StatusId = StatusEffectIds.Bleed,
                        Chance = 1f,
                        DurationTurns = 2,
                        StatusDamageMultiplier = 0f,
                        StatusDamagePerTurn = 7
                    }
                }
            });

            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyActiveOnHitStatuses(source, target),
                Is.EqualTo(1));

            var bleed = target.Character.PassiveEffects.Find(
                value => value.EffectId == StatusEffectIds.Bleed);
            Assert.That(bleed, Is.Not.Null);
            Assert.That(bleed.TotalDamage, Is.EqualTo(14));
            Assert.That(bleed.GetPayload<DamageOverTimePayload>().DamagePerTurn, Is.EqualTo(7));
            Assert.That(bleed.RemainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void AuraRefresh_AppliesConfiguredDamageOverTime()
        {
            var session = CreateSession(out var source, out var target);
            var effect = new SkillEffectData();
            SetPrivateField(effect, "statusId", StatusEffectIds.Burn);
            SetPrivateField(effect, "baseChance", 1f);
            SetPrivateField(effect, "durationTurns", 2);
            SetPrivateField(effect, "statusDamageMultiplier", .5f);
            SetPrivateField(effect, "auraRadius", 1);
            SetPrivateField(effect, "auraTargetingType", TargetingType.Enemies);
            var skill = CreateSkill("skill.test.burning_aura");
            var statusSystem = new StatusEffectSystem();

            Assert.That(
                CombatAuraSystem.Register(source, skill, effect, 0),
                Is.True);
            CombatAuraSystem.Refresh(session, statusSystem);

            var burn = target.Character.PassiveEffects.Find(
                value => value.EffectId == StatusEffectIds.Burn);
            Assert.That(burn, Is.Not.Null);
            Assert.That(burn.TotalDamage, Is.EqualTo(10));
            Assert.That(burn.RemainingDuration, Is.EqualTo(2));

            Object.DestroyImmediate(skill);
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

            var skill = CreateSkill("skill.test.bleed");

            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyStatus(
                    player,
                    enemy,
                    skill,
                    firstEffect,
                    effectIndex: 0),
                Is.True);

            Assert.That(
                statusSystem.ApplyStatus(
                    player,
                    enemy,
                    skill,
                    secondEffect,
                    effectIndex: 0),
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

            Object.DestroyImmediate(skill);
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

            var skill = CreateSkill("skill.test.bleed");

            var statusSystem = new StatusEffectSystem();

            statusSystem.ApplyStatus(
                enemy,
                player,
                skill,
                firstEffect,
                effectIndex: 0);

            statusSystem.ApplyStatus(
                enemy,
                player,
                skill,
                secondEffect,
                effectIndex: 0);

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

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void CalculateEffectiveDamage_UsesOwnersStatusResistance()
        {
            CreateSession(out _, out var target);

            Assert.That(
                DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(
                    target,
                    StatusEffectIds.Bleed,
                    25),
                Is.EqualTo(50));

            target.Character.Resistances.BleedResistance = 0.5f;

            Assert.That(
                DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(
                    target,
                    StatusEffectIds.Bleed,
                    25),
                Is.EqualTo(25));
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
                    new List<PassiveEffectSourceRuntime>(),
                    1,
                    1,
                    10,
                    PassiveEffectLifetime.Timed,
                    PassiveEffectTickPhase.TurnStart));

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
                    new List<PassiveEffectSourceRuntime>(),
                    1,
                    1,
                    10,
                    PassiveEffectLifetime.Timed,
                    PassiveEffectTickPhase.TurnStart));

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

        [Test]
        public void ProcessTurnStart_PoisonUsesPoisonResistance()
        {
            var session = CreateSession(out var player, out var enemy);

            player.Character.Resistances.PoisonResistance = 0.5f;

            var effect = CreateDamageOverTimeEffect(
                StatusEffectIds.Poison,
                duration: 1,
                damagePerTurn: 20);

            var skill = CreateSkill("skill.test.poison");
            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyStatus(enemy, player, skill, effect, 0),
                Is.True);

            statusSystem.ProcessTurnStart(session, player);

            Assert.That(player.Character.CurrentHealth, Is.EqualTo(80));
            Assert.That(player.Character.PassiveEffects, Is.Empty);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ProcessTurnStart_BurnUsesFireResistance()
        {
            var session = CreateSession(out var player, out var enemy);

            var effect = CreateDamageOverTimeEffect(
                StatusEffectIds.Burn,
                duration: 1,
                damagePerTurn: 20);

            var skill = CreateSkill("skill.test.burn");
            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyStatus(enemy, player, skill, effect, 0),
                Is.True);

            player.Character.Resistances.FireResistance = 1f;

            statusSystem.ProcessTurnStart(session, player);

            Assert.That(player.Character.CurrentHealth, Is.EqualTo(90));
            Assert.That(player.Character.PassiveEffects, Is.Empty);

            Object.DestroyImmediate(skill);
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
                    new List<PassiveEffectSourceRuntime>(),
                    1,
                    1,
                    0,
                    PassiveEffectLifetime.Timed,
                    PassiveEffectTickPhase.TurnEnd));

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

        [Test]
        public void RemoveStatus_RemovesAppliedStatus()
        {
            CreateSession(out var player, out var enemy);
            
            var effect = CreateBleedEffect(
                duration: 3,
                damagePerTurn: 10);

            var skill = CreateSkill("skill.test.bleed");

            var statusSystem = new StatusEffectSystem();

            Assert.That(
                statusSystem.ApplyStatus(
                    player,
                    enemy,
                    skill,
                    effect,
                    effectIndex: 0),
                Is.True);

            var removed = statusSystem.RemoveStatus(enemy, "bleed");

            Assert.That(removed, Is.True);
            Assert.That(
                StatusEffectSystem.HasStatus(enemy, "bleed"),
                Is.False);
            Assert.That(enemy.Character.PassiveEffects, Is.Empty);

            Object.DestroyImmediate(skill);
        }

        private SkillEffectData CreateDamageOverTimeEffect(
            string statusId,
            int duration,
            int damagePerTurn)
        {
            var effect = new SkillEffectData();

            SetPrivateField(effect, "statusId", statusId);
            SetPrivateField(effect, "durationTurns", duration);
            SetPrivateField(
                effect,
                "statusDamageMultiplier",
                damagePerTurn / 10f);
            SetPrivateField(effect, "baseChance", 1f);

            return effect;
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

        private static SkillData CreateSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();

            SetAutoProperty(skill, "SkillId", skillId);

            return skill;
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

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

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
    }
}
