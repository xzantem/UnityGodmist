using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    // Explicit, bounded capture in the existing Editor. Never changes scenes,
    // Play Mode, deep profiling, quality settings or the user's saved game.
    [InitializeOnLoad]
    internal static partial class DungeonLiveFrameCapture
    {
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/dungeon-whole-frame-20261016"));
        static bool capturing, wasEnabled, wasEditor;
        static int firstFrame;
        static double deadline, nextCheck;
        static string label;
        static bool combatEntryCapture;
        static int lastCollectedFrame, entryFrame;
        static readonly List<double> entryFrameTimes = new();
        static readonly List<FrameReport> entryFrames = new();
        static FrameReport worstFrame;
        static bool explorationSpikeCapture;
        static bool fullSpikeCapture;
        static double captureStarted, analysisMs, longestAnalysisMs;
        static int overwrittenFrames;
        static readonly List<SpikeFrame> spikeFrames = new();
        static readonly List<FrameReport> cpuPeaks = new(), wallPeaks = new();
        static readonly Dictionary<int, string> spikeNames = new();
        static readonly Dictionary<string, Sample> spikeTotals = new();
        static double navigationDeadline;
        static int navigationReports;
        static readonly string NavigationOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/combat-room-doors-20261020"));

        static void StopNavigationCapture()
        {
            Application.logMessageReceived -= CaptureNavigationFailure;
            navigationDeadline = 0;
        }

        static void CheckNavigationRequest()
        {
            if (navigationDeadline > 0 && (!EditorApplication.isPlaying
                || EditorApplication.timeSinceStartup >= navigationDeadline))
            {
                StopNavigationCapture();
                File.WriteAllText(Path.Combine(NavigationOutput, "navigation-state.txt"),
                    "Finished: " + navigationReports + " AI stop snapshots");
            }
            var request = Path.Combine(NavigationOutput, "navigation.request");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling || !File.Exists(request)) return;
            File.Delete(request);
            StopNavigationCapture();
            navigationReports = 0;
            navigationDeadline = EditorApplication.timeSinceStartup + 180;
            File.WriteAllText(Path.Combine(NavigationOutput, "live-navigation.txt"), "Combat navigation capture\n");
            File.WriteAllText(Path.Combine(NavigationOutput, "navigation-state.txt"), "Armed for 180 seconds");
            Application.logMessageReceived += CaptureNavigationFailure;
        }

        static void CaptureNavigationFailure(string message, string stackTrace, LogType type)
        {
            // Listen only during an explicit, bounded request. Read the session at the
            // existing AI stop checkpoint; never execute an action or advance contact memory.
            if (!message.StartsWith("[CombatPresentationSnapshot]", StringComparison.Ordinal)
                || !(message.Contains("phase=ai-no-action ") || message.Contains("aiJobState=canceled "))) return;
            try
            {
                var game = Godmist.Views.Game.GameBootstrap.Instance?.Session;
                var session = game?.Combats.FirstOrDefault(value => value.ActiveUnit?.Character != null
                    && message.Contains("unit=" + value.ActiveUnit.Character.CharacterId + " "));
                if (session == null) return;
                if (session.ActiveUnit.IsPlayerSide || session.ActiveUnit.CurrentActionPoints <= 0 || session.ActiveUnit.CurrentActionPoints
                    < Godmist.Systems.Combat.CombatMovementSystem.CalculatePathCost(session.ActiveUnit,1)) return;
                var text = new System.Text.StringBuilder().AppendLine(message);
                if (game.ActiveDungeon != null && game.ActiveDungeon.TryGetCurrentFloor(out var floor))
                {
                    text.AppendLine($"dungeon={game.ActiveDungeon.DungeonId} seed={game.ActiveDungeon.Seed} floor={game.ActiveDungeon.CurrentFloorIndex}");
                    foreach (var room in floor.Rooms)
                        text.AppendLine($"room={room.RoomId} template={room.TemplateId} origin={room.LayoutPosition} rotation={room.RotationDegrees}");
                    foreach (var door in floor.Objects.OfType<Godmist.Runtime.Dungeons.DoorDungeonObjectInstance>())
                        if (session.MapObjects.Any(value => value.ObjectId == door.InstanceId))
                            text.AppendLine($"floorDoor={door.InstanceId} edge={door.Edge.FirstCell}->{door.Edge.SecondCell} open={door.IsOpen}");
                }
                text.Append(BuildCombatNavigationReport(session));
                File.AppendAllText(Path.Combine(NavigationOutput, "live-navigation.txt"), text.ToString() + "\n");
                File.WriteAllText(Path.Combine(NavigationOutput, "navigation-state.txt"),
                    "Captured " + ++navigationReports + " AI stop snapshots");
                if (navigationReports >= 12) StopNavigationCapture();
            }
            catch (Exception exception)
            {
                StopNavigationCapture();
                File.WriteAllText(Path.Combine(NavigationOutput, "navigation-state.txt"), exception.ToString());
            }
        }

        internal static string BuildCombatNavigationReport(Godmist.Runtime.Combat.CombatSession session)
        {
            var text = new System.Text.StringBuilder();
            string Cell(Godmist.Runtime.Combat.GridPosition position) => $"({position.X},{position.Y},{position.Level},{position.StairStep})";
            var enemy = session.ActiveUnit;
            text.AppendLine($"combat={session.CombatId} bounds={session.DungeonContext?.WorldBounds} pending={session.MembershipChangePending}");
            foreach (var unit in session.Units)
            {
                text.AppendLine($"unit={unit.Character?.CharacterId} definition={unit.Character?.Definition?.Id} player={unit.IsPlayerSide} cell={Cell(unit.Position)} life={unit.Character?.LifeState} HP={unit.Character?.CurrentHealth} AP={unit.CurrentActionPoints}/{unit.MaxActionPoints} steps={unit.MovementStepsThisTurn} seenByActive={session.Contact?.Sees(enemy,unit)}");
                if (unit.Character != null)
                    text.AppendLine(" skills=" + string.Join(",",unit.Character.GetActiveSkills().Select(value=>value.SkillId))
                        + " statuses=" + string.Join(",",unit.Character.PassiveEffects.Select(value=>$"{value.EffectId}:{value.RemainingDuration}")));
            }
            foreach (var item in session.MapObjects)
                if (item is Godmist.Runtime.Combat.DoorCombatMapObject door)
                    text.AppendLine($"door={door.ObjectId} {Cell(door.Edge.FirstCell)}->{Cell(door.Edge.SecondCell)} open={door.IsOpen} mode={door.InteractionMode} wall={session.Grid.HasWallEdge(door.Edge.FirstCell,door.Edge.SecondCell)} traversable={session.Grid.CanMoveBetween(door.Edge.FirstCell,door.Edge.SecondCell)}");
            if (enemy != null)
                foreach (var cell in session.GetNavigationNeighbours(enemy.Position))
                    text.AppendLine($"neighbour={Cell(cell)} walkable={session.Grid.IsWalkable(cell)} blocked={session.Grid.IsBlocked(cell)} occupied={session.IsPositionOccupied(cell,enemy)} objectBlocked={session.DoesObjectBlockMovement(session.GetMapObjectAt(cell))} canMove={session.CanMoveBetween(enemy,enemy.Position,cell)} directDoor={session.FindClosedDirectDoor(enemy.Position,cell)?.ObjectId}");
            if (enemy != null)
            {
                var reachable = new HashSet<Godmist.Runtime.Combat.GridPosition> { enemy.Position };
                var pending = new Queue<Godmist.Runtime.Combat.GridPosition>();
                pending.Enqueue(enemy.Position);
                while (pending.Count > 0 && reachable.Count < 4096)
                {
                    var from = pending.Dequeue();
                    foreach (var to in session.GetNavigationNeighbours(from))
                    {
                        if (reachable.Contains(to) || !session.Grid.IsWalkable(to) || session.Grid.IsBlocked(to)
                            || session.IsPositionOccupied(to,enemy) || session.DoesObjectBlockMovement(session.GetMapObjectAt(to))
                            || !session.CanMoveBetween(enemy,from,to) && session.FindClosedDirectDoor(from,to) == null) continue;
                        reachable.Add(to);pending.Enqueue(to);
                    }
                }
                text.AppendLine($"openableDoorReachability={reachable.Count} truncated={pending.Count > 0}");
                foreach (var player in session.Units.Where(value => value.IsPlayerSide))
                {
                    var adjacent = reachable.Any(from => Math.Abs(from.X-player.Position.X)+Math.Abs(from.Y-player.Position.Y)==1
                        && (session.CanMoveBetween(enemy,from,player.Position) || session.FindClosedDirectDoor(from,player.Position)!=null));
                    text.AppendLine($"target={player.Character?.CharacterId} reachableAdjacent={adjacent}");
                }
            }
            // Diagnostic reflection is read-only and Editor-only: TryGetSearchPosition
            // would advance/consume the live observation, so it must not be called here.
            var memory = session.Contact == null ? null : typeof(Godmist.Runtime.Combat.DungeonContactState).GetField("memory",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(session.Contact) as System.Collections.IDictionary;
            if (memory != null)
                foreach (System.Collections.DictionaryEntry entry in memory)
                {
                    var pair = ((Godmist.Runtime.Combat.BattleUnit Enemy, Godmist.Runtime.Combat.BattleUnit Target))entry.Key;
                    if (pair.Enemy != enemy) continue;
                    var observation = entry.Value;
                    object Field(string name) => observation.GetType().GetField(name)?.GetValue(observation);
                    text.AppendLine($"memory={pair.Target.Character?.CharacterId} last={Cell((Godmist.Runtime.Combat.GridPosition)Field("Position"))} search={Cell((Godmist.Runtime.Combat.GridPosition)Field("SearchPosition"))} direction={Field("Direction")} forward={Field("ForwardSteps")} checked={Field("Checked")}");
                }
            return text.ToString();
        }

        static DungeonLiveFrameCapture()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }

        static void Restore()
        {
            StopNavigationCapture();
            if (!capturing) return;
            ProfilerDriver.enabled = wasEnabled;
            ProfilerDriver.profileEditor = wasEditor;
            capturing = false;
        }

        static void Tick()
        {
            if (!capturing)
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 1;
                CheckNavigationRequest();
                var request = Path.Combine(Output, "capture.request");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling || !File.Exists(request)) return;
                label = Path.GetFileName(File.ReadAllText(request).Trim());
                File.Delete(request);
                wasEnabled = ProfilerDriver.enabled;
                wasEditor = ProfilerDriver.profileEditor;
                fullSpikeCapture = label.StartsWith("full-spikes-", StringComparison.Ordinal);
                ProfilerDriver.profileEditor = fullSpikeCapture;
                ProfilerDriver.enabled = true;
                firstFrame = ProfilerDriver.lastFrameIndex + 2;
                combatEntryCapture = label.StartsWith("combat-entry-", StringComparison.Ordinal);
                explorationSpikeCapture = label.StartsWith("exploration-spikes-", StringComparison.Ordinal);
                lastCollectedFrame = firstFrame - 1;
                entryFrame = -1;
                entryFrameTimes.Clear(); entryFrames.Clear(); worstFrame = null;
                spikeFrames.Clear(); cpuPeaks.Clear(); wallPeaks.Clear();
                spikeNames.Clear(); spikeTotals.Clear();
                analysisMs = longestAnalysisMs = 0; overwrittenFrames = 0;
                captureStarted = EditorApplication.timeSinceStartup;
                deadline = captureStarted + (combatEntryCapture ? 60 : 12);
                capturing = true;
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Capturing " + label);
                return;
            }
            if (explorationSpikeCapture)
            {
                try
                {
                    var started = System.Diagnostics.Stopwatch.GetTimestamp();
                    CollectExplorationSpikes();
                    var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    analysisMs += elapsed; longestAnalysisMs = Math.Max(longestAnalysisMs, elapsed);
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused
                        && EditorApplication.timeSinceStartup < deadline && spikeFrames.Count < 5000) return;
                    var ordered = spikeFrames.OrderBy(f => f.frameMs).ToArray();
                    var p95 = ordered.Length == 0 ? null : ordered[Math.Max(0, (int)Math.Ceiling(ordered.Length * .95) - 1)];
                    File.WriteAllText(Path.Combine(Output, label + ".json"), JsonUtility.ToJson(new SpikeReport {
                        frames = spikeFrames.ToArray(), p95Frame = p95,
                        slowestGameFrames = cpuPeaks.OrderByDescending(f => f.playerLoopMs).ToArray(),
                        slowestWallFrames = wallPeaks.OrderByDescending(f => f.frameMs).ToArray(),
                        profilerWasEnabled = wasEnabled, profilerEnabledDuringCapture = true,
                        captureWallSeconds = EditorApplication.timeSinceStartup - captureStarted,
                        collectorTotalMs = analysisMs, longestCollectorBatchMs = longestAnalysisMs,
                        overwrittenProfilerFrames = overwrittenFrames
                    }, true));
                    File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + label + ": " + spikeFrames.Count
                        + " gameplay frames; separate per-frame costs and CPU/Editor peaks saved");
                }
                catch (Exception exception)
                { File.WriteAllText(Path.Combine(Output, "state.txt"), exception.ToString()); }
                Restore();
                return;
            }
            if (combatEntryCapture)
            {
                try
                {
                    CollectCombatEntryFrames();
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused
                        && EditorApplication.timeSinceStartup < deadline
                        && (entryFrame < 0 || lastCollectedFrame < entryFrame + 12)) return;
                    File.WriteAllText(Path.Combine(Output, label + ".json"), JsonUtility.ToJson(new EntryReport {
                        frames = entryFrameTimes.ToArray(), entryFrame = entryFrame,
                        entryWindow = entryFrames.ToArray(), worstFrame = worstFrame,
                        entryObserved = entryFrame >= 0, profilerWasEnabled = wasEnabled
                    }, true));
                    File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + label
                        + ": " + entryFrameTimes.Count + " frames; entry observed=" + (entryFrame >= 0));
                }
                catch (Exception exception)
                { File.WriteAllText(Path.Combine(Output, "state.txt"), exception.ToString()); }
                Restore();
                return;
            }
            // Bound the diagnostic below the Editor's default history capacity;
            // do not change the user's history/memory preferences.
            var frameLimit = fullSpikeCapture ? 180 : 90;
            if (EditorApplication.isPlaying && !EditorApplication.isPaused && EditorApplication.timeSinceStartup < deadline
                && ProfilerDriver.lastFrameIndex - firstFrame < frameLimit) return;
            try
            {
                if (fullSpikeCapture)
                {
                    var lastCompleted = ProfilerDriver.lastFrameIndex - 1;
                    var duration = EditorApplication.timeSinceStartup - captureStarted;
                    Restore();
                    ProfilerDriver.enabled = false;
                    try { SaveFullSpikes(label, firstFrame, lastCompleted, duration); }
                    finally { ProfilerDriver.profileEditor = wasEditor; ProfilerDriver.enabled = wasEnabled; }
                    return;
                }
                if (label.StartsWith("behaviour-gc-", StringComparison.Ordinal))
                {
                    var lastCompleted = ProfilerDriver.lastFrameIndex - 1;
                    var duration = EditorApplication.timeSinceStartup - captureStarted;
                    // Stop recording BEFORE walking samples or allocating reports.
                    // Analysis allocations must not be charged to measured gameplay.
                    Restore();
                    ProfilerDriver.enabled = false;
                    try { SaveBehaviourGc(label, Math.Max(firstFrame, lastCompleted - 89), lastCompleted, duration, wasEnabled); }
                    finally { ProfilerDriver.enabled = wasEnabled; }
                    return;
                }
                var frames = new List<double>();
                var totals = new Dictionary<string, Sample>();
                var last = ProfilerDriver.lastFrameIndex - 1;
                for (var frame = Math.Max(firstFrame, last - 89); frame <= last; frame++)
                {
                    using var data = ProfilerDriver.GetRawFrameDataView(frame, 0);
                    if (!data.valid) continue;
                    // Editor/background-only frames are not gameplay frames.
                    var gameplay = false;
                    for (var i = 0; i < data.sampleCount; i++)
                        if (data.GetSampleName(i) == "PlayerLoop" && data.GetSampleTimeMs(i) > .1f)
                        { gameplay = true; break; }
                    if (!gameplay) continue;
                    frames.Add(data.frameTimeMs);
                    for (var i = 0; i < data.sampleCount; i++)
                    {
                        var name = data.GetSampleName(i);
                        var time = data.GetSampleTimeMs(i);
                        var self = time;
                        var child = i + 1;
                        for (var n = 0; n < data.GetSampleChildrenCount(i); n++)
                        {
                            self -= data.GetSampleTimeMs(child);
                            child += 1 + data.GetSampleChildrenCountRecursive(child);
                        }
                        if (!totals.TryGetValue(name, out var sample)) totals.Add(name, sample = new Sample { name = name });
                        sample.totalMs += time; sample.selfMs += Math.Max(0, self); sample.calls++;
                    }
                }
                frames.Sort();
                File.WriteAllText(Path.Combine(Output, label + ".json"), JsonUtility.ToJson(new Report {
                    frames = frames.ToArray(), samples = totals.Values.OrderByDescending(s => s.selfMs).ToArray()
                }, true));
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished " + label + ": " + frames.Count + " frames");
            }
            catch (Exception exception)
            { File.WriteAllText(Path.Combine(Output, "state.txt"), exception.ToString()); }
            finally
            {
                Restore();
            }
        }
        static void CollectExplorationSpikes()
        {
            var first = ProfilerDriver.firstFrameIndex;
            if (lastCollectedFrame + 1 < first) overwrittenFrames += first - lastCollectedFrame - 1;
            var last = ProfilerDriver.lastFrameIndex - 1;
            for (var frame = Math.Max(lastCollectedFrame + 1, first); frame <= last && spikeFrames.Count < 5000; frame++)
            {
                lastCollectedFrame = frame;
                using var data = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (!data.valid) continue;
                spikeTotals.Clear();
                var gameMs = 0d;
                var editorMs = 0d;
                for (var i = 0; i < data.sampleCount; i++)
                {
                    var marker = data.GetSampleMarkerId(i);
                    if (!spikeNames.TryGetValue(marker, out var name)) spikeNames.Add(marker, name = data.GetSampleName(i));
                    var time = data.GetSampleTimeMs(i);
                    if (name == "PlayerLoop") gameMs += time;
                    if (name == "EditorLoop") editorMs += time;
                    if (!(name.StartsWith("Godmist.", StringComparison.Ordinal)
                        || name.Contains(" [Invoke]") || name.StartsWith("GC.", StringComparison.Ordinal)
                        || name.StartsWith("Physics", StringComparison.Ordinal)
                        || name.StartsWith("Gfx.", StringComparison.Ordinal)
                        || name.StartsWith("Wait", StringComparison.Ordinal)
                        || name.StartsWith("Profiler.", StringComparison.Ordinal)
                        || name == "PlayerLoop" || name == "EditorLoop"
                        || name == "BehaviourUpdate" || name == "LateBehaviourUpdate"
                        || name == "UGUI.Rendering.UpdateBatches" || name == "CanvasUpdate.PreRender"
                        || name == "RenderPlayModeViewCameras" || name == "Mono.JIT")) continue;
                    var self = time;
                    var child = i + 1;
                    for (var n = 0; n < data.GetSampleChildrenCount(i); n++)
                    {
                        self -= data.GetSampleTimeMs(child);
                        child += 1 + data.GetSampleChildrenCountRecursive(child);
                    }
                    if (!spikeTotals.TryGetValue(name, out var sample)) spikeTotals.Add(name, sample = new Sample { name = name });
                    sample.totalMs += time; sample.selfMs += Math.Max(0, self); sample.calls++;
                }
                if (gameMs <= .1d) continue;
                spikeFrames.Add(new SpikeFrame { frame = frame, frameMs = data.frameTimeMs,
                    playerLoopMs = gameMs, editorLoopMs = editorMs,
                    samples = spikeTotals.Values.Where(s => s.totalMs >= .02d).OrderByDescending(s => s.totalMs).ToArray() });
                var cpuCandidate = cpuPeaks.Count < 5 || gameMs > cpuPeaks.Min(f => f.playerLoopMs);
                var wallCandidate = wallPeaks.Count < 5 || data.frameTimeMs > wallPeaks.Min(f => f.frameMs);
                if (!cpuCandidate && !wallCandidate) continue;
                var details = new List<Sample>();
                for (var i = 0; i < data.sampleCount; i++)
                {
                    var time = data.GetSampleTimeMs(i);
                    if (time < .05f) continue;
                    var self = time;
                    var child = i + 1;
                    for (var n = 0; n < data.GetSampleChildrenCount(i); n++)
                    {
                        self -= data.GetSampleTimeMs(child);
                        child += 1 + data.GetSampleChildrenCountRecursive(child);
                    }
                    var marker = data.GetSampleMarkerId(i);
                    if (!spikeNames.TryGetValue(marker, out var name)) spikeNames.Add(marker, name = data.GetSampleName(i));
                    details.Add(new Sample { name = name, totalMs = time, selfMs = Math.Max(0, self), calls = 1 });
                }
                var full = new FrameReport { frame = frame, frameMs = data.frameTimeMs, playerLoopMs = gameMs,
                    samples = details.OrderByDescending(s => s.totalMs).ToArray() };
                if (cpuCandidate)
                {
                    cpuPeaks.Add(full);
                    if (cpuPeaks.Count > 5) cpuPeaks.Remove(cpuPeaks.OrderBy(f => f.playerLoopMs).First());
                }
                if (wallCandidate)
                {
                    wallPeaks.Add(full);
                    if (wallPeaks.Count > 5) wallPeaks.Remove(wallPeaks.OrderBy(f => f.frameMs).First());
                }
            }
        }
        // Read completed frames continuously, before the Profiler ring buffer can overwrite
        // the entry spike. Detailed samples are retained only for the peak and entry window.
        static void CollectCombatEntryFrames()
        {
            var last = ProfilerDriver.lastFrameIndex - 1;
            for (var frame = Math.Max(lastCollectedFrame + 1, ProfilerDriver.firstFrameIndex); frame <= last; frame++)
            {
                lastCollectedFrame = frame;
                using var data = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (!data.valid) continue;
                var gameplayMs = 0f;
                var startsCombat = false;
                for (var i = 0; i < data.sampleCount; i++)
                {
                    var name = data.GetSampleName(i);
                    if (name == "PlayerLoop") gameplayMs += data.GetSampleTimeMs(i);
                    if (name.StartsWith("Godmist.Dungeon.CombatEntry.", StringComparison.Ordinal)) startsCombat = true;
                }
                if (gameplayMs <= .1f) continue;
                if (startsCombat && entryFrame < 0) entryFrame = frame;
                entryFrameTimes.Add(data.frameTimeMs);
                var inEntryWindow = entryFrame >= 0 && frame <= entryFrame + 12;
                var isWorst = worstFrame == null || data.frameTimeMs > worstFrame.frameMs;
                if (!inEntryWindow && !isWorst) continue;
                var samples = new List<Sample>();
                for (var i = 0; i < data.sampleCount; i++)
                {
                    var time = data.GetSampleTimeMs(i);
                    if (time < .05f) continue;
                    var self = time;
                    var child = i + 1;
                    for (var n = 0; n < data.GetSampleChildrenCount(i); n++)
                    {
                        self -= data.GetSampleTimeMs(child);
                        child += 1 + data.GetSampleChildrenCountRecursive(child);
                    }
                    samples.Add(new Sample { name = data.GetSampleName(i), totalMs = time,
                        selfMs = Math.Max(0, self), calls = 1 });
                }
                var report = new FrameReport { frame = frame, frameMs = data.frameTimeMs,
                    playerLoopMs = gameplayMs, startsCombat = startsCombat,
                    samples = samples.OrderByDescending(s => s.totalMs).ToArray() };
                if (isWorst) worstFrame = report;
                if (inEntryWindow) entryFrames.Add(report);
            }
        }
        [Serializable] sealed class Sample { public string name; public double totalMs, selfMs; public int calls; }
        [Serializable] sealed class Report { public double[] frames; public Sample[] samples; }
        [Serializable] sealed class FrameReport { public int frame; public double frameMs, playerLoopMs; public bool startsCombat; public Sample[] samples; }
        [Serializable] sealed class EntryReport { public double[] frames; public int entryFrame; public bool entryObserved, profilerWasEnabled;
            public FrameReport worstFrame; public FrameReport[] entryWindow; }
        [Serializable] sealed class SpikeFrame { public int frame; public double frameMs, playerLoopMs, editorLoopMs; public Sample[] samples; }
        [Serializable] sealed class SpikeReport { public SpikeFrame[] frames; public SpikeFrame p95Frame;
            public FrameReport[] slowestGameFrames, slowestWallFrames;
            public bool profilerWasEnabled, profilerEnabledDuringCapture;
            public double captureWallSeconds, collectorTotalMs, longestCollectorBatchMs;
            public int overwrittenProfilerFrames; }
    }
}
