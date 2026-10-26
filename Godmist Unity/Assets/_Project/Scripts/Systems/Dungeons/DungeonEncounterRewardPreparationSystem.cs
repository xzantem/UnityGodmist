using System;
using System.Collections.Generic;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;

namespace Godmist.Systems.Dungeons
{
    public class DungeonEncounterRewardPreparationSystem
    {
        public void Prepare(
            DungeonInstance dungeon,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null)
        {
            if (dungeon == null) return;

            var random = new Random(dungeon.Seed);

            var lootTableSystem = new LootTableSystem(
                random,
                equipmentParts,
                lootLevel: dungeon.DungeonLevel, dropChanceBonus: dungeon.ItemDropChanceBonus);

            var lootCatalog = dungeon.Definition?.LootTableCatalog;
            var bossDefinition = dungeon.Definition?.BossEncounterDefinition;

            var rewardSystem = new CombatRewardSystem(
                lootTableSystem: lootTableSystem,
                lootTableResolver: characterDefinition =>
                {
                    if (lootCatalog != null
                        && lootCatalog.TryGetEnemyLootTable(
                            dungeon.Definition.Theme,
                            dungeon.DungeonLevel,
                            out var themedTable))
                    {
                        return themedTable;
                    }

                    return characterDefinition?.LootTable;
                },
                goldRandom: random.NextDouble);

            foreach (var floor in dungeon.Floors)
            {
                foreach (var encounterInstance in floor.Encounters)
                {
                    if (encounterInstance == null
                        || string.IsNullOrWhiteSpace(encounterInstance.InstanceId)
                        || encounterInstance.Definition == null)
                    {
                        continue;
                    }

                    if (dungeon.TryGetPreRolledRewards(
                            encounterInstance.InstanceId,
                            out _))
                    {
                        continue;
                    }

                    if (encounterInstance.IsReinforcement)
                    {
                        dungeon.TryStorePreRolledRewards(encounterInstance.InstanceId, new Godmist.Runtime.Combat.CombatRewardResult());
                        continue;
                    }

                    var rewards = rewardSystem.RollEncounterRewards(
                        encounterInstance.Definition,
                        enemyData => ResolveLootTable(
                            dungeon,
                            lootCatalog,
                            bossDefinition,
                            encounterInstance,
                            enemyData), dungeon.DungeonLevel, dungeon);

                    // Theme bags are common to the dungeon, while bones and brains
                    // belong to specific enemy species. Both must be pre-rolled so
                    // saving and reloading cannot change the reward.
                    foreach (var enemyReward in rewards.EnemyRewards)
                    {
                        var enemy = encounterInstance.Definition.Enemies.Find(data =>
                            data != null && data.InstanceId == enemyReward.EnemyInstanceId);
                        var speciesLoot = enemy?.CharacterDefinition?.LootTable;
                        if (speciesLoot != null
                            && lootCatalog != null
                            && lootCatalog.TryGetEnemyLootTable(dungeon.Definition.Theme,
                                dungeon.DungeonLevel, out _))
                            enemyReward.Items.AddRange(lootTableSystem.RollDirectLoot(speciesLoot));
                    }

                    dungeon.TryStorePreRolledRewards(
                        encounterInstance.InstanceId,
                        rewards);
                }
            }
        }

        private static LootTableData ResolveLootTable(
            DungeonInstance dungeon,
            DungeonLootTableCatalog lootCatalog,
            EncounterDefinition bossDefinition,
            DungeonEncounterInstance encounterInstance,
            EncounterEnemyData enemyData)
        {
            if (lootCatalog != null
                && encounterInstance?.Definition == bossDefinition
                && bossDefinition.Enemies != null
                && bossDefinition.BossEnemyIndex >= 0
                && bossDefinition.BossEnemyIndex < bossDefinition.Enemies.Count
                && ReferenceEquals(
                    bossDefinition.Enemies[bossDefinition.BossEnemyIndex],
                    enemyData)
                && lootCatalog.TryGetBossLootTable(
                    dungeon.Definition.Theme,
                    dungeon.DungeonLevel,
                    out var bossLootTable))
            {
                return bossLootTable;
            }

            if (lootCatalog != null
                && lootCatalog.TryGetEnemyLootTable(
                    dungeon.Definition.Theme,
                    dungeon.DungeonLevel,
                    out var enemyLootTable))
            {
                return enemyLootTable;
            }

            return null;
        }
    }
}
