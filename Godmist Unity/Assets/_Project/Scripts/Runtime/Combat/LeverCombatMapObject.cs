using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class LeverCombatMapObject : CombatMapObject
    {
        public override CombatMapObjectType ObjectType => CombatMapObjectType.Lever;

        public bool IsActivated;
        public string LinkedObjectId = string.Empty;

        public LeverCombatMapObject() {}

        public LeverCombatMapObject(string objectId, GridPosition position, 
            string linkedObjectId) : base(objectId, position)
        {
            LinkedObjectId = linkedObjectId ?? string.Empty;
        }

        public override bool CanInteract()
        {
            return true;
        }

        public override void Interact()
        {
            IsActivated = !IsActivated;
        }
    }
}