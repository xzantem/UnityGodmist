using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class InventoryServiceTests
    {
        private InventoryService inventoryService;

        [SetUp]
        public void SetUp()
        {
            inventoryService = new InventoryService();
        }

        [Test]
        public void ItemInstance_UsesDefinitionRarityAndGeneratesInstanceId()
        {
            var definition = CreateDefinition(
                "material.iron_ore",
                0f,
                RarityTier.Common);

            var instance = new ItemInstance(definition);

            Assert.That(instance.InstanceId, Is.Not.Empty);
            Assert.That(instance.Definition, Is.SameAs(definition));
            Assert.That(instance.CurrentRarity, Is.EqualTo(RarityTier.Common));
            Assert.That(instance.ItemLevel, Is.EqualTo(0));
        }

        [Test]
        public void TryAddItem_AddsDifferentInstancesOfTheSameDefinition()
        {
            var definition = CreateDefinition(
                "material.iron_ore",
                1f,
                RarityTier.Common);

            var inventory = new Inventory { MaxWeight = 5f };
            var firstItem = new ItemInstance(definition, "item.first");
            var secondItem = new ItemInstance(definition, "item.second");

            Assert.That(inventoryService.TryAddItem(inventory, firstItem), Is.True);
            Assert.That(inventoryService.TryAddItem(inventory, secondItem), Is.True);
            Assert.That(inventory.Items, Has.Count.EqualTo(2));
            Assert.That(inventoryService.GetCurrentWeight(inventory), Is.EqualTo(2f));
        }

        [Test]
        public void TryAddItem_RejectsDuplicateInstanceId()
        {
            var definition = CreateDefinition(
                "material.iron_ore",
                1f,
                RarityTier.Common);

            var inventory = new Inventory();
            var firstItem = new ItemInstance(definition, "item.duplicate");
            var duplicateItem = new ItemInstance(definition, "item.duplicate");

            Assert.That(inventoryService.TryAddItem(inventory, firstItem), Is.True);
            Assert.That(inventoryService.TryAddItem(inventory, duplicateItem), Is.False);
            Assert.That(inventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryAddItem_RejectsItemOverWeightLimit()
        {
            var definition = CreateDefinition(
                "weapon.test",
                3f,
                RarityTier.Common);

            var inventory = new Inventory { MaxWeight = 2f };
            var item = new ItemInstance(definition, "item.heavy");

            Assert.That(inventoryService.TryAddItem(inventory, item), Is.False);
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void TryRemoveItem_RemovesMatchingInstance()
        {
            var definition = CreateDefinition(
                "material.iron_ore",
                1f,
                RarityTier.Common);

            var inventory = new Inventory();
            var item = new ItemInstance(definition, "item.iron_01");

            inventoryService.TryAddItem(inventory, item);

            var wasRemoved = inventoryService.TryRemoveItem(
                inventory,
                "item.iron_01",
                out var removedItem);

            Assert.That(wasRemoved, Is.True);
            Assert.That(removedItem, Is.SameAs(item));
            Assert.That(inventory.Items, Is.Empty);
        }

        private static ItemDefinition CreateDefinition(
            string id,
            float weight,
            RarityTier rarity)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "Weight", weight);
            SetAutoProperty(definition, "BaseRarity", rarity);

            return definition;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            field.SetValue(target, value);
        }
    }
}