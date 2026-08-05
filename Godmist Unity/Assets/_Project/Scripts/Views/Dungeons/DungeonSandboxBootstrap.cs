using System;
using Godmist.Data.Dungeons;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonSandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonDefinition dungeonDefinition;
        [SerializeField] private DungeonLayoutView layoutView;
        [SerializeField] private DungeonCameraFramer cameraFramer;
        [SerializeField] private int seed = 12345;

        private readonly DungeonGeneratorService generator = new();

        private void Start()
        {
            if (dungeonDefinition == null || layoutView == null)
            {
                Debug.LogError(
                    "DungeonSandboxBootstrap: brak DungeonDefinition lub DungeonLayoutView.");
                return;
            }

            try
            {
                var dungeon = generator.Generate(
                    dungeonDefinition,
                    seed);

                layoutView.Rebuild(
                    dungeon,
                    dungeonDefinition.RoomTemplateCatalog);

                if (cameraFramer != null
                    && layoutView.TryGetWorldBounds(out var worldBounds))
                {
                    cameraFramer.Frame(worldBounds);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}