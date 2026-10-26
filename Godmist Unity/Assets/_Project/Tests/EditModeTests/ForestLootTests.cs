using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
namespace Godmist.Tests.EditMode
{
    public sealed class ForestLootTests
    {
        private const string Root="Assets/_Project/ScriptableObjects/Dungeons/Forest/Loot/";
        private static LootBagItemDefinition Bag(string key)=>AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(Root+"ID_Forest_"+key+".asset");
        [Test] public void ResourceBracketsUseCalibratedCraftingAndOneRareIngredientPool() {
            var bag=Bag("Resources"); Assert.That(bag.Brackets.Count,Is.EqualTo(2));
            foreach(var bracket in bag.Brackets) {
                var high=bracket.Bracket==LootLevelBracket.Level11To20;
                Assert.That(bracket.Pools[0].DropChance,Is.EqualTo(.8f));
                var crafting=bracket.Pools.Where(CraftingMaterialDropSetup.IsCraftingPool).ToArray();
                Assert.That(crafting,Is.Not.Empty);
                Assert.That(crafting.SelectMany(p=>p.Entries).All(e=>e.SelectionWeight>0 && e.Amount.Min==1 && e.Amount.Max==1),Is.True);
                Assert.That(crafting.SelectMany(p=>p.Entries).Any(e=>e.ItemDefinition.Id.Contains("energy_dust")),Is.False);
                Assert.That(crafting.SelectMany(p=>p.Entries).Select(e=>e.ItemDefinition.Id).Distinct(),Is.EquivalentTo(CraftingMaterialDropSetup.Materials.Where(m=>m.Tier==(high?15:5)).Select(m=>m.Id)));
                Assert.That(bracket.Pools.SelectMany(p=>p.Entries).Any(e=>e.ItemDefinition.Id.Contains("resin")),Is.False);
                var alchemy = bracket.Pools.Single(pool => pool.DistinctSelectionGroup == "alchemy");
                Assert.That(alchemy.DropChance, Is.EqualTo(.2f));
                Assert.That(alchemy.Entries.Select(e => e.ItemDefinition.Id), Is.EquivalentTo(new[]{"item.vs10.szyszka", "item.material.elf_ear"}));
                Assert.That(alchemy.Entries.All(e => e.Amount.Min == 1 && e.Amount.Max == 1), Is.True);
            }
        }
        [TestCase(1)] [TestCase(10)] [TestCase(11)] [TestCase(20)]
        public void RolledResourceBagsRespectCraftingDrawBudgetAndRareSingleIngredients(int level) {
            var random=new System.Random(413+level);
            var loot=new LootTableSystem(()=> (float)random.NextDouble(),lootLevel:level);
            var ingredientDrops=0;
            for(var i=0;i<200;i++) {
                var bag=loot.CreateLootBag(Bag("Resources")); Assert.That(bag.IsEmpty,Is.False);
                var specs=CraftingMaterialDropSetup.Materials.Where(m=>m.Tier==(level<=10?5:15) && !m.Runic).ToArray();
                var ids=specs.Select(m=>m.Id).ToHashSet();
                var staples=bag.Contents.Where(x=>ids.Contains(x.Definition.Id)).ToArray();
                var groups=staples.GroupBy(x=>x.Definition.Id).ToArray();
                var mean=specs.Sum(m=>m.Forest);
                Assert.That(staples.Length,Is.InRange((int)System.Math.Floor(mean),(int)System.Math.Ceiling(mean)));
                Assert.That(groups.Length,Is.InRange(1,staples.Length));
                var ingredients=bag.Contents.Count(x=>x.Definition.Id is "item.vs10.szyszka" or "item.material.elf_ear");
                Assert.That(ingredients,Is.InRange(0,1));ingredientDrops+=ingredients;
                if(level<=10)Assert.That(bag.Contents.Any(x=>x.Definition.Id=="item.runic.topaz" || x.Definition.Id=="item.runic.amber"),Is.False);
            }
            Assert.That(ingredientDrops,Is.InRange(20,60),"The 20% pool must be rare, rather than guaranteed or absent.");
        }
        [Test] public void ForestDungeonIsWiredAndCatalogContainsEveryBagItem() {
            var dungeon=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            Assert.That(dungeon.LootTableCatalog,Is.Not.Null);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            foreach(var key in new[]{"Resources","Weapons","Armor","Galdurytes"}) {
                Assert.That(catalog.ItemDefinitions,Has.Member(Bag(key)));
                foreach(var entry in Bag(key).Brackets.SelectMany(b=>b.Pools).SelectMany(p=>p.Entries))
                    Assert.That(catalog.ItemDefinitions,Has.Member(entry.ItemDefinition));
            }
            foreach(var level in new[]{1,11}) {
                Assert.That(dungeon.LootTableCatalog.TryGetTables(DungeonTheme.Forest,level,out var enemies,out var chests),Is.True);
                Assert.That(enemies.Entries[0].ItemDefinition,Is.SameAs(Bag("Resources")));
                Assert.That(enemies.Entries[0].DropChance,Is.EqualTo(.5f));
                Assert.That(chests.Entries[0].Amount.Min,Is.EqualTo(2)); Assert.That(chests.Entries[0].Amount.Max,Is.EqualTo(3));
            }
        }
        [TestCase(1,5)] [TestCase(11,15)]
        public void EquipmentBagsGenerateCompleteEquipmentAtBracketLevel(int level,int expected) {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var loot=new LootTableSystem(new System.Random(454),catalog.EquipmentPartDefinitions,lootLevel:level);
            foreach(var key in new[]{"Weapons","Armor"}) {
                var bag=loot.CreateLootBag(Bag(key)); Assert.That(bag.Contents.Count,Is.InRange(2,5));
                foreach(var item in bag.Contents.Cast<EquipmentItemInstance>()) {
                    Assert.That(item.ItemLevel,Is.EqualTo(expected+EquipmentGenerationService.GetQualityLevelAdjustment(item.Quality)));
                    Assert.That(item.PartIds.Count,Is.EqualTo(3));
                    Assert.That(item.PartIds.All(id=>catalog.EquipmentPartDefinitions.Any(p=>p.Id==id && (int)p.LevelBracket==expected)),Is.True);
                }
            }
        }
        [Test] public void ForestGaldurytesGenerateAtLevelTwenty() {
            var loot=new LootTableSystem(new System.Random(467),null,lootLevel:11);
            var bag=loot.CreateLootBag(Bag("Galdurytes")); Assert.That(bag.Contents.Count,Is.InRange(3,7));
            foreach(var item in bag.Contents.Cast<GalduryteItemInstance>()) {
                Assert.That(item.ItemLevel,Is.EqualTo(20)); Assert.That(item.Effects,Is.Not.Empty);
            }
        }
        [Test] public void EquipmentAndGalduryteBracketsUseExistingGeneratorRules() {
            foreach(var key in new[]{"Weapons","Armor"})
                foreach(var bracket in Bag(key).Brackets) {
                    Assert.That(bracket.Pools.Select(p=>p.DropChance),Is.EqualTo(new[]{1f,1f,.8f,.5f,.25f}));
                    foreach(var pool in bracket.Pools) {
                        Assert.That(pool.Entries.Count,Is.EqualTo(12));
                        Assert.That(pool.Entries.All(e=>e.HasFixedEquipmentQuality),Is.True);
                    }
                }
            Assert.That(Bag("Galdurytes").Brackets.Count,Is.EqualTo(1));
            Assert.That(Bag("Galdurytes").Brackets.Single().Pools.Select(p=>p.DropChance),Is.EqualTo(new[]{1f,1f,1f,.8f,.55f,.4f,.25f}));
        }
    }
}
