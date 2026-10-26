using System;
using Godmist.Data.Characters;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Imports the Catacombs' existing pixel art consistently with the playable characters.</summary>
    public static class VS15EnemySpritesSetup
    {
        private const string Frames = "Assets/_Project/Art/ThirdParty/0x72DungeonTilesetII/Core/frames/";
        private const string Enemies = "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_";

        [MenuItem("Godmist/Content/Configure VS-15 Enemy Sprites")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before configuring VS-15 sprites.");

            ConfigureEnemy("swordsman", "skelet_idle_anim", "skelet_run_anim");
            ConfigureEnemy("archer", "skelet_idle_anim", "skelet_run_anim");
            ConfigureEnemy("defender", "skelet_idle_anim", "skelet_run_anim");
            ConfigureEnemy("occultist", "necromancer_anim", null);
            ConfigureEnemy("undead", "big_zombie_idle_anim", "big_zombie_run_anim");
            ConfigureEnemy("ghoul", "tiny_zombie_idle_anim", "tiny_zombie_run_anim");
            ConfigureEnemy("wraith", "ice_zombie_anim", null);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureEnemy(string id, string idleName, string runName)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Enemies + id + ".asset")
                ?? throw new InvalidOperationException("Missing VS-15 enemy: " + id);
            var idle = LoadFrames(idleName);
            var run = runName == null ? Array.Empty<Sprite>() : LoadFrames(runName);
            var data = new SerializedObject(enemy);
            data.FindProperty("<VisualSprite>k__BackingField").objectReferenceValue = idle[0];
            SetFrames(data.FindProperty("<IdleAnimationFrames>k__BackingField"), idle);
            SetFrames(data.FindProperty("<RunAnimationFrames>k__BackingField"), run);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(enemy);
        }

        private static Sprite[] LoadFrames(string animation)
        {
            var frames = new Sprite[4];
            for (var index = 0; index < frames.Length; index++)
            {
                var path = Frames + animation + "_f" + index + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter
                    ?? throw new InvalidOperationException("Missing VS-15 sprite importer: " + path);
                if (importer.spritePixelsPerUnit != 16f || importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.mipmapEnabled)
                {
                    importer.spritePixelsPerUnit = 16f;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path)
                    ?? throw new InvalidOperationException("Missing VS-15 sprite: " + path);
            }
            return frames;
        }

        private static void SetFrames(SerializedProperty property, Sprite[] frames)
        {
            property.arraySize = frames.Length;
            for (var index = 0; index < frames.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
        }
    }
}
