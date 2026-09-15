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
        private CombatSession previousSession;
        private GameModeState? previousMode;
        private float nextInspectRefresh;
        private object tooltipTarget;
        private string tooltipBody;
        public bool HasWorldUnitHover => HoveredUnit != null || hoveredView != null || hoveredNpc != null;
        private int primaryFrame = -1, secondaryFrame = -1;
        public bool ConsumedPrimaryClickThisFrame => primaryFrame == Time.frameCount;
        public bool ConsumedSecondaryClickThisFrame => secondaryFrame == Time.frameCount;

        private void UpdateWorldInteraction()
        {
            var game = explorationParty != null ? explorationParty.Session : null;
            var combat = sandboxBootstrap != null ? sandboxBootstrap.Session : null;
            var mode = game?.CurrentMode;
            if (previousSession != combat || previousMode != mode)
            {
                ClearWorldInteraction();
                previousSession = combat; previousMode = mode;
            }
            if (DevConsoleView.IsOpen || (game != null && (game.IsDefeated
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
            if (Input.GetKeyDown(KeyCode.Escape)) { ClearWorldInteraction(); return; }

            if (floatingInspectWindow != null && floatingInspectWindow.IsVisible && Time.unscaledTime >= nextInspectRefresh)
            { nextInspectRefresh = Time.unscaledTime + .2f; floatingInspectWindow.Refresh(); }
            ResetHover();
            if (worldCamera == null || UIWorkspaceInputRouter.IsPointerOverUi()) { ClearTooltip(); return; }
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
            if (hoveredView != null) hoveredView.SetInspectionHighlight(true);
            if (hoveredNpc != null) hoveredNpc.SetInspectionHighlight(true);
            var character = HoveredUnit?.Character ?? hoveredView?.RuntimeCharacter;
            ShowHover(character);

            if (hoveredNpc != null && Input.GetMouseButtonDown(0))
            {
                primaryFrame = Time.frameCount;
                RequestNpc(hoveredNpc);
                return;
            }
            // Right-click during targeting retains cancellation, never opens inspect.
            if (!Input.GetMouseButtonDown(1) || targetSelectionController != null && targetSelectionController.IsSelectingTarget) return;
            if (contextMenu == null || character == null && hoveredNpc == null) return;
            secondaryFrame = Time.frameCount;
            ClearTooltip();
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
                contextMenu.Show(Input.mousePosition, new[] {
                    ("Inspektuj", (System.Action)(() => InspectWorldTarget(view, unit)), true) }, character.Definition?.DisplayName ?? character.CharacterId);
            }
        }

        private void RequestNpc(NpcView npc)
        {
            if (npc == null || explorationParty == null) return;
            if (!explorationParty.RequestNpcInteraction(npc) && worldTooltip != null)
                worldTooltip.ShowCompactText(npc.DisplayName, "Nie można podejść do tej postaci.", this);
        }

        private bool IsCombatTargetVisible(BattleUnit unit)
        {
            var session = sandboxBootstrap != null ? sandboxBootstrap.Session : null;
            return unit != null && unit.Character != null && unit.Character.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead && session != null && session.Units.Contains(unit)
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

        private void ShowHover(CharacterInstance character)
        {
            if (character == null && hoveredNpc == null) { ClearTooltip(); return; }
            object target = (object)character ?? hoveredNpc;
            var title = character != null ? character.Definition?.DisplayName ?? character.CharacterId : hoveredNpc.DisplayName;
            var body = character != null
                ? $"HP: {character.CurrentHealth}/{Mathf.Max(1, Mathf.RoundToInt(character.Stats?.MaxHealth.GetValue() ?? character.CurrentHealth))}"
                : "LPM: rozmowa · PPM: menu";
            if (tooltipTarget == target && tooltipBody == body) return;
            ClearTooltip(); tooltipTarget = target; tooltipBody = body;
            if (worldTooltip != null) worldTooltip.ShowCompactText(title, body, this);
        }

        private void ClearTooltip()
        {
            if (worldTooltip != null) worldTooltip.InvalidateSource(this);
            tooltipTarget = null; tooltipBody = null;
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
            ResetHover(); ClearTooltip(); CloseContextMenu();
            selectedUnit = null; inspectedView = null; HideInspect();
        }
    }
}
