using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Systems.Characters;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class InventoryEquipmentPanelView : MonoBehaviour
    {
        private enum SelectedItemSource
        {
            None,
            Inventory,
            Equipment,
            PendingLoot
        }

        private sealed class ItemStackEntry
        {
            public ItemInstance Representative;
            public int Count;
        }

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI selectedCharacterText;
        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI experienceText;
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("Dynamic lists")]
        [SerializeField] private Transform partyMembersContent;
        [SerializeField] private PartyMemberButtonView partyMemberButtonPrefab;
        [SerializeField] private Transform inventoryContent;
        [SerializeField] private Transform pendingLootContent;
        [SerializeField] private InventoryItemButtonView inventoryItemButtonPrefab;

        [Header("Equipment")]
        [SerializeField] private List<EquipmentSlotView> equipmentSlotViews = new();

        [Header("Actions")]
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Button claimLootButton;

        private readonly CharacterProgressionSystem progressionSystem = new();
        private readonly InventoryService inventoryService = new();
        private readonly EquipmentService equipmentService = new();
        private readonly LootCollectionService lootCollectionService = new();

        private GameSession gameSession;
        private CharacterInstance selectedCharacter;
        private ItemInstance selectedItem;
        private SelectedItemSource selectedItemSource;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            equipButton.onClick.AddListener(TryEquipSelectedItem);
            unequipButton.onClick.AddListener(TryUnequipSelectedItem);
            claimLootButton.onClick.AddListener(TryClaimSelectedLoot);

            panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void Open()
        {
            gameSession = GameBootstrap.Instance?.Session;

            if (gameSession == null) return;

            panelRoot.SetActive(true);
            selectedCharacter = GetInitialCharacter();
            selectedItem = null;
            selectedItemSource = SelectedItemSource.None;

            RefreshAll();
        }

        public void Close()
        {
            panelRoot.SetActive(false);

            selectedItem = null;
            selectedItemSource = SelectedItemSource.None;
        }

        private void SelectCharacter(CharacterInstance character)
        {
            if (character == null) return;

            selectedCharacter = character;
            selectedItem = null;
            selectedItemSource = SelectedItemSource.None;

            RefreshAll();
        }

        private void SelectInventoryItem(ItemInstance item)
        {
            selectedItem = item;
            selectedItemSource = SelectedItemSource.Inventory;

            RefreshAll();
        }

        private void SelectEquipmentItem(EquipmentItemInstance item)
        {
            selectedItem = item;
            selectedItemSource = SelectedItemSource.Equipment;

            RefreshAll();
        }

        private void SelectPendingLootItem(ItemInstance item)
        {
            selectedItem = item;
            selectedItemSource = SelectedItemSource.PendingLoot;

            RefreshAll();
        }

        private void TryEquipSelectedItem()
        {
            var equipmentItem = selectedItem as EquipmentItemInstance;

            if (selectedCharacter == null || selectedItemSource != SelectedItemSource.Inventory
                || equipmentItem == null)
            {
                SetMessage("Wybierz przedmiot wyposazalny z inventory.");
                return;
            }

            if (!equipmentService.TryEquip(selectedCharacter, equipmentItem.InstanceId))
            {
                SetMessage("Nie mozna zalozyc tego przedmiotu.");
                return;
            }

            selectedItem = selectedCharacter.Equipment.GetItem(equipmentItem.EquipmentSlot);
            selectedItemSource = SelectedItemSource.Equipment;
            SetMessage("Przedmiot zostal zalozony.");

            RefreshAll();
        }

        private void TryUnequipSelectedItem()
        {
            var equipmentItem = selectedItem as EquipmentItemInstance;

            if (selectedCharacter == null || selectedItemSource != SelectedItemSource.Equipment
                || equipmentItem == null)
            {
                SetMessage("Wybierz zalozony przedmiot.");
                return;
            }

            if (!equipmentService.TryUnequip(selectedCharacter, equipmentItem.EquipmentSlot))
            {
                SetMessage("Nie mozna zdjac przedmiotu. Brak miejsca lub limit wagi.");
                return;
            }

            selectedItem = equipmentItem;
            selectedItemSource = SelectedItemSource.Inventory;
            SetMessage("Przedmiot zostal zdjety.");

            RefreshAll();
        }

        private void TryClaimSelectedLoot()
        {
            if (gameSession == null || selectedCharacter == null
                || selectedItemSource != SelectedItemSource.PendingLoot
                || selectedItem == null)
            {
                SetMessage("Wybierz przedmiot z listy lupu.");
                return;
            }

            if (selectedCharacter.LifeState == CharacterLifeState.Dead)
            {
                SetMessage("Martwa postac nie moze odebrac lupu.");
                return;
            }

            if (!lootCollectionService.TryClaimPendingLoot(
                    gameSession,
                    selectedCharacter.CharacterId,
                    selectedItem.InstanceId))
            {
                SetMessage("Nie mozna odebrac lupu. Sprawdz limit wagi.");
                return;
            }

            selectedItem = null;
            selectedItemSource = SelectedItemSource.None;
            SetMessage("Przedmiot trafil do inventory.");

            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshPartyMembers();
            RefreshSelectedCharacterInfo();
            RefreshInventory();
            RefreshPendingLoot();
            RefreshEquipmentSlots();
            RefreshDetails();
            RefreshActionButtons();
        }

        private void RefreshPartyMembers()
        {
            ClearContent(partyMembersContent);

            if (gameSession == null) return;

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(characterId, out var character)) continue;

                var buttonView = Instantiate(partyMemberButtonPrefab, partyMembersContent);

                buttonView.Bind(
                    character,
                    ReferenceEquals(character, selectedCharacter),
                    SelectCharacter);
            }
        }

        private void RefreshSelectedCharacterInfo()
        {
            if (selectedCharacter == null)
            {
                selectedCharacterText.text = "Brak wybranej postaci";
                experienceText.text = "XP: -";
                weightText.text = "Waga: -";
                goldText.text = gameSession != null
                    ? $"Zloto: {gameSession.Gold}"
                    : "Zloto: -";

                return;
            }

            var characterName = selectedCharacter.Definition != null
                ? selectedCharacter.Definition.DisplayName
                : selectedCharacter.CharacterId;

            var className = selectedCharacter.ClassData != null
                ? selectedCharacter.ClassData.ClassName
                : "-";

            selectedCharacterText.text =
                $"{characterName} | {className} | poziom {selectedCharacter.Level}";

            experienceText.text = selectedCharacter.Level >= CharacterProgressionSystem.MaximumLevel
                ? "XP: maksymalny poziom"
                : $"XP: {selectedCharacter.CurrentExperience} / "
                + $"{progressionSystem.GetExperienceRequiredForNextLevel(selectedCharacter.Level)}";

            var currentWeight = inventoryService.GetCurrentWeight(selectedCharacter.Inventory);
            var maxWeight = selectedCharacter.Inventory.MaxWeight;

            weightText.text = maxWeight.HasValue
                ? $"Waga: {currentWeight:0.##}/{maxWeight.Value:0.##} kg"
                : $"Waga: {currentWeight:0.##} kg";

            goldText.text = $"Zloto: {gameSession?.Gold ?? 0}";
        }

        private void RefreshInventory()
        {
            ClearContent(inventoryContent);

            if (selectedCharacter?.Inventory?.Items == null) return;

            foreach (var stack in BuildItemStacks(selectedCharacter.Inventory.Items))
            {
                var buttonView = Instantiate(inventoryItemButtonPrefab, inventoryContent);

                buttonView.Bind(
                    stack.Representative,
                    stack.Count,
                    selectedItemSource == SelectedItemSource.Inventory
                    && ReferenceEquals(stack.Representative, selectedItem),
                    SelectInventoryItem);
            }
        }

        private void RefreshPendingLoot()
        {
            ClearContent(pendingLootContent);

            if (gameSession?.PendingLoot == null) return;

            foreach (var stack in BuildItemStacks(gameSession.PendingLoot))
            {
                var buttonView = Instantiate(inventoryItemButtonPrefab, pendingLootContent);

                buttonView.Bind(
                    stack.Representative,
                    stack.Count,
                    selectedItemSource == SelectedItemSource.PendingLoot
                    && ReferenceEquals(stack.Representative, selectedItem),
                    SelectPendingLootItem);
            }
        }

        private void RefreshEquipmentSlots()
        {
            foreach (var slotView in equipmentSlotViews)
            {
                if (slotView == null) continue;

                var item = selectedCharacter?.Equipment?.GetItem(slotView.Slot);

                slotView.Bind(
                    item,
                    selectedItemSource == SelectedItemSource.Equipment
                    && ReferenceEquals(item, selectedItem),
                    SelectEquipmentItem);
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
            var equipmentItem = selectedItem as EquipmentItemInstance;

            var requirements = equipmentItem == null
                ? string.Empty
                : $"\nWymagany poziom: {equipmentItem.RequiredLevel}";

            if (equipmentItem != null
                && !string.IsNullOrWhiteSpace(equipmentItem.RequiredClassId))
            {
                requirements += $"\nWymagana klasa: {equipmentItem.RequiredClassId}";
            }

            var modifiers = equipmentItem == null
                ? string.Empty
                : $"\nModyfikatory: {equipmentItem.StatModifiers.Count}";

            detailsText.text =
                $"{definition.DisplayName}\n" +
                $"{definition.ItemType} | {selectedItem.CurrentRarity} | poziom {selectedItem.ItemLevel}\n" +
                $"Waga: {definition.Weight:0.##} kg\n\n" +
                $"{definition.Description}" +
                requirements +
                modifiers;
        }

        private void RefreshActionButtons()
        {
            var equipmentItem = selectedItem as EquipmentItemInstance;

            equipButton.interactable = selectedItemSource == SelectedItemSource.Inventory
                && equipmentItem != null;

            unequipButton.interactable = selectedItemSource == SelectedItemSource.Equipment
                && equipmentItem != null;

            claimLootButton.interactable = selectedItemSource == SelectedItemSource.PendingLoot
                && selectedCharacter != null
                && selectedCharacter.LifeState != CharacterLifeState.Dead;
        }

        private CharacterInstance GetInitialCharacter()
        {
            if (gameSession == null) return null;

            var leaderId = gameSession.PlayerParty.ActiveLeaderId;

            if (!string.IsNullOrWhiteSpace(leaderId)
                && gameSession.CharactersById.TryGetValue(leaderId, out var leader))
            {
                return leader;
            }

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (gameSession.CharactersById.TryGetValue(characterId, out var character))
                {
                    return character;
                }
            }

            return null;
        }

        private static List<ItemStackEntry> BuildItemStacks(
            IEnumerable<ItemInstance> items)
        {
            var stacks = new List<ItemStackEntry>();

            if (items == null)
            {
                return stacks;
            }

            foreach (var item in items)
            {
                if (item?.Definition == null)
                {
                    continue;
                }

                ItemStackEntry matchingStack = null;

                foreach (var stack in stacks)
                {
                    if (!stack.Representative.CanStackWith(item))
                    {
                        continue;
                    }

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
