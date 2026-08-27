using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Quests;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
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
                new()
                {
                    new PassiveEffectSourceRuntime
                    {
                        SourceType = PassiveEffectSourceType.Skill,
                        SourceId = "skill.enemy.bleed.effect.0",
                        SourceCharacterId = "enemy.test"
                    }
                },
                stacks: 2,
                remainingDuration: 3,
                totalDamage: 30,
                lifetime: PassiveEffectLifetime.Timed,
                tickPhase: PassiveEffectTickPhase.TurnStart));

            var pendingLoot = new ItemInstance(
                materialDefinition,
                "item.pending.01");

            var session = new GameSession();
            session.TryRegisterCharacter(jerzy);
            session.TryAddCharacterToActiveParty("jerzy");
            session.TryAddGold(35);
            session.TrySetExplorationPosition(new Vector2(12.5f, -3.25f));
            session.AddPendingLoot(new[] { pendingLoot });

            var factory = new GameSessionSaveSnapshotFactory();

            var saveData = factory.CreateSnapshot(session, "Kampania testowa");

            Assert.That(saveData.Gold, Is.EqualTo(35));
            Assert.That(saveData.CampaignId, Is.EqualTo(session.CampaignId));
            Assert.That(saveData.HasExplorationPosition, Is.True);
            Assert.That(saveData.ExplorationPositionX, Is.EqualTo(12.5f));
            Assert.That(saveData.ExplorationPositionY, Is.EqualTo(-3.25f));
            Assert.That(saveData.Party.ActiveLeaderId, Is.EqualTo("jerzy"));
            Assert.That(saveData.Party.ActiveMemberIds, Is.EquivalentTo(new[] { "jerzy" }));

            Assert.That(saveData.Characters, Has.Count.EqualTo(1));

            var characterSaveData = saveData.Characters[0];

            Assert.That(characterSaveData.CharacterId, Is.EqualTo("jerzy"));
            Assert.That(characterSaveData.ClassId, Is.EqualTo("class.scout"));
            Assert.That(characterSaveData.Level, Is.EqualTo(3));
            Assert.That(characterSaveData.CurrentHealth, Is.EqualTo(67));
            Assert.That(characterSaveData.ResourcePool.CurrentValue, Is.EqualTo(18));
            Assert.That(characterSaveData.HasPersistentResistances, Is.True);
            Assert.That(characterSaveData.Resistances.BleedResistance, Is.EqualTo(0.25f));
            Assert.That(characterSaveData.ShieldLayers[0].CurrentValue, Is.EqualTo(12));
            Assert.That(characterSaveData.PassiveEffects[0].EffectId, Is.EqualTo("status.bleed"));
            Assert.That(
                characterSaveData.PassiveEffects[0].Lifetime,
                Is.EqualTo(PassiveEffectLifetime.Timed));
            Assert.That(
                characterSaveData.PassiveEffects[0].Sources[0].SourceId,
                Is.EqualTo("skill.enemy.bleed.effect.0"));
            Assert.That(characterSaveData.PassiveEffects[0].TickPhase, Is.EqualTo(PassiveEffectTickPhase.TurnStart));
            Assert.That(
                characterSaveData.PassiveEffects[0].Payloads[0].Type,
                Is.EqualTo(PassiveEffectPayloadType.DamageOverTime));
            Assert.That(
                characterSaveData.PassiveEffects[0].Payloads[0].RemainingTotalDamage,
                Is.EqualTo(30));

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
        public void CreateSnapshot_CapturesWorldQuestAndNpcState()
        {
            var questDefinition = CreateQuestDefinition(
                "quest.catacombs",
                "stage.explore",
                "objective.leave");

            var materialDefinition = CreateItemDefinition(
                "material.iron",
                ItemType.Material,
                isStackable: true);

            var quest = new QuestInstance(
                questDefinition,
                QuestStatus.Available);

            quest.TrySetStatus(QuestStatus.Active);
            quest.TrySetHidden(true);
            quest.TryAddObjectiveProgress("objective.leave");
            quest.TrySetQuestFlag("quest.flag.approached_guard", "true");
            quest.TryRecordBranch("branch.help_guard");
            quest.TryClaimReward("stage.explore.reward.0");

            var shopItem = new ItemInstance(
                materialDefinition,
                "item.shop.01");

            var buybackItem = new ItemInstance(
                materialDefinition,
                "item.buyback.01");

            var npc = new NpcInstance
            {
                NpcId = "npc.guard",
                LoyaltyLevel = 4,
                SpentGoldTotal = 125,
                ShopInventory = new ShopInventory()
            };

            npc.ShopInventory.Items.Add(shopItem);
            npc.ShopInventory.BuybackStock.Add(
                new BuybackEntry(buybackItem, soldPrice: 7));

            var session = new GameSession();
            session.WorldState.TrySetFlag("world.catacombs.opened", "true");
            session.TryRegisterQuest(quest);
            session.TryRegisterNpc(npc);

            var factory = new GameSessionSaveSnapshotFactory();

            var saveData = factory.CreateSnapshot(session, "Kampania testowa");

            Assert.That(saveData.WorldFlags, Has.Count.EqualTo(1));
            Assert.That(saveData.WorldFlags[0].Key, Is.EqualTo("world.catacombs.opened"));
            Assert.That(saveData.WorldFlags[0].Value, Is.EqualTo("true"));

            Assert.That(saveData.Quests, Has.Count.EqualTo(1));

            var questSaveData = saveData.Quests[0];

            Assert.That(questSaveData.QuestId, Is.EqualTo("quest.catacombs"));
            Assert.That(questSaveData.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(questSaveData.CurrentStageId, Is.EqualTo("stage.explore"));
            Assert.That(questSaveData.IsHidden, Is.True);
            Assert.That(questSaveData.ObjectiveProgress[0].ObjectiveId, Is.EqualTo("objective.leave"));
            Assert.That(questSaveData.ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));
            Assert.That(questSaveData.ObjectiveProgress[0].IsCompleted, Is.True);
            Assert.That(questSaveData.QuestFlags[0].Key, Is.EqualTo("quest.flag.approached_guard"));
            Assert.That(questSaveData.ChosenBranchIds, Is.EquivalentTo(new[] { "branch.help_guard" }));
            Assert.That(
                questSaveData.ClaimedRewardKeys,
                Is.EquivalentTo(new[] { "stage.explore.reward.0" }));

            Assert.That(saveData.Npcs, Has.Count.EqualTo(1));

            var npcSaveData = saveData.Npcs[0];

            Assert.That(npcSaveData.NpcId, Is.EqualTo("npc.guard"));
            Assert.That(npcSaveData.LoyaltyLevel, Is.EqualTo(4));
            Assert.That(npcSaveData.SpentGoldTotal, Is.EqualTo(125));
            Assert.That(
                npcSaveData.ShopItemInstanceIds,
                Is.EquivalentTo(new[] { "item.shop.01" }));

            Assert.That(npcSaveData.BuybackEntries, Has.Count.EqualTo(1));
            Assert.That(
                npcSaveData.BuybackEntries[0].ItemInstanceId,
                Is.EqualTo("item.buyback.01"));
            Assert.That(npcSaveData.BuybackEntries[0].SoldPrice, Is.EqualTo(7));

            Assert.That(saveData.ItemInstances, Has.Count.EqualTo(2));

            Object.DestroyImmediate(questDefinition);
            Object.DestroyImmediate(materialDefinition);
        }

        [Test]
        public void CreateSnapshot_CapturesActiveDungeonAsSeedAndDeltas()
        {
            var dungeonDefinition = ScriptableObject.CreateInstance<DungeonDefinition>();

            SetAutoProperty(dungeonDefinition, "Id", "dungeon.test");

            var dungeon = new DungeonInstance(dungeonDefinition, seed: 12345);
            var floor = new DungeonFloorInstance(0);

            var startRoom = new DungeonRoomInstance(
                "floor.0.start",
                DungeonRoomType.Start);

            var normalRoom = new DungeonRoomInstance(
                "floor.0.normal",
                DungeonRoomType.Normal);

            startRoom.TryMarkVisited();

            floor.TryAddRoom(startRoom);
            floor.TryAddRoom(normalRoom);
            dungeon.TryAddFloor(floor);
            dungeon.SetPlayerPosition(new Vector2(12.5f, -4f));
            dungeon.TryMarkEncounterCleared("encounter.floor.0.01");

            var session = new GameSession();
            session.TrySetActiveDungeon(dungeon);

            var factory = new GameSessionSaveSnapshotFactory();

            var saveData = factory.CreateSnapshot(session, "Kampania testowa");

            Assert.That(saveData.ActiveDungeon, Is.Not.Null);
            Assert.That(saveData.ActiveDungeon.DungeonId, Is.EqualTo("dungeon.test"));
            Assert.That(saveData.ActiveDungeon.Seed, Is.EqualTo(12345));
            Assert.That(saveData.ActiveDungeon.CurrentFloorIndex, Is.EqualTo(0));
            Assert.That(saveData.ActiveDungeon.HasPlayerPosition, Is.True);
            Assert.That(saveData.ActiveDungeon.PlayerPositionX, Is.EqualTo(12.5f));
            Assert.That(saveData.ActiveDungeon.PlayerPositionY, Is.EqualTo(-4f));
            Assert.That(
                saveData.ActiveDungeon.ClearedEncounterIds,
                Is.EquivalentTo(new[] { "encounter.floor.0.01" }));

            Assert.That(saveData.ActiveDungeon.FloorDeltas, Has.Count.EqualTo(1));
            Assert.That(
                saveData.ActiveDungeon.FloorDeltas[0].VisitedRoomIds,
                Is.EquivalentTo(new[] { "floor.0.start" }));

            Object.DestroyImmediate(dungeonDefinition);
        }

        [Test]
        public void CreateSnapshot_CapturesAndRestoresDefeatedEncounterEnemies()
        {
            var dungeonDefinition = ScriptableObject.CreateInstance<DungeonDefinition>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();

            SetAutoProperty(dungeonDefinition, "Id", "dungeon.test");

            var dungeon = new DungeonInstance(dungeonDefinition, seed: 12345);
            var floor = new DungeonFloorInstance(0);

            var room = new DungeonRoomInstance(
                "floor.0.normal",
                DungeonRoomType.Normal);

            var encounter = new DungeonEncounterInstance(
                "encounter.floor.0.normal.01",
                encounterDefinition,
                room.RoomId,
                new RectInt(0, 0, 4, 4));

            Assert.That(floor.TryAddRoom(room), Is.True);
            Assert.That(floor.TryAddEncounter(encounter), Is.True);
            Assert.That(
                encounter.TryMarkEnemyDefeated("enemy.floor.0.normal.01"),
                Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            var session = new GameSession();

            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var snapshotFactory = new GameSessionSaveSnapshotFactory();

            var saveData = snapshotFactory.CreateSnapshot(
                session,
                "Kampania testowa");

            Assert.That(saveData.ActiveDungeon.EncounterDeltas, Has.Count.EqualTo(1));

            var encounterDelta = saveData.ActiveDungeon.EncounterDeltas[0];

            Assert.That(
                encounterDelta.EncounterInstanceId,
                Is.EqualTo("encounter.floor.0.normal.01"));

            Assert.That(
                encounterDelta.DefeatedEnemyInstanceIds,
                Is.EquivalentTo(new[] { "enemy.floor.0.normal.01" }));

            var restoredFloor = new DungeonFloorInstance(0);

            var restoredRoom = new DungeonRoomInstance(
                "floor.0.normal",
                DungeonRoomType.Normal);

            var restoredEncounter = new DungeonEncounterInstance(
                "encounter.floor.0.normal.01",
                encounterDefinition,
                restoredRoom.RoomId,
                new RectInt(0, 0, 4, 4));

            Assert.That(restoredFloor.TryAddRoom(restoredRoom), Is.True);
            Assert.That(restoredFloor.TryAddEncounter(restoredEncounter), Is.True);

            Assert.That(
                restoredFloor.TryRestoreEncounterDefeatedEnemyInstanceIds(
                    encounterDelta.EncounterInstanceId,
                    encounterDelta.DefeatedEnemyInstanceIds),
                Is.True);

            Assert.That(
                restoredEncounter.IsEnemyDefeated("enemy.floor.0.normal.01"),
                Is.True);

            Object.DestroyImmediate(dungeonDefinition);
            Object.DestroyImmediate(encounterDefinition);
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

        private static QuestDefinition CreateQuestDefinition(
            string questId,
            string stageId,
            string objectiveId)
        {
            var objective = new QuestObjectiveDefinition();

            SetAutoProperty(objective, "ObjectiveId", objectiveId);
            SetAutoProperty(objective, "RequiredAmount", 1);

            var stage = new QuestStageDefinition();

            SetAutoProperty(stage, "StageId", stageId);
            SetAutoProperty(
                stage,
                "Objectives",
                new System.Collections.Generic.List<QuestObjectiveDefinition>
                {
                    objective
                });

            var definition = ScriptableObject.CreateInstance<QuestDefinition>();

            SetAutoProperty(definition, "Id", questId);
            SetAutoProperty(definition, "StartStageId", stageId);
            SetAutoProperty(
                definition,
                "Stages",
                new System.Collections.Generic.List<QuestStageDefinition>
                {
                    stage
                });

            return definition;
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
