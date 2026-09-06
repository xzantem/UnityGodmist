using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class NpcInstanceFactoryTests
    {
        [Test]
        public void Create_TraderNpc_CreatesSeparateShopItemInstances()
        {
            var material = CreateItemDefinition("material.test", ItemType.Material);
            var weapon = CreateItemDefinition("weapon.test", ItemType.Weapon);
            var traderRole = ScriptableObject.CreateInstance<TraderRoleData>();

            SetAutoProperty(
                traderRole,
                "InitialOffer",
                new List<ItemDefinition> { material, weapon, material });

            var npcDefinition = CreateNpcDefinition(
                "npc.test_trader",
                new List<NpcRoleData> { traderRole });

            var factory = new NpcInstanceFactory();

            var npc = factory.Create(npcDefinition);

            Assert.That(npc.GoldBalance, Is.EqualTo(250));
            Assert.That(npc.ShopInventory, Is.Not.Null);
            Assert.That(npc.ShopInventory.Items, Has.Count.EqualTo(3));
            Assert.That(npc.ShopInventory.Items[0].Definition, Is.SameAs(material));
            Assert.That(npc.ShopInventory.Items[1].Definition, Is.SameAs(weapon));
            Assert.That(
                npc.ShopInventory.Items[0].InstanceId,
                Is.Not.EqualTo(npc.ShopInventory.Items[2].InstanceId));
        }

        [Test]
        public void Create_NonTraderNpc_DoesNotCreateShopInventory()
        {
            var npcDefinition = CreateNpcDefinition(
                "npc.test_questgiver",
                new List<NpcRoleData>());

            var factory = new NpcInstanceFactory();

            var npc = factory.Create(npcDefinition);

            Assert.That(npc.ShopInventory, Is.Null);
        }

        private static NpcDefinition CreateNpcDefinition(
            string id,
            List<NpcRoleData> roles)
        {
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "Roles", roles);

            return definition;
        }

        private static ItemDefinition CreateItemDefinition(
            string id,
            ItemType itemType)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", itemType);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            return definition;
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
