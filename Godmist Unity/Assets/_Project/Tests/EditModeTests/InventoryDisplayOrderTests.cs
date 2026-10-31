using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class InventoryDisplayOrderTests
    {
        private readonly List<Object> assets = new();
        [TearDown] public void Cleanup() { foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }
        private T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value); return;
            }
            Assert.Fail("Missing property: " + name);
        }
        private ItemDefinition Definition(string id, string name, ItemType type = ItemType.Material, int price = 10)
        {
            var definition = Asset<ItemDefinition>();
            Set(definition, "Id", id); Set(definition, "DisplayName", name);
            Set(definition, "ItemType", type); Set(definition, "BaseValue", price); Set(definition, "IsStackable", true);
            return definition;
        }
        private static ItemInstance Item(ItemDefinition definition, string id, RarityTier rarity = RarityTier.Common)
            => new(definition, id) { CurrentRarity = rarity };
        private static object Member(object value, string name) => value.GetType().GetProperty(name)?.GetValue(value)
            ?? value.GetType().GetField(name)?.GetValue(value);
        private static List<object> Stacks(Type view, List<ItemInstance> items)
            => ((IEnumerable)view.GetMethod("BuildItemStacks", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { items })).Cast<object>().ToList();
        private static ItemInstance Representative(object stack) => (ItemInstance)Member(stack, "Representative");

        [TestCase(typeof(CharacterInfoWindowView))] [TestCase(typeof(TradeServiceWindowView))]
        [TestCase(typeof(DungeonLootWindowView))] [TestCase(typeof(InventoryEquipmentPanelView))] [TestCase(typeof(TradePanelView))]
        public void RemovingFirstPhysicalItemDoesNotReorderRemainingStacks(Type view)
        {
            var a = Definition("a", "Alfa"); var b = Definition("b", "Beta"); var c = Definition("c", "Gamma");
            var first = Item(a, "a1");
            var inventory = new List<ItemInstance> { first, Item(b, "b1"), Item(a, "a2"), Item(c, "c1") };
            var before = Stacks(view, inventory);
            Assert.That(before.Select(s => Representative(s).Definition.Id), Is.EqualTo(new[] { "a", "b", "c" }));
            inventory.Remove(first);
            var after = Stacks(view, inventory);
            Assert.That(after.Select(s => Representative(s).Definition.Id), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(Representative(after[0]).InstanceId, Is.EqualTo("a2"));
            Assert.That((int)Member(after[0], "Count"), Is.EqualTo(1));
            Assert.That(inventory.Select(i => i.InstanceId), Is.EqualTo(new[] { "b1", "a2", "c1" }), "Display ordering must not reorder runtime/save inventory.");
        }

        [Test]
        public void KeysApplyInApprovedPriorityWithRarityAndUnitPriceDescending()
        {
            var weapon = Item(Definition("weapon", "Z", ItemType.Weapon), "w", RarityTier.Trash);
            var armor = Item(Definition("armor", "A", ItemType.Armor), "a", RarityTier.Godly);
            var rare = Item(Definition("rare", "Z"), "r", RarityTier.Legendary);
            var costlyAlpha = Item(Definition("alpha-costly", "Alfa", price: 30), "ac");
            var cheapAlpha = Item(Definition("alpha-cheap", "Alfa", price: 10), "ah");
            var beta = Item(Definition("beta", "Beta", price: 100), "b");
            var sorted = ItemDisplayOrder.Sort(new[] { beta, cheapAlpha, armor, rare, costlyAlpha, weapon }, i => i);
            Assert.That(sorted, Is.EqualTo(new[] { weapon, armor, rare, costlyAlpha, cheapAlpha, beta }));
        }

        [TestCase(typeof(CharacterInfoWindowView))] [TestCase(typeof(TradeServiceWindowView))]
        [TestCase(typeof(DungeonLootWindowView))] [TestCase(typeof(InventoryEquipmentPanelView))] [TestCase(typeof(TradePanelView))]
        public void QuantityAndTotalStackValueCannotOverrideUnitPrice(Type view)
        {
            var cheap = Definition("cheap", "Ten sam przedmiot", price: 10);
            var expensive = Definition("expensive", "Ten sam przedmiot", price: 30);
            var items = Enumerable.Range(0, 20).Select(i => Item(cheap, "cheap" + i)).ToList();
            items.Insert(10, Item(expensive, "expensive"));
            var original = items.ToArray();
            Assert.That(Stacks(view, items).Select(s => Representative(s).Definition.Id), Is.EqualTo(new[] { "expensive", "cheap" }));
            Assert.That(items, Is.EqualTo(original));
        }

        [TestCase(false)] [TestCase(true)]
        public void EqualVisibleKeysKeepVariantOrderWhenRepresentativeChanges(bool nullVsEmptyName)
        {
            var definition = Asset<ConsumableItemDefinition>();
            Set(definition, "Id", "potion"); Set(definition, "DisplayName", "Napar");
            Set(definition, "ItemType", ItemType.Potion); Set(definition, "IsStackable", true); Set(definition, "BaseValue", 10);
            ConsumableItemInstance Potion(string id, float value) => new(definition, id)
            {
                CurrentCharges = 1,
                RuntimeEffects = new() { new ConsumableEffectRuntime { Value = value } }
            };
            var first = Potion("000", 10); var same = Potion("zzz", 10); var other = Potion("mmm", 20);
            if (nullVsEmptyName)
            {
                first.CustomName = null; same.CustomName = null; other.CustomName = string.Empty;
                other.RuntimeEffects[0].Value = 10;
            }
            Assert.That(first.CanStackWith(same), Is.True); Assert.That(first.CanStackWith(other), Is.False);
            var items = new List<ItemInstance> { first, other, same };
            var before = Stacks(typeof(CharacterInfoWindowView), items).Select(Representative).ToArray();
            items.Remove(first);
            var after = Stacks(typeof(CharacterInfoWindowView), items).Select(Representative).ToArray();
            Assert.That(after.Length, Is.EqualTo(before.Length));
            Assert.That(before.Zip(after, (left, right) => left.CanStackWith(right)).All(equal => equal), Is.True,
                "Different recipes cannot move just because a stack's representative ID changed.");
        }

        [Test]
        public void EqualEquipmentUsesStableInstanceIdentityWithoutMergingIt()
        {
            var definition = Definition("weapon", "Miecz", ItemType.Weapon); Set(definition, "IsStackable", false);
            var first = Item(definition, "a"); var second = Item(definition, "b");
            Assert.That(ItemDisplayOrder.Sort(new[] { second, first }, i => i), Is.EqualTo(new[] { first, second }));
            Assert.That(Stacks(typeof(CharacterInfoWindowView), new() { second, first }).Count, Is.EqualTo(2));
        }

        [Test]
        public void SaleProtectionStillSeparatesStacksAndDoesNotUseRepresentativeId()
        {
            var definition = Definition("material", "Materiał");
            var protectedFirst = Item(definition, "a"); protectedFirst.IsSaleProtected = true;
            var protectedNext = Item(definition, "z"); protectedNext.IsSaleProtected = true;
            var unprotected = Item(definition, "m");
            var items = new List<ItemInstance> { protectedFirst, unprotected, protectedNext };
            var before = Stacks(typeof(CharacterInfoWindowView), items).Select(s => Representative(s).IsSaleProtected).ToArray();
            items.Remove(protectedFirst);
            Assert.That(Stacks(typeof(CharacterInfoWindowView), items).Select(s => Representative(s).IsSaleProtected), Is.EqualTo(before));
        }

        [Test]
        public void DisplayPriceMatchesTooltipIncludingCraftingFallback()
        {
            var ingredient = Definition("ingredient", "Składnik", price: 20);
            var recipe = Asset<CraftableItemDefinition>();
            Set(recipe, "Id", "recipe"); Set(recipe, "DisplayName", "Produkt");
            recipe.Ingredients.Add(new CraftingMaterialRequirement(ingredient, 2));
            var item = Item(recipe, "product");
            Assert.That(ItemDisplayOrder.TryGetIntrinsicValue(item, out var value), Is.True);
            Assert.That(value, Is.EqualTo(40));
            Assert.That(UIWorkspaceTooltipView.BuildItemMeta(item), Does.Contain("40"));
        }

        [TestCase(false)] [TestCase(true)]
        public void StorageAndItsInventoryPaneUseTheSameStableGridOrder(bool isStash)
        {
            var root = new GameObject("storage ordering", typeof(RectTransform)); assets.Add(root);
            var window = root.AddComponent<CampaignStorageWindowView>();
            var templateRoot = new GameObject("template", typeof(RectTransform)); templateRoot.transform.SetParent(root.transform);
            var template = templateRoot.AddComponent<CampaignStorageItemView>(); templateRoot.SetActive(false);
            typeof(CampaignStorageWindowView).GetField("itemTemplate", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, template);
            var a = Definition("a", "Alfa"); var b = Definition("b", "Beta");
            var first = Item(a, "a1");
            var items = new List<ItemInstance> { first, Item(b, "b1"), Item(a, "a2") };
            string[] Draw(string name)
            {
                var content = new GameObject(name, typeof(RectTransform)); content.transform.SetParent(root.transform);
                typeof(CampaignStorageWindowView).GetMethod("AddStacks", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(window, new object[] { items, content.transform, isStash });
                return content.GetComponentsInChildren<CampaignStorageItemView>().Select(view => view.Item.Definition.Id).ToArray();
            }
            Assert.That(Draw("before"), Is.EqualTo(new[] { "a", "b" }));
            items.Remove(first);
            Assert.That(Draw("after"), Is.EqualTo(new[] { "a", "b" }));
        }
    }
}
