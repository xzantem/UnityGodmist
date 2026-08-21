using System;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterLevelGrowthSystem
    {
        private readonly CharacterStatPipelineSystem statPipeline;

        public CharacterLevelGrowthSystem(CharacterStatPipelineSystem statPipeline = null)
        {
            this.statPipeline = statPipeline ?? new CharacterStatPipelineSystem();
        }

        public void Apply(CharacterInstance character, bool grantCurrentHealthGrowth)
        {
            if (character?.Definition == null || character.Stats == null) return;

            var baseStats = character.Definition.BaseStats ?? new CharacterBaseStatsData();
            var growth = character.Definition.GrowthData ?? new CharacterGrowthData();
            var multiplier = GetCumulativeGrowthMultiplier(character.Level);
            var previousMaxHealth = Mathf.Max(
                1,
                Mathf.RoundToInt(character.Stats.MaxHealth.GetValue()));

            character.Stats.MaxHealth.BaseValue = baseStats.MaxHealth + growth.MaxHealth * multiplier;
            character.Stats.MinAttack.BaseValue = baseStats.MinAttack + growth.MinAttack * multiplier;
            character.Stats.MaxAttack.BaseValue = baseStats.MaxAttack + growth.MaxAttack * multiplier;
            character.Stats.PhysicalDefense.BaseValue = baseStats.PhysicalDefense
                + growth.PhysicalDefense * multiplier;
            character.Stats.MagicalDefense.BaseValue = baseStats.MagicalDefense
                + growth.MagicalDefense * multiplier;
            character.Stats.Dodge.BaseValue = baseStats.Dodge + growth.Dodge * multiplier;
            character.Stats.CritChance.BaseValue = baseStats.CritChance
                + growth.CritChance * multiplier;
            character.Stats.CritDamage.BaseValue = baseStats.CritDamage
                + growth.CritDamage * multiplier;
            character.Stats.Speed.BaseValue = baseStats.Speed + growth.Speed * multiplier;
            character.Stats.ResourceRegeneration.BaseValue = baseStats.ResourceRegeneration
                + growth.ResourceRegeneration * multiplier;
            character.Stats.WeaponAccuracy.BaseValue = baseStats.WeaponAccuracy
                + growth.WeaponAccuracy * multiplier;
            character.Stats.MaxResource.BaseValue = baseStats.MaxResource
                + growth.MaxResource * multiplier;

            if (character.ResourcePool != null)
            {
                character.ResourcePool.MaxValue = Mathf.Max(0, Mathf.RoundToInt(
                    character.Stats.MaxResource.GetValue()));
                character.ResourcePool.CurrentValue = Mathf.Clamp(
                    character.ResourcePool.CurrentValue,
                    0,
                    character.ResourcePool.MaxValue);
            }

            statPipeline.Refresh(character);

            if (!grantCurrentHealthGrowth) return;

            var currentMaxHealth = Mathf.Max(
                1,
                Mathf.RoundToInt(character.Stats.MaxHealth.GetValue()));
            character.CurrentHealth = Mathf.Clamp(
                character.CurrentHealth + Mathf.Max(0, currentMaxHealth - previousMaxHealth),
                0,
                currentMaxHealth);
        }

        public float GetCumulativeGrowthMultiplier(int level)
        {
            if (level < CharacterProgressionSystem.MinimumLevel
                || level > CharacterProgressionSystem.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            var multiplier = 0f;

            for (var targetLevel = CharacterProgressionSystem.MinimumLevel + 1;
                 targetLevel <= level;
                 targetLevel++)
            {
                multiplier += GetLevelBandCoefficient(targetLevel);
            }

            return multiplier;
        }

        public int GetLevelBandCoefficient(int targetLevel)
        {
            if (targetLevel < 2 || targetLevel > CharacterProgressionSystem.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(targetLevel));
            }

            if (targetLevel <= 10) return 1;
            if (targetLevel <= 20) return 2;
            if (targetLevel <= 30) return 3;
            if (targetLevel <= 40) return 5;
            return 9;
        }
    }
}
