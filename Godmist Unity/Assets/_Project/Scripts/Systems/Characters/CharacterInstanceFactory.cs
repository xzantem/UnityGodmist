using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Items;
using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterInstanceFactory
    {
        private readonly CharacterStatPipelineSystem statPipeline = new();

        public CharacterInstance Create(
            CharacterDefinition definition,
            CharacterClassData classData = null)
        {
            return Create(definition, classData, CharacterProgressionSystem.MinimumLevel);
        }

        public CharacterInstance Create(
            CharacterDefinition definition,
            CharacterClassData classData,
            int level)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            classData ??= definition.ClassData;

            if (classData == null)
            {
                throw new ArgumentNullException(nameof(classData));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("CharacterDefinition musi miec niepuste Id.", nameof(definition));
            }

            var baseStats = definition.BaseStats ?? new CharacterBaseStatsData();
            var maxHealth = Mathf.Max(1, baseStats.MaxHealth);
            var maxResource = Mathf.Max(0, baseStats.MaxResource);

            var startingResource = classData.ResourceType == ResourceType.Mana ? maxResource : 0;

            var character = new CharacterInstance
            {
                CharacterId = definition.Id,
                Definition = definition,
                ClassData = classData,
                Level = Mathf.Clamp(
                    level,
                    CharacterProgressionSystem.MinimumLevel,
                    CharacterProgressionSystem.MaximumLevel),
                CurrentExperience = 0,
                AvailableSkillPoints = 0,
                CurrentHealth = maxHealth,
                Honor = 0,
                WeaponAccuracy = baseStats.WeaponAccuracy,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,

                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(maxHealth),
                    MinAttack = new Stat(baseStats.MinAttack),
                    MaxAttack = new Stat(baseStats.MaxAttack),
                    PhysicalDefense = new Stat(baseStats.PhysicalDefense),
                    MagicalDefense = new Stat(baseStats.MagicalDefense),
                    PhysicalDefensePenetration = new Stat(baseStats.PhysicalDefensePenetration),
                    MagicalDefensePenetration = new Stat(baseStats.MagicalDefensePenetration),
                    Dodge = new Stat(baseStats.Dodge),
                    CritChance = new Stat(baseStats.CritChance),
                    CritDamage = new Stat(baseStats.CritDamage),
                    Speed = new Stat(baseStats.Speed),
                    ResourceRegeneration = new Stat(baseStats.ResourceRegeneration),
                    WeaponAccuracy = new Stat(baseStats.WeaponAccuracy),
                    MaxResource = new Stat(maxResource),
                    HealingPower = new Stat(baseStats.HealingPower)
                },

                ResourcePool = new ResourcePool(
                    classData.ResourceType,
                    startingResource,
                    maxResource,
                    baseStats.ResourceRegeneration),

                Resistances = CreateResistances(definition.BaseResistances),
                BaseResistances = CreateResistances(definition.BaseResistances),
                ShieldPool = new ShieldPool(),
                Inventory = new Inventory(),
                Skills = CopyDistinctSkills(
                    definition.StartingSkills,
                    ResolveBasicAttackSkill(definition, classData)),

                SkillTreeProgress = new List<SkillTreeProgress>(),

                PassiveEffects = new List<Runtime.Effects.PassiveEffectRuntime>()
            };

            statPipeline.Refresh(character);

            if (character.Level > CharacterProgressionSystem.MinimumLevel)
            {
                new CharacterLevelGrowthSystem(statPipeline).Apply(
                    character,
                    grantCurrentHealthGrowth: true);
            }

            new SkillLoadoutSystem().TryInitialize(character);
            return character;
        }

        private static SkillData ResolveBasicAttackSkill(
            CharacterDefinition definition,
            CharacterClassData classData)
        {
            if (definition?.BasicAttackSkill != null)
            {
                return definition.BasicAttackSkill;
            }

            foreach (var skill in definition?.StartingSkills
                         ?? new List<SkillData>())
            {
                if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    return skill;
                }
            }

            // Compatibility only for old content without any per-character
            // skill configuration. New content must use CharacterDefinition.
            return classData?.BasicAttackSkill;
        }

        private static ResistanceSet CreateResistances(
            CharacterResistanceData baseResistances)
        {
            if (baseResistances == null)
            {
                return new ResistanceSet();
            }

            return new ResistanceSet
            {
                BleedResistance = baseResistances.BleedResistance,
                PoisonResistance = baseResistances.PoisonResistance,
                FireResistance = baseResistances.FireResistance,
                WeakeningResistance = baseResistances.WeakeningResistance,
                SuppressionResistance = baseResistances.SuppressionResistance,
                MoveResistance = baseResistances.MoveResistance
            };
        }

        private static List<SkillData> CopyDistinctSkills(
            IReadOnlyList<SkillData> source,
            SkillData basicAttackSkill)
        {
            var result = new List<SkillData>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var skill in source ?? new List<SkillData>())
            {
                if (skill == null
                    || string.IsNullOrWhiteSpace(skill.SkillId)
                    || !seen.Add(skill.SkillId))
                {
                    continue;
                }

                result.Add(skill);
            }

            if (basicAttackSkill != null
                && !string.IsNullOrWhiteSpace(basicAttackSkill.SkillId)
                && seen.Add(basicAttackSkill.SkillId))
            {
                result.Add(basicAttackSkill);
            }

            return result;
        }
    }
}
