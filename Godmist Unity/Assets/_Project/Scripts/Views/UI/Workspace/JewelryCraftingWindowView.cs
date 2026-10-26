using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
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
    /// <summary>Serialized Enchanter service content. Its prefab wiring is documented in UNITY_HANDOFF.md.</summary>
    public sealed class JewelryCraftingWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private TMP_InputField customNameInput;
        [SerializeField] private Transform jewelryContent;
        [SerializeField] private Transform metalContent;
        [SerializeField] private Transform gemContent;
        [SerializeField] private Transform materialsContent;
        [SerializeField] private BlacksmithCraftingSlotView jewelrySlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView ingredientSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView materialSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI previewEffectsText;
        [SerializeField] private ScrollRect metalScroll;
        [SerializeField] private ScrollRect gemScroll;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button craftButton;

        private readonly JewelryCraftingService service = new();
        private readonly ItemInstanceFactory itemInstanceFactory = new();
        private GameSession session;
        private NpcInstance enchanter;
        private string recipientCharacterId = string.Empty;
        private JewelryItemDefinition selectedDefinition;
        private JewelryMetalData selectedMetal;
        private JewelryGemDefinition selectedGem;

        private void Awake()
        {
            HideTemplates();
            if (craftButton != null) craftButton.onClick.AddListener(TryCraft);
            if (customNameInput != null) customNameInput.onValueChanged.AddListener(HandleNameChanged);
        }

        private void OnEnable() => HideTemplates();

        private void OnDestroy()
        {
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
            if (customNameInput != null) customNameInput.onValueChanged.RemoveListener(HandleNameChanged);
        }

        public bool Show(NpcInstance nextEnchanter, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextEnchanter?.Definition?.GetRole<EnchanterRoleData>() == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId)) return false;

            enchanter = nextEnchanter;
            recipientCharacterId = nextRecipientCharacterId;
            serviceRoot?.SetActive(true);

            HideTemplates();
            PopulateJewelry();
            return selectedDefinition != null;
        }

        public void Hide() => serviceRoot?.SetActive(false);

        private void HandleNameChanged(string _) => RefreshPreview();

        private void PopulateJewelry()
        {
            ClearSlots(jewelryContent);
            var offers = enchanter?.Definition?.GetRole<EnchanterRoleData>()?.JewelryOffers
                ?.Where(item => item != null).Distinct().ToList() ?? new List<JewelryItemDefinition>();
            if (!offers.Contains(selectedDefinition)) selectedDefinition = offers.FirstOrDefault();
            foreach (var definition in offers)
            {
                CreateSlot(jewelrySlotTemplate, jewelryContent, definition.Icon, definition.JewelryTypeDisplayName,
                    string.Empty, string.Empty, ReferenceEquals(definition, selectedDefinition), false,
                    () =>
                    {
                        selectedDefinition = definition;
                        selectedMetal = null;
                        selectedGem = null;
                        if (metalScroll != null) metalScroll.verticalNormalizedPosition = 1f;
                        if (gemScroll != null) gemScroll.verticalNormalizedPosition = 1f;
                        PopulateJewelry();
                    });
            }
            PopulateIngredients();
        }

        private void PopulateIngredients()
        {
            var metalPosition = metalScroll != null ? metalScroll.verticalNormalizedPosition : 1f;
            var gemPosition = gemScroll != null ? gemScroll.verticalNormalizedPosition : 1f;
            ClearSlots(metalContent);
            ClearSlots(gemContent);
            if (selectedDefinition == null) { RefreshPreview(); return; }
            var metals = selectedDefinition.Metals?.Where(value => value != null).ToList() ?? new List<JewelryMetalData>();
            var gems = selectedDefinition.Gems?.Where(value => value != null && value.CraftingMaterial != null).ToList() ?? new List<JewelryGemDefinition>();
            if (!metals.Contains(selectedMetal)) selectedMetal = metals.FirstOrDefault();
            if (!gems.Contains(selectedGem)) selectedGem = gems.FirstOrDefault();
            foreach (var metal in metals)
            {
                var slot = CreateSlot(ingredientSlotTemplate, metalContent, metal.Material?.Icon, JewelryGenerationService.GetMetalDisplayName(metal),
                    BuildMetalTooltip(metal), string.Empty, ReferenceEquals(metal, selectedMetal), false,
                    () => { selectedMetal = metal; PopulateIngredients(); },
                    tooltipMeta: UIWorkspaceTooltipView.GetColoredRarityDisplayName(
                        JewelryGenerationService.GetMaterialRarity(metal.Material)));
                slot?.SetRarity(JewelryGenerationService.GetMaterialRarity(metal.Material));
            }
            foreach (var gem in gems)
            {
                var slot = CreateSlot(ingredientSlotTemplate, gemContent, gem.CraftingMaterial?.Icon, gem.CraftingMaterial?.DisplayName,
                    BuildGemTooltip(gem), string.Empty,
                    ReferenceEquals(gem, selectedGem), false,
                    () => { selectedGem = gem; PopulateIngredients(); },
                    tooltipMeta: UIWorkspaceTooltipView.GetColoredRarityDisplayName(
                        gem.CraftingMaterial?.BaseRarity ?? RarityTier.Common));
                slot?.SetRarity(gem.CraftingMaterial?.BaseRarity);
            }
            Canvas.ForceUpdateCanvases();
            if (metalScroll != null) metalScroll.verticalNormalizedPosition = metalPosition;
            if (gemScroll != null) gemScroll.verticalNormalizedPosition = gemPosition;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            ClearSlots(materialsContent);
            var preview = service.CreatePreview(session, enchanter, BuildRequest());
            foreach (var material in (preview.Materials ?? Array.Empty<CraftingMaterialCost>())
                .OrderBy(cost => cost.Material == selectedMetal?.Material ? 0 : 1))
            {
                var id = material.Material?.Id;
                var canBuy = material.HasPurchasableStock && material.PurchasePrice > 0;
                var materialSlot = CreateSlot(materialSlotTemplate, materialsContent, material.Material?.Icon,
                    material.Material?.DisplayName, string.Empty, $"{material.OwnedAmount}/{material.RequiredAmount}", false,
                    material.MissingAmount > 0, null,
                    canBuy ? material.PurchasePrice.ToString("N0") : null,
                    canBuy ? () => BuyMaterial(id) : null, material.PurchaseState,
                    material.PurchaseState == CraftingMaterialPurchaseState.Available);
                materialSlot?.SetItemTooltip(
                    material.Material == null ? null : itemInstanceFactory.Create(material.Material),
                    null,
                    BuildMaterialContextTooltip(material),
                    material.PurchasePrice > 0 ? material.PurchasePrice : (int?)null);
            }
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(ItemIconPresentation.GetIcon(preview.Result));
                if (preview.Result != null)
                    previewTooltipSlot.SetItemTooltip(preview.Result, null);
                else
                    previewTooltipSlot.SetTooltip(
                        "Wybierz składniki",
                        preview?.FailureReason ?? "Wybierz typ biżuterii, metal i klejnot.");
            }
            SetLabel(previewNameText, preview.Result?.DisplayName);
            var jewelryDefinition = preview.Result?.EquipmentDefinition as JewelryItemDefinition;
            SetLabel(previewSummaryText, preview.Result == null ? string.Empty
                : $"{jewelryDefinition?.JewelryTypeDisplayName ?? "Biżuteria"} • Poziom {preview.Result.ItemLevel}\n"
                    + UIWorkspaceTooltipView.GetColoredRarityDisplayName(preview.Result.CurrentRarity));
            SetLabel(previewEffectsText, preview.Result == null ? string.Empty
                : UIWorkspaceTooltipView.BuildJewelryCraftingSummary(preview.Result));
            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? string.Empty
                : (preview.Materials?.Any(cost => cost.MissingAmount > 0) == true
                    ? "Brak materiałów." : preview.FailureReason));
        }

        private void BuyMaterial(string id)
        {
            if (!service.TryBuyMissingMaterial(session, enchanter, BuildRequest(), id, out _, out var failure))
            {
                SetStatus(failure); return;
            }
            RefreshPreview();
        }

        private void TryCraft()
        {
            if (!service.TryCraft(session, enchanter, BuildRequest(), out _, out var failure))
            {
                SetStatus(failure); return;
            }
            RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private JewelryCraftRequest BuildRequest() => new()
        {
            Definition = selectedDefinition, Metal = selectedMetal, Gem = selectedGem,
            CustomName = customNameInput?.text ?? string.Empty, RecipientCharacterId = recipientCharacterId
        };

        private BlacksmithCraftingSlotView CreateSlot(BlacksmithCraftingSlotView template, Transform parent,
            Sprite icon, string title, string body, string count, bool selected, bool missing, Action select,
            string buy = null, Action buyAction = null,
            CraftingMaterialPurchaseState state = CraftingMaterialPurchaseState.NotNeeded,
            bool buyInteractable = true, string tooltipMeta = null)
        {
            if (template == null || parent == null) return null;
            var slot = Instantiate(template, parent);
            slot.gameObject.SetActive(true);
            slot.Bind(icon, title, body, count, selected, missing, select, buy, buyAction,
                state, buyInteractable, tooltipMeta);
            return slot;
        }

        private void ClearSlots(Transform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var slot = parent.GetChild(index).GetComponent<BlacksmithCraftingSlotView>();
                if (slot == null || slot == jewelrySlotTemplate || slot == ingredientSlotTemplate || slot == materialSlotTemplate) continue;
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
        }

        private void HideTemplates()
        {
            if (jewelrySlotTemplate != null) jewelrySlotTemplate.gameObject.SetActive(false);
            if (ingredientSlotTemplate != null) ingredientSlotTemplate.gameObject.SetActive(false);
            if (materialSlotTemplate != null) materialSlotTemplate.gameObject.SetActive(false);
        }

        private void SetStatus(string value) => SetLabel(statusText, value);
        private static void SetLabel(TextMeshProUGUI label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
        private static string BuildMetalTooltip(JewelryMetalData metal) => metal == null ? string.Empty
            : $"Poziom: {JewelryGenerationService.GetItemLevel(metal)}\n"
                + UIWorkspaceTooltipView.BuildJewelryIngredientEffects(metal.Modifiers);
        private static string BuildGemTooltip(JewelryGemDefinition gem)
        {
            var effects = UIWorkspaceTooltipView.BuildJewelryIngredientEffects(gem?.Modifiers);
            if (string.IsNullOrWhiteSpace(effects)) return "Brak dodatkowych efektów.";

            return effects;
        }
        private static string BuildMaterialContextTooltip(CraftingMaterialCost material)
        {
            if (material == null) return string.Empty;
            if (material.MissingAmount <= 0)
                return $"Wymagane: {material.RequiredAmount} • Posiadane: {material.OwnedAmount}";
            if (!string.IsNullOrWhiteSpace(material.PurchaseFailureReason))
                return $"Brakuje: {material.MissingAmount}\n<color=#E06C75>{material.PurchaseFailureReason}</color>";
            return $"Brakuje: {material.MissingAmount}";
        }
    }
}
