using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonObstacleView : DungeonObjectView
    {
        [SerializeField] private Collider2D blockingCollider;

        public bool HasBlockingCollider => blockingCollider != null;

        public void Initialize(
            ObstacleDungeonObjectInstance obstacle,
            Vector3 worldPosition)
        {
            if (!TryBind(obstacle, worldPosition)) return;

            if (blockingCollider == null) return;

            blockingCollider.enabled = obstacle.BlocksMovement;
            blockingCollider.isTrigger = false;
        }
    }
}