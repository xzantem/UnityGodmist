using System;
using Godmist.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class DownedCountdownSetup
    {
        [MenuItem("Godmist/UI/Configure downed skull artwork")]
        public static void ConfigureSkullArtwork()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring UI artwork.");
            GodmistGothicUISpriteImport.Configure();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GodmistGothicUISpriteImport.Root + "status_downed_skull.png");
            var palette = AssetDatabase.LoadAssetAtPath<Godmist.Views.UI.Workspace.UIWorkspaceStylePalette>(
                "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            if (sprite == null || palette == null) throw new InvalidOperationException("Missing Godmist skull artwork or palette.");
            var data = new SerializedObject(palette); var artwork = data.FindProperty("artwork");
            var index = 0;
            while (index < artwork.arraySize && artwork.GetArrayElementAtIndex(index).objectReferenceValue != sprite) index++;
            if (index == artwork.arraySize) artwork.arraySize++;
            artwork.GetArrayElementAtIndex(index).objectReferenceValue = sprite;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            Debug.Log("GODMIST_DOWNED_SKULL_CONFIGURED: shared palette and Gothic UI atlas.");
        }

        [MenuItem("Godmist/Combat/Apply Three Turn Downed Limit")]
        public static void Configure()
        {
            var defines = AssetDatabase.LoadAssetAtPath<GameDefines>("Assets/_Project/Resources/GameDefines.asset");
            if (defines == null || !defines.TryGet(GameDefineIds.DownedTurnLimit, out var entry))
                throw new InvalidOperationException("Missing downed-turn definition.");
            entry.DefaultIntegerValue = 3;
            entry.CurrentIntegerValue = 3;
            EditorUtility.SetDirty(defines);
            AssetDatabase.SaveAssets();
            Debug.Log("DOWNED_COUNTDOWN_CONFIGURED: 3 turns, 3 segments.");
        }
    }
}
