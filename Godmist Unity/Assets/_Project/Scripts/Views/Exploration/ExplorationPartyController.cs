using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
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
        private readonly HashSet<IInteractable> registeredInteractions = new();

        private GameSession gameSession;
        public GameSession Session => gameSession;
        public DungeonMinimapView DungeonMinimap { get; private set; }
        private int mapOverviewInputFrame=-1;
        public bool IsMapOverviewBlockingInput => DungeonMinimap!=null && DungeonMinimap.IsOverviewOpen || mapOverviewInputFrame==Time.frameCount;
        public void BindDungeonMinimap(DungeonMinimapView view) => DungeonMinimap=view;
        public void ConsumeMapOverviewInput() => mapOverviewInputFrame=Time.frameCount;
        private int claimedPrimaryFrame = -1;
        public bool IsWorldPrimaryClickClaimed => claimedPrimaryFrame == Time.frameCount
            || unitInteractions != null && (unitInteractions.ConsumedPrimaryClickThisFrame || unitInteractions.HasWorldUnitHover);
        public bool IsWorldInteractionClaimedThisFrame => claimedPrimaryFrame == Time.frameCount
            || unitInteractions != null && unitInteractions.ConsumedPrimaryClickThisFrame;
        public void ClaimWorldPrimaryClick() => claimedPrimaryFrame = Time.frameCount;
        public bool TryGetPointerWorldPosition(out Vector2 position)
        {
            position = worldCamera != null ? (Vector2)worldCamera.ScreenToWorldPoint(Godmist.Views.UI.ControllerInput.PointerPosition) : default;
            return worldCamera != null;
        }

        [SerializeField] private Godmist.Views.Combat.CombatUnitInspectController unitInteractions;

        [SerializeField] private float arrivalDistance = 0.05f;

        private Camera worldCamera;
        private Vector2? movementDestination;
        private Godmist.Views.Dungeons.DungeonCombatCoordinator combatCoordinator;
        public bool CanAcceptExplorationInput => gameSession != null && !gameSession.IsDefeated
            && !IsMapOverviewBlockingInput
            && gameSession.CurrentMode == GameModeState.Exploration
            && combatCoordinator?.IsPreparingOpening != true
            && (inventoryEquipmentPanel == null || !inventoryEquipmentPanel.IsOpen)
            && !DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput();
        public bool CanCycleLeader => gameSession?.PlayerParty?.ActiveMemberIds?.Count >= 2;
        public bool HasAvailableInteraction
        {
            get
            {
                if (!CanAcceptExplorationInput) return false;

                foreach (var interaction in registeredInteractions)
                {
                    if (interaction is Object unityObject && unityObject == null) continue;
                    if (interaction.CanInteract()) return true;
                }

                return false;
            }
        }

        public void RegisterInteraction(IInteractable interaction)
        {
            if (interaction != null) registeredInteractions.Add(interaction);
        }

        public IInteractable GetControllerInteraction()
        {
            if (!CanAcceptExplorationInput || ActiveLeaderTransform == null) return null;
            IInteractable best = null;
            var distance = float.PositiveInfinity;
            foreach (var interaction in registeredInteractions)
            {
                if (interaction is not Component component || component == null || !interaction.CanInteract()) continue;
                var next = (component.transform.position - ActiveLeaderTransform.position).sqrMagnitude;
                if (next < distance || Mathf.Approximately(next, distance)
                    && best is Component previous && string.CompareOrdinal(component.name, previous.name) < 0)
                { best = interaction; distance = next; }
            }
            return best;
        }

        public IReadOnlyList<IInteractable> GetControllerInteractions() => !CanAcceptExplorationInput || ActiveLeaderTransform == null
            ? System.Array.Empty<IInteractable>()
            : registeredInteractions.Where(value => value is Component component && component != null && value.CanInteract())
                .OrderBy(value => (((Component)value).transform.position - ActiveLeaderTransform.position).sqrMagnitude)
                .ThenBy(value => ((Component)value).name, System.StringComparer.Ordinal).ToArray();

        public void UnregisterInteraction(IInteractable interaction)
        {
            if (interaction != null) registeredInteractions.Remove(interaction);
        }
        public bool IsSeparated { get; private set; }
        public void SetCombatCoordinator(Godmist.Views.Dungeons.DungeonCombatCoordinator value) => combatCoordinator = value;
        public bool CanPrepareAttack(ExplorationCharacterView enemy) => combatCoordinator?.CanPrepareOpening(enemy) == true;
        public void PrepareAttack(ExplorationCharacterView enemy) => combatCoordinator?.TryPrepareOpening(enemy);
        public bool CanInteractWithCombatDoor(string objectId) => combatCoordinator != null
            && combatCoordinator.CanInteractWithDoor(objectId);
        public bool TryInteractWithCombatDoor(string objectId) => combatCoordinator != null
            && combatCoordinator.TryInteractWithDoor(objectId);
        public void RejoinAfterCombat() { IsSeparated = false; CancelMovement(); }
        public void SeparateForCombat()
        {
            IsSeparated = true;
            if (gameSession == null || gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null) CancelMovement();
        }
        public void CancelMovement() { meshRoutes.Clear(); pendingNpc = null; pendingEntrance = null; pendingReviveTarget = null; pendingChest = null; pendingWorldObject = null; destroyTarget = null; movementDestination = null; traversalPath.Clear(); followerRoutes.Clear(); cityLeaderRoute.Clear(); cityFollowerRoutes.Clear(); foreach (var view in viewsById.Values) view.PreferredTraversalDestination = null; }
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

            var canvas = inventoryEquipmentPanel?.GetComponentInParent<Canvas>();
            ContextualInputHintsView.Ensure(canvas)?.BindExploration(this, cameraController);
        }

        private void Update()
        {
            if(IsMapOverviewBlockingInput)return;
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()
                || (UIWorkspaceInputRouter.HasFocusedContextMenu() && Input.GetKeyDown(KeyCode.Tab))) { return; }

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
            if (gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null
                || combatCoordinator?.IsPreparingOpening == true) { return; }

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

            if (claimedPrimaryFrame == Time.frameCount || unitInteractions != null && unitInteractions.ConsumedPrimaryClickThisFrame) return;
            if (!(Input.GetMouseButtonDown(0) || Godmist.Views.UI.ControllerInput.Confirm)
                || worldCamera == null
                || UIWorkspaceInputRouter.IsPointerOverUi())
            {
                return;
            }

            pendingNpc = null; pendingEntrance = null; pendingReviveTarget = null; pendingChest = null; pendingWorldObject = null;
            var point = (Vector2)worldCamera.ScreenToWorldPoint(Godmist.Views.UI.ControllerInput.PointerPosition);
            foreach (var view in viewsById.Values)
                if (view.CharacterId != gameSession.PlayerParty.ActiveLeaderId
                    && (view.TerrainView == null || view.TerrainView.IsTraversalVisible(view.DungeonPosition))
                    && Vector2.Distance(view.Position, point) < .4f && SelectMember(view.CharacterId)) return;
            destroyTarget = null;
            TryMoveLeaderToPointerPosition();
        }

        private void TryMoveLeaderToPointerPosition()
        {
            var pointerWorldPosition = worldCamera.ScreenToWorldPoint(Godmist.Views.UI.ControllerInput.PointerPosition);
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
#if UNITY_EDITOR
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                FixedUpdateCore();
            }
            finally
            {
                RecordPerformanceSample("PartyFixedUpdate", started, null);
            }
