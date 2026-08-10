using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DoorDungeonObjectInstance : DungeonObjectInstance
    {
        public DungeonCellEdge Edge { get; }
        public DoorInteractionMode InteractionMode { get; }
        public bool IsOpen { get; private set; }

        public DoorDungeonObjectInstance(
            string instanceId,
            DoorDungeonObjectDefinition definition,
            DungeonCellEdge edge)
            : base(instanceId, definition, edge.FirstCell)
        {
            Edge = edge;
            InteractionMode = definition.InteractionMode;
            IsOpen = definition.StartsOpen;
        }

        public bool TrySetOpen(bool isOpen)
        {
            if (IsOpen == isOpen) return false;

            IsOpen = isOpen;
            return true;
        }

        public bool TryRestoreOpen(bool isOpen)
        {
            IsOpen = isOpen;
            return true;
        }
    }
}