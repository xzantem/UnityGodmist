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
    /// <summary>
    /// Content view for the Enchanter's galduryte production service. The parent
    /// NPC services window owns navigation and closing; identification has its
    /// own sibling content view and tab.
    /// </summary>
    public sealed class GalduryteCraftingWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform stoneGrid;
        [SerializeField] private Transform powderGrid;
        [SerializeField] private Transform materialsGrid;
        [SerializeField] private BlacksmithCraftingSlotView stoneSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView powderSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView materialSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI previewEffectsText;
        [SerializeField] private ScrollRect powderScroll;
        [SerializeField] private ScrollRect effectsScroll;
        [SerializeField] private LayoutElement effectsSectionLayout;
        [SerializeField] private Button craftButton;
        [SerializeField] private Button weaponButton;
        [SerializeField] private Button armorButton;
        [SerializeField] private TextMeshProUGUI weaponLabel;
        [SerializeField] private TextMeshProUGUI armorLabel;

        private readonly GalduryteCraftingService craftingService = new();
        private readonly ItemInstanceFactory itemInstanceFactory = new();
        private readonly List<BlacksmithCraftingSlotView> stoneSlots = new();
        private readonly List<BlacksmithCraftingSlotView> powderSlots = new();
        private readonly List<BlacksmithCraftingSlotView> materialSlots = new();
        private GameSession session;
        private NpcInstance enchanter;
        private string recipientCharacterId = string.Empty;
        private ItemDefinition selectedStone;
        private ItemDefinition selectedPowder;
        private GalduryteTargetType targetType = GalduryteTargetType.Weapon;
        private bool isVisible;

        private void Awake()
        {
            craftButton?.onClick.AddListener(TryCraft);
            weaponButton?.onClick.AddListener(SelectWeapon);
            armorButton?.onClick.AddListener(SelectArmor);
            HideTemplates();
        }

        private void LateUpdate()
        {
            if (!isVisible || effectsSectionLayout == null || effectsScroll == null
                || previewEffectsText == null) return;

            var column = effectsSectionLayout.transform.parent as RectTransform;
            var layout = column != null ? column.GetComponent<VerticalLayoutGroup>() : null;
            if (layout == null || effectsScroll.viewport.rect.width <= 0) return;

            // Give the description its preferred height, keeping room for costs and the action.
            // A small window can still scroll descriptions that exceed the remaining space.
            var availableHeight = column.rect.height - layout.padding.vertical;
            var childCount = 0;
            foreach (Transform child in column)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                childCount++;
                if (child != effectsSectionLayout.transform)
                    availableHeight -= LayoutUtility.GetMinHeight((RectTransform)child);
            }
            availableHeight -= layout.spacing * Mathf.Max(0, childCount - 1);
            var minimum = effectsSectionLayout.minHeight;
            var desiredHeight = Mathf.Ceil(previewEffectsText.GetPreferredValues(
                previewEffectsText.text, effectsScroll.viewport.rect.width, Mathf.Infinity).y) + 36f;
            var height = Mathf.Clamp(desiredHeight, minimum, Mathf.Max(minimum, availableHeight));
            if (!Mathf.Approximately(effectsSectionLayout.preferredHeight, height))
                effectsSectionLayout.preferredHeight = height;
        }

        private void OnDestroy()
        {
            craftButton?.onClick.RemoveListener(TryCraft);
            weaponButton?.onClick.RemoveListener(SelectWeapon);
            armorButton?.onClick.RemoveListener(SelectArmor);
        }

        private void OnDisable()
        {
            isVisible = false;
            ClearSlots(stoneSlots);
            ClearSlots(powderSlots);
            ClearSlots(materialSlots);
        }

        public bool Show(NpcInstance nextEnchanter, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextEnchanter == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId))
            {
                return false;
            }

            enchanter = nextEnchanter;
            recipientCharacterId = nextRecipientCharacterId;
            isVisible = true;
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateStoneOptions();
            PopulatePowderOptions();
            RefreshCraftPreview();
            RefreshTargetButtons();
            return true;
        }

        public void Hide()
        {
            isVisible = false;
            serviceRoot?.SetActive(false);
        }

        private void PopulateStoneOptions()
        {
            ClearSlots(stoneSlots);
            var options = enchanter?.Definition?.GetRole<EnchanterRoleData>()?.GalduryteStones
                ?.Where(option => option?.Material != null)
                .OrderBy(option => option.Tier)
                .ToList() ?? new List<GalduryteStoneData>();
            if (selectedStone == null || options.All(option => option.Material.Id != selectedStone.Id))
                selectedStone = options.FirstOrDefault()?.Material;

            foreach (var option in options)
            {
                var captured = option.Material;
                var slot = CreateSlot(
                    stoneSlotTemplate,
                    stoneGrid,
                    captured.Icon,
                    captured.DisplayName,
                    BuildStoneDescription(option.Tier),
                    string.Empty,
                    selectedStone?.Id == captured.Id,
                    false,
                    () =>
                    {
                        selectedStone = captured;
                        PopulateStoneOptions();
                        RefreshCraftPreview();
                    });
                slot?.SetRarity(captured.BaseRarity);
                if (slot != null) stoneSlots.Add(slot);
            }

            ConfigureGrid(
                stoneGrid,
                options.Count);
        }

        private void PopulatePowderOptions()
        {
            ClearSlots(powderSlots);
            var options = enchanter?.Definition?.GetRole<EnchanterRoleData>()?.GaldurytePowders
                ?.Where(option => option?.Material != null)
                .OrderBy(option => option.Color)
                .ToList() ?? new List<GaldurytePowderData>();
            if (selectedPowder == null || options.All(option => option.Material.Id != selectedPowder.Id))
                selectedPowder = options.FirstOrDefault()?.Material;

            foreach (var option in options)
            {
                var captured = option.Material;
                var slot = CreateSlot(
                    powderSlotTemplate,
                    powderGrid,
                    captured.Icon,
                    captured.DisplayName,
                    BuildPowderTooltip(option.Color),
                    string.Empty,
                    selectedPowder?.Id == captured.Id,
                    false,
                    () =>
                    {
                        var position = powderScroll != null ? powderScroll.verticalNormalizedPosition : 1f;
                        selectedPowder = captured;
                        PopulatePowderOptions();
                        Canvas.ForceUpdateCanvases();
                        if (powderScroll != null) powderScroll.verticalNormalizedPosition = position;
                        RefreshCraftPreview();
                    });
                slot?.SetRarity(captured.BaseRarity);
                if (slot != null) powderSlots.Add(slot);
            }

            ConfigureGrid(
                powderGrid,
                options.Count);
        }

        private void RefreshCraftPreview()
        {
            var preview = craftingService.CreateCraftPreview(
                session,
                enchanter,
                new GalduryteCraftRequest
                {
                    Stone = selectedStone,
                    Powder = selectedPowder,
                    TargetType = targetType,
                    RecipientCharacterId = recipientCharacterId
                });

            PopulateMaterials(preview);
            if (previewTooltipSlot != null)
            {
                if (preview.Result != null)
                {
                    previewTooltipSlot.SetIcon(ItemIconPresentation.GetIcon(preview.Result));
                    previewTooltipSlot.SetTooltip(
                        ItemRarityPresentation.Name(preview.Result),
                        BuildPreviewTooltip(preview.Result));
                    previewTooltipSlot.SetRarity(preview.Result.CurrentRarity);
                }
                else
                {
                    previewTooltipSlot.SetIcon(null);
                    previewTooltipSlot.SetTooltip(
                        "Wynik galdurytu",
                        string.IsNullOrWhiteSpace(preview.FailureReason)
                            ? "Wybierz kamień i proszek."
                            : preview.FailureReason);
                }
            }

            if (previewNameText != null)
                previewNameText.text = preview.Result == null ? "Wynik galdurytu" : ItemRarityPresentation.Name(preview.Result);
            if (previewSummaryText != null)
                previewSummaryText.text = preview.Result == null
                    ? string.IsNullOrWhiteSpace(preview.FailureReason)
                        ? "Wybierz kamień i proszek."
                        : preview.FailureReason
                    : BuildPreviewSummary(preview.Result);
            if (previewEffectsText != null)
            {
                var effects = BuildPossibleEffects(preview.Result);
                if (previewEffectsText.text != effects && effectsScroll != null)
                {
                    effectsScroll.StopMovement();
                    effectsScroll.verticalNormalizedPosition = 1f;
                }
                previewEffectsText.text = effects;
            }
            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? string.Empty : preview.Materials.Any(cost => cost.MissingAmount > 0) ? "Brak materiałów." : preview.FailureReason);
        }

        private void PopulateMaterials(GalduryteCraftPreview preview)
        {
            ClearSlots(materialSlots);
            if (preview?.Materials == null || materialsGrid == null) return;

            foreach (var material in preview.Materials)
            {
                if (material?.Material == null) continue;
                var materialId = material.Material.Id;
                var canBuy = material.HasPurchasableStock && material.PurchasePrice > 0;
                var slot = CreateSlot(
                    materialSlotTemplate,
                    materialsGrid,
                    material.Material.Icon,
                    material.Material.DisplayName,
                    string.Empty,
                    $"{material.OwnedAmount}/{material.RequiredAmount}",
                    false,
                    material.MissingAmount > 0,
                    null,
                    canBuy ? material.PurchasePrice.ToString("N0") : null,
                    canBuy ? () => BuyMaterial(materialId) : null,
                    material.PurchaseState,
                    material.PurchaseState == CraftingMaterialPurchaseState.Available);
                if (slot != null)
                {
                    slot.SetItemTooltip(
                        itemInstanceFactory.Create(material.Material),
                        null,
                        BuildMaterialContextTooltip(material),
                        material.PurchasePrice > 0 ? material.PurchasePrice : (int?)null);
                    materialSlots.Add(slot);
                }
            }

            ConfigureGrid(
                materialsGrid,
                materialSlots.Count);
        }

        private void BuyMaterial(string materialId)
        {
            if (!craftingService.TryBuyMissingMaterial(
                    session,
                    enchanter,
                    BuildRequest(),
                    materialId,
                    out var paidGold,
                    out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            RefreshCraftPreview();
        }

        private void TryCraft()
        {
            if (!isVisible) return;
            if (!craftingService.TryCraft(
                    session,
                    enchanter,
                    new GalduryteCraftRequest
                    {
                        Stone = selectedStone,
                        Powder = selectedPowder,
                        TargetType = targetType,
                        RecipientCharacterId = recipientCharacterId
                    },
                    out var crafted,
                    out var failureReason))
            {
                SetStatus(failureReason);
                return;
            }

            RefreshCraftPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void SelectWeapon()
        {
            targetType = GalduryteTargetType.Weapon;
            RefreshTargetButtons();
            PopulatePowderOptions();
            RefreshCraftPreview();
        }

        private void SelectArmor()
        {
            targetType = GalduryteTargetType.Armor;
            RefreshTargetButtons();
            PopulatePowderOptions();
            RefreshCraftPreview();
        }

        private GalduryteCraftRequest BuildRequest() => new()
        {
            Stone = selectedStone,
            Powder = selectedPowder,
            TargetType = targetType,
            RecipientCharacterId = recipientCharacterId
        };

        private void RefreshTargetButtons()
        {
            if (weaponLabel != null) { weaponLabel.text = "Broń"; weaponLabel.color = targetType == GalduryteTargetType.Weapon ? new Color32(226,184,78,255) : new Color32(223,215,184,255); }
            if (armorLabel != null) { armorLabel.text = "Pancerz"; armorLabel.color = targetType == GalduryteTargetType.Armor ? new Color32(226,184,78,255) : new Color32(223,215,184,255); }
            if (weaponButton != null) weaponButton.interactable = true;
            if (armorButton != null) armorButton.interactable = true;
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
            slot.Bind(
                icon,
                title,
                description,
                countLabel,
                selected,
                missing,
                selectAction,
                buyLabel,
                buyAction,
                purchaseState,
                buyInteractable);
            return slot;
        }

        private static void ClearSlots(List<BlacksmithCraftingSlotView> slots)
        {
            foreach (var slot in slots.Where(slot => slot != null)) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
            slots.Clear();
        }

        private void HideTemplates()
        {
            stoneSlotTemplate?.gameObject.SetActive(false);
            powderSlotTemplate?.gameObject.SetActive(false);
            materialSlotTemplate?.gameObject.SetActive(false);
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private static string BuildStoneDescription(GalduryteTier tier)
        {
            var level = GalduryteRuntimeRules.GetLevel(tier);
            return GalduryteRuntimeRules.TryGetEffectCountRange(level, out var minimum, out var maximum)
                ? $"Poziom {level} • {minimum}–{maximum} efektów"
                : $"Poziom {level}";
        }

        private string BuildPowderTooltip(GaldurytePowderColor color)
        {
            var effects = GalduryteRuntimeRules.GetEffectPool(targetType)
                .Where(effect => effect.PowderColor == color)
                .Select(effect =>
                {
                    var minimum = GalduryteRuntimeRules.GetEffectValue(
                        effect.EffectType, GalduryteEffectGrade.D);
                    var maximum = GalduryteRuntimeRules.GetEffectValue(
                        effect.EffectType, GalduryteEffectGrade.S);
                    return "• " + GalduryteRuntimeText.FormatEffectRange(
                        effect.EffectType, minimum, maximum);
                })
                .ToList();

            return effects.Count == 0
                ? "Gwarantuje jeden z: brak skonfigurowanych efektów."
                : "Gwarantuje jeden z:\n" + string.Join("\n", effects);
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

        public static string BuildPossibleEffects(GalduryteItemInstance result)
        {
            if (result == null) return string.Empty;
            var effects = GalduryteRuntimeRules.GetEffectPool(result.TargetType)
                .Where(effect => effect.PowderColor == result.GuaranteedPowderColor)
                .Select(effect => "• " + GalduryteRuntimeText.FormatEffectRange(effect.EffectType,
                    GalduryteRuntimeRules.GetEffectValue(effect.EffectType, GalduryteEffectGrade.D),
                    GalduryteRuntimeRules.GetEffectValue(effect.EffectType, GalduryteEffectGrade.S)));
            return "Jeden z efektów proszku:\n" + string.Join("\n", effects)
                + "\n\nPozostałe efekty: losowe z puli "
                + (result.TargetType == GalduryteTargetType.Weapon ? "broni." : "pancerza.");
        }

        private static string BuildPreviewSummary(GalduryteItemInstance result)
        {
            if (result == null) return string.Empty;
            return $"{FormatTarget(result.TargetType)} • Poziom {result.ItemLevel}\n"
                + $"Rzadkość: losowana • Efekty: {GetEffectCountRange(result.ItemLevel)}";
        }

        private static string BuildPreviewTooltip(GalduryteItemInstance result) =>
            $"Typ: {FormatTarget(result.TargetType)}\n"
            + $"Poziom galdurytu: {result.ItemLevel}\n"
            + "Rzadkość: losowana\n"
            + $"Efekty: {GetEffectCountRange(result.ItemLevel)}\n\n" + BuildPossibleEffects(result);

        private static string GetEffectCountRange(int level) =>
            GalduryteRuntimeRules.TryGetEffectCountRange(level, out var minimum, out var maximum)
                ? minimum == maximum ? minimum.ToString() : $"{minimum}–{maximum}"
                : "nieznana liczba";

        private static string FormatTarget(GalduryteTargetType targetType) => targetType switch
        {
            GalduryteTargetType.Weapon => "Broń",
            GalduryteTargetType.Armor => "Pancerz",
            _ => "Przedmiot"
        };

        private static void ConfigureGrid(
            Transform gridTransform,
            int itemCount)
        {
            if (gridTransform == null) return;
            var grid = gridTransform.GetComponent<GridLayoutGroup>();
            if (grid == null) return;

            // Cell size, alignment, spacing and constraint mode are serialized in
            // the prefab. Only the number of columns depends on the current data.
            grid.constraintCount = Mathf.Clamp(itemCount, 1, 5);
        }
    }
}
