using System;
using System.IO;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        public const string SlayerPath=Catacombs+"Enemies/CD_Catacombs_skeleton_slayer.asset";
        public const string SlayerPhasePath=Catacombs+"Enemies/CD_Catacombs_skeleton_slayer_enraged.asset";
        public const string SlayerEncounterPath=Catacombs+"Encounters/ED_Catacombs_skeleton_slayer.asset";
        static SerializedObject SlayerSkill(string key,string id,string title,int mp,int ap,AttackType attack,float accuracy=100)
        {
            var data=Skill("SD_Catacombs_skeleton_slayer_"+key,id,title,"",mp,ap,attack);
            Field(data,"SkillAccuracy",accuracy);return data;
        }
        static void SlayerHitCondition(SerializedProperty effect)
        {
            var c=Add(effect.FindPropertyRelative("conditions"));Raw(c,"conditionType",EffectConditionType.ReferencedEffectSucceeded);Raw(c,"referencedEffectIndex",0);
        }
        static void SlayerDamage(SerializedObject data,float penetration=0)
        {
            var e=Effect(data,EffectType.DealDamage);Raw(e,"damageType",DamageType.Physical);
            Raw(e,"damageValueSource",DamageValueSource.AttackRange);Raw(e,"bonusPenetration",penetration);Raw(e,"alwaysHits",false);
        }
        static void SlayerStun(SerializedObject data,float chance,int turns)
        {
            KingStatus(data,StatusEffectIds.Stun,chance,turns);
            SlayerHitCondition(data.FindProperty("effects").GetArrayElementAtIndex(data.FindProperty("effects").arraySize-1));
        }
        [MenuItem("Godmist/Content/Bosses 2.0/Configure Skeleton Slayer")]
        public static void ConfigureSkeletonSlayer()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var boss=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(SlayerPath);
            if(boss==null){boss=GetOrCreate<CharacterDefinition>(SlayerPath);EditorUtility.CopySerialized(Require<CharacterDefinition>(KingPath),boss);}
            var phase=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(SlayerPhasePath);
            if(phase==null){phase=GetOrCreate<CharacterDefinition>(SlayerPhasePath);EditorUtility.CopySerialized(boss,phase);}
            foreach(var definition in new[]{boss,phase})
            {
                var d=new SerializedObject(definition);Field(d,"Id",definition==boss?SkeletonSlayerBossSystem.BossId:SkeletonSlayerBossSystem.PhaseTwoId);
                Field(d,"DisplayName","Szkielet Zabójca");Field(d,"LootTable",null);Field(d,"SecondPhaseDefinition",null);
                Field(d,"SanctuaryFire",null);Field(d,"SanctuaryWarning",null);
                d.FindProperty(F("TransformationFrames")).ClearArray();
                var tags=d.FindProperty(F("Tags"));tags.ClearArray();Add(tags).enumValueIndex=(int)CharacterTag.Undead;Add(tags).enumValueIndex=(int)CharacterTag.Giant;
                var b=d.FindProperty(F("BaseStats"));var g=d.FindProperty(F("GrowthData"));
                // Przeciwnicy NPC.xlsx, Statystyki row9 (same growth convention as the King).
                foreach(var v in new[]{("MaxHealth",1250f,82f),
                    ("Dodge",15f,.1125f),("PhysicalDefense",22f,4.4f),("MagicalDefense",10f,2f),("CritChance",.13f,.000325f)})
                {
                    if(v.Item1 == "MaxHealth")Sub(b,v.Item1,Mathf.RoundToInt(v.Item2));
                    else Sub(b,v.Item1,v.Item2);
                    Sub(g,v.Item1,v.Item3);
                }
                Sub(b,"CritDamage",2f);Sub(b,"Speed",80f);
                var res=d.FindProperty(F("BaseResistances"));Sub(res,"BleedResistance",1f);Sub(res,"PoisonResistance",.4f);Sub(res,"FireResistance",.5f);Sub(res,"SuppressionResistance",.6f);
                Save(d);
            }
            var d1=SlayerSkill("crushing",SkeletonSlayerBossSystem.CrushingId,"Miażdżący Cios",0,60,AttackType.Melee,81);
            Mode(d1,TargetType.AreaAroundCaster,TargetingType.Enemies,RangeShape.Rectangle,1,3);SlayerDamage(d1,.2f);SlayerStun(d1,.8f,1);var crushing=KingSave(d1);
            d1=SlayerSkill("tremors",SkeletonSlayerBossSystem.TremorsId,"Wstrząsy",35,65,AttackType.Ranged);
            Mode(d1,TargetType.AreaAroundCaster,TargetingType.Enemies,RangeShape.Radial,2);SlayerDamage(d1);
            var e=Effect(d1,EffectType.Push);Raw(e,"baseChance",.8f);Raw(e,"displacementDistance",1);SlayerHitCondition(e);
            e=Effect(d1,EffectType.ModifyStat);Raw(e,"modifierStatType",CharacterStatType.WeaponAccuracy);Raw(e,"modifierValue",-20f);
            Raw(e,"modifierOperation",StatModifierOperation.FlatBase);Raw(e,"baseChance",.8f);Raw(e,"durationTurns",2);Raw(e,"polarity",EffectPolarity.Negative);SlayerHitCondition(e);var tremors=KingSave(d1);
            d1=SlayerSkill("defense",SkeletonSlayerBossSystem.DefenseId,"Nieugięta Obrona",50,80,AttackType.Unknown);
            Mode(d1,TargetType.Caster,TargetingType.Allies,RangeShape.Radial,0);Effect(d1,EffectType.ModifyActionValue);
            Field(d1,"TooltipDescription","Przez 2 tury otrzymuje o 50% mniej obrażeń. Następny atak zyskuje przyjęte obrażenia przed mitygacją; brak obrażeń powoduje ogłuszenie na 2 tury.");var defense=KingSave(d1);
            d1=SlayerSkill("bone_crush",SkeletonSlayerBossSystem.BoneCrushId,"Kościany Rozgrom",0,60,AttackType.Melee,81);
            Mode(d1,TargetType.AreaAroundTile,TargetingType.Enemies,RangeShape.Radial,2);
            var relative=d1.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("relativeRange");
            Raw(relative,"shape",RangeShape.Square);Raw(relative,"minRange",0);Raw(relative,"maxRange",1);
            SlayerDamage(d1,.2f);SlayerStun(d1,.8f,1);
            e=Effect(d1,EffectType.ModifyVar);Raw(e,"modifierVariableType",GameplayVariableType.DamageDealtPercent);Raw(e,"modifierOperation",StatModifierOperation.FlatBase);
            Raw(e,"modifierValue",-.2f);Raw(e,"baseChance",.4f);Raw(e,"durationTurns",2);Raw(e,"polarity",EffectPolarity.Negative);SlayerHitCondition(e);var crush=KingSave(d1);
            d1=SlayerSkill("hammer",SkeletonSlayerBossSystem.HammerId,"Młot Zniszczenia",0,60,AttackType.Ranged);
            Mode(d1,TargetType.Single,TargetingType.Enemies,RangeShape.Radial,2);KingStatus(d1,StatusEffectIds.Immobilize,1f,2);
            Field(d1,"TooltipDescription","Unieruchamia cel na 2 tury, tworzy Pole Impetu 3×3 i blokuje ruch rzucającego. Po 2 turach: 5 trafień ignorujących obronę, celność 85. Otrzymanie 10% maks. HP obrażeń przerywa przygotowanie.");var hammer=KingSave(d1);
            // Canonical cost; the standard Rage profile halves this to 50.
            d1=SlayerSkill("task",SkeletonSlayerBossSystem.TaskId,"Wykonać Zadanie",100,0,AttackType.Unknown);
            Mode(d1,TargetType.Caster,TargetingType.Allies,RangeShape.Radial,0);Effect(d1,EffectType.ModifyActionValue);
            Field(d1,"TooltipDescription","Przyspiesza następną turę o 100%.");var task=KingSave(d1);
            KingSkills(boss,crushing,tremors,defense);KingSkills(phase,crush,hammer,task);
            var phaseData=new SerializedObject(phase);
            var marker=GetOrCreate<SummonedObjectDefinition>(Catacombs+"Objects/SO_SkeletonSlayerMomentum.asset");
            var md=new SerializedObject(marker);Set(md.FindProperty("objectId"),"object.catacombs.skeleton_slayer.momentum");
            Set(md.FindProperty("displayName"),"Pole Impetu");Set(md.FindProperty("blocksMovement"),false);Set(md.FindProperty("blocksLineOfSight"),false);md.FindProperty("effects").ClearArray();Save(md);
            Field(phaseData,"SanctuaryWarning",marker);Save(phaseData);
            var bossData=new SerializedObject(boss);Field(bossData,"SecondPhaseDefinition",phase);Save(bossData);
            var encounter=GetOrCreate<EncounterDefinition>(SlayerEncounterPath);var ed=new SerializedObject(encounter);
            Field(ed,"Id","encounter.catacombs.skeleton_slayer");Field(ed,"DisplayName","Szkielet Zabójca");Field(ed,"GridWidth",15);Field(ed,"GridHeight",15);Field(ed,"BossEnemyIndex",0);
            var enemies=ed.FindProperty(F("Enemies"));enemies.ClearArray();var enemy=Add(enemies);
            Sub(enemy,"InstanceId",SkeletonSlayerBossSystem.BossId+".boss");Sub(enemy,"CharacterDefinition",boss);Sub(enemy,"ClassData",boss.ClassData);
            enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue=new Vector2Int(7,10);Sub(enemy,"VisionRange",9f);Sub(enemy,"VisionAngle",360f);
            enemy.FindPropertyRelative(F("InitialFacing")).vector2Value=Vector2.down;Save(ed);
            var catalog=new SerializedObject(Require<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset"));
            foreach(var c in new[]{boss,phase})AddDistinct(catalog.FindProperty(F("CharacterDefinitions")),c);
            AddDistinct(catalog.FindProperty(F("EncounterDefinitions")),encounter);
            foreach(var skill in new[]{crushing,tremors,defense,crush,hammer,task})AddDistinct(catalog.FindProperty(F("SkillDefinitions")),skill);Save(catalog);
            DeadKingArtSetup.Configure(JsonUtility.FromJson<DeadKingArtSetup.Config>(File.ReadAllText("Assets/_Project/Scripts/Editor/SkeletonSlayerArtBindings.json")));
            ConfigureSkeletonSlayerExpedition();
            AssetDatabase.SaveAssets();Debug.Log("SKELETON_SLAYER_CONFIGURED");
        }
    }
}
