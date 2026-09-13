using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Content view for the material-crafting tab of an NPC services window.
    /// It deliberately has no window ownership: NpcServicesWindowView owns mode,
    /// close behaviour and service-tab navigation.
    /// </summary>
    public sealed class MaterialCraftingWindowView : MonoBehaviour
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
        [SerializeField] private TextMeshProUGUI recipeSectionLabel;
        [SerializeField] private TextMeshProUGUI materialsSectionLabel;

        private readonly MaterialCraftingService craftingService = new();
        private GameSession session;
        private NpcInstance specialist;
        private TMP_InputField previewNameInput;
        private string recipientCharacterId = string.Empty;
        private CraftableItemDefinition selectedRecipe;
        private bool isVisible;

        private void Awake()
        {
            // This label was inherited from an input-field prefab. The input field
            // synchronizes its serialized empty value back into the TMP component,
            // hiding the product name written by RefreshResultPreview.
            previewNameInput = previewNameText?.GetComponent<TMP_InputField>();
            if (previewNameInput != null) previewNameInput.enabled = false;
            if (previewNameText != null) previewNameText.raycastTarget = false;
            HideTemplates();
            if (craftButton != null) craftButton.onClick.AddListener(TryCraft);
        }

        private void OnEnable() => HideTemplates();

        private void OnDisable() => isVisible = false;

        private void OnDestroy()
        {
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
        }

        public bool Show(NpcInstance nextSpecialist, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextSpecialist == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId))
            {
                return false;
            }

            specialist = nextSpecialist;
            recipientCharacterId = nextRecipientCharacterId;
            isVisible = true;
            serviceRoot?.SetActive(true);
            HideTemplates();
            if (recipeSectionLabel != null) recipeSectionLabel.text = "Receptury";
            if (materialsSectionLabel != null) materialsSectionLabel.text = "Materiały";
            PopulateRecipes();
            RefreshPreview();
            return true;
        }

        public void Hide()
        {
            isVisible = false;
            serviceRoot?.SetActive(false);
        }

        private void PopulateRecipes()
        {
            ClearSlots(recipeGrid);
            var recipes = specialist?.Definition?.Roles?.OfType<IMaterialCraftingSpecialist>()
                .FirstOrDefault()?.MaterialRecipes
                ?.Where(recipe => recipe != null)
                .OrderBy(recipe => recipe.DisplayName)
                .ToList() ?? new List<CraftableItemDefinition>();
            if (!recipes.Contains(selectedRecipe)) selectedRecipe = recipes.FirstOrDefault();

            foreach (var recipe in recipes)
            {
                var slot = CreateSlot(
                    recipeSlotTemplate,
                    recipeGrid,
                    recipe.Icon,
                    string.IsNullOrWhiteSpace(recipe.DisplayName)
                        ? recipe.DisplayName ?? recipe.Id
                        : recipe.DisplayName,
                    BuildRecipeTooltip(recipe),
                    $"×{recipe.OutputAmount}",
                    ReferenceEquals(recipe, selectedRecipe),
                    false,
                    () =>
                    {
                        selectedRecipe = recipe;
                        PopulateRecipes();
                        RefreshPreview();
                    });
                slot?.SetDefinitionTooltip(recipe);
            }
        }

        private void RefreshPreview()
        {
            ClearSlots(materialsGrid);
            var preview = craftingService.CreatePreview(session, specialist, BuildRequest());
            // The selected recipe is already the deterministic result. Keep it
            // visible even when the preview is not craftable yet (for example due
            // to missing materials or an invalid recipient), just like the other
            // crafting windows do.
            RefreshResultPreview(
                preview.Result ?? selectedRecipe,
                preview.ResultAmount > 0
                    ? preview.ResultAmount
                    : selectedRecipe?.OutputAmount ?? 0);

            foreach (var material in preview.Materials ?? Array.Empty<CraftingMaterialCost>())
            {
                var materialId = material.Material.Id;
                var canOfferPurchase = material.HasPurchasableStock && material.PurchasePrice > 0;
                var slot = CreateSlot(
                    materialSlotTemplate,
                    materialsGrid,
                    material.Material.Icon,
                    material.Material.DisplayName,
                    BuildMaterialDescription(material),
                    $"{material.OwnedAmount}/{material.RequiredAmount}",
                    false,
                    material.MissingAmount > 0,
                    null,
                    canOfferPurchase ? $"{material.PurchasableAmount} ({material.PurchasePrice}g)" : null,
                    canOfferPurchase ? () => BuyMissingMaterial(materialId) : null,
                    material.PurchaseState,
                    material.PurchaseState == CraftingMaterialPurchaseState.Available);
                slot?.SetDefinitionTooltip(material.Material, BuildMaterialDescription(material),
                    material.PurchasePrice > 0 ? material.PurchasePrice : (int?)null);
            }

            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? "Gotowe do wytworzenia." : preview.FailureReason);
        }

        private void BuyMissingMaterial(string materialId)
        {
            if (!isVisible) return;

            if (!craftingService.TryBuyMissingMaterial(
                    session, specialist, BuildRequest(), materialId,
                    out var paidGold, out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            SetStatus($"Kupiono materiały za {paidGold}.");
            RefreshPreview();
        }

        private void TryCraft()
        {
            if (!isVisible) return;

            if (!craftingService.TryCraft(
                    session, specialist, BuildRequest(), out var items,
                    out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            SetStatus($"Wytworzono: {items.Count}× {selectedRecipe.DisplayName}.");
            RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private MaterialCraftRequest BuildRequest() => new()
        {
            Recipe = selectedRecipe,
            RecipientCharacterId = recipientCharacterId
        };

        private void RefreshResultPreview(ItemDefinition result, int amount)
        {
            var resultName = GetResultDisplayName(result);
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(result?.Icon);
                previewTooltipSlot.SetTooltip(
                    resultName,
                    result == null
                        ? "Wybierz receptę, aby zobaczyć wynik."
                        : $"{resultName}\nWynik recepty: {amount}×\n{result.Description}");
            }

            if (previewNameText != null)
            {
                previewNameText.gameObject.SetActive(true);
                previewNameInput?.SetTextWithoutNotify(resultName);
                previewNameText.text = resultName;
            }
            if (previewSummaryText != null)
            {
                previewSummaryText.text = result == null
                    ? "Wybierz receptę."
                    : $"Wynik recepty • {amount}×";
            }
        }

        private static string GetResultDisplayName(ItemDefinition result)
        {
            if (!string.IsNullOrWhiteSpace(result?.DisplayName))
            {
                return result.DisplayName;
            }

            return !string.IsNullOrWhiteSpace(result?.Id)
                ? result.Id
                : "Wynik craftingu";
        }

        private static string BuildRecipeTooltip(CraftableItemDefinition recipe)
        {
            if (recipe == null) return string.Empty;
            var ingredients = recipe.Ingredients == null
                ? string.Empty
                : string.Join("\n", recipe.Ingredients
                    .Where(ingredient => ingredient?.Material != null)
                    .Select(ingredient => $"{ingredient.Amount}× {ingredient.Material.DisplayName}"));
            return $"Wynik: {recipe.OutputAmount}× {recipe.DisplayName}\n"
                + $"Składniki:\n{ingredients}";
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

        private void ClearSlots(Transform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).GetComponent<BlacksmithCraftingSlotView>();
                if (child == null || child == recipeSlotTemplate || child == materialSlotTemplate) continue;
                Destroy(child.gameObject);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
        }

        private void HideTemplates()
        {
            if (recipeSlotTemplate != null) recipeSlotTemplate.gameObject.SetActive(false);
            if (materialSlotTemplate != null) materialSlotTemplate.gameObject.SetActive(false);
        }

        private static string BuildMaterialDescription(CraftingMaterialCost material)
        {
            if (material == null) return string.Empty;
            if (string.IsNullOrWhiteSpace(material.PurchaseFailureReason))
                return material.Material?.Description ?? string.Empty;

            return (material.Material?.Description ?? string.Empty)
                + $"\n<color=#E06C75>{material.PurchaseFailureReason}</color>";
        }
    }
}
