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
            EnsureSliderInputSurface(targetProgressSlider);
            EnsureSliderInputSurface(successChanceSlider);
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
            targetProgressSlider.maxValue = 1f;
            targetProgressSlider.SetValueWithoutNotify(Mathf.Clamp(retainedTarget ?? currentProgress, currentProgress, 1f));
        }

        private static void EnsureSliderInputSurface(Slider slider)
        {
            if (slider == null) return;

            // A Fill Rect has zero width at value 0. Give the Slider root its own
            // transparent raycast target, so clicking and dragging works across the
            // complete control at every value.
            var inputSurface = slider.GetComponent<Graphic>();
            if (inputSurface == null)
            {
                var image = slider.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                inputSurface = image;
            }

            inputSurface.raycastTarget = true;
            if (slider.targetGraphic == null) slider.targetGraphic = inputSurface;
        }
        private static IEnumerable<EquipmentItemInstance> Enumerate(CharacterInstance owner) =>
            (owner.Inventory?.Items ?? new())
                .OfType<EquipmentItemInstance>()
                .Concat(owner.Equipment?.GetEquippedItems() ?? Enumerable.Empty<EquipmentItemInstance>())
                .Where(item => item?.Definition != null
                    && (item.Definition.ItemType == ItemType.Weapon || item.Definition.ItemType == ItemType.Armor));
        private void Refresh()
        {
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
                        ? $"{selected.CurrentRarity}\n{preview.FailureReason}"
                    : isReforge
                        ? $"{selected.CurrentRarity} → {preview.TargetRarity}\n" +
                          $"Zwiększenie: {preview.IncreaseChance:P0}  •  Bez zmian: {preview.UnchangedChance:P0}  •  Spadek: {preview.DecreaseChance:P0}"
                        : $"{selected.UpgradeProgress:P0} → {preview.TargetProgress:P0}\n" +
                          $"Szansa powodzenia: {preview.SuccessChance:P0}\n" +
                          $"Sockety: {selected.SocketCapacity} → {preview.SocketCapacity}";
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

            return new EquipmentItemInstance
            {
                InstanceId = selected.InstanceId,
                Definition = selected.Definition,
                CurrentRarity = projectedRarity,
                ItemLevel = selected.ItemLevel,
                CustomName = selected.CustomName,
                RequiredClassId = selected.RequiredClassId,
                RequiredLevel = selected.RequiredLevel,
                Quality = selected.Quality,
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
                if (!executed)
                {
                    feedback = error;
                }
                else if (result > before)
                {
                    feedback = $"<color=#3D9E63><b>REFORGING UDANY</b></color>  {before} → {result} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik reforgingu</b>\n{before} → {result}\nUnikalność została zwiększona.";
                }
                else if (result == before)
                {
                    feedback = $"<color=#F2C14E><b>REFORGING BEZ ZMIANY</b></color>  {before} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik reforgingu</b>\n{before} → {result}\nUnikalność nie zmieniła się.";
                }
                else
                {
                    feedback = $"<color=#E06C75><b>REFORGING NIEUDANY</b></color>  {before} → {result} • Koszt: {paid} g.";
                    executionPreviewText = $"<b>Wynik reforgingu</b>\n{before} → {result}\nUnikalność została obniżona.";
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
                        ? $"<b>Wynik ulepszenia</b>\nPostęp: {selected.UpgradeProgress:P0}\nSockety: {selected.SocketCapacity}/5"
                        : $"<b>Wynik ulepszenia</b>\nPostęp po niepowodzeniu: {selected.UpgradeProgress:P0}\nSockety: {selected.SocketCapacity}/5";

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
