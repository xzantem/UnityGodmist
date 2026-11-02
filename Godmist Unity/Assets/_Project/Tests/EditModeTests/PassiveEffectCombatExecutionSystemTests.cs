using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed partial class PassiveEffectCombatExecutionSystemTests
    {
        [Test]
        public void PreventDamage_OnlyAbsorbsMagicalDamage()
        {
            var definition = CreateDefinition(
                "passive.unique.magic-absorption",
                PassiveEffectTriggerType.OnDamageTaken,
                PassiveEffectOperationType.PreventDamage);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 1f);
            Set(definition.Operations[0],
                nameof(PassiveEffectOperationDefinition.Value), value);
            var tree = CreateTree(
                "tree.test",
                CreateNode("node.test", definition));
            var targetCharacter = CreateCharacter(tree);
            targetCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(targetCharacter);

            var source = new BattleUnit { Character = CreateCharacter(null) };
            var target = new BattleUnit { Character = targetCharacter };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target }
            };
            var magicalContext = new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                damageType: DamageType.Magical);
            var physicalContext = new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                damageType: DamageType.Physical);

            Assert.That(
                PassiveEffectCombatExecutionSystem.TryPreventDamage(
                    session, magicalContext, DamageType.Magical),
                Is.True);
            Assert.That(
                PassiveEffectCombatExecutionSystem.TryPreventDamage(
                    session, physicalContext, DamageType.Physical),
                Is.False);
        }

        [Test]
        public void RuntimeFlagAttack_CanBeConsumedBeforeKnifeCost()
        {
            var definition = CreateDefinition(
                "passive.unique.free-knife",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.SetRuntimeFlag);
            Set(definition.Operations[0],
                nameof(PassiveEffectOperationDefinition.ReferenceId),
                "dagger.prevent-consume");
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 1f);
            Set(definition.Operations[0],
                nameof(PassiveEffectOperationDefinition.Value), value);
            var tree = CreateTree(
                "tree.test",
                CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);

            var source = new BattleUnit { Character = sourceCharacter };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source }
            };
            var context = new PassiveEffectCombatContext(
                session: session,
                source: source,
                actionId: "attack-1");

            Assert.That(
                PassiveEffectCombatExecutionSystem.TryConsumeRuntimeFlagForAttack(
                    session, context, "dagger.prevent-consume"),
                Is.True);
        }

        [Test]
        public void DuplicateAction_IsQueuedUntilActionResolutionAndMarkedAsDuplicate()
        {
            var definition = CreateDefinition(
                "passive.unique.duplicate",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.DuplicateAction);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 1f);
            Set(definition.Operations[0],
                nameof(PassiveEffectOperationDefinition.Value), value);
            var tree = CreateTree(
                "tree.test",
                CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);

            var source = new BattleUnit { Character = sourceCharacter };
            var target = new BattleUnit { Character = CreateCharacter(null) };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target },
                PassiveEffectEventDispatcher = new PassiveEffectCombatEventDispatcher()
            };
            var callbackCount = 0;
            var duplicateFlag = false;
            using var executor = new PassiveEffectCombatExecutionSystem(
                session,
                statusEffectSystem: null);
            executor.DuplicateActionExecutor = context =>
            {
                callbackCount++;
                duplicateFlag = context.IsPassiveDuplicate;
                return true;
            };
            var context = new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                actionId: "attack-1");

            session.PassiveEffectEventDispatcher.Publish(
                PassiveEffectTriggerType.OnAttack,
                context);
            Assert.That(callbackCount, Is.EqualTo(0));

            executor.NotifyActionResolved(context);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(duplicateFlag, Is.True);
        }

        [Test]
        public void EquipmentPassiveTooltip_IncludesActivationText()
        {
            var definition = CreateDefinition(
                "passive.tooltip.trigger",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.DuplicateAction);
            var tooltip = new PassiveEffectTooltipDefinition();
            Set(tooltip, nameof(PassiveEffectTooltipDefinition.TriggerText),
                "Przy ataku.");
            Set(definition, nameof(PassiveEffectDefinition.Tooltip), tooltip);
            var result = new StringBuilder();
            var method = typeof(UIWorkspaceTooltipView).GetMethod(
                "AppendPassiveDescriptions",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method.Invoke(
                null,
                new object[]
                {
                    result,
                    new List<PassiveEffectDefinition> { definition }
                });

            Assert.That(result.ToString(), Does.Contain("Aktywacja: Przy ataku."));
        }

        [Test]
        public void CombatStatusTooltip_FormatsFlatWeaponAccuracyAsPoints()
        {
            var gameObject = new GameObject("Tooltip Test");
            try
            {
                var view = gameObject.AddComponent<UIWorkspaceTooltipView>();
                var effect = new PassiveEffectRuntime
                {
                    EffectId = "equipment.generated.stats",
                    StatModifiers = new List<StatModifier>
                    {
                        new()
                        {
                            StatType = CharacterStatType.WeaponAccuracy,
                            Operation = StatModifierOperation.FlatBase,
                            Value = 77f
                        }
                    }
                };
                var method = typeof(UIWorkspaceTooltipView).GetMethod(
                    "BuildStatusPayload",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                var text = (string)method.Invoke(
                    view,
                    new object[] { effect, null, null, null });

                Assert.That(text, Does.Contain("+77."));
                Assert.That(text, Does.Not.Contain("7700%"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CombatStatusTooltip_ResolvesTriggeredPassiveDescription()
        {
            var definition = CreateDefinition(
                "passive.tooltip.runtime",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.SetRuntimeFlag);
            Set(definition, nameof(PassiveEffectDefinition.DisplayName), "Testowa pasywka");
            Set(definition, nameof(PassiveEffectDefinition.Description),
                "Nie zużywa zasobu po ataku.");
            var tree = CreateTree("tree.tooltip.runtime", CreateNode(
                "node.tooltip.runtime",
                definition));
            var character = CreateCharacter(tree);
            character.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.tooltip.runtime", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(character);
            var runtime = character.PassiveEffects.Find(effect =>
                effect.DefinitionId == definition.Id);
            var owner = new BattleUnit { Character = character };
            var gameObject = new GameObject("Tooltip Test");

            try
            {
                var view = gameObject.AddComponent<UIWorkspaceTooltipView>();
                var method = typeof(UIWorkspaceTooltipView).GetMethod(
                    "BuildStatusPayload",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(runtime, Is.Not.Null);
                var text = (string)method.Invoke(
                    view,
                    new object[] { runtime, null, null, owner });

                Assert.That(text, Does.Contain("Nie zużywa zasobu po ataku."));
                Assert.That(text, Does.Contain("Aktywacja:"));
                Assert.That(text, Does.Not.Contain("Brak dodatkowego działania."));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(character.ClassData);
            }
        }

        [Test]
        public void Reconciliation_MaterializesInvestedNodeAsRuntimeCarrierWithRank()
        {
            var definition = CreateDefinition(
                "passive.node.rank",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.ModifyGameplayVariable);
            var node = new SkillTreeNodeDefinition();
            Set(node, nameof(SkillTreeNodeDefinition.Id), "node.test");
            Set(node, nameof(SkillTreeNodeDefinition.MaxRanks), 2);
            Set(node, nameof(SkillTreeNodeDefinition.PassiveEffects),
                new List<PassiveEffectDefinition> { definition });
            var tree = CreateTree("tree.test", node);
            var character = CreateCharacter(tree);
            character.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = node.Id, Ranks = 2 }
                }
            });

            new PassiveEffectReconciliationSystem().Reconcile(character);

            var runtime = character.PassiveEffects.Find(effect =>
                effect.DefinitionId == definition.Id);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(PassiveEffectValueResolver.Rank(runtime), Is.EqualTo(2));
            Assert.That(runtime.Stacks, Is.EqualTo(1));
            Assert.That(runtime.EffectId,
                Is.EqualTo("passive-migration.skill-tree.tree.test.node.node.test.passive.node.rank"));
        }

        [Test]
        public void DamageMultiplier_UsesRankValueAndContextCondition()
        {
            var definition = CreateDefinition(
                "passive.node.surprise",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.ModifyGameplayVariable);
            var condition = new PassiveEffectConditionDefinition();
            Set(condition, nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.CasterHasBuff);
            Set(condition, nameof(PassiveEffectConditionDefinition.ReferenceId),
                "invisibility");
            Set(definition, nameof(PassiveEffectDefinition.Conditions),
                new List<PassiveEffectConditionDefinition> { condition });
            var operation = definition.Operations[0];
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.RankValues),
                new List<float> { 0.3f, 0.6f });
            Set(operation, nameof(PassiveEffectOperationDefinition.Value), value);

            var tree = CreateTree("tree.test", CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 2 }
                }
            });
            sourceCharacter.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "invisibility",
                DefinitionId = string.Empty,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 2
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);
            var source = new BattleUnit { Character = sourceCharacter, IsPlayerSide = true };
            var target = new BattleUnit
            {
                Character = CreateCharacter(null),
                IsPlayerSide = false
            };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target }
            };
            var context = new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                skillId: "skill.test");

            var result = PassiveEffectCombatExecutionSystem.ApplyDamageMultiplier(
                session, context, 100f);

            Assert.That(result, Is.EqualTo(160f).Within(0.001f));
        }

        [Test]
        public void CriticalChance_UsesResolvedMeleeAttackType()
        {
            var definition = CreateDefinition(
                "passive.node.rapid",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.ModifyStat);
            var condition = new PassiveEffectConditionDefinition();
            Set(condition, nameof(PassiveEffectConditionDefinition.Type),
                PassiveEffectConditionType.AttackType);
            Set(condition, nameof(PassiveEffectConditionDefinition.ReferenceId), "Melee");
            Set(definition, nameof(PassiveEffectDefinition.Conditions),
                new List<PassiveEffectConditionDefinition> { condition });
            var operation = definition.Operations[0];
            Set(operation, nameof(PassiveEffectOperationDefinition.StatType),
                CharacterStatType.CritChance);
            Set(operation, nameof(PassiveEffectOperationDefinition.ModifierOperation),
                StatModifierOperation.FlatAbsolute);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.RankValues),
                new List<float> { 0.05f, 0.1f });
            Set(operation, nameof(PassiveEffectOperationDefinition.Value), value);

            var tree = CreateTree("tree.test", CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 2 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);
            var source = new BattleUnit { Character = sourceCharacter };
            var target = new BattleUnit { Character = CreateCharacter(null) };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target }
            };
            var context = new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                attackType: AttackType.Melee);

            var result = PassiveEffectCombatExecutionSystem.ApplyCriticalChance(
                session, context, 0.25f);

            Assert.That(result, Is.EqualTo(0.35f).Within(0.001f));
        }

        [Test]
        public void MissConversion_UsesOnMissOperation()
        {
            var definition = CreateDefinition(
                "passive.node.foresight",
                PassiveEffectTriggerType.OnMiss,
                PassiveEffectOperationType.ChangeHitResolution);
            var operation = definition.Operations[0];
            Set(operation, nameof(PassiveEffectOperationDefinition.ReferenceId), "miss-to-hit");
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 1f);
            Set(operation, nameof(PassiveEffectOperationDefinition.Value), value);
            var tree = CreateTree("tree.test", CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);
            var source = new BattleUnit { Character = sourceCharacter };
            var target = new BattleUnit { Character = CreateCharacter(null) };
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target }
            };

            var converted = PassiveEffectCombatExecutionSystem.TryConvertMiss(
                session,
                new PassiveEffectCombatContext(
                    session: session,
                    source: source,
                    target: target));

            Assert.That(converted, Is.True);
        }

        [Test]
        public void PublishedCriticalEvent_AppliesConfiguredStatusOperation()
        {
            var definition = CreateDefinition(
                "passive.node.low-blow",
                PassiveEffectTriggerType.OnCriticalHit,
                PassiveEffectOperationType.ApplyStatus);
            var operation = definition.Operations[0];
            Set(operation, nameof(PassiveEffectOperationDefinition.Target),
                PassiveEffectTargetType.PrimaryTarget);
            Set(operation, nameof(PassiveEffectOperationDefinition.ReferenceId), "stun");
            Set(operation, nameof(PassiveEffectOperationDefinition.DurationTurns), 1);
            var value = new PassiveEffectValueDefinition();
            Set(value, nameof(PassiveEffectValueDefinition.NumberValue), 1f);
            Set(operation, nameof(PassiveEffectOperationDefinition.Value), value);
            var tree = CreateTree("tree.test", CreateNode("node.test", definition));
            var sourceCharacter = CreateCharacter(tree);
            sourceCharacter.SkillTreeProgress.Add(new SkillTreeProgress
            {
                TreeId = tree.Id,
                NodeInvestments = new List<SkillTreeNodeInvestment>
                {
                    new() { NodeId = "node.test", Ranks = 1 }
                }
            });
            new PassiveEffectReconciliationSystem().Reconcile(sourceCharacter);
            var source = new BattleUnit { Character = sourceCharacter };
            var target = new BattleUnit { Character = CreateCharacter(null) };
            var dispatcher = new PassiveEffectCombatEventDispatcher();
            var session = new CombatSession
            {
                Units = new List<BattleUnit> { source, target },
                PassiveEffectEventDispatcher = dispatcher
            };
            var lifecycle = new PassiveEffectLifecycleSystem();
            var statuses = new StatusEffectSystem(
                passiveEffectLifecycleSystem: lifecycle,
                passiveEffectEventDispatcher: dispatcher);
            using var executor = new PassiveEffectCombatExecutionSystem(
                session, statuses, lifecycle);

            dispatcher.Publish(
                PassiveEffectTriggerType.OnCriticalHit,
                new PassiveEffectCombatContext(
                    session: session,
                    source: source,
                    target: target,
                    isCritical: true));

            Assert.That(StatusEffectSystem.HasStatus(target, "stun"), Is.True);
        }

        [Test]
        public void SkillTreeNodeTooltip_IncludesPassiveConditionAndTrigger()
        {
            var definition = CreateDefinition(
                "passive.node.tooltip",
                PassiveEffectTriggerType.OnAttack,
                PassiveEffectOperationType.ModifyGameplayVariable);
            var tooltip = new PassiveEffectTooltipDefinition();
            Set(tooltip, nameof(PassiveEffectTooltipDefinition.ConditionText),
                "Tylko dla ataków wręcz.");
            Set(tooltip, nameof(PassiveEffectTooltipDefinition.TriggerText),
                "Przy ataku.");
            Set(definition, nameof(PassiveEffectDefinition.Tooltip), tooltip);
            var node = CreateNode("node.tooltip", definition);
            Set(node, nameof(SkillTreeNodeDefinition.Description), "Bonus obrażeń.");

            var text = SkillTreeNodeView.BuildTooltip(node, 1, string.Empty);

            Assert.That(text, Does.Contain("Warunek: Tylko dla ataków wręcz."));
            Assert.That(text, Does.Contain("Aktywacja: Przy ataku."));
        }

        private static PassiveEffectDefinition CreateDefinition(
            string id,
            PassiveEffectTriggerType trigger,
            PassiveEffectOperationType operationType)
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, nameof(PassiveEffectDefinition.Id), id);
            var triggerDefinition = new PassiveEffectTriggerDefinition();
            Set(triggerDefinition, nameof(PassiveEffectTriggerDefinition.Type), trigger);
            Set(definition, nameof(PassiveEffectDefinition.Triggers),
                new List<PassiveEffectTriggerDefinition> { triggerDefinition });
            var operation = new PassiveEffectOperationDefinition();
            Set(operation, nameof(PassiveEffectOperationDefinition.Id), "operation.test");
            Set(operation, nameof(PassiveEffectOperationDefinition.Type), operationType);
            Set(definition, nameof(PassiveEffectDefinition.Operations),
                new List<PassiveEffectOperationDefinition> { operation });
            return definition;
        }

        private static SkillTreeDefinition CreateTree(
            string id,
            SkillTreeNodeDefinition node)
        {
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            Set(tree, nameof(SkillTreeDefinition.Id), id);
            Set(tree, nameof(SkillTreeDefinition.Nodes),
                new List<SkillTreeNodeDefinition> { node });
            return tree;
        }

        private static SkillTreeNodeDefinition CreateNode(
            string id,
            PassiveEffectDefinition definition)
        {
            var node = new SkillTreeNodeDefinition();
            Set(node, nameof(SkillTreeNodeDefinition.Id), id);
            Set(node, nameof(SkillTreeNodeDefinition.PassiveEffects),
                new List<PassiveEffectDefinition> { definition });
            return node;
        }

        private static CharacterInstance CreateCharacter(SkillTreeDefinition tree)
        {
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            Set(classData, nameof(CharacterClassData.Id), "class.test");
            Set(classData, nameof(CharacterClassData.SkillTrees),
                tree == null
                    ? new List<SkillTreeDefinition>()
                    : new List<SkillTreeDefinition> { tree });
            return new CharacterInstance
            {
                CharacterId = System.Guid.NewGuid().ToString("N"),
                ClassData = classData,
                LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100),
                    MinAttack = new Stat(0),
                    MaxAttack = new Stat(0),
                    PhysicalDefense = new Stat(0),
                    MagicalDefense = new Stat(0),
                    PhysicalDefensePenetration = new Stat(0),
                    MagicalDefensePenetration = new Stat(0),
                    Dodge = new Stat(0),
                    CritChance = new Stat(0.1f),
                    CritDamage = new Stat(1.5f),
                    Speed = new Stat(0),
                    ResourceRegeneration = new Stat(0),
                    WeaponAccuracy = new Stat(100),
                    MaxResource = new Stat(0),
                    HealingPower = new Stat(0)
                },
                CurrentHealth = 100
            };
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
