using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Debugging;
using System.Reflection;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEngine;
using NUnit.Framework;
using Godmist.Data.Combat;

namespace Godmist.Tests.EditMode
{
    public class DevConsoleCommandServiceTests
    {
        [Test]
        public void Execute_GoldCommand_AddsGoldToGameSession()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();

            var result = service.Execute(
                "gold 25",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(session.Gold, Is.EqualTo(25));
        }

        [Test]
        public void Execute_CharactersCommand_ListsRegisteredCharacter()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "characters",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("jerzy"));
        }

        [Test]
        public void Execute_XpWithCharacterId_AddsExperienceToTarget()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "xp jerzy 10",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.CurrentExperience, Is.EqualTo(10));
        }

        [Test]
        public void Execute_XpWithoutCharacterId_AddsExperienceToActiveParty()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var wladimir = CreateCharacter("wladimir");

            session.TryRegisterCharacter(jerzy);
            session.TryRegisterCharacter(wladimir);
            session.TryAddCharacterToActiveParty("jerzy");
            session.TryAddCharacterToActiveParty("wladimir");

            var result = service.Execute(
                "xp 10",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.CurrentExperience, Is.EqualTo(10));
            Assert.That(wladimir.CurrentExperience, Is.EqualTo(10));
        }

        [Test]
        public void Execute_HealWithCharacterId_HealsTargetOutsideCombat()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            jerzy.CurrentHealth = 70;

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "heal jerzy 25",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.CurrentHealth, Is.EqualTo(95));
        }

        [Test]
        public void Execute_ResourceWithCharacterId_RestoresTargetResource()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var wladimir = CreateCharacter("wladimir");
            wladimir.ResourcePool.CurrentValue = 20;

            session.TryRegisterCharacter(wladimir);

            var result = service.Execute(
                "resource wladimir 25",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(
                wladimir.ResourcePool.CurrentValue,
                Is.EqualTo(45));
        }

        [Test]
        public void Execute_HealWithoutCharacterId_UsesActiveCombatUnit()
        {
            var service = new DevConsoleCommandService();
            var player = CreateCharacter("jerzy");
            player.CurrentHealth = 90;

            var combat = new CombatSession
            {
                ActiveUnit = new BattleUnit
                {
                    Character = player,
                    IsPlayerSide = true
                }
            };

            var result = service.Execute(
                "heal 25",
                new GameSession(),
                combat);

            Assert.That(result.Success, Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void Execute_UnknownCommand_ReturnsFailure()
        {
            var service = new DevConsoleCommandService();

            var result = service.Execute(
                "unknown",
                new GameSession(),
                null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Nieznana komenda"));
        }

        [Test]
        public void Execute_GodMode_TogglesTargetCharacter()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "godmode jerzy",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.DebugIsInvulnerable, Is.True);

            result = service.Execute(
                "godmode jerzy",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.DebugIsInvulnerable, Is.False);
        }

        [Test]
        public void Execute_SetHp_ClampsValueToCharacterMaxHealth()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "sethp jerzy 250",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void Execute_SetResource_ClampsValueToResourceMaxValue()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "setresource jerzy 100",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(
                jerzy.ResourcePool.CurrentValue,
                Is.EqualTo(jerzy.ResourcePool.MaxValue));
        }

        [Test]
        public void Execute_GiveItem_AddsSeparateItemInstancesToInventory()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var material = CreateItemDefinition(
                "material.test",
                "Materiał testowy");

            session.TryRegisterCharacter(jerzy);
            catalog.ItemDefinitions.Add(material);

            var result = service.Execute(
                "giveitem jerzy material.test 2",
                session,
                null,
                catalog);

            Assert.That(result.Success, Is.True);
            Assert.That(jerzy.Inventory.Items, Has.Count.EqualTo(2));
            Assert.That(
                jerzy.Inventory.Items[0].Definition,
                Is.SameAs(material));
            Assert.That(
                jerzy.Inventory.Items[0].InstanceId,
                Is.Not.EqualTo(jerzy.Inventory.Items[1].InstanceId));

            Object.DestroyImmediate(material);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Execute_Items_ListsInventoryContents()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var material = CreateItemDefinition(
                "material.test",
                "Materiał testowy");

            session.TryRegisterCharacter(jerzy);
            catalog.ItemDefinitions.Add(material);

            service.Execute(
                "giveitem jerzy material.test 2",
                session,
                null,
                catalog);

            var result = service.Execute(
                "items jerzy",
                session,
                null,
                catalog);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("material.test"));
            Assert.That(result.Message, Does.Contain("x2"));

            Object.DestroyImmediate(material);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Execute_EncounterDefs_ListsEncounterDefinitions()
        {
            var service = new DevConsoleCommandService();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();

            SetAutoProperty(
                encounter,
                "Id",
                "combat.encounter.debug");

            SetAutoProperty(
                encounter,
                "DisplayName",
                "Debug Combat");

            SetAutoProperty(encounter, "GridWidth", 8);
            SetAutoProperty(encounter, "GridHeight", 8);
            SetAutoProperty(
                encounter,
                "Enemies",
                new List<EncounterEnemyData>());

            catalog.EncounterDefinitions.Add(encounter);

            var result = service.Execute(
                "encounterdefs",
                new GameSession(),
                null,
                catalog);

            Assert.That(result.Success, Is.True);
            Assert.That(
                result.Message,
                Does.Contain("combat.encounter.debug"));

            Assert.That(
                result.Message,
                Does.Contain("Debug Combat"));

            Object.DestroyImmediate(encounter);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Execute_CombatCommand_ForwardsEncounterIdToHandler()
        {
            var service = new DevConsoleCommandService();
            var receivedEncounterId = string.Empty;

            var result = service.Execute(
                "combat combat.encounter.debug",
                new GameSession(),
                null,
                null,
                encounterId =>
                {
                    receivedEncounterId = encounterId;

                    return new DevConsoleCommandResult(
                        true,
                        "Uruchomiono walke.");
                });

            Assert.That(result.Success, Is.True);
            Assert.That(
                receivedEncounterId,
                Is.EqualTo("combat.encounter.debug"));
        }

        [Test]
        public void Execute_ItemDefs_ListsDefinitionsFromCatalog()
        {
            var service = new DevConsoleCommandService();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var material = CreateItemDefinition(
                "material.test",
                "Materiał testowy");

            catalog.ItemDefinitions.Add(material);

            var result = service.Execute(
                "itemdefs",
                new GameSession(),
                null,
                catalog);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("material.test"));
            Assert.That(result.Message, Does.Contain("Materiał testowy"));

            Object.DestroyImmediate(material);
            Object.DestroyImmediate(catalog);
        }

        private static ItemDefinition CreateItemDefinition(
            string id,
            string displayName)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", displayName);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "BaseValue", 1);
            SetAutoProperty(definition, "Weight", 0f);
            SetAutoProperty(definition, "IsStackable", true);

            return definition;
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

        private static CharacterInstance CreateCharacter(
            string characterId)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                Level = 1,
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f)
                },
                ResourcePool = new ResourcePool(
                    ResourceType.Momentum,
                    currentValue: 0,
                    maxValue: 60,
                    regeneration: 0)
            };
        }
    }
}