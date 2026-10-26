using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.NPC;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatUnitInspectController
    {
        [Header("Shared world interaction")]
        [SerializeField] private ExplorationPartyController explorationParty;
        [SerializeField] private DungeonEncounterSpawner encounterSpawner;
        [SerializeField] private ExplorationSandboxBootstrap cityBootstrap;
        [SerializeField] private PartyRailMenuView contextMenu;
        [SerializeField] private UIWorkspaceTooltipView worldTooltip;
        [SerializeField] private UIWorkspaceWindowManager workspaceManager;
        private ExplorationCharacterView hoveredView, inspectedView, menuView;
        private NpcView hoveredNpc, menuNpc;
        private BattleUnit menuUnit;
        private KnifeCombatMapObject highlightedKnife;
        private CombatSession previousSession;
        private GameModeState? previousMode;
        private float nextInspectRefresh;
        private object tooltipTarget;
        private object tooltipSource;
        // Keep the view that owns an exploration tooltip independently from
        // the transient per-frame hover fields. A pinned tooltip can remain
        // visible after ResetHover clears those fields.
        private ExplorationCharacterView tooltipTargetView;
        private NpcView tooltipTargetNpc;
        private string tooltipBody;
        public bool HasWorldUnitHover => HoveredUnit != null || hoveredView != null || hoveredNpc != null;
        private int primaryFrame = -1, secondaryFrame = -1;
        public bool ConsumedPrimaryClickThisFrame => primaryFrame == Time.frameCount;
        public bool ConsumedSecondaryClickThisFrame => secondaryFrame == Time.frameCount;

        private void UpdateWorldInteraction()
        {
            // Knife outlines are a transient pointer affordance, independent of
            // the tooltip source. Clear them before every early return so a
            // tooltip/UI pointer transition can never leave one behind.
            ClearKnifeHighlight();
            var game = explorationParty != null ? explorationParty.Session : null;
            var combat = sandboxBootstrap != null ? sandboxBootstrap.Session : null;
            var mode = game?.CurrentMode;
            if (previousSession != combat || previousMode != mode)
            {
                ClearWorldInteraction();
                previousSession = combat; previousMode = mode;
            }
            if (sandboxBootstrap?.IsGameplaySuspended == true || DevConsoleView.IsOpen || (game != null && (game.IsDefeated
                || mode == GameModeState.Dialogue)) || UIWorkspaceInputRouter.HasFocusedTextInput())
            { ClearWorldInteraction(); return; }

            if (inspectedView != null && !inspectedView.IsVisibleForInspection
                || selectedUnit != null && inspectedView == null && !IsCombatTargetVisible(selectedUnit))
            { selectedUnit = null; inspectedView = null; HideInspect(); }
            if (contextMenu != null && contextMenu.gameObject.activeSelf
                && (menuView == null && menuUnit == null && menuNpc == null
                    || menuView != null && !menuView.IsVisibleForInspection
                    || menuUnit != null && !IsCombatTargetVisible(menuUnit)
                    || menuNpc != null && !menuNpc.isActiveAndEnabled)) CloseContextMenu();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Manual tooltip pins are dismissed only by a pointer press outside
                // the complete card chain. Escape remains available for ordinary
                // world inspection, but must not tear down an explicit pin.
                if (worldTooltip?.HasPinnedBranch == true) return;
                ClearWorldInteraction();
                return;
            }

            if (floatingInspectWindow != null && floatingInspectWindow.IsVisible && Time.unscaledTime >= nextInspectRefresh)
            { nextInspectRefresh = Time.unscaledTime + .2f; floatingInspectWindow.Refresh(); }
            if (!IsTooltipTargetValid()) { ClearTooltip(); return; }
            // Tooltip pinning is handled from its screen-space geometry so an
            // unlocked tooltip never needs to intercept world input. Keep the
            // world source/context menu dormant while the pointer is over it.
            if (UIWorkspaceTooltipView.IsPointerOverVisibleTooltip(Input.mousePosition)) return;
            ResetHover();
            if (worldCamera == null) { ClearTooltip(); return; }
            if (UIWorkspaceInputRouter.IsPointerOverUi()
                || UIWorkspaceInputRouter.HasFocusedContextMenu())
            {
                if (worldTooltip?.HasPinnedBranch == true) NotifyTooltipSourceExit();
                else ClearTooltip();
                return;
            }
            var world = (Vector2)worldCamera.ScreenToWorldPoint(Input.mousePosition);
            if (combat != null)
                HoveredUnit = GetUnitUnderMouse();
            else
            {
                if (explorationParty != null)
                    foreach (var view in explorationParty.ActiveMemberViews)
                        if (view != null && view.IsPointerHit(world)) { hoveredView = view; break; }
                if (hoveredView == null && encounterSpawner != null)
                    foreach (var view in encounterSpawner.EnemyViews)
                        if (view != null && view.IsPointerHit(world)) { hoveredView = view; break; }
                if (hoveredView == null && cityBootstrap != null)
                    foreach (var binding in cityBootstrap.NpcBindings)
                        if (binding?.NpcView != null && binding.NpcView.IsPointerHit(world)) { hoveredNpc = binding.NpcView; break; }
            }
            // A unit occupies the same grid cell as its ground objects. Keep
            // the richer unit inspect tooltip as the primary hover target.
            var hoveredKnife = combat != null && HoveredUnit == null
                ? GetVisibleKnifeUnderMouse(combat)
                : null;
            if (hoveredView != null) hoveredView.SetInspectionHighlight(true);
            if (hoveredNpc != null) hoveredNpc.SetInspectionHighlight(true);
            // Target selection already paints eligible knife cells and owns the
            // click. Keep the hover card out of that path so it can never make
            // the real skill target feel like a UI click.
            if (hoveredKnife != null && targetSelectionController?.IsSelectingTarget != true)
            {
                SetKnifeHighlight(hoveredKnife);
                ShowKnifeHover(combat, hoveredKnife);
            }
            else
            {
                ClearKnifeHover();
            }
            var character = HoveredUnit?.Character ?? hoveredView?.RuntimeCharacter;
            if (hoveredKnife == null) ShowHover(character, HoveredUnit ?? hoveredView?.CombatUnit);

            if (combat == null && hoveredView != null && Input.GetMouseButtonDown(0)
                && explorationParty?.CanRequestRevive(hoveredView) == true)
            {
                primaryFrame = Time.frameCount;
                explorationParty.RequestReviveInteraction(hoveredView);
                return;
            }

            if (hoveredNpc != null && Input.GetMouseButtonDown(0))
            {
                primaryFrame = Time.frameCount;
                RequestNpc(hoveredNpc);
                return;
            }
            if (Input.GetMouseButtonDown(0) && HoveredUnit != null
                && (targetSelectionController == null || !targetSelectionController.IsSelectingTarget)
                && Godmist.Systems.Combat.ScoutCombatSystem.CanExtractKnife(combat, combat?.ActiveUnit, HoveredUnit))
            {
                primaryFrame = Time.frameCount;
                sandboxBootstrap.GetDriver(combat)?.TryExtractScoutKnife(combat.ActiveUnit, HoveredUnit);
                return;
            }
            // Right-click during targeting retains cancellation, never opens inspect.
            if (!Input.GetMouseButtonDown(1) || targetSelectionController != null && targetSelectionController.IsSelectingTarget) return;
            if (contextMenu == null || character == null && hoveredNpc == null) return;
            secondaryFrame = Time.frameCount;
            // Opening a source context menu is a soft source transition. It
            // must not tear down a pinned tooltip, including when the menu is
            // opened for a different world target.
            if (worldTooltip?.HasPinnedBranch == true) NotifyTooltipSourceExit();
            else ClearTooltip();
            menuView = hoveredView; menuUnit = HoveredUnit; menuNpc = hoveredNpc;
            if (menuNpc != null)
            {
                var npc = menuNpc;
                contextMenu.Show(Input.mousePosition, new[] {
                    ("Rozmawiaj", (System.Action)(() => RequestNpc(npc)), true) }, npc.DisplayName);
            }
            else
            {
                var view = menuView; var unit = menuUnit;
                var actions = new System.Collections.Generic.List<(string, System.Action, bool)> {
                    ("Inspektuj", () => InspectWorldTarget(view, unit), true) };
                if (combat == null && explorationParty?.CanRequestRevive(view) == true)
                    actions.Insert(0, ("Podnieś", () => explorationParty.RequestReviveInteraction(view), true));
                if (Godmist.Systems.Combat.ScoutCombatSystem.CanExtractKnife(combat, combat?.ActiveUnit, unit))
                {
                    var actor = combat.ActiveUnit;
                    actions.Add(("Wyjmij sztylet", () => sandboxBootstrap.GetDriver(combat)?.TryExtractScoutKnife(actor, unit), true));
                }
                contextMenu.Show(Input.mousePosition, actions, character.Definition?.DisplayName ?? character.CharacterId);
            }
        }

        private void RequestNpc(NpcView npc)
        {
            if (npc == null || explorationParty == null) return;
            if (!explorationParty.RequestNpcInteraction(npc) && worldTooltip != null)
                worldTooltip.ShowCompactText(npc.DisplayName, "Nie można podejść do tej postaci.", npc);
        }

        private bool IsCombatTargetVisible(BattleUnit unit)
        {
            var session = sandboxBootstrap != null ? sandboxBootstrap.Session : null;
            return unit != null && unit.Character != null && unit.Character.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead && session != null && session.Units.Contains(unit)
                && !Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)
                && IsPositionVisible?.Invoke(unit.Position) != false;
        }

        private void InspectWorldTarget(ExplorationCharacterView view, BattleUnit unit)
        {
            if (view != null)
            {
                if (!view.IsVisibleForInspection) return;
                inspectedView = view;
                selectedUnit = view.CombatUnit ?? new BattleUnit { Character = view.RuntimeCharacter, IsPlayerSide = string.IsNullOrEmpty(view.EnemyInstanceId) };
            }
            else
            {
                if (!IsCombatTargetVisible(unit)) return;
                inspectedView = null; selectedUnit = unit;
            }
            ShowUnit(selectedUnit);
        }

        /// <summary>
        /// Opens the existing unit context menu for a unit portrait in the turn
        /// order. The turn-order view has no scene/world hit position, so it
        /// delegates inspection to the same floating inspect window used by
        /// world unit right-clicks.
        /// </summary>
        public bool ShowTurnOrderContextMenu(BattleUnit unit, Vector2 screenPosition)
        {
            if (contextMenu == null
                || !IsCombatTargetVisible(unit)
                || sandboxBootstrap != null && !sandboxBootstrap.IsUnitKnown(unit))
            {
                return false;
            }

            if (worldTooltip?.HasPinnedBranch == true) worldTooltip.NotifySourceExit();
            else ClearTooltip();

            menuView = null;
            menuNpc = null;
            menuUnit = unit;
            contextMenu.Show(screenPosition, new[] {
                ("Inspektuj", (System.Action)(() => InspectWorldTarget(null, unit)), true)
            }, unit.Character.Definition?.DisplayName ?? unit.Character.CharacterId);
            return true;
        }

        private void ShowHover(CharacterInstance character, BattleUnit unit = null)
        {
            if (targetSelectionController != null && targetSelectionController.IsSelectingTarget)
            { ClearTooltip(); return; }
            if (character == null && hoveredNpc == null) { NotifyTooltipSourceExit(); return; }
            object target = (object)unit ?? (object)hoveredView ?? (object)character ?? hoveredNpc;
            var title = character != null ? character.Definition?.DisplayName ?? character.CharacterId : hoveredNpc.DisplayName;
            var unknownEnemy = unit != null
                && !unit.IsPlayerSide
                && sandboxBootstrap != null
                && !sandboxBootstrap.IsUnitKnown(unit);
            var body = character != null
                ? unit != null
                    ? unknownEnemy ? "unit:unknown" : "unit:known"
                    : $"character:{(string.IsNullOrEmpty(hoveredView?.EnemyInstanceId) ? "player" : "enemy")}"
                : "LPM: rozmowa · PPM: menu";
            if (tooltipTarget == target && tooltipBody == body
                && worldTooltip?.IsVisibleOrPendingFor(tooltipSource ?? target) == true)
            {
                worldTooltip?.NotifySourceEnter(tooltipSource ?? target);
                return;
            }
            if (worldTooltip?.HasPinnedBranch == true)
            {
                worldTooltip.NotifySourceExit(tooltipSource ?? this);
                return;
            }
            ClearTooltip();
            tooltipTarget = target;
            tooltipSource = target;
            tooltipTargetView = hoveredView;
            tooltipTargetNpc = hoveredNpc;
            tooltipBody = body;
            if (worldTooltip == null) return;

            if (unknownEnemy)
            {
                worldTooltip.ShowText("Nieznany przeciwnik", string.Empty, target);
                return;
            }

            if (unit != null)
            {
                worldTooltip.ShowUnit(unit, target, true);
                return;
            }

            if (hoveredNpc != null)
            {
                worldTooltip.ShowCompactText(title, body, target);
                return;
            }

            worldTooltip.ShowUnit(character, target, string.IsNullOrEmpty(hoveredView?.EnemyInstanceId));
        }

        private KnifeCombatMapObject GetVisibleKnifeUnderMouse(CombatSession combat)
        {
            if (combat == null || !TryGetGridPositionUnderMouse(out var position)
                || IsPositionVisible?.Invoke(position) == false) return null;
            return combat.MapObjects.OfType<KnifeCombatMapObject>()
                .FirstOrDefault(knife => knife != null && knife.Position == position);
        }

        private void ShowKnifeHover(CombatSession combat, KnifeCombatMapObject knife)
        {
            var owner = combat?.Units?.FirstOrDefault(unit => unit?.Character != null
                && string.Equals(unit.Character.CharacterId, knife.OwnerCharacterId,
                    System.StringComparison.OrdinalIgnoreCase));
            var ownerName = string.IsNullOrWhiteSpace(owner?.Character?.Definition?.DisplayName)
                ? owner?.Character?.CharacterId ?? knife.OwnerCharacterId ?? "nieznany"
                : owner.Character.Definition.DisplayName;
            var body = string.Empty;
            if (tooltipTarget == knife && tooltipBody == body
                && worldTooltip?.IsVisibleOrPendingFor(tooltipSource ?? knife) == true)
            {
                worldTooltip?.NotifySourceEnter(tooltipSource ?? knife);
                return;
            }
            if (worldTooltip?.HasPinnedBranch == true)
            {
                worldTooltip.NotifySourceExit(tooltipSource ?? this);
                return;
            }
            ClearTooltip();
            tooltipTarget = knife;
            tooltipSource = knife;
            tooltipBody = body;
            worldTooltip?.ShowCompactText($"Sztylet - {ownerName}", body, knife);
        }

        private void ClearKnifeHover()
        {
            if (tooltipTarget is KnifeCombatMapObject)
            {
                NotifyTooltipSourceExit();
            }
        }

        private void SetKnifeHighlight(KnifeCombatMapObject knife)
        {
            if (knife == null) return;
            highlightedKnife = knife;
            unitViewSpawner?.SetKnifeHover(knife, true);
        }

        private void ClearKnifeHighlight()
        {
            if (highlightedKnife != null)
                unitViewSpawner?.SetKnifeHover(highlightedKnife, false);
            highlightedKnife = null;
        }

        private void ClearTooltip()
        {
            if (worldTooltip != null)
            {
                if (tooltipSource != null) worldTooltip.InvalidateSource(tooltipSource);
                // Keep compatibility with any legacy pending show still owned by
                // the controller while the scene transitions.
                worldTooltip.InvalidateSource(this);
            }
            tooltipTarget = null;
            tooltipSource = null;
            tooltipTargetView = null;
            tooltipTargetNpc = null;
            tooltipBody = null;
        }

        private void NotifyTooltipSourceExit()
        {
            worldTooltip?.NotifySourceExit(tooltipSource ?? tooltipTarget ?? this);
            // Keep the last world target while the card is still visible. A
            // child can be reached and pinned after its source exits; retaining
            // the target lets the hard fog/death validation close that branch.
            // Once the tooltip has actually hidden, the next source-exit call
            // releases the validation state and normal hover can start again.
            if (worldTooltip?.IsVisible == true || worldTooltip?.HasPinnedBranch == true)
                return;
            tooltipTarget = null;
            tooltipTargetView = null;
            tooltipTargetNpc = null;
            tooltipBody = null;
        }

        private bool IsTooltipTargetValid()
        {
            if (tooltipTarget == null) return true;
            if (tooltipTarget is KnifeCombatMapObject knife)
            {
                var combat = sandboxBootstrap != null ? sandboxBootstrap.Session : null;
                return combat != null && combat.MapObjects.Contains(knife)
                    && IsPositionVisible?.Invoke(knife.Position) != false;
            }
            if (tooltipTarget is BattleUnit unit)
            {
                if (!IsCombatTargetVisible(unit)) return false;
                return unit.IsPlayerSide
                    || sandboxBootstrap == null
                    || sandboxBootstrap.IsUnitKnown(unit);
            }

            if (tooltipTarget is CharacterInstance character)
                return tooltipTargetView != null
                    && ReferenceEquals(tooltipTargetView.RuntimeCharacter, character)
                    && tooltipTargetView.IsVisibleForInspection;

            if (tooltipTarget is ExplorationCharacterView view)
                return view.IsVisibleForInspection;

            if (tooltipTarget is NpcView npc)
                return npc.isActiveAndEnabled;
            if (tooltipTargetNpc != null)
                return tooltipTargetNpc.isActiveAndEnabled;
            return true;
        }
        private void ResetHover()
        {
            if (hoveredView != null) hoveredView.SetInspectionHighlight(false);
            if (hoveredNpc != null) hoveredNpc.SetInspectionHighlight(false);
            hoveredView = null; hoveredNpc = null; HoveredUnit = null;
        }
        private void CloseContextMenu()
        {
            if (contextMenu != null) contextMenu.Hide();
            menuView = null; menuNpc = null; menuUnit = null;
        }
        private void ClearWorldInteraction()
        {
            ClearKnifeHighlight();
            ResetHover(); ClearTooltip(); CloseContextMenu();
            selectedUnit = null; inspectedView = null; HideInspect();
        }
    }
}
