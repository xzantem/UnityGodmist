using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public class DungeonRoomEditorWindow : EditorWindow
    {
        private enum EditTool
        {
            Footprint,
            StructuralAnchor,
            SemanticAnchor,
            BoundaryEdge
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

        private EditTool activeTool;
        private DungeonStructuralAnchorType newStructuralType =
            DungeonStructuralAnchorType.Wall;
        private DungeonEdgeDirection newStructuralDirection =
            DungeonEdgeDirection.Right;
        private DungeonSemanticAnchorType newSemanticType =
            DungeonSemanticAnchorType.Chest;
        private int selectedStructuralAnchor = -1;
        private int selectedSemanticAnchor = -1;
        private bool? footprintPaintValue;
        private Vector2 gridScrollPosition;
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
            window.minSize = new Vector2(760f, 560f);
            window.SetTemplate(target);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (template == null)
            {
                EditorGUILayout.HelpBox(
                    "Wybierz DungeonRoomTemplateDefinition albo utworz nowy asset.",
                    MessageType.Info);
                return;
            }

            EnsureSerializedObject();
            serializedTemplate.Update();

            DrawTemplateProperties();
            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            DrawGridPanel();
            DrawSelectionPanel();
            EditorGUILayout.EndHorizontal();

            serializedTemplate.ApplyModifiedProperties();

            var report = DungeonRoomTemplateEditorValidator.Validate(template);
            DrawValidation(report);
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
            EditorGUILayout.EndHorizontal();
        }

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

            EditorGUILayout.LabelField("Resize", EditorStyles.boldLabel);
            DrawResizeButtons();
        }

        private void DrawGridPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField("Edit mode", EditorStyles.boldLabel);
            activeTool = (EditTool)GUILayout.Toolbar(
                (int)activeTool,
                new[]
                {
                    "Footprint",
                    "Structural anchors",
                    "Semantic anchors",
                    "Boundary edges"
                });

            DrawToolOptions();

            var size = sizeProperty.vector2IntValue;
            var cellSize = Mathf.Min(
                34f,
                Mathf.Max(16f, (position.width - 360f) / Mathf.Max(1, size.x)));
            var gridSize = new Vector2(
                size.x * cellSize,
                size.y * cellSize);

            gridScrollPosition = EditorGUILayout.BeginScrollView(
                gridScrollPosition,
                GUILayout.ExpandHeight(true));

            var gridRect = GUILayoutUtility.GetRect(
                gridSize.x,
                gridSize.y,
                GUILayout.ExpandWidth(false));

            DrawGrid(gridRect, cellSize, size);
            HandleGridInput(gridRect, cellSize, size);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawResizeButtons()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Up +", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Up, true);
            }
            if (GUILayout.Button("Up -", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Up, false);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Left +", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Left, true);
            }
            if (GUILayout.Button("Left -", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Left, false);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Right -", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Right, false);
            }
            if (GUILayout.Button("Right +", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Right, true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Down +", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Down, true);
            }
            if (GUILayout.Button("Down -", GUILayout.Width(58f)))
            {
                ResizeRoom(DungeonEdgeDirection.Down, false);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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

            var oldFloorCells = GetFloorCells(oldSize);

            if (!grow && HasAnchorsInRemovedStrip(
                    oldSize,
                    direction))
            {
                ShowNotification(new GUIContent(
                    "Nie mozna zmniejszyc pokoju: usuwany pas zawiera anchor."));
                return;
            }

            var oldUsesFullBoundingBox = floorOffsetsProperty.arraySize == 0;
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

            SetFloorCells(resizedFloorCells, newSize);
            ShiftAnchors(delta);
            ResizeAllowedBoundaryEdges(newSize, delta, resizedFloorCells);
            sizeProperty.vector2IntValue = newSize;

            ClearSelectionIfOutsideRoom();
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
                        new DungeonRoomBoundaryEdgeData(localCell, direction));
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
                    EditorGUILayout.LabelField(
                        "Kliknij lub przeciagaj po siatce, aby malowac podloge.");
                    break;

                case EditTool.StructuralAnchor:
                    newStructuralType = (DungeonStructuralAnchorType)EditorGUILayout.EnumPopup(
                        "New type",
                        newStructuralType);
                    newStructuralDirection = (DungeonEdgeDirection)EditorGUILayout.EnumPopup(
                        "Direction",
                        newStructuralDirection);
                    EditorGUILayout.LabelField(
                        "Kliknij komorke, aby dodac kotwice na krawedzi.");
                    break;

                case EditTool.SemanticAnchor:
                    newSemanticType = (DungeonSemanticAnchorType)EditorGUILayout.EnumPopup(
                        "New type",
                        newSemanticType);
                    EditorGUILayout.LabelField(
                        "Kliknij komorke, aby dodac semantic anchor.");
                    break;

                case EditTool.BoundaryEdge:
                    EditorGUILayout.LabelField(
                        "Kliknij zewnetrzna krawedz, aby wlaczyc albo wylaczyc przejscie.");
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

                    Handles.color = new Color(0f, 0f, 0f, 0.45f);
                    Handles.DrawLine(
                        new Vector3(cellRect.xMin, cellRect.yMin),
                        new Vector3(cellRect.xMax, cellRect.yMin));
                    Handles.DrawLine(
                        new Vector3(cellRect.xMin, cellRect.yMin),
                        new Vector3(cellRect.xMin, cellRect.yMax));
                }
            }

            DrawBoundaryEdges(gridRect, cellSize, size);
            DrawStructuralAnchors(gridRect, cellSize, size);
            DrawSemanticAnchors(gridRect, cellSize, size);
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

                DrawAnchorMarker(cellRect, color, type == DungeonStructuralAnchorType.Door
                    ? "D"
                    : "W");

                var neighbour = localCell + GetDirectionOffset(direction);
                var neighbourRect = GetCellRect(gridRect, cellSize, size, neighbour);
                DrawEdgeMarker(cellRect, neighbourRect, color);
            }
        }

        private void DrawSemanticAnchors(
            Rect gridRect,
            float cellSize,
            Vector2Int size)
        {
            if (!showSemanticAnchors) return;

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
            }
        }

        private void DrawSelectionPanel()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.Width(305f),
                GUILayout.ExpandHeight(true));

            switch (activeTool)
            {
                case EditTool.Footprint:
                    EditorGUILayout.LabelField(
                        "Footprint",
                        EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "Puste FloorCellOffsets oznacza pelny prostokat. "
                        + "Po pierwszej edycji siatka zostanie zapisana jawnie.",
                        MessageType.Info);
                    break;

                case EditTool.StructuralAnchor:
                    DrawStructuralSelection();
                    break;

                case EditTool.SemanticAnchor:
                    DrawSemanticSelection();
                    break;

                case EditTool.BoundaryEdge:
                    DrawBoundarySelection();
                    break;
            }

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
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<Type>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<LocalCell>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<Direction>k__BackingField"));

            if (GUILayout.Button("Remove selected anchor"))
            {
                structuralAnchorsProperty.DeleteArrayElementAtIndex(
                    selectedStructuralAnchor);
                selectedStructuralAnchor = -1;
            }
        }

        private void DrawSemanticSelection()
        {
            EditorGUILayout.LabelField(
                "Selected semantic anchor",
                EditorStyles.boldLabel);

            if (!IsValidSemanticSelection())
            {
                EditorGUILayout.HelpBox(
                    "Kliknij istniejaca kotwice na siatce, aby edytowac jej dane.",
                    MessageType.Info);
                return;
            }

            var property = semanticAnchorsProperty.GetArrayElementAtIndex(
                selectedSemanticAnchor);
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<Id>k__BackingField"));
            var typeProperty = property.FindPropertyRelative("<Type>k__BackingField");
            EditorGUILayout.PropertyField(typeProperty);
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<LocalCell>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<AreaSize>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<SpawnChance>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<IsRequired>k__BackingField"));
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative("<RequiresAnchorId>k__BackingField"));

            var activationGroupProperty = property.FindPropertyRelative(
                "<ActivationGroupId>k__BackingField");
            if (typeProperty.enumValueIndex == (int)DungeonSemanticAnchorType.Trap)
            {
                EditorGUILayout.PropertyField(
                    activationGroupProperty,
                    new GUIContent("Activation group (traps)"));
            }
            else
            {
                activationGroupProperty.stringValue = string.Empty;
            }

            if (GUILayout.Button("Remove selected anchor"))
            {
                semanticAnchorsProperty.DeleteArrayElementAtIndex(
                    selectedSemanticAnchor);
                selectedSemanticAnchor = -1;
            }
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
            var currentEvent = Event.current;

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

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 1)
            {
                RemoveAnchorAtCell(
                    GetCellFromPosition(
                        gridRect,
                        cellSize,
                        size,
                        currentEvent.mousePosition),
                    size);
                currentEvent.Use();
                Repaint();
                return;
            }

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

                case EditTool.BoundaryEdge:
                    break;
            }

            currentEvent.Use();
            Repaint();
        }

        private void RemoveAnchorAtCell(Vector2Int cell, Vector2Int size)
        {
            switch (activeTool)
            {
                case EditTool.StructuralAnchor:
                {
                    var structuralIndex = FindStructuralAnchorAt(cell, size);

                    if (structuralIndex >= 0)
                    {
                        structuralAnchorsProperty.DeleteArrayElementAtIndex(
                            structuralIndex);

                        if (selectedStructuralAnchor == structuralIndex)
                        {
                            selectedStructuralAnchor = -1;
                        }
                        else if (selectedStructuralAnchor > structuralIndex)
                        {
                            selectedStructuralAnchor--;
                        }
                    }
                    break;
                }

                case EditTool.SemanticAnchor:
                {
                    var semanticIndex = FindSemanticAnchorAt(cell, size);

                    if (semanticIndex >= 0)
                    {
                        semanticAnchorsProperty.DeleteArrayElementAtIndex(
                            semanticIndex);

                        if (selectedSemanticAnchor == semanticIndex)
                        {
                            selectedSemanticAnchor = -1;
                        }
                        else if (selectedSemanticAnchor > semanticIndex)
                        {
                            selectedSemanticAnchor--;
                        }
                    }
                    break;
                }

                case EditTool.BoundaryEdge:
                    break;
            }
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

            if (footprintPaintValue.Value)
            {
                floorCells.Add(cell);
            }
            else
            {
                floorCells.Remove(cell);
            }

            SetFloorCells(floorCells, size);
        }

        private void SelectOrAddStructuralAnchor(
            Vector2Int cell,
            Vector2Int size)
        {
            var existing = FindStructuralAnchorAt(cell, size);

            if (existing >= 0)
            {
                selectedStructuralAnchor = existing;
                return;
            }

            var floorCells = GetFloorCells(size);
            var secondCell = cell + GetDirectionOffset(newStructuralDirection);

            if (!floorCells.Contains(cell) || !floorCells.Contains(secondCell))
            {
                ShowNotification(new GUIContent(
                    "Structural anchor musi laczyc dwie komorki podlogi."));
                return;
            }

            for (var index = 0; index < structuralAnchorsProperty.arraySize; index++)
            {
                var property = structuralAnchorsProperty.GetArrayElementAtIndex(index);
                var localCell = property.FindPropertyRelative(
                    "<LocalCell>k__BackingField").vector2IntValue;
                var direction = (DungeonEdgeDirection)property.FindPropertyRelative(
                    "<Direction>k__BackingField").enumValueIndex;
                var otherCell = localCell + GetDirectionOffset(direction);

                if (AreSameEdge(cell, secondCell, localCell, otherCell))
                {
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
            selectedStructuralAnchor = newIndex;
        }

        private void SelectOrAddSemanticAnchor(
            Vector2Int cell,
            Vector2Int size)
        {
            var existing = FindSemanticAnchorAt(cell, size);

            if (existing >= 0)
            {
                selectedSemanticAnchor = existing;
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
            selectedSemanticAnchor = newIndex;
        }

        private void DrawValidation(
            DungeonRoomTemplateValidationReport report)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "Room template jest poprawny i moze zostac zapisany.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Zapis assetu jest zablokowany do czasu usuniecia wszystkich bledow.",
                MessageType.Error);

            foreach (var error in report.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
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
            AssetDatabase.SaveAssets();
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
            AssetDatabase.SaveAssets();
            SetTemplate(created);
        }

        private void SetTemplate(DungeonRoomTemplateDefinition nextTemplate)
        {
            template = nextTemplate;
            serializedTemplate = null;
            selectedStructuralAnchor = -1;
            selectedSemanticAnchor = -1;
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

            if (!boundaryEdgesConfiguredProperty.boolValue)
            {
                template.MigrateLegacyBoundaryEdges();
                serializedTemplate.Update();
            }
        }

        private HashSet<Vector2Int> GetFloorCells(Vector2Int size)
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

        private List<DungeonRoomBoundaryEdgeData> GetExternalBoundaryEdges(
            Vector2Int size)
        {
            return new List<DungeonRoomBoundaryEdgeData>(
                DungeonRoomBoundaryUtility.EnumerateExternalBoundaryEdges(
                    size,
                    GetFloorCells(size)));
        }

        private bool IsBoundaryEdgeAllowed(
            DungeonRoomBoundaryEdgeData edge)
        {
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

                if (candidateCell == localCell && candidateDirection == direction)
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

                if (localCell != edge.LocalCell || direction != edge.Direction)
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
        }

        private void SetAllBoundaryEdges(bool allowed)
        {
            boundaryEdgesConfiguredProperty.boolValue = true;

            var edges = allowed
                ? GetExternalBoundaryEdges(sizeProperty.vector2IntValue)
                : new List<DungeonRoomBoundaryEdgeData>();

            SetAllowedBoundaryEdges(edges);
        }

        private int CountAllowedBoundaryEdges()
        {
            if (!boundaryEdgesConfiguredProperty.boolValue)
            {
                return GetExternalBoundaryEdges(sizeProperty.vector2IntValue).Count;
            }

            return allowedBoundaryEdgesProperty.arraySize;
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
                _ => "?"
            };
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
