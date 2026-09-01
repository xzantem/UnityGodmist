using System;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class BlacksmithCraftingSlotView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Image border;
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI buyPriceText;

        private string tooltipTitle;
        private string tooltipBody;
        private ItemInstance tooltipItem;
        private CharacterInstance tooltipOwner;

        public void SetIcon(Sprite nextIcon)
        {
            if (icon == null) return;

            icon.sprite = nextIcon;
            icon.preserveAspect = true;
            icon.enabled = nextIcon != null;
        }

        public void SetTooltip(string nextTooltipTitle, string nextTooltipBody)
        {
            tooltipItem = null;
            tooltipOwner = null;
            tooltipTitle = nextTooltipTitle ?? string.Empty;
            tooltipBody = nextTooltipBody ?? string.Empty;
        }

        public void SetItemTooltip(ItemInstance item, CharacterInstance owner)
        {
            tooltipItem = item;
            tooltipOwner = owner;
        }

        public void Bind(
            Sprite nextIcon,
            string nextTooltipTitle,
            string nextTooltipBody,
            string countLabel,
            bool selected,
            bool missing,
            Action selectAction,
            string buyLabel = null,
            Action buyAction = null,
            CraftingMaterialPurchaseState purchaseState = CraftingMaterialPurchaseState.NotNeeded,
            bool buyInteractable = true)
        {
            SetTooltip(nextTooltipTitle, nextTooltipBody);
            SetIcon(nextIcon);

            if (countText != null)
            {
                countText.gameObject.SetActive(!string.IsNullOrWhiteSpace(countLabel));
                countText.text = countLabel ?? string.Empty;
            }

            var palette = GetComponent<UIWorkspaceStyleApplier>()?.Palette
                ?? GetComponentInChildren<UIWorkspaceStyleApplier>()?.Palette;
            if (border != null && palette != null)
            {
                var missingAccent = purchaseState is CraftingMaterialPurchaseState.InsufficientGold
                    or CraftingMaterialPurchaseState.OutOfStock
                    ? UIWorkspaceAccentStyle.Negative
                    : UIWorkspaceAccentStyle.Warning;
                border.color = missing
                    ? palette.GetAccentColor(missingAccent)
                    : selected
                        ? palette.GetAccentColor(UIWorkspaceAccentStyle.Selected)
                        : palette.GetAccentColor(UIWorkspaceAccentStyle.Neutral);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => selectAction?.Invoke());
            }

            var canBuy = buyAction != null && !string.IsNullOrWhiteSpace(buyLabel);
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(canBuy);
                buyButton.interactable = canBuy && buyInteractable;
                buyButton.onClick.RemoveAllListeners();
                if (canBuy) buyButton.onClick.AddListener(() => buyAction());
            }

            if (buyPriceText != null) buyPriceText.text = buyLabel ?? string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipItem != null)
            {
                UIWorkspaceTooltipView.ActiveInstance?.ShowItem(tooltipItem, tooltipOwner, this);
                return;
            }

            UIWorkspaceTooltipView.ActiveInstance?.ShowText(tooltipTitle, tooltipBody, this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
