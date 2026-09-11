using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public enum NpcServiceKind
    {
        Trade,
        EquipmentCrafting,
        MaterialCrafting,
        EquipmentUpgrade,
        EquipmentReforge,
        EquipmentInfusion,
        JewelryCrafting,
        GalduryteCrafting,
        GalduryteIdentification,
        PotionCrafting,
        PotionRefill,
        BombCrafting,
        BombRefill
    }

    public sealed class NpcServiceTabView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image selectedBorder;

        private Action selectAction;
        private string tooltipTitle = string.Empty;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Select);
        }

        public void Bind(
            Sprite nextIcon,
            string nextTooltipTitle,
            bool isSelected,
            Action nextSelectAction)
        {
            if (icon != null) icon.sprite = nextIcon;
            if (selectedBorder != null) selectedBorder.enabled = isSelected;
            tooltipTitle = nextTooltipTitle ?? string.Empty;
            selectAction = nextSelectAction;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrWhiteSpace(tooltipTitle))
            {
                UIWorkspaceTooltipView.ActiveInstance?.ShowText(
                    tooltipTitle,
                    string.Empty,
                    this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }

        private void Select()
        {
            selectAction?.Invoke();
        }
    }
}
