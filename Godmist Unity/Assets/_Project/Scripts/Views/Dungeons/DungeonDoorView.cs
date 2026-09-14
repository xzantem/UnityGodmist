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
    public class DungeonDoorView : DungeonObjectView, IInteractable
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private BoxCollider2D blockingCollider;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private float thickness = 0.16f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color closedColor = new(0.45f, 0.28f, 0.12f, 1f);
        [SerializeField] private Color openColor = new(0.45f, 0.28f, 0.12f, 0.2f);

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private Tilemap interactionTilemap;
        private DoorDungeonObjectInstance door;

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
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (!Input.GetKeyDown(interactionKey))
            {
                return;
            }

            if (!CanInteract())
            {
                return;
            }

            Interact();
        }

        public bool CanInteract()
        {
            if (!IsElevationVisible || partyController?.CanInteractWithGroundObjects != true) return false;
            var gameBootstrap = GameBootstrap.Instance;

            if (door == null
                || floor == null
                || partyController == null
                || partyController.ActiveLeaderTransform == null
                || interactionTilemap == null
                || gameBootstrap == null
                || gameBootstrap.Session == null
                || gameBootstrap.Session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            if (door.InteractionMode == DoorInteractionMode.LeverOnly)
            {
                return false;
            }

            var offset = partyController.ActiveLeaderTransform.position - transform.position;

            return offset.sqrMagnitude <= interactionRadius * interactionRadius
                   && DungeonExplorationInteractionUtility.HasDirectAccessToDoor(
                       floor,
                       ToDungeonCell(
                           interactionTilemap.WorldToCell(
                               partyController.ActiveLeaderTransform.position)),
                       door.Edge);
        }

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
                visualRenderer.transform.localScale = new Vector3(
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

            if (blockingCollider != null)
            {
                blockingCollider.enabled = !door.IsOpen;
            }

            if (visualRenderer != null)
            {
                visualRenderer.color = door.IsOpen
                    ? openColor
                    : closedColor;
            }
        }
    }
}
