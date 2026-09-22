using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed partial class PassiveEffectCombatExecutionSystemTests
    {
        private static BattleUnit ContentOwner(PassiveEffectDefinition definition)
        {
            var tree = CreateTree("tree.content", CreateNode("node.content", definition));
            var character = CreateCharacter(tree);
            character.ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 10);
            character.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment> { new() { NodeId = "node.content", Ranks = 1 } }
            });
            new PassiveEffectReconciliationSystem().Reconcile(character);
            return new BattleUnit { Character = character };
        }

        private static void DestroyOwner(BattleUnit owner)
        {
            foreach (var tree in owner.Character.ClassData.SkillTrees) Object.DestroyImmediate(tree);
            Object.DestroyImmediate(owner.Character.ClassData);
        }

        [TestCase(ResourceType.Mana)]
        [TestCase(ResourceType.Rage)]
        [TestCase(ResourceType.Momentum)]
        public void ContentCost_IsPerOwnerAndDoesNotMutateSharedSkill(ResourceType resource)
        {
            var definition = CreateDefinition("cost", PassiveEffectTriggerType.AlwaysActive, PassiveEffectOperationType.ModifyResourceCost);
            Set(definition.Operations[0].Value, "NumberValue", -.3f);
            var owner = ContentOwner(definition);
            var other = new BattleUnit { Character = CreateCharacter(null) };
            var skill = ScriptableObject.CreateInstance<SkillData>();
            Set(skill, "SkillId", "skill.content"); Set(skill, "ResourceCost", 35); Set(skill, "ResourceType", resource);
            try
            {
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(owner, skill), Is.EqualTo(25));
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(other, skill), Is.EqualTo(35));
                Assert.That(skill.ResourceCost, Is.EqualTo(35));
                Set(definition.Operations[0].Value, "NumberValue", -2f);
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(owner, skill), Is.Zero);
            }
            finally { DestroyOwner(owner); Object.DestroyImmediate(other.Character.ClassData); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void ContentResourceProc_OncePerActionAcrossHitsAndTargets()
        {
            var definition = CreateDefinition("mana.proc", PassiveEffectTriggerType.OnAttack, PassiveEffectOperationType.RestoreResource);
            Set(definition.Operations[0].Value, "NumberValue", 5f);
            var frequency = new PassiveEffectParameterDefinition();
            Set(frequency, "Key", "frequency"); Set(frequency, "StringValue", "once_per_action");
            definition.Operations[0].Parameters.Add(frequency);
            var owner = ContentOwner(definition);
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var session = new CombatSession { Units = new List<BattleUnit> { owner }, PassiveEffectEventDispatcher = dispatcher };
            try
            {
                using var execution = new PassiveEffectCombatExecutionSystem(session, null);
                for (var i = 0; i < 4; i++) dispatcher.Publish(PassiveEffectTriggerType.OnAttack,
                    new PassiveEffectCombatContext(source: owner, actionId: "a"));
                Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(5));
                dispatcher.Publish(PassiveEffectTriggerType.OnAttack, new PassiveEffectCombatContext(source: owner, actionId: "b"));
                Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
                dispatcher.Publish(PassiveEffectTriggerType.OnAttack, new PassiveEffectCombatContext(source: owner, actionId: "c", isPassiveReaction: true));
                Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
            }
            finally { DestroyOwner(owner); }
        }

        [Test]
        public void ContentHealing_UsesActualEventValue_AndRetainsDynamicCarrier()
        {
            var definition = CreateDefinition("heal.proc", PassiveEffectTriggerType.OnResourceSpent, PassiveEffectOperationType.Heal);
            Set(definition.Operations[0].Value, "Source", PassiveEffectValueSourceType.EventValue);
            Set(definition.Operations[0].Value, "Scale", .2f);
            var owner = ContentOwner(definition); owner.Character.CurrentHealth = 50;
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var session = new CombatSession { Units = new List<BattleUnit> { owner }, PassiveEffectEventDispatcher = dispatcher };
            try
            {
                Assert.That(owner.Character.PassiveEffects.Count, Is.GreaterThan(0));
                using var execution = new PassiveEffectCombatExecutionSystem(session, null);
                dispatcher.Publish(PassiveEffectTriggerType.OnResourceSpent, new PassiveEffectCombatContext(source: owner, eventValue: 30));
                Assert.That(owner.Character.CurrentHealth, Is.EqualTo(56));
            }
            finally { DestroyOwner(owner); }
        }

        [Test]
        public void ContentReactiveDamage_DoesNotRecursivelyTriggerRetaliation()
        {
            var definition = CreateDefinition("retaliate", PassiveEffectTriggerType.OnDamageTaken, PassiveEffectOperationType.DealDamage);
            Set(definition.Operations[0], "Target", PassiveEffectTargetType.Attacker);
            Set(definition.Operations[0], "ReferenceId", "Inevitable");
            Set(definition.Operations[0].Value, "NumberValue", 10f);
            var first = ContentOwner(definition);
            var second = ContentOwner(definition);
            first.Character.Stats.CritChance = new Godmist.Core.Types.Stat(0);
            second.Character.Stats.CritChance = new Godmist.Core.Types.Stat(0);
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var session = new CombatSession { Units = new List<BattleUnit> { first, second }, PassiveEffectEventDispatcher = dispatcher };
            try
            {
                var damage = new CombatDamageSystem();
                using var execution = new PassiveEffectCombatExecutionSystem(session, null) { ReactionDamageSystem = damage };
                damage.ApplyDamage(session, first, second, 5, DamageType.Inevitable);
                Assert.That(second.Character.CurrentHealth, Is.EqualTo(95));
                Assert.That(first.Character.CurrentHealth, Is.EqualTo(90));
            }
            finally { DestroyOwner(first); DestroyOwner(second); }
        }

        [Test]
        public void ContentEffectSelector_ChangesOnlyRequestedEffect()
        {
            var definition = CreateDefinition("duration", PassiveEffectTriggerType.AlwaysActive, PassiveEffectOperationType.ModifySkillParameter);
            Set(definition.Operations[0], "ReferenceId", "duration_turns");
            Set(definition.Operations[0], "ModifierOperation", StatModifierOperation.FlatAbsolute);
            Set(definition.Operations[0].Value, "NumberValue", 2f);
            var selector = new PassiveEffectParameterDefinition();
            Set(selector, "Key", "effect_index"); Set(selector, "IntegerValue", 1);
            definition.Operations[0].Parameters.Add(selector);
            var owner = ContentOwner(definition);
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var first = new SkillEffectData(); var second = new SkillEffectData();
            typeof(SkillData).GetField("effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(skill, new List<SkillEffectData> { first, second });
            try
            {
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(owner, skill, first).DurationTurns, Is.EqualTo(first.DurationTurns));
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(owner, skill, second).DurationTurns, Is.EqualTo(second.DurationTurns + 2));
            }
            finally { DestroyOwner(owner); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void ContentHealEvent_ReportsEffectiveHealingAndAdvancesTarget()
        {
            var definition = CreateDefinition("heal.av", PassiveEffectTriggerType.OnHeal, PassiveEffectOperationType.ModifyActionValue);
            Set(definition.Operations[0], "Target", PassiveEffectTargetType.PrimaryTarget);
            Set(definition.Operations[0].Value, "NumberValue", .2f);
            var owner = ContentOwner(definition);
            var target = new BattleUnit { Character = CreateCharacter(null), CurrentActionValue = 100 };
            target.Character.Stats.Speed = new Godmist.Core.Types.Stat(50);
            target.Character.CurrentHealth = 90;
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var session = new CombatSession { Units = new List<BattleUnit> { owner, target }, PassiveEffectEventDispatcher = dispatcher };
            float observed = 0;
            dispatcher.Published += e => { if (e.TriggerType == PassiveEffectTriggerType.OnHeal) observed = e.Context.EventValue; };
            try
            {
                using var execution = new PassiveEffectCombatExecutionSystem(session, null);
                new CombatRecoverySystem().Heal(target, 50,
                    new PassiveEffectCombatContext(session: session, source: owner, target: target));
                Assert.That(observed, Is.EqualTo(10));
                Assert.That(target.CurrentActionValue, Is.EqualTo(100 - .2f * CombatTurnSystem.CalculateBaseActionValue(target)).Within(.001f));
            }
            finally { DestroyOwner(owner); Object.DestroyImmediate(target.Character.ClassData); }
        }

        [Test]
        public void ContentRange_IsResolvedWithoutChangingOtherCasters()
        {
            var definition = CreateDefinition("range", PassiveEffectTriggerType.AlwaysActive, PassiveEffectOperationType.ModifySkillParameter);
            Set(definition.Operations[0], "ReferenceId", "range");
            Set(definition.Operations[0], "ModifierOperation", StatModifierOperation.FlatAbsolute);
            Set(definition.Operations[0].Value, "NumberValue", 3f);
            var owner = ContentOwner(definition);
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var mode = new SkillTargetModeData();
            try
            {
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveMode(owner, skill, mode).AbsoluteRange.MaxRange, Is.EqualTo(4));
                Assert.That(mode.AbsoluteRange.MaxRange, Is.EqualTo(1));
                Assert.That(PassiveEffectCombatExecutionSystem.ResolveMode(null, skill, mode).AbsoluteRange.MaxRange, Is.EqualTo(1));
            }
            finally { DestroyOwner(owner); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void ContentParameters_CopyEffectsWithoutChangingAuthoringData()
        {
            var definition = CreateDefinition("hits", PassiveEffectTriggerType.AlwaysActive, PassiveEffectOperationType.ModifySkillParameter);
            Set(definition.Operations[0], "ReferenceId", "hit_count");
            Set(definition.Operations[0], "ModifierOperation", StatModifierOperation.FlatAbsolute);
            Set(definition.Operations[0].Value, "NumberValue", 2f);
            var owner = ContentOwner(definition);
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var effect = new SkillEffectData();
            try
            {
                var resolved = PassiveEffectCombatExecutionSystem.ResolveEffect(owner, skill, effect);
                Assert.That(resolved.DamageHitCount, Is.EqualTo(3));
                Assert.That(effect.DamageHitCount, Is.EqualTo(1));
                Set(definition.Operations[0], "ReferenceId", "unknown_parameter");
                Assert.That(PassiveEffectDefinitionValidator.Validate(definition).IsValid, Is.False);
            }
            finally { DestroyOwner(owner); Object.DestroyImmediate(skill); }
        }
    }
}
