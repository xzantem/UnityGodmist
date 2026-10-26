using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CampaignStorageWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Transform stashItemsRoot;
        [SerializeField] private CampaignStorageItemView itemTemplate;
        [SerializeField] private GameObject dragGhostTemplate;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI itemCountText;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private Transform inventoryItemsRoot;
        [SerializeField] private TextMeshProUGUI inventoryHeaderText, inventoryCountText, inventoryEmptyText;
        [SerializeField] private Transform characterButtonsRoot;
        [SerializeField] private Button characterButtonTemplate;
        [SerializeField] private ItemActionPopupView itemActionPopupPrefab;
        [SerializeField, Min(1)] private int maximumColumns = 16;

        private readonly CampaignStorageService storage = new();
        private readonly List<CampaignStorageItemView> spawned = new();
        private readonly List<Button> characterButtons = new();
        private HashSet<string> clickStackIds;
        private bool clickFromStash;
        private Vector2 clickPosition;
        private float lastClickTime = float.NegativeInfinity;
        private readonly EquipmentService equipment = new();
        private ItemActionPopupView itemActionPopup;
        public CharacterInstance SelectedCharacter { get; private set; }
        private GameSession session;
        private CampaignStorageItemView dragged;
        private GameObject dragGhost;
        private GridLayoutGroup grid;
        private RectTransform contentRect;
        private RectTransform viewportRect;
        private bool isApplyingLayout;
        private bool snapLayoutPending;
        public static CampaignStorageWindowView Active { get; private set; }

        private void Awake()
        {
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            if (characterButtonTemplate != null) characterButtonTemplate.gameObject.SetActive(false);
            ResolveGridReferences();
        }

        public void Open(GameSession nextSession)
        {
            EndDrag(); itemActionPopup?.HideAll();
            if (session != null) session.PartyRosterChanged -= OnRosterChanged;
            session = nextSession;
            if (session == null) return;
            session.PartyRosterChanged += OnRosterChanged;
            SelectedCharacter = null;
            EnsureSelectedCharacter();
            RebuildCharacterButtons();
            ResetClickSequence();
            Active = this;
            if (titleText != null) titleText.text = "Skrytka";
            window?.Open();
            Refresh();
            ApplyResponsiveLayout();
            window?.ResizeFromBottomRight(Vector2.zero);
        }

        public void Refresh()
        {
            if (dragged != null) EndDrag();
            SetStatus(string.Empty);
            foreach (var view in spawned)
            {
                if (view == null) continue;
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(view);
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            spawned.Clear();
            EnsureSelectedCharacter();
            AddStacks(SelectedCharacter?.Inventory?.Items, inventoryItemsRoot, false);
            AddStacks(session?.Stash?.Items, stashItemsRoot, true);
            var stashCount = spawned.Count(slot => slot.IsStashItem);
            var inventoryCount = spawned.Count - stashCount;
            if (itemCountText != null) itemCountText.text = stashCount.ToString();
            if (inventoryCountText != null) inventoryCountText.text = inventoryCount.ToString();
            if (inventoryHeaderText != null) inventoryHeaderText.text = "Plecak — "
                + (SelectedCharacter?.Definition?.DisplayName ?? SelectedCharacter?.CharacterId ?? "—");
            if (inventoryEmptyText != null)
            {
                inventoryEmptyText.text = "Plecak jest pusty.";
                inventoryEmptyText.gameObject.SetActive(inventoryCount == 0);
            }
            if (emptyText != null)
            {
                emptyText.text = "Skrytka jest pusta.";
                emptyText.gameObject.SetActive(stashCount == 0);
            }
            ApplyResponsiveLayout();
        }

        private void OnDisable()
        {
            EndDrag();
            ResetClickSequence();
            itemActionPopup?.HideAll();
            if (session != null) session.PartyRosterChanged -= OnRosterChanged;
            if (Active == this) Active = null;
        }

        private void OnDestroy()
        {
            if (session != null) session.PartyRosterChanged -= OnRosterChanged;
            if (itemActionPopup != null) Destroy(itemActionPopup.gameObject);
        }

        private IEnumerable<string> MemberIds() => session == null ? Enumerable.Empty<string>()
            : session.PlayerParty.ActiveMemberIds.Concat(session.PlayerParty.BenchMemberIds).Distinct();

        private void EnsureSelectedCharacter()
        {
            if (SelectedCharacter != null && MemberIds().Contains(SelectedCharacter.CharacterId)
                && session.CharactersById.TryGetValue(SelectedCharacter.CharacterId, out var current)
                && ReferenceEquals(current, SelectedCharacter)) return;
            var leader = session?.PlayerParty.ActiveLeaderId;
            SelectedCharacter = MemberIds().OrderByDescending(id => id == leader)
                .Where(id => session.CharactersById.ContainsKey(id)).Select(id => session.CharactersById[id]).FirstOrDefault();
        }

        private void OnRosterChanged()
        {
            EnsureSelectedCharacter(); RebuildCharacterButtons(); ResetClickSequence(); itemActionPopup?.HideAll(); Refresh();
        }

        public void SelectCharacter(string id)
        {
            if (!MemberIds().Contains(id) || !session.CharactersById.TryGetValue(id, out var next)) return;
            EndDrag(); ResetClickSequence(); itemActionPopup?.HideAll();
            SelectedCharacter = next; RebuildCharacterButtons(); Refresh();
            var scroll = inventoryItemsRoot?.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        private void RebuildCharacterButtons()
        {
            foreach (var button in characterButtons)
                if (button != null) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            characterButtons.Clear();
            if (characterButtonTemplate == null || characterButtonsRoot == null) return;
            foreach (var id in MemberIds())
            {
                if (!session.CharactersById.TryGetValue(id, out var member)) continue;
                var button = Instantiate(characterButtonTemplate, characterButtonsRoot);
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = member.Definition?.UiPortraitSprite; icon.enabled = icon.sprite != null;
                var frame = button.targetGraphic as Image;
                if (frame != null && member == SelectedCharacter)
                {
                    frame.sprite = button.spriteState.pressedSprite;
                    var state = button.spriteState; state.highlightedSprite = state.pressedSprite;
                    state.selectedSprite = state.pressedSprite; button.spriteState = state;
                }
                button.name = "Character_" + id;
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => SelectCharacter(id));
                button.gameObject.SetActive(true); characterButtons.Add(button);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message;
            if (statusText.gameObject.activeSelf != !string.IsNullOrEmpty(message)) snapLayoutPending = true;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void LateUpdate()
        {
            if (!snapLayoutPending || window == null || window.IsResizing) return;
            snapLayoutPending = false;
            ApplyResponsiveLayout();
            window.ResizeFromBottomRight(Vector2.zero);
        }

        public void RefreshForSession(GameSession nextSession) { if (session == nextSession) Refresh(); }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && stashItemsRoot != null)
                ApplyResponsiveLayout();
        }

        private void ResolveGridReferences()
        {
            contentRect = stashItemsRoot as RectTransform;
            grid = contentRect?.GetComponent<GridLayoutGroup>();
            viewportRect = contentRect?.parent as RectTransform;

            if (viewportRect != null && viewportRect.GetComponent<RectMask2D>() == null)
                viewportRect.gameObject.AddComponent<RectMask2D>();
        }

        private void ApplyResponsiveLayout()
        {
            if (isApplyingLayout) return;

            ResolveGridReferences();
            if (grid == null || contentRect == null) return;

            isApplyingLayout = true;
            try
            {
                Canvas.ForceUpdateCanvases();

                if (window != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(window.RectTransform);

                if (viewportRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRect);

                var availableWidth = viewportRect != null
                    ? viewportRect.rect.width
                    : contentRect.rect.width;
                if (availableWidth <= 0f) return;

                var cellWidth = grid.cellSize.x;
                var horizontalSpacing = grid.spacing.x;
                var horizontalPadding = grid.padding.horizontal;
                if (window != null && viewportRect != null)
                {
                    var panels = inventoryItemsRoot != null ? 2 : 1;
                    var step = grid.cellSize + grid.spacing;
                    var inset = window.RectTransform.rect.size - Vector2.Scale(viewportRect.rect.size, new Vector2(panels, 1))
                        + Vector2.Scale(new Vector2(grid.padding.horizontal, grid.padding.vertical) - grid.spacing, new Vector2(panels, 1));
                    step.x *= panels;
                    window.ConfigureResizeGrid(step, inset, new Vector2Int(maximumColumns, 0));
                }
                var columns = Mathf.FloorToInt(
                    (availableWidth - horizontalPadding + horizontalSpacing + .01f)
                    / (cellWidth + horizontalSpacing));
                columns = Mathf.Clamp(columns, 1, Mathf.Max(1, maximumColumns));

                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;

                var itemCount = Mathf.Max(1, spawned.Count(slot => slot.IsStashItem));
                var rows = Mathf.CeilToInt(itemCount / (float)columns);
                var contentHeight = grid.padding.vertical
                    + rows * grid.cellSize.y
                    + Mathf.Max(0, rows - 1) * grid.spacing.y;

                contentRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(1f, contentHeight));
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                if (inventoryItemsRoot is RectTransform inventoryRect)
                {
                    var inventoryGrid = inventoryRect.GetComponent<GridLayoutGroup>();
                    inventoryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    inventoryGrid.constraintCount = columns;
                    var inventoryRows = Mathf.Max(1, Mathf.CeilToInt(spawned.Count(slot => !slot.IsStashItem) / (float)columns));
                    inventoryRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        inventoryGrid.padding.vertical + inventoryRows * inventoryGrid.cellSize.y
                        + (inventoryRows - 1) * inventoryGrid.spacing.y);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryRect);
                }
            }
            finally
            {
                isApplyingLayout = false;
            }
        }

        public void BeginDrag(CampaignStorageItemView item, PointerEventData eventData)
        {
            if (item?.Item == null) return;
            ResetClickSequence(); itemActionPopup?.HideAll(); EndDrag();
            dragged = item; UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();
            if (window?.Manager?.WorkspaceRoot == null) return;
            dragGhost = dragGhostTemplate != null
                ? Instantiate(dragGhostTemplate, window.Manager.WorkspaceRoot)
                : Instantiate(item.gameObject, window.Manager.WorkspaceRoot);
            var ghostRect = dragGhost.GetComponent<RectTransform>();
            ghostRect.anchorMin = ghostRect.anchorMax = new Vector2(.5f, .5f);
            ghostRect.pivot = new Vector2(.5f, .5f);
            ghostRect.sizeDelta = new Vector2(64f, 64f);
            var iconSprite = item.DisplayedIcon;
            if (dragGhostTemplate != null)
            {
                // PF_UIW_ItemSlot has a decorative root Image and a direct child named Icon.
                // The ghost must update that child, otherwise the decorative background replaces
                // the item icon while dragging from the stash.
                var image = dragGhost.transform.Find("Icon")?.GetComponent<Image>()
                    ?? dragGhost.GetComponent<Image>()
                    ?? dragGhost.GetComponentInChildren<Image>(true);
                if (image != null)
                {
                    image.sprite = iconSprite;
                    image.preserveAspect = true;
                    image.enabled = true;
                    image.color = image.sprite != null ? Color.white : new Color(.18f, .25f, .35f, 1f);
                }
            }
            var canvasGroup = dragGhost.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = dragGhost.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = .85f;
            var countLabel = dragGhost.transform.Find("CountLabel");
            if (countLabel != null) countLabel.gameObject.SetActive(false);
            dragGhost.SetActive(true);
            UpdateDrag(eventData);
        }
        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragGhost == null || eventData == null) return;
            var canvas = window.GetComponentInParent<Canvas>(); var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(window.Manager.WorkspaceRoot, eventData.position, camera, out var point)) dragGhost.GetComponent<RectTransform>().anchoredPosition = point;
        }
        public void EndDrag()
        {
            if (dragged != null) UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression();
            dragged = null;
            if (dragGhost != null) Destroy(dragGhost); dragGhost = null;
        }
        public void TryDrop(CampaignStorageItemView item, bool targetStash)
        {
            EndDrag();
            if (item?.Owner == this && item.IsStashItem != targetStash) TryQuickTransfer(item, ControlHeld);
        }

        private static bool ControlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private void ResetClickSequence()
        {
            clickStackIds = null; lastClickTime = float.NegativeInfinity;
        }

        public void HandleClick(CampaignStorageItemView slot, PointerEventData eventData)
        {
            if (slot?.Item == null || slot.Owner != this || eventData == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
            { ResetClickSequence(); ShowItemMenu(slot, eventData.position); return; }
            if (eventData.button != PointerEventData.InputButton.Left) return;
            HandleTransferClick(slot, eventData, ShiftHeld);
        }

        private void HandleTransferClick(CampaignStorageItemView slot, PointerEventData eventData, bool wholeStack)
        {
            // Keep a series tied to the original stack when a transfer rebuilds the grid.
            var items = slot.IsStashItem ? session?.Stash?.Items : SelectedCharacter?.Inventory?.Items;
            if (items == null || !items.Contains(slot.Item)) return;
            var continuation = clickStackIds != null && Time.unscaledTime - lastClickTime <= .35f
                && clickFromStash == slot.IsStashItem && (eventData.position - clickPosition).sqrMagnitude <= 36f;
            lastClickTime = Time.unscaledTime;
            if (continuation && !clickStackIds.Contains(slot.Item.InstanceId)) return;
            if (!continuation)
            {
                clickStackIds = new HashSet<string>(items.Where(item => item != null
                    && (ReferenceEquals(item, slot.Item) || slot.Item.CanStackWith(item))).Select(item => item.InstanceId));
                clickFromStash = slot.IsStashItem; clickPosition = eventData.position;
            }
            TryQuickTransfer(slot, !wholeStack);
        }

        public void TryQuickTransfer(CampaignStorageItemView slot, bool single)
        {
            EnsureSelectedCharacter();
            if (slot?.Owner != this || slot.Item == null || SelectedCharacter == null) return;
            if (session.IsCharacterInCombat(SelectedCharacter.CharacterId)) { SetStatus("Postać jest w walce."); return; }
            if (slot.IsStashItem) TryWithdraw(SelectedCharacter, slot.Item, single);
            else CompleteTransfer(storage.TryDeposit(session, SelectedCharacter.Inventory, slot.Item.InstanceId, single));
        }

        private void ShowItemMenu(CampaignStorageItemView slot, Vector2 position)
        {
            var root = window?.Manager?.WorkspaceRoot;
            if (itemActionPopupPrefab == null || root == null || SelectedCharacter == null) return;
            if (itemActionPopup == null) itemActionPopup = Instantiate(itemActionPopupPrefab, root);
            var item = slot.Item; var character = SelectedCharacter; var fromStash = slot.IsStashItem;
            var free = !session.IsCharacterInCombat(character.CharacterId);
            void Transfer(bool single)
            {
                if (SelectedCharacter != character || session.IsCharacterInCombat(character.CharacterId)) return;
                CompleteTransfer(fromStash ? storage.TryWithdraw(session, character.Inventory, item.InstanceId, single)
                    : storage.TryDeposit(session, character.Inventory, item.InstanceId, single));
            }
            var label = fromStash ? "Do plecaka" : "Do skrytki";
            var entries = new List<PartyRailMenuView.Entry> { new(label, () => Transfer(false), free) };
            if (item.Definition.IsStackable) entries.Add(new(label + " — 1 szt.", () => Transfer(true), free));
            if (item is EquipmentItemInstance equipmentItem)
                entries.Add(new("Załóż", () =>
                {
                    if (SelectedCharacter != character || session.IsCharacterInCombat(character.CharacterId)) return;
                    if (fromStash) TryEquipFromStash(slot);
                    else CompleteTransfer(equipment.TryEquip(character, item.InstanceId), "Nie można założyć tego wyposażenia.");
                }, free && EquipmentService.MeetsRequirements(character, equipmentItem)));
            itemActionPopup.ShowMenu(position, entries, item);
        }

        private void CompleteTransfer(bool success, string failure = "Nie można przenieść przedmiotu.")
        {
            SetStatus(success ? string.Empty : failure);
            if (!success) return;
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            CharacterInfoWindowView.RefreshOpenWindows(session); Refresh();
        }
        public void TryEquipFromStash(CampaignStorageItemView item)
        {
            if (item?.Item == null || SelectedCharacter == null || session.IsCharacterInCombat(SelectedCharacter.CharacterId)) return;
            if (storage.TryEquipFromStash(session, SelectedCharacter, item.Item.InstanceId))
            { CharacterInfoWindowView.RefreshOpenWindows(session); Refresh(); }
            else SetStatus("Nie można założyć tego wyposażenia.");
        }
        public void TryDeposit(CharacterInfoWindowView source, ItemInstance item, bool single)
        {
            var success = source?.Character != null && item != null && session != null
                && !session.IsCharacterInCombat(source.Character.CharacterId)
                && storage.TryDeposit(session, source.Character.Inventory, item.InstanceId, single);
            CompleteTransfer(success);
        }
        public bool TryDepositAtScreenPoint(CharacterInfoWindowView source, ItemInstance item, PointerEventData eventData, bool single)
        {
            ResolveGridReferences();
            // Empty space in the viewport is also a deposit target, even with no rows.
            var stashRect = viewportRect != null ? viewportRect : contentRect;
            if (stashRect == null || eventData == null) return false;
            var canvas = window?.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(stashRect, eventData.position, camera)) return false;
            if (source?.Character?.Inventory?.Items == null
                || item == null
                || !source.Character.Inventory.Items.Any(candidate => candidate?.InstanceId == item.InstanceId)) return false;
            TryDeposit(source, item, single);
            return true;
        }
        public void TryWithdraw(CharacterInstance target, ItemInstance item, bool single)
        {
            var success = target?.Inventory != null && item != null && session != null
                && !session.IsCharacterInCombat(target.CharacterId)
                && storage.TryWithdraw(session, target.Inventory, item.InstanceId, single);
            CompleteTransfer(success);
        }
        private void AddStacks(IEnumerable<ItemInstance> items, Transform root, bool isStash)
        {
            if (items == null || root == null || itemTemplate == null) return;
            var stacks = new List<List<ItemInstance>>();
            foreach (var item in items.Where(item => item?.Definition != null))
            {
                var stack = stacks.FirstOrDefault(group => group[0].CanStackWith(item));
                if (stack == null) stacks.Add(new List<ItemInstance> { item });
                else stack.Add(item);
            }
            foreach (var stack in stacks)
            {
                var view = Instantiate(itemTemplate, root); view.gameObject.SetActive(true);
                view.Bind(this, stack[0], stack.Count, isStash); spawned.Add(view);
            }
        }
    }
}
