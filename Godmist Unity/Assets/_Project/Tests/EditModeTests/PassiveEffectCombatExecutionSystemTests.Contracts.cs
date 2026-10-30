using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
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
    public sealed partial class PassiveEffectCombatExecutionSystemTests
    {
        private readonly List<UnityEngine.Object> contractAssets = new();
        [TearDown] public void CleanupContractAssets() { foreach (var asset in contractAssets) UnityEngine.Object.DestroyImmediate(asset); contractAssets.Clear(); }

        [TestCase(PassiveEffectTargetType.AllAllies)][TestCase(PassiveEffectTargetType.SelectedAlly)]
        [TestCase(PassiveEffectTargetType.ProtectedAlly)][TestCase(PassiveEffectTargetType.Area)]
        [TestCase(PassiveEffectTargetType.NearestTarget)][TestCase((PassiveEffectTargetType)999)]
        public void UnsupportedPassiveTargetIsRejectedInsteadOfBecomingPrimaryTarget(PassiveEffectTargetType target)
        {
            var definition = CreateDefinition("test.contract.target", PassiveEffectTriggerType.OnHit, PassiveEffectOperationType.ApplyStatus);
            Set(definition.Operations[0], nameof(PassiveEffectOperationDefinition.ReferenceId), "burn");
            Set(definition.Operations[0], nameof(PassiveEffectOperationDefinition.Target), target);
            Assert.That(PassiveEffectDefinitionValidator.Validate(definition).Errors.Any(error => error.Contains(target.ToString())), Is.True);
            Assert.That(PassiveEffectRuntimeFactory.TryCreate(definition, null, out _), Is.False);
            var method = typeof(PassiveEffectCombatExecutionSystem).GetMethod("ResolveTargets", BindingFlags.NonPublic | BindingFlags.Static);
            var enumerable = (IEnumerable<BattleUnit>)method.Invoke(null, new object[] { null, target, new PassiveEffectCombatContext() });
            Assert.Throws<InvalidOperationException>(() => enumerable.ToArray());
        }

        [TestCase(PassiveEffectValueSourceType.Stat)][TestCase(PassiveEffectValueSourceType.EventValue)]
        [TestCase(PassiveEffectValueSourceType.Resource)][TestCase(PassiveEffectValueSourceType.TargetCount)]
        [TestCase(PassiveEffectValueSourceType.RuntimeState)]
        public void StatusChanceRequiresItsActualConstantResolver(PassiveEffectValueSourceType source)
        {
            var definition = CreateDefinition("test.contract.status-value", PassiveEffectTriggerType.OnHit, PassiveEffectOperationType.ApplyStatus);
            Set(definition.Operations[0], nameof(PassiveEffectOperationDefinition.ReferenceId), "burn");
            Set(definition.Operations[0].Value, nameof(PassiveEffectValueDefinition.Source), source);
            Assert.That(PassiveEffectDefinitionValidator.Validate(definition).IsValid, Is.False);
            Assert.That(PassiveEffectRuntimeFactory.TryCreate(definition, null, out _), Is.False);
        }

        [TestCase(PassiveEffectOperationType.ReduceIncomingDamage)][TestCase(PassiveEffectOperationType.MaxHealthFromMaxResource)]
        [TestCase(PassiveEffectOperationType.LowHealthShield)][TestCase(PassiveEffectOperationType.OrderActionCostReduction)]
        public void SpecialUniqueOperationsRejectAConditionalOrTriggeredDefinition(PassiveEffectOperationType type)
        {
            var definition = CreateDefinition("test.contract.unique", PassiveEffectTriggerType.OnAttack, type);
            Assert.That(PassiveEffectDefinitionValidator.Validate(definition).Errors.Any(error => error.Contains("bezwarunkowej")), Is.True);
            Set(definition, nameof(PassiveEffectDefinition.Triggers), new List<PassiveEffectTriggerDefinition>());
            var condition = new PassiveEffectConditionDefinition(); Set(condition, nameof(PassiveEffectConditionDefinition.Type), PassiveEffectConditionType.CasterHealthBelowPercent);
            Set(definition, nameof(PassiveEffectDefinition.Conditions), new List<PassiveEffectConditionDefinition> { condition });
            Assert.That(PassiveEffectDefinitionValidator.Validate(definition).Errors.Any(error => error.Contains("bezwarunkowej")), Is.True);
        }

        [Test]
        public void StaticAndTriggeredConstantShareRankScaleOffsetAndDoNotUseStacksAsRank()
        {
            var definition = CreateDefinition("test.contract.rank", PassiveEffectTriggerType.OnHit, PassiveEffectOperationType.Heal);
            var value = definition.Operations[0].Value;
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 10f);
            Set(value, nameof(PassiveEffectValueDefinition.RankValues), new List<float> { 15, 25 });
            Set(value, nameof(PassiveEffectValueDefinition.Scale), 2f); Set(value, nameof(PassiveEffectValueDefinition.Offset), 3f);
            var node = CreateNode("node.contract", definition); var tree = CreateTree("tree.contract", node); contractAssets.Add(tree);
            var character = CreateCharacter(tree); contractAssets.Add(character.ClassData);
            character.SkillTreeProgress.Add(new SkillTreeProgress { TreeId = tree.Id, NodeInvestments = new() { new() { NodeId = node.Id, Ranks = 2 } } });
            new PassiveEffectReconciliationSystem().Reconcile(character);
            var runtime = character.PassiveEffects.Single(effect => effect.DefinitionId == definition.Id);
            Assert.That(PassiveEffectValueResolver.Rank(runtime), Is.EqualTo(2)); runtime.Stacks = 7;
            character.CurrentHealth = 10;
            var owner = new BattleUnit { Character = character }; var session = new CombatSession { Units = new() { owner }, PassiveEffectEventDispatcher = new() };
            using (var executor = new PassiveEffectCombatExecutionSystem(session, new StatusEffectSystem()))
                session.PassiveEffectEventDispatcher.Publish(PassiveEffectTriggerType.OnHit, new PassiveEffectCombatContext(session: session, source: owner, target: owner));
            Assert.That(character.CurrentHealth, Is.EqualTo(63));
            Set(definition, nameof(PassiveEffectDefinition.Triggers), new List<PassiveEffectTriggerDefinition>());
            Set(definition.Operations[0], nameof(PassiveEffectOperationDefinition.Type), PassiveEffectOperationType.ModifyStat);
            Assert.That(PassiveEffectRuntimeFactory.TryCreate(definition, null, out var persistent, 2), Is.True);
            Assert.That(persistent.StatModifiers.Single().Value, Is.EqualTo(53));
        }
    }
}
