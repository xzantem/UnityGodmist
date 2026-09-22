using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatSkillBarView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private RectTransform skillBarRoot;
        [SerializeField] private CombatSkillSlotView skillSlotTemplate;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;

        [SerializeField] private ScrollRect skillScroll;
        [SerializeField] private LayoutElement viewportLayout;
        [SerializeField] private GridLayoutGroup skillGrid;

        public SkillData HoveredSkill { get; private set; }
        private readonly List<CombatSkillSlotView> spawnedSlots = new();
        [System.NonSerialized] private CombatSession cachedSession;
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

            TryActivateSkillHotkey(activeUnit);
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
                    () => targetSelectionController?.BeginTargetSelection(skill),
                    () => sandboxBootstrap?.Session?.ActiveUnit,
                    hovered => { if(hovered)HoveredSkill=skill;else if(HoveredSkill==skill)HoveredSkill=null; });
                slot.SetShortcut(spawnedSlots.Count);
                spawnedSlots.Add(slot);
            }

            if (viewportLayout != null && skillGrid != null)
            {
                var rows = Mathf.Clamp(Mathf.CeilToInt(spawnedSlots.Count / 5f), 1, 2);
                viewportLayout.preferredHeight = rows * skillGrid.cellSize.y + (rows - 1) * skillGrid.spacing.y;
            }
            if (skillScroll != null) skillScroll.verticalNormalizedPosition = 1f;
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
                    && (PassiveEffectCombatExecutionSystem.ResolveResourceCost(activeUnit, skill) <= 0
                        || (resource != null
                            && resource.ResourceType == skill.ResourceType
                            && resource.CurrentValue >= PassiveEffectCombatExecutionSystem.ResolveResourceCost(activeUnit, skill)));
                var canPayKnives = skill.KnifeCost <= 0
                    || activeUnit.AvailableKnives >= skill.KnifeCost;
                var canUse = activeUnit.IsPlayerSide && (isActiveToggle
                    || (!wasChangedThisTurn && canPayActivationCost && canPayKnives));
                var reason = !activeUnit.IsPlayerSide ? "Tura przeciwnika."
                    : canUse ? null
                    : wasChangedThisTurn ? "Przełączono już w tej turze."
                    : activeUnit.CurrentActionPoints < actionCost ? "Za mało punktów akcji."
                    : !canPayKnives ? "Za mało noży."
                    : "Za mało zasobu lub niewłaściwy typ zasobu.";
                var knifeCount = skill.KnifeCost > 0
                    ? $"{activeUnit.AvailableKnives}/{activeUnit.MaxKnives}"
                    : string.Empty;
                spawnedSlots[slotIndex].RefreshAvailability(
                    canUse,
                    isActiveToggle ? 0 : actionCost,
                    isActiveToggle,
                    knifeCount,
                    targetSelectionController?.SelectedSkill == skill,
                    reason);
                slotIndex++;
            }
        }

        private void ClearSlots()
        {
            HoveredSkill=null;
            foreach (var slot in spawnedSlots)
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    Destroy(slot.gameObject);
                }
            }

            spawnedSlots.Clear();
        }

        private void TryActivateSkillHotkey(BattleUnit activeUnit)
        {
            if (DevConsoleView.IsOpen
                || UIWorkspaceInputRouter.HasFocusedTextInput()
                || activeUnit == null
                || !activeUnit.IsPlayerSide)
            {
                return;
            }

            var slotCount = Mathf.Min(10, spawnedSlots.Count);
            for (var index = 0; index < slotCount; index++)
            {
                var keyboardKey = index == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + index);
                var keypadKey = index == 9 ? KeyCode.Keypad0 : (KeyCode)((int)KeyCode.Keypad1 + index);
                if (!Input.GetKeyDown(keyboardKey)
                    && !Input.GetKeyDown(keypadKey))
                {
                    continue;
                }

                spawnedSlots[index]?.TryActivate();
                return;
            }
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
