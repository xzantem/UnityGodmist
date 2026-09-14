using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    /// <summary>Presentation focus only; never changes traversal, discovery or unit positions.</summary>
    public sealed class DungeonElevationViewState
    {
        private DungeonFloorMap map;
        private int[] levels = System.Array.Empty<int>();
        private readonly Dictionary<Vector2Int, DungeonSurfacePosition> visibleSurfaces = new();
        private readonly Dictionary<Vector2Int, DungeonTraversalPosition> visiblePositions = new();
        private DungeonTraversalPosition? focus;
        private bool cutAway;
        private float focusHeight;
        private Vector2Int focusCell;
        public int ProjectionLevel => IsManual ? Level : levels.LastOrDefault();
        public IEnumerable<DungeonSurfacePosition> CutawaySurfaces => map == null ? System.Array.Empty<DungeonSurfacePosition>() : map.Surfaces.Where(IsCutAway);
        private bool IsCutAway(DungeonSurfacePosition surface) => focus.HasValue && !IsManual && cutAway
            && surface.Level > focusHeight && Mathf.Abs(surface.Cell.x - focusCell.x) <= 1 && Mathf.Abs(surface.Cell.y - focusCell.y) <= 1;
        public bool Focus(DungeonTraversalPosition position, bool resumeFollowing = false)
        {
            if (map == null || !map.TryGetTraversalCell(position, out var cell)) return false;
            if (focus == position && (!resumeFollowing || !IsManual)) return false;
            var previousCeiling = ProjectionLevel;
            var previousLevel = Level;
            var previousCutaway = cutAway && !IsManual;
            focus = position; focusCell = cell;
            map.TryGetTraversalHeight(position, out var height);
            focusHeight = height;
            var level = Mathf.CeilToInt(height);
            cutAway = map.Surfaces.Any(s => s.Cell == cell && s.Level > height) || HasStairAbove(cell, height);
            if (!IsManual || resumeFollowing) { Level = level; IsManual = false; }
            // Most free movement changes only the focus cell, not the visible geometry.
            if (previousCeiling == ProjectionLevel && previousLevel == Level
                && !previousCutaway && !(cutAway && !IsManual)) return false;
            RebuildProjection(); return true;
        }
        private bool HasStairAbove(Vector2Int cell, float height)
        {
            foreach (var stair in map.Stairs)
            {
                if (!map.TryGetStairPath(stair, out var path)) continue;
                for (var i = 1; i < path.Count - 1; i++)
                    if (path[i] == cell && map.TryGetTraversalHeight(new DungeonTraversalPosition(stair, i), out var stairHeight)
                        && stairHeight > height) return true;
            }
            return false;
        }
        public bool IsSelectable(DungeonTraversalPosition position) => IsVisible(position)
            && map.TryGetTraversalHeight(position, out var height) && Mathf.CeilToInt(height) == Level;
        public int Level { get; private set; }
        public bool IsManual { get; private set; }
        public IEnumerable<DungeonSurfacePosition> VisibleSurfaces => visibleSurfaces.Values;

        public void Bind(DungeonFloorMap value, int focusLevel = 0)
        {
            var sameMap = ReferenceEquals(map, value);
            map = value;
            if (!sameMap) { focus = null; cutAway = false; }
            var available = new HashSet<int>();
            if (map != null)
            {
                foreach (var surface in map.Surfaces) available.Add(surface.Level);
                foreach (var stair in map.Stairs)
                    if (map.TryGetStairPath(stair, out var path))
                        for (var i = 1; i < path.Count - 1; i++)
                            if (map.TryGetTraversalHeight(new DungeonTraversalPosition(stair, i), out var height)) available.Add(Mathf.CeilToInt(height));
            }
            levels = available.OrderBy(level => level).ToArray();
            if (!sameMap) IsManual = false;
            var desiredLevel = sameMap ? Level : focusLevel;
            Level = levels.Contains(desiredLevel) ? desiredLevel : levels.FirstOrDefault();
            RebuildProjection();
        }

        public bool Step(int direction)
        {
            if (direction == 0 || levels.Length == 0) return false;
            var startLevel = !IsManual && direction < 0 ? ProjectionLevel : Level;
            var index = System.Array.IndexOf(levels, startLevel) + (direction > 0 ? 1 : -1);
            if (index < 0 || index >= levels.Length) return false;
            IsManual = true;
            Level = levels[index];
            RebuildProjection();
            return true;
        }

        /// <summary>Explicit focus or execution resumes following; idle ticks must not cancel manual planning.</summary>
        public bool Follow(int level, bool resumeFollowing = false)
        {
            if (!levels.Contains(level) || (IsManual && !resumeFollowing)) return false;
            var changed = IsManual || Level != level;
            IsManual = false;
            if (!changed) return false;
            Level = level;
            RebuildProjection();
            return true;
        }

        public bool TryGetVisiblePosition(Vector2Int cell, out DungeonTraversalPosition position)
            => visiblePositions.TryGetValue(cell, out position);
        public bool TryGetVisibleSurface(Vector2Int cell, out DungeonSurfacePosition surface)
        {
            surface = default;
            if (!visiblePositions.TryGetValue(cell, out var position) || position.IsOnStairs) return false;
            surface = position.Surface; return true;
        }
        public bool IsVisible(DungeonTraversalPosition position) => map != null
            && map.TryGetTraversalCell(position, out var cell) && visiblePositions.TryGetValue(cell, out var current) && current == position;

        public bool IsVisible(DungeonSurfacePosition surface)
            => IsVisible(new DungeonTraversalPosition(surface));

        public bool IsVisible(DungeonSurfaceEdge edge)
            => edge.Level <= ProjectionLevel && (IsVisible(edge.First) || IsVisible(edge.Second));

        private void RebuildProjection()
        {
            visibleSurfaces.Clear();
            visiblePositions.Clear();
            if (map == null) return;
            foreach (var surface in map.Surfaces)
            {
                if (surface.Level > ProjectionLevel || IsCutAway(surface)) continue;
                if (!visibleSurfaces.TryGetValue(surface.Cell, out var previous) || surface.Level > previous.Level)
                    visibleSurfaces[surface.Cell] = surface;
            }
            foreach (var pair in visibleSurfaces) visiblePositions[pair.Key] = new DungeonTraversalPosition(pair.Value);
            foreach (var stairs in map.Stairs.OrderBy(s => s.First).ThenBy(s => s.Second))
            {
                if (!map.TryGetStairPath(stairs, out var path)) continue;
                for (var i = 1; i < path.Count - 1; i++)
                {
                    var position = new DungeonTraversalPosition(stairs, i);
                    map.TryGetTraversalHeight(position, out var height);
                    if (Mathf.CeilToInt(height) > ProjectionLevel) continue;
                    if (focus.HasValue && !IsManual && cutAway && height > focusHeight
                        && Mathf.Abs(path[i].x - focusCell.x) <= 1 && Mathf.Abs(path[i].y - focusCell.y) <= 1) continue;
                    if (visiblePositions.TryGetValue(path[i], out var existing))
                    {
                        map.TryGetTraversalHeight(existing, out var previousHeight);
                        if (previousHeight >= height) continue;
                    }
                    visiblePositions[path[i]] = position;
                }
            }
        }
    }
}
