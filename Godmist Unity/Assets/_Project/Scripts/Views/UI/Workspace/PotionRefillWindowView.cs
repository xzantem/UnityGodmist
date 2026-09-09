using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Game;
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

        /// <summary>Used by the prefab layout installer to wire a self-contained refill service.</summary>
        public void ConfigureLayout(
            GameObject root,
            Transform consumablesContent,
            Transform mediumsContent,
            BlacksmithCraftingSlotView consumableTemplate,
            BlacksmithCraftingSlotView mediumTemplate,
            BlacksmithCraftingSlotView previewSlot,
            TextMeshProUGUI nameText,
            TextMeshProUGUI summaryText,
            TextMeshProUGUI nextStatusText,
            Button refillButton,
            ConsumableUseType type)
        {
            serviceRoot = root;
            recipeGrid = consumablesContent;
            materialsGrid = mediumsContent;
            recipeSlotTemplate = consumableTemplate;
            materialSlotTemplate = mediumTemplate;
            previewTooltipSlot = previewSlot;
            previewNameText = nameText;
            previewSummaryText = summaryText;
            statusText = nextStatusText;
            craftButton = refillButton;
            refillType = type;
        }

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
            isVisible = true;
            ClearRuntimeSlots();
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateConsumables();
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
                .GroupBy(consumable => consumable.InstanceId)
                .Select(group => group.First())
                .ToList();
            if (!consumables.Contains(selectedConsumable)) selectedConsumable = consumables.FirstOrDefault();
            if (selectedConsumable == null) selectedMedium = null;

            foreach (var consumable in consumables)
            {
                BlacksmithCraftingSlotView slot = null;
                slot = CreateSlot(
                    recipeSlotTemplate,
                    targetGrid,
                    consumable.Definition?.Icon,
                    consumable.DisplayName,
                    $"Ładunki: {consumable.CurrentCharges}/{consumable.MaxCharges}",
                    $"{consumable.CurrentCharges}/{consumable.MaxCharges}",
                    ReferenceEquals(consumable, selectedConsumable),
                    consumable.CurrentCharges >= consumable.MaxCharges,
                    () =>
                    {
                        selectedConsumable = consumable;
                        selectedMedium = null;
                        foreach (var candidate in consumableSlots)
                            candidate?.SetSelected(false);
                        slot?.SetSelected(true);
                        RefreshPreview();
                    });
                if (slot != null) consumableSlots.Add(slot);
            }
        }

        private void RefreshPreview()
        {
            var targetGrid = GetMaterialsGrid();
            ClearSlots(mediumSlots);
            var preview = refillService.CreatePreview(session, alchemist, BuildRequest());
            var visibleOptions = selectedMedium == null
                ? preview.MediumOptions ?? Enumerable.Empty<RefillMediumOption>()
                : (preview.MediumOptions ?? Enumerable.Empty<RefillMediumOption>())
                    .Where(option => option?.Medium?.Id == selectedMedium.Id);
            foreach (var option in visibleOptions)
            {
                if (option?.Medium == null) continue;
                var cost = option.Cost;
                var canOfferPurchase = cost?.HasPurchasableStock == true && cost.PurchasePrice > 0;
                mediumSlots.Add(CreateSlot(
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
                        if (cost?.MissingAmount > 0) return;
                        selectedMedium = selectedMedium?.Id == option.Medium.Id ? null : option.Medium;
                        RefreshPreview();
                    },
                    canOfferPurchase ? $"{cost.PurchasableAmount} ({cost.PurchasePrice}g)" : null,
                    canOfferPurchase ? () => TryBuyMedium(option.Medium) : null,
                    cost?.PurchaseState ?? CraftingMaterialPurchaseState.OutOfStock,
                    cost?.PurchaseState == CraftingMaterialPurchaseState.Available));
            }

            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(preview.Consumable?.Definition?.Icon);
                previewTooltipSlot.SetTooltip(
                    preview.Consumable?.DisplayName ?? GetOperationName(),
                    preview.Consumable == null
                        ? $"Wybierz {GetConsumableName()}."
                        : $"Ładunki: {preview.CurrentCharges}/{preview.MaxCharges}\nPo napełnieniu: {preview.MaxCharges}/{preview.MaxCharges}");
            }
            if (previewNameText != null) previewNameText.text = preview.Consumable?.DisplayName ?? string.Empty;
            if (previewSummaryText != null)
                previewSummaryText.text = preview.Consumable == null
                    ? $"Wybierz {GetConsumableName()}."
                    : $"Ładunki: {preview.CurrentCharges}/{preview.MaxCharges} → {preview.MaxCharges}/{preview.MaxCharges}";
            if (craftButton != null) craftButton.interactable = preview.CanRefill;
            SetStatus(preview.CanRefill ? "Gotowe do napełnienia." : preview.FailureReason);
        }

        private void TryRefill()
        {
            if (!isVisible) return;

            if (!refillService.TryRefill(session, alchemist, BuildRequest(), out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            SetStatus(refillType == ConsumableUseType.Bomb
                ? "Bomba została napełniona do pełna."
                : "Mikstura została napełniona do pełna.");
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

        private string GetOperationName() => refillType == ConsumableUseType.Bomb
            ? "Napełnianie bomby"
            : "Napełnianie mikstury";

        private Transform GetRecipeGrid() => ResolveInnerGrid(recipeGrid, "RecipeContent");

        private Transform GetMaterialsGrid() => ResolveInnerGrid(materialsGrid, "MaterialsGrid");

        private static Transform ResolveInnerGrid(Transform configured, string expectedName)
        {
            if (configured == null) return null;
            if (configured.name == expectedName) return configured;

            foreach (Transform child in configured)
            {
                var nested = ResolveInnerGrid(child, expectedName);
                if (nested != null) return nested;
            }

            return configured;
        }

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
            foreach (var slot in slots.Where(slot => slot != null)) Destroy(slot.gameObject);
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
            if (statusText != null) statusText.text = message ?? string.Empty;
        }

        private string BuildMediumTooltip(RefillMediumOption option)
        {
            var kind = refillType == ConsumableUseType.Bomb ? "prochu strzelniczego" : "alkoholu";
            return $"Tier {option.Tier}. Jedna jednostka {kind} napełnia {GetConsumableName()} do pełna.";
        }
    }
}
