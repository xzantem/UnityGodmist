using System.Reflection;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ContentValidatorTests
    {
        [Test]
        public void ValidateProject_ReportsDuplicateIdWithAssetAndField()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var first = CreateItem("item.duplicate", "First");
            var second = CreateItem("item.duplicate", "Second");
            catalog.ItemDefinitions.Add(first);
            catalog.ItemDefinitions.Add(second);

            var report = ContentAuthoringValidator.ValidateProject(catalog, new Object[] { first, second });

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == first && issue.FieldPath == "Id" && issue.Message.Contains("zduplikowane")));
            Assert.That(report.Errors, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == second && issue.FieldPath == "Id" && issue.Message.Contains("zduplikowane")));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ValidateProject_ReportsUnassignedAndDeadAssetAsWarning()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var item = CreateItem("item.orphan", "Orphan");

            var report = ContentAuthoringValidator.ValidateProject(catalog, new Object[] { item });

            Assert.That(report.Warnings, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == item && issue.FieldPath == "GameContentCatalog"));
            Assert.That(report.Warnings, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == item && issue.FieldPath == "References" && issue.Message.Contains("martwy")));
            Assert.That(report.Infos, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == null && issue.Message.Contains("Przeskanowano 1")));

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ValidateProject_ReportsMissingLootItemReference()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var lootTable = ScriptableObject.CreateInstance<LootTableData>();
            lootTable.Entries.Add(new LootEntryData());

            var report = ContentAuthoringValidator.ValidateProject(
                catalog, new Object[] { lootTable });

            Assert.That(report.Errors, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == lootTable
                && issue.FieldPath == "Entries[0].ItemDefinition"
                && issue.Message.Contains("Brak itemu")));

            Object.DestroyImmediate(lootTable);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ValidateProject_ReportsEquipmentWithoutCompatiblePart()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var equipment = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            var characterClass = ScriptableObject.CreateInstance<Godmist.Data.Characters.CharacterClassData>();
            SetAutoProperty(equipment, "Id", "item.weapon.test");
            SetAutoProperty(equipment, "DisplayName", "Test weapon");
            SetAutoProperty(equipment, "RequiredClass", characterClass);
            SetAutoProperty(characterClass, "Id", "class.test");
            SetAutoProperty(characterClass, "ClassName", "Test class");
            catalog.ItemDefinitions.Add(equipment);
            catalog.CharacterClasses.Add(characterClass);

            var report = ContentAuthoringValidator.ValidateProject(
                catalog, new Object[] { equipment, characterClass });

            Assert.That(report.Errors, Has.Some.Matches<ContentValidationIssue>(issue =>
                issue.Asset == equipment
                && issue.FieldPath == "RequiredClass / ItemType"
                && issue.Message.Contains("WeaponHead")));

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(characterClass);
            Object.DestroyImmediate(catalog);
        }

        private static ItemDefinition CreateItem(string id, string displayName)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetAutoProperty(item, "Id", id);
            SetAutoProperty(item, "DisplayName", displayName);
            return item;
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
