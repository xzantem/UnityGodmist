using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public enum CombatDrawerEdge
    {
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>
    /// Animates one HUD panel between its authored edge position and an off-screen
    /// position. The toggle button is intentionally separate from the drawer rect,
    /// so it remains available after the panel has retracted.
    /// </summary>
    public class CombatEdgeDrawer : MonoBehaviour
    {
        [Header("Drawer")]
        [SerializeField] private RectTransform drawerRect;
        [SerializeField] private CanvasGroup drawerCanvasGroup;
        [SerializeField] private CombatDrawerEdge edge = CombatDrawerEdge.Left;
        [SerializeField, Min(0f)] private float edgeGap = 8f;
        [SerializeField, Min(0.01f)] private float animationDuration = 0.18f;
        [SerializeField] private bool startsExpanded = true;

        [Header("Manual Toggle")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private RectTransform toggleRect;
        [SerializeField] private RectTransform chevronRect;
        [SerializeField] private float expandedChevronRotationZ = 180f;
        [SerializeField] private float collapsedChevronRotationZ;

        private Vector2 expandedPosition;
        private Vector2 collapsedPosition;
        private Vector2 animationStartPosition;
        private Vector2 collapsedTogglePosition;
        private Vector2 expandedTogglePosition;
        private Vector2 animationStartTogglePosition;
        private float animationStartTime;
        private bool isAnimating;
        private bool isExpanded;
        private bool initialized;

        public bool IsExpanded => isExpanded;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(Toggle);
            }
        }

        private void OnDisable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(Toggle);
            }
        }

        private void Update()
        {
            if (!isAnimating || drawerRect == null)
            {
                return;
            }

            var progress = Mathf.Clamp01(
                (Time.unscaledTime - animationStartTime) / animationDuration);
            var easedProgress = progress * progress * (3f - 2f * progress);
            drawerRect.anchoredPosition = Vector2.Lerp(
                animationStartPosition,
                isExpanded ? expandedPosition : collapsedPosition,
                easedProgress);
            if (toggleRect != null)
            {
                toggleRect.anchoredPosition = Vector2.Lerp(
                    animationStartTogglePosition,
                    isExpanded ? expandedTogglePosition : collapsedTogglePosition,
                    easedProgress);
            }

            if (progress >= 1f)
            {
                isAnimating = false;
                ApplyCollapsedInteractionState();
            }
        }

        public void Toggle()
        {
            SetExpanded(!isExpanded);
        }

        public void Show()
        {
            SetExpanded(true);
        }

        public void Hide()
        {
            SetExpanded(false);
        }

        public void SetExpanded(bool expanded, bool immediate = false)
        {
            EnsureInitialized();
            if (drawerRect == null)
            {
                return;
            }

            if (isExpanded == expanded && !isAnimating)
            {
                return;
            }

            isExpanded = expanded;
            UpdateChevron();
            expandedTogglePosition = GetTogglePositionForDrawerPosition(
                expandedPosition);

            if (drawerCanvasGroup != null && expanded)
            {
                drawerCanvasGroup.alpha = 1f;
                drawerCanvasGroup.interactable = true;
                drawerCanvasGroup.blocksRaycasts = true;
            }

            if (immediate)
            {
                isAnimating = false;
                drawerRect.anchoredPosition = expanded
                    ? expandedPosition
                    : collapsedPosition;
                if (toggleRect != null)
                {
                    toggleRect.anchoredPosition = expanded
                        ? expandedTogglePosition
                        : collapsedTogglePosition;
                }
                ApplyCollapsedInteractionState();
                return;
            }

            animationStartPosition = drawerRect.anchoredPosition;
            if (toggleRect != null)
            {
                animationStartTogglePosition = toggleRect.anchoredPosition;
            }
            animationStartTime = Time.unscaledTime;
            isAnimating = true;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            if (drawerRect == null)
            {
                drawerRect = transform as RectTransform;
            }

            if (drawerRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            if (toggleRect == null && toggleButton != null)
            {
                toggleRect = toggleButton.transform as RectTransform;
            }

            expandedPosition = drawerRect.anchoredPosition;
            collapsedPosition = expandedPosition + GetCollapseOffset();
            if (toggleRect != null)
            {
                // Author the handle at the collapsed screen edge. The expanded
                // position is calculated from the moving inner edge of the drawer.
                collapsedTogglePosition = toggleRect.anchoredPosition;
            }
            initialized = true;
            // Force SetExpanded to apply the authored initial state even when it is
            // collapsed; otherwise the equality early-return would leave it visible.
            isExpanded = !startsExpanded;
            SetExpanded(startsExpanded, immediate: true);
        }

        private Vector2 GetCollapseOffset()
        {
            return edge switch
            {
                CombatDrawerEdge.Left => Vector2.left
                    * (drawerRect.rect.width + edgeGap),
                CombatDrawerEdge.Right => Vector2.right
                    * (drawerRect.rect.width + edgeGap),
                CombatDrawerEdge.Top => Vector2.up
                    * (drawerRect.rect.height + edgeGap),
                CombatDrawerEdge.Bottom => Vector2.down
                    * (drawerRect.rect.height + edgeGap),
                _ => Vector2.zero
            };
        }

        private Vector2 GetTogglePositionForDrawerPosition(
            Vector2 drawerPosition)
        {
            if (toggleRect == null
                || drawerRect.parent is not RectTransform drawerParentRect
                || toggleRect.parent is not RectTransform toggleParentRect)
            {
                return Vector2.zero;
            }

            var drawerCorners = new Vector3[4];
            drawerRect.GetWorldCorners(drawerCorners);
            var currentEdgeWorldPosition = edge switch
            {
                CombatDrawerEdge.Left => (drawerCorners[2] + drawerCorners[3]) * 0.5f,
                CombatDrawerEdge.Right => (drawerCorners[0] + drawerCorners[1]) * 0.5f,
                // GetWorldCorners order: bottom-left, top-left, top-right, bottom-right.
                CombatDrawerEdge.Top => (drawerCorners[0] + drawerCorners[3]) * 0.5f,
                CombatDrawerEdge.Bottom => (drawerCorners[1] + drawerCorners[2]) * 0.5f,
                _ => Vector3.zero
            };
            var drawerPositionDelta = drawerPosition - drawerRect.anchoredPosition;
            var targetEdgeWorldPosition = currentEdgeWorldPosition
                + drawerParentRect.TransformVector(drawerPositionDelta);
            var targetEdgeLocalPosition = (Vector2)toggleParentRect.InverseTransformPoint(
                targetEdgeWorldPosition);
            var toggleAnchorReference = new Vector2(
                Mathf.Lerp(
                    toggleParentRect.rect.xMin,
                    toggleParentRect.rect.xMax,
                    toggleRect.anchorMin.x),
                Mathf.Lerp(
                    toggleParentRect.rect.yMin,
                    toggleParentRect.rect.yMax,
                    toggleRect.anchorMin.y));
            return targetEdgeLocalPosition - toggleAnchorReference;
        }

        private void UpdateChevron()
        {
            if (chevronRect == null)
            {
                return;
            }

            var rotation = isExpanded
                ? expandedChevronRotationZ
                : collapsedChevronRotationZ;
            chevronRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void ApplyCollapsedInteractionState()
        {
            if (drawerCanvasGroup == null)
            {
                return;
            }

            drawerCanvasGroup.alpha = isExpanded ? 1f : 0f;
            drawerCanvasGroup.interactable = isExpanded;
            drawerCanvasGroup.blocksRaycasts = isExpanded;
        }
    }
}
