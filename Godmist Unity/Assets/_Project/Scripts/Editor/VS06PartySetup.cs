using System;
using System.Linq;
using Godmist.Data.Characters;
using UnityEditor;
using UnityEngine;

// Explicit content authoring; never modifies a running campaign.
public static class VS06PartySetup
{
    const string Root = "Assets/_Project/ScriptableObjects/Characters/";
    const string Frames = "Assets/_Project/Art/ThirdParty/0x72DungeonTilesetII/Core/frames/";
    static string Field(string name) => "<" + name + ">k__BackingField";
    static void Number(SerializedProperty parent, string name, float value)
    {
        var p = parent.FindPropertyRelative(Field(name));
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value;
        else p.floatValue = value;
    }
    static Sprite Sprite(string name)
    {
        var path = Frames + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new InvalidOperationException("Missing texture: " + name);
        // Same world scale as the existing knight/elf actors; keep sprite IDs intact.
        var defaults = importer.GetDefaultPlatformTextureSettings();
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16f)
            || importer.filterMode != FilterMode.Point
            || defaults.textureCompression != TextureImporterCompression.Uncompressed
            || standalone.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaults);
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
            ?? throw new InvalidOperationException("Missing sprite: " + name);
    }

    [MenuItem("Godmist/Content/Configure VS-06 party")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        // Source: design/Statystyki Postaci.xlsx, columns Scout/Warrior/Mage/Paladin.
        string[] names = { "Jerzy", "Wladimir_Test", "Mage", "Paladin" };
        string[] stats = { "MaxHealth", "MinAttack", "MaxAttack", "PhysicalDefense", "MagicalDefense", "Dodge", "Speed", "MaxResource", "ResourceRegeneration", "CritChance", "CritDamage", "WeaponAccuracy" };
        float[,] values = {
            {300,18,36,8,4,17,55,200,0,0,1,0},
            {375,24,32,12,8,10,40,50,0,0,1,0},
            {250,27,36,6,12,6,45,100,20,0,1,0},
            {450,21,30,16,16,8,35,100,15,0,1,0}
        };
        string[] growthNames = { "MaxHealth", "MinAttack", "MaxAttack", "PhysicalDefense", "MagicalDefense", "Dodge" };
        float[,] growth = {{10,.8f,1.1f,.33f,.2f,.08f},{12.5f,.7f,.95f,.45f,.3f,.05f},{7.5f,1,1.3f,.25f,.5f,.04f},{20,.6f,.8f,.6f,.6f,.04f}};
        string[] resistanceNames = { "BleedResistance", "PoisonResistance", "FireResistance", "WeakeningResistance", "SuppressionResistance", "MoveResistance" };
        // EOR maps to both existing hard-control resistance fields, as on Jerzy.
        float[,] resistance = {{.5f,.7f,.4f,.5f,.35f,.35f},{.7f,.5f,.4f,.6f,.6f,.6f},{.25f,.25f,.8f,.7f,.3f,.3f},{.3f,.4f,.5f,.25f,.75f,.75f}};
        for (var i = 0; i < names.Length; i++)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "CD_" + names[i] + ".asset")
                ?? throw new InvalidOperationException(names[i]);
            var so = new SerializedObject(character);
            for (var j=0;j<stats.Length;j++) Number(so.FindProperty(Field("BaseStats")),stats[j],values[i,j]);
            for (var j=0;j<growthNames.Length;j++) Number(so.FindProperty(Field("GrowthData")),growthNames[j],growth[i,j]);
            for (var j=0;j<resistanceNames.Length;j++) Number(so.FindProperty(Field("BaseResistances")),resistanceNames[j],resistance[i,j]);
            if (i >= 2)
            {
                var prefix = i == 2 ? "wizzard_f" : "knight_f";
                so.FindProperty(Field("VisualSprite")).objectReferenceValue = Sprite(prefix + "_idle_anim_f0");
                so.FindProperty(Field("HitSprite")).objectReferenceValue = Sprite(prefix + "_hit_anim_f0");
                foreach (var animation in new[] { "Idle", "Run" })
                {
                    var list = so.FindProperty(Field(animation + "AnimationFrames")); list.arraySize = 4;
                    for (var frame=0;frame<4;frame++) list.GetArrayElementAtIndex(frame).objectReferenceValue = Sprite(prefix + "_" + animation.ToLowerInvariant() + "_anim_f" + frame);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(character);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("VS-06: four party definitions configured; stable IDs and skill content preserved.");
    }
}
