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
                Level = 1,
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
                    Dodge = new Stat(baseStats.Dodge),
                    CritChance = new Stat(baseStats.CritChance),
                    CritDamage = new Stat(baseStats.CritDamage),
                    Speed = new Stat(baseStats.Speed),
                    ResourceRegeneration = new Stat(baseStats.ResourceRegeneration)
                },

                ResourcePool = new ResourcePool(
                    classData.ResourceType,
                    startingResource,
                    maxResource,
                    baseStats.ResourceRegeneration),

                Resistances = new ResistanceSet(),
                ShieldPool = new ShieldPool(),
                Inventory = new Inventory(),
                Skills = definition.StartingSkills != null ? new List<SkillData>(definition.StartingSkills)
                    : new List<SkillData>(),

                PassiveEffects = new List<Runtime.Effects.PassiveEffectRuntime>()
            };

            statPipeline.Refresh(character);
            return character;
        }
    }
}
