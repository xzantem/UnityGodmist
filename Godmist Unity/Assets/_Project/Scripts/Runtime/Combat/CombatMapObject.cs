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

    [Serializable]
    public sealed class KnifeCombatMapObject : CombatMapObject
    {
        public string OwnerCharacterId = string.Empty;
        public int KnifeCount = 1;
        // One-shot presentation hint; never part of persistent/save state.
        [NonSerialized] public GridPosition? LandingOrigin;
        [NonSerialized] public bool LandingFromHit;
        public override CombatMapObjectType ObjectType => CombatMapObjectType.Knife;

        public KnifeCombatMapObject() { }

        public KnifeCombatMapObject(string objectId, GridPosition position,
            string ownerCharacterId, int knifeCount) : base(objectId, position)
        {
            OwnerCharacterId = ownerCharacterId ?? string.Empty;
            KnifeCount = Math.Max(1, knifeCount);
        }

        public override bool CanInteract() => false;
        public override void Interact() { }
    }
}
