using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonLeverView : DungeonObjectView, IInteractable
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Color inactiveColor = new(1f, 0.15f, 0.8f, 1f);
        [SerializeField] private Color activeColor = new(0.3f, 0.9f, 0.35f, 1f);

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private LeverDungeonObjectInstance lever;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            LeverDungeonObjectInstance nextLever,
            Vector3 worldPosition)
        {
            if (!TryBind(nextLever, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
            lever = nextLever;
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
            if (lever == null || floor == null || partyController?.ActiveLeaderTransform == null)
            {
                return false;
            }

            if (session == null || session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            return (partyController.ActiveLeaderTransform.position - transform.position)
                       .sqrMagnitude <= interactionRadius * interactionRadius;
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
                visualRenderer.color = lever != null && lever.IsActivated
                    ? activeColor
                    : inactiveColor;
            }
        }
    }
}
