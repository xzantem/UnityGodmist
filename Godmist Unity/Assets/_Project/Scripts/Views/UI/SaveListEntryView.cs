using System;
using Godmist.Core.Enums;
using Godmist.Systems.SaveLoad;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class SaveListEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailsText;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new(0.08f, 0.12f, 0.18f, 0.92f);
        [SerializeField] private Color selectedColor = new(0.36f, 0.25f, 0.06f, 0.96f);

        public void Bind(
            GameSaveSummary summary,
            bool isSelected,
            Action<GameSaveSummary> selectionCallback)
        {
            if (summary == null) return;

            titleText.text = summary.DisplayName;
            detailsText.text = BuildDetailsText(summary);

            SetSelected(isSelected);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selectionCallback?.Invoke(summary));
        }

        public void SetSelected(bool isSelected)
        {
            if (background != null) background.color = isSelected ? selectedColor : normalColor;
        }

        private static string BuildDetailsText(GameSaveSummary summary)
        {
            var slotLabel = summary.SlotType switch
            {
                SaveSlotType.Quick => "Szybki zapis",
                SaveSlotType.Autosave => "Automatyczny zapis",
                _ => "Zapis ręczny"
            };

            var dateLabel = summary.UpdatedAtUtc == DateTime.MinValue
                ? "nieznana data"
                : summary.UpdatedAtUtc
                    .ToLocalTime()
                    .ToString("dd.MM.yyyy HH:mm");

            return $"{slotLabel} | {dateLabel}";
        }
    }
}