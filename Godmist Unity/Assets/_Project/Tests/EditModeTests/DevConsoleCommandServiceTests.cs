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
        public void Execute_CreateItem_UsesNamedOptionalAmountAndCharacterId()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var leader = CreateCharacter("jerzy");
            var target = CreateCharacter("wladimir");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var material = CreateItemDefinition(
                "material.test",
                "Materiał testowy");

            session.TryRegisterCharacter(leader);
            session.TryRegisterCharacter(target);
            session.TryAddCharacterToActiveParty("jerzy");
            catalog.ItemDefinitions.Add(material);

            var result = service.Execute(
                "createitem material.test characterId=wladimir amount=2",
                session,
                null,
                catalog);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(target.Inventory.Items, Has.Count.EqualTo(2));
            Assert.That(leader.Inventory.Items, Is.Empty);

            Object.DestroyImmediate(material);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Execute_CreateConsumable_UsesNamedOptionalCharacterId()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var leader = CreateCharacter("jerzy");
            var target = CreateCharacter("wladimir");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var potion = CreateConsumableDefinition(
                "item.consumable.potion");

            session.TryRegisterCharacter(leader);
            session.TryRegisterCharacter(target);
            session.TryAddCharacterToActiveParty("jerzy");
            catalog.ItemDefinitions.Add(potion);

            var result = service.Execute(
                "createconsumable item.consumable.potion " +
                "RestoreHealth 0.25 RestoreResource 0.1 ModifyDamage 0.2 " +
                "characterId=wladimir",
                session,
                null,
                catalog);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(target.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(leader.Inventory.Items, Is.Empty);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Execute_CreateConsumable_UtilityPointsToCreateItem()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var returnScroll = ScriptableObject.CreateInstance<ConsumableItemDefinition>();

            SetAutoProperty(returnScroll, "Id", "item.consumable.return_scroll");
            SetAutoProperty(returnScroll, "DisplayName", "Return Scroll");
            SetAutoProperty(returnScroll, "ItemType", ItemType.Tool);
            SetAutoProperty(returnScroll, "UseType", ConsumableUseType.Utility);

            session.TryRegisterCharacter(jerzy);
            session.TryAddCharacterToActiveParty("jerzy");
            catalog.ItemDefinitions.Add(returnScroll);

            var result = service.Execute(
                "createconsumable item.consumable.return_scroll",
                session,
                null,
                catalog);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("createitem"));
            Assert.That(jerzy.Inventory.Items, Is.Empty);

            Object.DestroyImmediate(returnScroll);
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

        [Test]
        public void Execute_UnitWithCharacterId_ReturnsDetailedRuntimeReport()
        {
            var service = new DevConsoleCommandService();
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");

            jerzy.CurrentHealth = 75;
            jerzy.ShieldPool = new ShieldPool();
            jerzy.ShieldPool.AddLayer(20, "test.shield");
            jerzy.Honor = 15;
            jerzy.DebugIsInvulnerable = true;

            session.TryRegisterCharacter(jerzy);

            var result = service.Execute(
                "unit jerzy",
                session,
                null);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("jerzy"));
            Assert.That(result.Message, Does.Contain("HP: 75/100"));
            Assert.That(result.Message, Does.Contain("Tarcza: 20"));
            Assert.That(result.Message, Does.Contain("Honor: 15"));
            Assert.That(result.Message, Does.Contain("Godmode: ON"));
            Assert.That(
                result.Message,
                Does.Contain("Walka: postać nie uczestniczy"));
        }

        [Test]
        public void Execute_UnitWithoutId_UsesActiveCombatUnit()
        {
            var service = new DevConsoleCommandService();
            var jerzy = CreateCharacter("jerzy");

            var unit = new BattleUnit
            {
                Character = jerzy,
                Position = new GridPosition(3, 4),
                CurrentActionPoints = 12,
                MaxActionPoints = 15,
                IsPlayerSide = true
            };

            var combatSession = new CombatSession();
            combatSession.Units.Add(unit);
            combatSession.ActiveUnit = unit;

            var result = service.Execute(
                "unit",
                new GameSession(),
                combatSession);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("jerzy"));
            Assert.That(result.Message, Does.Contain("Pozycja: 3, 4"));
            Assert.That(result.Message, Does.Contain("AP: 12/15"));
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

        private static ConsumableItemDefinition CreateConsumableDefinition(
            string id)
        {
            var definition =
                ScriptableObject.CreateInstance<ConsumableItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", ItemType.Potion);
            SetAutoProperty(definition, "UseType", ConsumableUseType.Potion);

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
