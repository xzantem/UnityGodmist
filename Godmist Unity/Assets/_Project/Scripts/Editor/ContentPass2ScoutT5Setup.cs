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
    public static class ContentPass2ScoutT5Setup
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        const string Art="Assets/_Project/Art/Icons/Passives/VS22/ScoutT5/";
        [Serializable] class Plan{public Entry[] entries;}
        [Serializable] class Entry{public string slot,tree,name;public int column,ranks;public string[] requirements,descriptions;}
        static string F(string s)=>"<"+s+">k__BackingField";
        static void Set(SerializedProperty p,object value)
        {
            if(p==null)throw new InvalidOperationException("Missing serialized field");
            if(value is string text)p.stringValue=text;else if(value is bool b)p.boolValue=b;
            else if(value is int n)p.intValue=n;else if(value is Enum)p.intValue=Convert.ToInt32(value);
            else if(value is UnityEngine.Object o)p.objectReferenceValue=o;else p.floatValue=Convert.ToSingle(value);
        }
        static void Field(SerializedObject s,string name,object value)=>Set(s.FindProperty(F(name)),value);
        [MenuItem("Tools/Godmist/Content Pass 2/Apply Scout T5")]
        public static void Apply()
        {
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/Art/ScoutT5Icons20261102/prompts.json")));
            if(plan.entries.Length!=10)throw new InvalidOperationException("Expected ten nodes");
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var cat=new SerializedObject(catalog);var skills=cat.FindProperty(F("SkillDefinitions"));
            foreach(var entry in plan.entries)
            {
                var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Root+"Skills/SkillTrees/ST_Scout_"+(entry.tree=="assassin"?"Assassin":"BladeDancer")+".asset");
                var icon=ImportIcon(Art+entry.slot+"-32.png");
                var active=entry.slot is "a2" or "b1" or "b4";
                var skill=active?Skill(entry,icon):null;
                if(skill!=null&&!Enumerable.Range(0,skills.arraySize).Any(i=>skills.GetArrayElementAtIndex(i).objectReferenceValue==skill))
                {skills.InsertArrayElementAtIndex(skills.arraySize);skills.GetArrayElementAtIndex(skills.arraySize-1).objectReferenceValue=skill;}
                var so=new SerializedObject(tree);var nodes=so.FindProperty(F("Nodes"));
                var id="node.scout."+entry.tree+".t5_"+entry.column;
                var index=Enumerable.Range(0,nodes.arraySize).Where(i=>nodes.GetArrayElementAtIndex(i).FindPropertyRelative(F("Id")).stringValue==id).DefaultIfEmpty(-1).First();
                if(index<0){index=nodes.arraySize;nodes.InsertArrayElementAtIndex(index);}
                var node=nodes.GetArrayElementAtIndex(index);
                Set(node.FindPropertyRelative(F("Id")),id);Set(node.FindPropertyRelative(F("DisplayName")),entry.name);
                Set(node.FindPropertyRelative(F("Description")),entry.descriptions[0]);Set(node.FindPropertyRelative(F("Tier")),5);
                Set(node.FindPropertyRelative(F("Column")),entry.column);Set(node.FindPropertyRelative(F("MaxRanks")),entry.ranks);
                node.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=skill;
                node.FindPropertyRelative(F("Icon")).objectReferenceValue=icon;node.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=node.FindPropertyRelative(F("Requirements"));req.ClearArray();req.arraySize=entry.requirements.Length;
                for(var j=0;j<entry.requirements.Length;j++)
                {Set(req.GetArrayElementAtIndex(j).FindPropertyRelative(F("NodeId")),"node.scout."+entry.tree+"."+entry.requirements[j]);Set(req.GetArrayElementAtIndex(j).FindPropertyRelative(F("MinimumRanks")),1);}
                so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(tree);
            }
            cat.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);
        }
        static SkillData Skill(Entry e,Sprite icon)
        {
            var path=Root+"Skills/SD_ScoutT5_"+e.slot+".asset";var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<SkillData>();AssetDatabase.CreateAsset(skill,path);}
            var so=new SerializedObject(skill);bool curse=e.slot=="b4",roll=e.slot=="a2";
            Field(so,"SkillId","skill.scout."+e.tree+".t5_"+e.column);Field(so,"DisplayName",e.name);Field(so,"TooltipDescription",e.descriptions[0]);Field(so,"Icon",icon);
            Field(so,"ActionCost",roll?80:curse?70:50);Field(so,"ResourceCost",roll?80:curse?55:50);Field(so,"ResourceType",ResourceType.Momentum);
            Field(so,"SkillAccuracy",curse?82f:100f);Field(so,"AttackType",curse?AttackType.Ranged:AttackType.Unknown);
            Field(so,"RequiresLineOfSight",curse||roll);Field(so,"KnifeCost",curse?1:0);Field(so,"FreeKnivesInMelee",curse);
            Field(so,"PointBlankDamageMultiplier",curse?.6f:1f);Field(so,"PointBlankAttackType",curse?AttackType.Melee:AttackType.Unknown);
            var modes=so.FindProperty("targetModes");modes.ClearArray();modes.arraySize=1;var mode=modes.GetArrayElementAtIndex(0);
            Set(mode.FindPropertyRelative("targetType"),curse?TargetType.Single:TargetType.Caster);Set(mode.FindPropertyRelative("targetingType"),curse?TargetingType.Enemies:TargetingType.Allies);
            Set(mode.FindPropertyRelative("includesCaster"),!curse);var range=mode.FindPropertyRelative("absoluteRange");Set(range.FindPropertyRelative("shape"),RangeShape.Radial);Set(range.FindPropertyRelative("minRange"),curse?1:0);Set(range.FindPropertyRelative("maxRange"),curse?4:0);
            var effects=so.FindProperty("effects");effects.arraySize=roll?3:1;
            for(var i=0;i<effects.arraySize;i++)
            {
                var effect=effects.GetArrayElementAtIndex(i);
                Set(effect.FindPropertyRelative("stableEffectId"),roll?new[]{"move","critical","dodge"}[i]:"main");
                Set(effect.FindPropertyRelative("effectType"),curse?EffectType.DealDamage:roll&&i==0?EffectType.Teleport:EffectType.ApplyUniqueBuff);
                Set(effect.FindPropertyRelative("polarity"),curse?EffectPolarity.Negative:EffectPolarity.Positive);Set(effect.FindPropertyRelative("alwaysHits"),!curse);Set(effect.FindPropertyRelative("baseChance"),1f);
                if(roll&&i==0)
                {var dest=effect.FindPropertyRelative("teleportDestinationRange");Set(dest.FindPropertyRelative("shape"),RangeShape.Radial);Set(dest.FindPropertyRelative("minRange"),1);Set(dest.FindPropertyRelative("maxRange"),2);}
                else if(roll)
                {var conditions=effect.FindPropertyRelative("conditions");conditions.arraySize=1;
                    Set(conditions.GetArrayElementAtIndex(0).FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);
                    Set(conditions.GetArrayElementAtIndex(0).FindPropertyRelative("referencedEffectId"),"move");
                    Set(conditions.GetArrayElementAtIndex(0).FindPropertyRelative("referencedEffectIndex"),0);Set(effect.FindPropertyRelative("uniqueBuffType"),i==1?UniqueBuffType.GuaranteedCriticalHit:UniqueBuffType.GuaranteedDodge);Set(effect.FindPropertyRelative("uniqueBuffStacks"),1);Set(effect.FindPropertyRelative("durationTurns"),0);}
                else if(curse)
                {Set(effect.FindPropertyRelative("damageType"),DamageType.Physical);Set(effect.FindPropertyRelative("damageValueSource"),DamageValueSource.AttackRange);Set(effect.FindPropertyRelative("damageMultiplier"),1f);Set(effect.FindPropertyRelative("damageHitCount"),1);}
                else
                {Set(effect.FindPropertyRelative("uniqueBuffType"),UniqueBuffType.AuraStatus);Set(effect.FindPropertyRelative("uniqueBuffStacks"),1);Set(effect.FindPropertyRelative("statusId"),"poison");Set(effect.FindPropertyRelative("baseChance"),.7f);Set(effect.FindPropertyRelative("statusDamageMultiplier"),.4f);Set(effect.FindPropertyRelative("durationTurns"),3);Set(effect.FindPropertyRelative("auraRadius"),2);Set(effect.FindPropertyRelative("auraTargetingType"),TargetingType.Enemies);}
            }
            so.FindProperty("stableEffectIdsInitialized").boolValue=true;
            ContextualSkillHitSetup.Apply(so);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(skill);AssetDatabase.SaveAssetIfDirty(skill);return skill;
        }
        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=false;
            var settings=imp.GetDefaultPlatformTextureSettings();settings.textureCompression=TextureImporterCompression.Uncompressed;imp.SetPlatformTextureSettings(settings);imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
