using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
namespace Godmist.Runtime.Combat
{
    public sealed class TrapCombatMapObject : CombatMapObject
    {
        public SpikeTrapDungeonObjectInstance Trap { get; }
        public override CombatMapObjectType ObjectType => CombatMapObjectType.Trap;
        public override bool BlocksMovement => false;
        public override bool BlocksLineOfSight => false;
        public TrapCombatMapObject(SpikeTrapDungeonObjectInstance trap, GridPosition position) : base(trap.InstanceId, position) { Trap = trap; }
        public override bool CanInteract() => false;
        public override void Interact() { }
    }
}
