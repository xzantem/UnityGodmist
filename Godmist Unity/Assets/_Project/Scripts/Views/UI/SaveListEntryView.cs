using System;
using Godmist.Core.Enums;
using Godmist.Systems.SaveLoad;
using Godmist.Data.World;
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
        [SerializeField] private Sprite normalBackground;
        [SerializeField] private Sprite selectedBackground;

        public void Bind(
            GameSaveSummary summary,
            bool isSelected,
            Action<GameSaveSummary> selectionCallback,
            GameContentCatalog catalog = null,
            bool compact = false)
        {
            if (summary == null) return;

            titleText.text = compact && summary.SlotType != SaveSlotType.Manual ? SaveSummaryPresentation.Slot(summary) : SaveSummaryPresentation.Title(summary, catalog);
            detailsText.text = compact ? $"{SaveSummaryPresentation.Date(summary)}\n{SaveSummaryPresentation.Location(summary, catalog)}" : SaveSummaryPresentation.RowDetails(summary, catalog);

            SetSelected(isSelected);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selectionCallback?.Invoke(summary));
        }

        public void SetSelected(bool isSelected)
        {
            if (background == null) return;
            if (normalBackground != null && selectedBackground != null)
            {
                background.sprite = isSelected ? selectedBackground : normalBackground;
                background.color = Color.white;
            }
            else background.color = isSelected ? selectedColor : normalColor;
        }

    }
}
