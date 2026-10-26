using System;
using System.Collections.Generic;
using Godmist.Data.Combat;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonEncounterInstance
    {
        public string InstanceId { get; }
        public EncounterDefinition Definition { get; }
        private List<EncounterEnemyData> reinforcementEnemies;
        public bool IsReinforcement => reinforcementEnemies != null;
        public IReadOnlyList<EncounterEnemyData> Enemies => reinforcementEnemies ?? Definition.Enemies;
        public bool TryConfigureReinforcements(IEnumerable<string> enemyIds)
        {
            if (IsReinforcement || enemyIds == null) return false;
            var selected = new List<EncounterEnemyData>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in enemyIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return false;
                var enemy = Definition.Enemies.Find(candidate => candidate?.InstanceId == id);
                if (enemy?.CharacterDefinition == null || enemy.ClassData == null) return false;
                selected.Add(enemy);
            }
            if (selected.Count == 0) return false;
            reinforcementEnemies = selected;
            return true;
        }
        public string RoomId { get; }
        public RectInt TriggerArea { get; }

        public Dictionary<string, Vector2Int> StationaryEnemyHomes { get; } = new(StringComparer.Ordinal);

        public bool IsAlerted { get; private set; }
        public Vector2Int ExplorationPosition { get; private set; }

        public bool HasRetreatPosition { get; private set; }
        public Vector2Int RetreatPosition { get; private set; }
        private readonly HashSet<string> defeatedEnemyInstanceIds = new();
        private readonly HashSet<string> generatedEnemyBodyEnemyInstanceIds = new();
        private readonly Dictionary<string, Vector2Int>
            enemyExplorationPositions = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> DefeatedEnemyInstanceIds =>
            defeatedEnemyInstanceIds;
        public IReadOnlyCollection<string> GeneratedEnemyBodyEnemyInstanceIds =>
            generatedEnemyBodyEnemyInstanceIds;

        public IReadOnlyDictionary<string, Vector2Int> EnemyExplorationPositions =>
            enemyExplorationPositions;

        public DungeonEncounterInstance(
            string instanceId,
            EncounterDefinition definition,
            string roomId,
            RectInt triggerArea)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException(
                    "Encounter InstanceId cannot be empty.",
                    nameof(instanceId));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException(
                    "Encounter RoomId cannot be empty.",
                    nameof(roomId));
            }

            if (triggerArea.width <= 0 || triggerArea.height <= 0)
            {
                throw new ArgumentException(
                    "Encounter trigger area must have positive size.",
                    nameof(triggerArea));
            }

            InstanceId = instanceId;
            Definition = definition;
            RoomId = roomId;
            TriggerArea = triggerArea;

            ExplorationPosition = new Vector2Int(
                triggerArea.xMin + triggerArea.width / 2,
                triggerArea.yMin + triggerArea.height / 2);
        }

        public bool TrySetAlerted()
        {
            if (IsAlerted) return false;

            IsAlerted = true;
            return true;
        }

        public bool TryClearAlerted()
        {
            if (!IsAlerted) return false;

            IsAlerted = false;
            return true;
        }

        public bool TrySetExplorationPosition(Vector2Int position)
        {
            if (ExplorationPosition == position) return false;

            ExplorationPosition = position;
            return true;
        }

        public bool TrySetRetreatPosition(Vector2Int position)
        {
            if (HasRetreatPosition && RetreatPosition == position) return false;

            RetreatPosition = position;
            HasRetreatPosition = true;
            return true;
        }

        public bool IsEnemyDefeated(string enemyInstanceId)
        {
            return !string.IsNullOrWhiteSpace(enemyInstanceId)
                && defeatedEnemyInstanceIds.Contains(enemyInstanceId);
        }

        public bool TryMarkEnemyDefeated(string enemyInstanceId)
        {
            if (string.IsNullOrWhiteSpace(enemyInstanceId)) return false;

            return defeatedEnemyInstanceIds.Add(enemyInstanceId);
        }

        public bool TrySetEnemyExplorationPosition(
            string enemyInstanceId,
            Vector2Int position)
        {
            if (string.IsNullOrWhiteSpace(enemyInstanceId)) return false;

            enemyExplorationPositions[enemyInstanceId] = position;
            return true;
        }

        public bool TryGetEnemyExplorationPosition(
            string enemyInstanceId,
            out Vector2Int position)
        {
            return enemyExplorationPositions.TryGetValue(
                enemyInstanceId,
                out position);
        }

        public bool IsEnemyBodyGenerated(string enemyInstanceId)
        {
            return !string.IsNullOrWhiteSpace(enemyInstanceId)
                && generatedEnemyBodyEnemyInstanceIds.Contains(enemyInstanceId);
        }

        public bool TryMarkEnemyBodyGenerated(string enemyInstanceId)
        {
            if (string.IsNullOrWhiteSpace(enemyInstanceId)) return false;

            return generatedEnemyBodyEnemyInstanceIds.Add(enemyInstanceId);
        }

        public bool TryRestoreGeneratedEnemyBodyEnemyInstanceIds(
            IEnumerable<string> restoredEnemyInstanceIds)
        {
            var restoredIds = new HashSet<string>();

            if (restoredEnemyInstanceIds != null)
            {
                foreach (var enemyInstanceId in restoredEnemyInstanceIds)
                {
                    if (string.IsNullOrWhiteSpace(enemyInstanceId))
                    {
                        return false;
                    }

                    restoredIds.Add(enemyInstanceId);
                }
            }

            generatedEnemyBodyEnemyInstanceIds.Clear();

            foreach (var enemyInstanceId in restoredIds)
            {
                generatedEnemyBodyEnemyInstanceIds.Add(enemyInstanceId);
            }

            return true;
        }

        public bool TryRestoreDefeatedEnemyInstanceIds(
            IEnumerable<string> restoredEnemyInstanceIds)
        {
            var restoredIds = new HashSet<string>();

            if (restoredEnemyInstanceIds != null)
            {
                foreach (var enemyInstanceId in restoredEnemyInstanceIds)
                {
                    if (string.IsNullOrWhiteSpace(enemyInstanceId))
                    {
                        return false;
                    }

                    restoredIds.Add(enemyInstanceId);
                }
            }

            defeatedEnemyInstanceIds.Clear();

            foreach (var enemyInstanceId in restoredIds)
            {
                defeatedEnemyInstanceIds.Add(enemyInstanceId);
            }

            return true;
        }
    }
}
