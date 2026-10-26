using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Game;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class PotionRefillWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform recipeGrid;
        [SerializeField] private Transform materialsGrid;
        [SerializeField] private BlacksmithCraftingSlotView recipeSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView materialSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI mediumEmptyText;
        [SerializeField] private TextMeshProUGUI consumableCountText;
        [SerializeField] private TextMeshProUGUI consumableEmptyText;
        [SerializeField] private TextMeshProUGUI previewEffectsText;
        [SerializeField] private ScrollRect consumableScroll;
        [SerializeField] private GameObject previewSection;
        [SerializeField] private GameObject effectsSection;
        [SerializeField] private GameObject mediumSection;
        [SerializeField] private GameObject emptySummarySpacer;
        [SerializeField] private Button craftButton;
        [SerializeField] private ConsumableUseType refillType = ConsumableUseType.Potion;

        private readonly PotionRefillService refillService = new();
        private GameSession session;
        private NpcInstance alchemist;
        private string recipientCharacterId = string.Empty;
        private ConsumableItemInstance selectedConsumable;
        private ItemDefinition selectedMedium;
        private readonly List<BlacksmithCraftingSlotView> consumableSlots = new();
        private readonly List<BlacksmithCraftingSlotView> mediumSlots = new();
        private bool isVisible;

        private void Awake()
        {
            HideTemplates();
            craftButton?.onClick.AddListener(TryRefill);
        }

        private void OnDestroy() => craftButton?.onClick.RemoveListener(TryRefill);

        private void OnDisable()
        {
            isVisible = false;
            ClearRuntimeSlots();
        }

        public bool Show(NpcInstance nextAlchemist, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextAlchemist == null || string.IsNullOrWhiteSpace(nextRecipientCharacterId))
                return false;

            alchemist = nextAlchemist;
            recipientCharacterId = nextRecipientCharacterId;
            selectedConsumable = null;
            selectedMedium = null;
            isVisible = true;
            ClearRuntimeSlots();
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateConsumables();
            if (consumableScroll != null) consumableScroll.verticalNormalizedPosition = 1;
            RefreshPreview();
            return true;
        }

        public void Hide()
        {
            isVisible = false;
            ClearRuntimeSlots();
            serviceRoot?.SetActive(false);
        }

        private void PopulateConsumables()
        {
            var targetGrid = GetRecipeGrid();
            ClearSlots(consumableSlots);
            var consumables = refillService.GetPartyConsumables(session, refillType)
                .Where(consumable => consumable.CurrentCharges < consumable.MaxCharges)
                .GroupBy(consumable => consumable.InstanceId)
                .Select(group => group.First())
                .ToList();
            if (!consumables.Contains(selectedConsumable)) selectedConsumable = consumables.FirstOrDefault();
            if (selectedConsumable == null) selectedMedium = null;
            if (consumableCountText != null) consumableCountText.text = consumables.Count.ToString();
            if (consumableEmptyText != null)
            {
                consumableEmptyText.text = refillType == ConsumableUseType.Bomb
                    ? "Brak bomb do napełnienia." : "Brak mikstur do napełnienia.";
                consumableEmptyText.gameObject.SetActive(consumables.Count == 0);
            }

            foreach (var consumable in consumables)
            {
                BlacksmithCraftingSlotView slot = null;
                slot = CreateSlot(
                    recipeSlotTemplate,
                    targetGrid,
                    ItemIconPresentation.GetIcon(consumable),
                    consumable.DisplayName,
                    $"Ładunki: {consumable.CurrentCharges}/{consumable.MaxCharges}",
                    $"{consumable.CurrentCharges}/{consumable.MaxCharges}",
                    ReferenceEquals(consumable, selectedConsumable),
                    false,
                    () =>
                    {
                        selectedConsumable = consumable;
                        selectedMedium = null;
                        foreach (var candidate in consumableSlots)
                            candidate?.SetSelected(false);
                        slot?.SetSelected(true);
                        RefreshPreview();
                    });
                if (slot != null)
                {
                    slot.SetItemTooltip(consumable, FindOwner(consumable));
                    consumableSlots.Add(slot);
                }
            }

        }

        private void RefreshPreview()
        {
            var targetGrid = GetMaterialsGrid();
            ClearSlots(mediumSlots);
            var hasSelection = selectedConsumable != null;
            previewSection?.SetActive(hasSelection);
            effectsSection?.SetActive(hasSelection);
            mediumSection?.SetActive(hasSelection);
            emptySummarySpacer?.SetActive(!hasSelection);
            var preview = refillService.CreatePreview(session, alchemist, BuildRequest());
            var options = preview.MediumOptions ?? Array.Empty<RefillMediumOption>();
            if (!options.Any(option => option.Medium?.Id == selectedMedium?.Id))
            {
                selectedMedium = (options.FirstOrDefault(option => option.Cost?.MissingAmount == 0)
                    ?? options.FirstOrDefault())?.Medium;
                preview = refillService.CreatePreview(session, alchemist, BuildRequest());
            }
            if (mediumEmptyText != null)
            {
                mediumEmptyText.gameObject.SetActive(selectedConsumable != null && options.Count == 0);
                mediumEmptyText.text = refillType == ConsumableUseType.Bomb
                    ? "Brak odpowiedniego prochu strzelniczego." : "Brak alkoholu lub składnika alchemicznego.";
            }
            foreach (var option in options)
            {
                if (option?.Medium == null) continue;
                var cost = option.Cost;
                var canOfferPurchase = cost?.HasPurchasableStock == true && cost.PurchasePrice > 0;
                var slot = CreateSlot(
                    materialSlotTemplate,
                    targetGrid,
                    option.Medium.Icon,
                    option.Medium.DisplayName,
                    BuildMediumTooltip(option),
                    cost == null ? "0/1" : $"{cost.OwnedAmount}/1",
                    selectedMedium?.Id == option.Medium.Id,
                    cost?.MissingAmount > 0,
                    () =>
                    {
                        selectedMedium = option.Medium;
                        RefreshPreview();
                    },
                    canOfferPurchase ? cost.PurchasePrice.ToString() : null,
                    canOfferPurchase ? () => TryBuyMedium(option.Medium) : null,
                    cost?.PurchaseState ?? CraftingMaterialPurchaseState.OutOfStock,
                    cost?.PurchaseState == CraftingMaterialPurchaseState.Available);
                slot?.SetDefinitionTooltip(option.Medium, BuildMediumTooltip(option),
                    cost?.PurchasePrice > 0 ? cost.PurchasePrice : (int?)null);
                mediumSlots.Add(slot);
            }


            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(ItemIconPresentation.GetIcon(preview.Consumable));
                if (preview.Consumable != null)
                    previewTooltipSlot.SetItemTooltip(
                        preview.Consumable,
                        FindOwner(preview.Consumable),
                        $"Po napełnieniu: {preview.MaxCharges}/{preview.MaxCharges}");
                else
                    previewTooltipSlot.SetTooltip(string.Empty, string.Empty);
            }
            if (previewNameText != null) previewNameText.text = preview.Consumable?.DisplayName ?? string.Empty;
            if (previewSummaryText != null)
                previewSummaryText.text = preview.Consumable == null
                    ? string.Empty
                    : $"Ładunki: {preview.CurrentCharges}/{preview.MaxCharges} → {preview.MaxCharges}/{preview.MaxCharges}"
                        + $"\nOdzyskane ładunki: {preview.MaxCharges - preview.CurrentCharges}";
            if (previewEffectsText != null)
                previewEffectsText.text = preview.Consumable == null ? string.Empty
                    : refillType == ConsumableUseType.Bomb
                        ? UIWorkspaceTooltipView.BuildBombEffectSummary(preview.Consumable, FindOwner(preview.Consumable))
                        : UIWorkspaceTooltipView.BuildPotionEffectSummary(preview.Consumable);
            if (craftButton != null) craftButton.interactable = preview.CanRefill;
            SetStatus(preview.CanRefill || selectedConsumable == null || options.Count == 0 ? string.Empty
                : options.FirstOrDefault(option => option.Medium?.Id == selectedMedium?.Id)?.Cost?.MissingAmount > 0
                    ? refillType == ConsumableUseType.Bomb ? "Brak prochu strzelniczego." : "Brak wybranego materiału."
                    : preview.FailureReason);
        }

        private void TryRefill()
        {
            if (!isVisible) return;

            if (!refillService.TryRefill(session, alchemist, BuildRequest(), out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            PopulateConsumables();
            RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void TryBuyMedium(ItemDefinition medium)
        {
            if (!isVisible || medium == null) return;
            selectedMedium = medium;
            if (!refillService.TryBuyMedium(session, alchemist, BuildRequest(), out _, out var failureReason))
            {
                RefreshPreview();
                SetStatus(failureReason);
                return;
            }

            RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private PotionRefillRequest BuildRequest() => new()
        {
            ConsumableInstanceId = selectedConsumable?.InstanceId ?? string.Empty,
            RecipientCharacterId = recipientCharacterId,
            MediumDefinitionId = selectedMedium?.Id ?? string.Empty
        };

        private string GetConsumableName() => refillType == ConsumableUseType.Bomb ? "bombę" : "miksturę";

        private CharacterInstance FindOwner(ConsumableItemInstance item) => item == null ? null
            : session.PlayerParty.ActiveMemberIds.Where(session.CharactersById.ContainsKey)
                .Select(id => session.CharactersById[id])
                .FirstOrDefault(character => character.Inventory?.Items.Contains(item) == true);

        private Transform GetRecipeGrid() => recipeGrid;

        private Transform GetMaterialsGrid() => materialsGrid;

        private BlacksmithCraftingSlotView CreateSlot(
            BlacksmithCraftingSlotView template,
            Transform parent,
            Sprite icon,
            string title,
            string description,
            string countLabel,
            bool selected,
            bool missing,
            Action selectAction,
            string buyLabel = null,
            Action buyAction = null,
            CraftingMaterialPurchaseState purchaseState = CraftingMaterialPurchaseState.NotNeeded,
            bool buyInteractable = true)
        {
            if (template == null || parent == null) return null;
            var slot = Instantiate(template, parent);
            slot.gameObject.SetActive(true);
            slot.Bind(icon, title, description, countLabel, selected, missing, selectAction,
                buyLabel, buyAction, purchaseState, buyInteractable);
            return slot;
        }

        private static void ClearSlots(List<BlacksmithCraftingSlotView> slots)
        {
            foreach (var slot in slots.Where(slot => slot != null))
            {
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            slots.Clear();
        }

        private void ClearRuntimeSlots()
        {
            ClearSlots(consumableSlots);
            ClearSlots(mediumSlots);
        }

        private void HideTemplates()
        {
            recipeSlotTemplate?.gameObject.SetActive(false);
            materialSlotTemplate?.gameObject.SetActive(false);
            HideNestedTemplates(serviceRoot?.transform);
        }

        private static void HideNestedTemplates(Transform parent)
        {
            if (parent == null) return;
            foreach (Transform child in parent)
            {
                if (child.name == "RecipeSlotTemplate" || child.name == "MaterialCardTemplate")
                    child.gameObject.SetActive(false);
                else
                    HideNestedTemplates(child);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private string BuildMediumTooltip(RefillMediumOption option)
        {
            return $"{option.Medium.DisplayName}: 1 szt. → pełne ładunki.";
        }
    }
}
