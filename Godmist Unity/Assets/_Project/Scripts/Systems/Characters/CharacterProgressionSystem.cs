using System;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Data.Configuration;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterProgressionSystem
    {
        public static int MinimumLevel => GameDefinesRuntime.GetInt(
            GameDefineIds.MinimumCharacterLevel, 1);
        public static int MaximumLevel => GameDefinesRuntime.GetInt(
            GameDefineIds.MaximumCharacterLevel, 50);

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

            var coefficient = GameDefinesRuntime.GetFloat(
                GameDefineIds.ExperienceFormulaCoefficient, 1200f);
            var exponent = GameDefinesRuntime.GetFloat(
                GameDefineIds.ExperienceFormulaExponent, 1.5f);
            var constant = GameDefinesRuntime.GetFloat(
                GameDefineIds.ExperienceFormulaConstant, 1200f);
            return (int)Math.Round(coefficient * Math.Pow(currentLevel, exponent) + constant);
        }

        public static double GetEnemyExperienceBase(int enemyLevel)
        {
            var level = Math.Clamp(enemyLevel, 1, 50);
            return 6d * (247 - 2 * level) * (Math.Pow(level, 1.5d) + 1d) / 49d;
        }

        public static int GetEnemyExperience(int enemyLevel, int characterLevel)
        {
            var difference = Math.Clamp(characterLevel - enemyLevel, 0, 6);
            return (int)Math.Round(GetEnemyExperienceBase(enemyLevel) * (1d - .15d * difference));
        }

        public static int GetStandardMissionExperience(int missionLevel)
        {
            // Round once, after multiplying the unrounded enemy reward.
            return (int)Math.Round(20d * GetEnemyExperienceBase(missionLevel));
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
                character.AvailableSkillPoints += GetSkillPointsGrantedOnLevelUp(character.Level);
                levelsGained++;
            }

            if (character.Level == MaximumLevel) character.CurrentExperience = 0;
            if (levelsGained > 0) growthSystem.Apply(character, grantCurrentHealthGrowth: true);
            character.NotifyExperienceGained(experienceAmount, levelsGained);

            return levelsGained;
        }

        public bool TrySetLevel(
            CharacterInstance character,
            int targetLevel,
            out int previousLevel)
        {
            previousLevel = character?.Level ?? 0;

            if (character == null
                || targetLevel < MinimumLevel
                || targetLevel > MaximumLevel)
            {
                return false;
            }

            if (character.Level == targetLevel)
            {
                character.CurrentExperience = 0;
                growthSystem.Apply(character, grantCurrentHealthGrowth: false);
                return true;
            }

            var levelDifference = targetLevel - character.Level;
            if (levelDifference > 0)
            {
                for (var level = character.Level + 1;
                     level <= targetLevel;
                     level++)
                {
                    character.AvailableSkillPoints +=
                        GetSkillPointsGrantedOnLevelUp(level);
                }
            }
            else
            {
                for (var level = targetLevel + 1;
                     level <= character.Level;
                     level++)
                {
                    character.AvailableSkillPoints = Math.Max(
                        0,
                        character.AvailableSkillPoints
                            - GetSkillPointsGrantedOnLevelUp(level));
                }
            }

            character.Level = targetLevel;
            character.CurrentExperience = 0;
            growthSystem.Apply(character, grantCurrentHealthGrowth: levelDifference > 0);
            if (character.Stats != null)
            {
                character.CurrentHealth = Mathf.Clamp(
                    character.CurrentHealth,
                    0,
                    character.MaximumHealth);
            }
            return true;
        }

        public static int GetSkillPointsGrantedOnLevelUp(int newLevel) => newLevel == 2
            ? GameDefinesRuntime.GetInt(GameDefineIds.SkillPointsAtLevelTwo, 2)
            : GameDefinesRuntime.GetInt(GameDefineIds.SkillPointsAtOtherLevels, 1);


    } 
}
