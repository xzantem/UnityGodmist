using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class EncounterTriggerView : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private float actionPointsPerSpeed = 0.25f;
        [SerializeField] private float combatStartDistance = 0.85f;

        [Header("Exploration Chase")]
        [SerializeField] private float chaseSpeed = 4.5f;

        private readonly Dictionary<ExplorationCharacterView, Vector2>
            homePositionByEnemy = new();

        private ExplorationPartyController partyController;
        private DungeonFloorMap floorMap;
        private DungeonEncounterInstance encounterInstance;
        private readonly DungeonCombatContextFactory dungeonCombatContextFactory = new();
        private IReadOnlyList<ExplorationCharacterView> enemyViews;
        private ExplorationCharacterView chaserView;

        private bool isTriggered;
        private bool hasInitialized;
        private bool isReturningHome;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorMap nextFloorMap,
            DungeonEncounterInstance nextEncounterInstance,
            IReadOnlyList<ExplorationCharacterView> nextEnemyViews)
        {
            partyController = nextPartyController;
            floorMap = nextFloorMap;
            encounterInstance = nextEncounterInstance;
            enemyViews = nextEnemyViews;

            if (partyController == null
                || floorMap == null
                || encounterInstance == null
                || enemyViews == null
                || enemyViews.Count == 0)
            {
                Debug.LogError(
                    "EncounterTriggerView: brak PartyController, mapy, "
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

            foreach (var enemyView in enemyViews)
            {
                if (enemyView == null) continue;

                homePositionByEnemy[enemyView] = enemyView.Position;
            }

            hasInitialized = true;

            if (IsEncounterCleared())
            {
                HideEncounter();
            }
        }

        private void Update()
        {
            if (!hasInitialized || isTriggered) return;

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
            var leaderPosition = partyController.ActiveLeaderTransform.position;

            foreach (var enemyView in enemyViews)
            {
                if (enemyView == null || !enemyView.gameObject.activeInHierarchy)
                {
                    continue;
                }

                enemyView.MoveTowards(
                    leaderPosition,
                    chaseSpeed * Time.deltaTime,
                    0f);
            }

            if (chaserView == null) return;

            encounterInstance.TrySetExplorationPosition(
                ToGridPosition(chaserView.Position));
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
                    <= combatStartDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLeaderInsideEncounterArea()
        {
            var leaderPosition = partyController.ActiveLeaderTransform.position;
            var area = encounterInstance.TriggerArea;

            return leaderPosition.x >= area.xMin
                && leaderPosition.x < area.xMax
                && leaderPosition.y >= area.yMin
                && leaderPosition.y < area.yMax;
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
            if (!encounterInstance.TryClearAlerted()) return;

            isReturningHome = true;
        }

        private void ReturnEnemiesHome()
        {
            var allEnemiesReturned = true;

            foreach (var pair in homePositionByEnemy)
            {
                if (pair.Key == null) continue;

                pair.Key.MoveTowards(
                    pair.Value,
                    chaseSpeed * Time.deltaTime,
                    0f);

                if (Vector2.Distance(pair.Key.Position, pair.Value) > 0.001f)
                {
                    allEnemiesReturned = false;
                }
            }

            if (!allEnemiesReturned) return;

            isReturningHome = false;

            if (chaserView == null) return;

            encounterInstance.TrySetExplorationPosition(
                ToGridPosition(chaserView.Position));
        }

        private void TryStartEncounter()
        {
            var gameBootstrap = GameBootstrap.Instance;
            var gameSession = gameBootstrap != null
                ? gameBootstrap.Session
                : null;

            if (gameSession == null || gameSession.ActiveCombat != null)
            {
                return;
            }

            if (!dungeonCombatContextFactory.TryCreate(
                    gameSession.ActiveDungeon,
                    encounterInstance,
                    out var dungeonCombatContext))
            {
                Debug.LogError(
                    "EncounterTriggerView: nie udalo sie utworzyc DungeonCombatContext.");

                return;
            }

            if (!gameSession.TrySetActiveDungeonCombatContext(
                    dungeonCombatContext))
            {
                Debug.LogError(
                    "EncounterTriggerView: nie udalo sie zapisac DungeonCombatContext.");

                return;
            }

            var factory = new CombatSessionFactory(actionPointsPerSpeed);

            if (!factory.TryCreate(
                    gameSession,
                    encounterInstance,
                    out var combatSession))
            {
                gameSession.TryClearActiveDungeonCombatContext();

                Debug.LogError(
                    "EncounterTriggerView: nie udalo sie utworzyc CombatSession.");

                return;
            }

            Debug.Log(
                $"Dungeon combat grid: {combatSession.Grid.Width}x"
                + $"{combatSession.Grid.Height}, "
                + $"units={combatSession.Units.Count}");

            if (!gameSession.TrySetActiveCombat(combatSession))
            {
                gameSession.TryClearActiveDungeonCombatContext();

                return;
            }

            isTriggered = true;
            gameBootstrap.StartCombat();
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

        private static Vector2Int ToGridPosition(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x),
                Mathf.RoundToInt(worldPosition.y));
        }

        private void OnDrawGizmosSelected()
        {
            if (encounterInstance == null) return;

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