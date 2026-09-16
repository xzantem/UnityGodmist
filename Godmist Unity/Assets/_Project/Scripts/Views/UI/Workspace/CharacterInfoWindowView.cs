using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Views.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoWindowView : MonoBehaviour
    {
        private static readonly HashSet<CharacterInfoWindowView> OpenWindows = new();

        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI headerTitleText;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI identityText;
        [SerializeField] private TextMeshProUGUI progressionText;
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

        private readonly InventoryService inventoryService = new();
        private readonly EquipmentService equipmentService = new();
        private readonly CharacterProgressionSystem progressionSystem = new();
        private readonly LootBagService lootBagService = new();
        private readonly ConsumableService consumableService = new();
        private readonly List<CharacterInfoItemView> inventoryViews = new();

        private GameSession session;
        private CharacterInstance character;
        private ItemInstance draggedItem;
        private CharacterInfoEquipmentSlotView draggedEquipmentSlot;
        private GameObject dragGhost;
        private SkillLoadoutModalView skillLoadoutWindow;
        private SkillTreeProgressionPanelView skillTreeWindow;

        public string CharacterId => character?.CharacterId;
        public CharacterInstance Character => character;

        public bool MeetsEquipmentRequirements(EquipmentItemInstance item) =>
            character != null && EquipmentService.MeetsRequirements(character, item);

        private void OnEnable()
        {
            OpenWindows.Add(this);
        }

        private void OnDisable()
        {
            OpenWindows.Remove(this);
        }

        private void Awake()
        {
            if (inventoryItemTemplate != null) inventoryItemTemplate.gameObject.SetActive(false);
            if (inventorySearchInput != null) inventorySearchInput.onValueChanged.AddListener(_ => RefreshInventory());
            if (skillLoadoutButton != null) skillLoadoutButton.onClick.AddListener(OpenSkillLoadout);
            if (skillTreeButton != null) skillTreeButton.onClick.AddListener(OpenSkillTree);

            foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
            {
                slot?.Initialize(this);
            }
        }

        private void OnDestroy()
        {
            if (inventorySearchInput != null) inventorySearchInput.onValueChanged.RemoveAllListeners();
            if (skillLoadoutButton != null) skillLoadoutButton.onClick.RemoveListener(OpenSkillLoadout);
            if (skillTreeButton != null) skillTreeButton.onClick.RemoveListener(OpenSkillTree);
        }

        public void Initialize(GameSession nextSession, string characterId)
        {
            session = nextSession;
            session?.CharactersById.TryGetValue(characterId, out character);
            Refresh();
            window?.Focus();
        }

        private void LateUpdate() => RefreshProgressionButtons();

        private void RefreshProgressionButtons()
        {
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
                portrait.sprite = character.Definition?.VisualSprite;
                portrait.enabled = portrait.sprite != null;
            }

            var name = character.Definition?.DisplayName ?? character.CharacterId;
            var className = character.ClassData?.ClassName ?? "-";
            if (headerTitleText != null) headerTitleText.text = name;
            identityText.text = $"Klasa: {className}";
            progressionText.text = character.Level >= CharacterProgressionSystem.MaximumLevel
                ? $"Poziom {character.Level} | XP: maks."
                : $"Poziom {character.Level} | XP: {character.CurrentExperience}/{progressionSystem.GetExperienceRequiredForNextLevel(character.Level)}";

            var maxHealth = Mathf.RoundToInt(character.Stats.MaxHealth.GetValue());
            var resource = character.ResourcePool;
            healthResourceText.text = resource == null
                ? $"HP: {character.CurrentHealth}/{maxHealth}"
                : $"HP: {character.CurrentHealth}/{maxHealth}\n{resource.ResourceType}: {resource.CurrentValue}/{resource.MaxValue}";
            statsText.text = BuildStats(character);
            resistancesText.text = BuildResistances(character.Resistances);
            honorAvailabilityText.text = $"Honor: {character.Honor}\nStan: {character.LifeState} | {character.AvailabilityState}";

            RefreshInventory();
            RefreshEquipment();
        }

        private void OpenSkillLoadout()
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

        private void OpenSkillTree()
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
                            ?.TryReturnToArungardFromDungeon() ?? false)) return;
            Refresh();
        }

        public void TryUnequip(EquipmentSlot slot)
        {
            if (character == null || session?.IsCharacterInCombat(character.CharacterId) == true) return;
            if (equipmentService.TryUnequip(character, slot)) Refresh();
        }

        public void BeginDrag(ItemInstance item, PointerEventData eventData)
        {
            if (item == null || character == null) return;
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
            if (sourceSlot?.Item == null || character == null) return;
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
                icon.sprite = item.Definition?.Icon;
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
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
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
                || character == null)
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
            foreach (var view in inventoryViews) if (view != null) Destroy(view.gameObject);
            inventoryViews.Clear();
            if (character?.Inventory?.Items == null || inventoryItemTemplate == null || inventoryRoot == null) return;

            var filter = inventorySearchInput?.text?.Trim();
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
            inventoryFooterText.text = $"Waga: {currentWeight:0.##}/{(maxWeight.HasValue ? maxWeight.Value.ToString("0.##") : "∞")} kg | Złoto: {session?.Gold ?? 0}";
        }

        private void RefreshEquipment()
        {
            foreach (var slot in equipmentSlots ?? Array.Empty<CharacterInfoEquipmentSlotView>())
                slot?.Bind(character?.Equipment?.GetItem(slot.Slot));
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

        private static string BuildStats(CharacterInstance value)
        {
            var stats = value.Stats;
            return $"Atak: {stats.MinAttack.GetValue():0}-{stats.MaxAttack.GetValue():0}\nObrona fiz.: {stats.PhysicalDefense.GetValue():0}\nObrona mag.: {stats.MagicalDefense.GetValue():0}\nUnik: {stats.Dodge.GetValue():0} | Kryt: {stats.CritChance.GetValue() * 100f:0.#}%\nObr. kryt.: {stats.CritDamage.GetValue():0.00} | Szybkość: {stats.Speed.GetValue():0}\nCelność: {stats.WeaponAccuracy.GetValue():0}";
        }

        private static string BuildResistances(ResistanceSet value)
        {
            if (value == null) return "Odporności: -";
            return $"Krwawienie: {value.BleedResistance * 100f:0.#}% | Trucizna: {value.PoisonResistance * 100f:0.#}%\nOgień: {value.FireResistance * 100f:0.#}% | Osłabienie: {value.WeakeningResistance * 100f:0.#}%\nEOR: {value.SuppressionResistance * 100f:0.#}% | Ruch: {value.MoveResistance * 100f:0.#}%";
        }
    }
}
