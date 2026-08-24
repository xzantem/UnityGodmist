using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonEnemyBodyTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void VictoryCreatesBodiesOnlyForDefeatedEnemyRewards()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);

            var item = new ItemInstance(
                CreateItemDefinition("item.body"),
                "item.body.instance");

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.defeated",
                            Gold = 17,
                            Items = { item }
                        },
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.escaped",
                            Gold = 99
                        }
                    }
                }
            };

            combat.TryMarkEnemyDefeated("enemy.defeated");

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);

            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(
                floor.EnemyBodies[0].EnemyInstanceId,
                Is.EqualTo("enemy.defeated"));
            Assert.That(floor.EnemyBodies[0].Gold, Is.EqualTo(17));
            Assert.That(
                floor.EnemyBodies[0].Items[0].InstanceId,
                Is.EqualTo("item.body.instance"));

            var bodyInstanceId = floor.EnemyBodies[0].InstanceId;
            Assert.That(floor.TryRemoveEnemyBody(bodyInstanceId), Is.True);
            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);
            Assert.That(floor.EnemyBodies, Is.Empty);
        }

        [Test]
        public void EscapeDoesNotCreateBodies()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Escaped,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.escaped",
                            Gold = 25
                        }
                    }
                }
            };

            combat.TryMarkEnemyDefeated("enemy.escaped");

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);

            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(floor.EnemyBodies, Is.Empty);
        }

        [Test]
        public void BodyClaimsItemAndGoldExactlyOnce()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.leader",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(
                session.TryAddCharacterToActiveParty(character.CharacterId),
                Is.True);

            var dungeon = new DungeonInstance("dungeon.body", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(floor.TrySetMap(new DungeonFloorMap()), Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var item = new ItemInstance(
                CreateItemDefinition("item.claim"),
                "item.claim.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.claim",
                "enemy.claim",
                Vector2Int.zero,
                13,
                new[] { item });

            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            var service = new LootCollectionService();

            Assert.That(
                service.TryClaimDungeonBodyItem(
                    session,
                    body.InstanceId,
                    item.InstanceId),
                Is.True);
            Assert.That(character.Inventory.Items, Is.EqualTo(new[] { item }));
            Assert.That(
                service.TryClaimDungeonBodyItem(
                    session,
                    body.InstanceId,
                    item.InstanceId),
                Is.False);

            Assert.That(
                service.TryClaimDungeonBodyGold(
                    session,
                    body.InstanceId,
                    out var claimedGold),
                Is.True);
            Assert.That(claimedGold, Is.EqualTo(13));
            Assert.That(session.Gold, Is.EqualTo(13));
            Assert.That(floor.EnemyBodies, Is.Empty);
            Assert.That(
                service.TryClaimDungeonBodyGold(
                    session,
                    body.InstanceId,
                    out _),
                Is.False);
        }

        [Test]
        public void OverweightBodyItemDoesNotPreventClaimingItsGold()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.overweight",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory { MaxWeight = 1f }
            };

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.overweight", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var itemDefinition = CreateItemDefinition("item.overweight");
            SetAutoProperty(itemDefinition, "Weight", 2f);
            var item = new ItemInstance(itemDefinition, "item.overweight.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.overweight", "enemy.overweight", Vector2Int.zero,
                11, new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            var service = new LootCollectionService();
            Assert.That(service.TryClaimNextDungeonBodyItem(
                session, body.InstanceId, out _), Is.False);
            Assert.That(service.TryClaimDungeonBodyGold(
                session, body.InstanceId, out var gold), Is.True);
            Assert.That(gold, Is.EqualTo(11));
            Assert.That(session.Gold, Is.EqualTo(11));
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(floor.EnemyBodies[0].Items[0], Is.SameAs(item));
        }

        [Test]
        public void BodyItemClaimCanTargetAnotherActiveCharacterAndPreservesInstanceId()
        {
            var session = new GameSession(GameModeState.Exploration);
            var leader = new CharacterInstance
            {
                CharacterId = "character.leader.target",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };
            var target = new CharacterInstance
            {
                CharacterId = "character.target",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };

            Assert.That(session.TryRegisterCharacter(leader), Is.True);
            Assert.That(session.TryRegisterCharacter(target), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(leader.CharacterId), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(target.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.target", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var item = new ItemInstance(
                CreateItemDefinition("item.target"),
                "item.target.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.target",
                "enemy.target",
                Vector2Int.zero,
                0,
                new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            Assert.That(new LootCollectionService().TryClaimDungeonBodyItem(
                session,
                body.InstanceId,
                target.CharacterId,
                item.InstanceId), Is.True);
            Assert.That(target.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(target.Inventory.Items[0], Is.SameAs(item));
            Assert.That(target.Inventory.Items[0].InstanceId,
                Is.EqualTo("item.target.instance"));
            Assert.That(leader.Inventory.Items, Is.Empty);
        }

        [Test]
        public void BodyClaimsAreRejectedDuringCombat()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.combat",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.combat", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            var item = new ItemInstance(
                CreateItemDefinition("item.combat"),
                "item.combat.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.combat",
                "enemy.combat",
                Vector2Int.zero,
                7,
                new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            Assert.That(session.TrySetActiveCombat(new CombatSession
            {
                CombatId = "combat.active"
            }), Is.True);

            var service = new LootCollectionService();
            Assert.That(service.TryClaimDungeonBodyItem(
                session,
                body.InstanceId,
                item.InstanceId), Is.False);
            Assert.That(service.TryClaimDungeonBodyGold(
                session,
                body.InstanceId,
                out _), Is.False);
            Assert.That(character.Inventory.Items, Is.Empty);
            Assert.That(session.Gold, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotCapturesBodyGoldPositionAndExactItemIds()
        {
            var session = new GameSession();
            var dungeon = new DungeonInstance("dungeon.save.body", 5);
            var floor = new DungeonFloorInstance(0);
            var item = new ItemInstance(
                CreateItemDefinition("item.save.body"),
                "item.save.body.instance");

            Assert.That(
                floor.TryAddEnemyBody(
                    new DungeonEnemyBodyInstance(
                        "body.save",
                        "enemy.save",
                        new Vector2Int(8, -2),
                        21,
                        new[] { item })),
                Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var saveData = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                session,
                "Body save");

            var bodyDelta = saveData.ActiveDungeon.FloorDeltas[0]
                .EnemyBodyDeltas[0];

            Assert.That(bodyDelta.BodyInstanceId, Is.EqualTo("body.save"));
            Assert.That(bodyDelta.EnemyInstanceId, Is.EqualTo("enemy.save"));
            Assert.That(bodyDelta.PositionX, Is.EqualTo(8));
            Assert.That(bodyDelta.PositionY, Is.EqualTo(-2));
            Assert.That(bodyDelta.Gold, Is.EqualTo(21));
            Assert.That(
                bodyDelta.ItemInstanceIds,
                Is.EqualTo(new[] { "item.save.body.instance" }));
            Assert.That(
                saveData.ItemInstances[0].InstanceId,
                Is.EqualTo("item.save.body.instance"));
        }

        [Test]
        public void SaveRestore_PreservesEnemyBodyPayload()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var dungeonDefinition = CreateRestoreDungeonDefinition();
            var item = new ItemInstance(
                CreateItemDefinition("item.body.restore"),
                "item.body.restore.instance");

            createdObjects.Add(catalog);
            catalog.DungeonDefinitions.Add(dungeonDefinition);
            catalog.ItemDefinitions.Add(item.Definition);

            var dungeon = new DungeonGeneratorService().Generate(
                dungeonDefinition,
                731);
            var floor = dungeon.Floors[0];

            Assert.That(floor.TryAddEnemyBody(new DungeonEnemyBodyInstance(
                "body.restore", "enemy.restore", new Vector2Int(4, 7),
                19, new[] { item })), Is.True);

            var session = new GameSession(GameModeState.Exploration);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var serializer = new GameSaveJsonSerializer();
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                session, "body restore");

            Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot),
                out var restoredSave), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(
                restoredSave, catalog, out var restoredSession), Is.True);
            Assert.That(restoredSession.ActiveDungeon.TryGetCurrentFloor(
                out var restoredFloor), Is.True);
            Assert.That(restoredFloor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(restoredFloor.EnemyBodies[0].InstanceId,
                Is.EqualTo("body.restore"));
            Assert.That(restoredFloor.EnemyBodies[0].EnemyInstanceId,
                Is.EqualTo("enemy.restore"));
            Assert.That(restoredFloor.EnemyBodies[0].Position,
                Is.EqualTo(new Vector2Int(4, 7)));
            Assert.That(restoredFloor.EnemyBodies[0].Gold, Is.EqualTo(19));
            Assert.That(restoredFloor.EnemyBodies[0].Items[0].InstanceId,
                Is.EqualTo("item.body.restore.instance"));
        }

        private DungeonDefinition CreateRestoreDungeonDefinition()
        {
            var templates = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            var layout = ScriptableObject.CreateInstance<DungeonLayoutProfile>();
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();
            createdObjects.Add(templates);
            createdObjects.Add(layout);
            createdObjects.Add(definition);

            SetAutoProperty(templates, "Id", "templates.body.restore");
            foreach (var roomType in new[]
                     {
                         DungeonRoomType.Start,
                         DungeonRoomType.Normal,
                         DungeonRoomType.Corridor,
                         DungeonRoomType.Final
                     })
            {
                var template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
                createdObjects.Add(template);
                SetAutoProperty(template, "Id", "room.body." + roomType);
                SetAutoProperty(template, "RoomType", roomType);
                SetAutoProperty(template, "SizeInCells", new Vector2Int(8, 8));
                templates.Templates.Add(template);
            }

            SetAutoProperty(definition, "Id", "dungeon.body.restore");
            SetAutoProperty(definition, "IsProcedural", true);
            SetAutoProperty(definition, "MinFloorCount", 1);
            SetAutoProperty(definition, "MaxFloorCount", 1);
            SetAutoProperty(definition, "MinRoomCountPerFloor", 3);
            SetAutoProperty(definition, "MaxRoomCountPerFloor", 3);
            SetAutoProperty(definition, "MinEncounterCountPerFloor", 0);
            SetAutoProperty(definition, "MaxEncounterCountPerFloor", 0);
            SetAutoProperty(definition, "MinObjectCountPerFloor", 0);
            SetAutoProperty(definition, "MaxObjectCountPerFloor", 0);
            SetAutoProperty(definition, "RoomTemplateCatalog", templates);
            SetAutoProperty(definition, "LayoutProfile", layout);
            return definition;
        }

        private GameSession CreateSessionWithDungeon(
            out DungeonInstance dungeon,
            out DungeonCombatContext context)
        {
            var encounterDefinition =
                ScriptableObject.CreateInstance<EncounterDefinition>();
            createdObjects.Add(encounterDefinition);

            var floor = new DungeonFloorInstance(0);
            var room = new DungeonRoomInstance(
                "room.body",
                DungeonRoomType.Normal);

            Assert.That(floor.TryAddRoom(room), Is.True);
            Assert.That(
                floor.TrySetMap(new DungeonFloorMap()),
                Is.True);

            var encounter = new DungeonEncounterInstance(
                "encounter.body",
                encounterDefinition,
                room.RoomId,
                new RectInt(0, 0, 1, 1));

            Assert.That(floor.TryAddEncounter(encounter), Is.True);

            dungeon = new DungeonInstance("dungeon.body", 1);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            var session = new GameSession();
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            context = new DungeonCombatContext(
                dungeon.DungeonId,
                0,
                encounter.InstanceId,
                room.RoomId,
                Vector2Int.zero,
                new Vector2Int(3, 4),
                new[] { Vector2Int.zero },
                null,
                null);

            Assert.That(session.TrySetActiveDungeonCombatContext(context), Is.True);
            return session;
        }

        private ItemDefinition CreateItemDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

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

            field.SetValue(target, value);
        }
    }
}
