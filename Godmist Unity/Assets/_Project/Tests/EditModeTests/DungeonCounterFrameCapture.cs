using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Profiling;

namespace Godmist.Tests.EditMode
{
    // Explicit 12-second capture: selected native counters, no raw profiler tree,
    // per-frame allocations, scene objects, worker processes or gameplay changes.
    [InitializeOnLoad]
    internal static class DungeonCounterFrameCapture
    {
        private const int Capacity = 4096;
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/frame-counters-pass-20261017"));
        private static readonly string[] Names = {
            "Godmist.Diagnostics.FrameAlignment", "PlayerLoop", "EditorLoop", "Main Thread",
            "Godmist.Exploration.NewTarget", "Godmist.Navigation.MeshPlan", "Godmist.Navigation.TraversalPlan",
            "Godmist.Exploration.LeaderTick", "Godmist.Exploration.FollowersTick",
            "Godmist.Dungeon.Patrols", "Godmist.Dungeon.Patrols.Sense", "Godmist.Dungeon.Patrols.Move",
            "Godmist.Dungeon.Traps", "Godmist.Dungeon.Detection", "Godmist.Dungeon.Fog", "Godmist.Dungeon.Fog.Render",
            "Godmist.Lighting.Refresh", "Godmist.Lighting.SourceSelection", "Godmist.Lighting.ReceiverInputs",
            "Godmist.Lighting.ShadowMasks", "Godmist.Lighting.ContactShadows", "Godmist.Lighting.CasterCollection",
            "Godmist.Game.Autosave", "Godmist.Combat.CreateContext", "Godmist.SkillBar.RebuildSlots",
            "BehaviourUpdate", "LateBehaviourUpdate", "RenderPlayModeViewCameras", "UGUI.Rendering.UpdateBatches",
            "Physics2D.Simulate", "GC.Collect", "GarbageCollector.CollectIncremental", "Gfx.WaitForPresentOnGfxThread",
            "Godmist.Lighting.Late", "Godmist.Dungeon.FloorLate", "Godmist.Dungeon.CameraPresentation",
            "Godmist.Dungeon.Fog.Animation", "Godmist.Dungeon.Minimap.Late", "Godmist.Dungeon.Minimap.Terrain",
            "Godmist.Dungeon.Minimap.Markers", "Godmist.Exploration.ActorLate", "Godmist.Dungeon.ObjectLate",
            "Godmist.Dungeon.ObstacleLate", "Godmist.Dungeon.BodyLate", "Godmist.Combat.ViewsLate",
            "Godmist.Exploration.CameraLate", "Godmist.UI.CombatHudLate", "Godmist.UI.TooltipLate",
            "Godmist.Dungeon.Patrols.Plan", "Godmist.Dungeon.Patrols.Facing", "Godmist.Lighting.ShadowInputs",
            "Godmist.Dungeon.Fog.Feedback", "Godmist.Dungeon.FovPreview", "Godmist.Dungeon.RoomAlerts",
            "Godmist.Dungeon.Exits", "Godmist.Dungeon.Merges", "Godmist.Dungeon.Sessions", "Godmist.Dungeon.Focus",
            "Godmist.Exploration.FollowerGoal", "Godmist.Exploration.FollowerDirect", "Godmist.Exploration.FollowerPlan",
            "Godmist.Exploration.FollowerMove", "Godmist.Navigation.Revision", "Godmist.Navigation.ObstacleIndex",
            "Godmist.Navigation.DirectValidation", "Godmist.Navigation.Bake", "Godmist.Exploration.TraversalCollider",
            "Godmist.Exploration.StandingQuery", "Godmist.Exploration.MovementQuery", "Godmist.Dungeon.Patrols.ValidateMove",
            "Godmist.Dungeon.Patrols.ExecuteMove", "Godmist.Dungeon.Patrols.Persist",
            "Godmist.Lighting.PrepareDraws", "Godmist.Lighting.RecordDraws", "Godmist.Lighting.SubmitDraws",
            "Godmist.Lighting.SourceFixtures", "Godmist.Lighting.TorchPresentation", "Godmist.Lighting.SourceCandidates",
            "Godmist.Lighting.ArenaSources", "Godmist.Lighting.MeshLookup", "Godmist.Lighting.MeshBuild",
            "Godmist.Exploration.Move", "Godmist.Exploration.Facing", "Godmist.Exploration.CommitMove",
            "Godmist.Animation.Resolve", "Godmist.Animation.LoadSprite", "Godmist.Animation.PrepareDirections",
            "Godmist.Dungeon.Fog.BoundaryKnowledge", "Godmist.Dungeon.Fog.Tiles", "Godmist.Dungeon.Fog.Walls",
            "Godmist.Dungeon.Fog.Ground", "Godmist.Dungeon.Fog.Soft", "Godmist.Dungeon.Fog.UploadColors",
            "Views.dll!Godmist.Views.Exploration::ExplorationCharacterView.Update() [Invoke]",
            "Views.dll!Godmist.Views.Dungeons::DungeonCombatCoordinator.Update() [Invoke]",
            "Views.dll!Godmist.Views.Dungeons::DungeonChestView.Update() [Invoke]",
            "Views.dll!Godmist.Views.UI.Workspace::CharacterInfoPartyRailView.Update() [Invoke]",
            "Views.dll!Godmist.Views.Combat::CombatUnitInspectController.Update() [Invoke]",
            "Views.dll!Godmist.Views.Dungeons::DungeonTacticalTooltip.Update() [Invoke]",
            "Views.dll!Godmist.Views.UI::ContextualInputHintsView.Update() [Invoke]",
            "UnityEngine.UI.dll!UnityEngine.EventSystems::EventSystem.Update() [Invoke]",
            "Mono.JIT", "Profiler.FlushMemoryCounters", "WaitForTargetFPS",
            "Godmist.Dungeon.Traps.GatherOccupants", "Godmist.Dungeon.Traps.AdvanceStatuses",
            "Godmist.Dungeon.Traps.CellEntries", "Godmist.Dungeon.Traps.Deaths",
            "Godmist.Exploration.StatusServices", "Godmist.Exploration.StatusTick",
            "Godmist.Exploration.StatusResources", "Godmist.Exploration.StatusTurnStart", "Godmist.Exploration.StatusTurnEnd",
            "Godmist.Lighting.ReadCasterState", "Godmist.Lighting.PrepareProperties", "Godmist.Lighting.CasterProperties",
            "Godmist.Dungeon.Patrols.ReadPosition", "Godmist.Dungeon.Patrols.StoreActor", "Godmist.Dungeon.Patrols.StoreEncounter",
            "Godmist.Lighting.ReceiverWallSelection", "Godmist.Lighting.ReceiverWalls",
            "Godmist.Lighting.ReceiverGroundSelection", "Godmist.Lighting.ReceiverGround", "Godmist.Lighting.ReceiverBlockRead",
            "Godmist.Dungeon.Fog.BuildMesh", "Godmist.Dungeon.Fog.UploadGeometry",
            "Godmist.Lighting.ContactVisibilityReuse"
        };
        private struct CounterHead { }
        private struct CounterTail { }
        private static Recorder[] recorders;
        private static bool[] previousEnabled, available;
        private static CustomSampler alignmentSampler;
        private static FrameRow[] rows;
        private static long[] nanoseconds;
        private static int[] calls;
        private static bool capturing, previousProfiler, finishRequested, profilerReenabled, callbacksInstalled;
        private static int count, skip, lastFrame;
        private static long previousTimestamp, deadlineTimestamp;
        private static double nextCheck, captureStarted;
        private static string label, failure;

