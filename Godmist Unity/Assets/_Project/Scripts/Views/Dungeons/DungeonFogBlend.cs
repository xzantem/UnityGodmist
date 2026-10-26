using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    /// <summary>Presentation only: soften towards darker neighbouring cells, never reveal them.</summary>
    public static class DungeonFogBlend
    {
        public const float FeatherWidth = .6f;
        private static readonly float[,] gridWeights = BuildGridWeights(FeatherWidth);
        private static readonly float[,] forestGridWeights = BuildGridWeights(.95f);
        private static readonly float[,] forestVoidWeights = BuildGridWeights(.35f);

        // The 9x9 mesh always samples the same coordinates. Only the eight
        // knowledge values change; distance/sqrt/smoothstep do not belong in
        // the per-cell refresh loop.
        private static float[,] BuildGridWeights(float featherWidth)
        {
            var weights = new float[81, 8];
            for (var sy = 0; sy <= 8; sy++) for (var sx = 0; sx <= 8; sx++)
            {
                var index = 0;
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0) continue;
                    var dx = Mathf.Max(Mathf.Abs(sx / 8f - (x + .5f)) - .5f, 0f);
                    var dy = Mathf.Max(Mathf.Abs(sy / 8f - (y + .5f)) - .5f, 0f);
                    var distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / featherWidth);
                    weights[sy * 9 + sx, index++] = Mathf.SmoothStep(0, 1, 2f * distance / (1f + distance));
                }
            }
            return weights;
        }

        public static void SampleGridBrightness(DungeonKnowledge center, DungeonKnowledge[] neighbours, float[] samples, bool forest = false, int forestVoidMask = 0)
        {
            var weights = forest ? forestGridWeights : gridWeights;
            for (var sample = 0; sample < 81; sample++)
            {
                if (center == DungeonKnowledge.Unexplored) { samples[sample] = 0; continue; }
                var known = 1f;
                var visible = center == DungeonKnowledge.Visible ? 1f : 0f;
                for (var index = 0; index < 8; index++)
                {
                    if (neighbours[index] == DungeonKnowledge.Visible) continue;
                    var weight = weights[sample, index];
                    if (neighbours[index] == DungeonKnowledge.Unexplored) known = Mathf.Min(known, weight);
                    visible = Mathf.Min(visible, weight);
                }
                samples[sample] = .55f * known + .45f * visible;
                if (forest && forestVoidMask != 0)
                {
                    // A fog quad exists only over known walkable ground.
                    // Feather its alpha to zero at decorative woodland so an
                    // unknown floor beside a corner cannot stamp a black
                    // square ending abruptly at that quad's void-side edge.
                    // Unknown walkable cells themselves remain unrendered.
                    var fade = 1f;
                    for (var index = 0; index < 8; index++)
                        if ((forestVoidMask & (1 << index)) != 0)
                            fade = Mathf.Min(fade, forestVoidWeights[sample, index]);
                    samples[sample] = Mathf.Lerp(Brightness(center), samples[sample], fade);
                }
            }
        }
        public static float Brightness(DungeonKnowledge knowledge) => knowledge == DungeonKnowledge.Visible
            ? 1f : knowledge == DungeonKnowledge.Remembered ? .55f : 0f;

        // Eight neighbours, row-major from bottom-left, omitting the centre.
        // Missing terrain must be supplied as the centre state, not as unknown.
        public static float Opacity(DungeonKnowledge center, DungeonKnowledge[] neighbours, Vector2 point)
        {
            var baseline = Brightness(center);
            if (baseline == 0f) return 1f;
            var known = 1f;
            var visible = center == DungeonKnowledge.Visible ? 1f : 0f;
            var index = 0;
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var other = neighbours[index++];
                if (other == DungeonKnowledge.Visible) continue;
                var dx = Mathf.Max(Mathf.Abs(point.x - (x + .5f)) - .5f, 0f);
                var dy = Mathf.Max(Mathf.Abs(point.y - (y + .5f)) - .5f, 0f);
                var distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / FeatherWidth);
                // Bias the fade towards the dark edge to limit its opaque halo.
                // Keep a flat slope at both ends so the rim stays soft.
                var weight = Mathf.SmoothStep(0, 1, 2f * distance / (1f + distance));
                if (other == DungeonKnowledge.Unexplored) known = Mathf.Min(known, weight);
                visible = Mathf.Min(visible, weight);
            }
            // Blend the known and currently-visible contours separately. A single
            // minimum of brightness targets breaks continuity where all three
            // knowledge states meet (visible and remembered sides fade differently).
            var brightness = .55f * known + .45f * visible;
            return 1f - brightness / baseline;
        }
    }
}
