using Godmist.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [InitializeOnLoad]
    internal static class GameDefinesAssetInitializer
    {
        private const string AssetPath = "Assets/_Project/Resources/GameDefines.asset";

        static GameDefinesAssetInitializer()
        {
            EditorApplication.delayCall += EnsureAsset;
        }

        private static void EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameDefines>(AssetPath);
            if (existing != null)
            {
                var changed = false;
                foreach (var defaultEntry in GameDefineDefaults.CreateEntries())
                {
                    if (defaultEntry != null && !existing.TryGet(defaultEntry.Id, out _))
                    {
                        existing.AddEntry(defaultEntry);
                        changed = true;
                    }
                }

                // OnEnable materializes the default entries for the checked-in
                // empty seed asset. This also backfills new defaults added by
                // later versions without overwriting authored current values.
                if (changed) EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return;
            }

            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            AssetDatabase.CreateAsset(definitions, AssetPath);
            EditorUtility.SetDirty(definitions);
            AssetDatabase.SaveAssets();
        }
    }
}
