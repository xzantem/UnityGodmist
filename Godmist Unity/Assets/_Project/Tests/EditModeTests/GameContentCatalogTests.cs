using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class GameContentCatalogTests
    {
        [Test]
        public void TryGetCharacter_WhenIdExists_ReturnsDefinition()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetAutoProperty(character, "Id", "character.jerzy");

            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            SetAutoProperty(
                catalog,
                "CharacterDefinitions",
                new System.Collections.Generic.List<CharacterDefinition>
                {
                    character
                });

            var result = catalog.TryGetCharacter(
                "character.jerzy",
                out var foundCharacter);

            Assert.That(result, Is.True);
            Assert.That(foundCharacter, Is.SameAs(character));

            Object.DestroyImmediate(character);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void TryGetItem_WhenIdIsMissing_ReturnsFalse()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            var result = catalog.TryGetItem(
                "item.missing",
                out ItemDefinition foundItem);

            Assert.That(result, Is.False);
            Assert.That(foundItem, Is.Null);

            Object.DestroyImmediate(catalog);
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            FieldInfo field = null;
            var type = target.GetType();

            while (type != null && field == null)
            {
                field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}