using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Views.UI;
using Godmist.Views.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoWindowView : MonoBehaviour
    {
        private static readonly HashSet<CharacterInfoWindowView> OpenWindows = new();
        private readonly CharacterStatHoverController statHover = new();

        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI identityText;
        [SerializeField] private TextMeshProUGUI progressionText;
        [SerializeField] private TextMeshProUGUI experienceValueText;
        [SerializeField] private TextMeshProUGUI honorValueText;
        [SerializeField] private TextMeshProUGUI goldValueText;
        [SerializeField] private TextMeshProUGUI healthResourceText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI resistancesText;
        [SerializeField] private TextMeshProUGUI honorAvailabilityText;
        [SerializeField] private TMP_InputField inventorySearchInput;
        [SerializeField] private TextMeshProUGUI inventoryFooterText;
        [SerializeField] private Transform inventoryRoot;
        [SerializeField] private CharacterInfoItemView inventoryItemTemplate;
        [SerializeField] private CharacterInfoEquipmentSlotView[] equipmentSlots;
        [SerializeField] private GameObject dragGhostTemplate;
        [SerializeField] private Button skillLoadoutButton;
        [SerializeField] private SkillLoadoutModalView skillLoadoutWindowPrefab;
        [SerializeField] private Button skillTreeButton;
        [SerializeField] private SkillTreeProgressionPanelView skillTreeWindowPrefab;
        [SerializeField] private Image experienceFill;
        [SerializeField] private Image healthFill;
        private Image shieldOverlay;
        [SerializeField] private Image resourceFill;
        [SerializeField] private TMP_Text inventoryEmptyText;
        [SerializeField] private Button clearSearchButton;
        [SerializeField] private PartyRailMenuView equipmentMenuPrefab;
        [SerializeField] private ItemActionPopupView itemActionPopupPrefab;
        private ItemActionPopupView itemActionPopup;
        [SerializeField] private GameObject itemDetailsPanel;
        [SerializeField] private RectTransform inventoryScrollRect;
        [SerializeField] private TMP_Text itemDetailsTitle, itemDetailsBody, itemDetailsMeta;
        [SerializeField] private Image itemDetailsIcon;
        [SerializeField] private Button closeItemDetailsButton;
        [SerializeField] private ScrollRect itemDetailsScroll;
        private CharacterInfoEquipmentSlotView selectedEquipmentSlot;
        private EquipmentItemInstance pinnedItem;
        private PartyRailMenuView equipmentMenu;

        private readonly InventoryService inventoryService = new();
        private readonly EquipmentService equipmentService = new();
        private readonly CharacterProgressionSystem progressionSystem = new();
        private readonly LootBagService lootBagService = new();
        private readonly ConsumableService consumableService = new();
        private readonly List<CharacterInfoItemView> inventoryViews = new();

        private GameSession session;
        private CharacterInstance character;
        private CharacterInstance experienceSource;
        private TMP_Text developmentButtonLabel;
        private int displayedDevelopmentPoints = int.MinValue;
        private ItemInstance draggedItem;
        private CharacterInfoEquipmentSlotView draggedEquipmentSlot;
        private GameObject dragGhost;
        private SkillLoadoutModalView skillLoadoutWindow;
        private SkillTreeProgressionPanelView skillTreeWindow;

        public string CharacterId => character?.CharacterId;
        public Func<string, string, string> CampAction { private get; set; }

        public void TryUseCampingKit(ItemInstance item)
        {
            if (!Godmist.Systems.Dungeons.DungeonCampService.IsCampingKit(item)
                || character?.Inventory.Items.Contains(item) != true) return;
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            var reason = CampAction?.Invoke(character.CharacterId, item.InstanceId) ?? "Obóz można rozbić tylko w lochu.";
            Refresh();
            if (!string.IsNullOrEmpty(reason))
            {
                var root = window?.Manager?.WorkspaceRoot;
                if (itemActionPopup == null && root != null && itemActionPopupPrefab != null)
                    itemActionPopup = Instantiate(itemActionPopupPrefab, root);
                itemActionPopup?.ShowNotice(reason);
            }
        }
        public CharacterInstance Character => character;
        public SkillTreeProgressionPanelView DevelopmentWindowPrefab => skillTreeWindowPrefab;

        public bool MeetsEquipmentRequirements(EquipmentItemInstance item) =>
            character != null && EquipmentService.MeetsRequirements(character, item);

        private void OnEnable()
        {
            OpenWindows.Add(this);
            SubscribeToProgression();
        }

        private void OnDisable()
        {
            statHover.Clear();
            if (experienceSource != null) experienceSource.ExperienceGained -= OnExperienceGained;
            experienceSource = null;
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            itemActionPopup?.HideAll();
            equipmentMenu?.Hide();
            CloseItemDetails();
            if (draggedItem != null || draggedEquipmentSlot != null || dragGhost != null) EndDrag();
            OpenWindows.Remove(this);
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
        }

        private void Awake()
        {
            if (inventoryItemTemplate != null) inventoryItemTemplate.gameObject.SetActive(false);
            if (inventorySearchInput != null) inventorySearchInput.onValueChanged.AddListener(_ => RefreshInventory());
            if (skillLoadoutButton != null) skillLoadoutButton.onClick.AddListener(OpenSkillLoadout);
            if (skillTreeButton != null) skillTreeButton.onClick.AddListener(OpenSkillTree);
            if (clearSearchButton != null) clearSearchButton.onClick.AddListener(ClearInventorySearch);
            if (closeItemDetailsButton != null) closeItemDetailsButton.onClick.AddListener(CloseItemDetails);
            if (itemDetailsPanel != null) itemDetailsPanel.SetActive(false);

            foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
            {
                slot?.Initialize(this);
            }
        }

        private void OnDestroy()
        {
            if (itemActionPopup != null) DestroyView(itemActionPopup.gameObject);
            if (equipmentMenu != null) DestroyView(equipmentMenu.gameObject);
            if (clearSearchButton != null) clearSearchButton.onClick.RemoveListener(ClearInventorySearch);
            if (closeItemDetailsButton != null) closeItemDetailsButton.onClick.RemoveListener(CloseItemDetails);
            if (inventorySearchInput != null) inventorySearchInput.onValueChanged.RemoveAllListeners();
            if (skillLoadoutButton != null) skillLoadoutButton.onClick.RemoveListener(OpenSkillLoadout);
            if (skillTreeButton != null) skillTreeButton.onClick.RemoveListener(OpenSkillTree);
        }

        public void Initialize(GameSession nextSession, string characterId)
        {
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            itemActionPopup?.HideAll();
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            CloseItemDetails();
            session = nextSession;
            character = null;
            session?.CharactersById.TryGetValue(characterId, out character);
            SubscribeToProgression();
            Refresh();
            window?.Focus();
        }

        private void SubscribeToProgression()
        {
            if (experienceSource != null) experienceSource.ExperienceGained -= OnExperienceGained;
            experienceSource = isActiveAndEnabled ? character : null;
            if (experienceSource != null) experienceSource.ExperienceGained += OnExperienceGained;
            if (session == null || !isActiveAndEnabled) return;
            session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            session.CharacterProgressionChanged += OnCharacterProgressionChanged;
        }

        private void OnCharacterProgressionChanged(string characterId)
        {
            if (isActiveAndEnabled && character?.CharacterId == characterId) Refresh();
        }
        private void OnExperienceGained(int amount, int levels) { if (isActiveAndEnabled) Refresh(); }

        private void LateUpdate()
        {
            RefreshProgressionButtons();
            if (character != null)
            {
                RefreshVitals();
                RefreshStatTexts();
                statHover.Tick(Godmist.Views.UI.ControllerInput.PointerPosition);
            }
        }

        public void ClearInventorySearch()
        {
            if (inventorySearchInput != null) inventorySearchInput.text = string.Empty;
        }

        public void SelectEquipmentSlot(CharacterInfoEquipmentSlotView slot)
        {
            selectedEquipmentSlot = slot;
            pinnedItem = slot?.Item;
            foreach (var entry in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
                entry?.SetSelected(entry == slot);
            RefreshItemDetails();
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
        }

        public void CloseItemDetails()
        {
            selectedEquipmentSlot = null;
            pinnedItem = null;
            foreach (var entry in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>()) entry?.SetSelected(false);
            if (itemDetailsPanel != null) itemDetailsPanel.SetActive(false);
            SetInventoryDetailsInset(false);
        }

        private void SetInventoryDetailsInset(bool visible)
        {
            if (inventoryScrollRect != null)
                inventoryScrollRect.offsetMin = new Vector2(inventoryScrollRect.offsetMin.x, visible ? 406 : 86);
        }

        private ItemRarityBorderGraphic itemDetailsRarityBorder;

        private void RefreshItemDetails()
        {
            if (itemDetailsPanel == null) return;
            if (pinnedItem?.Definition == null || selectedEquipmentSlot == null
                || !ReferenceEquals(character?.Equipment?.GetItem(selectedEquipmentSlot.Slot), pinnedItem))
            { CloseItemDetails(); return; }
            itemDetailsPanel.SetActive(true);
            SetInventoryDetailsInset(true);
            itemDetailsTitle.text = ItemRarityPresentation.Name(pinnedItem);
            itemDetailsIcon.sprite = ItemIconPresentation.GetIcon(pinnedItem);
            ItemRarityPresentation.ApplyBorder(ref itemDetailsRarityBorder, itemDetailsIcon.rectTransform, pinnedItem, 0f);
            itemDetailsIcon.enabled = itemDetailsIcon.sprite != null;
            itemDetailsBody.text = UIWorkspaceTooltipView.BuildEquipmentDetails(pinnedItem, character);
            itemDetailsMeta.text = UIWorkspaceTooltipView.BuildItemMeta(pinnedItem);
            if (itemDetailsScroll != null) itemDetailsScroll.verticalNormalizedPosition = 1;
        }

        public void ShowEquipmentMenu(CharacterInfoEquipmentSlotView slot, Vector2 screenPosition)
        {
            if (slot?.Item != null) ShowItemMenu(slot.Item, screenPosition);
        }

        public void ShowItemMenu(ItemInstance item, Vector2 position)
        {
            if (session == null || !ItemOwnershipActions.Owns(character, item)) return;
            var root = window?.Manager?.WorkspaceRoot;
            if (root == null || itemActionPopupPrefab == null) return;
            if (itemActionPopup == null) itemActionPopup = Instantiate(itemActionPopupPrefab, root);
            var entries = new List<PartyRailMenuView.Entry>();
            var free = !session.IsCharacterInCombat(character.CharacterId);
            string hint = null;
            if (item is EquipmentItemInstance equipment)
            {
                var equipped = ReferenceEquals(character.Equipment?.GetItem(equipment.EquipmentSlot), equipment);
                var canStore = !equipped || inventoryService.CanAddItem(character.Inventory, item);
                if (free && !canStore) hint = "Brak miejsca w plecaku.";
                entries.Add(new PartyRailMenuView.Entry(equipped ? "Zdejmij" : "Załóż", () =>
                {
                    if (!ItemOwnershipActions.Owns(character, item) || session.IsCharacterInCombat(character.CharacterId)) return;
                    var success = equipped ? ReferenceEquals(character.Equipment.GetItem(equipment.EquipmentSlot), item)
                        && equipmentService.TryUnequip(character, equipment.EquipmentSlot)
                        : equipmentService.TryEquip(character, item.InstanceId);
                    if (success) itemActionPopup.RefreshOwners(session);
                    else itemActionPopup.ShowNotice("Nie można zmienić wyposażenia. Sprawdź wymagania i miejsce w plecaku.");
                }, free && canStore && (equipped || EquipmentService.MeetsRequirements(character, equipment))));
            }
            else if (Godmist.Systems.Dungeons.DungeonCampService.IsCampingKit(item))
                entries.Add(new PartyRailMenuView.Entry("Rozbij obóz", () => { if (ItemOwnershipActions.Owns(character, item)) TryUseCampingKit(item); }, free));
            else if (item is LootBagItemInstance)
                entries.Add(new PartyRailMenuView.Entry("Otwórz", () => { if (ItemOwnershipActions.Owns(character, item)) TryOpenLootBag(item); }, free));
            else if (item is ConsumableItemInstance consumable)
                entries.Add(new PartyRailMenuView.Entry(consumable.IsFood ? "Zjedz" : "Użyj", () => { if (ItemOwnershipActions.Owns(character, item)) TryUseConsumable(item); }, free));
            itemActionPopup.AddOwnedEntries(entries, session, character, item, null);
            itemActionPopup.ShowMenu(position, entries, item, hint);
        }

        private void RefreshVitals()
        {
            var maximum = character.MaximumHealth;
            var resource = character.ResourcePool;
            healthResourceText.text = $"<line-height=44>{UiTerms.Health}<pos=180>{CharacterStatValueLinks.Health(character)}"
                + (resource == null ? string.Empty : $"\n{CombatResourceColors.ResourceName(resource.ResourceType)}<pos=180>{CharacterStatValueLinks.Resource(character)}") + "</line-height>";
            if (healthFill != null) healthFill.fillAmount = maximum > 0 ? Mathf.Clamp01((float)character.CurrentHealth / maximum) : 0;
            CombatHealthPresentation.UpdateShieldOverlay(ref shieldOverlay, healthFill,
                character.ShieldPool?.TotalShield ?? 0, maximum);
            if (resourceFill != null)
            {
                resourceFill.gameObject.SetActive(resource != null);
                resourceFill.color = resource == null ? Color.clear : CombatResourceColors.Get(resource.ResourceType);
                resourceFill.fillAmount = resource?.MaxValue > 0 ? Mathf.Clamp01((float)resource.CurrentValue / resource.MaxValue) : 0;
            }
        }
        private static void DestroyView(GameObject node)
        {
            if (Application.isPlaying) Destroy(node);
            else DestroyImmediate(node);
        }

        private void RefreshProgressionButtons()
        {
            if (developmentButtonLabel == null && skillTreeButton != null)
                developmentButtonLabel = skillTreeButton.GetComponentInChildren<TMP_Text>(true);
            var points = Mathf.Max(0, character?.AvailableSkillPoints ?? 0);
            if (developmentButtonLabel != null && displayedDevelopmentPoints != points)
            {
                displayedDevelopmentPoints = points;
                developmentButtonLabel.text = points > 0 ? $"Rozwój (+{points})" : "Rozwój";
            }
            var visible = session != null && character != null
                && !session.IsCharacterInCombat(character.CharacterId);
            if (skillLoadoutButton != null && skillLoadoutButton.gameObject.activeSelf != visible)
                skillLoadoutButton.gameObject.SetActive(visible);
            if (skillTreeButton != null && skillTreeButton.gameObject.activeSelf != visible)
                skillTreeButton.gameObject.SetActive(visible);
        }

        public void Refresh()
        {
            RefreshProgressionButtons();
            if (character == null) return;

            if (portrait != null)
            {
                var definition = character.Definition;
                portrait.sprite = CharacterAnimationResolver.TryResolveCharacter(definition?.Id, Vector2.down,
                    CharacterAnimationState.Idle, out var idle) && idle.FrameCount > 0
                    ? idle.Frames[0]
                    : definition?.IdleAnimationFrames?.Find(frame => frame != null) ?? definition?.VisualSprite;
                portrait.preserveAspect = true;
                portrait.enabled = portrait.sprite != null;
            }

            var name = character.Definition?.DisplayName ?? character.CharacterId;
            var className = LocalizeClass(character.ClassData?.ClassName);
            if (headerTitleText != null) headerTitleText.text = name;
            identityText.text = $"Klasa: {className}";
            var experience = character.Level >= CharacterProgressionSystem.MaximumLevel
                ? "maksimum"
                : $"{character.CurrentExperience}/{progressionSystem.GetExperienceRequiredForNextLevel(character.Level)}";
            progressionText.text = experienceValueText != null ? $"Poziom {character.Level}" : character.Level >= CharacterProgressionSystem.MaximumLevel
                ? $"Poziom {character.Level}\nDoświadczenie: maksimum"
                : $"Poziom {character.Level}\nXP: {character.CurrentExperience}/{progressionSystem.GetExperienceRequiredForNextLevel(character.Level)}";
            if (experienceValueText != null) experienceValueText.text = experience;
            if (experienceFill != null) experienceFill.fillAmount = character.Level >= CharacterProgressionSystem.MaximumLevel
                ? 1 : Mathf.Clamp01((float)character.CurrentExperience / progressionSystem.GetExperienceRequiredForNextLevel(character.Level));

            RefreshVitals();
            RefreshStatTexts();
            honorAvailabilityText.text = (honorValueText == null ? $"Honor<pos=180>{character.Honor}\n" : string.Empty)
                + $"Stan: {LifeStateName(character.LifeState)}\n{AvailabilityName(character.AvailabilityState)}";
            if (honorValueText != null) honorValueText.text = character.Honor.ToString();

            RefreshInventory();
            RefreshEquipment();
        }

        private void RefreshStatTexts()
        {
            statsText.text="<line-height=22>"+CharacterStatValueLinks.Decorate(BuildStats(character),character)+"</line-height>";
            resistancesText.text="<line-height=22>"+CharacterStatValueLinks.Decorate(BuildResistances(character.Resistances),character,true)+"</line-height>";
            statHover.Bind(character,transform,UIWorkspaceTooltipView.ActiveInstance,statsText,resistancesText,healthResourceText);
        }

        public void OpenSkillLoadout()
        {
            if (session == null || character == null || session.IsCharacterInCombat(character?.CharacterId)) return;
            if (skillLoadoutWindow == null)
            {
                var manager = window?.Manager;
                if (skillLoadoutWindowPrefab == null || manager?.WorkspaceRoot == null) return;
                skillLoadoutWindow = Instantiate(skillLoadoutWindowPrefab, manager.WorkspaceRoot);
            }

            skillLoadoutWindow.Open(session, character.CharacterId);
        }

        public void OpenSkillTree()
        {
            if (session == null || character == null || session.IsCharacterInCombat(character?.CharacterId)) return;
            if (skillTreeWindow == null)
            {
                var manager = window?.Manager;
                if (skillTreeWindowPrefab == null || manager?.WorkspaceRoot == null) return;
                skillTreeWindow = Instantiate(skillTreeWindowPrefab, manager.WorkspaceRoot);
            }

            skillTreeWindow.Open(session, character.CharacterId);
        }

        public void TryActivateItem(ItemInstance item)
        {
            if (!isActiveAndEnabled || session == null || !ItemOwnershipActions.Owns(character, item)
                || session.IsCharacterInCombat(character.CharacterId)) return;
            if (item is EquipmentItemInstance equipment)
            {
                if (ReferenceEquals(character.Equipment?.GetItem(equipment.EquipmentSlot), equipment))
                    TryUnequip(equipment.EquipmentSlot);
                else TryEquip(item);
            }
            else if (Godmist.Systems.Dungeons.DungeonCampService.IsCampingKit(item)) TryUseCampingKit(item);
            else if (item is LootBagItemInstance) TryOpenLootBag(item);
            else if (item is ConsumableItemInstance) TryUseConsumable(item);
        }

        public void TryEquip(ItemInstance item)
        {
            if (item is not EquipmentItemInstance equipment || character == null
                || session?.IsCharacterInCombat(character.CharacterId) == true) return;
            if (equipmentService.TryEquip(character, equipment.InstanceId)) Refresh();
        }

        public void TryOpenLootBag(ItemInstance item)
        {
            if (item is not LootBagItemInstance lootBag
                || session == null
                || character == null)
            {
                return;
            }

            if (lootBagService.TryOpenLootBag(
                    session,
                    character.CharacterId,
                    lootBag.InstanceId,
                    out var openedItemCount))
            {
                Refresh();
                return;
            }

            Debug.LogWarning(
                $"Nie można otworzyć lootbaga '{lootBag.InstanceId}': "
                + $"zawartość={lootBag.Contents?.Count ?? 0}, "
                + $"walka={(session.IsCharacterInCombat(character?.CharacterId) ? "aktywna" : "brak")}, "
                + $"otwarte={openedItemCount}.",
                this);
        }

        public void TryUseConsumable(ItemInstance item)
        {
            if (item is not ConsumableItemInstance consumable || character == null || session == null || session.IsCharacterInCombat(character?.CharacterId)) return;
            if (!consumableService.TryUseInExploration(
                    character,
                    consumable,
                    out _,
                    returnTransition: () =>
                        Godmist.Views.Game.GameBootstrap.Instance
                            ?.TryReturnToArungardFromDungeon() ?? false,
                    passiveTicksActive: Godmist.Views.Game.GameBootstrap.Instance?.IsDungeonScene == true)) return;
            Refresh();
        }

        public void TryUnequip(EquipmentSlot slot)
        {
            if (character == null || session?.IsCharacterInCombat(character.CharacterId) == true) return;
            if (equipmentService.TryUnequip(character, slot)) Refresh();
        }

        public void BeginDrag(ItemInstance item, PointerEventData eventData)
        {
            if (item == null || character == null || session?.IsCharacterInCombat(character.CharacterId) == true) return;
            draggedItem = item;
            UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();
            draggedEquipmentSlot = null;
            if (item is EquipmentItemInstance equipment)
            {
                foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
                    slot?.SetDropHighlight(slot.Slot == equipment.EquipmentSlot);
            }

            CreateDragGhost(item, eventData);
        }

        public void BeginUnequipDrag(CharacterInfoEquipmentSlotView sourceSlot, PointerEventData eventData)
        {
            if (sourceSlot?.Item == null || character == null || session?.IsCharacterInCombat(character.CharacterId) == true) return;
            draggedItem = sourceSlot.Item;
            UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();
            draggedEquipmentSlot = sourceSlot;
            CreateDragGhost(sourceSlot.Item, eventData);
        }

        private void CreateDragGhost(ItemInstance item, PointerEventData eventData)
        {
            if (dragGhostTemplate == null) return;
            dragGhost = Instantiate(dragGhostTemplate, window.Manager.WorkspaceRoot);
            dragGhost.name = "CharacterInfoDragGhost";
            var group = dragGhost.GetComponent<CanvasGroup>() ?? dragGhost.AddComponent<CanvasGroup>();
            group.alpha = 0.9f;
            group.blocksRaycasts = false;
            var icon = dragGhost.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.enabled = icon.sprite != null;
            }
            var countLabel = dragGhost.transform.Find("CountLabel");
            if (countLabel != null) countLabel.gameObject.SetActive(false);
            UpdateDrag(eventData);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragGhost == null || window?.Manager?.WorkspaceRoot == null) return;
            var canvas = window.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(window.Manager.WorkspaceRoot, eventData.position, camera, out var point))
                dragGhost.GetComponent<RectTransform>().anchoredPosition = point;
        }

        public void EndDrag()
        {
            UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression();
            draggedItem = null;
            draggedEquipmentSlot = null;
            foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>()) slot?.SetDropHighlight(false);
            if (dragGhost != null) Destroy(dragGhost);
            dragGhost = null;
        }

        public void TryDropToCampaignStorage(ItemInstance item, PointerEventData eventData)
        {
            CampaignStorageWindowView.Active?.TryDepositAtScreenPoint(
                this,
                item,
                eventData,
                (Input.GetKey(KeyCode.LeftControl) || Godmist.Views.UI.ControllerInput.Alternate) || Input.GetKey(KeyCode.RightControl));
        }

        public void TryHandleDrop(CharacterInfoEquipmentSlotView target)
        {
            if (draggedEquipmentSlot == null
                && draggedItem is EquipmentItemInstance equipment
                && equipment.EquipmentSlot == target.Slot)
            {
                TryEquip(equipment);
            }

            EndDrag();
        }

        public void TryHandleInventoryDrop()
        {
            if (draggedEquipmentSlot != null)
            {
                TryUnequip(draggedEquipmentSlot.Slot);
            }

            EndDrag();
        }

        public void TryReceiveInventoryTransfer(
            CharacterInfoWindowView sourceWindow,
            ItemInstance item)
        {
            if (sourceWindow == null
                || sourceWindow == this
                || sourceWindow.session != session
                || sourceWindow.character?.Inventory == null
                || character?.Inventory == null
                || session?.IsCharacterInCombat(character.CharacterId) == true
                || session?.IsCharacterInCombat(sourceWindow.character.CharacterId) == true
                || item == null)
            {
                return;
            }

            if (!inventoryService.TryTransferItem(
                    sourceWindow.character.Inventory,
                    character.Inventory,
                    item.InstanceId))
            {
                return;
            }

            sourceWindow.EndDrag();
            sourceWindow.Refresh();
            Refresh();
        }

        public void TryReceiveLootItem(
            DungeonLootWindowView sourceWindow,
            ItemInstance item)
        {
            if (sourceWindow == null
                || item == null
                || session == null
                || character == null
                || session.IsCharacterInCombat(character.CharacterId))
            {
                sourceWindow?.EndDrag();
                return;
            }

            if (sourceWindow.TryClaimItemForCharacter(
                    character.CharacterId,
                    item.InstanceId))
            {
                Refresh();
            }
            else
            {
                sourceWindow.EndDrag();
            }
        }

        public static void RefreshOpenWindows(GameSession session)
        {
            if (session == null) return;

            foreach (var window in new List<CharacterInfoWindowView>(OpenWindows))
            {
                if (window != null && window.session == session) window.Refresh();
            }
        }

        private void RefreshInventory()
        {
            foreach (var view in inventoryViews) if (view != null)
            {
                view.gameObject.SetActive(false);
                DestroyView(view.gameObject);
            }
            inventoryViews.Clear();
            if (character?.Inventory?.Items == null || inventoryItemTemplate == null || inventoryRoot == null) return;

            var filter = inventorySearchInput?.text?.Trim();
            if (clearSearchButton != null) clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(filter));
            foreach (var stack in BuildItemStacks(character.Inventory.Items))
            {
                var item = stack.Representative;
                if (item?.Definition == null || (!string.IsNullOrEmpty(filter) && item.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                var view = Instantiate(inventoryItemTemplate, inventoryRoot);
                view.gameObject.SetActive(true);
                view.Bind(item, stack.Count, this);
                inventoryViews.Add(view);
            }

            var currentWeight = inventoryService.GetCurrentWeight(character.Inventory);
            var maxWeight = character.Inventory.MaxWeight;
            inventoryFooterText.text = maxWeight.HasValue
                ? $"Waga: {currentWeight:0.##}/{maxWeight.Value:0.##} kg"
                : $"Waga: {currentWeight:0.##} kg (bez limitu)";
            if (goldValueText != null) goldValueText.text = (session?.Gold ?? 0).ToString();
            else inventoryFooterText.text += $"\nZłoto drużyny: {session?.Gold ?? 0}";
            if (inventoryEmptyText != null)
            {
                inventoryEmptyText.gameObject.SetActive(inventoryViews.Count == 0);
                inventoryEmptyText.text = character.Inventory.Items.Count == 0 ? "Plecak jest pusty" : "Brak pasujących przedmiotów";
            }
        }

        private void RefreshEquipment()
        {
            foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
                slot?.Bind(character?.Equipment?.GetItem(slot.Slot));
            RefreshItemDetails();
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

            return ItemDisplayOrder.Sort(stacks, stack => stack.Representative);
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

        private static string BuildStats(CharacterInstance value)
        {
            var stats = value.Stats;
            return $"{UiTerms.Attack}<pos=180>{stats.MinAttack.GetValue():0}–{stats.MaxAttack.GetValue():0}\n{UiTerms.Stat(CharacterStatType.PhysicalDefense)}<pos=180>{stats.PhysicalDefense.GetValue():0}\n{UiTerms.Stat(CharacterStatType.MagicalDefense)}<pos=180>{stats.MagicalDefense.GetValue():0}\n{UiTerms.Stat(CharacterStatType.Dodge)}<pos=180>{stats.Dodge.GetValue():0}\n{UiTerms.Stat(CharacterStatType.CritChance)}<pos=180>{stats.CritChance.GetValue() * 100f:0.#}%\n{UiTerms.Stat(CharacterStatType.CritDamage)}<pos=180>×{stats.CritDamage.GetValue():0.##}\n{UiTerms.Stat(CharacterStatType.Speed)}<pos=180>{stats.Speed.GetValue():0}\n{UiTerms.Stat(CharacterStatType.WeaponAccuracy)}<pos=180>{stats.WeaponAccuracy.GetValue():0}";
        }

        private static string BuildResistances(ResistanceSet value)
        {
            if (value == null) return "Odporności: -";
            return $"{UiTerms.ResistanceRow(ResistanceType.Bleed)}<pos=180>{value.BleedResistance * 100f:0.#}%\n{UiTerms.ResistanceRow(ResistanceType.Poison)}<pos=180>{value.PoisonResistance * 100f:0.#}%\n{UiTerms.ResistanceRow(ResistanceType.Fire)}<pos=180>{value.FireResistance * 100f:0.#}%\n{UiTerms.ResistanceRow(ResistanceType.Weakening)}<pos=180>{value.WeakeningResistance * 100f:0.#}%\n{UiTerms.ResistanceRow(ResistanceType.Suppression)}<pos=180>{value.SuppressionResistance * 100f:0.#}%\n{UiTerms.ResistanceRow(ResistanceType.Move)}<pos=180>{value.MoveResistance * 100f:0.#}%";
        }
        private static string LocalizeClass(string name) => name switch
        { "Scout" => "Zwiadowca", "Warrior" => "Wojownik", "Mage" => "Mag", "Paladin" => "Paladyn", _ => name ?? "—" };
        private static string LifeStateName(CharacterLifeState state) => state switch
        { CharacterLifeState.Alive => "Żywy", CharacterLifeState.Downed => "Powalony", CharacterLifeState.Dead => "Martwy", _ => "Nieznany stan" };
        private static string AvailabilityName(CharacterAvailabilityState state) => state switch
        { CharacterAvailabilityState.Available => "Dostępny", CharacterAvailabilityState.Abandoned => "Opuścił drużynę",
            CharacterAvailabilityState.StoryLocked => "Niedostępny fabularnie", CharacterAvailabilityState.Recoverable => "Możliwy do odzyskania", _ => "Niedostępny" };
    }
}
