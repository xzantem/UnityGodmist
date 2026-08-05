using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private Color normalColor = new(0.11f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color selectedColor = new(0.35f, 0.28f, 0.12f, 1f);

        private EquipmentItemInstance item;
        private Action<EquipmentItemInstance> selectionCallback;

        public EquipmentSlot Slot => slot;

        public void Bind(
            EquipmentItemInstance nextItem,
            bool isSelected,
            Action<EquipmentItemInstance> nextSelectionCallback)
        {
            item = nextItem;
            selectionCallback = nextSelectionCallback;

            slotNameText.text = GetSlotName(slot);
            itemNameText.text = item?.Definition != null
                ? item.Definition.DisplayName
                : "Pusty";

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

        private static string GetSlotName(EquipmentSlot equipmentSlot)
        {
            return equipmentSlot switch
            {
                EquipmentSlot.Weapon => "Bron",
                EquipmentSlot.Armor => "Pancerz",
                EquipmentSlot.Necklace => "Naszyjnik",
                EquipmentSlot.Ring => "Pierscien",
                EquipmentSlot.Bracelet => "Bransoleta",
                EquipmentSlot.Earrings => "Kolczyki",
                _ => equipmentSlot.ToString()
            };
        }
    }
}