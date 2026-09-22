using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

public static partial class VS07ScoutSetup
{
    sealed class Node
    {
        public readonly string Tree,Id,Name,Description,Requirement,Skill;
        public readonly int Tier,Column,Ranks;
        public Node(string tree,string id,string name,string description,string requirement,string skill,int tier,int column,int ranks)
        {Tree=tree;Id=id;Name=name;Description=description;Requirement=requirement;Skill=skill;Tier=tier;Column=column;Ranks=ranks;}
    }
    static readonly Node[] Nodes = {
        new Node("assassin","backstab","Cios w Plecy","Zwiększa obrażenia zadawane od tyłu o 10%/20%","","",1,1,2),
        new Node("assassin","ricochet","Rykoszet","Zadaje losowe obrażenia fizyczne, każde kolejne trafienie ma 10% większą szansę krytyczną","","ricochet",1,2,1),
        new Node("assassin","farsight","Dalekie Widzenie","Zwiększa zasięg bezwzględny i względny Rykoszetu o 1/3","ricochet","",1,3,2),
        new Node("assassin","critical_point","Krytyczny Punkt","Zwiększa obrażenia krytyczne zyskane ze Słabego Punktu o 5%/10%/15%","weak_point","",1,4,3),
        new Node("assassin","weak_point","Słaby Punkt","[95%] Zwiększa szansę krytyczną o 10% i obrażenia krytyczne o 20% na 5 tur wobec wroga","","weak_point",1,5,1),
        new Node("assassin","leg_sweep","Podcięcie Nóg","Atakowanie przeciwników od tyłu po poruszaniu się ma 25%/50% szansy na ogłuszenie na 1 turę","backstab","",2,1,2),
        new Node("assassin","throw_power","Siła Rzutu","Każdy kolejny przeciwnik trafiony Rykoszetem otrzymuje 10%/20% więcej obrażeń","ricochet","",2,2,2),
        new Node("assassin","knife_bounce","Odbicie Sztyletu","Zwiększa ilość celów trafianych przez Rykoszet o 2","throw_power","",2,3,1),
        new Node("assassin","rapid_strikes","Gwałtowne Uderzenia","Zwiększa szansę krytyczną w walce wręcz o 5%/10%","","",2,4,2),
        new Node("assassin","deep_analysis","Dogłębna Analiza","Ciosy krytyczne podczas działania Słabego Punktu dają 20%/30% penetracji fizycznej wobec każdego przeciwnika na 3 tury","weak_point","",2,5,2),
        new Node("assassin","running_attack","Atak w Biegu","Przechodzenie obok przeciwników zadaje im 25%/50% losowych obrażeń fizycznych z celnością 77 (raz na turę)","leg_sweep","",3,1,2),
        new Node("assassin","surprise","Zaskoczenie","Atak wyprowadzony podczas trwania Wojownika Cieni ma 30%/60% większe obrażenia","shadow_warrior","",3,2,2),
        new Node("assassin","shadow_warrior","Wojownik Cieni","Daje niewidzialność na 3 tury, Szarża 1","","shadow",3,3,1),
        new Node("assassin","karambit_style","Styl Karambitowy","Zwiększa obrażenia w walce wręcz o 25%/60%/100%","rapid_strikes","",3,4,3),
        new Node("assassin","throat_cut","Podcięcie Gardła","Daje 4%/7%/10% szansy na natychmiastowe zabicie ciosem wręcz przeciwnika, którego zabiłaby pięciokrotność zadanych obrażeń. Bossy zamiast tego otrzymują 100% obrażeń więcej","karambit_style","",3,5,3),
        new Node("assassin","keen_eye","Celny Wzrok","Zwiększa szansę na Ostry Nóż o 5%/10%","sharp_knife","",4,1,2),
        new Node("assassin","sharp_knife","Ostry Nóż","Rzucone noże mają 15% szansy na wbicie się w przeciwnika. Kliknięcie wyjmuje go rzucając [90%] krwawienie 60%/90% na 3 tury","","",4,2,2),
        new Node("assassin","light_step","Lekki Krok","Zmniejsza wymagania ruchowe podczas trwania Wojownika Cieni o 1/3 punkty akcji","shadow_warrior","",4,3,2),
        new Node("assassin","precise_strike","Trafny Cios","Zwiększa czas trwania ogłuszenia Uderzenia Głowicą do 2 tur","pommel_strike","",4,4,1),
        new Node("assassin","pommel_strike","Uderzenie Głowicą","Daje 15%/30% na ogłuszenie przeciwnika na 1 turę, jeśli Podcięcie Gardła nie powiedzie się","throat_cut","",4,5,2),
        new Node("blade_dancer","viruses","Wirusy Osłabiające","Zatruty Sztylet zmniejsza odporność na zatrucie o 15%/30% na 3 tury","poisoned_dagger","",1,1,2),
        new Node("blade_dancer","poisoned_dagger","Zatruty Sztylet","Zadaje najmniejsze obrażenia fizyczne, [65%] zatrucie 90% na 3 tury","","poisoned_dagger",1,2,1),
        new Node("blade_dancer","venom","Jad","Zwiększa obrażenia zatrucia Zatrutego Sztyletu o 10%/20%/30%","poisoned_dagger","",1,3,3),
        new Node("blade_dancer","strong_insult","Silna Zniewaga","Zwiększa bonus obrażeń za każdy punkt Złości o 0.05%/0.1%","anger","",1,4,2),
        new Node("blade_dancer","anger","Złość","Zadawanie obrażeń nakłada na przeciwnika 10 punktów Złości (max 100), każdy punkt zwiększa otrzymywane obrażenia o 0.15%","","",1,5,1),
        new Node("blade_dancer","knee_throw","Rzut w Kolano","Rzut Sztyletem ma 10%/20%/30% na spowolnienie przeciwnika o 15 na 3 tury","","",2,1,3),
        new Node("blade_dancer","combo","Kombo","Daje 5%/10% szansy na podwojenie Rzutu Sztyletem","","",2,2,2),
        new Node("blade_dancer","quick_strikes","Szybkie Ciosy","Zadaje 50% losowych obrażeń fizycznych 3 razy, każdy cios ma 35% na podwójne obrażenia","","quick_strikes",2,3,1),
        new Node("blade_dancer","series","Seria Ciosów","Po 3 bazowych ciosach Szybkich Ciosów osobno losuje 50% na każdy z 1/2 dodatkowych ciosów. Porażka nie przerywa kolejnych losowań.","quick_strikes","",2,4,2),
        new Node("blade_dancer","revenge","Zemsta","Otrzymywanie obrażeń od przeciwników nakłada na nich 10 punktów Złości","anger","",2,5,1),
        new Node("blade_dancer","stimulating_tonic","Tonik Pobudzający","Przyspieszenie o 20 na 5 tur, po tym spowolnienie o 10 na 3 tury","","tonic",3,1,1),
        new Node("blade_dancer","combo_master","Mistrz Kombo","Kombo narzuca [80%] krwawienie 100%/120% na 3 tury","combo","",3,2,2),
        new Node("blade_dancer","fine_thrusts","Finezyjne Pchnięcia","Zwiększa szanse na podwójne obrażenia Szybkich Ciosów o 10%/20%","quick_strikes","",3,3,2),
        new Node("blade_dancer","dance_of_doom","Taniec Zagłady","Zwiększa maksymalną ilość dodatkowych ciosów z Serii Ciosów o 1/2","series","",3,4,2),
        new Node("blade_dancer","mental_control","Kontrola Psychiczna","Zwiększa przyrost Złości o 1/3/5","revenge","",3,5,3),
        new Node("blade_dancer","brew_strength","Siła Naparu","Zwiększa przyspieszenie Toniku Pobudzającego o 10 i spowolnienie o 5","stimulating_tonic","",4,1,1),
        new Node("blade_dancer","strong_liver","Mocna Wątroba","Skraca spowolnienie Toniku Pobudzającego o 1 turę","brew_strength","",4,2,1),
        new Node("blade_dancer","accurate_throw","Celny Rzut","Zwiększa obrażenia o 2%/4% za każde pole odległości od celu","","",4,3,2),
        new Node("blade_dancer","poison_master","Mistrz Trucizny","Zwiększa siłę wszystkich zatruć o 10%/20%[p]","","",4,4,2),
        new Node("blade_dancer","spare_daggers","Zapasowe Sztylety","Zwiększa maksymalną ilość sztyletów o 1/2","","",4,5,2),
    };
    [MenuItem("Godmist/Content/Configure VS-07 complete Scout")]
    public static void Configure()
    {
        ConfigureStarters();
        var skills=new Dictionary<string,SkillData>();
        skills["ricochet"]=Skill("SD_ScoutRicochet","skill.scout.ricochet","Rykoszet",60,70,1,4,81,
            "Trafia do 3 różnych wrogów. Każdy kolejny cel daje +10% szansy na kryt. Kolejny wróg musi być w zasięgu 2 od poprzedniego.",so=>{
                Field(so,"RequiresLineOfSight",true); Field(so,"FreeKnivesInMelee",false); Field(so,"PointBlankAttackType",AttackType.Ranged);
                var mode=so.FindProperty("targetModes").GetArrayElementAtIndex(0);
                Set(mode.FindPropertyRelative("chainTargets"),3);
                var range=mode.FindPropertyRelative("relativeRange");
                Set(range.FindPropertyRelative("shape"),RangeShape.Square);
                Set(range.FindPropertyRelative("minRange"),1); Set(range.FindPropertyRelative("maxRange"),2);
                Effect(so,0,EffectType.DealDamage);
            },TargetType.Chain);
        skills["weak_point"]=Skill("SD_ScoutWeakPoint","skill.scout.weak_point","Słaby Punkt",80,55,0,8,100,
            "95% szansy na oznaczenie wroga na 5 tur. Ataki przeciw niemu mają +10% szansy na kryt i +20% obrażeń krytycznych.",so=>
                Effect(so,0,EffectType.MarkWeakPoint,p=>{Set(p.FindPropertyRelative("baseChance"),.95f);Set(p.FindPropertyRelative("durationTurns"),5);}));
        skills["shadow"]=Skill("SD_ScoutShadow","skill.scout.shadow","Wojownik Cieni",50,70,0,1,100,
            "Przemieszcza na sąsiednie wolne pole i daje niewidzialność na 3 tury.",so=>{
                Field(so,"RequiresLineOfSight",true);
                Effect(so,0,EffectType.Teleport,p=>{
                    Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);
                    var range=p.FindPropertyRelative("teleportDestinationRange");
                    Set(range.FindPropertyRelative("shape"),RangeShape.Radial);Set(range.FindPropertyRelative("minRange"),1);Set(range.FindPropertyRelative("maxRange"),1);
                });
                Effect(so,1,EffectType.ApplyStatus,p=>{
                    Set(p.FindPropertyRelative("polarity"),EffectPolarity.Positive);
                    Set(p.FindPropertyRelative("statusId"),StatusEffectIds.Invisibility);Set(p.FindPropertyRelative("durationTurns"),3);
                });
            },TargetType.Caster);
        skills["poisoned_dagger"]=Skill("SD_ScoutPoisonedDagger","skill.scout.poisoned_dagger","Zatruty Sztylet",25,65,1,4,81,
            "Zadaje minimalne obrażenia fizyczne. Po trafieniu: 65% szansy na zatrucie o sile 90% na 3 tury.",so=>{
                Effect(so,0,EffectType.DealDamage,p=>Set(p.FindPropertyRelative("damageValueSource"),DamageValueSource.MinAttack));
                Effect(so,1,EffectType.ModifyResistance,p=>{
                    Set(p.FindPropertyRelative("resistanceType"),ResistanceType.Poison);Set(p.FindPropertyRelative("durationTurns"),3);
                    Set(p.FindPropertyRelative("modifierValue"),-.15f); RequireHit(p);
                });
                Effect(so,2,EffectType.ApplyStatus,p=>{
                    Set(p.FindPropertyRelative("statusId"),StatusEffectIds.Poison);Set(p.FindPropertyRelative("durationTurns"),3);
                    Set(p.FindPropertyRelative("baseChance"),.65f);Set(p.FindPropertyRelative("statusDamageMultiplier"),.9f);RequireHit(p);
                });
            });
        skills["quick_strikes"]=Skill("SD_ScoutQuickStrikes","skill.scout.quick_strikes","Szybkie Ciosy",30,70,3,4,74,
            "3 ciosy po 50% losowych obrażeń fizycznych. Każdy ma 35% szansy na podwójne obrażenia. W zwarciu nie zużywa sztyletów.",so=>
                Effect(so,0,EffectType.DealDamage,p=>{
                    Set(p.FindPropertyRelative("damageMultiplier"),.5f);Set(p.FindPropertyRelative("damageHitCount"),3);
                    Set(p.FindPropertyRelative("additionalHitChance"),.5f);
                    Set(p.FindPropertyRelative("bonusDamageMultiplierChance"),.35f);Set(p.FindPropertyRelative("bonusDamageMultiplier"),2f);
                }));
        skills["tonic"]=Skill("SD_ScoutTonic","skill.scout.tonic","Tonik Pobudzający",65,40,0,0,100,
            "+20 szybkości na 5 tur, następnie -10 szybkości na 3 tury.",so=>{
                for(var i=0;i<2;i++) { var index=i; Effect(so,index,EffectType.ModifyStat,p=>{
                    Set(p.FindPropertyRelative("polarity"),index==0?EffectPolarity.Positive:EffectPolarity.Negative);
                    Set(p.FindPropertyRelative("modifierStatType"),CharacterStatType.Speed);
                    Set(p.FindPropertyRelative("modifierOperation"),StatModifierOperation.FlatAbsolute);
                    Set(p.FindPropertyRelative("modifierValue"),index==0?20f:-10f);
                    Set(p.FindPropertyRelative("durationTurns"),index==0?5:3);Set(p.FindPropertyRelative("delayCasterTurns"),index==0?0:5);
                }); }
            },TargetType.Caster);
        foreach(var treeName in new[]{"assassin","blade_dancer"})
        {
            var tree=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Scout_"+(treeName=="assassin"?"Assassin":"BladeDancer")+".asset");
            var so=new SerializedObject(tree);
            Field(so,"Id","tree.scout."+treeName);Field(so,"DisplayName",treeName=="assassin"?"Skrytobójca":"Tancerz Ostrzy");
            var global=so.FindProperty(F("GlobalPassive"));
            Set(global.FindPropertyRelative(F("DisplayName")),"Pasyw drzewka");
            Set(global.FindPropertyRelative(F("Description")),treeName=="assassin"?"Za punkt: +0.2% szansy krytycznej, +0.5% obrażeń wręcz.":"Za punkt: +1% szansy niezużycia sztyletu, +1% siły zatrucia.");
            var globals=global.FindPropertyRelative(F("PassivesPerPoint"));globals.ClearArray();
            if(treeName=="assassin")
            {
                globals.arraySize=1;var passive=globals.GetArrayElementAtIndex(0);
                Set(passive.FindPropertyRelative(F("Id")),"tree.scout.assassin.global.critical_chance");
                Set(passive.FindPropertyRelative(F("DisplayName")),"Instynkt Zabójcy");
                Set(passive.FindPropertyRelative(F("StackingRule")),PassiveEffectStackingRule.Additive);
                var operations=passive.FindPropertyRelative(F("Operations"));operations.arraySize=1;var op=operations.GetArrayElementAtIndex(0);
                Set(op.FindPropertyRelative(F("Id")),"modify-critical-chance");
                Set(op.FindPropertyRelative(F("Type")),PassiveEffectOperationType.ModifyStat);
                Set(op.FindPropertyRelative(F("StatType")),CharacterStatType.CritChance);
                Set(op.FindPropertyRelative(F("ModifierOperation")),StatModifierOperation.FlatAbsolute);
                var value=op.FindPropertyRelative(F("Value"));Set(value.FindPropertyRelative(F("NumberValue")),.002f);Set(value.FindPropertyRelative(F("Scale")),1f);
            }
            var list=so.FindProperty(F("Nodes"));list.ClearArray();
            foreach(var node in Nodes)
            {
                if(node.Tree!=treeName)continue;
                list.InsertArrayElementAtIndex(list.arraySize);var p=list.GetArrayElementAtIndex(list.arraySize-1);
                Set(p.FindPropertyRelative(F("Id")),"node.scout."+treeName+"."+node.Id);
                Set(p.FindPropertyRelative(F("DisplayName")),node.Name);Set(p.FindPropertyRelative(F("Description")),node.Description);
                Set(p.FindPropertyRelative(F("Tier")),node.Tier);Set(p.FindPropertyRelative(F("Column")),node.Column);Set(p.FindPropertyRelative(F("MaxRanks")),node.Ranks);
                p.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=node.Skill.Length>0?skills[node.Skill]:null;
                p.FindPropertyRelative(F("Icon")).objectReferenceValue=node.Skill.Length>0?skills[node.Skill].Icon:Load<SkillData>("Skills/SD_KnifeToss.asset").Icon;
                p.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=p.FindPropertyRelative(F("Requirements"));req.ClearArray();
                if(node.Requirement.Length>0) {req.arraySize=1;Set(req.GetArrayElementAtIndex(0).FindPropertyRelative(F("NodeId")),"node.scout."+treeName+"."+node.Requirement);Set(req.GetArrayElementAtIndex(0).FindPropertyRelative(F("MinimumRanks")),1);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var scout=new SerializedObject(Load<CharacterClassData>("Classes/CCD_Scout.asset"));
        var trees=scout.FindProperty(F("SkillTrees"));trees.arraySize=2;
        trees.GetArrayElementAtIndex(0).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Scout_Assassin.asset");
        trees.GetArrayElementAtIndex(1).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Scout_BladeDancer.asset");
        scout.ApplyModifiedPropertiesWithoutUndo();
        var catalog=new SerializedObject(Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"));var entries=catalog.FindProperty(F("SkillDefinitions"));
        foreach(var skill in skills.Values)
        {
            var found=false;for(var i=0;i<entries.arraySize;i++)found|=entries.GetArrayElementAtIndex(i).objectReferenceValue==skill;
            if(!found){entries.InsertArrayElementAtIndex(entries.arraySize);entries.GetArrayElementAtIndex(entries.arraySize-1).objectReferenceValue=skill;}
        }
        catalog.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
    }
    static void RequireHit(SerializedProperty effect)
    {
        var conditions=effect.FindPropertyRelative("conditions");conditions.arraySize=1;
        var p=conditions.GetArrayElementAtIndex(0);
        Set(p.FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);
        Set(p.FindPropertyRelative("referencedEffectIndex"),0);
    }
}
