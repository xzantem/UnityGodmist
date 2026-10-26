using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Dungeons
{
    /// <summary>Visibility calculation; publishes complete results only, on the owner's thread.</summary>
    public sealed class DungeonFogUpdateJob : IDisposable
    {
        private readonly DungeonFloorInstance floor;
        private readonly DungeonFloorMap map;
        private readonly int mapRevision, debugRevision, fogRevision;
        private readonly IReadOnlyList<DungeonTraversalPosition> sourceObservers;
        private readonly DungeonTraversalPosition[] observers;
        private readonly List<(DungeonObjectInstance Object, bool Active, bool State)> objects = new();
        private readonly HashSet<DungeonTraversalPosition> visible = new(), discovered = new();
        private readonly IEnumerator<bool> work;
        private readonly Task<bool> worker;
        private int canceled;
        private readonly Action<DungeonFloorInstance, HashSet<DungeonTraversalPosition>, HashSet<DungeonTraversalPosition>> apply;
        private readonly Func<bool> canContinue;
        private static readonly Unity.Profiling.ProfilerMarker SliceSample = new("Godmist.Dungeon.Fog.Slice");
        private static readonly Unity.Profiling.ProfilerMarker ApplySample = new("Godmist.Dungeon.Fog.Apply");

        public bool IsComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public bool Applied { get; private set; }

        internal DungeonFogUpdateJob(DungeonFloorInstance floor,
            IReadOnlyList<DungeonTraversalPosition> sourceObservers, DungeonVisibilitySystem visibility,
            Action<DungeonFloorInstance, HashSet<DungeonTraversalPosition>, HashSet<DungeonTraversalPosition>> apply,
            Func<bool> canContinue, DungeonVisibilitySystem.VisibilityInput input = null)
        {
            this.floor = floor;
            this.sourceObservers = sourceObservers;
            this.apply = apply;
            this.canContinue = canContinue;
            map = floor?.Map;
            mapRevision = map?.TraversalRevision ?? -1;
            debugRevision = floor?.Fog.DebugRevision ?? -1;
            fogRevision = floor?.Fog.Revision ?? -1;
            observers = new DungeonTraversalPosition[sourceObservers?.Count ?? 0];
            for (var i = 0; i < observers.Length; i++) observers[i] = sourceObservers[i];
            if (floor != null)
                foreach (var obj in floor.Objects)
                    objects.Add((obj, obj.IsActive, DungeonFogSystem.ReadState(obj)));
            if (input == null)
                work = visibility.CollectPartyVisibleWork(floor, observers, null, visible, discovered).GetEnumerator();
            else
                worker = Task.Run(() =>
                {
                    using var steps = DungeonVisibilitySystem.CollectPartyVisibleWork(input, observers,
                        null, visible, discovered).GetEnumerator();
                    while (Volatile.Read(ref canceled) == 0)
                        if (!steps.MoveNext()) return true;
                    return false;
                });
        }

        private bool IsCurrent()
        {
            if (map == null || !ReferenceEquals(map, floor.Map) || map.TraversalRevision != mapRevision
                || floor.Fog.DebugRevision != debugRevision || floor.Fog.Revision != fogRevision
                || floor.Objects.Count != objects.Count
                || (sourceObservers?.Count ?? 0) != observers.Length || !(canContinue?.Invoke() ?? true)) return false;
            for (var i = 0; i < observers.Length; i++)
                if (sourceObservers[i] != observers[i]) return false;
            for (var i = 0; i < objects.Count; i++)
            {
                var saved = objects[i];
                if (!ReferenceEquals(floor.Objects[i], saved.Object) || saved.Object.IsActive != saved.Active
                    || DungeonFogSystem.ReadState(saved.Object) != saved.State) return false;
            }
            return true;
        }

        public void AdvanceSlice(double milliseconds = 2, int maxSteps = 4096)
        {
            if (milliseconds <= 0 || !double.IsFinite(milliseconds)) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (maxSteps < 1) throw new ArgumentOutOfRangeException(nameof(maxSteps));
            if (IsComplete) return;
            using var sample = SliceSample.Auto();
            // The owner thread cannot mutate inputs within this call. Validate once
            // per slice rather than scanning every object before every visibility ray.
            if (!IsCurrent()) { Dispose(); return; }
            if (worker != null)
            {
                if (!worker.IsCompleted) return;
                if (!worker.GetAwaiter().GetResult()) { Dispose(); return; }
                using (ApplySample.Auto()) apply(floor, visible, discovered);
                Applied = true;
                IsComplete = true;
                return;
            }
            var started = Stopwatch.GetTimestamp();
            for (var step = 0; step < maxSteps; step++)
            {
                if (!work.MoveNext())
                {
                    using (ApplySample.Auto()) apply(floor, visible, discovered);
                    Applied = true;
                    IsComplete = true;
                    work.Dispose();
                    return;
                }
                if ((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency >= milliseconds) return;
            }
        }

        public void Dispose()
        {
            if (IsComplete) return;
            IsCanceled = true;
            IsComplete = true;
            Volatile.Write(ref canceled, 1);
            work?.Dispose();
            if (worker != null)
                _ = worker.ContinueWith(task => { _ = task.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
