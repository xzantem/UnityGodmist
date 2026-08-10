using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class GameSessionSaveSnapshotFactoryTests
    {
        [Test]
        public void CreateSnapshot_CapturesPartyCharactersItemsAndPendingLoot()
        {
            var jerzyDefinition = CreateCharacterDefinition("jerzy");
            var scoutClass = CreateClassData("class.scout", ResourceType.Momentum);
            var materialDefinition = CreateItemDefinition(
                "material.iron",
                ItemType.Material,
                isStackable: true);

            var weaponDefinition = CreateEquipmentDefinition(
                "weapon.test.sword",
                EquipmentSlot.Weapon);

            var material = new ItemInstance(
                materialDefinition,
                "item.material.01");

            var weapon = new EquipmentItemInstance(
                weaponDefinition,
                "item.weapon.01")
            {
                RequiredClassId = "class.scout",
                RequiredLevel = 2
            };

            var jerzy = new CharacterInstance
            {
                CharacterId = "jerzy",
                Definition = jerzyDefinition,
                ClassData = scoutClass,
                Level = 3,
                CurrentExperience = 28,
                AvailableSkillPoints = 2,
                CurrentHealth = 67,
                Honor = 15,
                WeaponAccuracy = 112f,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                ResourcePool = new ResourcePool(
                    ResourceType.Momentum,
                    currentValue: 18,
                    maxValue: 60,
                    regeneration: 4),
                Resistances = new ResistanceSet
                {
                    BleedResistance = 0.25f,
                    FireResistance = 0.1f
                },
                ShieldPool = new ShieldPool(),
                Inventory = new Inventory(),
                Equipment = new EquipmentSet()
            };

            jerzy.ResourcePool.HasStartedTurn = true;
            jerzy.ResourcePool.FractionalGainRemainder = 0.5f;
            jerzy.ShieldPool.AddLayer(12, "skill.test.shield");
            jerzy.Inventory.Items.Add(material);
            jerzy.Equipment.Weapon = weapon;
            jerzy.PassiveEffects.Add(new PassiveEffectRuntime(
                "status.bleed",
                new() { "enemy.test" },
                stacks: 2,
                remainingDuration: 3,
                totalDamage: 30));

            var pendingLoot = new ItemInstance(
                materialDefinition,
                "item.pending.01");

            var session = new GameSession();
            session.TryRegisterCharacter(jerzy);
            session.TryAddCharacterToActiveParty("jerzy");
            session.TryAddGold(35);
            session.AddPendingLoot(new[] { pendingLoot });

            var factory = new GameSessionSaveSnapshotFactory();

            var saveData = factory.CreateSnapshot(session, "Kampania testowa");

            Assert.That(saveData.Gold, Is.EqualTo(35));
            Assert.That(saveData.Party.ActiveLeaderId, Is.EqualTo("jerzy"));
            Assert.That(saveData.Party.ActiveMemberIds, Is.EquivalentTo(new[] { "jerzy" }));

            Assert.That(saveData.Characters, Has.Count.EqualTo(1));

            var characterSaveData = saveData.Characters[0];

            Assert.That(characterSaveData.CharacterId, Is.EqualTo("jerzy"));
            Assert.That(characterSaveData.ClassId, Is.EqualTo("class.scout"));
            Assert.That(characterSaveData.Level, Is.EqualTo(3));
            Assert.That(characterSaveData.CurrentHealth, Is.EqualTo(67));
            Assert.That(characterSaveData.ResourcePool.CurrentValue, Is.EqualTo(18));
            Assert.That(characterSaveData.Resistances.BleedResistance, Is.EqualTo(0.25f));
            Assert.That(characterSaveData.ShieldLayers[0].CurrentValue, Is.EqualTo(12));
            Assert.That(characterSaveData.PassiveEffects[0].EffectId, Is.EqualTo("status.bleed"));

            Assert.That(
                characterSaveData.InventoryItemInstanceIds,
                Is.EquivalentTo(new[] { "item.material.01" }));

            Assert.That(
                characterSaveData.Equipment.WeaponItemInstanceId,
                Is.EqualTo("item.weapon.01"));

            Assert.That(saveData.ItemInstances, Has.Count.EqualTo(3));
            Assert.That(
                saveData.PendingLootItemInstanceIds,
                Is.EquivalentTo(new[] { "item.pending.01" }));

            var weaponSaveData = saveData.ItemInstances.Find(
                item => item.InstanceId == "item.weapon.01");

            Assert.That(weaponSaveData.IsEquipment, Is.True);
            Assert.That(weaponSaveData.RequiredClassId, Is.EqualTo("class.scout"));
            Assert.That(weaponSaveData.RequiredLevel, Is.EqualTo(2));

            Object.DestroyImmediate(jerzyDefinition);
            Object.DestroyImmediate(scoutClass);
            Object.DestroyImmediate(materialDefinition);
            Object.DestroyImmediate(weaponDefinition);
        }

        [Test]
        public void CreateSnapshot_WhenCombatIsActive_ThrowsException()
        {
            var session = new GameSession();
            session.TrySetActiveCombat(
                new Godmist.Runtime.Combat.CombatSession
                {
                    CombatId = "combat.test"
                });

            var factory = new GameSessionSaveSnapshotFactory();

            Assert.Throws<System.InvalidOperationException>(
                () => factory.CreateSnapshot(session, "Niepoprawny zapis"));
        }

        private static CharacterDefinition CreateCharacterDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            SetAutoProperty(definition, "Id", id);

            return definition;
        }

        private static CharacterClassData CreateClassData(
            string id,
            ResourceType resourceType)
        {
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();

            SetAutoProperty(classData, "Id", id);
            SetAutoProperty(classData, "ResourceType", resourceType);

            return classData;
        }

        private static ItemDefinition CreateItemDefinition(
            string id,
            ItemType itemType,
            bool isStackable)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "ItemType", itemType);
            SetAutoProperty(definition, "IsStackable", isStackable);

            return definition;
        }

        private static EquipmentItemDefinition CreateEquipmentDefinition(
            string id,
            EquipmentSlot slot)
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "ItemType", ItemType.Weapon);
            SetAutoProperty(definition, "EquipmentSlot", slot);

            return definition;
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

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}