using System;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class CampingKitIconSetup
    {
        private const string IconPath = "Assets/_Project/Art/Icons/Items/VS22/camping-kit-32-v1.png";
        private const string ItemPath = "Assets/_Project/ScriptableObjects/Items/ID_CampingKit.asset";

        [MenuItem("Godmist/Art/Configure Camping Kit Icon")]
        public static void ConfigureIcon()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            if (sprite == null || definition == null)
                throw new InvalidOperationException("Missing camping kit sprite or definition.");
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("<Icon>k__BackingField").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            if (sprite.rect.width != 32 || sprite.rect.height != 32 ||
                sprite.pixelsPerUnit != 32 || sprite.texture.filterMode != FilterMode.Point ||
                definition.Icon != sprite ||
                ItemIconPresentation.GetIcon(new ItemInstance(definition, "camping-kit-icon-check")) != sprite)
                throw new InvalidOperationException("Camping kit icon validation failed.");
            Debug.Log("CAMPING KIT ICON PASS: 32x32, Point, 32 PPU, definition and inventory resolver assigned.");
        }
    }
}
