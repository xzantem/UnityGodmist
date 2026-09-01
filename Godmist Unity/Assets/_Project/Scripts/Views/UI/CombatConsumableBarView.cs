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
    /// <summary>Combat-only strip for consumables carried by the active character.</summary>
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

            var signature = BuildSignature(activeUnit);
            if (cachedUnit != activeUnit || cachedSignature != signature) Rebuild(activeUnit, signature);
            Refresh(activeUnit);
        }

        private void Rebuild(BattleUnit activeUnit, string signature)
        {
            Clear();
            if (slotRoot == null || slotTemplate == null) return;
            foreach (var item in activeUnit.Character?.Inventory?.Items ?? new List<ItemInstance>())
            {
                if (item is not ConsumableItemInstance consumable) continue;
                if (!IsCombatUsable(consumable)) continue;
                var skill = ConsumableRuntimeActionBuilder.Build(consumable);
                if (skill == null) continue;
                items.Add(consumable);
                var captured = consumable;
                var slot = Instantiate(slotTemplate, slotRoot);
                slot.gameObject.SetActive(true);
                slot.Initialize(skill, tooltipView,
                    () => targetSelectionController?.BeginConsumableTargetSelection(captured),
                    () => sandboxBootstrap?.Session?.ActiveUnit);
                slots.Add(slot);
            }
            cachedUnit = activeUnit;
            cachedSignature = signature;
        }

        private static bool IsCombatUsable(ConsumableItemInstance item)
        {
            if (item == null) return false;
            if (item.RuntimeEffects == null || item.RuntimeEffects.Count == 0)
            {
                return ConsumableRuntimeRules.TryGetImpliedUtilityEffect(
                    item.ConsumableDefinition,
                    out var impliedEffect)
                    && impliedEffect.EffectType != ConsumableRuntimeEffectType.ReturnToArungard;
            }
            foreach (var effect in item.RuntimeEffects)
            {
                if (effect?.EffectType != ConsumableRuntimeEffectType.ReturnToArungard)
                    return true;
            }

            return false;
        }

        private void Refresh(BattleUnit activeUnit)
        {
            for (var index = 0; index < slots.Count && index < items.Count; index++)
            {
                var item = items[index];
                var definition = item.ConsumableDefinition;
                var cost = 0;
                var available = item.HasCharges
                    && !activeUnit.ConsumableUseTypesUsedThisTurn.Contains(definition.UseType)
                    && activeUnit.CurrentActionPoints >= cost;
                slots[index].RefreshAvailability(available, cost, false,
                    $"{item.CurrentCharges}/{item.MaxCharges}");
            }
        }

        private string BuildSignature(BattleUnit unit)
        {
            var value = string.Empty;
            foreach (var item in unit.Character?.Inventory?.Items ?? new List<ItemInstance>())
                if (item is ConsumableItemInstance consumable)
                    value += $"|{consumable.InstanceId}:{consumable.CurrentCharges}";
            return value;
        }

        private void Clear()
        {
            foreach (var slot in slots) if (slot != null) Destroy(slot.gameObject);
            slots.Clear();
            items.Clear();
        }
    }
}
