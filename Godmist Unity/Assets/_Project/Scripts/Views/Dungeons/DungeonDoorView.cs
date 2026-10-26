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
    public class DungeonDoorView : DungeonObjectView, IExplorationWorldInteraction
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private BoxCollider2D blockingCollider;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private float thickness = 0.16f;
        [SerializeField] private bool preserveSpriteProportions;
        [SerializeField] private Sprite horizontalEdgeSprite;
        [SerializeField] private Sprite verticalEdgeSprite;
        [SerializeField, Min(0)] private float projectedHeight;
        [SerializeField, Range(.1f, 1f)] private float frontHeightRatio = 1f;
        [SerializeField, Min(.1f)] private float sideLengthRatio = 1f;
        public float ProjectedVisualElevation => preserveSpriteProportions && visualRenderer != null
            && verticalEdgeSprite != null && visualRenderer.sprite == verticalEdgeSprite ? projectedHeight : 0;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color closedColor = new(0.45f, 0.28f, 0.12f, 1f);
        [SerializeField] private Color openColor = new(0.45f, 0.28f, 0.12f, 0.2f);

        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private DungeonFloorInstance floor;
        private Tilemap interactionTilemap;
        private DoorDungeonObjectInstance door;

        public ExplorationPartyController InteractionParty => partyController;
        public DungeonFloorInstance InteractionFloor => floor;
        public float InteractionRadius => interactionRadius;
        public KeyCode InteractionKey => interactionKey;
        public bool CanRequestInteraction => isActiveAndEnabled && IsElevationVisible && door?.IsActive == true
            && floor != null && ReferenceEquals(floor, partyController?.InteractionFloor) && interactionTilemap != null
            && (partyController?.CanRequestWorldInteraction == true || CanRequestCombatInteraction)
            && door.InteractionMode != DoorInteractionMode.LeverOnly;
        private bool CanRequestCombatInteraction => door != null
            && partyController?.CanInteractWithCombatDoor(door.InstanceId) == true;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            DoorDungeonObjectInstance nextDoor,
            Tilemap floorTilemap)
        {
            if (nextPartyController == null || nextFloor == null || nextDoor == null || floorTilemap == null)
            {
                return;
            }

            partyController = nextPartyController;
            registeredPartyController?.UnregisterInteraction(this);
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            floor = nextFloor;
            interactionTilemap = floorTilemap;
            door = nextDoor;

            var firstCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    door.Edge.FirstCell.x,
                    door.Edge.FirstCell.y,
                    0));

            var secondCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    door.Edge.SecondCell.x,
                    door.Edge.SecondCell.y,
                    0));

            var worldPosition = (firstCenter + secondCenter) * 0.5f;

            if (!TryBind(door, worldPosition))
            {
                return;
            }

            ConfigureEdgeVisual();
            RefreshState();
            ExplorationWorldObjectInteraction.Attach(this, this, () => CanRequestCombatInteraction);
        }

        private void OnDestroy()
        {
            registeredPartyController?.UnregisterInteraction(this);
        }

        private bool renderedOpen;

        protected override int ResolveDepthSortingOrder(DungeonFloorMapView view)
        {
            if (!preserveSpriteProportions || door == null)
                return base.ResolveDepthSortingOrder(view);
            // Sort at the southern ground edge (also the hinge when open),
            // never at the elevated top or a fixed priority above all actors.
            var vertical = door.Edge.SecondCell.x != door.Edge.FirstCell.x;
            return view.GetDoorDepthSortingOrder(transform.position.y - (vertical ? .5f : 0));
        }

        private void Update()
        {
            if (door != null && renderedOpen != door.IsOpen) RefreshState();
            // The shared pointer owner handles both E and LPM in either mode.
        }
        public bool CanInteract()
        {
            if (isActiveAndEnabled && IsElevationVisible && door != null
                && partyController?.CanInteractWithCombatDoor(door.InstanceId) == true) return true;
            return CanRequestInteraction && partyController.CanInteractWithGroundObjects
                && partyController.ActiveLeaderTransform != null && CanInteractFrom(partyController.ActiveLeaderTransform.position);
        }
        public bool CanInteractFrom(Vector2 position) => CanRequestInteraction
            && (position-(Vector2)transform.position).sqrMagnitude <= interactionRadius*interactionRadius
            && DungeonExplorationInteractionUtility.HasDirectAccessToDoor(floor,
                ToDungeonCell(interactionTilemap.WorldToCell(position)), door.Edge);

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public void Interact()
        {
            if (!CanInteract())
            {
                return;
            }

            if (partyController.TryInteractWithCombatDoor(door.InstanceId))
            {
                RefreshState();
                return;
            }

            // A failed combat interaction must never fall through to the free exploration toggle.
            if (partyController.Session?.CurrentMode != GameModeState.Exploration
                || partyController.Session.GetCombatForCharacter(partyController.Session.PlayerParty.ActiveLeaderId) != null) return;

            if (!floor.TrySetDoorOpen(door.InstanceId, !door.IsOpen))
            {
                return;
            }

            RefreshState();
        }

        private void ConfigureEdgeVisual()
        {
            if (door == null)
            {
                return;
            }

            var delta = door.Edge.SecondCell - door.Edge.FirstCell;
            var size = delta.x != 0
                ? new Vector2(thickness, 1f)
                : new Vector2(1f, thickness);

            if (visualRenderer != null)
            {
                var orientedSprite = delta.x != 0 ? verticalEdgeSprite : horizontalEdgeSprite;
                if (orientedSprite != null) visualRenderer.sprite = orientedSprite;
                visualRenderer.transform.localScale = preserveSpriteProportions ? Vector3.one : new Vector3(
                    size.x,
                    size.y,
                    1f);
            }

            if (blockingCollider != null)
            {
                blockingCollider.size = size;
                blockingCollider.isTrigger = false;
            }
        }

        public void RefreshState()
        {
            if (door == null)
            {
                return;
            }

            renderedOpen = door.IsOpen;
            if (blockingCollider != null)
            {
                blockingCollider.enabled = !door.IsOpen;
            }

            if (visualRenderer != null)
            {
                ApplyPresentation(PresentationState(door.IsOpen));
            }
        }
        private void ApplyPresentation(bool open)
        {
            var verticalEdge = door.Edge.SecondCell.x != door.Edge.FirstCell.x;
            if (preserveSpriteProportions && horizontalEdgeSprite != null && verticalEdgeSprite != null)
            {
                var sideView = verticalEdge != open;
                visualRenderer.sprite = sideView ? verticalEdgeSprite : horizontalEdgeSprite;
                visualRenderer.transform.localScale = new Vector3(1, sideView ? sideLengthRatio : frontHeightRatio, 1);
                // Fixed hinges: west end for a horizontal threshold, south end
                // for a vertical one. Front art is bottom-anchored, so reducing
                // its height keeps the base fixed. Side art stays centered on
                // the actual threshold rather than the neighboring wall cap.
                // Lengthening a centered side sprite needs half the added
                // length in its center so its south end/hinge remains fixed.
                var sideCenter = (sideLengthRatio - 1) * .5f;
                visualRenderer.transform.localPosition = open
                    ? verticalEdge ? new Vector3(.5f, -.5f, 0) : new Vector3(-.5f, projectedHeight + .5f + sideCenter, 0)
                    : verticalEdge ? new Vector3(0, projectedHeight + sideCenter, 0) : Vector3.zero;
            }
            visualRenderer.color = open ? openColor : closedColor;
        }
        protected override void RefreshFogVisual()
        {
            if (visualRenderer != null && door != null) ApplyPresentation(PresentationState(door.IsOpen));
        }
    }
}
