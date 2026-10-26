using System;
using System.Collections.Generic;
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
    public sealed class DungeonRoutePlannerTests
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
        public void LengthBranchBudgetIsExpeditionWideAndCanReachBothBounds(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var profile = definition.GetLengthProfile(length);
            Assert.That(profile.MinBranchCount, Is.EqualTo((int)length));
            Assert.That(profile.MaxBranchCount, Is.EqualTo((int)length + 2));
            var totals = new HashSet<int>();
            for (var seed = 0; seed < 64; seed++)
            {
                var plans = DungeonRoutePlanner.CreateExpedition(definition, profile, new System.Random(seed));
                var total = plans.Sum(p => p.Branches.Count);
                totals.Add(total);
                Assert.That(total, Is.InRange((int)length, (int)length + 2));
                foreach (var plan in plans)
                {
                    Assert.That(plan.Branches.Count, Is.GreaterThanOrEqualTo(1));
                    Assert.That(plan.Edges.Count(e => e.GateIndex >= 0), Is.InRange(1, plan.Branches.Count));
                    Assert.DoesNotThrow(() => DungeonRoutePlanner.Validate(plan));
                }
            }
            Assert.That(totals, Does.Contain((int)length));
            Assert.That(totals, Does.Contain((int)length + 2));
        }

        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        public void PlansReserveBothLoopArmsBeforeAnyGeometry(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var profile = definition.GetLengthProfile(length);
            var unequalCount = 0;
            var loopCount = 0;
            for (var seed = 0; seed < 64; seed++)
            {
                var plan = DungeonRoutePlanner.Create(definition, profile, 0, 1 + seed % 4, new System.Random(seed));
                Assert.That(plan.BaseRoomCount, Is.InRange(profile.MinRoomsPerFloor, profile.MaxRoomsPerFloor));
                Assert.That(plan.Loops.Count, Is.GreaterThanOrEqualTo(1));
                Assert.That(plan.Nodes.Count(n => !n.Transit), Is.EqualTo(plan.BaseRoomCount));
                foreach (var loop in plan.Loops)
                {
                    loopCount++;
                    if (loop.FirstArm.Count != loop.SecondArm.Count) unequalCount++;
                    Assert.That(loop.FirstArm.Count, Is.InRange(profile.MinLoopArmRoomCount, profile.MaxLoopArmRoomCount));
                    Assert.That(loop.SecondArm.Count, Is.InRange(profile.MinLoopArmRoomCount, profile.MaxLoopArmRoomCount));
                    Assert.That((loop.FirstArm.Count + loop.SecondArm.Count) % 2, Is.Zero);
                    Assert.That(loop.FirstArm.Intersect(loop.SecondArm), Is.Empty);
                    foreach (var arm in new[] { loop.FirstArm, loop.SecondArm })
                    {
                        var path = new[] { loop.Fork }.Concat(arm).Concat(new[] { loop.Join }).ToArray();
                        for (var i = 1; i < path.Length; i++)
                            Assert.That(plan.Edges.Count(e => (e.First == path[i - 1] && e.Second == path[i])
                                || (e.Second == path[i - 1] && e.First == path[i])), Is.EqualTo(1));
                    }
                }
                foreach (var branch in plan.Branches)
                    Assert.That(branch.Rooms.Count - 1, Is.InRange(profile.MinBranchTransitRoomCount, profile.MaxBranchTransitRoomCount));
                Assert.That(plan.Edges.Count - plan.Nodes.Count + 1, Is.EqualTo(plan.Loops.Count));
                Assert.DoesNotThrow(() => DungeonRoutePlanner.Validate(plan));
            }
            Assert.That(unequalCount, Is.GreaterThan(loopCount / 2), "Unequal arms must be the normal outcome, not a rare exception.");
        }

        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(14)]
        [TestCase(16)]
        public void ClosedWalksPreserveCountAndDoNotCollapseToRectangles(int count)
        {
            var method = typeof(DungeonGeneratorService).GetMethod("CreateLoopShape", BindingFlags.Static | BindingFlags.NonPublic);
            var shapes = new HashSet<string>();
            for (var seed = 0; seed < 64; seed++)
            {
                List<Vector2Int> Shape() => (List<Vector2Int>)method.Invoke(null, new object[] { count, new System.Random(seed) });
                var points = Shape();
                CollectionAssert.AreEqual(points, Shape(), "The same seed must reproduce shape selection.");
                Assert.That(points.Distinct().Count(), Is.EqualTo(count), "Cycle rooms must never overlap.");
                var turns = 0;
                var twiceArea = 0;
                for (var i = 0; i < count; i++)
                {
                    var before = points[i] - points[(i + count - 1) % count];
                    var after = points[(i + 1) % count] - points[i];
                    Assert.That(Math.Abs(after.x) + Math.Abs(after.y), Is.EqualTo(1), "Every edge, including closure, must be a direct passage.");
                    if (before != after) turns++;
                    var next = points[(i + 1) % count];
                    twiceArea += points[i].x * next.y - points[i].y * next.x;
                }
                Assert.That(turns, Is.GreaterThanOrEqualTo(count == 6 ? 4 : count >= 12 ? 8 : 6));
                var half = count / 2;
                var minimumRectangleArea = count == 6 ? 2 : Enumerable.Range(2, half - 3)
                    .Where(w => Math.Max(w, half - w) <= 2 * Math.Min(w, half - w)).Min(w => w * (half - w));
                Assert.That(twiceArea / 2, Is.GreaterThanOrEqualTo(minimumRectangleArea - Math.Max(1, count / 6)),
                    "Corner changes must retain space between the arms.");
                shapes.Add(string.Join(";", points));
            }
            if (count >= 8) Assert.That(shapes.Count, Is.GreaterThan(1));
        }

        static void AssertIrregularGeometry(DungeonFloorInstance floor, DungeonFloorRoutePlan plan)
        {
            foreach (var loop in plan.Loops)
            {
                var ids = new[] { loop.Fork }.Concat(loop.FirstArm).Concat(new[] { loop.Join }).Concat(loop.SecondArm.Reverse()).ToArray();
                var steps = Enumerable.Range(0, ids.Length).Select(i =>
                {
                    var edge = floor.Connections.Single(e => e.Connects(ids[i], ids[(i + 1) % ids.Length]));
                    return (edge.SecondDoorCell - edge.FirstDoorCell) * (edge.FirstRoomId == ids[i] ? 1 : -1);
                }).ToArray();
                var turns = 0;
                for (var i = 0; i < steps.Length; i++)
                {
                    var before = steps[(i + steps.Length - 1) % steps.Length];
                    var after = steps[i];
                    Assert.That(after.x == 0 ^ after.y == 0, Is.True, "Realized edges must remain orthogonal.");
                    if (before.x * after.y != before.y * after.x) turns++;
                }
                Assert.That(turns, Is.GreaterThanOrEqualTo(steps.Length == 6 ? 4 : steps.Length >= 12 ? 8 : 6),
                    "Production placement must retain irregular cycle geometry.");
            }
        }

        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        public void GeometryRealizesTheExactSamePlanAcrossPlacementRetries(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var plan = DungeonRoutePlanner.Create(definition, definition.GetLengthProfile(length), 0, 2, new System.Random(231025));
            var lookupType = typeof(DungeonGeneratorService).GetNestedType("PassageLookup", BindingFlags.NonPublic);
            var embed = typeof(DungeonGeneratorService).GetMethod("EmbedRoutePlan", BindingFlags.Static | BindingFlags.NonPublic);
            static string Edge(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
            for (var variant = 0; variant < 2; variant++)
            {
                var lookup = Activator.CreateInstance(lookupType, true);
                var random = new System.Random(variant + 231025);
                DungeonFloorInstance floor = null; var failures = new List<string>();
                for (var attempt = 0; attempt < 32 && floor == null; attempt++)
                    try { floor = (DungeonFloorInstance)embed.Invoke(null, new[] { definition, (object)plan, random, lookup }); }
                    catch (TargetInvocationException error) { failures.Add(error.InnerException.Message); }
                Assert.That(floor, Is.Not.Null, string.Join("; ", failures.Distinct()));
                AssertIrregularGeometry(floor, plan);
                Assert.That(new HashSet<string>(floor.Rooms.Select(r => r.RoomId)).SetEquals(plan.Nodes.Select(n => n.Id)), Is.True);
                foreach (var node in plan.Nodes)
                {
                    floor.TryGetRoom(node.Id, out var room);
                    Assert.That(room.IsBranchTransitRoom, Is.EqualTo(node.Transit));
                    Assert.That(room.IsRequiredProgressionRoom, Is.EqualTo(node.Required));
                    Assert.That(room.RoomType == node.Type || (node.IsLoopRoom && room.RoomType == DungeonRoomType.Corridor), Is.True);
                }
                Assert.That(new HashSet<string>(floor.Connections.Select(e => Edge(e.FirstRoomId, e.SecondRoomId)))
                    .SetEquals(plan.Edges.Select(e => Edge(e.First, e.Second))), Is.True);
                foreach (var gate in plan.Edges.Where(e => e.GateIndex >= 0))
                    Assert.That(floor.Connections.Single(e => e.Connects(gate.First, gate.Second)).RequiredBranchRoomId, Is.EqualTo(gate.ControlRoomId));
                var dungeon = new DungeonInstance(definition, 231025, 10, length); dungeon.TryAddFloor(floor);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, $"{id}-{length}-variant-{variant}.json"), JsonUtility.ToJson(
                    new DungeonSaveData { DungeonId = id, Length = length, Seed = 231025, Layout = DungeonLayoutSnapshot.Capture(dungeon) }));
            }
        }

        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        public void EveryFloorRealizesTheWholeExpeditionPlanMadeBeforePlacement(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            var profile = definition.GetLengthProfile(length);
            static string Edge(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
            for (var seed = 231040; seed < 231042; seed++)
            {
                var topologyRandom = new System.Random(seed);
                topologyRandom.Next(profile.FloorCount, profile.FloorCount + 1);
                var plans = DungeonRoutePlanner.CreateExpedition(definition, profile, topologyRandom);
                Assert.That(plans.Sum(p => p.Branches.Count), Is.InRange((int)length, (int)length + 2));
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                foreach (var floor in dungeon.Floors)
                {
                    var plan = plans[floor.FloorIndex];
                    AssertIrregularGeometry(floor, plan);
                    Assert.That(new HashSet<string>(floor.Connections.Select(e => Edge(e.FirstRoomId, e.SecondRoomId)))
                        .SetEquals(plan.Edges.Select(e => Edge(e.First, e.Second))), Is.True,
                        "Geometry of one floor must not reroll topology on any later floor.");
                    Assert.That(new HashSet<string>(floor.Rooms.Select(r => r.RoomId)).SetEquals(plan.Nodes.Select(n => n.Id)), Is.True);
                    foreach (var node in plan.Nodes)
                    {
                        floor.TryGetRoom(node.Id, out var room);
                        Assert.That(room.IsRequiredProgressionRoom, Is.EqualTo(node.Required));
                        Assert.That(room.IsBranchTransitRoom, Is.EqualTo(node.Transit));
                    }
                }
            }
        }
    }
}
