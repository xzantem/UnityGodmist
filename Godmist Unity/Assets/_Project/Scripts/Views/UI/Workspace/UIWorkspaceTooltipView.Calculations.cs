using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        private readonly Dictionary<string,CombatTooltipGlossary.Entry> calculationEntries = new();
        public string LinkCalculation(string value,string title,string body)
        {
            body=CompactCalculationBody(body);
            var id="calculation."+Hash128.Compute(title+"\n"+body);
            GetRootTooltip().calculationEntries[id]=new CombatTooltipGlossary.Entry {Id=id,Title=title,Description=body};
            return $"<link=\"{id}\"><color=#7EDBFF>{value}</color></link>";
        }
        private static string CompactCalculationBody(string body)
        {
            var lines=(body??string.Empty).Split('\n');
            var result=new StringBuilder();
            var seen=new HashSet<string>();
            foreach(var original in lines)
            {
                var line=original;
                if(line.StartsWith("Umiejętność<pos=",StringComparison.Ordinal))continue;
                var plain=PlainCalculationText(line);
                if(plain.Contains(" → "))
                {
                    // Equal authored/resolved values add no explanation.
                    var separator=line.IndexOf("<pos=",StringComparison.Ordinal);
                    if(separator>=0)
                    {
                        var end=line.IndexOf('>',separator);
                        var values=PlainCalculationText(line.Substring(end+1)).Split(new[]{" → "},StringSplitOptions.None);
                        if(values.Length==2 && values[0]==values[1])
                        {
                            var label=PlainCalculationText(line.Substring(0,separator)).Replace(" — baza → talenty","");
                            if(label=="Szansa")label="Szansa efektu";
                            line=Row(label,values[1]).TrimEnd('\n');
                        }
                    }
                }
                var column=line.IndexOf("<pos=",StringComparison.Ordinal);
                if(column>=0)
                {
                    var end=line.IndexOf('>',column);
                    line=Row(line.Substring(0,column),line.Substring(end+1)).TrimEnd('\n');
                    if(line.Length==0)continue;
                }
                if(!string.IsNullOrWhiteSpace(line) && !seen.Add(PlainCalculationText(line)))continue;
                result.Append(line).Append('\n');
            }
            return result.ToString().Trim();
        }
        private const string BonusColor = "#8ED59B";
        private const string PenaltyColor = "#F08C85";
        private const string ResultColor = "#E7C77B";
        private static string PlainCalculationText(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]*>", "");
        private static bool Number(string value,out float number) => float.TryParse(value,System.Globalization.NumberStyles.Float,UiTerms.PolishCulture,out number);
        private static string Colored(string value,bool positive) => $"<color={(positive ? BonusColor : PenaltyColor)}>{value}</color>";
        private static string Multiplier(string value)
        {
            value=value.TrimStart('×');
            return Number(value,out var number) ? "×"+(number*100).ToString("0.##",UiTerms.PolishCulture)+"%" : "×"+value;
        }
        private static string Row(string name,string value)
        {
            if(name.EndsWith(" ×",StringComparison.Ordinal))
            {
                name=name.Substring(0,name.Length-2);
                value=string.Join(" → ",value.Split(new[]{" → "},StringSplitOptions.None).Select(Multiplier));
            }
            // Effect observations may already include a plus sign before a negative value.
            value=value.Replace("+-","−");
            var plain=PlainCalculationText(value);
            var lower=name.ToLower(UiTerms.PolishCulture);
            var primary=lower is "wartość bazowa" or "wydane punkty" or "baza" or "wynik" or "obrażenia" or "leczenie" or "tarcza" or "koszt końcowy" or "koszt bazowy"
                || lower.StartsWith("aktualn",StringComparison.Ordinal) || lower.StartsWith("maksymaln",StringComparison.Ordinal) || lower.StartsWith("minimaln",StringComparison.Ordinal) || lower.StartsWith("atak",StringComparison.Ordinal) || lower.EndsWith(" — baza",StringComparison.Ordinal)
                || int.TryParse(lower,out _);
            // Zero-valued optional chances/bonuses add no information. A zero
            // outcome, base or actual zero multiplier still explains the result.
            if(!primary && System.Text.RegularExpressions.Regex.IsMatch(plain.Trim(),@"^[+−-]?0(?:[.,]0+)?(?:%| pp)?(?:–[+−-]?0(?:[.,]0+)?(?:%| pp)?)?$"))return string.Empty;
            var isCost=lower.Contains("koszt") || lower.Contains("otrzymywane obrażenia");
            if(lower is "wynik" or "obrażenia" or "leczenie" or "tarcza" or "koszt końcowy")
            {
                var uncolored=System.Text.RegularExpressions.Regex.Replace(value,@"</?color(?:=[^>]*)?>","");
                value=$"<color={ResultColor}><b>{uncolored}</b></color>";
            }
            else if(lower.Contains("bazow") || lower == "baza" || lower.EndsWith(" — baza",StringComparison.Ordinal))
                value=System.Text.RegularExpressions.Regex.Replace(value,@"</?color(?:=[^>]*)?>","");
            else if(!value.Contains("<color="))
            {
                if(plain.StartsWith("×") && Number(plain.Substring(1).TrimEnd('%'),out var multiplier))
                {
                    var neutral=plain.EndsWith("%")?100:1;
                    if(!Mathf.Approximately(multiplier,neutral) && lower!="obrażenia umiejętności" && !lower.Contains("współczynnik"))
                        value=Colored(value,(multiplier>neutral)!=isCost);
                }
                else if((plain.StartsWith("+")||plain.StartsWith("−")||plain.StartsWith("-")) && !plain.Contains("→"))
                    value=Colored(value,plain.StartsWith("+")!=isCost);
            }
            var labelLength=PlainCalculationText(name).Length;
            return labelLength>30 || plain.Length>22
                ? $"{name}\n<indent=8%>{value}</indent>\n" : $"{name}<pos=68%>{value}\n";
        }
        private static string ExplanationTable(CombatValueExplanation trace,BattleUnit caster=null,UIWorkspaceTooltipView tooltip=null)
        {
            var result=new StringBuilder();
            foreach(var (key,value) in trace.Rows)
            {
                if(key is "periodic_after_source" or "base_rounding" or "incoming_before_redirect" or "equipment_rounding" or "healing_power")continue;
                if(key is "parameter_rounding" or "warrior_cost" or "mage_cost")
                {
                    var index=trace.Rows.FindIndex(r=>r.Key==key);
                    if(index>0 && trace.Rows[index-1].Value==value)continue;
                }
                if(key=="damage_dealt" && caster?.Character?.PassiveEffects?.Any(e=>PassiveEffectLifecycleSystem.IsActive(e)
                    && e.GetPayload<VariableModifiersPayload>()?.Modifiers?.Any(m=>m.VariableType==GameplayVariableType.DamageDealtPercent)==true)==true
                    && Number(value,out var dealt) && Mathf.Approximately(dealt,new CombatVariableSystem().GetMultiplier(caster.Character,GameplayVariableType.DamageDealtPercent)))continue;
                var isMultiplier=key is "bonus_damage" or "coefficient" or "healing_coefficient" or "mage" or "scout" or "rage_multiplier" or "guaranteed_critical" or "damage_dealt" or "skill_modifier" or "weakening" or "equipment_outgoing" or "resource_equipment"
                    || key.StartsWith("node.") || key.StartsWith("tree.") || value.StartsWith("×");
                var numeric=value.TrimStart('×');
                if(isMultiplier && key!="coefficient" && key!="healing_coefficient" && Number(numeric,out var factor) && Mathf.Approximately(factor,1))continue;
                if(value.StartsWith("Nie uwzględniono"))continue;
                var label=key switch {
                    "periodic_after_source"=>"Obrażenia okresowe — baza", "healing_attack"=>"Atak — średnia", "healing_coefficient"=>"Współczynnik leczenia ×", "healing_power"=>"Siła leczenia", "resource_base"=>"Koszt bazowy", "parameter_rounding"=>"Po premiach do kosztu", "warrior_cost"=>"Po talentach Wojownika",
                    "mage_cost"=>"Po talentach Maga", "resource_equipment"=>"Koszt — wyposażenie ×", "equipment_rounding"=>"Po zaokrągleniu w górę", "resource_result"=>"Koszt końcowy",
                    "attack"=>"Atak postaci", "coefficient"=>"Obrażenia umiejętności ×", "bonus_damage"=>"Podwojenie obrażeń ×",
                    "base_rounding"=>"Po zaokrągleniu w górę", "mage"=>"Premie Maga ×", "scout"=>"Premie Zwiadowcy ×",
                    "rage_spent"=>"Wydawana furia", "rage_multiplier"=>"Premia za furię ×",
                    "guaranteed_critical"=>"Obrażenia krytyczne ×", "damage_dealt"=>"Zadawane obrażenia ×",
                    "skill_modifier"=>"Modyfikatory umiejętności ×", "weakening"=>"Osłabienie ×", "equipment_outgoing"=>"Premie wyposażenia ×", "incoming_before_redirect"=>"Po modyfikatorach", "result"=>"Obrażenia", _=>key};
                if(key.StartsWith("node.") || key.StartsWith("tree."))
                {
                    foreach(var tree in caster?.Character?.ClassData?.SkillTrees ?? new())
                    {
                        if(tree?.Id==key)label=UiTerms.Present(tree.DisplayName);
                        var node=tree?.Nodes?.FirstOrDefault(n=>n.Id==key);
                        if(node!=null)label=UiTerms.Present(node.DisplayName);
                    }
                }
                var runtime=caster?.Character?.PassiveEffects?.FirstOrDefault(e=>e.DefinitionId==key||e.EffectId==key);
                if(runtime!=null)label=SourceName(caster,runtime);
                var displayedValue=(key.StartsWith("node.")||key.StartsWith("tree."))
                    && float.TryParse(value,System.Globalization.NumberStyles.Float,UiTerms.PolishCulture,out _)?"×"+value:value;
                if(isMultiplier && !label.EndsWith(" ×",StringComparison.Ordinal))displayedValue=Multiplier(displayedValue);
                if(key=="attack")displayedValue=string.Join("–",value.Split('–').Select(v=>Number(v,out var n)?Mathf.RoundToInt(n).ToString():v));
                if(key=="attack" || key=="healing_attack")
                    label=tooltip?.LinkCalculation(label,"Atak postaci",SourceDetails(caster,CharacterStatType.MinAttack,CharacterStatType.MaxAttack)) ?? label;
                result.Append(Row(label,displayedValue));
            }

            return result.ToString();
        }
        private string DamageValue(SkillData skill,BattleUnit caster,SkillEffectData effect)
        {
            var trace=new CombatValueExplanation();
            var range=CombatSkillPreviewCalculator.CalculateSingleHitRange(caster,skill,effect,trace);
            if (!range.HasDamage) return FormatPercent(effect.DamageMultiplier)+" ataku";
            var details=ExplanationTable(trace,caster,this);
            return LinkCalculation(range.ToDisplayString(),"Obrażenia: "+UiTerms.Present(skill.DisplayName),details);
        }
        private static string SourceDetails(BattleUnit caster,params CharacterStatType[] stats)
        {
            if (caster?.Character==null) return string.Empty;
            var result=new StringBuilder();
            if(stats.Contains(CharacterStatType.MinAttack) && stats.Contains(CharacterStatType.MaxAttack))
            {
                var character=caster.Character;
                string Range(float min,float max) => min.ToString("0.##",UiTerms.PolishCulture)+"–"+max.ToString("0.##",UiTerms.PolishCulture);
                result.Append(Row("Atak bazowy",Range(character.Stats.MinAttack.BaseValue,character.Stats.MaxAttack.BaseValue)));
                foreach(var effect in character.PassiveEffects.Where(PassiveEffectLifecycleSystem.IsActive))
                {
                    var modifiers=effect.GetPayload<StatModifiersPayload>()?.Modifiers;
                    if(modifiers==null)continue;
                    // Pair endpoints only when their operation and stacking agree.
                    // Keep independent modifiers separate instead of combining their arithmetic.
                    foreach(var group in modifiers.Where(m=>m.StatType is CharacterStatType.MinAttack or CharacterStatType.MaxAttack).GroupBy(m=>(m.Operation,m.Stacking)))
                    {
                        var lows=group.Where(m=>m.StatType==CharacterStatType.MinAttack).ToArray();
                        var highs=group.Where(m=>m.StatType==CharacterStatType.MaxAttack).ToArray();
                        for(var i=0;i<Math.Max(lows.Length,highs.Length);i++)
                        {
                            var low=i<lows.Length?lows[i].Value:0;
                            var high=i<highs.Length?highs[i].Value:0;
                            var left=FormatStatusStatValue(CharacterStatType.MinAttack,group.Key.Operation,low);
                            var right=FormatStatusStatValue(CharacterStatType.MaxAttack,group.Key.Operation,high);
                            var value=left==right?left:left+"–"+right.TrimStart('+');
                            if(low*high<0)value=Colored(left,low>0)+"–"+Colored(right,high>0);
                            result.Append(Row(SourceName(caster,effect),value));
                        }
                    }
                }
                var armorNode=character.ClassData?.SkillTrees?.SelectMany(t=>t.Nodes).FirstOrDefault(n=>n.Id=="node.warrior.brawler.spiked_armor");
                if(armorNode!=null && WarriorCombatSystem.B(caster,"spiked_armor")>0)
                {
                    var without=Godmist.Systems.Characters.SkillTreeRankPreview.Create(character,armorNode,0);
                    var low=character.Stats.MinAttack.GetValue()-without.Stats.MinAttack.GetValue();var high=character.Stats.MaxAttack.GetValue()-without.Stats.MaxAttack.GetValue();
                    result.Append(Row(UiTerms.Present(armorNode.DisplayName),low.ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+"–"+high.ToString("0.##",UiTerms.PolishCulture)));
                }
                result.Append(Row("Wynik",Range(character.Stats.MinAttack.GetValue(),character.Stats.MaxAttack.GetValue())));
                return result.ToString();
            }
            foreach(var type in stats.Distinct())
            {
                var stat=caster.Character.Stats.GetStat(type);
                result.Append(Row(UiTerms.Stat(type,caster.Character.ResourcePool?.ResourceType ?? ResourceType.None)+" — baza",stat.BaseValue.ToString("0.##",UiTerms.PolishCulture)));
                foreach(var effect in caster.Character.PassiveEffects.Where(PassiveEffectLifecycleSystem.IsActive))
                {
                    foreach(var modifier in effect.GetPayload<StatModifiersPayload>()?.Modifiers ?? new())
                        if(modifier.StatType==type)
                            result.Append(Row(SourceName(caster,effect),FormatStatusStatValue(type,modifier.Operation,modifier.Value)));
                }
                result.Append(Row(UiTerms.Stat(type)+" — wynik",stat.GetValue().ToString("0.##",UiTerms.PolishCulture)));
            }
            return result.ToString();
        }
        private static string SourceName(BattleUnit caster,PassiveEffectRuntime effect)
        {
            if(!string.IsNullOrWhiteSpace(effect.DisplayName)) return UiTerms.Present(effect.DisplayName);
            foreach(var tree in caster.Character.ClassData?.SkillTrees ?? new())
            {
                var node=tree?.Nodes?.FirstOrDefault(n=>effect.Sources.Any(s=>s.SourceId.Contains(n.Id)));
                if(node!=null)return UiTerms.Present(node.DisplayName);
                if(tree!=null && effect.Sources.Any(s=>s.SourceId.Contains(tree.Id)))return UiTerms.Present(tree.DisplayName);
            }
            foreach(var item in caster.Character.Equipment?.GetEquippedItems() ?? Array.Empty<Godmist.Runtime.Items.EquipmentItemInstance>())
            {
                foreach(var stone in item?.SocketedGaldurytes ?? new())
                    if(!string.IsNullOrEmpty(stone.InstanceId)&&effect.Sources.Any(s=>s.SourceId.Contains(stone.InstanceId)))return stone.DisplayName;
                if(item!=null && !string.IsNullOrEmpty(item.InstanceId) && effect.Sources.Any(s=>s.SourceId.Contains(item.InstanceId)))return item.DisplayName;
            }
            return HeroCombatStatusText.Name(effect.EffectId) ?? effect.EffectId;
        }
        private string DescribeRicochetCritical(SkillData skill,BattleUnit caster)
        {
            var mode=PassiveEffectCombatExecutionSystem.ResolveMode(caster,skill,skill.TargetModes[0]);
            var table=Row(LinkCalculation("Szansa krytyczna postaci","Szansa krytyczna",SourceDetails(caster,CharacterStatType.CritChance)),FormatPercent(caster?.Character?.Stats?.CritChance.GetValue()??0))
                +Row("Za kolejny cel","+10 pp")+"Cel<pos=68%>Szansa krytyczna\n";
            if(CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit))table=Row("Gwarantowany krytyk","100%")+"Cel<pos=68%>Szansa krytyczna\n";
            for(var i=0;i<mode.ChainTargets;i++)table+=Row((i+1).ToString(),FormatPercent(CombatDamageSystem.PreviewUntargetedCriticalChance(caster,skill,i)));
            table+="\n"+LinkRule("Warunki wyliczenia","Rykoszet — kolejność celów","Premia wynika z kolejności celów w łańcuchu. Chybienie poprzedniego celu jej nie usuwa. Premie zależne od konkretnego przeciwnika są wyliczane w podglądzie wskazanego celu.");
            return "Szansa krytyczna wzrasta o "+LinkCalculation("10 pp","Rykoszet — szansa krytyczna",table)+" przy każdym kolejnym celu łańcucha.";
        }

        private string AccuracyHeader(SkillData skill,BattleUnit caster)
        {
            if(skill?.Effects?.Any(e=>e!=null && !e.AlwaysHits && e.Polarity==EffectPolarity.Negative
                && (e.EffectType!=EffectType.DealDamage || !CombatDamageSystem.IsGuaranteedHitDamageType(e.DamageType)))!=true) return string.Empty;
            var accuracy=PassiveEffectCombatExecutionSystem.ResolveParameter(caster,skill,"accuracy",skill.SkillAccuracy);
            var value=accuracy.ToString("0.##",UiTerms.PolishCulture);
            return "<size=85%>Celność: "+LinkCalculation(value,"Celność",Row("Celność umiejętności",skill.SkillAccuracy.ToString("0.##",UiTerms.PolishCulture))
                +(Mathf.Approximately(skill.SkillAccuracy,accuracy)?"":Row("Celność po talentach",value))+Row("Celność broni",(caster?.Character?.WeaponAccuracy??100).ToString("0.##",UiTerms.PolishCulture))
                +Row("Wynik",FormatValue(CombatDamageSystem.CalculateAccuracy(caster?.Character?.WeaponAccuracy??100,accuracy)))
                +"\nSzansa trafienia uwzględnia również unik wskazanego przeciwnika.")+"</size>\n\n";
        }
    }
}
