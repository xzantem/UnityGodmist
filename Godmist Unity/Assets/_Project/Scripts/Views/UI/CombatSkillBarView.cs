using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatSkillBarView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private RectTransform skillBarRoot;
        [SerializeField] private CombatSkillSlotView skillSlotTemplate;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private CombatTooltipView tooltipView;

        private readonly List<CombatSkillSlotView> spawnedSlots = new();
        private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private int cachedSkillCount = -1;

        private void Update()
        {
            var session = sandboxBootstrap?.Session;
            if (session?.ActiveUnit == null)
            {
                targetSelectionController?.CancelTargetSelection();
                ClearSlots();
                ResetCache();
                return;
            }

            var activeUnit = session.ActiveUnit;
            var skillCount = activeUnit.Character?.Skills?.Count ?? 0;
            if (cachedSession != session || cachedActiveUnit != activeUnit
                || cachedSkillCount != skillCount)
            {
                RebuildSlots(activeUnit);
            }
            else
            {
                RefreshSlotVisuals(activeUnit);
            }
        }

        private void RebuildSlots(BattleUnit activeUnit)
        {
            targetSelectionController?.CancelTargetSelection();
            tooltipView?.Hide();
            ClearSlots();

            if (skillBarRoot == null || skillSlotTemplate == null)
            {
                return;
            }

            foreach (var skill in activeUnit.Character.Skills)
            {
                if (skill == null)
                {
                    continue;
                }

                var slot = Instantiate(skillSlotTemplate, skillBarRoot);
                slot.gameObject.SetActive(true);
                slot.Initialize(
                    skill,
                    tooltipView,
                    () => targetSelectionController?.BeginTargetSelection(skill));
                spawnedSlots.Add(slot);
            }

            cachedSession = sandboxBootstrap?.Session;
            cachedActiveUnit = activeUnit;
            cachedSkillCount = activeUnit.Character.Skills.Count;
            RefreshSlotVisuals(activeUnit);
        }

        private void RefreshSlotVisuals(BattleUnit activeUnit)
        {
            var skills = activeUnit.Character?.Skills;
            if (skills == null)
            {
                return;
            }

            var slotIndex = 0;
            foreach (var skill in skills)
            {
                if (skill == null || slotIndex >= spawnedSlots.Count)
                {
                    continue;
                }

                var actionCost = CombatActionCostCalculator.CalculateSkillCost(activeUnit, skill);
                var resource = activeUnit.Character.ResourcePool;
                var canUse = activeUnit.CurrentActionPoints >= actionCost
                    && (resource == null || resource.CurrentValue >= skill.ResourceCost);
                spawnedSlots[slotIndex].RefreshAvailability(canUse, actionCost);
                slotIndex++;
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in spawnedSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }

            spawnedSlots.Clear();
        }

        private void ResetCache()
        {
            cachedSession = null;
            cachedActiveUnit = null;
            cachedSkillCount = -1;
        }
    }
}
