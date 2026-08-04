using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonInstanceTests
    {
        [Test]
        public void Constructor_StoresDungeonIdAndSeed()
        {
            var dungeon = new DungeonInstance("dungeon.test.catacombs", 12345);

            Assert.That(dungeon.DungeonId, Is.EqualTo("dungeon.test.catacombs"));
            Assert.That(dungeon.Seed, Is.EqualTo(12345));
            Assert.That(dungeon.CurrentFloorIndex, Is.EqualTo(0));
            Assert.That(dungeon.IsCompleted, Is.False);
        }

        [Test]
        public void TrySetCurrentFloorIndex_RejectsNegativeIndex()
        {
            var dungeon = new DungeonInstance("dungeon.test", 1);

            var changed = dungeon.TrySetCurrentFloorIndex(-1);

            Assert.That(changed, Is.False);
            Assert.That(dungeon.CurrentFloorIndex, Is.EqualTo(0));
        }

        [Test]
        public void TryMarkCompleted_CanOnlyCompleteOnce()
        {
            var dungeon = new DungeonInstance("dungeon.test", 1);

            Assert.That(dungeon.TryMarkCompleted(), Is.True);
            Assert.That(dungeon.TryMarkCompleted(), Is.False);
            Assert.That(dungeon.IsCompleted, Is.True);
        }

        [Test]
        public void GameSession_StoresAndClearsActiveDungeon()
        {
            var session = new GameSession(GameModeState.Exploration);
            var dungeon = new DungeonInstance("dungeon.test", 1);

            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            Assert.That(session.ActiveDungeon, Is.SameAs(dungeon));

            Assert.That(session.TryClearActiveDungeon(), Is.True);
            Assert.That(session.ActiveDungeon, Is.Null);
        }

        [Test]
        public void DungeonState_StoresPlayerPositionAndClearedEncounter()
        {
            var dungeon = new DungeonInstance("dungeon.test", 1);
            var playerPosition = new Vector2(4.5f, -2f);

            dungeon.SetPlayerPosition(playerPosition);

            Assert.That(dungeon.HasPlayerPosition, Is.True);
            Assert.That(dungeon.PlayerPosition, Is.EqualTo(playerPosition));

            Assert.That(
                dungeon.IsEncounterCleared("encounter.test"),
                Is.False);

            Assert.That(
                dungeon.TryMarkEncounterCleared("encounter.test"),
                Is.True);

            Assert.That(
                dungeon.IsEncounterCleared("encounter.test"),
                Is.True);
        }
    }
}