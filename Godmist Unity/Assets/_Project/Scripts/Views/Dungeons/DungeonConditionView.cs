using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;

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
        private ConditionDungeonObjectInstance condition;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ConditionDungeonObjectInstance nextCondition,
            Vector3 worldPosition)
        {
            if (!TryBind(nextCondition, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
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
            var session = GameBootstrap.Instance?.Session;
            if (condition == null
                || condition.IsSatisfied
                || floor == null
                || partyController?.ActiveLeaderTransform == null
                || session == null
                || session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            return (partyController.ActiveLeaderTransform.position - transform.position)
                       .sqrMagnitude <= interactionRadius * interactionRadius;
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
