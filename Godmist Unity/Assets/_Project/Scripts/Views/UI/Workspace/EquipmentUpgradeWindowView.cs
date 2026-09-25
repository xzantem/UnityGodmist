using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
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
    public sealed class EquipmentUpgradeWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform equipmentGrid;
        [SerializeField] private BlacksmithCraftingSlotView equipmentSlotTemplate;
        [SerializeField] private Slider targetProgressSlider;
        [SerializeField] private Slider successChanceSlider;
        [SerializeField] private TextMeshProUGUI targetProgressLabel;
        [SerializeField] private TextMeshProUGUI successChanceLabel;
        [SerializeField] private GameObject upgradeOptionsRoot;
        [SerializeField] private BlacksmithCraftingSlotView previewSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button actionButton;
        [SerializeField] private bool isReforge;

        private readonly EquipmentUpgradeService service = new();
        private readonly List<BlacksmithCraftingSlotView> slots = new();
        private GameSession session; private NpcInstance blacksmith;
        private EquipmentItemInstance selected; private CharacterInstance selectedOwner;
        private string executionPreviewText;

        private void Awake()
        {
            if (equipmentSlotTemplate != null) equipmentSlotTemplate.gameObject.SetActive(false);
            if (actionButton != null) actionButton.onClick.AddListener(Execute);
            if (targetProgressSlider != null) targetProgressSlider.onValueChanged.AddListener(HandleSliderChanged);
            if (successChanceSlider != null) successChanceSlider.onValueChanged.AddListener(HandleSliderChanged);
        }
        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(Execute);
            if (targetProgressSlider != null) targetProgressSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            if (successChanceSlider != null) successChanceSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        }
        public bool Show(NpcInstance nextBlacksmith, string _)
        {
            session = GameBootstrap.Instance?.Session; blacksmith = nextBlacksmith;
            if (session == null || blacksmith == null) return false;
            executionPreviewText = null;
            serviceRoot?.SetActive(true); if (upgradeOptionsRoot != null) upgradeOptionsRoot.SetActive(!isReforge);
            Populate(); return true;
        }
        public void Hide() => serviceRoot?.SetActive(false);
        private void HandleSliderChanged(float _)
        {
            // Displayed percentages and the actual operation must use the same value.
            if (targetProgressSlider != null)
                targetProgressSlider.SetValueWithoutNotify(Mathf.Max(
                    selected?.UpgradeProgress ?? 0f,
                    Mathf.Round(targetProgressSlider.value * 100f) / 100f));
            if (successChanceSlider != null)
                successChanceSlider.SetValueWithoutNotify(Mathf.Round(successChanceSlider.value * 100f) / 100f);
            executionPreviewText = null;
            Refresh();
        }

        private void Populate(float? retainedUpgradeTarget = null)
        {
            foreach (var slot in slots) if (slot != null) Destroy(slot.gameObject); slots.Clear();
            var candidates = session.PlayerParty.ActiveMemberIds.SelectMany(id => session.CharactersById.TryGetValue(id, out var owner) ? Enumerate(owner).Select(item => (owner: owner, item: item)) : Enumerable.Empty<(CharacterInstance owner, EquipmentItemInstance item)>()).ToList();
            if (!candidates.Any(pair => ReferenceEquals(pair.item, selected))) { var first = candidates.FirstOrDefault(); selectedOwner = first.owner; selected = first.item; }
            foreach (var pair in candidates)
            {
                var slot = Instantiate(equipmentSlotTemplate, equipmentGrid); slot.gameObject.SetActive(true);
                slot.Bind(pair.item.Definition.Icon, pair.item.DisplayName, $"{pair.owner.Definition.DisplayName}\n{pair.item.CurrentRarity} • {pair.item.UpgradeProgress:P0}", string.Empty, ReferenceEquals(pair.item, selected), false, () => { executionPreviewText = null; selectedOwner = pair.owner; selected = pair.item; Populate(); });
                slot.SetItemTooltip(pair.item, pair.owner);
                slots.Add(slot);
            }
            ConfigureTargetProgressSlider(retainedUpgradeTarget);
            Refresh();
        }

        private void ConfigureTargetProgressSlider(float? retainedTarget = null)
        {
            if (isReforge || targetProgressSlider == null) return;

            var currentProgress = Mathf.Clamp01(selected?.UpgradeProgress ?? 0f);
            targetProgressSlider.interactable = true;
            targetProgressSlider.wholeNumbers = false;
            targetProgressSlider.minValue = currentProgress;
            targetProgressSlider.maxValue = Mathf.Min(1f, currentProgress + .2f);
            targetProgressSlider.SetValueWithoutNotify(Mathf.Clamp(retainedTarget ?? currentProgress, currentProgress, targetProgressSlider.maxValue));
        }

        private static IEnumerable<EquipmentItemInstance> Enumerate(CharacterInstance owner) =>
            (owner.Inventory?.Items ?? new())
                .OfType<EquipmentItemInstance>()
                .Concat(owner.Equipment?.GetEquippedItems() ?? Enumerable.Empty<EquipmentItemInstance>())
                .Where(item => item?.Definition != null
                    && (item.Definition.ItemType == ItemType.Weapon || item.Definition.ItemType == ItemType.Armor));
        private void Refresh()
        {
            if (targetProgressLabel != null) targetProgressLabel.text = $"Cel ulepszenia: {targetProgressSlider.value:P0}";
            if (successChanceLabel != null) successChanceLabel.text = $"Szansa powodzenia: {successChanceSlider.value:P0}";
            var catalog = GameBootstrap.Instance?.ContentCatalog;
            var preview = isReforge ? service.CreateReforgePreview(session, blacksmith, selected, catalog) : service.CreateUpgradePreview(session, blacksmith, selected, targetProgressSlider?.value ?? 0f, successChanceSlider?.value ?? 0f, catalog);
            var previewItem = BuildPreviewItem(preview);
            if (previewSlot != null)
            {
                previewSlot.SetIcon(previewItem?.Definition?.Icon);
                previewSlot.SetItemTooltip(previewItem, selectedOwner);
            }
            if (previewNameText != null) previewNameText.text = previewItem?.DisplayName ?? "Wybierz wyposażenie.";
            if (previewText != null)
            {
                previewText.text = selected == null
                    ? "Wybierz wyposażenie."
                    : isReforge && selected.CurrentRarity == RarityTier.Godly
                        ? $"{UIWorkspaceTooltipView.GetRarityDisplayName(selected.CurrentRarity)}\n{preview.FailureReason}"
                    : isReforge
                        ? $"{UIWorkspaceTooltipView.GetRarityDisplayName(selected.CurrentRarity)} → {UIWorkspaceTooltipView.GetRarityDisplayName(preview.TargetRarity)}\n" +
                          $"Zwiększenie: {preview.IncreaseChance:P0}  •  Bez zmian: {preview.UnchangedChance:P0}  •  Spadek: {preview.DecreaseChance:P0}"
                        : $"Ulepszenie: {selected.UpgradeProgress:P0} → {preview.TargetProgress:P0}\n" +
                          $"Szansa powodzenia: {preview.SuccessChance:P0}\n" +
                          $"Gniazda: {selected.SocketCapacity} → {preview.SocketCapacity}\nPorażka: −10–20 pkt proc.; nadmiarowe galduryty pozostają nieaktywne.";
            }
            if (costText != null)
            {
                var missingGold = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
                costText.text = preview.CanExecute
                    ? $"<color=#F2C14E>Koszt: {preview.GoldCost} g</color>"
                    : $"<color=#E06C75>Koszt: {preview.GoldCost} g • Brakuje {missingGold} g</color>";
            }
            SetStatus(preview.CanExecute ? string.Empty : preview.FailureReason);
            if (actionButton != null) actionButton.interactable = preview.CanExecute;
        }

        private EquipmentItemInstance BuildPreviewItem(EquipmentUpgradePreview preview)
        {
            if (selected == null || !string.IsNullOrWhiteSpace(executionPreviewText)) return selected;

            var projectedProgress = isReforge
                ? selected.UpgradeProgress
                : preview.TargetProgress > selected.UpgradeProgress && preview.TargetProgress <= 1f
                    ? preview.TargetProgress
                    : selected.UpgradeProgress;
            var projectedRarity = isReforge && selected.CurrentRarity != RarityTier.Godly
                ? preview.TargetRarity
                : selected.CurrentRarity;
            var upgradeFactor = (1f + projectedProgress) / (1f + selected.UpgradeProgress);

            selected.RecalculateStatModifiers();
            var result = new EquipmentItemInstance
            {
                InstanceId = selected.InstanceId,
                Definition = selected.Definition,
                CurrentRarity = projectedRarity,
                ItemLevel = selected.ItemLevel,
                CustomName = selected.CustomName,
                RequiredClassId = selected.RequiredClassId,
                RequiredLevel = selected.RequiredLevel,
                Quality = selected.Quality,
                CraftedBaseValue = selected.CraftedBaseValue,
                SocketedGaldurytes = new List<GalduryteItemInstance>(selected.SocketedGaldurytes ?? new()),
                PartIds = new List<string>(selected.PartIds ?? new()),
                UpgradeProgress = projectedProgress,
                BaseStatModifiers = CopyModifiers(selected.BaseStatModifiers),
                StatModifiers = (selected.StatModifiers ?? new()).Select(modifier =>
                {
                    var copy = CopyModifier(modifier);
                    if (IsUpgradeStat(copy.StatType)) copy.Value *= upgradeFactor;
                    return copy;
                }).ToList()
            };
            result.RecalculateStatModifiers();
            return result;
        }

        private bool IsUpgradeStat(CharacterStatType stat) => selected.Definition.ItemType == ItemType.Weapon
            ? stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack
            : stat is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense;

        private static List<StatModifier> CopyModifiers(IEnumerable<StatModifier> modifiers) =>
            (modifiers ?? Enumerable.Empty<StatModifier>()).Where(modifier => modifier != null).Select(CopyModifier).ToList();

        private static StatModifier CopyModifier(StatModifier modifier) => new()
        {
            StatType = modifier.StatType,
            Operation = modifier.Operation,
            Stacking = modifier.Stacking,
            Value = modifier.Value
        };
        private void Execute()
        {
            var catalog = GameBootstrap.Instance?.ContentCatalog;
            string feedback;
            float? retainedUpgradeTarget = null;
            if (isReforge)
            {
                var before = selected?.CurrentRarity;
                var executed = service.TryReforge(session, blacksmith, selectedOwner, selected, catalog,
                    out var result, out var paid, out var error);
                var beforeName = before.HasValue ? UIWorkspaceTooltipView.GetRarityDisplayName(before.Value) : string.Empty;
                var resultName = UIWorkspaceTooltipView.GetRarityDisplayName(result);
                if (!executed)
                {
                    feedback = error;
                }
                else if (result > before)
                {
                    feedback = $"<color=#3D9E63><b>PRZEKUCIE UDANE</b></color>  {beforeName} → {resultName} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik przekucia</b>\n{beforeName} → {resultName}\nUnikalność została zwiększona.";
                }
                else if (result == before)
                {
                    feedback = $"<color=#F2C14E><b>PRZEKUCIE BEZ ZMIANY</b></color>  {beforeName} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik przekucia</b>\n{beforeName} → {resultName}\nUnikalność nie zmieniła się.";
                }
                else
                {
                    feedback = $"<color=#E06C75><b>PRZEKUCIE NIEUDANE</b></color>  {beforeName} → {resultName} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik przekucia</b>\n{beforeName} → {resultName}\nUnikalność została obniżona.";
                }
            }
            else
            {
                var executed = service.TryUpgrade(session, blacksmith, selectedOwner, selected,
                    targetProgressSlider.value, successChanceSlider.value, catalog,
                    out var success, out var paid, out var error);
                feedback = !executed
                    ? error
                    : success
                        ? $"<color=#3D9E63><b>ULEPSZENIE UDANE</b></color>  Koszt: {paid} g."
                        : $"<color=#E06C75><b>ULEPSZENIE NIEUDANE</b></color>  {error} Koszt: {paid} g.";

                if (executed)
                    executionPreviewText = success
                        ? $"<b>Wynik ulepszenia</b>\nPostęp: {selected.UpgradeProgress:P0}\nGniazda: {selected.SocketCapacity}"
                        : $"<b>Wynik ulepszenia</b>\nPostęp po niepowodzeniu: {selected.UpgradeProgress:P0}\nGniazda: {selected.SocketCapacity}";

                // The requested target remains useful after a failed or rejected attempt.
                // Only a successful upgrade replaces it with the new current progress.
                if (!executed || !success) retainedUpgradeTarget = targetProgressSlider.value;
            }

            Populate(retainedUpgradeTarget);
            SetStatus(feedback);
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
        }
    }
}
