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
    // Focused, explicit, additive authoring. Never runs automatically on import.
    public static class ContentPass2ManAtArmsSetup
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        const string TreePath=Root+"Skills/SkillTrees/ST_Warrior_ManAtArms.asset";
        const string Art="Assets/_Project/Art/Icons/Passives/VS22/WarriorManAtArms/";
        [Serializable] class Plan {public Entry[] entries;}
        [Serializable] class Entry {public string slot,name;public int ranks;public string[] requirements,descriptions;public Active active;}
        [Serializable] class Active {public int cost,ap;public string effect;}
        static string F(string s)=>"<"+s+">k__BackingField";
        static void Set(SerializedProperty p,object v)
        {
            if(p==null)throw new InvalidOperationException("Unknown serialized field");
            if(v is UnityEngine.Object o)p.objectReferenceValue=o;
            else if(v is string s)p.stringValue=s;else if(v is bool b)p.boolValue=b;
            else if(v is Enum)p.intValue=Convert.ToInt32(v);else if(v is int i)p.intValue=i;
            else p.floatValue=Convert.ToSingle(v);
        }
        static void Field(SerializedObject s,string key,object value)=>Set(s.FindProperty(F(key)),value);
        [MenuItem("Tools/Godmist/Content Pass 2/Apply Man-at-Arms T1-T5")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/Art/ManAtArmsIcons20261102/prompts.json")));
            if(plan.entries.Length!=25)throw new InvalidOperationException("Expected25 approved nodes");
            var icons=plan.entries.Select(e=>ImportIcon(Art+e.slot+"-32.png")).ToArray();
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreePath);
            if(tree==null){tree=ScriptableObject.CreateInstance<SkillTreeDefinition>();AssetDatabase.CreateAsset(tree,TreePath);}
            var so=new SerializedObject(tree);Field(so,"Id",WarriorCombatSystem.ManAtArms);Field(so,"DisplayName","Zbrojny");
            var global=so.FindProperty(F("GlobalPassive"));
            Set(global.FindPropertyRelative(F("DisplayName")),"Pasyw drzewka");
            Set(global.FindPropertyRelative(F("Description")),"Za punkt: +15 maksymalnego zdrowia; 2% zredukowanych obrażeń wzmacnia następny atak.");
            global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var nodes=so.FindProperty(F("Nodes"));nodes.ClearArray();nodes.arraySize=25;
            var skills=new System.Collections.Generic.List<SkillData>();
            for(var i=0;i<25;i++)
            {
                var e=plan.entries[i];var node=nodes.GetArrayElementAtIndex(i);
                Set(node.FindPropertyRelative(F("Id")),"node.warrior.man_at_arms."+e.slot);
                Set(node.FindPropertyRelative(F("DisplayName")),e.name);
                Set(node.FindPropertyRelative(F("Description")),e.descriptions[0]);
                Set(node.FindPropertyRelative(F("Tier")),e.slot[1]-'0');
                Set(node.FindPropertyRelative(F("Column")),e.slot[3]-'0');
                Set(node.FindPropertyRelative(F("MaxRanks")),e.ranks);
                node.FindPropertyRelative(F("Icon")).objectReferenceValue=icons[i];
                node.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                node.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=null;
                if(!string.IsNullOrEmpty(e.active?.effect))
                {
                    var skill=ConfigureSkill(e,icons[i]);skills.Add(skill);
                    node.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=skill;
                }
                var req=node.FindPropertyRelative(F("Requirements"));req.ClearArray();req.arraySize=e.requirements.Length;
                for(var j=0;j<e.requirements.Length;j++)
                {
                    Set(req.GetArrayElementAtIndex(j).FindPropertyRelative(F("NodeId")),"node.warrior.man_at_arms."+e.requirements[j]);
                    Set(req.GetArrayElementAtIndex(j).FindPropertyRelative(F("MinimumRanks")),1);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(tree);AssetDatabase.SaveAssetIfDirty(tree);
            AppendToWarrior();
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var cs=new SerializedObject(catalog);var list=cs.FindProperty(F("SkillDefinitions"));
            foreach(var skill in skills)
            {
                if(Enumerable.Range(0,list.arraySize).Any(i=>list.GetArrayElementAtIndex(i).objectReferenceValue==skill))continue;
                list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;
            }
            cs.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);
        }
        public static void AppendToWarrior()
        {
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreePath);if(tree==null)return;
            var c=AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_Warrior.asset");
            if(c.SkillTrees.Any(t=>t==tree))return;
            var so=new SerializedObject(c);var list=so.FindProperty(F("SkillTrees"));
            list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=tree;
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(c);
        }
        static SkillData ConfigureSkill(Entry e,Sprite icon)
        {
            var path=Root+"Skills/SD_WarriorManAtArms_"+e.slot+".asset";
            var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<SkillData>();AssetDatabase.CreateAsset(skill,path);}
            var so=new SerializedObject(skill);
            Field(so,"SkillId","skill.warrior.man_at_arms."+e.slot);Field(so,"DisplayName",e.name);
            Field(so,"TooltipDescription",e.descriptions[0]);Field(so,"Icon",icon);
            Field(so,"ActionCost",e.active.ap);Field(so,"ResourceCost",e.active.cost);
            Field(so,"ResourceType",ResourceType.Rage);Field(so,"SkillAccuracy",e.slot=="t2_3"?70f:100f);
            Field(so,"AttackType",e.slot=="t2_3"?AttackType.Melee:AttackType.Unknown);
            Field(so,"RequiresLineOfSight",e.slot=="t2_3");
            var modes=so.FindProperty("targetModes");modes.ClearArray();modes.arraySize=1;
            var mode=modes.GetArrayElementAtIndex(0);var enemy=e.slot=="t2_3";
            Set(mode.FindPropertyRelative("targetType"),enemy?TargetType.Single:TargetType.Caster);
            Set(mode.FindPropertyRelative("targetingType"),enemy?TargetingType.Enemies:TargetingType.Allies);
            Set(mode.FindPropertyRelative("includesCaster"),!enemy);
            var range=mode.FindPropertyRelative("absoluteRange");Set(range.FindPropertyRelative("shape"),RangeShape.Radial);
            Set(range.FindPropertyRelative("minRange"),enemy?1:0);Set(range.FindPropertyRelative("maxRange"),enemy?1:0);
            var effects=so.FindProperty("effects");effects.arraySize=1;var effect=effects.GetArrayElementAtIndex(0);
            Set(effect.FindPropertyRelative("stableEffectId"),"main");
            so.FindProperty("stableEffectIdsInitialized").boolValue=true;
            Set(effect.FindPropertyRelative("effectType"),Enum.Parse<EffectType>(e.active.effect));
            Set(effect.FindPropertyRelative("polarity"),enemy?EffectPolarity.Negative:EffectPolarity.Positive);
            Set(effect.FindPropertyRelative("alwaysHits"),!enemy);Set(effect.FindPropertyRelative("baseChance"),enemy?.7f:1f);
            Set(effect.FindPropertyRelative("durationTurns"),e.slot=="t5_3"?2:e.slot=="t2_3"?3:5);
            if(e.slot=="t1_2")Set(effect.FindPropertyRelative("shieldMaxHealthPercent"),.2f);
            if(e.slot=="t2_3")Set(effect.FindPropertyRelative("statusId"),"stun");
            if(e.slot=="t4_1")
            {
                Set(effect.FindPropertyRelative("modifierStatType"),CharacterStatType.Dodge);
                Set(effect.FindPropertyRelative("modifierOperation"),StatModifierOperation.FlatAbsolute);
                Set(effect.FindPropertyRelative("modifierValue"),5f);
            }
            if(e.slot=="t5_3")Set(effect.FindPropertyRelative("percentageValue"),.2f);
            ContextualSkillHitSetup.Apply(so);so.ApplyModifiedPropertiesWithoutUndo();
            // Keep effect identities stable across repeat authoring.
            SkillEffectIdentity.ExecutionOrder(skill);
            EditorUtility.SetDirty(skill);AssetDatabase.SaveAssetIfDirty(skill);
            return skill;
        }
        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException("Missing icon "+path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=false;
            foreach(var platform in new[]{"DefaultTexturePlatform","Standalone"})
            {
                var settings=platform=="DefaultTexturePlatform"?imp.GetDefaultPlatformTextureSettings():imp.GetPlatformTextureSettings(platform);
                settings.textureCompression=TextureImporterCompression.Uncompressed;settings.crunchedCompression=false;imp.SetPlatformTextureSettings(settings);
            }
            imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
