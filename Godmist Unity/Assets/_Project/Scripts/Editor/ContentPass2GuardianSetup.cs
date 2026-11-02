using System;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Core.Enums;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ContentPass2GuardianSetup
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        [Serializable] class Plan { public Entry[] entries; }
        [Serializable] class Active { public int impet,ap_percent; }
        [Serializable] class Entry
        {
            public string slot,id,name,rules;
            public int ranks,column;
            public string[] requirements,descriptions;
            public Active active;
        }
        static string F(string name)=>"<"+name+">k__BackingField";
        static void Text(SerializedProperty owner,string name,string value)=>owner.FindPropertyRelative(F(name)).stringValue=value;
        [MenuItem("Tools/Godmist/Content Pass 2/Apply Guardian T1-T5")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ContentPass2/guardian-content.json")));
            if(plan?.entries?.Length!=25 || plan.entries.Select(n=>n.id).Distinct().Count()!=25 || plan.entries.Sum(n=>n.ranks)!=44)
                throw new InvalidOperationException("Expected 25 Guardian T1-T5 nodes.");
            // JsonUtility creates a default Active for JSON null on some Unity versions;
            // costs are also checked when identifying the three production active nodes.
            bool IsActive(Entry n)=>n.active!=null && n.active.impet>0;
            if(plan.entries.Count(IsActive)!=3 || plan.entries.Any(n=>n.descriptions?.Length!=n.ranks || n.requirements.Any(req=>!plan.entries.Any(x=>x.slot==req))))
                throw new InvalidOperationException("Invalid active budget, rank copy or prerequisites.");
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Root+"Skills/SkillTrees/ST_Scout_Guardian.asset");
            var scout=AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_Scout.asset");
            if(tree==null || scout==null) throw new InvalidOperationException("Missing existing Guardian tree or Scout class asset.");
            var so=new SerializedObject(tree);
            so.FindProperty(F("Id")).stringValue="tree.scout.guardian";
            so.FindProperty(F("DisplayName")).stringValue="Strażnik";
            var global=so.FindProperty(F("GlobalPassive"));
            Text(global,"DisplayName","Pasyw drzewka");
            Text(global,"Description","Za punkt: +0,5% obrony fizycznej i magicznej.");
            global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var nodes=so.FindProperty(F("Nodes"));nodes.ClearArray();nodes.arraySize=plan.entries.Length;
            for(var i=0;i<plan.entries.Length;i++)
            {
                var entry=plan.entries[i];var node=nodes.GetArrayElementAtIndex(i);bool active=IsActive(entry);
                Text(node,"Id",entry.id);Text(node,"DisplayName",entry.name);Text(node,"Description",entry.descriptions[0]);
                node.FindPropertyRelative(F("Tier")).intValue=entry.slot[1]-'0';
                node.FindPropertyRelative(F("Column")).intValue=entry.column>0 ? entry.column : entry.slot[3]-'0';
                node.FindPropertyRelative(F("MaxRanks")).intValue=entry.ranks;
                var icon=ImportIcon("Assets/_Project/Art/Icons/Passives/VS22/ScoutGuardian/"+entry.slot+"-32.png");
                var skill=active?ConfigureSkill(entry,icon):null;
                node.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=skill;
                node.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                node.FindPropertyRelative(F("Icon")).objectReferenceValue=icon;
                var requirements=node.FindPropertyRelative(F("Requirements"));requirements.ClearArray();requirements.arraySize=entry.requirements.Length;
                for(var j=0;j<entry.requirements.Length;j++)
                {
                    Text(requirements.GetArrayElementAtIndex(j),"NodeId","node.scout.guardian."+entry.requirements[j]);
                    requirements.GetArrayElementAtIndex(j).FindPropertyRelative(F("MinimumRanks")).intValue=1;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(tree);
            var classSo=new SerializedObject(scout);var trees=classSo.FindProperty(F("SkillTrees"));
            if(!Enumerable.Range(0,trees.arraySize).Any(i=>trees.GetArrayElementAtIndex(i).objectReferenceValue==tree))
            {var index=trees.arraySize;trees.arraySize++;trees.GetArrayElementAtIndex(index).objectReferenceValue=tree;}
            classSo.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(scout);
        }

        static SkillData ConfigureSkill(Entry entry,Sprite icon)
        {
            var path=Root+"Skills/SD_ScoutGuardian_"+entry.slot+".asset";
            var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<SkillData>();AssetDatabase.CreateAsset(skill,path);}
            var so=new SerializedObject(skill);
            void Field(string name,string value)=>so.FindProperty(F(name)).stringValue=value;
            void Number(string name,int value)=>so.FindProperty(F(name)).intValue=value;
            Field("SkillId","skill.scout.guardian."+entry.slot);Field("DisplayName",entry.name);Field("TooltipDescription",entry.descriptions[0]);
            so.FindProperty(F("Icon")).objectReferenceValue=icon;Number("ActionCost",entry.active.ap_percent);Number("ResourceCost",entry.active.impet);
            Number("ResourceType",(int)ResourceType.Momentum);so.FindProperty(F("SkillAccuracy")).floatValue=100;
            Number("AttackType",(int)AttackType.Unknown);
            var counter=entry.slot=="t5_4";var taunt=entry.slot=="t1_4";
            so.FindProperty(F("RequiresLineOfSight")).boolValue=!counter;
            var modes=so.FindProperty("targetModes");modes.ClearArray();modes.arraySize=1;var mode=modes.GetArrayElementAtIndex(0);
            mode.FindPropertyRelative("targetType").intValue=(int)(counter?TargetType.Caster:TargetType.Single);
            mode.FindPropertyRelative("targetingType").intValue=(int)(taunt?TargetingType.Enemies:TargetingType.Allies);
            mode.FindPropertyRelative("includesCaster").boolValue=!taunt;
            var range=mode.FindPropertyRelative("absoluteRange");range.FindPropertyRelative("shape").intValue=(int)RangeShape.Radial;
            range.FindPropertyRelative("minRange").intValue=taunt?1:0;range.FindPropertyRelative("maxRange").intValue=counter?0:4;
            var effects=so.FindProperty("effects");effects.ClearArray();effects.arraySize=1;var effect=effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("stableEffectId").stringValue="main";
            effect.FindPropertyRelative("effectType").intValue=(int)(counter?EffectType.GuardianCounter:taunt?EffectType.ApplyStatus:EffectType.Shield);
            effect.FindPropertyRelative("polarity").intValue=(int)(taunt?EffectPolarity.Negative:EffectPolarity.Positive);
            effect.FindPropertyRelative("alwaysHits").boolValue=true;effect.FindPropertyRelative("baseChance").floatValue=taunt?.9f:1f;
            effect.FindPropertyRelative("durationTurns").intValue=counter?2:3;
            if(taunt)effect.FindPropertyRelative("statusId").stringValue="taunt";
            if(!counter&&!taunt)effect.FindPropertyRelative("shieldMaxHealthPercent").floatValue=.15f;
            so.FindProperty("stableEffectIdsInitialized").boolValue=true;
            ContextualSkillHitSetup.Apply(so);so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(skill);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var cat=new SerializedObject(catalog);var skills=cat.FindProperty(F("SkillDefinitions"));
            if(!Enumerable.Range(0,skills.arraySize).Any(i=>skills.GetArrayElementAtIndex(i).objectReferenceValue==skill))
            {var index=skills.arraySize;skills.arraySize++;skills.GetArrayElementAtIndex(index).objectReferenceValue=skill;}
            cat.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(catalog);return skill;
        }
        static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;
            var settings=importer.GetDefaultPlatformTextureSettings();settings.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
