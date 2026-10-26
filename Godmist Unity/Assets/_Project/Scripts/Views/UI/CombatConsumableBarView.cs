using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Views.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.UI
{
    /// <summary>Combat-only strip for consumables carried by player participants.</summary>
    public sealed class CombatConsumableBarView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private RectTransform slotRoot;
        [SerializeField] private CombatSkillSlotView slotTemplate;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;

        private readonly List<CombatSkillSlotView> slots = new();
        private readonly List<ConsumableItemInstance> items = new();
        private BattleUnit cachedUnit;
        private string cachedSignature = string.Empty;

        private void Awake()
        {
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
        }

        private void Update()
        {
            var activeUnit = sandboxBootstrap?.Session?.ActiveUnit;
            if (activeUnit == null || !activeUnit.IsPlayerSide)
            {
                Clear();
                cachedUnit = null;
                cachedSignature = string.Empty;
                return;
            }

            var signature = ConsumableService.CombatInventorySignature(sandboxBootstrap?.Session);
            if (cachedUnit != activeUnit || cachedSignature != signature) Rebuild(activeUnit, signature);
            Refresh(activeUnit);
        }

        private void Rebuild(BattleUnit activeUnit, string signature)
        {
            Clear();
            if (slotRoot == null || slotTemplate == null) return;
            foreach (var consumable in ConsumableService.GetCombatItems(sandboxBootstrap?.Session))
            {
                items.Add(consumable);
                var captured = consumable;
                var slot = Instantiate(slotTemplate, slotRoot);
                slot.gameObject.SetActive(true);
                slot.InitializeConsumable(consumable, tooltipView,
                    () => targetSelectionController?.BeginConsumableTargetSelection(captured),
                    () => sandboxBootstrap?.Session?.ActiveUnit);
                slots.Add(slot);
            }
            cachedUnit = activeUnit;
            cachedSignature = signature;
        }

        private void Refresh(BattleUnit activeUnit)
        {
            for (var index = 0; index < slots.Count && index < items.Count; index++)
            {
                var item = items[index];
                var definition = item.ConsumableDefinition;
                var cost = CombatActionCostCalculator.CalculatePercentageCost(activeUnit.MaxActionPoints, ConsumableRuntimeActionBuilder.ActionCostPercent);
                var available = ConsumableService.CanUseInCombat(sandboxBootstrap?.Session, activeUnit, item);
                slots[index].RefreshAvailability(available, cost, false,
                    $"{item.CurrentCharges}/{item.MaxCharges}", reason:
                    activeUnit.ConsumableUseTypesUsedThisTurn.Contains(definition.UseType)
                        ? "Ten typ przedmiotu został już użyty w tej turze."
                        : activeUnit.CurrentActionPoints < cost ? "Za mało punktów akcji." : null);
            }
        }

        private void Clear()
        {
            foreach (var slot in slots) if (slot != null) Destroy(slot.gameObject);
            slots.Clear();
            items.Clear();
        }
    }
}
