using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Quests;
using Godmist.Views.UI.Workspace;
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
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Color selectedColor = new(0.38f, 0.29f, 0.09f, 1f);
        [SerializeField] private Color defaultColor = new(0.08f, 0.12f, 0.18f, 1f);
        [SerializeField] private UIWorkspaceStylePalette palette;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private GameObject trackedMarker;

        private string questId;

        public void Bind(
            QuestInstance quest,
            bool isSelected,
            Action<string> selectionCallback,
            bool isTracked = false)
        {
            questId = quest?.QuestId ?? string.Empty;

            if (levelText != null)
            {
                var level = quest?.RadiantRewardOffer?.Level;
                levelText.text = level.HasValue ? level.Value.ToString() : "—";
                levelText.color = level.HasValue
                    ? QuestJournalPresentation.Cream : new Color32(145, 143, 132, 255);
            }

            if (titleText != null)
            {
                titleText.text = quest?.DisplayName ?? questId;
            }

            if (statusText != null)
            {
                statusText.text = quest != null ? QuestJournalPresentation.ProgressSummary(quest) : string.Empty;
            }

            if (background != null)
            {
                background.color = palette != null ? Color.white : isSelected
                    ? selectedColor
                    : defaultColor;
            }

            if (selectionFrame != null && palette != null)
                palette.ApplyFrameAccent(selectionFrame, isSelected
                    ? UIWorkspaceAccentStyle.Selected : UIWorkspaceAccentStyle.Neutral);
            if (trackedMarker != null) trackedMarker.SetActive(isTracked);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selectionCallback?.Invoke(questId));
        }

    }
}
