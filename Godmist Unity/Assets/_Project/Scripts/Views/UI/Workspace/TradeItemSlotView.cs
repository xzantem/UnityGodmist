using System;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Godmist.Views.UI.Workspace
{
    public enum TradeItemSlotSource
    {
        PlayerInventory,
        TraderOffer,
        Buyback
    }

    /// <summary>
    /// UIW item slot used by the trade service. The slot deliberately has no
    /// permanent transaction button: left click and drag-and-drop are the two
    /// transaction affordances (double click / Shift for a stack), while the tooltip carries the price.
    /// </summary>
    public sealed class TradeItemSlotView : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Button button;
        private ItemRarityBorderGraphic rarityBorder;
        [SerializeField] private Image icon;
        [SerializeField] private Image border;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI countText;

        [SerializeField] private Image countBackground;
        [SerializeField] private Image saleProtectionBadge;

        private TradeServiceWindowView owner;
        private string tooltipExtra;
        private CharacterInstance tooltipOwner;
        private int? tooltipPrice;

        public ItemInstance Item { get; private set; }
        public BuybackEntry BuybackEntry { get; private set; }
        public TradeItemSlotSource Source { get; private set; }

        private void Awake() => AutoWire();

        public void AutoWire()
        {
            button ??= GetComponent<Button>();
            icon ??= transform.Find("Icon")?.GetComponent<Image>();
            border ??= transform.Find("Border")?.GetComponent<Image>();
            unavailableOverlay ??= transform.Find("Overlay")?.GetComponent<Image>();
            countText ??= transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
        }

        public void Bind(
            TradeServiceWindowView nextOwner,
            ItemInstance item,
            int stackCount,
            TradeItemSlotSource source,
            string priceLabel,
            bool unavailable,
            CharacterInstance nextTooltipOwner = null,
            BuybackEntry buybackEntry = null,
            int? nextTooltipPrice = null)
        {
            AutoWire();
            owner = nextOwner;
            Item = item;
            ItemRarityPresentation.ApplyBorder(ref rarityBorder, transform as RectTransform, item);
            BuybackEntry = buybackEntry;
            Source = source;
            if (saleProtectionBadge != null) saleProtectionBadge.gameObject.SetActive(item?.IsSaleProtected == true);
            tooltipOwner = nextTooltipOwner;
            tooltipExtra = priceLabel ?? string.Empty;
            tooltipPrice = nextTooltipPrice;

            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            if (countText != null)
            {
                var showCount = item?.Definition?.IsStackable == true && stackCount > 1;
                if (countBackground != null)
                    countBackground.gameObject.SetActive(showCount);
                countText.gameObject.SetActive(showCount);
                countText.text = showCount ? stackCount.ToString() : string.Empty;
            }

            if (unavailableOverlay != null)
            {
                unavailableOverlay.gameObject.SetActive(unavailable);
                unavailableOverlay.raycastTarget = false;
            }

            if (border != null)
            {
                var palette = GetComponent<UIWorkspaceStyleApplier>()?.Palette
                    ?? GetComponentInChildren<UIWorkspaceStyleApplier>()?.Palette;
                if (palette != null)
                {
                    palette.ApplyFrameAccent(border, unavailable
                        ? UIWorkspaceAccentStyle.Negative
                        : UIWorkspaceAccentStyle.Neutral);
                }
            }

            if (button != null)
            {
                // An unaffordable item remains clickable so its failure message
                // and tooltip are still useful. The transaction service remains
                // authoritative for the final validation.
                button.interactable = item != null;
                button.onClick.RemoveAllListeners();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData?.button == PointerEventData.InputButton.Left)
            {
                owner?.TryHandleClick(this, eventData);
            }
            else if (eventData?.button == PointerEventData.InputButton.Right) owner?.ShowItemMenu(this, eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.BeginDrag(this, eventData); }

        public void OnDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.UpdateDrag(eventData); }

        public void OnEndDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.EndDrag(this, eventData); }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null)
            {
                UIWorkspaceTooltipView.ActiveInstance?.ShowItemWithExtra(
                    Item,
                    tooltipOwner,
                    this,
                    tooltipExtra,
                    tooltipPrice);
            }
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);






    }
}
