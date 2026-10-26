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
    /// <summary>
    /// Prefab-driven Alchemist view. Its layout and interactive slots are copied from
    /// Blacksmith crafting; this class binds only alchemical data and interactions.
    /// </summary>
    public sealed class AlchemyCraftingWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [Header("Ingredients")]
        [SerializeField] private Transform ingredientContent;
        [SerializeField] private BlacksmithCraftingSlotView ingredientSlotTemplate;
        [SerializeField] private List<BlacksmithCraftingSlotView> selectedIngredientSlots = new();
        [SerializeField] private ScrollRect ingredientScroll;
        [SerializeField] private TextMeshProUGUI ingredientCountText;
        [Header("Selectors")]
        [SerializeField] private TMP_Dropdown mediumDropdown;
        [SerializeField] private TMP_Dropdown catalystDropdown;
        [SerializeField] private TMP_InputField customNameInput;
        [Header("Summary")]
        [SerializeField] private Transform materialRequirementsContent;
        [SerializeField] private BlacksmithCraftingSlotView materialSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI previewEffectsText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button craftButton;
        [SerializeField] private ConsumableUseType craftingType = ConsumableUseType.Potion;

        private readonly AlchemyCraftingService craftingService = new();
        private readonly List<ItemDefinition> selectedIngredients = new();
        private readonly Dictionary<string, BlacksmithCraftingSlotView> ingredientSlots = new();
        private readonly List<BlacksmithCraftingSlotView> requirementSlots = new();
        private readonly List<ItemDefinition> displayedMediums = new();
        private readonly List<ItemDefinition> displayedCatalysts = new();
        private GameSession session;
        private NpcInstance alchemist;
        private string recipientCharacterId = string.Empty;
        private ItemDefinition selectedMedium;
        private ItemDefinition selectedCatalyst;
        private string customName = string.Empty;
        private bool isVisible;
        private bool suppressInputEvents;
        private string materialRequirementKey;

        private void Awake()
        {
            HideTemplates();
            BindStaticInputs();
        }

        private void OnDestroy()
        {
            if (mediumDropdown != null) mediumDropdown.onValueChanged.RemoveListener(SelectMedium);
            if (catalystDropdown != null) catalystDropdown.onValueChanged.RemoveListener(SelectCatalyst);
            if (customNameInput != null) customNameInput.onValueChanged.RemoveListener(HandleCustomNameChanged);
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
        }

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
            selectedIngredients.Clear();
            selectedMedium = null;
            selectedCatalyst = null;
            materialRequirementKey = null;
            customName = string.Empty;
            isVisible = true;
            serviceRoot?.SetActive(true);
            ConfigureIngredientScroll();
            BindStaticInputs();
            SetCustomNameInput(string.Empty);
            Refresh();
            return true;
        }

        public void Hide()
        {
            isVisible = false;
            ClearRuntimeSlots();
            serviceRoot?.SetActive(false);
        }

        private void ConfigureIngredientScroll()
        {
            if (ingredientScroll != null) { ingredientScroll.verticalNormalizedPosition = 1; return; }
            if (ingredientContent is not RectTransform grid ||
                grid.parent is not RectTransform viewport) return;

            // The existing alchemy prefab has a fixed-height ingredient section
            // but no viewport. Its grid grows behind the selector and name fields.
            viewport.gameObject.TryGetComponent<RectMask2D>(out var mask);
            if (mask == null) viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.TryGetComponent<Image>(out var background);
            if (background == null) background = viewport.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;

            var fitter = grid.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = Vector2.one;
            grid.pivot = new Vector2(.5f, 1f);
            grid.anchoredPosition = Vector2.zero;
            grid.sizeDelta = Vector2.zero;

            var scroll = viewport.GetComponent<ScrollRect>();
            if (scroll == null) scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BindStaticInputs()
        {
            if (mediumDropdown != null)
            {
                mediumDropdown.onValueChanged.RemoveListener(SelectMedium);
                mediumDropdown.onValueChanged.AddListener(SelectMedium);
            }
            if (catalystDropdown != null)
            {
                catalystDropdown.onValueChanged.RemoveListener(SelectCatalyst);
                catalystDropdown.onValueChanged.AddListener(SelectCatalyst);
            }
            if (customNameInput != null)
            {
                customNameInput.lineType = TMP_InputField.LineType.SingleLine;
                customNameInput.characterLimit = 64;
                customNameInput.onValueChanged.RemoveListener(HandleCustomNameChanged);
                customNameInput.onValueChanged.AddListener(HandleCustomNameChanged);
            }
            if (craftButton != null)
            {
                craftButton.onClick.RemoveListener(TryCraft);
                craftButton.onClick.AddListener(TryCraft);
            }
        }

        private void Refresh()
        {
            if (!isVisible) return;
            RefreshIngredients();
            RefreshSelectedIngredients();
            if (ingredientCountText != null)
                ingredientCountText.text = $"{selectedIngredients.Count}/{AlchemyRuntimeRules.RequiredIngredientCount}";
            RefreshSelectors();
            RefreshPreview();
        }

        private void RefreshIngredients()
        {
            if (ingredientContent == null || ingredientSlotTemplate == null) return;
            var activeIds = new HashSet<string>();
            foreach (var option in craftingService.GetIngredientOptions(alchemist)
                         .Where(option => option?.Material != null)
                         .OrderBy(option => AlchemyRuntimeRules.GetEffectTier(option.Material))
                         .ThenBy(option => option.Material.DisplayName))
            {
                var material = option.Material;
                activeIds.Add(material.Id);
                var slot = GetOrCreateSlot(ingredientSlotTemplate, ingredientContent, material.Id, ingredientSlots);
                slot?.Bind(material.Icon, material.DisplayName, BuildIngredientTooltip(option),
                    CountPartyItems(material.Id).ToString(),
                    selectedIngredients.Any(item => item?.Id == material.Id), false,
                    () => ToggleIngredient(material));
                slot?.SetDefinitionTooltip(material, BuildIngredientTooltip(option));
                slot?.SetSelectionInteractable(selectedIngredients.Any(item => item?.Id == material.Id)
                    || selectedIngredients.Count < AlchemyRuntimeRules.RequiredIngredientCount,
                    ingredientScroll != null ? .65f : .4f);
            }
            HideUnusedSlots(ingredientSlots, activeIds);
        }

        private void RefreshSelectedIngredients()
        {
            for (var index = 0; index < selectedIngredientSlots.Count; index++)
            {
                var slot = selectedIngredientSlots[index];
                if (slot == null) continue;
                var ingredient = index < selectedIngredients.Count ? selectedIngredients[index] : null;
                if (ingredient == null)
                {
                    slot.Bind(null, "Pusty składnik", "Wybierz składnik z listy powyżej.",
                        string.Empty, false, false, null);
                    continue;
                }

                var option = craftingService.GetIngredientOptions(alchemist)
                    .FirstOrDefault(candidate => candidate?.Material?.Id == ingredient.Id);
                var capturedIndex = index;
                slot.Bind(ingredient.Icon, ingredient.DisplayName, BuildIngredientTooltip(option),
                    string.Empty, true, false, () => RemoveIngredient(capturedIndex));
                slot.SetDefinitionTooltip(ingredient, BuildIngredientTooltip(option));
            }
        }

        private void RefreshSelectors()
        {
            RefreshMediumDropdown();
            RefreshCatalystDropdown();
        }

        private void RefreshMediumDropdown()
        {
            if (mediumDropdown == null) return;
            displayedMediums.Clear();
            var hasRecipe = selectedIngredients.Count == AlchemyRuntimeRules.RequiredIngredientCount;
            if (hasRecipe)
            {
                var level = AlchemyRuntimeRules.GetConsumableLevel(selectedIngredients.Max(AlchemyRuntimeRules.GetEffectTier));
                displayedMediums.AddRange(craftingService.GetMediumOptions(alchemist, craftingType)
                    .Where(option => option?.Material != null && AlchemyRuntimeRules.IsMediumCompatible(level, option.Tier))
                    .OrderBy(option => option.Tier).Select(option => option.Material));
            }
            if (!displayedMediums.Any(item => item?.Id == selectedMedium?.Id))
                selectedMedium = ingredientScroll != null ? displayedMediums.FirstOrDefault() : null;

            suppressInputEvents = true;
            mediumDropdown.ClearOptions();
            var mediumLabel = craftingType == ConsumableUseType.Bomb
                ? "Wybierz proch strzelniczy"
                : "Wybierz alkohol";
            var labels = new List<string> { ingredientScroll != null ? "—" : hasRecipe ? mediumLabel : "Wybierz 3 składniki" };
            labels.AddRange(displayedMediums.Select(item => item.DisplayName));
            mediumDropdown.AddOptions(labels);
            mediumDropdown.interactable = hasRecipe && displayedMediums.Count > 0;
            mediumDropdown.SetValueWithoutNotify(selectedMedium == null
                ? 0 : Mathf.Max(0, displayedMediums.FindIndex(item => item.Id == selectedMedium.Id) + 1));
            suppressInputEvents = false;
        }

        private void RefreshCatalystDropdown()
        {
            if (catalystDropdown == null) return;
            displayedCatalysts.Clear();
            displayedCatalysts.AddRange(craftingService.GetCatalystOptions(alchemist)
                .Where(option => option?.Material != null).OrderBy(option => option.Tier)
                .ThenBy(option => option.Material.DisplayName).Select(option => option.Material));
            if (!displayedCatalysts.Any(item => item?.Id == selectedCatalyst?.Id)) selectedCatalyst = null;

            suppressInputEvents = true;
            catalystDropdown.ClearOptions();
            catalystDropdown.AddOptions(new[] { "Brak katalizatora" }
                .Concat(displayedCatalysts.Select(GetDisplayName)).ToList());
            catalystDropdown.interactable = displayedCatalysts.Count > 0;
            catalystDropdown.SetValueWithoutNotify(selectedCatalyst == null
                ? 0 : Mathf.Max(0, displayedCatalysts.FindIndex(item => item.Id == selectedCatalyst.Id) + 1));
            suppressInputEvents = false;
        }

        private string GetDisplayName(ItemDefinition definition)
        {
            if (definition == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(definition.DisplayName)) return definition.DisplayName;

            var catalog = GameBootstrap.Instance?.ContentCatalog;
            if (catalog != null
                && !string.IsNullOrWhiteSpace(definition.Id)
                && catalog.TryGetItem(definition.Id, out var canonical)
                && !string.IsNullOrWhiteSpace(canonical?.DisplayName))
            {
                return canonical.DisplayName;
            }

            return definition.name;
        }

        private void RefreshPreview()
        {
            var request = BuildRequest();
            var preview = craftingService.CreatePreview(session, alchemist, request);
            session.CharactersById.TryGetValue(recipientCharacterId, out var recipient);
            var materialCosts = preview.Materials?.Count > 0
                ? preview.Materials
                : craftingService.GetDraftMaterialCosts(session, alchemist, request);
            // Draft ingredients can be bought before a medium is selected.
            RefreshMaterialRequirements(materialCosts, true);
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(ItemIconPresentation.GetIcon(preview.Result));
                if (preview.Result != null)
                    previewTooltipSlot.SetItemTooltip(preview.Result, recipient);
                else
                    previewTooltipSlot.SetTooltip("Podgląd", string.Empty);
            }
            if (previewNameText != null) previewNameText.text = preview.Result == null ? "—" : ItemRarityPresentation.Name(preview.Result);
            if (previewSummaryText != null)
                previewSummaryText.text = preview.Result == null
                    ? string.Empty
                    : $"Poziom {preview.ResultLevel} • Ładunki: {preview.Result.MaxCharges}"
                        + (craftingType == ConsumableUseType.Bomb
                            ? $"\nZasięg: {preview.Result.BlastRange} • Obszar: {preview.Result.BlastAreaSize}×{preview.Result.BlastAreaSize}"
                            : string.Empty);
            if (previewEffectsText != null)
            {
                previewEffectsText.text = (craftingType == ConsumableUseType.Bomb
                    ? UIWorkspaceTooltipView.BuildBombEffectSummary(preview.Result, recipient)
                    : UIWorkspaceTooltipView.BuildPotionEffectSummary(preview.Result)) ?? string.Empty;
            }
            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft || selectedIngredients.Count < AlchemyRuntimeRules.RequiredIngredientCount
                ? null : materialCosts?.Any(cost => cost.MissingAmount > 0) == true
                    ? "Brak materiałów." : preview.FailureReason);
        }

        private void RefreshMaterialRequirements(IReadOnlyList<AlchemyMaterialCost> materialCosts, bool canPurchase)
        {
            if (materialRequirementsContent == null || materialSlotTemplate == null || materialCosts == null) return;
            var orderedCosts = ingredientScroll != null
                ? materialCosts.OrderBy(cost => MaterialRequirementOrder(cost?.Material)).ToArray()
                : materialCosts;
            var nextKey = string.Join("|", orderedCosts.Where(cost => cost?.Material != null)
                .Select(cost => $"{cost.Material.Id}:{cost.RequiredAmount}"));
            // The scroll belongs to this view's material grid. A new recipe starts
            // at the top; buying ingredients or renaming keeps the current position.
            var scroll = ingredientScroll != null
                ? materialRequirementsContent.GetComponentInParent<ScrollRect>() : null;
            var position = nextKey == materialRequirementKey && scroll != null
                ? scroll.verticalNormalizedPosition : 1f;
            ClearRequirementSlots();
            foreach (var cost in orderedCosts)
            {
                if (cost?.Material == null) continue;
                // Match Blacksmith crafting: stock availability decides whether the
                // purchase affordance is shown; affordability decides only whether it
                // is interactable. This lets the player see what can be bought even
                // when they do not currently have enough gold.
                var canOfferPurchase = canPurchase && cost.HasPurchasableStock && cost.PurchasePrice > 0;
                var slot = Instantiate(materialSlotTemplate, materialRequirementsContent);
                requirementSlots.Add(slot);
                slot.gameObject.SetActive(true);
                slot?.Bind(cost.Material.Icon, cost.Material.DisplayName, BuildMaterialDescription(cost),
                    $"{cost.OwnedAmount}/{cost.RequiredAmount}", cost.MissingAmount <= 0, cost.MissingAmount > 0,
                    null, canOfferPurchase ? cost.PurchasePrice.ToString("N0") : null,
                    canOfferPurchase ? () => TryBuyMissingMaterial(cost.Material.Id) : null,
                    cost.PurchaseState, cost.PurchaseState == CraftingMaterialPurchaseState.Available);
                slot?.SetDefinitionTooltip(cost.Material, BuildMaterialDescription(cost),
                    cost.PurchasePrice > 0 ? cost.PurchasePrice : (int?)null);
            }
            if (scroll != null && materialRequirementsContent is RectTransform content)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = position;
            }
            materialRequirementKey = nextKey;
        }

        private int MaterialRequirementOrder(ItemDefinition material)
        {
            var index = selectedIngredients.FindIndex(ingredient => ingredient?.Id == material?.Id);
            if (index >= 0) return index;
            if (material?.Id == selectedMedium?.Id) return selectedIngredients.Count;
            if (material?.Id == selectedCatalyst?.Id) return selectedIngredients.Count + 1;
            return int.MaxValue;
        }

        private void ToggleIngredient(ItemDefinition material)
        {
            if (!isVisible || material == null) return;
            var index = selectedIngredients.FindIndex(item => item?.Id == material.Id);
            if (index >= 0) selectedIngredients.RemoveAt(index);
            else if (selectedIngredients.Count < AlchemyRuntimeRules.RequiredIngredientCount) selectedIngredients.Add(material);
            Refresh();
        }

        private void RemoveIngredient(int index)
        {
            if (index < 0 || index >= selectedIngredients.Count) return;
            selectedIngredients.RemoveAt(index);
            Refresh();
        }

        private void SelectMedium(int value)
        {
            if (suppressInputEvents || !isVisible) return;
            selectedMedium = value > 0 && value <= displayedMediums.Count ? displayedMediums[value - 1] : null;
            RefreshPreview();
        }

        private void SelectCatalyst(int value)
        {
            if (suppressInputEvents || !isVisible) return;
            selectedCatalyst = value > 0 && value <= displayedCatalysts.Count ? displayedCatalysts[value - 1] : null;
            RefreshPreview();
        }

        private void HandleCustomNameChanged(string value)
        {
            if (suppressInputEvents || !isVisible) return;
            customName = value?.Trim() ?? string.Empty;
            RefreshPreview();
        }

        private void TryCraft()
        {
            if (!isVisible) return;
            if (!craftingService.TryCraft(session, alchemist, BuildRequest(), out var items, out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }
            SetStatus(null);
            if (ingredientScroll == null)
            {
                selectedIngredients.Clear(); selectedMedium = null; selectedCatalyst = null; customName = string.Empty;
                SetCustomNameInput(string.Empty);
            }
            Refresh();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void TryBuyMissingMaterial(string materialId)
        {
            if (!isVisible) return;
            if (!craftingService.TryBuyMissingMaterial(session, alchemist, BuildRequest(), materialId, out _, out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }
            Refresh();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private AlchemyCraftRequest BuildRequest() => new()
        {
            UseType = craftingType, Ingredients = new List<ItemDefinition>(selectedIngredients), Medium = selectedMedium,
            Catalyst = selectedCatalyst, CustomName = customName, RecipientCharacterId = recipientCharacterId
        };

        private void ClearRuntimeSlots()
        {
            ClearSlots(ingredientSlots);
            ClearRequirementSlots();
        }

        private void ClearRequirementSlots()
        {
            foreach (var slot in requirementSlots.Where(slot => slot != null)) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
            requirementSlots.Clear();
        }

        private static void ClearSlots(IDictionary<string, BlacksmithCraftingSlotView> slots)
        {
            foreach (var slot in slots.Values.Where(slot => slot != null)) Destroy(slot.gameObject);
            slots.Clear();
        }

        private void HideTemplates()
        {
            if (ingredientSlotTemplate != null) ingredientSlotTemplate.gameObject.SetActive(false);
            if (materialSlotTemplate != null) materialSlotTemplate.gameObject.SetActive(false);
        }

        private static BlacksmithCraftingSlotView GetOrCreateSlot(BlacksmithCraftingSlotView template, Transform parent,
            string key, IDictionary<string, BlacksmithCraftingSlotView> cache)
        {
            if (template == null || parent == null || string.IsNullOrWhiteSpace(key)) return null;
            if (!cache.TryGetValue(key, out var slot) || slot == null)
            {
                slot = Instantiate(template, parent);
                cache[key] = slot;
            }
            else if (slot.transform.parent != parent) slot.transform.SetParent(parent, false);
            slot.gameObject.SetActive(true);
            return slot;
        }

        private static void HideUnusedSlots(IReadOnlyDictionary<string, BlacksmithCraftingSlotView> slots, ISet<string> activeIds)
        {
            foreach (var pair in slots)
                if (pair.Value != null && !activeIds.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
        }

        private int CountPartyItems(string definitionId)
        {
            if (session == null || string.IsNullOrWhiteSpace(definitionId)) return 0;
            return session.PlayerParty.ActiveMemberIds.Sum(characterId => session.CharactersById.TryGetValue(characterId, out var character)
                ? character.Inventory?.Items?.Count(item => item?.Definition?.Id == definitionId) ?? 0 : 0);
        }

        private void SetCustomNameInput(string value)
        {
            if (customNameInput == null) return;
            suppressInputEvents = true;
            customNameInput.SetTextWithoutNotify(value ?? string.Empty);
            suppressInputEvents = false;
        }

        private void SetStatus(string value)
        {
            if (statusText == null) return;
            statusText.text = value ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private string BuildIngredientTooltip(AlchemyIngredientData option)
        {
            var role = alchemist?.Definition?.GetRole<Godmist.Data.NPC.AlchemistRoleData>();
            var definition = craftingType == ConsumableUseType.Bomb ? role?.BombBaseDefinition : role?.PotionBaseDefinition;
            return UIWorkspaceTooltipView.BuildAlchemyIngredientEffects(option?.GetEffect(craftingType), definition)
                + $"\nPoziom: {AlchemyRuntimeRules.GetConsumableLevel(AlchemyRuntimeRules.GetEffectTier(option?.Material))}";
        }

        private static string BuildMaterialDescription(AlchemyMaterialCost cost)
        {
            if (!string.IsNullOrWhiteSpace(cost.PurchaseFailureReason)) return cost.PurchaseFailureReason;
            if (cost.MissingAmount <= 0) return "Wymaganie spełnione.";
            return cost.HasPurchasableStock ? $"Brakuje: {cost.MissingAmount}. Materiał dostępny u Alchemika."
                : $"Brakuje: {cost.MissingAmount}. Alchemik nie ma zapasu.";
        }

    }
}