#else
            FixedUpdateCore();
#endif
        }

        private void FixedUpdateCore()
        {
            if(IsMapOverviewBlockingInput)return;
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()) return;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || combatCoordinator?.IsPreparingOpening == true
                || gameSession.GetCombatForCharacter(gameSession.PlayerParty.ActiveLeaderId) != null)
            {
                destroyTarget = null; pendingNpc = null; pendingEntrance = null; pendingReviveTarget = null; pendingChest = null; pendingWorldObject = null;
                return;
            }

            if (inventoryEquipmentPanel != null && inventoryEquipmentPanel.IsOpen) return;

            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;
            if (TickReviveApproach()) return;
            if (leaderView.RuntimeCharacter != null && leaderView.RuntimeCharacter.LifeState != CharacterLifeState.Alive) { CancelMovement(); return; }
            if (TickChestApproach()) return;
            if (TickWorldObjectApproach()) return;
            if (traversalFloor?.Map != null && !IsNoclipEnabled)
            {
                using (LeaderTickSample.Auto()) TickTraversalMovement(leaderView);
                using (FollowersTickSample.Auto()) MoveFollowers(leaderView);
                using (PersistPositionSample.Auto()) PersistCurrentPosition(leaderView);
                return;
            }

            if (TickNpcApproach()) return;
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

#if UNITY_EDITOR
        private static int performanceSamplesLogged;
        private static double performanceLogWindowStartedAt = -1d;

        private static bool ShouldRecordPerformanceSample(long started)
            => (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000d / System.Diagnostics.Stopwatch.Frequency >= 50d;

        private static void RecordPerformanceSample(string operation, long started, string context)
        {
            var elapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                                      * 1000d / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds < 50d)
                return;

            var now = Time.realtimeSinceStartup;
            if (performanceLogWindowStartedAt < 0d || now - performanceLogWindowStartedAt >= 60d)
            {
                performanceLogWindowStartedAt = now;
                performanceSamplesLogged = 0;
            }

            if (performanceSamplesLogged >= 10)
                return;

            performanceSamplesLogged++;
            var suffix = string.IsNullOrEmpty(context) ? string.Empty : " " + context;
            UnityEngine.Debug.LogWarning(
                $"[ExplorationPerf] {operation} {elapsedMilliseconds:F1}ms{suffix}");
        }

        private static string FormatTraversalPosition(DungeonTraversalPosition position)
        {
            if (position.IsOnStairs)
                return "stairs";

            return $"({position.Surface.Cell.x},{position.Surface.Cell.y})@{position.Surface.Level}";
        }
