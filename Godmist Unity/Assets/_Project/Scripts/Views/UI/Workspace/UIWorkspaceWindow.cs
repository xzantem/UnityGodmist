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

        public RectTransform RectTransform => rectTransform != null
            ? rectTransform
            : rectTransform = GetComponent<RectTransform>();
        public UIWorkspaceWindowManager Manager => manager;
        public bool IsOpen => gameObject.activeInHierarchy;

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
            var sizeDelta = new Vector2(
                allowHorizontalResize ? localDelta.x : 0f,
                allowVerticalResize ? -localDelta.y : 0f);
            var nextSize = Vector2.Max(minimumSize, RectTransform.sizeDelta + sizeDelta);
            nextSize = Vector2.Min(nextSize, allowedMaximum);
            if (!allowHorizontalResize) nextSize.x = RectTransform.sizeDelta.x;
            if (!allowVerticalResize) nextSize.y = RectTransform.sizeDelta.y;
            var appliedDelta = nextSize - RectTransform.sizeDelta;

            RectTransform.sizeDelta = nextSize;
            RectTransform.anchoredPosition += new Vector2(
                appliedDelta.x * (1f - RectTransform.pivot.x),
                -appliedDelta.y * RectTransform.pivot.y);
            ClampToWorkspace();
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

            var halfSize = RectTransform.rect.size * 0.5f;
            var min = root.rect.min + halfSize;
            var max = root.rect.max - halfSize;
            var position = RectTransform.anchoredPosition;
            position.x = min.x > max.x
                ? root.rect.center.x
                : Mathf.Clamp(position.x, min.x, max.x);
            position.y = min.y > max.y
                ? root.rect.center.y
                : Mathf.Clamp(position.y, min.y, max.y);
            RectTransform.anchoredPosition = position;
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
