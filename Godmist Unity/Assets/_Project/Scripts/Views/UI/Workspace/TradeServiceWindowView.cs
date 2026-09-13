using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Trade content for the shared NPC services window. It intentionally owns
    /// no UIWorkspaceWindow; the parent service shell owns mode, focus and close.
    /// </summary>
    public sealed class TradeServiceWindowView : MonoBehaviour
    {
        private sealed class ItemStackEntry
        {
            public ItemInstance Representative;
            public int Count;
        }

        [Header("Trade content")]
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private RectTransform playerItemsContent;
        [SerializeField] private RectTransform traderItemsContent;
        [SerializeField] private RectTransform playerDropArea;
        [SerializeField] private RectTransform traderDropArea;
        [SerializeField] private TradeItemSlotView itemSlotTemplate;
        [SerializeField] private TextMeshProUGUI playerHeaderText;
        [SerializeField] private TextMeshProUGUI traderHeaderText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Sprite goldIconSprite;

        private readonly List<TradeItemSlotView> spawnedSlots = new();
        private GameSession session;
        private TradeService tradeService;
        private readonly InventoryService inventoryService = new();
        private NpcInstance trader;
        private CharacterInstance tradingCharacter;
        private TradeItemSlotView draggedSlot;
        private GameObject dragGhost;
        [SerializeField] private GameObject partialSaleConfirmationRoot;
        [SerializeField] private TextMeshProUGUI partialSaleText;
        [SerializeField] private Button partialSaleConfirmButton;
        [SerializeField] private Button partialSaleCancelButton;
        [SerializeField] private TextMeshProUGUI playerGoldText;
        [SerializeField] private TextMeshProUGUI traderGoldText;
        private ItemInstance pendingPartialSaleItem;
        private bool isShown;

        private bool IsShown => isShown && (serviceRoot == null || serviceRoot.activeSelf);

        private void Awake()
        {
            partialSaleConfirmButton.onClick.AddListener(ConfirmPartialSale);
            partialSaleCancelButton.onClick.AddListener(HidePartialSaleConfirmation);
            if (itemSlotTemplate != null
                && itemSlotTemplate.transform.IsChildOf(transform))
            {
                itemSlotTemplate.gameObject.SetActive(false);
            }

            // A slot template must never be rendered as part of the service
            // view. Spawned slots are created after Awake, so this only catches
            // accidental in-hierarchy template instances in older prefab data.
            foreach (var template in GetComponentsInChildren<TradeItemSlotView>(true))
            {
                if (template != itemSlotTemplate)
                    template.gameObject.SetActive(false);
            }

            if (serviceRoot != null && serviceRoot != gameObject)
                serviceRoot.SetActive(false);
        }

        private void OnDisable()
        {
            EndDrag(null, null);
            HidePartialSaleConfirmation();
            isShown = false;
        }

        public bool Show(NpcInstance nextTrader, string recipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            tradeService = GameBootstrap.Instance?.TradeService;

            if (session == null
                || tradeService == null
                || nextTrader?.Definition?.GetRole<TraderRoleData>() == null
                || nextTrader.ShopInventory == null
                || string.IsNullOrWhiteSpace(recipientCharacterId)
                || !session.CharactersById.TryGetValue(recipientCharacterId, out var buyer))
            {
                return false;
            }

            if (!HasRequiredReferences())
                return false;

            trader = nextTrader;
            tradingCharacter = buyer;
            isShown = true;
            if (serviceRoot != null && serviceRoot != gameObject)
                serviceRoot.SetActive(true);
            SetMessage("Kliknij — kup/sprzedaj  •  Przeciągnij — przenieś");
            Refresh();
            return true;
        }

        public void Hide()
        {
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            EndDrag(null, null);
            HidePartialSaleConfirmation();
            isShown = false;
            if (serviceRoot != null && serviceRoot != gameObject)
                serviceRoot.SetActive(false);
            trader = null;
            tradingCharacter = null;
            ClearSlots();
        }

        public void TryHandleClick(TradeItemSlotView slot)
        {
            if (!IsShown || slot?.Item == null) return;

            switch (slot.Source)
            {
                case TradeItemSlotSource.PlayerInventory:
                    TrySell(slot.Item);
                    break;
                case TradeItemSlotSource.TraderOffer:
                    TryBuy(slot.Item, null);
                    break;
                case TradeItemSlotSource.Buyback:
                    TryBuy(slot.Item, slot.BuybackEntry);
                    break;
            }
        }

        public void BeginDrag(TradeItemSlotView slot, PointerEventData eventData)
        {
            if (!IsShown || slot?.Item == null || eventData == null) return;

            draggedSlot = slot;
            UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();
            var workspaceRoot = ResolveWorkspaceRoot();
            if (workspaceRoot == null)
            {
                draggedSlot = null;
                UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression();
                return;
            }

            // Clone the actual UIW slot instead of creating a bare Image. This
            // preserves the slot background/border and still gives a useful
            // dark placeholder when an item has no art sprite yet.
            dragGhost = Instantiate(slot.gameObject, workspaceRoot, false);
            dragGhost.name = "TradeDragGhost";
            dragGhost.SetActive(true);
            var ghostRect = dragGhost.GetComponent<RectTransform>();
            if (ghostRect == null)
            {
                EndDrag(null, null);
                return;
            }

            ghostRect.SetAsLastSibling();
            ghostRect.anchorMin = ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.anchoredPosition = Vector2.zero;
            ghostRect.localRotation = Quaternion.identity;
            ghostRect.localScale = Vector3.one;
            ghostRect.sizeDelta = slot.GetComponent<RectTransform>()?.rect.size
                ?? new Vector2(56f, 56f);
            var ghostSlot = dragGhost.GetComponent<TradeItemSlotView>();
            if (ghostSlot != null) ghostSlot.enabled = false;
            var ghostButton = dragGhost.GetComponent<Button>();
            if (ghostButton != null) ghostButton.enabled = false;
            var canvasGroup = dragGhost.GetComponent<CanvasGroup>()
                ?? dragGhost.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.8f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            UpdateDrag(eventData);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragGhost == null || eventData == null) return;

            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var workspaceRoot = ResolveWorkspaceRoot();
            if (workspaceRoot == null) return;

            // World-space placement avoids anchor/parent-space drift when the
            // workspace root is scaled by the canvas. The ghost is still a
            // child of the workspace root, so it remains above all windows.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    workspaceRoot,
                    eventData.position,
                    camera,
                    out var worldPoint))
            {
                dragGhost.GetComponent<RectTransform>().position = worldPoint;
            }
        }

        public void EndDrag(TradeItemSlotView source, PointerEventData eventData)
        {
            if (draggedSlot != null && eventData != null && IsShown)
            {
                var target = GetDropTarget(eventData.position);
                if (target == TradeItemSlotSource.PlayerInventory
                    && draggedSlot.Source != TradeItemSlotSource.PlayerInventory)
                {
                    TryBuy(draggedSlot.Item, draggedSlot.BuybackEntry);
                }
                else if (target == TradeItemSlotSource.TraderOffer
                    && draggedSlot.Source == TradeItemSlotSource.PlayerInventory)
                {
                    TrySell(draggedSlot.Item);
                }
            }

            UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression();
            draggedSlot = null;
            if (dragGhost != null) Destroy(dragGhost);
            dragGhost = null;
        }

        private void TryBuy(ItemInstance item, BuybackEntry buybackEntry)
        {
            if (item == null || session == null || tradeService == null) return;

            var success = buybackEntry != null
                ? tradeService.TryBuyFromBuyback(
                    session,
                    trader,
                    tradingCharacter,
                    item.InstanceId,
                    out var paidGold)
                : tradeService.TryBuyFromTrader(
                    session,
                    trader,
                    tradingCharacter,
                    item.InstanceId,
                    out paidGold);

            if (!success)
            {
                var price = buybackEntry != null
                    ? tradeService.GetPriceFromBuyback(trader, buybackEntry)
                    : tradeService.GetPriceToBuyFromTrader(trader, item);
                SetMessage(BuildBuyFailureMessage(item, price));
                UIWorkspaceTooltipView.ActiveInstance?.Hide();
                Refresh();
                return;
            }

            SetMessage($"Kupiono przedmiot za {paidGold}.");
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            Refresh();
        }

        private void TrySell(ItemInstance item)
        {
            if (item == null || session == null || tradeService == null) return;

            if (!tradeService.TryGetSellQuote(
                    trader,
                    item,
                    out var normalPrice,
                    out var payableGold,
                    out var quoteFailureReason))
            {
                SetMessage(quoteFailureReason);
                UIWorkspaceTooltipView.ActiveInstance?.Hide();
                Refresh();
                return;
            }

            if (payableGold < normalPrice)
            {
                ShowPartialSaleConfirmation(item, normalPrice, payableGold);
                return;
            }

            ExecuteSell(item, acceptPartialPayment: false);
        }

        private void ExecuteSell(
            ItemInstance item,
            bool acceptPartialPayment)
        {
            if (!tradeService.TrySellToTrader(
                    session,
                    trader,
                    tradingCharacter,
                    item.InstanceId,
                    acceptPartialPayment,
                    out var receivedGold))
            {
                SetMessage(GetSellFailureReason(item));
                UIWorkspaceTooltipView.ActiveInstance?.Hide();
                Refresh();
                return;
            }

            SetMessage($"Sprzedano przedmiot za {receivedGold}. Trafił do odkupu.");
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            Refresh();
        }

        private void Refresh()
        {
            if (!IsShown) return;

            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            ClearSlots();
            if (goldText != null) goldText.gameObject.SetActive(false);
            if (playerHeaderText != null)
            {
                var playerName = tradingCharacter?.Definition?.DisplayName
                    ?? tradingCharacter?.CharacterId
                    ?? "Gracz";
                ConfigurePanelHeader(playerHeaderText, playerName, session?.Gold ?? 0);
            }
            if (traderHeaderText != null)
            {
                ConfigurePanelHeader(
                    traderHeaderText,
                    trader?.Definition?.DisplayName ?? "Handlarz",
                    trader?.GoldBalance ?? 0);
            }
            foreach (var stack in BuildItemStacks(tradingCharacter?.Inventory?.Items))
            {
                var price = tradeService.GetPriceWhenSellingToTrader(trader, stack.Representative);
                SpawnSlot(
                    playerItemsContent,
                    stack.Representative,
                    stack.Count,
                    TradeItemSlotSource.PlayerInventory,
                    BuildSellPriceTooltip(stack.Representative, price),
                    price <= 0,
                    tradingCharacter,
                    null,
                    price);
            }

            foreach (var stack in BuildItemStacks(trader?.ShopInventory?.Items))
            {
                var price = tradeService.GetPriceToBuyFromTrader(
                    trader,
                    stack.Representative);
                SpawnSlot(
                    traderItemsContent,
                    stack.Representative,
                    stack.Count,
                    TradeItemSlotSource.TraderOffer,
                    BuildBuyPriceTooltip(stack.Representative, price),
                    price <= 0 || session != null && session.Gold < price,
                    null,
                    null,
                    price);
            }

            foreach (var entry in trader?.ShopInventory?.BuybackStock ?? new List<BuybackEntry>())
            {
                if (entry?.Item?.Definition == null) continue;
                var price = tradeService.GetPriceFromBuyback(trader, entry);
                SpawnSlot(
                    traderItemsContent,
                    entry.Item,
                    1,
                    TradeItemSlotSource.Buyback,
                    BuildBuyPriceTooltip(entry.Item, price),
                    price <= 0 || session != null && session.Gold < price,
                    null,
                    entry,
                    price);
            }

            Canvas.ForceUpdateCanvases();
            ForceRebuild(playerItemsContent);
            ForceRebuild(traderItemsContent);
        }

        private void SpawnSlot(
            RectTransform content,
            ItemInstance item,
            int count,
            TradeItemSlotSource source,
            string priceLabel,
            bool unavailable,
            CharacterInstance tooltipOwner,
            BuybackEntry buybackEntry,
            int? tooltipPrice)
        {
            if (content == null || itemSlotTemplate == null || item == null) return;

            var slot = Instantiate(itemSlotTemplate, content);
            slot.gameObject.SetActive(true);
            slot.Bind(
                this,
                item,
                count,
                source,
                priceLabel,
                unavailable,
                tooltipOwner,
                buybackEntry,
                tooltipPrice);
            spawnedSlots.Add(slot);
        }

        private TradeItemSlotSource? GetDropTarget(Vector2 screenPosition)
        {
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (playerDropArea != null
                && RectTransformUtility.RectangleContainsScreenPoint(playerDropArea, screenPosition, camera))
            {
                return TradeItemSlotSource.PlayerInventory;
            }

            if (traderDropArea != null
                && RectTransformUtility.RectangleContainsScreenPoint(traderDropArea, screenPosition, camera))
            {
                return TradeItemSlotSource.TraderOffer;
            }

            return null;
        }

        private string BuildBuyPriceTooltip(ItemInstance item, int price)
        {
            var result = price <= 0
                ? "Kupno: brak poprawnej ceny."
                : string.Empty;
            if (price <= 0)
            {
                result = $"{result}\n<color=#E06C75>{GetBuyRestrictionReason(item)}</color>";
            }
            else if (session != null && session.Gold < price)
                result = "<color=#E06C75>Brak wystarczającej ilości złota.</color>";
            return result;
        }

        private string BuildSellPriceTooltip(ItemInstance item, int price)
        {
            var result = string.Empty;
            if (price <= 0)
            {
                result = $"<color=#E06C75>{GetSellFailureReason(item)}</color>";
            }
            else if (trader != null && trader.GoldBalance < price)
            {
                result = $"NPC może obecnie zapłacić: {trader.GoldBalance}";
            }

            return result;
        }

        private void ShowPartialSaleConfirmation(ItemInstance item, int normalPrice, int payableGold)
        {
            pendingPartialSaleItem = item;
            partialSaleText.text = $"{trader?.Definition?.DisplayName ?? "NPC"} ma tylko {payableGold} g.\n"
                + $"Czy na pewno chcesz sprzedać ten przedmiot za tę kwotę?\nNormalna cena: {normalPrice} g";
            partialSaleConfirmationRoot.SetActive(true);
        }

        private void ConfirmPartialSale()
        {
            var item = pendingPartialSaleItem;
            HidePartialSaleConfirmation();

            if (item != null)
            {
                ExecuteSell(item, acceptPartialPayment: true);
            }
        }

        private void HidePartialSaleConfirmation()
        {
            pendingPartialSaleItem = null;
            if (partialSaleConfirmationRoot != null) partialSaleConfirmationRoot.SetActive(false);
        }





        private void ConfigurePanelHeader(TextMeshProUGUI header, string displayName, long gold)
        {
            if (header == null) return;
            header.text = displayName ?? string.Empty;
            var amount = header == playerHeaderText ? playerGoldText : traderGoldText;
            if (amount != null) amount.text = gold.ToString();
        }



        private string GetSellFailureReason(ItemInstance item)
        {
            if (tradeService == null)
                return "Nie można sprzedać przedmiotu: brak usługi handlu.";

            return tradeService.CanSellItem(trader, item, out var reason)
                ? "Nie można sprzedać tego przedmiotu."
                : reason;
        }

        private string BuildBuyFailureMessage(ItemInstance item, int price)
        {
            if (price <= 0)
                return GetBuyRestrictionReason(item);
            if (session == null || session.Gold < price)
                return "Brakuje złota na ten zakup.";

            var inventory = tradingCharacter?.Inventory;
            if (inventory?.Items == null)
                return "Nie można kupić przedmiotu: brak inventory postaci.";
            if (inventory.Items.Any(candidate => candidate?.InstanceId == item?.InstanceId))
                return "Nie można kupić tego samego egzemplarza drugi raz.";

            if (inventory.MaxWeight.HasValue
                && !float.IsPositiveInfinity(inventory.MaxWeight.Value)
                && inventoryService.GetCurrentWeight(inventory)
                    + Mathf.Max(0f, item?.Definition?.Weight ?? 0f)
                    > inventory.MaxWeight.Value)
            {
                return "Nie można kupić przedmiotu. Sprawdź limit wagi.";
            }

            return "Nie można kupić przedmiotu.";
        }

        private string GetBuyRestrictionReason(ItemInstance item)
        {
            if (tradeService == null)
                return "Nie można kupić przedmiotu: brak usługi handlu.";

            return tradeService.CanBuyItem(trader, item, out var reason)
                ? "Nie można kupić przedmiotu: brak poprawnej ceny."
                : reason;
        }

        private bool HasRequiredReferences()
        {
            if (serviceRoot != null
                && playerItemsContent != null
                && traderItemsContent != null
                && playerDropArea != null
                && traderDropArea != null
                && itemSlotTemplate != null)
            {
                return true;
            }

            Debug.LogError(
                "TradeServiceWindowView requires its serialized prefab references. "
                + "Configure the TradeServiceRoot and both item grids in PF_UIW_NpcServicesWindow.",
                this);
            return false;
        }

        private RectTransform ResolveWorkspaceRoot()
        {
            var window = GetComponentInParent<UIWorkspaceWindow>();
            return window?.Manager?.WorkspaceRoot
                ?? GetComponentInParent<Canvas>()?.rootCanvas?.transform as RectTransform;
        }

        private void ClearSlots()
        {
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }

            spawnedSlots.Clear();
        }

        private static void ForceRebuild(RectTransform content)
        {
            if (content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void SetMessage(string message)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
        }

        private static List<ItemStackEntry> BuildItemStacks(IEnumerable<ItemInstance> items)
        {
            var stacks = new List<ItemStackEntry>();
            if (items == null) return stacks;

            foreach (var item in items)
            {
                if (item?.Definition == null) continue;
                var matching = stacks.FirstOrDefault(stack => stack.Representative.CanStackWith(item));
                if (matching == null)
                {
                    stacks.Add(new ItemStackEntry { Representative = item, Count = 1 });
                }
                else
                {
                    matching.Count++;
                }
            }

            return stacks;
        }
    }
}
