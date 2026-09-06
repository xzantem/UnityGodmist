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
    /// transaction affordances, while the tooltip carries the price.
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
        [SerializeField] private Image icon;
        [SerializeField] private Image border;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI countText;

        private Image countBackground;
        private static Sprite countBadgeSprite;

        private TradeServiceWindowView owner;
        private string tooltipExtra;
        private CharacterInstance tooltipOwner;

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
            EnsureCountBackground();
        }

        public void Bind(
            TradeServiceWindowView nextOwner,
            ItemInstance item,
            int stackCount,
            TradeItemSlotSource source,
            string priceLabel,
            bool unavailable,
            CharacterInstance nextTooltipOwner = null,
            BuybackEntry buybackEntry = null)
        {
            AutoWire();
            owner = nextOwner;
            Item = item;
            BuybackEntry = buybackEntry;
            Source = source;
            tooltipOwner = nextTooltipOwner;
            tooltipExtra = priceLabel ?? string.Empty;

            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            if (countText != null)
            {
                var showCount = item?.Definition?.IsStackable == true && stackCount > 1;
                ConfigureCountBadge();
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
                    border.color = unavailable
                        ? palette.GetAccentColor(UIWorkspaceAccentStyle.Negative)
                        : palette.GetAccentColor(UIWorkspaceAccentStyle.Neutral);
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
                owner?.TryHandleClick(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this, eventData);

        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag(this, eventData);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null)
            {
                UIWorkspaceTooltipView.ActiveInstance?.ShowItemWithExtra(
                    Item,
                    tooltipOwner,
                    this,
                    tooltipExtra);
            }
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);

        private void EnsureCountBackground()
        {
            if (countBackground != null || countText == null) return;

            var existing = transform.Find("CountBackground")?.GetComponent<Image>();
            if (existing != null)
            {
                countBackground = existing;
                return;
            }

            var backgroundObject = new GameObject(
                "CountBackground",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(transform, false);
            countBackground = backgroundObject.GetComponent<Image>();
            countBackground.raycastTarget = false;
            countBackground.sprite = GetCountBadgeSprite();
            countBackground.type = Image.Type.Sliced;
            countBackground.color = new Color(0f, 0f, 0f, .78f);

            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(1f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 0f);
            backgroundRect.pivot = new Vector2(1f, 0f);
            backgroundRect.sizeDelta = new Vector2(28f, 22f);
            backgroundRect.anchoredPosition = new Vector2(-3f, 3f);

            backgroundObject.transform.SetSiblingIndex(countText.transform.GetSiblingIndex());
        }

        private void ConfigureCountBadge()
        {
            if (countText == null) return;
            EnsureCountBackground();

            var countRect = countText.rectTransform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.sizeDelta = new Vector2(28f, 22f);
            countRect.anchoredPosition = new Vector2(-3f, 3f);
            countText.alignment = TextAlignmentOptions.Center;
            countText.verticalAlignment = VerticalAlignmentOptions.Middle;
            countText.fontStyle = FontStyles.Bold;
            countText.fontSize = Mathf.Max(16f, countText.fontSize);
            countText.raycastTarget = false;
        }

        private Sprite GetCountBadgeSprite()
        {
            if (countBadgeSprite != null) return countBadgeSprite;
            countBadgeSprite = border?.sprite;
            if (countBadgeSprite != null) return countBadgeSprite;

            countBadgeSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(.5f, .5f));
            countBadgeSprite.name = "RuntimeTradeCountBadge";
            return countBadgeSprite;
        }
    }
}
