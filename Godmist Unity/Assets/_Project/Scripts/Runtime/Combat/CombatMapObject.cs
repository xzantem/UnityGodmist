using System;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public abstract class CombatMapObject : IInteractable
    {
        public string ObjectId = string.Empty;
        public GridPosition Position;

        public abstract CombatMapObjectType ObjectType { get; }
        public virtual bool BlocksMovement => false;
        public virtual bool BlocksLineOfSight => false;

        protected CombatMapObject() {}

        protected CombatMapObject(string objectId, GridPosition position)
        {
            ObjectId = objectId ?? string.Empty;
            Position = position;
        }

        public abstract bool CanInteract();
        public abstract void Interact();
    }
}