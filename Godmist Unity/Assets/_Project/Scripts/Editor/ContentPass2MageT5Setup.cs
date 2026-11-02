using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ContentPass2MageT5Setup
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static string F(string name)=>"<"+name+">k__BackingField";
        [Serializable] class Plan{public Entry[] entries;}
        [Serializable] class Active{public int mana,ap_percent;}
        [Serializable] class Entry{public string slot,id,tree,name;public int ranks,column;public string[] requirements,descriptions;public Active active;}
        [MenuItem("Godmist/Content Pass 2/Apply Mage Tier Five")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Tools/ContentPass2/mage-t5-content.json"));
            if(plan?.entries?.Length!=10||plan.entries.Sum(e=>e.ranks)!=18)throw new InvalidOperationException("Expected10 Mage T5 nodes/18 ranks.");
            foreach(var e in plan.entries)
            {
                var path=Root+"Skills/SkillTrees/ST_Mage_"+(e.tree=="fire"?"Fire":"Warlock")+".asset";
                var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);
                if(tree==null)throw new InvalidOperationException(path);
                var icon=ImportIcon("Assets/_Project/Art/Icons/Passives/VS22/MageT5/"+e.slot+"-32.png");
                var skill=e.active!=null&&e.active.mana>0?ConfigureSkill(e,icon):null;
                var so=new SerializedObject(tree);var nodes=so.FindProperty(F("Nodes"));
                var index=Enumerable.Range(0,nodes.arraySize).Where(i=>nodes.GetArrayElementAtIndex(i).FindPropertyRelative(F("Id")).stringValue==e.id).DefaultIfEmpty(-1).First();
                if(index<0){index=nodes.arraySize;nodes.arraySize++;}
                var node=nodes.GetArrayElementAtIndex(index);
                Field(node,"Id",e.id);Field(node,"DisplayName",e.name);Field(node,"Description",e.descriptions[0]);
                Field(node,"Tier",5);Field(node,"Column",e.column);Field(node,"MaxRanks",e.ranks);Field(node,"Icon",icon);Field(node,"GrantedSkill",skill);
                node.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var requirements=node.FindPropertyRelative(F("Requirements"));requirements.ClearArray();requirements.arraySize=e.requirements.Length;
                for(int i=0;i<e.requirements.Length;i++){Field(requirements.GetArrayElementAtIndex(i),"NodeId","node.mage."+e.tree+"."+e.requirements[i]);Field(requirements.GetArrayElementAtIndex(i),"MinimumRanks",1);}
                so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(tree);
            }
            // Existing debuff is contact-gated just like its burn; no effect after a dodged spell.
            var arrow=AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_MageFireArrow.asset");var a=new SerializedObject(arrow);var effects=a.FindProperty("effects");
            var first=effects.GetArrayElementAtIndex(0).FindPropertyRelative("stableEffectId").stringValue;
            for(int i=0;i<effects.arraySize;i++)
                if(effects.GetArrayElementAtIndex(i).FindPropertyRelative("effectType").intValue==(int)EffectType.ModifyVar)
                    RequireHit(effects.GetArrayElementAtIndex(i),first);
            a.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(arrow);
        }
        static void Set(SerializedProperty p,object value)
        {
            if(value is UnityEngine.Object obj)p.objectReferenceValue=obj;
            else if(value==null)p.objectReferenceValue=null;
            else if(value is string text)p.stringValue=text;
            else if(value is bool flag)p.boolValue=flag;
            else if(value is int number)p.intValue=number;
            else if(value is Enum)p.intValue=Convert.ToInt32(value);
            else p.floatValue=Convert.ToSingle(value);
        }
        static void Field(SerializedProperty p,string name,object value)=>Set(p.FindPropertyRelative(F(name)),value);
        static void Field(SerializedObject p,string name,object value)=>Set(p.FindProperty(F(name)),value);
        static void RequireHit(SerializedProperty effect,string first)
        {
            var conditions=effect.FindPropertyRelative("conditions");conditions.ClearArray();conditions.arraySize=1;var c=conditions.GetArrayElementAtIndex(0);
            Set(c.FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);
            Set(c.FindPropertyRelative("referencedEffectIndex"),0);Set(c.FindPropertyRelative("referencedEffectId"),first);
        }
        static SkillData ConfigureSkill(Entry entry,Sprite icon)
        {
            var path=Root+"Skills/SD_MageT5_"+entry.slot+".asset";var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<SkillData>();AssetDatabase.CreateAsset(skill,path);}
            var so=new SerializedObject(skill);var teleport=entry.slot=="w4";
            Field(so,"SkillId","skill.mage."+entry.tree+".t5_"+entry.column);Field(so,"DisplayName",entry.name);
            Field(so,"TooltipDescription",entry.descriptions[0]);Field(so,"Icon",icon);Field(so,"ActionCost",entry.active.ap_percent);
            Field(so,"ResourceCost",entry.active.mana);Field(so,"ResourceType",ResourceType.Mana);Field(so,"AttackType",AttackType.Unknown);
            Field(so,"SkillAccuracy",100f);Field(so,"RequiresLineOfSight",true);
            var modes=so.FindProperty("targetModes");modes.ClearArray();modes.arraySize=1;var mode=modes.GetArrayElementAtIndex(0);
            Set(mode.FindPropertyRelative("targetType"),teleport?TargetType.Single:TargetType.AreaAroundTile);
            Set(mode.FindPropertyRelative("targetingType"),teleport?TargetingType.Allies:TargetingType.Enemies);
            Set(mode.FindPropertyRelative("includesCaster"),teleport);
            var range=mode.FindPropertyRelative("absoluteRange");Set(range.FindPropertyRelative("shape"),RangeShape.Radial);
            Set(range.FindPropertyRelative("minRange"),0);Set(range.FindPropertyRelative("maxRange"),teleport?7:8);
            range=mode.FindPropertyRelative("relativeRange");Set(range.FindPropertyRelative("shape"),RangeShape.Square);
            Set(range.FindPropertyRelative("minRange"),0);Set(range.FindPropertyRelative("maxRange"),1);
            var effects=so.FindProperty("effects");effects.ClearArray();effects.arraySize=teleport?1:2;
            for(int i=0;i<effects.arraySize;i++)
            {
                var effect=effects.GetArrayElementAtIndex(i);Set(effect.FindPropertyRelative("stableEffectId"),i==0?"hit":"burn");
                Set(effect.FindPropertyRelative("effectType"),teleport?EffectType.Teleport:i==0?EffectType.DealDamage:EffectType.ApplyStatus);
                Set(effect.FindPropertyRelative("polarity"),teleport?EffectPolarity.Positive:EffectPolarity.Negative);
                Set(effect.FindPropertyRelative("baseChance"),i==0?1f:.85f);Set(effect.FindPropertyRelative("alwaysHits"),teleport||i>0);
                Set(effect.FindPropertyRelative("damageMultiplier"),1f);Set(effect.FindPropertyRelative("bonusDamageMultiplier"),1f);
                Set(effect.FindPropertyRelative("damageValueSource"),DamageValueSource.AttackRange);Set(effect.FindPropertyRelative("damageType"),DamageType.Magical);
                Set(effect.FindPropertyRelative("damageHitCount"),1);
                if(teleport){var dest=effect.FindPropertyRelative("teleportDestinationRange");Set(dest.FindPropertyRelative("shape"),RangeShape.Radial);Set(dest.FindPropertyRelative("minRange"),1);Set(dest.FindPropertyRelative("maxRange"),6);}
                else if(i==1){Set(effect.FindPropertyRelative("statusId"),"burn");Set(effect.FindPropertyRelative("statusDamageMultiplier"),.9f);Set(effect.FindPropertyRelative("durationTurns"),4);Set(effect.FindPropertyRelative("stacks"),1);RequireHit(effect,"hit");}
            }
            Set(so.FindProperty("stableEffectIdsInitialized"),true);ContextualSkillHitSetup.Apply(so);so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(skill);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");var cat=new SerializedObject(catalog);var list=cat.FindProperty(F("SkillDefinitions"));
            if(!Enumerable.Range(0,list.arraySize).Any(i=>list.GetArrayElementAtIndex(i).objectReferenceValue==skill)){list.arraySize++;list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;}
            cat.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);return skill;
        }
        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;
            var settings=importer.GetDefaultPlatformTextureSettings();settings.textureCompression=TextureImporterCompression.Uncompressed;importer.SetPlatformTextureSettings(settings);importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
