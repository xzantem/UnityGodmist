using System;
using System.Linq;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ManaExitDeadKingPolishSetup
    {
        public const string UpPath = "Assets/_Project/Art/Props/Catacombs/CatacombsStairsUp32-v3.png";
        public const string ClosedStairsPath = "Assets/_Project/Art/Props/Catacombs/CatacombsStairsClosed32-v1.png";
        public const string ClosedGatePath = "Assets/Resources/Godmist/ForestArt/Exploration/exit-gate-closed-v1.png";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var path in new[] { UpPath, ClosedStairsPath, ClosedGatePath })
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                if (path == ClosedGatePath)
                {
                    var open = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Godmist/ForestArt/Exploration/exit-gate-v2.png");
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(open.pivot.x / open.rect.width, open.pivot.y / open.rect.height);
                }
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            const string prefab = "Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var data = new SerializedObject(root.GetComponent<DungeonExitView>());
                data.FindProperty("upwardSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(UpPath);
                data.FindProperty("closedStairSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ClosedStairsPath);
                data.FindProperty("closedForestSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ClosedGatePath);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var serialized = new SerializedObject(glossary);
            var entries = serialized.FindProperty("entries");
            foreach (var id in new[] { "mana", "catacombs.spectral_knight.shell" })
            {
                var source = CombatTooltipGlossary.BuiltInEntries.Single(e => e.Id == id);
                SerializedProperty entry = null;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == id)
                        entry = entries.GetArrayElementAtIndex(i);
                if (entry == null)
                {
                    entry = entries.GetArrayElementAtIndex(entries.arraySize++);
                    entry.FindPropertyRelative("Aliases").ClearArray();
                    entry.FindPropertyRelative("Id").stringValue = id;
                }
                entry.FindPropertyRelative("Title").stringValue = source.Title;
                entry.FindPropertyRelative("Description").stringValue = source.Description;
                if (id != "mana")
                    entry.FindPropertyRelative("Icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillData>(
                        "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_dead_king_guard.asset").Icon;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(glossary);
        }
    }
}
