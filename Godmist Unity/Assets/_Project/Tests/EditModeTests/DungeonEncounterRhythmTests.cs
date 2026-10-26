using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonEncounterRhythmTests
    {
        private static string Output => Path.GetFullPath("../LocalHandoffs/encounter-rhythm-20261023");

        [Test, Explicit("Capture the original placement before changing the selector.")]
        public void CaptureBefore() => Capture("before");

        [Test, Explicit("Paired placement audit on unchanged generated room graphs.")]
        public void CaptureAfter() => Capture("after");

        [Test]
        public void LinearFloorBreaksLongEmptyRunsWithoutIncreasingBudgetOrMovingExit()
        {
            var floor = Linear(20); var candidates = floor.Rooms.Skip(1).Reverse().ToList();
            DungeonEncounterRoomSelector.Prioritize(floor, candidates, 5, false);
            var selected = candidates.Take(5).Select(r => int.Parse(r.RoomId)).ToHashSet();
            Assert.That(selected, Has.Count.EqualTo(5)); Assert.That(selected.Contains(19), Is.True);
            int run = 0, longest = 0;
            for (int i = 1; i < 20; i++) { run = selected.Contains(i) ? 0 : run + 1; longest = Math.Max(longest, run); }
            Assert.That(longest, Is.LessThanOrEqualTo(4)); Assert.That(selected.Contains(18), Is.False);
        }

        [Test]
        public void UnequalLoopArmsAndLongBranchReceiveInteriorFightsAndKeepGate()
        {
            var floor = new DungeonFloorInstance(0);
            for (int i = 0; i < 26; i++) floor.TryAddRoom(new DungeonRoomInstance(i.ToString(),
                i == 0 ? DungeonRoomType.Start : i == 17 ? DungeonRoomType.Final : DungeonRoomType.Normal,
                isBranchTransitRoom: i >= 18));
            void Edge(int a, int b) => floor.TryAddConnection(a.ToString(), b.ToString());
            void Path(params int[] nodes) { for (int i = 1; i < nodes.Length; i++) Edge(nodes[i - 1], nodes[i]); }
            Path(0,1,2,3,4,5,6,7,16,17);Path(1,8,9,10,11,12,13,14,15,16);Path(4,18,19,20,21,22,23,24,25);
            var gate = floor.Connections.Single(c => c.FirstRoomId == "16" && c.SecondRoomId == "17");
            Assert.That(gate.TrySetProgressionGate("gate", "condition", "25"), Is.True);
            var edges = floor.Connections.Select(c => (c.FirstRoomId,c.SecondRoomId,c.HasProgressionGate,c.RequiredBranchRoomId)).ToArray();
            var candidates = floor.Rooms.Skip(1).OrderByDescending(r => int.Parse(r.RoomId)).ToList();
            candidates.Remove(floor.Rooms[17]);candidates.Insert(0,floor.Rooms[17]);
            DungeonEncounterRoomSelector.Prioritize(floor,candidates,5,false);
            var chosen=candidates.Take(5).Select(r=>int.Parse(r.RoomId)).ToArray();
            Assert.That(chosen.Contains(17),Is.True);Assert.That(chosen.Any(i=>i>=2&&i<=7),Is.True);
            Assert.That(chosen.Any(i=>i>=8&&i<=15),Is.True);Assert.That(chosen.Any(i=>i>=18&&i<=24),Is.True);
            Assert.That(floor.Connections.Select(c=>(c.FirstRoomId,c.SecondRoomId,c.HasProgressionGate,c.RequiredBranchRoomId)),Is.EqualTo(edges));
        }

        [Test]
        public void BossIsAlreadyACoveredFinalRoomAndNeverConsumesAnotherRegularSlot()
        {
            var floor=Linear(20);var candidates=floor.Rooms.Skip(1).Take(18).Reverse().ToList();
            DungeonEncounterRoomSelector.Prioritize(floor,candidates,4,true);
            Assert.That(candidates.Take(4).Select(r=>r.RoomId).Distinct().Count(),Is.EqualTo(4));
            Assert.That(candidates.Take(4).Any(r=>r.RoomType==DungeonRoomType.Final||r.RoomId=="18"),Is.False);
        }

        [Test]
        public void SeededCandidateTiesRemainReproducibleAndVaried()
        {
            var floor=Linear(20);var signatures=new HashSet<string>();
            string Choose(int seed)
            {
                var candidates=floor.Rooms.Skip(1).Take(18).ToList();var random=new System.Random(seed);
                for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);(candidates[i],candidates[j])=(candidates[j],candidates[i]);}
                candidates.Insert(0,floor.Rooms[19]);DungeonEncounterRoomSelector.Prioritize(floor,candidates,5,false);
                return string.Join(",",candidates.Take(5).Select(r=>int.Parse(r.RoomId)).OrderBy(i=>i));
            }
            for(int seed=0;seed<12;seed++){var first=Choose(seed);Assert.That(Choose(seed),Is.EqualTo(first));signatures.Add(first);}
            Assert.That(signatures.Count,Is.GreaterThan(1));
        }

        [Test]
        public void SparseEligibilityAndNoGraphRemainBoundedWithoutInventingRoomsOrFights()
        {
            var floor=Linear(30);var candidates=new List<DungeonRoomInstance>{floor.Rooms[29],floor.Rooms[1],floor.Rooms[2],floor.Rooms[3]};
            var original=candidates.ToArray();DungeonEncounterRoomSelector.Prioritize(floor,candidates,2,false);
            Assert.That(candidates,Is.EquivalentTo(original));Assert.That(candidates.Take(2).Contains(floor.Rooms[29]),Is.True);
            var empty=new DungeonFloorInstance(0);empty.TryAddRoom(new DungeonRoomInstance("a",DungeonRoomType.Normal));empty.TryAddRoom(new DungeonRoomInstance("b",DungeonRoomType.Normal));
            var disconnected=empty.Rooms.ToList();DungeonEncounterRoomSelector.Prioritize(empty,disconnected,1,false);
            Assert.That(disconnected.Select(r=>r.RoomId),Is.EqualTo(new[]{"a","b"}));
        }

        [Test]
        public void LargeFloorSelectionHasBoundedWorkAndNoRuntimeTick()
        {
            var floor=Linear(90);var times=new List<double>();
            for(int i=0;i<12;i++)
            {
                var candidates=floor.Rooms.Skip(1).Reverse().ToList();var timer=System.Diagnostics.Stopwatch.StartNew();
                DungeonEncounterRoomSelector.Prioritize(floor,candidates,17,false);timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds);
                Assert.That(candidates.Take(17).Select(r=>r.RoomId).Distinct().Count(),Is.EqualTo(17));
            }
            times.Sort();Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"selector-timings.txt"),$"90 rooms / 17 fights / 12 runs; median={times[6]:F3} ms; max={times.Last():F3} ms (EditMode)");
            TestContext.WriteLine(File.ReadAllText(Path.Combine(Output,"selector-timings.txt")));
        }

        [Test]
        public void ClosedCycleWithoutAJunctionStillSpreadsFightsAroundBothArms()
        {
            var floor=new DungeonFloorInstance(0);
            for(int i=0;i<12;i++)floor.TryAddRoom(new DungeonRoomInstance(i.ToString(),DungeonRoomType.Normal));
            for(int i=0;i<12;i++)floor.TryAddConnection(i.ToString(),((i+1)%12).ToString());
            var candidates=floor.Rooms.Reverse().ToList();DungeonEncounterRoomSelector.Prioritize(floor,candidates,2,false);
            int distance=Math.Abs(int.Parse(candidates[0].RoomId)-int.Parse(candidates[1].RoomId));
            Assert.That(Math.Min(distance,12-distance),Is.EqualTo(6));
        }

        [TestCase("dungeon.catacombs.expedition")]
        [TestCase("dungeon.forest.expedition")]
        public void FullSavedEncounterLocationsAreNotRedistributedOnRestore(string id)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition=catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id==id);
            var dungeon=new DungeonGeneratorService().Generate(definition,231024,10,DungeonLength.Normal);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var save=new DungeonSaveData{DungeonId=id,Seed=dungeon.Seed,DungeonLevel=10,Length=DungeonLength.Normal,
                LayoutVersion=DungeonLayoutSnapshot.CurrentVersion,Layout=DungeonLayoutSnapshot.Capture(dungeon)};
            foreach(var floor in dungeon.Floors)
            {
                save.FloorDeltas.Add(new DungeonFloorDeltaSaveData{FloorIndex=floor.FloorIndex,
                    ProgressionGateDeltas=floor.Connections.Where(c=>c.HasProgressionGate)
                        .Select(c=>new DungeonProgressionGateDeltaSaveData{GateId=c.ProgressionGateId}).ToList()});
                foreach(var encounter in floor.Encounters)
                    save.EncounterDeltas.Add(new DungeonEncounterDeltaSaveData{EncounterInstanceId=encounter.InstanceId});
            }
            var original=JsonUtility.ToJson(save);
            Assert.That(DungeonLayoutSnapshot.TryRestore(JsonUtility.FromJson<DungeonSaveData>(original),definition,catalog,out var restored),Is.True);
            new DungeonEncounterPlacementSystem().Place(restored);
            save.Layout=DungeonLayoutSnapshot.Capture(restored);
            Assert.That(JsonUtility.ToJson(save),Is.EqualTo(original),"An existing map retains every encounter and its exact stored location.");
        }

        private static DungeonFloorInstance Linear(int size)
        {
            var floor=new DungeonFloorInstance(0);
            for(int i=0;i<size;i++)
            {floor.TryAddRoom(new DungeonRoomInstance(i.ToString(),i==0?DungeonRoomType.Start:i==size-1?DungeonRoomType.Final:DungeonRoomType.Normal));if(i>0)floor.TryAddConnection((i-1).ToString(),i.ToString());}
            return floor;
        }

        private static void Capture(string stage)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var directory = Path.Combine(Output, stage); Directory.CreateDirectory(directory);
            var timings = new List<double>();
            foreach (var theme in new[] { "forest", "catacombs" })
            foreach (var length in new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
            foreach (var seed in Enumerable.Range(231023, 8).Concat(theme == "catacombs" && length == DungeonLength.Normal ? new[] { 1000955580, 1513482231 } : Array.Empty<int>()))
            {
                var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + theme + ".expedition");
                var file = Path.Combine(directory, $"{theme}-{length}-{seed}.json");
                Assert.That(stage != "before" || !File.Exists(file), Is.True, "Never overwrite the original baseline.");
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                new DungeonEncounterPlacementSystem().Place(dungeon); timer.Stop(); timings.Add(timer.Elapsed.TotalMilliseconds);
                var snapshot = new DungeonSaveData { DungeonId = definition.Id, Seed = seed,
                    DungeonLevel = 10, Length = length, LayoutVersion = DungeonLayoutSnapshot.CurrentVersion, Layout = DungeonLayoutSnapshot.Capture(dungeon) };
                File.WriteAllText(file, JsonUtility.ToJson(snapshot));
                if(stage=="after")
                {
                    var before=JsonUtility.FromJson<DungeonSaveData>(File.ReadAllText(Path.Combine(Output,"before",Path.GetFileName(file))));
                    for(int i=0;i<snapshot.Layout.Count;i++)
                    {
                        Assert.That(snapshot.Layout[i].Encounters.Select(e=>e.DefinitionId),Is.EqualTo(before.Layout[i].Encounters.Select(e=>e.DefinitionId)),"Same budget and enemy roster sequence on every floor.");
                        before.Layout[i].Encounters.Clear();snapshot.Layout[i].Encounters.Clear();
                    }
                    Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(JsonUtility.ToJson(before)),"Placement must not change any generated room, connector, gate, terrain or object.");
                }
            }
            timings.Sort();
            var text = $"expeditions={timings.Count}; placement median={timings[timings.Count / 2]:F3} ms; max={timings.Last():F3} ms (EditMode, not frame time)";
            File.WriteAllText(Path.Combine(Output, stage + "-timings.txt"), text); TestContext.WriteLine(text);
        }
    }
}
