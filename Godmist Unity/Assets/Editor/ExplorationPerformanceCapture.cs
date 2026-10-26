using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// Opt-in CPU capture through out/exploration-performance.request. No scene,
// input, camera or Play state changes; restores the previous profiler state.
[InitializeOnLoad]
internal static class ExplorationPerformanceCapture
{
    private static readonly Dictionary<string, (double Sum, double Max, int Calls)> markers = new();
    private static readonly List<(double Cpu, double Gpu, long Alloc)> frames = new();
    private static readonly List<(double Cpu, string Samples)> worst = new();
    private static readonly List<double> gameFrameTimes = new();
    private static readonly Dictionary<int, string> names = new();
    private static readonly List<(string Name, double Ms)> samples = new();
    private static bool recording, wasEnabled, wasEditor, wasDeep;
    private static bool wasUi, wasUiDetails;
    private static bool spikeCapture, wasBinary;
    private static string wasLogFile, rawLogFile;
    private static readonly List<double> runtimeFrameTimes = new();
    private static int lastRuntimeFrame, unavailableFrames;
    private static double lastTick, maxTickGap;
    private static long tickReadStarted;
    private static int lastFrame, detailedFrames, truncatedFrames;
    private static bool focusedAtStart;
    private static double started, pollAt;
    private static double dungeonReadyAt;
    private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "out"));
    private static string Request => Path.Combine(Root, "exploration-performance.request");
    // Older assemblies running in Play must not consume a request intended for
    // this capture version before the new runtime code has been imported.
    private static string NextRequest => Path.Combine(Root, "exploration-performance-v9.request");
    private static string Status => Path.Combine(Root, "exploration-performance.status");

    static ExplorationPerformanceCapture()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!recording)
        {
            if (EditorApplication.timeSinceStartup < pollAt) return;
            pollAt = EditorApplication.timeSinceStartup + 1;
            var request = File.Exists(NextRequest) ? NextRequest : Request;
            if (!File.Exists(request)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            { File.WriteAllText(Status, "Awaiting normal Play Mode; no editor state changes requested."); return; }
            if (!Application.isFocused)
            { File.WriteAllText(Status, "Awaiting focused Game View; no editor state changes requested."); return; }
            var requestedScene = File.ReadAllText(request).Trim();
            spikeCapture = requestedScene == "combat-spikes" || requestedScene == "dungeon-spikes";
            if (requestedScene == "combat" || requestedScene == "combat-spikes")
            {
                var combat = Godmist.Views.Game.GameBootstrap.Instance?.Session?.ActiveCombat;
                if (combat == null || combat.Outcome != Godmist.Core.Enums.CombatOutcomeState.Ongoing)
                {
                    dungeonReadyAt = 0;
                    File.WriteAllText(Status, "Awaiting normal dungeon combat; no editor state changes requested.");
                    return;
                }
            }
            if (requestedScene == "dungeon" || requestedScene == "combat" || spikeCapture)
            {
                if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("DungeonSandbox").isLoaded)
                {
                    dungeonReadyAt = 0;
                    File.WriteAllText(Status, "Awaiting loaded dungeon; startup/menu frames excluded.");
                    return;
                }
                if (dungeonReadyAt == 0) dungeonReadyAt = EditorApplication.timeSinceStartup + 5;
                if (EditorApplication.timeSinceStartup < dungeonReadyAt)
                { File.WriteAllText(Status, "Dungeon warmup; waiting 5 seconds before recording."); return; }
            }
            File.Delete(request);
            markers.Clear(); frames.Clear(); worst.Clear(); names.Clear(); gameFrameTimes.Clear();
            runtimeFrameTimes.Clear();
            unavailableFrames = 0;
            maxTickGap = 0;
            lastRuntimeFrame = Time.frameCount;
            detailedFrames = truncatedFrames = 0;
            wasEnabled = ProfilerDriver.enabled;
            wasEditor = ProfilerDriver.profileEditor;
            wasDeep = ProfilerDriver.deepProfiling;
            wasBinary = UnityEngine.Profiling.Profiler.enableBinaryLog;
            wasLogFile = UnityEngine.Profiling.Profiler.logFile;
            rawLogFile = null;
            if (spikeCapture)
            {
                rawLogFile = Path.Combine(Root, requestedScene + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".raw");
                UnityEngine.Profiling.Profiler.logFile = rawLogFile;
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            }
            wasUi = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UI);
            wasUiDetails = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UIDetails);
            // Never enable Deep Profile: it materially changes frame cost.
            // Include editor samples: otherwise editor work/waits collapse into
            // an opaque EditorLoop and can hide the source of slow Game View frames.
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.enabled = true;
            // CPU callback samples remain available. Detailed UI statistics caused
            // a 478 ms measurement spike in the previous capture.
            UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UI, false);
            UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UIDetails, false);
            lastFrame = ProfilerDriver.lastFrameIndex;
            started = EditorApplication.timeSinceStartup;
            lastTick = started;
            focusedAtStart = Application.isFocused;
            recording = true;
            File.WriteAllText(Status, "Recording CPU/render markers for " + (spikeCapture ? "45" : "20")
                + " seconds; deepProfile=" + wasDeep + "; binaryLog=" + spikeCapture);
            return;
        }
        try
        {
            var now = EditorApplication.timeSinceStartup;
            maxTickGap = Math.Max(maxTickGap, (now - lastTick) * 1000);
            lastTick = now;
            if (EditorApplication.isPlaying && Time.frameCount != lastRuntimeFrame)
            {
                runtimeFrameTimes.Add(Time.unscaledDeltaTime * 1000d);
                lastRuntimeFrame = Time.frameCount;
            }
            var latest = ProfilerDriver.lastFrameIndex;
            tickReadStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            // Do not silently discard older queued frames after a long editor stall.
            // Timing reads are cheap; the detailed tree budget is shared by this tick.
            for (var f = lastFrame + 1; f <= latest; f++) Read(f);
            lastFrame = latest;
            if (EditorApplication.timeSinceStartup - started > (spikeCapture ? 45 : 20) || !EditorApplication.isPlaying)
            { WriteReport(); Restore(); }
        }
        catch (Exception ex)
        { File.WriteAllText(Status, "Failed: " + ex); Restore(); }
    }

    private static void Read(int index)
    {
        using var data = ProfilerDriver.GetRawFrameDataView(index, 0);
        if (!data.valid || data.sampleCount == 0) { unavailableFrames++; return; }
        // Native per-sample reads can themselves stall a busy frame. Retain
        // every frame timing, but inspect the marker tree only every eighth frame.
        frames.Add((data.frameTimeMs, data.frameGpuTimeMs, 0));
        if (frames.Count % 8 != 1 && data.frameTimeMs <= 1000d / 30) return;
        if ((System.Diagnostics.Stopwatch.GetTimestamp() - tickReadStarted)
            * 1000d / System.Diagnostics.Stopwatch.Frequency >= 2) return;
        detailedFrames++;
        // Keep editor-only frames too: a stalled editor may prevent PlayerLoop
        // altogether. Report actual game-frame progress separately below.
        samples.Clear();
        var gameUpdate = false;
        long alloc = 0;
        var readStarted = tickReadStarted;
        for (var s = 0; s < data.sampleCount; s++)
        {
            if (s % 64 == 0 && (System.Diagnostics.Stopwatch.GetTimestamp() - readStarted)
                    * 1000d / System.Diagnostics.Stopwatch.Frequency >= 2)
            { truncatedFrames++; break; }
            var id = data.GetSampleMarkerId(s);
            if (!names.TryGetValue(id, out var name)) names[id] = name = data.GetSampleName(s);
            var ms = data.GetSampleTimeMs(s);
            if (name == "GC.Alloc")
            {
                if (data.GetSampleMetadataCount(s) > 0) alloc += data.GetSampleMetadataAsLong(s, 0);
                continue;
            }
            if (name == "Update.ScriptRunBehaviourUpdate") gameUpdate = true;
            if (ms > 0) samples.Add((name, ms));
        }
        frames[frames.Count - 1] = (data.frameTimeMs, data.frameGpuTimeMs, alloc);
        if (gameUpdate) gameFrameTimes.Add(data.frameTimeMs);
        foreach (var s in samples)
        {
            markers.TryGetValue(s.Name, out var value);
            markers[s.Name] = (value.Sum + s.Ms, Math.Max(value.Max, s.Ms), value.Calls + 1);
        }
        // Avoid sorting/formatting every sample of every frame (especially
        // hundreds of thousands of GC.Alloc markers on a loading frame).
        if (worst.Count == 5 && data.frameTimeMs <= worst[4].Cpu) return;
        worst.Add((data.frameTimeMs, string.Join("\n", samples.OrderByDescending(s => s.Ms).Take(40)
            .Select(s => Format(s.Ms) + "\t" + s.Name))));
        worst.Sort((a, b) => b.Cpu.CompareTo(a.Cpu));
        if (worst.Count > 5) worst.RemoveAt(5);
    }

    private static string Format(double ms) => ms.ToString("F3", CultureInfo.InvariantCulture);
    private static void WriteReport()
    {
        var lines = new List<string> { "captureVersion=9", "utc=" + DateTime.UtcNow.ToString("O"), "frames=" + frames.Count,
            "deepProfile=" + wasDeep, "timings=inclusive; nested markers overlap; zero GPU time means unavailable",
            "markerSampling=1/8 frames plus frames over 33.333ms; detailedFrames=" + detailedFrames
                + "; selection is biased toward slow frames; worst attribution covers detailed frames only" };
        lines.Add("unavailableProfilerFrames=" + unavailableFrames + ";maxEditorTickGapMs=" + Format(maxTickGap)
            + ";binaryLog=" + (rawLogFile ?? "disabled"));
        if (runtimeFrameTimes.Count > 0)
        {
            var times = runtimeFrameTimes.OrderBy(time => time).ToArray();
            lines.Add("observedRuntimeFrames=" + times.Length + ";runtimeDeltaMedianMs=" + Format(times[times.Length / 2])
                + ";runtimeDeltaP95Ms=" + Format(times[(int)((times.Length - 1) * .95)])
                + ";runtimeDeltaMaxMs=" + Format(times.Last())
                + ";runtimeDeltasOver100ms=" + times.Count(time => time > 100)
                + ";runtimeDeltasOver200ms=" + times.Count(time => time > 200)
                + ";runtimeDeltaIncludesWaiting=True;oneObservationPerEditorTick=True");
        }
        lines.Add("truncatedMarkerTrees=" + truncatedFrames
            + ";markerReadBudgetMs=2; partial trees may undercount markers and game-update detection; frame timings remain complete for captured frames");
        var elapsed = EditorApplication.timeSinceStartup - started;
        lines.Add("elapsedSeconds=" + Format(elapsed) + ";sampledGameFrames=" + gameFrameTimes.Count
            + ";focusedAtStart=" + focusedAtStart + ";focusedAtEnd=" + Application.isFocused);
        if (frames.Count > 0)
        {
            var times = frames.Select(f => f.Cpu).OrderBy(t => t).ToArray();
            lines.Add("meanCpuMs=" + Format(times.Average()) + ";p95CpuMs=" + Format(times[(int)((times.Length - 1) * .95)])
                + ";maxCpuMs=" + Format(times.Last()) + ";framesOver100ms=" + times.Count(t => t > 100));
            lines.Add("meanGpuMs=" + Format(frames.Average(f => f.Gpu)) + ";meanSampledGcAllocBytes="
                + Format(frames.Sum(f => f.Alloc) / (double)Math.Max(1, detailedFrames)));
        }
        if (gameFrameTimes.Count > 0)
        {
            var times = gameFrameTimes.OrderBy(t => t).ToArray();
            lines.Add("sampledGameUpdateFrames=" + times.Length + ";sampledGameFrameMedianMs=" + Format(times[times.Length / 2])
                + ";gameFrameP95Ms=" + Format(times[(int)((times.Length - 1) * .95)])
                + ";gameFramesOver16_667ms=" + times.Count(t => t > 1000d / 60)
                + ";gameFramesOver33_333ms=" + times.Count(t => t > 1000d / 30)
                + ";gameFramesOver100ms=" + times.Count(t => t > 100));
        }
        lines.Add("marker\tinclusiveMsPerSampledFrame\tmaxSampledSingleCallMs\tsampledCalls");
        var largest = markers.OrderByDescending(p => p.Value.Sum).Take(100).Select(p => p.Key).ToHashSet();
        lines.AddRange(markers.Where(p => largest.Contains(p.Key) || p.Key.StartsWith("Godmist.", StringComparison.Ordinal))
            .OrderByDescending(p => p.Value.Sum).Select(p => p.Key.Replace('\t', ' ')
            + "\t" + Format(p.Value.Sum / Math.Max(1, detailedFrames)) + "\t" + Format(p.Value.Max) + "\t" + p.Value.Calls));
        foreach (var frame in worst) { lines.Add("WORST DETAILED FRAME (may include editor-only work) " + Format(frame.Cpu) + "ms"); lines.Add(frame.Samples); }
        File.WriteAllLines(Path.Combine(Root, "exploration-performance.txt"), lines);
        File.WriteAllText(Status, "Complete; frames=" + frames.Count);
    }

    private static void Restore()
    {
        if (!recording) return;
        if (spikeCapture)
        {
            // Close our native log before restoring the user's profiler output.
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = "";
            UnityEngine.Profiling.Profiler.logFile = wasLogFile;
            UnityEngine.Profiling.Profiler.enableBinaryLog = wasBinary;
        }
        ProfilerDriver.enabled = wasEnabled;
        ProfilerDriver.profileEditor = wasEditor;
        UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UI, wasUi);
        UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.UIDetails, wasUiDetails);
        recording = false;
    }
}
