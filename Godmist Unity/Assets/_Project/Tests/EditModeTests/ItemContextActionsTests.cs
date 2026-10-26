using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ItemContextActionsTests
    {
        private readonly List<Object> assets = new();
        private GameSession session;
        private CharacterInstance owner, recipient;
        private NpcInstance trader;
        private readonly TradeService trade = new();
        private ItemDefinition material;

        [SetUp]
        public void Setup()
        {
            session = new GameSession();
            owner = Character("owner"); recipient = Character("recipient");
            var role = new TraderRoleData(); Set(role,"RelevantItemTypes",new List<ItemType>());
            var definition = Asset<NpcDefinition>(); Set(definition,"Id","trader"); Set(definition,"Roles",new List<NpcRoleData>{role});
            trader = new NpcInstance { NpcId = "trader", Definition = definition, ShopInventory = new ShopInventory(), GoldBalance = 1000 };
            material = Asset<ItemDefinition>(); Set(material,"Id","material"); Set(material,"ItemType",ItemType.Material);
            Set(material,"IsStackable",true); Set(material,"Weight",2f); Set(material,"BaseValue",100);
        }
        [TearDown] public void Teardown() { foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }
        private CharacterInstance Character(string id)
        {
            var value = new CharacterInstance { CharacterId = id, AvailabilityState = CharacterAvailabilityState.Available, LifeState = CharacterLifeState.Alive };
            session.TryRegisterCharacter(value); session.TryAddCharacterToActiveParty(id); return value;
        }
        private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        private ItemInstance Item(string id, bool protectedSale = false) => new(material,id) { IsSaleProtected = protectedSale };
        private static string[] Ids(params ItemInstance[] items) => items.Select(item=>item.InstanceId).ToArray();
        private static void Set(object target,string name,object value)
        {
            var type = target.GetType(); FieldInfo field = null;
            while(type != null && field == null) { field = type.GetField($"<{name}>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance); type = type.BaseType; }
            Assert.That(field,Is.Not.Null,name); field.SetValue(target,value);
        }

        [Test]
        public void ProtectedItemCannotBeSoldThroughSingleOrBatchAndStacksStaySeparate()
        {
            var a = Item("a",true); var b = Item("b"); owner.Inventory.Items.AddRange(new[]{a,b});
            Assert.That(a.CanStackWith(b),Is.False);
            Assert.That(trade.TrySellToTrader(session,trader,owner,a.InstanceId,true,out _),Is.False);
            Assert.That(trade.TrySellBatch(session,trader,owner,Ids(a,b),true,out _,out var reason),Is.False);
            Assert.That(reason,Does.Contain("zablokowany")); Assert.That(owner.Inventory.Items,Is.EqualTo(new[]{a,b}));
            Assert.That(trader.ShopInventory.BuybackStock,Is.Empty); Assert.That(session.Gold,Is.Zero);
        }
        [Test]
        public void ProtectionDoesNotBlockOwnershipTransferAndUnlockRegroupsStack()
        {
            var a = Item("a"); var b = Item("b"); owner.Inventory.Items.AddRange(new[]{a,b});
            var service = new ItemOwnershipActions(); var ids = ItemOwnershipActions.CaptureStack(owner,a);
            Assert.That(service.TrySetSaleProtection(owner,a,ids,true),Is.True);
            Assert.That(service.TryTransfer(session,owner,a,ids,1,recipient,false,true,out _),Is.True);
            Assert.That(recipient.Inventory.Items.Single(),Is.SameAs(a)); Assert.That(a.IsSaleProtected,Is.True);
            Assert.That(service.TrySetSaleProtection(owner,b,Ids(b),false),Is.True);
            Assert.That(a.CanStackWith(b),Is.False);
            Assert.That(service.TrySetSaleProtection(recipient,a,Ids(a),false),Is.True); Assert.That(a.CanStackWith(b),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void BatchPurchaseIsAtomicWhenGoldOrWeightIsInsufficient(bool weight)
        {
            var a=Item("a");var b=Item("b");trader.ShopInventory.Items.AddRange(new[]{a,b});
            session.TryAddGold(weight ? 1000 : 150); if(weight) owner.Inventory.MaxWeight=3;
            var before=session.Gold;
            Assert.That(trade.TryBuyBatch(session,trader,owner,Ids(a,b),false,out _,out _),Is.False);
            Assert.That(trader.ShopInventory.Items,Is.EqualTo(new[]{a,b})); Assert.That(owner.Inventory.Items,Is.Empty);
            Assert.That(session.Gold,Is.EqualTo(before)); Assert.That(trader.SpentGoldTotal,Is.Zero);
        }
        [Test]
        public void BatchPurchaseUsesOneQuotedPriceAndCountsLoyaltyOnce()
        {
            var a=Item("a");var b=Item("b");trader.ShopInventory.Items.AddRange(new[]{a,b});session.TryAddGold(300);
            var price=trade.GetPriceToBuyFromTrader(trader,a)+trade.GetPriceToBuyFromTrader(trader,b);
            Assert.That(trade.TryBuyBatch(session,trader,owner,Ids(a,b),false,out var paid,out _),Is.True);
            Assert.That(paid,Is.EqualTo(price));Assert.That(session.Gold,Is.EqualTo(300-price));
            Assert.That(trader.GoldBalance,Is.EqualTo(1000+price));Assert.That(trader.SpentGoldTotal,Is.EqualTo(price));
            Assert.That(owner.Inventory.Items,Is.EqualTo(new[]{a,b}));Assert.That(trader.ShopInventory.Items,Is.Empty);
        }
        [Test]
        public void PartialBatchSaleRequiresConfirmationAndBalancesBuybackPayments()
        {
            var a=Item("a");var b=Item("b");owner.Inventory.Items.AddRange(new[]{a,b});trader.GoldBalance=61;
            Assert.That(trade.TryGetSellBatchQuote(trader,owner,Ids(a,b),out var normal,out var payable,out _),Is.True);
            Assert.That(normal,Is.EqualTo(100));Assert.That(payable,Is.EqualTo(61));
            Assert.That(trade.TrySellBatch(session,trader,owner,Ids(a,b),false,out _,out _),Is.False);
            Assert.That(owner.Inventory.Items.Count,Is.EqualTo(2));
            Assert.That(trade.TrySellBatch(session,trader,owner,Ids(a,b),true,out var received,out _),Is.True);
            Assert.That(received,Is.EqualTo(61));Assert.That(session.Gold,Is.EqualTo(61));Assert.That(trader.GoldBalance,Is.Zero);
            Assert.That(trader.ShopInventory.BuybackStock.Sum(entry=>entry.SoldPrice),Is.EqualTo(61));
        }
        [Test]
        public void DuplicateAndStaleSelectionsCannotPartiallyMutateInventories()
        {
            var a=Item("a");var b=Item("b");owner.Inventory.Items.AddRange(new[]{a,b});
            Assert.That(new InventoryService().TryTransferItems(owner.Inventory,recipient.Inventory,new[]{"a","a"}),Is.False);
            Assert.That(new InventoryService().TryTransferItems(owner.Inventory,recipient.Inventory,new[]{"a","missing"}),Is.False);
            Assert.That(trade.TrySellBatch(session,trader,owner,new[]{"a","missing"},true,out _,out _),Is.False);
            Assert.That(owner.Inventory.Items,Is.EqualTo(new[]{a,b}));Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [Test]
        public void SelectedTransferQuantityIsAtomicAndStashIsCityOnly()
        {
            var a=Item("a");var b=Item("b");var c=Item("c");owner.Inventory.Items.AddRange(new[]{a,b,c});
            recipient.Inventory.MaxWeight=3;var service=new ItemOwnershipActions();var ids=Ids(a,b,c);
            Assert.That(service.TryTransfer(session,owner,a,ids,2,recipient,false,true,out _),Is.False);
            Assert.That(owner.Inventory.Items.Count,Is.EqualTo(3));Assert.That(recipient.Inventory.Items,Is.Empty);
            Assert.That(service.TryTransfer(session,owner,a,ids,2,null,true,false,out _),Is.False);
            Assert.That(service.TryTransfer(session,owner,a,ids,2,null,true,true,out _),Is.True);
            Assert.That(session.Stash.Items,Is.EqualTo(new[]{a,b}));Assert.That(owner.Inventory.Items.Single(),Is.SameAs(c));
        }
        [Test]
        public void RapidClicksSellFromOriginalStackAndNeverShiftedNeighbour()
        {
            var clicks=new TradeClickSequence();var point=new Vector2(100,100);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"a",new[]{"a","b"},point,0),Is.False);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"a",new[]{"a","b"},point,.1f),Is.True);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"b",new[]{"b"},point,.2f),Is.True);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"neighbour",new[]{"neighbour"},point,.3f),Is.False);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"neighbour",new[]{"neighbour"},point,.4f),Is.False);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"neighbour",new[]{"neighbour"},point,1),Is.False);
            Assert.That(clicks.Click(TradeItemSlotSource.PlayerInventory,"neighbour",new[]{"neighbour"},point,1.1f),Is.True);
        }
        [Test]
        public void ProtectionSurvivesActualSnapshotJsonRestoreAndMissingLegacyFieldDefaultsFalse()
        {
            var a=Item("a",true);var b=Item("b");session.Stash.Items.AddRange(new[]{a,b});
            // Empty-character session is sufficient to exercise the common item pipeline.
            var saveSession=new GameSession();saveSession.Stash.Items.AddRange(new[]{a,b});
            var catalog=Asset<GameContentCatalog>();catalog.ItemDefinitions.Add(material);
            var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(saveSession,"Protection");
            var serializer=new GameSaveJsonSerializer();var json=serializer.Serialize(snapshot);
            Assert.That(serializer.TryDeserialize(json,out var data),Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(data,catalog,out var restored),Is.True);
            Assert.That(restored.Stash.Items.Single(item=>item.InstanceId=="a").IsSaleProtected,Is.True);
            Assert.That(restored.Stash.Items.Single(item=>item.InstanceId=="b").IsSaleProtected,Is.False);
            var legacy=json.Replace("\"IsSaleProtected\": true,","").Replace("\"IsSaleProtected\": false,","");
            Assert.That(serializer.TryDeserialize(legacy,out data),Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(data,catalog,out restored),Is.True);
            Assert.That(restored.Stash.Items.All(item=>!item.IsSaleProtected),Is.True);
        }

        [Test]
        public void ProtectedFreeStarterCannotBypassZeroPriceSaleValidation()
        {
            var definition=Asset<EquipmentItemDefinition>();Set(definition,"Id","starter");Set(definition,"IsStarterEquipment",true);
            var starter=new ItemInstance(definition,"starter.one"){IsSaleProtected=true};owner.Inventory.Items.Add(starter);
            Assert.That(TradeService.IsFreeStarter(starter),Is.True);
            Assert.That(trade.TrySellToTrader(session,trader,owner,starter.InstanceId,true,out _),Is.False);
            Assert.That(owner.Inventory.Items.Single(),Is.SameAs(starter));
        }
        [Test]
        public void BatchBuybackChargesEachItemAndDoesNotAwardLoyaltyAgain()
        {
            var a=Item("a");var b=Item("b");trader.ShopInventory.BuybackStock.Add(new BuybackEntry(a,50));
            trader.ShopInventory.BuybackStock.Add(new BuybackEntry(b,50));session.TryAddGold(300);
            Assert.That(trade.TryBuyBatch(session,trader,owner,Ids(a,b),true,out var paid,out _),Is.True);
            Assert.That(paid,Is.EqualTo(200));Assert.That(trader.SpentGoldTotal,Is.Zero);
            Assert.That(trader.ShopInventory.BuybackStock,Is.Empty);Assert.That(owner.Inventory.Items.Count,Is.EqualTo(2));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(3)]
        public void InvalidTransferQuantityDoesNotMoveItems(int count)
        {
            var a=Item("a");var b=Item("b");owner.Inventory.Items.AddRange(new[]{a,b});
            Assert.That(new ItemOwnershipActions().TryTransfer(session,owner,a,Ids(a,b),count,recipient,false,true,out _),Is.False);
            Assert.That(owner.Inventory.Items.Count,Is.EqualTo(2));Assert.That(recipient.Inventory.Items,Is.Empty);
        }
        [Test]
        public void StaleEquippedSelectionCannotTransferReplacementOrUseTemporaryBagSpace()
        {
            var definition=Asset<EquipmentItemDefinition>();Set(definition,"Id","weapon");Set(definition,"ItemType",ItemType.Weapon);
            Set(definition,"Weight",2f);var a=new EquipmentItemInstance(definition,"a");var b=new EquipmentItemInstance(definition,"b");
            owner.Equipment.Weapon=b;owner.Inventory.MaxWeight=0;recipient.Inventory.MaxWeight=0;
            var service=new EquipmentService();
            Assert.That(service.TryTransferEquippedItem(owner,EquipmentSlot.Weapon,a.InstanceId,recipient.Inventory),Is.False);
            Assert.That(service.TryTransferEquippedItem(owner,EquipmentSlot.Weapon,b.InstanceId,recipient.Inventory),Is.False);
            Assert.That(owner.Equipment.Weapon,Is.SameAs(b));Assert.That(owner.Inventory.Items,Is.Empty);
        }
        [Test]
        public void SaleRejectsWalletOverflowBeforeMovingAnyItem()
        {
            var a=Item("a");owner.Inventory.Items.Add(a);session.TryAddGold(int.MaxValue);
            Assert.That(trade.TrySellBatch(session,trader,owner,Ids(a),false,out _,out _),Is.False);
            Assert.That(session.Gold,Is.EqualTo(int.MaxValue));Assert.That(owner.Inventory.Items.Single(),Is.SameAs(a));
            Assert.That(trader.ShopInventory.BuybackStock,Is.Empty);
        }
        [Test]
        public void MissingBuybackDoesNotSpendNpcGoldOrRemovePlayerItem()
        {
            var a=Item("a");owner.Inventory.Items.Add(a);trader.ShopInventory.BuybackStock=null;
            Assert.That(trade.TrySellBatch(session,trader,owner,Ids(a),true,out _,out _),Is.False);
            Assert.That(owner.Inventory.Items.Single(),Is.SameAs(a));Assert.That(trader.GoldBalance,Is.EqualTo(1000));
            Assert.That(session.Gold,Is.Zero);
        }

        [TestCase(TradeItemSlotSource.PlayerInventory)]
        [TestCase(TradeItemSlotSource.TraderOffer)]
        [TestCase(TradeItemSlotSource.Buyback)]
        public void PartialTradeKeepsFirstStackUnitInInterleavedInventory(TradeItemSlotSource source)
        {
            var first = Item("first"); var other = Item("protected", true);
            var refill = Item("refill"); var last = Item("last");
            var ordered = new[] { first, other, refill, last };
            if (source == TradeItemSlotSource.PlayerInventory) owner.Inventory.Items.AddRange(ordered);
            else if (source == TradeItemSlotSource.TraderOffer) trader.ShopInventory.Items.AddRange(ordered);
            else foreach (var item in ordered) trader.ShopInventory.BuybackStock.Add(new BuybackEntry(item, 50));
            var go = new GameObject("Trade selection test"); assets.Add(go);
            var view = go.AddComponent<TradeServiceWindowView>();
            var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TradeServiceWindowView).GetField("tradingCharacter", hidden).SetValue(view, owner);
            typeof(TradeServiceWindowView).GetField("trader", hidden).SetValue(view, trader);
            var ids = (List<string>)typeof(TradeServiceWindowView).GetMethod("CaptureStack", hidden)
                .Invoke(view, new object[] { first, source });
            Assert.That(ids, Is.EqualTo(new[] { "last", "refill", "first" }));
            session.TryAddGold(1000);
            if (source == TradeItemSlotSource.PlayerInventory)
                Assert.That(trade.TrySellBatch(session, trader, owner, ids.Take(2).ToArray(), false, out _, out _), Is.True);
            else Assert.That(trade.TryBuyBatch(session, trader, owner, ids.Take(2).ToArray(),
                source == TradeItemSlotSource.Buyback, out _, out _), Is.True);
            var remaining = source == TradeItemSlotSource.PlayerInventory ? owner.Inventory.Items
                : source == TradeItemSlotSource.TraderOffer ? trader.ShopInventory.Items
                : trader.ShopInventory.BuybackStock.Select(entry => entry.Item).ToList();
            Assert.That(remaining, Is.EqualTo(new[] { first, other }));
        }
    }
}
