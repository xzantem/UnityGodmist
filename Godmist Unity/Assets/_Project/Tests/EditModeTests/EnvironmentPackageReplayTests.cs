using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EnvironmentPackageReplayTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/contact-shadow-cost-pass-20261018"));
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
        static T Bind<T>(object owner, string method) where T : Delegate => (T)owner.GetType().GetMethod(method, Flags).CreateDelegate(typeof(T), owner);
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }
        [Serializable] sealed class Frame { public string label; public Vector2 center; public bool debug; public int radius; }
        [Serializable] sealed class Trace { public int seed; public Frame[] frames; }
        [Serializable] sealed class Sample { public int index, traceFrame; public float time; public double patrol, fog, light, total, contact; public bool fogRefreshed; public long contactTicks; public long[] contactPhases, fogPhases, lightFogPhases; }
        [Serializable] sealed class Stat { public string name; public double mean, median, p95, max; }
        [Serializable] sealed class Report
        {
            public string stage, traceHash, initialState, finalState, contactHash, fogHash, maskHash, scope;
            public int seed, rooms, cells, warmupFrames, measuredFrames, gcCollections;
            public Sample[] samples; public Stat[] stats;
            public Stat[] fogPhases, lightFogPhases;
            public int fogIndexBuilds;
            public int fogElevationQueries, fogSpriteTargetWrites;
        }
        static string ActorState(DungeonEncounterSpawner encounters, DungeonFloorInstance floor)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            foreach (var actor in encounters.EnemyViews)
            {
                writer.Write(actor.RuntimeCharacter.Definition.Id); var world = actor.Position;
                writer.Write(world.x); writer.Write(world.y); writer.Write(actor.FacingDirection.x); writer.Write(actor.FacingDirection.y);
                var position = actor.DungeonPosition; floor.Map.TryGetTraversalCell(position, out var cell);
                writer.Write(cell.x); writer.Write(cell.y); writer.Write(position.IsOnStairs);
            }
            foreach (var state in floor.Patrols.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value))
            { writer.Write(state.NextPoint); writer.Write(state.WaitRemaining); writer.Write(state.Stops); writer.Write(state.ObservedCharacterId ?? ""); }
            return Hash(stream.ToArray());
        }
        static string MeshHash(Mesh mesh)
        {
            if (mesh == null) return "none";
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            foreach (var value in mesh.vertices) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
            foreach (var value in mesh.colors32) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }
            foreach (var value in mesh.triangles) writer.Write(value);
            return Hash(stream.ToArray());
        }
        static Stat Stats(string name, IEnumerable<double> values)
        {
            var ordered = values.OrderBy(v => v).ToArray();
            return new Stat { name = name, mean = ordered.Average(), median = ordered[(ordered.Length - 1) / 2], p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1], max = ordered[ordered.Length - 1] };
        }
        [Test]
        public void ReplaySameProductionSequence()
            => RunReplay(Output, false);

        [Test]
        public void ReplayDetailedFogSequence()
            => RunReplay(Path.GetFullPath(Path.Combine(Output, "../environment-regression-pass-20261018")), true);

        void RunReplay(string output, bool detailed)
        {
            string stage = File.ReadAllText(Path.Combine(output, "stage.txt")).Trim();
            string json = File.ReadAllText(Path.Combine(Output, "before-trace.json"));
            var trace = JsonUtility.FromJson<Trace>(json);
            var report = new Report { stage = stage, seed = trace.seed, traceHash = Hash(System.Text.Encoding.UTF8.GetBytes(json)), scope = "Deterministic production subsystem replay, fixed elapsed/time inputs, no Profiler. Timed buckets: patrol call, fog presentation/animation call, lighting call (including camera coverage). Sum is subsystem CPU, not a full rendered game frame. Input preparation, physics sync, hashes/GPU checks and dungeon entry excluded." };
            var probe = new DungeonEntryPerformanceTests { OutputOverride = output, StageOverride = "package-" + stage, SeedOverride = trace.seed };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var coordinator = Read<DungeonCombatCoordinator>(bootstrap, "combatCoordinator");
                var encounters = Read<DungeonEncounterSpawner>(bootstrap, "encounterSpawner");
                var floor = session.ActiveDungeon.Floors[0]; var camera = Read<Camera>(rig, "worldCamera");
                report.rooms = floor.Rooms.Count; report.cells = floor.Map.FloorCells.Count;
                var patrol = Bind<Action<float, float>>(coordinator, "UpdatePatrolsAtTime");
                var light = Bind<Action<float>>(rig, "RefreshLighting"); var fogTick = Bind<Action<float>>(map, "TickFogAnimation");
                light(0); // Create the normal lighting resources before warmup/timing.
                var contact = Read<object>(rig, "contactShadows");
                var contactType = contact.GetType(); contactType.GetProperty("MeasurePhaseTimings").SetValue(contact, true);
                var totalProperty = contactType.GetProperty("LastTotalTicks"); var phases = (long[])contactType.GetProperty("LastPhaseTicks").GetValue(contact);
                map.MeasureFogPhaseTimings = detailed;
                var fogPhases = map.FogPhaseTicks;
                int indexBuilds = map.FogGeometryIndexBuildCount;
                int elevationQueries = map.FogElevationQueryCount, spriteWrites = map.FogSpriteTargetWriteCount;
                report.warmupFrames = trace.frames.Length * 3; report.measuredFrames = trace.frames.Length * 12;
                var values = new List<Sample>(); var watch = new System.Diagnostics.Stopwatch(); int gc = 0;
                for (int frame = 0; frame < report.warmupFrames + report.measuredFrames; frame++)
                {
                    var input = trace.frames[frame % trace.frames.Length]; float time = (frame + 1) * .02f;
                    bool fogRefresh = frame % 5 == 0 || floor.Fog.DebugFogDisabled != input.debug;
                    if (floor.Fog.DebugFogDisabled != input.debug) floor.Fog.ToggleDebugFog();
                    if (fogRefresh && !input.debug)
                    {
                        var cell = map.WorldToCell(input.center);
                        floor.Fog.SetVisible(floor.Map.FloorCells.Where(c => Mathf.Abs(c.x - cell.x) <= input.radius && Mathf.Abs(c.y - cell.y) <= input.radius)
                            .OrderBy(c => c.y).ThenBy(c => c.x).Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray());
                    }
                    camera.orthographicSize = 9; camera.aspect = 16f / 9f; camera.transform.position = new Vector3(input.center.x, input.center.y, -10);
                    Physics2D.SyncTransforms(); Array.Clear(phases, 0, phases.Length); totalProperty.SetValue(contact, 0L);
                    bool measured = frame >= report.warmupFrames;
                    if (frame == report.warmupFrames) { report.initialState = ActorState(encounters, floor); gc = GC.CollectionCount(0); }
                    var sample = new Sample { index = frame, traceFrame = frame % trace.frames.Length, time = time, fogRefreshed = fogRefresh };
                    watch.Restart(); patrol(.02f, time); sample.patrol = watch.Elapsed.TotalMilliseconds;
                    if (detailed) Array.Clear(fogPhases, 0, fogPhases.Length);
                    watch.Restart(); if (fogRefresh) { map.ApplyFog(floor); fogTick(.2f); } sample.fog = watch.Elapsed.TotalMilliseconds;
                    if (detailed) { sample.fogPhases = (long[])fogPhases.Clone(); Array.Clear(fogPhases, 0, fogPhases.Length); }
                    watch.Restart(); light(time); sample.light = watch.Elapsed.TotalMilliseconds;
                    if (detailed) sample.lightFogPhases = (long[])fogPhases.Clone();
                    sample.total = sample.patrol + sample.fog + sample.light;
                    sample.contactTicks = (long)totalProperty.GetValue(contact); sample.contact = sample.contactTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    sample.contactPhases = (long[])phases.Clone();
                    if (measured) values.Add(sample);
                }
                report.gcCollections = GC.CollectionCount(0) - gc; report.finalState = ActorState(encounters, floor);
                report.contactHash = MeshHash(Read<Mesh>(contact, "mesh")); report.fogHash = MeshHash(Read<Mesh>(map, "fogBlendMesh"));
                report.maskHash = (string)typeof(DungeonLightingOptimizationTests).GetMethod("MaskHash", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { rig.ShadowMask, Math.Max(1, Read<ICollection>(rig, "activeTorches").Count) });
                report.samples = values.ToArray(); report.stats = new[] { Stats("Sum of timed buckets", values.Select(s => s.total)), Stats("Patrols", values.Select(s => s.patrol)),
                    Stats("Fog calls", values.Select(s => s.fog)), Stats("Lighting calls", values.Select(s => s.light)), Stats("Contact child", values.Select(s => s.contact)) };
                if (detailed)
                {
                    var names = new[] { "Setup/coverage", "Tiles", "Index/gates", "Walls", "Ground/decorations", "Soft fog", "Floor colour flush", "Animation tick",
                        "Restore targets (setup child)", "Geometry index build (index child)", "Retarget patterns (soft child)", "Mesh membership/build (soft/animation child)",
                        "Geometry upload (mesh child)", "Colour upload (mesh/animation child)", "Fog visibility setter" };
                    double Ms(long ticks) => ticks * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    report.fogPhases = names.Select((name, i) => Stats(name, values.Select(s => Ms(s.fogPhases[i])))).ToArray();
                    report.lightFogPhases = names.Select((name, i) => Stats(name, values.Select(s => Ms(s.lightFogPhases[i])))).ToArray();
                    report.fogIndexBuilds = map.FogGeometryIndexBuildCount - indexBuilds;
                    report.fogElevationQueries = map.FogElevationQueryCount - elevationQueries;
                    report.fogSpriteTargetWrites = map.FogSpriteTargetWriteCount - spriteWrites;
                }
                if (stage == "after")
                {
                    var previous = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(output, "before-package-report.json")));
                    Assert.That(report.traceHash, Is.EqualTo(previous.traceHash)); Assert.That(report.initialState, Is.EqualTo(previous.initialState));
                    Assert.That(report.finalState, Is.EqualTo(previous.finalState), "Patrol movement, waits, stops, facing and tracked targets must match.");
                    Assert.That(report.contactHash, Is.EqualTo(previous.contactHash)); Assert.That(report.fogHash, Is.EqualTo(previous.fogHash)); Assert.That(report.maskHash, Is.EqualTo(previous.maskHash));
                }
            };
            probe.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
            File.WriteAllText(Path.Combine(output, stage + "-package-report.json"), JsonUtility.ToJson(report, true));
        }
    }
}
