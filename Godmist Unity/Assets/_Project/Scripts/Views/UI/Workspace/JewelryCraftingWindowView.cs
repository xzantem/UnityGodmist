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
            ConfigureJewelryLayout();
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
            ConfigureJewelryLayout();
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
                        PopulateJewelry();
                    });
            }
            ConfigureJewelrySectionHeight(offers.Count);
            PopulateIngredients();
        }

        private void PopulateIngredients()
        {
            ClearSlots(metalContent);
            ClearSlots(gemContent);
            if (selectedDefinition == null) { RefreshPreview(); return; }
            var metals = selectedDefinition.Metals?.Where(value => value != null).ToList() ?? new List<JewelryMetalData>();
            var gems = selectedDefinition.Gems?.Where(value => value != null && value.CraftingMaterial != null).ToList() ?? new List<JewelryGemDefinition>();
            if (!metals.Contains(selectedMetal)) selectedMetal = metals.FirstOrDefault();
            if (!gems.Contains(selectedGem)) selectedGem = gems.FirstOrDefault();
            foreach (var metal in metals)
            {
                CreateSlot(ingredientSlotTemplate, metalContent, null, JewelryGenerationService.GetMetalDisplayName(metal),
                    BuildMetalTooltip(metal), string.Empty, ReferenceEquals(metal, selectedMetal), false,
                    () => { selectedMetal = metal; PopulateIngredients(); },
                    tooltipMeta: UIWorkspaceTooltipView.GetRarityDisplayName(
                        JewelryGenerationService.GetMaterialRarity(metal.Material)));
            }
            foreach (var gem in gems)
            {
                CreateSlot(ingredientSlotTemplate, gemContent, gem.CraftingMaterial?.Icon, gem.CraftingMaterial?.DisplayName,
                    BuildGemTooltip(gem), string.Empty,
                    ReferenceEquals(gem, selectedGem), false,
                    () => { selectedGem = gem; PopulateIngredients(); },
                    tooltipMeta: UIWorkspaceTooltipView.GetRarityDisplayName(
                        gem.CraftingMaterial?.BaseRarity ?? RarityTier.Common));
            }
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            ClearSlots(materialsContent);
            var preview = service.CreatePreview(session, enchanter, BuildRequest());
            foreach (var material in preview.Materials ?? Array.Empty<CraftingMaterialCost>())
            {
                var id = material.Material?.Id;
                var canBuy = material.HasPurchasableStock && material.PurchasePrice > 0;
                var materialSlot = CreateSlot(materialSlotTemplate, materialsContent, material.Material?.Icon,
                    material.Material?.DisplayName, string.Empty, $"{material.OwnedAmount}/{material.RequiredAmount}", false,
                    material.MissingAmount > 0, null,
                    canBuy ? $"{material.PurchasableAmount}× • {material.PurchasePrice}g" : null,
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
                previewTooltipSlot.SetIcon(preview.Result?.Definition?.Icon);
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
                : $"{jewelryDefinition?.JewelryTypeDisplayName ?? "Biżuteria"} • Poziom {preview.Result.ItemLevel}");
            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? "Gotowe do craftingu." : preview.FailureReason);
        }

        private void BuyMaterial(string id)
        {
            if (!service.TryBuyMissingMaterial(session, enchanter, BuildRequest(), id, out var paid, out var failure))
            {
                SetStatus(failure); return;
            }
            SetStatus($"Kupiono materiały za {paid}.");
            RefreshPreview();
        }

        private void TryCraft()
        {
            if (!service.TryCraft(session, enchanter, BuildRequest(), out var item, out var failure))
            {
                SetStatus(failure); return;
            }
            SetStatus($"Wytworzono: {item.DisplayName}.");
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
                Destroy(slot.gameObject);
            }
        }

        private void HideTemplates()
        {
            if (jewelrySlotTemplate != null) jewelrySlotTemplate.gameObject.SetActive(false);
            if (ingredientSlotTemplate != null) ingredientSlotTemplate.gameObject.SetActive(false);
            if (materialSlotTemplate != null) materialSlotTemplate.gameObject.SetActive(false);
        }

        private void ConfigureJewelryLayout()
        {
            var root = serviceRoot != null ? serviceRoot.transform : transform;
            SetText(root.Find("AssemblyColumn/BaseSection/SectionTitle"), "1. Wybierz biżuterię");
            SetText(root.Find("AssemblyColumn/PartsSection/SectionTitle"), "2. Wybierz materiały");
            SetText(root.Find("AssemblyColumn/SettingsSection/SectionTitle"), "3. Nazwa");
            SetText(root.Find("AssemblyColumn/PartsSection/Content/PartColumns/WeaponHeadStage/StageLabel"), "Wybierz metal");
            SetText(root.Find("AssemblyColumn/PartsSection/Content/PartColumns/WeaponBinderStage/StageLabel"), "Wybierz klejnot");

            foreach (var stageName in new[]
            {
                "WeaponHandleStage", "ArmorPlateStage", "ArmorBinderStage", "ArmorBaseStage"
            })
            {
                root.Find($"AssemblyColumn/PartsSection/Content/PartColumns/{stageName}")?.gameObject.SetActive(false);
            }

            root.Find("AssemblyColumn/SettingsSection/Content/QualityDropdown")?.gameObject.SetActive(false);

            if (jewelryContent != null && jewelryContent.TryGetComponent<GridLayoutGroup>(out var jewelryGrid))
            {
                jewelryGrid.padding = new RectOffset(0, 0, 0, 0);
                jewelryGrid.spacing = new Vector2(8f, 8f);
                jewelryGrid.childAlignment = TextAnchor.UpperCenter;
                jewelryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                jewelryGrid.constraintCount = 4;
            }

            ConfigureJewelrySectionHeight(1);

            ConfigurePreviewTextLayout();
        }

        private void ConfigureJewelrySectionHeight(int itemCount)
        {
            if (jewelryContent == null) return;

            var grid = jewelryContent.GetComponent<GridLayoutGroup>();
            var section = jewelryContent.parent?.parent as RectTransform;
            var sectionLayout = section?.GetComponent<LayoutElement>();
            if (grid == null || sectionLayout == null) return;

            var columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : Mathf.Max(1, itemCount);
            var rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, itemCount) / (float)columns));
            var gridHeight = grid.padding.vertical
                + rows * grid.cellSize.y
                + Mathf.Max(0, rows - 1) * grid.spacing.y;

            // BaseSection's content rect has a fixed 52 px vertical inset. Keep
            // only a compact title/content gap and a small bottom breathing room
            // instead of retaining the equipment panel's 256 px height.
            sectionLayout.minHeight = -1f;
            sectionLayout.preferredHeight = Mathf.Ceil(gridHeight + 64f);
            LayoutRebuilder.MarkLayoutForRebuild(section);
        }

        private void ConfigurePreviewTextLayout()
        {
            if (previewNameText == null) return;

            previewNameText.textWrappingMode = TextWrappingModes.Normal;
            previewNameText.overflowMode = TextOverflowModes.Overflow;
            previewNameText.alignment = TextAlignmentOptions.Left;

            var nameLayout = previewNameText.GetComponent<LayoutElement>();
            if (nameLayout != null)
            {
                nameLayout.minHeight = -1;
                nameLayout.preferredHeight = -1;
            }

            var resultInfo = previewNameText.transform.parent as RectTransform;
            if (resultInfo == null) return;

            var resultInfoFitter = resultInfo.GetComponent<ContentSizeFitter>()
                ?? resultInfo.gameObject.AddComponent<ContentSizeFitter>();
            resultInfoFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var resultHeader = resultInfo.parent as RectTransform;
            if (resultHeader == null) return;

            var resultHeaderLayout = resultHeader.GetComponent<LayoutElement>()
                ?? resultHeader.gameObject.AddComponent<LayoutElement>();
            resultHeaderLayout.minHeight = 80f;
            resultHeaderLayout.preferredHeight = -1f;
        }

        private static void SetText(Transform target, string value)
        {
            var label = target?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = value;
        }

        private void SetStatus(string value) { if (statusText != null) statusText.text = value ?? string.Empty; }
        private static void SetLabel(TextMeshProUGUI label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
        private static string BuildMetalTooltip(JewelryMetalData metal) => metal == null ? string.Empty
            : $"Poziom wynikowego przedmiotu: {JewelryGenerationService.GetItemLevel(metal)}\n"
                + UIWorkspaceTooltipView.BuildJewelryIngredientEffects(metal.Modifiers);
        private static string BuildGemTooltip(JewelryGemDefinition gem)
        {
            var effects = UIWorkspaceTooltipView.BuildJewelryIngredientEffects(gem?.Modifiers);
            if (string.IsNullOrWhiteSpace(effects)) return "Po osadzeniu: brak dodatkowych efektów";

            return string.Join(
                "\n",
                effects.Split('\n')
                    .Where(effect => !string.IsNullOrWhiteSpace(effect))
                    .Select(effect => $"Po osadzeniu: {effect}"));
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
