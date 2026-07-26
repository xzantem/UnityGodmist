using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class ObstacleCombatMapObject : CombatMapObject
    {
        public override CombatMapObjectType ObjectType => CombatMapObjectType.Obstacle;

        public override bool BlocksMovement => blocksMovement;
        public override bool BlocksLineOfSight => blocksLineOfSight;

        private readonly bool blocksMovement;
        private readonly bool blocksLineOfSight;

        public ObstacleCombatMapObject(string objectId, GridPosition position, bool blocksMovement,
            bool blocksLineOfSight) : base(objectId, position)
        {
            this.blocksMovement = blocksMovement;
            this.blocksLineOfSight = blocksLineOfSight;
        }

        public override bool CanInteract() => false;

        public override void Interact() {}
    }
}