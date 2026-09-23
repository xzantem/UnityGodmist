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
        [SerializeField] private GameObject variantSection;
        [SerializeField] private TMP_Dropdown firstVariantDropdown;
        [SerializeField] private TMP_Dropdown secondVariantDropdown;

        private readonly MaterialCraftingService craftingService = new();
        private readonly Dictionary<CraftableItemDefinition, BlacksmithCraftingSlotView> recipeSlots = new();
        private GameSession session;
        private NpcInstance specialist;
        private TMP_InputField previewNameInput;
        private string recipientCharacterId = string.Empty;
        private CraftableItemDefinition selectedRecipe;
        private int selectedVariant;
        private VariantSelection variantSelection;
        private bool updatingVariantDropdowns;
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
            if (firstVariantDropdown != null)
                firstVariantDropdown.onValueChanged.AddListener(HandleVariantChanged);
            if (secondVariantDropdown != null)
                secondVariantDropdown.onValueChanged.AddListener(HandleVariantChanged);
        }

        private void OnEnable() => HideTemplates();

        private void OnDisable() => isVisible = false;

        private void OnDestroy()
        {
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
            if (firstVariantDropdown != null)
                firstVariantDropdown.onValueChanged.RemoveListener(HandleVariantChanged);
            if (secondVariantDropdown != null)
                secondVariantDropdown.onValueChanged.RemoveListener(HandleVariantChanged);
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
            // RecipeContent was added below the shared scroll prefab's Content,
            // while ScrollRect still points at that fixed-height parent. Scroll
            // the actual growing grid so long recipe lists can move.
            if (recipeGrid is RectTransform recipeContent)
            {
                var scroll = recipeContent.GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.content = recipeContent;
            }
            HideTemplates();
            if (recipeSectionLabel != null) recipeSectionLabel.text = "Receptury";
            if (materialsSectionLabel != null) materialsSectionLabel.text = "Materiały";
            PopulateRecipes();
            RefreshVariantSelectors();
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
            recipeSlots.Clear();
            var recipes = specialist?.Definition?.Roles?.OfType<IMaterialCraftingSpecialist>()
                .FirstOrDefault()?.MaterialRecipes
                ?.Where(recipe => recipe != null)
                .OrderBy(recipe => recipe.DisplayName)
                .ToList() ?? new List<CraftableItemDefinition>();
            if (!recipes.Contains(selectedRecipe)) { selectedRecipe = recipes.FirstOrDefault(); selectedVariant = 0; }

            foreach (var recipe in recipes)
            {
                var slot = CreateSlot(
                    recipeSlotTemplate,
                    recipeGrid,
                    recipe.ResultDefinition?.Icon,
                    string.IsNullOrWhiteSpace(recipe.DisplayName)
                        ? recipe.DisplayName ?? recipe.Id : recipe.DisplayName,
                    BuildRecipeTooltip(recipe),
                    $"×{recipe.OutputAmount}",
                    ReferenceEquals(recipe, selectedRecipe),
                    false,
                    () =>
                    {
                        selectedRecipe = recipe;
                        selectedVariant = 0;
                        foreach (var entry in recipeSlots)
                            entry.Value.SetSelected(ReferenceEquals(entry.Key, recipe));
                        RefreshVariantSelectors();
                        RefreshPreview();
                    });
                slot?.SetDefinitionTooltip(recipe.ResultDefinition,
                    recipe.RecipeVariantCount > 1 ? "Składniki wybierz po prawej stronie." : string.Empty);
                if (slot != null) recipeSlots[recipe] = slot;
            }
        }

        private void RefreshVariantSelectors()
        {
            variantSelection = VariantSelection.ForRecipe(selectedRecipe);
            updatingVariantDropdowns = true;
            var hasChoices = variantSelection != null && variantSection != null
                && firstVariantDropdown != null
                && (!variantSelection.HasSecondChoice || secondVariantDropdown != null);
            variantSection?.SetActive(hasChoices);
            if (!hasChoices)
            {
                updatingVariantDropdowns = false;
                return;
            }

            firstVariantDropdown.ClearOptions();
            firstVariantDropdown.AddOptions(variantSelection.OptionLabels);
            firstVariantDropdown.SetValueWithoutNotify(variantSelection.FirstChoiceFor(selectedVariant));
            firstVariantDropdown.RefreshShownValue();
            if (firstVariantDropdown.transform is RectTransform firstRect)
            {
                firstRect.anchorMax = new Vector2(
                    variantSelection.HasSecondChoice ? .48f : 1f, firstRect.anchorMax.y);
                firstRect.sizeDelta = new Vector2(
                    variantSelection.HasSecondChoice ? -16f : -24f, firstRect.sizeDelta.y);
            }

            if (secondVariantDropdown != null)
            {
                secondVariantDropdown.gameObject.SetActive(variantSelection.HasSecondChoice);
                if (variantSelection.HasSecondChoice)
                {
                    secondVariantDropdown.ClearOptions();
                    secondVariantDropdown.AddOptions(variantSelection.OptionLabels);
                    secondVariantDropdown.SetValueWithoutNotify(variantSelection.SecondChoiceFor(selectedVariant));
                    secondVariantDropdown.RefreshShownValue();
                }
            }
            updatingVariantDropdowns = false;
        }

        private void HandleVariantChanged(int _)
        {
            if (updatingVariantDropdowns || !isVisible || variantSelection == null) return;
            selectedVariant = variantSelection.Resolve(
                firstVariantDropdown?.value ?? 0,
                secondVariantDropdown?.value ?? 0);
            RefreshPreview();
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
                preview.Result ?? selectedRecipe?.ResultDefinition,
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
            RecipeVariantIndex = selectedVariant,
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
            var ingredients = recipe.GetIngredients() == null
                ? string.Empty
                : string.Join("\n", recipe.GetIngredients()
                    .Where(ingredient => ingredient?.Material != null)
                    .Select(ingredient => $"{ingredient.Amount}× {ingredient.Material.DisplayName}"));
            return $"Wynik: {recipe.OutputAmount}× {GetResultDisplayName(recipe.ResultDefinition)}\n"
                + (recipe.RecipeVariantCount > 1
                    ? "Składniki wybierz po prawej stronie."
                    : $"Składniki:\n{ingredients}");
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

        // Existing assets enumerate legal variants for pricing and crafting.
        // This adapter exposes interchangeable ingredient positions as one or
        // two dropdowns without changing their authored recipe definitions.
        public sealed class VariantSelection
        {
            private readonly List<int[]> choicesByVariant;
            private readonly Dictionary<(int First, int Second), int> variantByChoices;
            public readonly List<string> OptionLabels;
            public readonly bool HasSecondChoice;

            private VariantSelection(List<string> labels, List<int[]> choices,
                Dictionary<(int First, int Second), int> lookup, bool hasSecondChoice)
            {
                OptionLabels = labels;
                choicesByVariant = choices;
                variantByChoices = lookup;
                HasSecondChoice = hasSecondChoice;
            }

            public static VariantSelection ForRecipe(CraftableItemDefinition recipe)
            {
                if (recipe == null || recipe.RecipeVariantCount <= 1) return null;
                var maps = new List<Dictionary<string, int>>();
                var materials = new Dictionary<string, ItemDefinition>();
                for (var variant = 0; variant < recipe.RecipeVariantCount; variant++)
                {
                    var requirements = recipe.GetIngredients(variant);
                    if (requirements == null || requirements.Any(item => item?.Material == null
                        || string.IsNullOrWhiteSpace(item.Material.Id) || item.Amount <= 0))
                        return FromVariantLabels(recipe);

                    var amounts = new Dictionary<string, int>();
                    foreach (var item in requirements)
                    {
                        var id = item.Material.Id;
                        amounts[id] = amounts.GetValueOrDefault(id) + item.Amount;
                        materials[id] = item.Material;
                    }
                    maps.Add(amounts);
                }

                var common = materials.Keys.ToDictionary(id => id,
                    id => maps.Min(map => map.GetValueOrDefault(id)));
                var variable = maps.Select(map => map.ToDictionary(entry => entry.Key,
                        entry => entry.Value - common[entry.Key])
                    .Where(entry => entry.Value > 0)
                    .ToDictionary(entry => entry.Key, entry => entry.Value)).ToList();
                var unit = variable.SelectMany(map => map.Values).Aggregate(0, GreatestCommonDivisor);
                if (unit <= 0) return FromVariantLabels(recipe);

                var positions = variable.Select(map => map.Values.Sum(amount => amount / unit)).Distinct().ToList();
                if (positions.Count != 1 || positions[0] is < 1 or > 2
                    || variable.Any(map => map.Values.Any(amount => amount % unit != 0)))
                    return FromVariantLabels(recipe);

                var options = variable.SelectMany(map => map.Keys).Distinct()
                    .OrderBy(id => materials[id].DisplayName, StringComparer.CurrentCulture)
                    .ThenBy(id => id, StringComparer.Ordinal).ToList();
                var expectedVariants = positions[0] == 1
                    ? options.Count : options.Count * (options.Count + 1) / 2;
                if (expectedVariants != recipe.RecipeVariantCount)
                    return FromVariantLabels(recipe);

                var lookup = new Dictionary<(int First, int Second), int>();
                var choices = new List<int[]>();
                for (var variant = 0; variant < variable.Count; variant++)
                {
                    var selected = variable[variant]
                        .SelectMany(entry => Enumerable.Repeat(options.IndexOf(entry.Key), entry.Value / unit))
                        .OrderBy(index => index).ToArray();
                    if (selected.Length != positions[0]) return FromVariantLabels(recipe);
                    var key = (selected[0], selected.Length == 2 ? selected[1] : -1);
                    if (!lookup.TryAdd(key, variant)) return FromVariantLabels(recipe);
                    choices.Add(selected);
                }

                return new VariantSelection(options.Select(id => materials[id].DisplayName).ToList(),
                    choices, lookup, positions[0] == 2);
            }

            private static VariantSelection FromVariantLabels(CraftableItemDefinition recipe)
            {
                var labels = Enumerable.Range(0, recipe.RecipeVariantCount)
                    .Select(index => string.IsNullOrWhiteSpace(recipe.GetVariantLabel(index))
                        ? $"Wariant {index + 1}" : recipe.GetVariantLabel(index)).ToList();
                var choices = Enumerable.Range(0, labels.Count)
                    .Select(index => new[] { index }).ToList();
                return new VariantSelection(labels, choices, null, false);
            }

            public int FirstChoiceFor(int variant) =>
                variant >= 0 && variant < choicesByVariant.Count ? choicesByVariant[variant][0] : 0;

            public int SecondChoiceFor(int variant) =>
                HasSecondChoice && variant >= 0 && variant < choicesByVariant.Count
                    ? choicesByVariant[variant][1] : 0;

            public int Resolve(int first, int second)
            {
                if (variantByChoices == null)
                    return Mathf.Clamp(first, 0, choicesByVariant.Count - 1);
                var key = HasSecondChoice
                    ? (Mathf.Min(first, second), Mathf.Max(first, second))
                    : (first, -1);
                return variantByChoices.TryGetValue(key, out var variant) ? variant : 0;
            }

            private static int GreatestCommonDivisor(int left, int right)
            {
                while (right != 0) (left, right) = (right, left % right);
                return left;
            }
        }
    }
}
