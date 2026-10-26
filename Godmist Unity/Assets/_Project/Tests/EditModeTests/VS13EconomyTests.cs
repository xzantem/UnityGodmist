using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Game;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class VS13EconomyTests
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";

        [Test]
        public void ProposedStockResolvesInCatalogWithoutOverlappingEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Check(VS13EconomySetup.AlchemistLocal(), VS13EconomySetup.AlchemistRotating());
            Check(VS13EconomySetup.EnchanterLocal(), VS13EconomySetup.EnchanterRotating());

            void Check(VS13EconomySetup.StockSpec[] local, VS13EconomySetup.GroupSpec[] groups)
            {
                var all = local.Concat(groups.SelectMany(group => group.Candidates)).ToArray();
                Assert.That(all.Select(entry => entry.Name).Distinct().Count(), Is.EqualTo(all.Length));
                foreach (var entry in all)
                {
                    Assert.That(VS13EconomySetup.StockCatalogItems(catalog).Count(item => item.DisplayName == entry.Name), Is.EqualTo(1), entry.Name);
                    Assert.That(entry.Limit, Is.GreaterThan(0), entry.Name);
                    Assert.That(entry.From, Is.InRange(1, 20), entry.Name);
                    Assert.That(entry.Through, Is.GreaterThanOrEqualTo(entry.From), entry.Name);
                }
                foreach (var level in new[] { 1, 10, 11, 20 })
                foreach (var group in groups)
                {
                    var available = group.Candidates.Count(entry => entry.From <= level
                        && entry.Through >= level);
                    if (available > 0)
                        Assert.That(available, Is.GreaterThanOrEqualTo(group.Slots),
                            $"{group.Id} @ L{level}");
                }
            }
        }

        [Test]
        public void AlchemyIngredientsAndMediaHaveMerchantSources()
        {
            var role = AssetDatabase.LoadAssetAtPath<AlchemistRoleData>(
                Root + "NPC/Roles/AR_Arungard_Alchemist.asset");
            Assert.That(role, Is.Not.Null);
            var stock = Names(VS13EconomySetup.AlchemistLocal(),
                VS13EconomySetup.AlchemistRotating());
            foreach (var item in role.IngredientOptions.Select(option => option.Material)
                .Concat(role.PotionAlcohols.Select(option => option.Material))
                .Concat(role.BombGunpowders.Select(option => option.Material))
                .Concat(role.Catalysts.Select(option => option.Material)))
            {
                Assert.That(item, Is.Not.Null);
                Assert.That(stock.Contains(item.DisplayName), Is.True, item.DisplayName);
            }
        }

        [Test]
        public void JewelryAndGalduryteInputsHaveMerchantOrBossSources()
        {
            var role = AssetDatabase.LoadAssetAtPath<EnchanterRoleData>(
                Root + "NPC/Roles/ER_Arungard_Enchanter.asset");
            Assert.That(role, Is.Not.Null);
            var stock = Names(VS13EconomySetup.EnchanterLocal(),
                VS13EconomySetup.EnchanterRotating());
            Assert.That(role.GalduryteStones[0].Material.DisplayName,
                Is.EqualTo("Wapienny kamień galdurytu"));
            foreach (var jewelry in role.JewelryOffers)
            {
                foreach (var metal in jewelry.Metals)
                    Assert.That(stock.Contains(metal.Material.DisplayName), Is.True,
                        metal.Material.DisplayName);
                foreach (var gem in jewelry.Gems)
                {
                    // Obsidian is the Catacombs boss gem, not regular stock.
                    var name = gem.CraftingMaterial.DisplayName;
                    Assert.That(name == "Obsydian" || stock.Contains(name), Is.True, name);
                }
            }
            foreach (var powder in role.GaldurytePowders)
                Assert.That(stock.Contains(powder.Material.DisplayName), Is.True,
                    powder.Material.DisplayName);
            Assert.That(stock.Contains("Lity wapień"), Is.True);
            Assert.That(stock.Contains("Wapienny kamień galdurytu"), Is.True);
        }

        [TestCase(1, 8, 5)]
        [TestCase(11, 8, 7)]
        public void AuthoredOffersRespectLevelAndReplaceLegacyInitialOffer(
            int level, int alchemistSlots, int enchanterSlots)
        {
            var alchemistRole = AssetDatabase.LoadAssetAtPath<TraderRoleData>(
                Root + "NPC/Roles/TRD_Arungard_Alchemist.asset");
            var enchanterRole = AssetDatabase.LoadAssetAtPath<TraderRoleData>(
                Root + "NPC/Roles/TRD_Arungard_Enchanter.asset");
            var enchanterServices = AssetDatabase.LoadAssetAtPath<EnchanterRoleData>(
                Root + "NPC/Roles/ER_Arungard_Enchanter.asset");
            Assert.That(alchemistRole.InitialOffer, Is.Empty);
            Assert.That(enchanterRole.InitialOffer, Is.Empty);
            Assert.That(enchanterServices.GalduryteStones.Count, Is.EqualTo(1));

            CheckShop("ND_Arungard_Alchemist.asset", alchemistRole,
                VS13EconomySetup.AlchemistLocal(), alchemistSlots);
            CheckShop("ND_Arungard_Enchanter.asset", enchanterRole,
                VS13EconomySetup.EnchanterLocal(), enchanterSlots);

            void CheckShop(string npcAsset, TraderRoleData role,
                VS13EconomySetup.StockSpec[] local, int expectedSlots)
            {
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                    Root + "NPC/" + npcAsset);
                var npc = new NpcInstanceFactory().Create(definition, level);
                Assert.That(npc.RotatingStockItemIds.Count, Is.EqualTo(expectedSlots));
                foreach (var entry in role.LocalStock)
                {
                    var count = npc.ShopInventory.Items.Count(item => item.Definition == entry.Item);
                    Assert.That(count, Is.EqualTo(entry.IsAvailable(level) ? entry.Limit : 0),
                        entry.Item.DisplayName + " @ L" + level);
                }
                foreach (var item in npc.ShopInventory.Items)
                {
                    Assert.That(local.Any(spec => spec.Name == item.Definition.DisplayName
                            && spec.From <= level && spec.Through >= level)
                        || npc.RotatingStockItemIds.Contains(item.Definition.Id), Is.True,
                        item.Definition.DisplayName + " @ L" + level);
                    Assert.That(item.Definition.DisplayName, Is.Not.EqualTo("Obsydian"));
                }
            }
        }

        [TestCase(1, 0)]
        [TestCase(11, 3)]
        public void SpecialistsSellThreeCraftedItemsPerUnlockedCategory(int level,
            int expectedGaldurytes)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            var alchemist = Create("ND_Arungard_Alchemist.asset");
            var smith = Create("ND_Arungard_Blacksmith.asset");
            var enchanter = Create("ND_Arungard_Enchanter.asset");

            Assert.That(Crafted<ConsumableItemInstance>(alchemist).Count(item => !item.IsBomb),
                Is.EqualTo(3));
            Assert.That(Crafted<ConsumableItemInstance>(alchemist).Count(item => item.IsBomb),
                Is.EqualTo(3));
            Assert.That(Crafted<EquipmentItemInstance>(smith).Count(item =>
                item.Definition.ItemType == Godmist.Core.Enums.ItemType.Weapon), Is.EqualTo(3));
            Assert.That(Crafted<EquipmentItemInstance>(smith).Count(item =>
                item.Definition.ItemType == Godmist.Core.Enums.ItemType.Armor), Is.EqualTo(3));
            Assert.That(Crafted<JewelryItemInstance>(enchanter).Count(), Is.EqualTo(3));
            Assert.That(Crafted<GalduryteItemInstance>(enchanter).Count(),
                Is.EqualTo(expectedGaldurytes));
            var trade = new TradeService();
            foreach (var npc in new[] { alchemist, smith, enchanter })
                foreach (var item in npc.ShopInventory.Items.Where(item =>
                    npc.CraftedStockInstanceIds.Contains(item.InstanceId)))
                    Assert.That(trade.GetPriceToBuyFromTrader(npc, item),
                        Is.GreaterThan(0), item.DisplayName);

            NpcInstance Create(string asset)
            {
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(Root + "NPC/" + asset);
                Assert.That(definition, Is.Not.Null, asset);
                return new NpcInstanceFactory().Create(definition, level, catalog);
            }
        }

        [Test]
        public void RefreshKeepsSomeUnsoldInstancesAndReplacesTheRest()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                Root + "NPC/ND_Arungard_Alchemist.asset");
            var npc = new NpcInstanceFactory().Create(definition, 11, catalog);
            var original = Crafted<ConsumableItemInstance>(npc)
                .Where(item => !item.IsBomb).Select(item => item.InstanceId).ToHashSet();
            Assert.That(original.Count, Is.EqualTo(3));

            new TraderStockService(new Random(7)).Refresh(npc, 11, catalog: catalog);
            var current = Crafted<ConsumableItemInstance>(npc)
                .Where(item => !item.IsBomb).Select(item => item.InstanceId).ToHashSet();
            Assert.That(current.Count, Is.EqualTo(3));
            Assert.That(current.Intersect(original).Count(), Is.EqualTo(1));
            Assert.That(npc.CraftedStockInstanceIds.Contains(current.First()), Is.True);
        }

        [TestCase(1, EquipmentQuality.Poor, EquipmentQuality.Normal)]
        [TestCase(5, EquipmentQuality.Normal, EquipmentQuality.Poor)]
        [TestCase(7, EquipmentQuality.Normal, EquipmentQuality.Excellent)]
        [TestCase(10, EquipmentQuality.Excellent, EquipmentQuality.Normal)]
        [TestCase(11, EquipmentQuality.Poor, EquipmentQuality.Normal)]
        [TestCase(20, EquipmentQuality.Excellent, EquipmentQuality.Normal)]
        [TestCase(25, EquipmentQuality.Excellent, EquipmentQuality.Normal)]
        public void BlacksmithStockHasLevelBiasedQualitySpread(int level,
            EquipmentQuality dominant, EquipmentQuality adjacent)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                Root + "NPC/ND_Arungard_Blacksmith.asset");
            var npc = new NpcInstanceFactory().Create(definition, level, catalog);
            Check();
            new TraderStockService(new Random(23)).Refresh(npc, level, catalog: catalog);
            Check();

            void Check()
            {
                foreach (var type in new[] { ItemType.Weapon, ItemType.Armor })
                {
                    var items = Crafted<EquipmentItemInstance>(npc)
                        .Where(item => item.Definition.ItemType == type).ToList();
                    Assert.That(items.Count, Is.EqualTo(3), $"{type} L{level}");
                    Assert.That(items.Count(item => item.Quality == dominant),
                        Is.EqualTo(2), $"{type} L{level}");
                    Assert.That(items.Count(item => item.Quality == adjacent),
                        Is.EqualTo(1), $"{type} L{level}");
                }
            }
        }

        [Test]
        public void BlacksmithReplacesPreviousTierAtLevelEleven()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                Root + "NPC/ND_Arungard_Blacksmith.asset");
            var npc = new NpcInstanceFactory().Create(definition, 10, catalog);
            Assert.That(Crafted<EquipmentItemInstance>(npc).All(item => item.ItemLevel < 11),
                Is.True);

            new TraderStockService(new Random(11)).Refresh(npc, 11, catalog: catalog);
            var offer = Crafted<EquipmentItemInstance>(npc);
            Assert.That(offer.Count, Is.EqualTo(6));
            Assert.That(offer.All(item => item.ItemLevel >= 11), Is.True);
            foreach (var type in new[] { ItemType.Weapon, ItemType.Armor })
            {
                var group = offer.Where(item => item.Definition.ItemType == type).ToList();
                Assert.That(group.Count(item => item.Quality == EquipmentQuality.Poor),
                    Is.EqualTo(2));
                Assert.That(group.Count(item => item.Quality == EquipmentQuality.Normal),
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void BlacksmithKeepsQualityBiasWhilePartyLevelsUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                Root + "NPC/ND_Arungard_Blacksmith.asset");
            var npc = new NpcInstanceFactory().Create(definition, 5, catalog);
            var stock = new TraderStockService(new Random(29));
            foreach (var level in new[] { 6, 7, 8, 9, 10, 11 })
            {
                stock.Refresh(npc, level, catalog: catalog);
                var dominant = level < 8 ? EquipmentQuality.Normal
                    : level < 11 ? EquipmentQuality.Excellent : EquipmentQuality.Poor;
                foreach (var type in new[] { ItemType.Weapon, ItemType.Armor })
                {
                    var items = Crafted<EquipmentItemInstance>(npc)
                        .Where(item => item.Definition.ItemType == type).ToList();
                    Assert.That(items.Count, Is.EqualTo(3), $"{type} L{level}");
                    Assert.That(items.Count(item => item.Quality == dominant),
                        Is.EqualTo(2), $"{type} L{level}");
                }
            }
        }

        [Test]
        public void CraftedOfferSurvivesSaveRestoreWithSameIdsAndPrices()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                Root + "NPC/ND_Arungard_Alchemist.asset");
            var npc = new NpcInstanceFactory().Create(definition, 11, catalog);
            var original = Crafted<ConsumableItemInstance>(npc).ToDictionary(
                item => item.InstanceId,
                item => (item.DisplayName, item.BaseValue, item.RuntimeEffects.Count));
            Assert.That(original.Count, Is.EqualTo(6));
            var trade = new TradeService();
            Assert.That(Crafted<ConsumableItemInstance>(npc).All(item =>
                trade.GetPriceToBuyFromTrader(npc, item) > 0), Is.True);

            var session = new GameSession();
            Assert.That(session.TryRegisterNpc(npc), Is.True);
            var saved = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "VS13 merchant");
            Assert.That(new GameSessionRestoreFactory().TryCreate(
                saved, catalog, out var restored), Is.True);
            Assert.That(restored.NpcsById.TryGetValue(npc.NpcId, out var restoredNpc), Is.True);
            var restoredOffer = Crafted<ConsumableItemInstance>(restoredNpc).ToDictionary(
                item => item.InstanceId,
                item => (item.DisplayName, item.BaseValue, item.RuntimeEffects.Count));
            Assert.That(restoredOffer, Is.EquivalentTo(original));
        }

        private static List<T> Crafted<T>(Godmist.Runtime.NPC.NpcInstance npc)
            where T : ItemInstance => npc.ShopInventory.Items.OfType<T>()
                .Where(item => npc.CraftedStockInstanceIds.Contains(item.InstanceId)).ToList();

        private static HashSet<string> Names(VS13EconomySetup.StockSpec[] local,
            VS13EconomySetup.GroupSpec[] groups) =>
            new(local.Select(entry => entry.Name)
                .Concat(groups.SelectMany(group => group.Candidates)
                    .Select(entry => entry.Name)), StringComparer.Ordinal);
    }
}
