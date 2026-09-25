using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Runtime.Characters;
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

        [Test]
        public void BodiesCanAppearDuringCombatWithoutDuplicatingAfterVictoryAndRetainHeight()
        {
            CreateGameSessionWithDungeon(out var dungeon);
            dungeon.TryGetFloor(0, out var floor);
            var upper = new DungeonSurfacePosition(Vector2Int.zero, 1);
            floor.Map.TryAddFloorCell(upper, "room.test");
            var context = new DungeonCombatContext("dungeon.test", 0, "encounter.test", "room.test",
                Vector2Int.zero, Vector2Int.zero, new[] { Vector2Int.zero }, null, null, surfaceMap: floor.Map);
            var session = new CombatSession { DungeonContext = context, Outcome = CombatOutcomeState.Ongoing };
            session.TryMarkEnemyDefeated("enemy.dead", new GridPosition(0, 0, 1));
            Assert.That(DungeonCombatOutcomeSystem.TryCreateEnemyBodies(dungeon, context, session), Is.True);
            Assert.That(floor.EnemyBodies.Count, Is.EqualTo(1));
            Assert.That(floor.TryGetActorPosition("body:encounter.test.body.enemy.dead", out var position), Is.True);
            Assert.That(position.Surface, Is.EqualTo(upper));
            session.Outcome = CombatOutcomeState.Victory;
            Assert.That(DungeonCombatOutcomeSystem.TryCreateEnemyBodies(dungeon, context, session), Is.True);
            Assert.That(floor.EnemyBodies.Count, Is.EqualTo(1));
        }

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

            Assert.That(
                dungeon.TrySetPendingPlayerPartyPositions(
                    new Dictionary<string, Vector2>
                    {
                        ["character.pending"] = Vector2.one
                    }),
                Is.True);

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
            Assert.That(dungeon.HasPendingPlayerPartyPositions, Is.False);
        }

        [Test]
        public void VictoryStandsUpAllDownedAlliesWithoutRevivingDeadCharacters()
        {
            var downed = new BattleUnit { IsPlayerSide = true, DownedTurnsRemaining = 2,
                Character = new CharacterInstance { CharacterId = "downed", LifeState = CharacterLifeState.Downed } };
            var second = new BattleUnit { IsPlayerSide = true, DownedTurnsRemaining = 1,
                Character = new CharacterInstance { CharacterId = "second", LifeState = CharacterLifeState.Downed } };
            var dead = new BattleUnit { IsPlayerSide = true,
                Character = new CharacterInstance { CharacterId = "dead", LifeState = CharacterLifeState.Dead } };
            var session = new CombatSession { Outcome = CombatOutcomeState.Ongoing,
                Units = new List<BattleUnit> { downed, second, dead } };

            new CombatOutcomeSystem().EvaluateCombatOutcome(session);

            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Victory));
            Assert.That(downed.Character.LifeState, Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(second.Character.LifeState, Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(downed.Character.CurrentHealth, Is.GreaterThan(0));
            Assert.That(downed.DownedTurnsRemaining, Is.Zero);
            Assert.That(second.DownedTurnsRemaining, Is.Zero);
            Assert.That(dead.Character.LifeState, Is.EqualTo(CharacterLifeState.Dead));
        }

        [Test]
        public void TryApplyOutcome_VictoryStoresPlayerBattlePositions()
        {
            var gameSession = CreateGameSessionWithDungeon(
                out var dungeon);
            var leader = new CharacterInstance
            {
                CharacterId = "character.leader"
            };
            var companion = new CharacterInstance
            {
                CharacterId = "character.companion"
            };

            Assert.That(gameSession.TryRegisterCharacter(leader), Is.True);
            Assert.That(gameSession.TryRegisterCharacter(companion), Is.True);
            Assert.That(
                gameSession.TryAddCharacterToActiveParty(leader.CharacterId),
                Is.True);
            Assert.That(
                gameSession.TryAddCharacterToActiveParty(companion.CharacterId),
                Is.True);

            var combatSession = new CombatSession
            {
                CombatId = "encounter.test",
                Outcome = CombatOutcomeState.Victory,
                Units = new List<BattleUnit>
                {
                    new BattleUnit
                    {
                        Character = leader,
                        IsPlayerSide = true,
                        Position = new GridPosition(2, 3)
                    },
                    new BattleUnit
                    {
                        Character = companion,
                        IsPlayerSide = true,
                        Position = new GridPosition(1, 3)
                    }
                }
            };

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combatSession),
                Is.True);

            Assert.That(
                dungeon.PendingPlayerPartyPositions[leader.CharacterId],
                Is.EqualTo(new Vector2(2.5f, 3.5f)));
            Assert.That(
                dungeon.PendingPlayerPartyPositions[companion.CharacterId],
                Is.EqualTo(new Vector2(1.5f, 3.5f)));
            Assert.That(dungeon.PlayerPosition, Is.EqualTo(new Vector2(2.5f, 3.5f)));
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
