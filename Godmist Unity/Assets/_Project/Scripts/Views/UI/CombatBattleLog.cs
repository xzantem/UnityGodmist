using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatBattleLog : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect scrollRect;
        [Header("Readability")]
        [SerializeField, Min(1)] private int maximumEntries = 100;
        [SerializeField, Min(1f)] private float fontSize = 18f;
        [SerializeField, Min(0f)] private float lineSpacing = 8f;
        [SerializeField] private bool separateEntries = true;

        private readonly List<string> entries = new();

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
            if (logText == null)
            {
                return;
            }

            logText.fontSize = fontSize;
            logText.lineSpacing = lineSpacing;
            logText.raycastTarget = false;
            logText.text = string.Join(separateEntries ? "\n\n" : "\n", entries);
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