#endif

        private void OnDestroy()
        {
            DisposeNavigationMeshes();
            if (gameSession == null) return;

            gameSession.PlayerParty.ActiveLeaderChanged -= HandleActiveLeaderChanged;
            gameSession.PlayerParty.GroupsChanged -= CancelMovement;
            registeredInteractions.Clear();
        }

        private void MoveFollowers(ExplorationCharacterView leaderView)
        {
            if (gameSession.GetCombatForCharacter(leaderView.CharacterId) != null) return;
            using var navigationBatch = BeginTraversalQueryBatch();
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
            // Keyboard movement belongs to the free camera when unlocked; the pad's
            // left stick remains independent from the right-stick camera controls.
            var directMovement = ExplorationKeyboardInput.ReadGameplayMovement(cameraController == null || cameraController.IsFollowingLeader);
            if (directMovement.sqrMagnitude > 0f)
            {
                pendingNpc = null; pendingEntrance = null; pendingReviveTarget = null; pendingChest = null; pendingWorldObject = null; destroyTarget = null; movementDestination = null; cityLeaderRoute.Clear();
                return directMovement;
            }

            if (!movementDestination.HasValue) return Vector2.zero;

            var destination = IsNoclipEnabled
                ? movementDestination.Value
                : CityWaypoint(leaderView, cityLeaderRoute, movementDestination.Value);
            var offset = destination - leaderView.Position;
            var finalOffset = movementDestination.Value - leaderView.Position;

            // Arrival is defined by the requested final goal. CityWaypoint can
            // temporarily return the actor position while a blocked city route
            // is being replanned; that is not completion and must preserve the
            // pending NPC/entrance interaction.
            if (finalOffset.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                movementDestination = null;
                cityLeaderRoute.Clear();
                return Vector2.zero;
            }

            if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance)
                return Vector2.zero;

            var movementStep = Mathf.Max(.0001f, movementSpeed * Time.fixedDeltaTime);
            return offset.normalized * Mathf.Min(1f, offset.magnitude / movementStep);
        }

        public void RestorePartyFormation(Vector2 leaderPosition)
        {
            var leaderView = GetActiveLeaderView();

            if (leaderView == null) return;

            if (GameBootstrap.Instance?.IsDungeonScene == true
                && TryFindSafeDungeonFormationPosition(leaderView, leaderPosition, null, out var safeLeaderPosition))
                leaderPosition = safeLeaderPosition;
            leaderView.SetPosition(leaderPosition);
            Physics2D.SyncTransforms();

            var previousView = leaderView;
            var placed = new List<Vector2> { leaderPosition };

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (characterId == gameSession.PlayerParty.ActiveLeaderId) continue;
                if (!viewsById.TryGetValue(characterId, out var followerView)) continue;

                var followerPosition = GameBootstrap.Instance?.IsDungeonScene == true
                    ? previousView.Position + Vector2.left * followerDistance
                    : FindOpenCityFormationPosition(followerView, previousView.Position, placed);
                if (GameBootstrap.Instance?.IsDungeonScene == true
                    && TryFindSafeDungeonFormationPosition(followerView, followerPosition, placed, out var safeFollowerPosition))
                    followerPosition = safeFollowerPosition;

                followerView.SetPosition(followerPosition);
                placed.Add(followerPosition);
                previousView = followerView;
            }

            Physics2D.SyncTransforms();
        }

        private Vector2 FindOpenCityFormationPosition(
            ExplorationCharacterView follower, Vector2 anchor, IReadOnlyList<Vector2> placed)
        {
            var directions = new[]
            {
                Vector2.left, Vector2.down, Vector2.up, Vector2.right,
                new Vector2(-1f, -1f).normalized, new Vector2(-1f, 1f).normalized,
                new Vector2(1f, -1f).normalized, new Vector2(1f, 1f).normalized
            };
            for (var ring = 1; ring <= 4; ring++)
            {
                foreach (var direction in directions)
                {
                    var candidate = anchor + direction * followerDistance * ring;
                    if (placed.Any(position => Vector2.Distance(position, candidate) < followerDistance * .7f)
                        || !follower.CanStandAt(candidate)
                        || !follower.HasClearMovementSegment(anchor, candidate)) continue;
                    return candidate;
                }
            }

            return anchor;
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

            if (GameBootstrap.Instance?.IsDungeonScene == true
                && TryFindSafeDungeonFormationPosition(leaderView, leaderPosition, null, out var safeLeaderPosition))
                leaderPosition = safeLeaderPosition;
            leaderView.SetPosition(leaderPosition);
            var placed = new List<Vector2> { leaderPosition };

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

                if (GameBootstrap.Instance?.IsDungeonScene == true
                    && TryFindSafeDungeonFormationPosition(characterView, characterPosition, placed, out var safeCharacterPosition))
                    characterPosition = safeCharacterPosition;
                characterView.SetPosition(characterPosition);
                placed.Add(characterPosition);
            }

            Physics2D.SyncTransforms();
        }
    }
}
