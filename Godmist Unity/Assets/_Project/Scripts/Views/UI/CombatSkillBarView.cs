using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatSkillBarView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private RectTransform skillBarRoot;
        [SerializeField] private CombatSkillSlotView skillSlotTemplate;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;

        private readonly List<CombatSkillSlotView> spawnedSlots = new();
        private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private int cachedSkillCount = -1;
        private string cachedSkillOrder = string.Empty;

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
            var activeSkills = activeUnit.Character?.GetActiveSkills();
            var skillCount = activeSkills?.Count ?? 0;
            var skillOrder = BuildSkillOrderSignature(activeSkills);
            if (cachedSession != session || cachedActiveUnit != activeUnit
                || cachedSkillCount != skillCount
                || cachedSkillOrder != skillOrder)
            {
                RebuildSlots(activeUnit, activeSkills, skillOrder);
            }
            else
            {
                RefreshSlotVisuals(activeUnit);
            }
        }

        private void RebuildSlots(
            BattleUnit activeUnit,
            IReadOnlyList<SkillData> activeSkills,
            string skillOrder)
        {
            targetSelectionController?.CancelTargetSelection();
            tooltipView?.Hide();
            ClearSlots();

            if (skillBarRoot == null || skillSlotTemplate == null)
            {
                return;
            }

            foreach (var skill in activeSkills ?? new List<SkillData>())
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
            cachedSkillCount = activeSkills?.Count ?? 0;
            cachedSkillOrder = skillOrder ?? string.Empty;
            RefreshSlotVisuals(activeUnit);
        }

        private static string BuildSkillOrderSignature(IReadOnlyList<SkillData> skills)
        {
            if (skills == null || skills.Count == 0)
            {
                return string.Empty;
            }

            var signature = string.Empty;
            foreach (var skill in skills)
            {
                signature += "|" + (skill?.SkillId ?? string.Empty);
            }

            return signature;
        }

        private void RefreshSlotVisuals(BattleUnit activeUnit)
        {
            var skills = activeUnit.Character?.GetActiveSkills();
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
                var isActiveToggle = skill.IsToggle
                    && activeUnit.ActiveToggleSkillIds != null
                    && activeUnit.ActiveToggleSkillIds.Contains(skill.SkillId);
                var wasChangedThisTurn = skill.IsToggle
                    && activeUnit.ToggleSkillIdsUsedThisTurn != null
                    && activeUnit.ToggleSkillIdsUsedThisTurn.Contains(skill.SkillId);
                var canPayActivationCost = activeUnit.CurrentActionPoints >= actionCost
                    && (skill.ResourceCost <= 0
                        || (resource != null
                            && resource.ResourceType == skill.ResourceType
                            && resource.CurrentValue >= skill.ResourceCost));
                var canPayKnives = skill.KnifeCost <= 0
                    || activeUnit.AvailableKnives >= skill.KnifeCost;
                var canUse = isActiveToggle
                    || (!wasChangedThisTurn && canPayActivationCost && canPayKnives);
                var knifeCount = skill.KnifeCost > 0
                    ? $"{activeUnit.AvailableKnives}/{activeUnit.MaxKnives}"
                    : string.Empty;
                spawnedSlots[slotIndex].RefreshAvailability(
                    canUse,
                    isActiveToggle ? 0 : actionCost,
                    isActiveToggle,
                    knifeCount);
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
            cachedSkillOrder = string.Empty;
        }
    }
}
