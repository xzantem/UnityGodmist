using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        public const string BanditRoot=Root+"Dungeons/Forest/";
        public const string BanditPath=BanditRoot+"CD_Forest_BanditLeader.asset", BanditPhasePath=BanditRoot+"CD_Forest_BanditLeaderRevealed.asset";
        public const string BanditEncounterPath=BanditRoot+"ED_Forest_BanditLeader.asset";
        public const string BanditDungeonPath=BanditRoot+"DD_Forest_BanditLeaderExpedition.asset";
        public const string BanditBagPath=BanditRoot+"Loot/ID_Forest_BanditLeaderBag.asset";
        static SerializedObject BanditSkill(string key,string name,int mp,int ap,AttackType attack)
        {
            var d=new SerializedObject(GetOrCreate<SkillData>(BanditRoot+"SD_Forest_BanditLeader_"+key+".asset"));
            Field(d,"SkillId",BanditLeaderBossSystem.Prefix+key);Field(d,"DisplayName",name);Field(d,"ResourceType",ResourceType.Mana);
            Field(d,"ResourceCost",mp);Field(d,"ActionCost",ap);Field(d,"AttackType",attack);Field(d,"SkillAccuracy",100f);
            Field(d,"RequiresLineOfSight",false);d.FindProperty("effects").ClearArray();return d;
        }
        static SummonedObjectDefinition BanditTrap(string key,bool mine)
        {
            var trap=GetOrCreate<SummonedObjectDefinition>(BanditRoot+"SO_BanditLeader_"+key+".asset");var d=new SerializedObject(trap);
            Set(d.FindProperty("objectId"),"object.forest.bandit_leader."+key);Set(d.FindProperty("displayName"),mine?"Bandycka Mina":"Pułapka na Niedźwiedzie");
            Set(d.FindProperty("triggersOnEntry"),true);Set(d.FindProperty("entryAffectsArea"),true);Set(d.FindProperty("hiddenUntilTriggered"),true);
            Set(d.FindProperty("blocksMovement"),false);Set(d.FindProperty("blocksLineOfSight"),false);Set(d.FindProperty("effectTargetingType"),TargetingType.Enemies);
            var range=d.FindProperty("effectRange");Raw(range,"shape",RangeShape.Square);Raw(range,"minRange",0);Raw(range,"maxRange",1);
            var effects=d.FindProperty("effects");effects.ClearArray();var effect=Add(effects);
            Raw(effect,"effectType",mine?SummonedObjectEffectType.DealDamage:SummonedObjectEffectType.ApplyStatus);
            Raw(effect,"damageType",DamageType.Physical);Raw(effect,"damageValueSource",DamageValueSource.AttackRange);Raw(effect,"damageMultiplier",2f);
            Raw(effect,"statusId",StatusEffectIds.Immobilize);Raw(effect,"baseChance",1f);Raw(effect,"durationTurns",2);Raw(effect,"stacks",1);
            Save(d);return trap;
        }
        [MenuItem("Godmist/Content/Bosses 2.0/Configure Bandit Leader")]
        public static void ConfigureBanditLeader()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var trap=BanditTrap("bear",false);var mine=BanditTrap("mine",true);
            var skills=new SkillData[6];
            for(var i=0;i<2;i++)
            {
                var d=BanditSkill(i==0?"bear":"mine",i==0?"Pułapka na Niedźwiedzie":"Bandycka Mina",0,40,AttackType.Unknown);
                Mode(d,TargetType.Caster,TargetingType.Allies,RangeShape.Square,0);
                var e=Effect(d,EffectType.CreateObject);Raw(e,"summonedObjectDefinition",i==0?trap:mine);Raw(e,"maxActiveObjects",int.MaxValue);
                var range=e.FindPropertyRelative("objectPlacementRange");Raw(range,"shape",RangeShape.Square);Raw(range,"minRange",1);Raw(range,"maxRange",2);
                Field(d,"TooltipDescription",i==0?"Ukryta pułapka. Wejście wroga: unieruchomienie na 2 tury w obszarze 3×3.":"Ukryta mina. Wejście wroga: 2× fizycznych obrażeń w obszarze 3×3.");
                skills[i]=KingSave(d);
            }
            var order=BanditSkill("order","Rozkaz Wzmacniający",0,40,AttackType.Unknown);
            Mode(order,TargetType.Single,TargetingType.Allies,RangeShape.Square,9);
            var effect=Effect(order,EffectType.ModifyVar);Raw(effect,"modifierVariableType",GameplayVariableType.DamageDealtPercent);Raw(effect,"modifierValue",.2f);Raw(effect,"durationTurns",2);
            effect=Effect(order,EffectType.ModifyStat);Raw(effect,"modifierStatType",CharacterStatType.Speed);Raw(effect,"modifierOperation",StatModifierOperation.FlatBase);Raw(effect,"modifierValue",10f);Raw(effect,"durationTurns",2);
            effect=Effect(order,EffectType.ModifyActionValue);Raw(effect,"actionValuePercent",.5f);
            skills[2]=KingSave(order);
            var cut=BanditSkill("cut","Precyzyjne Cięcie",0,55,AttackType.Melee);
            Field(cut,"SkillAccuracy",90f);Mode(cut,TargetType.Single,TargetingType.Enemies,RangeShape.Square,1);SlayerDamage(cut,.25f);
            Field(cut,"TooltipDescription","25% penetracji; 50% szansy na jedno bezpłatne powtórzenie z 70% obrażeń.");skills[3]=KingSave(cut);
            var fire=BanditSkill("fire","Ogniowa Dysza",40,35,AttackType.Ranged);
            Mode(fire,TargetType.AreaAroundCaster,TargetingType.Enemies,RangeShape.Cone,3);
            effect=Effect(fire,EffectType.DealDamage);Raw(effect,"damageType",DamageType.Magical);Raw(effect,"damageValueSource",DamageValueSource.AttackRange);Raw(effect,"onHitStatusId",StatusEffectIds.Burn);
            Raw(effect,"onHitStatusChance",.8f);Raw(effect,"onHitStatusDurationTurns",3);Raw(effect,"statusDamageMultiplier",.6f);
            skills[4]=KingSave(fire);
            var strike=BanditSkill("strike","Ukryte Przebicie",40,100,AttackType.Ranged);
            Field(strike,"RequiredCasterStatusId",StatusEffectIds.Invisibility);Field(strike,"GuaranteedCritical",true);
            Mode(strike,TargetType.Single,TargetingType.Enemies,RangeShape.Square,6);SlayerDamage(strike);
            effect=strike.FindProperty("effects").GetArrayElementAtIndex(0);Raw(effect,"alwaysHits",true);Raw(effect,"damageMultiplier",5f);
            Field(strike,"TooltipDescription","Jeden cios ×5, zawsze trafia krytycznie. Każdy przyległy sojusznik celu zmniejsza obrażenia o 30%, maksymalnie o 90%.");skills[5]=KingSave(strike);
            var presentation=Require<CharacterDefinition>(BanditRoot+"CD_Forest_BanditThug.asset");
            var boss=GetOrCreate<CharacterDefinition>(BanditPath);var phase=GetOrCreate<CharacterDefinition>(BanditPhasePath);
            foreach(var definition in new[]{boss,phase})
            {
                if(definition.VisualSprite==null)EditorUtility.CopySerialized(presentation,definition);
                var d=new SerializedObject(definition);Field(d,"Id",definition==boss?BanditLeaderBossSystem.BossId:BanditLeaderBossSystem.PhaseTwoId);
                Field(d,"DisplayName","Przywódca Bandytów");Field(d,"LootTable",null);Field(d,"SecondPhaseDefinition",null);
                Field(d,"AiRole",definition==boss?CombatAiRole.Support:CombatAiRole.Dps);
                var b=d.FindProperty(F("BaseStats"));var g=d.FindProperty(F("GrowthData"));
                foreach(var value in new[]{("MaxHealth",500f,33f),("MinAttack",65f,4.23f),("MaxAttack",90f,5.85f),("Dodge",25f,.1875f),("PhysicalDefense",15f,3f),("MagicalDefense",12f,2.4f),("CritChance",.12f,.0003f)})
                {
                    if(value.Item1 is "MaxHealth" or "MinAttack" or "MaxAttack")Sub(b,value.Item1,Mathf.RoundToInt(value.Item2));else Sub(b,value.Item1,value.Item2);
                    Sub(g,value.Item1,value.Item3);
                }
                Sub(b,"CritDamage",2f);Sub(b,"Speed",80f);Sub(b,"MaxResource",100);Sub(b,"ResourceRegeneration",15);
                var res=d.FindProperty(F("BaseResistances"));Sub(res,"BleedResistance",.3f);Sub(res,"PoisonResistance",.5f);Sub(res,"FireResistance",.4f);Sub(res,"SuppressionResistance",.5f);Save(d);
            }
            KingSkills(boss,skills.Take(3).ToArray());KingSkills(phase,skills.Skip(3).ToArray());var bd=new SerializedObject(boss);Field(bd,"SecondPhaseDefinition",phase);Save(bd);
            var encounter=GetOrCreate<EncounterDefinition>(BanditEncounterPath);var ed=new SerializedObject(encounter);
            Field(ed,"Id","encounter.forest.bandit_leader");Field(ed,"DisplayName","Przywódca Bandytów i obstawa");Field(ed,"GridWidth",16);Field(ed,"GridHeight",16);Field(ed,"BossEnemyIndex",0);
            var enemies=ed.FindProperty(F("Enemies"));enemies.ClearArray();
            var units=new[]{boss,presentation,presentation,Require<CharacterDefinition>(BanditRoot+"CD_Forest_BanditShooter.asset")};
            var positions=new[]{new Vector2Int(8,11),new Vector2Int(6,8),new Vector2Int(10,8),new Vector2Int(8,9)};
            for(var i=0;i<units.Length;i++)
            {
                var enemy=Add(enemies);Sub(enemy,"InstanceId","enemy.forest.bandit_leader."+i);Sub(enemy,"CharacterDefinition",units[i]);Sub(enemy,"ClassData",units[i].ClassData);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue=positions[i];Sub(enemy,"VisionRange",9f);Sub(enemy,"VisionAngle",360f);enemy.FindPropertyRelative(F("InitialFacing")).vector2Value=Vector2.down;
            }
            Save(ed);
            var catalog=new SerializedObject(Require<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset"));
            foreach(var unit in new[]{boss,phase})AddDistinct(catalog.FindProperty(F("CharacterDefinitions")),unit);
            foreach(var skill in skills)AddDistinct(catalog.FindProperty(F("SkillDefinitions")),skill);
            AddDistinct(catalog.FindProperty(F("EncounterDefinitions")),encounter);Save(catalog);
            ConfigureBanditLeaderLoot();AssetDatabase.SaveAssets();Debug.Log("BANDIT_LEADER_CONFIGURED: gameplay and expedition; dedicated boss art pending.");
        }
    }
}
