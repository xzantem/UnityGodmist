using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    /// <summary>Transient presentation samples; never part of fog knowledge or save data.</summary>
    public sealed class DungeonFogTransition
    {
        public const float Duration = .3f;
        private readonly float[] current, start, target;
        private float startBaseline, targetBaseline, elapsed;
        public float Baseline { get; private set; }
        public bool Active { get; private set; }
        public float this[int index] => current[index];

        public DungeonFogTransition(float baseline, IReadOnlyList<float> samples)
        {
            current = new float[samples.Count]; start = new float[samples.Count]; target = new float[samples.Count];
            Baseline = startBaseline = targetBaseline = baseline;
            for (var i = 0; i < samples.Count; i++) current[i] = start[i] = target[i] = samples[i];
        }

        public void Retarget(float baseline, IReadOnlyList<float> samples, bool snap = false)
        {
            var changed = baseline != targetBaseline;
            for (var i = 0; i < target.Length; i++) changed |= target[i] != samples[i];
            if (!changed && !snap) return;
            startBaseline = Baseline; targetBaseline = baseline; elapsed = 0;
            for (var i = 0; i < target.Length; i++) { start[i] = current[i]; target[i] = samples[i]; }
            Active = !snap && changed;
            if (snap) { Baseline = baseline; for (var i = 0; i < target.Length; i++) current[i] = target[i]; }
        }

        public bool Advance(float deltaTime)
        {
            if (!Active) return false;
            elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0, deltaTime));
            var blend = Mathf.SmoothStep(0, 1, elapsed / Duration);
            Baseline = Mathf.Lerp(startBaseline, targetBaseline, blend);
            for (var i = 0; i < current.Length; i++) current[i] = Mathf.Lerp(start[i], target[i], blend);
            Active = elapsed < Duration;
            return true;
        }
    }
}
