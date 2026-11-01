using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        public bool IsSkillApproachInProgress { get; private set; }

        public IReadOnlyList<DungeonTraversalPosition> GetSkillApproachPath(DungeonTraversalPosition destination)
        {
            var leader = GetActiveLeaderView();
            if (leader?.RuntimeCharacter?.LifeState != CharacterLifeState.Alive || traversalFloor?.Map == null
                || !traversalFloor.Map.TryGetTraversalCell(destination, out var cell)) return null;
            using var navigationBatch = BeginTraversalQueryBatch();
            var world = TraversalAnchorWorld(leader, cell);
            if (!FindTraversalPathWithTargetWorld(leader, destination, out var path, world)) return null;
            SmoothTraversalPath(leader, destination, world, path);
            return path;
        }

        public bool TryBeginSkillApproach(IReadOnlyList<DungeonTraversalPosition> path)
        {
            var leader = GetActiveLeaderView();
            if (path == null || path.Count == 0 || leader?.RuntimeCharacter?.LifeState != CharacterLifeState.Alive
                || combatCoordinator?.IsPreparingOpening != true || traversalFloor?.Map == null
                || !traversalFloor.Map.TryGetTraversalCell(path[path.Count - 1], out var cell)) return false;
            var route = GetSkillApproachPath(path[path.Count - 1]);
            if (route == null || route.Count < 2) return false;
            CommitTraversalRoute(leader, route, TraversalAnchorWorld(leader, cell));
            IsSkillApproachInProgress = true;
            return true;
        }

        private void TickSkillApproach()
        {
            var leader = GetActiveLeaderView();
            if (leader?.RuntimeCharacter?.LifeState != CharacterLifeState.Alive)
            { CancelMovement(); return; }
            // Same traversal/collision/stairs owner as normal exploration; no combat
            // movement system, resource spend or independent transform animation.
            TickTraversalMovement(leader);
            MoveFollowers(leader);
            PersistCurrentPosition(leader);
            if (!movementDestination.HasValue && traversalPath.Count == 0) IsSkillApproachInProgress = false;
        }
    }
}
