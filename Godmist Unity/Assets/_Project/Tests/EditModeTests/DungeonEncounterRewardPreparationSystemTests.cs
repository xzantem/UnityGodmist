using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Types;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
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

        [Test]
        public void Prepare_BossUsesBossLootTableForBossEnemy()
        {
            var bossItemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            SetAutoProperty(bossItemDefinition, "Id", "item.boss.lootbag");

            var bossLootTable = ScriptableObject.CreateInstance<LootTableData>();
            bossLootTable.Entries.Add(new LootEntryData());
            SetAutoProperty(
                bossLootTable.Entries[0],
                "ItemDefinition",
                bossItemDefinition);
            SetAutoProperty(bossLootTable.Entries[0], "DropChance", 1f);
            SetAutoProperty(
                bossLootTable.Entries[0],
                "Amount",
                new IntRange(1, 1));

            var lootCatalog = ScriptableObject.CreateInstance<
                DungeonLootTableCatalog>();
            var catalogEntry = new DungeonLootTableEntryData();
            SetAutoProperty(catalogEntry, "BossLootTable", bossLootTable);
            lootCatalog.Entries.Add(catalogEntry);

            var bossCharacter = ScriptableObject.CreateInstance<
                Godmist.Data.Characters.CharacterDefinition>();
            SetAutoProperty(bossCharacter, "Id", "character.boss");

            var encounterDefinition = ScriptableObject.CreateInstance<
                EncounterDefinition>();
            SetAutoProperty(encounterDefinition, "Id", "encounter.boss");
            var bossEnemy = new EncounterEnemyData();
            SetAutoProperty(bossEnemy, "InstanceId", "enemy.boss");
            SetAutoProperty(bossEnemy, "CharacterDefinition", bossCharacter);
            SetAutoProperty(
                encounterDefinition,
                "Enemies",
                new List<EncounterEnemyData> { bossEnemy });

            var dungeonDefinition = ScriptableObject.CreateInstance<
                DungeonDefinition>();
            SetAutoProperty(dungeonDefinition, "Id", "dungeon.boss");
            SetAutoProperty(dungeonDefinition, "LootTableCatalog", lootCatalog);
            SetAutoProperty(
                dungeonDefinition,
                "BossEncounterDefinition",
                encounterDefinition);

            var dungeon = new DungeonInstance(dungeonDefinition, 12345);
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance(
                "room.final",
                Core.Enums.DungeonRoomType.Final));
            floor.TryAddEncounter(new DungeonEncounterInstance(
                "dungeon.boss.floor_0.room.final.boss",
                encounterDefinition,
                "room.final",
                new UnityEngine.RectInt(0, 0, 1, 1)));
            dungeon.TryAddFloor(floor);

            new DungeonEncounterRewardPreparationSystem().Prepare(dungeon);

            Assert.That(
                dungeon.TryGetPreRolledRewards(
                    "dungeon.boss.floor_0.room.final.boss",
                    out var rewards),
                Is.True);
            Assert.That(rewards.EnemyRewards, Has.Count.EqualTo(1));
            Assert.That(rewards.EnemyRewards[0].ExperienceLevel, Is.EqualTo(dungeon.DungeonLevel));
            Assert.That(rewards.EnemyRewards[0].ExperiencePerActiveCharacter,
                Is.EqualTo(Godmist.Systems.Characters.CharacterProgressionSystem.GetEnemyExperience(dungeon.DungeonLevel, dungeon.DungeonLevel)));
            Assert.That(rewards.EnemyRewards[0].Items, Has.Count.EqualTo(1));
            Assert.That(
                rewards.EnemyRewards[0].Items[0].Definition,
                Is.SameAs(bossItemDefinition));
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
