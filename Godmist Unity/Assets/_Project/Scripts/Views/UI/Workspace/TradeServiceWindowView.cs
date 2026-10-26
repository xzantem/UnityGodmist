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
        [SerializeField] private ItemActionPopupView itemActionPopupPrefab;
        private ItemActionPopupView itemActionPopup;
        private readonly TradeClickSequence clickSequence = new();

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
        private bool isShown;

        private bool IsShown => isShown && (serviceRoot == null || serviceRoot.activeSelf);

        private void Awake()
        {
            HidePartialSaleConfirmation();
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
            itemActionPopup?.HideAll(); clickSequence.Reset();
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
            itemActionPopup?.HideAll(); clickSequence.Reset();
            tradingCharacter = buyer;
            isShown = true;
            if (serviceRoot != null && serviceRoot != gameObject)
                serviceRoot.SetActive(true);
            SetMessage(null);
            Refresh();
            return true;
        }

        public void Hide()
        {
            itemActionPopup?.HideAll(); clickSequence.Reset();
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

        public void TryHandleClick(TradeItemSlotView slot, PointerEventData eventData)
        {
            if (!IsShown || slot?.Item == null || itemActionPopup?.IsModalOpen == true) return;
            var ids = CaptureStack(slot.Item, slot.Source);
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            { clickSequence.Reset(); Transact(slot.Source, ids); }
            else if (clickSequence.Click(slot.Source, slot.Item.InstanceId, ids, eventData.position, Time.unscaledTime))
                Transact(slot.Source, ids.Take(1).ToArray());
        }

        private ItemActionPopupView Popup
        {
            get
            {
                if (itemActionPopup == null && itemActionPopupPrefab != null && ResolveWorkspaceRoot() != null)
                    itemActionPopup = Instantiate(itemActionPopupPrefab, ResolveWorkspaceRoot());
                return itemActionPopup;
            }
        }

        private void OnDestroy() { if (itemActionPopup != null) Destroy(itemActionPopup.gameObject); }

        private List<string> CaptureStack(ItemInstance item, TradeItemSlotSource source)
        {
            var items = source == TradeItemSlotSource.PlayerInventory ? tradingCharacter.Inventory.Items
                : source == TradeItemSlotSource.TraderOffer ? trader.ShopInventory.Items
                : trader.ShopInventory.BuybackStock.Select(entry => entry.Item);
            // Keep the first unit as the displayed stack's ordering anchor until it is exhausted.
            // Restocks can append matching units after other types in the source inventory.
            return items.Where(candidate => ReferenceEquals(candidate, item) || item.CanStackWith(candidate)).Reverse()
                .Select(candidate => candidate.InstanceId).ToList();
        }

        public void ShowItemMenu(TradeItemSlotView slot, Vector2 position)
        {
            if (!IsShown || slot?.Item == null || Popup == null) return;
            clickSequence.Reset();
            var item = slot.Item; var source = slot.Source;
            var ids = CaptureStack(item, source);
            if (ids.Count == 0) return;
            var sell = source == TradeItemSlotSource.PlayerInventory;
            var verb = sell ? "Sprzedaj" : "Kup";
            var allowed = !sell || tradeService.CanSellItem(trader, item, out _);
            var entries = new List<PartyRailMenuView.Entry>
            {
                new($"{verb} 1", () => Transact(source, ids.Take(1).ToArray()), allowed),
                new($"{verb} X…", () => Popup.RequestQuantity($"{verb} przedmioty", ids.Count,
                    count => item.DisplayName + "\n" + DescribeTransaction(source, ids.Take(count).ToArray()),
                    count => Transact(source, ids.Take(count).ToArray(), fromQuantity: true)), allowed && ids.Count > 1),
                new($"{verb} cały stos ({ids.Count})", () => Transact(source, ids), allowed && ids.Count > 1)
            };
            if (sell) Popup.AddOwnedEntries(entries, session, tradingCharacter, item, null);
            else Popup.AddCompare(entries, tradingCharacter, item);
            Popup.ShowMenu(position, entries, item);
        }

        private string DescribeTransaction(TradeItemSlotSource source, IReadOnlyList<string> ids)
        {
            if (source == TradeItemSlotSource.PlayerInventory)
                return tradeService.TryGetSellBatchQuote(trader, tradingCharacter, ids, out var price, out var payable, out var reason)
                    ? $"{ids.Count} szt. • {price} złota" + (payable < price ? $"\nKupiec może zapłacić tylko {payable} złota." : "") : reason;
            long total = 0;
            foreach (var id in ids)
            {
                var item = source == TradeItemSlotSource.Buyback
                    ? trader.ShopInventory.BuybackStock.FirstOrDefault(entry => entry.Item.InstanceId == id)?.Item
                    : trader.ShopInventory.Items.FirstOrDefault(candidate => candidate.InstanceId == id);
                if (item == null) return "Przedmiot nie jest już dostępny.";
                total += tradeService.GetPriceToBuyFromTrader(trader, item);
            }
            return $"{ids.Count} szt. • {total} złota\nPosiadane złoto: {session.Gold}";
        }

        private string Transact(TradeItemSlotSource source, IReadOnlyList<string> ids, bool fromQuantity = false)
        {
            if (!IsShown) return "Okno handlu zostało zamknięte.";
            if (source == TradeItemSlotSource.PlayerInventory)
            {
                if (!tradeService.TryGetSellBatchQuote(trader, tradingCharacter, ids, out var normal, out var payable, out var quoteReason))
                { SetMessage(quoteReason); return quoteReason; }
                if (payable < normal)
                {
                    ShowBatchPartialConfirmation(ids.ToArray(), normal, payable);
                    // Retain the newly opened confirmation instead of closing it as the quantity callback returns.
                    return fromQuantity ? "Potwierdź poniżej sprzedaż za mniejszą kwotę." : null;
                }
                return ExecuteBatchSell(ids, false);
            }
            var success = tradeService.TryBuyBatch(session, trader, tradingCharacter, ids,
                source == TradeItemSlotSource.Buyback, out _, out var reason);
            SetMessage(success ? null : reason);
            if (success) RefreshAfterTransaction();
            return success ? null : reason;
        }

        private string ExecuteBatchSell(IReadOnlyList<string> ids, bool partial)
        {
            var success = tradeService.TrySellBatch(session, trader, tradingCharacter, ids, partial, out _, out var reason);
            SetMessage(success ? null : reason);
            if (success) RefreshAfterTransaction();
            return success ? null : reason;
        }

        private void ShowBatchPartialConfirmation(string[] ids, int normal, int payable)
        {
            clickSequence.Reset();
            Popup.RequestConfirmation("Kupiec ma za mało złota",
                $"Sprzedaż {ids.Length} szt. za {payable} złota.\nNormalna cena: {normal} złota.\nCzy chcesz sprzedać za mniejszą kwotę?", () =>
                {
                    if (!IsShown) return "Okno handlu zostało zamknięte.";
                    if (!tradeService.TryGetSellBatchQuote(trader, tradingCharacter, ids, out var nextNormal, out var nextPayable, out var reason)) return reason;
                    if (nextNormal != normal || nextPayable != payable)
                    { ShowBatchPartialConfirmation(ids, nextNormal, nextPayable); return "Kwota się zmieniła. Potwierdź aktualną ofertę."; }
                    return ExecuteBatchSell(ids, true);
                });
        }

        private void RefreshAfterTransaction()
        {
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            CharacterInfoWindowView.RefreshOpenWindows(session);
            Refresh();
        }

        public void BeginDrag(TradeItemSlotView slot, PointerEventData eventData)
        {
            if (!IsShown || slot?.Item == null || eventData == null) return;
            clickSequence.Reset();

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

        private void TryBuy(ItemInstance item, BuybackEntry entry)
        {
            var source = entry == null ? TradeItemSlotSource.TraderOffer : TradeItemSlotSource.Buyback;
            if (item != null) Transact(source, CaptureStack(item, source).Take(1).ToArray());
        }

        private void TrySell(ItemInstance item)
        {
            if (item != null) Transact(TradeItemSlotSource.PlayerInventory,
                CaptureStack(item, TradeItemSlotSource.PlayerInventory).Take(1).ToArray());
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
                    stack.Representative.IsSaleProtected || price <= 0 && !TradeService.IsFreeStarter(stack.Representative),
                    tradingCharacter,
                    null,
                    stack.Representative.IsSaleProtected ? (int?)null : price);
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

            foreach (var stack in BuildItemStacks(trader?.ShopInventory?.BuybackStock.Select(entry => entry.Item)))
            {
                var entry = trader.ShopInventory.BuybackStock.First(value => value.Item == stack.Representative);
                var price = tradeService.GetPriceFromBuyback(trader, entry);
                SpawnSlot(
                    traderItemsContent,
                    entry.Item,
                    stack.Count,
                    TradeItemSlotSource.Buyback,
                    BuildBuyPriceTooltip(entry.Item, price),
                    price <= 0 && !TradeService.IsFreeStarter(entry.Item) || session != null && session.Gold < price,
                    null,
                    entry,
                    price);
            }

            Canvas.ForceUpdateCanvases();
            ForceRebuild(playerItemsContent);
            ForceRebuild(traderItemsContent);
        }

        public void RefreshForSession(GameSession nextSession) { if (session == nextSession && IsShown) Refresh(); }

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
            if (TradeService.IsFreeStarter(item)) return "Odkup wyposażenia startowego: 0 złota.";
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
            if (item.IsSaleProtected) return string.Empty; // The shared item footer already displays protection.
            if (TradeService.IsFreeStarter(item)) return "Wyposażenie startowe — sprzedaż za 0 złota.";
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

        private void HidePartialSaleConfirmation()
        {
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
            if (price <= 0 && !TradeService.IsFreeStarter(item))
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
                if (slot != null) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
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
            if (messageText == null) return;
            messageText.text = message ?? string.Empty;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
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
