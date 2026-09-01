using System.Collections;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Views.Combat;
using Godmist.Systems.Items;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class CombatConsumableSelectionView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CombatSkillSlotView itemTemplate;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        [SerializeField, Min(1)] private int maximumColumns = 8;
        [SerializeField, Min(1)] private int maximumRows = 8;
        [SerializeField] private Vector2 slotSize = new(64f, 64f);
        [SerializeField] private Vector2 slotSpacing = new(8f, 8f);
        [SerializeField] private Vector2 windowPadding = new(24f, 24f);
        private readonly List<CombatSkillSlotView> spawned = new();
        private Coroutine resizeRoutine;
        private BattleUnit lastBuiltUnit;
        private string lastBuiltInventorySignature = string.Empty;

        // PF_UIW_Window's Content is stretched with a -32/-80 size delta
        // (horizontal/vertical chrome plus header). The selection grid must
        // reserve that space when calculating the outer window size; otherwise
        // the first layout pass collapses the body to the header height.
        private static readonly Vector2 WindowContentChrome = new(60f, 96f);

        public static CombatConsumableSelectionView Create(Transform parent, CombatConsumableSelectionView prefab)
        {
            if (parent == null || prefab == null) return null;
            var instance = Instantiate(prefab, parent, false);
            instance.ResolvePrefabReferences();
            instance.gameObject.SetActive(false);
            return instance;
        }

        public void Configure(
            CombatSandboxBootstrap bootstrap,
            CombatTargetSelectionController targetController,
            UIWorkspaceTooltipView tooltip)
        {
            sandboxBootstrap = bootstrap ?? FindAnyObjectByType<CombatSandboxBootstrap>();
            targetSelectionController = targetController
                ?? FindAnyObjectByType<CombatTargetSelectionController>();
            tooltipView = tooltip;
            ResolvePrefabReferences();
        }

        private void Awake()
        {
            // The prefab contains the slot template under the ScrollRect. Older
            // instances also carried a second copy of this controller on the
            // scroll content. That copy must never disable its own GameObject:
            // doing so races the root controller's first Open() and leaves the
            // first presentation with an empty viewport. It only exists to
            // provide the serialized template reference.
            ResolvePrefabReferences();
            if (window == null)
            {
                enabled = false;
                return;
            }

            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Re-enable the content after the legacy duplicate component's Awake
            // has run and disabled its own GameObject.
            ResolvePrefabReferences();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            var unit = sandboxBootstrap?.Session?.ActiveUnit;
            var signature = BuildInventorySignature(unit);
            if (unit == null
                || contentRoot == null
                || itemTemplate == null
                || (ReferenceEquals(unit, lastBuiltUnit)
                    && signature == lastBuiltInventorySignature))
            {
                return;
            }

            Rebuild();
            ApplyLayoutAndResize();
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
        }

        public void Open()
        {
            sandboxBootstrap ??= FindAnyObjectByType<CombatSandboxBootstrap>();
            targetSelectionController ??= FindAnyObjectByType<CombatTargetSelectionController>();
            ResolvePrefabReferences();
            // Treat every open as a fresh presentation. The active unit can
            // have received a runtime item since the previous close, and a
            // cache hit on the first frame would otherwise skip the rebuild.
            lastBuiltUnit = null;
            lastBuiltInventorySignature = string.Empty;
            // Activate the hierarchy before the first rebuild. PF_UIW_Window's
            // content and layout components only receive their reliable first
            // layout pass while active; rebuilding an inactive hierarchy can
            // leave the first open empty until the window is reopened.
            if (window != null) window.Open();
            else gameObject.SetActive(true);

            Rebuild();
            ApplyLayoutAndResize();
            if (resizeRoutine != null) StopCoroutine(resizeRoutine);
            resizeRoutine = StartCoroutine(ResizeAfterLayout());
        }

        public void Close()
        {
            if (window != null) window.Close();
            else gameObject.SetActive(false);
            lastBuiltUnit = null;
            lastBuiltInventorySignature = string.Empty;
        }

        private void Rebuild()
        {
            foreach (var slot in spawned) if (slot != null) Destroy(slot.gameObject);
            spawned.Clear();
            var unit = sandboxBootstrap?.Session?.ActiveUnit;
            if (unit?.Character?.Inventory?.Items == null || contentRoot == null || itemTemplate == null)
            {
                lastBuiltUnit = null;
                lastBuiltInventorySignature = string.Empty;
                return;
            }
            foreach (var raw in unit.Character.Inventory.Items)
            {
                if (raw is not ConsumableItemInstance item) continue;
                if (!IsCombatUsable(item)) continue;
                var skill = ConsumableRuntimeActionBuilder.Build(item);
                if (skill == null) continue;
                var current = item;
                var slot = Instantiate(itemTemplate, contentRoot, false);
                slot.gameObject.SetActive(true);
                slot.Initialize(skill, tooltipView,
                    () => { Close(); targetSelectionController?.BeginConsumableTargetSelection(current); },
                    () => sandboxBootstrap?.Session?.ActiveUnit);
                spawned.Add(slot);
            }

            lastBuiltUnit = unit;
            lastBuiltInventorySignature = BuildInventorySignature(unit);

        }

        private static string BuildInventorySignature(BattleUnit unit)
        {
            if (unit?.Character?.Inventory?.Items == null) return string.Empty;
            var result = string.Empty;
            foreach (var item in unit.Character.Inventory.Items)
            {
                if (item is ConsumableItemInstance consumable)
                {
                    result += $"|{consumable.InstanceId}:{consumable.CurrentCharges}";
                }
            }

            return result;
        }

        private static bool IsCombatUsable(ConsumableItemInstance item)
        {
            if (item == null) return false;
            if (item.RuntimeEffects == null || item.RuntimeEffects.Count == 0)
            {
                return ConsumableRuntimeRules.TryGetImpliedUtilityEffect(
                    item.ConsumableDefinition,
                    out var impliedEffect)
                    && impliedEffect.EffectType != ConsumableRuntimeEffectType.ReturnToArungard;
            }
            foreach (var effect in item.RuntimeEffects)
            {
                if (effect?.EffectType != ConsumableRuntimeEffectType.ReturnToArungard)
                    return true;
            }

            return false;
        }

        private IEnumerator ResizeAfterLayout()
        {
            yield return null;
            // The combat bootstrap can finish the active-unit/inventory binding in
            // the same frame as the window is opened. Rebuild once after that
            // binding so the first open is populated, not only the second one.
            Rebuild();
            ApplyLayoutAndResize();
            // UIWorkspaceWindow activates and registers the object in the same
            // frame as the ScrollRect hierarchy. Give Unity one more layout
            // pass before the final size is applied, otherwise the initial
            // ContentSizeFitter pass can still restore the tiny prefab size.
            yield return null;
            Rebuild();
            ApplyLayoutAndResize();
            yield return new WaitForEndOfFrame();
            ApplyLayoutAndResize();
            resizeRoutine = null;
        }

        private void ApplyLayoutAndResize()
        {
            Canvas.ForceUpdateCanvases();
            ForceLayoutHierarchy();
            Canvas.ForceUpdateCanvases();
            ResizeForItemCount();
            Canvas.ForceUpdateCanvases();
            ForceLayoutHierarchy();
            Canvas.ForceUpdateCanvases();
            window?.ClampToWorkspace();
        }

        private void ForceLayoutHierarchy()
        {
            // Rebuild the outer window first so the ScrollRect viewport receives
            // its final dimensions before GridLayoutGroup places its children.
            if (window != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(window.RectTransform);

            var viewport = contentRoot?.parent as RectTransform;
            if (viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

            if (contentRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        private void ResizeForItemCount()
        {
            if (contentRoot == null) return;
            var count = spawned.Count;
            GetGridDimensions(count, out var columns, out var rows);
            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            var gridPadding = grid?.padding;
            var horizontalPadding = gridPadding?.horizontal ?? 0;
            var verticalPadding = gridPadding?.vertical ?? 0;
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.cellSize = slotSize;
                grid.spacing = slotSpacing;

                var contentHeight = verticalPadding
                    + rows * slotSize.y
                    + Mathf.Max(0, rows - 1) * slotSpacing.y;
                contentRoot.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(1f, contentHeight));
            }

            if (window == null) return;
            var visibleRows = Mathf.Min(rows, maximumRows);
            var viewportChrome = GetViewportChrome();
            var size = new Vector2(
                horizontalPadding
                    + columns * slotSize.x + Mathf.Max(0, columns - 1) * slotSpacing.x
                    + viewportChrome.x,
                visibleRows * slotSize.y + Mathf.Max(0, visibleRows - 1) * slotSpacing.y
                    + verticalPadding
                    + viewportChrome.y);
            // UIWorkspaceWindow's interactive resize path enforces this bound;
            // the automatic content resize must use the same bound as well.
            size = Vector2.Max(size, window.MinimumSize);
            window.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            window.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private void GetGridDimensions(int count, out int columns, out int rows)
        {
            var itemCount = Mathf.Max(1, count);
            var maxColumns = Mathf.Max(1, maximumColumns);
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

        private Vector2 GetViewportChrome()
        {
            var viewport = contentRoot?.parent as RectTransform;
            if (window != null && viewport != null)
            {
                var outerSize = window.RectTransform.rect.size;
                var viewportSize = viewport.rect.size;
                var measured = outerSize - viewportSize;
                if (measured.x > 0f && measured.y > 0f)
                {
                    return measured;
                }
            }

            // PF_UIW_Window body (-32,-80) plus PF_UIW_ScrollView viewport
            // (-28,-16). This fallback is only used before the hierarchy has a
            // valid RectTransform size.
            return WindowContentChrome;
        }
    }
}
