using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class PassiveEffectReconciliationSystemTests
    {
        private CharacterStatPipelineSystem statPipeline;

        [SetUp]
        public void SetUp()
        {
            statPipeline = new CharacterStatPipelineSystem();
        }

        [Test]
        public void Refresh_MaterializesEquipmentPassivesAndRemovesThemAfterUnequip()
        {
            var character = CreateCharacter();
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            try
            {
                Set(definition, "Id", "equipment.test.weapon");
                Set(definition, "DisplayName", "Testowa broń");
                Set(definition, "ItemType", ItemType.Weapon);
                Set(definition, "PassiveEffects", new List<PassiveEffectDefinition>
                {
                    CreateVariablePassive(
                        "equipment.passive.gold",
                        GameplayVariableType.GoldGainPercent,
                        .10f)
                });

                var item = new EquipmentItemInstance(definition, "equipment.instance.1")
                {
                    StatModifiers = new List<StatModifier>
                    {
                        new()
                        {
                            StatType = CharacterStatType.MinAttack,
                            Operation = StatModifierOperation.FlatBase,
                            Value = 4f
                        }
                    }
                };
                character.Equipment.Weapon = item;
                character.PassiveEffects.Add(CreateUnmanagedEffect());

                statPipeline.Refresh(character);
                statPipeline.Refresh(character);

                Assert.That(character.Stats.MinAttack.GetValue(), Is.EqualTo(24f));
                Assert.That(new CombatVariableSystem().GetGoldMultiplier(character), Is.EqualTo(1.1f).Within(.001f));
                Assert.That(character.PassiveEffects.Count(effect => PassiveEffectReconciliationSystem.IsManaged(effect)), Is.EqualTo(2));
                Assert.That(character.PassiveEffects.Count(effect => effect.EffectId == "unmanaged.effect"), Is.EqualTo(1));

                character.Equipment.Weapon = null;
                statPipeline.Refresh(character);

                Assert.That(character.Stats.MinAttack.GetValue(), Is.EqualTo(20f));
                Assert.That(new CombatVariableSystem().GetGoldMultiplier(character), Is.EqualTo(1f));
                Assert.That(character.PassiveEffects.Count(effect => PassiveEffectReconciliationSystem.IsManaged(effect)), Is.EqualTo(0));
                Assert.That(character.PassiveEffects.Count(effect => effect.EffectId == "unmanaged.effect"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Refresh_MaterializesSelectedJewelryMetalAndGemPassives()
        {
            var character = CreateCharacter();
            character.BaseResistances.FireResistance = .10f;
            var definition = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            try
            {
                Set(definition, "Id", "jewelry.test.ring");
                Set(definition, "DisplayName", "Testowy pierścień");
                Set(definition, "ItemType", ItemType.Jewelry);
                Set(definition, "JewelrySlot", EquipmentSlot.Ring);

                var metal = new JewelryMetalData();
                Set(metal, "Id", "metal.test");
                Set(metal, "DisplayName", "Testowy metal");
                Set(metal, "PassiveEffects", new List<PassiveEffectDefinition>
                {
                    CreateResistancePassive(
                        "jewelry.metal.test.fire",
                        ResistanceType.Fire,
                        .20f)
                });

                var gem = new JewelryGemData();
                Set(gem, "Id", "gem.test");
                Set(gem, "DisplayName", "Testowy kamień");
                Set(gem, "PassiveEffects", new List<PassiveEffectDefinition>
                {
                    CreateVariablePassive(
                        "jewelry.gem.test.experience",
                        GameplayVariableType.ExperienceGainPercent,
                        .10f),
                    CreateVariablePassive(
                        "jewelry.gem.test.catacombs",
                        GameplayVariableType.DamageDealtPercentInDungeonTheme,
                        .25f,
                        "Catacombs")
                });

                Set(definition, "Metals", new List<JewelryMetalData> { metal });
                Set(definition, "Gems", new List<JewelryGemData> { gem });

                var jewelry = new JewelryItemInstance(definition, "jewelry.instance.1")
                {
                    MetalId = metal.Id,
                    MetalDisplayName = metal.DisplayName,
                    GemId = gem.Id,
                    GemDisplayName = gem.DisplayName
                };
                character.Equipment.Ring = jewelry;

                statPipeline.Refresh(character);

                Assert.That(character.Resistances.FireResistance, Is.EqualTo(.30f).Within(.001f));
                Assert.That(new CombatVariableSystem().GetExperienceMultiplier(character), Is.EqualTo(1.10f).Within(.001f));
                Assert.That(
                    new CombatVariableSystem().GetDungeonThemeDamageMultiplier(character, DungeonTheme.Catacombs),
                    Is.EqualTo(1.25f).Within(.001f));
                Assert.That(
                    new CombatVariableSystem().GetDungeonThemeDamageMultiplier(character, DungeonTheme.Story),
                    Is.EqualTo(1f).Within(.001f));
                Assert.That(character.PassiveEffects.Select(effect => effect.DefinitionId), Does.Contain("jewelry.metal.test.fire"));
                Assert.That(character.PassiveEffects.Select(effect => effect.DefinitionId), Does.Contain("jewelry.gem.test.experience"));
                Assert.That(character.PassiveEffects.Select(effect => effect.DefinitionId), Does.Contain("jewelry.gem.test.catacombs"));

                character.Equipment.Ring = null;
                statPipeline.Refresh(character);

                Assert.That(character.Resistances.FireResistance, Is.EqualTo(.10f).Within(.001f));
                Assert.That(new CombatVariableSystem().GetExperienceMultiplier(character), Is.EqualTo(1f));
                Assert.That(
                    new CombatVariableSystem().GetDungeonThemeDamageMultiplier(character, DungeonTheme.Catacombs),
                    Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static CharacterInstance CreateCharacter()
        {
            return new CharacterInstance
            {
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f),
                    MinAttack = new Stat(20f),
                    MaxAttack = new Stat(30f),
                    PhysicalDefense = new Stat(10f),
                    MagicalDefense = new Stat(10f),
                    PhysicalDefensePenetration = new Stat(0f),
                    MagicalDefensePenetration = new Stat(0f),
                    Dodge = new Stat(0f),
                    CritChance = new Stat(0f),
                    CritDamage = new Stat(1.5f),
                    Speed = new Stat(100f),
                    ResourceRegeneration = new Stat(2f),
                    WeaponAccuracy = new Stat(100f),
                    MaxResource = new Stat(100f),
                    HealingPower = new Stat(0f)
                },
                CurrentHealth = 100,
                Equipment = new EquipmentSet(),
                PassiveEffects = new List<PassiveEffectRuntime>(),
                BaseResistances = new ResistanceSet(),
                Resistances = new ResistanceSet()
            };
        }

        private static PassiveEffectDefinition CreateVariablePassive(
            string id,
            GameplayVariableType variableType,
            float value,
            string referenceId = "")
        {
            var operation = new PassiveEffectOperationDefinition();
            Set(operation, "Id", $"{id}.operation");
            Set(operation, "Type", PassiveEffectOperationType.ModifyGameplayVariable);
            Set(operation, "VariableType", variableType);
            Set(operation, "ReferenceId", referenceId);
            Set(operation, "Value", CreateValue(value));
            return CreateDefinition(id, operation);
        }

        private static PassiveEffectDefinition CreateResistancePassive(
            string id,
            ResistanceType resistanceType,
            float value)
        {
            var operation = new PassiveEffectOperationDefinition();
            Set(operation, "Id", $"{id}.operation");
            Set(operation, "Type", PassiveEffectOperationType.ModifyResistance);
            Set(operation, "ResistanceType", resistanceType);
            Set(operation, "Value", CreateValue(value));
            return CreateDefinition(id, operation);
        }

        private static PassiveEffectDefinition CreateDefinition(
            string id,
            PassiveEffectOperationDefinition operation)
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, "Id", id);
            Set(definition, "Operations", new List<PassiveEffectOperationDefinition> { operation });
            return definition;
        }

        private static PassiveEffectValueDefinition CreateValue(float numberValue)
        {
            var value = new PassiveEffectValueDefinition();
            Set(value, "Source", PassiveEffectValueSourceType.Constant);
            Set(value, "NumberValue", numberValue);
            return value;
        }

        private static PassiveEffectRuntime CreateUnmanagedEffect()
        {
            return new PassiveEffectRuntime
            {
                EffectId = "unmanaged.effect",
                Sources = new List<PassiveEffectSourceRuntime>
                {
                    new()
                    {
                        SourceType = PassiveEffectSourceType.RuntimeSystem,
                        SourceId = "runtime.system"
                    }
                }
            };
        }

        private static void Set(object target, string propertyName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Nie znaleziono backing field dla {propertyName}.");
        }
    }
}
