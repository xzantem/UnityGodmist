using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Core.Types;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EquipmentServiceTests
    {
        private InventoryService inventoryService;
        private EquipmentService equipmentService;

        [SetUp]
        public void SetUp()
        {
            inventoryService = new InventoryService();
            equipmentService = new EquipmentService();
        }

        [Test]
        public void TryEquip_ValidWeapon_MovesItFromInventoryToSlot()
        {
            var character = CreateCharacter("class.scout", 1);
            var weapon = CreateEquipmentItem(
                "weapon.scout.test",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.weapon_01");

            inventoryService.TryAddItem(character.Inventory, weapon);

            var equipped = equipmentService.TryEquip(
                character,
                weapon.InstanceId);

            Assert.That(equipped, Is.True);
            Assert.That(character.Equipment.Weapon, Is.SameAs(weapon));
            Assert.That(character.Inventory.Items, Is.Empty);
        }

        [Test]
        public void TryEquip_ClassRequirementNotMet_LeavesItemInInventory()
        {
            var character = CreateCharacter("class.scout", 5);
            var weapon = CreateEquipmentItem(
                "weapon.warrior.test",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.warrior_weapon");

            weapon.RequiredClassId = "class.warrior";

            inventoryService.TryAddItem(character.Inventory, weapon);

            var equipped = equipmentService.TryEquip(
                character,
                weapon.InstanceId);

            Assert.That(equipped, Is.False);
            Assert.That(character.Equipment.Weapon, Is.Null);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryEquip_LevelRequirementNotMet_LeavesItemInInventory()
        {
            var character = CreateCharacter("class.scout", 2);
            var weapon = CreateEquipmentItem(
                "weapon.scout.high_level",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.high_level_weapon");

            weapon.RequiredLevel = 3;

            inventoryService.TryAddItem(character.Inventory, weapon);

            var equipped = equipmentService.TryEquip(
                character,
                weapon.InstanceId);

            Assert.That(equipped, Is.False);
            Assert.That(character.Equipment.Weapon, Is.Null);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryEquip_WrongItemTypeForSlot_IsRejected()
        {
            var character = CreateCharacter("class.scout", 1);
            var materialAsWeapon = CreateEquipmentItem(
                "material.test",
                ItemType.Material,
                EquipmentSlot.Weapon,
                "item.invalid_weapon");

            inventoryService.TryAddItem(character.Inventory, materialAsWeapon);

            var equipped = equipmentService.TryEquip(
                character,
                materialAsWeapon.InstanceId);

            Assert.That(equipped, Is.False);
            Assert.That(character.Equipment.Weapon, Is.Null);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryEquip_ReplacesExistingWeaponAndReturnsOldOneToInventory()
        {
            var character = CreateCharacter("class.scout", 1);

            var oldWeapon = CreateEquipmentItem(
                "weapon.scout.old",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.old_weapon");

            var newWeapon = CreateEquipmentItem(
                "weapon.scout.new",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.new_weapon");

            inventoryService.TryAddItem(character.Inventory, oldWeapon);
            equipmentService.TryEquip(character, oldWeapon.InstanceId);

            inventoryService.TryAddItem(character.Inventory, newWeapon);

            var equipped = equipmentService.TryEquip(
                character,
                newWeapon.InstanceId);

            Assert.That(equipped, Is.True);
            Assert.That(character.Equipment.Weapon, Is.SameAs(newWeapon));
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items[0], Is.SameAs(oldWeapon));
        }

        [Test]
        public void TryUnequip_ReturnsItemToInventory()
        {
            var character = CreateCharacter("class.scout", 1);
            var weapon = CreateEquipmentItem(
                "weapon.scout.test",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.weapon_01");

            inventoryService.TryAddItem(character.Inventory, weapon);
            equipmentService.TryEquip(character, weapon.InstanceId);

            var unequipped = equipmentService.TryUnequip(
                character,
                EquipmentSlot.Weapon);

            Assert.That(unequipped, Is.True);
            Assert.That(character.Equipment.Weapon, Is.Null);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items[0], Is.SameAs(weapon));
        }

        private static CharacterInstance CreateCharacter(
            string classId,
            int level)
        {
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();

            SetAutoProperty(classData, "Id", classId);

            return new CharacterInstance
            {
                Level = level,
                ClassData = classData,
                Inventory = new Inventory(),
                Equipment = new EquipmentSet()
            };
        }

        private static EquipmentItemInstance CreateEquipmentItem(
            string definitionId,
            ItemType itemType,
            EquipmentSlot slot,
            string instanceId)
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();

            SetAutoProperty(definition, "Id", definitionId);
            SetAutoProperty(definition, "DisplayName", definitionId);
            SetAutoProperty(definition, "ItemType", itemType);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "Weight", 1f);
            SetAutoProperty(definition, "EquipmentSlot", slot);

            return new EquipmentItemInstance(definition, instanceId);
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
                $"Nie znaleziono backing field dla właściwości '{propertyName}'.");

            field.SetValue(target, value);
        }

        [Test]
        public void TryEquip_StatModifiers_RefreshesCharacterStats()
        {
            var character = CreateCharacter("class.scout", 1);

            character.Stats = new CharacterStats
            {
                Speed = new Stat(100f)
            };

            var weapon = CreateEquipmentItem(
                "weapon.scout.speed",
                ItemType.Weapon,
                EquipmentSlot.Weapon,
                "item.speed_weapon");

            weapon.StatModifiers.Add(new StatModifier
            {
                StatType = CharacterStatType.Speed,
                Operation = StatModifierOperation.Percent,
                Stacking = StatModifierStacking.Additive,
                Value = 0.10f
            });

            inventoryService.TryAddItem(character.Inventory, weapon);

            Assert.That(equipmentService.TryEquip(character, weapon.InstanceId), Is.True);
            Assert.That(character.Stats.Speed.GetValue(), Is.EqualTo(110f).Within(0.001f));

            Assert.That(
                equipmentService.TryUnequip(character, EquipmentSlot.Weapon),
                Is.True);

            Assert.That(character.Stats.Speed.GetValue(), Is.EqualTo(100f).Within(0.001f));
        }
    }
}