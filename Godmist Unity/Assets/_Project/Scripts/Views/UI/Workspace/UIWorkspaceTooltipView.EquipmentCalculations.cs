using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Utilities;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        public string BuildEquipmentCalculationDetails(EquipmentItemInstance item, CharacterInstance owner,
            GameContentCatalog catalog = null) => BuildEquipmentDetails(item,owner,true,this,catalog);

        private string LinkEquipmentValue(string display, EquipmentItemInstance item, CharacterStatType stat,
            StatModifierOperation operation, bool attack, GameContentCatalog catalog)
        {
            var title=attack ? "Atak przedmiotu" : (operation==StatModifierOperation.Maximum?"Limit — ":"")+GetEquipmentStatDisplayName(stat,item);
            var stats=attack?new[]{CharacterStatType.MinAttack,CharacterStatType.MaxAttack}:new[]{stat};
            catalog ??= GameBootstrap.Instance?.ContentCatalog;
            return LinkCalculation(display,title,EquipmentValueDetails(item,stats,operation,catalog));
        }

        private string EquipmentValueDetails(EquipmentItemInstance item, CharacterStatType[] stats,
            StatModifierOperation operation, GameContentCatalog catalog)
        {
            bool Included(StatModifier m) => m!=null && (operation==StatModifierOperation.FlatBase
                ? m.Operation is not StatModifierOperation.Percent and not StatModifierOperation.Maximum : m.Operation==operation);
            float[] Values(IEnumerable<StatModifier> modifiers) => stats.Select(stat =>
            {
                var rows=(modifiers??Array.Empty<StatModifier>()).Where(m=>Included(m)&&m.StatType==stat).ToArray();
                if(operation==StatModifierOperation.Maximum)return rows.Length==0?0:rows.Min(m=>m.Value);
                if(operation==StatModifierOperation.Percent)
                {
                    var additive=rows.Where(m=>m.Stacking==StatModifierStacking.Additive).Sum(m=>m.Value);
                    var multiplier=1f;foreach(var m in rows.Where(m=>m.Stacking!=StatModifierStacking.Additive))multiplier*=Mathf.Max(0,1+m.Value);
                    return (1+additive)*multiplier-1;
                }
                return rows.Sum(m=>m.Value);
            }).ToArray();
            var actual=Values(item.StatModifiers);var baseline=Values(item.BaseStatModifiers);
            string Format(float[] values,bool signed=false,bool multiplier=false,bool final=false)
            {
                if(multiplier && values.All(v=>Mathf.Approximately(v,values[0])))values=new[]{values[0]};
                return string.Join("–",values.Select((v,i)=>
            {
                if(multiplier)return "×"+(v*100).ToString("0.##",UiTerms.PolishCulture)+"%";
                var text=operation==StatModifierOperation.Maximum ? FormatValue(v) : operation==StatModifierOperation.Percent ? (v*100).ToString("0.##",UiTerms.PolishCulture)+"%"
                    : final || stats[i] is CharacterStatType.CritChance or CharacterStatType.CritDamage ? FormatEquipmentStatValue(stats[i],v) : FormatValue(v);
                return signed&&v>0?"+"+text:text;
            }));
            }
            bool Equal(float[] a,float[] b)=>a.Zip(b,(x,y)=>Mathf.Abs(x-y)<=.0001f*Mathf.Max(1,Mathf.Abs(y))).All(v=>v);
            bool Nonzero(float[] a)=>a.Any(v=>Mathf.Abs(v)>.0001f);
            var result=new StringBuilder();
            var predicted=baseline.Select((v,i)=>v*item.GetStatProgressionMultiplier(stats[i])).ToArray();
            // Saved payload is authoritative. Never manufacture provenance from changed definitions
            // or repair legacy equipment as a side effect of opening its tooltip.
            if(item.BaseStatModifiers==null || item.BaseStatModifiers.Count==0 || !Equal(predicted,actual))
                return Row("Wartość bazowa",Format(actual))+(Format(actual)==Format(actual,final:true)?"":Row("Wynik",Format(actual,final:true)));

            var detailed=false;var terms=0;
            if(item is JewelryItemInstance jewelry && item.EquipmentDefinition is JewelryItemDefinition definition)
            {
                var metal=definition.Metals?.FirstOrDefault(m=>m!=null&&JewelryGenerationService.GetMetalRecipeKey(m)==jewelry.MetalId);
                var gem=definition.Gems?.FirstOrDefault(g=>g!=null&&g.CraftingMaterial?.Id==jewelry.GemId);
                List<StatModifier> ToStats(List<JewelryModifierData> source)=>(source??new()).Where(m=>m!=null&&m.Target==JewelryModifierTarget.Stat)
                    .Select(m=>new StatModifier{StatType=m.StatType,Operation=m.Operation,Stacking=StatModifierStacking.Additive,Value=m.Value}).ToList();
                if(metal!=null && gem!=null)
                {
                    var metals=ToStats(metal.Modifiers);var gems=ToStats(gem.Modifiers);
                    if(Equal(Values(metals.Concat(gems)),baseline))
                    {
                        var a=Values(metals);var b=Values(gems);
                        if(Nonzero(a)){result.Append(Row(EquipmentBaseSource("Metal — baza",jewelry.MetalDisplayName,Format(a)),Format(a)));terms++;}
                        if(Nonzero(b)){result.Append(Row(EquipmentBaseSource("Kamień — baza",jewelry.GemDisplayName,Format(b)),Format(b)));terms++;}
                        detailed=result.Length>0;
                    }
                }
            }
            else if(item.UsesEquipmentProgression && item.PartIds?.Count==3 && catalog!=null)
            {
                var parts=item.PartIds.Select(id=>catalog.EquipmentPartDefinitions?.FirstOrDefault(p=>p!=null&&p.Id==id)).ToArray();
                if(parts.All(p=>p?.Stats!=null) && parts.All(p=>p.LevelBracket==parts[0].LevelBracket))
                {
                    var bases=new float[stats.Length][];var percentages=new float[stats.Length][];var quality=new float[stats.Length];
                    for(var i=0;i<stats.Length;i++)EquipmentGenerationService.GetStatInputs(parts,item.Definition.ItemType,item.RequiredClassId,item.Quality,(int)parts[0].LevelBracket,stats[i],out bases[i],out percentages[i],out quality[i]);
                    var generated=stats.Select((_,i)=>(bases[i].Sum()+quality[i])*(1+percentages[i].Sum())).ToArray();
                    // Generated parts produce FlatBase only; otherwise their formula cannot explain this row.
                    if(operation==StatModifierOperation.FlatBase && Equal(generated,baseline))
                    {
                        for(var n=0;n<parts.Length;n++)
                        {
                            var values=stats.Select((_,i)=>bases[i][n]).ToArray();
                            if(Nonzero(values)){result.Append(Row(EquipmentBaseSource("Baza",parts[n].DisplayName,Format(values)),Format(values)));terms++;}
                        }
                        if(Nonzero(quality)){result.Append(Row("Jakość — "+GetQualityDisplayName(item.Quality),Colored(Format(quality,true),quality.Sum()>0)));terms++;}
                        var percent=stats.Select((_,i)=>percentages[i].Sum()).ToArray();
                        if(Nonzero(percent))
                        {
                            // Part percentages add together before multiplying the base plus quality.
                            var sources=new StringBuilder();
                            for(var n=0;n<parts.Length;n++)
                            {
                                var values=stats.Select((_,i)=>percentages[i][n]*100).ToArray();
                                if(Nonzero(values))sources.Append(Row(UiTerms.Present(parts[n].DisplayName),string.Join("–",values.Select(v=>v.ToString("+0.##;-0.##;0",UiTerms.PolishCulture)+" pp"))));
                            }
                            var multipliers=percent.Select(v=>1+v).ToArray();
                            var text=Format(multipliers,multiplier:true);
                            var link=LinkEquipmentPartsPercent(text,sources.ToString()+Row("Wynik",text));
                            result.Append(Row("Modyfikatory części",link.Replace("#7EDBFF",percent.Sum()>0?BonusColor:PenaltyColor)));terms++;
                        }
                        detailed=true;
                    }
                }
            }
            if(!detailed){result.Append(Row("Wartość bazowa",Format(baseline)));terms++;}
            var progression=stats.Select(item.GetStatProgressionMultiplier).ToArray();
            if(progression.Any(v=>!Mathf.Approximately(v,1)))
            {
                var primary=item.Definition.ItemType==ItemType.Weapon
                    ? stats.All(s=>s is CharacterStatType.MinAttack or CharacterStatType.MaxAttack)
                    : stats.All(s=>s is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense);
                var label=primary?"Ulepszenie — "+FormatPercent(Mathf.Clamp01(item.UpgradeProgress)):"Rzadkość — "+GetRarityDisplayName(item.CurrentRarity);
                result.Append(Row(label,Colored(Format(progression,multiplier:true),progression.Sum()>progression.Length)));terms++;
            }
            if(terms>1 || !Equal(baseline,actual) || Format(actual)!=Format(actual,final:true))result.Append(Row("Wynik",Format(actual,final:true)));
            return result.ToString();
        }

        private string EquipmentBaseSource(string label,string source,string value)
            => LinkCalculation(label,"Źródło bazy",UiTerms.Present(source)+"\n\n"+Row("Baza",value));

        private string LinkEquipmentPartsPercent(string text,string body)
            => LinkCalculation(text,"Modyfikatory części",body);
    }
}
