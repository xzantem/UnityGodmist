using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CharacterProgressionSystemTests
    {
        private CharacterProgressionSystem progressionSystem;

        [SetUp]
        public void SetUp()
        {
            progressionSystem = new CharacterProgressionSystem();
        }

        [Test]
        public void GetExperienceRequiredForNextLevel_ReturnsExpectedValueForLevelOne()
        {
            var requiredExperience =
                progressionSystem.GetExperienceRequiredForNextLevel(1);

            Assert.That(requiredExperience, Is.EqualTo(24));
        }

        [Test]
        public void AddExperience_BelowThreshold_AddsExperienceWithoutLevelUp()
        {
            var character = CreateLevelOneCharacter();

            var levelsGained = progressionSystem.AddExperience(character, 23);

            Assert.That(levelsGained, Is.EqualTo(0));
            Assert.That(character.Level, Is.EqualTo(1));
            Assert.That(character.CurrentExperience, Is.EqualTo(23));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(0));
        }

        [Test]
        public void AddExperience_AtThreshold_LevelsUpAndGrantsTwoStartingSkillPoints()
        {
            var character = CreateLevelOneCharacter();

            var levelsGained = progressionSystem.AddExperience(character, 24);

            Assert.That(levelsGained, Is.EqualTo(1));
            Assert.That(character.Level, Is.EqualTo(2));
            Assert.That(character.CurrentExperience, Is.EqualTo(0));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(2));
        }

        [Test]
        public void AddExperience_CanGainMultipleLevelsAndKeepOverflow()
        {
            var character = CreateLevelOneCharacter();

            var levelsGained = progressionSystem.AddExperience(character, 75);

            Assert.That(levelsGained, Is.EqualTo(2));
            Assert.That(character.Level, Is.EqualTo(3));
            Assert.That(character.CurrentExperience, Is.EqualTo(5));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(3));
        }

        [Test]
        public void AddExperience_AtMaximumLevel_DoesNotChangeProgression()
        {
            var character = new CharacterInstance
            {
                Level = CharacterProgressionSystem.MaximumLevel,
                CurrentExperience = 0,
                AvailableSkillPoints = 50
            };

            var levelsGained = progressionSystem.AddExperience(character, 1000);

            Assert.That(levelsGained, Is.EqualTo(0));
            Assert.That(character.Level, Is.EqualTo(50));
            Assert.That(character.CurrentExperience, Is.EqualTo(0));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(50));
        }

        [Test]
        public void AddExperience_FromLevelOneToMaximum_GrantsFiftySkillPoints()
        {
            var character = CreateLevelOneCharacter();

            while (character.Level < CharacterProgressionSystem.MaximumLevel)
            {
                progressionSystem.AddExperience(
                    character,
                    progressionSystem.GetExperienceRequiredForNextLevel(character.Level));
            }

            Assert.That(character.Level, Is.EqualTo(50));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(50));
        }

        [Test]
        public void AddExperience_NonPositiveAmount_DoesNotChangeCharacter()
        {
            var character = CreateLevelOneCharacter();

            var levelsGained = progressionSystem.AddExperience(character, 0);

            Assert.That(levelsGained, Is.EqualTo(0));
            Assert.That(character.Level, Is.EqualTo(1));
            Assert.That(character.CurrentExperience, Is.EqualTo(0));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(0));
        }

        [Test]
        public void TryMigrateLegacySkillPointTotal_AddsTheMissingStartingPointOnce()
        {
            var character = new CharacterInstance
            {
                Level = 50,
                AvailableSkillPoints = 49
            };

            Assert.That(
                CharacterProgressionSystem.TryMigrateLegacySkillPointTotal(character),
                Is.True);
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(50));
            Assert.That(
                CharacterProgressionSystem.TryMigrateLegacySkillPointTotal(character),
                Is.False);
        }

        private static CharacterInstance CreateLevelOneCharacter()
        {
            return new CharacterInstance
            {
                Level = 1,
                CurrentExperience = 0,
                AvailableSkillPoints = 0
            };
        }
    }
}
