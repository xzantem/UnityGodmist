using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        public const string KingPath = Catacombs + "Enemies/CD_Catacombs_dead_king.asset";
        public const string EnragedKingPath = Catacombs + "Enemies/CD_Catacombs_dead_king_enraged.asset";
        public const string KnightPath = Catacombs + "Enemies/CD_Catacombs_spectral_knight.asset";
        static void KingStatus(SerializedObject data, string id, float chance, int turns, float damage = 0)
        {
            var effect = Effect(data, EffectType.ApplyStatus);
            Raw(effect,"statusId",id); Raw(effect,"baseChance",chance); Raw(effect,"durationTurns",turns);
            Raw(effect,"statusDamageMultiplier",damage);
        }
        static SkillData KingSave(SerializedObject data) { Save(data); return (SkillData)data.targetObject; }
        static void KingSkills(CharacterDefinition definition, params SkillData[] skills)
        {
            var data = new SerializedObject(definition);
            var list = data.FindProperty(F("StartingSkills")); list.ClearArray();
            foreach(var skill in skills) Add(list).objectReferenceValue=skill;
            Field(data,"BasicAttackSkill",skills[0]);
            var tags = data.FindProperty(F("Tags"));
            bool giant=false; for(var i=0;i<tags.arraySize;i++) giant |= tags.GetArrayElementAtIndex(i).enumValueIndex==(int)CharacterTag.Giant;
            if(!giant && definition.Id != DeadKingBossSystem.KnightId) Add(tags).enumValueIndex=(int)CharacterTag.Giant;
            Save(data);
        }
        [MenuItem("Godmist/Content/Bosses 2.0/Configure Dead King")]
        public static void ConfigureKingV2()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var boss=Require<CharacterDefinition>(KingPath);
            var phase=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(EnragedKingPath);
            if(phase==null) {phase=GetOrCreate<CharacterDefinition>(EnragedKingPath);EditorUtility.CopySerialized(boss,phase);}
            var knight=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(KnightPath);
            if(knight==null) {knight=GetOrCreate<CharacterDefinition>(KnightPath);EditorUtility.CopySerialized(Require<CharacterDefinition>(Catacombs+"Enemies/CD_Catacombs_swordsman.asset"),knight);}
            var data=Skill("SD_Catacombs_dead_king_wave","skill.enemy.catacombs.dead_king_wave","Widmowa Fala","",5,60,AttackType.Ranged);
            Mode(data,TargetType.AreaAroundCaster,TargetingType.Enemies,RangeShape.Line,6);
            var effect=Effect(data,EffectType.DealDamage);Raw(effect,"damageType",DamageType.Magical);Raw(effect,"damageValueSource",DamageValueSource.AttackRange);Raw(effect,"alwaysHits",true);
            effect=Effect(data,EffectType.ModifyStat);Raw(effect,"modifierStatType",CharacterStatType.Speed);Raw(effect,"modifierValue",-10f);Raw(effect,"durationTurns",2);Raw(effect,"baseChance",.8f);Raw(effect,"polarity",EffectPolarity.Negative);
            var wave=KingSave(data);
            data=Skill("SD_Catacombs_dead_king_command",DeadKingBossSystem.CommandId,"Rozkaz Króla","Wrogowie skupiają się na oznaczonym celu i zadają mu o 20% więcej obrażeń. Tylko jeden rozkaz może być aktywny.",20,30,AttackType.Unknown);
            Mode(data,TargetType.Single,TargetingType.Enemies,RangeShape.Radial,9);
            effect=Effect(data,EffectType.ModifyVar);Raw(effect,"modifierVariableType",GameplayVariableType.DamageDealtPercent);Raw(effect,"modifierValue",-.5f);Raw(effect,"modifierOperation",StatModifierOperation.FlatBase);Raw(effect,"durationTurns",3);Raw(effect,"polarity",EffectPolarity.Negative);
            var command=KingSave(data);
            data=Skill("SD_Catacombs_spectral_knight_strike","skill.enemy.catacombs.spectral_knight_strike","Widmowe Cięcie","",0,60,AttackType.Melee);
            Mode(data,TargetType.Single,TargetingType.Enemies,RangeShape.Radial,1);
            effect=Effect(data,EffectType.DealDamage);Raw(effect,"damageType",DamageType.Physical);Raw(effect,"damageValueSource",DamageValueSource.AttackRange);
            KingStatus(data,StatusEffectIds.Stun,.2f,1);
            var hit = Add(data.FindProperty("effects").GetArrayElementAtIndex(1).FindPropertyRelative("conditions"));
            Raw(hit,"conditionType",EffectConditionType.ReferencedEffectSucceeded);Raw(hit,"referencedEffectIndex",0);
            var strike=KingSave(data);
            var kd=new SerializedObject(knight);Field(kd,"Id",DeadKingBossSystem.KnightId);Field(kd,"DisplayName","Widmowy Rycerz");
            Sub(kd.FindProperty(F("BaseStats")),"MaxHealth",3);Sub(kd.FindProperty(F("GrowthData")),"MaxHealth",0f);
            var knightTags = kd.FindProperty(F("Tags"));
            for(var i=knightTags.arraySize-1;i>=0;i--) if(knightTags.GetArrayElementAtIndex(i).enumValueIndex==(int)CharacterTag.Giant) knightTags.DeleteArrayElementAtIndex(i);
            Field(kd,"LootTable",null);Save(kd);KingSkills(knight,strike);
            data=Skill("SD_Catacombs_dead_king_guard",CatacombsBossSystem.GuardSkillId,"Straż!","Przywołuje do 3 Widmowych Rycerzy. Każdy ginie po 3 trafieniach; obrażenia okresowe nie liczą się.",60,30,AttackType.Unknown);
            Mode(data,TargetType.Caster,TargetingType.Allies,RangeShape.Radial,0);
            effect=Effect(data,EffectType.Summon);Raw(effect,"summonedCharacterDefinition",knight);Raw(effect,"summonedClassData",knight.ClassData);
            Raw(effect,"summonDurationTurns",99);Raw(effect,"maxActiveSummons",3);Raw(effect,"summonPercentMaxHealth",1f);Raw(effect,"summonPersistent",true);
            var range=effect.FindPropertyRelative("summonPlacementRange");Raw(range,"shape",RangeShape.Radial);Raw(range,"minRange",1);Raw(range,"maxRange",9);
            var guard=KingSave(data);
            data=Skill("SD_Catacombs_dead_king_blade",DeadKingBossSystem.BladeId,"Widmowe Ostrze","Teleportuje do celu przed uderzeniem.",0,60,AttackType.Ranged);
            Mode(data,TargetType.Single,TargetingType.Enemies,RangeShape.Radial,6);
            effect=Effect(data,EffectType.DealDamage);Raw(effect,"damageType",DamageType.Magical);Raw(effect,"damageValueSource",DamageValueSource.AttackRange);Raw(effect,"alwaysHits",true);Raw(effect,"bonusPenetration",.4f);
            effect=Effect(data,EffectType.ModifyVar);Raw(effect,"modifierVariableType",GameplayVariableType.DamageReceivedPercent);Raw(effect,"modifierValue",.5f);Raw(effect,"baseChance",.3f);Raw(effect,"durationTurns",2);Raw(effect,"polarity",EffectPolarity.Negative);
            var blade=KingSave(data);
            data=Skill("SD_Catacombs_dead_king_scream","skill.enemy.catacombs.dead_king_scream","Krzyk Cienia","",25,30,AttackType.Ranged);
            Mode(data,TargetType.All,TargetingType.Enemies,RangeShape.Radial,0);KingStatus(data,StatusEffectIds.Bleed,.5f,2,1f);KingStatus(data,StatusEffectIds.Stun,.5f,2);
            var scream=KingSave(data);
            data=Skill("SD_Catacombs_dead_king_shadow",DeadKingBossSystem.ShadowId,"Cień Destrukcji","Pozostawia 10 bezpiecznych pól. Po 2 turach uderza trzykrotnie poza nimi, po czym ogłusza Króla na 2 tury.",60,100,AttackType.Unknown);
            Mode(data,TargetType.Caster,TargetingType.Allies,RangeShape.Radial,0);Effect(data,EffectType.ModifyActionValue);
            var shadow=KingSave(data);
            KingSkills(boss,wave,command,guard);KingSkills(phase,blade,scream,shadow);
            var phaseData=new SerializedObject(phase);Field(phaseData,"Id",DeadKingBossSystem.PhaseTwoId);Field(phaseData,"DisplayName","Duch Umarłego Króla");Field(phaseData,"SecondPhaseDefinition",null);
            Field(phaseData,"SanctuaryWarning",KingMarker("Safe","Bezpieczne pole",new Color32(140,180,195,85)));
            Field(phaseData,"SanctuaryFire",KingMarker("Dark","Cień Destrukcji",new Color32(8,4,18,200)));Save(phaseData);
            var bossData=new SerializedObject(boss);Field(bossData,"SecondPhaseDefinition",phase);Save(bossData);
            var catalog=new SerializedObject(Require<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset"));
            AddDistinct(catalog.FindProperty(F("CharacterDefinitions")),phase);AddDistinct(catalog.FindProperty(F("CharacterDefinitions")),knight);
            foreach(var skill in new[]{wave,command,guard,strike,blade,scream,shadow})AddDistinct(catalog.FindProperty(F("SkillDefinitions")),skill);
            Save(catalog);AssetDatabase.SaveAssets();
        }
        static SummonedObjectDefinition KingMarker(string suffix,string label,Color32 tint)
        {
            var path=Catacombs+"Objects/DeadKing"+suffix+".png";
            if(!System.IO.File.Exists(path))
            {
                var texture=new Texture2D(32,32);var colors=new Color32[1024];for(var i=0;i<colors.Length;i++)colors[i]=tint;
                texture.SetPixels32(colors);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            var marker=GetOrCreate<SummonedObjectDefinition>(Catacombs+"Objects/SO_DeadKing"+suffix+".asset");var data=new SerializedObject(marker);
            Set(data.FindProperty("objectId"),"object.catacombs.dead_king."+suffix.ToLowerInvariant());Set(data.FindProperty("displayName"),label);
            Set(data.FindProperty("worldSprite"),AssetDatabase.LoadAssetAtPath<Sprite>(path));Set(data.FindProperty("blocksMovement"),false);Set(data.FindProperty("blocksLineOfSight"),false);data.FindProperty("effects").ClearArray();Save(data);return marker;
        }
    }
}
