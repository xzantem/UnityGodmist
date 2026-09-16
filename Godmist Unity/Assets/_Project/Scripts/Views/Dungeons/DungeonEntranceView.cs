using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    [DefaultExecutionOrder(-90)]
    public class DungeonEntranceView : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private DungeonDefinition dungeonDefinition;
        [SerializeField] private int seed = 12345;
        [SerializeField] private DungeonSelectorWindowView selector;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Godmist.Views.Combat.CombatUnitVisualStateView hoverOutline;
        [SerializeField] private UIWorkspaceTooltipView tooltip;
        private Collider2D clickArea;
        private bool openedDuringVisit;
        public float InteractionRadius => interactionRadius;
        private void Awake()
        {
            clickArea = GetComponent<Collider2D>();
        }
        private void ClearHover()
        {
            if (hoverOutline != null) hoverOutline.ClearHighlight();
            if (tooltip != null) tooltip.InvalidateSource(this);
        }
        private void OnDisable() => ClearHover();
        public bool CanInteract() => isActiveAndEnabled && partyController != null
            && partyController.CanAcceptExplorationInput && partyController.ActiveLeaderTransform != null
            && ((Vector2)(partyController.ActiveLeaderTransform.position-transform.position)).sqrMagnitude <= interactionRadius*interactionRadius;
        public bool TryInteract()
        {
            if (!CanInteract()) return false;
            var game = GameBootstrap.Instance;
            if (game?.Session == null || game.Session.CurrentMode != GameModeState.Exploration) return false;
            ClearHover();
            partyController.CancelMovement();
            if (selector != null) { openedDuringVisit = true; selector.Open(game, dungeonDefinition); return true; }
            return game.TryEnterDungeon(dungeonDefinition, seed);
        }
        // A small exit margin prevents repeated openings when movement jitters at the boundary.
        private bool CanAutomaticallyOpen()
        {
            if (partyController == null || partyController.ActiveLeaderTransform == null) return false;
            var distance = ((Vector2)(partyController.ActiveLeaderTransform.position - transform.position)).sqrMagnitude;
            if (distance > (interactionRadius + .25f) * (interactionRadius + .25f)) openedDuringVisit = false;
            return !openedDuringVisit && CanInteract() && !partyController.Session.HasCombat;
        }
        private void Update()
        {
            var automaticEntry = CanAutomaticallyOpen();
            // Never automatically use the debug fallback that bypasses choosing an offer.
            if (automaticEntry && selector != null) { TryInteract(); return; }
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()
                || UIWorkspaceInputRouter.IsPointerOverUi() || partyController == null
                || !partyController.CanAcceptExplorationInput || selector != null && selector.IsOpen) { ClearHover(); return; }
            if (Input.GetKeyDown(interactionKey)) { TryInteract(); return; }
            var hovered = !partyController.IsWorldPrimaryClickClaimed && clickArea != null && clickArea.enabled
                && partyController.TryGetPointerWorldPosition(out var point) && clickArea.OverlapPoint(point);
            if (!hovered) { ClearHover(); return; }
            if (hoverOutline != null) hoverOutline.Refresh(null, false, true, false, true);
            if (tooltip != null) tooltip.ShowCompactText("Wyprawy", "Wybierz wyprawę · LPM lub podejdź", this);
            if (!Input.GetMouseButtonDown(0)) return;
            partyController.ClaimWorldPrimaryClick();
            if (!partyController.RequestEntranceInteraction(this) && tooltip != null)
            {
                tooltip.InvalidateSource(this);
                tooltip.ShowCompactText("Wejście do lochu", "Nie można podejść do wejścia.", this);
            }
        }
    }
}
