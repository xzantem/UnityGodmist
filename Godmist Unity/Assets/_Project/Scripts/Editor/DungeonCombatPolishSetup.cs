using System;
using System.IO;
using System.Linq;
using Godmist.Data.World;
using Godmist.Views.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class DungeonCombatPolishSetup
    {
        public const string UpPath = ManaExitDeadKingPolishSetup.UpPath;
        [MenuItem("Godmist/Content/Apply dungeon combat polish October25")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            AssetDatabase.ImportAsset(UpPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(UpPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            const string path = "Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(root.GetComponent<DungeonExitView>());
                data.FindProperty("upwardSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(UpPath);
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            SkillCastBannerSetup.Apply();
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var weakPoint = catalog.SkillDefinitions.Single(s => s != null && s.SkillId == "skill.scout.weak_point");
            var state = new SerializedObject(weakPoint);
            state.FindProperty("<ResourceCost>k__BackingField").intValue = 50;
            state.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(weakPoint);
            var output = Path.GetFullPath("../LocalHandoffs/dungeon-combat-polish-20261025"); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "authoring.txt"), "Upstairs v2; compact skill banner; Weak Point50 Momentum. No other skill balance changes.");
        }
    }
}
