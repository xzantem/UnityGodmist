using System.Collections.Generic;
using Godmist.Data.Configuration;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class EncounterTriggerView : MonoBehaviour
    {
        private sealed class EnemyNavigationState
        {
            public List<Vector2Int> Path { get; } = new();
            public int NextWaypointIndex { get; set; }
            public Vector2Int TargetCell { get; set; }
        }

        [Header("Combat")]
        private float ActionPointsPerSpeed => GameDefinesRuntime.GetFloat(
            GameDefineIds.ActionPointsPerSpeed, .25f);
        private float CombatStartDistance => GameDefinesRuntime.GetFloat(
            GameDefineIds.EncounterCombatStartDistance, .85f);

        [Header("Exploration Chase")]
        private float ChaseSpeed => GameDefinesRuntime.GetFloat(
            GameDefineIds.EncounterChaseSpeed, 4.5f);
        [SerializeField] private float waypointArrivalDistance = 0.03f;

        private readonly Dictionary<ExplorationCharacterView, Vector2>
            homePositionByEnemy = new();

        private readonly Dictionary<ExplorationCharacterView, EnemyNavigationState>
            navigationByEnemy = new();

        private ExplorationPartyController partyController;
        private DungeonFloorMap floorMap;
        private DungeonFloorMapView floorMapView;
        private DungeonEncounterInstance encounterInstance;
        private readonly DungeonCombatContextFactory dungeonCombatContextFactory = new();
        private IReadOnlyList<ExplorationCharacterView> enemyViews;
        private ExplorationCharacterView chaserView;

        private bool isTriggered;
        private Godmist.Views.Dungeons.DungeonCombatCoordinator combatCoordinator;
        public DungeonEncounterInstance Encounter => encounterInstance;
        public IReadOnlyList<ExplorationCharacterView> EnemyViews => enemyViews;
        public void SetCombatCoordinator(Godmist.Views.Dungeons.DungeonCombatCoordinator value) => combatCoordinator = value;
        public void ReleaseEncounter()
        {
            var survivors = new List<ExplorationCharacterView>();
            navigationByEnemy.Clear();
            foreach (var view in enemyViews)
            {
                if (view == null || !view.gameObject.activeSelf
                    || view.RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Dead)
                {
                    if (view != null) homePositionByEnemy.Remove(view);
                    continue;
                }
                survivors.Add(view);
                navigationByEnemy[view] = new EnemyNavigationState();
            }
            enemyViews = survivors;
            chaserView = survivors.Count > 0 ? survivors[0] : null;
            isTriggered = false;
            isReturningHome = true;
        }
        public void MarkTriggered() => isTriggered = true;
        private bool hasInitialized;
        private bool isReturningHome;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorMap nextFloorMap,
            DungeonFloorMapView nextFloorMapView,
            DungeonEncounterInstance nextEncounterInstance,
            IReadOnlyList<ExplorationCharacterView> nextEnemyViews)
        {
            partyController = nextPartyController;
            floorMap = nextFloorMap;
            floorMapView = nextFloorMapView;
            encounterInstance = nextEncounterInstance;
            enemyViews = nextEnemyViews;

            if (partyController == null
                || floorMap == null
                || floorMapView == null
                || encounterInstance == null
                || enemyViews == null
                || enemyViews.Count == 0)
            {
                Debug.LogError(
                    "EncounterTriggerView: brak PartyController, mapy, widoku mapy, "
                    + "encounteru lub widocznych przeciwnikow.");

                enabled = false;
                return;
            }

            chaserView = enemyViews[0];

            if (chaserView == null)
            {
                Debug.LogError(
                    "EncounterTriggerView: brak lidera grupy przeciwnikow.");

                enabled = false;
                return;
            }

            homePositionByEnemy.Clear();
            navigationByEnemy.Clear();

            foreach (var enemyView in enemyViews)
            {
                if (enemyView == null)
                {
                    continue;
                }

                homePositionByEnemy[enemyView] = enemyView.Position;
                navigationByEnemy[enemyView] = new EnemyNavigationState();
            }

            hasInitialized = true;

            if (IsEncounterCleared())
            {
                HideEncounter();
            }
        }

        private void Update()
        {
            if (!hasInitialized || isTriggered || (combatCoordinator != null && combatCoordinator.IsRoomFighting(encounterInstance.RoomId)))
            {
                return;
            }

            if (IsEncounterCleared())
            {
                HideEncounter();
                return;
            }

            if (partyController?.ActiveLeaderTransform == null)
            {
                return;
            }

            if (isReturningHome)
            {
                if (IsLeaderInEncounterRoom())
                {
                    isReturningHome = false;
                    encounterInstance.TrySetAlerted();
                }
                else
                {
                    ReturnEnemiesHome();
                    return;
                }
            }

            if (!encounterInstance.IsAlerted)
            {
                if (!IsLeaderInEncounterRoom())
                {
                    encounterInstance.TrySetRetreatPosition(
                        ToGridPosition(
                            partyController.ActiveLeaderTransform.position));

                    return;
                }

                encounterInstance.TrySetAlerted();
            }

            if (!IsLeaderInEncounterRoom())
            {
                BeginReturningHome();
                return;
            }

            MoveEnemiesTowardsLeader();

            if (IsAnyEnemyCloseEnoughToStartCombat())
            {
                TryStartEncounter();
            }
        }

        private void MoveEnemiesTowardsLeader()
        {
            var leaderPosition = (Vector2)partyController.ActiveLeaderTransform.position;

            foreach (var enemyView in enemyViews)
            {
                if (enemyView == null || !enemyView.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var chaseStopDistance = Mathf.Max(
                    0f,
                    CombatStartDistance - 0.1f);

                TryMoveEnemyAlongFloor(
                    enemyView,
                    leaderPosition,
                    chaseStopDistance);
            }

            if (chaserView == null)
            {
                return;
            }

            encounterInstance.TrySetExplorationPosition(
                ToGridPosition(chaserView.Position));
        }

        private bool TryMoveEnemyAlongFloor(
            ExplorationCharacterView enemyView,
            Vector2 targetPosition,
            float stopDistance)
        {
            if (enemyView == null || floorMap == null)
            {
                return false;
            }

            if (!navigationByEnemy.TryGetValue(enemyView, out var state))
            {
                state = new EnemyNavigationState();
                navigationByEnemy[enemyView] = state;
            }

            var targetCell = ToGridPosition(targetPosition);

            if (ShouldRebuildPath(enemyView, state, targetCell)
                && !TryBuildPath(enemyView, state, targetCell))
            {
                return false;
            }

            AdvanceReachedWaypoints(enemyView, state);

            if (state.NextWaypointIndex >= state.Path.Count)
            {
                enemyView.MoveTowards(
                    targetPosition,
                    ChaseSpeed * Time.deltaTime,
                    stopDistance);

                return true;
            }

            var nextCell = state.Path[state.NextWaypointIndex];
            var isFinalWaypoint = state.NextWaypointIndex
                == state.Path.Count - 1;

            if (isFinalWaypoint)
            {
                enemyView.MoveTowards(
                    targetPosition,
                    ChaseSpeed * Time.deltaTime,
                    stopDistance);

                return true;
            }

            enemyView.MoveTowards(
                floorMapView.CellToWorldCenter(nextCell),
                ChaseSpeed * Time.deltaTime,
                0f);

            return true;
        }

        private bool ShouldRebuildPath(
            ExplorationCharacterView enemyView,
            EnemyNavigationState state,
            Vector2Int targetCell)
        {
            if (state.Path.Count == 0 || state.TargetCell != targetCell)
            {
                return true;
            }

            if (state.NextWaypointIndex >= state.Path.Count)
            {
                return ToGridPosition(enemyView.Position) != targetCell;
            }

            var currentCell = ToGridPosition(enemyView.Position);
            var nextCell = state.Path[state.NextWaypointIndex];

            return currentCell != nextCell
                && !floorMap.CanMoveBetween(currentCell, nextCell);
        }

        private bool TryBuildPath(
            ExplorationCharacterView enemyView,
            EnemyNavigationState state,
            Vector2Int targetCell)
        {
            var startCell = ToGridPosition(enemyView.Position);

            if (!DungeonExplorationPathfinder.TryFindPath(
                    floorMap,
                    startCell,
                    targetCell,
                    out var path))
            {
                state.Path.Clear();
                state.NextWaypointIndex = 0;

                return false;
            }

            state.Path.Clear();
            state.Path.AddRange(path);
            state.TargetCell = targetCell;
            state.NextWaypointIndex = path.Count > 1 ? 1 : 0;

            return true;
        }

        private void AdvanceReachedWaypoints(
            ExplorationCharacterView enemyView,
            EnemyNavigationState state)
        {
            while (state.NextWaypointIndex < state.Path.Count)
            {
                var waypoint = state.Path[state.NextWaypointIndex];
                var waypointPosition = floorMapView.CellToWorldCenter(waypoint);

                if (Vector2.Distance(enemyView.Position, waypointPosition)
                    > waypointArrivalDistance)
                {
                    return;
                }

                state.NextWaypointIndex++;
            }
        }

        private bool IsAnyEnemyCloseEnoughToStartCombat()
        {
            var leaderPosition = partyController.ActiveLeaderTransform.position;

            foreach (var enemyView in enemyViews)
            {
                if (enemyView == null || !enemyView.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (Vector2.Distance(enemyView.Position, leaderPosition)
                    <= CombatStartDistance + 0.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLeaderInEncounterRoom()
        {
            var leaderCell = ToGridPosition(
                partyController.ActiveLeaderTransform.position);

            return floorMap.TryGetRoomId(leaderCell, out var roomId)
                && roomId == encounterInstance.RoomId;
        }

        private void BeginReturningHome()
        {
            if (!encounterInstance.TryClearAlerted())
            {
                return;
            }

            isReturningHome = true;
        }

        private void ReturnEnemiesHome()
        {
            var allEnemiesReturned = true;

            foreach (var pair in homePositionByEnemy)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                TryMoveEnemyAlongFloor(pair.Key, pair.Value, 0f);

                if (Vector2.Distance(pair.Key.Position, pair.Value) > 0.001f)
                {
                    allEnemiesReturned = false;
                }
            }

            if (!allEnemiesReturned)
            {
                return;
            }

            isReturningHome = false;

            if (chaserView == null)
            {
                return;
            }

            encounterInstance.TrySetExplorationPosition(
                ToGridPosition(chaserView.Position));
        }

        private void TryStartEncounter()
        {
            if (combatCoordinator == null)
            {
                Debug.LogError("EncounterTriggerView: brak DungeonCombatCoordinator.");
                return;
            }
            combatCoordinator.TryStartEncounter(this);
        }

        private bool IsEncounterCleared()
        {
            var activeDungeon = GameBootstrap.Instance?.Session.ActiveDungeon;

            return activeDungeon != null
                && activeDungeon.IsEncounterCleared(
                    encounterInstance.InstanceId);
        }

        private void HideEncounter()
        {
            foreach (var enemyView in enemyViews)
            {
                if (enemyView != null)
                {
                    enemyView.gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(false);
        }

        private Vector2Int ToGridPosition(Vector3 worldPosition)
        {
            return floorMapView.WorldToCell(worldPosition);
        }

        private Vector2Int ToGridPosition(Vector2 worldPosition)
        {
            return floorMapView.WorldToCell(worldPosition);
        }

        private void OnDrawGizmosSelected()
        {
            if (encounterInstance == null)
            {
                return;
            }

            var area = encounterInstance.TriggerArea;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);

            Gizmos.DrawCube(
                new Vector3(
                    area.xMin + area.width * 0.5f,
                    area.yMin + area.height * 0.5f,
                    0f),
                new Vector3(area.width, area.height, 0f));
        }
    }
}
