using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Dungeons;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController : MonoBehaviour
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
        private Godmist.Views.Dungeons.DungeonCombatCoordinator combatCoordinator;
        public bool CanAcceptExplorationInput => gameSession != null && !gameSession.IsDefeated
            && gameSession.CurrentMode == GameModeState.Exploration
            && (inventoryEquipmentPanel == null || !inventoryEquipmentPanel.IsOpen)
            && !DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput();
        public bool IsSeparated { get; private set; }
        public void SetCombatCoordinator(Godmist.Views.Dungeons.DungeonCombatCoordinator value) => combatCoordinator = value;
        public void RejoinAfterCombat() { IsSeparated = false; CancelMovement(); }
        public void SeparateForCombat()
        {
            IsSeparated = true;
            if (gameSession == null || gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null) CancelMovement();
        }
        public void CancelMovement() { destroyTarget = null; movementDestination = null; traversalPath.Clear(); followerRoutes.Clear(); cityLeaderRoute.Clear(); cityFollowerRoutes.Clear(); foreach (var view in viewsById.Values) view.PreferredTraversalDestination = null; }
        public void CloseInventoryForCombat() => inventoryEquipmentPanel?.Close();
        public void RefreshControlState() => RefreshLeaderVisuals();

        public void RemoveUnavailableViews()
        {
            var removed = new List<string>();
            foreach (var pair in viewsById)
                if (!gameSession.PlayerParty.IsActiveMember(pair.Key))
                { pair.Value.gameObject.SetActive(false); removed.Add(pair.Key); }
            foreach (var id in removed) viewsById.Remove(id);
            RefreshLeaderVisuals();
        }

        public bool SelectMember(string id)
        {
            if (gameSession == null || gameSession.IsDefeated || !gameSession.TrySetActiveLeader(id)) return false;
            CancelMovement();
            combatCoordinator?.RefreshFocus();
            return true;
        }

        public void CenterMember(string id)
        {
            if (!viewsById.TryGetValue(id, out var view)) return;
            if (gameSession.GetCombatForCharacter(id) != null && combatCoordinator != null)
            {
                SelectMember(id);
                combatCoordinator.CenterPartyMember(view.CombatUnit);
            }
            else cameraController?.CenterOn(view.transform.position, id == gameSession.PlayerParty.ActiveLeaderId);
        }

        public bool IsNoclipEnabled { get; private set; }

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
                gameSession.PlayerParty.GroupsChanged -= CancelMovement;
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
            gameSession.PlayerParty.GroupsChanged += CancelMovement;

            RefreshLeaderVisuals();
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) { return; }

            if (gameSession == null || gameSession.IsDefeated)
            {
                return;
            }

            if (combatCoordinator != null && Input.GetKeyDown(KeyCode.Tab))
            {
                TryCycleLeader();
                combatCoordinator.RefreshFocus();
                return;
            }
            if (gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null) { return; }

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

            var point = (Vector2)worldCamera.ScreenToWorldPoint(Input.mousePosition);
            foreach (var view in viewsById.Values)
                if (view.CharacterId != gameSession.PlayerParty.ActiveLeaderId
                    && (view.TerrainView == null || view.TerrainView.IsTraversalVisible(view.DungeonPosition))
                    && Vector2.Distance(view.Position, point) < .4f && SelectMember(view.CharacterId)) return;
            destroyTarget = null;
            TryMoveLeaderToPointerPosition();
        }

        private void TryMoveLeaderToPointerPosition()
        {
            var pointerWorldPosition = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            var terrainView = GetActiveLeaderView()?.TerrainView;
            if (!IsNoclipEnabled && traversalFloor?.Map != null)
            { TrySetTraversalDestination(pointerWorldPosition); return; }

            if (DungeonEnemyBodyView.IsPointerOverBody(pointerWorldPosition))
            {
                return;
            }

            if (!IsNoclipEnabled) { TrySetCityDestination(pointerWorldPosition); return; }
            movementDestination = new Vector2(pointerWorldPosition.x, pointerWorldPosition.y);
        }

        private void FixedUpdate()
        {
            if (DevConsoleView.IsOpen) return;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null)
            {
                destroyTarget = null;
                return;
            }

            if (inventoryEquipmentPanel != null && inventoryEquipmentPanel.IsOpen) return;

            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;
            if (traversalFloor?.Map != null && !IsNoclipEnabled)
            {
                TickTraversalMovement(leaderView);
                MoveFollowers(leaderView);
                PersistCurrentPosition(leaderView);
                return;
            }

            var movement = GetMovementDirection(leaderView);
            TickDestroyObstacle(leaderView);

            if (movement.sqrMagnitude > 0f)
            {
                leaderView.TerrainView?.FollowElevation(0, true);
                var offset = movement * movementSpeed * Time.fixedDeltaTime;
                if (IsNoclipEnabled)
                {
                    leaderView.SetPosition(leaderView.Position + offset);
                }
                else
                {
                    leaderView.Move(offset);
                }
            }

            MoveFollowers(leaderView);

            PersistCurrentPosition(leaderView);
        }

        private void OnDestroy()
        {
            if (gameSession == null) return;

            gameSession.PlayerParty.ActiveLeaderChanged -= HandleActiveLeaderChanged;
            gameSession.PlayerParty.GroupsChanged -= CancelMovement;
        }

        private void MoveFollowers(ExplorationCharacterView leaderView)
        {
            if (gameSession.GetCombatForCharacter(leaderView.CharacterId) != null) return;
            var previousView = leaderView;

            foreach (var characterId in gameSession.PlayerParty.GetGroup(gameSession.PlayerParty.GetGroupIndex(leaderView.CharacterId)))
            {
                if (characterId == gameSession.PlayerParty.ActiveLeaderId
                    || !gameSession.PlayerParty.AreGrouped(characterId, leaderView.CharacterId)
                    || gameSession.GetCombatForCharacter(characterId) != null) continue;

                if (!viewsById.TryGetValue(characterId, out var followerView)
                    || followerView.RuntimeCharacter?.LifeState != CharacterLifeState.Alive) continue;

                if (traversalFloor?.Map != null && !IsNoclipEnabled) FollowTraversal(followerView, previousView);
                else if (!IsNoclipEnabled) FollowCity(followerView, previousView);
                else followerView.MoveTowards(previousView.Position,
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
            if (gameSession == null || gameSession.PlayerParty == null)
            {
                return null;
            }

            viewsById.TryGetValue(gameSession.PlayerParty.ActiveLeaderId, out var leaderView);

            return leaderView;
        }

        private void HandleActiveLeaderChanged(string previousLeaderId, string nextLeaderId)
        {
            CancelMovement();
            var leader = GetActiveLeaderView();
            leader?.TerrainView?.FollowElevation(leader.PresentationLevel, true);
            RefreshLeaderVisuals();
        }

        private void RefreshLeaderVisuals()
        {
            if (gameSession == null) return;

            foreach (var characterView in viewsById.Values)
            {
                characterView.SetIsActiveLeader(characterView.CombatUnit == null && characterView.CharacterId == gameSession.PlayerParty.ActiveLeaderId);
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
                    destroyTarget = null; movementDestination = null; cityLeaderRoute.Clear();
                    return keyboardMovement;
                }
            }

            if (!movementDestination.HasValue) return Vector2.zero;

            var destination = IsNoclipEnabled ? movementDestination.Value : CityWaypoint(leaderView, cityLeaderRoute, movementDestination.Value);
            var offset = destination - leaderView.Position;

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

        public void SetNoclip(bool enabled)
        {
            IsNoclipEnabled = enabled;
            CancelMovement();

            foreach (var characterView in viewsById.Values)
            {
                characterView.SetNoclip(enabled);
            }

            RefreshLeaderVisuals();
        }

        public bool TryTeleport(Vector2 position)
        {
            if (gameSession == null || gameSession.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            RestorePartyFormation(position);
            PersistCurrentPosition();
            return true;
        }

        public void PersistCurrentPosition()
        {
            PersistCurrentPosition(GetActiveLeaderView());
        }

        private void PersistCurrentPosition(ExplorationCharacterView leaderView)
        {
            if (gameSession == null || leaderView == null) return;

            if (gameSession.ActiveDungeon != null
                && GameBootstrap.Instance != null
                && GameBootstrap.Instance.IsDungeonScene)
            {
                gameSession.ActiveDungeon.SetPlayerPosition(leaderView.Position);
                return;
            }

            // The town has its own position. An active dungeon is deliberately
            // kept in the session while the player is in Arungard, but town
            // movement must never overwrite the dungeon resume position.
            gameSession.TrySetExplorationPosition(leaderView.Position);
        }

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
