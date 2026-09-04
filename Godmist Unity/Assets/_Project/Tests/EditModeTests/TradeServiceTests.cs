using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class TradeServiceTests
    {
        [Test]
        public void GetPriceToBuyFromTrader_UsesItemBaseValue()
        {
            var service = new TradeService();
            var item = CreateItem("item.material", ItemType.Material, 80);

            var price = service.GetPriceToBuyFromTrader(item);

            Assert.That(price, Is.EqualTo(80));
        }

        [Test]
        public void GetPriceToBuyFromTrader_AppliesNpcLoyaltyDiscount()
        {
            var service = new TradeService();
            var trader = CreateTrader(new List<ItemType>());
            trader.LoyaltyLevel = 12;
            var item = CreateItem("item.material", ItemType.Material, 100);

            var price = service.GetPriceToBuyFromTrader(trader, item);

            Assert.That(price, Is.EqualTo(75));
        }

        [Test]
        public void TryBuyFromTrader_ChargesDiscountedPrice()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType>());
            trader.LoyaltyLevel = 12;
            var buyer = new CharacterInstance();
            var item = CreateItem("item.weapon", ItemType.Weapon, 100);

            session.TryAddGold(100);
            trader.ShopInventory.Items.Add(item);

            var result = service.TryBuyFromTrader(
                session,
                trader,
                buyer,
                item.InstanceId,
                out var paidGold);

            Assert.That(result, Is.True);
            Assert.That(paidGold, Is.EqualTo(75));
            Assert.That(session.Gold, Is.EqualTo(25));
            Assert.That(trader.SpentGoldTotal, Is.EqualTo(75));
        }

        [Test]
        public void MaterialTrade_RejectsMaterialAboveNpcLoyaltyGate()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType>());
            trader.LoyaltyLevel = 4;
            var buyer = new CharacterInstance();
            var item = CreateItem(
                "item.uncommon_material",
                ItemType.Material,
                100,
                RarityTier.Uncommon);

            session.TryAddGold(100);
            trader.ShopInventory.Items.Add(item);

            var result = service.TryBuyFromTrader(
                session,
                trader,
                buyer,
                item.InstanceId,
                out var paidGold);

            Assert.That(result, Is.False);
            Assert.That(paidGold, Is.EqualTo(0));
            Assert.That(session.Gold, Is.EqualTo(100));
            Assert.That(trader.ShopInventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetPriceWhenSellingToTrader_UsesHalfForRelevantItem()
        {
            var service = new TradeService();
            var trader = CreateTrader(new List<ItemType> { ItemType.Weapon });
            var weapon = CreateItem("item.weapon", ItemType.Weapon, 100);

            var price = service.GetPriceWhenSellingToTrader(trader, weapon);

            Assert.That(price, Is.EqualTo(50));
        }

        [Test]
        public void GetPriceWhenSellingToTrader_UsesQuarterForIrrelevantItem()
        {
            var service = new TradeService();
            var trader = CreateTrader(new List<ItemType> { ItemType.Weapon });
            var material = CreateItem("item.material", ItemType.Material, 100);

            var price = service.GetPriceWhenSellingToTrader(trader, material);

            Assert.That(price, Is.EqualTo(25));
        }

        [Test]
        public void TryBuyFromTrader_TransfersItemAndSpendsGold()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType>());
            var buyer = new CharacterInstance();
            var item = CreateItem("item.weapon", ItemType.Weapon, 60);

            session.TryAddGold(100);
            trader.ShopInventory.Items.Add(item);

            var result = service.TryBuyFromTrader(
                session,
                trader,
                buyer,
                item.InstanceId,
                out var paidGold);

            Assert.That(result, Is.True);
            Assert.That(paidGold, Is.EqualTo(60));
            Assert.That(session.Gold, Is.EqualTo(40));
            Assert.That(trader.SpentGoldTotal, Is.EqualTo(60));
            Assert.That(trader.ShopInventory.Items, Is.Empty);
            Assert.That(buyer.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(buyer.Inventory.Items[0], Is.SameAs(item));
        }

        [Test]
        public void TrySellToTrader_TransfersItemToBuybackAndGrantsGold()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType> { ItemType.Material });
            var seller = new CharacterInstance();
            var item = CreateItem("item.material", ItemType.Material, 100);

            seller.Inventory.Items.Add(item);

            var result = service.TrySellToTrader(
                session,
                trader,
                seller,
                item.InstanceId,
                out var receivedGold);

            Assert.That(result, Is.True);
            Assert.That(receivedGold, Is.EqualTo(50));
            Assert.That(session.Gold, Is.EqualTo(50));
            Assert.That(seller.Inventory.Items, Is.Empty);
            Assert.That(trader.ShopInventory.BuybackStock, Has.Count.EqualTo(1));
            Assert.That(trader.ShopInventory.BuybackStock[0].Item, Is.SameAs(item));
            Assert.That(trader.ShopInventory.BuybackStock[0].SoldPrice, Is.EqualTo(50));
        }

        [Test]
        public void TryBuyFromTrader_WhenPlayerCannotAffordItem_DoesNotTransferItem()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType>());
            var buyer = new CharacterInstance();
            var item = CreateItem("item.weapon", ItemType.Weapon, 60);

            trader.ShopInventory.Items.Add(item);

            var result = service.TryBuyFromTrader(
                session,
                trader,
                buyer,
                item.InstanceId,
                out var paidGold);

            Assert.That(result, Is.False);
            Assert.That(paidGold, Is.EqualTo(0));
            Assert.That(session.Gold, Is.EqualTo(0));
            Assert.That(trader.ShopInventory.Items, Has.Count.EqualTo(1));
            Assert.That(buyer.Inventory.Items, Is.Empty);
        }

        [Test]
        public void TryBuyFromBuyback_ReturnsExactSoldItemForOriginalSellPrice()
        {
            var service = new TradeService();
            var session = new GameSession();
            var trader = CreateTrader(new List<ItemType> { ItemType.Material });
            var character = new CharacterInstance();
            var item = CreateItem("item.material", ItemType.Material, 100);

            character.Inventory.Items.Add(item);

            var didSell = service.TrySellToTrader(
                session,
                trader,
                character,
                item.InstanceId,
                out var receivedGold);

            var didBuyBack = service.TryBuyFromBuyback(
                session,
                trader,
                character,
                item.InstanceId,
                out var paidGold);

            Assert.That(didSell, Is.True);
            Assert.That(receivedGold, Is.EqualTo(50));
            Assert.That(didBuyBack, Is.True);
            Assert.That(paidGold, Is.EqualTo(50));
            Assert.That(session.Gold, Is.EqualTo(0));
            Assert.That(trader.ShopInventory.BuybackStock, Is.Empty);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items[0], Is.SameAs(item));
        }

        private static NpcInstance CreateTrader(List<ItemType> relevantItemTypes)
        {
            var traderRole = ScriptableObject.CreateInstance<TraderRoleData>();

            SetAutoProperty(
                traderRole,
                "RelevantItemTypes",
                relevantItemTypes);

            var definition = ScriptableObject.CreateInstance<NpcDefinition>();

            SetAutoProperty(definition, "Id", "npc.trader");
            SetAutoProperty(definition, "DisplayName", "Trader");
            SetAutoProperty(
                definition,
                "Roles",
                new List<NpcRoleData> { traderRole });

            return new NpcInstance
            {
                NpcId = "npc.trader",
                Definition = definition,
                ShopInventory = new ShopInventory()
            };
        }

        private static ItemInstance CreateItem(
            string itemId,
            ItemType itemType,
            int baseValue,
            RarityTier baseRarity = RarityTier.Common)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", itemId);
            SetAutoProperty(definition, "DisplayName", itemId);
            SetAutoProperty(definition, "ItemType", itemType);
            SetAutoProperty(definition, "BaseValue", baseValue);
            SetAutoProperty(definition, "BaseRarity", baseRarity);

            return new ItemInstance(definition, $"{itemId}.instance");
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var type = target.GetType();
            FieldInfo field = null;

            while (type != null && field == null)
            {
                field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);

                type = type.BaseType;
            }

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono backing field dla '{propertyName}'.");

            field.SetValue(target, value);
        }
    }
}
