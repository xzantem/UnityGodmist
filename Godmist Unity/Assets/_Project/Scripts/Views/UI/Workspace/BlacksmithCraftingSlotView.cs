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

        private ItemRarityBorderGraphic rarityBorder;
        private string tooltipTitle;
        private string tooltipBody;
        private ItemInstance tooltipItem;
        private CharacterInstance tooltipOwner;
        private string tooltipExtraBody;
        private int? tooltipPrice;
        private string tooltipMeta;
        private bool selectedState;
        private bool missingState;
        private bool hoveredState;
        private bool selectionInteractable = true;
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
            SetRarity(null);
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
            if (definition == null) { SetItemTooltip(null, null); return; }
            SetItemTooltip(new ItemInstance(definition), null, extraBody, contextPrice);
        }

        public void SetItemTooltip(
            ItemInstance item,
            CharacterInstance owner,
            string extraBody,
            int? contextPrice = null)
        {
            tooltipItem = item;
            SetRarity(item?.CurrentRarity);
            tooltipOwner = owner;
            tooltipExtraBody = extraBody ?? string.Empty;
            tooltipPrice = contextPrice;
            tooltipMeta = string.Empty;
        }

        public void SetRarity(Godmist.Core.Enums.RarityTier? rarity)
        {
            ItemRarityPresentation.ApplyBorder(ref rarityBorder,
                button != null ? button.transform as RectTransform : transform as RectTransform, rarity);
        }

        public void SetSelected(bool selected)
        {
            selectedState = selected;
            RefreshBorderColor();
        }

        public void SetSelectionInteractable(bool interactable, float disabledBrightness = .4f)
        {
            selectionInteractable = interactable;
            var rootButton = GetComponent<Button>();
            if (rootButton != null) rootButton.interactable = interactable;
            disabledBrightness = Mathf.Clamp01(disabledBrightness);
            if (icon != null) icon.color = interactable ? Color.white
                : new Color(disabledBrightness, disabledBrightness, disabledBrightness, 1f);
            if (countText != null)
            {
                var color = countText.color;
                color.a = interactable ? 1f : Mathf.Clamp01(disabledBrightness + .05f);
                countText.color = color;
            }
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
                countText.color = buyButton != null
                    ? missing ? new Color32(230, 163, 111, 255) : new Color32(112, 199, 135, 255)
                    : new Color32(223, 215, 184, 255);
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
            var accent = missingState
                ? missingAccent
                : selectedState
                    ? UIWorkspaceAccentStyle.Selected
                    : buyButton != null
                        ? UIWorkspaceAccentStyle.Positive
                        : hoveredState ? UIWorkspaceAccentStyle.Hover : UIWorkspaceAccentStyle.Neutral;
            palette.ApplyFrameAccent(border, accent, !selectionInteractable);
            // Other services still use a plain surface with their frame on this layer.
            if (accent == UIWorkspaceAccentStyle.Neutral
                && button != null && button.targetGraphic is Image surface
                && surface.sprite != palette.GetArtwork("item_frame")) border.enabled = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoveredState = true;
            RefreshBorderColor();
            // Pointer-enter also visits ancestors. Keep the fraction explanation
            // when entering a material card directly through its counter.
            if (eventData?.pointerEnter != null
                && eventData.pointerEnter.GetComponent<MaterialCraftingCountTooltipView>() != null) return;
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

        public void OnPointerExit(PointerEventData eventData)
        {
            hoveredState = false;
            RefreshBorderColor();
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }
    }
}
