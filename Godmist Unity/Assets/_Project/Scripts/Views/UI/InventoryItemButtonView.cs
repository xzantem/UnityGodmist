using System;
using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class InventoryItemButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemInfoText;
        [SerializeField] private Color normalColor = new(0.11f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color selectedColor = new(0.35f, 0.28f, 0.12f, 1f);

        private ItemInstance item;
        private Action<ItemInstance> selectionCallback;

        public void Bind(
            ItemInstance nextItem,
            int stackCount,
            bool isSelected,
            Action<ItemInstance> nextSelectionCallback,
            string contextInfo = null)
        {
            item = nextItem;
            selectionCallback = nextSelectionCallback;

            var definition = item?.Definition;

            var stackSuffix = stackCount > 1 ? $" x{stackCount}" : string.Empty;

            if (itemNameText != null)
            {
                var itemName = definition != null ? definition.DisplayName : "-";
                itemNameText.text = $"{itemName}{stackSuffix}";
            }

            var defaultInfo = definition != null
                ? $"{item.CurrentRarity} | poziom {item.ItemLevel} | {definition.Weight:0.##} kg"
                : "-";

            itemInfoText.text = string.IsNullOrWhiteSpace(contextInfo)
                ? defaultInfo
                : $"{defaultInfo}\n{contextInfo}";

            if (iconImage != null)
            {
                var hasIcon = definition?.Icon != null;

                iconImage.gameObject.SetActive(hasIcon);

                if (hasIcon)
                {
                    iconImage.sprite = definition.Icon;
                }
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = item != null;
            button.onClick.AddListener(NotifySelected);
        }

        private void NotifySelected()
        {
            selectionCallback?.Invoke(item);
        }
    }
}