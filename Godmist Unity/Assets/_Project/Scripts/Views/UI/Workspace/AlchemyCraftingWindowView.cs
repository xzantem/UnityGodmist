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
            customName = string.Empty;
            isVisible = true;
            serviceRoot?.SetActive(true);
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
            RefreshSelectors();
            RefreshPreview();
        }

        private void RefreshIngredients()
        {
            if (ingredientContent == null || ingredientSlotTemplate == null) return;
            var activeIds = new HashSet<string>();
            foreach (var option in craftingService.GetIngredientOptions(alchemist)
                         .Where(option => option?.Material != null)
                         .OrderBy(option => option.Material.DisplayName))
            {
                var material = option.Material;
                activeIds.Add(material.Id);
                var slot = GetOrCreateSlot(ingredientSlotTemplate, ingredientContent, material.Id, ingredientSlots);
                slot?.Bind(material.Icon, material.DisplayName, BuildIngredientTooltip(option),
                    $"x{CountPartyItems(material.Id)}",
                    selectedIngredients.Any(item => item?.Id == material.Id), false,
                    () => ToggleIngredient(material));
                slot?.SetDefinitionTooltip(material, BuildIngredientTooltip(option));
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
            if (!displayedMediums.Any(item => item?.Id == selectedMedium?.Id)) selectedMedium = null;

            suppressInputEvents = true;
            mediumDropdown.ClearOptions();
            var mediumLabel = craftingType == ConsumableUseType.Bomb
                ? "Wybierz proch strzelniczy"
                : "Wybierz alkohol";
            var labels = new List<string> { hasRecipe ? mediumLabel : "Wybierz 3 składniki" };
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
            var materialCosts = preview.Materials?.Count > 0
                ? preview.Materials
                : craftingService.GetDraftMaterialCosts(session, alchemist, request);
            // Draft ingredients can be bought before a medium is selected.
            RefreshMaterialRequirements(materialCosts, true);
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(preview.Result?.Definition?.Icon);
                if (preview.Result != null)
                    previewTooltipSlot.SetItemTooltip(preview.Result, null);
                else
                    previewTooltipSlot.SetTooltip(
                        "Wynik craftingu",
                        "Wybierz trzy różne składniki i właściwy alkohol lub proch.");
            }
            if (previewNameText != null) previewNameText.text = preview.Result?.DisplayName ?? "Wybierz składniki";
            if (previewSummaryText != null)
                previewSummaryText.text = preview.Result == null
                    ? $"Wybierz {AlchemyRuntimeRules.RequiredIngredientCount} różne składniki."
                    : $"Poziom {preview.ResultLevel} • tier efektu {preview.ResultEffectTier} • ładunki {preview.Result.MaxCharges}";
            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? "Gotowe do wytworzenia." : preview.FailureReason);
        }

        private void RefreshMaterialRequirements(IReadOnlyList<AlchemyMaterialCost> materialCosts, bool canPurchase)
        {
            if (materialRequirementsContent == null || materialSlotTemplate == null || materialCosts == null) return;
            ClearRequirementSlots();
            foreach (var cost in materialCosts)
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
                    null, canOfferPurchase ? $"{cost.PurchasableAmount}× • {cost.PurchasePrice}g" : null,
                    canOfferPurchase ? () => TryBuyMissingMaterial(cost.Material.Id) : null,
                    cost.PurchaseState, cost.PurchaseState == CraftingMaterialPurchaseState.Available);
                slot?.SetDefinitionTooltip(cost.Material, BuildMaterialDescription(cost),
                    cost.PurchasePrice > 0 ? cost.PurchasePrice : (int?)null);
            }
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
            SetStatus($"Wytworzono: {items[0].DisplayName}.");
            selectedIngredients.Clear(); selectedMedium = null; selectedCatalyst = null; customName = string.Empty;
            SetCustomNameInput(string.Empty);
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
            foreach (var slot in requirementSlots.Where(slot => slot != null)) Destroy(slot.gameObject);
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
            if (statusText != null) statusText.text = value ?? string.Empty;
        }

        private string BuildIngredientTooltip(AlchemyIngredientData option)
        {
            var effect = option?.GetEffect(craftingType);
            if (effect == null) return "Brak skonfigurowanego efektu.";
            var duration = effect.DurationTurns > 0 ? $" przez {effect.DurationTurns} tur" : string.Empty;
            return $"{FormatEffectType(effect.EffectType)}: {FormatEffectValue(effect.Value)}{duration}\nTier efektu: {AlchemyRuntimeRules.GetEffectTier(option.Material)}";
        }

        private static string BuildMaterialDescription(AlchemyMaterialCost cost)
        {
            if (!string.IsNullOrWhiteSpace(cost.PurchaseFailureReason)) return cost.PurchaseFailureReason;
            if (cost.MissingAmount <= 0) return "Wymaganie spełnione.";
            return cost.HasPurchasableStock ? $"Brakuje: {cost.MissingAmount}. Materiał dostępny u Alchemika."
                : $"Brakuje: {cost.MissingAmount}. Alchemik nie ma zapasu.";
        }

        private static string FormatEffectType(ConsumableRuntimeEffectType effectType) => effectType switch
        {
            ConsumableRuntimeEffectType.RestoreHealth => "Odzyskanie HP",
            ConsumableRuntimeEffectType.RegenHealth => "Regeneracja HP",
            ConsumableRuntimeEffectType.DealPhysicalDamage => "Obrażenia fizyczne",
            ConsumableRuntimeEffectType.DealMagicalDamage => "Obrażenia magiczne",
            _ => effectType.ToString()
        };

        private static string FormatEffectValue(float value) => $"{value * 100f:0.##}%";

    }
}
