using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonTrapAndConditionTests
    {
        [Test]
        public void TriggerSpikeTrap_TriggersOnlyItsActivationGroup()
        {
            var definition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();

            try
            {
                var floor = CreateFloor();
                var first = new SpikeTrapDungeonObjectInstance(
                    "trap.first",
                    definition,
                    new Vector2Int(1, 1),
                    "group.a");
                var chained = new SpikeTrapDungeonObjectInstance(
                    "trap.chained",
                    definition,
                    new Vector2Int(2, 1),
                    "group.a");
                var unrelated = new SpikeTrapDungeonObjectInstance(
                    "trap.unrelated",
                    definition,
                    new Vector2Int(3, 1),
                    "group.b");
                var sameGroupOtherRoom = new SpikeTrapDungeonObjectInstance(
                    "trap.other.room",
                    definition,
                    new Vector2Int(6, 1),
                    "group.a");

                Assert.That(floor.TryAddObject(first), Is.True);
                Assert.That(floor.TryAddObject(chained), Is.True);
                Assert.That(floor.TryAddObject(unrelated), Is.True);
                Assert.That(floor.TryAddObject(sameGroupOtherRoom), Is.True);

                Assert.That(
                    floor.TryTriggerSpikeTrap(
                        first.InstanceId,
                        out var triggered),
                    Is.True);

                Assert.That(triggered, Has.Count.EqualTo(2));
                Assert.That(first.IsTriggered, Is.True);
                Assert.That(chained.IsTriggered, Is.True);
                Assert.That(unrelated.IsTriggered, Is.False);
                Assert.That(sameGroupOtherRoom.IsTriggered, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RestoreObjectState_RestoresTrapLeverAndConditionState()
        {
            var trapDefinition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();
            var leverDefinition = ScriptableObject.CreateInstance<LeverDungeonObjectDefinition>();
            var conditionDefinition = ScriptableObject.CreateInstance<ConditionDungeonObjectDefinition>();

            try
            {
                var floor = CreateFloor();
                var trap = new SpikeTrapDungeonObjectInstance(
                    "trap",
                    trapDefinition,
                    new Vector2Int(1, 1));
                var lever = new LeverDungeonObjectInstance(
                    "lever",
                    leverDefinition,
                    new Vector2Int(2, 1));
                var condition = new ConditionDungeonObjectInstance(
                    "condition",
                    conditionDefinition,
                    new Vector2Int(3, 1));

                Assert.That(floor.TryAddObject(trap), Is.True);
                Assert.That(floor.TryAddObject(lever), Is.True);
                Assert.That(floor.TryAddObject(condition), Is.True);
                Assert.That(trap.TryTrigger(), Is.True);
                Assert.That(lever.TryToggle(out _), Is.True);
                Assert.That(condition.TrySatisfy(), Is.True);

                Assert.That(floor.TryRestoreObjectState(
                    trap.InstanceId, true, false, false, false, false, false), Is.True);
                Assert.That(floor.TryRestoreObjectState(
                    lever.InstanceId, true, false, false, false, false, false), Is.True);
                Assert.That(floor.TryRestoreObjectState(
                    condition.InstanceId, true, false, false, false, false, false), Is.True);

                Assert.That(trap.IsTriggered, Is.False);
                Assert.That(lever.IsActivated, Is.False);
                Assert.That(condition.IsSatisfied, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(trapDefinition);
                Object.DestroyImmediate(leverDefinition);
                Object.DestroyImmediate(conditionDefinition);
            }
        }

        [Test]
        public void ProgressionGateUnlocksOnlyAfterAllRoomSourcesAreSatisfied()
        {
            var connection = new DungeonRoomConnection("main", "branch");

            Assert.That(
                connection.TrySetProgressionGate(
                    "gate",
                    "condition",
                    "branch"),
                Is.True);
            Assert.That(
                connection.TrySetProgressionRequirementSources(
                    new[] { "lever.one", "lever.two" }),
                Is.True);

            Assert.That(
                connection.TrySatisfyProgressionCondition(
                    "condition",
                    "lever.one"),
                Is.False);
            Assert.That(connection.IsProgressionGateUnlocked, Is.False);

            Assert.That(
                connection.TrySatisfyProgressionCondition(
                    "condition",
                    "lever.two"),
                Is.True);
            Assert.That(connection.IsProgressionGateUnlocked, Is.True);
        }

        private static DungeonFloorInstance CreateFloor()
        {
            var floor = new DungeonFloorInstance(0);
            Assert.That(floor.TryAddRoom(new DungeonRoomInstance(
                "room",
                DungeonRoomType.Normal,
                "template",
                Vector2Int.zero)), Is.True);

            var map = new DungeonFloorMap();

            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 5; y++)
                {
                    Assert.That(
                        map.TryAddFloorCell(new Vector2Int(x, y), "room"),
                        Is.True);
                }
            }

            Assert.That(floor.TryAddRoom(new DungeonRoomInstance(
                "room.other",
                DungeonRoomType.Normal,
                "template",
                new Vector2Int(6, 0))), Is.True);

            for (var x = 6; x < 11; x++)
            {
                for (var y = 0; y < 5; y++)
                {
                    Assert.That(
                        map.TryAddFloorCell(new Vector2Int(x, y), "room.other"),
                        Is.True);
                }
            }

            Assert.That(floor.TrySetMap(map), Is.True);
            return floor;
        }
    }
}
