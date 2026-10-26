using System;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GodmistLoadingScreenSetup
{
    [MenuItem("Godmist/UI/Configure loading screen style")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Configure loading screen style outside Play Mode.");
        var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        if (palette == null || palette.GetArtwork("slider_track") == null || palette.GetArtwork("fill_runtime") == null)
            throw new InvalidOperationException("Loading screen requires the existing Gothic bar artwork.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string path="Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
        var scene=SceneManager.GetSceneByPath(path);
        var alreadyLoaded=scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var loader in root.GetComponentsInChildren<SceneLoader>(true))
                {
                    var state = new SerializedObject(loader);
                    state.FindProperty("loadingStyle").objectReferenceValue = palette;
                    state.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            if (count != 1) throw new InvalidOperationException("Expected one Bootstrap scene loader, got " + count);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("LOADING_SCREEN_STYLE_CONFIGURED: existing Gothic palette wired to the persistent scene loader.");
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene,true); }
    }
}
