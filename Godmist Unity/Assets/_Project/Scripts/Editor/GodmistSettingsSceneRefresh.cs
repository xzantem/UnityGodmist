using System.Linq;
using Godmist.Views.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Refreshes a clean, stale Bootstrap after external scene authoring.</summary>
[InitializeOnLoad]
public static class GodmistSettingsSceneRefresh
{
    static GodmistSettingsSceneRefresh()
    {
        EditorApplication.playModeStateChanged += BeforePlay;
    }

    private static MainMenuSettingsView[] Views(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<MainMenuSettingsView>(true)).ToArray();

    public static bool RefreshIfStale(Scene scene, out string message)
    {
        message = string.Empty;
        if (!scene.IsValid() || !scene.isLoaded || scene.path != GodmistMainMenuSetup.ScenePath) return true;
        var missing = Views(scene).SelectMany(view => view.GetMissingReferences()).Distinct().ToArray();
        if (missing.Length == 0) return true;

        message = "Bootstrap ma nieaktualne referencje ustawień: " + string.Join(", ", missing) + ". ";
        // Never discard local scene edits or other open scenes to refresh UI.
        if (scene.isDirty || SceneManager.sceneCount != 1)
        {
            message += "Zachowaj lokalne zmiany osobno i otwórz ponownie zapisaną scenę Bootstrap. Nie nadpisuj poprawnego pliku starą sceną.";
            return false;
        }

        var saved = EditorSceneManager.OpenPreviewScene(scene.path);
        bool valid;
        try
        {
            var views = Views(saved);
            valid = views.Length > 0 && views.All(view => view.GetMissingReferences().Length == 0);
        }
        finally { EditorSceneManager.ClosePreviewScene(saved); }
        if (!valid)
        {
            message += "Braki są również w pliku. Uruchom Godmist > UI > Configure main menu settings.";
            return false;
        }

        EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
        message = "Odświeżono czystą scenę Bootstrap z pliku: referencje ustawień są kompletne.";
        return true;
    }

    private static void BeforePlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        var scene = SceneManager.GetSceneByPath(GodmistMainMenuSetup.ScenePath);
        if (!RefreshIfStale(scene, out var message))
        {
            EditorApplication.isPlaying = false;
            Debug.LogError(message);
        }
        else if (message.Length > 0) Debug.Log(message);
    }
}
