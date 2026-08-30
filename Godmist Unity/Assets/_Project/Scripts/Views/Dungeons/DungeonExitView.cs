using System;
using Godmist.Core.Enums;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonExitView : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private ExplorationPartyController partyController;
        private Action interactionAction;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            Vector2 position)
        {
            partyController = nextPartyController;
            transform.position = position;
            interactionAction = null;
        }

        public void InitializeTransition(
            ExplorationPartyController nextPartyController,
            Vector2 position,
            Action nextInteractionAction)
        {
            partyController = nextPartyController;
            transform.position = position;
            interactionAction = nextInteractionAction;
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (!Input.GetKeyDown(interactionKey)) return;

            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null
                || gameBootstrap.Session.CurrentMode != GameModeState.Exploration
                || partyController == null
                || partyController.ActiveLeaderTransform == null)
            {
                return;
            }

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            if (offset.sqrMagnitude > interactionRadius * interactionRadius)
            {
                return;
            }

            if (interactionAction != null)
            {
                interactionAction.Invoke();
                return;
            }

            gameBootstrap.TryExitDungeon();
        }
    }
}
