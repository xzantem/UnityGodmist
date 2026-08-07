using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class QuestJournalQuestButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Color selectedColor = new(0.38f, 0.29f, 0.09f, 1f);
        [SerializeField] private Color defaultColor = new(0.08f, 0.12f, 0.18f, 1f);

        private string questId;

        public void Bind(
            QuestInstance quest,
            bool isSelected,
            Action<string> selectionCallback)
        {
            questId = quest?.QuestId ?? string.Empty;

            if (titleText != null)
            {
                titleText.text = quest?.Definition?.DisplayName ?? questId;
            }

            if (statusText != null)
            {
                statusText.text = GetStatusLabel(quest?.Status);
            }

            if (background != null)
            {
                background.color = isSelected
                    ? selectedColor
                    : defaultColor;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selectionCallback?.Invoke(questId));
        }

        private static string GetStatusLabel(QuestStatus? status)
        {
            return status switch
            {
                QuestStatus.Active => "Aktywne",
                QuestStatus.Completed => "Ukończone",
                QuestStatus.TurnedIn => "Oddane",
                QuestStatus.Failed => "Nieudane",
                _ => string.Empty
            };
        }
    }
}