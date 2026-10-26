using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonLeverView : DungeonObjectView, IExplorationWorldInteraction
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color inactiveColor = new(1f, 0.15f, 0.8f, 1f);
        [SerializeField] private Color activeColor = new(0.3f, 0.9f, 0.35f, 1f);
        private bool mirrorHandleWhenActive;

        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private LeverDungeonObjectInstance lever;

        public void ConfigureRuntimePresentation(
            Color nextInactiveColor,
            Color nextActiveColor,
            bool nextMirrorHandleWhenActive)
        {
            inactiveColor = nextInactiveColor;
            activeColor = nextActiveColor;
            mirrorHandleWhenActive = nextMirrorHandleWhenActive;
            RefreshVisual();
        }

        public ExplorationPartyController InteractionParty => partyController;
        public DungeonFloorInstance InteractionFloor => floor;
        public float InteractionRadius => interactionRadius;
        public KeyCode InteractionKey => interactionKey;
        public bool CanRequestInteraction => isActiveAndEnabled && IsElevationVisible && lever?.IsActive == true
            && floor != null && ReferenceEquals(floor, partyController?.InteractionFloor) && floorTilemap != null && partyController?.CanRequestWorldInteraction == true && (lever.IsReversible || !lever.IsActivated);

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            LeverDungeonObjectInstance nextLever,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap)
        {
            if (!TryBind(nextLever, worldPosition)) return;

            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            floor = nextFloor;
            floorTilemap = nextFloorTilemap;
            lever = nextLever;
            visualRenderer ??= GetComponent<SpriteRenderer>();
            if (visualRenderer != null)
            {
                visualRenderer.sortingOrder = Mathf.Max(
                    visualRenderer.sortingOrder,
                    6);
            }
            RefreshVisual();
            ExplorationWorldObjectInteraction.Attach(this, this);
        }

        protected override int ResolveDepthSortingOrder(DungeonFloorMapView view)
            => view.GetStandingObjectDepthSortingOrder(transform.position.y);

        private void OnDestroy()
        {
            registeredPartyController?.UnregisterInteraction(this);
        }

        public bool CanInteract() => CanRequestInteraction && partyController.CanInteractWithGroundObjects
            && partyController.ActiveLeaderTransform != null && CanInteractFrom(partyController.ActiveLeaderTransform.position);
        public bool CanInteractFrom(Vector2 position) => CanRequestInteraction
            && (position-(Vector2)transform.position).sqrMagnitude <= interactionRadius*interactionRadius
            && DungeonExplorationInteractionUtility.HasDirectAccess(floor,
                ToDungeonCell(floorTilemap.WorldToCell(position)), lever.Position);

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public void Interact()
        {
            if (!CanInteract() || !lever.TryToggle(out var isActivated)) return;

            if (isActivated
                && !string.IsNullOrWhiteSpace(lever.ProgressionConditionId))
            {
                floor.TrySatisfyProgressionCondition(
                    lever.ProgressionConditionId,
                    lever.InstanceId);
            }

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visualRenderer != null)
            {
                var isActive = PresentationState(lever != null && lever.IsActivated);
                visualRenderer.color = isActive
                    ? activeColor
                    : inactiveColor;
                if (mirrorHandleWhenActive) visualRenderer.flipX = isActive;
            }
        }
        protected override void RefreshFogVisual() => RefreshVisual();
    }
}
