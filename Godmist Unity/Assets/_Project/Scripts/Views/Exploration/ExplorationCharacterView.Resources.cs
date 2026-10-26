using Godmist.Core.Enums;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationCharacterView
    {
        private bool pendingExplorationMovement;
        private Vector2 pendingMovementOrigin;
        private float pendingMovementDistance;

        private bool TracksExplorationMomentum => CombatUnit == null && !noclipEnabled
            && RuntimeCharacter?.LifeState == CharacterLifeState.Alive
            && RuntimeCharacter.ResourcePool?.ResourceType == ResourceType.Momentum;

        private void RecordExplorationMovement(float worldDistance)
        {
            if (!TracksExplorationMomentum || worldDistance <= 0f) return;
            var size = terrainView == null ? 1f : Vector2.Distance(
                terrainView.CellToWorldCenter(Vector2Int.zero), terrainView.CellToWorldCenter(Vector2Int.right));
            if (size > .0001f) ExplorationStatusEffectSystem.ProcessMovement(RuntimeCharacter, worldDistance / size);
        }

        private void QueueExplorationMovement(float requestedDistance)
        {
            if (!TracksExplorationMomentum) return;
            FlushExplorationMovement();
            if (!pendingExplorationMovement)
            {
                pendingExplorationMovement = true;
                pendingMovementOrigin = Position;
            }
            pendingMovementDistance += requestedDistance;
        }

        private void FlushExplorationMovement()
        {
            if (!pendingExplorationMovement) return;
            if (!TracksExplorationMomentum) { ClearPendingExplorationMovement(); return; }
            // MovePosition is deferred until physics. Keep the request until the
            // body really moves; collision-blocked input never resets the idle timer.
            var distance = Vector2.Distance(pendingMovementOrigin, Position);
            if (distance <= 0f) return;
            RecordExplorationMovement(Mathf.Min(distance, pendingMovementDistance));
            ClearPendingExplorationMovement();
        }

        private void ClearPendingExplorationMovement()
        {
            pendingExplorationMovement = false;
            pendingMovementDistance = 0f;
        }
    }
}
