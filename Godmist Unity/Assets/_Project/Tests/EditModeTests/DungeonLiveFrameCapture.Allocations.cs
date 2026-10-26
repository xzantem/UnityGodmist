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
        internal readonly struct AllocationScope
        {
            internal readonly string Name;
            internal readonly int End;
            internal AllocationScope(string name, int end) { Name = name; End = end; }
        }

        internal static void CloseAllocationScopes(List<AllocationScope> scopes, int index)
        {
            while (scopes.Count > 0 && index >= scopes[scopes.Count - 1].End) scopes.RemoveAt(scopes.Count - 1);
        }

        internal static (bool Gameplay, string Callback, string Operation) AllocationContext(List<AllocationScope> scopes)
        {
            bool gameplay = false; string callback = "(no callback marker)";
            foreach (var scope in scopes)
            {
                if (scope.Name == "PlayerLoop") gameplay = true;
                if (scope.Name.EndsWith(" [Invoke]", StringComparison.Ordinal)) callback = scope.Name;
            }
            return (gameplay, callback, scopes.Count == 0 ? "(no parent marker)" : scopes[scopes.Count - 1].Name);
        }

        [Serializable] sealed class AllocationOwner
        {
            public string callback, operation;
            public long bytes;
            public int calls, missingByteMetadata;
        }
        [Serializable] sealed class BehaviourGcFrame
        {
            public int frame, rawSampleCount, gameplayAllocationCalls;
            public double frameMs, playerLoopMs, behaviourMs, gcCollectMs, incrementalGcMs;
            public long gameplayAllocationBytes, otherAllocationBytes;
            public Sample[] callbacks, slowSamples;
            public AllocationOwner[] allocationOwners;
        }
        [Serializable] sealed class BehaviourGcReport
        {
            public BehaviourGcFrame[] frames;
            public AllocationOwner[] allocationOwners;
            public double recordingWallSeconds, analysisMs;
            public bool profilerWasEnabled, profilerEnabledDuringCapture = true, analysisAfterRecordingStopped = true;
            public int requestedFirstFrame, requestedLastFrame, invalidFrames, nonGameplayFrames;
            public string scope = "main thread only; callback/parent markers are owners, not managed allocation call stacks";
        }

        private static void SaveBehaviourGc(string captureLabel, int first, int last, double duration, bool previousProfiler)
        {
            var watch = Stopwatch.StartNew();
            var frames = new List<BehaviourGcFrame>(90);
            var names = new Dictionary<int, string>();
            var scopes = new List<AllocationScope>(64);
            var totals = new Dictionary<(string Callback, string Operation), AllocationOwner>();
            int invalid = 0, nonGameplay = 0;
            for (int index = first; index <= last; index++)
            {
                using var data = ProfilerDriver.GetRawFrameDataView(index, 0);
                if (!data.valid) { invalid++; continue; }
                var frame = ReadBehaviourGcFrame(data, names, scopes);
                if (frame.playerLoopMs <= .1) nonGameplay++;
                frames.Add(frame);
                foreach (var owner in frame.allocationOwners)
                {
                    var key = (owner.callback, owner.operation);
                    if (!totals.TryGetValue(key, out var total)) totals.Add(key, total = new AllocationOwner { callback = owner.callback, operation = owner.operation });
                    total.bytes += owner.bytes; total.calls += owner.calls; total.missingByteMetadata += owner.missingByteMetadata;
                }
            }
            watch.Stop();
            var report = new BehaviourGcReport {
                frames = frames.ToArray(), allocationOwners = totals.Values.OrderByDescending(owner => owner.bytes).ToArray(),
                recordingWallSeconds = duration, analysisMs = watch.Elapsed.TotalMilliseconds,
                profilerWasEnabled = previousProfiler, requestedFirstFrame = first, requestedLastFrame = last,
                invalidFrames = invalid, nonGameplayFrames = nonGameplay
            };
            File.WriteAllText(Path.Combine(Output, captureLabel + ".json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + captureLabel + ": " + frames.Count
                + " profiled frames (" + (frames.Count - nonGameplay) + " gameplay); allocation bytes and Update owners; analysis after recording stopped");
        }

        private static BehaviourGcFrame ReadBehaviourGcFrame(RawFrameDataView data, Dictionary<int, string> names, List<AllocationScope> scopes)
        {
            var frame = new BehaviourGcFrame { frame = data.frameIndex, frameMs = data.frameTimeMs, rawSampleCount = data.sampleCount };
            var callbacks = new Dictionary<string, Sample>();
            var slow = new List<Sample>();
            var allocations = new Dictionary<(string Callback, string Operation), AllocationOwner>();
            scopes.Clear();
            for (int i = 0; i < data.sampleCount; i++)
            {
                CloseAllocationScopes(scopes, i);
                var marker = data.GetSampleMarkerId(i);
                if (!names.TryGetValue(marker, out var name)) names.Add(marker, name = data.GetSampleName(i));
                var context = AllocationContext(scopes);
                if (name == "GC.Alloc")
                {
                    bool hasBytes = data.GetSampleMetadataCount(i) > 0;
                    long bytes = hasBytes ? data.GetSampleMetadataAsLong(i, 0) : 0;
                    if (context.Gameplay)
                    {
                        frame.gameplayAllocationBytes += bytes; frame.gameplayAllocationCalls++;
                        var key = (context.Callback, context.Operation);
                        if (!allocations.TryGetValue(key, out var owner)) allocations.Add(key, owner = new AllocationOwner { callback = key.Callback, operation = key.Operation });
                        owner.bytes += bytes; owner.calls++; if (!hasBytes) owner.missingByteMetadata++;
                    }
                    else frame.otherAllocationBytes += bytes;
                }
                else
                {
                    var time = data.GetSampleTimeMs(i);
                    if (name == "PlayerLoop") frame.playerLoopMs += time;
                    if (name == "BehaviourUpdate") frame.behaviourMs += time;
                    // GC and waits outside PlayerLoop can still stall the next
                    // gameplay frame. Retain them instead of losing Editor gaps.
                    if (name == "GC.Collect") frame.gcCollectMs += time;
                    if (name == "GarbageCollector.CollectIncremental") frame.incrementalGcMs += time;
                    bool callback = context.Gameplay && name.EndsWith(" [Invoke]", StringComparison.Ordinal);
                    if (callback || time >= .5f)
                    {
                        var self = time; int child = i + 1;
                        for (int n = 0; n < data.GetSampleChildrenCount(i); n++)
                        { self -= data.GetSampleTimeMs(child); child += 1 + data.GetSampleChildrenCountRecursive(child); }
                        if (callback)
                        {
                            if (!callbacks.TryGetValue(name, out var sample)) callbacks.Add(name, sample = new Sample { name = name });
                            sample.totalMs += time; sample.selfMs += Math.Max(0, self); sample.calls++;
                        }
                        if (time >= .5f) slow.Add(new Sample { name = name, totalMs = time, selfMs = Math.Max(0, self), calls = 1 });
                    }
                }
                if (data.GetSampleChildrenCount(i) > 0)
                    scopes.Add(new AllocationScope(name, i + 1 + data.GetSampleChildrenCountRecursive(i)));
            }
            frame.callbacks = callbacks.Values.OrderByDescending(sample => sample.totalMs).ToArray();
            frame.slowSamples = slow.OrderByDescending(sample => sample.totalMs).Take(160).ToArray();
            frame.allocationOwners = allocations.Values.OrderByDescending(owner => owner.bytes).ToArray();
            return frame;
        }
    }
}
