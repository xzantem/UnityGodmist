using System;

namespace Godmist.Runtime.Dungeons
{
    // One observer's suspicion of the whole party. Inactive floors do not advance time.
    public sealed class DungeonAwarenessState
    {
        public const float DetectionSeconds = 2f;
        public const float DecaySeconds = 3f;
        public float Progress { get; private set; }
        public bool IsDetected => Progress >= 1f;

        public bool TryRestore(float progress)
        {
            if (!float.IsFinite(progress) || progress < 0f || progress > 1f) return false;
            Progress = progress;
            return true;
        }

        public bool Advance(float elapsedSeconds, bool seesParty, bool isFighting)
        {
            if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (isFighting) { Progress = 0f; return seesParty; }
            Progress = Math.Clamp(Progress + elapsedSeconds * (seesParty ? 1f / DetectionSeconds : -1f / DecaySeconds), 0f, 1f);
            return seesParty && IsDetected;
        }
    }
}
