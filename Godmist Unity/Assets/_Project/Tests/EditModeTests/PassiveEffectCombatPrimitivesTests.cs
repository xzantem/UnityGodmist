using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public sealed class PassiveEffectCombatPrimitivesTests
    {
        [Test]
        public void Context_KeepsResolvedAttackTypeSeparateFromDamageType()
        {
            var context = new PassiveEffectCombatContext(
                skillId: "skill.knife-toss",
                attackType: AttackType.Melee,
                attackPosition: AttackPosition.Unknown,
                damageType: DamageType.Magical,
                distance: 1);

            Assert.That(context.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(context.DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(context.Distance, Is.EqualTo(1));
            Assert.That(context.AttackPosition, Is.EqualTo(AttackPosition.Unknown));
        }

        [Test]
        public void Evaluator_MatchesAttackTypeDistanceSkillAndTrigger()
        {
            var definition = CreateDefinition(
                new PassiveEffectConditionDefinition(),
                new PassiveEffectTriggerDefinition());
            Set(definition.Conditions[0], nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.AttackType);
            Set(definition.Conditions[0], nameof(PassiveEffectConditionDefinition.ReferenceId),
                nameof(AttackType.Melee));
            Set(definition.Triggers[0], nameof(PassiveEffectTriggerDefinition.Type),
                PassiveEffectTriggerType.OnAttack);

            var context = new PassiveEffectCombatContext(
                skillId: "skill.knife-toss",
                attackType: AttackType.Melee,
                distance: 1);
            var combatEvent = new PassiveEffectCombatEvent(
                PassiveEffectTriggerType.OnAttack,
                context);

            var result = PassiveEffectCombatEvaluator.Evaluate(
                definition,
                new PassiveEffectRuntime { EffectId = "passive.test" },
                combatEvent);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.CanApply, Is.True);
            Assert.That(result.Operations, Has.Count.EqualTo(1));
        }

        [Test]
        public void Evaluator_FailsClosedWhenRequiredDistanceIsUnknown()
        {
            var condition = new PassiveEffectConditionDefinition();
            Set(condition, nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.Distance);
            Set(condition, nameof(PassiveEffectConditionDefinition.Comparison),
                PassiveEffectComparisonType.GreaterOrEqual);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 2f);
            Set(condition, nameof(PassiveEffectConditionDefinition.Value), value);
            var definition = CreateDefinition(condition, null);

            var result = PassiveEffectCombatEvaluator.Evaluate(
                definition,
                null,
                new PassiveEffectCombatEvent(
                    PassiveEffectTriggerType.OnAttack,
                    new PassiveEffectCombatContext()));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.CanApply, Is.False);
            Assert.That(result.FailureReason, Does.Contain("Distance"));
        }

        [Test]
        public void Validator_RejectsMissingReferenceForAttackTypeCondition()
        {
            var definition = CreateDefinition(new PassiveEffectConditionDefinition(), null);
            Set(definition.Conditions[0], nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.AttackType);

            var result = PassiveEffectDefinitionValidator.Validate(definition);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("wymaga referencji"));
        }

        [Test]
        public void Evaluator_UsesRuntimeStacksAndKeepsOperationOrder()
        {
            var condition = new PassiveEffectConditionDefinition();
            Set(condition, nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.StackCount);
            Set(condition, nameof(PassiveEffectConditionDefinition.Comparison),
                PassiveEffectComparisonType.GreaterOrEqual);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 3f);
            Set(condition, nameof(PassiveEffectConditionDefinition.Value), value);

            var firstOperation = new PassiveEffectOperationDefinition();
            Set(firstOperation, nameof(PassiveEffectOperationDefinition.Id), "operation.first");
            var secondOperation = new PassiveEffectOperationDefinition();
            Set(secondOperation, nameof(PassiveEffectOperationDefinition.Id), "operation.second");
            var definition = CreateDefinition(condition, null);
            Set(definition, nameof(PassiveEffectDefinition.Operations),
                new List<PassiveEffectOperationDefinition>
                {
                    firstOperation,
                    secondOperation
                });

            var result = PassiveEffectCombatEvaluator.Evaluate(
                definition,
                new PassiveEffectRuntime { EffectId = "passive.test", Stacks = 3 },
                new PassiveEffectCombatEvent(
                    PassiveEffectTriggerType.OnAttack,
                    new PassiveEffectCombatContext()));

            Assert.That(result.CanApply, Is.True);
            Assert.That(result.Operations[0].Id, Is.EqualTo("operation.first"));
            Assert.That(result.Operations[1].Id, Is.EqualTo("operation.second"));
        }

        [Test]
        public void Dispatcher_PreservesPublishOrderWhenHandlerPublishesAnotherEvent()
        {
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var events = new List<PassiveEffectCombatEvent>();
            dispatcher.Published += combatEvent =>
            {
                events.Add(combatEvent);
                if (combatEvent.TriggerType == PassiveEffectTriggerType.OnAttack)
                {
                    dispatcher.Publish(
                        PassiveEffectTriggerType.OnHit,
                        combatEvent.Context);
                }
            };

            dispatcher.Publish(
                PassiveEffectTriggerType.OnAttack,
                new PassiveEffectCombatContext());

            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[0].TriggerType, Is.EqualTo(PassiveEffectTriggerType.OnAttack));
            Assert.That(events[1].TriggerType, Is.EqualTo(PassiveEffectTriggerType.OnHit));
            Assert.That(events[0].Sequence, Is.LessThan(events[1].Sequence));
        }

        [Test]
        public void Dispatcher_WithNoSubscribers_DrainsPublishedEvent()
        {
            var dispatcher = new PassiveEffectCombatEventDispatcher();

            Assert.DoesNotThrow(() => dispatcher.Publish(
                PassiveEffectTriggerType.OnTurnStart,
                new PassiveEffectCombatContext()));
        }

        [Test]
        public void StatusSystem_PublishesExpirationHookWithoutActivatingPassiveContent()
        {
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var published = new List<PassiveEffectCombatEvent>();
            dispatcher.Published += published.Add;
            var character = new CharacterInstance
            {
                LifeState = CharacterLifeState.Alive,
                PassiveEffects = new List<PassiveEffectRuntime>
                {
                    new PassiveEffectRuntime
                    {
                        EffectId = "status.test",
                        Lifetime = PassiveEffectLifetime.Timed,
                        RemainingDuration = 1,
                        TickPhase = PassiveEffectTickPhase.TurnEnd
                    }
                }
            };
            var unit = new BattleUnit { Character = character };

            new StatusEffectSystem(
                passiveEffectEventDispatcher: dispatcher).ProcessTurnEnd(unit);

            Assert.That(published, Has.Count.EqualTo(1));
            Assert.That(published, Has.Some.Matches<PassiveEffectCombatEvent>(
                combatEvent => combatEvent.TriggerType == PassiveEffectTriggerType.OnStatusExpired
                    && combatEvent.EventId == "status.test"));
            Assert.That(character.PassiveEffects, Is.Empty);
        }

        private static PassiveEffectDefinition CreateDefinition(
            PassiveEffectConditionDefinition condition,
            PassiveEffectTriggerDefinition trigger)
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, nameof(PassiveEffectDefinition.Id), "passive.test");
            var operation = new PassiveEffectOperationDefinition();
            Set(operation, nameof(PassiveEffectOperationDefinition.Id), "operation.test");
            Set(definition, nameof(PassiveEffectDefinition.Operations),
                new List<PassiveEffectOperationDefinition> { operation });
            Set(definition, nameof(PassiveEffectDefinition.Conditions),
                condition == null
                    ? new List<PassiveEffectConditionDefinition>()
                    : new List<PassiveEffectConditionDefinition> { condition });
            Set(definition, nameof(PassiveEffectDefinition.Triggers),
                trigger == null
                    ? new List<PassiveEffectTriggerDefinition>()
                    : new List<PassiveEffectTriggerDefinition> { trigger });
            return definition;
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
