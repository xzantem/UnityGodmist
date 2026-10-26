using System.Linq;
using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private readonly DungeonFogSystem fogSystem = new();
        private static readonly Unity.Profiling.ProfilerMarker FogVisibilitySample = new("Godmist.Dungeon.Fog.Visibility");
        private static readonly Unity.Profiling.ProfilerMarker FogRenderSample = new("Godmist.Dungeon.Fog.Render");
        private static readonly Unity.Profiling.ProfilerMarker FogFeedbackSample = new("Godmist.Dungeon.Fog.Feedback");
        private DungeonTraversalPosition[] fogObservers;
        private (string Id, bool Active, bool State)[] fogObjects;
        private DungeonFloorMap fogMap;
        private float nextFogTime;
        private int fogDebugRevision = -1;
        private int fogBodyCount = -1;
        private readonly HashSet<DungeonTraversalPosition> previousFogVisible = new();

        private DungeonFogUpdateJob fogJob;
        private DungeonTraversalPosition[] pendingFogObservers;
        private (string Id, bool Active, bool State)[] pendingFogObjects;
        private int fogMapRevision = -1, lastFogSliceFrame = -1;
        private bool pendingFogRefreshObjects;

        private void UpdateFog(bool force = false)
        {
            using var sample = FogSample.Auto();
            force |= fogDebugRevision != floor.Fog.DebugRevision;
            if (force) { fogJob?.Dispose(); fogJob = null; }
            if (fogJob == null)
            {
                if (!force && Time.unscaledTime < nextFogTime) return;
                nextFogTime = Time.unscaledTime + DungeonFloorMapView.FogRefreshInterval;
                fogDebugRevision = floor.Fog.DebugRevision;
                var members = party.ActiveMemberViews.Where(IsLiving).ToArray();
                var observers = members.Select(view => view.DungeonPosition).ToArray();
                var objectStates = floor.Objects.Select(obj => (obj.InstanceId, obj.IsActive, DungeonFogSystem.ReadState(obj))).ToArray();
                if (!force && fogObservers != null && fogObservers.SequenceEqual(observers)
                    && fogObjects.SequenceEqual(objectStates) && ReferenceEquals(fogMap, floor.Map)
                    && fogMapRevision == floor.Map.TraversalRevision
                    && !fogGeometryDirty && fogBodyCount == floor.EnemyBodies.Count) return;
                pendingFogRefreshObjects = force || fogObservers == null || fogGeometryDirty || !ReferenceEquals(fogMap, floor.Map)
                    || fogMapRevision != floor.Map.TraversalRevision || fogObjects == null
                    || !fogObjects.SequenceEqual(objectStates) || fogBodyCount != floor.EnemyBodies.Count;
                pendingFogObservers = observers;
                pendingFogObjects = objectStates;
                fogGeometryDirty = false;
                var calculatingFloor = floor;
                var bodies = floor.EnemyBodies.Count;
                fogJob = fogSystem.CreateUpdateJob(floor, observers, () =>
                    ReferenceEquals(floor, calculatingFloor) && !fogGeometryDirty
                    && floor.EnemyBodies.Count == bodies && FogMembersMatch(members), background: true);
                previousFogVisible.Clear();
                previousFogVisible.UnionWith(floor.Fog.Visible);
            }
            // Forced refreshes can also arrive from combat events in this frame.
            if (lastFogSliceFrame == Time.frameCount) return;
            lastFogSliceFrame = Time.frameCount;
            using (FogVisibilitySample.Auto()) fogJob.AdvanceSlice();
            if (!fogJob.IsComplete) return;
            var applied = fogJob.Applied;
            fogJob.Dispose();
            fogJob = null;
            if (!applied) { fogObservers = null; nextFogTime = 0; return; }
            fogObservers = pendingFogObservers;
            fogObjects = pendingFogObjects;
            fogMap = floor.Map;
            fogMapRevision = floor.Map.TraversalRevision;
            fogBodyCount = floor.EnemyBodies.Count;
            if (!pendingFogRefreshObjects && previousFogVisible.SetEquals(floor.Fog.Visible)) return;
            using (FogRenderSample.Auto()) map.ApplyFog(floor);
            using (FogFeedbackSample.Auto()) gridFeedback.RefreshVisibility();
        }

        // Finish a captured observer position even if the party keeps walking.
        // Otherwise a multi-frame calculation can be canceled forever. Applied
        // positions stay in fogObservers, so the next job picks up newer positions.
        // Actor death/removal and geometry changes still invalidate the result.
        private bool FogMembersMatch(IReadOnlyList<Godmist.Views.Exploration.ExplorationCharacterView> expected)
        {
            var index = 0;
            foreach (var view in party.ActiveMemberViews)
            {
                if (!IsLiving(view)) continue;
                if (index >= expected.Count || !ReferenceEquals(view, expected[index++])) return false;
            }
            return index == expected.Count;
        }

        private void CancelFogUpdate()
        {
            fogJob?.Dispose();
            fogJob = null;
            fogObservers = null;
            pendingFogObservers = null;
            pendingFogObjects = null;
            nextFogTime = 0;
        }
        private bool fogGeometryDirty;
    }
}
