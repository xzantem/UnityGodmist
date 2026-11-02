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
        public void RequiredExperience_MatchesFormulaThroughLevel49_AndStopsAt50()
        {
            for (var level = 1; level < 50; level++)
                Assert.That(progressionSystem.GetExperienceRequiredForNextLevel(level),
                    Is.EqualTo((int)System.Math.Round(1200d * System.Math.Pow(level, 1.5d) + 1200d)));
            Assert.That(progressionSystem.GetExperienceRequiredForNextLevel(50), Is.Zero);
        }

        [TestCase(1, 1, 60)]
        [TestCase(1, 2, 51)]
        [TestCase(1, 7, 6)]
        [TestCase(1, 50, 6)]
        [TestCase(2, 1, 114)]
        public void EnemyExperience_AppliesCappedPenaltyOnlyForWeakerEnemies(int enemy, int player, int expected)
        {
            Assert.That(CharacterProgressionSystem.GetEnemyExperience(enemy, player), Is.EqualTo(expected));
        }

        [TestCase(1, .025, .5)]
        [TestCase(25, .0201020408163, .4020408163265)]
        [TestCase(50, .015, .3)]
        public void ExperienceCurve_HasAgreedRewardShares(int level, double enemyShare, double missionShare)
        {
            var threshold = 1200d * (System.Math.Pow(level, 1.5d) + 1d);
            Assert.That(CharacterProgressionSystem.GetEnemyExperienceBase(level) / threshold, Is.EqualTo(enemyShare).Within(.000001));
            Assert.That(CharacterProgressionSystem.GetStandardMissionExperience(level) / threshold, Is.EqualTo(missionShare).Within(.00001));
        }

        [Test]
        public void GetExperienceRequiredForNextLevel_ReturnsExpectedValueForLevelOne()
        {
            var requiredExperience =
                progressionSystem.GetExperienceRequiredForNextLevel(1);

            Assert.That(requiredExperience, Is.EqualTo(2400));
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

            var levelsGained = progressionSystem.AddExperience(character, 2400);

            Assert.That(levelsGained, Is.EqualTo(1));
            Assert.That(character.Level, Is.EqualTo(2));
            Assert.That(character.CurrentExperience, Is.EqualTo(0));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(2));
        }

        [Test]
        public void AddExperience_CanGainMultipleLevelsAndKeepOverflow()
        {
            var character = CreateLevelOneCharacter();

            var levelsGained = progressionSystem.AddExperience(character, 6999);

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
                Godmist.Systems.SaveLoad.LegacySaveMigrations.RecoverSkillPointTotal(character),
                Is.True);
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(50));
            Assert.That(
                Godmist.Systems.SaveLoad.LegacySaveMigrations.RecoverSkillPointTotal(character),
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
