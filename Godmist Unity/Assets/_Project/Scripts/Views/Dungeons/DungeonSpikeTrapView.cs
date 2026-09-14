using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonSpikeTrapView : DungeonObjectView
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Color armedColor = new(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color triggeredColor = new(0.45f, 0.45f, 0.45f, 1f);

        private SpikeTrapDungeonObjectInstance trap;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            SpikeTrapDungeonObjectInstance nextTrap,
            Tilemap nextFloorTilemap,
            Vector3 worldPosition)
        {
            if (!TryBind(nextTrap, worldPosition)) return;

            trap = nextTrap;
            RefreshVisual();
        }

        private void Update() => RefreshVisual();

        private void RefreshVisual()
        {
            if (visualRenderer != null)
            {
                visualRenderer.color = trap != null && trap.IsTriggered
                    ? triggeredColor
                    : armedColor;
            }
        }

    }
}
