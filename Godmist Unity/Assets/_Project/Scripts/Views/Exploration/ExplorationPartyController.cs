using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class ExplorationPartyController : MonoBehaviour
    {
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private float followerDistance = 1f;
        [SerializeField] private ExplorationCameraFollow cameraController;
        [SerializeField] private InventoryEquipmentPanelView inventoryEquipmentPanel;

        private readonly Dictionary<string, ExplorationCharacterView> viewsById = new();

        private GameSession gameSession;

        [SerializeField] private float arrivalDistance = 0.05f;

        private Camera worldCamera;
        private Vector2? movementDestination;

        public Transform ActiveLeaderTransform
        {
            get
            {
                var leaderView = GetActiveLeaderView();
                return leaderView != null ? leaderView.transform : null;
            }
        }

        public void Initialize(GameSession session, IReadOnlyList<ExplorationCharacterView> characterViews)
        {
            if (gameSession != null)
            {
                gameSession.PlayerParty.ActiveLeaderChanged -= HandleActiveLeaderChanged;
            }

            gameSession = session;
            worldCamera = Camera.main;
            viewsById.Clear();

            foreach (var characterView in characterViews)
            {
                if (characterView == null || string.IsNullOrWhiteSpace(characterView.CharacterId)) continue;

                viewsById[characterView.CharacterId] = characterView;
            }

            gameSession.PlayerParty.ActiveLeaderChanged += HandleActiveLeaderChanged;

            RefreshLeaderVisuals();
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;

            if (gameSession == null)
            {
                return;
            }

            if (gameSession.ActiveCombat != null)
            {
                return;
            }

            if (inventoryEquipmentPanel != null && inventoryEquipmentPanel.IsOpen)
            {
                return;
            }

            if (gameSession.CurrentMode == GameModeState.Dialogue)
            {
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    TryCycleLeader();
                }

                return;
            }

            if (gameSession.CurrentMode != GameModeState.Exploration)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TryCycleLeader();
            }

            if (!Input.GetMouseButtonDown(0)
                || worldCamera == null
                || UIWorkspaceInputRouter.IsPointerOverUi())
            {
                return;
            }

            TryMoveLeaderToPointerPosition();
        }

        private void TryMoveLeaderToPointerPosition()
        {
            var pointerWorldPosition = worldCamera.ScreenToWorldPoint(Input.mousePosition);

            if (DungeonEnemyBodyView.IsPointerOverBody(pointerWorldPosition))
            {
                return;
            }

            movementDestination = new Vector2(
                pointerWorldPosition.x,
                pointerWorldPosition.y);
        }

        private void FixedUpdate()
        {
            if (DevConsoleView.IsOpen) return;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || gameSession.ActiveCombat != null)
            {
                return;
            }

            if (inventoryEquipmentPanel != null && inventoryEquipmentPanel.IsOpen) return;

            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;

            var movement = GetMovementDirection(leaderView);

            if (movement.sqrMagnitude > 0f)
            {
                leaderView.Move(movement * movementSpeed * Time.fixedDeltaTime);
            }

            MoveFollowers(leaderView);

            if (gameSession.ActiveDungeon != null)
            {
                gameSession.ActiveDungeon.SetPlayerPosition(leaderView.Position);
            }
            else
            {
                gameSession.TrySetExplorationPosition(leaderView.Position);
            }
        }

        private void OnDestroy()
        {
            if (gameSession == null) return;

            gameSession.PlayerParty.ActiveLeaderChanged -= HandleActiveLeaderChanged;
        }

        private void MoveFollowers(ExplorationCharacterView leaderView)
        {
            var previousView = leaderView;

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (characterId == gameSession.PlayerParty.ActiveLeaderId) continue;

                if (!viewsById.TryGetValue(characterId, out var followerView)) continue;

                followerView.MoveTowards(previousView.Position, 
                    movementSpeed * Time.fixedDeltaTime, followerDistance);

                previousView = followerView;
            }
        }

        private void TryCycleLeader()
        {
            var memberIds = gameSession.PlayerParty.ActiveMemberIds;

            if (memberIds.Count < 2) return;

            var currentIndex = 0;

            for (var index = 0; index < memberIds.Count; index++)
            {
                if (memberIds[index] == gameSession.PlayerParty.ActiveLeaderId)
                {
                    currentIndex = index;
                    break;
                }
            }

            for (var offset = 1; offset <= memberIds.Count; offset++)
            {
                var candidateIndex = (currentIndex + offset) % memberIds.Count;

                if (gameSession.TrySetActiveLeader(memberIds[candidateIndex])) return;
            }
        }

        private ExplorationCharacterView GetActiveLeaderView()
        {
            viewsById.TryGetValue(gameSession.PlayerParty.ActiveLeaderId, out var leaderView);

            return leaderView;
        }

        private void HandleActiveLeaderChanged(string previousLeaderId, string nextLeaderId)
        {
            movementDestination = null;
            
            RefreshLeaderVisuals();
        }

        private void RefreshLeaderVisuals()
        {
            if (gameSession == null) return;

            foreach (var characterView in viewsById.Values)
            {
                characterView.SetIsActiveLeader(characterView.CharacterId == gameSession.PlayerParty.ActiveLeaderId);
            }
        }

        private Vector2 GetMovementDirection(ExplorationCharacterView leaderView)
        {
            if (cameraController == null || cameraController.IsFollowingLeader)
            {
                var keyboardMovement = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));

                if (keyboardMovement.sqrMagnitude > 1f)
                {
                    keyboardMovement.Normalize();
                }

                if (keyboardMovement.sqrMagnitude > 0f)
                {
                    movementDestination = null;
                    return keyboardMovement;
                }
            }

            if (!movementDestination.HasValue) return Vector2.zero;

            var offset = movementDestination.Value - leaderView.Position;

            if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                movementDestination = null;
                return Vector2.zero;
            }

            return offset.normalized;
        }

        public void RestorePartyFormation(Vector2 leaderPosition)
        {
            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;

            leaderView.SetPosition(leaderPosition);

            var previousView = leaderView;

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (characterId == gameSession.PlayerParty.ActiveLeaderId) continue;
                if (!viewsById.TryGetValue(characterId, out var followerView)) continue;

                var followerPosition = previousView.Position + Vector2.left * followerDistance;

                followerView.SetPosition(followerPosition);
                previousView = followerView;
            }

            Physics2D.SyncTransforms();
        }

        public IReadOnlyCollection<ExplorationCharacterView> ActiveMemberViews =>
            viewsById.Values;

        public void RestorePartyPositions(
            IReadOnlyDictionary<string, Vector2> positions,
            Vector2 fallbackLeaderPosition)
        {
            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;

            var leaderPosition = fallbackLeaderPosition;
            var leaderId = gameSession.PlayerParty.ActiveLeaderId;

            if (!string.IsNullOrWhiteSpace(leaderId)
                && positions != null
                && positions.TryGetValue(
                    leaderId,
                    out var storedLeaderPosition))
            {
                leaderPosition = storedLeaderPosition;
            }

            leaderView.SetPosition(leaderPosition);

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (characterId == leaderId
                    || !viewsById.TryGetValue(
                        characterId,
                        out var characterView)
                    || positions == null
                    || !positions.TryGetValue(
                        characterId,
                        out var characterPosition))
                {
                    continue;
                }

                characterView.SetPosition(characterPosition);
            }

            Physics2D.SyncTransforms();
        }
    }
}
