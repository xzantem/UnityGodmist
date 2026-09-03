using System.Collections.Generic;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ContentImportTests
    {
        [Test]
        public void ParseDelimited_SupportsQuotedSeparatorsAndNewLines()
        {
            var rows = ContentImportSourceReader.ParseDelimited(
                "name;description\n\"Potion; Large\";\"Line one\nLine two\"\n",
                ';');

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[1][0], Is.EqualTo("Potion; Large"));
            Assert.That(rows[1][1], Is.EqualTo("Line one\nLine two"));
        }

        [Test]
        public void BuildPreview_GeneratesIdFromNameAndIgnoresNumericSourceId()
        {
            var sheet = new ContentImportSheet(
                "Items",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "ID", "item_type", "price" },
                    new[] { "Żelazna Ruda", "123", "Material", "7" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "items.csv", sheet, ContentImportTargetType.ItemDefinition, null);

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Id, Is.EqualTo("item.zelazna_ruda"));
            Assert.That(rows[0].Values["BaseValue"], Is.EqualTo("7"));
            Assert.That(rows[0].ChangeKind, Is.EqualTo(ContentImportChangeKind.Create));
        }

        [Test]
        public void BuildPreview_UsesExplicitStableIdAndIgnoresNumericLegacyId()
        {
            var sheet = new ContentImportSheet(
                "Items",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "ID", "id", "item_type" },
                    new[] { "Ruda żelaza", "123", "item.material.smithing.iron_ore", "Material" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "items.csv", sheet, ContentImportTargetType.ItemDefinition, null);

            Assert.That(rows[0].Id, Is.EqualTo("item.material.smithing.iron_ore"));
        }

        [Test]
        public void BuildAssetFileName_UsesConventionPrefixAndIdSegment()
        {
            Assert.That(
                ContentImportApplier.BuildAssetFileName(
                    ContentImportTargetType.ItemDefinition,
                    "item.material.smithing.iron_ore"),
                Is.EqualTo("ID_IronOre"));
            Assert.That(
                ContentImportApplier.BuildAssetFileName(
                    ContentImportTargetType.CraftableItemDefinition,
                    "item.material.smithing.iron_ingot"),
                Is.EqualTo("CID_IronIngot"));
        }

        [Test]
        public void BuildPreview_UsesOnlyActiveCatalogForUpdate()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var existing = ScriptableObject.CreateInstance<ItemDefinition>();
            SetBackingField(existing, "Id", "item.iron_ore");
            SetBackingField(existing, "DisplayName", "Iron Ore");
            catalog.ItemDefinitions.Add(existing);

            var sheet = new ContentImportSheet(
                "Items",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "ID" },
                    new[] { "Iron Ore", "999" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "items.csv", sheet, ContentImportTargetType.ItemDefinition, catalog);

            Assert.That(rows[0].ChangeKind, Is.EqualTo(ContentImportChangeKind.Update));
            Assert.That(rows[0].ExistingAsset, Is.SameAs(existing));

            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void BuildPreview_CraftableReportsMissingIngredientReference()
        {
            var sheet = new ContentImportSheet(
                "Recipes",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "crafting_discipline", "ingredient" },
                    new[] { "Iron Chain", "Smithing", "item.missing" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "recipes.csv", sheet, ContentImportTargetType.CraftableItemDefinition, null);

            Assert.That(rows[0].IsValid, Is.False);
            Assert.That(rows[0].MissingReferences, Has.Some.Contains("Brak składnika"));
        }

        [Test]
        public void BuildPreview_SkillMapsAttackTypes()
        {
            var sheet = new ContentImportSheet(
                "Skills",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "attack_type", "point_blank_attack_type" },
                    new[] { "Knife Toss", "ranged", "melee" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "active_skills.csv", sheet, ContentImportTargetType.SkillData, null);

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Values["AttackType"], Is.EqualTo(nameof(Godmist.Core.Enums.AttackType.Ranged)));
            Assert.That(rows[0].Values["PointBlankAttackType"], Is.EqualTo(nameof(Godmist.Core.Enums.AttackType.Melee)));
        }

        [Test]
        public void BuildPreview_CraftableMapsMultipleIngredientsFromRecipe()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var iron = CreateItem(catalog, "item.iron", "Iron");
            var coal = CreateItem(catalog, "item.coal", "Coal");
            var sheet = new ContentImportSheet(
                "Recipes",
                new List<IReadOnlyList<string>>
                {
                    new[] { "name", "crafting_discipline", "recipe" },
                    new[] { "Steel Bar", "Smithing", "2x Iron + 1x Coal -> 1x Steel Bar" }
                },
                0);

            var rows = ContentImportMapper.BuildPreview(
                "recipes.csv", sheet, ContentImportTargetType.CraftableItemDefinition, catalog);

            Assert.That(rows[0].IsValid, Is.True);
            Assert.That(rows[0].Values["IngredientMaterial_1"], Is.EqualTo("item.iron"));
            Assert.That(rows[0].Values["IngredientAmount_1"], Is.EqualTo("2"));
            Assert.That(rows[0].Values["IngredientMaterial_2"], Is.EqualTo("item.coal"));
            Assert.That(rows[0].Values["IngredientAmount_2"], Is.EqualTo("1"));

            Object.DestroyImmediate(iron);
            Object.DestroyImmediate(coal);
            Object.DestroyImmediate(catalog);
        }

        private static ItemDefinition CreateItem(GameContentCatalog catalog, string id, string displayName)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetBackingField(item, "Id", id);
            SetBackingField(item, "DisplayName", displayName);
            catalog.ItemDefinitions.Add(item);
            return item;
        }

        private static void SetBackingField(Object target, string propertyName, object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
