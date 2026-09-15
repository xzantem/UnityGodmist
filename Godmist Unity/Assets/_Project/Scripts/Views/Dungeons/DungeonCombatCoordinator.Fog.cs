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
        private DungeonTraversalPosition[] fogObservers;
        private (string Id, bool Active, bool State)[] fogObjects;
        private DungeonFloorMap fogMap;
        private float nextFogTime;
        private int fogBodyCount = -1;
        private readonly HashSet<DungeonTraversalPosition> previousFogVisible = new();

        private void UpdateFog(bool force = false)
        {
            if (!force && Time.unscaledTime < nextFogTime) return;
            nextFogTime = Time.unscaledTime + .1f;
            var observers = party.ActiveMemberViews.Where(IsLiving).Select(view => view.DungeonPosition).ToArray();
            var objectStates = floor.Objects.Select(obj => (obj.InstanceId, obj.IsActive, DungeonFogSystem.ReadState(obj))).ToArray();
            if (!force && fogObservers != null && fogObservers.SequenceEqual(observers)
                && fogObjects.SequenceEqual(objectStates) && ReferenceEquals(fogMap, floor.Map)
                && !fogGeometryDirty && fogBodyCount == floor.EnemyBodies.Count) return;
            var refreshObjects = force || fogGeometryDirty || !ReferenceEquals(fogMap, floor.Map)
                || fogObjects == null || !fogObjects.SequenceEqual(objectStates) || fogBodyCount != floor.EnemyBodies.Count;
            previousFogVisible.Clear();
            previousFogVisible.UnionWith(floor.Fog.Visible);
            fogObservers = observers; fogObjects = objectStates;
            fogMap = floor.Map; fogGeometryDirty = false;
            fogBodyCount = floor.EnemyBodies.Count;
            fogSystem.Update(floor, observers);
            if (!refreshObjects && previousFogVisible.SetEquals(floor.Fog.Visible)) return;
            map.ApplyFog(floor);
            gridFeedback.RefreshVisibility();
        }
        private bool fogGeometryDirty;
    }
}
