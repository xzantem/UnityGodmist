using System;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;

namespace Godmist.Systems.Dungeons
{
    public class DungeonEncounterRewardPreparationSystem
    {
        public void Prepare(DungeonInstance dungeon)
        {
            if (dungeon == null) return;

            var random = new Random(dungeon.Seed);

            var lootTableSystem = new LootTableSystem(
                () => (float)random.NextDouble());

            var rewardSystem = new CombatRewardSystem(
                lootTableSystem: lootTableSystem);

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