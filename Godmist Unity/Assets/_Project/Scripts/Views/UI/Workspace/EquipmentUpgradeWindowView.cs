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
        [SerializeField] private TextMeshProUGUI equipmentCountText;
        [SerializeField] private TextMeshProUGUI previewStatsText;
        [SerializeField] private TextMeshProUGUI riskText;
        [SerializeField] private TextMeshProUGUI availableGoldText;
        [SerializeField] private TextMeshProUGUI missingGoldText;
        [SerializeField] private TextMeshProUGUI targetMinimumText;
        [SerializeField] private TextMeshProUGUI targetMaximumText;
        [SerializeField] private TextMeshProUGUI chanceMinimumText;
        [SerializeField] private TextMeshProUGUI chanceMaximumText;
        [SerializeField] private TextMeshProUGUI costHeadingText;
        [SerializeField] private GameObject reforgeOutcomesRoot;
        [SerializeField] private TextMeshProUGUI reforgeIncreaseText;
        [SerializeField] private TextMeshProUGUI reforgeUnchangedText;
        [SerializeField] private TextMeshProUGUI reforgeDecreaseText;
        [SerializeField] private TextMeshProUGUI reforgeIncreaseChanceText;
        [SerializeField] private TextMeshProUGUI reforgeUnchangedChanceText;
        [SerializeField] private TextMeshProUGUI reforgeDecreaseChanceText;
        [SerializeField] private GameObject reforgeResultRoot;
        [SerializeField] private TextMeshProUGUI reforgeResultNameText;
        [SerializeField] private TextMeshProUGUI reforgeResultStatsText;

        private readonly EquipmentUpgradeService service = new();
        private readonly List<BlacksmithCraftingSlotView> slots = new();
        private GameSession session; private NpcInstance blacksmith;
        private EquipmentItemInstance selected; private CharacterInstance selectedOwner;
        private string executionPreviewText;
        private bool configuringSliders;
        private bool upgradeAttemptCompleted;

        private void Awake()
        {
            HideMechanicsFooter();
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
            upgradeAttemptCompleted = false;
            ClearReforgeResult();
            serviceRoot?.SetActive(true); if (upgradeOptionsRoot != null) upgradeOptionsRoot.SetActive(!isReforge);
            Populate(); return true;
        }
        public void Hide() => serviceRoot?.SetActive(false);
        private void HandleSliderChanged(float _)
        {
            if (configuringSliders) return;
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
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(slot);
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            slots.Clear();
            var candidates = session.PlayerParty.ActiveMemberIds.SelectMany(id => session.CharactersById.TryGetValue(id, out var owner) ? Enumerate(owner).Select(item => (owner: owner, item: item)) : Enumerable.Empty<(CharacterInstance owner, EquipmentItemInstance item)>())
                .Where(pair => pair.item.EquipmentDefinition?.IsStarterEquipment == false
                    && (isReforge ? pair.item.CurrentRarity < RarityTier.Godly : pair.item.UpgradeProgress < 1f)).ToList();
            if (!candidates.Any(pair => ReferenceEquals(pair.item, selected)))
            {
                var first = candidates.FirstOrDefault();
                selectedOwner = first.owner;
                selected = first.item;
                // A completed item may leave the list after an attempt. Its result
                // must not be displayed as the preview of the next selected item.
                executionPreviewText = null;
            }
            foreach (var pair in candidates)
            {
                var slot = Instantiate(equipmentSlotTemplate, equipmentGrid); slot.gameObject.SetActive(true);
                slot.Bind(ItemIconPresentation.GetIcon(pair.item), ItemRarityPresentation.Name(pair.item), $"{pair.owner.Definition.DisplayName}\n{UIWorkspaceTooltipView.GetColoredRarityDisplayName(pair.item.CurrentRarity)} • {pair.item.UpgradeProgress:P0}", equipmentCountText != null ? $"{pair.item.UpgradeProgress:P0}" : string.Empty, ReferenceEquals(pair.item, selected), false, () => { executionPreviewText = null; ClearReforgeResult(); selectedOwner = pair.owner; selected = pair.item; Populate(); });
                slot.SetItemTooltip(pair.item, pair.owner);
                slots.Add(slot);
            }
            if (equipmentCountText != null) equipmentCountText.text = candidates.Count.ToString();
            ConfigureTargetProgressSlider(retainedUpgradeTarget);
            Refresh();
        }

        private void ConfigureTargetProgressSlider(float? retainedTarget = null)
        {
            if (isReforge || targetProgressSlider == null) return;

            configuringSliders = true;
            try
            {
                var currentProgress = Mathf.Clamp01(selected?.UpgradeProgress ?? 0f);
                targetProgressSlider.interactable = CanAdjustUpgrade();
                targetProgressSlider.wholeNumbers = false;
                targetProgressSlider.minValue = currentProgress;
                targetProgressSlider.maxValue = Mathf.Min(1f, currentProgress + .2f);
                targetProgressSlider.SetValueWithoutNotify(Mathf.Clamp(retainedTarget ?? currentProgress, currentProgress, targetProgressSlider.maxValue));
            }
            finally { configuringSliders = false; }
        }

        private static IEnumerable<EquipmentItemInstance> Enumerate(CharacterInstance owner) =>
            (owner.Inventory?.Items ?? new())
                .OfType<EquipmentItemInstance>()
                .Concat(owner.Equipment?.GetEquippedItems() ?? Enumerable.Empty<EquipmentItemInstance>())
                .Where(item => item?.Definition != null
                    && (item.Definition.ItemType == ItemType.Weapon || item.Definition.ItemType == ItemType.Armor));
        private void Refresh()
        {
            HideMechanicsFooter();
            if (targetProgressLabel != null) targetProgressLabel.text = $"Docelowe ulepszenie: {targetProgressSlider?.value ?? 0f:P0}";
            if (successChanceLabel != null) successChanceLabel.text = $"Szansa powodzenia: {successChanceSlider?.value ?? 0f:P0}";
            if (!isReforge)
            {
                var canAdjust = CanAdjustUpgrade();
                if (targetMinimumText != null) targetMinimumText.text = canAdjust ? $"{targetProgressSlider.minValue:P0}" : "—";
                if (targetMaximumText != null) targetMaximumText.text = canAdjust ? $"{targetProgressSlider.maxValue:P0}" : "—";
                if (chanceMinimumText != null) chanceMinimumText.text = canAdjust ? $"{successChanceSlider.minValue:P0}" : "—";
                if (chanceMaximumText != null) chanceMaximumText.text = canAdjust ? $"{successChanceSlider.maxValue:P0}" : "—";
            }
            if (costHeadingText != null) costHeadingText.text = upgradeAttemptCompleted ? "Koszt kolejnej próby" : "Koszt próby";
            var catalog = GameBootstrap.Instance?.ContentCatalog;
            var preview = isReforge ? service.CreateReforgePreview(session, blacksmith, selected, catalog) : service.CreateUpgradePreview(session, blacksmith, selected, targetProgressSlider?.value ?? 0f, successChanceSlider?.value ?? 0f, catalog);
            var previewItem = BuildPreviewItem(preview);
            if (previewSlot != null)
            {
                previewSlot.SetIcon(ItemIconPresentation.GetIcon(previewItem));
                previewSlot.SetItemTooltip(previewItem, selectedOwner);
            }
            if (previewNameText != null) previewNameText.text = previewItem == null ? "Wybierz wyposażenie." : ItemRarityPresentation.Name(previewItem);
            if (previewText != null)
            {
                previewText.text = selected == null
                    ? "Wybierz wyposażenie."
                    : isReforge && selected.CurrentRarity == RarityTier.Godly
                        ? $"{UIWorkspaceTooltipView.GetColoredRarityDisplayName(selected.CurrentRarity)}\n{preview.FailureReason}"
                    : isReforge
                        ? $"{UIWorkspaceTooltipView.GetColoredRarityDisplayName(selected.CurrentRarity)} → {UIWorkspaceTooltipView.GetColoredRarityDisplayName(preview.TargetRarity)}\n" +
                          $"Zwiększenie: {preview.IncreaseChance:P0}  •  Bez zmian: {preview.UnchangedChance:P0}  •  Spadek: {preview.DecreaseChance:P0}"
                        : $"Ulepszenie: {selected.UpgradeProgress:P0} → {preview.TargetProgress:P0}\n" +
                          $"Szansa powodzenia: {preview.SuccessChance:P0}\n" +
                          $"Gniazda: {selected.SocketCapacity} → {preview.SocketCapacity}";
            }
            if (!isReforge && previewStatsText != null)
            {
                var starter = selected?.EquipmentDefinition.IsStarterEquipment == true;
                var complete = selected?.UpgradeProgress >= 1f;
                var projectsUpgrade = selected != null && !starter && !complete
                    && previewItem.UpgradeProgress > selected.UpgradeProgress;
                if (previewText != null) previewText.text = selected == null ? "Brak wyposażenia"
                    : starter ? "Wyposażenie startowe"
                    : $"{selectedOwner?.Definition.DisplayName} • Poziom {selected.ItemLevel}";
                previewStatsText.text = selected == null ? "Wybierz przedmiot z listy."
                    : projectsUpgrade ? $"Ulepszenie: {selected.UpgradeProgress:P0} → {previewItem.UpgradeProgress:P0}\n"
                        + UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(selected, previewItem)
                    : $"Ulepszenie: {selected.UpgradeProgress:P0}\n"
                        + UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(selected, selected, showCurrentValues: true);
                RefreshCost(preview);
                if (successChanceSlider != null) successChanceSlider.interactable = CanAdjustUpgrade();
                SetStatus(selected == null ? "Brak wyposażenia do ulepszenia."
                    : complete ? "Osiągnięto maksymalne ulepszenie (100%)."
                    : preview.GoldCost > 0 ? string.Empty : preview.FailureReason);
            }
            else if (isReforge && previewStatsText != null)
            {
                RefreshReforgePresentation(preview);
                RefreshCost(preview);
            }
            else
            {
                if (costText != null)
                {
                    var missingGold = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
                    costText.text = preview.CanExecute
                        ? $"<color=#F2C14E>Koszt: {preview.GoldCost} g</color>"
                        : $"<color=#E06C75>Koszt: {preview.GoldCost} g • Brakuje {missingGold} g</color>";
                }
                SetStatus(preview.CanExecute ? string.Empty : preview.FailureReason);
            }
            if (actionButton != null) actionButton.interactable = preview.CanExecute;
        }

        // Keep the serialized reference compatible with existing prefabs.
        private void HideMechanicsFooter()
        {
            if (riskText == null) return;
            riskText.text = string.Empty;
            riskText.gameObject.SetActive(false);
        }

        private bool CanAdjustUpgrade() => selected != null && !selected.EquipmentDefinition.IsStarterEquipment && selected.UpgradeProgress < 1f;

        private void RefreshCost(EquipmentUpgradePreview preview)
        {
            if (costText != null)
            {
                costText.text = EquipmentUpgradePresentation.Cost(preview.GoldCost);
                costText.color = preview.GoldCost > 0 && !preview.CanExecute ? new Color32(224,108,117,255) : new Color32(223,215,184,255);
            }
            if (availableGoldText != null) availableGoldText.text = $"Posiadane złoto: {session?.Gold ?? 0:N0}";
            if (missingGoldText != null) missingGoldText.text = EquipmentUpgradePresentation.Shortfall(preview.GoldCost, session?.Gold ?? 0);
        }

        private void RefreshReforgePresentation(EquipmentUpgradePreview preview)
        {
            var hasOutcomes = selected != null && preview.GoldCost > 0;
            if (previewText != null) previewText.text = selected == null ? "Brak wyposażenia"
                : $"{selectedOwner?.Definition.DisplayName} • Poziom {selected.ItemLevel}\nRzadkość: {UIWorkspaceTooltipView.GetColoredRarityDisplayName(selected.CurrentRarity)}";
            previewStatsText.text = hasOutcomes ? "Możliwe wyniki" : string.Empty;
            if (reforgeOutcomesRoot != null) reforgeOutcomesRoot.SetActive(hasOutcomes);
            if (hasOutcomes)
            {
                SetReforgeOutcome(reforgeIncreaseText, reforgeIncreaseChanceText, "Wyższa", preview.TargetRarity, preview.IncreaseChance);
                SetReforgeOutcome(reforgeUnchangedText, reforgeUnchangedChanceText, "Bez zmian", selected.CurrentRarity, preview.UnchangedChance);
                SetReforgeOutcome(reforgeDecreaseText, reforgeDecreaseChanceText, "Niższa",
                    (RarityTier)Math.Max((int)RarityTier.Trash, (int)selected.CurrentRarity - 1), preview.DecreaseChance);
            }
            var projected = BuildReforgeSuccessPreview(preview);
            if (reforgeResultRoot != null) reforgeResultRoot.SetActive(projected != null);
            if (reforgeResultNameText != null) reforgeResultNameText.text = projected == null
                ? string.Empty : "Przy udanym przekuciu";
            if (reforgeResultStatsText != null)
            {
                var changes = UIWorkspaceTooltipView.BuildEquipmentReforgeChanges(selected, projected);
                reforgeResultStatsText.text = projected == null ? string.Empty
                    : string.IsNullOrEmpty(changes) ? "Statystyki bez zmian." : changes;
            }
            SetStatus(selected == null ? "Brak wyposażenia do przekucia."
                : selected.EquipmentDefinition.IsStarterEquipment ? "Wyposażenia startowego nie można przekuwać."
                : selected.CurrentRarity == RarityTier.Godly ? "Osiągnięto najwyższą rzadkość wyposażenia."
                : preview.GoldCost > 0 ? string.Empty : preview.FailureReason);
        }

        private static void SetReforgeOutcome(TextMeshProUGUI label, TextMeshProUGUI chance,
            string outcome, RarityTier rarity, float probability)
        {
            if (label != null) label.text = probability > .00001f
                ? $"{outcome}: {UIWorkspaceTooltipView.GetColoredRarityDisplayName(rarity)}" : $"{outcome}: —";
            if (chance != null) chance.text = $"{probability:P0}";
        }

        private void ClearReforgeResult()
        {
            if (reforgeResultRoot != null) reforgeResultRoot.SetActive(false);
            if (reforgeResultNameText != null) reforgeResultNameText.text = string.Empty;
            if (reforgeResultStatsText != null) reforgeResultStatsText.text = string.Empty;
        }

        private EquipmentItemInstance BuildReforgeSuccessPreview(EquipmentUpgradePreview preview)
        {
            if (selected == null || preview.GoldCost <= 0 || preview.IncreaseChance <= 0f) return null;
            var projected = new EquipmentItemInstance
            {
                Definition = selected.Definition,
                CurrentRarity = selected.CurrentRarity,
                RequiredClassId = selected.RequiredClassId,
                UpgradeProgress = selected.UpgradeProgress,
                BaseStatModifiers = CopyModifiers(selected.BaseStatModifiers),
                StatModifiers = CopyModifiers(selected.StatModifiers)
            };
            // Capture a legacy baseline on the detached instance just as the service does.
            projected.RecalculateStatModifiers();
            projected.CurrentRarity = preview.TargetRarity;
            projected.RecalculateStatModifiers();
            return projected;
        }

        private EquipmentItemInstance BuildPreviewItem(EquipmentUpgradePreview preview)
        {
            // A random reforge has no single projected item. Preview the actual current item.
            if (selected == null || isReforge) return selected;

            var projectedProgress = preview.TargetProgress > selected.UpgradeProgress && preview.TargetProgress <= 1f
                ? preview.TargetProgress : selected.UpgradeProgress;

            selected.RecalculateStatModifiers();
            var result = new EquipmentItemInstance
            {
                InstanceId = selected.InstanceId,
                Definition = selected.Definition,
                CurrentRarity = selected.CurrentRarity,
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
                StatModifiers = CopyModifiers(selected.StatModifiers)
            };
            result.RecalculateStatModifiers();
            return result;
        }

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
                var beforeName = before.HasValue ? UIWorkspaceTooltipView.GetColoredRarityDisplayName(before.Value) : string.Empty;
                var resultName = UIWorkspaceTooltipView.GetColoredRarityDisplayName(result);
                if (!executed)
                {
                    feedback = error;
                }
                else if (result > before)
                {
                    feedback = $"<color=#3D9E63><b>PRZEKUCIE UDANE</b></color>\n{beforeName} → {resultName} • Koszt: {paid:N0} złota.";
                }
                else if (result == before)
                {
                    feedback = $"<color=#F2C14E><b>PRZEKUCIE BEZ ZMIANY</b></color>\n{beforeName} • Koszt: {paid:N0} złota.";
                }
                else
                {
                    feedback = $"<color=#E06C75><b>PRZEKUCIE NIEUDANE</b></color>\n{beforeName} → {resultName} • Koszt: {paid:N0} złota.";
                }
                if (executed)
                {
                    upgradeAttemptCompleted = true;
                    executionPreviewText = null;
                }
            }
            else
            {
                var beforeProgress = selected?.UpgradeProgress ?? 0f;
                var executed = service.TryUpgrade(session, blacksmith, selectedOwner, selected,
                    targetProgressSlider.value, successChanceSlider.value, catalog,
                    out var success, out var paid, out var error);
                feedback = !executed
                    ? error
                    : success
                        ? $"<color=#3D9E63><b>ULEPSZENIE UDANE</b></color>\nKoszt: {paid} złota."
                        : $"<color=#E06C75><b>ULEPSZENIE NIEUDANE</b></color>\nUtrata: {Mathf.RoundToInt((beforeProgress - selected.UpgradeProgress) * 100)} pkt proc.\nKoszt: {paid} złota.";

                if (executed)
                {
                    upgradeAttemptCompleted = true;
                    executionPreviewText = null;
                }

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
