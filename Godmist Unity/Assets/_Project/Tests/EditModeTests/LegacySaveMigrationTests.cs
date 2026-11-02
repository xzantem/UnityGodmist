using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Serialization;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class LegacySaveMigrationTests
    {
        [Test]
        public void LegacyEquipmentAndSkillPointsSurviveTwoCompleteSaveLoadCycles()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
            var character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 50);
            character.SkillTreeProgress.Clear(); character.AvailableSkillPoints = 49;
            var equipmentDefinition = catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().First(item => item.ItemType == ItemType.Weapon
                && item.RequiredClass?.Id == character.ClassData.Id && item is not UniqueEquipmentDefinition);
            var weapon = new EquipmentItemInstance(equipmentDefinition) { CurrentRarity = RarityTier.Common, UpgradeProgress = .5f,
                BaseStatModifiers = new() { new() { StatType = CharacterStatType.MinAttack, Value = 100 }, new() { StatType = CharacterStatType.MaxAttack, Value = 100 } } };
            weapon.RecalculateStatModifiers(); character.Equipment.Weapon = weapon;
            var game = new GameSession(GameModeState.Exploration);
            Assert.That(game.TryRegisterCharacter(character), Is.True); Assert.That(game.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "v0.1.0-compatible legacy fixture");
            Assert.That(snapshot.SaveVersion, Is.EqualTo(1));
            var savedWeapon = snapshot.ItemInstances.Single(item => item.InstanceId == weapon.InstanceId);
            savedWeapon.BaseStatModifiers.Clear(); foreach (var stat in savedWeapon.StatModifiers) stat.Value = 150;
            for (var cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, catalog, out var restored, out var diagnostic), Is.True,
                    diagnostic?.ToString());
                var restoredCharacter = restored.CharactersById[character.CharacterId];
                Assert.That(restoredCharacter.AvailableSkillPoints, Is.EqualTo(50));
                Assert.That(restoredCharacter.Equipment.Weapon.BaseStatModifiers.Single(stat => stat.StatType == CharacterStatType.MinAttack).Value, Is.EqualTo(100).Within(.001));
                Assert.That(restoredCharacter.Equipment.Weapon.StatModifiers.Single(stat => stat.StatType == CharacterStatType.MinAttack).Value, Is.EqualTo(125).Within(.001));
                Assert.That(LegacySaveMigrations.RecoverSkillPointTotal(restoredCharacter), Is.False);
                Assert.That(LegacySaveMigrations.RecoverEquipmentBaseline(restoredCharacter.Equipment.Weapon), Is.False);
                snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(restored, "migration cycle " + cycle);
            }
        }

        [Test]
        public void RecalculationCannotSilentlyRunAnOldUpgradeMigration()
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            try
            {
                typeof(ItemDefinition).GetProperty(nameof(definition.Id)).SetValue(definition, "test.legacy-migration.weapon");
                typeof(ItemDefinition).GetProperty(nameof(definition.ItemType)).SetValue(definition, ItemType.Weapon);
                var item = new EquipmentItemInstance(definition) { UpgradeProgress = .5f,
                    StatModifiers = new() { new() { StatType = CharacterStatType.MinAttack, Value = 150 } } };
                Assert.Throws<InvalidOperationException>(() => item.RecalculateStatModifiers());
                Assert.That(LegacySaveMigrations.RecoverEquipmentBaseline(item), Is.True); item.RecalculateStatModifiers();
                Assert.That(item.StatModifiers[0].Value, Is.EqualTo(125).Within(.001));
                Assert.That(LegacySaveMigrations.RecoverEquipmentBaseline(item), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void WarriorAliasMappingIsBoundToTheOriginalLegacyIdentifiers()
        {
            var original = new[] { "skill.warrior.bandage", "skill.warrior.bleeding_destruction" };
            var migrated = original.Select(id => LegacySaveMigrations.LoadoutSkillId("class.warrior", original, id)).ToArray();
            Assert.That(migrated, Is.EqualTo(new[] { "skill.warrior.bleeding_destruction", "skill.warrior.discharge" }));
            Assert.That(migrated.Select(id => LegacySaveMigrations.LoadoutSkillId("class.warrior", migrated, id)), Is.EqualTo(migrated));
        }
    }
}
