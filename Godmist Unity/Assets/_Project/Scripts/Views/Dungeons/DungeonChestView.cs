using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonChestView : DungeonObjectView, IInteractable
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private ExplorationPartyController partyController;
        private ChestDungeonObjectInstance chest;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            ChestDungeonObjectInstance nextChest,
            Vector3 worldPosition)
        {
            if (!TryBind(nextChest, worldPosition)) return;

            partyController = nextPartyController;
            chest = nextChest;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(interactionKey)) return;
            if (!CanInteract()) return;

            Interact();
        }

        public bool CanInteract()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (chest == null
                || !chest.IsActive
                || partyController == null
                || partyController.ActiveLeaderTransform == null
                || gameBootstrap == null
                || gameBootstrap.Session == null
                || gameBootstrap.Session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            return offset.sqrMagnitude
                   <= interactionRadius * interactionRadius;
        }

        public void Interact()
        {
            if (!CanInteract()) return;
            if (!chest.TryClaimItems(out var items)) return;

            GameBootstrap.Instance.Session.AddPendingLoot(items);

            Destroy(gameObject);
        }
    }
}