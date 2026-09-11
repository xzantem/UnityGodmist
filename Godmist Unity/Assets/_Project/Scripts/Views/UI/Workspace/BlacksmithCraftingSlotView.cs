using System;
using Godmist.Data.Items;
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
        private string tooltipExtraBody;
        private int? tooltipPrice;
        private string tooltipMeta;
        private bool selectedState;
        private bool missingState;
        private CraftingMaterialPurchaseState purchaseState;

        public void SetIcon(Sprite nextIcon)
        {
            if (icon == null) return;

            icon.sprite = nextIcon;
            icon.preserveAspect = true;
            icon.enabled = nextIcon != null;
        }

        public void SetTooltip(string nextTooltipTitle, string nextTooltipBody)
        {
            SetTooltip(nextTooltipTitle, nextTooltipBody, string.Empty);
        }

        public void SetTooltip(string nextTooltipTitle, string nextTooltipBody, string nextTooltipMeta)
        {
            tooltipItem = null;
            tooltipOwner = null;
            tooltipExtraBody = string.Empty;
            tooltipPrice = null;
            tooltipMeta = nextTooltipMeta ?? string.Empty;
            tooltipTitle = nextTooltipTitle ?? string.Empty;
            tooltipBody = nextTooltipBody ?? string.Empty;
        }

        public void SetItemTooltip(ItemInstance item, CharacterInstance owner)
        {
            SetItemTooltip(item, owner, string.Empty);
        }

        /// <summary>Uses the normal inventory/trade tooltip for a catalog item.</summary>
        public void SetDefinitionTooltip(ItemDefinition definition, string extraBody = "", int? contextPrice = null)
        {
            if (definition == null) return;
            SetItemTooltip(new ItemInstance(definition), null, extraBody, contextPrice);
        }

        public void SetItemTooltip(
            ItemInstance item,
            CharacterInstance owner,
            string extraBody,
            int? contextPrice = null)
        {
            tooltipItem = item;
            tooltipOwner = owner;
            tooltipExtraBody = extraBody ?? string.Empty;
            tooltipPrice = contextPrice;
            tooltipMeta = string.Empty;
        }

        public void SetSelected(bool selected)
        {
            selectedState = selected;
            RefreshBorderColor();
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
            bool buyInteractable = true,
            string nextTooltipMeta = null)
        {
            SetTooltip(nextTooltipTitle, nextTooltipBody, nextTooltipMeta);
            SetIcon(nextIcon);
            selectedState = selected;
            missingState = missing;
            this.purchaseState = purchaseState;

            if (countText != null)
            {
                countText.gameObject.SetActive(!string.IsNullOrWhiteSpace(countLabel));
                countText.text = countLabel ?? string.Empty;
            }

            RefreshBorderColor();

            var rootButton = GetComponent<Button>();
            if (rootButton == null)
            {
                rootButton = gameObject.AddComponent<Button>();
                rootButton.transition = Selectable.Transition.None;
            }

            if (rootButton.targetGraphic == null)
                rootButton.targetGraphic = GetComponent<Graphic>();

            // Some material cards contain a nested PF_UIW_ItemSlot Button while
            // recipe cards have a Button on their root. Keep one click target for
            // the entire card so clicking its icon/count/background behaves identically.
            if (button != null && button != rootButton)
            {
                button.onClick.RemoveAllListeners();
                button.enabled = false;
            }

            rootButton.onClick.RemoveAllListeners();
            rootButton.onClick.AddListener(() => selectAction?.Invoke());

            var canBuy = buyAction != null && !string.IsNullOrWhiteSpace(buyLabel);
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(canBuy);
                buyButton.interactable = canBuy && buyInteractable;
                buyButton.onClick.RemoveAllListeners();
                if (canBuy) buyButton.onClick.AddListener(() => buyAction());
            }

            if (buyPriceText != null)
            {
                buyPriceText.text = buyLabel ?? string.Empty;
                var priceRect = buyPriceText.rectTransform;
                priceRect.offsetMin = new Vector2(2f, priceRect.offsetMin.y);
                priceRect.offsetMax = new Vector2(-2f, priceRect.offsetMax.y);
                buyPriceText.alignment = TextAlignmentOptions.Center;
                buyPriceText.verticalAlignment = VerticalAlignmentOptions.Middle;
                buyPriceText.textWrappingMode = TextWrappingModes.NoWrap;
                buyPriceText.overflowMode = TextOverflowModes.Overflow;
                buyPriceText.enableAutoSizing = true;
                buyPriceText.fontSizeMin = 8f;
                buyPriceText.fontSizeMax = Mathf.Max(8f, buyPriceText.fontSize);
            }
        }

        private void RefreshBorderColor()
        {
            var palette = GetComponent<UIWorkspaceStyleApplier>()?.Palette
                ?? GetComponentInChildren<UIWorkspaceStyleApplier>()?.Palette;
            if (border == null || palette == null) return;

            var missingAccent = purchaseState is CraftingMaterialPurchaseState.InsufficientGold
                or CraftingMaterialPurchaseState.OutOfStock
                ? UIWorkspaceAccentStyle.Negative
                : UIWorkspaceAccentStyle.Warning;
            border.color = missingState
                ? palette.GetAccentColor(missingAccent)
                : selectedState
                    ? palette.GetAccentColor(UIWorkspaceAccentStyle.Selected)
                    : palette.GetAccentColor(UIWorkspaceAccentStyle.Neutral);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipItem != null)
            {
                if (string.IsNullOrWhiteSpace(tooltipExtraBody))
                    UIWorkspaceTooltipView.ActiveInstance?.ShowItem(tooltipItem, tooltipOwner, this);
                else
                    UIWorkspaceTooltipView.ActiveInstance?.ShowItemWithExtra(
                        tooltipItem, tooltipOwner, this, tooltipExtraBody, tooltipPrice);
                return;
            }

            UIWorkspaceTooltipView.ActiveInstance?.ShowText(
                tooltipTitle, tooltipBody, this, tooltipMeta);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
