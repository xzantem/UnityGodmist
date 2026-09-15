using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class ObstacleCombatMapObject : CombatMapObject
    {
        public override CombatMapObjectType ObjectType => CombatMapObjectType.Obstacle;

        public Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance Source { get; }
        public bool IsActive => Source?.IsActive ?? true;
        public override bool BlocksMovement => IsActive && blocksMovement;
        public override bool BlocksLineOfSight => IsActive && blocksLineOfSight;

        private readonly bool blocksMovement;
        private readonly bool blocksLineOfSight;

        public ObstacleCombatMapObject(string objectId, GridPosition position, bool blocksMovement,
            bool blocksLineOfSight, Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance source = null) : base(objectId, position)
        {
            Source = source;
            this.blocksMovement = blocksMovement;
            this.blocksLineOfSight = blocksLineOfSight;
        }

        public override bool CanInteract() => false;

        public override void Interact() {}
    }
    public sealed class CombatObjectDamageAction : IDisposable
    {
        private readonly CombatSession session;
        private readonly CombatObjectDamageAction previous;
        public readonly System.Collections.Generic.List<ObstacleCombatMapObject> Barriers = new();
        public readonly System.Collections.Generic.HashSet<string> Direct = new();
        public readonly System.Collections.Generic.HashSet<string> Collisions = new();
        public CombatObjectDamageAction(CombatSession session)
        {
            this.session = session; previous = session.ObjectDamageAction; session.ObjectDamageAction = this;
            foreach (var obj in session.MapObjects)
                if (obj is ObstacleCombatMapObject o && o.BlocksMovement) Barriers.Add(o);
        }
        public void Dispose() { session.ObjectDamageAction = previous; }
    }
}