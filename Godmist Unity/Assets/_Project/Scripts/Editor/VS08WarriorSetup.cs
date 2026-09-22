using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable content authoring. Does not run on import or change live saves.
public static partial class VS08WarriorSetup
{
    const string Root = "Assets/_Project/ScriptableObjects/";
    static string F(string name) => "<" + name + ">k__BackingField";
    static void Set(SerializedProperty p, object value)
    {
        if (value is UnityEngine.Object obj) p.objectReferenceValue = obj;
        else if (value is string text) p.stringValue = text;
        else if (value is bool flag) p.boolValue = flag;
        else if (value is Enum) p.intValue = Convert.ToInt32(value);
        else if (value is int integer) p.intValue = integer;
        else p.floatValue = Convert.ToSingle(value);
    }
    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(Root + path) ?? throw new InvalidOperationException(path);
    static void Field(SerializedObject so, string name, object value) => Set(so.FindProperty(F(name)), value);
    static void Effect(SerializedObject so, int index, EffectType type, Action<SerializedProperty> configure = null)
    {
        var list = so.FindProperty("effects");
        list.arraySize = Math.Max(list.arraySize, index + 1);
        var p = list.GetArrayElementAtIndex(index);
        Set(p.FindPropertyRelative("effectType"), type);
        Set(p.FindPropertyRelative("baseChance"), 1f);
        Set(p.FindPropertyRelative("damageMultiplier"), 1f);
        Set(p.FindPropertyRelative("damageHitCount"), 1);
        Set(p.FindPropertyRelative("bonusDamageMultiplier"), 1f);
        Set(p.FindPropertyRelative("durationTurns"), 1);
        Set(p.FindPropertyRelative("stacks"), 1);
        Set(p.FindPropertyRelative("alwaysHits"), type != EffectType.DealDamage);
        configure?.Invoke(p);
    }
    static SkillData Skill(string file, string id, string name, int resource, int ap, int knives,
        int range, float accuracy, string description, Action<SerializedObject> configure,
        TargetType target = TargetType.Single)
    {
        var path = Root + "Skills/" + file + ".asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillData>(); AssetDatabase.CreateAsset(skill, path); }
        var so = new SerializedObject(skill);
        Field(so,"SkillId",id); Field(so,"DisplayName",name); Field(so,"TooltipDescription",description);
        Field(so,"ResourceCost",resource); Field(so,"ResourceType",ResourceType.Rage);
        Field(so,"ActionCost",ap); Field(so,"KnifeCost",knives); Field(so,"SkillAccuracy",accuracy);
        Field(so,"AttackType",knives > 0 ? AttackType.Melee : AttackType.Melee);
        Field(so,"PointBlankAttackType",knives > 0 ? AttackType.Melee : AttackType.Unknown);
        Field(so,"PointBlankDamageMultiplier",1f); Field(so,"FreeKnivesInMelee",false);
        Field(so,"GuaranteedCritical",false); Field(so,"CanRecoverKnives",false);
        Field(so,"RequiresLineOfSight",target == TargetType.Single);
        if (skill.Icon == null) Field(so,"Icon",Load<SkillData>("Skills/SD_KnifeToss.asset").Icon);
        var modes = so.FindProperty("targetModes"); modes.ClearArray(); modes.arraySize = 1;
        var mode = modes.GetArrayElementAtIndex(0);
        Set(mode.FindPropertyRelative("targetType"),target);
        Set(mode.FindPropertyRelative("targetingType"),TargetingType.Enemies);
        Set(mode.FindPropertyRelative("includesCaster"),false);
        var absolute = mode.FindPropertyRelative("absoluteRange");
        Set(absolute.FindPropertyRelative("shape"),RangeShape.Radial);
        Set(absolute.FindPropertyRelative("minRange"),1); Set(absolute.FindPropertyRelative("maxRange"),range);
        so.FindProperty("effects").ClearArray();
        configure(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(skill);
        var report = CombatDataValidator.ValidateSkill(skill);
        if (!report.IsValid) throw new InvalidOperationException(id + ": " + string.Join("; ",report.Errors));
        return skill;
    }

    static void Ally(SerializedObject so,bool caster=false)
    {
        var mode=so.FindProperty("targetModes").GetArrayElementAtIndex(0);
        Set(mode.FindPropertyRelative("targetingType"),TargetingType.Allies);Set(mode.FindPropertyRelative("includesCaster"),caster);
    }
    static void Status(SerializedObject so,int index,string id,float chance,int turns,float damage=0,bool hit=true)
    {
        Effect(so,index,EffectType.ApplyStatus,p=>{Set(p.FindPropertyRelative("statusId"),id);Set(p.FindPropertyRelative("baseChance"),chance);Set(p.FindPropertyRelative("durationTurns"),turns);Set(p.FindPropertyRelative("statusDamageMultiplier"),damage);if(hit)RequireHit(p);});
    }
    static void RequireHit(SerializedProperty effect)
    {
        var list=effect.FindPropertyRelative("conditions");list.arraySize=1;var c=list.GetArrayElementAtIndex(0);
        Set(c.FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);Set(c.FindPropertyRelative("referencedEffectIndex"),0);
    }
    [MenuItem("Godmist/Content/Configure VS-08 Warrior")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var skills=new Dictionary<string,SkillData>();
        skills["cut"]=Skill("SD_WarriorCut","skill.warrior.cut","Cięcie",0,60,0,1,82,"Zadaje losowe obrażenia fizyczne.",so=>Effect(so,0,EffectType.DealDamage));
        skills["kick"]=Skill("SD_WarriorKick","skill.warrior.kick","Kopnięcie",20,40,0,1,100,"Odepchnięcie o 1 pole; 80% szansy na -15 uniku na 1 turę.",so=>{
            Effect(so,0,EffectType.Push,p=>Set(p.FindPropertyRelative("displacementDistance"),1));
            Effect(so,1,EffectType.ModifyStat,p=>{Set(p.FindPropertyRelative("baseChance"),.8f);Set(p.FindPropertyRelative("modifierStatType"),CharacterStatType.Dodge);Set(p.FindPropertyRelative("modifierOperation"),StatModifierOperation.FlatAbsolute);Set(p.FindPropertyRelative("modifierValue"),-15f);});});
        skills["battle_cry"]=Skill("SD_BattleCry",Load<SkillData>("Skills/SD_BattleCry.asset").SkillId,"Okrzyk Wojenny",30,35,0,0,100,"Cała drużyna zadaje 15% więcej obrażeń przez 3 tury.",so=>{
            Ally(so,true);Effect(so,0,EffectType.ModifyVar,p=>{Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);Set(p.FindPropertyRelative("modifierVariableType"),GameplayVariableType.DamageDealtPercent);Set(p.FindPropertyRelative("modifierValue"),.15f);Set(p.FindPropertyRelative("durationTurns"),3);});},TargetType.All);
        skills["right_hook"]=Skill("SD_WarriorRightHook","skill.warrior.right_hook","Prawy Sierpowy",35,60,0,1,72,"Najmniejsze obrażenia fizyczne i 80% szansy na ogłuszenie na 3 tury.",so=>{
            Effect(so,0,EffectType.DealDamage,p=>Set(p.FindPropertyRelative("damageValueSource"),DamageValueSource.MinAttack));Status(so,1,StatusEffectIds.Stun,.8f,3);});
        skills["bandage"]=Skill("SD_WarriorBandage","skill.warrior.bandage","Bandaż Polowy",40,65,0,2,100,"Leczy 8% brakującego zdrowia sojusznika i usuwa krwawienie.",so=>{
            Ally(so);Effect(so,0,EffectType.HealMissingHealthPercent,p=>{Set(p.FindPropertyRelative("percentageValue"),.08f);Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);});
            Effect(so,1,EffectType.RemoveStatus,p=>{Set(p.FindPropertyRelative("statusId"),StatusEffectIds.Bleed);Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);});});
        skills["destruction"]=Skill("SD_WarriorDestruction","skill.warrior.destruction","Rozkaz Unicestwienia",45,70,0,4,100,"90% szansy: cel otrzymuje od drużyny 20% więcej obrażeń przez 3 tury.",so=>Effect(so,0,EffectType.MarkForDestruction,p=>{Set(p.FindPropertyRelative("baseChance"),.9f);Set(p.FindPropertyRelative("durationTurns"),3);}));
        skills["immobilize"]=Skill("SD_WarriorImmobilize","skill.warrior.immobilize","Unieruchomienie",40,70,0,1,71,"Losowe obrażenia fizyczne; 90% szansy na unieruchomienie na 2 tury.",so=>{Effect(so,0,EffectType.DealDamage);Status(so,1,StatusEffectIds.Immobilize,.9f,2);});
        skills["protection"]=Skill("SD_WarriorProtection","skill.warrior.protection","Rozkaz Ochrony",35,70,0,4,100,"Ochrona na 3 tury: -30% przyjmowanych obrażeń oraz -10% za każdego sąsiadującego sojusznika, do -60%.",so=>{Ally(so);Effect(so,0,EffectType.ProtectAlly,p=>{Set(p.FindPropertyRelative("durationTurns"),3);Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);});});
        skills["regroup"]=Skill("SD_WarriorRegroup","skill.warrior.regroup","Rozkaz Przegrupowania",30,70,0,3,100,"Zeruje AV sojusznika, przyspieszając jego kolejną turę.",so=>{Ally(so);Effect(so,0,EffectType.ModifyActionValue,p=>Set(p.FindPropertyRelative("actionValuePercent"),-1f));Effect(so,1,EffectType.GrantFreeMovement);});
        skills["bleeding_destruction"]=Skill("SD_WarriorBleedingDestruction","skill.warrior.bleeding_destruction","Krwawiące Zniszczenie",40,65,0,1,75,"Najmniejsze obrażenia fizyczne; 75% szansy na krwawienie 90% przez 4 tury.",so=>{Effect(so,0,EffectType.DealDamage,p=>Set(p.FindPropertyRelative("damageValueSource"),DamageValueSource.MinAttack));Status(so,1,StatusEffectIds.Bleed,.75f,4,.9f);});
        skills["shout"]=Skill("SD_WarriorShout","skill.warrior.shout","Krzyk",0,60,0,3,100,"Zużywa całą aktualną wściekłość. 90% szansy na ogłuszenie na 1 turę, przy wydaniu co najmniej 50 PW: 3 tury.",so=>Status(so,0,StatusEffectIds.Stun,.9f,1,0,false));
        skills["whirlwind"]=Skill("SD_WarriorWhirlwind","skill.warrior.whirlwind","Młyniec",40,20,0,1,79,"Trzy ciosy po 25% losowych obrażeń fizycznych we wszystkich sąsiadujących wrogów.",so=>Effect(so,0,EffectType.DealDamage,p=>{Set(p.FindPropertyRelative("damageHitCount"),3);Set(p.FindPropertyRelative("damageMultiplier"),.25f);}),TargetType.AreaAroundCaster);
        skills["death_wish"]=Skill("SD_WarriorDeathWish","skill.warrior.death_wish","Pragnienie Śmierci",0,0,0,0,100,"Podtrzymanie: większe z 20 PW i 25% maksymalnej wściekłości na turę. +50% zadawanych i przyjmowanych obrażeń; śmiertelne trafienia pozostawiają 1 HP. Wygasa przy braku surowca.",so=>{
            Field(so,"IsToggle",true);Field(so,"ToggleUpkeepFlatResourceCost",20);Field(so,"ToggleUpkeepMaxResourcePercent",.25f);
            for(var i=0;i<2;i++){var j=i;Effect(so,i,EffectType.ModifyVar,p=>{Set(p.FindPropertyRelative("modifierVariableType"),j==0?GameplayVariableType.DamageDealtPercent:GameplayVariableType.DamageReceivedPercent);Set(p.FindPropertyRelative("modifierValue"),.5f);Set(p.FindPropertyRelative("durationTurns"),1);});}},TargetType.Caster);
        ConfigureTrees(skills);
        var character=new SerializedObject(Load<CharacterDefinition>("Characters/CD_Wladimir_Test.asset"));
        var starters=character.FindProperty(F("StartingSkills"));starters.arraySize=5;var ids=new[]{"cut","kick","battle_cry","right_hook","bandage"};
        for(var i=0;i<5;i++)starters.GetArrayElementAtIndex(i).objectReferenceValue=skills[ids[i]];
        Field(character,"BasicAttackSkill",skills["cut"]);character.ApplyModifiedPropertiesWithoutUndo();
        var cls=new SerializedObject(Load<CharacterClassData>("Classes/CCD_Warrior.asset"));Field(cls,"BasicAttackSkill",skills["cut"]);
        var trees=cls.FindProperty(F("SkillTrees"));trees.arraySize=2;trees.GetArrayElementAtIndex(0).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Warrior_Commander.asset");trees.GetArrayElementAtIndex(1).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Warrior_Brawler.asset");cls.ApplyModifiedPropertiesWithoutUndo();
        var catalog=new SerializedObject(Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"));var list=catalog.FindProperty(F("SkillDefinitions"));
        foreach(var skill in skills.Values){var found=false;for(var i=0;i<list.arraySize;i++)found|=list.GetArrayElementAtIndex(i).objectReferenceValue==skill;if(!found){list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;}}
        catalog.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
    }
    static readonly string[][] Nodes = {
        new[]{"commander","destruction","Rozkaz Unicestwienia","[90%] Zwiększa obrażenia zadawane celowi przez wszystkich sojuszników o 20% na 3 tury","","destruction","1","1","1"},
        new[]{"commander","marking","Oznaczenie","Zwiększa szansę na obrażenia krytyczne przeciwko celowi objętemu Rozkazem Unicestwienia o 50%/100%[p]","destruction","","1","2","2"},
        new[]{"commander","armor_shred","Zdarcie Pancerza","Zwiększa penetrację fizyczną o 25%","","","1","3","1"},
        new[]{"commander","armor_piercing","Przebijanie Pancerza","Każde obrażenia są zwiększone o 0.15%/0.3% za każdy aktualny procent zdrowia przeciwnika","armor_shred","","1","4","2"},
        new[]{"commander","weapon_mastery","Mistrzostwo Broni","Zwiększa szansę krytyczną o 5%/10%/15%[p]","","","1","5","3"},
        new[]{"commander","breakdown","Rozbicie","Zwiększa bonus obrażeń Rozkazu Unicestwienia o 5%/10%/15%","destruction","","2","1","3"},
        new[]{"commander","immobilize","Unieruchomienie","Zadaje losowe obrażenia fizyczne, [90%] unieruchomienie na 2 tury","","immobilize","2","2","1"},
        new[]{"commander","broken_bones","Połamane Kości","Zwiększa czas trwania Unieruchomienia o 2 tury","immobilize","","2","3","1"},
        new[]{"commander","piercing","Przebicie","Zwiększa obrażenia krytyczne o 20%","","","2","4","1"},
        new[]{"commander","deep_wounds","Głębokie Rany","Obrażenia krytyczne mają 90% szansy na wyrządzenie krwawienia 45%/60%/75% na 3 tury","weapon_mastery,piercing","","2","5","3"},
        new[]{"commander","flanking","Flankowanie","Zmniejsza obronę celu objętego Rozkazem Unicestwienia o 5%/10% za każdego przyległego sojusznika","breakdown","","3","1","2"},
        new[]{"commander","pinning","Przygwożdżenie","Unieruchomienie zmniejsza unik przeciwnika o 12/20","immobilize","","3","2","2"},
        new[]{"commander","trauma","Trauma","[85%] Ciosy krytyczne zwiększają obrażenia DoT na przeciwniku o 30%/60% na 5 tur","","","3","3","2"},
        new[]{"commander","protection","Rozkaz Ochrony","Zmniejsza przyjmowane obrażenia celu o 30% i o dodatkowe 10% za każdego przyległego sojusznika na 3 tury","","protection","3","4","1"},
        new[]{"commander","renewal","Odnowienie Sił","Daje 5%/10%/15% szansy na uniknięcie ogłuszenia i odnowę 10% maksymalnego zdrowia","","","3","5","3"},
        new[]{"commander","regroup","Rozkaz Przegrupowania","Przyśpiesza ruch celu do przodu o 100%","","regroup","4","1","1"},
        new[]{"commander","strategy","Strategia","Rozkaz Przegrupowania daje wszystkim sojusznikom 2 darmowe kroki w każdej turze przez 3 tury","regroup","","4","2","1"},
        new[]{"commander","finisher","Dobitka","Cięcie ma 3%/6%/9% na niezużycie punktów akcji","","","4","3","3"},
        new[]{"commander","riposte","Riposta","Przeciwnicy częściej obierają za cel osobę objętą Rozkazem Ochrony, szansa zwiększa się za każdą przyległą osobę. Zwraca 30% obrażeń przedmitygacyjnych do atakującego","protection","","4","4","1"},
        new[]{"commander","furious_rising","Wściekłe Powstanie","Odnowienie Sił zadaje przeciwnikowi, który chciał wywołać ogłuszenie obrażenia magiczne równe 3%/7% jego maksymalnego zdrowia ze zwiększoną szansą na cios krytyczny o 10%.","renewal","","4","5","2"},
        new[]{"brawler","bleeding_destruction","Krwawiące Zniszczenie","Zadaje najmniejsze obrażenia fizyczne, [75%] krwawienie 90% na 4 tury","","bleeding_destruction","1","1","1"},
        new[]{"brawler","wounding","Wyrządzanie Ran","Każdy posiadany punkt wściekłości zwiększa szansę na krwawienie Krwawiącego Zniszczenia o 0.05%/0.1%","bleeding_destruction","","1","2","2"},
        new[]{"brawler","thirst_revenge","Pragnienie Zemsty","Przyjmowanie obrażeń generuje 1/2/3 wściekłości za każde utracone 5% maksymalnego zdrowia","rage","","1","3","3"},
        new[]{"brawler","rage","Wściekłość","Zwiększa ilość wściekłości generowanej przez Cięcie o 5 i zmniejsza efekt kary za niedobór wściekłości o 50%","","","1","4","1"},
        new[]{"brawler","furious_strikes","Wściekłe Uderzenia","Zmniejsza koszt wściekłości umiejętności ofensywnych o 10%/20%/30%","rage","","1","5","3"},
        new[]{"brawler","larger_wound","Obszerniejsza Rana","Zwiększa obrażenia krwawienia Krwawiącego Zniszczenia o 15%/30%","bleeding_destruction","","2","1","2"},
        new[]{"brawler","revenge","Zemsta","Każdy posiadany punkt wściekłości zwiększa szansę na obrażenia krytyczne o 0.05%/0.1%","wounding","","2","2","2"},
        new[]{"brawler","release_rage","Wyzwolić Wściekłość","Zwiększa maksymalną ilość punktów wściekłości o 10/20/30","","","2","3","3"},
        new[]{"brawler","shout","Krzyk","[90%] Ogłusza na 1 turę, lub na 3 tury, jeśli zużyje co najmniej 50 wściekłości","","shout","2","4","1"},
        new[]{"brawler","spiked_armor","Kolczasta Zbroja","Zwiększa atak o 0.1 za każdy punkt obrony fizycznej","","","2","5","1"},
        new[]{"brawler","whirlwind","Młyniec","Zadaje 25% losowych obrażeń 3 razy","","whirlwind","3","1","1"},
        new[]{"brawler","third_time","Do Trzech Razy Sztuka","Co trzecie Cięcie z rzędu na tym samym celu ma zwiększone obrażenia o 0.25%/0.5% za każdy posiadany punkt wściekłości","revenge","","3","2","2"},
        new[]{"brawler","amok","Amok","Ciosy krytyczne zwiększają maksymalną wściekłość o 20/30/40 na 5 tur","release_rage","","3","3","3"},
        new[]{"brawler","unawareness","Wykorzystanie Nieświadomości","Ataki na celach ogłuszonych generują 50%/100% więcej wściekłości","shout","","3","4","2"},
        new[]{"brawler","aggressive_nature","Agresywna Natura","Pasywnie generuje 3 wściekłości na turę","","","3","5","1"},
        new[]{"brawler","conflagration","Centrum Pożogi","Każdy cios zadany przez Młyniec ma 15% szansy na wyrządzenie podpalenia 30%/50% na 2 tury","whirlwind","","4","1","2"},
        new[]{"brawler","force_moment","Moment Siły","Zwiększa ilość ciosów zadawanych przez Młyniec o 1/2","conflagration","","4","2","2"},
        new[]{"brawler","adrenaline","Adrenalina","Zwiększa szybkość o 4 za każde 25 punktów wściekłości","","","4","3","1"},
        new[]{"brawler","death_wish","Pragnienie Śmierci","Zużywa 25% maksymalnej lub 20 wściekłości (większe) na turę, zwiększa zadawane i przyjmowane obrażenia o 50%, każde śmiertelne obrażenia są negowane, kończy efekt gdy braknie wściekłości","","death_wish","4","4","1"},
        new[]{"brawler","desperation","Desperacja","Zwiększa bonus zadawanych i przyjmowanych obrażeń Pragnienia Śmierci o 25%/50%","death_wish","","4","5","2"}
    };
    static void ConfigureTrees(Dictionary<string,SkillData> skills)
    {
        foreach(var treeName in new[]{"commander","brawler"})
        {
            var path=Root+"Skills/SkillTrees/ST_Warrior_"+(treeName=="commander"?"Commander":"Brawler")+".asset";
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);if(tree==null){tree=ScriptableObject.CreateInstance<SkillTreeDefinition>();AssetDatabase.CreateAsset(tree,path);}
            var so=new SerializedObject(tree);Field(so,"Id","tree.warrior."+treeName);Field(so,"DisplayName",treeName=="commander"?"Dowódca":"Awanturnik");
            var global=so.FindProperty(F("GlobalPassive"));Set(global.FindPropertyRelative(F("DisplayName")),"Pasyw drzewka");Set(global.FindPropertyRelative(F("Description")),treeName=="commander"?"Za punkt: +0,3% szansy krytycznej. Dodatkowe nieuchronne obrażenia: 0,002% obrażeń trafienia za każdy procent ulepszenia broni i punkt drzewka.":"Za punkt: +1% maksymalnej wściekłości; 2% faktycznie wydanej wściekłości zamienia się w zdrowie.");global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var list=so.FindProperty(F("Nodes"));list.ClearArray();
            foreach(var node in Nodes)
            {
                if(node[0]!=treeName)continue;list.InsertArrayElementAtIndex(list.arraySize);var p=list.GetArrayElementAtIndex(list.arraySize-1);
                Set(p.FindPropertyRelative(F("Id")),"node.warrior."+treeName+"."+node[1]);Set(p.FindPropertyRelative(F("DisplayName")),node[2]);Set(p.FindPropertyRelative(F("Description")),node[3]);
                Set(p.FindPropertyRelative(F("Tier")),int.Parse(node[6]));Set(p.FindPropertyRelative(F("Column")),int.Parse(node[7]));Set(p.FindPropertyRelative(F("MaxRanks")),int.Parse(node[8]));
                p.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=node[5].Length>0?skills[node[5]]:null;p.FindPropertyRelative(F("Icon")).objectReferenceValue=skills["cut"].Icon;p.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=p.FindPropertyRelative(F("Requirements"));req.ClearArray();foreach(var id in node[4].Split(',')){if(id.Length==0)continue;req.InsertArrayElementAtIndex(req.arraySize);var q=req.GetArrayElementAtIndex(req.arraySize-1);Set(q.FindPropertyRelative(F("NodeId")),"node.warrior."+treeName+"."+id);Set(q.FindPropertyRelative(F("MinimumRanks")),1);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
