using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonCombatOutcomeSystemTests
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
        public void TryApplyOutcome_Victory_MarksEncounterAsCleared()
        {
            var gameSession = CreateGameSessionWithDungeon(
                out var dungeon);

            var combatSession = new CombatSession
            {
                CombatId = "encounter.test",
                Outcome = CombatOutcomeState.Victory
            };

            var system = new DungeonCombatOutcomeSystem();

            var result = system.TryApplyOutcome(
                gameSession,
                combatSession);

            Assert.That(result, Is.True);

            Assert.That(
                dungeon.IsEncounterCleared("encounter.test"),
                Is.True);
        }

        [Test]
        public void TryApplyOutcome_Escaped_DoesNotClearEncounter()
        {
            var gameSession = CreateGameSessionWithDungeon(
                out var dungeon);

            var combatSession = new CombatSession
            {
                CombatId = "encounter.test",
                Outcome = CombatOutcomeState.Escaped
            };

            var system = new DungeonCombatOutcomeSystem();

            var result = system.TryApplyOutcome(
                gameSession,
                combatSession);

            Assert.That(result, Is.True);

            Assert.That(
                dungeon.IsEncounterCleared("encounter.test"),
                Is.False);
        }

        [Test]
        public void TryApplyOutcome_OngoingCombat_ReturnsFalse()
        {
            var system = new DungeonCombatOutcomeSystem();

            var result = system.TryApplyOutcome(
                new GameSession(),
                new CombatSession
                {
                    CombatId = "encounter.test",
                    Outcome = CombatOutcomeState.Ongoing
                });

            Assert.That(result, Is.False);
        }

        private GameSession CreateGameSessionWithDungeon(
            out DungeonInstance dungeon)
        {
            var encounterDefinition =
                ScriptableObject.CreateInstance<EncounterDefinition>();

            createdObjects.Add(encounterDefinition);

            var floorMap = new DungeonFloorMap();

            floorMap.TryAddFloorCell(
                new Vector2Int(0, 0),
                "room.test");

            var floor = new DungeonFloorInstance(0);

            var room = new DungeonRoomInstance(
                "room.test",
                DungeonRoomType.Normal);

            floor.TryAddRoom(room);
            floor.TrySetMap(floorMap);

            var encounter = new DungeonEncounterInstance(
                "encounter.test",
                encounterDefinition,
                room.RoomId,
                new RectInt(0, 0, 1, 1));

            floor.TryAddEncounter(encounter);

            dungeon = new DungeonInstance(
                "dungeon.test",
                123);

            dungeon.TryAddFloor(floor);

            var gameSession = new GameSession();

            gameSession.TrySetActiveDungeon(dungeon);

            var context = new DungeonCombatContext(
                "dungeon.test",
                0,
                "encounter.test",
                "room.test",
                new Vector2Int(0, 0),
                new Vector2Int(0, 0),
                new[] { new Vector2Int(0, 0) },
                null,
                null);

            gameSession.TrySetActiveDungeonCombatContext(context);

            return gameSession;
        }
    }
}