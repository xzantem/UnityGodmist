using Godmist.Views.UI.Workspace;
using UnityEditor;
using UnityEngine;

/// <summary>Assign the existing Gothic bar and resource icon to the shared loyalty tooltip.</summary>
public static class NpcLoyaltyTooltipSetup
{
    [MenuItem("Godmist/UI/Assign loyalty tooltip artwork")]
    public static void Configure()
    {
        const string path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
                "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            var view = new SerializedObject(root.GetComponent<UIWorkspaceTooltipView>());
            view.FindProperty("npcLoyaltyGoldIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
            view.FindProperty("npcLoyaltyBarFrame").objectReferenceValue = palette.GetArtwork("slider_track");
            view.FindProperty("npcLoyaltyBarFill").objectReferenceValue = palette.GetArtwork("fill_runtime");
            foreach (var field in new[] { "npcLoyaltyGoldIcon", "npcLoyaltyBarFrame", "npcLoyaltyBarFill" })
                if (view.FindProperty(field).objectReferenceValue == null)
                    throw new System.InvalidOperationException("Missing loyalty tooltip artwork: " + field);
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
