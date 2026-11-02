using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class PassiveEffectPayloadCodecTests
    {
        private static IEnumerable<PassiveEffectPayloadType> Types() => Enum.GetValues(typeof(PassiveEffectPayloadType)).Cast<PassiveEffectPayloadType>();

        private static PassiveEffectPayloadRuntime Sample(PassiveEffectPayloadType type)
        {
            var value = (PassiveEffectPayloadRuntime)Activator.CreateInstance(PassiveEffectPayloadCodec.RuntimeType(type));
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(int)) field.SetValue(value, 4);
                else if (field.FieldType == typeof(float)) field.SetValue(value, .125f);
                else if (field.FieldType == typeof(string)) field.SetValue(value, "sample." + field.Name);
                else if (field.FieldType.IsEnum) field.SetValue(value, Enum.GetValues(field.FieldType).GetValue(1));
            }
            if (value is StatModifiersPayload stats) stats.Modifiers.Add(new StatModifier { StatType = CharacterStatType.Speed, Value = 13 });
            if (value is VariableModifiersPayload variables) variables.Modifiers.Add(new GameplayVariableModifier { VariableType = GameplayVariableType.DamageDealtPercent, ReferenceId = "reference", Value = .17f });
            if (value is CounterattackPayload counterattack)
            {
                counterattack.Range = new SkillRangeData(RangeShape.Square, 1, 3);
                var effect = new SkillEffectData(); counterattack.Effects.Add(effect);
                typeof(SkillEffectData).GetField("conditions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect,
                    new List<SkillEffectConditionData> { new() });
                typeof(SkillEffectData).GetField("counterattackEffects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect,
                    new List<SkillEffectData> { new() });
            }
            return value;
        }

        [Test]
        public void EveryConcreteRuntimePayloadAndEnumHasExactlyOneContract()
        {
            Assert.That(PassiveEffectPayloadCodec.RegisteredTypes, Is.EquivalentTo(Types()));
            var concrete = typeof(PassiveEffectPayloadRuntime).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(PassiveEffectPayloadRuntime)) && type != typeof(UnknownPayload));
            Assert.That(Types().Select(PassiveEffectPayloadCodec.RuntimeType), Is.EquivalentTo(concrete));
        }

        [TestCaseSource(nameof(Types))]
        public void LifecycleClonePreservesEveryFieldWithoutSharingMutableState(PassiveEffectPayloadType type)
        {
            var source = Sample(type); var clone = PassiveEffectPayloadCodec.Clone(source);
            Assert.That(clone, Is.Not.SameAs(source)); Assert.That(JsonUtility.ToJson(clone), Is.EqualTo(JsonUtility.ToJson(source)));
            if (source is StatModifiersPayload stats)
            {
                var copied = (StatModifiersPayload)clone;
                Assert.That(copied.Modifiers, Is.Not.SameAs(stats.Modifiers)); Assert.That(copied.Modifiers[0], Is.Not.SameAs(stats.Modifiers[0]));
                copied.Modifiers[0].Value = 99; Assert.That(stats.Modifiers[0].Value, Is.EqualTo(13));
            }
            if (source is VariableModifiersPayload variables)
            {
                var copied = (VariableModifiersPayload)clone;
                Assert.That(copied.Modifiers[0], Is.Not.SameAs(variables.Modifiers[0]));
            }
            if (source is CounterattackPayload counterattack)
            {
                var copied = (CounterattackPayload)clone;
                Assert.That(copied.Range, Is.Not.SameAs(counterattack.Range)); Assert.That(copied.Effects[0], Is.Not.SameAs(counterattack.Effects[0]));
                Assert.That(copied.Effects[0].Conditions[0], Is.Not.SameAs(counterattack.Effects[0].Conditions[0]));
                Assert.That(copied.Effects[0].CounterattackEffects[0], Is.Not.SameAs(counterattack.Effects[0].CounterattackEffects[0]));
            }
        }

        [TestCaseSource(nameof(Types))]
        public void PersistentPayloadsRoundTripAndCombatOnlyPayloadsAreExplicitlyRejected(PassiveEffectPayloadType type)
        {
            var source = Sample(type);
            if (type == PassiveEffectPayloadType.Counterattack)
            {
                Assert.That(PassiveEffectPayloadCodec.IsPersistent(source), Is.False);
                Assert.Throws<NotSupportedException>(() => PassiveEffectPayloadCodec.Write(source));
                return;
            }
            var data = PassiveEffectPayloadCodec.Write(source);
            var restored = PassiveEffectPayloadCodec.Read(data);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(source)));
        }

        private sealed class UnknownPayload : PassiveEffectPayloadRuntime { public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.Counterattack; }
        [Test]
        public void UnknownRuntimeTypesCannotHideBehindAKnownEnumOrBeSilentlyDropped()
        {
            Assert.Throws<NotSupportedException>(() => PassiveEffectPayloadCodec.Clone(new UnknownPayload()));
            Assert.Throws<NotSupportedException>(() => PassiveEffectPayloadCodec.IsPersistent(new UnknownPayload()));
            Assert.Throws<NotSupportedException>(() => PassiveEffectPayloadCodec.Read(new Godmist.Core.Serialization.PassiveEffectPayloadSaveData { Type = (PassiveEffectPayloadType)999 }));
        }
    }
}
