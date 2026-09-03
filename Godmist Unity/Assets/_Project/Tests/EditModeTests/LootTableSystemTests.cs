using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
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

        [Test]
        public void RollLoot_Jewelry_GeneratesMetalStatsAndGemPassive()
        {
            var definition = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            SetAutoProperty(definition, "Id", "jewelry.test");
            SetAutoProperty(definition, "DisplayName", "pierścień testowy");
            SetAutoProperty(definition, "ItemType", ItemType.Jewelry);
            SetAutoProperty(definition, "JewelrySlot", EquipmentSlot.Ring);

            var metal = new JewelryMetalData();
            SetAutoProperty(metal, "Id", "iron");
            SetAutoProperty(metal, "DisplayName", "Żelazny");
            SetAutoProperty(metal, "MinimumLevel", 1);
            SetAutoProperty(metal, "StatModifiers", new List<StatModifier>
            {
                new()
                {
                    StatType = CharacterStatType.MinAttack,
                    Operation = StatModifierOperation.FlatBase,
                    Value = 2f
                }
            });

            var gem = new JewelryGemData();
            SetAutoProperty(gem, "Id", "topaz");
            SetAutoProperty(gem, "DisplayName", "Topaz");
            var passive = new PassiveEffectDefinition();
            var operation = new PassiveEffectOperationDefinition();
            SetAutoProperty(passive, "Id", "jewelry.gem.topaz.experience");
            SetAutoProperty(operation, "Id", "operation.experience");
            SetAutoProperty(operation, "Type", PassiveEffectOperationType.ModifyGameplayVariable);
            SetAutoProperty(operation, "VariableType", GameplayVariableType.ExperienceGainPercent);
            var passiveValue = new PassiveEffectValueDefinition();
            SetAutoProperty(passiveValue, "NumberValue", .01f);
            SetAutoProperty(operation, "Value", passiveValue);
            SetAutoProperty(passive, "Operations", new List<PassiveEffectOperationDefinition> { operation });
            SetAutoProperty(gem, "PassiveEffects", new List<PassiveEffectDefinition> { passive });
            SetAutoProperty(definition, "Metals", new List<JewelryMetalData> { metal });
            SetAutoProperty(definition, "Gems", new List<JewelryGemData> { gem });

            var items = new LootTableSystem(() => 0f, lootLevel: 1)
                .RollLoot(CreateLootTable(definition, 1f, new IntRange(1, 1)));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0], Is.TypeOf<JewelryItemInstance>());

            var jewelry = (JewelryItemInstance)items[0];
            Assert.That(jewelry.EquipmentSlot, Is.EqualTo(EquipmentSlot.Ring));
            Assert.That(jewelry.MetalId, Is.EqualTo("iron"));
            Assert.That(jewelry.GemId, Is.EqualTo("topaz"));
            Assert.That(gem.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(jewelry.StatModifiers, Has.Count.EqualTo(1));
        }

        [Test]
        public void RollLoot_EquipmentWithParts_GeneratesCompleteEquipmentPayload()
        {
            var scout = ScriptableObject.CreateInstance<CharacterClassData>();
            SetAutoProperty(scout, "Id", "class.scout");

            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            SetAutoProperty(definition, "Id", "equipment.scout.test");
            SetAutoProperty(definition, "DisplayName", "Broń testowa");
            SetAutoProperty(definition, "ItemType", ItemType.Weapon);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "RequiredClass", scout);

            var lootTable = CreateLootTable(
                definition,
                dropChance: 1f,
                amount: new IntRange(1, 1));
            var parts = new[]
            {
                CreatePart("head", scout, EquipmentPartKind.WeaponHead),
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder),
                CreatePart("handle", scout, EquipmentPartKind.WeaponHandle)
            };

            var items = new LootTableSystem(new System.Random(7), parts)
                .RollLoot(lootTable);

            Assert.That(items, Has.Count.EqualTo(1));
            var equipment = items[0] as EquipmentItemInstance;
            Assert.That(equipment, Is.Not.Null);
            Assert.That(equipment.RequiredClassId, Is.EqualTo("class.scout"));
            Assert.That(equipment.PartIds, Is.EquivalentTo(new[] { "head", "binder", "handle" }));
            Assert.That(equipment.StatModifiers, Is.Not.Empty);
        }

        [Test]
        public void RollLoot_LootBag_UsesOneEntryFromEachTriggeredPool()
        {
            var material = CreateItemDefinition("item.pool_material");
            var galduryte = CreateItemDefinition("item.pool_galduryte");
            var lootBagDefinition = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            SetAutoProperty(lootBagDefinition, "Id", "item.test_lootbag");
            SetAutoProperty(lootBagDefinition, "ItemType", ItemType.Lootbag);
            SetAutoProperty(lootBagDefinition, "BaseRarity", RarityTier.Common);

            var bracket = new LootBagBracketData();
            SetAutoProperty(bracket, "Bracket", LootLevelBracket.Level1To10);
            bracket.Pools.Add(CreatePool(
                1f,
                CreatePoolEntry(material, new IntRange(2, 2)),
                CreatePoolEntry(galduryte, new IntRange(1, 1))));
            bracket.Pools.Add(CreatePool(
                1f,
                CreatePoolEntry(material, new IntRange(3, 3))));
            lootBagDefinition.Brackets.Add(bracket);

            var sourceTable = ScriptableObject.CreateInstance<LootTableData>();
            sourceTable.Entries.Add(CreateLootEntry(
                lootBagDefinition,
                1f,
                new IntRange(1, 1)));

            var loot = new LootTableSystem(() => 0f, lootLevel: 1)
                .RollLoot(sourceTable);

            Assert.That(loot, Has.Count.EqualTo(1));
            Assert.That(loot[0], Is.TypeOf<LootBagItemInstance>());

            var bag = (LootBagItemInstance)loot[0];
            Assert.That(bag.ItemLevel, Is.EqualTo(1));
            Assert.That(bag.Contents, Has.Count.EqualTo(5));
            Assert.That(
                bag.Contents.FindAll(item => item.Definition == material),
                Has.Count.EqualTo(5));
            Assert.That(
                bag.Contents.FindAll(item => item.Definition == galduryte),
                Has.Count.EqualTo(0));
        }

        [Test]
        public void RollLoot_LootBag_SetsLevelBracketFromLootLevel()
        {
            var levelTenItem = CreateItemDefinition("item.bracket_1_10");
            var levelElevenItem = CreateItemDefinition("item.bracket_11_20");
            var lootBagDefinition = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            SetAutoProperty(lootBagDefinition, "Id", "item.bracket_lootbag");
            SetAutoProperty(lootBagDefinition, "ItemType", ItemType.Lootbag);
            SetAutoProperty(lootBagDefinition, "BaseRarity", RarityTier.Common);

            var firstBracket = new LootBagBracketData();
            SetAutoProperty(firstBracket, "Bracket", LootLevelBracket.Level1To10);
            firstBracket.Pools.Add(CreatePool(
                1f,
                CreatePoolEntry(levelTenItem, new IntRange(1, 1))));

            var secondBracket = new LootBagBracketData();
            SetAutoProperty(secondBracket, "Bracket", LootLevelBracket.Level11To20);
            secondBracket.Pools.Add(CreatePool(
                1f,
                CreatePoolEntry(levelElevenItem, new IntRange(1, 1))));

            lootBagDefinition.Brackets.Add(firstBracket);
            lootBagDefinition.Brackets.Add(secondBracket);

            var sourceTable = ScriptableObject.CreateInstance<LootTableData>();
            sourceTable.Entries.Add(CreateLootEntry(
                lootBagDefinition,
                1f,
                new IntRange(1, 1)));

            var bag = (LootBagItemInstance)new LootTableSystem(
                    () => 0f,
                    lootLevel: 11)
                .RollLoot(sourceTable)[0];

            Assert.That(bag.ItemLevel, Is.EqualTo(11));
            Assert.That(bag.Contents, Has.Count.EqualTo(1));
            Assert.That(bag.Contents[0].Definition, Is.SameAs(levelElevenItem));
        }

        [Test]
        public void RollLoot_LootBag_BossUniqueDrop_SelectsOneOfFourAndForcesMasterpiece()
        {
            var classes = new[]
            {
                CreateClass("class.warrior"),
                CreateClass("class.scout"),
                CreateClass("class.mage"),
                CreateClass("class.paladin")
            };
            var uniqueDefinitions = new List<BossUniqueEquipmentDefinition>();
            var parts = new List<EquipmentPartDefinition>();

            for (var index = 0; index < classes.Length; index++)
            {
                var definition = ScriptableObject.CreateInstance<
                    BossUniqueEquipmentDefinition>();
                SetAutoProperty(definition, "Id", $"unique.{index}");
                SetAutoProperty(definition, "DisplayName", $"Unikat {index}");
                SetAutoProperty(definition, "ItemType", ItemType.Weapon);
                SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
                SetAutoProperty(definition, "RequiredClass", classes[index]);
                SetAutoProperty(definition, "BossId", "boss.test");
                uniqueDefinitions.Add(definition);

                parts.Add(CreatePart(
                    $"head.{index}", classes[index], EquipmentPartKind.WeaponHead));
                parts.Add(CreatePart(
                    $"binder.{index}", classes[index], EquipmentPartKind.WeaponBinder));
                parts.Add(CreatePart(
                    $"handle.{index}", classes[index], EquipmentPartKind.WeaponHandle));
            }

            var lootBagDefinition = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            SetAutoProperty(lootBagDefinition, "Id", "item.boss_unique_lootbag");
            SetAutoProperty(lootBagDefinition, "ItemType", ItemType.Lootbag);
            SetAutoProperty(lootBagDefinition, "BaseRarity", RarityTier.Common);

            var bracket = new LootBagBracketData();
            SetAutoProperty(bracket, "Bracket", LootLevelBracket.Level1To10);
            lootBagDefinition.Brackets.Add(bracket);
            SetAutoProperty(lootBagDefinition, "BossUniqueDrop", new BossUniqueDropData());
            SetAutoProperty(
                lootBagDefinition.BossUniqueDrop,
                "EquipmentDefinitions",
                uniqueDefinitions);

            var sourceTable = ScriptableObject.CreateInstance<LootTableData>();
            sourceTable.Entries.Add(CreateLootEntry(
                lootBagDefinition,
                1f,
                new IntRange(1, 1)));

            var randomValues = new Queue<float>(new[] { 0f, 0f, .51f });
            var loot = new LootTableSystem(
                    () => randomValues.Dequeue(),
                    new ItemInstanceFactory(),
                    new EquipmentGenerationService(new System.Random(7)),
                    parts,
                    lootLevel: 1)
                .RollLoot(sourceTable);

            var bag = (LootBagItemInstance)loot[0];
            Assert.That(bag.Contents, Has.Count.EqualTo(1));

            var equipment = bag.Contents[0] as EquipmentItemInstance;
            Assert.That(equipment, Is.Not.Null);
            Assert.That(equipment.Definition, Is.SameAs(uniqueDefinitions[2]));
            Assert.That(equipment.Quality, Is.EqualTo(EquipmentQuality.Masterpiece));
            Assert.That(equipment.ItemLevel, Is.EqualTo(10));
            Assert.That(equipment.RequiredLevel, Is.EqualTo(equipment.ItemLevel));
            Assert.That(equipment.PartIds, Has.Count.EqualTo(3));
        }

        [Test]
        public void LootBagService_OpensWhatFitsAndLeavesRemainingContents()
        {
            var gameSession = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.lootbag",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory { MaxWeight = 1f }
            };
            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToActiveParty(character.CharacterId);

            var bagDefinition = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            SetAutoProperty(bagDefinition, "Id", "item.service_lootbag");
            SetAutoProperty(bagDefinition, "ItemType", ItemType.Lootbag);
            SetAutoProperty(bagDefinition, "BaseRarity", RarityTier.Common);

            var bag = new LootBagItemInstance(bagDefinition, "bag.service.01");
            var fittingItem = CreateItem("item.fitting", 0.5f);
            var heavyItem = CreateItem("item.heavy", 2f);
            bag.Contents.Add(fittingItem);
            bag.Contents.Add(heavyItem);
            character.Inventory.Items.Add(bag);

            var opened = new LootBagService().TryOpenLootBag(
                gameSession,
                character.CharacterId,
                bag.InstanceId,
                out var openedItemCount);

            Assert.That(opened, Is.True);
            Assert.That(openedItemCount, Is.EqualTo(1));
            Assert.That(
                character.Inventory.Items.Contains(fittingItem),
                Is.True);
            Assert.That(
                bag.Contents.Contains(heavyItem),
                Is.True);
            Assert.That(
                bag.Contents.Contains(fittingItem),
                Is.False);
            Assert.That(
                character.Inventory.Items.Contains(bag),
                Is.True);
        }

        [Test]
        public void LootBagService_RemovesBagAfterAllContentsAreOpened()
        {
            var gameSession = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.empty_lootbag",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory { MaxWeight = 1f }
            };
            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToActiveParty(character.CharacterId);

            var bagDefinition = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            SetAutoProperty(bagDefinition, "Id", "item.empty_lootbag");
            SetAutoProperty(bagDefinition, "ItemType", ItemType.Lootbag);
            SetAutoProperty(bagDefinition, "BaseRarity", RarityTier.Common);

            var bag = new LootBagItemInstance(bagDefinition, "bag.empty.01");
            var item = CreateItem("item.only", 0.5f);
            bag.Contents.Add(item);
            character.Inventory.Items.Add(bag);

            var opened = new LootBagService().TryOpenLootBag(
                gameSession,
                character.CharacterId,
                bag.InstanceId,
                out var openedItemCount);

            Assert.That(opened, Is.True);
            Assert.That(openedItemCount, Is.EqualTo(1));
            Assert.That(character.Inventory.Items.Contains(item), Is.True);
            Assert.That(character.Inventory.Items.Contains(bag), Is.False);
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

        private static CharacterClassData CreateClass(string id)
        {
            var characterClass = ScriptableObject.CreateInstance<CharacterClassData>();
            SetAutoProperty(characterClass, "Id", id);
            return characterClass;
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

        private static LootEntryData CreateLootEntry(
            ItemDefinition itemDefinition,
            float dropChance,
            IntRange amount)
        {
            var entry = new LootEntryData();
            SetAutoProperty(entry, "ItemDefinition", itemDefinition);
            SetAutoProperty(entry, "DropChance", dropChance);
            SetAutoProperty(entry, "Amount", amount);
            return entry;
        }

        private static LootBagPoolData CreatePool(
            float dropChance,
            params LootBagPoolEntryData[] entries)
        {
            var pool = new LootBagPoolData();
            SetAutoProperty(pool, "DropChance", dropChance);

            foreach (var entry in entries)
            {
                pool.Entries.Add(entry);
            }

            return pool;
        }

        private static LootBagPoolEntryData CreatePoolEntry(
            ItemDefinition itemDefinition,
            IntRange amount)
        {
            var entry = new LootBagPoolEntryData();
            SetAutoProperty(entry, "ItemDefinition", itemDefinition);
            SetAutoProperty(entry, "Amount", amount);
            return entry;
        }

        private static ItemInstance CreateItem(
            string id,
            float weight)
        {
            var definition = CreateItemDefinition(id);
            SetAutoProperty(definition, "Weight", weight);
            return new ItemInstance(definition, $"instance.{id}");
        }

        private static EquipmentPartDefinition CreatePart(
            string id,
            CharacterClassData characterClass,
            EquipmentPartKind kind)
        {
            var part = ScriptableObject.CreateInstance<EquipmentPartDefinition>();
            var stats = new EquipmentPartStats();
            switch (kind)
            {
                case EquipmentPartKind.WeaponHead:
                    stats.MinAttackBase = 10f;
                    stats.MaxAttackBase = 20f;
                    break;
                case EquipmentPartKind.WeaponBinder:
                    stats.CritChanceBase = .1f;
                    break;
                case EquipmentPartKind.WeaponHandle:
                    stats.WeaponAccuracyBase = 5f;
                    break;
            }

            SetAutoProperty(part, "Id", id);
            SetAutoProperty(part, "RequiredClass", characterClass);
            SetAutoProperty(part, "Kind", kind);
            SetAutoProperty(part, "LevelBracket", EquipmentPartLevelBracket.Level5);
            SetAutoProperty(part, "Stats", stats);
            return part;
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
