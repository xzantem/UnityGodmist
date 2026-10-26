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
    public class DungeonConditionView : DungeonObjectView, IExplorationWorldInteraction
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color unsatisfiedColor = new(1f, 0.15f, 0.8f, 1f);
        [SerializeField] private Color satisfiedColor = new(0.3f, 0.9f, 0.35f, 1f);

        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private ConditionDungeonObjectInstance condition;

        public void ConfigureRuntimePresentation(
            Color nextUnsatisfiedColor,
            Color nextSatisfiedColor)
        {
            unsatisfiedColor = nextUnsatisfiedColor;
            satisfiedColor = nextSatisfiedColor;
            RefreshVisual();
        }

        public ExplorationPartyController InteractionParty => partyController;
        public DungeonFloorInstance InteractionFloor => floor;
        public float InteractionRadius => interactionRadius;
        public KeyCode InteractionKey => interactionKey;
        public bool CanRequestInteraction => isActiveAndEnabled && IsElevationVisible && condition?.IsActive == true
            && floor != null && ReferenceEquals(floor, partyController?.InteractionFloor) && floorTilemap != null && partyController?.CanRequestWorldInteraction == true && !condition.IsSatisfied;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ConditionDungeonObjectInstance nextCondition,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap)
        {
            if (!TryBind(nextCondition, worldPosition)) return;

            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
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
            ExplorationWorldObjectInteraction.Attach(this, this);
        }

        private void OnDestroy()
        {
            registeredPartyController?.UnregisterInteraction(this);
        }

        public bool CanInteract() => CanRequestInteraction && partyController.CanInteractWithGroundObjects
            && partyController.ActiveLeaderTransform != null && CanInteractFrom(partyController.ActiveLeaderTransform.position);
        public bool CanInteractFrom(Vector2 position) => CanRequestInteraction
            && (position-(Vector2)transform.position).sqrMagnitude <= interactionRadius*interactionRadius
            && DungeonExplorationInteractionUtility.HasDirectAccess(floor,
                ToDungeonCell(floorTilemap.WorldToCell(position)), condition.Position);

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
                visualRenderer.color = PresentationState(condition != null && condition.IsSatisfied)
                    ? satisfiedColor
                    : unsatisfiedColor;
            }
        }
        protected override void RefreshFogVisual() => RefreshVisual();
    }
}
