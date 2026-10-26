using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonBranchTopologyTests
    {
        const string Folder = "../LocalHandoffs/branch-counts-20261023";
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        [OneTimeSetUp] public void AuthorProfiles() => DungeonLengthSetup.ApplyAll();

        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        public void BranchesHaveAdditionalApproachRoomsAndRealAlternativeRoutes(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var timer = Stopwatch.StartNew();
            var dungeon = new DungeonGeneratorService().Generate(definition, 231023, 10, length);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            timer.Stop();
            Assert.That(dungeon.Floors.Sum(f => f.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional)),
                Is.InRange((int)length, (int)length + 2));
            foreach (var floor in dungeon.Floors) CheckTopology(dungeon, floor);
            new DungeonObjectPlacementSystem().Place(dungeon, Catalog.EquipmentPartDefinitions);
            Export(dungeon, "after", timer.ElapsedMilliseconds);
        }

        internal static void CheckTopology(DungeonInstance dungeon, DungeonFloorInstance floor)
        {
            var profile = dungeon.LengthProfile;
            var destinations = floor.Rooms.Where(r => r.RoomType == DungeonRoomType.Optional).ToArray();
            Assert.That(destinations.Length, Is.InRange(1, profile.MaxBranchCount));
            var budget = floor.Rooms.Count(r => !r.IsBranchTransitRoom);
            Assert.That(budget, Is.InRange(profile.MinRoomsPerFloor, profile.MaxRoomsPerFloor));
            Assert.That(floor.Encounters.Count, Is.InRange(profile.MinEncountersPerFloor, profile.MaxEncountersPerFloor));
            Assert.That(floor.Encounters.Count / (double)budget, Is.InRange(.3 - 1e-6, .4 + 1e-6));
            var allTransit = new HashSet<string>();
            var roots = new HashSet<string>();
            foreach (var destination in destinations)
            {
                var current = destination;
                string previous = null;
                var count = 0;
                while (true)
                {
                    var next = floor.Connections.Where(c => c.FirstRoomId == current.RoomId || c.SecondRoomId == current.RoomId)
                        .Select(c => c.FirstRoomId == current.RoomId ? c.SecondRoomId : c.FirstRoomId)
                        .Where(r => r != previous).ToArray();
                    Assert.That(next.Length, Is.EqualTo(1), "A branch must not get a shortcut to its destination.");
                    floor.TryGetRoom(next[0], out var room);
                    if (!room.IsBranchTransitRoom)
                    {
                        Assert.That(room.RoomType, Is.EqualTo(DungeonRoomType.Normal).Or.EqualTo(DungeonRoomType.Corridor));
                        Assert.That(roots.Add(room.RoomId), Is.True, "Branches should have distinct junctions.");
                        break;
                    }
                    Assert.That(allTransit.Add(room.RoomId), Is.True, "Independent branches cannot share approaches.");
                    count++;
                    previous = current.RoomId;
                    current = room;
                }
                Assert.That(count, Is.InRange(profile.MinBranchTransitRoomCount, profile.MaxBranchTransitRoomCount));
            }
            Assert.That(allTransit.Count, Is.EqualTo(floor.Rooms.Count(r => r.IsBranchTransitRoom)));
            Assert.That(floor.Connections.Count - floor.Rooms.Count + 1, Is.GreaterThanOrEqualTo(1));
            var start = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Start).RoomId;
            var final = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Final).RoomId;
            foreach (var gate in floor.Connections.Where(c => c.HasProgressionGate))
                Assert.That(Reachable(floor, start, gate).Contains(final), Is.False, "Alternative routes cannot bypass a gate.");
            foreach (var edge in floor.Connections)
            {
                var queue = new Queue<(string room, int distance)>();
                var visited = new HashSet<string> { edge.FirstRoomId };
                queue.Enqueue((edge.FirstRoomId, 0));
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.room == edge.SecondRoomId)
                    {
                        Assert.That(current.distance + 1,
                            Is.InRange(2 * profile.MinLoopArmRoomCount + 2, 2 * profile.MaxLoopArmRoomCount + 2),
                            "A realized cycle must obey the planned size policy, including its fork and join.");
                        break;
                    }
                    foreach (var candidate in floor.Connections)
                    {
                        if (candidate == edge) continue;
                        var next = candidate.FirstRoomId == current.room ? candidate.SecondRoomId
                            : candidate.SecondRoomId == current.room ? candidate.FirstRoomId : null;
                        if (next != null && visited.Add(next)) queue.Enqueue((next, current.distance + 1));
                    }
                }
            }
        }

        static HashSet<string> Reachable(DungeonFloorInstance floor, string start, DungeonRoomConnection skip)
        {
            var found = new HashSet<string> { start };
            var queue = new Queue<string>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var connection in floor.Connections)
                {
                    if (connection == skip) continue;
                    var next = connection.FirstRoomId == room ? connection.SecondRoomId
                        : connection.SecondRoomId == room ? connection.FirstRoomId : null;
                    if (next != null && found.Add(next)) queue.Enqueue(next);
                }
            }
            return found;
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void AdditionalSeedsRespectBranchLengthsWithoutDroppingAlternativeRoutes(string id)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            foreach (var length in new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
            for (var seed = 0; seed < 24; seed++)
            {
                var timer = Stopwatch.StartNew();
                var dungeon = new DungeonGeneratorService().Generate(definition, 231030 + seed, 10, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                timer.Stop();
                Assert.That(dungeon.Floors.Sum(f => f.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional)),
                    Is.InRange((int)length, (int)length + 2));
                foreach (var floor in dungeon.Floors) CheckTopology(dungeon, floor);
                TestContext.WriteLine($"{id} {length} seed {dungeon.Seed}: {timer.ElapsedMilliseconds} ms");
            }
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void TransitRolesGeometryAndCombatBudgetSurviveJsonRoundTrip(string id)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var dungeon = new DungeonGeneratorService().Generate(definition, 231024, 10, DungeonLength.Normal);
            var save = new DungeonSaveData { DungeonId = id, Seed = dungeon.Seed, DungeonLevel = 10,
                Length = DungeonLength.Normal, LayoutVersion = DungeonLayoutSnapshot.CurrentVersion,
                Layout = DungeonLayoutSnapshot.Capture(dungeon) };
            foreach (var floor in dungeon.Floors)
                save.FloorDeltas.Add(new DungeonFloorDeltaSaveData { FloorIndex = floor.FloorIndex,
                    ProgressionGateDeltas = floor.Connections.Where(c => c.HasProgressionGate)
                        .Select(c => new DungeonProgressionGateDeltaSaveData { GateId = c.ProgressionGateId }).ToList() });
            var json = JsonUtility.ToJson(save);
            Assert.That(DungeonLayoutSnapshot.TryRestore(JsonUtility.FromJson<DungeonSaveData>(json),
                definition, Catalog, out var restored), Is.True);
            Assert.That(JsonUtility.ToJson(new DungeonSaveData { Layout = DungeonLayoutSnapshot.Capture(restored) }),
                Is.EqualTo(JsonUtility.ToJson(new DungeonSaveData { Layout = DungeonLayoutSnapshot.Capture(dungeon) })));
            new DungeonEncounterPlacementSystem().Place(restored);
            foreach (var floor in restored.Floors) CheckTopology(restored, floor);
        }

        [Test]
        public void IrregularForestWithFourLongBranchesCanBacktrackWithoutChangingItsPlan()
        {
            var definition = UnityEngine.Object.Instantiate(Catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition"));
            // Regression: all 32 greedy floor attempts ran out of placement space.
            // Pin the original branch budget to preserve that exact four-branch
            // plan when current production profiles change.
            try
            {
                var profile = definition.GetLengthProfile(DungeonLength.Long);
                typeof(DungeonLengthProfile).GetField("<MinBranchCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, 2);
                typeof(DungeonLengthProfile).GetField("<MaxBranchCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, 4);
                var dungeon = new DungeonGeneratorService().Generate(definition, 231035, 10, DungeonLength.Long);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var floor = dungeon.Floors.Single();
                Assert.That(floor.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional), Is.EqualTo(4));
                CheckTopology(dungeon, floor);
                Export(dungeon, "four-branch-regression", 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void FixedSeedReproducesTheCompleteNewTopology(string id)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            string Capture() => JsonUtility.ToJson(new DungeonSaveData { Layout = DungeonLayoutSnapshot.Capture(
                new DungeonGeneratorService().Generate(definition, -231023, 10, DungeonLength.Normal)) });
            Assert.That(Capture(), Is.EqualTo(Capture()));
        }

        [TestCase(3, DungeonRoomType.Optional, true, false)]
        [TestCase(4, DungeonRoomType.Optional, false, false)]
        [TestCase(4, DungeonRoomType.Normal, true, true)]
        public void SnapshotRoleValidationAcceptsLegacyAndRejectsMalformedTransitDestinations(
            int version, DungeonRoomType type, bool valid, bool transit)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition");
            var dungeon = new DungeonInstance(definition, 1, 10);
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("room", type));
            var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "room");
            floor.TrySetMap(map); dungeon.TryAddFloor(floor);
            var save = new DungeonSaveData { DungeonId = definition.Id, DungeonLevel = 10, LayoutVersion = version,
                Layout = DungeonLayoutSnapshot.Capture(dungeon), FloorDeltas = new List<DungeonFloorDeltaSaveData> { new() { FloorIndex = 0 } } };
            save.Layout[0].Rooms[0].BranchTransit = true;
            Assert.That(DungeonLayoutSnapshot.TryRestore(JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(save)),
                definition, Catalog, out var restored), Is.EqualTo(valid));
            if (valid) Assert.That(restored.Floors[0].Rooms[0].IsBranchTransitRoom, Is.EqualTo(transit));
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void ExportBaselineWithLegacyBranchPolicy(string id)
        {
            var definition = UnityEngine.Object.Instantiate(Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id));
            try
            {
                foreach (var profile in definition.LengthProfiles)
                    foreach (var name in new[] { "MinBranchTransitRoomCount", "MaxBranchTransitRoomCount" })
                        typeof(DungeonLengthProfile).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(profile, 0);
                Export(new DungeonGeneratorService().Generate(definition, 231023, 10, DungeonLength.Normal), "before", 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        internal static void Export(DungeonInstance dungeon, string label, long milliseconds)
        {
            Directory.CreateDirectory(Folder);
            var save = new DungeonSaveData { DungeonId = dungeon.DungeonId, Seed = dungeon.Seed,
                Length = dungeon.Length, Layout = DungeonLayoutSnapshot.Capture(dungeon) };
            File.WriteAllText(Path.Combine(Folder, $"{dungeon.DungeonId}-{dungeon.Length}-{label}.json"), JsonUtility.ToJson(save));
            TestContext.WriteLine($"{dungeon.DungeonId} {dungeon.Length}: {dungeon.Floors.Sum(f => f.Rooms.Count)} rooms; {milliseconds} ms.");
        }
    }
}
