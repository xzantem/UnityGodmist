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

        private readonly List<string> entries = new();

        public void AddEntry(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            entries.Add(message);
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

            logText.text = string.Join("\n", entries);
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