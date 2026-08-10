using System;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public abstract class DungeonObjectInstance
    {
        public string InstanceId { get; }
        public DungeonObjectDefinition Definition { get; }
        public Vector2Int Position { get; }
        public bool IsActive { get; protected set; } = true;

        public DungeonObjectType ObjectType => Definition.ObjectType;

        protected DungeonObjectInstance(
            string instanceId,
            DungeonObjectDefinition definition,
            Vector2Int position)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException(
                    "Dungeon object InstanceId cannot be empty.",
                    nameof(instanceId));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            InstanceId = instanceId;
            Definition = definition;
            Position = position;
        }

        public bool TryDeactivate()
        {
            if (!IsActive) return false;

            IsActive = false;
            return true;
        }

        public bool TryRestoreIsActive(bool isActive)
        {
            IsActive = isActive;
            return true;
        }
    }
}