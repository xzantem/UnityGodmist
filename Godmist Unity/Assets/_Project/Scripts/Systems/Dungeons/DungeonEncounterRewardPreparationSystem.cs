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
            if (dungeon?.Definition?.EncounterDefinitions == null) return;

            var random = new Random(dungeon.Seed);
            var lootTableSystem = new LootTableSystem(
                () => (float)random.NextDouble());

            var rewardSystem = new CombatRewardSystem(
                lootTableSystem: lootTableSystem);

            foreach (var encounter in dungeon.Definition.EncounterDefinitions)
            {
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.Id))
                {
                    continue;
                }

                if (dungeon.TryGetPreRolledRewards(encounter.Id, out _))
                {
                    continue;
                }

                var rewards = rewardSystem.RollEncounterRewards(encounter);

                dungeon.TryStorePreRolledRewards(encounter.Id, rewards);
            }
        }
    }
}