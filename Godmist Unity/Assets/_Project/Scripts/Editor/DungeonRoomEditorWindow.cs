using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Systems.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public partial class DungeonRoomEditorWindow : EditorWindow
    {
        private enum EditTool
        {
            Select,
            Footprint,
            StructuralAnchor,
            SemanticAnchor,
            BoundaryEdge,
            Stairs
        }

        private DungeonRoomTemplateDefinition template;
        private SerializedObject serializedTemplate;
        private SerializedProperty idProperty;
        private SerializedProperty displayNameProperty;
        private SerializedProperty roomTypeProperty;
        private SerializedProperty sizeProperty;
        private SerializedProperty footprintProperty;
        private SerializedProperty floorOffsetsProperty;
        private SerializedProperty boundaryEdgesConfiguredProperty;
        private SerializedProperty allowedBoundaryEdgesProperty;
        private SerializedProperty structuralAnchorsProperty;
        private SerializedProperty semanticAnchorsProperty;
        private SerializedProperty contentGroupsProperty;
        private readonly HashSet<string> selectedAnchorIds = new(StringComparer.Ordinal);
        private int selectedGroup = -1;
        private Vector2 leftScroll, detailsScroll, validationScroll;
        private Vector2 pan;
        private float zoom = 1f;
        private bool fitGrid = true;
        private bool showRoomSettings, showResize, showValidation;
        private bool panning;
        private string anchorSearch = string.Empty;
        private string newGroupId = "group.new";
        private bool newGroupRequired;
        private float newGroupChance = 1f;
        private DungeonDefinition validationDungeon;
        private string poolValidationMessage;
        private bool poolValidationPassed;
        private DungeonRoomTemplateValidationReport validationReport;

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            serializedTemplate?.Dispose();
            serializedTemplate = null;
        }
        private void OnUndoRedo()
        {
            serializedTemplate?.Update();
            ClearAnchorSelection();
            CancelStairDraft();
            poolValidationMessage = null;
            Repaint();
        }

        private EditTool activeTool;
        private DungeonStructuralAnchorType newStructuralType =
            DungeonStructuralAnchorType.Wall;
        private DungeonWallType newWallType = DungeonWallType.Full;
        private DungeonEdgeDirection newStructuralDirection =
            DungeonEdgeDirection.Right;
        private DungeonSemanticAnchorType newSemanticType =
            DungeonSemanticAnchorType.Chest;
        private int selectedStructuralAnchor = -1;
        private int selectedSemanticAnchor = -1;
        private bool? footprintPaintValue;
        private bool showStructuralAnchors = true;
        private bool showSemanticAnchors = true;
        private bool showBoundaryEdges = true;

        [MenuItem("Godmist/Dungeons/Dungeon Room Editor")]
        private static void OpenFromMenu()
        {
            Open(null);
        }

        [MenuItem("CONTEXT/DungeonRoomTemplateDefinition/Open Dungeon Room Editor")]
        private static void OpenFromContextMenu(MenuCommand command)
        {
            Open((DungeonRoomTemplateDefinition)command.context);
        }

        public static void Open(DungeonRoomTemplateDefinition target)
        {
            var window = GetWindow<DungeonRoomEditorWindow>(
                "Dungeon Room Editor");
            window.minSize = new Vector2(960f, 560f);
            window.SetTemplate(target);
            window.Show();
        }

        private void OnGUI()
        {
            if (template != null)
            {
                EnsureSerializedObject();
                serializedTemplate.Update();
                validationReport = DungeonRoomTemplateEditorValidator.Validate(template);
            }
            DrawToolbar();

            if (template == null)
            {
                EditorGUILayout.HelpBox(
                    "Wybierz DungeonRoomTemplateDefinition albo utworz nowy asset.",
                    MessageType.Info);
                return;
            }

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 110f;
            EditorGUILayout.BeginHorizontal();
            DrawLibraryPanel();
            DrawGridPanel();
            DrawSelectionPanel();
            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = previousLabelWidth;
            if (serializedTemplate.ApplyModifiedProperties() || GUI.changed) poolValidationMessage = null;
            DrawValidation(validationReport);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var selected = (DungeonRoomTemplateDefinition)EditorGUILayout.ObjectField(
                template,
                typeof(DungeonRoomTemplateDefinition),
                false,
                GUILayout.Width(280f));

            if (selected != template)
            {
                SetTemplate(selected);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                CreateNewTemplate();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                SaveTemplate();
            }

            GUILayout.FlexibleSpace();
            if (template != null)
            {
                GUILayout.Label(EditorUtility.IsDirty(template) ? "Niezapisane zmiany" : "Zapisano", EditorStyles.miniLabel);
                if (GUILayout.Button(validationReport?.IsValid == true ? "Walidacja: OK" : "Błędy walidacji",
                        EditorStyles.toolbarButton, GUILayout.Width(120f))) showValidation = !showValidation;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLibraryPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(245f), GUILayout.ExpandHeight(true));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            showRoomSettings = EditorGUILayout.Foldout(showRoomSettings, "Ustawienia pokoju", true);
            if (showRoomSettings) DrawTemplateProperties();
            showResize = EditorGUILayout.Foldout(showResize, $"Rozmiar: {sizeProperty.vector2IntValue.x} × {sizeProperty.vector2IntValue.y}", true);
            if (showResize) DrawResizeButtons();
            EditorGUILayout.Space(6f);
            DrawLevelControls();
            EditorGUILayout.LabelField("Narzędzie", EditorStyles.boldLabel);
            activeTool = (EditTool)GUILayout.SelectionGrid((int)activeTool,
                new[] { "Zaznacz", "Podłoga", "Postaw strukturę", "Postaw zawartość", "Przejścia", "Schody" }, 1);
            DrawToolOptions();
            EditorGUILayout.Space(6f);
            anchorSearch = EditorGUILayout.TextField("Szukaj", anchorSearch);
            EditorGUILayout.LabelField($"Grupy ({contentGroupsProperty.arraySize})", EditorStyles.boldLabel);
            for (var i = 0; i < contentGroupsProperty.arraySize; i++)
            {
                var group = contentGroupsProperty.GetArrayElementAtIndex(i);
                var id = Field(group, "Id").stringValue;
                if (!MatchesSearch(id)) continue;
                if (GUILayout.Toggle(selectedGroup == i, $"{id} ({Field(group, "AnchorIds").arraySize})", "Button") && selectedGroup != i)
                    SelectGroup(i);
            }
            EditorGUILayout.LabelField($"Zawartość ({semanticAnchorsProperty.arraySize})", EditorStyles.boldLabel);
            for (var i = 0; i < semanticAnchorsProperty.arraySize; i++)
            {
                var anchor = semanticAnchorsProperty.GetArrayElementAtIndex(i);
                var id = Field(anchor, "Id").stringValue;
                if (!MatchesSearch(id) || activeLevel != 0) continue;
                var label = GetSemanticLabel((DungeonSemanticAnchorType)Field(anchor, "Type").enumValueIndex) + "  " + id;
                if (GUILayout.Button(new GUIContent(label, id), selectedAnchorIds.Contains(id) ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    SelectSemantic(i, Event.current.control || Event.current.command);
            }
            EditorGUILayout.LabelField($"Struktura ({structuralAnchorsProperty.arraySize})", EditorStyles.boldLabel);
            for (var i = 0; i < structuralAnchorsProperty.arraySize; i++)
            {
                var anchor = structuralAnchorsProperty.GetArrayElementAtIndex(i);
                var label = $"{(DungeonStructuralAnchorType)Field(anchor, "Type").enumValueIndex} {Field(anchor, "LocalCell").vector2IntValue}";
                if (!MatchesSearch(label) || Field(anchor, "Level").intValue != activeLevel) continue;
                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    ClearAnchorSelection();
                    selectedStructuralAnchor = i;
                    activeTool = EditTool.Select;
                    showStructuralAnchors = true;
                }
            }
            DrawStairLibrary();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private bool MatchesSearch(string value) => string.IsNullOrWhiteSpace(anchorSearch)
            || value.IndexOf(anchorSearch, StringComparison.OrdinalIgnoreCase) >= 0;

        private static SerializedProperty Field(SerializedProperty property, string name)
            => property.FindPropertyRelative($"<{name}>k__BackingField");

        private void DrawTemplateProperties()
        {
            EditorGUILayout.LabelField(
                "Room template",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(idProperty);
            EditorGUILayout.PropertyField(displayNameProperty);
            EditorGUILayout.PropertyField(roomTypeProperty);

            var sizeBefore = sizeProperty.vector2IntValue;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(sizeProperty);
            EditorGUI.EndDisabledGroup();
            var sizeAfter = sizeProperty.vector2IntValue;

            if (sizeAfter.x < 1 || sizeAfter.y < 1)
            {
                sizeProperty.vector2IntValue = new Vector2Int(
                    Mathf.Max(1, sizeAfter.x),
                    Mathf.Max(1, sizeAfter.y));
            }

            if (sizeBefore != sizeAfter)
            {
                ClearSelectionIfOutsideRoom();
            }

            EditorGUILayout.PropertyField(serializedTemplate.FindProperty("<CanBeRequiredProgressionBranch>k__BackingField"), new GUIContent("Odnoga progresji"));
            EditorGUILayout.PropertyField(serializedTemplate.FindProperty("<AllowedRotationDegrees>k__BackingField"), new GUIContent("Obroty"), true);
        }

        private void DrawGridPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Siatka · poziom {activeLevel}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var nextZoom = GUILayout.HorizontalSlider(zoom, .1f, 3f, GUILayout.Width(90f));
            if (!Mathf.Approximately(nextZoom, zoom)) { zoom = nextZoom; fitGrid = false; }
            GUILayout.Label($"{zoom:P0}", GUILayout.Width(42f));
            if (GUILayout.Button("Dopasuj", EditorStyles.toolbarButton, GUILayout.Width(62f))) fitGrid = true;
            EditorGUILayout.EndHorizontal();
            var viewport = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var size = sizeProperty.vector2IntValue;
            if (fitGrid && Event.current.type == EventType.Repaint)
            {
                zoom = Mathf.Clamp(Mathf.Min((viewport.width - 40f) / (size.x * 34f),
                    (viewport.height - 40f) / (size.y * 34f)), .1f, 3f);
                pan = Vector2.zero;
                fitGrid = false;
            }
            var current = Event.current;
            var panControl = GUIUtility.GetControlID("RoomGridPan".GetHashCode(), FocusType.Passive);
            if (current.type == EventType.MouseDown && current.button == 2 && viewport.Contains(current.mousePosition))
            {
                panning = true;
                GUIUtility.hotControl = panControl;
                current.Use();
            }
            if (current.type == EventType.MouseDrag && panning)
            {
                pan += current.delta;
                current.Use();
                Repaint();
            }
            if (current.type == EventType.MouseUp && panning)
            {
                panning = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }
            if (current.type == EventType.ScrollWheel && viewport.Contains(current.mousePosition))
            {
                var oldZoom = zoom;
                zoom = Mathf.Clamp(zoom * Mathf.Pow(1.1f, -current.delta.y), .1f, 3f);
                pan = (pan - (current.mousePosition - viewport.center)) * (zoom / oldZoom)
                    + current.mousePosition - viewport.center;
                fitGrid = false;
                current.Use();
                Repaint();
            }
            EditorGUI.DrawRect(viewport, new Color(.12f, .13f, .15f));
            GUI.BeginGroup(viewport);
            var cellSize = 34f * zoom;
            var dimensions = new Vector2(size.x * cellSize, size.y * cellSize);
            var gridRect = new Rect((viewport.size - dimensions) * .5f + pan, dimensions);
            DrawGrid(gridRect, cellSize, size);
            if (new Rect(Vector2.zero, viewport.size).Contains(Event.current.mousePosition))
                HandleGridInput(gridRect, cellSize, size);
            GUI.EndGroup();
            EditorGUILayout.LabelField("LPM: aktywne narzędzie • Ctrl+klik: wielokrotny wybór • MMB: przesuń • kółko: zoom", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawResizeButtons()
        {
            foreach (var direction in new[] { DungeonEdgeDirection.Up, DungeonEdgeDirection.Left,
                         DungeonEdgeDirection.Right, DungeonEdgeDirection.Down })
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(direction.ToString(), GUILayout.Width(70f));
                if (GUILayout.Button("−", GUILayout.Width(45f))) ResizeRoom(direction, false);
                if (GUILayout.Button("+", GUILayout.Width(45f))) ResizeRoom(direction, true);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ResizeRoom(
            DungeonEdgeDirection direction,
            bool grow)
        {
            var oldSize = sizeProperty.vector2IntValue;
            var newSize = oldSize;
            var delta = Vector2Int.zero;

            switch (direction)
            {
                case DungeonEdgeDirection.Left:
                    newSize.x += grow ? 1 : -1;
                    delta.x = grow ? 1 : -1;
                    break;
                case DungeonEdgeDirection.Right:
                    newSize.x += grow ? 1 : -1;
                    break;
                case DungeonEdgeDirection.Up:
                    newSize.y += grow ? 1 : -1;
                    break;
                case DungeonEdgeDirection.Down:
                    newSize.y += grow ? 1 : -1;
                    delta.y = grow ? 1 : -1;
                    break;
            }

            if (newSize.x < 1 || newSize.y < 1)
            {
                ShowNotification(new GUIContent(
                    "Rozmiar pokoju nie moze byc mniejszy niz 1x1."));
                return;
            }

            var oldFloorCells = GetFootprintCells(oldSize);

            if (!grow && HasAnchorsInRemovedStrip(
                    oldSize,
                    direction))
            {
                ShowNotification(new GUIContent(
                    "Nie można zmniejszyć pokoju: usuwany pas zawiera kotwę, koniec schodów lub trudny teren."));
                return;
            }

            var oldUsesFullBoundingBox = floorOffsetsProperty.arraySize == 0 && surfacesProperty.arraySize == 0;
            var resizedFloorCells = new HashSet<Vector2Int>();

            if (oldUsesFullBoundingBox)
            {
                for (var x = 0; x < newSize.x; x++)
                {
                    for (var y = 0; y < newSize.y; y++)
                    {
                        resizedFloorCells.Add(new Vector2Int(x, y));
                    }
                }
            }
            else
            {
                foreach (var cell in oldFloorCells)
                {
                    if (!grow && IsInRemovedStrip(cell, oldSize, direction))
                    {
                        continue;
                    }

                    var resizedCell = cell + delta;

                    if (IsInsideRoom(resizedCell, newSize))
                    {
                        resizedFloorCells.Add(resizedCell);
                    }
                }
            }

            if (resizedFloorCells.Count == 0)
            {
                ShowNotification(new GUIContent("Nie można usunąć wszystkich powierzchni pokoju."));
                return;
            }
            ResizeSurfacesAndStairs(oldSize, newSize, direction, grow, delta);
            SetFloorCells(resizedFloorCells, newSize);
            ShiftAnchors(delta);
            ResizeAllowedBoundaryEdges(newSize, delta, resizedFloorCells);
            sizeProperty.vector2IntValue = newSize;

            ClearAnchorSelection();
            CancelStairDraft();
            serializedTemplate.ApplyModifiedProperties();
            Repaint();
        }

        private bool HasAnchorsInRemovedStrip(
            Vector2Int size,
            DungeonEdgeDirection direction)
        {
            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                var firstCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var anchorDirection = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;
                var secondCell = firstCell + GetDirectionOffset(anchorDirection);

                if (IsInRemovedStrip(firstCell, size, direction)
                    || IsInRemovedStrip(secondCell, size, direction))
                {
                    return true;
                }
            }

            for (var index = 0; index < semanticAnchorsProperty.arraySize; index++)
            {
                var property = semanticAnchorsProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var areaSize = property.FindPropertyRelative(
                    "<AreaSize>k__BackingField").vector2IntValue;

                for (var x = localCell.x; x < localCell.x + areaSize.x; x++)
                {
                    for (var y = localCell.y; y < localCell.y + areaSize.y; y++)
                    {
                        if (IsInRemovedStrip(new Vector2Int(x, y), size, direction))
                        {
                            return true;
                        }
                    }
                }
            }

            for (var i = 0; i < stairsProperty.arraySize; i++)
            {
                var stair = stairsProperty.GetArrayElementAtIndex(i);
                if (IsInRemovedStrip(ReadSurface(Field(stair, "First")).Cell, size, direction)
                    || IsInRemovedStrip(ReadSurface(Field(stair, "Second")).Cell, size, direction)
                    || ReadStairPath(stair).Any(cell => IsInRemovedStrip(cell, size, direction))) return true;
            }
            var terrain = serializedTemplate.FindProperty("<DifficultTerrainCells>k__BackingField");
            for (var i = 0; i < terrain.arraySize; i++)
                if (IsInRemovedStrip(terrain.GetArrayElementAtIndex(i).vector2IntValue, size, direction)) return true;
            return false;
        }

        private static bool IsInRemovedStrip(
            Vector2Int cell,
            Vector2Int size,
            DungeonEdgeDirection direction)
        {
            return direction switch
            {
                DungeonEdgeDirection.Left => cell.x == 0,
                DungeonEdgeDirection.Right => cell.x == size.x - 1,
                DungeonEdgeDirection.Up => cell.y == size.y - 1,
                DungeonEdgeDirection.Down => cell.y == 0,
                _ => false
            };
        }

        private void ShiftAnchors(Vector2Int delta)
        {
            if (delta == Vector2Int.zero)
            {
                return;
            }

            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                var localCellProperty = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField");
                localCellProperty.vector2IntValue += delta;
            }

            for (var index = 0; index < semanticAnchorsProperty.arraySize; index++)
            {
                var property = semanticAnchorsProperty.GetArrayElementAtIndex(index);
                var localCellProperty = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField");
                localCellProperty.vector2IntValue += delta;
            }
        }

        private void ResizeAllowedBoundaryEdges(
            Vector2Int newSize,
            Vector2Int delta,
            HashSet<Vector2Int> newFloorCells)
        {
            var transformedEdges = new List<DungeonRoomBoundaryEdgeData>();

            for (var index = 0; index < allowedBoundaryEdgesProperty.arraySize; index++)
            {
                var property = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue + delta;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;

                if (newFloorCells.Contains(localCell)
                    && DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(
                        localCell,
                        direction,
                        newSize,
                        newFloorCells))
                {
                    transformedEdges.Add(
                        new DungeonRoomBoundaryEdgeData(localCell, direction, Field(property, "Level").intValue));
                }
            }

            SetAllowedBoundaryEdges(transformedEdges);
        }

        private void DrawToolOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Preview visibility", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            showStructuralAnchors = GUILayout.Toggle(
                showStructuralAnchors,
                "Structural",
                EditorStyles.toolbarButton);
            showSemanticAnchors = GUILayout.Toggle(
                showSemanticAnchors,
                "Semantic",
                EditorStyles.toolbarButton);
            showBoundaryEdges = GUILayout.Toggle(
                showBoundaryEdges,
                "Boundary",
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            switch (activeTool)
            {
                case EditTool.Footprint:
                    EditorGUILayout.HelpBox("Kliknij lub przeciągaj, aby malować podłogę.", MessageType.None);
                    break;

                case EditTool.Select:
                    EditorGUILayout.HelpBox("Klik zaznacza. Ctrl+klik przełącza kotwę w zaznaczeniu. Usuwanie w panelu właściwości.", MessageType.None);
                    break;

                case EditTool.StructuralAnchor:
                    newStructuralType = (DungeonStructuralAnchorType)EditorGUILayout.EnumPopup(
                        "New type",
                        newStructuralType);
                    newStructuralDirection = (DungeonEdgeDirection)EditorGUILayout.EnumPopup(
                        "Direction",
                        newStructuralDirection);
                    if (newStructuralType == DungeonStructuralAnchorType.Door && activeLevel != 0)
                        EditorGUILayout.HelpBox("Drzwi jako obiekt: tylko poziom 0. Wyjścia pokojów ustaw narzędziem Przejścia.", MessageType.Info);
                    if (newStructuralType == DungeonStructuralAnchorType.Wall)
                        newWallType = (DungeonWallType)EditorGUILayout.EnumPopup("Wall type", newWallType);
                    EditorGUILayout.HelpBox("Full: ściana; Low: murek +2 AP; Railing: balustrada. Kliknij pole przy krawędzi.", MessageType.None);
                    break;

                case EditTool.Stairs:
                    EditorGUILayout.HelpBox(pendingStairStart.HasValue
                        ? $"Początek: {pendingStairStart.Value}. Klikaj kolejne sąsiednie pola. Aby zakończyć, zmień poziom i kliknij powierzchnię końcową."
                        : "Kliknij początek na podłodze, potem kolejne pola trasy. Zmień poziom, aby wskazać koniec.", MessageType.None);
                    if (pendingStairPath.Count > 1 && GUILayout.Button("Cofnij ostatnie pole")) pendingStairPath.RemoveAt(pendingStairPath.Count - 1);
                    if (pendingStairStart.HasValue && GUILayout.Button("Anuluj połączenie")) CancelStairDraft();
                    break;
                case EditTool.SemanticAnchor:
                    if (activeLevel != 0)
                        EditorGUILayout.HelpBox("Kotwy zawartości można obecnie umieszczać tylko na poziomie 0.", MessageType.Info);
                    newSemanticType = (DungeonSemanticAnchorType)EditorGUILayout.EnumPopup(
                        "New type",
                        newSemanticType);
                    EditorGUILayout.HelpBox("Kliknij wolną komórkę, aby postawić kotwę zawartości.", MessageType.None);
                    break;

                case EditTool.BoundaryEdge:
                    EditorGUILayout.HelpBox("Kliknij zewnętrzną krawędź, aby przełączyć przejście.", MessageType.None);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGrid(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            var floorCells = GetFloorCells(size);

            EditorGUI.DrawRect(gridRect, new Color(0.08f, 0.08f, 0.08f));

            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var cellRect = GetCellRect(gridRect, cellSize, size, cell);
                    var isFloor = floorCells.Contains(cell);

                    EditorGUI.DrawRect(
                        cellRect,
                        isFloor
                            ? new Color(0.28f, 0.31f, 0.36f)
                            : new Color(0.11f, 0.11f, 0.11f));

                }
            }

            DrawOtherLevelPreview(gridRect, cellSize, size);
            // Draw after every cell fill, using the same GUI clipping space as the viewport.
            // Handles.DrawLine does not share the nested GUI group's coordinates reliably.
            var gridColor = new Color(0f, 0f, 0f, 0.45f);
            for (var x = 0; x <= size.x; x++)
                EditorGUI.DrawRect(new Rect(gridRect.x + x * cellSize, gridRect.y, 1f, gridRect.height), gridColor);
            for (var y = 0; y <= size.y; y++)
                EditorGUI.DrawRect(new Rect(gridRect.x, gridRect.y + y * cellSize, gridRect.width, 1f), gridColor);

            DrawBoundaryEdges(gridRect, cellSize, size);
            DrawStructuralAnchors(gridRect, cellSize, size);
            DrawSemanticAnchors(gridRect, cellSize, size);
            DrawStairMarkers(gridRect, cellSize, size);
            if (selectedSurface.HasValue && selectedSurface.Value.Level == activeLevel)
                DrawSelectionOutline(GetCellRect(gridRect, cellSize, size, selectedSurface.Value.Cell));
        }

        private void DrawBoundaryEdges(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            if (!showBoundaryEdges)
            {
                return;
            }

            foreach (var edge in GetExternalBoundaryEdges(size))
            {
                var cellRect = GetCellRect(
                    gridRect,
                    cellSize,
                    size,
                    edge.LocalCell);
                var edgeRect = GetBoundaryEdgeRect(
                    cellRect,
                    edge.Direction,
                    6f);
                var color = IsBoundaryEdgeAllowed(edge)
                    ? new Color(0.25f, 0.85f, 0.35f, 0.95f)
                    : new Color(0.75f, 0.3f, 0.3f, 0.72f);

                EditorGUI.DrawRect(edgeRect, color);
            }
        }

        private void DrawStructuralAnchors(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            if (!showStructuralAnchors) return;

            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                if (Field(property, "Level").intValue != activeLevel) continue;
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var type = (DungeonStructuralAnchorType)property.FindPropertyRelative(
                    "<Type>k__BackingField").enumValueIndex;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;
                var cellRect = GetCellRect(gridRect, cellSize, size, localCell);
                var color = type == DungeonStructuralAnchorType.Door
                    ? new Color(0.85f, 0.55f, 0.15f, 0.95f)
                    : new Color(0.75f, 0.75f, 0.8f, 0.95f);

                var wallType = (DungeonWallType)property.FindPropertyRelative("<WallType>k__BackingField").enumValueIndex;
                if (type == DungeonStructuralAnchorType.Wall && wallType != DungeonWallType.Full)
                    color = wallType == DungeonWallType.Low ? new Color(0.65f, 0.48f, 0.28f) : new Color(0.35f, 0.75f, 0.8f);
                DrawAnchorMarker(cellRect, color, type == DungeonStructuralAnchorType.Door ? "D"
                    : wallType == DungeonWallType.Low ? "L" : wallType == DungeonWallType.Railing ? "R" : "W");

                var neighbour = localCell + GetDirectionOffset(direction);
                var neighbourRect = GetCellRect(gridRect, cellSize, size, neighbour);
                DrawEdgeMarker(cellRect, neighbourRect, color);
                if (index == selectedStructuralAnchor) DrawSelectionOutline(cellRect);
            }
        }

        private void DrawSemanticAnchors(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            if (!showSemanticAnchors || activeLevel != 0) return;

            for (var index = 0; index < semanticAnchorsProperty.arraySize; index++)
            {
                var property = semanticAnchorsProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var areaSize = property.FindPropertyRelative(
                    "<AreaSize>k__BackingField").vector2IntValue;
                var type = (DungeonSemanticAnchorType)property.FindPropertyRelative(
                    "<Type>k__BackingField").enumValueIndex;
                var areaRect = GetAreaRect(
                    gridRect,
                    cellSize,
                    size,
                    localCell,
                    areaSize);

                DrawAnchorMarker(
                    areaRect,
                    new Color(0.25f, 0.75f, 0.95f, 0.82f),
                    GetSemanticLabel(type));
                if (selectedAnchorIds.Contains(Field(property, "Id").stringValue)) DrawSelectionOutline(areaRect);
            }
        }

        private static void DrawSelectionOutline(Rect rect)
        {
            var color = new Color(1f, .8f, .15f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), color);
        }

        private void DrawSelectionPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300f), GUILayout.ExpandHeight(true));
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            if (selectedStair >= 0 && selectedStair < stairsProperty.arraySize) DrawStairSelection();
            else if (selectedSurface.HasValue) DrawSurfaceSelection();
            else if (selectedGroup >= 0 && selectedGroup < contentGroupsProperty.arraySize) DrawGroupSelection();
            else if (selectedAnchorIds.Count > 1) DrawMultiSelection();
            else if (IsValidSemanticSelection()) DrawSemanticSelection();
            else if (IsValidStructuralSelection()) DrawStructuralSelection();
            else
            {
                EditorGUILayout.LabelField("Właściwości", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Wybierz kotwę na siatce lub na liście. Ctrl+klik dodaje/usuwa kotwę z zaznaczenia. Stawianie wymaga osobnego narzędzia.", MessageType.Info);
                if (activeTool == EditTool.BoundaryEdge) DrawBoundarySelection();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStructuralSelection()
        {
            EditorGUILayout.LabelField(
                "Selected structural anchor",
                EditorStyles.boldLabel);

            if (!IsValidStructuralSelection())
            {
                EditorGUILayout.HelpBox(
                    "Kliknij istniejaca kotwice na siatce, aby edytowac jej typ "
                    + "i kierunek.",
                    MessageType.Info);
                return;
            }

            var property = structuralAnchorsProperty.GetArrayElementAtIndex(
                selectedStructuralAnchor);
            EditorGUILayout.LabelField("Poziom", Field(property, "Level").intValue.ToString());
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<Type>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<LocalCell>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<Direction>k__BackingField"));

            if ((DungeonStructuralAnchorType)property.FindPropertyRelative("<Type>k__BackingField").enumValueIndex
                == DungeonStructuralAnchorType.Wall)
                EditorGUILayout.PropertyField(property.FindPropertyRelative("<WallType>k__BackingField"));
            if (GUILayout.Button("Remove selected anchor"))
            {
                structuralAnchorsProperty.DeleteArrayElementAtIndex(
                    selectedStructuralAnchor);
                selectedStructuralAnchor = -1;
            }
        }

        private void DrawSemanticSelection()
        {
            EditorGUILayout.LabelField("Kotwa zawartości", EditorStyles.boldLabel);
            var property = semanticAnchorsProperty.GetArrayElementAtIndex(selectedSemanticAnchor);
            var oldId = Field(property, "Id").stringValue;
            var nextId = EditorGUILayout.DelayedTextField("Id", oldId);
            if (nextId != oldId)
            {
                if (DungeonRoomEditorContentEdits.RenameAnchor(serializedTemplate, oldId, nextId, out var error))
                {
                    selectedAnchorIds.Remove(oldId);
                    selectedAnchorIds.Add(nextId);
                }
                else ShowNotification(new GUIContent(error));
            }
            var typeProperty = Field(property, "Type");
            var previousType = typeProperty.enumValueIndex;
            EditorGUILayout.PropertyField(typeProperty, new GUIContent("Typ"));
            var groupIndex = FindGroupForAnchor(Field(property, "Id").stringValue);
            var enemy = typeProperty.enumValueIndex == (int)DungeonSemanticAnchorType.Enemy;
            if (enemy && previousType != typeProperty.enumValueIndex)
            {
                if (groupIndex >= 0 || HasDependents(Field(property, "Id").stringValue))
                {
                    typeProperty.enumValueIndex = previousType;
                    enemy = false;
                    ShowNotification(new GUIContent("Najpierw usuń członkostwo grupy i zależności od tej kotwy."));
                }
                else
                {
                    Field(property, "RequiresAnchorId").stringValue = string.Empty;
                    Field(property, "ObjectDefinition").objectReferenceValue = null;
                    Field(property, "AreaSize").vector2IntValue = Vector2Int.one;
                    Field(property, "SpawnChance").floatValue = 1f;
                    Field(property, "IsRequired").boolValue = false;
                }
            }
            EditorGUILayout.PropertyField(Field(property, "LocalCell"), new GUIContent("Pole"));
            if (enemy)
            {
                EditorGUILayout.PropertyField(Field(property, "EncounterEnemyIndex"), new GUIContent("Indeks przeciwnika"));
                EditorGUILayout.HelpBox("Indeks na liście Enemies w EncounterDefinition, liczony od 0.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(Field(property, "AreaSize"), new GUIContent("Obszar"));
                EditorGUILayout.PropertyField(Field(property, "ObjectDefinition"), new GUIContent("Definicja obiektu"));
                EditorGUILayout.HelpBox("Puste: losowanie z puli typu kotwy. Konkretna definicja musi należeć do puli lochu.", MessageType.None);
                if (groupIndex < 0)
                {
                    EditorGUILayout.PropertyField(Field(property, "IsRequired"), new GUIContent("Stały element"));
                    using (new EditorGUI.DisabledScope(Field(property, "IsRequired").boolValue))
                    {
                        DrawChance(Field(property, "SpawnChance"));
                        DrawDistributionKey(property);
                    }
                }
                else
                {
                    var group = contentGroupsProperty.GetArrayElementAtIndex(groupIndex);
                    EditorGUILayout.HelpBox("Wymaganie i szansę określa grupa: " + Field(group, "Id").stringValue, MessageType.Info);
                    if (GUILayout.Button("Edytuj grupę")) SelectGroup(groupIndex);
                }
                DrawDependencyPicker(property);
                if ((DungeonSemanticAnchorType)typeProperty.enumValueIndex == DungeonSemanticAnchorType.Trap)
                    EditorGUILayout.PropertyField(Field(property, "ActivationGroupId"), new GUIContent("Grupa aktywacji"));
                if (groupIndex < 0) DrawCreateGroup();
                else if (GUILayout.Button("Odłącz od grupy"))
                {
                    var id = Field(property, "Id").stringValue;
                    if (!DungeonRoomEditorContentEdits.DetachAnchor(serializedTemplate, id, out var error))
                        ShowNotification(new GUIContent(error));
                }
                DrawJoinGroup(property, groupIndex);
            }
            if (GUILayout.Button("Usuń kotwę")) DeleteSelectedSemantic();
        }

        private bool HasDependents(string id)
        {
            for (var i = 0; i < semanticAnchorsProperty.arraySize; i++)
                if (Field(semanticAnchorsProperty.GetArrayElementAtIndex(i), "RequiresAnchorId").stringValue == id) return true;
            return false;
        }

        private void DrawDependencyPicker(SerializedProperty anchor)
        {
            var ownId = Field(anchor, "Id").stringValue;
            var dependency = Field(anchor, "RequiresAnchorId");
            var ids = new List<string> { string.Empty };
            var labels = new List<string> { "Brak" };
            for (var i = 0; i < semanticAnchorsProperty.arraySize; i++)
            {
                var other = semanticAnchorsProperty.GetArrayElementAtIndex(i);
                var id = Field(other, "Id").stringValue;
                if (id == ownId || Field(other, "Type").enumValueIndex == (int)DungeonSemanticAnchorType.Enemy) continue;
                ids.Add(id);
                labels.Add(id);
            }
            if (!ids.Contains(dependency.stringValue)) { ids.Add(dependency.stringValue); labels.Add("Brak kotwy: " + dependency.stringValue); }
            var selected = ids.IndexOf(dependency.stringValue);
            var next = EditorGUILayout.Popup("Wymaga kotwy", selected, labels.ToArray());
            if (next != selected) dependency.stringValue = ids[next];
        }

        private void DrawJoinGroup(SerializedProperty anchor, int currentGroup)
        {
            var labels = new List<string> { "Wybierz grupę…" };
            var indices = new List<int>();
            for (var i = 0; i < contentGroupsProperty.arraySize; i++)
            {
                if (i == currentGroup) continue;
                labels.Add(Field(contentGroupsProperty.GetArrayElementAtIndex(i), "Id").stringValue);
                indices.Add(i);
            }
            if (indices.Count == 0) return;
            var next = EditorGUILayout.Popup("Przypisz do", 0, labels.ToArray());
            if (next == 0) return;
            if (!DungeonRoomEditorContentEdits.JoinGroup(serializedTemplate, Field(anchor, "Id").stringValue, indices[next - 1], out var error))
                ShowNotification(new GUIContent(error));
        }

        private static void DrawChance(SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            var percent = EditorGUILayout.FloatField("Szansa (%)", property.floatValue * 100f);
            if (EditorGUI.EndChangeCheck() && float.IsFinite(percent)) property.floatValue = Mathf.Clamp01(percent / 100f);
        }

        private void DrawMultiSelection()
        {
            EditorGUILayout.LabelField($"Zaznaczono: {selectedAnchorIds.Count}", EditorStyles.boldLabel);
            foreach (var id in selectedAnchorIds.OrderBy(id => id, StringComparer.Ordinal)) EditorGUILayout.LabelField(id);
            DrawCreateGroup();
            if (GUILayout.Button("Usuń zaznaczone kotwy")) DeleteSelectedSemantic();
        }

        private void DrawCreateGroup()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Grupa z zaznaczenia", EditorStyles.boldLabel);
            newGroupId = EditorGUILayout.TextField("Id grupy", newGroupId);
            newGroupRequired = EditorGUILayout.Toggle("Stała grupa", newGroupRequired);
            using (new EditorGUI.DisabledScope(newGroupRequired))
                newGroupChance = Mathf.Clamp01(EditorGUILayout.FloatField("Szansa (%)", newGroupChance * 100f) / 100f);
            EditorGUILayout.HelpBox("Kotwy przejmą wspólne wymaganie i szansę. Zaznaczaj nieprzypisane kotwy; przeciwnicy nie należą do grup zawartości.", MessageType.None);
            if (!GUILayout.Button("Utwórz grupę")) return;
            if (DungeonRoomEditorContentEdits.CreateGroup(serializedTemplate, newGroupId, selectedAnchorIds,
                    newGroupRequired, newGroupChance, out var error))
                SelectGroup(contentGroupsProperty.arraySize - 1);
            else ShowNotification(new GUIContent(error));
        }

        private void DrawGroupSelection()
        {
            var group = contentGroupsProperty.GetArrayElementAtIndex(selectedGroup);
            EditorGUILayout.LabelField("Grupa zawartości", EditorStyles.boldLabel);
            var id = Field(group, "Id");
            var nextId = EditorGUILayout.DelayedTextField("Id", id.stringValue);
            if (nextId != id.stringValue)
            {
                var duplicate = false;
                for (var i = 0; i < contentGroupsProperty.arraySize; i++)
                    if (i != selectedGroup && Field(contentGroupsProperty.GetArrayElementAtIndex(i), "Id").stringValue == nextId) duplicate = true;
                if (string.IsNullOrWhiteSpace(nextId) || duplicate) ShowNotification(new GUIContent("Id grupy musi być niepuste i unikalne."));
                else id.stringValue = nextId;
            }
            EditorGUILayout.PropertyField(Field(group, "IsRequired"), new GUIContent("Stała grupa"));
            using (new EditorGUI.DisabledScope(Field(group, "IsRequired").boolValue)) DrawChance(Field(group, "SpawnChance"));
            using (new EditorGUI.DisabledScope(Field(group, "IsRequired").boolValue)) DrawDistributionKey(group);
            EditorGUILayout.HelpBox("Cały komplet albo nic. Nie jest to grupa wspólnej aktywacji pułapek.", MessageType.Info);
            var members = Field(group, "AnchorIds");
            EditorGUILayout.LabelField($"Kotwy ({members.arraySize})", EditorStyles.boldLabel);
            for (var i = 0; i < members.arraySize; i++)
            {
                var memberId = members.GetArrayElementAtIndex(i).stringValue;
                if (GUILayout.Button(memberId, EditorStyles.miniButton))
                {
                    var index = FindSemanticIndex(memberId);
                    if (index >= 0) SelectSemantic(index, false);
                    else ShowNotification(new GUIContent("Brak kotwy: " + memberId));
                }
            }
            if (GUILayout.Button("Rozwiąż grupę — zachowaj kotwy"))
            {
                if (EditorUtility.DisplayDialog("Rozwiąż grupę", "Kotwy przejmą wymaganie i szansę grupy, ale będą losowane osobno. Zniknie gwarancja wspólnego wystąpienia.", "Rozwiąż", "Anuluj"))
                {
                    DungeonRoomEditorContentEdits.DissolveGroup(serializedTemplate, selectedGroup);
                    selectedGroup = -1;
                }
            }
            if (GUILayout.Button("Usuń grupę wraz z kotwami")) DeleteSelectedSemantic();
        }

        private int FindGroupForAnchor(string id) => DungeonRoomEditorContentEdits.FindGroup(serializedTemplate, id);
        private int FindSemanticIndex(string id)
        {
            for (var i = 0; i < semanticAnchorsProperty.arraySize; i++)
                if (Field(semanticAnchorsProperty.GetArrayElementAtIndex(i), "Id").stringValue == id) return i;
            return -1;
        }

        private void ClearAnchorSelection()
        {
            selectedAnchorIds.Clear();
            selectedStair = -1;
            selectedSurface = null;
            selectedSemanticAnchor = selectedStructuralAnchor = selectedGroup = -1;
        }

        private void SelectSemantic(int index, bool additive)
        {
            ChangeLevel(0);
            var id = Field(semanticAnchorsProperty.GetArrayElementAtIndex(index), "Id").stringValue;
            if (!additive) ClearAnchorSelection();
            selectedGroup = selectedStructuralAnchor = -1;
            if (additive && selectedAnchorIds.Contains(id)) selectedAnchorIds.Remove(id);
            else selectedAnchorIds.Add(id);
            selectedSemanticAnchor = selectedAnchorIds.Contains(id) ? index
                : selectedAnchorIds.Count == 0 ? -1 : FindSemanticIndex(selectedAnchorIds.OrderBy(s => s, StringComparer.Ordinal).First());
            activeTool = EditTool.Select;
            showSemanticAnchors = true;
        }

        private void SelectGroup(int index)
        {
            ChangeLevel(0);
            ClearAnchorSelection();
            if (index < 0 || index >= contentGroupsProperty.arraySize) return;
            selectedGroup = index;
            var members = Field(contentGroupsProperty.GetArrayElementAtIndex(index), "AnchorIds");
            for (var i = 0; i < members.arraySize; i++) selectedAnchorIds.Add(members.GetArrayElementAtIndex(i).stringValue);
            activeTool = EditTool.Select;
            showSemanticAnchors = true;
        }

        private void DeleteSelectedSemantic()
        {
            if (!DungeonRoomEditorContentEdits.DeleteAnchors(serializedTemplate, selectedAnchorIds, out var error))
                ShowNotification(new GUIContent(error));
            else ClearAnchorSelection();
        }
        private void DrawBoundarySelection()
        {
            EditorGUILayout.LabelField(
                "External passage edges",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Zielone krawedzie sa dozwolone dla generatora. Czerwone sa zablokowane. "
                + "Kliknij pojedynczy odcinek na siatce, aby zmienic stan.",
                MessageType.Info);

            if (GUILayout.Button("Enable all legal edges"))
            {
                SetAllBoundaryEdges(true);
            }

            if (GUILayout.Button("Disable all edges"))
            {
                SetAllBoundaryEdges(false);
            }

            var allowedCount = CountAllowedBoundaryEdges();
            var totalCount = GetExternalBoundaryEdges(
                sizeProperty.vector2IntValue).Count;
            EditorGUILayout.LabelField(
                $"Allowed edges: {allowedCount}/{totalCount}");
        }

        private void HandleGridInput(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            ProcessGridInput(Event.current, gridRect, cellSize, size);
        }

        private void ProcessGridInput(Event currentEvent, Rect gridRect, float cellSize, Vector2Int size)
        {

            if (currentEvent.type == EventType.MouseUp)
            {
                footprintPaintValue = null;
                return;
            }

            if (currentEvent.type != EventType.MouseDown
                && currentEvent.type != EventType.MouseDrag)
            {
                return;
            }

            if (!gridRect.Contains(currentEvent.mousePosition)) return;

            if (currentEvent.button != 0) return;

            if (activeTool == EditTool.BoundaryEdge)
            {
                if (currentEvent.type == EventType.MouseDown
                    && TryGetBoundaryEdgeAtPosition(
                        gridRect,
                        cellSize,
                        size,
                        currentEvent.mousePosition,
                        out var boundaryEdge))
                {
                    ToggleBoundaryEdge(boundaryEdge);
                    currentEvent.Use();
                    Repaint();
                }

                return;
            }

            var cell = GetCellFromPosition(
                gridRect,
                cellSize,
                size,
                currentEvent.mousePosition);

            if (!IsInsideRoom(cell, size)) return;

            switch (activeTool)
            {
                case EditTool.Select:
                    if (currentEvent.type == EventType.MouseDown)
                    {
                        var semantic = showSemanticAnchors ? FindSemanticAnchorAt(cell, size) : -1;
                        var structural = showStructuralAnchors ? FindStructuralAnchorAt(cell, size) : -1;
                        if (semantic >= 0) SelectSemantic(semantic, currentEvent.control || currentEvent.command);
                        else if (structural >= 0)
                        {
                            ClearAnchorSelection();
                            selectedStructuralAnchor = structural;
                        }
                        else if (!currentEvent.control && !currentEvent.command)
                        {
                            ClearAnchorSelection();
                            var surface = new Godmist.Runtime.Dungeons.DungeonSurfacePosition(cell, activeLevel);
                            selectedStair = FindStairAt(surface);
                            if (selectedStair < 0 && GetFloorCells(size).Contains(cell)) selectedSurface = surface;
                        }
                    }
                    break;
                case EditTool.Footprint:
                    PaintFootprintCell(cell, currentEvent.type == EventType.MouseDown, size);
                    break;

                case EditTool.StructuralAnchor:
                    if (currentEvent.type == EventType.MouseDown)
                    {
                        SelectOrAddStructuralAnchor(cell, size);
                    }
                    break;

                case EditTool.SemanticAnchor:
                    if (currentEvent.type == EventType.MouseDown)
                    {
                        SelectOrAddSemanticAnchor(cell, size);
                    }
                    break;

                case EditTool.Stairs:
                    if (currentEvent.type == EventType.MouseDown) PlaceStairEndpoint(cell);
                    break;
                case EditTool.BoundaryEdge:
                    break;
            }

            currentEvent.Use();
            Repaint();
        }

        private void PaintFootprintCell(
            Vector2Int cell,
            bool isMouseDown,
            Vector2Int size)
        {
            var floorCells = GetFloorCells(size);

            if (isMouseDown)
            {
                footprintPaintValue = !floorCells.Contains(cell);
            }

            if (!footprintPaintValue.HasValue) return;

            if (!TryPaintSurface(cell, activeLevel, footprintPaintValue.Value, out var error))
                ShowNotification(new GUIContent(error));
        }

        private void SelectOrAddStructuralAnchor(
            Vector2Int cell,
            Vector2Int size)
        {
            if (newStructuralType == DungeonStructuralAnchorType.Door && activeLevel != 0) return;
            var floorCells = GetFloorCells(size);
            var secondCell = cell + GetDirectionOffset(newStructuralDirection);

            if (!floorCells.Contains(cell) || (newStructuralType != DungeonStructuralAnchorType.Wall && !floorCells.Contains(secondCell)))
            {
                ShowNotification(new GUIContent(
                    "Structural anchor musi laczyc dwie komorki podlogi."));
                return;
            }

            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                if (Field(property, "Level").intValue != activeLevel) continue;
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;
                var otherCell = localCell + GetDirectionOffset(direction);

                if (AreSameEdge(cell, secondCell, localCell, otherCell))
                {
                    ClearAnchorSelection();
                    selectedStructuralAnchor = index;
                    return;
                }
            }

            var newIndex = structuralAnchorsProperty.arraySize;
            structuralAnchorsProperty.InsertArrayElementAtIndex(newIndex);
            var element = structuralAnchorsProperty.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative("<Type>k__BackingField").enumValueIndex =
                (int)newStructuralType;
            element.FindPropertyRelative("<LocalCell>k__BackingField").vector2IntValue =
                cell;
            element.FindPropertyRelative("<Direction>k__BackingField").enumValueIndex =
                (int)newStructuralDirection;
            element.FindPropertyRelative("<WallType>k__BackingField").enumValueIndex = (int)newWallType;
            Field(element, "Level").intValue = activeLevel;
            ClearAnchorSelection();
            selectedStructuralAnchor = newIndex;
        }

        private void SelectOrAddSemanticAnchor(
            Vector2Int cell,
            Vector2Int size)
        {
            if (activeLevel != 0) return;
            var existing = FindSemanticAnchorAt(cell, size);

            if (existing >= 0)
            {
                ClearAnchorSelection();
                selectedSemanticAnchor = existing;
                selectedAnchorIds.Add(Field(semanticAnchorsProperty.GetArrayElementAtIndex(existing), "Id").stringValue);
                return;
            }

            if (!GetFloorCells(size).Contains(cell))
            {
                ShowNotification(new GUIContent(
                    "Semantic anchor musi zaczynac sie na komorce podlogi."));
                return;
            }

            var newIndex = semanticAnchorsProperty.arraySize;
            semanticAnchorsProperty.InsertArrayElementAtIndex(newIndex);
            var element = semanticAnchorsProperty.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative("<Id>k__BackingField").stringValue =
                GetUniqueSemanticAnchorId();
            element.FindPropertyRelative("<Type>k__BackingField").enumValueIndex =
                (int)newSemanticType;
            element.FindPropertyRelative("<LocalCell>k__BackingField").vector2IntValue =
                cell;
            element.FindPropertyRelative("<AreaSize>k__BackingField").vector2IntValue =
                Vector2Int.one;
            element.FindPropertyRelative("<SpawnChance>k__BackingField").floatValue =
                1f;
            element.FindPropertyRelative("<IsRequired>k__BackingField").boolValue =
                false;
            element.FindPropertyRelative("<RequiresAnchorId>k__BackingField").stringValue =
                string.Empty;
            element.FindPropertyRelative(
                "<ActivationGroupId>k__BackingField").stringValue = string.Empty;
            element.FindPropertyRelative(
                "<EncounterEnemyIndex>k__BackingField").intValue = -1;
            element.FindPropertyRelative("<ObjectDefinition>k__BackingField").objectReferenceValue = null;
            Field(element, "DistributionKey").stringValue = string.Empty;
            ClearAnchorSelection();
            selectedSemanticAnchor = newIndex;
            selectedAnchorIds.Add(Field(element, "Id").stringValue);
        }

        private void DrawValidation(DungeonRoomTemplateValidationReport report)
        {
            if (report == null) return;
            showValidation = EditorGUILayout.Foldout(showValidation,
                report.IsValid ? "Walidacja szablonu: OK" : $"Walidacja: {report.Errors.Count} błędów", true);
            if (!showValidation) return;
            validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.Height(140f));
            foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.BeginHorizontal();
            var nextDungeon = (DungeonDefinition)EditorGUILayout.ObjectField("Pula lochu", validationDungeon, typeof(DungeonDefinition), false);
            if (nextDungeon != validationDungeon) { validationDungeon = nextDungeon; poolValidationMessage = null; }
            using (new EditorGUI.DisabledScope(validationDungeon == null))
                if (GUILayout.Button("Sprawdź pulę", GUILayout.Width(105f)))
                {
                    try
                    {
                        if (validationDungeon.RoomTemplateCatalog == null || !validationDungeon.RoomTemplateCatalog.Templates.Contains(template))
                            throw new InvalidOperationException("Wybrany loch nie zawiera tego szablonu.");
                        DungeonObjectPlacementSystem.ValidateContentDefinitions(validationDungeon);
                        poolValidationMessage = "Pula: OK dla poziomu z definicji lochu. Runtime sprawdzi miejsca po wygenerowaniu drzwi i przeciwników.";
                        poolValidationPassed = true;
                    }
                    catch (InvalidOperationException exception)
                    {
                        poolValidationMessage = exception.Message;
                        poolValidationPassed = false;
                    }
                }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(poolValidationMessage))
                EditorGUILayout.HelpBox(poolValidationMessage, poolValidationPassed ? MessageType.Info : MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void SaveTemplate()
        {
            if (template == null) return;

            var report = DungeonRoomTemplateEditorValidator.Validate(template);

            if (!report.IsValid)
            {
                ShowNotification(new GUIContent(
                    "Nie mozna zapisac: room template jest niepoprawny."));
                return;
            }

            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
            ShowNotification(new GUIContent("Room template zapisany."));
        }

        private void CreateNewTemplate()
        {
            const string folder =
                "Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates";

            var path = EditorUtility.SaveFilePanelInProject(
                "Create Dungeon Room Template",
                "DungeonRoomTemplate",
                "asset",
                "Wybierz miejsce dla nowego room template.",
                folder);

            if (string.IsNullOrWhiteSpace(path)) return;

            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var created = CreateInstance<DungeonRoomTemplateDefinition>();
            var serialized = new SerializedObject(created);
            serialized.FindProperty("<Id>k__BackingField").stringValue =
                BuildDefaultId(path);
            serialized.FindProperty("<DisplayName>k__BackingField").stringValue =
                System.IO.Path.GetFileNameWithoutExtension(path);
            serialized.FindProperty("<RoomType>k__BackingField").enumValueIndex =
                (int)DungeonRoomType.Normal;
            serialized.FindProperty("<SizeInCells>k__BackingField").vector2IntValue =
                new Vector2Int(8, 8);
            serialized.FindProperty("<BoundaryEdgesConfigured>k__BackingField").boolValue =
                true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssetIfDirty(created);
            SetTemplate(created);
        }

        private void SetTemplate(DungeonRoomTemplateDefinition nextTemplate)
        {
            template = nextTemplate;
            serializedTemplate?.Dispose();
            serializedTemplate = null;
            ClearAnchorSelection();
            activeTool = EditTool.Select;
            activeLevel = 0;
            CancelStairDraft();
            fitGrid = true;
            pan = Vector2.zero;
            poolValidationMessage = null;
            footprintPaintValue = null;

            if (template != null)
            {
                Selection.activeObject = template;
                titleContent = new GUIContent(
                    $"Room Editor - {template.name}");
            }
        }

        private void EnsureSerializedObject()
        {
            if (serializedTemplate != null) return;

            serializedTemplate = new SerializedObject(template);
            idProperty = serializedTemplate.FindProperty(
                "<Id>k__BackingField");
            displayNameProperty = serializedTemplate.FindProperty(
                "<DisplayName>k__BackingField");
            roomTypeProperty = serializedTemplate.FindProperty(
                "<RoomType>k__BackingField");
            sizeProperty = serializedTemplate.FindProperty(
                "<SizeInCells>k__BackingField");
            footprintProperty = serializedTemplate.FindProperty(
                "<Footprint>k__BackingField");
            floorOffsetsProperty = footprintProperty.FindPropertyRelative(
                "<FloorCellOffsets>k__BackingField");
            boundaryEdgesConfiguredProperty = serializedTemplate.FindProperty(
                "<BoundaryEdgesConfigured>k__BackingField");
            allowedBoundaryEdgesProperty = serializedTemplate.FindProperty(
                "<AllowedBoundaryEdges>k__BackingField");
            structuralAnchorsProperty = serializedTemplate.FindProperty(
                "<StructuralAnchors>k__BackingField");
            semanticAnchorsProperty = serializedTemplate.FindProperty(
                "<SemanticAnchors>k__BackingField");
            contentGroupsProperty = serializedTemplate.FindProperty("<ContentGroups>k__BackingField");
            surfacesProperty = serializedTemplate.FindProperty("<Surfaces>k__BackingField");
            stairsProperty = serializedTemplate.FindProperty("<Stairs>k__BackingField");

            if (!boundaryEdgesConfiguredProperty.boolValue)
            {
                template.MigrateLegacyBoundaryEdges();
                serializedTemplate.Update();
            }
        }

        private HashSet<Vector2Int> GetFootprintCells(Vector2Int size)
        {
            var floorCells = new HashSet<Vector2Int>();

            if (floorOffsetsProperty.arraySize == 0)
            {
                for (var x = 0; x < size.x; x++)
                {
                    for (var y = 0; y < size.y; y++)
                    {
                        floorCells.Add(new Vector2Int(x, y));
                    }
                }

                return floorCells;
            }

            for (var index = 0; index < floorOffsetsProperty.arraySize; index++)
            {
                floorCells.Add(
                    floorOffsetsProperty.GetArrayElementAtIndex(index).vector2IntValue);
            }

            return floorCells;
        }

        private void SetFloorCells(
            HashSet<Vector2Int> floorCells,
            Vector2Int size)
        {
            var isFullBoundingBox = floorCells.Count == size.x * size.y;
            var orderedCells = new List<Vector2Int>(floorCells);
            orderedCells.Sort(CompareCells);

            floorOffsetsProperty.arraySize = isFullBoundingBox
                ? 0
                : orderedCells.Count;

            if (isFullBoundingBox) return;

            for (var index = 0; index < orderedCells.Count; index++)
            {
                floorOffsetsProperty.GetArrayElementAtIndex(index).vector2IntValue =
                orderedCells[index];
            }
        }

        private List<DungeonRoomBoundaryEdgeData> GetExternalBoundaryEdges(Vector2Int size)
        {
            var activeCells = GetFloorCells(size);
            return DungeonRoomBoundaryUtility.EnumerateExternalBoundaryEdges(size, GetFootprintCells(size))
                .Where(edge => activeCells.Contains(edge.LocalCell))
                .Select(edge => new DungeonRoomBoundaryEdgeData(edge.LocalCell, edge.Direction, activeLevel)).ToList();
        }

        private bool IsBoundaryEdgeAllowed(
            DungeonRoomBoundaryEdgeData edge)
        {
            for (var i = 0; i < structuralAnchorsProperty.arraySize; i++)
            {
                var wall = structuralAnchorsProperty.GetArrayElementAtIndex(i);
                if (Field(wall, "Level").intValue == edge.Level
                    && wall.FindPropertyRelative("<Type>k__BackingField").enumValueIndex == (int)DungeonStructuralAnchorType.Wall
                    && wall.FindPropertyRelative("<LocalCell>k__BackingField").vector2IntValue == edge.LocalCell
                    && wall.FindPropertyRelative("<Direction>k__BackingField").enumValueIndex == (int)edge.Direction)
                    return false;
            }
            if (!boundaryEdgesConfiguredProperty.boolValue)
            {
                return true;
            }

            return ContainsBoundaryEdge(
                edge.LocalCell,
                edge.Direction);
        }

        private bool ContainsBoundaryEdge(
            Vector2Int localCell,
            DungeonEdgeDirection direction)
        {
            for (var index = 0; index < allowedBoundaryEdgesProperty.arraySize; index++)
            {
                var property = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(index);
                var candidateCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var candidateDirection = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;

                if (candidateCell == localCell && candidateDirection == direction && Field(property, "Level").intValue == activeLevel)
                {
                    return true;
                }
            }

            return false;
        }

        private void ToggleBoundaryEdge(
            DungeonRoomBoundaryEdgeData edge)
        {
            boundaryEdgesConfiguredProperty.boolValue = true;

            for (var index = 0; index < allowedBoundaryEdgesProperty.arraySize; index++)
            {
                var property = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;

                if (localCell != edge.LocalCell || direction != edge.Direction || Field(property, "Level").intValue != edge.Level)
                {
                    continue;
                }

                allowedBoundaryEdgesProperty.DeleteArrayElementAtIndex(index);
                return;
            }

            var newIndex = allowedBoundaryEdgesProperty.arraySize;
            allowedBoundaryEdgesProperty.InsertArrayElementAtIndex(newIndex);
            var newProperty = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(newIndex);
            newProperty.FindPropertyRelative("<LocalCell>k__BackingField").vector2IntValue =
                edge.LocalCell;
            newProperty.FindPropertyRelative("<Direction>k__BackingField").enumValueIndex =
                (int)edge.Direction;
            Field(newProperty, "Level").intValue = edge.Level;
        }

        private void SetAllBoundaryEdges(bool allowed)
        {
            boundaryEdgesConfiguredProperty.boolValue = true;

            var edges = allowed
                ? GetExternalBoundaryEdges(sizeProperty.vector2IntValue)
                : new List<DungeonRoomBoundaryEdgeData>();

            edges.AddRange(ReadBoundaryEdges().Where(edge => edge.Level != activeLevel));
            SetAllowedBoundaryEdges(edges);
        }

        private int CountAllowedBoundaryEdges()
        {
            var count = 0;
            foreach (var edge in GetExternalBoundaryEdges(sizeProperty.vector2IntValue))
                if (IsBoundaryEdgeAllowed(edge)) count++;
            return count;
        }

        private void SetAllowedBoundaryEdges(
            List<DungeonRoomBoundaryEdgeData> edges)
        {
            allowedBoundaryEdgesProperty.arraySize = edges.Count;

            for (var index = 0; index < edges.Count; index++)
            {
                var property = allowedBoundaryEdgesProperty.GetArrayElementAtIndex(index);
                property.FindPropertyRelative("<LocalCell>k__BackingField").vector2IntValue =
                    edges[index].LocalCell;
                property.FindPropertyRelative("<Direction>k__BackingField").enumValueIndex =
                    (int)edges[index].Direction;
                Field(property, "Level").intValue = edges[index].Level;
            }
        }

        private bool TryGetBoundaryEdgeAtPosition(
            Rect gridRect,
            float cellSize,
            Vector2Int size,
            Vector2 mousePosition,
            out DungeonRoomBoundaryEdgeData result)
        {
            foreach (var edge in GetExternalBoundaryEdges(size))
            {
                var cellRect = GetCellRect(
                    gridRect,
                    cellSize,
                    size,
                    edge.LocalCell);
                var edgeRect = GetBoundaryEdgeRect(
                    cellRect,
                    edge.Direction,
                    10f);

                if (edgeRect.Contains(mousePosition))
                {
                    result = edge;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private int FindStructuralAnchorAt(Vector2Int cell, Vector2Int size)
        {
            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                if (Field(property, "Level").intValue != activeLevel) continue;
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;
                var otherCell = localCell + GetDirectionOffset(direction);

                if (localCell == cell || otherCell == cell)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindSemanticAnchorAt(Vector2Int cell, Vector2Int size)
        {
            if (activeLevel != 0) return -1;
            for (var index = 0; index < semanticAnchorsProperty.arraySize; index++)
            {
                var property = semanticAnchorsProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var areaSize = property.FindPropertyRelative(
                    "<AreaSize>k__BackingField").vector2IntValue;

                if (cell.x >= localCell.x
                    && cell.x < localCell.x + areaSize.x
                    && cell.y >= localCell.y
                    && cell.y < localCell.y + areaSize.y)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsValidStructuralSelection()
        {
            return selectedStructuralAnchor >= 0
                && selectedStructuralAnchor < structuralAnchorsProperty.arraySize;
        }

        private bool IsValidSemanticSelection()
        {
            return selectedSemanticAnchor >= 0
                && selectedSemanticAnchor < semanticAnchorsProperty.arraySize;
        }

        private void ClearSelectionIfOutsideRoom()
        {
            var size = sizeProperty.vector2IntValue;

            if (IsValidStructuralSelection())
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(
                    selectedStructuralAnchor);
                var cell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;

                if (!IsInsideRoom(cell, size)) selectedStructuralAnchor = -1;
            }

            if (IsValidSemanticSelection())
            {
                var property = semanticAnchorsProperty.GetArrayElementAtIndex(
                    selectedSemanticAnchor);
                var cell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;

                if (!IsInsideRoom(cell, size)) selectedSemanticAnchor = -1;
            }
        }

        private string GetUniqueSemanticAnchorId()
        {
            var baseId = "anchor." + newSemanticType.ToString().ToLowerInvariant();
            var suffix = 1;

            while (true)
            {
                var candidate = $"{baseId}.{suffix:00}";
                var exists = false;

                for (var index = 0; index < semanticAnchorsProperty.arraySize; index++)
                {
                    var id = semanticAnchorsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("<Id>k__BackingField").stringValue;

                    if (id == candidate)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists) return candidate;
                suffix++;
            }
        }

        private static Rect GetCellRect(
            Rect gridRect,
            float cellSize,
            Vector2Int size,
            Vector2Int cell)
        {
            return new Rect(
                gridRect.x + cell.x * cellSize,
                gridRect.y + (size.y - 1 - cell.y) * cellSize,
                cellSize,
                cellSize);
        }

        private static Rect GetBoundaryEdgeRect(
            Rect cellRect,
            DungeonEdgeDirection direction,
            float thickness)
        {
            var halfThickness = thickness * 0.5f;

            return direction switch
            {
                DungeonEdgeDirection.Up => new Rect(
                    cellRect.xMin,
                    cellRect.yMin - halfThickness,
                    cellRect.width,
                    thickness),
                DungeonEdgeDirection.Right => new Rect(
                    cellRect.xMax - halfThickness,
                    cellRect.yMin,
                    thickness,
                    cellRect.height),
                DungeonEdgeDirection.Down => new Rect(
                    cellRect.xMin,
                    cellRect.yMax - halfThickness,
                    cellRect.width,
                    thickness),
                DungeonEdgeDirection.Left => new Rect(
                    cellRect.xMin - halfThickness,
                    cellRect.yMin,
                    thickness,
                    cellRect.height),
                _ => cellRect
            };
        }

        private static Rect GetAreaRect(
            Rect gridRect,
            float cellSize,
            Vector2Int size,
            Vector2Int localCell,
            Vector2Int areaSize)
        {
            return new Rect(
                gridRect.x + localCell.x * cellSize,
                gridRect.y + (size.y - localCell.y - areaSize.y) * cellSize,
                areaSize.x * cellSize,
                areaSize.y * cellSize);
        }

        private static Vector2Int GetCellFromPosition(
            Rect gridRect,
            float cellSize,
            Vector2Int size,
            Vector2 mousePosition)
        {
            var x = Mathf.FloorToInt((mousePosition.x - gridRect.x) / cellSize);
            var visualY = Mathf.FloorToInt((mousePosition.y - gridRect.y) / cellSize);
            return new Vector2Int(x, size.y - 1 - visualY);
        }

        private static Vector2Int GetDirectionOffset(DungeonEdgeDirection direction)
        {
            return direction switch
            {
                DungeonEdgeDirection.Up => Vector2Int.up,
                DungeonEdgeDirection.Right => Vector2Int.right,
                DungeonEdgeDirection.Down => Vector2Int.down,
                DungeonEdgeDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        private static bool AreSameEdge(
            Vector2Int first,
            Vector2Int second,
            Vector2Int otherFirst,
            Vector2Int otherSecond)
        {
            return first == otherFirst && second == otherSecond
                || first == otherSecond && second == otherFirst;
        }

        private static bool IsInsideRoom(Vector2Int cell, Vector2Int size)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < size.x
                && cell.y < size.y;
        }

        private static int CompareCells(Vector2Int first, Vector2Int second)
        {
            var xComparison = first.x.CompareTo(second.x);
            return xComparison != 0
                ? xComparison
                : first.y.CompareTo(second.y);
        }

        private static string GetSemanticLabel(DungeonSemanticAnchorType type)
        {
            return type switch
            {
                DungeonSemanticAnchorType.Chest => "C",
                DungeonSemanticAnchorType.Obstacle => "O",
                DungeonSemanticAnchorType.Trap => "T",
                DungeonSemanticAnchorType.Lever => "L",
                DungeonSemanticAnchorType.Reward => "R",
                DungeonSemanticAnchorType.Condition => "P",
                DungeonSemanticAnchorType.Enemy => "E",
                _ => "?"
            };
        }

        private static void DrawDistributionKey(SerializedProperty property)
        {
            EditorGUILayout.PropertyField(Field(property, "DistributionKey"), new GUIContent("Klucz rozkładu"));
            EditorGUILayout.HelpBox("Ten sam klucz współdzieli historię w całym lochu, również przy różnych szansach. "
                + "Grupa stanowi jedną okazję. Dla zależnych okazji i wspólnych obszarów użyj osobnych kluczy. "
                + "Walidacja puli wykrywa również cykle między szablonami. Klucz wymagany dla szans większych od 0% i mniejszych od 100%.", MessageType.None);
        }

        private static void DrawAnchorMarker(Rect rect, Color color, string label)
        {
            var marker = new Rect(
                rect.x + rect.width * 0.18f,
                rect.y + rect.height * 0.18f,
                rect.width * 0.64f,
                rect.height * 0.64f);
            EditorGUI.DrawRect(marker, color);
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
            GUI.Label(marker, label, style);
        }

        private static void DrawEdgeMarker(Rect first, Rect second, Color color)
        {
            var firstCenter = first.center;
            var secondCenter = second.center;

            if (Mathf.Abs(firstCenter.x - secondCenter.x)
                >= Mathf.Abs(firstCenter.y - secondCenter.y))
            {
                EditorGUI.DrawRect(
                    new Rect(
                        Mathf.Min(firstCenter.x, secondCenter.x),
                        firstCenter.y - 1.5f,
                        Mathf.Abs(firstCenter.x - secondCenter.x),
                        3f),
                    color);
                return;
            }

            EditorGUI.DrawRect(
                new Rect(
                    firstCenter.x - 1.5f,
                    Mathf.Min(firstCenter.y, secondCenter.y),
                    3f,
                    Mathf.Abs(firstCenter.y - secondCenter.y)),
                color);
        }

        private static string BuildDefaultId(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path)
                .ToLowerInvariant()
                .Replace(' ', '.');
            return $"room.{name}";
        }
    }

    // Serialized edits keep reference changes and collection changes in a single Undo operation.
    public static class DungeonRoomEditorContentEdits
    {
        private static SerializedProperty Anchors(SerializedObject data) => data.FindProperty("<SemanticAnchors>k__BackingField");
        private static SerializedProperty Groups(SerializedObject data) => data.FindProperty("<ContentGroups>k__BackingField");
        private static SerializedProperty Field(SerializedProperty property, string name) => property.FindPropertyRelative($"<{name}>k__BackingField");
        private static IEnumerable<SerializedProperty> Elements(SerializedProperty array)
        {
            for (var i = 0; i < array.arraySize; i++) yield return array.GetArrayElementAtIndex(i);
        }
        private static SerializedProperty Anchor(SerializedObject data, string id)
            => Elements(Anchors(data)).FirstOrDefault(a => Field(a, "Id").stringValue == id);
        private static void Commit(SerializedObject data, string label)
        {
            Undo.SetCurrentGroupName(label);
            data.ApplyModifiedProperties();
        }

        public static int FindGroup(SerializedObject data, string anchorId)
        {
            var groups = Groups(data);
            for (var i = 0; i < groups.arraySize; i++)
                if (Elements(Field(groups.GetArrayElementAtIndex(i), "AnchorIds")).Any(p => p.stringValue == anchorId)) return i;
            return -1;
        }

        public static bool RenameAnchor(SerializedObject data, string oldId, string newId, out string error)
        {
            error = null;
            if (oldId == newId) return true;
            var anchor = Anchor(data, oldId);
            if (anchor == null || string.IsNullOrWhiteSpace(newId) || Anchor(data, newId) != null)
            {
                error = "Id kotwy musi być niepuste i unikalne.";
                return false;
            }
            Field(anchor, "Id").stringValue = newId;
            foreach (var other in Elements(Anchors(data)))
                if (Field(other, "RequiresAnchorId").stringValue == oldId) Field(other, "RequiresAnchorId").stringValue = newId;
            foreach (var group in Elements(Groups(data)))
                foreach (var member in Elements(Field(group, "AnchorIds")))
                    if (member.stringValue == oldId) member.stringValue = newId;
            Commit(data, "Rename room anchor and references");
            return true;
        }

        public static bool DeleteAnchors(SerializedObject data, IEnumerable<string> selection, out string error)
        {
            error = null;
            var ids = new HashSet<string>(selection, StringComparer.Ordinal);
            if (ids.Count == 0) { error = "Brak zaznaczonych kotew."; return false; }
            foreach (var anchor in Elements(Anchors(data)))
            {
                if (ids.Contains(Field(anchor, "Id").stringValue)) continue;
                var dependency = Field(anchor, "RequiresAnchorId").stringValue;
                if (!ids.Contains(dependency)) continue;
                error = $"Kotwa '{Field(anchor, "Id").stringValue}' wymaga '{dependency}'. Usuń zależność albo zaznacz również zależną kotwę.";
                return false;
            }
            var anchors = Anchors(data);
            for (var i = anchors.arraySize - 1; i >= 0; i--)
                if (ids.Contains(Field(anchors.GetArrayElementAtIndex(i), "Id").stringValue)) anchors.DeleteArrayElementAtIndex(i);
            var groups = Groups(data);
            for (var i = groups.arraySize - 1; i >= 0; i--)
            {
                var members = Field(groups.GetArrayElementAtIndex(i), "AnchorIds");
                for (var j = members.arraySize - 1; j >= 0; j--)
                    if (ids.Contains(members.GetArrayElementAtIndex(j).stringValue)) members.DeleteArrayElementAtIndex(j);
                if (members.arraySize == 0) groups.DeleteArrayElementAtIndex(i);
            }
            Commit(data, "Delete room anchors and group references");
            return true;
        }

        public static bool CreateGroup(SerializedObject data, string id, IEnumerable<string> selection,
            bool required, float chance, out string error)
        {
            error = null;
            var ids = selection.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (string.IsNullOrWhiteSpace(id) || ids.Count == 0 || !float.IsFinite(chance) || chance < 0 || chance > 1
                || Elements(Groups(data)).Any(g => Field(g, "Id").stringValue == id))
            {
                error = "Grupa wymaga unikalnego Id, zaznaczenia i szansy 0–100%.";
                return false;
            }
            foreach (var anchorId in ids)
            {
                var anchor = Anchor(data, anchorId);
                if (anchor != null && Field(anchor, "Type").enumValueIndex != (int)DungeonSemanticAnchorType.Enemy && FindGroup(data, anchorId) < 0) continue;
                error = $"Kotwa '{anchorId}' nie istnieje, jest przeciwnikiem albo należy już do grupy.";
                return false;
            }
            if (!CanAssignGroup(data, ids, id, out error)) return false;
            var groups = Groups(data);
            var index = groups.arraySize;
            groups.InsertArrayElementAtIndex(index);
            var group = groups.GetArrayElementAtIndex(index);
            Field(group, "Id").stringValue = id;
            Field(group, "IsRequired").boolValue = required;
            Field(group, "SpawnChance").floatValue = chance;
            Field(group, "DistributionKey").stringValue = string.Empty;
            var members = Field(group, "AnchorIds");
            members.arraySize = ids.Count;
            for (var i = 0; i < ids.Count; i++)
            {
                members.GetArrayElementAtIndex(i).stringValue = ids[i];
                NormalizeMember(Anchor(data, ids[i]));
            }
            Commit(data, "Create room content group");
            return true;
        }

        private static void NormalizeMember(SerializedProperty anchor)
        {
            Field(anchor, "IsRequired").boolValue = false;
            Field(anchor, "SpawnChance").floatValue = 1f;
            Field(anchor, "DistributionKey").stringValue = string.Empty;
        }

        public static bool JoinGroup(SerializedObject data, string id, int targetIndex, out string error)
        {
            error = null;
            var anchor = Anchor(data, id);
            var groups = Groups(data);
            if (anchor == null || targetIndex < 0 || targetIndex >= groups.arraySize
                || Field(anchor, "Type").enumValueIndex == (int)DungeonSemanticAnchorType.Enemy)
            {
                error = "Nieprawidłowa kotwa lub grupa.";
                return false;
            }
            var targetId = Field(groups.GetArrayElementAtIndex(targetIndex), "Id").stringValue;
            if (FindGroup(data, id) == targetIndex) return true;
            if (!CanAssignGroup(data, new[] { id }, targetId, out error)) return false;
            RemoveMembership(data, id, false);
            var target = Elements(groups).First(g => Field(g, "Id").stringValue == targetId);
            var members = Field(target, "AnchorIds");
            members.InsertArrayElementAtIndex(members.arraySize);
            members.GetArrayElementAtIndex(members.arraySize - 1).stringValue = id;
            NormalizeMember(anchor);
            Commit(data, "Assign room anchor to group");
            return true;
        }

        public static bool DetachAnchor(SerializedObject data, string id, out string error)
        {
            error = null;
            if (Anchor(data, id) == null || FindGroup(data, id) < 0) { error = "Kotwa nie należy do grupy."; return false; }
            RemoveMembership(data, id, true);
            Commit(data, "Detach room anchor from group");
            return true;
        }

        private static void RemoveMembership(SerializedObject data, string id, bool inheritSettings)
        {
            var index = FindGroup(data, id);
            if (index < 0) return;
            var groups = Groups(data);
            var group = groups.GetArrayElementAtIndex(index);
            if (inheritSettings)
            {
                var anchor = Anchor(data, id);
                if (anchor != null)
                {
                    Field(anchor, "IsRequired").boolValue = Field(group, "IsRequired").boolValue;
                    Field(anchor, "SpawnChance").floatValue = Field(group, "SpawnChance").floatValue;
                    Field(anchor, "DistributionKey").stringValue = Field(group, "DistributionKey").stringValue;
                }
            }
            var members = Field(group, "AnchorIds");
            for (var i = members.arraySize - 1; i >= 0; i--)
                if (members.GetArrayElementAtIndex(i).stringValue == id) members.DeleteArrayElementAtIndex(i);
            if (members.arraySize == 0) groups.DeleteArrayElementAtIndex(index);
        }

        public static void DissolveGroup(SerializedObject data, int index)
        {
            var group = Groups(data).GetArrayElementAtIndex(index);
            var members = Elements(Field(group, "AnchorIds")).Select(p => p.stringValue).ToList();
            foreach (var member in members) RemoveMembership(data, member, true);
            Commit(data, "Dissolve room content group");
        }

        private static bool CanAssignGroup(SerializedObject data, IEnumerable<string> changedIds, string targetId, out string error)
        {
            var changed = new HashSet<string>(changedIds, StringComparer.Ordinal);
            var unitByAnchor = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var anchor in Elements(Anchors(data)))
            {
                var id = Field(anchor, "Id").stringValue;
                if (changed.Contains(id)) unitByAnchor[id] = "group:" + targetId;
                else
                {
                    var group = FindGroup(data, id);
                    unitByAnchor[id] = group < 0 ? "anchor:" + id : "group:" + Field(Groups(data).GetArrayElementAtIndex(group), "Id").stringValue;
                }
            }
            var edges = unitByAnchor.Values.Distinct().ToDictionary(id => id, _ => new HashSet<string>(), StringComparer.Ordinal);
            foreach (var anchor in Elements(Anchors(data)))
            {
                var from = unitByAnchor[Field(anchor, "Id").stringValue];
                var dependency = Field(anchor, "RequiresAnchorId").stringValue;
                if (unitByAnchor.TryGetValue(dependency, out var to) && from != to) edges[from].Add(to);
            }
            var active = new HashSet<string>();
            var done = new HashSet<string>();
            bool Visit(string id)
            {
                if (done.Contains(id)) return true;
                if (!active.Add(id)) return false;
                foreach (var next in edges[id]) if (!Visit(next)) return false;
                active.Remove(id);
                done.Add(id);
                return true;
            }
            var valid = edges.Keys.All(Visit);
            error = valid ? null : "Ta grupa utworzyłaby cykl zależności. Zmień zaznaczenie lub zależności.";
            return valid;
        }
    }

    [CustomEditor(typeof(DungeonRoomTemplateDefinition))]
    public class DungeonRoomTemplateDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Dungeon Room Editor"))
            {
                DungeonRoomEditorWindow.Open(
                    (DungeonRoomTemplateDefinition)target);
            }

            EditorGUILayout.Space(6f);
            DrawDefaultInspector();

            var report = DungeonRoomTemplateEditorValidator.Validate(
                (DungeonRoomTemplateDefinition)target);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Room template validation", EditorStyles.boldLabel);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "Room template jest poprawny.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Zapis assetu jest zablokowany, dopoki room template jest niepoprawny.",
                MessageType.Error);

            foreach (var error in report.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
