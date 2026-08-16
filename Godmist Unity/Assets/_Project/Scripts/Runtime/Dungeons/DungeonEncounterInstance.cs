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
        public string RoomId { get; }
        public RectInt TriggerArea { get; }

        public bool IsAlerted { get; private set; }
        public Vector2Int ExplorationPosition { get; private set; }

        public bool HasRetreatPosition { get; private set; }
        public Vector2Int RetreatPosition { get; private set; }
        private readonly HashSet<string> defeatedEnemyInstanceIds = new();

        public IReadOnlyCollection<string> DefeatedEnemyInstanceIds =>
            defeatedEnemyInstanceIds;

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