        static DungeonCounterFrameCapture()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }

        private static void Tick()
        {
            if (capturing)
            {
                if (finishRequested || !EditorApplication.isPlaying || EditorApplication.isPaused
                    || Stopwatch.GetTimestamp() >= deadlineTimestamp) Finish();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            var request = Path.Combine(Output, "capture.request");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling || !File.Exists(request)) return;
            label = Path.GetFileName(File.ReadAllText(request).Trim()); File.Delete(request);
            Directory.CreateDirectory(Output);
            try { Start(); }
            catch (Exception exception)
            {
                Restore();
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Failed to start: " + exception);
            }
        }

        private static void Start()
        {
            previousProfiler = ProfilerDriver.enabled;
            ProfilerDriver.enabled = false;
            capturing = true;
            recorders = new Recorder[Names.Length]; previousEnabled = new bool[Names.Length]; available = new bool[Names.Length];
            rows = new FrameRow[Capacity]; nanoseconds = new long[Capacity * Names.Length]; calls = new int[nanoseconds.Length];
            alignmentSampler = CustomSampler.Create(Names[0]);
            for (var i = 0; i < Names.Length; i++)
            {
                var recorder = i == 0 ? alignmentSampler.GetRecorder() : Recorder.Get(Names[i]);
                recorders[i] = recorder;
                available[i] = recorder != null && recorder.isValid;
                if (!available[i]) continue;
                previousEnabled[i] = recorder.enabled;
                recorder.enabled = true;
            }
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!InsertCallbacks(ref loop)) throw new InvalidOperationException("Expected Update and PreLateUpdate phases are missing.");
            PlayerLoop.SetPlayerLoop(loop);
            callbacksInstalled = true;
            count = 0; skip = 4; lastFrame = -1; finishRequested = profilerReenabled = false; failure = null;
            previousTimestamp = Stopwatch.GetTimestamp();
            deadlineTimestamp = previousTimestamp + Stopwatch.Frequency * 12;
            captureStarted = EditorApplication.timeSinceStartup;
            File.WriteAllText(Path.Combine(Output, "state.txt"), "Capturing " + label + " (selected counters; full Profiler disabled)");
        }

        private static void ReadPreviousFrame()
        {
            if (!capturing || finishRequested) return;
            try
            {
                var now = Stopwatch.GetTimestamp();
                var frame = Time.frameCount;
                if (frame == lastFrame) return;
                lastFrame = frame;
                var intervalMs = (now - previousTimestamp) * 1000d / Stopwatch.Frequency;
                previousTimestamp = now;
                if (skip > 0) { skip--; return; }
                if (count >= Capacity) { finishRequested = true; return; }
                var offset = count * Names.Length;
                for (var i = 0; i < recorders.Length; i++)
                {
                    if (!available[i]) continue;
                    calls[offset + i] = recorders[i].sampleBlockCount;
                    nanoseconds[offset + i] = recorders[i].elapsedNanoseconds;
                }
                profilerReenabled |= ProfilerDriver.enabled;
                rows[count++] = new FrameRow { observedFrame = frame, deltaMs = Time.unscaledDeltaTime * 1000d,
                    updateIntervalMs = intervalMs,
                    collectorNs = (Stopwatch.GetTimestamp() - now) * 1e9 / Stopwatch.Frequency };
                if (now >= deadlineTimestamp) finishRequested = true;
            }
            catch (Exception exception) { failure = exception.ToString(); finishRequested = true; }
        }

        private static void EmitAlignmentPattern()
        {
            if (!capturing || finishRequested || alignmentSampler == null) return;
            // Includes a zero-sample frame: this also detects stale counter values.
            for (var i = 0; i < Time.frameCount % 7; i++) { alignmentSampler.Begin(); alignmentSampler.End(); }
        }

        private static void Finish()
        {
            var wallSeconds = EditorApplication.timeSinceStartup - captureStarted;
            var frameCount = count;
            Restore(); // Restore before serializing/writing; export is outside measured gameplay.
            try
            {
                var lag = InferLag(rows, calls, Names.Length, frameCount, out var matches);
                var reportRows = new FrameRow[frameCount]; Array.Copy(rows, reportRows, frameCount);
                var times = new long[frameCount * Names.Length]; Array.Copy(nanoseconds, times, times.Length);
                var hits = new int[times.Length]; Array.Copy(calls, hits, hits.Length);
                var report = new Report { metricNames = Names, metricAvailable = available, frames = reportRows,
                    elapsedNs = times, sampleCounts = hits, recorderFrameLag = lag, alignmentMatches = matches,
                    alignmentValid = available[0] && frameCount >= 21 && matches >= frameCount * .99d,
                    captureWallSeconds = wallSeconds, profilerWasEnabled = previousProfiler,
                    profilerWasReenabledDuringCapture = profilerReenabled, failure = failure,
                    vSyncCount = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate };
                File.WriteAllText(Path.Combine(Output, label + ".json"), JsonUtility.ToJson(report));
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + label + ": " + frameCount
                    + " frames; counter alignment=" + report.alignmentValid + "; lag=" + lag);
            }
            catch (Exception exception) { File.WriteAllText(Path.Combine(Output, "state.txt"), "Export failed: " + exception); }
        }

        private static void Restore()
        {
            if (!capturing) return;
            capturing = false;
            try
            {
                if (callbacksInstalled)
                {
                    var loop = PlayerLoop.GetCurrentPlayerLoop(); RemoveCallbacks(ref loop); PlayerLoop.SetPlayerLoop(loop);
                    callbacksInstalled = false;
                }
            }
            finally
            {
                try
                {
                    if (recorders != null)
                        for (var i = 0; i < recorders.Length; i++)
                            if (recorders[i] != null && recorders[i].isValid) recorders[i].enabled = previousEnabled[i];
                }
                finally { ProfilerDriver.enabled = previousProfiler; }
            }
        }

        internal static bool InsertCallbacks(ref PlayerLoopSystem loop)
        {
            var head = Insert(ref loop, typeof(UnityEngine.PlayerLoop.Update),
                new PlayerLoopSystem { type = typeof(CounterHead), updateDelegate = ReadPreviousFrame }, true);
            var tail = Insert(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate),
                new PlayerLoopSystem { type = typeof(CounterTail), updateDelegate = EmitAlignmentPattern }, false);
            return head && tail;
        }

        private static bool Insert(ref PlayerLoopSystem loop, Type phase, PlayerLoopSystem entry, bool prepend)
        {
            if (loop.type == phase)
            {
                var existing = loop.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var replacement = new PlayerLoopSystem[existing.Length + 1];
                Array.Copy(existing, 0, replacement, prepend ? 1 : 0, existing.Length);
                replacement[prepend ? 0 : existing.Length] = entry; loop.subSystemList = replacement; return true;
            }
            if (loop.subSystemList == null) return false;
            for (var i = 0; i < loop.subSystemList.Length; i++)
            {
                var child = loop.subSystemList[i];
                if (!Insert(ref child, phase, entry, prepend)) continue;
                var replacement = (PlayerLoopSystem[])loop.subSystemList.Clone(); replacement[i] = child;
                loop.subSystemList = replacement; return true;
            }
            return false;
        }

        internal static void RemoveCallbacks(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return;
            var old = loop.subSystemList;
            var remaining = 0;
            for (var i = 0; i < old.Length; i++)
                if (old[i].type != typeof(CounterHead) && old[i].type != typeof(CounterTail)) remaining++;
            var replacement = new PlayerLoopSystem[remaining];
            var index = 0;
            for (var i = 0; i < old.Length; i++)
            {
                var child = old[i];
                if (child.type == typeof(CounterHead) || child.type == typeof(CounterTail)) continue;
                RemoveCallbacks(ref child); replacement[index++] = child;
            }
            loop.subSystemList = replacement;
        }

        internal static int InferLag(FrameRow[] data, int[] counts, int width, int length, out int matches)
        {
            matches = -1;
            var bestLag = 0;
            for (var lag = 0; lag <= 3; lag++)
            {
                var current = 0;
                for (var row = 0; row < length; row++)
                    if (counts[row * width] == ((data[row].observedFrame - lag) % 7 + 7) % 7) current++;
                if (current <= matches) continue;
                matches = current; bestLag = lag;
            }
            return bestLag;
        }

        [Serializable] internal struct FrameRow
        {
            public int observedFrame;
            public double deltaMs, updateIntervalMs, collectorNs;
        }
        [Serializable] private sealed class Report
        {
            public string[] metricNames;
            public bool[] metricAvailable;
            public FrameRow[] frames;
            public long[] elapsedNs;
            public int[] sampleCounts;
            public int recorderFrameLag, alignmentMatches, vSyncCount, targetFrameRate;
            public bool alignmentValid, profilerWasEnabled, profilerWasReenabledDuringCapture;
            public double captureWallSeconds;
            public string failure;
        }
    }
}
