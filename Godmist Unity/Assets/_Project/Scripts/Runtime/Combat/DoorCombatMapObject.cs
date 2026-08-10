using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class DoorCombatMapObject : CombatMapObject
    {
        public override CombatMapObjectType ObjectType
            => CombatMapObjectType.Door;

        public override bool BlocksMovement => !IsOpen;
        public override bool BlocksLineOfSight => !IsOpen;

        public BattleCellEdge Edge { get; }
        public bool IsOpen;
        public DoorInteractionMode InteractionMode;

        public DoorCombatMapObject()
        {
        }

        public DoorCombatMapObject(
            string objectId,
            GridPosition firstCell,
            GridPosition secondCell,
            DoorInteractionMode interactionMode,
            bool isOpen = false)
            : base(objectId, firstCell)
        {
            Edge = new BattleCellEdge(firstCell, secondCell);
            InteractionMode = interactionMode;
            IsOpen = isOpen;
        }

        public bool ContainsCell(GridPosition position)
        {
            return Edge.FirstCell == position
                || Edge.SecondCell == position;
        }

        public override bool CanInteract()
        {
            return InteractionMode != DoorInteractionMode.LeverOnly;
        }

        public override void Interact()
        {
            TryToggleDirectly();
        }

        public bool TryToggleDirectly()
        {
            if (InteractionMode == DoorInteractionMode.LeverOnly)
            {
                return false;
            }

            IsOpen = !IsOpen;
            return true;
        }

        public bool TryToggleFromLever()
        {
            if (InteractionMode == DoorInteractionMode.DirectOnly)
            {
                return false;
            }

            IsOpen = !IsOpen;
            return true;
        }
    }
}