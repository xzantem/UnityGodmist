using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Core.Interfaces;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonExitView : MonoBehaviour, IExplorationWorldInteraction
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [SerializeField] private ItemActionPopupView confirmationPrefab;
        private ItemActionPopupView confirmation;
        private bool requiresClearedExitRoom;
        [SerializeField] private Sprite closedForestSprite;
        [SerializeField] private Sprite closedStairSprite;
        private bool goesUp;
        private Sprite openStandingSprite;
        private DungeonInstance presentationDungeon;
        private DungeonFloorMapView groundTerrain;
        private int lastClearedEncounterCount = -1;
        public bool IsExitBlocked { get; private set; }

        public void ConfigureExitState(DungeonInstance dungeon)
        {
            presentationDungeon = dungeon;
            lastClearedEncounterCount = -1;
            RefreshExitState();
        }

        public void ConfigureGroundPresentation(DungeonFloorMapView terrain)
        {
            if (groundTerrain != null)
            {
                groundTerrain.UnregisterLightingPresentation(transitionRenderer);
            }
            groundTerrain = terrain;
            if (UsesStandingPresentation) return;
            groundTerrain?.RegisterGroundLightingPresentation(transitionRenderer);
        }

        private void RefreshExitState()
        {
            var dungeon = presentationDungeon;
            var clearedCount = dungeon?.ClearedEncounterIds.Count ?? -1;
            if (lastClearedEncounterCount == clearedCount && dungeon != null) return;
            lastClearedEncounterCount = clearedCount;
            IsExitBlocked = requiresClearedExitRoom && !DungeonExitRules.IsExitRoomCleared(dungeon, interactionFloor);
            if (standingRenderer != null)
                standingRenderer.sprite = IsExitBlocked && closedForestSprite != null ? closedForestSprite : openStandingSprite;
            RefreshStairSprite();
        }
        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private Action interactionAction;
        private DungeonFloorInstance interactionFloor;
        [SerializeField] private SpriteRenderer transitionRenderer;
        [SerializeField] private Sprite upwardSprite;
        [SerializeField] private Sprite downwardSprite;
        public void ConfigureFloorDirection(bool upwards)
        {
            goesUp = upwards;
            RefreshStairSprite();
        }
        private void RefreshStairSprite()
        {
            var sprite = !goesUp && IsExitBlocked && closedStairSprite != null
                ? closedStairSprite : goesUp ? upwardSprite : downwardSprite;
            if (transitionRenderer != null && sprite != null) transitionRenderer.sprite = sprite;
        }
        private DungeonFloorMapView standingTerrain;
        private UnityEngine.Rendering.SortingGroup standingGroup;
        private SpriteRenderer standingRenderer;
        private SpriteRenderer[] fogRenderers;
        public bool UsesStandingPresentation => standingGroup != null;

        public void ConfigureFogPresentation()
        {
            fogRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            RefreshFogPresentation();
        }

        private void RefreshFogPresentation()
        {
            if (fogRenderers == null) return;
            var known = partyController != null && ReferenceEquals(interactionFloor, partyController.InteractionFloor)
                && partyController.IsInteractionPositionKnown(transform.position);
            foreach (var renderer in fogRenderers)
                if (renderer != null) renderer.forceRenderingOff = !known;
        }

        public void ConfigureStandingPresentation(DungeonFloorMapView terrain, UnityEngine.Rendering.SortingGroup group)
        {
            standingTerrain?.UnregisterLightingPresentation(standingRenderer);
            standingTerrain = terrain;
            standingGroup = group;
            standingRenderer = group != null ? group.GetComponent<SpriteRenderer>() : null;
            standingTerrain?.RegisterGroundLightingPresentation(standingRenderer, false, true);
            openStandingSprite = standingRenderer != null ? standingRenderer.sprite : null;
            lastClearedEncounterCount = -1;
            RefreshExitState();
            RefreshStandingDepth();
        }

        private void LateUpdate() { RefreshExitState(); RefreshStandingDepth(); RefreshFogPresentation(); }

        private void RefreshStandingDepth()
        {
            if (standingTerrain == null || standingGroup == null) return;
            var layer = standingTerrain.FloorFeedbackSortingLayerID;
            var order = standingTerrain.GetStandingObjectDepthSortingOrder(standingRenderer != null ? standingRenderer.transform.position.y : transform.position.y);
            if (standingGroup.sortingLayerID != layer) standingGroup.sortingLayerID = layer;
            if (standingGroup.sortingOrder != order) standingGroup.sortingOrder = order;
        }

        public ExplorationPartyController InteractionParty => partyController;
        public DungeonFloorInstance InteractionFloor => interactionFloor;
        public float InteractionRadius => interactionRadius;
        public KeyCode InteractionKey => interactionKey;
        public bool CanRequestInteraction => isActiveAndEnabled && partyController?.CanRequestWorldInteraction == true
            && (interactionFloor == null || ReferenceEquals(interactionFloor, partyController.InteractionFloor))
            && partyController.IsInteractionPositionKnown(transform.position) && !IsExitBlocked;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            Vector2 position)
        {
            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            transform.position = position;
            interactionAction = null;
            requiresClearedExitRoom = true;
            interactionFloor = partyController?.InteractionFloor;
            ConfigureExitState(GameBootstrap.Instance?.Session?.ActiveDungeon);
            foreach (var label in GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (label.gameObject == gameObject) label.enabled = false; else label.gameObject.SetActive(false);
            ExplorationWorldObjectInteraction.Attach(this, this);
        }

        public void InitializeTransition(
            ExplorationPartyController nextPartyController,
            Vector2 position,
            Action nextInteractionAction, bool requiresClearedExitRoom = false, bool goesUp = false)
        {
            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            transform.position = position;
            interactionAction = nextInteractionAction;
            this.requiresClearedExitRoom = requiresClearedExitRoom;
            ConfigureFloorDirection(goesUp);
            interactionFloor = partyController?.InteractionFloor;
            ConfigureExitState(GameBootstrap.Instance?.Session?.ActiveDungeon);
            foreach (var label in GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (label.gameObject == gameObject) label.enabled = false; else label.gameObject.SetActive(false);
            ExplorationWorldObjectInteraction.Attach(this, this);
        }

        public void Interact()
        {
            RefreshExitState();
            if (IsExitBlocked || !CanInteract()) return;
            var game = GameBootstrap.Instance;
            var dungeon = game?.Session?.ActiveDungeon;
            if (interactionAction != null) { interactionAction.Invoke(); return; }
            if (dungeon == null || confirmation != null && confirmation.IsModalOpen) return;
            Popup()?.RequestConfirmation("Opuścić loch?", "Czy chcesz zakończyć wyprawę i wrócić do miasta?", () =>
            {
                if (this == null) return "Wyjście nie jest teraz dostępne.";
                RefreshExitState();
                if (!ReferenceEquals(game.Session?.ActiveDungeon, dungeon) || IsExitBlocked || !CanInteract())
                    return "Wyjście nie jest teraz dostępne.";
                return game.TryExitDungeon() ? null : "Wyjście nie jest teraz dostępne.";
            });
        }

        private ItemActionPopupView Popup()
        {
            if (confirmation == null && confirmationPrefab != null && UIWorkspaceWindowManager.Instance != null)
                confirmation = Instantiate(confirmationPrefab, UIWorkspaceWindowManager.Instance.WorkspaceRoot);
            if (confirmation == null) Debug.LogError("Dungeon exit: missing confirmation prefab or workspace.", this);
            return confirmation;
        }

        public bool CanInteract() => CanRequestInteraction && partyController.CanInteractWithGroundObjects
            && partyController.ActiveLeaderTransform != null && CanInteractFrom(partyController.ActiveLeaderTransform.position);
        public bool CanInteractFrom(Vector2 position) => CanRequestInteraction
            && (position-(Vector2)transform.position).sqrMagnitude <= interactionRadius*interactionRadius
            && partyController.HasDirectInteractionAccess(position, transform.position);

        private void OnDestroy()
        {
            standingTerrain?.UnregisterLightingPresentation(standingRenderer);
            groundTerrain?.UnregisterLightingPresentation(transitionRenderer);
            if (confirmation != null) Destroy(confirmation.gameObject);
            registeredPartyController?.UnregisterInteraction(this);
        }
    }
}
