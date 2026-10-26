using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Dungeons;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class AlchemySupplyAuditTests
    {
        internal string OutputOverride;
        string Output => OutputOverride ?? "../LocalHandoffs/alchemy-supply-20261024";
        [TestCase("catacombs", 10)] [TestCase("catacombs", 20)]
        [TestCase("forest", 10)] [TestCase("forest", 20)]
        [Explicit("Read-only supply audit: generated full clears and detached merchant stock; no content edits or live saves.")]
        public void ReportFullClearSupply(string biome, int level)
        {
            Directory.CreateDirectory(Output);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + biome + ".expedition");
            var alchemist = AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_Alchemist.asset");
            var ingredientIds = alchemist.GetRole<AlchemistRoleData>().IngredientOptions.Where(o => o?.Material != null).Select(o => o.Material.Id).ToHashSet();
            var rows = new List<string> { "biome,level,length,seed,floors,rooms,encounters,enemies,source,item,name,quantity,alchemyMaterial,refillIngredient" };
            var runs = new List<string> { "biome,level,length,seed,floors,rooms,encounters,enemies,bonesDirect,bonesBag,bonesChest,ingredientsDirect,ingredientsBag,ingredientsChest,resourceBags" };
            foreach (var length in new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
            for (var n = 0; n < 12; n++)
            {
                var seed = 241026 + n;
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, level, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                new DungeonObjectPlacementSystem().Place(dungeon, catalog.EquipmentPartDefinitions);
                new DungeonEncounterRewardPreparationSystem().Prepare(dungeon, catalog.EquipmentPartDefinitions);
                var counts = new Dictionary<(string Source, string Item), (int Count, ItemInstance Item)>();
                var bags = 0;
                void Count(ItemInstance item, string source)
                {
                    if (item is LootBagItemInstance bag)
                    {
                        if (item.Definition.Id.EndsWith(".resources", StringComparison.Ordinal)) bags++;
                        foreach (var child in bag.Contents) Count(child, source == "direct" ? "bag" : source);
                        return;
                    }
                    if (item?.Definition == null) return;
                    var key = (source, item.Definition.Id);
                    counts.TryGetValue(key, out var prior); counts[key] = (prior.Count + 1, item);
                }
                var encounters = dungeon.Floors.SelectMany(f => f.Encounters).Where(e => !e.IsReinforcement).ToArray();
                foreach (var encounter in encounters)
                {
                    Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId, out var rewards), Is.True);
                    foreach (var item in rewards.EnemyRewards.SelectMany(e => e.Items)) Count(item, "direct");
                    foreach (var item in rewards.Items) Count(item, "direct");
                }
                foreach (var chest in dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>())
                    foreach (var item in chest.Items) Count(item, "chest");
                var prefix = $"{biome},{level},{length},{seed},{dungeon.Floors.Count},{dungeon.Floors.Sum(f => f.Rooms.Count)},{encounters.Length},{encounters.Sum(e => e.Definition.Enemies.Count)}";
                foreach (var pair in counts.OrderBy(p => p.Key.Source).ThenBy(p => p.Key.Item))
                    rows.Add($"{prefix},{pair.Key.Source},{pair.Key.Item},{pair.Value.Item.Definition.DisplayName.Replace(',', ';')},{pair.Value.Count},{pair.Value.Item.Definition.MaterialDiscipline == MaterialDiscipline.Alchemy},{ingredientIds.Contains(pair.Key.Item)}");
                int Bones(string source) => counts.Where(c => c.Key.Source == source && c.Key.Item == "item.alchemy.skeleton_bone").Sum(c => c.Value.Count);
                int Ingredients(string source) => counts.Where(c => c.Key.Source == source && ingredientIds.Contains(c.Key.Item)).Sum(c => c.Value.Count);
                runs.Add($"{prefix},{Bones("direct")},{Bones("bag")},{Bones("chest")},{Ingredients("direct")},{Ingredients("bag")},{Ingredients("chest")},{bags}");
            }
            File.WriteAllLines(Path.Combine(Output, biome + "-" + level + "-items.csv"), rows);
            File.WriteAllLines(Path.Combine(Output, biome + "-" + level + "-clears.csv"), runs);
            var stock = new List<string> { "level,seed,item,name,quantity,alchemyMaterial,refillIngredient" };
            for (var n = 0; n < 12; n++)
            {
                var npc = new NpcInstance { NpcId = alchemist.Id, Definition = alchemist, ShopInventory = new ShopInventory() };
                new TraderStockService(new Random(241026 + n)).Refresh(npc, level, rotate: false, catalog: catalog);
                foreach (var group in npc.ShopInventory.Items.Where(i => i?.Definition != null).GroupBy(i => i.Definition.Id))
                {
                    var item = group.First();
                    stock.Add($"{level},{241026 + n},{group.Key},{item.Definition.DisplayName.Replace(',', ';')},{group.Count()},{item.Definition.MaterialDiscipline == MaterialDiscipline.Alchemy},{ingredientIds.Contains(group.Key)}");
                }
            }
            File.WriteAllLines(Path.Combine(Output, "stock-" + level + ".csv"), stock);
        }
    }
}
