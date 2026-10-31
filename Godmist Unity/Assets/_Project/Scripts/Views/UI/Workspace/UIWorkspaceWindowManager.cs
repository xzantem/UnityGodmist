using System.Collections.Generic;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Owns presentation order and focused-window routing for one workspace layer.
    /// It does not pause gameplay; context menus can claim input for one frame.
    /// </summary>
    public sealed class UIWorkspaceWindowManager : MonoBehaviour
    {
        [SerializeField] private RectTransform workspaceRoot;
        [SerializeField] private KeyCode closeFocusedWindowKey = KeyCode.Escape;

        private readonly List<UIWorkspaceWindow> openWindows = new();
        private const float CascadeHeaderReveal = 48f;
        private static readonly Vector2 CascadeHorizontalOffset = new(48f, 0f);

        public static UIWorkspaceWindowManager Instance { get; private set; }
        public RectTransform WorkspaceRoot => workspaceRoot != null
            ? workspaceRoot
            : transform as RectTransform;
        public UIWorkspaceWindow FocusedWindow { get; private set; }
        public bool HasOpenWindows => openWindows.Count > 0;
        private int contextMenuInputFrame = -1;
        public bool ConsumedContextMenuInputThisFrame => contextMenuInputFrame == Time.frameCount;
        public void ConsumeContextMenuInput() => contextMenuInputFrame = Time.frameCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("W scenie istnieje więcej niż jeden UIWorkspaceWindowManager.", this);
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen
                || closeFocusedWindowKey == KeyCode.None
                || !Input.GetKeyDown(closeFocusedWindowKey)
                || UIWorkspaceInputRouter.HasFocusedContextMenu()
                || UIWorkspaceInputRouter.HasFocusedTextInput())
            {
                return;
            }

            if (FocusedWindow == null)
            {
                return;
            }

            FocusedWindow.Close();
        }

        public void Register(UIWorkspaceWindow window)
        {
            if (window == null || openWindows.Contains(window))
            {
                return;
            }

            var previousWindow = FocusedWindow;
            openWindows.Add(window);
            if (previousWindow != null && previousWindow != window)
            {
                window.RectTransform.anchoredPosition = GetCascadedPosition(window, previousWindow);
            }
            Focus(window);
        }

        public void Unregister(UIWorkspaceWindow window)
        {
            if (window == null)
            {
                return;
            }

            openWindows.Remove(window);

            if (FocusedWindow != window)
            {
                return;
            }

            FocusedWindow = openWindows.Count > 0
                ? openWindows[openWindows.Count - 1]
                : null;
        }

        public void Focus(UIWorkspaceWindow window)
        {
            if (window == null)
            {
                return;
            }

            // Explicit focus may arrive before Unity invokes OnEnable in EditMode
            // tests or immediately after a dynamically created window is parented.
            if (!openWindows.Contains(window)) openWindows.Add(window);

            FocusedWindow = window;
            window.transform.SetAsLastSibling();
            window.GetComponent<Godmist.Views.UI.ControllerNavigationScope>()?.Focus();
        }

        public void CascadeAndFocus(UIWorkspaceWindow window, UIWorkspaceWindow previousWindow)
        {
            if (window == null) return;
            if (previousWindow != null && previousWindow != window)
                window.RectTransform.anchoredPosition = GetCascadedPosition(window, previousWindow);
            Focus(window);
        }

        private static Vector2 GetCascadedPosition(UIWorkspaceWindow window, UIWorkspaceWindow previousWindow)
        {
            var next = window.RectTransform;
            var previous = previousWindow.RectTransform;

            // Offset from the prior window's top edge instead of from its pivot.
            // Pivot-only offsets fail when a large window is opened first: a smaller
            // window can sit entirely inside it and leave no header to focus.
            var previousTop = previousWindow.RectTransform.anchoredPosition.y
                + previous.rect.height * (1f - previous.pivot.y);
            var nextTopOffset = next.rect.height * (1f - next.pivot.y);
            return new Vector2(
                previous.anchoredPosition.x + CascadeHorizontalOffset.x,
                previousTop - CascadeHeaderReveal - nextTopOffset);
        }
    }
}
