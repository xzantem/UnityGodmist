using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        private static bool IsHeroSkill(SkillData skill) => skill?.SkillId?.StartsWith("skill.warrior.")==true
            || skill?.SkillId?.StartsWith("skill.scout.")==true || skill?.SkillId?.StartsWith("skill.mage.")==true
            || skill?.SkillId?.StartsWith("skill.paladin.")==true || skill?.SkillId is "skill.knife_toss" or "skill.battle_cry";

        private string DescribeReviewedEffect(SkillData skill,BattleUnit caster,SkillEffectData effect)
        {
            if(!IsHeroSkill(skill) || skill.SkillId=="skill.warrior.bandage")return null;
            // These effects exist only because of a talent; the authored zero is
            // not an active zero-strength debuff.
            if(skill.SkillId=="skill.scout.poisoned_dagger" && effect.EffectType==EffectType.ModifyResistance
                && ScoutCombatSystem.BladeRank(caster,"viruses")==0)return string.Empty;
            string Percent(float v)=>FormatPercent(v);
            string Turns(int t)=>FixedTurns(t,"Czas trwania: "+UiTerms.Present(skill.DisplayName));
            string F(string value,string title)=>BaseValue(value,title);
            string Param(string key,string value)=>LinkEffectParameter(skill,caster,effect,key,value);
            var duration=effect.DurationTurns>0 && effect.EffectType is not (EffectType.Push or EffectType.Pull) ? " na "+LinkEffectParameter(skill,caster,effect,"duration",FormatTurnCount(effect.DurationTurns)) : "";
            var sourceChance=effect.EffectType is EffectType.ApplyStatus or EffectType.MageFireAura ? StatusEffectSystem.CalculateSourceStatusChance(caster,effect.StatusId,effect.BaseChance) : effect.BaseChance;
            var chance=sourceChance<.9999f ? LinkEffectParameter(skill,caster,effect,"chance",Percent(sourceChance))+" szansy na " : "";
            var hit=!effect.AlwaysHits && effect.Polarity==EffectPolarity.Negative && effect.EffectType!=EffectType.DealDamage
                || effect.Conditions?.Any(c=>c!=null && c.ConditionType==EffectConditionType.ReferencedEffectSucceeded)==true;
            var prefix=hit?"Przy trafieniu: ":"";
            string text;
            switch(effect.EffectType)
            {
                case EffectType.DealDamage:
                    var value=DamageValue(skill,caster,effect);
                    text=effect.DamageHitCount>1 ? $"Wykonuje {Param("hit_count",effect.DamageHitCount.ToString())} {(effect.DamageHitCount<=4?"trafienia":"trafień")} po {value} obrażeń {GetDamageTypeDisplayName(effect.DamageType)} każde"
                        :$"Zadaje {value} obrażeń {GetDamageTypeDisplayName(effect.DamageType)}";
                    if(effect.BonusDamageMultiplierChance>0)
                        text+=$", z {Param("bonus_chance",Percent(effect.BonusDamageMultiplierChance))} szansy na "+(Mathf.Approximately(effect.BonusDamageMultiplier,2)?BonusDamageLink(skill,caster,effect):$"obrażenia {F("×"+Percent(effect.BonusDamageMultiplier),"Mnożnik dodatkowych obrażeń")} każdego trafienia");
                    if(effect.BonusPenetration>0)text+=$" z dodatkową {(effect.DamageType==DamageType.Magical?"penetracją magiczną":"penetracją fizyczną")} {Param("penetration",FormatValue(effect.BonusPenetration*100)+" pp")}";
                    if(effect.LifeStealPercent>0)text+=$" i przywraca zdrowie równe {LifeStealValue(caster,effect.LifeStealPercent,true)} zadanych obrażeń";
                    if(effect.AllyLifeStealPercent>0)text+=$" i przywraca zdrowie równe {F(Percent(effect.AllyLifeStealPercent),"Udział leczenia sojuszników")} zadanych obrażeń";
                    if(effect.ResourceDrainPercent>0)text+=$" i odbiera celowi {F(Percent(effect.ResourceDrainPercent),"Odbieranie zasobu")} zadanych obrażeń jako zasób";
                    if(effect.AdditionalHitTrials>0)text+=$". Może wykonać {Param("hit_trials",effect.AdditionalHitTrials.ToString())} {(effect.AdditionalHitTrials==1?"dodatkowy cios":"dodatkowe ciosy")}, każdy z niezależną szansą {F(Percent(effect.AdditionalHitChance),"Szansa dodatkowego ciosu")}";
                    break;
                case EffectType.ApplyStatus:
                    if(skill.SkillId=="skill.warrior.shout")text=$"{Param("chance",Percent(sourceChance))} szansy na ogłuszenie na {F("1","Czas ogłuszenia — mniej niż próg furii")} turę. Przy zużyciu co najmniej {F("50","Próg furii dla dłuższego ogłuszenia")} furii: {F("2","Czas ogłuszenia — od progu furii")} tury";
                    else if(IsDamageOverTime(effect.StatusId))
                    {
                        var number=PeriodicValue(caster,effect);
                        text=prefix+chance+GetStatusDisplayName(effect.StatusId).ToLower(UiTerms.PolishCulture)+$" zadające {number} obrażeń na turę przez {Turns(effect.DurationTurns)}";
                    }
                    else if(effect.StatusId==StatusEffectIds.Invisibility)text=$"Zapewnia niewidzialność{duration}";
                    else text=prefix+chance+GetStatusDisplayName(effect.StatusId).ToLower(UiTerms.PolishCulture)+duration;
                    break;
                case EffectType.MarkWeakPoint:
                    text=$"{Param("chance",Percent(effect.BaseChance))} szansy na zwiększenie szansy krytycznej przeciw celowi o {F("10 pp","Słaby Punkt — szansa krytyczna")} i obrażeń krytycznych o {WeakPointDamage(caster)}{duration}";break;
                case EffectType.MarkForDestruction:
                    text=$"{Param("chance",Percent(effect.BaseChance))} szansy na zwiększenie otrzymywanych obrażeń o {TalentPercent(caster,"Rozkaz Unicestwienia — otrzymywane obrażenia",.2f,.2f+.05f*WarriorCombatSystem.C(caster,"breakdown"),"node.warrior.commander.breakdown")}{duration}";break;
                case EffectType.ProtectAlly:
                    text=$"Zmniejsza otrzymywane obrażenia na {Turns(effect.DurationTurns)} o {F("30%","Rozkaz Ochrony — redukcja bazowa")} i o dodatkowe {F("10 pp","Rozkaz Ochrony — premia za sojusznika")} za każdego przyległego sojusznika, maksymalnie o {ProtectionLimit()}";break;
                case EffectType.GrantFreeMovement:
                    if(WarriorCombatSystem.C(caster,"strategy")==0)return string.Empty;
                    text=$"Wszyscy sojusznicy otrzymują po {F("2","Darmowe kroki na turę")} darmowe kroki na turę przez {Turns(3)}";break;
                case EffectType.ModifyActionValue when skill.SkillId=="skill.warrior.regroup":text=$"Przyspiesza następny ruch celu o {F("100%","Rozkaz Gotowości — przyspieszenie ruchu")}";break;
                case EffectType.ModifyStat:
                    if(effect.ModifierStatType is not (CharacterStatType.Dodge or CharacterStatType.WeaponAccuracy or CharacterStatType.Speed))
                    {
                        text=prefix+(chance.Length>0?LinkEffectParameter(skill,caster,effect,"chance",Percent(sourceChance))+" szansy: ":"")
                            +FormatStatusStatModifier(effect.ModifierStatType,effect.ModifierOperation,effect.ModifierValue,EffectResource(skill,effect,caster))+duration;
                        break;
                    }
                    var name=effect.ModifierStatType switch { CharacterStatType.Dodge=>"unik", CharacterStatType.WeaponAccuracy=>"celność",CharacterStatType.Speed=>"szybkość",_=>UiTerms.Stat(effect.ModifierStatType).ToLower(UiTerms.PolishCulture) };
                    var increase=effect.ModifierValue>=0;
                    var amount=effect.ModifierOperation==StatModifierOperation.Percent?Percent(Mathf.Abs(effect.ModifierValue)):FormatValue(Mathf.Abs(effect.ModifierValue));
                    var grammaticalName=chance.Length>0 ? name switch { "unik"=>"uniku", "celność"=>"celności", "szybkość"=>"szybkości", _=>name } : name;
                    amount=LinkEffectParameter(skill,caster,effect,"modifier",amount);
                    text=prefix+(chance.Length>0?chance+(increase?"zwiększenie ":"zmniejszenie "):(increase?"Zwiększa ":"Zmniejsza "))+grammaticalName+" o "+amount+duration;
                    if(effect.DelayCasterTurns>0)text=$"Po {F(effect.DelayCasterTurns.ToString(),"Opóźnienie efektu")} "+(effect.DelayCasterTurns==1?"turze ":"turach ")+char.ToLower(text[0],UiTerms.PolishCulture)+text.Substring(1);
                    break;
                case EffectType.ModifyVar:
                    var variable=UiTerms.Variable(effect.ModifierVariableType).ToLower(UiTerms.PolishCulture);
                    text=prefix+(effect.ModifierValue>=0?"Zwiększa ":"Zmniejsza ")+variable+" o "+Percent(Mathf.Abs(effect.ModifierValue))+(skill.IsToggle?"":duration);break;
                case EffectType.ModifyResistance:
                    text=prefix+chance+(effect.ModifierValue<0?"zmniejszenie ":"zwiększenie ")+UiTerms.Resistance(effect.ResistanceType).ToLower(UiTerms.PolishCulture)+" o "+FormatValue(Mathf.Abs(effect.ModifierValue)*100)+" pp"+duration;break;
                case EffectType.Push:
                case EffectType.Pull:
                    var push=effect.EffectType==EffectType.Push;
                    text=prefix+chance+(chance.Length>0?(push?"odepchnięcie":"przyciągnięcie"):(push?"Odpycha":"Przyciąga"))+" cel"+(chance.Length>0?"u":"")+" o "+LinkEffectParameter(skill,caster,effect,"distance",FormatDistance(effect.DisplacementDistance));
                    if(skill.SkillId=="skill.paladin.grounding_wave")text=$"Przyciąga wrogów o {LinkEffectParameter(skill,caster,effect,"distance",FormatDistance(effect.DisplacementDistance))} w kierunku środka wybranego obszaru";
                    break;
                case EffectType.Teleport:text=$"Teleportuje na wybrane pole w zasięgu {F(effect.TeleportDestinationRange.MaxRange.ToString(),"Zasięg teleportacji")+" "+(effect.TeleportDestinationRange.MaxRange==1?"pola":"pól")}";break;
                case EffectType.SwapPositions:text=$"{Param("chance",Percent(effect.BaseChance))} szansy na zamianę miejsc z celem";break;
                case EffectType.MageSacrifice:text=$"Zmniejsza zadawane obrażenia wybranych sojuszników o {LinkCalculation("20%","Przejęcie Mocy — zadawane obrażenia sojusznika",Row("Zmiana bazowa","-20%"))} na {Turns(3)} i zwiększa własne o {SacrificeGain(caster,skill)} {LinkRule("za każdego z nich","Przejęcie Mocy — zasady","Każdy odrębny sojusznik daje jedną premię. Ponowne użycie na tym samym sojuszniku zastępuje poprzedni efekt i odświeża czas.")}";break;
                case EffectType.MageRitual:text=$"Poświęca {F("10%","Rytuał Odnowy — poświęcane zdrowie")} maksymalnego zdrowia celu, pozostawiając co najmniej {F("1","Minimalne pozostawione zdrowie")} HP, następnie przywraca łącznie {F(Percent(effect.PercentageValue),"Rytuał Odnowy — odzysk maksymalnego zdrowia celu")} jego maksymalnego zdrowia przez {Turns(effect.DurationTurns)}";break;
                case EffectType.MageFireAura:
                    var dot=PeriodicValue(caster,effect);
                    text=$"Otacza cel aurą ognia na {FixedTurns(3,"Czas trwania Ognistej Aury")}. Aura ma {Param("chance",Percent(sourceChance))} szansy na podpalenie przyległych wrogów, zadające {dot} obrażeń na turę przez {Turns(effect.DurationTurns)}";break;
                case EffectType.SacrificeHealthForResource:text=$"Poświęca {F(Percent(effect.HealthSacrificePercent),"Przemiana Witalności — poświęcane zdrowie")} maksymalnego zdrowia i przywraca {F(FormatValue(effect.ResourceRestorePerSacrificedHealth),"Mana za punkt poświęconego zdrowia")} many za każdy poświęcony punkt zdrowia";break;
                case EffectType.Shield:
                    text=$"Zapewnia {ShieldValue(caster,effect)} {LinkRule("tarczy","Magiczna Tarcza — zasady","Tury odbiorcy, odliczane na końcu jego tury. Ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas.")}{duration}";break;
                case EffectType.BlockResourceRegeneration:text="Blokuje regenerację many";break;
                case EffectType.PaladinHeal:
                    if(caster?.Character?.Stats==null)return "Przywraca zdrowie.";
                    var single=PaladinCombatSystem.IsSingleHealingPreview(caster.WarriorState.Session,caster,null,skill);
                    var range=PaladinCombatSystem.HealingPowerRange(caster,skill,effect,single);
                    var healingTrace=new CombatValueExplanation();
                    PaladinCombatSystem.CalculateHealing(caster,skill,effect,single,healingTrace);
                    var heal=range.Minimum==range.Maximum?range.Minimum.ToString():$"{range.Minimum}–{range.Maximum}";
                    text="Przywraca "+LinkCalculation(heal,"Siła leczenia",ExplanationTable(healingTrace,caster,this)
                        +Row("Losowe odchylenie",Percent(PaladinCombatSystem.HealingVariation))+Row("Leczenie",heal)
                        +(PaladinCombatSystem.HealingCritChance(caster,single)>0?Row("Szansa krytyczna leczenia",HealingCriticalValue(caster,single)):"")
                        )+" zdrowia";break;
                case EffectType.PaladinRegeneration:
                    text=$"Przywraca łącznie {F("40%","Święta Regeneracja — udział maksymalnego zdrowia celu")} maksymalnego zdrowia celu przez {Turns(4)}";
                    if(PaladinCombatSystem.P(caster,"haste")>0)text+=$". Przywraca natychmiast {TalentPercent(caster,"Święta Regeneracja — leczenie natychmiastowe",0,PaladinCombatSystem.P(caster,"haste") switch {1=>.15f,2=>.25f,_=>.4f},"node.paladin.priest.haste")} tej sumy";break;
                case EffectType.PaladinBlessing:text=$"Zwiększa zadawane i zmniejsza otrzymywane obrażenia o {TalentPercent(caster,"Błogosławieństwo — siła premii",.1f,PaladinCombatSystem.BlessingStrength(caster),"node.paladin.priest.strong_blessing")} na {Turns(4)}";break;
                case EffectType.CreateObject when skill.SkillId=="skill.paladin.judgement_field":
                    var field=CombatSkillPreviewCalculator.CalculateDirectDamageRange(caster,DamageType.Magical,.5f);
                    text=$"Tworzy na {Turns(4)} pole, które zmniejsza szybkość wrogów o {F("5","Pole Osądu — zmniejszenie szybkości")} i zadaje {DirectDamageValue(caster,.5f,field.ToDisplayString())} obrażeń magicznych";break;
                case EffectType.ApplyUniqueBuff when effect.UniqueBuffType==UniqueBuffType.MageMaximumDamage:
                    text=$"Ataki zadają maksymalne obrażenia przez {EffectDuration(skill,caster,effect)}";break;
                case EffectType.ApplyUniqueBuff when effect.UniqueBuffType==UniqueBuffType.MageOffering:
                    var bonus=MageCombatSystem.OfferingBonus(caster);
                    var penalty=bonus*(1-.1f*MageCombatSystem.W(caster,"calm"));
                    text=Mathf.Approximately(bonus,penalty)?$"Zwiększa zadawane i otrzymywane obrażenia o {OfferingValue(caster,false)}":$"Zwiększa zadawane obrażenia o {OfferingValue(caster,false)} i otrzymywane obrażenia o {OfferingValue(caster,true)}";break;
                default:return null;
            }
            if(effect.EffectType is EffectType.ModifyStat or EffectType.ModifyVar or EffectType.ModifyResistance)
            {
                var modifier=effect.EffectType switch
                {
                    EffectType.ModifyStat=>FormatStatusStatValue(effect.ModifierStatType,effect.ModifierOperation,effect.ModifierValue),
                    EffectType.ModifyResistance=>FormatValue(Mathf.Abs(effect.ModifierValue)*100)+" pp",
                    _=>Percent(Mathf.Abs(effect.ModifierValue))
                };
                // ModifyStat's concise sentence already links its unsigned value.
                if(effect.EffectType!=EffectType.ModifyStat || effect.ModifierStatType is not (CharacterStatType.Dodge or CharacterStatType.WeaponAccuracy or CharacterStatType.Speed))
                    text=Regex.Replace(text,@"<link=[""'][^>]*>.*?</link>|<[^>]*>|"+Regex.Escape(modifier),
                        m=>m.Value.StartsWith("<")?m.Value:LinkEffectParameter(skill,caster,effect,"modifier",modifier));
            }
            // Dedicated links explain the hovered parameter; the generic catalog
            // fallback below is retained only for effect types not yet specialized.
            if(effect.EffectType is EffectType.ModifyStat or EffectType.ModifyVar or EffectType.ModifyResistance or EffectType.Push or EffectType.Pull)
                return text.TrimEnd('.')+".";
            return text.TrimEnd('.')+".";
        }
        private string LinkEffectParameter(SkillData skill,BattleUnit caster,SkillEffectData effect,string parameter,string displayed)
        {
            var authored=skill.Effects.FirstOrDefault(e=>e.EffectType==effect.EffectType && e.TargetModeIndex==effect.TargetModeIndex
                && e.DelayCasterTurns==effect.DelayCasterTurns && e.StatusId==effect.StatusId && e.ModifierStatType==effect.ModifierStatType
                && e.ModifierVariableType==effect.ModifierVariableType && e.UniqueBuffType==effect.UniqueBuffType) ?? effect;
            float Read(SkillEffectData e)=>parameter switch {"chance"=>e.BaseChance,"duration"=>e.DurationTurns,"distance"=>e.DisplacementDistance,
                "hit_count"=>e.DamageHitCount,"hit_trials"=>e.AdditionalHitTrials,"bonus_chance"=>e.BonusDamageMultiplierChance,
                "penetration"=>e.BonusPenetration,"dot_coefficient"=>e.StatusDamageMultiplier,_=>e.ModifierValue};
            string Format(SkillEffectData e)=>parameter switch {
                "chance" or "bonus_chance"=>FormatPercent(Read(e)),"duration"=>TurnValue(e.DurationTurns),"distance"=>FormatDistance(e.DisplacementDistance),
                "penetration"=>FormatValue(e.BonusPenetration*100)+" pp","dot_coefficient"=>"×"+FormatPercent(e.StatusDamageMultiplier),
                "hit_count" or "hit_trials"=>FormatValue(Read(e)),
                _=>e.EffectType switch {EffectType.ModifyStat=>FormatStatusStatValue(e.ModifierStatType,e.ModifierOperation,e.ModifierValue),
                    EffectType.ModifyResistance=>(e.ModifierValue*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp",_=>FormatStatusVariableValue(e.ModifierVariableType,e.ModifierValue)}};
            var statName=effect.EffectType==EffectType.ModifyStat?UiTerms.Stat(effect.ModifierStatType):effect.EffectType==EffectType.ModifyResistance?UiTerms.Resistance(effect.ResistanceType)
                :effect.EffectType==EffectType.ModifyVar?UiTerms.Variable(effect.ModifierVariableType):effect.EffectType is EffectType.ApplyStatus or EffectType.MageFireAura?UiTerms.Status(effect.StatusId):UiTerms.Present(skill.DisplayName);
            var title=parameter switch {"chance"=>"Szansa nałożenia: "+statName,"duration"=>"Czas trwania: "+statName,
                "distance"=>effect.EffectType==EffectType.Push?"Odległość odepchnięcia":"Odległość przyciągnięcia",
                "hit_count"=>"Podstawowe trafienia: "+statName,"hit_trials"=>"Dodatkowe ciosy: "+statName,
                "bonus_chance"=>"Szansa podwójnych obrażeń","penetration"=>UiTerms.Stat(effect.DamageType==DamageType.Magical?CharacterStatType.MagicalDefensePenetration:CharacterStatType.PhysicalDefensePenetration),
                "dot_coefficient"=>"Współczynnik obrażeń okresowych",_=>statName+" — zmiana"};
            var rows=new StringBuilder();
            var scout=caster?.Character==null?authored:ScoutCombatSystem.ResolveEffect(caster,skill,authored);
            var warrior=caster?.Character==null?scout:WarriorCombatSystem.ResolveEffect(caster,skill,scout);
            var mage=caster?.Character==null?warrior:MageCombatSystem.ResolveEffect(caster,skill,warrior);
            void Step(SkillEffectData before,SkillEffectData after,string id)
            {
                var delta=Read(after)-Read(before);if(UnityEngine.Mathf.Approximately(delta,0))return;
                var percent=parameter is "chance" or "bonus_chance" or "penetration";
                var amount=(percent?delta*100:delta).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+(percent?" pp":"");
                if(parameter=="dot_coefficient" || parameter=="modifier" && effect.ModifierOperation==StatModifierOperation.Percent)
                    amount=(delta*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp";
                if(effect.EffectType==EffectType.ModifyVar && effect.ModifierVariableType==GameplayVariableType.DamageReceivedPercent)amount=Colored(amount,delta<0);
                rows.Append(Row(TalentName(caster,id),amount));
            }
            var scoutId=skill.SkillId switch {"skill.scout.poisoned_dagger"=>parameter=="modifier"?"node.scout.blade_dancer.viruses":"node.scout.blade_dancer.venom",
                "skill.scout.tonic"=>"node.scout.blade_dancer.brew_strength",_=>parameter=="bonus_chance"?"node.scout.blade_dancer.fine_thrusts":"node.scout.blade_dancer.series"};
            if(parameter=="hit_trials" && skill.SkillId=="skill.scout.quick_strikes")
            {
                var series=ScoutCombatSystem.BladeRank(caster,"series");var dance=series>0?ScoutCombatSystem.BladeRank(caster,"dance_of_doom"):0;
                if(series>0)rows.Append(Row(TalentName(caster,"node.scout.blade_dancer.series"),"+"+series));
                if(dance>0)rows.Append(Row(TalentName(caster,"node.scout.blade_dancer.dance_of_doom"),"+"+dance));
            }
            else Step(authored,scout,scoutId);
            Step(scout,warrior,skill.SkillId switch {"skill.warrior.whirlwind"=>"node.warrior.brawler.force_moment","skill.warrior.immobilize"=>"node.warrior.commander.broken_bones",_=>"node.warrior.brawler.desperation"});
            Step(warrior,mage,skill.SkillId=="skill.mage.magic_focus"?"node.mage.warlock.master_concentration":parameter=="dot_coefficient"?"node.mage.fire.burning_aura":parameter=="penetration"?"node.mage.fire.piercing":"node.mage.fire.pyromania");
            var trace=new CombatValueExplanation();var index=-1;for(var i=0;i<skill.Effects.Count;i++)if(ReferenceEquals(skill.Effects[i],authored)){index=i;break;}
            var key=parameter switch {"chance"=>"status_chance","duration"=>"duration_turns","hit_count"=>"hit_count","hit_trials"=>"additional_hit_trials",_=>null};
            if(key!=null)PassiveEffectCombatExecutionSystem.ResolveParameter(caster,skill,key,Read(mage),index,explanation:trace);
            foreach(var (source,value) in trace.Rows)
            {
                var amount=value;if(parameter=="chance" && Number(value,out var delta))amount=(delta*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp";
                rows.Append(Row(source.StartsWith("node.")?TalentName(caster,source):UiTerms.Present(source),amount));
            }
            var result=Format(effect);
            if(parameter=="chance" && effect.EffectType is EffectType.ApplyStatus or EffectType.MageFireAura)
            {
                var equipment=new CombatValueExplanation();result=FormatPercent(StatusEffectSystem.CalculateSourceStatusChance(caster,effect.StatusId,effect.BaseChance,equipment));
                foreach(var (source,value) in equipment.Rows)rows.Append(Row(source,value));
            }
            var baseline=Format(authored);
            // The effect description conveys increase/decrease; details show its magnitude.
            // Only contributions modifying that base receive benefit/penalty colors.
            if(parameter=="modifier")
            {baseline=baseline.TrimStart('+','-','−');result=result.TrimStart('+','-','−');}
            var body=baseline!=result || rows.Length>0?Row("Baza",baseline)+rows+Row("Wynik",result)
                :Row(parameter switch {"chance"=>"Szansa bazowa","duration"=>"Czas bazowy","distance"=>"Odległość bazowa",_=>"Wartość bazowa"},result);
            return LinkCalculation(displayed,title,body);
        }
    }
}
