using System;
using System.Collections.Generic;
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
        private readonly HashSet<GridPosition> occupiedPositions;

        public ObstacleCombatMapObject(string objectId, GridPosition position, bool blocksMovement,
            bool blocksLineOfSight, Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance source = null,
            IEnumerable<GridPosition> occupiedPositions = null) : base(objectId, position)
        {
            Source = source;
            this.blocksMovement = blocksMovement;
            this.blocksLineOfSight = blocksLineOfSight;
            this.occupiedPositions = occupiedPositions == null
                ? null
                : new HashSet<GridPosition>(occupiedPositions);
        }

        public override bool CanInteract() => false;

        public override void Interact() {}

        public bool Occupies(GridPosition position)
        {
            if (!IsActive)
                return false;

            if (occupiedPositions != null)
                return !position.IsOnStairs && occupiedPositions.Contains(position);

            return Position == position;
        }

        public IEnumerable<GridPosition> EnumerateOccupiedPositions()
        {
            if (occupiedPositions == null)
            {
                yield return Position;
                yield break;
            }

            foreach (var occupiedPosition in occupiedPositions)
                yield return occupiedPosition;
        }
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
