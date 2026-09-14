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
    public class DungeonConditionView : DungeonObjectView, IInteractable
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color unsatisfiedColor = new(1f, 0.15f, 0.8f, 1f);
        [SerializeField] private Color satisfiedColor = new(0.3f, 0.9f, 0.35f, 1f);

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private ConditionDungeonObjectInstance condition;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ConditionDungeonObjectInstance nextCondition,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap)
        {
            if (!TryBind(nextCondition, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
            floorTilemap = nextFloorTilemap;
            condition = nextCondition;
            visualRenderer ??= GetComponent<SpriteRenderer>();
            if (visualRenderer != null)
            {
                visualRenderer.sortingOrder = Mathf.Max(
                    visualRenderer.sortingOrder,
                    6);
            }
            RefreshVisual();
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen || !Input.GetKeyDown(interactionKey)) return;
            if (CanInteract()) Interact();
        }

        public bool CanInteract()
        {
            if (!IsElevationVisible || partyController?.CanInteractWithGroundObjects != true) return false;
            var session = GameBootstrap.Instance?.Session;
            if (condition == null
                || condition.IsSatisfied
                || floor == null
                || floorTilemap == null
                || partyController?.ActiveLeaderTransform == null
                || session == null
                || session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            return (partyController.ActiveLeaderTransform.position - transform.position)
                       .sqrMagnitude <= interactionRadius * interactionRadius
                   && DungeonExplorationInteractionUtility.HasDirectAccess(
                       floor,
                       ToDungeonCell(
                           floorTilemap.WorldToCell(
                               partyController.ActiveLeaderTransform.position)),
                       condition.Position);
        }

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public void Interact()
        {
            if (!CanInteract() || !condition.TrySatisfy()) return;

            if (!string.IsNullOrWhiteSpace(condition.ProgressionConditionId))
            {
                floor.TrySatisfyProgressionCondition(
                    condition.ProgressionConditionId,
                    condition.InstanceId);
            }

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visualRenderer != null)
            {
                visualRenderer.color = condition != null && condition.IsSatisfied
                    ? satisfiedColor
                    : unsatisfiedColor;
            }
        }
    }
}
