using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class TradePanelView : MonoBehaviour
    {
        private enum SelectedItemSource
        {
            None,
            PlayerInventory,
            TraderStock,
            BuybackStock
        }

        private sealed class ItemStackEntry
        {
            public ItemInstance Representative;
            public int Count;
        }

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Lists")]
        [SerializeField] private Transform playerItemsContent;
        [SerializeField] private Transform traderItemsContent;
        [SerializeField] private Transform buybackItemsContent;
        [SerializeField] private InventoryItemButtonView itemButtonPrefab;

        [Header("Actions")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;

        private GameSession gameSession;
        private TradeService tradeService;
        private NpcInstance trader;
        private CharacterInstance tradingCharacter;
        private ItemInstance selectedItem;
        private BuybackEntry selectedBuybackEntry;
        private SelectedItemSource selectedItemSource;

        private bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            buyButton.onClick.AddListener(TryBuySelectedItem);
            sellButton.onClick.AddListener(TrySellSelectedItem);

            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;

            Close();
        }

        public bool Open(NpcInstance nextTrader, string buyerCharacterId)
        {
            var bootstrap = GameBootstrap.Instance;

            gameSession = bootstrap?.Session;
            tradeService = bootstrap?.TradeService;

            if (gameSession == null
                || tradeService == null
                || nextTrader?.Definition?.GetRole<TraderRoleData>() == null
                || nextTrader.ShopInventory == null
                || gameSession.CurrentMode != GameModeState.Dialogue)
            {
                return false;
            }

            if (!gameSession.CharactersById.TryGetValue(
                    buyerCharacterId,
                    out var buyer))
            {
                return false;
            }

            if (!gameSession.TrySetMode(GameModeState.Menu))
            {
                return false;
            }

            trader = nextTrader;
            tradingCharacter = buyer;
            selectedItem = null;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.None;

            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();

            SetMessage("Wybierz przedmiot do kupna lub sprzedazy.");
            RefreshAll();

            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;

            panelRoot.SetActive(false);

            selectedItem = null;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.None;
            trader = null;
            tradingCharacter = null;

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Menu)
            {
                gameSession.TrySetMode(GameModeState.Exploration);
            }
        }

        private void SelectPlayerItem(ItemInstance item)
        {
            selectedItem = item;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.PlayerInventory;

            RefreshAll();
        }

        private void SelectTraderItem(ItemInstance item)
        {
            selectedItem = item;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.TraderStock;

            RefreshAll();
        }

        private void SelectBuybackItem(BuybackEntry entry)
        {
            selectedItem = entry?.Item;
            selectedBuybackEntry = entry;
            selectedItemSource = SelectedItemSource.BuybackStock;

            RefreshAll();
        }

        private void TryBuySelectedItem()
        {
            if (selectedItem == null
                || selectedItemSource != SelectedItemSource.TraderStock
                && selectedItemSource != SelectedItemSource.BuybackStock)
            {
                SetMessage("Wybierz przedmiot z oferty lub odkupku handlarza.");
                return;
            }

            var wasBuyback = selectedItemSource == SelectedItemSource.BuybackStock;

            var didBuy = wasBuyback
                ? tradeService.TryBuyFromBuyback(
                    gameSession,
                    trader,
                    tradingCharacter,
                    selectedItem.InstanceId,
                    out var paidGold)
                : tradeService.TryBuyFromTrader(
                    gameSession,
                    trader,
                    tradingCharacter,
                    selectedItem.InstanceId,
                    out paidGold);

            if (!didBuy)
            {
                SetMessage("Nie mozna kupic przedmiotu. Sprawdz zloto i limit wagi.");
                return;
            }

            selectedItem = null;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.None;

            var sourceLabel = wasBuyback ? "z odkupku" : "z oferty";
            SetMessage($"Kupiono przedmiot {sourceLabel} za {paidGold} zl.");

            RefreshAll();
        }

        private void TrySellSelectedItem()
        {
            if (selectedItemSource != SelectedItemSource.PlayerInventory
                || selectedItem == null)
            {
                SetMessage("Wybierz przedmiot z inventory postaci.");
                return;
            }

            if (!tradeService.TrySellToTrader(
                    gameSession,
                    trader,
                    tradingCharacter,
                    selectedItem.InstanceId,
                    out var receivedGold))
            {
                SetMessage("Nie mozna sprzedac tego przedmiotu.");
                return;
            }

            selectedItem = null;
            selectedBuybackEntry = null;
            selectedItemSource = SelectedItemSource.None;

            SetMessage(
                $"Sprzedano przedmiot za {receivedGold} zl. " +
                "Jest dostepny w odkupku handlarza.");

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (!IsOpen) return;

            RefreshHeader();
            RefreshPlayerItems();
            RefreshTraderItems();
            RefreshBuybackItems();
            RefreshDetails();
            RefreshActionButtons();
        }

        private void RefreshHeader()
        {
            var traderName = trader?.Definition != null
                ? trader.Definition.DisplayName
                : trader?.NpcId ?? "-";

            titleText.text = $"Handel - {traderName}";
            goldText.text = $"Zloto: {gameSession?.Gold ?? 0}";
        }

        private void RefreshPlayerItems()
        {
            ClearContent(playerItemsContent);

            if (tradingCharacter?.Inventory?.Items == null) return;

            foreach (var stack in BuildItemStacks(tradingCharacter.Inventory.Items))
            {
                var sellPrice = tradeService.GetPriceWhenSellingToTrader(
                    trader,
                    stack.Representative);

                var buttonView = Instantiate(itemButtonPrefab, playerItemsContent);

                buttonView.Bind(
                    stack.Representative,
                    stack.Count,
                    selectedItemSource == SelectedItemSource.PlayerInventory
                    && ReferenceEquals(selectedItem, stack.Representative),
                    SelectPlayerItem,
                    $"Sprzedaz: {sellPrice} zl");
            }
        }

        private void RefreshTraderItems()
        {
            ClearContent(traderItemsContent);

            if (trader?.ShopInventory?.Items == null) return;

            foreach (var stack in BuildItemStacks(trader.ShopInventory.Items))
            {
                var buyPrice = tradeService.GetPriceToBuyFromTrader(
                    stack.Representative);

                var buttonView = Instantiate(itemButtonPrefab, traderItemsContent);

                buttonView.Bind(
                    stack.Representative,
                    stack.Count,
                    selectedItemSource == SelectedItemSource.TraderStock
                    && ReferenceEquals(selectedItem, stack.Representative),
                    SelectTraderItem,
                    $"Kupno: {buyPrice} zl");
            }
        }

        private void RefreshBuybackItems()
        {
            ClearContent(buybackItemsContent);

            if (trader?.ShopInventory?.BuybackStock == null) return;

            foreach (var entry in trader.ShopInventory.BuybackStock)
            {
                if (entry?.Item?.Definition == null) continue;

                var buybackPrice = tradeService.GetPriceFromBuyback(entry);
                var buttonView = Instantiate(itemButtonPrefab, buybackItemsContent);

                buttonView.Bind(
                    entry.Item,
                    1,
                    selectedItemSource == SelectedItemSource.BuybackStock
                    && ReferenceEquals(selectedBuybackEntry, entry),
                    _ => SelectBuybackItem(entry),
                    $"Odkup: {buybackPrice} zl");
            }
        }

        private void RefreshDetails()
        {
            if (selectedItem?.Definition == null)
            {
                detailsText.text = "Wybierz przedmiot, aby zobaczyc szczegoly.";
                return;
            }

            var definition = selectedItem.Definition;
            var price = GetSelectedBuyOrSellPrice(out var priceLabel);

            detailsText.text =
                $"{definition.DisplayName}\n" +
                $"{definition.ItemType} | {selectedItem.CurrentRarity} | poziom {selectedItem.ItemLevel}\n" +
                $"Waga: {definition.Weight:0.##} kg\n" +
                $"{priceLabel}: {price} zl\n\n" +
                definition.Description;
        }

        private void RefreshActionButtons()
        {
            var buyPrice = selectedItemSource == SelectedItemSource.TraderStock
                || selectedItemSource == SelectedItemSource.BuybackStock
                ? GetSelectedBuyOrSellPrice(out _)
                : 0;

            var sellPrice = selectedItemSource == SelectedItemSource.PlayerInventory
                ? tradeService.GetPriceWhenSellingToTrader(trader, selectedItem)
                : 0;

            buyButton.interactable = buyPrice > 0
                && gameSession != null
                && gameSession.Gold >= buyPrice;

            sellButton.interactable = sellPrice > 0;
        }

        private int GetSelectedBuyOrSellPrice(out string priceLabel)
        {
            switch (selectedItemSource)
            {
                case SelectedItemSource.TraderStock:
                    priceLabel = "Cena zakupu";
                    return tradeService.GetPriceToBuyFromTrader(selectedItem);

                case SelectedItemSource.BuybackStock:
                    priceLabel = "Cena odkupku";
                    return tradeService.GetPriceFromBuyback(selectedBuybackEntry);

                case SelectedItemSource.PlayerInventory:
                    priceLabel = "Cena sprzedazy";
                    return tradeService.GetPriceWhenSellingToTrader(
                        trader,
                        selectedItem);

                default:
                    priceLabel = "Cena";
                    return 0;
            }
        }

        private static List<ItemStackEntry> BuildItemStacks(
            IEnumerable<ItemInstance> items)
        {
            var stacks = new List<ItemStackEntry>();

            if (items == null) return stacks;

            foreach (var item in items)
            {
                if (item?.Definition == null) continue;

                ItemStackEntry matchingStack = null;

                foreach (var stack in stacks)
                {
                    if (!stack.Representative.CanStackWith(item)) continue;

                    matchingStack = stack;
                    break;
                }

                if (matchingStack == null)
                {
                    stacks.Add(new ItemStackEntry
                    {
                        Representative = item,
                        Count = 1
                    });

                    continue;
                }

                matchingStack.Count++;
            }

            return stacks;
        }

        private static void ClearContent(Transform content)
        {
            if (content == null) return;

            for (var index = content.childCount - 1; index >= 0; index--)
            {
                Destroy(content.GetChild(index).gameObject);
            }
        }

        private void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }
    }
}