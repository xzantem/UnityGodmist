using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ItemInstanceFactoryTests
    {
        [Test]
        public void Create_RegularItemDefinition_CreatesRegularItemInstance()
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", "item.test_material");
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            var factory = new ItemInstanceFactory();

            var item = factory.Create(definition);

            Assert.That(item, Is.TypeOf<ItemInstance>());
            Assert.That(item.Definition, Is.SameAs(definition));
            Assert.That(item.CurrentRarity, Is.EqualTo(RarityTier.Common));
        }

        [Test]
        public void Create_EquipmentItemDefinition_CreatesEquipmentItemInstance()
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();

            SetAutoProperty(definition, "Id", "item.test_weapon");
            SetAutoProperty(definition, "ItemType", ItemType.Weapon);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            var factory = new ItemInstanceFactory();

            var item = factory.Create(definition);

            Assert.That(item, Is.TypeOf<EquipmentItemInstance>());

            var equipmentItem = item as EquipmentItemInstance;

            Assert.That(equipmentItem.Definition, Is.SameAs(definition));
            Assert.That(
                equipmentItem.EquipmentSlot,
                Is.EqualTo(EquipmentSlot.Weapon));
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
