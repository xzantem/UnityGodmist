using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
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
        public void GetSuggestions_HelpArgument_ReturnsOnlyLegalCommandNames()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "help cre",
                null,
                null,
                null);

            Assert.That(suggestions, Is.EqualTo(new[]
            {
                "createconsumable",
                "createequipment",
                "createitem"
            }));
        }

        [Test]
        public void GetSuggestions_CreateEquipmentQuality_IncludesMasterpiece()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createequipment equipment.test M",
                null,
                null,
                null);

            Assert.That(suggestions, Does.Contain("Masterpiece"));
        }

        [Test]
        public void GetSuggestions_NoclipArgument_ReturnsOnlySupportedModes()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "noclip o",
                null,
                null,
                null);

            Assert.That(suggestions, Is.EqualTo(new[] { "off", "on" }));
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

        [Test]
        public void GetSuggestions_PotionEffectPrefix_ReturnsAllMatchingLegalEffects()
        {
            var potion = CreateConsumableDefinition(
                "item.consumable.potion",
                ConsumableUseType.Potion);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.Add(potion);

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createconsumable item.consumable.potion R",
                null,
                null,
                catalog);

            Assert.That(
                suggestions,
                Is.EqualTo(new[]
                {
                    "RegenHealth",
                    "RegenResource",
                    "RestoreHealth",
                    "RestoreResource"
                }));

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetSuggestions_CreateConsumableItemPrefix_ExcludesUtilityDefinitions()
        {
            var potion = CreateConsumableDefinition(
                "item.consumable.potion",
                ConsumableUseType.Potion);
            var returnScroll = CreateConsumableDefinition(
                "item.consumable.return_scroll",
                ConsumableUseType.Utility);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.Add(potion);
            catalog.ItemDefinitions.Add(returnScroll);

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createconsumable item.consumable.",
                null,
                null,
                catalog);

            Assert.That(suggestions, Does.Contain("item.consumable.potion"));
            Assert.That(
                suggestions,
                Does.Not.Contain("item.consumable.return_scroll"));

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(returnScroll);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetSuggestions_CreateItem_OffersCharacterForOptionalArgument()
        {
            var session = new GameSession();
            session.TryRegisterCharacter(
                new CharacterInstance { CharacterId = "jerzy" });

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createitem item.test characterId=je",
                session,
                null,
                null);

            Assert.That(suggestions, Is.EqualTo(new[] { "characterId=jerzy" }));
        }

        [Test]
        public void GetSuggestions_CreateItem_OffersCharacterAfterOptionalAmount()
        {
            var session = new GameSession();
            session.TryRegisterCharacter(
                new CharacterInstance { CharacterId = "jerzy" });

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createitem item.test 2 je",
                session,
                null,
                null);

            Assert.That(suggestions, Is.EqualTo(new[] { "jerzy" }));
        }

        [Test]
        public void GetSuggestions_CreateItem_DoesNotOfferNamedArgumentLabels()
        {
            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createitem item.test a",
                null,
                null,
                null);

            Assert.That(suggestions, Does.Not.Contain("amount="));
            Assert.That(suggestions, Does.Not.Contain("characterId="));
        }

        [Test]
        public void GetSuggestions_UtilityConsumable_ReturnsNoFurtherSuggestions()
        {
            var returnScroll = CreateConsumableDefinition(
                "item.consumable.return_scroll",
                ConsumableUseType.Utility);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.Add(returnScroll);

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createconsumable item.consumable.return_scroll ",
                null,
                null,
                catalog);

            Assert.That(suggestions, Is.Empty);

            Object.DestroyImmediate(returnScroll);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetSuggestions_AfterThreePotionEffects_ReturnsNoFurtherEffects()
        {
            var potion = CreateConsumableDefinition(
                "item.consumable.potion",
                ConsumableUseType.Potion);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.Add(potion);

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createconsumable item.consumable.potion RestoreHealth 0 RestoreResource 0 ModifyDamage 0 ",
                null,
                null,
                catalog);

            Assert.That(suggestions, Is.Empty);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetSuggestions_IllegalEffect_DoesNotSuggestItsArguments()
        {
            var potion = CreateConsumableDefinition(
                "item.consumable.potion",
                ConsumableUseType.Potion);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.Add(potion);

            var suggestions = DevConsoleSuggestionService.GetSuggestions(
                "createconsumable item.consumable.potion ApplyStatus 1 ",
                null,
                null,
                catalog);

            Assert.That(suggestions, Is.Empty);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetSuggestions_ContentDebugArguments_UsesRuntimeAndCatalogIds()
        {
            var session = new GameSession();
            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            var craftingRole = ScriptableObject.CreateInstance<BlacksmithRoleData>();
            var questDefinition = ScriptableObject.CreateInstance<QuestDefinition>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            SetAutoProperty(npcDefinition, "Id", "npc.blacksmith");
            SetAutoProperty(npcDefinition, "Roles", new List<NpcRoleData> { craftingRole });
            SetAutoProperty(questDefinition, "Id", "quest.debug");
            SetAutoProperty(catalog, "QuestDefinitions", new List<QuestDefinition> { questDefinition });
            session.TryRegisterNpc(new NpcInstance
            {
                NpcId = "npc.blacksmith",
                Definition = npcDefinition
            });
            session.WorldState.TrySetFlag("tutorial.completed", "true");

            Assert.That(
                DevConsoleSuggestionService.GetSuggestions(
                    "queststate quest.", session, null, catalog),
                Is.EqualTo(new[] { "quest.debug" }));
            Assert.That(
                DevConsoleSuggestionService.GetSuggestions(
                    "setworldflag tutorial.", session, null, catalog),
                Is.EqualTo(new[] { "tutorial.completed" }));

            Object.DestroyImmediate(npcDefinition);
            Object.DestroyImmediate(craftingRole);
            Object.DestroyImmediate(questDefinition);
            Object.DestroyImmediate(catalog);
        }

        private static ConsumableItemDefinition CreateConsumableDefinition(
            string id,
            ConsumableUseType useType)
        {
            var definition =
                ScriptableObject.CreateInstance<ConsumableItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "UseType", useType);
            SetAutoProperty(
                definition,
                "ItemType",
                useType == ConsumableUseType.Potion
                    ? ItemType.Potion
                    : ItemType.Tool);

            return definition;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var fieldName = $"<{propertyName}>k__BackingField";
            var targetType = target.GetType();
            FieldInfo field = null;

            while (targetType != null && field == null)
            {
                field = targetType.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                targetType = targetType.BaseType;
            }

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}
