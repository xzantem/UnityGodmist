using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class PassiveEffectLifecycleSystemTests
    {
        [Test]
        public void GetEffectsForTick_ReturnsOnlyActiveEffectsForRequestedPhase()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    new PassiveEffectRuntime(
                        "bleed",
                        new List<string>(),
                        stacks: 1,
                        remainingDuration: 2,
                        totalDamage: 10,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnStart),
                    new PassiveEffectRuntime(
                        "stun",
                        new List<string>(),
                        stacks: 1,
                        remainingDuration: 2,
                        totalDamage: 0,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnEnd),
                    new PassiveEffectRuntime
                    {
                        EffectId = "passive.test",
                        IsTimed = false
                    }
                }
            };

            var system = new PassiveEffectLifecycleSystem();

            var effects = system.GetEffectsForTick(
                character,
                PassiveEffectTickPhase.TurnStart);

            Assert.That(effects, Has.Count.EqualTo(1));
            Assert.That(effects[0].EffectId, Is.EqualTo("bleed"));
        }

        [Test]
        public void AdvanceDurationAndRemoveIfExpired_RemovesEffectAtZero()
        {
            var effect = new PassiveEffectRuntime(
                "stun",
                new List<string>(),
                stacks: 1,
                remainingDuration: 2,
                totalDamage: 0,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnEnd);

            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime> { effect }
            };

            var system = new PassiveEffectLifecycleSystem();

            var expiredAfterFirstAdvance =
                system.AdvanceDurationAndRemoveIfExpired(character, effect);

            Assert.That(expiredAfterFirstAdvance, Is.False);
            Assert.That(effect.RemainingDuration, Is.EqualTo(1));
            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));

            var expiredAfterSecondAdvance =
                system.AdvanceDurationAndRemoveIfExpired(character, effect);

            Assert.That(expiredAfterSecondAdvance, Is.True);
            Assert.That(character.PassiveEffects, Is.Empty);
        }

        [Test]
        public void RemoveExpiredEffects_DoesNotRemovePermanentPassive()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    new PassiveEffectRuntime
                    {
                        EffectId = "passive.permanent",
                        IsTimed = false
                    },
                    new PassiveEffectRuntime(
                        "expired.status",
                        new List<string>(),
                        stacks: 1,
                        remainingDuration: 0,
                        totalDamage: 0,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnEnd)
                }
            };

            var system = new PassiveEffectLifecycleSystem();

            var removedCount = system.RemoveExpiredEffects(character);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(
                character.PassiveEffects[0].EffectId,
                Is.EqualTo("passive.permanent"));
        }
    }
}