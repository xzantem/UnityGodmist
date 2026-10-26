using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class JewelryGenerationServiceTests
    {
        [Test]
        public void GenerateSelected_UsesSelectedMetalForLevelRarityAndStats()
        {
            var definition = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            Set(definition, "Id", "jewelry.ring");
            Set(definition, "ItemType", ItemType.Jewelry);
            Set(definition, "JewelrySlot", EquipmentSlot.Ring);
            var chain = CreateMaterial("item.smithing.iron_chain", "Żelazny łańcuch", 100, RarityTier.Common);
            var topaz = CreateMaterial("item.gem.topaz", "Topaz", 1151, RarityTier.Uncommon);
            var metal = new JewelryMetalData();
            Set(metal, "Material", chain);
            Set(metal, "Amount", 1);
            Set(metal, "Modifiers", new List<JewelryModifierData>
            {
                new()
            });
            Set(metal.Modifiers[0], "Target", JewelryModifierTarget.Stat);
            Set(metal.Modifiers[0], "StatType", CharacterStatType.MaxHealth);
            Set(metal.Modifiers[0], "Value", 30f);
            var gem = ScriptableObject.CreateInstance<JewelryGemDefinition>();
            Set(gem, "CraftingMaterial", topaz);

            var result = new JewelryGenerationService().GenerateSelected(definition, metal, gem);

            Assert.That(result.ItemLevel, Is.EqualTo(10));
            Assert.That(result.CurrentRarity, Is.EqualTo(RarityTier.Common));
            Assert.That(result.MetalId, Is.EqualTo("item.smithing.iron_chain:1"));
            Assert.That(result.GemId, Is.EqualTo("item.gem.topaz"));
            Assert.That(result.StatModifiers, Has.Count.EqualTo(1));
            Assert.That(result.DisplayName, Is.EqualTo("Żelazny pierścień z topazem"));
            Assert.That(result.RequiredLevel, Is.EqualTo(10));
            Assert.That(result.RequiredClassId, Is.Empty);
            Assert.That(result.BaseValue, Is.EqualTo(1251));

            Object.DestroyImmediate(chain);
            Object.DestroyImmediate(topaz);
            Object.DestroyImmediate(gem);
            Object.DestroyImmediate(definition);
        }

        [TestCase(EquipmentSlot.Ring, "Sztabka żelaza", "Żelazny pierścień z granatem")]
        [TestCase(EquipmentSlot.Necklace, "Sztabka niklu", "Niklowy naszyjnik z granatem")]
        [TestCase(EquipmentSlot.Bracelet, "Sztabka miedzi", "Miedziana bransoleta z granatem")]
        [TestCase(EquipmentSlot.Earrings, "Sztabka stali", "Stalowe kolczyki z granatem")]
        public void GeneratedName_UsesMetalAndJewelryGender(EquipmentSlot slot, string metal, string expected)
        {
            var definition = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            try
            {
                Set(definition, "Id", "jewelry.naming.test");
                Set(definition, "ItemType", ItemType.Jewelry);
                Set(definition, "JewelrySlot", slot);
                var item = new Godmist.Runtime.Items.JewelryItemInstance(definition)
                    { MetalDisplayName = metal, GemDisplayName = "Granat" };
                Assert.That(item.DisplayName, Is.EqualTo(expected));
                item.CustomName = "Moja biżuteria";
                Assert.That(item.DisplayName, Is.EqualTo("Moja biżuteria"));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static ItemDefinition CreateMaterial(string id, string displayName, int baseValue, RarityTier rarity)
        {
            var material = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(material, "Id", id);
            Set(material, "DisplayName", displayName);
            Set(material, "BaseValue", baseValue);
            Set(material, "BaseRarity", rarity);
            return material;
        }

        private static void Set(object target, string property, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField($"<{property}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
        }
    }
}
