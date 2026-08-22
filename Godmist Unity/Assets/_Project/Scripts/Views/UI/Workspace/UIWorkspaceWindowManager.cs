using System.Collections.Generic;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Owns presentation order and focused-window routing for one workspace layer.
    /// It deliberately does not pause or otherwise block gameplay input.
    /// </summary>
    public sealed class UIWorkspaceWindowManager : MonoBehaviour
    {
        [SerializeField] private RectTransform workspaceRoot;
        [SerializeField] private KeyCode closeFocusedWindowKey = KeyCode.Escape;

        private readonly List<UIWorkspaceWindow> openWindows = new();

        public static UIWorkspaceWindowManager Instance { get; private set; }
        public RectTransform WorkspaceRoot => workspaceRoot != null
            ? workspaceRoot
            : transform as RectTransform;
        public UIWorkspaceWindow FocusedWindow { get; private set; }
        public bool HasOpenWindows => openWindows.Count > 0;

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
                || UIWorkspaceInputRouter.HasFocusedTextInput())
            {
                return;
            }

            FocusedWindow?.Close();
        }

        public void Register(UIWorkspaceWindow window)
        {
            if (window == null || openWindows.Contains(window))
            {
                return;
            }

            openWindows.Add(window);
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
            if (window == null || !openWindows.Contains(window))
            {
                return;
            }

            FocusedWindow = window;
            window.transform.SetAsLastSibling();
        }
    }
}
