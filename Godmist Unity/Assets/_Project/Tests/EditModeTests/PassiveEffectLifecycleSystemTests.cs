using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
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
        public void ProcessTurnStart_BleedProcessor_DealsDamageAndExpires()
        {
            var effect = new PassiveEffectRuntime(
                "bleed",
                new List<string>(),
                stacks: 1,
                remainingDuration: 3,
                totalDamage: 30,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnStart);

            var character = new CharacterInstance
            {
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                Resistances = new ResistanceSet
                {
                    BleedResistance = 0.5f
                },
                PassiveEffects = new List<PassiveEffectRuntime> { effect }
            };

            var unit = new BattleUnit
            {
                Character = character
            };

            var session = new CombatSession();
            session.Units.Add(unit);

            var lifecycleSystem = new PassiveEffectLifecycleSystem(
                turnProcessors: new IPassiveEffectTurnProcessor[]
                {
                    new BleedPassiveEffectProcessor(new CombatDamageSystem())
                });

            lifecycleSystem.ProcessTurnStart(session, unit);

            Assert.That(character.CurrentHealth, Is.EqualTo(90));
            Assert.That(effect.RemainingDuration, Is.EqualTo(2));
            Assert.That(effect.TotalDamage, Is.EqualTo(20));

            lifecycleSystem.ProcessTurnStart(session, unit);
            lifecycleSystem.ProcessTurnStart(session, unit);

            Assert.That(character.CurrentHealth, Is.EqualTo(70));
            Assert.That(character.PassiveEffects, Is.Empty);
        }

        [Test]
        public void TryApplyTimedEffect_BleedRefreshesDurationAndAddsDamage()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>()
            };

            var system = new PassiveEffectLifecycleSystem();

            var firstEffect = new PassiveEffectRuntime(
                "bleed",
                new List<string> { "jerzy" },
                stacks: 1,
                remainingDuration: 2,
                totalDamage: 68,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnStart);

            var secondEffect = new PassiveEffectRuntime(
                "bleed",
                new List<string> { "wladimir" },
                stacks: 1,
                remainingDuration: 5,
                totalDamage: 130,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnStart);

            Assert.That(
                system.TryApplyTimedEffect(
                    character,
                    firstEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage,
                    out _),
                Is.True);

            Assert.That(
                system.TryApplyTimedEffect(
                    character,
                    secondEffect,
                    PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage,
                    out var combinedEffect),
                Is.True);

            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(combinedEffect.RemainingDuration, Is.EqualTo(5));
            Assert.That(combinedEffect.TotalDamage, Is.EqualTo(198));
            Assert.That(combinedEffect.Stacks, Is.EqualTo(1));
            Assert.That(
                combinedEffect.SourceEntityIds,
                Is.EquivalentTo(new[] { "jerzy", "wladimir" }));
        }

        [Test]
        public void TryApplyTimedEffect_NormalStatusKeepsHighestValues()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>()
            };

            var system = new PassiveEffectLifecycleSystem();

            var firstEffect = new PassiveEffectRuntime(
                "stun",
                new List<string> { "jerzy" },
                stacks: 1,
                remainingDuration: 1,
                totalDamage: 0,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnEnd);

            var secondEffect = new PassiveEffectRuntime(
                "stun",
                new List<string> { "wladimir" },
                stacks: 3,
                remainingDuration: 2,
                totalDamage: 10,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnEnd);

            system.TryApplyTimedEffect(
                character,
                firstEffect,
                PassiveEffectStackingMode.MaxDurationAndValues,
                out _);

            system.TryApplyTimedEffect(
                character,
                secondEffect,
                PassiveEffectStackingMode.MaxDurationAndValues,
                out var combinedEffect);

            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(combinedEffect.RemainingDuration, Is.EqualTo(2));
            Assert.That(combinedEffect.TotalDamage, Is.EqualTo(10));
            Assert.That(combinedEffect.Stacks, Is.EqualTo(3));
            Assert.That(
                combinedEffect.SourceEntityIds,
                Is.EquivalentTo(new[] { "jerzy", "wladimir" }));
        }

        [Test]
        public void RemoveEffect_RemovesOnlyRequestedEffect()
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
                        totalDamage: 20,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnStart),
                    new PassiveEffectRuntime(
                        "stun",
                        new List<string>(),
                        stacks: 1,
                        remainingDuration: 1,
                        totalDamage: 0,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnEnd)
                }
            };

            var system = new PassiveEffectLifecycleSystem();

            var removed = system.RemoveEffect(character, "bleed");

            Assert.That(removed, Is.True);
            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(
                character.PassiveEffects[0].EffectId,
                Is.EqualTo("stun"));
        }

        [Test]
        public void HasActiveEffect_IgnoresExpiredTimedEffect()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    new PassiveEffectRuntime(
                        "stun",
                        new List<string>(),
                        stacks: 1,
                        remainingDuration: 0,
                        totalDamage: 0,
                        isTimed: true,
                        tickPhase: PassiveEffectTickPhase.TurnEnd)
                }
            };

            Assert.That(
                PassiveEffectLifecycleSystem.HasActiveEffect(
                    character,
                    "stun"),
                Is.False);
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

        [Test]
        public void TryApplyTimedEffect_HardCc_ReplacesOnlyDuration()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>()
            };

            var system = new PassiveEffectLifecycleSystem();

            var firstEffect = new PassiveEffectRuntime(
                "stun",
                new List<string> { "jerzy" },
                stacks: 1,
                remainingDuration: 2,
                totalDamage: 0,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnEnd);

            var secondEffect = new PassiveEffectRuntime(
                "stun",
                new List<string> { "wladimir" },
                stacks: 3,
                remainingDuration: 1,
                totalDamage: 20,
                isTimed: true,
                tickPhase: PassiveEffectTickPhase.TurnEnd);

            system.TryApplyTimedEffect(
                character,
                firstEffect,
                PassiveEffectStackingMode.ReplaceDuration,
                out _);

            system.TryApplyTimedEffect(
                character,
                secondEffect,
                PassiveEffectStackingMode.ReplaceDuration,
                out var combinedEffect);

            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(combinedEffect.RemainingDuration, Is.EqualTo(1));
            Assert.That(combinedEffect.Stacks, Is.EqualTo(1));
            Assert.That(combinedEffect.TotalDamage, Is.EqualTo(0));
            Assert.That(
                combinedEffect.SourceEntityIds,
                Is.EquivalentTo(new[] { "jerzy", "wladimir" }));
        }
    }
}