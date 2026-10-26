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
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ContactShadowCostTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/contact-shadow-cost-pass-20261018"));
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }

        sealed class Subject : IDisposable
        {
            public readonly object Owner;
            public readonly Action<DungeonFloorMapView, Renderer, Rect, IEnumerable<DungeonObjectView>, IEnumerable<SpriteRenderer>, Sprite, IEnumerable<SpriteRenderer>> Rebuild;
            readonly Action dispose;
            readonly long[] ticks;
            readonly PropertyInfo total;
            public Subject(Type type, Transform parent)
            {
                Owner = Activator.CreateInstance(type, new object[] { parent, Shader.Find("Godmist/Dungeon/Contact Shadows") });
                Rebuild = (Action<DungeonFloorMapView, Renderer, Rect, IEnumerable<DungeonObjectView>, IEnumerable<SpriteRenderer>, Sprite, IEnumerable<SpriteRenderer>>)type.GetMethod("Rebuild").CreateDelegate(typeof(Action<DungeonFloorMapView, Renderer, Rect, IEnumerable<DungeonObjectView>, IEnumerable<SpriteRenderer>, Sprite, IEnumerable<SpriteRenderer>>), Owner);
                dispose = (Action)type.GetMethod("Dispose").CreateDelegate(typeof(Action), Owner);
                type.GetProperty("MeasurePhaseTimings").SetValue(Owner, true);
                ticks = (long[])type.GetProperty("LastPhaseTicks").GetValue(Owner); total = type.GetProperty("LastTotalTicks");
                Read<MeshRenderer>(Owner, "renderer").enabled = false; // Test overlays never double-darken the scene.
            }
            public void SaveTiming(Sample sample, bool reference)
            {
                var values = reference ? sample.before : sample.after;
                values[0] = (long)total.GetValue(Owner) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                for (int i = 0; i < ticks.Length; i++) values[i + 1] = ticks[i] * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }
            public void Dispose() => dispose();
        }

        [Serializable] sealed class Frame { public string label; public Vector2 center; public bool debug; public int radius; }
        [Serializable] sealed class Trace { public int seed = 181026; public Frame[] frames; }
        [Serializable] sealed class Sample { public int round, frame; public bool referenceFirst; public int beforeGc, afterGc; public double[] before = new double[9], after = new double[9]; }
        [Serializable] sealed class Distribution { public double mean, median, p95, max; }
        [Serializable] sealed class Phase { public string name; public Distribution before, after; }
        [Serializable] sealed class Report
        {
            public string stage, traceHash, frozenSourceHash, scope;
            public int seed = 181026, rooms, cells, objectViews, warmupRounds = 3, measuredRounds = 24, pairedCalls, geometryComparisons, pixelComparisons;
            public bool nonBlankPixels, profilerDisabled = true;
            public Phase[] phases;
            public Sample[] samples;
            public int[] bandsPerFrame, verticesPerFrame;
            public int nativeCornerProjections, unitCornerProjections;
            public int[] projectedRects, defaultDistinctHashes, mixedDistinctHashes;
        }
        static Distribution Stats(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(v => v).ToArray();
            return new Distribution { mean = ordered.Average(), median = ordered[(ordered.Length - 1) / 2], p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1], max = ordered[ordered.Length - 1] };
        }

        [Test]
        public void CompareProductionForestReplay()
        {
            var stage = File.ReadAllText(Path.Combine(Output, "stage.txt")).Trim();
            var report = new Report { stage = stage, scope = "Paired contact Rebuild CPU only, same production forest/objects/camera/fog; alternating A/B order, warmed caches, no Profiler. GPU checks are outside timings. Not a whole-frame FPS benchmark or reconstruction of the historic 11.70 ms frame." };
            report.frozenSourceHash = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, "_Project/Tests/EditModeTests/ContactShadowCostBaseline.cs")));
            var probe = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "contact-" + stage, SeedOverride = report.seed };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var objects = Read<DungeonObjectSpawner>(bootstrap, "objectSpawner");
                var camera = Read<Camera>(rig, "worldCamera");
                var floor = session.ActiveDungeon.Floors[0];
                Assert.That(map.UsesVegetationPresentation, Is.True);
                report.rooms = floor.Rooms.Count; report.cells = floor.Map.FloorCells.Count; report.objectViews = objects.SpawnedObjects.Count;
                var frames = new List<Frame>();
                var rooms = floor.Rooms.ToArray();
                foreach (int index in new[] { 0, rooms.Length / 3, rooms.Length * 2 / 3, rooms.Length - 1 })
                {
                    var center = map.CellToWorldCenter(rooms[index].LayoutPosition + Vector2Int.one * 2);
                    frames.Add(new Frame { label = "revealed/pan/" + index, center = center, debug = true });
                    frames.Add(new Frame { label = "revealed/stable/" + index, center = center, debug = true });
                    frames.Add(new Frame { label = "revealed/subcell/" + index, center = center + new Vector2(.375f, .125f), debug = true });
                }
                var last = frames[frames.Count - 1].center;
                for (int i = 0; i < 6; i++) frames.Add(new Frame { label = "fog/" + i, center = last + Vector2.right * (i / 2), debug = false, radius = i % 2 == 0 ? 6 : 8 });
                var trace = new Trace { frames = frames.ToArray() };
                string traceJson = JsonUtility.ToJson(trace, true); report.traceHash = Hash(System.Text.Encoding.UTF8.GetBytes(traceJson));
                File.WriteAllText(Path.Combine(Output, stage + "-trace.json"), traceJson);
                if (stage == "after")
                {
                    var initial = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(Output, "before-report.json")));
                    Assert.That(report.traceHash, Is.EqualTo(initial.traceHash)); Assert.That(report.frozenSourceHash, Is.EqualTo(initial.frozenSourceHash));
                    Assert.That(report.cells, Is.EqualTo(initial.cells)); Assert.That(report.objectViews, Is.EqualTo(initial.objectViews));
                }
                using var reference = new Subject(typeof(ContactShadowCostBaseline), rig.transform);
                using var candidate = new Subject(typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows", true), rig.transform);
                var receiver = Read<Renderer>(rig, "floorReceiver");
                var transitions = Read<List<SpriteRenderer>>(rig, "groundPresentations");
                var ground = Read<List<SpriteRenderer>>(rig, "groundContactPresentations");
                var pillar = Read<Sprite>(rig, "pillarContactSprite");
                Rect viewport = default;
                void SetFrame(Frame frame)
                {
                    if (floor.Fog.DebugFogDisabled != frame.debug) floor.Fog.ToggleDebugFog();
                    if (!frame.debug)
                    {
                        var cell = map.WorldToCell(frame.center);
                        floor.Fog.SetVisible(floor.Map.FloorCells.Where(c => Mathf.Abs(c.x - cell.x) <= frame.radius && Mathf.Abs(c.y - cell.y) <= frame.radius)
                            .OrderBy(c => c.y).ThenBy(c => c.x).Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray());
                    }
                    camera.orthographicSize = 9; camera.aspect = 16f / 9f;
                    camera.transform.position = new Vector3(frame.center.x, frame.center.y, -10);
                    map.ApplyFog(floor); map.EnsureCameraPresentation();
                    viewport = new Rect(frame.center.x - 19, frame.center.y - 12, 38, 24);
                }
                void Call(Subject subject) => subject.Rebuild(map, receiver, viewport, objects.SpawnedObjects, transitions, pillar, ground);
                var target = new RenderTexture(1216, 768, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
                var image = new Texture2D(1216, 768, TextureFormat.RGBA32, false, true);
                using var commands = new CommandBuffer();
                byte[] Pixels(Subject subject)
                {
                    commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.white);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(viewport.xMin, viewport.xMax, viewport.yMin, viewport.yMax, -1, 1), true));
                    commands.DrawMesh(Read<Mesh>(subject.Owner, "mesh"), Read<GameObject>(subject.Owner, "root").transform.localToWorldMatrix, Read<Material>(subject.Owner, "material"));
                    Graphics.ExecuteCommandBuffer(commands); var previous = RenderTexture.active;
                    try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1216, 768), 0, 0); image.Apply(); return image.GetRawTextureData<byte>().ToArray(); }
                    finally { RenderTexture.active = previous; }
                }
                try
                {
                    report.bandsPerFrame = new int[frames.Count]; report.verticesPerFrame = new int[frames.Count];
                    report.projectedRects = new int[frames.Count]; report.defaultDistinctHashes = new int[frames.Count]; report.mixedDistinctHashes = new int[frames.Count];
                    for (int i = 0; i < frames.Count; i++)
                    {
                        SetFrame(frames[i]); Call(reference); Call(candidate);
                        var beforeMesh = Read<Mesh>(reference.Owner, "mesh"); var afterMesh = Read<Mesh>(candidate.Owner, "mesh");
                        CollectionAssert.AreEqual(beforeMesh.vertices, afterMesh.vertices); CollectionAssert.AreEqual(beforeMesh.colors32, afterMesh.colors32); CollectionAssert.AreEqual(beforeMesh.triangles, afterMesh.triangles);
                        report.geometryComparisons++;
                        var expected = Pixels(reference); var actual = Pixels(candidate);
                        Assert.That(actual.SequenceEqual(expected), Is.True, "GPU pixels: " + frames[i].label); report.pixelComparisons++;
                        report.nonBlankPixels |= expected.Where((_, channel) => channel % 4 != 3).Any(value => value < 255);
                        report.bandsPerFrame[i] = Read<ICollection>(reference.Owner, "bands").Count; report.verticesPerFrame[i] = beforeMesh.vertexCount;
                        var rects = Read<HashSet<Rect>>(reference.Owner, "projectedBands");
                        report.projectedRects[i] = rects.Count;
                        report.defaultDistinctHashes[i] = rects.Select(r => r.GetHashCode()).Distinct().Count();
                        report.mixedDistinctHashes[i] = rects.Select(r => System.HashCode.Combine(r.x, r.y, r.width, r.height)).Distinct().Count();
                    }
                    Assert.That(report.nonBlankPixels, Is.True, "Positive control: real contact pixels must be drawn.");
                }
                finally { Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
                for (int round = 0; round < report.warmupRounds; round++) foreach (var frame in frames) { SetFrame(frame); Call(reference); Call(candidate); }
                var samples = new List<Sample>();
                for (int round = 0; round < report.measuredRounds; round++) for (int i = 0; i < frames.Count; i++)
                {
                    SetFrame(frames[i]); var sample = new Sample { round = round, frame = i, referenceFirst = (round + i) % 2 == 0 };
                    void Measure(Subject subject, bool old)
                    {
                        int gc = GC.CollectionCount(0); Call(subject); int count = GC.CollectionCount(0) - gc;
                        subject.SaveTiming(sample, old); if (old) sample.beforeGc = count; else sample.afterGc = count;
                    }
                    if (sample.referenceFirst) { Measure(reference, true); Measure(candidate, false); }
                    else { Measure(candidate, false); Measure(reference, true); }
                    samples.Add(sample);
                }
                report.samples = samples.ToArray(); report.pairedCalls = samples.Count;
                if (stage == "after")
                {
                    var type = candidate.Owner.GetType();
                    report.nativeCornerProjections = (int)type.GetProperty("NativeCornerProjectionCount").GetValue(candidate.Owner);
                    report.unitCornerProjections = (int)type.GetProperty("UnitCornerProjectionCount").GetValue(candidate.Owner);
                    Assert.That(report.unitCornerProjections, Is.GreaterThan(0), "The production 2D grid must exercise the guarded shortcut.");
                }
                var names = new[] { "Total", "Walls/setup", "Object sources", "Ground sources", "Forest contacts", "Transitions", "Reuse/prune", "Projection/clipping", "Mesh upload/remember" };
                report.phases = names.Select((name, index) => new Phase { name = name, before = Stats(samples.Select(s => s.before[index])), after = Stats(samples.Select(s => s.after[index])) }).ToArray();
            };
            probe.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
            File.WriteAllText(Path.Combine(Output, stage + "-report.json"), JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Paired samples: " + report.pairedCalls + "; median before/after: " + report.phases[0].before.median + "/" + report.phases[0].after.median);
        }
    }
}
