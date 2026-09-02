using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class PassiveEffectDefinitionTests
    {
        [Test]
        public void Definition_ExposesComposableStaticParts()
        {
            var definition = new PassiveEffectDefinition();

            Assert.That(definition.Id, Is.Empty);
            Assert.That(definition.Lifetime, Is.EqualTo(PassiveEffectLifetime.Persistent));
            Assert.That(definition.Conditions, Is.Not.Null);
            Assert.That(definition.Triggers, Is.Not.Null);
            Assert.That(definition.Operations, Is.Not.Null);
            Assert.That(definition.Tooltip, Is.Not.Null);
            Assert.That(definition.IsAlwaysActive, Is.True);
            Assert.That(definition.IsTriggered, Is.False);
        }

        [Test]
        public void RuntimeFactory_BindsDefinitionSourceAndTimedLifetime()
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, nameof(PassiveEffectDefinition.Id), "passive.test.timed");
            Set(definition, nameof(PassiveEffectDefinition.DisplayName), "Timed passive");
            Set(definition, nameof(PassiveEffectDefinition.Lifetime), PassiveEffectLifetime.Timed);
            Set(definition, nameof(PassiveEffectDefinition.DefaultDurationTurns), 3);
            Set(definition, nameof(PassiveEffectDefinition.TickPhase), PassiveEffectTickPhase.TurnStart);

            var source = new PassiveEffectSourceRuntime
            {
                SourceType = PassiveEffectSourceType.Equipment,
                SourceId = "item.test.01",
                SourceCharacterId = "character.test"
            };

            Assert.That(
                PassiveEffectRuntimeFactory.TryCreate(definition, source, out var runtime),
                Is.True);
            Assert.That(runtime.DefinitionId, Is.EqualTo("passive.test.timed"));
            Assert.That(runtime.EffectId, Is.EqualTo("passive.test.timed"));
            Assert.That(runtime.DisplayName, Is.EqualTo("Timed passive"));
            Assert.That(runtime.Lifetime, Is.EqualTo(PassiveEffectLifetime.Timed));
            Assert.That(runtime.RemainingDuration, Is.EqualTo(3));
            Assert.That(runtime.TickPhase, Is.EqualTo(PassiveEffectTickPhase.TurnStart));
            Assert.That(runtime.Sources, Has.Count.EqualTo(1));
            Assert.That(runtime.Sources[0].SourceId, Is.EqualTo("item.test.01"));
        }

        [Test]
        public void RuntimeState_SetReplacesOnlyTheMatchingKey()
        {
            var runtime = new PassiveEffectRuntime();
            runtime.SetStateValue(PassiveEffectRuntimeStateValue.Integer("stacks", 2));
            runtime.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("armed", true));
            runtime.SetStateValue(PassiveEffectRuntimeStateValue.Integer("stacks", 5));

            Assert.That(runtime.StateValues, Has.Count.EqualTo(2));
            Assert.That(runtime.GetStateValue("stacks").IntegerValue, Is.EqualTo(5));
            Assert.That(runtime.GetStateValue("armed").BooleanValue, Is.True);
            Assert.That(runtime.RemoveStateValue("armed"), Is.True);
            Assert.That(runtime.GetStateValue("armed"), Is.Null);
        }

        [Test]
        public void Validator_RejectsUnstableOperationsAndMissingReferences()
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, nameof(PassiveEffectDefinition.Id), "passive.invalid");
            var operation = new PassiveEffectOperationDefinition();
            Set(operation, nameof(PassiveEffectOperationDefinition.Id), "operation.apply-status");
            Set(operation, nameof(PassiveEffectOperationDefinition.Type), PassiveEffectOperationType.ApplyStatus);
            var operationWithoutId = new PassiveEffectOperationDefinition();
            Set(definition, nameof(PassiveEffectDefinition.Operations), new List<PassiveEffectOperationDefinition>
            {
                operation,
                operation,
                operationWithoutId
            });

            var result = PassiveEffectDefinitionValidator.Validate(definition);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("stabilnego ID"));
            Assert.That(result.Errors, Has.Some.Contains("zduplikowane ID"));
            Assert.That(result.Errors, Has.Some.Contains("wymaga referencji"));
        }

        private static void Set<T>(object target, string propertyName, T value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property {propertyName}");
            property.SetValue(target, value);
        }
    }
}
