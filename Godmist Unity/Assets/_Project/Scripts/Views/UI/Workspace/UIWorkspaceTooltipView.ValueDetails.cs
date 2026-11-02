using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Core.Constants;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        private const string NumericTokenPattern = @"<link=[""'][^>]*>.*?</link>|<[^>]*>|[+−-]?\d+(?:[.,]\d+)?(?:[–]\d+(?:[.,]\d+)?)?(?:%| pp)?";
        internal string LinkRule(string label,string title,string body)
        {
            var id="detail."+UnityEngine.Hash128.Compute(title+"\n"+body);
            GetRootTooltip().calculationEntries[id]=new CombatTooltipGlossary.Entry{Id=id,Title=title,Description=UiTerms.Present(body)};
            return $"<link=\"{id}\"><color=#7EDBFF>{label}</color></link>";
        }
        internal string GlobalPassiveDetails(SkillTreeDefinition tree,int points,ResourceType resource)
            => "Wydane punkty: "+BaseValue(points.ToString(),"Wydane punkty", "Wydane punkty")+"\n\n"+AnnotateGlobalValues(tree,points,resource)
                +"\n\nPremia za punkt:\n"+AnnotateGlobalValues(tree,1,resource)
                +(tree.Id is WarriorCombatSystem.Commander or WarriorCombatSystem.Brawler or PaladinCombatSystem.Priest?"\n"+LinkRule("Zasady działania",UiTerms.Present(tree.DisplayName),tree.Id switch
                    {WarriorCombatSystem.Commander=>"Ułamkowe obrażenia zachowują resztę do kolejnych trafień.",WarriorCombatSystem.Brawler=>"Podstawa leczenia: faktycznie wydana furia. Niepełne HP zachowuje resztę do kolejnych wydatków.",_=>"Podstawa zwrotu: faktycznie przywrócone zdrowie, bez nadmiarowego leczenia. Zwrot jest zaokrąglany w dół."}):"");
        private string BaseValue(string value,string title,string label="Wartość bazowa")
            => LinkCalculation(value,title,Row(label,value));
        private string ChangedValue(string value,string title,string baseline,string source,string contribution)
            => LinkCalculation(value,title,baseline==value?Row("Wartość bazowa",value):Row("Baza",baseline)
                +(string.IsNullOrWhiteSpace(contribution)?"":Row(source,contribution))+Row("Wynik",value));
        private static string TalentName(BattleUnit caster,string id)
            => UiTerms.Present(caster?.Character?.ClassData?.SkillTrees?.Where(t=>t!=null).SelectMany(t=>t.Nodes).FirstOrDefault(n=>n.Id==id)?.DisplayName ?? "Talenty");
        private string TalentPercent(BattleUnit caster,string title,float baseline,float result,string id)
            => ChangedValue(FormatPercent(result),title,FormatPercent(baseline),TalentName(caster,id),
                ((result-baseline)*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp");
        private static string TurnValue(int turns)=>turns==1?"1 tura":FormatTurnCount(turns);
        private string FixedTurns(int turns,string title)=>BaseValue(FormatTurnCount(turns),title,"Czas bazowy");
        private string EffectDuration(SkillData skill,BattleUnit caster,SkillEffectData effect)
            => LinkEffectParameter(skill,caster,effect,"duration",FormatTurnCount(effect.DurationTurns));
        private string ShieldValue(BattleUnit caster,SkillEffectData effect)
        {
            var result=CombatShieldSystem.CalculateSkillShield(caster,effect);
            var average=caster?.Character?.Stats==null?0:CombatDamageValueCalculator.CalculateDamageValue(caster,DamageValueSource.AverageAttack,1);
            return LinkCalculation(result.ToString(),"Tarcza",Row(LinkCalculation("Atak — średnia","Atak postaci",SourceDetails(caster,CharacterStatType.MinAttack,CharacterStatType.MaxAttack)),average.ToString())
                +Row("Współczynnik tarczy","×"+FormatPercent(effect.ShieldAverageAttackMultiplier))
                +(effect.BaseValue==0?"":Row("Baza tarczy",effect.BaseValue.ToString()))+Row("Wynik",result.ToString()));
        }
        private string PeriodicValue(BattleUnit caster,SkillEffectData effect)
        {
            var trace=new CombatValueExplanation();
            var damage=HeroTalentEffectValues.InitialPeriodicDamage(caster,effect,effect.DurationTurns,trace);
            return LinkCalculation(damage.ToString(),UiTerms.Status(effect.StatusId)+" — obrażenia na turę",
                Row(LinkCalculation("Atak — średnia","Atak postaci",SourceDetails(caster,CharacterStatType.MinAttack,CharacterStatType.MaxAttack)),
                    caster?.Character?.Stats==null?"—":CombatDamageValueCalculator.CalculateDamageValue(caster,DamageValueSource.AverageAttack,1).ToString())
                +Row("Obrażenia umiejętności","×"+FormatPercent(effect.StatusDamageMultiplier))
                +ExplanationTable(trace,caster,this)+Row("Wynik",damage.ToString()));
        }
        private string OfferingValue(BattleUnit caster,bool incoming)
        {
            var bonus=MageCombatSystem.OfferingBonus(caster);
            var calm=incoming?1-.1f*MageCombatSystem.W(caster,"calm"):1;
            var result=bonus*calm;var title=incoming?UiTerms.Variable(GameplayVariableType.DamageReceivedPercent):UiTerms.Variable(GameplayVariableType.DamageDealtPercent);
            var body=Row("Baza","30%");
            if(!UnityEngine.Mathf.Approximately(bonus,.3f))body+=Row(TalentName(caster,"node.mage.warlock.superhuman_offering"),Colored(((bonus-.3f)*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp",!incoming));
            if(calm!=1)body+=Row(TalentName(caster,"node.mage.warlock.calm"),Colored("×"+FormatPercent(calm),true));
            return LinkCalculation(FormatPercent(result),title,body+Row("Wynik",FormatPercent(result)));
        }
        private string HealingCriticalValue(BattleUnit caster,bool single)
        {
            var chance=PaladinCombatSystem.HealingCritChance(caster,single);
            var body=Row(LinkCalculation("Szansa krytyczna postaci","Szansa krytyczna",SourceDetails(caster,CharacterStatType.CritChance)),FormatPercent(caster.Character.Stats.CritChance.GetValue()))
                +Row("Współczynnik: "+TalentName(caster,"node.paladin.priest.unexpected_grace"),"×"+FormatPercent(.5f*PaladinCombatSystem.P(caster,"unexpected_grace")));
            if(single && PaladinCombatSystem.P(caster,"critical_focus")>0)body+=Row(TalentName(caster,"node.paladin.priest.critical_focus"),"×"+FormatPercent(1+.5f*PaladinCombatSystem.P(caster,"critical_focus")));
            return LinkCalculation(FormatPercent(chance),"Szansa krytyczna leczenia",body+Row("Wynik",FormatPercent(chance)));
        }
        private string WeakPointDamage(BattleUnit caster)
        {
            var value=.2f+.05f*ScoutCombatSystem.AssassinRank(caster,"critical_point");
            return ChangedValue(value.ToString("0.00",UiTerms.PolishCulture),UiTerms.Stat(CharacterStatType.CritDamage),"0,20",TalentName(caster,"node.scout.assassin.critical_point"),
                (.05f*ScoutCombatSystem.AssassinRank(caster,"critical_point")).ToString("+0.##;-0.##;0",UiTerms.PolishCulture));
        }
        private string ProtectionLimit()
        {
            var body=new StringBuilder();for(var n=0;n<=3;n++)body.Append(Row("Przylegli sojusznicy: "+n,(30+10*n)+"%"));
            return LinkCalculation("60%","Rozkaz Ochrony — limit redukcji",body.ToString());
        }
        private string LifeStealValue(BattleUnit caster,float coefficient,bool single)
        {
            var result=PaladinCombatSystem.LifeStealCoefficient(caster,coefficient,single);
            return LinkCalculation(FormatPercent(result),"Leczenie z zadanych obrażeń",Row("Bazowy udział obrażeń",FormatPercent(coefficient))
                +(single && PaladinCombatSystem.P(caster,"healer_focus")>0?Row(TalentName(caster,"node.paladin.priest.healer_focus"),"×"+FormatPercent(PaladinCombatSystem.SingleTargetHealingMultiplier(caster))):"")
                +Row("Wynik",FormatPercent(result)));
        }
        private string SacrificeGain(BattleUnit caster,SkillData skill)
        {
            var mode=PassiveEffectCombatExecutionSystem.ResolveMode(caster,skill,skill.TargetModes[0]);
            var body=Row("Za jednego sojusznika","20%");for(var n=1;n<=Math.Max(1,mode.ChainTargets);n++)body+=Row("Wybrani sojusznicy: "+n,(20*n)+"%");
            return LinkCalculation("20%","Przejęcie Mocy — zadawane obrażenia",body);
        }
        private string DirectDamageValue(BattleUnit caster,float coefficient,string value)
        {
            var trace=new CombatValueExplanation();CombatSkillPreviewCalculator.CalculateDirectDamageRange(caster,DamageType.Magical,coefficient,trace);
            return LinkCalculation(value,"Obrażenia Pola Osądu",ExplanationTable(trace,caster,this));
        }
        private string BonusDamageLink(SkillData skill,BattleUnit caster,SkillEffectData effect)
        {
            var trace=new CombatValueExplanation();CombatSkillPreviewCalculator.CalculateSingleHitRange(caster,skill,effect,trace,bonusProc:true);
            return LinkCalculation("podwojenie obrażeń każdego trafienia","Podwójne obrażenia: "+UiTerms.Present(skill.DisplayName),ExplanationTable(trace,caster,this));
        }
        private string TiringValues()
            => $"{LinkRule("Trafienia umiejętności","Wycieńczające Zaklęcia — zasady",MageTiringSpellSystem.HitDescription)} mają {BaseValue(FormatPercent(MageTiringSpellSystem.BaseChance),"Wycieńczające Zaklęcia — szansa nałożenia")} szansy na zmniejszenie szybkości celu o {BaseValue(FormatValue(Math.Abs(MageTiringSpellSystem.SpeedPerStack)),"Wycieńczające Zaklęcia — szybkość za nałożenie")} na {FixedTurns(MageTiringSpellSystem.DurationTurns,"Wycieńczające Zaklęcia — czas trwania")}, do {BaseValue(MageTiringSpellSystem.MaximumStacks.ToString(),"Wycieńczające Zaklęcia — limit nałożeń")} nałożeń.";
        private string DischargeValue(BattleUnit caster)
        {
            var spent=Math.Max(WarriorDischargeRules.MinimumRage,WarriorDischargeRules.Cost(caster));
            return LinkCalculation("1%","Wyładowanie — obrażenia za furię",Row("Wydawana furia",spent.ToString())+Row("Premia za punkt furii","1%")
                +Row("Mnożnik obrażeń","×"+FormatPercent(WarriorDischargeRules.Multiplier(spent))));
        }
        private static string ActionCostDetails(SkillData skill,BattleUnit caster,string result,bool recovery)
        {
            var body=Row("Koszt bazowy",skill.ActionCost+"% PA");
            if(recovery)
            {
                var multiplier=skill.SkillId=="skill.scout.hook" && ScoutCombatSystem.BladeRank(caster,ScoutCombatSystem.EfficientRecoveryNode)>0?.4f:skill.KnifeRecoveryCostMultiplier;
                var resolved=PassiveEffectCombatExecutionSystem.ResolveParameter(caster,skill,"knife_recovery_cost_multiplier",multiplier);
                if(resolved!=1)body+=Row("Koszt — "+(skill.SkillId=="skill.scout.hook" && ScoutCombatSystem.BladeRank(caster,ScoutCombatSystem.EfficientRecoveryNode)>0?TalentName(caster,"node.scout.blade_dancer.efficient_recovery"):"odzysk sztyletu"),"×"+FormatPercent(resolved));
            }
            var order=UniqueEquipmentCombatRules.OrderCostMultiplier(caster,skill);
            if(order!=1)body+=Row("Koszt — wyposażenie","×"+FormatPercent(order));
            if(caster?.MaxActionPoints>0)body+=Row("Maksymalne PA",Godmist.Runtime.Combat.ActionPointUnits.Format(caster.MaxActionPoints));
            return body==Row("Koszt bazowy",skill.ActionCost+"% PA") && result==skill.ActionCost+"% PA"?Row("Koszt końcowy",result):body+Row("Koszt końcowy",result);
        }
        private static string ResourceCostDetails(SkillData skill,BattleUnit caster,CombatValueExplanation trace,bool recovery=false)
        {
            var result=trace.Rows.LastOrDefault(r=>r.Key=="resource_result").Value;
            var body=Row("Koszt bazowy",skill.ResourceCost.ToString());
            var equipment=false;var equipmentRows=new StringBuilder();var equipmentCount=0;
            foreach(var (key,value) in trace.Rows)
            {
                if(key=="mage_cost"){equipment=true;continue;}
                if(key is "resource_base" or "parameter_rounding" or "warrior_cost" or "resource_equipment" or "equipment_rounding" or "resource_result")continue;
                var name=key.StartsWith("node.")?TalentName(caster,key):UiTerms.Present(key);
                var row=Row("Koszt — "+name,equipment && value.StartsWith("+")?"-"+value.Substring(1):value);
                if(equipment){equipmentRows.Append(row);equipmentCount++;}else body+=row;
            }
            if(skill.ResourceType==ResourceType.Mana)
            {
                var mastery=MageCombatSystem.W(caster,"mastery");
                if(mastery>0)body+=Row("Koszt — "+TalentName(caster,"node.mage.warlock.mastery"),(-5*mastery).ToString());
                var points=MageCombatSystem.Points(caster?.Character,MageCombatSystem.Warlock);
                if(points>0)body+=Row("Koszt — Czarnoksiężnik","×"+FormatPercent(Math.Max(0,1-points*MageCombatSystem.ManaCostReductionPerPoint)));
                if(MageCombatSystem.W(caster,"magic_saving")>0 && caster.Character.CurrentHealth<caster.Character.Stats.MaxHealth.GetValue()*.5f)
                    body+=Row("Koszt — "+TalentName(caster,"node.mage.warlock.magic_saving"),"×80%");
            }
            if(skill.ResourceType==ResourceType.Rage && PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill) && WarriorCombatSystem.B(caster,"furious_strikes")>0)
                body+=Row("Koszt — "+TalentName(caster,"node.warrior.brawler.furious_strikes"),"×"+FormatPercent(1-.1f*WarriorCombatSystem.B(caster,"furious_strikes")));
            body+=equipmentRows.ToString();
            if(equipmentCount>1)body+=Row("Koszt — wyposażenie razem","×"+FormatPercent(float.Parse(trace.Rows.First(r=>r.Key=="resource_equipment").Value,UiTerms.PolishCulture)));
            if(recovery)
            {
                var multiplier=skill.SkillId=="skill.scout.hook" && ScoutCombatSystem.BladeRank(caster,ScoutCombatSystem.EfficientRecoveryNode)>0?.4f:skill.KnifeRecoveryCostMultiplier;
                var resolved=PassiveEffectCombatExecutionSystem.ResolveParameter(caster,skill,"knife_recovery_cost_multiplier",multiplier);
                if(resolved!=1)body+=Row("Koszt — "+(skill.SkillId=="skill.scout.hook" && ScoutCombatSystem.BladeRank(caster,ScoutCombatSystem.EfficientRecoveryNode)>0?TalentName(caster,"node.scout.blade_dancer.efficient_recovery"):"odzysk sztyletu"),"×"+FormatPercent(resolved));
            }
            return body==Row("Koszt bazowy",skill.ResourceCost.ToString())?Row("Koszt końcowy",result??"0"):body+Row("Koszt końcowy",result??"0");
        }
        // This decorates geometry only. It never guesses an effect calculation
        // from a number's spelling or shares one bag of effects across numbers.
        private string GeometryValues(string text,SkillData skill,BattleUnit caster)
        {
            if(!IsHeroSkill(skill))return text;
            var ordinal=0;
            return Regex.Replace(text,NumericTokenPattern,m=>{
                if(m.Value.StartsWith("<"))return m.Value;
                var prefix=text.Substring(0,m.Index);
                var line=prefix.Substring(Math.Max(prefix.LastIndexOf('\n'),prefix.LastIndexOf(';'))+1);
                var title=line.Contains("Zasięg")?"Zasięg umiejętności":line.Contains("łańcuch")&&!line.Contains("zasięgu")?"Liczba celów łańcucha":line.Contains("kwadrat")||line.Contains("prostokąt")?"Wymiar obszaru":"Zasięg obszaru";
                var modes=skill.TargetModes?.Where(a=>a!=null).ToArray() ?? Array.Empty<SkillTargetModeData>();
                var current=modes.Select(a=>PassiveEffectCombatExecutionSystem.ResolveMode(caster,skill,a)).ToArray();
                var before=title=="Liczba celów łańcucha"?modes.FirstOrDefault()?.ChainTargets.ToString():null;
                var body=Row("Wartość wynikowa",m.Value);
                if(title=="Zasięg umiejętności" && modes.Length>0)
                {
                    var min=modes.Min(a=>a.AbsoluteRange?.MinRange??0);var max=modes.Max(a=>a.AbsoluteRange?.MaxRange??0);
                    var low=current.Min(a=>a.AbsoluteRange?.MinRange??0);var high=current.Max(a=>a.AbsoluteRange?.MaxRange??0);
                    var baseline=FormatRange(min,max);var result=FormatRange(low,high);
                    body=Row("Zasięg bazowy",baseline)+(baseline==result?"":Row("Zasięg po talentach",result));
                }
                else if(before!=null && before!=m.Value)body=Row("Baza",before)+Row(TalentName(caster,"node.scout.assassin.knife_bounce"),"+"+2*ScoutCombatSystem.AssassinRank(caster,"knife_bounce"))+Row("Wynik",m.Value);
                else if(title=="Wymiar obszaru" && skill.SkillId=="skill.paladin.judgement_field")
                {
                    var rank=PaladinCombatSystem.C(caster,"field_range");body=Row("Promień bazowy","2 pola");
                    if(rank>0)body+=Row(TalentName(caster,"node.paladin.crusader.field_range"),"+"+rank);
                    body+=Row("Średnica","2 × "+(2+rank)+" + 1")+Row("Wynik",m.Value);
                }
                else if(title=="Zasięg obszaru" && modes.Length>0)
                {
                    var relative=modes[0].TargetType is TargetType.AreaAroundTile or TargetType.Chain;
                    var authored=relative?modes[0].RelativeRange:modes[0].AbsoluteRange;
                    var resolved=relative?current[0].RelativeRange:current[0].AbsoluteRange;
                    if(authored!=null && resolved!=null)
                    {
                        var baseline=FormatRange(authored);var final=FormatRange(resolved);
                        body=Row("Zasięg bazowy",baseline);
                        if(baseline!=final)body+=Row(TalentName(caster,"node.scout.assassin.farsight"),"+"+(resolved.MaxRange-authored.MaxRange))+Row("Wynik",final);
                    }
                }
                ordinal++;return LinkCalculation(m.Value,title,body);
            });
        }
        internal string AnnotateTalentValues(string text,SkillTreeNodeDefinition node,int rank,CharacterInstance character,Func<int,string> describe)
        {
            var previous=rank>1?Regex.Matches(describe(rank-1),NumericTokenPattern).Cast<Match>().Where(m=>!m.Value.StartsWith("<")).Select(m=>m.Value).ToArray():Array.Empty<string>();
            var templates=SkillTreeTooltipCopy.Descriptions.TryGetValue(node.Id,out var copies)?copies:Array.Empty<string>();
            var fixedValues=templates.Select(t=>Regex.Matches(t.Replace("{X–Y}","0–0").Replace("{X}","0").Replace("{furia}","0"),NumericTokenPattern)
                .Cast<Match>().Where(m=>!m.Value.StartsWith("<")).Select(m=>m.Value).ToArray()).ToArray();
            var ordinal=0;
            return Regex.Replace(text,NumericTokenPattern,m=>{
                if(m.Value.StartsWith("<"))return m.Value;
                var role=NumericRole(text,m);
                var resolved=ResolvedTalentValue(node,rank,character,m.Value,ordinal);
                if(resolved!=null){ordinal++;return resolved;}
                if(role is not ("Obrażenia" or "Leczenie") && fixedValues.Length>0 && fixedValues.All(v=>ordinal<v.Length&&v[ordinal]==m.Value))
                {
                    ordinal++;return BaseValue(m.Value,UiTerms.Present(node.DisplayName)+" — "+role.ToLower(UiTerms.PolishCulture));
                }
                var body=Row("Ranga talentu",rank+"/"+node.EffectiveMaxRanks);
                if(ordinal<previous.Length && previous[ordinal]!=m.Value)body+=Row("Poprzednia ranga",previous[ordinal]);
                body+=Row("Wynik",m.Value);ordinal++;
                // Build-dependent damage/healing gets its actual attack input,
                // while thresholds and durations remain their own scalar values.
                if((role=="Obrażenia"||role=="Leczenie") && character?.Stats!=null)
                {
                    var caster=new BattleUnit{Character=character};
                    body=Row(LinkCalculation("Atak postaci","Atak postaci",SourceDetails(caster,CharacterStatType.MinAttack,CharacterStatType.MaxAttack)),
                        FormatValue(character.Stats.MinAttack.GetValue())+"–"+FormatValue(character.Stats.MaxAttack.GetValue()))+body;
                }
                body=TalentBuildDetails(node,rank,character,role,m.Value,body);
                return LinkCalculation(m.Value,UiTerms.Present(node.DisplayName)+" — "+role.ToLower(UiTerms.PolishCulture),body);
            });
        }
        internal string AnnotateGlobalValues(SkillTreeDefinition tree,int points,ResourceType resource)
        {
            var text=SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree,points,resource);
            var one=Regex.Matches(SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree,1,resource),NumericTokenPattern).Cast<Match>().Where(m=>!m.Value.StartsWith("<")).Select(m=>m.Value).ToArray();
            var ordinal=0;
            return Regex.Replace(text,NumericTokenPattern,m=>{
                if(m.Value.StartsWith("<"))return m.Value;
                var index=ordinal++;
                var roles=tree.Id switch {
                    PaladinCombatSystem.Priest=>new[]{"Siła leczenia","Zwrot zdrowia"},
                    PaladinCombatSystem.Crusader=>new[]{"Obrażenia przeciw nieumarłym i demonom"},
                    MageCombatSystem.Warlock=>new[]{"Koszt many","Obrażenia przeciw jednemu przeciwnikowi"},
                    MageCombatSystem.Fire=>new[]{"Obrażenia od ognia",UiTerms.Resistance(ResistanceType.Fire)},
                    ScoutCombatSystem.AssassinTree=>new[]{UiTerms.Stat(CharacterStatType.CritChance),"Obrażenia w walce wręcz"},
                    ScoutCombatSystem.BladeTree=>new[]{"Szansa zachowania sztyletów","Obrażenia od zatrucia"},
                    WarriorCombatSystem.Commander=>new[]{UiTerms.Stat(CharacterStatType.CritChance),"Obrażenia nieuchronne","Ulepszenie broni"},
                    WarriorCombatSystem.Brawler=>new[]{UiTerms.Stat(CharacterStatType.MaxResource,ResourceType.Rage),"Leczenie za wydaną furię"},
                    _=>new[]{"Premia pasywu"}};
                var role=index<roles.Length?roles[index]:"Wartość odniesienia";
                if(tree.Id==WarriorCombatSystem.Commander&&index==2)return BaseValue(m.Value,"Ulepszenie broni — wartość odniesienia");
                var body=Row("Wydane punkty",points.ToString());
                if(index<one.Length)body+=Row("Wartość przy jednym punkcie",one[index]);
                return LinkCalculation(m.Value,UiTerms.Present(tree.DisplayName)+" — "+role.ToLower(UiTerms.PolishCulture),body+Row("Wynik",m.Value));
            });
        }
        private string ResolvedTalentValue(SkillTreeNodeDefinition node,int rank,CharacterInstance character,string display,int ordinal)
        {
            if(character==null)return null;var source=new BattleUnit{Character=character};
            SkillData Skill(string id)=>character.Skills.Concat(character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).FirstOrDefault(s=>s?.SkillId==id);
            string Parameter(string skillId,EffectType type,string key,CharacterStatType? stat=null)
            {
                var skill=Skill(skillId);var raw=skill?.Effects.FirstOrDefault(e=>e.EffectType==type && (!stat.HasValue || e.ModifierStatType==stat));
                return raw==null?null:LinkEffectParameter(skill,source,PassiveEffectCombatExecutionSystem.ResolveEffect(source,skill,raw),key,display);
            }
            switch(node.Id)
            {
                case "node.scout.blade_dancer.fine_thrusts":return Parameter("skill.scout.quick_strikes",EffectType.DealDamage,"bonus_chance");
                case "node.warrior.brawler.force_moment":return Parameter("skill.warrior.whirlwind",EffectType.DealDamage,"hit_count");
                case "node.warrior.commander.broken_bones":return Parameter("skill.warrior.immobilize",EffectType.ApplyStatus,"duration");
                case "node.scout.blade_dancer.brew_strength":
                    var tonic=Skill("skill.scout.tonic");var rawTonic=tonic?.Effects.FirstOrDefault(e=>e.EffectType==EffectType.ModifyStat && (ordinal<2?e.ModifierValue>0:e.ModifierValue<0));
                    return rawTonic==null?null:LinkEffectParameter(tonic,source,PassiveEffectCombatExecutionSystem.ResolveEffect(source,tonic,rawTonic),ordinal%2==0?"modifier":"duration",display);
                case "node.scout.blade_dancer.viruses":return Parameter("skill.scout.poisoned_dagger",EffectType.ModifyResistance,ordinal==0?"modifier":"duration");
                case "node.mage.fire.pyromania":return Parameter(ordinal==0?"skill.mage.fire_aura":"skill.mage.fireball",ordinal==0?EffectType.MageFireAura:EffectType.ApplyStatus,"chance");
                case "node.mage.warlock.superhuman_offering":return OfferingValue(source,ordinal>0);
                case "node.mage.warlock.calm":return OfferingValue(source,true);
                case "node.scout.assassin.critical_point":return WeakPointDamage(source);
                case "node.scout.assassin.keen_eye":
                    return TalentPercent(source,"Szansa na Głębokie Wbicie",.15f,ScoutCombatSystem.KnifeEmbeddingChance(source),node.Id);
                case "node.scout.blade_dancer.series":
                case "node.scout.blade_dancer.dance_of_doom":
                    if(display.EndsWith("%"))return BaseValue(display,"Szansa dodatkowego ciosu");
                    var quick=Skill("skill.scout.quick_strikes");if(quick==null)return null;
                    var parent=character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).FirstOrDefault(n=>n.Id=="node.scout.blade_dancer.series");
                    var minimum=node.Requirements.Where(r=>r.NodeId==parent?.Id).Select(r=>r.MinimumRanks).DefaultIfEmpty(0).Max();
                    var assumed=parent!=null&&ScoutCombatSystem.BladeRank(source,"series")<minimum;
                    var projected=assumed?new BattleUnit{Character=SkillTreeRankPreview.Create(character,parent,minimum)}:source;
                    var rawQuick=quick.Effects.First(e=>e.EffectType==EffectType.DealDamage);
                    var link=LinkEffectParameter(quick,projected,PassiveEffectCombatExecutionSystem.ResolveEffect(projected,quick,rawQuick),"hit_trials",display);
                    if(!assumed)return link;
                    var id=Regex.Match(link,@"calculation\.[a-z0-9]+").Value;var entry=GetRootTooltip().calculationEntries[id];
                    return LinkCalculation(display,entry.Title,entry.Description+"\n"+Row("Wymagana ranga: "+UiTerms.Present(parent.DisplayName),minimum.ToString()));
                case "node.warrior.brawler.rage":
                    return LinkCalculation(display,"Cięcie — generowanie furii",Row("Bazowy przyrost",RageResourceRule.PreviewOffensiveGain(character).ToString())
                        +Row(UiTerms.Present(node.DisplayName),"+"+HeroTalentEffectValues.CutRageBonus(rank))+Row("Wynik",display));
                case "node.scout.blade_dancer.efficient_recovery":
                    var hook=Skill("skill.scout.hook");if(hook==null)return null;
                    if(ordinal==0)return LinkCalculation(display,"Koszt PA — odzysk sztyletu",ActionCostDetails(hook,source,display.TrimEnd('%')+"% PA",true));
                    var trace=new CombatValueExplanation();PassiveEffectCombatExecutionSystem.ResolveResourceCost(source,hook,true,trace);
                    return LinkCalculation(display,"Koszt impetu — odzysk sztyletu",ResourceCostDetails(hook,source,trace,true));
                case "node.scout.assassin.farsight":
                case "node.scout.assassin.knife_bounce":
                    var ricochet=Skill("skill.scout.ricochet");if(ricochet==null)return null;
                    var raw=ricochet.TargetModes[0];var mode=PassiveEffectCombatExecutionSystem.ResolveMode(source,ricochet,raw);
                    if(node.Id.EndsWith("knife_bounce"))return ChangedValue(display,"Liczba celów Rykoszetu",raw.ChainTargets.ToString(),UiTerms.Present(node.DisplayName),"+"+(mode.ChainTargets-raw.ChainTargets));
                    var before=ordinal==0?raw.AbsoluteRange.MaxRange:raw.RelativeRange.MaxRange;var after=ordinal==0?mode.AbsoluteRange.MaxRange:mode.RelativeRange.MaxRange;
                    return ChangedValue(display,ordinal==0?"Zasięg Rykoszetu":"Odległość celów Rykoszetu",before.ToString(),UiTerms.Present(node.DisplayName),"+"+(after-before));
            }
            return null;
        }
        private string TalentBuildDetails(SkillTreeNodeDefinition node,int rank,CharacterInstance character,string role,string value,string fallback)
        {
            if(character?.Stats==null || role is not ("Obrażenia" or "Leczenie"))return fallback;
            var source=new BattleUnit{Character=character};var coefficient=0f;var turns=3;var status=StatusEffectIds.Bleed;
            switch(node.Id)
            {
                case "node.scout.assassin.running_attack":
                    var trace=new CombatValueExplanation();CombatSkillPreviewCalculator.CalculateSingleHitRange(source,null,SkillEffectData.ForDamage(DamageType.Physical,DamageValueSource.AttackRange,HeroTalentEffectValues.RunningAttackMultiplier(rank)),trace);
                    return ExplanationTable(trace,source,this);
                case "node.paladin.crusader.judgement_release":
                    var direct=new CombatValueExplanation();CombatSkillPreviewCalculator.CalculateDirectDamageRange(source,DamageType.Magical,HeroTalentEffectValues.JudgementReleaseMultiplier(rank),direct);
                    return ExplanationTable(direct,source,this);
                case "node.warrior.brawler.conflagration":coefficient=HeroTalentEffectValues.ConflagrationMultiplier(rank);status=StatusEffectIds.Burn;turns=2;break;
                case "node.warrior.commander.deep_wounds":coefficient=HeroTalentEffectValues.DeepWoundsMultiplier(rank);break;
                case "node.scout.assassin.sharp_knife":coefficient=HeroTalentEffectValues.KnifeBleedMultiplier(rank);break;
                case "node.scout.blade_dancer.combo_master":coefficient=HeroTalentEffectValues.ComboBleedMultiplier(rank);break;
                case "node.scout.blade_dancer.venom":
                    var skill=character.Skills.Concat(character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).FirstOrDefault(s=>s?.SkillId=="skill.scout.poisoned_dagger");
                    if(skill==null)return fallback;
                    var poison=skill.Effects.First(e=>e.EffectType==EffectType.ApplyStatus);
                    var resolved=PassiveEffectCombatExecutionSystem.ResolveEffect(source,skill,poison);var poisonTrace=new CombatValueExplanation();
                    HeroTalentEffectValues.InitialPeriodicDamage(source,resolved,resolved.DurationTurns,poisonTrace);
                    return AttackAverageRow(source,true)+Row("Obrażenia umiejętności","×"+FormatPercent(resolved.StatusDamageMultiplier))+ExplanationTable(poisonTrace,source,this)+Row("Wynik",value);
                case "node.paladin.priest.healing_aura":
                    return AttackAverageRow(source)+Row("Współczynnik leczenia","×"+FormatPercent(rank==1?.07f:.15f))
                        +(PaladinCombatSystem.Points(character,PaladinCombatSystem.Priest)>0?Row("Kapłan","×"+FormatPercent(1+.01f*PaladinCombatSystem.Points(character,PaladinCombatSystem.Priest))):"")+Row("Wynik",value);
                default:return fallback;
            }
            var body=AttackAverageRow(source,true)+Row("Obrażenia umiejętności","×"+FormatPercent(coefficient));
            if(StatusEffectSystem.HasStatus(source,StatusEffectIds.Weakening))body+=Row("Osłabienie","×80%");
            if(status==StatusEffectIds.Burn)
            {
                var trace=new CombatValueExplanation();MageCombatSystem.CalculateInitialBurnTotal(source,StatusEffectSystem.CalculateStatusDamagePerTurn(source,status,coefficient)*turns,trace);body+=ExplanationTable(trace,source,this);
            }
            return body+Row("Wynik",value);
        }
        private string AttackAverageRow(BattleUnit source,bool rounded=false)
            => Row(LinkCalculation("Atak — średnia","Atak postaci",SourceDetails(source,CharacterStatType.MinAttack,CharacterStatType.MaxAttack)),
                rounded?CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AverageAttack,1).ToString():FormatValue((source.Character.Stats.MinAttack.GetValue()+source.Character.Stats.MaxAttack.GetValue())*.5f));
        private static string NumericRole(string text,Match match)
        {
            var after=text.Substring(match.Index+match.Length);
            if(Regex.IsMatch(after,@"^\s*(?:tury|turę|tur|turach|turze)\b"))return "Czas trwania";
            if(Regex.IsMatch(after,@"^\s*(?:pola|pól|pole)\b"))return "Zasięg";
            if(Regex.IsMatch(after,@"^\s*nałoż"))return "Nałożenia";
            if(Regex.IsMatch(after,@"^\s*(?:dodatkowe|dodatkowy)?\s*(?:cios|trafi)"))return "Liczba trafień";
            if(after.TrimStart().StartsWith("za każdy",StringComparison.Ordinal))return "Współczynnik";
            if(Regex.IsMatch(after,@"^\s*punkt(?:u|y|ów)? średniego ataku"))return "Atak — wartość odniesienia";
            if(after.StartsWith(" obrażeń",StringComparison.Ordinal))return "Obrażenia";
            if(after.StartsWith(" zdrowia",StringComparison.Ordinal))return "Leczenie";
            var before=text.Substring(0,match.Index);var start=Math.Max(before.LastIndexOf('.'),Math.Max(before.LastIndexOf(','),before.LastIndexOf(';')));
            var clause=before.Substring(start+1).ToLower(UiTerms.PolishCulture);
            if(after.TrimStart().StartsWith("szans",StringComparison.Ordinal)||clause.Contains("szans"))return clause.Contains("szansę krytyczną")||clause.Contains("szansy krytycznej")||clause.Contains("szansa krytyczna")?UiTerms.Stat(CharacterStatType.CritChance):"Szansa nałożenia";
            if(clause.Contains("mniej niż")||clause.Contains("poniżej")||clause.Contains("co najmniej")||clause.Contains("ponad"))return "Próg działania";
            if(clause.Contains("koszt"))return "Koszt";
            if(clause.Contains("obrażeń krytycznych"))return UiTerms.Stat(CharacterStatType.CritDamage);
            if(clause.Contains("obraż"))return "Modyfikator obrażeń";
            if(clause.Contains("zdrow"))return UiTerms.Stat(CharacterStatType.MaxHealth);
            if(clause.Contains("szybko"))return UiTerms.Stat(CharacterStatType.Speed);
            if(clause.Contains("odporność"))return "Odporność";
            if(clause.Contains("unik"))return UiTerms.Stat(CharacterStatType.Dodge);
            if(clause.Contains("zasięg")||clause.Contains("odległość"))return "Zasięg";
            if(clause.Contains("regenerac"))return "Regeneracja";
            if(clause.Contains("atak"))return "Współczynnik ataku";
            if(clause.Contains("punkt")||after.Contains("za każdy"))return "Współczynnik";
            if(clause.Contains("maksymaln")||clause.Contains("poniżej")||clause.Contains("co najmniej")||clause.Contains("ponad"))return "Próg działania";
            if(clause.Contains("cios")||clause.Contains("trafi"))return "Liczba trafień";
            return "Parametr";
        }
    }
}
