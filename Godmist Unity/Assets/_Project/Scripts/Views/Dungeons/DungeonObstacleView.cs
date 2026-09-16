using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonObstacleView : DungeonObjectView
    {
        [SerializeField] private Collider2D blockingCollider;

        private SpriteRenderer[] hitRenderers;
        private int previousHits;
        private float hitFlashUntil;
        public bool HasBlockingCollider => blockingCollider != null;

        public void Initialize(
            ObstacleDungeonObjectInstance obstacle,
            Vector3 worldPosition)
        {
            if (!TryBind(obstacle, worldPosition)) return;

            hitRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            previousHits = obstacle.RemainingHits;
            if (blockingCollider == null) return;

            blockingCollider.enabled = obstacle.IsActive && obstacle.BlocksMovement;
            blockingCollider.isTrigger = false;
        }
        protected override void LateUpdate()
        {
            if (DungeonObject is ObstacleDungeonObjectInstance obstacle)
            {
                if (obstacle.RemainingHits < previousHits) hitFlashUntil = Time.time + .12f;
                previousHits = obstacle.RemainingHits;
                if (blockingCollider != null) blockingCollider.enabled = obstacle.IsActive && obstacle.BlocksMovement;
            }
            base.LateUpdate();
            if (DungeonObject is { IsActive: false } && IsElevationVisible && hitRenderers != null)
                foreach (var renderer in hitRenderers)
                    if (renderer != null) renderer.forceRenderingOff = true;
        }
        protected override void RefreshFogVisual()
        {
            if (IsElevationVisible && Time.time < hitFlashUntil && hitRenderers != null)
                foreach (var renderer in hitRenderers) if (renderer != null) renderer.color = Color.white;
        }
    }
}