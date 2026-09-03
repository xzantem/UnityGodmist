using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CharacterStatPipelineSystemTests
    {
        private CharacterStatPipelineSystem statPipeline;

        [SetUp]
        public void SetUp()
        {
            statPipeline = new CharacterStatPipelineSystem();
        }

        [Test]
        public void Refresh_AppliesAllModifierStagesInCorrectOrder()
        {
            var character = CreateCharacter();

            character.Equipment.Weapon = new EquipmentItemInstance
            {
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = CharacterStatType.MaxHealth,
                        Operation = StatModifierOperation.FlatBase,
                        Value = 20f
                    },
                    new()
                    {
                        StatType = CharacterStatType.MaxHealth,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Additive,
                        Value = 0.10f
                    },
                    new()
                    {
                        StatType = CharacterStatType.MaxHealth,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Multiplicative,
                        Value = 0.10f
                    },
                    new()
                    {
                        StatType = CharacterStatType.MaxHealth,
                        Operation = StatModifierOperation.FlatAbsolute,
                        Value = 7f
                    }
                }
            };

            statPipeline.Refresh(character);

            Assert.That(
                character.Stats.MaxHealth.GetValue(),
                Is.EqualTo(152.2f).Within(0.001f));
        }

        [Test]
        public void Refresh_AdditivePercentModifiers_AreSummed()
        {
            var character = CreateCharacter();

            character.Equipment.Weapon = new EquipmentItemInstance
            {
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = CharacterStatType.Speed,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Additive,
                        Value = 0.05f
                    },
                    new()
                    {
                        StatType = CharacterStatType.Speed,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Additive,
                        Value = 0.05f
                    }
                }
            };

            statPipeline.Refresh(character);

            Assert.That(
                character.Stats.Speed.GetValue(),
                Is.EqualTo(110f).Within(0.001f));
        }

        [Test]
        public void Refresh_MultiplicativePercentModifiers_AreAppliedSeparately()
        {
            var character = CreateCharacter();

            character.Equipment.Weapon = new EquipmentItemInstance
            {
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = CharacterStatType.Speed,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Multiplicative,
                        Value = 0.05f
                    },
                    new()
                    {
                        StatType = CharacterStatType.Speed,
                        Operation = StatModifierOperation.Percent,
                        Stacking = StatModifierStacking.Multiplicative,
                        Value = 0.20f
                    }
                }
            };

            statPipeline.Refresh(character);

            Assert.That(
                character.Stats.Speed.GetValue(),
                Is.EqualTo(126f).Within(0.001f));
        }

        [Test]
        public void Refresh_RemovingSource_ReturnsStatToBaseValue()
        {
            var character = CreateCharacter();

            character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = CharacterStatType.MinAttack,
                        Operation = StatModifierOperation.FlatBase,
                        Value = 30f
                    }
                }
            });

            statPipeline.Refresh(character);

            Assert.That(
                character.Stats.MinAttack.GetValue(),
                Is.EqualTo(50f).Within(0.001f));

            character.PassiveEffects.Clear();
            statPipeline.Refresh(character);

            Assert.That(
                character.Stats.MinAttack.GetValue(),
                Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void Refresh_AppliesGlobalSkillTreePassivePerInvestedPoint()
        {
            var character = CreateCharacter();
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();

            try
            {
                SetAutoProperty(tree, "Id", "tree.test");
                SetAutoProperty(
                    tree,
                    "GlobalPassive",
                    CreateGlobalPassive(
                        CharacterStatType.CritChance,
                        StatModifierOperation.FlatAbsolute,
                        0.002f));
                SetAutoProperty(classData, "SkillTrees", new List<SkillTreeDefinition> { tree });
                character.ClassData = classData;
                character.SkillTreeProgress.Add(new SkillTreeProgress
                {
                    TreeId = tree.Id,
                    NodeInvestments = new List<SkillTreeNodeInvestment>
                    {
                        new() { NodeId = "node.first", Ranks = 3 }
                    }
                });

                statPipeline.Refresh(character);

                Assert.That(
                    character.Stats.CritChance.GetValue(),
                    Is.EqualTo(0.106f).Within(0.001f));
                Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
                Assert.That(character.PassiveEffects[0].DefinitionId, Is.EqualTo("passive.test.global"));
                Assert.That(
                    character.PassiveEffects[0].Sources[0].SourceId,
                    Is.EqualTo("passive-migration.skill-tree.tree.test"));

                statPipeline.Refresh(character);
                Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));

                character.SkillTreeProgress.Clear();
                statPipeline.Refresh(character);
                Assert.That(character.PassiveEffects, Is.Empty);
                Assert.That(
                    character.Stats.CritChance.GetValue(),
                    Is.EqualTo(0.1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(tree);
            }
        }

        private static CharacterInstance CreateCharacter()
        {
            return new CharacterInstance
            {
                CurrentHealth = 100,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f),
                    MinAttack = new Stat(20f),
                    MaxAttack = new Stat(30f),
                    PhysicalDefense = new Stat(10f),
                    MagicalDefense = new Stat(10f),
                    Dodge = new Stat(0f),
                    CritChance = new Stat(0.1f),
                    CritDamage = new Stat(1.5f),
                    Speed = new Stat(100f),
                    ResourceRegeneration = new Stat(2f)
                },
                ResourcePool = new ResourcePool(
                    ResourceType.Mana,
                    0,
                    100,
                    2),
                Equipment = new EquipmentSet(),
                PassiveEffects = new List<PassiveEffectRuntime>()
            };
        }

        private static SkillTreeGlobalPassiveDefinition CreateGlobalPassive(
            CharacterStatType statType,
            StatModifierOperation operation,
            float value)
        {
            var passive = new SkillTreeGlobalPassiveDefinition();
            var definition = new PassiveEffectDefinition();
            var effectOperation = new PassiveEffectOperationDefinition();
            SetAutoProperty(definition, "Id", "passive.test.global");
            SetAutoProperty(effectOperation, "Id", "operation.test.stat");
            SetAutoProperty(effectOperation, "Type", PassiveEffectOperationType.ModifyStat);
            SetAutoProperty(effectOperation, "StatType", statType);
            SetAutoProperty(effectOperation, "ModifierOperation", operation);
            var effectValue = new PassiveEffectValueDefinition();
            SetAutoProperty(effectValue, "NumberValue", value);
            SetAutoProperty(effectOperation, "Value", effectValue);
            SetAutoProperty(definition, "Operations", new List<PassiveEffectOperationDefinition>
            {
                effectOperation
            });
            SetAutoProperty(
                passive,
                "PassivesPerPoint",
                new List<PassiveEffectDefinition>
                {
                    definition
                });
            return passive;
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
