using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class GodmistMainMenuSetup
{
    private static Transform FindMenuCanvas(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
        .Single(canvas => canvas.name == "Canvas").transform;

    public static void ConfigureReleaseVersionLabel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring the version label.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save existing Bootstrap scene edits before authoring the version label.");
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var menu = FindMenuCanvas(scene).Find("MainMenuRoot");
            palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            ConfigureReleaseVersionLabel(menu, menu.GetComponent<MainMenuView>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the version label.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void ConfigureReleaseVersionLabel(Transform menu, MainMenuView view)
    {
        // Reuse the menu's existing palette/font. This footer has no interaction
        // and is anchored outside the button column and its Layout Groups.
        var label = Text(menu, "ReleaseVersion", UnityEditor.PlayerSettings.bundleVersion + " — Early Access", 18);
        var data = new SerializedObject(view);
        data.FindProperty("versionLabel").objectReferenceValue = label;
        data.ApplyModifiedPropertiesWithoutUndo();
        Fixed(label.transform, Vector2.right, Vector2.right, new Vector2(-28, 24), new Vector2(440, 30));
        label.alignment = TextAlignmentOptions.BottomRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.transform.SetAsLastSibling();
        if (PrefabUtility.IsPartOfPrefabInstance(view)) PrefabUtility.RecordPrefabInstancePropertyModifications(view);
    }
}
