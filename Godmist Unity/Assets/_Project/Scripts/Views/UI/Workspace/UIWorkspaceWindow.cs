using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Common chrome behavior for a non-modal floating workspace window.
    /// Drag and resize handles forward their pointer deltas here.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIWorkspaceWindow : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private UIWorkspaceWindowManager manager;
        [SerializeField] private Button closeButton;
        [SerializeField, Min(1f)] private Vector2 minimumSize = new(320f, 240f);
        [SerializeField, Min(1f)] private Vector2 maximumSize = new(1200f, 900f);
        [SerializeField] private bool allowHorizontalResize = true;
        [SerializeField] private bool allowVerticalResize = true;

        private RectTransform rectTransform;
        private Vector2 resizeGridStep;
        private Vector2 resizeGridInset;
        private Vector2Int resizeGridMaximumCells;
        private Vector2 requestedResizeSize;
        private bool isResizing;

        public RectTransform RectTransform => rectTransform != null
            ? rectTransform
            : rectTransform = GetComponent<RectTransform>();
        public UIWorkspaceWindowManager Manager => manager;
        public bool IsOpen => gameObject.activeInHierarchy;
        public Vector2 MinimumSize => minimumSize;
        public Vector2 MaximumSize => maximumSize;
        public bool IsResizing => isResizing;

        // Runtime opt-in: ordinary windows retain continuous resizing.
        public void ConfigureResizeGrid(Vector2 step, Vector2 inset, Vector2Int maximumCells = default)
        {
            resizeGridStep = step;
            resizeGridInset = inset;
            resizeGridMaximumCells = maximumCells;
        }

        public void BeginResize()
        {
            requestedResizeSize = RectTransform.sizeDelta;
            isResizing = true;
        }

        public void EndResize() => isResizing = false;

        public void ConfigureResizeAxes(bool horizontal, bool vertical)
        {
            allowHorizontalResize = horizontal;
            allowVerticalResize = vertical;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (manager == null)
            {
                manager = GetComponentInParent<UIWorkspaceWindowManager>();
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        private void OnEnable()
        {
            ResolveManager()?.Register(this);
        }

        private void OnDisable()
        {
            EndResize();
            if (manager != null)
            {
                manager.Unregister(this);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Focus();
        }

        public void Open()
        {
            var resolvedManager = ResolveManager();
            var previousWindow = resolvedManager?.FocusedWindow;
            gameObject.SetActive(true);
            resolvedManager?.CascadeAndFocus(this, previousWindow);
        }

        public void Close()
        {
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            gameObject.SetActive(false);
        }

        public void Focus()
        {
            ResolveManager()?.Focus(this);
        }

        public void Move(Vector2 localDelta)
        {
            RectTransform.anchoredPosition += localDelta;
            ClampToWorkspace();
        }

        public void ResizeFromBottomRight(Vector2 localDelta)
        {
            var root = ResolveManager()?.WorkspaceRoot;
            var parentSize = root != null ? root.rect.size : maximumSize;
            var allowedMaximum = Vector2.Min(maximumSize, parentSize);
            if (resizeGridStep.x > 0f && resizeGridMaximumCells.x > 0)
                allowedMaximum.x = Mathf.Min(allowedMaximum.x, resizeGridInset.x + resizeGridMaximumCells.x * resizeGridStep.x);
            if (resizeGridStep.y > 0f && resizeGridMaximumCells.y > 0)
                allowedMaximum.y = Mathf.Min(allowedMaximum.y, resizeGridInset.y + resizeGridMaximumCells.y * resizeGridStep.y);
            var sizeDelta = new Vector2(
                allowHorizontalResize ? localDelta.x : 0f,
                allowVerticalResize ? -localDelta.y : 0f);
            // Keep sub-cell pointer movement, otherwise repeated small deltas never reach a snap.
            var nextSize = Vector2.Max(minimumSize,
                (isResizing ? requestedResizeSize : RectTransform.sizeDelta) + sizeDelta);
            nextSize = Vector2.Min(nextSize, allowedMaximum);
            if (isResizing) requestedResizeSize = nextSize;
            nextSize.x = SnapResizeAxis(nextSize.x, minimumSize.x, allowedMaximum.x,
                resizeGridStep.x, resizeGridInset.x, resizeGridMaximumCells.x);
            nextSize.y = SnapResizeAxis(nextSize.y, minimumSize.y, allowedMaximum.y,
                resizeGridStep.y, resizeGridInset.y, resizeGridMaximumCells.y);
            if (!allowHorizontalResize) nextSize.x = RectTransform.sizeDelta.x;
            if (!allowVerticalResize) nextSize.y = RectTransform.sizeDelta.y;
            var appliedDelta = nextSize - RectTransform.sizeDelta;

            RectTransform.sizeDelta = nextSize;
            RectTransform.anchoredPosition += new Vector2(
                appliedDelta.x * (1f - RectTransform.pivot.x),
                -appliedDelta.y * RectTransform.pivot.y);
            ClampToWorkspace();
        }

        private static float SnapResizeAxis(float requested, float minimum, float maximum,
            float step, float inset, int maximumCells)
        {
            if (step <= 0f) return requested;
            var first = Mathf.Max(1, Mathf.CeilToInt((minimum - inset) / step - .001f));
            var last = Mathf.FloorToInt((maximum - inset) / step + .001f);
            if (maximumCells > 0) last = Mathf.Min(last, maximumCells);
            // A very small workspace may not fit even one cell plus the window chrome.
            if (last < 1) return Mathf.Min(requested, maximum);
            first = Mathf.Min(first, last); // Fitting the workspace takes precedence over the minimum.
            var cells = Mathf.Clamp(Mathf.RoundToInt((requested - inset) / step), first, last);
            return inset + cells * step;
        }

        public void ClampToWorkspace()
        {
            var root = ResolveManager()?.WorkspaceRoot;
            if (root == null || root == RectTransform.parent)
            {
                root = RectTransform.parent as RectTransform;
            }

            if (root == null)
            {
                return;
            }

            // Compare actual corners in workspace coordinates, independent of anchors/pivot.
            var corners = new Vector3[4];
            RectTransform.GetWorldCorners(corners);
            var min = (Vector2)root.InverseTransformPoint(corners[0]);
            var max = min;
            foreach (var corner in corners)
            {
                var point = (Vector2)root.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            var bounds = root.rect;
            var correction = Vector2.zero;
            correction.x = max.x - min.x > bounds.width ? bounds.center.x - (min.x + max.x) * .5f
                : min.x < bounds.xMin ? bounds.xMin - min.x : max.x > bounds.xMax ? bounds.xMax - max.x : 0f;
            correction.y = max.y - min.y > bounds.height ? bounds.center.y - (min.y + max.y) * .5f
                : min.y < bounds.yMin ? bounds.yMin - min.y : max.y > bounds.yMax ? bounds.yMax - max.y : 0f;
            RectTransform.position += root.TransformVector(correction);
        }

        private UIWorkspaceWindowManager ResolveManager()
        {
            if (manager == null)
            {
                manager = GetComponentInParent<UIWorkspaceWindowManager>()
                    ?? UIWorkspaceWindowManager.Instance;
            }

            return manager;
        }
    }
}
