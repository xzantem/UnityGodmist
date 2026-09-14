using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private DungeonFloorInstance traversalFloor;
        private DungeonFloorMapView traversalMapView;
        private readonly Queue<DungeonTraversalPosition> traversalPath = new();
        public bool CanInteractWithGroundObjects => GetActiveLeaderView() is { } leader
            && (!leader.UsesLayeredTraversal || (!leader.DungeonPosition.IsOnStairs && leader.DungeonPosition.Surface.Level == 0));

        public bool CanInteractAt(DungeonTraversalPosition target)
        {
            var leader = GetActiveLeaderView();
            if (leader == null) return false;
            if (!leader.UsesLayeredTraversal) return !target.IsOnStairs && target.Surface.Level == 0;
            return target == leader.DungeonPosition || traversalFloor.Map.GetTraversalNeighbours(leader.DungeonPosition).Contains(target);
        }

        public void ConfigureTraversal(DungeonFloorInstance floor, DungeonFloorMapView map, CombatGridFeedbackRenderer feedback)
        {
            traversalFloor = floor; traversalMapView = map;
            foreach (var view in viewsById.Values) view.CanEnterTraversal = position => CanEnterTraversal(view, position);
        }

        public bool CanEnterTraversal(ExplorationCharacterView actor, DungeonTraversalPosition position)
        {
            if (traversalFloor?.Map == null || !traversalFloor.Map.TryGetTraversalCell(position, out var cell)) return false;
            if (!position.IsOnStairs && position.Surface.Level == 0
                && traversalFloor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o => o.IsActive && o.BlocksMovement && o.Position == cell)) return false;
            return true;
        }

        private bool FindTraversalPath(ExplorationCharacterView actor, DungeonTraversalPosition target, out List<DungeonTraversalPosition> path)
            => DungeonExplorationPathfinder.TryFindPath(traversalFloor.Map, actor.DungeonPosition, target, out path,
                position => CanEnterTraversal(actor, position));

        private void TrySetTraversalDestination(Vector3 world)
        {
            var leader = GetActiveLeaderView();
            if (leader == null || traversalFloor?.Map == null || !traversalMapView.TryGetVisibleTraversal(world, out var target)
                || DungeonEnemyBodyView.IsPointerOverBody(world) || !FindTraversalPath(leader, target, out var path)) return;
            CancelMovement();
            movementDestination = world;
            foreach (var position in path.Skip(1)) traversalPath.Enqueue(position);
        }

        private void TickTraversalMovement(ExplorationCharacterView leader)
        {
            var keyboard = cameraController == null || cameraController.IsFollowingLeader
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
            if (keyboard.sqrMagnitude > 0)
            {
                traversalPath.Clear(); movementDestination = null; leader.PreferredTraversalDestination = null;
                leader.Move(Vector2.ClampMagnitude(keyboard, 1) * movementSpeed * Time.fixedDeltaTime);
                traversalMapView.FocusElevation(leader.DungeonPosition, true);
                return;
            }
            while (traversalPath.Count > 0 && traversalPath.Peek() == leader.DungeonPosition) traversalPath.Dequeue();
            if (traversalPath.Count > 0)
            {
                var next = traversalPath.Peek(); leader.PreferredTraversalDestination = next;
                traversalFloor.Map.TryGetTraversalCell(next, out var cell);
                leader.MoveTowards(traversalMapView.CellToWorldCenter(cell), movementSpeed * Time.fixedDeltaTime, 0);
            }
            else if (movementDestination.HasValue)
            {
                leader.PreferredTraversalDestination = null;
                leader.MoveTowards(movementDestination.Value, movementSpeed * Time.fixedDeltaTime, 0);
                if ((leader.Position - movementDestination.Value).sqrMagnitude < .000001f) movementDestination = null;
            }
            if (traversalPath.Count > 0 || movementDestination.HasValue) traversalMapView.FocusElevation(leader.DungeonPosition, true);
        }

        private void FollowTraversal(ExplorationCharacterView follower, ExplorationCharacterView previous)
        {
            // Exploration follower pathfinding belongs to its own card.
            follower.PreferredTraversalDestination = null;
            follower.MoveTowards(previous.Position, movementSpeed * Time.fixedDeltaTime, followerDistance);
        }
    }
}
