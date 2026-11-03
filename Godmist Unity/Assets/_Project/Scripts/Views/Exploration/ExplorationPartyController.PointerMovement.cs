using Godmist.Core.Enums;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private const float PointerHoldDelay = .2f;
        private const float PointerRepathInterval = .1f;
        private const float PointerTargetDistance = .15f;
        private Mouse pointerMovementMouse;
        private string pointerMovementLeader;
        private float pointerMovementPressedAt, nextPointerMovementRepath;
        private Vector2 lastPointerMovementSample;

        // This transient gesture owns retargeting, never the route's lifetime.
        // Release ends steering; the existing movement owner finishes its route.
        private void BeginPointerMovementGesture(Vector2 point, float now)
        {
            CancelPointerMovementGesture();
            if (!CanSteerPointerMovement || IsWorldPrimaryClickClaimed) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.enabled || !mouse.leftButton.isPressed) return;
            pointerMovementMouse = mouse;
            pointerMovementLeader = gameSession.PlayerParty.ActiveLeaderId;
            pointerMovementPressedAt = now;
            nextPointerMovementRepath = now + PointerHoldDelay;
            lastPointerMovementSample = point;
        }

        private bool CanSteerPointerMovement => CanAcceptExplorationInput
            && gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) == null
            && GetActiveLeaderView()?.RuntimeCharacter?.LifeState == CharacterLifeState.Alive
            && worldCamera != null && Time.timeScale > 0f && !ControllerInput.Active
            && !UIWorkspaceInputRouter.IsPointerOverUi()
            && !UIWorkspaceInputRouter.HasFocusedContextMenu()
            && UIWorkspaceWindowManager.Instance?.HasOpenWindows != true
            && !IsWorldInteractionClaimedThisFrame
            && ExplorationKeyboardInput.ReadMovement().sqrMagnitude == 0f;

        private void LateUpdate() => TickPointerMovementGesture(Time.unscaledTime);

        private void TickPointerMovementGesture(float now)
        {
            if (pointerMovementMouse == null) return;
            if (!CanSteerPointerMovement || pointerMovementMouse != Mouse.current
                || !pointerMovementMouse.added || !pointerMovementMouse.enabled
                || !pointerMovementMouse.leftButton.isPressed
                || !worldCamera.pixelRect.Contains(pointerMovementMouse.position.ReadValue())
                || pointerMovementMouse.rightButton.isPressed || pointerMovementMouse.middleButton.isPressed
                || pointerMovementLeader != gameSession.PlayerParty.ActiveLeaderId)
            { CancelPointerMovementGesture(); return; }
            if (now - pointerMovementPressedAt < PointerHoldDelay || now < nextPointerMovementRepath) return;
            var leader = GetActiveLeaderView();
            if (leader.IsCrossingLowWall || leader.DungeonPosition.IsOnStairs) return;
            // Compare world points, not mouse deltas: a following camera changes
            // the destination under a stationary cursor while the party walks.
            var point = (Vector2)worldCamera.ScreenToWorldPoint(pointerMovementMouse.position.ReadValue());
            if ((point - lastPointerMovementSample).sqrMagnitude < PointerTargetDistance * PointerTargetDistance) return;
            nextPointerMovementRepath = now + PointerRepathInterval;
            lastPointerMovementSample = point;
            // An unreachable point must leave the last accepted route intact.
            TryMoveLeaderToWorldPosition(point);
        }

        private void CancelPointerMovementGesture()
        {
            pointerMovementMouse = null;
            pointerMovementLeader = null;
        }

        private void OnDisable() => CancelPointerMovementGesture();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelPointerMovementGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelPointerMovementGesture(); }
    }
}
