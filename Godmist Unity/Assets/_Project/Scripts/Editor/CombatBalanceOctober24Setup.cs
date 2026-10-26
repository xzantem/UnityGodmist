using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Views.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class CombatBalanceOctober24Setup
    {
        public const string UpPath="Assets/_Project/Art/Props/Catacombs/CatacombsStairsUp32-v1.png";
        [MenuItem("Godmist/Content/Apply focused combat balance October 24")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var output=Path.GetFullPath("../LocalHandoffs/combat-balance-20261024");Directory.CreateDirectory(output);
            var report=new System.Collections.Generic.List<string>();
            var crushing=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinCrushingBlow.asset");
            var data=new SerializedObject(crushing);var effects=data.FindProperty("effects");
            if(crushing.Effects[1].EffectType==EffectType.ApplyStatus)effects.MoveArrayElement(2,1);
            data.FindProperty("<TooltipDescription>k__BackingField").stringValue="Minimalne obrażenia fizyczne; najpierw 90% szansy na obniżenie odporności na kontrolę o 30% na 3 tury, następnie 50% szansy na ogłuszenie na 2 tury.";
            foreach (var effect in Enumerable.Range(0, effects.arraySize).Select(effects.GetArrayElementAtIndex))
                if (effect.FindPropertyRelative("effectType").intValue == (int)EffectType.ApplyStatus
                    && effect.FindPropertyRelative("statusId").stringValue == StatusEffectIds.Stun)
                    effect.FindPropertyRelative("durationTurns").intValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(crushing);
            var transfusion=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinTransfusion.asset");
            data=new SerializedObject(transfusion);data.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("lifeStealPercent").floatValue=2;
            data.FindProperty("<TooltipDescription>k__BackingField").stringValue="Losowe obrażenia magiczne; rzucający odzyskuje 200% faktycznie zadanych obrażeń jako HP.";
            data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(transfusion);
            foreach(var name in new[]{"Heal","GroupHeal"})
            {
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_Paladin"+name+".asset");
                data=new SerializedObject(skill);
                data.FindProperty("<TooltipDescription>k__BackingField").stringValue=name=="Heal"
                    ?"Przywraca 100–140% siły leczenia (średniego ataku).":"Przywraca wszystkim sojusznikom 42–58% siły leczenia.";
                data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(skill);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/_Project/ScriptableObjects"}))
            {
                var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(character==null||!character.Id.StartsWith("enemy.",StringComparison.Ordinal)||character.Id.Contains(".test"))continue;
                if(character.name=="CD_Enemy_Test")
                {
                    data=new SerializedObject(character);data.FindProperty("<AttackMultiplier>k__BackingField").floatValue=1;
                    data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(character);continue;
                }
                data=new SerializedObject(character);data.FindProperty("<AttackMultiplier>k__BackingField").floatValue=.85f;
                data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(character);
                report.Add(character.Id+": attack multiplier .85; HP/base/growth unchanged");
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(UpPath);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
            const string prefab="Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var marker=root.transform.Find("Marker").GetComponent<SpriteRenderer>();
                data=new SerializedObject(root.GetComponent<DungeonExitView>());
                data.FindProperty("transitionRenderer").objectReferenceValue=marker;
                data.FindProperty("downwardSprite").objectReferenceValue=marker.sprite;
                data.FindProperty("upwardSprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(UpPath);
                data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Path.Combine(output,"authoring.txt"),report);
        }
    }
}
