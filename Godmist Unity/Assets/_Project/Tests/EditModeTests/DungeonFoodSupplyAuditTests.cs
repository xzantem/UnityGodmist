using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonFoodSupplyAuditTests
    {
        [TestCase("forest", 10)] [TestCase("catacombs", 10)] [TestCase("forest", 20)] [TestCase("catacombs", 20)]
        [Explicit("Read-only full-clear baseline on fixed seeds.")]
        public void Before(string biome, int level) => Report("before", biome, level);
        [TestCase("forest", 10)] [TestCase("catacombs", 10)] [TestCase("forest", 20)] [TestCase("catacombs", 20)]
        [Explicit("Read-only supply measurement after approved changes.")]
        public void After(string biome, int level) => Report("after", biome, level);
        static void Report(string phase, string biome, int level)
        {
            var output = "../LocalHandoffs/dungeon-food-recovery-20261024/" + phase; Directory.CreateDirectory(output);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + biome + ".expedition");
            var rows = new List<string> { "biome,level,length,seed,floors,rooms,encounters,enemies,chests,enemyFood,chestFood,campFraction" };
            foreach (var length in level == 20 ? new[] { DungeonLength.Normal } : new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
            for (var n = 0; n < 4; n++)
            {
                var seed = 241026 + n;
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, level, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                new DungeonObjectPlacementSystem().Place(dungeon, catalog.EquipmentPartDefinitions);
                new DungeonEncounterRewardPreparationSystem().Prepare(dungeon, catalog.EquipmentPartDefinitions);
                int Food(ItemInstance item) => item is LootBagItemInstance bag ? bag.Contents.Sum(Food) : item?.Definition?.Id == "item.consumable.ration" ? 1 : 0;
                var encounters = dungeon.Floors.SelectMany(f => f.Encounters).Where(e => !e.IsReinforcement).ToArray();
                var enemyFood = 0;
                foreach (var encounter in encounters)
                {
                    Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId, out var rewards), Is.True);
                    enemyFood += rewards.EnemyRewards.SelectMany(e => e.Items).Sum(Food) + rewards.Items.Sum(Food);
                }
                var chests = dungeon.Floors.SelectMany(f => f.Objects).OfType<Godmist.Runtime.Dungeons.ChestDungeonObjectInstance>().ToArray();
                var chestFood = chests.Sum(c => c.Items.Sum(Food));
                rows.Add($"{biome},{level},{length},{seed},{dungeon.Floors.Count},{dungeon.Floors.Sum(f => f.Rooms.Count)},{encounters.Length},{encounters.Sum(e => e.Definition.Enemies.Count)},{chests.Length},{enemyFood},{chestFood},{DungeonCampService.RecoveryFraction.ToString(CultureInfo.InvariantCulture)}");
            }
            File.WriteAllLines(output + "/" + biome + "-" + level + ".csv", rows);
        }
    }
}
