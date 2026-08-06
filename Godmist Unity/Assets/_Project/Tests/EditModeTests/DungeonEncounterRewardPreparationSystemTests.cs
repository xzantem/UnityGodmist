using System.Collections.Generic;
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
        public void Prepare_SameEncounterInstance_DoesNotRollRewardsAgain()
        {
            var encounterDefinition = ScriptableObject.CreateInstance<
                EncounterDefinition>();

            SetAutoProperty(
                encounterDefinition,
                "Id",
                "encounter.test");

            var dungeonDefinition = ScriptableObject.CreateInstance<
                DungeonDefinition>();

            SetAutoProperty(
                dungeonDefinition,
                "Id",
                "dungeon.test");

            var dungeon = new DungeonInstance(
                dungeonDefinition,
                seed: 12345);

            var floor = new DungeonFloorInstance(0);

            floor.TryAddRoom(new DungeonRoomInstance(
                "room.normal",
                Core.Enums.DungeonRoomType.Normal));

            var encounterInstance = new DungeonEncounterInstance(
                "dungeon.test.floor_0.room.normal.encounter",
                encounterDefinition,
                "room.normal",
                new RectInt(0, 0, 8, 8));

            floor.TryAddEncounter(encounterInstance);
            dungeon.TryAddFloor(floor);

            var preparationSystem =
                new DungeonEncounterRewardPreparationSystem();

            preparationSystem.Prepare(dungeon);

            var firstLookupSucceeded = dungeon.TryGetPreRolledRewards(
                encounterInstance.InstanceId,
                out var firstRewards);

            preparationSystem.Prepare(dungeon);

            var secondLookupSucceeded = dungeon.TryGetPreRolledRewards(
                encounterInstance.InstanceId,
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