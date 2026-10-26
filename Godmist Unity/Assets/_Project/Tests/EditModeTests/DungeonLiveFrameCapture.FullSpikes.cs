using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    internal static partial class DungeonLiveFrameCapture
    {
        [Serializable] internal sealed class FullSpikeRow
        {
            public int frame, samples;
            public double startMs, frameMs, playerMs, editorMs, gcMs, incrementalMs, profilerMs, cameraMs, shadowPrepareMs;
        }
        [Serializable] internal sealed class FullSpikeNode
        {
            public int index, parent = -1, end, allocationCalls, flags;
            public string name;
            public double startMs, totalMs, selfMs, rawSelfMs;
            public long allocationBytes;
        }
        [Serializable] private sealed class FullSpikeThread
        {
            public int thread;
            public string name, group;
            public double startMs, frameMs;
            public bool fullTimedHierarchy;
            public FullSpikeNode[] nodes;
        }
        [Serializable] private sealed class FullSpikeDetail
        {
            public int frame;
            public FullSpikeThread[] threads;
            public bool threadLimitReached;
        }
        [Serializable] private sealed class FullSpikeReport
        {
            public FullSpikeRow[] frames;
            public FullSpikeDetail[] peaks;
            public string rawProfilePath;
            public bool rawSaved, profilerWasEnabled, editorWasEnabled;
            public bool editorProfiled = true, analysisAfterRecordingStopped = true;
            public double recordingWallSeconds, saveRawMs, analysisMs;
            public int requestedFirst, requestedLast, overwrittenFrames, invalidFrames;
            public FullSpikeCamera[] camerasAtExport;
            public string scope = "All recorded threads in native .data; JSON main/render timed hierarchy; other threads roots and >=0.5ms self samples. GC.Alloc leaves aggregated on immediate parent. No deep profiling or managed allocation stacks. Timings include Profiler/Editor overhead; not FPS benchmark.";
        }

        [Serializable] private sealed class FullSpikeCamera
        {
            public string name, type, target;
            public int pixelWidth, pixelHeight, cullingMask;
            public float depth, orthographicSize;
            public bool orthographic, occlusionCulling;
        }

        internal static void CloseFullSpikeParents(List<FullSpikeNode> stack, int index)
        {
            while (stack.Count > 0 && index >= stack[stack.Count - 1].end) stack.RemoveAt(stack.Count - 1);
        }

        internal static int[] SelectFullSpikeFrames(IReadOnlyList<FullSpikeRow> rows)
        {
            if (rows.Count == 0) return Array.Empty<int>();
            var selected = new HashSet<int>();
            // Keep Editor-only gaps as well as gameplay spikes. Rankings are
            // separate because the largest Editor frame need not run the game.
            foreach (var row in rows.OrderByDescending(r => r.frameMs).Take(3)) selected.Add(row.frame);
            foreach (var row in rows.Where(r => r.playerMs > .1).OrderByDescending(r => r.playerMs).Take(2)) selected.Add(row.frame);
            foreach (var row in rows.Where(r => r.cameraMs > .05).OrderByDescending(r => r.cameraMs).Take(2)) selected.Add(row.frame);
            foreach (var row in rows.Where(r => r.shadowPrepareMs > .05).OrderByDescending(r => r.shadowPrepareMs).Take(2)) selected.Add(row.frame);
            var editor = rows.OrderByDescending(r => r.editorMs).First(); selected.Add(editor.frame);
            var gc = rows.OrderByDescending(r => r.gcMs + r.incrementalMs).First();
            if (gc.gcMs + gc.incrementalMs > .05)
                foreach (var row in rows) if (Math.Abs((long)row.frame - gc.frame) <= 1) selected.Add(row.frame);
            return selected.OrderBy(frame => frame).ToArray();
        }

        private static void SaveFullSpikes(string captureLabel, int first, int last, double duration)
        {
            if (ProfilerDriver.enabled) throw new InvalidOperationException("Stop recording before raw export and analysis.");
            var watch = Stopwatch.StartNew();
            var rawPath = Path.Combine(Output, captureLabel + ".data");
            var rawSaved = ProfilerDriver.SaveProfile(rawPath);
            var saveMs = watch.Elapsed.TotalMilliseconds;
            var rows = new List<FullSpikeRow>();
            var names = new Dictionary<int, string>();
            var report = new FullSpikeReport { rawProfilePath = rawPath, rawSaved = rawSaved,
                profilerWasEnabled = wasEnabled, editorWasEnabled = wasEditor, recordingWallSeconds = duration,
                requestedFirst = first, requestedLast = last, overwrittenFrames = Math.Max(0, ProfilerDriver.firstFrameIndex - first), saveRawMs = saveMs };
            for (var frame = Math.Max(first, ProfilerDriver.firstFrameIndex); frame <= last; frame++)
            {
                using var data = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (!data.valid) { report.invalidFrames++; continue; }
                var row = new FullSpikeRow { frame = frame, samples = data.sampleCount, startMs = data.frameStartTimeMs, frameMs = data.frameTimeMs };
                for (var index = 0; index < data.sampleCount; index++)
                {
                    var marker = data.GetSampleMarkerId(index);
                    if (!names.TryGetValue(marker, out var name)) names.Add(marker, name = data.GetSampleName(index));
                    var ms = data.GetSampleTimeMs(index);
                    if (name == "PlayerLoop") row.playerMs += ms;
                    if (name == "EditorLoop") row.editorMs += ms;
                    if (name == "GC.Collect") row.gcMs += ms;
                    if (name == "GarbageCollector.CollectIncremental") row.incrementalMs += ms;
                    if (name == "Profiler.FlushCounters") row.profilerMs += ms;
                    if (name == "RenderPlayModeViewCameras") row.cameraMs += ms;
                    if (name == "Godmist.Lighting.PrepareDraws") row.shadowPrepareMs += ms;
                }
                rows.Add(row);
            }
            var peaks = new List<FullSpikeDetail>();
            foreach (var frame in SelectFullSpikeFrames(rows))
            {
                var threads = new List<FullSpikeThread>();
                var detail = new FullSpikeDetail { frame = frame };
                for (var thread = 0; thread < 128; thread++)
                {
                    using var data = ProfilerDriver.GetRawFrameDataView(frame, thread);
                    if (!data.valid) break;
                    var full = thread == 0 || data.threadName.IndexOf("Render", StringComparison.OrdinalIgnoreCase) >= 0
                        || data.threadName.IndexOf("GfxDevice", StringComparison.OrdinalIgnoreCase) >= 0;
                    var nodes = ReadFullSpikeNodes(data, full, names);
                    threads.Add(new FullSpikeThread { thread = thread, name = data.threadName, group = data.threadGroupName,
                        startMs = data.frameStartTimeMs, frameMs = data.frameTimeMs, fullTimedHierarchy = full, nodes = nodes });
                    if (thread == 127) detail.threadLimitReached = true;
                }
                detail.threads = threads.ToArray(); peaks.Add(detail);
            }
            report.frames = rows.ToArray(); report.peaks = peaks.ToArray();
            // Diagnostic inventory only, after recording has stopped. It is
            // export-time context, not a claim about every captured frame.
            report.camerasAtExport = Camera.allCameras.Select(camera => new FullSpikeCamera {
                name = camera.name, type = camera.cameraType.ToString(), target = camera.targetTexture != null ? camera.targetTexture.name : "screen",
                pixelWidth = camera.pixelWidth, pixelHeight = camera.pixelHeight, cullingMask = camera.cullingMask,
                depth = camera.depth, orthographicSize = camera.orthographicSize,
                orthographic = camera.orthographic, occlusionCulling = camera.useOcclusionCulling }).ToArray();
            report.analysisMs = watch.Elapsed.TotalMilliseconds - saveMs;
            File.WriteAllText(Path.Combine(Output, captureLabel + ".json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + captureLabel + ": " + rows.Count
                + " frames incl Editor; " + peaks.Count + " peak hierarchies; native raw saved=" + rawSaved);
        }

        private static FullSpikeNode[] ReadFullSpikeNodes(RawFrameDataView data, bool full, Dictionary<int, string> names)
        {
            var nodes = new List<FullSpikeNode>();
            var parents = new List<FullSpikeNode>(64);
            for (var index = 0; index < data.sampleCount; index++)
            {
                CloseFullSpikeParents(parents, index);
                var marker = data.GetSampleMarkerId(index);
                if (!names.TryGetValue(marker, out var name)) names.Add(marker, name = data.GetSampleName(index));
                var parent = parents.Count > 0 ? parents[parents.Count - 1] : null;
                if (name == "GC.Alloc")
                {
                    if (parent != null)
                    {
                        parent.allocationCalls++;
                        if (data.GetSampleMetadataCount(index) > 0) parent.allocationBytes += data.GetSampleMetadataAsLong(index, 0);
                    }
                    continue;
                }
                var ms = data.GetSampleTimeMs(index);
                var self = (double)ms;
                var child = index + 1;
                for (var count = 0; count < data.GetSampleChildrenCount(index); count++)
                { self -= data.GetSampleTimeMs(child); child += 1 + data.GetSampleChildrenCountRecursive(child); }
                var node = new FullSpikeNode { index = index, parent = parent?.index ?? -1,
                    end = index + 1 + data.GetSampleChildrenCountRecursive(index), name = name,
                    startMs = data.GetSampleStartTimeMs(index) - data.frameStartTimeMs, totalMs = ms,
                    rawSelfMs = self, selfMs = Math.Max(0, self), flags = (int)data.GetSampleFlags(index) };
                // Keep every timed ancestor in main/render trees. Other thread
                // detail is compact; their complete tree remains in .data.
                if (full || parent == null || self >= .5) nodes.Add(node);
                if (node.end > index + 1) parents.Add(node);
            }
            return nodes.ToArray();
        }
    }
}
