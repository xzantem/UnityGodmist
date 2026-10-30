using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        public CombatTooltipGlossary.Entry BuildCharacterValueDetails(CharacterInstance character,string id,string display)
        {
            if(character?.Stats==null || string.IsNullOrEmpty(id))return null;
            string title,body;
            if(id.StartsWith(CharacterStatValueLinks.StatPrefix,StringComparison.Ordinal) && Enum.TryParse(id.Substring(CharacterStatValueLinks.StatPrefix.Length),out CharacterStatType type) && Enum.IsDefined(typeof(CharacterStatType),type))
            {
                title=type==CharacterStatType.MinAttack?UiTerms.Attack:UiTerms.Stat(type,character.ResourcePool?.ResourceType??ResourceType.None);
                body=BuildCharacterStatDetails(character,type,display);
            }
            else if(id.StartsWith(CharacterStatValueLinks.ResistancePrefix,StringComparison.Ordinal)&&Enum.TryParse(id.Substring(CharacterStatValueLinks.ResistancePrefix.Length),out ResistanceType resistance)&&resistance!=ResistanceType.All&&Enum.IsDefined(typeof(ResistanceType),resistance))
            {
                title=UiTerms.Resistance(resistance);body=BuildCharacterResistanceDetails(character,resistance,display);
            }
            else return null;
            return new CombatTooltipGlossary.Entry{Id=id,Title=title,Description=body};
        }
        public string BuildCharacterStatDetails(CharacterInstance c,CharacterStatType type,string display=null)
        {
            var types=type==CharacterStatType.MinAttack?new[]{CharacterStatType.MinAttack,CharacterStatType.MaxAttack}:new[]{type};
            var calculation=CharacterStatExplanation.Create(c);
            var actual=types.Select(t=>c.Stats.GetStat(t).GetValue()).ToArray();
            if(type==CharacterStatType.MaxHealth)display=c.MaximumHealth.ToString(UiTerms.PolishCulture);
            display ??=CharacterVector(types,actual);
            if(types.Any(t=>!UnityEngine.Mathf.Approximately(calculation.Values[t],c.Stats.GetStat(t).GetValue())))return Row("Wartość bieżąca",display);
            var baseline=types.Select(t=>c.Stats.GetStat(t).BaseValue).ToArray();
            var body=new StringBuilder(Row(type==CharacterStatType.MinAttack?"Atak bazowy":"Baza",CharacterVector(types,baseline)));
            var rows=calculation.Rows.Where(r=>types.Contains(r.Stat)).OrderBy(r=>r.Stage).ToArray();
            var additive=rows.Where(r=>r.Stage==1).ToArray();var writtenPercent=false;var terms=0;
            foreach(var group in rows.GroupBy(r=>(r.Stage,r.Source,r.Operation,r.Stacking)))
            {
                var first=group.First();
                if(first.Stage==1)
                {
                    if(writtenPercent)continue;writtenPercent=true;
                    var percentages=types.Select(t=>additive.Where(r=>r.Stat==t).Aggregate(0f,(v,r)=>v+r.Value)).ToArray();
                    if(percentages.All(v=>v==0))continue;
                    var sources=new StringBuilder();
                    foreach(var source in additive.GroupBy(r=>r.Source))
                    {
                        var sourceValues=types.Select(t=>source.Where(r=>r.Stat==t).Sum(r=>r.Value)).ToArray();
                        sources.Append(Row(CharacterSource(c,source.Key),CharacterPercent(sourceValues,false)));
                    }
                    var multiplier=CharacterPercent(percentages,true);var link=LinkCalculation("Premie procentowe","Premie procentowe",sources+Row("Wynik",multiplier));
                    body.Append(Row(link,CharacterColored(multiplier,percentages)));terms++;continue;
                }
                var values=types.Select(t=>
                {
                    var matches=group.Where(r=>r.Stat==t).ToArray();
                    if(first.Operation==StatModifierOperation.Maximum)return matches.Length==0?float.NaN:matches.Min(r=>r.Value);
                    if(first.Operation==StatModifierOperation.Percent)return matches.Aggregate(1f,(v,r)=>v*UnityEngine.Mathf.Max(0,1+r.Value))-1;
                    return matches.Sum(r=>r.Value);
                }).ToArray();
                if(first.Source=="#minimum")body.Append(Row("Minimum",type==CharacterStatType.MaxHealth?"1":"0"));
                else
                {
                    var value=first.Operation==StatModifierOperation.Percent?CharacterPercent(values,true):CharacterVector(types,values,first.Operation!=StatModifierOperation.Maximum);
                    var label=first.Operation==StatModifierOperation.Maximum?"Limit — "+CharacterSource(c,first.Source):CharacterSource(c,first.Source);
                    if(first.Operation!=StatModifierOperation.Maximum)value=CharacterColored(value,values);
                    body.Append(Row(label,value));
                }
                terms++;
            }
            if(type==CharacterStatType.MaxHealth && actual[0]!=c.MaximumHealth)
                body.Append(Row("Wynik po zaokrągleniu",display));
            else if(terms>0 || CharacterVector(types,baseline)!=display)body.Append(Row("Wynik",display));
            return body.ToString();
        }
        static string CharacterVector(CharacterStatType[] types,float[] values,bool delta=false)
            =>string.Join("–",values.Select((v,i)=>
            {
                if(float.IsNaN(v))return "—";
                var probability=types[i] is CharacterStatType.CritChance or CharacterStatType.PhysicalDefensePenetration or CharacterStatType.MagicalDefensePenetration;
                var text=(probability?v*100:v).ToString(delta?"+0.##;-0.##;0":"0.##",UiTerms.PolishCulture);
                if(probability)return text+(delta?" pp":"%");
                return types[i]==CharacterStatType.CritDamage&&!delta?"×"+text:text;
            }));
        static string CharacterPercent(float[] percentages,bool multiplier)
        {
            if(percentages.All(v=>UnityEngine.Mathf.Approximately(v,percentages[0])))percentages=new[]{percentages[0]};
            return string.Join("–",percentages.Select(v=>multiplier?"×"+((1+v)*100).ToString("0.##",UiTerms.PolishCulture)+"%":(v*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+"%"));
        }
        static string CharacterColored(string text,float[] values)
        {
            if(values.All(v=>v>=0))return Colored(text,true);
            if(values.All(v=>v<=0))return Colored(text,false);
            return text; // Mixed ranges retain signs without misclassifying either endpoint.
        }
        string CharacterSource(CharacterInstance c,string key)
        {
            if(key=="#momentum")return UiTerms.Resource(ResourceType.Momentum);
            var effect=c.PassiveEffects?.FirstOrDefault(e=>e.EffectId==key);
            var item=(c.Equipment?.GetEquippedItems()??Array.Empty<EquipmentItemInstance>()).FirstOrDefault(i=>i!=null&&(!string.IsNullOrEmpty(i.InstanceId))&&(key=="item:"+i.InstanceId || effect?.Sources.Any(s=>s.SourceId?.Contains(i.InstanceId)==true)==true));
            if(item!=null)return LinkCalculation(item.DisplayName,item.DisplayName,BuildEquipmentCalculationDetails(item,c));
            foreach(var tree in c.ClassData?.SkillTrees??new())
            {
                if(tree.Id==key)return UiTerms.Present(tree.DisplayName);
                var node=tree.Nodes?.FirstOrDefault(n=>n.Id==key);
                if(node!=null)return UiTerms.Present(node.DisplayName);
            }
            return effect==null?UiTerms.Present(key):SourceName(new BattleUnit{Character=c},effect);
        }
        public string BuildCharacterResistanceDetails(CharacterInstance c,ResistanceType type,string display=null)
        {
            var trace=new CombatValueExplanation();var value=ModifyResistanceSystem.CalculateResistance(c,type,explanation:trace);
            var actual=type switch{ResistanceType.Bleed=>c.Resistances?.BleedResistance??0,ResistanceType.Poison=>c.Resistances?.PoisonResistance??0,ResistanceType.Fire=>c.Resistances?.FireResistance??0,ResistanceType.Weakening=>c.Resistances?.WeakeningResistance??0,ResistanceType.Suppression=>c.Resistances?.SuppressionResistance??0,_=>c.Resistances?.MoveResistance??0};
            display ??=FormatPercent(actual);
            if(!UnityEngine.Mathf.Approximately(value,actual))return Row("Wartość bieżąca",display);
            var body=new StringBuilder();var terms=0;
            foreach(var (key,text) in trace.Rows)
            {
                if(key=="raw")continue;
                if(key=="base"){body.Append(Row("Baza",Number(text,out var b)?FormatPercent(b):text));continue;}
                var amount=text.EndsWith("%",StringComparison.Ordinal)?text.Substring(0,text.Length-1)+" pp":Number(text,out var number)?(number*100).ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp":text;
                if(Number(amount.Replace(" pp",""),out var delta)&&delta==0)continue;
                body.Append(Row(CharacterSource(c,key),amount));terms++;
            }
            var raw=trace.Rows.FirstOrDefault(r=>r.Key=="raw");
            if(Number(raw.Value,out var rawValue)&&(rawValue<0||rawValue>1)){body.Append(Row("Limit",FormatPercent(value)));terms++;}
            if(terms>0)body.Append(Row("Wynik",display));
            return body.ToString();
        }
    }
}
