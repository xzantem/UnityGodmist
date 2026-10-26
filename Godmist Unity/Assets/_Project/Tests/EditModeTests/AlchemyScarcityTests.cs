using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class AlchemyScarcityTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        const string Output = "../LocalHandoffs/alchemy-scarcity-20261024";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static GameContentCatalog Catalog => Load<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
        static NpcDefinition Alchemist => Load<NpcDefinition>(Root + "NPC/ND_Arungard_Alchemist.asset");
        static HashSet<string> Ingredients => Alchemist.GetRole<AlchemistRoleData>().IngredientOptions.Select(i => i.Material.Id).ToHashSet();
        static string OtherPools(LootBagItemDefinition bag, string[] ids) => string.Join("\n", bag.Brackets.SelectMany(b => b.Pools
            .Where(p => p.Entries.All(e => !ids.Contains(e.ItemDefinition.Id)))
            .Select(p => b.Bracket + ":" + JsonUtility.ToJson(p))));
        [Test, Explicit("Apply the approved alchemy scarcity values through Unity Editor API only.")]
        public void ApplyContentPreservingOtherRewards()
        {
            var forest = Load<LootBagItemDefinition>(AlchemyScarcitySetup.ForestBag);
            var catacombs = Load<LootBagItemDefinition>(AlchemyScarcitySetup.CatacombsBag);
            var forestOther = OtherPools(forest, AlchemyScarcitySetup.ForestIngredients);
            var catacombsOther = OtherPools(catacombs, AlchemyScarcitySetup.CatacombsIngredients);
            AlchemyScarcitySetup.Apply();
            Assert.That(OtherPools(forest, AlchemyScarcitySetup.ForestIngredients), Is.EqualTo(forestOther));
            Assert.That(OtherPools(catacombs, AlchemyScarcitySetup.CatacombsIngredients), Is.EqualTo(catacombsOther));
            Assert.That(Load<LootTableData>(AlchemyScarcitySetup.SkeletonTable).Entries.Single().DropChance, Is.EqualTo(.05f));
            Assert.That(Load<LootTableData>(AlchemyScarcitySetup.UndeadTable).Entries.Single().DropChance, Is.EqualTo(.1f));
        }
        [TestCase(AlchemyScarcitySetup.ForestBag, 2)] [TestCase(AlchemyScarcitySetup.CatacombsBag, 4)]
        public void EveryBracketHasOneTwentyPercentPoolWithOneItem(string path, int kinds)
        {
            var bag = Load<LootBagItemDefinition>(path);
            var ids = path == AlchemyScarcitySetup.ForestBag ? AlchemyScarcitySetup.ForestIngredients : AlchemyScarcitySetup.CatacombsIngredients;
            foreach (var bracket in bag.Brackets)
            {
                var pools = bracket.Pools.Where(p => p.Entries.Any(e => ids.Contains(e.ItemDefinition.Id))).ToArray();
                Assert.That(pools.Length, Is.EqualTo(1)); Assert.That(pools[0].DropChance, Is.EqualTo(.2f));
                Assert.That(pools[0].Entries.Count, Is.EqualTo(kinds));
                Assert.That(pools[0].Entries.All(e => e.Amount.Min == 1 && e.Amount.Max == 1), Is.True);
            }
        }
        [TestCase(10)] [TestCase(20)]
        public void FreshAndRetainedMerchantStockContainSixSingleIngredients(int level)
        {
            var npc = new NpcInstance { NpcId = Alchemist.Id, Definition = Alchemist, ShopInventory = new ShopInventory() };
            var stock = new TraderStockService(new System.Random(241026)); stock.Refresh(npc, level, false, Catalog);
            Assert.That(npc.ShopInventory.Items.Count(i => Ingredients.Contains(i.Definition.Id)), Is.EqualTo(6));
            var chosen = npc.ShopInventory.Items.Where(i => Ingredients.Contains(i.Definition.Id)).ToArray();
            var factory = new ItemInstanceFactory();
            foreach (var item in chosen) for (var i = 0; i < 4; i++) npc.ShopInventory.Items.Add(factory.Create(item.Definition));
            Assert.That(Catalog.TryGetItem("item.alchemy.skeleton_bone", out var bone), Is.True);
            var buyback = new BuybackEntry(factory.Create(bone), 10); npc.ShopInventory.BuybackStock.Add(buyback);
            var unrelated = npc.ShopInventory.Items.Where(i => !Ingredients.Contains(i.Definition.Id)).ToArray();
            Assert.That(TraderStockService.EnforceAlchemyIngredientLimits(npc), Is.EqualTo(24));
            Assert.That(chosen.All(i => npc.ShopInventory.Items.Contains(i)), Is.True);
            Assert.That(unrelated.All(i => npc.ShopInventory.Items.Contains(i)), Is.True);
            Assert.That(npc.ShopInventory.BuybackStock.Single(), Is.SameAs(buyback));
            Assert.That(TraderStockService.EnforceAlchemyIngredientLimits(npc), Is.Zero);
            stock.Refresh(npc, level, true, Catalog);
            Assert.That(npc.ShopInventory.Items.Count(i => Ingredients.Contains(i.Definition.Id)), Is.EqualTo(6));
        }
        [TestCase(false)] [TestCase(true)]
        public void LegacySaveTrimsOnlyMerchantIngredientsWithoutRefillOrPlayerLoss(bool depleted)
        {
            var npc = new NpcInstanceFactory().Create(Alchemist, 10, Catalog); var factory = new ItemInstanceFactory();
            var chosen = npc.ShopInventory.Items.Where(i => Ingredients.Contains(i.Definition.Id)).ToArray();
            foreach (var item in chosen) for (var i = 0; i < 4; i++) npc.ShopInventory.Items.Add(factory.Create(item.Definition));
            if (depleted) npc.ShopInventory.Items.RemoveAll(i => i.Definition.Id == chosen[0].Definition.Id);
            var game = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstanceFactory().Create(Load<CharacterDefinition>(Root + "Characters/CD_Wladimir_Test.asset"));
            game.TryRegisterCharacter(character); game.TryAddCharacterToActiveParty(character.CharacterId); game.TryRegisterNpc(npc);
            Assert.That(Catalog.TryGetItem("item.alchemy.skeleton_bone", out var bone), Is.True);
            for (var i = 0; i < 17; i++) character.Inventory.Items.Add(factory.Create(bone));
            npc.ShopInventory.BuybackStock.Add(new BuybackEntry(factory.Create(bone), 10));
            var bag = new LootTableSystem(new System.Random(42), Catalog.EquipmentPartDefinitions, lootLevel: 10)
                .CreateLootBag(Load<LootBagItemDefinition>(AlchemyScarcitySetup.CatacombsBag));
            // An already obtained bag may exceed today's drop limits and must stay intact.
            for (var i = 0; i < 9; i++) bag.Contents.Add(factory.Create(bone));
            character.Inventory.Items.Add(bag); var contents = bag.Contents.Select(i => i.InstanceId).ToArray();
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Alchemy scarcity legacy");
            var oldShopCount = snapshot.Npcs.Single().ShopItemInstanceIds.Count;
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, Catalog, out var restored), Is.True);
            var restoredNpc = restored.NpcsById[npc.NpcId];
            Assert.That(restoredNpc.ShopInventory.Items.Count(i => Ingredients.Contains(i.Definition.Id)), Is.EqualTo(depleted ? 5 : 6));
            Assert.That(restoredNpc.ShopInventory.BuybackStock.Single().Item.Definition.Id, Is.EqualTo(bone.Id));
            var inventory = restored.CharactersById[character.CharacterId].Inventory;
            Assert.That(inventory.Items.Count(i => i.Definition.Id == bone.Id), Is.EqualTo(17));
            Assert.That(inventory.Items.OfType<LootBagItemInstance>().Single().Contents.Select(i => i.InstanceId), Is.EqualTo(contents));
            Assert.That(snapshot.Npcs.Single().ShopItemInstanceIds.Count, Is.EqualTo(oldShopCount));
        }
        [TestCase("catacombs", 10)] [TestCase("catacombs", 20)]
        [TestCase("forest", 10)] [TestCase("forest", 20)]
        [Explicit("Repeat the same 144 full clears after approved content edits; preserve before reports.")]
        public void CompareFullClearsOnTheSameSeeds(string biome, int level)
        {
            var directory = Output + "/after-results";
            new AlchemySupplyAuditTests { OutputOverride = directory }.ReportFullClearSupply(biome, level);
            var file = biome + "-" + level + "-clears.csv";
            var before = File.ReadAllLines(Output + "/before-results/" + file).Skip(1).Select(s => s.Split(',')).ToArray();
            var after = File.ReadAllLines(directory + "/" + file).Skip(1).Select(s => s.Split(',')).ToArray();
            Assert.That(after.Length, Is.EqualTo(36));
            for (var i = 0; i < before.Length; i++)
                Assert.That(after[i].Take(8), Is.EqualTo(before[i].Take(8)), "Same biome/level/length/seed and unchanged floor, room, encounter and enemy counts.");
            int IngredientsTotal(string[][] rows) => rows.Sum(r => int.Parse(r[11]) + int.Parse(r[12]) + int.Parse(r[13]));
            Assert.That(IngredientsTotal(after), Is.LessThan(IngredientsTotal(before) * .25f), "The approved scarcity must reduce actual supply substantially.");
            var stock = File.ReadAllLines(directory + "/stock-" + level + ".csv").Skip(1).Select(s => s.Split(','));
            foreach (var seed in stock.GroupBy(row => row[1]))
                Assert.That(seed.Where(row => row[6] == "True").Sum(row => int.Parse(row[4])), Is.EqualTo(6));
        }
    }
}
