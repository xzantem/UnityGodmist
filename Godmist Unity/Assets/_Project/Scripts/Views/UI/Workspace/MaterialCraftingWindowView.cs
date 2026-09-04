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

        private readonly MaterialCraftingService craftingService = new();
        private GameSession session;
        private NpcInstance blacksmith;
        private string recipientCharacterId = string.Empty;
        private CraftableItemDefinition selectedRecipe;

        private void Awake()
        {
            HideTemplates();
            if (craftButton != null) craftButton.onClick.AddListener(TryCraft);
        }

        private void OnEnable() => HideTemplates();

        private void OnDestroy()
        {
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
        }

        public bool Show(NpcInstance nextBlacksmith, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextBlacksmith == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId))
            {
                return false;
            }

            blacksmith = nextBlacksmith;
            recipientCharacterId = nextRecipientCharacterId;
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateRecipes();
            RefreshPreview();
            return true;
        }

        public void Hide()
        {
            serviceRoot?.SetActive(false);
        }

        private void PopulateRecipes()
        {
            ClearSlots(recipeGrid);
            var recipes = blacksmith?.Definition?.GetRole<BlacksmithRoleData>()?.MaterialRecipes
                ?.Where(recipe => recipe != null)
                .OrderBy(recipe => recipe.DisplayName)
                .ToList() ?? new List<CraftableItemDefinition>();
            if (!recipes.Contains(selectedRecipe)) selectedRecipe = recipes.FirstOrDefault();

            foreach (var recipe in recipes)
            {
                CreateSlot(
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
            }
        }

        private void RefreshPreview()
        {
            ClearSlots(materialsGrid);
            var preview = craftingService.CreatePreview(session, blacksmith, BuildRequest());
            RefreshResultPreview(preview.Result, preview.ResultAmount);

            foreach (var material in preview.Materials ?? Array.Empty<CraftingMaterialCost>())
            {
                var materialId = material.Material.Id;
                var canOfferPurchase = material.HasPurchasableStock && material.PurchasePrice > 0;
                CreateSlot(
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
            }

            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? "Gotowe do wytworzenia." : preview.FailureReason);
        }

        private void BuyMissingMaterial(string materialId)
        {
            if (!craftingService.TryBuyMissingMaterial(
                    session, blacksmith, BuildRequest(), materialId,
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
            if (!craftingService.TryCraft(
                    session, blacksmith, BuildRequest(), out var items,
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
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(result?.Icon);
                previewTooltipSlot.SetTooltip(
                    result?.DisplayName ?? "Wynik craftingu",
                    result == null
                        ? "Wybierz receptę, aby zobaczyć wynik."
                        : $"{result.DisplayName}\nWynik recepty: {amount}×\n{result.Description}");
            }

            if (previewNameText != null) previewNameText.text = result?.DisplayName ?? string.Empty;
            if (previewSummaryText != null)
            {
                previewSummaryText.text = result == null
                    ? "Wybierz receptę."
                    : $"Wynik recepty • {amount}×";
            }
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
