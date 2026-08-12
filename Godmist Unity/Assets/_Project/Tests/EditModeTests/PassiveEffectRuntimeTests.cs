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
