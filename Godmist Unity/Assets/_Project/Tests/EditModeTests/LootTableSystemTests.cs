using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class LootTableSystemTests
    {
        [Test]
        public void RollLoot_GuaranteedTwoItems_CreatesSeparateInstances()
        {
            var itemDefinition = CreateItemDefinition("item.test_material");
            var lootTable = CreateLootTable(
                itemDefinition,
                dropChance: 1f,
                amount: new IntRange(2, 2));

            var lootTableSystem = new LootTableSystem(() => 0f);

            var items = lootTableSystem.RollLoot(lootTable);

            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items[0].Definition, Is.SameAs(itemDefinition));
            Assert.That(items[1].Definition, Is.SameAs(itemDefinition));
            Assert.That(items[0].InstanceId, Is.Not.EqualTo(items[1].InstanceId));
        }

        [Test]
        public void RollLoot_ZeroDropChance_ReturnsNoItems()
        {
            var itemDefinition = CreateItemDefinition("item.never_drops");
            var lootTable = CreateLootTable(
                itemDefinition,
                dropChance: 0f,
                amount: new IntRange(1, 1));

            var lootTableSystem = new LootTableSystem(() => 0.5f);

            var items = lootTableSystem.RollLoot(lootTable);

            Assert.That(items, Is.Empty);
        }

        [Test]
        public void RollLoot_MaximumRandomValue_ReturnsMaximumAmount()
        {
            var itemDefinition = CreateItemDefinition("item.range_test");
            var lootTable = CreateLootTable(
                itemDefinition,
                dropChance: 1f,
                amount: new IntRange(1, 3));

            var randomValues = new Queue<float>(new[] { 0f, 1f });

            var lootTableSystem = new LootTableSystem(
                () => randomValues.Dequeue());

            var items = lootTableSystem.RollLoot(lootTable);

            Assert.That(items, Has.Count.EqualTo(3));
        }

        [Test]
        public void RollLoot_EquipmentDefinition_CreatesEquipmentItemInstance()
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();

            SetAutoProperty(definition, "Id", "item.test_weapon");
            SetAutoProperty(definition, "DisplayName", "Testowy miecz");
            SetAutoProperty(definition, "ItemType", ItemType.Weapon);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "EquipmentSlot", EquipmentSlot.Weapon);

            var lootTable = CreateLootTable(
                definition,
                dropChance: 1f,
                amount: new IntRange(1, 1));

            var lootTableSystem = new LootTableSystem(() => 0f);

            var items = lootTableSystem.RollLoot(lootTable);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0], Is.TypeOf<EquipmentItemInstance>());

            var equipmentItem = items[0] as EquipmentItemInstance;

            Assert.That(equipmentItem.EquipmentSlot, Is.EqualTo(EquipmentSlot.Weapon));
        }

        private static ItemDefinition CreateItemDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            return definition;
        }

        private static LootTableData CreateLootTable(
            ItemDefinition itemDefinition,
            float dropChance,
            IntRange amount)
        {
            var lootTable = ScriptableObject.CreateInstance<LootTableData>();
            var entry = new LootEntryData();

            SetAutoProperty(entry, "ItemDefinition", itemDefinition);
            SetAutoProperty(entry, "DropChance", dropChance);
            SetAutoProperty(entry, "Amount", amount);

            lootTable.Entries.Add(entry);

            return lootTable;
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
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}