using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class PassiveEffectRuntimeTests
    {
        [Test]
        public void RecoveryTicks_PreserveFractionalRemainderThroughJsonSavePayload()
        {
            var payload = new PeriodicRecoveryPayload { HealthPercent = .025f, ResourcePercent = .025f };
            var unit = new BattleUnit { Character = new CharacterInstance
            {
                CurrentHealth = 1, LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats { MaxHealth = new Stat(100) },
                ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 0)
            }};
            var effect = new PassiveEffectRuntime();
            effect.SetPayload(payload);
            var processor = new PeriodicRecoveryPassiveEffectProcessor(new CombatRecoverySystem());
            processor.ProcessTurn(null, unit, effect, out _);
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(4));
            var json = UnityEngine.JsonUtility.ToJson(payload);
            var restored = UnityEngine.JsonUtility.FromJson<PeriodicRecoveryPayload>(json);
            Assert.That(restored.HealthRoundingCarry, Is.EqualTo(-.5f));
            Assert.That(restored.ResourceRoundingCarry, Is.EqualTo(-.5f));
            payload.HealthRoundingCarry = restored.HealthRoundingCarry;
            payload.ResourceRoundingCarry = restored.ResourceRoundingCarry;
            for (var turn = 1; turn < 10; turn++) processor.ProcessTurn(null, unit, effect, out _);
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(26));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(25));
        }

        [Test]
        public void Constructor_WithTimedLifetime_KeepsDuration()
        {
            var effect = new PassiveEffectRuntime(
                "bleed",
                new List<PassiveEffectSourceRuntime>(),
                stacks: 1,
                remainingDuration: 3,
                totalDamage: 30,
                lifetime: PassiveEffectLifetime.Timed,
                tickPhase: PassiveEffectTickPhase.TurnStart);

            Assert.That(effect.Lifetime, Is.EqualTo(PassiveEffectLifetime.Timed));
            Assert.That(effect.RemainingDuration, Is.EqualTo(3));
            Assert.That(
                effect.GetPayload<DamageOverTimePayload>()?.RemainingTotalDamage,
                Is.EqualTo(30));
        }

        [Test]
        public void SetPayload_ReplacesOnlyTheMatchingPayloadType()
        {
            var effect = new PassiveEffectRuntime();
            effect.SetPayload(new DamageOverTimePayload
            {
                RemainingTotalDamage = 20
            });
            effect.SetPayload(new SleepHealingPayload
            {
                MaxHealthPercent = 0.1f
            });
            effect.SetPayload(new DamageOverTimePayload
            {
                RemainingTotalDamage = 40
            });

            Assert.That(effect.Payloads, Has.Count.EqualTo(2));
            Assert.That(
                effect.GetPayload<DamageOverTimePayload>()?.RemainingTotalDamage,
                Is.EqualTo(40));
            Assert.That(
                effect.GetPayload<SleepHealingPayload>()?.MaxHealthPercent,
                Is.EqualTo(0.1f));
        }

        [Test]
        public void DefaultPassiveEffect_IsPermanent()
        {
            var effect = new PassiveEffectRuntime
            {
                EffectId = "passive.test",
                StatModifiers = new List<StatModifier>()
            };

            Assert.That(effect.Lifetime, Is.EqualTo(PassiveEffectLifetime.Persistent));
        }

        [Test]
        public void ProcessTurnStart_DoesNotRemovePermanentPassiveEffect()
        {
            var character = new CharacterInstance
            {
                CharacterId = "jerzy",
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    new PassiveEffectRuntime
                    {
                        EffectId = "passive.test",
                        Lifetime = PassiveEffectLifetime.Persistent
                    }
                }
            };

            var unit = new BattleUnit
            {
                Character = character
            };

            var session = new CombatSession();
            session.Units.Add(unit);

            var system = new StatusEffectSystem();

            system.ProcessTurnStart(session, unit);
            system.ProcessTurnEnd(unit);

            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(character.PassiveEffects[0].EffectId, Is.EqualTo("passive.test"));
        }
    }
}
