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
            var binder = CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level15,
                stats => { stats.MinAttackPercent = .2f; stats.MaxAttackPercent = .2f; });
            var handle = CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level15,
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
        public void Generate_ScoutPoorArmor_ScalesQualityAdjustmentBeforePartPercentages()
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.armor", ItemType.Armor, scout);
            var plate = CreatePart("plate", scout, EquipmentPartKind.ArmorPlate,
                EquipmentPartLevelBracket.Level5,
                stats => stats.MagicalDefenseBase = 4f);
            var binder = CreatePart("binder", scout, EquipmentPartKind.ArmorBinder,
                EquipmentPartLevelBracket.Level5,
                stats => stats.MagicalDefensePercent = -0.2f);
            var armorBase = CreatePart("base", scout, EquipmentPartKind.ArmorBase,
                EquipmentPartLevelBracket.Level5,
                stats => stats.MagicalDefensePercent = -0.2f);

            var item = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition,
                new[] { plate, binder, armorBase },
                EquipmentQuality.Poor,
                RarityTier.Common);

            Assert.That(GetValue(item, CharacterStatType.MagicalDefense), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(item.ItemLevel, Is.EqualTo(2));
            Assert.That(item.RequiredLevel, Is.EqualTo(2));
        }

        [Test]
        public void Generate_PaladinPoorWeapon_DoesNotScaleFlatQualityAdjustmentsWithLevel()
        {
            var paladin = CreateClass("class.paladin");
            var definition = CreateDefinition("equipment.paladin.weapon", ItemType.Weapon, paladin);
            var parts = CreateQualityMatrixParts(paladin, ItemType.Weapon);
            foreach (var part in parts)
                SetAutoProperty(part, "LevelBracket", EquipmentPartLevelBracket.Level45);

            var normal = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Normal, RarityTier.Common);
            var poor = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Poor, RarityTier.Common);

            Assert.That(GetValue(poor, CharacterStatType.MinAttack) - GetValue(normal, CharacterStatType.MinAttack),
                Is.EqualTo(-18f).Within(0.0001f));
            Assert.That(GetValue(poor, CharacterStatType.MaxAttack) - GetValue(normal, CharacterStatType.MaxAttack),
                Is.EqualTo(-54f).Within(0.0001f));
            Assert.That(GetValue(poor, CharacterStatType.CritChance) - GetValue(normal, CharacterStatType.CritChance),
                Is.EqualTo(-0.01f).Within(0.0001f));
            Assert.That(GetValue(poor, CharacterStatType.WeaponAccuracy) - GetValue(normal, CharacterStatType.WeaponAccuracy),
                Is.EqualTo(-2f).Within(0.0001f));
        }

        [TestCase("class.warrior", ItemType.Weapon, 5f, 10f, 0.01f, 0.02f, 1f, 0f, 0f, 0f, 0f, 0f)]
        [TestCase("class.scout", ItemType.Weapon, 2f, 10f, 0.01f, 0.03f, 1f, 0f, 0f, 0f, 0f, 0f)]
        [TestCase("class.mage", ItemType.Weapon, 4f, 12f, 0f, 0.02f, 0f, 0.25f, 1.5f, 0f, 0f, 0f)]
        [TestCase("class.paladin", ItemType.Weapon, 2f, 6f, 0.01f, 0.02f, 2f, 0f, 0f, 0f, 0f, 0f)]
        [TestCase("class.warrior", ItemType.Armor, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 50f, 6f, 4f)]
        [TestCase("class.scout", ItemType.Armor, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 30f, 4f, 2f)]
        [TestCase("class.mage", ItemType.Armor, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 20f, 2f, 6f)]
        [TestCase("class.paladin", ItemType.Armor, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 70f, 8f, 8f)]
        public void Generate_QualityAdjustments_MatchClassAndEquipmentMatrix(
            string classId,
            ItemType itemType,
            float expectedPoorMinOrHealth,
            float expectedPoorMax,
            float expectedPoorCritChance,
            float expectedPoorCritDamage,
            float expectedPoorAccuracy,
            float expectedPoorResourceRegeneration,
            float expectedPoorMaxResource,
            float expectedPoorMaxHealth,
            float expectedPoorPhysicalDefense,
            float expectedPoorMagicalDefense)
        {
            var characterClass = CreateClass(classId);
            var definition = CreateDefinition("equipment.quality.matrix", itemType, characterClass);
            var parts = CreateQualityMatrixParts(characterClass, itemType);

            var normal = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Normal, RarityTier.Common);
            var poor = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Poor, RarityTier.Common);
            var excellent = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Excellent, RarityTier.Common);
            var masterpiece = new EquipmentGenerationService().GenerateFromSelectedParts(
                definition, parts, EquipmentQuality.Masterpiece, RarityTier.Common);

            if (itemType == ItemType.Weapon)
            {
                AssertQualityDelta(normal, poor, CharacterStatType.MinAttack, -expectedPoorMinOrHealth);
                AssertQualityDelta(normal, poor, CharacterStatType.MaxAttack, -expectedPoorMax);
                AssertQualityDelta(normal, poor, CharacterStatType.CritDamage, -expectedPoorCritDamage);

                if (classId == "class.mage")
                {
                    AssertQualityDelta(normal, poor, CharacterStatType.ResourceRegeneration, -expectedPoorResourceRegeneration);
                    AssertQualityDelta(normal, poor, CharacterStatType.MaxResource, -expectedPoorMaxResource);
                }
                else
                {
                    AssertQualityDelta(normal, poor, CharacterStatType.CritChance, -expectedPoorCritChance);
                    AssertQualityDelta(normal, poor, CharacterStatType.WeaponAccuracy, -expectedPoorAccuracy);
                }

                AssertQualityDelta(normal, excellent, CharacterStatType.MinAttack, expectedPoorMinOrHealth);
                AssertQualityDelta(normal, excellent, CharacterStatType.MaxAttack, expectedPoorMax);
                AssertQualityDelta(normal, excellent, CharacterStatType.CritDamage, expectedPoorCritDamage);

                if (classId == "class.mage")
                {
                    AssertQualityDelta(normal, excellent, CharacterStatType.ResourceRegeneration, expectedPoorResourceRegeneration);
                    AssertQualityDelta(normal, excellent, CharacterStatType.MaxResource, expectedPoorMaxResource);
                }
                else
                {
                    AssertQualityDelta(normal, excellent, CharacterStatType.CritChance, expectedPoorCritChance);
                    AssertQualityDelta(normal, excellent, CharacterStatType.WeaponAccuracy, expectedPoorAccuracy);
                }

                AssertQualityDelta(normal, masterpiece, CharacterStatType.MinAttack, expectedPoorMinOrHealth * 4f);
                AssertQualityDelta(normal, masterpiece, CharacterStatType.MaxAttack, expectedPoorMax * 4f);
                AssertQualityDelta(normal, masterpiece, CharacterStatType.CritDamage, expectedPoorCritDamage * 4f);

                if (classId == "class.mage")
                {
                    AssertQualityDelta(normal, masterpiece, CharacterStatType.ResourceRegeneration, expectedPoorResourceRegeneration * 4f);
                    AssertQualityDelta(normal, masterpiece, CharacterStatType.MaxResource, expectedPoorMaxResource * 4f);
                }
                else
                {
                    AssertQualityDelta(normal, masterpiece, CharacterStatType.CritChance, expectedPoorCritChance * 4f);
                    AssertQualityDelta(normal, masterpiece, CharacterStatType.WeaponAccuracy, expectedPoorAccuracy * 4f);
                }
            }
            else
            {
                AssertQualityDelta(normal, poor, CharacterStatType.MaxHealth, -expectedPoorMaxHealth);
                AssertQualityDelta(normal, poor, CharacterStatType.Dodge, -2f);
                AssertQualityDelta(normal, poor, CharacterStatType.PhysicalDefense, -expectedPoorPhysicalDefense);
                AssertQualityDelta(normal, poor, CharacterStatType.MagicalDefense, -expectedPoorMagicalDefense);

                AssertQualityDelta(normal, excellent, CharacterStatType.MaxHealth, expectedPoorMaxHealth);
                AssertQualityDelta(normal, excellent, CharacterStatType.Dodge, 2f);
                AssertQualityDelta(normal, excellent, CharacterStatType.PhysicalDefense, expectedPoorPhysicalDefense);
                AssertQualityDelta(normal, excellent, CharacterStatType.MagicalDefense, expectedPoorMagicalDefense);

                AssertQualityDelta(normal, masterpiece, CharacterStatType.MaxHealth, expectedPoorMaxHealth * 4f);
                AssertQualityDelta(normal, masterpiece, CharacterStatType.Dodge, 8f);
                AssertQualityDelta(normal, masterpiece, CharacterStatType.PhysicalDefense, expectedPoorPhysicalDefense * 4f);
                AssertQualityDelta(normal, masterpiece, CharacterStatType.MagicalDefense, expectedPoorMagicalDefense * 4f);
            }
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
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level15, _ => { }),
                CreatePart("handle", warrior, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level5, _ => { })
            };

            Assert.That(new EquipmentGenerationService().Generate(
                definition, parts, EquipmentQuality.Normal, RarityTier.Common), Is.Null);
        }

        [Test]
        public void MixedTiersAreRejectedAndRandomGenerationUsesOnlyCompleteTiers()
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var head = CreatePart("head.t1", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level5, _ => { });
            var binder = CreatePart("binder.t2", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level15, _ => { });
            var handle = CreatePart("handle.t2", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level15, _ => { });
            var generator = new EquipmentGenerationService(new System.Random(17));
            Assert.That(generator.GenerateFromSelectedParts(definition, new[] { head, binder, handle },
                EquipmentQuality.Normal, RarityTier.Common), Is.Null);
            Assert.That(generator.Generate(definition, new[] { head, binder, handle },
                EquipmentQuality.Normal, RarityTier.Common), Is.Null);
            var matchingHead = CreatePart("head.t2", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level15, _ => { });
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var item = generator.Generate(definition, new[] { head, binder, handle, matchingHead },
                    EquipmentQuality.Normal, RarityTier.Common);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.PartIds, Does.Contain("head.t2"));
                Assert.That(item.PartIds, Does.Not.Contain("head.t1"));
                Assert.That(item.ItemLevel, Is.EqualTo(15));
            }
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
        public void Generate_DerivesItemLevelFromSharedPartBracket(EquipmentQuality quality, int expectedLevel)
        {
            var scout = CreateClass("class.scout");
            var definition = CreateDefinition("equipment.scout.weapon", ItemType.Weapon, scout);
            var parts = new[]
            {
                CreatePart("head", scout, EquipmentPartKind.WeaponHead, EquipmentPartLevelBracket.Level15, _ => { }),
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level15, _ => { }),
                CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level15, _ => { })
            };

            var item = new EquipmentGenerationService().Generate(definition, parts, quality, RarityTier.Common);
            Assert.That(item.ItemLevel, Is.EqualTo(expectedLevel));
            Assert.That(item.RequiredLevel, Is.EqualTo(expectedLevel));
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
                CreatePart("binder", scout, EquipmentPartKind.WeaponBinder, EquipmentPartLevelBracket.Level15, s => s.MaxAttackPercent = .2f),
                CreatePart("handle", scout, EquipmentPartKind.WeaponHandle, EquipmentPartLevelBracket.Level15, s => s.MaxAttackPercent = .2f)
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
            Assert.That(restoredItem.StatModifiers.Single().Value, Is.EqualTo(322f));
        }

        private static float GetValue(Godmist.Runtime.Items.EquipmentItemInstance item, CharacterStatType stat)
        {
            return item.StatModifiers.Single(modifier => modifier.StatType == stat).Value;
        }

        private static void AssertQualityDelta(
            Godmist.Runtime.Items.EquipmentItemInstance baseline,
            Godmist.Runtime.Items.EquipmentItemInstance item,
            CharacterStatType stat,
            float expectedDelta)
        {
            Assert.That(
                GetValue(item, stat) - GetValue(baseline, stat),
                Is.EqualTo(expectedDelta).Within(0.0001f),
                $"Unexpected {stat} quality delta.");
        }

        private static EquipmentPartDefinition[] CreateQualityMatrixParts(
            CharacterClassData characterClass,
            ItemType itemType)
        {
            if (itemType == ItemType.Weapon)
            {
                var head = CreatePart("matrix.head", characterClass, EquipmentPartKind.WeaponHead,
                    EquipmentPartLevelBracket.Level5,
                    stats =>
                    {
                        stats.MinAttackBase = 100f;
                        stats.MaxAttackBase = 100f;
                        stats.CritDamageBase = 1f;
                    });
                var binder = CreatePart("matrix.binder", characterClass, EquipmentPartKind.WeaponBinder,
                    EquipmentPartLevelBracket.Level5,
                    stats =>
                    {
                        stats.CritChanceBase = 0.1f;
                        stats.ResourceRegenerationBase = 10f;
                    });
                var handle = CreatePart("matrix.handle", characterClass, EquipmentPartKind.WeaponHandle,
                    EquipmentPartLevelBracket.Level5,
                    stats =>
                    {
                        stats.WeaponAccuracyBase = 100f;
                        stats.MaxResourceBase = 100f;
                    });
                return new[] { head, binder, handle };
            }

            var plate = CreatePart("matrix.plate", characterClass, EquipmentPartKind.ArmorPlate,
                EquipmentPartLevelBracket.Level5,
                stats =>
                {
                    stats.PhysicalDefenseBase = 100f;
                    stats.MagicalDefenseBase = 100f;
                });
            var binderPart = CreatePart("matrix.binder", characterClass, EquipmentPartKind.ArmorBinder,
                EquipmentPartLevelBracket.Level5,
                stats => stats.MaxHealthBase = 100f);
            var armorBase = CreatePart("matrix.base", characterClass, EquipmentPartKind.ArmorBase,
                EquipmentPartLevelBracket.Level5,
                stats => stats.DodgeBase = 100f);
            return new[] { plate, binderPart, armorBase };
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
