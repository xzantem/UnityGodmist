using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EquipmentGenerationServiceTests
    {
        [Test]
        public void Generate_ScoutWeapon_AppliesPartPercentagesToLocalItemStats()
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var head = CreatePart("head", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level15,
                stats => { stats.MinAttackBase = 100; stats.MaxAttackBase = 200; });
            var binder = CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level5,
                stats => { stats.MinAttackPercent = .2f; stats.MaxAttackPercent = .2f; });
            var handle = CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5,
                stats => { stats.MinAttackPercent = .2f; stats.MaxAttackPercent = .2f; });

            var item = new EquipmentGenerationService(new System.Random(2)).Generate(
                definition, new[] { head, binder, handle }, EquipmentQuality.Normal, RarityTier.Common);

            Assert.That(item.RequiredLevel, Is.EqualTo(15));
            Assert.That(item.ItemLevel, Is.EqualTo(15));
            Assert.That(item.RequiredClassId, Is.EqualTo("class.scout"));
            Assert.That(GetValue(item, CharacterStatType.MinAttack), Is.EqualTo(140f));
            Assert.That(GetValue(item, CharacterStatType.MaxAttack), Is.EqualTo(280f));
        }

        [Test]
        public void Generate_RejectsPartsFromAnotherClass()
        {
            var scout = CreateClass("class.scout");
            var warrior = CreateClass("class.warrior");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var parts = new[]
            {
                CreatePart("head", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level5, _ => { }),
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level5, _ => { }),
                CreatePart("handle", warrior, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5, _ => { })
            };

            Assert.That(new EquipmentGenerationService().Generate(
                definition, parts, EquipmentQuality.Normal, RarityTier.Common), Is.Null);
        }

        [Test]
        public void GenerateFromSelectedParts_PreservesThePlayerSelectedPartPayload()
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var head = CreatePart("head.selected", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level5, _ => { });
            var binder = CreatePart("binder.selected", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level5, _ => { });
            var handle = CreatePart("handle.selected", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5, _ => { });

            var item = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition,
                new[] { head, binder, handle },
                EquipmentQuality.Excellent,
                RarityTier.Rare);

            Assert.That(item, Is.Not.Null);
            Assert.That(item.PartIds, Is.EqualTo(new[]
            {
                "head.selected", "binder.selected", "handle.selected"
            }));
            Assert.That(item.Quality, Is.EqualTo(EquipmentQuality.Excellent));
            Assert.That(item.CurrentRarity, Is.EqualTo(RarityTier.Rare));
        }

        [TestCase(EquipmentQuality.Poor, 12)]
        [TestCase(EquipmentQuality.Normal, 15)]
        [TestCase(EquipmentQuality.Excellent, 18)]
        [TestCase(EquipmentQuality.Masterpiece, 20)]
        public void Generate_DerivesItemLevelFromHighestPartBracket(EquipmentQuality quality, int expectedLevel)
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var parts = new[]
            {
                CreatePart("head", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level15, _ => { }),
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level5, _ => { }),
                CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5, _ => { })
            };

            var item = new EquipmentGenerationService().Generate(definition, parts, quality, RarityTier.Common);
            Assert.That(item.RequiredLevel, Is.EqualTo(15));
            Assert.That(item.ItemLevel, Is.EqualTo(expectedLevel));
        }

        [Test]
        public void SaveRestore_PreservesGeneratedEquipmentPayload()
        {
            var scout = CreateClass("class.scout");
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetAutoProperty(characterDefinition, "Id", "character.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var item = new EquipmentGenerationService().Generate(definition, new[]
            {
                CreatePart("head", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level15, s => s.MaxAttackBase = 200),
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level5, s => s.MaxAttackPercent = .2f),
                CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5, s => s.MaxAttackPercent = .2f)
            }, EquipmentQuality.Excellent, RarityTier.Rare);
            item.CustomName = "Mój wykuty miecz";
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.CharacterDefinitions.Add(characterDefinition);
            catalog.CharacterClasses.Add(scout);
            catalog.ItemDefinitions.Add(definition);
            var character = new CharacterInstanceFactory().Create(characterDefinition, scout);
            character.Inventory.Items.Add(item);
            var session = new GameSession(GameModeState.Exploration, "equipment.save");
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "Equipment");
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot), out var data), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(data, catalog, out var restored), Is.True);
            var restoredItem = restored.CharactersById[character.CharacterId].Inventory.Items.Single() as Godmist.Runtime.Items.EquipmentItemInstance;

            Assert.That(restoredItem.Quality, Is.EqualTo(item.Quality));
            Assert.That(restoredItem.CurrentRarity, Is.EqualTo(item.CurrentRarity));
            Assert.That(restoredItem.ItemLevel, Is.EqualTo(item.ItemLevel));
            Assert.That(restoredItem.RequiredLevel, Is.EqualTo(item.RequiredLevel));
            Assert.That(restoredItem.RequiredClassId, Is.EqualTo("class.scout"));
            Assert.That(restoredItem.CustomName, Is.EqualTo("Mój wykuty miecz"));
            Assert.That(restoredItem.PartIds, Is.EqualTo(item.PartIds));
            Assert.That(restoredItem.StatModifiers.Single().Value, Is.EqualTo(283f));
        }

        private static float GetValue(Godmist.Runtime.Items.EquipmentItemInstance item, CharacterStatType stat)
        {
            return item.StatModifiers.Single(modifier => modifier.StatType == stat).Value;
        }

        private static CharacterClassData CreateClass(string id)
        {
            var value = ScriptableObject.CreateInstance<CharacterClassData>();
            SetAutoProperty(value, "Id", id);
            return value;
        }

        private static EquipmentItemDefinition CreateDefinition(string id, ItemType itemType, CharacterClassData requiredClass)
        {
            var value = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            SetAutoProperty(value, "Id", id);
            SetAutoProperty(value, "ItemType", itemType);
            SetAutoProperty(value, "RequiredClass", requiredClass);
            return value;
        }

        private static EquipmentPartDefinition CreatePart(string id, CharacterClassData requiredClass,
            EquipmentPartKind kind, EquipmentPartLevelBracket bracket, Action<EquipmentPartStats> configure)
        {
            var value = ScriptableObject.CreateInstance<EquipmentPartDefinition>();
            var stats = new EquipmentPartStats();
            configure(stats);
            SetAutoProperty(value, "Id", id);
            SetAutoProperty(value, "RequiredClass", requiredClass);
            SetAutoProperty(value, "Kind", kind);
            SetAutoProperty(value, "LevelBracket", bracket);
            SetAutoProperty(value, "Stats", stats);
            return value;
        }

        private static void SetAutoProperty(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
