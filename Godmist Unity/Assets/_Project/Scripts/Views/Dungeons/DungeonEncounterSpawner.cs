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

        private readonly List<EncounterTriggerView> spawnedTriggers = new();
        private readonly List<ExplorationCharacterView> spawnedEnemyViews = new();

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController)
        {
            ClearSpawnedViews();

            if (floor == null || partyController == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak pietra lub PartyController.");

                return false;
            }

            if (encounterTriggerPrefab == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak prefabu EncounterTriggerView.");

                return false;
            }

            if (enemyViewPrefab == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak prefabu widocznego przeciwnika.");

                return false;
            }

            foreach (var encounterInstance in floor.Encounters)
            {
                if (encounterInstance == null || IsEncounterCleared(encounterInstance))
                {
                    continue;
                }

                SpawnEnemyViews(encounterInstance);

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
                    encounterInstance);

                spawnedTriggers.Add(triggerView);
            }

            return true;
        }

        private void SpawnEnemyViews(
            DungeonEncounterInstance encounterInstance)
        {
            var enemies = encounterInstance.Definition?.Enemies;

            if (enemies == null || enemies.Count == 0)
            {
                return;
            }

            var validEnemyCount = 0;

            foreach (var enemyData in enemies)
            {
                if (enemyData?.CharacterDefinition != null)
                {
                    validEnemyCount++;
                }
            }

            if (validEnemyCount == 0)
            {
                return;
            }

            var enemyIndex = 0;
            var parent = enemyViewsRoot != null
                ? enemyViewsRoot
                : transform;

            foreach (var enemyData in enemies)
            {
                if (enemyData?.CharacterDefinition == null)
                {
                    continue;
                }

                var enemyView = Instantiate(enemyViewPrefab, parent);

                enemyView.name =
                    $"EncounterEnemy_{encounterInstance.InstanceId}_{enemyIndex:00}";

                enemyView.Bind(enemyData.CharacterDefinition);
                enemyView.SetIsActiveLeader(false);
                enemyView.SetTint(enemyTint);
                enemyView.SetPosition(GetFormationPosition(
                    encounterInstance.TriggerArea,
                    enemyIndex,
                    validEnemyCount));

                spawnedEnemyViews.Add(enemyView);
                enemyIndex++;
            }
        }

        private static Vector2 GetFormationPosition(
            RectInt area,
            int index,
            int memberCount)
        {
            var columns = Mathf.Min(
                memberCount,
                Mathf.Max(1, area.width));

            var rows = Mathf.CeilToInt(
                memberCount / (float)columns);

            var column = index % columns;
            var row = index / columns;

            return new Vector2(
                area.xMin + (column + 0.5f) * area.width / columns,
                area.yMin + (row + 0.5f) * area.height / rows);
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