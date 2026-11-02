using Godmist.Systems.Dungeons;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private ExplorationWorldObjectInteraction pendingWorldObject;
        public DungeonFloorInstance InteractionFloor => traversalFloor;
        public bool CanRequestWorldInteraction => CanAcceptExplorationInput && gameSession.ActiveCombat == null
            && combatCoordinator?.IsPreparingOpening != true && ActiveLeaderTransform != null
            && gameSession.CharactersById.TryGetValue(gameSession.PlayerParty.ActiveLeaderId, out var leader)
            && leader.LifeState == CharacterLifeState.Alive && !gameSession.IsCharacterInCombat(leader.CharacterId);

        public bool HasDirectInteractionAccess(Vector2 from, Vector2 target)
            => traversalFloor?.Map == null || traversalMapView == null
                || DungeonExplorationInteractionUtility.HasDirectAccess(traversalFloor,
                    traversalMapView.WorldToCell(from), traversalMapView.WorldToCell(target));

        public bool IsInteractionPositionKnown(Vector2 point)
        {
            if (traversalFloor?.Map == null || traversalMapView == null) return true;
            var cell = traversalMapView.WorldToCell(point);
            return !traversalMapView.IsSurfaceOccluded(new DungeonSurfacePosition(cell))
                && (traversalFloor.Fog.DebugFogDisabled || traversalFloor.Fog.Get(
                    new DungeonTraversalPosition(new DungeonSurfacePosition(cell))) != DungeonKnowledge.Unexplored);
        }

        public bool RequestWorldObjectInteraction(ExplorationWorldObjectInteraction target)
        {
            if (target == null || target.Party != this || !target.CanRequestInteraction) return false;
            CancelMovement();
            if (target.CanInteract()) { target.Interact(); return true; }
            if (!TryApproachPosition(target.Position, target.InteractionRadius,
                goal => target.CanInteractFrom(goal) && (traversalMapView == null
                    || traversalMapView.TryGetVisibleTraversal(goal, out var position)
                        && !position.IsOnStairs && position.Surface.Level == 0))) return false;
            pendingWorldObject = target;
            return true;
        }

        private bool TickWorldObjectApproach()
        {
            if (ReferenceEquals(pendingWorldObject, null)) return false;
            if (pendingWorldObject == null || !pendingWorldObject.CanRequestInteraction
                || ExplorationKeyboardInput.ReadGameplayMovement().sqrMagnitude > 0)
            { CancelMovement(); return false; }
            if (pendingWorldObject.CanInteract())
            {
                var target = pendingWorldObject; CancelMovement(); target.Interact(); return true;
            }
            if (!movementDestination.HasValue) CancelMovement();
            return false;
        }
    }
}
