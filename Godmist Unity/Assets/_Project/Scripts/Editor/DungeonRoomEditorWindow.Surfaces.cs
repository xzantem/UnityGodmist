using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public partial class DungeonRoomEditorWindow
    {
        private SerializedProperty surfacesProperty, stairsProperty;
        private int activeLevel, selectedStair = -1;
        private bool showOtherLevels = true;
        private DungeonSurfacePosition? selectedSurface, pendingStairStart;
        private readonly List<Vector2Int> pendingStairPath = new();
        private int editingStair = -1;

        private void CancelStairDraft() { pendingStairStart = null; pendingStairPath.Clear(); editingStair = -1; }

        private List<Vector2Int> ReadStairPath(SerializedProperty stair)
        {
            var property = Field(stair, "PathCells");
            var path = new List<Vector2Int>();
            for (var i = 0; i < property.arraySize; i++) path.Add(property.GetArrayElementAtIndex(i).vector2IntValue);
            if (path.Count == 0 && Godmist.Core.Utilities.DungeonStairPathUtility.TryResolve(
                    ReadSurface(Field(stair, "First")).Cell, ReadSurface(Field(stair, "Second")).Cell, null, out var inferred, out _))
                path.AddRange(inferred);
            return path;
        }

        private static DungeonSurfacePosition ReadSurface(SerializedProperty property)
            => new(Field(property, "LocalCell").vector2IntValue, Field(property, "Level").intValue);

        private static void WriteSurface(SerializedProperty property, DungeonSurfacePosition surface)
        {
            Field(property, "LocalCell").vector2IntValue = surface.Cell;
            Field(property, "Level").intValue = surface.Level;
        }

        private List<DungeonSurfacePosition> ReadSurfaces()
        {
            if (surfacesProperty.arraySize == 0)
                return GetFootprintCells(sizeProperty.vector2IntValue).Select(cell => new DungeonSurfacePosition(cell)).ToList();
            var result = new List<DungeonSurfacePosition>();
            for (var i = 0; i < surfacesProperty.arraySize; i++) result.Add(ReadSurface(surfacesProperty.GetArrayElementAtIndex(i)));
            return result;
        }

        private HashSet<Vector2Int> GetFloorCells(Vector2Int size)
        {
            if (surfacesProperty.arraySize == 0) return activeLevel == 0 ? GetFootprintCells(size) : new HashSet<Vector2Int>();
            return ReadSurfaces().Where(surface => surface.Level == activeLevel).Select(surface => surface.Cell).ToHashSet();
        }

        private List<DungeonRoomBoundaryEdgeData> ReadBoundaryEdges()
        {
            var result = new List<DungeonRoomBoundaryEdgeData>();
            for (var i = 0; i < allowedBoundaryEdgesProperty.arraySize; i++)
            {
                var edge = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(i);
                result.Add(new DungeonRoomBoundaryEdgeData(Field(edge, "LocalCell").vector2IntValue,
                    (DungeonEdgeDirection)Field(edge, "Direction").enumValueIndex, Field(edge, "Level").intValue));
            }
            return result;
        }

        private void WriteSurfaces(List<DungeonSurfacePosition> surfaces)
        {
            surfaces.Sort();
            surfacesProperty.arraySize = surfaces.Count;
            for (var i = 0; i < surfaces.Count; i++) WriteSurface(surfacesProperty.GetArrayElementAtIndex(i), surfaces[i]);
            var footprint = surfaces.Select(surface => surface.Cell).ToHashSet();
            for (var i = 0; i < stairsProperty.arraySize; i++) footprint.UnionWith(ReadStairPath(stairsProperty.GetArrayElementAtIndex(i)));
            SetFloorCells(footprint, sizeProperty.vector2IntValue);
            var keys = surfaces.ToHashSet();
            SetAllowedBoundaryEdges(ReadBoundaryEdges().Where(edge => keys.Contains(new DungeonSurfacePosition(edge.LocalCell, edge.Level))
                && DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(edge.LocalCell, edge.Direction, sizeProperty.vector2IntValue, footprint)).ToList());
        }

        private void ChangeLevel(int level)
        {
            if (activeLevel == level) return;
            activeLevel = level;
            ClearAnchorSelection();
            footprintPaintValue = null;
            // Keep the first stair endpoint while the author switches to its destination level.
            Repaint();
        }

        private void DrawLevelControls()
        {
            EditorGUILayout.LabelField("Poziom powierzchni", EditorStyles.boldLabel);
            var levels = ReadSurfaces().Select(surface => surface.Level).Append(activeLevel).Distinct().OrderBy(level => level).ToArray();
            var index = System.Array.IndexOf(levels, activeLevel);
            var next = EditorGUILayout.Popup("Edytowany", index, levels.Select(level => $"Poziom {level}").ToArray());
            if (next != index) ChangeLevel(levels[next]);
            ChangeLevel(EditorGUILayout.DelayedIntField("Przejdź do", activeLevel));
            showOtherLevels = EditorGUILayout.ToggleLeft("Podgląd pozostałych poziomów", showOtherLevels);
            EditorGUILayout.HelpBox("Podłoga maluje tylko wybrany poziom. Nowy poziom: wpisz numer i namaluj powierzchnie.", MessageType.None);
        }

        private bool TryPaintSurface(Vector2Int cell, int level, bool add, out string error)
        {
            error = null;
            if (!IsInsideRoom(cell, sizeProperty.vector2IntValue)) { error = "Pole jest poza rozmiarem pokoju."; return false; }
            var surface = new DungeonSurfacePosition(cell, level);
            var surfaces = ReadSurfaces();
            if (surfaces.Contains(surface) == add) return true;
            if (!add)
            {
                if (surfaces.Count <= 1) { error = "Pokój musi zachować co najmniej jedną powierzchnię."; return false; }
                if (HasSurfaceReferences(surface, out error)) return false;
                surfaces.Remove(surface);
            }
            else surfaces.Add(surface);
            WriteSurfaces(surfaces);
            serializedTemplate.ApplyModifiedProperties();
            poolValidationMessage = null;
            return true;
        }

        private bool HasSurfaceReferences(DungeonSurfacePosition surface, out string error)
        {
            error = null;
            if (FindStairAt(surface) >= 0) error = "Najpierw usuń schody korzystające z tej powierzchni.";
            for (var i = 0; error == null && i < structuralAnchorsProperty.arraySize; i++)
            {
                var anchor = structuralAnchorsProperty.GetArrayElementAtIndex(i);
                if (Field(anchor, "Level").intValue != surface.Level) continue;
                var first = Field(anchor, "LocalCell").vector2IntValue;
                var second = first + GetDirectionOffset((DungeonEdgeDirection)Field(anchor, "Direction").enumValueIndex);
                if (first == surface.Cell || (Field(anchor, "Type").enumValueIndex == (int)DungeonStructuralAnchorType.Door && second == surface.Cell))
                    error = "Najpierw usuń lub przenieś kotwę strukturalną z tej powierzchni.";
            }
            if (surface.Level == 0)
            {
                for (var i = 0; error == null && i < semanticAnchorsProperty.arraySize; i++)
                {
                    var anchor = semanticAnchorsProperty.GetArrayElementAtIndex(i);
                    if (new RectInt(Field(anchor, "LocalCell").vector2IntValue, Field(anchor, "AreaSize").vector2IntValue).Contains(surface.Cell))
                        error = $"Powierzchnia należy do kotwy '{Field(anchor, "Id").stringValue}'. Najpierw przenieś lub usuń kotwę.";
                }
                var terrain = serializedTemplate.FindProperty("<DifficultTerrainCells>k__BackingField");
                for (var i = 0; error == null && i < terrain.arraySize; i++)
                    if (terrain.GetArrayElementAtIndex(i).vector2IntValue == surface.Cell) error = "Powierzchnia ma trudny teren. Najpierw usuń jego oznaczenie.";
            }
            return error != null;
        }

        private void ResizeSurfacesAndStairs(Vector2Int oldSize, Vector2Int newSize, DungeonEdgeDirection direction, bool grow, Vector2Int delta)
        {
            if (surfacesProperty.arraySize > 0)
            {
                var surfaces = ReadSurfaces().Where(surface => grow || !IsInRemovedStrip(surface.Cell, oldSize, direction))
                    .Select(surface => new DungeonSurfacePosition(surface.Cell + delta, surface.Level)).OrderBy(surface => surface).ToList();
                surfacesProperty.arraySize = surfaces.Count;
                for (var i = 0; i < surfaces.Count; i++) WriteSurface(surfacesProperty.GetArrayElementAtIndex(i), surfaces[i]);
            }
            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                var stair = stairsProperty.GetArrayElementAtIndex(i);
                foreach (var name in new[] { "First", "Second" })
                {
                    var endpoint = Field(stair, name);
                    var surface = ReadSurface(endpoint);
                    WriteSurface(endpoint, new DungeonSurfacePosition(surface.Cell + delta, surface.Level));
                }
                var path = Field(stair, "PathCells");
                for (var j = 0; j < path.arraySize; j++) path.GetArrayElementAtIndex(j).vector2IntValue += delta;
            }
            var terrain = serializedTemplate.FindProperty("<DifficultTerrainCells>k__BackingField");
            for (var i = 0; i < terrain.arraySize; i++) terrain.GetArrayElementAtIndex(i).vector2IntValue += delta;
        }

        private int FindStairAt(DungeonSurfacePosition surface)
        {
            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                var stair = stairsProperty.GetArrayElementAtIndex(i);
                if (ReadSurface(Field(stair, "First")) == surface || ReadSurface(Field(stair, "Second")) == surface) return i;
            }
            return -1;
        }

        private bool TryAddStair(DungeonSurfacePosition first, DungeonSurfacePosition second, out string error)
            => TrySaveStairRoute(first, second, null, -1, out error);

        private bool TrySaveStairRoute(DungeonSurfacePosition first, DungeonSurfacePosition second,
            IReadOnlyList<Vector2Int> authoredPath, int replaceIndex, out string error)
        {
            error = null;
            var surfaces = ReadSurfaces();
            if (first.Level == second.Level) error = "Schody muszą łączyć różne poziomy.";
            else if (!surfaces.Contains(first) || !surfaces.Contains(second)) error = "Oba końce schodów muszą wskazywać istniejące powierzchnie.";
            if (error != null) return false;
            if (!Godmist.Core.Utilities.DungeonStairPathUtility.TryResolve(first.Cell, second.Cell, authoredPath, out var path, out error)) return false;
            foreach (var cell in path)
                if (!IsInsideRoom(cell, sizeProperty.vector2IntValue)) { error = $"Pole schodów {cell} poza rozmiarem pokoju."; return false; }
            if (replaceIndex >= stairsProperty.arraySize || replaceIndex < -1) { error = "Edytowane schody już nie istnieją."; return false; }
            var key = new DungeonStairConnection(first, second);
            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                if (i == replaceIndex) continue;
                var existing = stairsProperty.GetArrayElementAtIndex(i);
                var a = ReadSurface(Field(existing, "First")); var b = ReadSurface(Field(existing, "Second"));
                if (a.Level != b.Level && new DungeonStairConnection(a, b).Equals(key))
                { error = "Takie połączenie schodów już istnieje."; return false; }
            }
            if (first != key.First) System.Array.Reverse(path);
            var index = replaceIndex < 0 ? stairsProperty.arraySize : replaceIndex;
            if (replaceIndex < 0) stairsProperty.InsertArrayElementAtIndex(index);
            var stair = stairsProperty.GetArrayElementAtIndex(index);
            WriteSurface(Field(stair, "First"), key.First); WriteSurface(Field(stair, "Second"), key.Second);
            var cells = Field(stair, "PathCells"); cells.arraySize = path.Length;
            for (var i = 0; i < path.Length; i++) cells.GetArrayElementAtIndex(i).vector2IntValue = path[i];
            WriteSurfaces(surfaces);
            serializedTemplate.ApplyModifiedProperties();
            ClearAnchorSelection(); selectedStair = index;
            poolValidationMessage = null;
            return true;
        }

        private void PlaceStairEndpoint(Vector2Int cell)
        {
            var surface = new DungeonSurfacePosition(cell, activeLevel);
            if (!pendingStairStart.HasValue)
            {
                if (!ReadSurfaces().Contains(surface)) { ShowNotification(new GUIContent("Wybierz istniejącą powierzchnię początku.")); return; }
                CancelStairDraft(); pendingStairStart = surface; pendingStairPath.Add(cell); ClearAnchorSelection(); return;
            }
            var last = pendingStairPath[pendingStairPath.Count - 1];
            if (activeLevel != pendingStairStart.Value.Level)
            {
                if (!ReadSurfaces().Contains(surface)) { ShowNotification(new GUIContent("Koniec musi leżeć na istniejącej powierzchni.")); return; }
                var route = new List<Vector2Int>(pendingStairPath);
                if (cell != last) route.Add(cell);
                if (TrySaveStairRoute(pendingStairStart.Value, surface, route, editingStair, out var error)) CancelStairDraft();
                else ShowNotification(new GUIContent(error));
                return;
            }
            if (!IsInsideRoom(cell, sizeProperty.vector2IntValue) || !Godmist.Core.Utilities.DungeonStairPathUtility.AreAdjacent(last, cell))
            { ShowNotification(new GUIContent("Wybierz kolejne pole sąsiadujące przez krawędź.")); return; }
            pendingStairPath.Add(cell);
        }

        private void BeginStairRouteEdit()
        {
            var index = selectedStair;
            var first = ReadSurface(Field(stairsProperty.GetArrayElementAtIndex(index), "First"));
            CancelStairDraft(); ChangeLevel(first.Level); ClearAnchorSelection();
            editingStair = index; pendingStairStart = first; pendingStairPath.Add(first.Cell); activeTool = EditTool.Stairs;
        }

        private void DrawStairLibrary()
        {
            EditorGUILayout.LabelField($"Schody ({stairsProperty.arraySize})", EditorStyles.boldLabel);
            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                var stair = stairsProperty.GetArrayElementAtIndex(i);
                var first = ReadSurface(Field(stair, "First")); var second = ReadSurface(Field(stair, "Second"));
                var label = $"{i + 1}: L{first.Level} {first.Cell} ↔ L{second.Level} {second.Cell}";
                if (!MatchesSearch(label)) continue;
                if (GUILayout.Button(new GUIContent(label, label), selectedStair == i ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                {
                    CancelStairDraft(); ChangeLevel(first.Level);
                    ClearAnchorSelection(); selectedStair = i; activeTool = EditTool.Select;
                }
            }
        }

        private void DrawStairSelection()
        {
            EditorGUILayout.LabelField("Połączenie schodów", EditorStyles.boldLabel);
            var stair = stairsProperty.GetArrayElementAtIndex(selectedStair);
            foreach (var name in new[] { "First", "Second" })
            {
                var surface = ReadSurface(Field(stair, name));
                EditorGUILayout.LabelField(name == "First" ? "Początek" : "Koniec", $"{surface.Cell}, poziom {surface.Level}");
                if (GUILayout.Button($"Pokaż poziom {surface.Level}"))
                {
                    var index = selectedStair; ChangeLevel(surface.Level); selectedStair = index;
                }
            }
            var route = ReadStairPath(stair);
            if (route.Count < 2) EditorGUILayout.HelpBox("To starsze połączenie bez przebiegu. Wskaż trasę, aby udostępnić je dla ruchu.", MessageType.Warning);
            else EditorGUILayout.LabelField("Długość", $"{route.Count - 1} kroków, {System.Math.Max(0, route.Count - 2)} pól pośrednich");
            if (GUILayout.Button("Edytuj przebieg")) { BeginStairRouteEdit(); return; }
            if (GUILayout.Button("Usuń schody"))
            {
                stairsProperty.DeleteArrayElementAtIndex(selectedStair);
                WriteSurfaces(ReadSurfaces());
                serializedTemplate.ApplyModifiedProperties();
                CancelStairDraft(); ClearAnchorSelection();
            }
        }

        private void DrawSurfaceSelection()
        {
            var surface = selectedSurface.Value;
            EditorGUILayout.LabelField("Powierzchnia", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pole", surface.Cell.ToString());
            EditorGUILayout.LabelField("Poziom", surface.Level.ToString());
            if (GUILayout.Button("Usuń powierzchnię"))
            {
                if (TryPaintSurface(surface.Cell, surface.Level, false, out var error)) ClearAnchorSelection();
                else ShowNotification(new GUIContent(error));
            }
        }

        private void DrawOtherLevelPreview(Rect grid, float cellSize, Vector2Int size)
        {
            if (!showOtherLevels) return;
            foreach (var group in ReadSurfaces().Where(surface => surface.Level != activeLevel).GroupBy(surface => surface.Cell))
            {
                var rect = GetCellRect(grid, cellSize, size, group.Key);
                var inset = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
                var color = new Color(.48f, .55f, .68f, .3f);
                EditorGUI.DrawRect(new Rect(inset.x, inset.y, inset.width, 1), color);
                EditorGUI.DrawRect(new Rect(inset.x, inset.yMax, inset.width, 1), color);
                EditorGUI.DrawRect(new Rect(inset.x, inset.y, 1, inset.height), color);
                EditorGUI.DrawRect(new Rect(inset.xMax, inset.y, 1, inset.height), color);
                if (cellSize >= 24f)
                {
                    var previous = GUI.color; GUI.color = new Color(.65f, .7f, .8f, .6f);
                    GUI.Label(inset, string.Join(",", group.Select(surface => $"L{surface.Level}")), EditorStyles.miniLabel);
                    GUI.color = previous;
                }
            }
        }

        private void DrawStairMarkers(Rect grid, float cellSize, Vector2Int size)
        {
            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                var stair = stairsProperty.GetArrayElementAtIndex(i);
                var first = ReadSurface(Field(stair, "First")); var second = ReadSurface(Field(stair, "Second"));
                if (activeLevel == first.Level || activeLevel == second.Level || (showOtherLevels && selectedStair == i))
                    DrawStairRoute(ReadStairPath(stair), grid, cellSize, size, selectedStair == i ? 1f : .45f);
                foreach (var end in new[] { first, second })
                {
                    if (end.Level != activeLevel) continue;
                    var other = end == first ? second : first;
                    var rect = GetCellRect(grid, cellSize, size, end.Cell);
                    DrawAnchorMarker(rect, new Color(.65f, .4f, .85f), $"S{i + 1}→{other.Level}");
                    if (selectedStair == i) DrawSelectionOutline(rect);
                }
            }
            if (pendingStairStart.HasValue) DrawStairRoute(pendingStairPath, grid, cellSize, size, .9f);
            if (pendingStairStart.HasValue && pendingStairStart.Value.Level == activeLevel)
                DrawSelectionOutline(GetCellRect(grid, cellSize, size, pendingStairStart.Value.Cell));
        }
        private static void DrawStairRoute(IReadOnlyList<Vector2Int> path, Rect grid, float cellSize, Vector2Int size, float alpha)
        {
            var color = new Color(.7f, .42f, .92f, alpha);
            for (var i = 0; i < path.Count; i++)
            {
                var rect = GetCellRect(grid, cellSize, size, path[i]);
                if (i > 0)
                {
                    var previous = GetCellRect(grid, cellSize, size, path[i - 1]).center;
                    var current = rect.center;
                    // GUI rectangles share the viewport's clipping and coordinate space.
                    if (Godmist.Core.Utilities.DungeonStairPathUtility.AreAdjacent(path[i - 1], path[i]))
                        EditorGUI.DrawRect(new Rect(Mathf.Min(previous.x, current.x) - 2, Mathf.Min(previous.y, current.y) - 2,
                            Mathf.Abs(previous.x - current.x) + 4, Mathf.Abs(previous.y - current.y) + 4), color);
                }
                if (i > 0 && i < path.Count - 1)
                    DrawAnchorMarker(rect, color, i.ToString());
            }
        }
    }
}
