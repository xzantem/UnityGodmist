using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Combat;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DevConsoleSuggestionServiceTests
    {
        [Test]
        public void GetSuggestions_CommandPrefix_ReturnsMatchingCommands()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "go",
                null,
                null,
                null);

            Assert.That(suggestions, Does.Contain("gold"));
            Assert.That(suggestions, Does.Contain("godmode"));
            Assert.That(suggestions, Does.Not.Contain("heal"));
        }

        [Test]
        public void GetSuggestions_EmptyInput_ReturnsNoSuggestions()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                string.Empty,
                null,
                null,
                null);

            Assert.That(suggestions, Is.Empty);
        }

        [Test]
        public void GetSuggestions_CharacterArgument_ReturnsKnownCharacterId()
        {
            var gameSession = new GameSession();

            gameSession.TryRegisterCharacter(
                new CharacterInstance { CharacterId = "jerzy" });

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "heal je",
                gameSession,
                null,
                null);

            Assert.That(suggestions, Is.EqualTo(new[] { "jerzy" }));
        }

        [Test]
        public void GetSuggestions_CombatArgument_ReturnsEncounterId()
        {
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            SetAutoProperty(
                encounter,
                "Id",
                "combat.encounter.debug");

            SetAutoProperty(
                catalog,
                "EncounterDefinitions",
                new List<EncounterDefinition> { encounter });

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "combat combat.",
                null,
                null,
                catalog);

            Assert.That(
                suggestions,
                Is.EqualTo(new[] { "combat.encounter.debug" }));

            Object.DestroyImmediate(encounter);
            Object.DestroyImmediate(catalog);
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}