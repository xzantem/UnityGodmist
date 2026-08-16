using System;
using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterProgressionSystem
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 50;

        private readonly CharacterLevelGrowthSystem growthSystem;

        public CharacterProgressionSystem(CharacterLevelGrowthSystem growthSystem = null)
        {
            this.growthSystem = growthSystem ?? new CharacterLevelGrowthSystem();
        }

        public int GetExperienceRequiredForNextLevel(int currentLevel)
        {
            if (currentLevel < MinimumLevel || currentLevel > MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            }

            if (currentLevel == MaximumLevel) return 0;

            return Mathf.RoundToInt(12f * Mathf.Pow(currentLevel, 1.5f) + 12f);
        }

        public int AddExperience(CharacterInstance character, int experienceAmount)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (experienceAmount <= 0 || character.Level >= MaximumLevel) return 0;

            character.CurrentExperience += experienceAmount;

            var levelsGained = 0;

            while (character.Level < MaximumLevel)
            {
                var requiredExperience = GetExperienceRequiredForNextLevel(character.Level);

                if (character.CurrentExperience < requiredExperience) break;

                character.CurrentExperience -= requiredExperience;
                character.Level++;
                character.AvailableSkillPoints++;
                levelsGained++;
            }

            if (character.Level == MaximumLevel) character.CurrentExperience = 0;
            if (levelsGained > 0) growthSystem.Apply(character, grantCurrentHealthGrowth: true);

            return levelsGained;
        }
    } 
}
