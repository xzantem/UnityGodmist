using UnityEngine;
using System;
using Godmist.Data.Combat;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonEncounterInstance
    {
        public string InstanceId { get; }
        public EncounterDefinition Definition { get; }
        public string RoomId { get; }
        public RectInt TriggerArea { get; }

        public DungeonEncounterInstance
            (string instanceId, EncounterDefinition definition, string roomId, RectInt triggerArea)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("Encounter InstanceId cannot be empty.", nameof(instanceId));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Encounter RoomId cannot be empty", nameof(roomId));
            }

            if (triggerArea.width <= 0 || triggerArea.height <= 0)
            {
                throw new ArgumentException("Encounter trigger area must have positive size", nameof(triggerArea));
            }

            InstanceId = instanceId;
            Definition = definition;
            RoomId = roomId;
            TriggerArea = triggerArea;
        }
    }
}