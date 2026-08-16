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
    public class CombatVariableSystemTests
    {
        [Test]
        public void GetMultiplier_AdditiveAndMultiplicativeModifiers_UsesExpectedPipeline()
        {
            var character = new CharacterInstance
            {
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    CreateEffect(
                        GameplayVariableType.DamageDealtPercent,
                        StatModifierStacking.Additive,
                        0.25f),

                    CreateEffect(
                        GameplayVariableType.DamageDealtPercent,
                        StatModifierStacking.Multiplicative,
                        0.2f)
                }
            };

            var system = new CombatVariableSystem();

            var multiplier = system.GetMultiplier(
                character,
                GameplayVariableType.DamageDealtPercent);

            Assert.That(multiplier, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void ApplyDamageModifiers_UsesDealtAndReceivedModifiers()
        {
            var source = new BattleUnit
            {
                Character = new CharacterInstance
                {
                    PassiveEffects = new List<PassiveEffectRuntime>
                    {
                        CreateEffect(
                            GameplayVariableType.DamageDealtPercent,
                            StatModifierStacking.Additive,
                            0.25f),

                        CreateEffect(
                            GameplayVariableType.DamageDealtPercent,
                            StatModifierStacking.Multiplicative,
                            0.2f)
                    }
                }
            };

            var target = new BattleUnit
            {
                Character = new CharacterInstance
                {
                    PassiveEffects = new List<PassiveEffectRuntime>
                    {
                        CreateEffect(
                            GameplayVariableType.DamageReceivedPercent,
                            StatModifierStacking.Additive,
                            0.1f)
                    }
                }
            };

            var system = new CombatVariableSystem();

            var damage = system.ApplyDamageModifiers(
                source,
                target,
                requestedDamage: 100);

            Assert.That(damage, Is.EqualTo(165));
        }

        [Test]
        public void GetMultiplier_NoModifiers_ReturnsOne()
        {
            var system = new CombatVariableSystem();

            var multiplier = system.GetMultiplier(
                new CharacterInstance(),
                GameplayVariableType.DamageReceivedPercent);

            Assert.That(multiplier, Is.EqualTo(1f));
        }

        private static PassiveEffectRuntime CreateEffect(
            GameplayVariableType variableType,
            StatModifierStacking stacking,
            float value)
        {
            return new PassiveEffectRuntime(
                "test.effect",
                new List<PassiveEffectSourceRuntime>(),
                stacks: 1,
                remainingDuration: 0,
                totalDamage: 0,
                lifetime: PassiveEffectLifetime.Persistent)
            {
                VariableModifiers = new List<GameplayVariableModifier>
                {
                    new()
                    {
                        VariableType = variableType,
                        Stacking = stacking,
                        Value = value
                    }
                }
            };
        }
    }
}
