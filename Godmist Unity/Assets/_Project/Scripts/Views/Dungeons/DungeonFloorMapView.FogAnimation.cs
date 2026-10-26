using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        // Five updates per second for fog calculation and presentation.
        public const float FogRefreshInterval = .2f;
        private float fogAnimationElapsed;
        private sealed class FogCellVisual
        {
            public DungeonFogTransition Transition;
            public Color TargetColor;
            public float TargetBaseline;
            public bool Boundary;
            public int FirstVertex;
            public bool ColorsDirty;
        }

        private sealed class FogSpriteVisual
        {
            public Color BaseColor;
            public Vector2 Current, Start, Target;
            public float Elapsed;
            public bool Active, Shown;
        }

        private Dictionary<Vector2Int, DungeonKnowledge> previousFogProjection = new();
        private readonly Dictionary<Vector2Int, FogCellVisual> fogCellVisuals = new();
        private readonly Dictionary<SpriteRenderer, FogSpriteVisual> fogSpriteVisuals = new();
        private readonly List<Vector2Int> expiredFogCells = new();
        private readonly List<SpriteRenderer> expiredFogSprites = new();
        private readonly float[] fogTargetSamples = new float[81];
        private readonly float[] fogStartSamples = new float[81];
        private DungeonFloorInstance animatedFogFloor;
        private DungeonFloorMap animatedFogMap;
        private int animatedProjectionLevel, animatedDebugRevision;
        private bool fogAnimationContextReady, snapFogAnimation = true;
        private bool batchingFloorColors;
        private readonly Dictionary<Vector3Int, Color> pendingFloorColors = new();
        public int FogFloorColorWriteCount { get; private set; }
        public int FogFloorColorCoalescedWriteCount { get; private set; }
        public int FogSpriteTargetWriteCount { get; private set; }
        // Opt-in diagnostic totals. The replay resets these between owner
        // calls; normal gameplay never reads the clock.
        public bool MeasureFogPhaseTimings { get; set; }
        public long[] FogPhaseTicks { get; } = new long[15];
        private readonly struct FogPhaseTiming : System.IDisposable
        {
            private readonly DungeonFloorMapView owner;
            private readonly int phase;
            private readonly long started;
            public FogPhaseTiming(DungeonFloorMapView owner, int phase)
            {
                this.owner = owner; this.phase = phase;
                started = owner.MeasureFogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            }
            public void Dispose()
            {
                if (started != 0) owner.FogPhaseTicks[phase] += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            }
        }

        private void SetFloorPresentationColor(Vector3Int cell, Color color)
        {
            // Only the last colour can be rendered in this synchronous refresh.
            // Do not cache across frames: external tile colour edits remain live.
            if (batchingFloorColors)
            {
                if (pendingFloorColors.ContainsKey(cell)) FogFloorColorCoalescedWriteCount++;
                pendingFloorColors[cell] = color;
                return;
            }
            floorTilemap.SetColor(cell, color); FogFloorColorWriteCount++;
        }

        private void FlushFloorPresentationColors()
        {
            foreach (var pair in pendingFloorColors)
            { floorTilemap.SetColor(pair.Key, pair.Value); FogFloorColorWriteCount++; }
            pendingFloorColors.Clear();
        }

        private void BeginFogAnimationRefresh()
        {
            var debugRevision = FogFloor?.Fog.DebugRevision ?? 0;
            incrementalForestFog = UsesVegetationPresentation && !boundMap.HasElevatedSurfaces
                && fogAnimationContextReady && ReferenceEquals(animatedFogFloor, FogFloor)
                && ReferenceEquals(animatedFogMap, boundMap) && animatedProjectionLevel == elevation.ProjectionLevel
                && animatedDebugRevision == debugRevision && fogBoundaryRevision == boundMap.TraversalRevision;
            fogBoundaryRevision = boundMap.TraversalRevision;
            snapFogAnimation = !Application.isPlaying || FogFloor == null || FogFloor.Fog.DebugFogDisabled || !fogAnimationContextReady
                || !ReferenceEquals(animatedFogFloor, FogFloor) || !ReferenceEquals(animatedFogMap, boundMap)
                || animatedProjectionLevel != elevation.ProjectionLevel || animatedDebugRevision != debugRevision;
            if (snapFogAnimation) RestoreFogAnimationTargets();
            // Both dictionaries belong to this view. Retain the exact previous
            // projection without copying every covered cell on each pan/reveal.
            var reusable = previousFogProjection;
            previousFogProjection = fogProjection;
            fogProjection = reusable;
            animatedFogFloor = FogFloor; animatedFogMap = boundMap;
            animatedProjectionLevel = elevation.ProjectionLevel; animatedDebugRevision = debugRevision;
            fogAnimationContextReady = true;
        }

        private void RestoreFogAnimationTargets()
        {
            using var timing = new FogPhaseTiming(this, 8);
            foreach (var pair in fogCellVisuals)
                if (floorTilemap != null) SetFloorPresentationColor(new Vector3Int(pair.Key.x, pair.Key.y, 0), pair.Value.TargetColor);
            foreach (var pair in fogSpriteVisuals)
                if (pair.Key != null)
                {
                    var target = FogSpriteColor(pair.Value.BaseColor, pair.Value.Target);
                    // Read the actual renderer: external colour edits must
                    // still be repaired, including sub-epsilon differences.
                    if (!pair.Key.color.Equals(target)) { pair.Key.color = target; FogSpriteTargetWriteCount++; }
                }
            fogCellVisuals.Clear(); fogSpriteVisuals.Clear();
            fogTargetPatterns.Clear();
        }

        private void ResetFogAnimations()
        {
            fogAnimationElapsed = 0;
            RestoreFogAnimationTargets();
            presentedFogTints.Clear();
            previousFogProjection.Clear(); fogProjection.Clear();
            animatedFogFloor = null; animatedFogMap = null;
            fogAnimationContextReady = false; snapFogAnimation = true;
            incrementalForestFog = false; fogBoundaryRevision = -1;
            presentationCoverageReady = false;
            fogTargetPatterns.Clear();
        }

        /// <summary>Presentation only; hiding remains immediate and follows logical fog knowledge.</summary>
        public void PresentFogSprite(SpriteRenderer sprite, Color baseColor, bool show, float brightness = 1)
        {
            if (sprite.forceRenderingOff == show) sprite.forceRenderingOff = !show;
            var target = new Vector2(brightness, 1);
            if (!fogSpriteVisuals.TryGetValue(sprite, out var visual))
            {
                // Unexplored layers cannot animate yet. Don't allocate and
                // tick a transition for every hidden tree on the entire floor.
                // First reveal still starts from zero alpha.
                if (!show)
                {
                    var hiddenColor = FogSpriteColor(baseColor, target);
                    if (sprite.color != hiddenColor) sprite.color = hiddenColor;
                    return;
                }
                var initial = target;
                if (!snapFogAnimation && show) initial.y = 0;
                visual = new FogSpriteVisual { Current = initial, Target = initial };
                fogSpriteVisuals.Add(sprite, visual);
            }
            visual.BaseColor = baseColor;
            if (!show || snapFogAnimation)
            {
                visual.Current = visual.Target = target; visual.Active = false;
            }
            else if (!visual.Shown || visual.Target != target)
            {
                if (!visual.Shown) visual.Current.y = 0;
                visual.Start = visual.Current; visual.Target = target;
                visual.Elapsed = 0; visual.Active = true;
            }
            visual.Shown = show;
            var color = FogSpriteColor(baseColor, visual.Current);
            if (sprite.color != color) sprite.color = color;
        }

        private static Color FogSpriteColor(Color baseColor, Vector2 fog)
            => new(baseColor.r * fog.x, baseColor.g * fog.x, baseColor.b * fog.x, baseColor.a * fog.y);

        private static readonly Unity.Profiling.ProfilerMarker FogLateSample = new("Godmist.Dungeon.FloorLate");
        private static readonly Unity.Profiling.ProfilerMarker FogAnimationSample = new("Godmist.Dungeon.Fog.Animation");
        private void LateUpdate()
        {
            using var sample = FogLateSample.Auto();
            EnsureCameraPresentation(); AdvanceFogAnimation(Time.deltaTime);
        }

        private void AdvanceFogAnimation(float deltaTime)
        {
            fogAnimationElapsed += Mathf.Max(0, deltaTime);
            if (fogAnimationElapsed < FogRefreshInterval) return;
            var elapsed = fogAnimationElapsed;
            fogAnimationElapsed = 0;
            // One presentation update after a stall; never replay missed ticks.
            TickFogAnimation(elapsed);
        }

        private void TickFogAnimation(float deltaTime)
        {
            AdvanceForestKnowledge(deltaTime);
            using var timing = new FogPhaseTiming(this, 7);
            using var sample = FogAnimationSample.Auto();
            var colorsChanged = false;
            expiredFogCells.Clear();
            foreach (var pair in fogCellVisuals)
            {
                var visual = pair.Value;
                if (visual.Transition.Advance(deltaTime))
                {
                    ApplyFogCellColor(pair.Key, visual); WriteFogCellColors(visual); colorsChanged = true;
                }
                if (!visual.Transition.Active && !visual.Boundary) expiredFogCells.Add(pair.Key);
            }
            foreach (var cell in expiredFogCells) { fogCellVisuals.Remove(cell); fogTargetPatterns.Remove(cell); }
            if (expiredFogCells.Count > 0) BuildFogMesh();
            else if (colorsChanged && fogBlendMesh != null)
                UploadFogColors();

            expiredFogSprites.Clear();
            foreach (var pair in fogSpriteVisuals)
            {
                if (pair.Key == null) { expiredFogSprites.Add(pair.Key); continue; }
                var visual = pair.Value;
                if (!visual.Active) continue;
                visual.Elapsed = Mathf.Min(DungeonFogTransition.Duration, visual.Elapsed + Mathf.Max(0, deltaTime));
                var blend = Mathf.SmoothStep(0, 1, visual.Elapsed / DungeonFogTransition.Duration);
                visual.Current = Vector2.Lerp(visual.Start, visual.Target, blend);
                pair.Key.color = FogSpriteColor(visual.BaseColor, visual.Current);
                visual.Active = visual.Elapsed < DungeonFogTransition.Duration;
            }
            foreach (var sprite in expiredFogSprites) fogSpriteVisuals.Remove(sprite);
        }

        private void ApplyFogCellColor(Vector2Int cell, FogCellVisual visual)
        {
            var color = visual.TargetColor;
            var ratio = visual.TargetBaseline > 0 ? visual.Transition.Baseline / visual.TargetBaseline : 0;
            color.r *= ratio; color.g *= ratio; color.b *= ratio;
            // Fresh woodland tiles fade over the decorative litter instead
            // of covering it with an opaque black square for the reveal.
            if (UsesVegetationPresentation) color.a *= Mathf.Clamp01(ratio);
            SetFloorPresentationColor(new Vector3Int(cell.x, cell.y, 0), color);
        }

        private void WriteFogCellColors(FogCellVisual visual)
        {
            FogColorPatchWriteCount++;
            // Static litter has the same light response on either side of the
            // frontier. Knowledge dims routes/props, not rectangular soil tiles.
            var brightness = forestFogGroundMaterial != null ? 1f : 0;
            for (var i = 0; i < fogTargetSamples.Length; i++)
                fogColors[visual.FirstVertex + i] = new Color(brightness, brightness, brightness,
                    visual.Transition.Baseline > 0
                        ? Mathf.Clamp01(1 - visual.Transition[i] / visual.Transition.Baseline) : 0);
            visual.ColorsDirty = false;
            dirtyFogColorSlots.Add(visual.FirstVertex / 81);
        }
    }
}
