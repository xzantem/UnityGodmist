using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Views.Combat;
using Godmist.Systems.Items;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    [DefaultExecutionOrder(-110)]
    public sealed class CombatConsumableSelectionView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CombatSkillSlotView itemTemplate;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        [SerializeField, Min(1)] private int maximumColumns = 4;
        [SerializeField, Min(1)] private int maximumRows = 4;
        [SerializeField] private Vector2 slotSize = new(64f, 64f);
        [SerializeField] private Vector2 slotSpacing = new(8f, 8f);
        [SerializeField] private Vector2 windowPadding = new(24f, 24f);
        [SerializeField] private GameObject emptyState;
        private readonly List<CombatSkillSlotView> spawned = new();
        private readonly List<ConsumableItemInstance> items = new();
        private readonly List<int> stackCounts = new();
        private readonly Vector3[] corners = new Vector3[4];
        private BattleUnit lastBuiltUnit;
        private CombatSession openedSession;
        private string lastBuiltInventorySignature = string.Empty;
        private RectTransform anchor;
        private ScrollRect scroll;
        private int dismissedFrame = -1;
        // The authored slot border extends four pixels outside its layout rect.
        // Leave another four pixels between that artwork and the clip edge.
        private const int GridInset = 8;
        public bool IsOpen => gameObject.activeInHierarchy;
        public bool OwnsMenuInput => IsOpen || dismissedFrame == Time.frameCount;

        public static CombatConsumableSelectionView Create(Transform parent, CombatConsumableSelectionView prefab)
        {
            if (parent == null || prefab == null) return null;
            var instance = Instantiate(prefab, parent, false);
            instance.ResolvePrefabReferences();
            instance.gameObject.SetActive(false);
            return instance;
        }

        public void Configure(CombatSandboxBootstrap bootstrap, CombatTargetSelectionController targetController,
            UIWorkspaceTooltipView tooltip)
        {
            sandboxBootstrap = bootstrap;
            targetSelectionController = targetController;
            tooltipView = tooltip;
            ResolvePrefabReferences();
        }

        private void Awake()
        {
            ResolvePrefabReferences();
            if (window == null) { enabled = false; return; }
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            // Create/the authored inactive prefab owns visibility. Awake can run during the first Open.
        }

        private void OnEnable() => ResolvePrefabReferences();
        private void OnDisable() => tooltipView?.HideForSourceWithin(transform);

        private void Update()
        {
            var session = sandboxBootstrap?.Session;
            if (session != openedSession || session?.ActiveUnit != lastBuiltUnit
                || session?.ActiveUnit?.IsPlayerSide != true || session.Outcome != CombatOutcomeState.Ongoing
                || anchor == null || !anchor.gameObject.activeInHierarchy
                || targetSelectionController?.SelectedSkill != null)
            { Close(); return; }
            if (!DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput())
            {
                var canvas = GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (HandleDismissInput(Input.GetKeyDown(KeyCode.Escape), Input.GetMouseButtonDown(1),
                    Input.GetMouseButtonDown(0), Godmist.Views.UI.ControllerInput.PointerPosition, camera)) return;
            }
            var signature = ConsumableService.CombatInventorySignature(session);
            if (signature != lastBuiltInventorySignature) { Rebuild(); ApplyLayoutAndResize(); }
            RefreshAvailability();
        }

        private void LateUpdate() { if (IsOpen) PositionAtAnchor(); }

        private bool ContainsPointer(Vector2 point, Camera camera) =>
            RectTransformUtility.RectangleContainsScreenPoint(transform as RectTransform, point, camera)
            || anchor != null && RectTransformUtility.RectangleContainsScreenPoint(anchor, point, camera);

        private bool HandleDismissInput(bool escape, bool secondary, bool primary, Vector2 pointer, Camera camera)
        {
            if (!escape && !secondary && (!primary || ContainsPointer(pointer, camera))) return false;
            ClaimDismissalInput();
            Close();
            return true;
        }

        private void ClaimDismissalInput()
        {
            dismissedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        private void ResolvePrefabReferences()
        {
            window ??= GetComponent<UIWorkspaceWindow>();

            // The first version of this prefab accidentally had a second
            // CombatConsumableSelectionView on the content/template child. The
            // root instance is the one referenced by CombatActionButtonsView,
            // so recover the configured template from that child at runtime.
            foreach (var candidate in GetComponentsInChildren<CombatConsumableSelectionView>(true))
            {
                if (candidate == this) continue;
                if (itemTemplate == null && candidate.itemTemplate != null)
                    itemTemplate = candidate.itemTemplate;
                candidate.enabled = false;
                // Its Awake previously disabled the content object itself;
                // the content must remain active when the root window opens.
                candidate.gameObject.SetActive(true);
            }

            if (contentRoot == null)
            {
                foreach (var grid in GetComponentsInChildren<GridLayoutGroup>(true))
                {
                    contentRoot = grid.transform as RectTransform;
                    if (contentRoot != null) break;
                }
            }


            if (itemTemplate == null)
            {
                foreach (var candidate in GetComponentsInChildren<CombatSkillSlotView>(true))
                {
                    itemTemplate = candidate;
                    break;
                }
            }
            if (window != null && itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        }

        // Kept for callers that reopen the same attached panel.
        public void Open() => Open(anchor);
        public void Open(RectTransform sourceAnchor)
        {
            if (sourceAnchor == null || sandboxBootstrap?.Session?.ActiveUnit?.IsPlayerSide != true
                || targetSelectionController == null) return;
            anchor = sourceAnchor;
            openedSession = sandboxBootstrap.Session;
            targetSelectionController.CancelTargetSelection();
            ResolvePrefabReferences();
            ConfigureAttachedPresentation();
            tooltipView?.Hide();
            window.Open();
            Rebuild();
            ApplyLayoutAndResize();
            RefreshAvailability();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void Close()
        {
            tooltipView?.HideForSourceWithin(transform);
            if (window != null) window.Close();
            else gameObject.SetActive(false);
            lastBuiltUnit = null;
            openedSession = null;
            lastBuiltInventorySignature = string.Empty;
        }

        private void ConfigureAttachedPresentation()
        {
            // Adapt the existing authored window and slot hierarchy, without a new prefab or scene lookup.
            var header = window.transform.Find("Header");
            if (header != null) header.gameObject.SetActive(false);
            foreach (var handle in window.GetComponentsInChildren<UIWorkspaceWindowResizeHandle>(true))
                handle.gameObject.SetActive(false);
            foreach (var handle in window.GetComponentsInChildren<UIWorkspaceWindowDragHandle>(true))
                handle.enabled = false;
            window.ConfigureResizeAxes(false, false);
            var body = window.transform.Find("Content") as RectTransform;
            if (body != null)
            {
                body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
                body.offsetMin = windowPadding * .5f; body.offsetMax = -windowPadding * .5f;
            }
            scroll = contentRoot != null ? contentRoot.GetComponentInParent<ScrollRect>(true) : null;
            if (scroll != null)
            {
                var rect = scroll.transform as RectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                var background = scroll.GetComponent<Image>();
                if (background != null) background.enabled = false;
                var innerBorder = scroll.transform.Find("Border");
                if (innerBorder != null) innerBorder.gameObject.SetActive(false);
                // The variant already owns RectMask2D. Do not combine it with
                // the inherited stencil mask and its viewport graphic.
                var clip = scroll.viewport.GetComponent<RectMask2D>();
                if (clip != null)
                {
                    clip.enabled = true;
                    clip.padding = Vector4.zero;
                    clip.softness = Vector2Int.zero;
                    var stencil = scroll.viewport.GetComponent<Mask>();
                    if (stencil != null) stencil.enabled = false;
                    var graphic = scroll.viewport.GetComponent<Image>();
                    if (graphic != null) graphic.enabled = false;
                }
                scroll.horizontal = false;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                scroll.verticalNormalizedPosition = 1f;
            }
            if (emptyState != null)
            {
                var label = emptyState.GetComponent<TMPro.TMP_Text>();
                if (label != null) label.text = "Brak przedmiotów";
            }
        }

        private void Rebuild()
        {
            foreach (var slot in spawned)
            {
                if (slot == null) continue;
                slot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(slot.gameObject);
                else DestroyImmediate(slot.gameObject);
            }
            spawned.Clear(); items.Clear(); stackCounts.Clear();
            var unit = sandboxBootstrap?.Session?.ActiveUnit;
            if (unit != null && contentRoot != null && itemTemplate != null)
            foreach (var item in ConsumableService.GetCombatItems(sandboxBootstrap.Session))
            {
                var existing = items.FindIndex(candidate => candidate.CanStackWith(item));
                if (existing >= 0) { stackCounts[existing]++; continue; }
                items.Add(item); stackCounts.Add(1);
                var slot = Instantiate(itemTemplate, contentRoot, false);
                slot.gameObject.SetActive(true);
                slot.InitializeConsumable(item, tooltipView, () => Select(item), () => sandboxBootstrap?.Session?.ActiveUnit);
                spawned.Add(slot);
            }
            if (emptyState != null) emptyState.SetActive(spawned.Count == 0);
            lastBuiltUnit = unit;
            lastBuiltInventorySignature = ConsumableService.CombatInventorySignature(sandboxBootstrap?.Session);
        }

        private void Select(ConsumableItemInstance item)
        {
            var unit = sandboxBootstrap?.Session?.ActiveUnit;
            if (unit != lastBuiltUnit || !ConsumableService.CanUseInCombat(sandboxBootstrap?.Session, unit, item)) return;
            ClaimDismissalInput();
            Close();
            targetSelectionController.BeginConsumableTargetSelection(item);
        }

        private void RefreshAvailability()
        {
            var unit = sandboxBootstrap?.Session?.ActiveUnit;
            for (var i = 0; i < spawned.Count; i++)
            {
                var item = items[i];
                var used = unit == null || unit.ConsumableUseTypesUsedThisTurn.Contains(item.ConsumableDefinition.UseType);
                var cost = CombatActionCostCalculator.CalculatePercentageCost(unit?.MaxActionPoints ?? 0, ConsumableRuntimeActionBuilder.ActionCostPercent);
                var enoughPoints = unit != null && unit.CurrentActionPoints >= cost;
                var count = item.MaxCharges > 1 ? $"{item.CurrentCharges}/{item.MaxCharges}" : stackCounts[i].ToString();
                spawned[i].RefreshAvailability(ConsumableService.CanUseInCombat(sandboxBootstrap?.Session, unit, item), cost, false, count,
                    reason: used ? "Ten typ przedmiotu został już użyty w tej turze."
                        : !enoughPoints ? "Za mało punktów akcji." : null);
            }
        }

        private void ApplyLayoutAndResize()
        {
            if (window == null || contentRoot == null) return;
            GetGridDimensions(spawned.Count, out var columns, out var rows);
            var parent = window.RectTransform.parent as RectTransform;
            var available = parent != null ? parent.rect.size : new Vector2(800, 600);
            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            var cell = slotSize + slotSpacing;
            var fittedColumns = Mathf.Max(1, Mathf.FloorToInt((available.x - windowPadding.x - GridInset * 2 + slotSpacing.x) / cell.x));
            columns = Mathf.Min(columns, fittedColumns);
            rows = Mathf.CeilToInt(Mathf.Max(1, spawned.Count) / (float)columns);
            var fittedRows = Mathf.Max(1, Mathf.FloorToInt((available.y - windowPadding.y - GridInset * 2 + slotSpacing.y) / cell.y));
            var visibleRows = Mathf.Min(rows, Mathf.Clamp(maximumRows, 1, 4), fittedRows);
            var scrollbar = rows > visibleRows ? 16f : 0f;
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
                grid.cellSize = slotSize; grid.spacing = slotSpacing;
                grid.padding = new RectOffset(GridInset, GridInset, GridInset, GridInset);
            }
            if (scroll != null)
            {
                scroll.vertical = scrollbar > 0;
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(scrollbar > 0);
                scroll.viewport.anchorMin = Vector2.zero;
                scroll.viewport.anchorMax = Vector2.one;
                scroll.viewport.offsetMin = Vector2.zero;
                scroll.viewport.offsetMax = new Vector2(-scrollbar, 0);
            }
            var size = new Vector2(columns * cell.x - slotSpacing.x + windowPadding.x + GridInset * 2 + scrollbar,
                visibleRows * cell.y - slotSpacing.y + windowPadding.y + GridInset * 2);
            if (spawned.Count == 0) size = new Vector2(216, 64);
            window.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(size.x, available.x));
            window.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(size.y, available.y));
            contentRoot.anchorMin = new Vector2(0, 1);
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(.5f, 1);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(0, rows * cell.y - slotSpacing.y + GridInset * 2);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window.RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            PositionAtAnchor();
        }

        private void PositionAtAnchor()
        {
            if (anchor == null || window == null || window.RectTransform.parent is not RectTransform parent) return;
            anchor.GetWorldCorners(corners);
            var min = (Vector2)parent.InverseTransformPoint(corners[0]);
            var max = (Vector2)parent.InverseTransformPoint(corners[2]);
            var rect = window.RectTransform;
            rect.pivot = Vector2.zero;
            var bounds = parent.rect;
            var x = Mathf.Clamp((min.x + max.x - rect.rect.width) * .5f, bounds.xMin, bounds.xMax - rect.rect.width);
            var y = max.y + 8f;
            if (y + rect.rect.height > bounds.yMax) y = min.y - 8f - rect.rect.height;
            y = Mathf.Clamp(y, bounds.yMin, bounds.yMax - rect.rect.height);
            rect.position = parent.TransformPoint(new Vector3(x, y, 0f));
        }

        private void GetGridDimensions(int count, out int columns, out int rows)
        {
            var itemCount = Mathf.Max(1, count);
            var maxColumns = Mathf.Clamp(maximumColumns, 1, 4);
            var bestColumns = 1;
            var bestRows = itemCount;
            var bestScore = GetGridScore(bestColumns, bestRows, itemCount);
            var bestArea = bestColumns * bestRows;
            var bestAspectDifference = Mathf.Abs(bestColumns - bestRows);

            for (var candidateColumns = 1;
                candidateColumns <= Mathf.Min(maxColumns, itemCount);
                candidateColumns++)
            {
                var candidateRows = Mathf.CeilToInt(itemCount / (float)candidateColumns);
                var candidateArea = candidateColumns * candidateRows;
                var candidateAspectDifference = Mathf.Abs(candidateColumns - candidateRows);
                var candidateScore = GetGridScore(
                    candidateColumns,
                    candidateRows,
                    itemCount);
                if (candidateScore < bestScore
                    || (Mathf.Approximately(candidateScore, bestScore)
                        && (candidateArea < bestArea
                            || (candidateArea == bestArea
                                && (candidateAspectDifference < bestAspectDifference
                                    || (candidateAspectDifference == bestAspectDifference
                                        && candidateColumns > bestColumns))))))
                {
                    bestColumns = candidateColumns;
                    bestRows = candidateRows;
                    bestScore = candidateScore;
                    bestArea = candidateArea;
                    bestAspectDifference = candidateAspectDifference;
                }
            }

            columns = bestColumns;
            rows = bestRows;
        }

        private static float GetGridScore(
            int columns,
            int rows,
            int itemCount)
        {
            var area = columns * rows;
            var emptySlots = area - itemCount;
            // Prefer a compact, near-square grid, while penalizing empty cells
            // enough that 3 items remain 3x1 rather than becoming 2x2. This
            // yields 3x2 for five or six items and 4x2 for seven or eight.
            return Mathf.Max(columns, rows) + emptySlots;
        }

    }
}
