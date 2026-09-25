using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonCampTests
    {
        private GameSession game;
        private CharacterInstance mage, warrior;
        private GameContentCatalog catalog;
        private ItemDefinition kit;
        private DungeonCampService service;

        [SetUp] public void Setup()
        {
            game = new GameSession(GameModeState.Exploration);
            mage = Character("mage", ResourceType.Mana);
            warrior = Character("warrior", ResourceType.Rage);
            game.TryRegisterCharacter(mage); game.TryRegisterCharacter(warrior);
            game.TryAddCharacterToActiveParty(mage.CharacterId); game.TryAddCharacterToActiveParty(warrior.CharacterId);
            game.TrySetActiveLeader(mage.CharacterId);
            var dungeon = new DungeonInstance("catacombs", 1);
            dungeon.TryAddFloor(new DungeonFloorInstance(0)); dungeon.TryAddFloor(new DungeonFloorInstance(1));
            game.TrySetActiveDungeon(dungeon);
            kit = ScriptableObject.CreateInstance<ItemDefinition>();
            typeof(ItemDefinition).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(kit, DungeonCampService.KitId);
            catalog = ScriptableObject.CreateInstance<GameContentCatalog>(); catalog.ItemDefinitions.Add(kit);
            service = new DungeonCampService();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(kit); Object.DestroyImmediate(catalog); }
        private static CharacterInstance Character(string id, ResourceType type) => new()
        {
            CharacterId = id, LifeState = CharacterLifeState.Alive, CurrentHealth = 10,
            Stats = new CharacterStats { MaxHealth = new Stat(100) },
            ResourcePool = new ResourcePool(type, 10, 100, 20)
        };
        [Test] public void SuppliesMatchFloorsAndCannotBeIssuedAgain()
        {
            Assert.That(service.TryIssueSupplies(game, catalog), Is.True);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(service.TryIssueSupplies(game, catalog), Is.False);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(2));
        }
        [Test] public void TwoCampsOnSameFloorConsumeTwoItemsAndDoNotRestoreRage()
        {
            service.TryIssueSupplies(game, catalog);
            var floor = game.ActiveDungeon.Floors[0];
            Assert.That(service.TryCamp(game, floor, out _), Is.True);
            Assert.That(mage.CurrentHealth, Is.EqualTo(50));
            Assert.That(mage.ResourcePool.CurrentValue, Is.EqualTo(50));
            Assert.That(warrior.CurrentHealth, Is.EqualTo(50));
            Assert.That(warrior.ResourcePool.CurrentValue, Is.EqualTo(10));
            Assert.That(service.TryCamp(game, floor, out _), Is.True);
            Assert.That(mage.CurrentHealth, Is.EqualTo(90));
            Assert.That(mage.Inventory.Items, Is.Empty);
            Assert.That(service.TryCamp(game, floor, out _), Is.False);
        }
        [Test] public void FailureDoesNotConsumeAndDeadMembersAreNotRevived()
        {
            service.TryIssueSupplies(game, catalog);
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[1], out _), Is.False);
            game.PlayerParty.TryDetach(warrior.CharacterId);
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[0], out _), Is.False);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(2));
            warrior.LifeState = CharacterLifeState.Dead; warrior.CurrentHealth = 0;
            mage.CurrentHealth = 90;
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[0], out _), Is.True);
            Assert.That(mage.CurrentHealth, Is.EqualTo(100));
            Assert.That(warrior.CurrentHealth, Is.Zero);
            Assert.That(warrior.LifeState, Is.EqualTo(CharacterLifeState.Dead));
        }
        [Test] public void ExitRemovesTransferredAndStoredSuppliesWithoutRefillOnResume()
        {
            service.TryIssueSupplies(game, catalog);
            game.Stash.Items.Add(mage.Inventory.Items[0]); mage.Inventory.Items.RemoveAt(0);
            warrior.Inventory.Items.Add(mage.Inventory.Items[0]); mage.Inventory.Items.Clear();
            service.RemoveSupplies(game);
            Assert.That(game.Stash.Items, Is.Empty);
            Assert.That(warrior.Inventory.Items, Is.Empty);
            Assert.That(service.TryIssueSupplies(game, catalog), Is.False);
        }
        [Test] public void InventoryUseConsumesTheClickedInstanceFromItsOwner()
        {
            service.TryIssueSupplies(game, catalog);
            var clicked = mage.Inventory.Items[1];
            mage.Inventory.Items.Remove(clicked); warrior.Inventory.Items.Add(clicked);
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[0], out _, warrior.CharacterId, clicked.InstanceId), Is.True);
            Assert.That(warrior.Inventory.Items, Is.Empty);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[0], out _, warrior.CharacterId, clicked.InstanceId), Is.False);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(1));
        }
        [Test] public void CombatBlocksCampWithoutConsumingSupplies()
        {
            service.TryIssueSupplies(game, catalog);
            Assert.That(game.TrySetActiveCombat(new Godmist.Runtime.Combat.CombatSession { CombatId = "camp.test.combat" }), Is.True);
            Assert.That(service.TryCamp(game, game.ActiveDungeon.Floors[0], out _), Is.False);
            Assert.That(mage.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(mage.CurrentHealth, Is.EqualTo(10));
        }
    }
}
