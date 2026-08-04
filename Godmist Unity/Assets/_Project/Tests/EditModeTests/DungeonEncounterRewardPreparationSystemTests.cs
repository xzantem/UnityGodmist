using System.Reflection;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonEncounterRewardPreparationSystemTests
    {
        [Test]
        public void Prepare_SameDungeonAndEncounter_DoesNotRollRewardsAgain()
        {
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            SetAutoProperty(encounter, "Id", "encounter.test");

            var dungeonDefinition = ScriptableObject.CreateInstance<DungeonDefinition>();
            SetAutoProperty(dungeonDefinition, "Id", "dungeon.test");

            dungeonDefinition.EncounterDefinitions.Add(encounter);

            var dungeon = new DungeonInstance(dungeonDefinition, seed: 12345);
            var preparationSystem = new DungeonEncounterRewardPreparationSystem();

            preparationSystem.Prepare(dungeon);

            var firstLookupSucceeded = dungeon.TryGetPreRolledRewards(
                encounter.Id,
                out var firstRewards);

            preparationSystem.Prepare(dungeon);

            var secondLookupSucceeded = dungeon.TryGetPreRolledRewards(
                encounter.Id,
                out var secondRewards);

            Assert.That(firstLookupSucceeded, Is.True);
            Assert.That(secondLookupSucceeded, Is.True);
            Assert.That(secondRewards, Is.SameAs(firstRewards));
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}