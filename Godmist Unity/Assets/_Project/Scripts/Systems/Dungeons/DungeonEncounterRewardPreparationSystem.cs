using System;
using System.Collections.Generic;
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
                lootLevel: dungeon.DungeonLevel);

            var lootCatalog = dungeon.Definition?.LootTableCatalog;

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
                });

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

                    var rewards = rewardSystem.RollEncounterRewards(
                        encounterInstance.Definition);

                    dungeon.TryStorePreRolledRewards(
                        encounterInstance.InstanceId,
                        rewards);
                }
            }
        }
    }
}
