using System.Reflection;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ContentAuthoringValidationTests
    {
        [Test]
        public void Validate_ItemDefinition_ReportsMissingIdentity()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();

            var report = ContentAuthoringValidator.Validate(item);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.EqualTo("Id nie może być puste."));
            Assert.That(report.Errors, Has.Some.EqualTo("DisplayName nie może być puste."));

            Object.DestroyImmediate(item);
        }

        [Test]
        public void Validate_CraftableItem_ReportsMissingRecipeData()
        {
            var recipe = ScriptableObject.CreateInstance<CraftableItemDefinition>();
            SetAutoProperty(recipe, "Id", "item.recipe.test");
            SetAutoProperty(recipe, "DisplayName", "Test recipe");

            var report = ContentAuthoringValidator.Validate(recipe);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("CraftingDiscipline"));
            Assert.That(report.Errors, Has.Some.Contains("co najmniej jeden składnik"));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Validate_RejectsDuplicateIdInActiveCatalog()
        {
            var existing = ScriptableObject.CreateInstance<ItemDefinition>();
            var duplicate = ScriptableObject.CreateInstance<ItemDefinition>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            SetAutoProperty(existing, "Id", "item.duplicate");
            SetAutoProperty(existing, "DisplayName", "Existing");
            SetAutoProperty(duplicate, "Id", "item.duplicate");
            SetAutoProperty(duplicate, "DisplayName", "Duplicate");
            catalog.ItemDefinitions.Add(existing);

            var report = ContentAuthoringValidator.Validate(duplicate, catalog);

            Assert.That(report.Errors, Has.Some.Contains("jest już używane"));

            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(duplicate);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_JewelryWithoutRequiredClass_DoesNotReportClassWarning()
        {
            var jewelry = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            SetAutoProperty(jewelry, "Id", "item.jewelry.bracelet.test");
            SetAutoProperty(jewelry, "DisplayName", "Test bracelet");
            SetAutoProperty(jewelry, "ItemType", Core.Enums.ItemType.Jewelry);
            SetAutoProperty(jewelry, "JewelrySlot", Core.Enums.EquipmentSlot.Bracelet);

            var report = ContentAuthoringValidator.Validate(jewelry);

            Assert.That(report.Warnings, Has.None.Contains("klasy postaci"));

            Object.DestroyImmediate(jewelry);
        }

        [Test]
        public void Validate_CharacterWithoutClass_DoesNotReportClassWarning()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetAutoProperty(character, "Id", "enemy.test");
            SetAutoProperty(character, "DisplayName", "Test enemy");

            var report = ContentAuthoringValidator.Validate(character);

            Assert.That(report.Warnings, Has.None.Contains("przypisanej klasy"));

            Object.DestroyImmediate(character);
        }

        [Test]
        public void Validate_Skill_UsesSkillIdAndReportsInvalidCosts()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            SetAutoProperty(skill, "SkillId", "skill.test");
            SetAutoProperty(skill, "DisplayName", "Test skill");
            SetAutoProperty(skill, "ActionCost", -1);

            var report = ContentAuthoringValidator.Validate(skill);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.EqualTo("ActionCost nie może być ujemne."));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void Validate_RejectsDuplicateCharacterIdInActiveCatalog()
        {
            var existing = ScriptableObject.CreateInstance<CharacterDefinition>();
            var duplicate = ScriptableObject.CreateInstance<CharacterDefinition>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            SetAutoProperty(existing, "Id", "character.duplicate");
            SetAutoProperty(existing, "DisplayName", "Existing");
            SetAutoProperty(duplicate, "Id", "character.duplicate");
            SetAutoProperty(duplicate, "DisplayName", "Duplicate");
            catalog.CharacterDefinitions.Add(existing);

            var report = ContentAuthoringValidator.Validate(duplicate, catalog);

            Assert.That(report.Errors, Has.Some.Contains("jest już używane"));

            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(duplicate);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_Encounter_ReportsBossIndexOutsideEnemyList()
        {
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            SetAutoProperty(encounter, "Id", "encounter.test");
            SetAutoProperty(encounter, "DisplayName", "Test encounter");
            SetAutoProperty(encounter, "BossEnemyIndex", 1);

            var report = ContentAuthoringValidator.Validate(encounter);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("BossEnemyIndex"));

            Object.DestroyImmediate(encounter);
        }

        [Test]
        public void Validate_Dungeon_ReportsInvertedRanges()
        {
            var dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
            SetAutoProperty(dungeon, "Id", "dungeon.test");
            SetAutoProperty(dungeon, "DisplayName", "Test dungeon");
            SetAutoProperty(dungeon, "MinFloorCount", 3);
            SetAutoProperty(dungeon, "MaxFloorCount", 2);

            var report = ContentAuthoringValidator.Validate(dungeon);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("FloorCount maximum"));

            Object.DestroyImmediate(dungeon);
        }

        private static void SetAutoProperty<T>(T target, string propertyName, object value)
        {
            var type = target.GetType();
            var backingFieldName = $"<{propertyName}>k__BackingField";
            while (type != null)
            {
                var field = type.GetField(
                    backingFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Nie znaleziono backing field dla właściwości '{propertyName}'.");
        }
    }
}
