using System.Collections.Generic;
using System.Linq;
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
        private const float ShortcutHeaderHeight = 18f;
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
        private readonly Dictionary<int, CombatSkillSlotView> shortcutSlots = new();
        private readonly Stack<CombatSkillSlotView> spareSlots = new();
        private System.Func<BattleUnit> explorationActor;
        private System.Action<SkillData> selectExplorationSkill;
        private System.Action<SkillData> hoverExplorationSkill;
        public void BindExploration(System.Func<BattleUnit> actor, System.Action<SkillData> selectSkill,
            System.Action<SkillData> hoverSkill = null)
        { explorationActor = actor; selectExplorationSkill = selectSkill; hoverExplorationSkill = hoverSkill; }
        private void SetSkillHover(SkillData skill)
        {
            HoveredSkill = skill;
            if (skill == null)
            { targetSelectionController?.SetHoveredSkill(null); hoverExplorationSkill?.Invoke(null); }
            else if (sandboxBootstrap?.Session != null) targetSelectionController?.SetHoveredSkill(skill);
            else hoverExplorationSkill?.Invoke(skill);
        }
        public BattleUnit DisplayActor => sandboxBootstrap?.Session?.ActiveUnit ?? explorationActor?.Invoke();

        private void SelectSkill(SkillData skill)
        {
            if (sandboxBootstrap?.Session == null) selectExplorationSkill?.Invoke(skill);
            else targetSelectionController?.BeginTargetSelection(skill);
        }
        private BattleUnit cachedActiveUnit;
        private int cachedSkillCount = -1;
        private string cachedSkillOrder = string.Empty;
        private readonly List<SkillData> cachedSkills = new();
        private readonly List<string> cachedSkillIds = new();
        private readonly List<string> cachedLoadoutIds = new();
        private RectOffset originalGridPadding;
        private bool originalGridPaddingCaptured;
        private bool concealed;
        private static readonly Unity.Profiling.ProfilerMarker SkillsSample = new("Godmist.SkillBar.ResolveSkills");
        private static readonly Unity.Profiling.ProfilerMarker RebuildSample = new("Godmist.SkillBar.RebuildSlots");
        private static readonly Unity.Profiling.ProfilerMarker BindSample = new("Godmist.SkillBar.BindSlot");
        private static readonly Unity.Profiling.ProfilerMarker AvailabilitySample = new("Godmist.SkillBar.Availability");
        private static readonly Unity.Profiling.ProfilerMarker InputSample = new("Godmist.SkillBar.Input");

        private void Update()
        {
            if (sandboxBootstrap?.IsGameplaySuspended == true) return;
            var activeUnit = DisplayActor;
            if (activeUnit == null)
            {
                targetSelectionController?.CancelTargetSelection();
                ClearSlots();
                SetShortcutHeaderReserved(false);
                ResetCache();
                return;
            }

            var hideSkills = !activeUnit.IsPlayerSide && !sandboxBootstrap.IsUnitVisible(activeUnit);
            if (concealed != hideSkills)
            {
                concealed = hideSkills;
                targetSelectionController?.CancelTargetSelection();
                tooltipView?.Hide();
                ClearSlots();
                SetShortcutHeaderReserved(false);
                ResetCache();
            }
            if (concealed) return;
            IReadOnlyList<SkillData> activeSkills;
            using (SkillsSample.Auto()) activeSkills = activeUnit.Character?.GetActiveSkills();
            var skillCount = activeSkills?.Count ?? 0;
            var loadout = activeUnit.Character?.ActiveSkillLoadout;
            var loadoutIds = loadout != null && loadout.IsInitialized ? loadout.ActiveSkillSlotIds : null;
            // Entering a targeting preview wraps the same character. Retain the
            // slots so rebuilding them cannot cancel the skill just selected.
            if (cachedActiveUnit?.Character != activeUnit.Character
                || cachedActiveUnit?.IsPlayerSide != activeUnit.IsPlayerSide
                || cachedSkillCount != skillCount
                || !MatchesSkillOrder(activeSkills, loadoutIds))
            {
                RebuildSlots(activeUnit, activeSkills, BuildSkillOrderSignature(activeSkills));
            }
            else
            {
                RefreshSlotVisuals(activeUnit, activeSkills);
            }

            using (InputSample.Auto()) TryActivateSkillHotkey(activeUnit);
        }

        private void RebuildSlots(
            BattleUnit activeUnit,
            IReadOnlyList<SkillData> activeSkills,
            string skillOrder)
        {
            using var sample = RebuildSample.Auto();
            targetSelectionController?.CancelTargetSelection();
            tooltipView?.Hide();
            ClearSlots();

            if (skillBarRoot == null || skillSlotTemplate == null)
            {
                return;
            }

            SetShortcutHeaderReserved(true);

            var activeLoadout = activeUnit.Character?.ActiveSkillLoadout;
            var activeLoadoutSlots = activeLoadout != null && activeLoadout.IsInitialized
                ? activeLoadout.ActiveSkillSlotIds : null;
            foreach (var skill in activeSkills ?? new List<SkillData>())
            {
                if (skill == null)
                {
                    continue;
                }

                using var bindSample = BindSample.Auto();
                var slot = spareSlots.Count > 0 ? spareSlots.Pop() : Instantiate(skillSlotTemplate, skillBarRoot);
                slot.transform.SetAsLastSibling();
                slot.Initialize(
                    skill,
                    tooltipView,
                    () => SelectSkill(skill),
                    () => DisplayActor,
                    hovered => { if(hovered)SetSkillHover(skill);else if(HoveredSkill==skill)SetSkillHover(null); });
                slot.gameObject.SetActive(true);
                var shortcutIndex = spawnedSlots.Count;
                if (activeLoadoutSlots != null)
                {
                    shortcutIndex = -1;
                    for (var i = 0; i < activeLoadoutSlots.Count; i++)
                        if (activeLoadoutSlots[i] == skill.SkillId) { shortcutIndex = i; break; }
                }
                slot.SetShortcut(shortcutIndex);
                if (shortcutIndex >= 0 && shortcutIndex < 10) shortcutSlots[shortcutIndex] = slot;
                spawnedSlots.Add(slot);
            }

            if (viewportLayout != null && skillGrid != null)
            {
                var rows = Mathf.Clamp(Mathf.CeilToInt(spawnedSlots.Count / 5f), 1, 2);
                viewportLayout.preferredHeight = skillGrid.padding.top
                    + rows * skillGrid.cellSize.y
                    + (rows - 1) * skillGrid.spacing.y
                    + skillGrid.padding.bottom;
            }
            if (skillScroll != null) skillScroll.verticalNormalizedPosition = 1f;
            cachedActiveUnit = activeUnit;
            cachedSkillCount = activeSkills?.Count ?? 0;
            cachedSkillOrder = skillOrder ?? string.Empty;
            cachedSkills.Clear(); cachedSkillIds.Clear(); cachedLoadoutIds.Clear();
            if (activeSkills != null) foreach (var skill in activeSkills)
            { cachedSkills.Add(skill); cachedSkillIds.Add(skill?.SkillId); }
            if (activeLoadoutSlots != null) cachedLoadoutIds.AddRange(activeLoadoutSlots);
            RefreshSlotVisuals(activeUnit, activeSkills);
        }

        private void SetShortcutHeaderReserved(bool reserved)
        {
            if (skillGrid == null) return;

            if (!originalGridPaddingCaptured)
            {
                var padding = skillGrid.padding;
                originalGridPadding = new RectOffset(
                    padding.left,
                    padding.right,
                    padding.top,
                    padding.bottom);
                originalGridPaddingCaptured = true;
            }

            skillGrid.padding.top = originalGridPadding.top
                + (reserved ? Mathf.CeilToInt(ShortcutHeaderHeight) : 0);
        }

        private bool MatchesSkillOrder(IReadOnlyList<SkillData> skills, IReadOnlyList<string> loadout)
        {
            if ((skills?.Count ?? 0) != cachedSkills.Count || (loadout?.Count ?? 0) != cachedLoadoutIds.Count) return false;
            for (var i = 0; i < cachedSkills.Count; i++)
                if (skills[i] != cachedSkills[i] || skills[i]?.SkillId != cachedSkillIds[i]) return false;
            for (var i = 0; i < cachedLoadoutIds.Count; i++)
                if (loadout[i] != cachedLoadoutIds[i]) return false;
            return true;
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

        private void RefreshSlotVisuals(BattleUnit activeUnit, IReadOnlyList<SkillData> skills)
        {
            using var sample = AvailabilitySample.Auto();
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

                var canRecoverKnife = skill.CanRecoverKnives && sandboxBootstrap?.Session?.MapObjects
                    .OfType<KnifeCombatMapObject>().Any(k => ScoutCombatSystem.RecoverableKnife(
                        sandboxBootstrap.Session, activeUnit, skill, k.Position) != null) == true;
                var actionCost = PaladinCombatSystem.PreviewHealCost(sandboxBootstrap?.Session,activeUnit,skill,null,CombatActionCostCalculator.CalculateSkillCost(activeUnit, skill));
                var activationActionCost = canRecoverKnife
                    ? CombatActionCostCalculator.CalculateSkillCost(activeUnit, skill, true) : actionCost;
                var resourceCost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(activeUnit, skill, canRecoverKnife);
                var resource = activeUnit.Character.ResourcePool;
                var isActiveToggle = skill.IsToggle
                    && activeUnit.ActiveToggleSkillIds != null
                    && activeUnit.ActiveToggleSkillIds.Contains(skill.SkillId);
                var wasChangedThisTurn = skill.IsToggle
                    && activeUnit.ToggleSkillIdsUsedThisTurn != null
                    && activeUnit.ToggleSkillIdsUsedThisTurn.Contains(skill.SkillId);
                var canPayActivationCost = activeUnit.CurrentActionPoints >= activationActionCost
                    && (resourceCost <= 0
                        || (resource != null
                            && resource.ResourceType == skill.ResourceType
                            && (resource.CurrentValue >= resourceCost
                                || WarriorCombatSystem.CanOverspend(activeUnit,skill,resourceCost))));
                var canPayKnives = skill.KnifeCost <= 0
                    || activeUnit.AvailableKnives >= skill.KnifeCost
                    || skill.FreeKnivesInMelee;
                var canUse = activeUnit.IsPlayerSide && (isActiveToggle
                    || (!wasChangedThisTurn && canPayActivationCost && canPayKnives && WarriorCombatSystem.CanActivate(activeUnit,skill)));
                var reason = !activeUnit.IsPlayerSide ? "Tura przeciwnika."
                    : canUse ? null
                    : wasChangedThisTurn ? "Przełączono już w tej turze."
                    : activeUnit.CurrentActionPoints < activationActionCost ? "Za mało punktów akcji."
                    : !canPayKnives ? "Za mało noży."
                    : "Za mało zasobu lub niewłaściwy typ zasobu.";
                spawnedSlots[slotIndex].RefreshAvailability(
                    canUse,
                    isActiveToggle ? 0 : actionCost,
                    isActiveToggle,
                    string.Empty,
                    targetSelectionController?.SelectedSkill == skill,
                    reason);
                slotIndex++;
            }
        }

        private void ClearSlots()
        {
            SetSkillHover(null);
            foreach (var slot in spawnedSlots)
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    spareSlots.Push(slot);
                }
            }

            spawnedSlots.Clear();
            shortcutSlots.Clear();
        }

        private void TryActivateSkillHotkey(BattleUnit activeUnit)
        {
            if (DevConsoleView.IsOpen
                || UIWorkspaceInputRouter.HasFocusedTextInput()
                || UIWorkspaceInputRouter.HasFocusedContextMenu()
                || activeUnit == null
                || !activeUnit.IsPlayerSide)
            {
                return;
            }

            for (var index = 0; index < 10; index++)
            {
                if (!shortcutSlots.TryGetValue(index, out var slot)) continue;
                var keyboardKey = index == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + index);
                var keypadKey = index == 9 ? KeyCode.Keypad0 : (KeyCode)((int)KeyCode.Keypad1 + index);
                if (!Input.GetKeyDown(keyboardKey)
                    && !Input.GetKeyDown(keypadKey))
                {
                    continue;
                }

                slot.TryActivate();
                return;
            }
        }

        private void ResetCache()
        {
            cachedActiveUnit = null;
            cachedSkillCount = -1;
            cachedSkillOrder = string.Empty;
            cachedSkills.Clear(); cachedSkillIds.Clear(); cachedLoadoutIds.Clear();
        }
    }
}
