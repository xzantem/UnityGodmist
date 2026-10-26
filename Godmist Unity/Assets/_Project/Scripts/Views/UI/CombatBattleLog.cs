using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatBattleLog : MonoBehaviour, ICanvasElement
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect scrollRect;
        [Header("Readability")]
        [SerializeField, Min(1)] private int maximumEntries = 100;
        [SerializeField] private bool separateEntries = true;

        private readonly List<string> entries = new();
        private bool viewDirty, scrollPending;

        private void Awake()
        {
            RefreshView();
        }

        public void AddEntry(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            entries.Add(message);
            if (entries.Count > maximumEntries)
            {
                entries.RemoveAt(0);
            }
            RefreshView();
            ScrollToBottom();
        }

        public void ClearLog()
        {
            entries.Clear();
            RefreshView();
            ScrollToBottom();
        }

        private void RefreshView()
        {
            viewDirty = true;
        }

        private void LateUpdate()
        {
            if (viewDirty)
            {
                if (logText != null) logText.text = string.Join(separateEntries ? "\n\n" : "\n", entries);
                viewDirty = false;
            }
            if (scrollPending && scrollRect != null)
                CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }

        // Scroll after the normal layout pass. A damage burst must never force
        // every canvas to rebuild for every individual battle-log line.
        public void Rebuild(CanvasUpdate phase)
        {
            if (phase != CanvasUpdate.PostLayout || !scrollPending) return;
            if (scrollRect != null && scrollRect.content != null) scrollRect.verticalNormalizedPosition = 0f;
            scrollPending = false;
        }
        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        public bool IsDestroyed() => this == null;
        private void OnDisable() => CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

        private void ScrollToBottom()
        {
            scrollPending = true;
        }
    }
}
