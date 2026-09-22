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
    [Serializable]
    public sealed class CraftingPartSelectionView
    {
        public EquipmentPartKind Kind;
        public GameObject Root;
        public Transform Content;
    }

    public sealed class BlacksmithCraftingWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TMP_InputField customNameInput;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Transform equipmentContent;
        [SerializeField] private Transform materialsContent;
        [SerializeField] private BlacksmithCraftingSlotView equipmentSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView partSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView materialSlotTemplate;
        [SerializeField] private TextMeshProUGUI previewText;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button craftButton;
        [SerializeField] private CraftingPartSelectionView[] partSelections;

        private readonly BlacksmithCraftingService craftingService = new();
        private readonly List<BlacksmithCraftingSlotView> spawnedSlots = new();
        private GameSession session;
        private NpcInstance blacksmith;
        private string recipientCharacterId = string.Empty;
        private EquipmentItemDefinition selectedDefinition;
        private EquipmentQuality selectedQuality = EquipmentQuality.Normal;
        private readonly Dictionary<EquipmentPartKind, EquipmentPartDefinition> selectedParts = new();

        private void Awake()
        {
            HideTemplates();
            if (craftButton != null) craftButton.onClick.AddListener(TryCraft);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);
            if (customNameInput != null) customNameInput.onValueChanged.AddListener(HandleNameChanged);
        }

        private void OnEnable() => HideTemplates();

        private void OnDestroy()
        {
            if (craftButton != null) craftButton.onClick.RemoveListener(TryCraft);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(HandleQualityChanged);
            if (customNameInput != null) customNameInput.onValueChanged.RemoveListener(HandleNameChanged);
        }

        public bool Open(NpcInstance nextBlacksmith, string nextRecipientCharacterId)
        {
            if (!Show(nextBlacksmith, nextRecipientCharacterId)) return false;
            window?.Open();
            return true;
        }

        public bool Show(NpcInstance nextBlacksmith, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextBlacksmith == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId)) return false;

            blacksmith = nextBlacksmith;
            recipientCharacterId = nextRecipientCharacterId;
            serviceRoot?.SetActive(true);
            HideTemplates();
            selectedParts.Clear();
            PopulateQualityOptions();
            PopulateEquipmentChoices();

            if (selectedDefinition == null)
            {
                SetStatus("Kowal nie ma jeszcze skonfigurowanego wyposażenia.");
                return false;
            }

            RefreshPreview();
            return true;
        }

        public void Hide()
        {
            serviceRoot?.SetActive(false);
        }

        private void PopulateQualityOptions()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string> { "Kiepska", "Normalna", "Doskonała" });
            selectedQuality = EquipmentQuality.Normal;
            qualityDropdown.value = 1;
        }

        private void PopulateEquipmentChoices()
        {
            ClearSlots(equipmentContent);
            var classOffers = blacksmith?.Definition
                ?.GetRole<BlacksmithRoleData>()?.ClassOffers
                ?? new List<BlacksmithClassCraftingOffer>();
            var definitions = classOffers
                .Where(offer => offer != null)
                .Select(offer => offer.WeaponBase)
                .Where(item => item != null && item.ItemType == ItemType.Weapon)
                .Concat(classOffers
                    .Where(offer => offer != null)
                    .Select(offer => offer.ArmorBase)
                    .Where(item => item != null && item.ItemType == ItemType.Armor))
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .ToList();
            if (!definitions.Contains(selectedDefinition)) selectedDefinition = definitions.FirstOrDefault();

            foreach (var definition in definitions)
            {
                var slot = CreateSlot(
                    equipmentSlotTemplate,
                    equipmentContent,
                    definition.Icon,
                    definition.DisplayName,
                    definition.Description,
                    string.Empty,
                    ReferenceEquals(definition, selectedDefinition),
                    false,
                    () =>
                {
                    selectedDefinition = definition;
                    selectedParts.Clear();
                    PopulateEquipmentChoices();
                    PopulatePartChoices();
                    RefreshPreview();
                });
                // A base choice is a crafting category, not an actual item instance.
                // Keep its hover affordance deliberately compact: its name only.
                slot?.SetTooltip(definition.DisplayName, string.Empty);
            }

            if (selectedDefinition != null) PopulatePartChoices();
        }

        private void PopulatePartChoices()
        {
            var catalog = GameBootstrap.Instance?.ContentCatalog;
            foreach (var selection in partSelections ?? Array.Empty<CraftingPartSelectionView>())
            {
                var isRequired = GetRequiredPartKinds(selectedDefinition?.ItemType)
                    .Contains(selection?.Kind ?? default);
                if (selection?.Root != null) selection.Root.SetActive(isRequired);
                ClearSlots(selection?.Content);
                if (!isRequired || selection?.Content == null || selectedDefinition == null) continue;

                foreach (var part in catalog?.EquipmentPartDefinitions
                             ?.Where(part => part != null
                                 && !part.IsStarterOnly && part.RequiredClass == selectedDefinition.RequiredClass
                                 && part.Kind == selection.Kind)
                             .OrderBy(part => part.Id)
                         ?? Enumerable.Empty<EquipmentPartDefinition>())
                {
                    var slot = CreateSlot(
                        partSlotTemplate,
                        selection.Content,
                        part.Icon,
                        string.IsNullOrWhiteSpace(part.DisplayName) ? part.Id : part.DisplayName,
                        BuildPartTooltip(part),
                        string.Empty,
                        selectedParts.TryGetValue(selection.Kind, out var selectedPart)
                            && ReferenceEquals(selectedPart, part),
                        false,
                        () =>
                    {
                        selectedParts[selection.Kind] = part;
                        PopulatePartChoices();
                        RefreshPreview();
                    });
                }
            }
        }

        private void RefreshPreview()
        {
            ClearSlots(materialsContent);
            var preview = craftingService.CreatePreview(session, blacksmith, BuildRequest());
            var result = preview.Result;
            RefreshResultPreview(result);

            foreach (var material in preview.Materials ?? Array.Empty<CraftingMaterialCost>())
            {
                var materialId = material.Material.Id;
                var canOfferPurchase = material.HasPurchasableStock
                    && material.PurchasePrice > 0;
                var slot = CreateSlot(
                    materialSlotTemplate,
                    materialsContent,
                    material.Material.Icon,
                    material.Material.DisplayName,
                    BuildMaterialDescription(material),
                    $"{material.OwnedAmount}/{material.RequiredAmount}",
                    false,
                    material.MissingAmount > 0,
                    null,
                    canOfferPurchase
                        ? $"{material.PurchasableAmount}× • {material.PurchasePrice}g"
                        : null,
                    canOfferPurchase ? () => BuyMissingMaterial(materialId) : null,
                    material.PurchaseState,
                    material.PurchaseState == CraftingMaterialPurchaseState.Available);
                slot?.SetDefinitionTooltip(material.Material, BuildMaterialDescription(material),
                    material.PurchasePrice > 0 ? material.PurchasePrice : (int?)null);
            }

            if (craftButton != null) craftButton.interactable = preview.CanCraft;
            SetStatus(preview.CanCraft ? "Gotowe do craftingu." : preview.FailureReason);
        }

        private void BuyMissingMaterial(string materialId)
        {
            if (!craftingService.TryBuyMissingMaterial(
                    session, blacksmith, BuildRequest(), materialId,
                    out var paidGold, out var failure))
            {
                SetStatus(failure);
                return;
            }

            SetStatus($"Kupiono materiały za {paidGold}.");
            RefreshPreview();
        }

        private void TryCraft()
        {
            if (!craftingService.TryCraft(session, blacksmith, BuildRequest(), out var item, out var failure))
            {
                SetStatus(failure);
                return;
            }

            SetStatus(
                $"Wykuto: {item.DisplayName} "
                + $"(Jakość: {GetQualityDisplayName(item.Quality)}, "
                + $"Rzadkość: {item.CurrentRarity}).");
            RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private EquipmentCraftRequest BuildRequest()
        {
            selectedQuality = GetQualityForDropdownValue(qualityDropdown?.value ?? 1);
            return new EquipmentCraftRequest
            {
                Definition = selectedDefinition,
                Parts = GetRequiredPartKinds(selectedDefinition?.ItemType)
                    .Select(kind => selectedParts.TryGetValue(kind, out var part) ? part : null)
                    .ToList(),
                Quality = selectedQuality,
                CustomName = customNameInput?.text ?? string.Empty,
                RecipientCharacterId = recipientCharacterId
            };
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
            if (parent == null || template == null) return null;
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
            spawnedSlots.Add(slot);
            return slot;
        }

        private void ClearSlots(Transform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).GetComponent<BlacksmithCraftingSlotView>();
                if (child == null || child == equipmentSlotTemplate
                    || child == partSlotTemplate || child == materialSlotTemplate) continue;
                spawnedSlots.Remove(child);
                Destroy(child.gameObject);
            }
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value ?? string.Empty;
        }

        private void HideTemplates()
        {
            if (equipmentSlotTemplate != null) equipmentSlotTemplate.gameObject.SetActive(false);
            if (partSlotTemplate != null) partSlotTemplate.gameObject.SetActive(false);
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

        private void RefreshResultPreview(EquipmentItemInstance result)
        {
            var hasStructuredPreview = previewTooltipSlot != null
                || previewNameText != null
                || previewSummaryText != null;
            var usesLegacyText = previewText != null
                && previewText != previewNameText
                && previewText != previewSummaryText;

            if (usesLegacyText)
            {
                previewText.gameObject.SetActive(!hasStructuredPreview);
                previewText.text = BuildPreviewText(result);
            }

            if (!hasStructuredPreview) return;

            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(result?.Definition?.Icon);
                if (result != null)
                    previewTooltipSlot.SetItemTooltip(result, null);
                else
                    previewTooltipSlot.SetTooltip(
                        "Podgląd przedmiotu",
                        "Wybierz wyposażenie i części, aby zobaczyć szczegóły.");
            }

            SetPreviewLabel(previewNameText, result?.DisplayName);
            SetPreviewLabel(previewSummaryText, BuildPreviewSummary(result));
        }

        private static void SetPreviewLabel(TextMeshProUGUI label, string value)
        {
            if (label == null) return;

            label.text = value ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }

        private static string BuildPreviewText(EquipmentItemInstance result)
        {
            if (result == null) return "Wybierz wyposażenie i po jednej części każdego typu.";

            var itemType = GetItemTypeDisplayName(result.Definition != null
                ? result.Definition.ItemType
                : default);
            return $"{result.DisplayName}\n"
                + $"{itemType}\n"
                + $"Poziom przedmiotu: {result.ItemLevel}\n"
                + $"Jakość: {GetQualityDisplayName(result.Quality)}\n"
                + "Rzadkość: losowana przy craftingu\n"
                + BuildStatsText(result);
        }

        private static string BuildPreviewSummary(EquipmentItemInstance result)
        {
            if (result == null) return "Wybierz wyposażenie, aby zobaczyć podgląd.";

            var itemType = GetItemTypeDisplayName(result.Definition != null
                ? result.Definition.ItemType
                : default);
            var requiredClass = GetRequiredClassDisplayName(result);
            return $"{itemType} • {requiredClass} • Poziom {result.ItemLevel}";
        }

        private static string GetRequiredClassDisplayName(EquipmentItemInstance result)
        {
            return result == null ? "dowolna" : UIWorkspaceTooltipView.GetRequiredClassDisplayName(result);
        }

        private static string BuildPartTooltip(EquipmentPartDefinition part)
        {
            if (part == null) return string.Empty;

            var lines = new List<string>
            {
                $"{GetPartKindDisplayName(part.Kind)} • "
                + $"{GetPartRequiredClassDisplayName(part)} • "
                + $"Poziom {(int)part.LevelBracket}"
            };
            if (!string.IsNullOrWhiteSpace(part.Description))
            {
                lines.Add(part.Description.Trim());
            }

            AppendPartStats(lines, part.Stats);
            return string.Join("\n", lines);
        }

        private static string GetPartRequiredClassDisplayName(EquipmentPartDefinition part)
        {
            var className = part?.RequiredClass?.ClassName;
            return string.IsNullOrWhiteSpace(className) ? "dowolna klasa" : className;
        }

        private static string GetPartKindDisplayName(EquipmentPartKind kind)
        {
            return kind switch
            {
                EquipmentPartKind.WeaponHead => "Głowica broni",
                EquipmentPartKind.WeaponBinder => "Oprawa broni",
                EquipmentPartKind.WeaponHandle => "Rękojeść broni",
                EquipmentPartKind.ArmorPlate => "Płyta pancerza",
                EquipmentPartKind.ArmorBinder => "Oprawa pancerza",
                EquipmentPartKind.ArmorBase => "Podstawa pancerza",
                _ => "Część wyposażenia"
            };
        }

        private static void AppendPartStats(List<string> lines, EquipmentPartStats stats)
        {
            if (lines == null || stats == null) return;

            if (Mathf.Abs(stats.MinAttackBase) >= 0.0001f
                || Mathf.Abs(stats.MaxAttackBase) >= 0.0001f)
            {
                lines.Add($"Atak: {FormatValue(stats.MinAttackBase)}–{FormatValue(stats.MaxAttackBase)}");
            }

            AppendPartBaseStat(lines, CharacterStatType.CritChance, stats.CritChanceBase);
            AppendPartBaseStat(lines, CharacterStatType.CritDamage, stats.CritDamageBase);
            AppendPartBaseStat(lines, CharacterStatType.WeaponAccuracy, stats.WeaponAccuracyBase);
            AppendPartBaseStat(lines, CharacterStatType.MaxResource, stats.MaxResourceBase);
            AppendPartBaseStat(lines, CharacterStatType.ResourceRegeneration, stats.ResourceRegenerationBase);
            AppendPartBaseStat(lines, CharacterStatType.MaxHealth, stats.MaxHealthBase);
            AppendPartBaseStat(lines, CharacterStatType.Dodge, stats.DodgeBase);
            AppendPartBaseStat(lines, CharacterStatType.PhysicalDefense, stats.PhysicalDefenseBase);
            AppendPartBaseStat(lines, CharacterStatType.MagicalDefense, stats.MagicalDefenseBase);

            AppendPartPercentStat(lines, CharacterStatType.MinAttack, stats.MinAttackPercent);
            AppendPartPercentStat(lines, CharacterStatType.MaxAttack, stats.MaxAttackPercent);
            AppendPartPercentStat(lines, CharacterStatType.CritChance, stats.CritChancePercent);
            AppendPartPercentStat(lines, CharacterStatType.CritDamage, stats.CritDamagePercent);
            AppendPartPercentStat(lines, CharacterStatType.WeaponAccuracy, stats.WeaponAccuracyPercent);
            AppendPartPercentStat(lines, CharacterStatType.MaxResource, stats.MaxResourcePercent);
            AppendPartPercentStat(lines, CharacterStatType.ResourceRegeneration, stats.ResourceRegenerationPercent);
            AppendPartPercentStat(lines, CharacterStatType.MaxHealth, stats.MaxHealthPercent);
            AppendPartPercentStat(lines, CharacterStatType.Dodge, stats.DodgePercent);
            AppendPartPercentStat(lines, CharacterStatType.PhysicalDefense, stats.PhysicalDefensePercent);
            AppendPartPercentStat(lines, CharacterStatType.MagicalDefense, stats.MagicalDefensePercent);
        }

        private static void AppendPartBaseStat(
            List<string> lines,
            CharacterStatType stat,
            float value)
        {
            if (lines == null || Mathf.Abs(value) < 0.0001f) return;

            lines.Add($"{GetStatDisplayName(stat)}: {FormatBaseStatValue(stat, value)}");
        }

        private static void AppendPartPercentStat(
            List<string> lines,
            CharacterStatType stat,
            float value)
        {
            if (lines == null || Mathf.Abs(value) < 0.0001f) return;

            var arrow = value > 0f ? "↑" : "↓";
            var formattedValue = stat == CharacterStatType.Dodge
                ? $"{Mathf.RoundToInt(Mathf.Abs(value) * 100f)}%"
                : stat == CharacterStatType.CritDamage
                    ? $"{Mathf.Abs(value) * 100f:0.00}%"
                    : $"{Mathf.Abs(value) * 100f:0.##}%";
            lines.Add($"{GetStatDisplayName(stat)}: {arrow}{formattedValue}");
        }

        private static string FormatBaseStatValue(CharacterStatType stat, float value)
        {
            return stat switch
            {
                CharacterStatType.CritChance => $"{value * 100f:0.##}%",
                CharacterStatType.CritDamage => value.ToString("0.00;-0.00;0.00"),
                CharacterStatType.Dodge => Mathf.RoundToInt(value).ToString("0;-0;0"),
                _ => FormatValue(value)
            };
        }

        private static string BuildStatsText(EquipmentItemInstance result)
        {
            if (result?.StatModifiers == null || result.StatModifiers.Count == 0)
            {
                return "Brak dodatkowych statystyk.";
            }

            var values = result.StatModifiers
                .Where(modifier => modifier != null)
                .GroupBy(modifier => modifier.StatType)
                .ToDictionary(group => group.Key, group => group.Sum(modifier => modifier.Value));
            var lines = new List<string>();

            var minAttack = GetStatValue(values, CharacterStatType.MinAttack);
            var maxAttack = GetStatValue(values, CharacterStatType.MaxAttack);
            if (Mathf.Abs(minAttack) >= 0.0001f || Mathf.Abs(maxAttack) >= 0.0001f)
            {
                lines.Add($"Atak: {FormatValue(minAttack)}–{FormatValue(maxAttack)}");
            }

            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                if (stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack
                    || !values.TryGetValue(stat, out var value)
                    || Mathf.Abs(value) < 0.0001f) continue;

                lines.Add($"{GetStatDisplayName(stat)}: {FormatStatValue(stat, value)}");
            }

            return lines.Count == 0 ? "Brak dodatkowych statystyk." : string.Join("\n", lines);
        }

        private static float GetStatValue(
            IReadOnlyDictionary<CharacterStatType, float> values,
            CharacterStatType stat)
        {
            return values != null && values.TryGetValue(stat, out var value) ? value : 0f;
        }

        private static string FormatStatValue(CharacterStatType stat, float value)
        {
            return stat switch
            {
                CharacterStatType.CritChance => $"{value * 100f:0.##}%",
                CharacterStatType.CritDamage => value.ToString("0.00;-0.00;0.00"),
                CharacterStatType.Dodge => Mathf.RoundToInt(value).ToString("0;-0;0"),
                _ => FormatValue(value)
            };
        }

        private static string FormatValue(float value) => value.ToString("0.##;-0.##;0");

        private static string GetStatDisplayName(CharacterStatType statType)
        {
            return statType switch
            {
                CharacterStatType.MinAttack => "Atak min.",
                CharacterStatType.MaxAttack => "Atak maks.",
                CharacterStatType.MaxHealth => "Zdrowie",
                CharacterStatType.PhysicalDefense => "Obrona fizyczna",
                CharacterStatType.MagicalDefense => "Obrona magiczna",
                CharacterStatType.Dodge => "Unik",
                CharacterStatType.CritChance => "Szansa krytyczna",
                CharacterStatType.CritDamage => "Obrażenia krytyczne",
                CharacterStatType.Speed => "Szybkość",
                CharacterStatType.ResourceRegeneration => "Regeneracja zasobu",
                CharacterStatType.WeaponAccuracy => "Celność",
                CharacterStatType.MaxResource => "Maksymalny zasób",
                CharacterStatType.PhysicalDefensePenetration => "Przebicie obrony fizycznej",
                CharacterStatType.MagicalDefensePenetration => "Przebicie obrony magicznej",
                CharacterStatType.HealingPower => "Moc leczenia",
                _ => statType.ToString()
            };
        }

        private static string GetItemTypeDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Weapon => "Broń",
                ItemType.Armor => "Pancerz",
                ItemType.Jewelry => "Biżuteria",
                ItemType.Material => "Materiał",
                _ => "Wyposażenie"
            };
        }

        private static string GetQualityDisplayName(EquipmentQuality quality)
        {
            return quality switch
            {
                EquipmentQuality.Poor => "Kiepska",
                EquipmentQuality.Normal => "Normalna",
                EquipmentQuality.Excellent => "Doskonała",
                EquipmentQuality.Masterpiece => "Arcydzieło",
                _ => quality.ToString()
            };
        }

        private void HandleQualityChanged(int value)
        {
            selectedQuality = GetQualityForDropdownValue(value);
            RefreshPreview();
        }

        private static EquipmentQuality GetQualityForDropdownValue(int value) => value switch
        {
            0 => EquipmentQuality.Poor,
            2 => EquipmentQuality.Excellent,
            _ => EquipmentQuality.Normal
        };

        private void HandleNameChanged(string _) => RefreshPreview();

        private static IEnumerable<EquipmentPartKind> GetRequiredPartKinds(ItemType? itemType)
        {
            return itemType switch
            {
                ItemType.Weapon => new[]
                {
                    EquipmentPartKind.WeaponHead,
                    EquipmentPartKind.WeaponBinder,
                    EquipmentPartKind.WeaponHandle
                },
                ItemType.Armor => new[]
                {
                    EquipmentPartKind.ArmorPlate,
                    EquipmentPartKind.ArmorBinder,
                    EquipmentPartKind.ArmorBase
                },
                _ => Array.Empty<EquipmentPartKind>()
            };
        }
    }
}
