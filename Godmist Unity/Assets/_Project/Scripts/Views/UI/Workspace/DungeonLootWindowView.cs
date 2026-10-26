using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Floating presentation of remaining dungeon loot from a body or chest.
    /// The view only forwards collection intents to LootCollectionService.
    /// </summary>
    public sealed class DungeonLootWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform itemRoot;
        [SerializeField] private DungeonLootItemView itemTemplate;
        [SerializeField] private DungeonLootGoldSlotView goldSlot;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private Button collectAllButton;

        private readonly LootCollectionService lootCollectionService = new();
        private readonly List<DungeonLootItemView> itemViews = new();

        private GameSession session;
        private string bodyInstanceId = string.Empty;
        private string chestInstanceId = string.Empty;
        private bool isChestSource;
        private GameObject dragGhost;
        private DungeonLootItemView draggedItemView;

        public static DungeonLootWindowView ActiveInstance { get; private set; }
        public static event Action<string, string> LootClaimed;
        public string BodyInstanceId => bodyInstanceId;
        public GameSession Session => session;
        public UIWorkspaceWindow Window => window;

        private void Awake()
        {
            ActiveInstance = this;
            collectAllButton?.onClick.AddListener(CollectAll);
        }

        private void Update()
        {
            if (window != null && window.IsOpen && !CanCollect())
            {
                EndDrag();
                window.Close();
                return;
            }
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (Input.GetKeyDown(KeyCode.B) && window?.IsOpen == true
                && window.Manager?.FocusedWindow == window && !DevConsoleView.IsOpen
                && (selected == null || (selected.GetComponent<TMP_InputField>() == null
                    && selected.GetComponent<InputField>() == null))) CollectAll();
        }

        private void OnDisable()
        {
            EndDrag();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            collectAllButton?.onClick.RemoveListener(CollectAll);
        }

        public static DungeonLootWindowView OpenOrReuse(
            GameSession nextSession,
            string nextBodyInstanceId,
            DungeonLootWindowView prefab = null)
        {
            if (!CanOpen(nextSession, nextBodyInstanceId)) return null;

            var manager = UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null) return null;

            var view = ActiveInstance;
            if (view == null)
            {
                view = prefab != null
                    ? Instantiate(prefab, manager.WorkspaceRoot)
                    : CreateFallback(manager.WorkspaceRoot);
            }

            view.Initialize(nextSession, nextBodyInstanceId);
            return view;
        }

        public static DungeonLootWindowView OpenChestOrReuse(
            GameSession nextSession,
            string nextChestInstanceId,
            DungeonLootWindowView prefab = null)
        {
            if (!CanOpenChest(nextSession, nextChestInstanceId)) return null;

            var manager = UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null) return null;

            var view = ActiveInstance;
            if (view == null)
            {
                view = prefab != null
                    ? Instantiate(prefab, manager.WorkspaceRoot)
                    : CreateFallback(manager.WorkspaceRoot);
            }

            view.InitializeChest(nextSession, nextChestInstanceId);
            return view;
        }

        public void Initialize(
            GameSession nextSession,
            string nextBodyInstanceId)
        {
            session = nextSession;
            bodyInstanceId = nextBodyInstanceId ?? string.Empty;
            chestInstanceId = string.Empty;
            isChestSource = false;

            if (window != null) window.Open();
            Refresh();
        }

        public void InitializeChest(
            GameSession nextSession,
            string nextChestInstanceId)
        {
            session = nextSession;
            bodyInstanceId = string.Empty;
            chestInstanceId = nextChestInstanceId ?? string.Empty;
            isChestSource = true;

            if (window != null) window.Open();
            Refresh();
        }

        public void Refresh()
        {
            if (isChestSource)
            {
                if (!TryGetChest(out var chest))
                {
                    EndDrag();
                    window?.Close();
                    return;
                }

                if (chest.Items.Count == 0 && chest.Gold <= 0)
                {
                    Close();
                    return;
                }

                RefreshItems(
                    chest.Items,
                    "Łup ze skrzyni",
                    "Skrzynia jest pusta.",
                    chest.Gold,
                    showGold: true);
                return;
            }

            if (!TryGetBody(out var body))
            {
                EndDrag();
                window?.Close();
                return;
            }

            if (body.IsEmpty)
            {
                Close();
                return;
            }

            RefreshItems(
                body.Items,
                "Łup z ciała",
                "Ciało jest puste.",
                body.Gold,
                showGold: true);
        }

        private void RefreshItems(
            IReadOnlyList<ItemInstance> items,
            string title,
            string emptyMessage,
            int gold,
            bool showGold)
        {
            if (window != null && !window.IsOpen) window.Open();
            if (titleText != null) titleText.text = title;

            ClearItemViews();

            foreach (var stack in BuildItemStacks(items))
            {
                CreateItemView(stack.Representative, stack.Count);
            }

            if (goldSlot != null)
            {
                goldSlot.Bind(gold, this);
                goldSlot.gameObject.SetActive(showGold && gold > 0);
                goldSlot.transform.SetAsFirstSibling();
            }

            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(
                    items.Count == 0 && (!showGold || gold <= 0));
                emptyText.text = emptyMessage;
            }

            window?.Focus();
            if (collectAllButton != null) collectAllButton.interactable = items.Count > 0 || gold > 0;
        }

        public void CollectAll()
        {
            if (!CanCollect()) return;
            var sourceId = isChestSource ? chestInstanceId : bodyInstanceId;
            if (lootCollectionService.ClaimAllDungeonLoot(session, sourceId, isChestSource) == 0) return;
            EndDrag();
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            LootClaimed?.Invoke(sourceId, "Zebrano łup");
            CharacterInfoWindowView.RefreshOpenWindows(session);
            Refresh();
        }

        public bool TryClaimItem(string itemInstanceId)
        {
            ItemInstance item;
            bool claimed;

            if (isChestSource)
            {
                if (!TryGetChest(out var chest)
                    || !chest.TryGetItem(itemInstanceId, out item))
                {
                    return false;
                }

                claimed = lootCollectionService.TryClaimDungeonChestItem(
                    session,
                    chestInstanceId,
                    itemInstanceId);
            }
            else
            {
                if (!TryGetBody(out var body)
                    || !body.TryGetItem(itemInstanceId, out item))
                {
                    return false;
                }

                claimed = lootCollectionService.TryClaimDungeonBodyItem(
                    session,
                    bodyInstanceId,
                    itemInstanceId);
            }

            if (!claimed) return false;

            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            LootClaimed?.Invoke(
                isChestSource ? chestInstanceId : bodyInstanceId,
                $"+ {item.Definition?.DisplayName ?? "Przedmiot"}");
            EndDrag();
            CharacterInfoWindowView.RefreshOpenWindows(session);
            Refresh();
            return true;
        }

        public bool TryClaimItemForCharacter(
            string characterId,
            string itemInstanceId)
        {
            ItemInstance item;
            bool claimed;

            if (isChestSource)
            {
                if (!TryGetChest(out var chest)
                    || !chest.TryGetItem(itemInstanceId, out item))
                {
                    return false;
                }

                claimed = lootCollectionService.TryClaimDungeonChestItem(
                    session,
                    chestInstanceId,
                    characterId,
                    itemInstanceId);
            }
            else
            {
                if (!TryGetBody(out var body)
                    || !body.TryGetItem(itemInstanceId, out item))
                {
                    return false;
                }

                claimed = lootCollectionService.TryClaimDungeonBodyItem(
                    session,
                    bodyInstanceId,
                    characterId,
                    itemInstanceId);
            }

            if (!claimed) return false;

            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            LootClaimed?.Invoke(
                isChestSource ? chestInstanceId : bodyInstanceId,
                $"+ {item.Definition?.DisplayName ?? "Przedmiot"}");
            EndDrag();
            CharacterInfoWindowView.RefreshOpenWindows(session);
            Refresh();
            return true;
        }

        public bool TryClaimGold()
        {
            if (!CanCollect()) return false;
            var claimed = isChestSource
                ? lootCollectionService.TryClaimDungeonChestGold(session, chestInstanceId,
                    out var claimedGoldFromChest)
                : lootCollectionService.TryClaimDungeonBodyGold(session, bodyInstanceId,
                    out claimedGoldFromChest);
            if (!claimed) return false;

            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            LootClaimed?.Invoke(isChestSource ? chestInstanceId : bodyInstanceId,
                $"+ {claimedGoldFromChest}");
            CharacterInfoWindowView.RefreshOpenWindows(session);
            Refresh();
            return true;
        }

        public void BeginDrag(
            DungeonLootItemView source,
            UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (source?.Item == null || !CanCollect()) return;

            draggedItemView = source;
            UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();

            if (window?.Manager?.WorkspaceRoot == null) return;

            dragGhost = new GameObject(
                "DungeonLootDragGhost",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dragGhost.transform.SetParent(
                window.Manager.WorkspaceRoot,
                false);
            dragGhost.transform.SetAsLastSibling();

            var ghostRect = dragGhost.GetComponent<RectTransform>();
            ghostRect.sizeDelta = new Vector2(56f, 56f);
            var ghostImage = dragGhost.GetComponent<Image>();
            ghostImage.sprite = ItemIconPresentation.GetIcon(source.Item);
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;
            var group = dragGhost.AddComponent<CanvasGroup>();
            group.alpha = 0.85f;
            group.blocksRaycasts = false;
            UpdateDrag(eventData);
        }

        public void UpdateDrag(
            UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (dragGhost == null
                || eventData == null
                || window?.Manager?.WorkspaceRoot == null)
            {
                return;
            }

            var canvas = window.GetComponentInParent<Canvas>();
            var camera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    window.Manager.WorkspaceRoot,
                    eventData.position,
                    camera,
                    out var point))
            {
                dragGhost.GetComponent<RectTransform>().anchoredPosition = point;
            }
        }

        public void EndDrag()
        {
            UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression();
            draggedItemView = null;

            if (dragGhost != null) Destroy(dragGhost);
            dragGhost = null;
        }

        public CharacterInstance ResolveActiveCharacter()
        {
            var id = session?.PlayerParty.ActiveLeaderId;
            return !string.IsNullOrWhiteSpace(id)
                && session.CharactersById.TryGetValue(id, out var character)
                ? character
                : null;
        }

        private void CreateItemView(ItemInstance item, int stackCount)
        {
            DungeonLootItemView view;
            if (itemTemplate != null && itemRoot != null)
            {
                view = Instantiate(itemTemplate, itemRoot);
                view.gameObject.SetActive(true);
            }
            else
            {
                var itemObject = new GameObject(
                    "LootItem",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                itemObject.transform.SetParent(itemRoot, false);
                view = itemObject.AddComponent<DungeonLootItemView>();
                BuildFallbackItemVisual(itemObject.transform);
            }

            view.Bind(item, stackCount, this);
            itemViews.Add(view);
        }

        private static void BuildFallbackItemVisual(Transform root)
        {
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(root, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.2f);
            iconRect.anchorMax = new Vector2(0.88f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0f);
            labelRect.anchorMax = new Vector2(0.95f, 0.22f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 11f;
            label.raycastTarget = false;
        }

        private void ClearItemViews()
        {
            foreach (var view in itemViews)
            {
                if (view != null)
                {
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            }

            itemViews.Clear();
        }

        private static List<ItemStackEntry> BuildItemStacks(IEnumerable<ItemInstance> items)
        {
            var stacks = new List<ItemStackEntry>();
            if (items == null) return stacks;

            foreach (var item in items)
            {
                if (item?.Definition == null) continue;

                ItemStackEntry matchingStack = null;
                foreach (var stack in stacks)
                {
                    if (stack.Representative.CanStackWith(item))
                    {
                        matchingStack = stack;
                        break;
                    }
                }

                if (matchingStack == null)
                {
                    stacks.Add(new ItemStackEntry(item));
                }
                else
                {
                    matchingStack.Count++;
                }
            }

            return stacks;
        }

        private sealed class ItemStackEntry
        {
            public ItemStackEntry(ItemInstance representative)
            {
                Representative = representative;
                Count = 1;
            }

            public ItemInstance Representative { get; }
            public int Count { get; set; }
        }

        private bool TryGetBody(out DungeonEnemyBodyInstance body)
        {
            body = null;

            return CanCollect()
                && session.ActiveDungeon != null
                && session.ActiveDungeon.TryGetCurrentFloor(out var floor)
                && floor.TryGetEnemyBody(bodyInstanceId, out body);
        }

        private bool TryGetChest(out ChestDungeonObjectInstance chest)
        {
            chest = null;

            return CanCollect()
                && session.ActiveDungeon != null
                && session.ActiveDungeon.TryGetCurrentFloor(out var floor)
                && floor.TryGetObject(chestInstanceId, out var dungeonObject)
                && dungeonObject is ChestDungeonObjectInstance currentChest
                && currentChest.IsUnlocked
                && (chest = currentChest) != null;
        }

        private bool CanCollect()
        {
            return isChestSource
                ? CanOpenChest(session, chestInstanceId)
                : CanOpen(session, bodyInstanceId);
        }

        private static bool CanOpen(
            GameSession value,
            string nextBodyInstanceId)
        {
            return value != null
                && value.CurrentMode == GameModeState.Exploration
                && value.ActiveCombat == null
                && !string.IsNullOrWhiteSpace(nextBodyInstanceId)
                && value.ActiveDungeon != null
                && value.ActiveDungeon.TryGetCurrentFloor(out var floor)
                && floor.TryGetEnemyBody(nextBodyInstanceId, out var body)
                && body != null;
        }

        private static bool CanOpenChest(
            GameSession value,
            string nextChestInstanceId)
        {
            return value != null
                && value.CurrentMode == GameModeState.Exploration
                && value.ActiveCombat == null
                && !string.IsNullOrWhiteSpace(nextChestInstanceId)
                && value.ActiveDungeon != null
                && value.ActiveDungeon.TryGetCurrentFloor(out var floor)
                && floor.TryGetObject(nextChestInstanceId, out var dungeonObject)
                && dungeonObject is ChestDungeonObjectInstance chest
                && chest.IsUnlocked;
        }

        private static DungeonLootWindowView CreateFallback(
            RectTransform workspaceRoot)
        {
            var windowObject = new GameObject(
                "DungeonLootWindow",
                typeof(RectTransform));
            windowObject.SetActive(false);
            windowObject.transform.SetParent(workspaceRoot, false);

            var rect = windowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(180f, 40f);
            rect.sizeDelta = new Vector2(420f, 360f);

            var background = windowObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);
            var window = windowObject.AddComponent<UIWorkspaceWindow>();
            var view = windowObject.AddComponent<DungeonLootWindowView>();
            view.window = window;
            view.BuildFallbackWindowVisual(windowObject.transform);
            windowObject.SetActive(true);
            return view;
        }

        private void BuildFallbackWindowVisual(Transform root)
        {
            var titleBar = CreateRect(
                root,
                "TitleBar",
                new Vector2(0f, 0.86f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            var titleBackground = titleBar.gameObject.AddComponent<Image>();
            titleBackground.color = new Color(0.13f, 0.18f, 0.26f, 1f);

            titleText = CreateText(
                titleBar,
                "Title",
                "Łup z ciała",
                TextAlignmentOptions.MidlineLeft,
                18f);
            titleText.rectTransform.anchorMin = new Vector2(0.05f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.78f, 1f);
            titleText.rectTransform.offsetMin = Vector2.zero;
            titleText.rectTransform.offsetMax = Vector2.zero;

            var closeObject = new GameObject(
                "Close",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            closeObject.transform.SetParent(titleBar, false);
            var closeRect = closeObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.86f, 0.12f);
            closeRect.anchorMax = new Vector2(0.98f, 0.88f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeLabel = CreateText(
                closeObject.transform,
                "Label",
                "×",
                TextAlignmentOptions.Center,
                24f);
            closeLabel.rectTransform.anchorMin = Vector2.zero;
            closeLabel.rectTransform.anchorMax = Vector2.one;
            closeLabel.rectTransform.offsetMin = Vector2.zero;
            closeLabel.rectTransform.offsetMax = Vector2.zero;
            closeObject.GetComponent<Button>().onClick.AddListener(Close);

            var content = CreateRect(
                root,
                "Content",
                new Vector2(0.05f, 0.18f),
                new Vector2(0.95f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            var scrollRect = content.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            var viewport = CreateRect(
                content,
                "Viewport",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var grid = CreateRect(
                viewport,
                "ItemGrid",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, -12f),
                new Vector2(-12f, 12f));
            grid.pivot = new Vector2(0.5f, 1f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(76f, 76f);
            layout.spacing = new Vector2(8f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.UpperLeft;
            var fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = grid;
            itemRoot = grid;

            var footer = CreateRect(
                root,
                "Footer",
                new Vector2(0.05f, 0.03f),
                new Vector2(0.95f, 0.15f),
                Vector2.zero,
                Vector2.zero);
            emptyText = CreateText(
                footer,
                "Empty",
                "Ciało jest puste.",
                TextAlignmentOptions.MidlineLeft,
                14f);
            emptyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            emptyText.rectTransform.anchorMax = Vector2.one;
            emptyText.rectTransform.offsetMin = Vector2.zero;
            emptyText.rectTransform.offsetMax = Vector2.zero;

            var goldObject = new GameObject(
                "GoldSlot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            goldObject.transform.SetParent(itemRoot, false);
            var goldRect = goldObject.GetComponent<RectTransform>();
            goldRect.sizeDelta = new Vector2(76f, 76f);
            goldRect.offsetMin = Vector2.zero;
            goldRect.offsetMax = Vector2.zero;
            goldSlot = goldObject.AddComponent<DungeonLootGoldSlotView>();
            var goldIcon = goldObject.GetComponent<Image>();
            var goldLabel = CreateText(
                goldObject.transform,
                "CountLabel",
                "0",
                TextAlignmentOptions.BottomRight,
                14f);
            goldLabel.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            goldLabel.rectTransform.anchorMax = Vector2.one;
            goldLabel.rectTransform.offsetMin = Vector2.zero;
            goldLabel.rectTransform.offsetMax = Vector2.zero;
            goldSlot.ConfigureRuntime(goldIcon, goldLabel);

            var allObject = new GameObject("CollectAll", typeof(RectTransform), typeof(Image), typeof(Button));
            allObject.transform.SetParent(footer, false);
            var allRect = allObject.GetComponent<RectTransform>();
            allRect.anchorMin = Vector2.zero; allRect.anchorMax = Vector2.one;
            allRect.offsetMin = Vector2.zero; allRect.offsetMax = Vector2.zero;
            var allLabel = CreateText(allObject.transform, "Label", "Zbierz wszystko [B]", TextAlignmentOptions.Center, 16f);
            allLabel.rectTransform.anchorMin = Vector2.zero; allLabel.rectTransform.anchorMax = Vector2.one;
            allLabel.rectTransform.offsetMin = Vector2.zero; allLabel.rectTransform.offsetMax = Vector2.zero;
            collectAllButton = allObject.GetComponent<Button>();
            collectAllButton.onClick.AddListener(CollectAll);
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            TextAlignmentOptions alignment,
            float fontSize)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void Close()
        {
            EndDrag();
            window?.Close();
        }
    }
}
