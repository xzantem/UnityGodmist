using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    public static class ContentPass2FrostSetup
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        [Serializable] class Plan{public Entry[] entries;}
        [Serializable] class Entry{public string slot,id,name;public int ranks,tier,column;public string[] requirements,descriptions;public Active active;}
        [Serializable] class Active{public int mana,ap_percent,range;}
        static string F(string key)=>"<"+key+">k__BackingField";
        static void Set(SerializedProperty p,object v)
        {
            if(p==null)throw new InvalidOperationException("Missing serialized property");
            if(v is UnityEngine.Object o)p.objectReferenceValue=o;else if(v==null)p.objectReferenceValue=null;
            else if(v is string s)p.stringValue=s;else if(v is bool b)p.boolValue=b;
            else if(v is int i)p.intValue=i;else if(v is Enum)p.intValue=Convert.ToInt32(v);else p.floatValue=Convert.ToSingle(v);
        }
        static void Field(SerializedObject so,string key,object v)=>Set(so.FindProperty(F(key)),v);
        static void Field(SerializedProperty so,string key,object v)=>Set(so.FindPropertyRelative(F(key)),v);
        [MenuItem("Godmist/Content Pass 2/Apply Frost Mage")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Tools/ContentPass2/frost-content.json"));
            if(plan.entries.Length!=25||plan.entries.Sum(e=>e.ranks)!=44||plan.entries.Count(e=>e.active?.mana>0)!=3)throw new InvalidOperationException("Expected25/44/3");
            ImportIcon("Assets/Resources/Godmist/CombatPoses/TargetVfx/cp2-frost-floor-32.png");
            var path=Root+"Skills/SkillTrees/ST_Mage_Frost.asset";
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);
            if(tree==null){tree=ScriptableObject.CreateInstance<SkillTreeDefinition>();AssetDatabase.CreateAsset(tree,path);}
            var so=new SerializedObject(tree);Field(so,"Id",MageCombatSystem.Frost);Field(so,"DisplayName","Mag Mrozu");
            var global=so.FindProperty(F("GlobalPassive"));Field(global,"DisplayName","Pasyw drzewka");
            Field(global,"Description","Za punkt: +1,5 pp szansy krytycznej przeciw zamrożonemu celowi i +0,5 pp odporności na kontrolę.");
            global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var nodes=so.FindProperty(F("Nodes"));nodes.ClearArray();nodes.arraySize=25;
            for(int i=0;i<plan.entries.Length;i++)
            {
                var e=plan.entries[i];var icon=ImportIcon("Assets/_Project/Art/Icons/Passives/VS22/MageFrost/"+e.slot+"-32.png");
                var skill=e.active?.mana>0?ConfigureSkill(e,icon):null;var node=nodes.GetArrayElementAtIndex(i);
                Field(node,"Id",e.id);Field(node,"DisplayName",e.name);Field(node,"Description",e.descriptions[0]);
                Field(node,"Tier",e.tier);Field(node,"Column",e.column);Field(node,"MaxRanks",e.ranks);Field(node,"Icon",icon);Field(node,"GrantedSkill",skill);
                node.FindPropertyRelative(F("PassiveEffects")).ClearArray();var req=node.FindPropertyRelative(F("Requirements"));req.ClearArray();req.arraySize=e.requirements.Length;
                for(int j=0;j<e.requirements.Length;j++){Field(req.GetArrayElementAtIndex(j),"NodeId","node.mage.frost."+e.requirements[j]);Field(req.GetArrayElementAtIndex(j),"MinimumRanks",1);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(tree);
            var mage=AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_Mage.asset");var cls=new SerializedObject(mage);var list=cls.FindProperty(F("SkillTrees"));
            if(!mage.SkillTrees.Contains(tree)){list.arraySize++;Set(list.GetArrayElementAtIndex(list.arraySize-1),tree);cls.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(mage);}
        }
        static SkillData ConfigureSkill(Entry e,Sprite icon)
        {
            var path=Root+"Skills/SD_MageFrost_"+e.slot+".asset";var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<SkillData>();AssetDatabase.CreateAsset(skill,path);}
            var so=new SerializedObject(skill);Field(so,"SkillId","skill.mage.frost."+e.slot);Field(so,"DisplayName",e.name);Field(so,"TooltipDescription",e.descriptions[0]);Field(so,"Icon",icon);
            Field(so,"ResourceType",ResourceType.Mana);Field(so,"ResourceCost",e.active.mana);Field(so,"ActionCost",e.active.ap_percent);Field(so,"RequiresLineOfSight",true);
            Field(so,"SkillAccuracy",100f);Field(so,"AttackType",AttackType.Unknown);
            var modes=so.FindProperty("targetModes");modes.ClearArray();modes.arraySize=1;var mode=modes.GetArrayElementAtIndex(0);
            Set(mode.FindPropertyRelative("targetType"),e.slot=="t5_4"?TargetType.AreaAroundTile:TargetType.Single);
            Set(mode.FindPropertyRelative("targetingType"),e.slot=="t3_4"?TargetingType.Allies:TargetingType.Enemies);Set(mode.FindPropertyRelative("includesCaster"),e.slot=="t3_4");
            var range=mode.FindPropertyRelative("absoluteRange");Set(range.FindPropertyRelative("shape"),RangeShape.Radial);Set(range.FindPropertyRelative("minRange"),0);Set(range.FindPropertyRelative("maxRange"),e.active.range);
            range=mode.FindPropertyRelative("relativeRange");Set(range.FindPropertyRelative("shape"),RangeShape.Square);Set(range.FindPropertyRelative("minRange"),0);Set(range.FindPropertyRelative("maxRange"),1);
            var effects=so.FindProperty("effects");effects.ClearArray();effects.arraySize=e.slot=="t1_4"?2:1;
            for(int i=0;i<effects.arraySize;i++)
            {
                var effect=effects.GetArrayElementAtIndex(i);Set(effect.FindPropertyRelative("stableEffectId"),i==0?"main":"freeze");
                Set(effect.FindPropertyRelative("effectType"),e.slot=="t3_4"?EffectType.MageFrostAura:e.slot=="t5_4"?EffectType.MageFrostField:i==0?EffectType.DealDamage:EffectType.ApplyStatus);
                Set(effect.FindPropertyRelative("polarity"),e.slot=="t3_4"?EffectPolarity.Positive:EffectPolarity.Negative);Set(effect.FindPropertyRelative("baseChance"),i==1?.5f:1f);
                Set(effect.FindPropertyRelative("alwaysHits"),e.slot!="t1_4"||i==1);Set(effect.FindPropertyRelative("damageHitCount"),1);
                Set(effect.FindPropertyRelative("damageType"),DamageType.Magical);Set(effect.FindPropertyRelative("damageValueSource"),DamageValueSource.AttackRange);Set(effect.FindPropertyRelative("damageMultiplier"),1.25f);
                Set(effect.FindPropertyRelative("bonusDamageMultiplier"),1f);Set(effect.FindPropertyRelative("durationTurns"),e.slot=="t3_4"?3:e.slot=="t5_4"?6:i==1?2:0);
                if(i==1)
                {
                    Set(effect.FindPropertyRelative("statusId"),"freeze");Set(effect.FindPropertyRelative("stacks"),1);
                    var conditions=effect.FindPropertyRelative("conditions");conditions.arraySize=1;var condition=conditions.GetArrayElementAtIndex(0);
                    Set(condition.FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);Set(condition.FindPropertyRelative("referencedEffectId"),"main");Set(condition.FindPropertyRelative("referencedEffectIndex"),0);
                }
            }
            Set(so.FindProperty("stableEffectIdsInitialized"),true);ContextualSkillHitSetup.Apply(so);so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(skill);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");var cat=new SerializedObject(catalog);var skills=cat.FindProperty(F("SkillDefinitions"));
            if(!catalog.SkillDefinitions.Contains(skill)){skills.arraySize++;Set(skills.GetArrayElementAtIndex(skills.arraySize-1),skill);cat.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);}
            return skill;
        }
        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=false;
            var config=imp.GetDefaultPlatformTextureSettings();config.textureCompression=TextureImporterCompression.Uncompressed;imp.SetPlatformTextureSettings(config);imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
