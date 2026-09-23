using System;
using UnityEditor;

namespace Godmist.Editor
{
    public static class VS15CatacombsSetup
    {
        [MenuItem("Godmist/Content/Configure VS-15 Catacombs")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before configuring VS-15.");
            VS15CatacombsEnemiesSetup.Configure();
            VS15EnemySpritesSetup.Configure();
            VS15CatacombsLootSetup.Configure();
        }
    }
}
