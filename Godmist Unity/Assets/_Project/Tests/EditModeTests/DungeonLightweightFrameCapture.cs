using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    // One explicit 12-second capture, no profiler collection or helper process.
    [InitializeOnLoad]
    internal static class DungeonLightweightFrameCapture
    {
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/visibility-budget-pass-20261017"));
        static readonly List<double> frames = new();
        static readonly List<double> frameIntervals = new();
        static bool capturing, wasEnabled;
        static double nextCheck, deadline, captureStart, lastFrameTime;
        static int lastFrame, skip, missedFrames;
        static string label;
        static DungeonLightweightFrameCapture()
        { EditorApplication.update += Tick; AssemblyReloadEvents.beforeAssemblyReload += Restore; EditorApplication.quitting += Restore; }
        static void Restore()
        { if (capturing) { ProfilerDriver.enabled = wasEnabled; capturing = false; } }
        static void Tick()
        {
            if (!capturing)
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 1;
                var request = Path.Combine(Output, "lightweight.request");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling || !File.Exists(request)) return;
                label = Path.GetFileName(File.ReadAllText(request).Trim()); File.Delete(request);
                wasEnabled = ProfilerDriver.enabled; ProfilerDriver.enabled = false;
                frames.Clear(); frameIntervals.Clear(); lastFrame = Time.frameCount; skip = 2; missedFrames = 0;
                captureStart = lastFrameTime = EditorApplication.timeSinceStartup;
                deadline = captureStart + 12; capturing = true;
                File.WriteAllText(Path.Combine(Output, "lightweight-state.txt"), "Capturing " + label + " (Profiler disabled temporarily)");
                return;
            }
            if (Time.frameCount != lastFrame)
            {
                missedFrames += Mathf.Max(0, Time.frameCount - lastFrame - 1);
                lastFrame = Time.frameCount;
                var now = EditorApplication.timeSinceStartup;
                var interval = (now - lastFrameTime) * 1000d;
                lastFrameTime = now;
                if (skip > 0) skip--;
                else if (Time.unscaledDeltaTime > 0)
                { frames.Add(Time.unscaledDeltaTime * 1000d); frameIntervals.Add(interval); }
            }
            if (EditorApplication.isPlaying && !EditorApplication.isPaused && EditorApplication.timeSinceStartup < deadline && frames.Count < 2000) return;
            try
            {
                frames.Sort();
                frameIntervals.Sort();
                File.WriteAllText(Path.Combine(Output, label + ".json"), JsonUtility.ToJson(new Report { frames = frames.ToArray(), profilerEnabledDuringCapture = false,
                    frameIntervalsMs = frameIntervals.ToArray(), captureWallSeconds = EditorApplication.timeSinceStartup - captureStart,
                    profilerWasEnabled = wasEnabled, missedFrameSamples = missedFrames, targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount }, true));
                File.WriteAllText(Path.Combine(Output, "lightweight-state.txt"), "Finished " + label + ": " + frames.Count + " frames");
            }
            finally { Restore(); }
        }
        [Serializable] sealed class Report { public double[] frames, frameIntervalsMs; public double captureWallSeconds; public bool profilerEnabledDuringCapture, profilerWasEnabled; public int missedFrameSamples, targetFrameRate, vSyncCount; }
    }
}
