using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Data.Quests;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    // One scoped migration; its frozen input is the released v0.1.0 authoring list.
    public static class StableContentIdentitySetup
    {
        [Serializable] public class Manifest { public string baseCommit; public Skill[] skills; public Quest[] quests; public Alias[] aliases; }
        [Serializable] public class Alias { public string oldId, newId; }
        [Serializable] public class Skill { public string assetPath, skillId; public Effect[] effects; }
        [Serializable] public class Effect { public int index; public string id, shapeJson; public Effect[] children; }
        [Serializable] public class Quest { public string assetPath, questId; public Stage[] stages; }
        [Serializable] public class Stage { public string stageId; public Reward[] rewards; }
        [Serializable] public class Reward { public int index,type,amount; public string id,itemGuid,worldFlagId,worldFlagValue; }
        public static void Apply(string manifestPath, string evidenceDirectory)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest.baseCommit!="89159e69fca45e96dc56a3bf5ff0c7de9ba066a0") throw new InvalidOperationException("Wrong release manifest.");
            Directory.CreateDirectory(evidenceDirectory);
            var evidence = new List<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if(skill.HasStableEffectIds) { SkillEffectIdentity.ExecutionOrder(skill); continue; }
                var prior=manifest.skills.SingleOrDefault(entry=>entry.skillId==skill.SkillId);
                var serialized=new SerializedObject(skill);
                BindEffects(skill,serialized.FindProperty("effects"),prior?.effects, evidence);
                serialized.FindProperty("stableEffectIdsInitialized").boolValue=true;
                Backup(path,evidenceDirectory);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SkillEffectIdentity.ExecutionOrder(skill);
                EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:QuestDefinition",new[]{"Assets/_Project"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var quest=AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if(quest.HasStableRewardIds) { ValidateQuest(quest); continue; }
                var prior=manifest.quests.SingleOrDefault(entry=>entry.questId==quest.Id);
                var serialized=new SerializedObject(quest);
                var stages=serialized.FindProperty("<Stages>k__BackingField");
                for(var i=0;i<quest.Stages.Count;i++)
                {
                    var stage=quest.Stages[i]; var old=prior?.stages.SingleOrDefault(entry=>entry.stageId==stage.StageId);
                    var unused=new List<Reward>(old?.rewards??Array.Empty<Reward>());
                    var rewards=stages.GetArrayElementAtIndex(i).FindPropertyRelative("<CompletionRewards>k__BackingField");
                    for(var j=0;j<stage.CompletionRewards.Count;j++)
                    {
                        var reward=stage.CompletionRewards[j];
                        var itemGuid=reward.ItemDefinition==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reward.ItemDefinition));
                        var candidates=unused.Where(entry=>entry.type==(int)reward.RewardType && entry.itemGuid==itemGuid
                            && entry.worldFlagId==reward.WorldFlagId).OrderBy(entry=>entry.amount==reward.Amount?0:1).ThenBy(entry=>entry.index==j?0:1).ToList();
                        var match=candidates.FirstOrDefault();
                        var id=match?.id??reward.RewardId;
                        if(string.IsNullOrEmpty(id)) id=Guid.NewGuid().ToString("N");
                        rewards.GetArrayElementAtIndex(j).FindPropertyRelative("rewardId").stringValue=id;
                        if(match!=null)unused.Remove(match);
                        evidence.Add($"REWARD {quest.Id}/{stage.StageId}/{j} -> {id}; legacy={match?.index.ToString()??"new"}");
                    }
                }
                serialized.FindProperty("stableRewardIdsInitialized").boolValue=true;
                Backup(path,evidenceDirectory); serialized.ApplyModifiedPropertiesWithoutUndo();
                ValidateQuest(quest); EditorUtility.SetDirty(quest); AssetDatabase.SaveAssetIfDirty(quest);
            }
            File.WriteAllLines(Path.Combine(evidenceDirectory,"authored-identities.txt"),evidence);
        }
        private static void Backup(string assetPath,string directory)
        {
            var destination=Path.Combine(directory,assetPath.Replace('/','_'));
            if(!File.Exists(destination)) File.Copy(assetPath,destination);
        }
        private static int Score(SkillEffectData current, SkillEffectData prior)
        {
            if(current.EffectType!=prior.EffectType) return -1;
            var score=current.TargetModeIndex==prior.TargetModeIndex?100:0;
            score+=current.ModifierStatType==prior.ModifierStatType?20:0;
            score+=current.ModifierVariableType==prior.ModifierVariableType?20:0;
            score+=current.ResistanceType==prior.ResistanceType?20:0;
            score+=current.StatusId==prior.StatusId?30:0;
            score+=current.UniqueBuffType==prior.UniqueBuffType?30:0;
            score+=current.DamageMultiplier==prior.DamageMultiplier?5:0;
            score+=current.ModifierValue==prior.ModifierValue?5:0;
            return score;
        }
        private static void BindEffects(SkillData skill,SerializedProperty effects,Effect[] historical,List<string> evidence)
        {
            var unused=new List<Effect>(historical??Array.Empty<Effect>());
            for(var i=0;i<effects.arraySize;i++)
            {
                var entry=effects.GetArrayElementAtIndex(i);
                var current=JsonUtility.FromJson<SkillEffectData>(entry.boxedValue is SkillEffectData value?JsonUtility.ToJson(value):"{}");
                var candidates=unused.Select(old=>(old,score:Score(current,JsonUtility.FromJson<SkillEffectData>(old.shapeJson))))
                    .Where(pair=>pair.score>=0).OrderByDescending(pair=>pair.score).ThenBy(pair=>pair.old.index==i?0:1).ToList();
                var match=candidates.Count>0?candidates[0].old:null;
                var id=match?.id??current.StableId;
                if(string.IsNullOrEmpty(id)) id=Guid.NewGuid().ToString("N");
                entry.FindPropertyRelative("stableEffectId").stringValue=id;
                if(match!=null)unused.Remove(match);
                evidence.Add($"EFFECT {skill.SkillId}/{entry.propertyPath} -> {id}; legacy={match?.index.ToString()??"new"}");
                BindEffects(skill,entry.FindPropertyRelative("counterattackEffects"),match?.children,evidence);
            }
            // Bind after every ID in this sibling list has been assigned.
            for(var i=0;i<effects.arraySize;i++)
            {
                var conditions=effects.GetArrayElementAtIndex(i).FindPropertyRelative("conditions");
                for(var c=0;c<conditions.arraySize;c++)
                {
                    var condition=conditions.GetArrayElementAtIndex(c);
                    var type=(EffectConditionType)condition.FindPropertyRelative("conditionType").intValue;
                    if(type is not(EffectConditionType.ReferencedEffectSucceeded or EffectConditionType.ReferencedEffectDealtHealthDamage))continue;
                    var index=condition.FindPropertyRelative("referencedEffectIndex").intValue;
                    if(index<0||index>=effects.arraySize) throw new InvalidOperationException("Invalid legacy condition reference: "+skill.SkillId);
                    condition.FindPropertyRelative("referencedEffectId").stringValue=effects.GetArrayElementAtIndex(index).FindPropertyRelative("stableEffectId").stringValue;
                }
            }
        }
        public static void ValidateQuest(QuestDefinition quest)
        {
            var ids=new HashSet<string>();
            foreach(var stage in quest.Stages)
            foreach(var reward in stage.CompletionRewards)
                if(string.IsNullOrWhiteSpace(reward.RewardId)||!ids.Add(reward.RewardId))throw new InvalidOperationException("Missing/duplicate reward identity: "+quest.Id);
        }
    }
}
