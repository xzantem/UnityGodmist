using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonEncounterSpawner : MonoBehaviour
    {
        [Header("Encounter Trigger")]
        [SerializeField] private EncounterTriggerView encounterTriggerPrefab;
        [SerializeField] private Transform encountersRoot;

        [Header("Visible Enemies")]
        [SerializeField] private ExplorationCharacterView enemyViewPrefab;
        [SerializeField] private Transform enemyViewsRoot;
        [SerializeField] private Color enemyTint = new(0.85f, 0.35f, 0.35f, 1f);

        [SerializeField] private DungeonFloorMapView floorMapView;

        private readonly List<EncounterTriggerView> spawnedTriggers = new();
        private readonly List<ExplorationCharacterView> spawnedEnemyViews = new();

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController)
        {
            ClearSpawnedViews();

            if (floor == null
                || floor.Map == null
                || partyController == null
                || floorMapView == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak pietra, mapy, PartyController lub DungeonFloorMapView.");

                return false;
            }

            if (encounterTriggerPrefab == null || enemyViewPrefab == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak prefabu triggera lub widocznego przeciwnika.");

                return false;
            }

            foreach (var encounterInstance in floor.Encounters)
            {
                if (encounterInstance == null
                    || IsEncounterCleared(encounterInstance))
                {
                    continue;
                }

                var enemyViews = SpawnEnemyViews(
                    encounterInstance,
                    floor.Map);

                if (enemyViews.Count == 0)
                {
                    continue;
                }

                var triggerParent = encountersRoot != null
                    ? encountersRoot
                    : transform;

                var triggerView = Instantiate(
                    encounterTriggerPrefab,
                    triggerParent);

                triggerView.name =
                    $"EncounterTrigger_{encounterInstance.InstanceId}";

                triggerView.Initialize(
                    partyController,
                    floor.Map,
                    floorMapView,
                    encounterInstance,
                    enemyViews);

                spawnedTriggers.Add(triggerView);
            }

            return true;
        }

        private List<ExplorationCharacterView> SpawnEnemyViews(
            DungeonEncounterInstance encounterInstance,
            DungeonFloorMap floorMap)
        {
            var result = new List<ExplorationCharacterView>();
            var enemies = encounterInstance.Definition?.Enemies;

            if (enemies == null || enemies.Count == 0)
            {
                return result;
            }

            var validEnemyCount = 0;

            foreach (var enemyData in enemies)
            {
                if (enemyData?.CharacterDefinition != null
                    && !encounterInstance.IsEnemyDefeated(
                        enemyData.InstanceId))
                {
                    validEnemyCount++;
                }
            }

            if (validEnemyCount == 0)
            {
                return result;
            }

            var enemyIndex = 0;
            var parent = enemyViewsRoot != null
                ? enemyViewsRoot
                : transform;

            foreach (var enemyData in enemies)
            {
                if (enemyData?.CharacterDefinition == null
                    || encounterInstance.IsEnemyDefeated(
                        enemyData.InstanceId))
                {
                    continue;
                }

                var enemyView = Instantiate(enemyViewPrefab, parent);

                enemyView.name =
                    $"EncounterEnemy_{encounterInstance.InstanceId}_{enemyIndex:00}";

                enemyView.Bind(enemyData.CharacterDefinition);
                enemyView.SetIsActiveLeader(false);
                enemyView.SetTint(enemyTint);

                var cell = enemyIndex == 0
                    && IsRoomFloorCell(
                        floorMap,
                        encounterInstance.RoomId,
                        encounterInstance.ExplorationPosition)
                    ? encounterInstance.ExplorationPosition
                    : GetFormationCell(
                        floorMap,
                        encounterInstance.RoomId,
                        encounterInstance.TriggerArea,
                        enemyIndex,
                        validEnemyCount,
                        encounterInstance.ExplorationPosition);

                enemyView.SetPosition(
                    floorMapView.CellToWorldCenter(cell));

                spawnedEnemyViews.Add(enemyView);
                result.Add(enemyView);

                enemyIndex++;
            }

            return result;
        }

        private static Vector2Int GetFormationCell(
            DungeonFloorMap floorMap,
            string roomId,
            RectInt area,
            int index,
            int memberCount,
            Vector2Int fallbackCell)
        {
            var candidates = new List<Vector2Int>();

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    var cell = new Vector2Int(x, y);

                    if (IsRoomFloorCell(floorMap, roomId, cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return fallbackCell;
            }

            return candidates[index % candidates.Count];
        }

        private static bool IsRoomFloorCell(
            DungeonFloorMap floorMap,
            string roomId,
            Vector2Int cell)
        {
            return floorMap != null
                && floorMap.IsWalkable(cell)
                && floorMap.TryGetRoomId(cell, out var cellRoomId)
                && cellRoomId == roomId;
        }

        private static bool IsEncounterCleared(
            DungeonEncounterInstance encounterInstance)
        {
            var activeDungeon = GameBootstrap.Instance?.Session.ActiveDungeon;

            return activeDungeon != null
                && activeDungeon.IsEncounterCleared(
                    encounterInstance.InstanceId);
        }

        private void OnDestroy()
        {
            ClearSpawnedViews();
        }

        private void ClearSpawnedViews()
        {
            foreach (var triggerView in spawnedTriggers)
            {
                if (triggerView != null)
                {
                    Destroy(triggerView.gameObject);
                }
            }

            spawnedTriggers.Clear();

            foreach (var enemyView in spawnedEnemyViews)
            {
                if (enemyView != null)
                {
                    Destroy(enemyView.gameObject);
                }
            }

            spawnedEnemyViews.Clear();
        }
    }
}
