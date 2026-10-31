using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class EquipmentCalculationTooltipTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const string CatalogPath="Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";
        GameObject root;UIWorkspaceTooltipView view,previous;GameContentCatalog catalog;
        Dictionary<string,CombatTooltipGlossary.Entry> Entries => (Dictionary<string,CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
        static string Plain(string s)=>Regex.Replace(s,"<[^>]*>","");
        [SetUp] public void Setup()
        {
            catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);Assert.That(catalog,Is.Not.Null);
            previous=UIWorkspaceTooltipView.ActiveInstance;
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            view=root.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,view);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        IEnumerable<EquipmentItemDefinition> Definitions()=>catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().Where(d=>d is not JewelryItemDefinition && d is not UniqueEquipmentDefinition && !d.IsStarterEquipment && d.RequiredClass!=null && d.ItemType is ItemType.Weapon or ItemType.Armor)
            .GroupBy(d=>(d.RequiredClass.Id,d.ItemType)).Select(g=>g.First());
        EquipmentItemInstance Generate(EquipmentItemDefinition definition,EquipmentQuality quality,RarityTier rarity)
            =>new EquipmentGenerationService(new System.Random(42)).Generate(definition,catalog.EquipmentPartDefinitions,quality,rarity);
        [Test] public void AllEquipmentProgressionValuesKeepTheOriginalFormula()
        {
            var count=0;
            foreach(var definition in Definitions())foreach(EquipmentQuality quality in Enum.GetValues(typeof(EquipmentQuality)))foreach(RarityTier rarity in Enum.GetValues(typeof(RarityTier)))foreach(var progress in new[]{0f,.4f,1f})
            {
                var item=Generate(definition,quality,rarity);Assert.That(item,Is.Not.Null,definition.Id);item.UpgradeProgress=progress;item.RecalculateStatModifiers();
                var r=EquipmentItemInstance.RarityMultiplier(rarity);
                foreach(var m in item.BaseStatModifiers)
                {
                    var primary=definition.ItemType==ItemType.Weapon?m.StatType is CharacterStatType.MinAttack or CharacterStatType.MaxAttack:m.StatType is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense;
                    var expected=primary?1+(definition.ItemType==ItemType.Weapon?.5f:1.5f)*progress
                        :m.StatType is CharacterStatType.WeaponAccuracy or CharacterStatType.Dodge?1+.3f*(r-1)
                        :m.StatType is CharacterStatType.MaxResource or CharacterStatType.ResourceRegeneration?1+.6f*(r-1)
                        :m.StatType==CharacterStatType.CritDamage?1+.5f*(r-1)
                        :m.StatType is CharacterStatType.MaxHealth or CharacterStatType.CritChance?r:1;
                    Assert.That(item.GetStatProgressionMultiplier(m.StatType),Is.EqualTo(expected));
                    Assert.That(item.StatModifiers.Single(x=>x.StatType==m.StatType).Value,Is.EqualTo(m.Value*expected));
                }
                count++;
            }
            Assert.That(count,Is.GreaterThan(500));
        }
        [Test] public void EachVisibleStatLinksToItsOwnResolvedDetailsWithoutMutation()
        {
            foreach(var definition in Definitions())foreach(var quality in new[]{EquipmentQuality.Poor,EquipmentQuality.Masterpiece})
            {
                var item=Generate(definition,quality,RarityTier.Rare);item.UpgradeProgress=.4f;item.RecalculateStatModifiers();
                var before=JsonUtility.ToJson(item);var random=UnityEngine.Random.state;Entries.Clear();
                var body=view.BuildEquipmentCalculationDetails(item,null,catalog);
                Assert.That(body,Does.Contain("calculation."));
                foreach(var line in body.Split('\n').Where(l=>l.Contains(ItemStatColumnsView.Marker)))Assert.That(line,Does.Contain("calculation."),Plain(line));
                foreach(var e in Entries.Values)
                {
                    Assert.That(Plain(e.Description).Trim(),Is.Not.Empty);
                    foreach(var line in e.Description.Split('\n').Where(l=>l.StartsWith("Wartość bazowa")))Assert.That(line,Does.Not.Contain("<color="));
                }
                var attack=Entries.Values.FirstOrDefault(e=>e.Title=="Atak przedmiotu");
                if(definition.ItemType==ItemType.Weapon){Assert.That(attack,Is.Not.Null);Assert.That(Plain(attack.Description),Does.Contain("Jakość").And.Contain("Ulepszenie").And.Contain("Wynik"));}
                Assert.That(JsonUtility.ToJson(item),Is.EqualTo(before));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(item,null),Does.Not.Contain("calculation."));
            }
        }
        [Test] public void UnmodifiedSingleBaseDoesNotRepeatTheResult()
        {
            var item=Generate(Definitions().First(d=>d.ItemType==ItemType.Weapon),EquipmentQuality.Normal,RarityTier.Common);
            item.PartIds.Clear();item.BaseStatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=10},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=20}};
            item.RecalculateStatModifiers();view.BuildEquipmentCalculationDetails(item,null,catalog);
            var entry=Entries.Values.Single(e=>e.Title=="Atak przedmiotu");
            Assert.That(Plain(entry.Description),Does.Contain("10–20").And.Not.Contain("Wynik"));
            Assert.That(entry.Description,Does.Not.Contain("<color="));
        }
        [Test] public void FractionalInputsAreNotMisrepresentedAsZeroBeforeRounding()
        {
            var item=Generate(Definitions().First(d=>d.ItemType==ItemType.Weapon),EquipmentQuality.Normal,RarityTier.Common);
            item.PartIds.Clear();item.BaseStatModifiers=new(){new StatModifier{StatType=CharacterStatType.ResourceRegeneration,Value=.4f}};
            item.RecalculateStatModifiers();view.BuildEquipmentCalculationDetails(item,null,catalog);
            var entry=Entries.Values.Single();Assert.That(Plain(entry.Description),Does.Contain("0,4").And.Contain("Wynik0"));
        }
        [Test] public void EqualContributionsFromDifferentWrappedSourcesAreBothVisible()
        {
            view.LinkCalculation("×140%","Modyfikatory części","Pierwsza długa nazwa części\n<indent=8%>+20 pp</indent>\nDruga długa nazwa części\n<indent=8%>+20 pp</indent>");
            Assert.That(Regex.Matches(Entries.Values.Single().Description,Regex.Escape("+20 pp")).Count,Is.EqualTo(2));
        }
        [Test] public void ItemNumberLinksNeverReceiveUnderlinesAndBaseSourcesStayCompact()
        {
            var item=Generate(Definitions().First(d=>d.ItemType==ItemType.Weapon),EquipmentQuality.Poor,RarityTier.Common);
            var body=view.BuildEquipmentCalculationDetails(item,null,catalog);
            var decorated=(string)typeof(UIWorkspaceTooltipView).GetMethod("LinkItemMechanics",Hidden).Invoke(view,new object[]{item,body});
            var links=Regex.Matches(decorated,@"<link=""calculation\.[^""]+"">(.*?)</link>",RegexOptions.Singleline);
            Assert.That(links.Count,Is.GreaterThan(0));
            foreach(Match link in links)Assert.That(link.Value,Does.Not.Contain("<u>"));
            Assert.That(decorated,Does.Contain("<u>"),"Glossary terms retain their underline");
            var attack=Entries.Values.Single(e=>e.Title=="Atak przedmiotu");
            Assert.That(Plain(attack.Description),Does.StartWith("Baza").And.Not.Contain("Wartość bazowa —"));
            Assert.That(Entries.Values.Any(e=>e.Title=="Źródło bazy"&&e.Description.Contains("Baza")),Is.True);
        }
        [Test] public void LegacyAndChangedDefinitionsDoNotInventPartSourcesOrRepairSavedItems()
        {
            var item=Generate(Definitions().First(d=>d.ItemType==ItemType.Weapon),EquipmentQuality.Excellent,RarityTier.Rare);
            item.StatModifiers[0].Value+=123;var before=JsonUtility.ToJson(item);view.BuildEquipmentCalculationDetails(item,null,catalog);
            var entry=Entries.Values.Single(e=>e.Title=="Atak przedmiotu");Assert.That(Plain(entry.Description),Does.Contain("Wartość bazowa").And.Not.Contain("Jakość").And.Not.Contain("Ulepszenie"));
            item.BaseStatModifiers.Clear();before=JsonUtility.ToJson(item);Entries.Clear();view.BuildEquipmentCalculationDetails(item,null,catalog);
            Assert.That(JsonUtility.ToJson(item),Is.EqualTo(before));Assert.That(Entries.Values.Single(e=>e.Title=="Atak przedmiotu").Description.Split('\n')[0],Does.Not.Contain("<color="));
        }
        [Test] public void JewelryShowsActualMetalAndGemSourcesAndPercentAndCapStaySeparate()
        {
            var definition=catalog.ItemDefinitions.OfType<JewelryItemDefinition>().First(d=>d.Metals.Count>0&&d.Gems.Count>0);
            var item=new JewelryGenerationService(()=>.5f).GenerateSelected(definition,definition.Metals[0],definition.Gems[0]);
            var before=JsonUtility.ToJson(item);view.BuildEquipmentCalculationDetails(item,null,catalog);Assert.That(Entries.Values.Any(e=>(Plain(e.Description).Contains("Metal") || Plain(e.Description).Contains("Kamień"))),Is.True);
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.MaxHealth,Operation=StatModifierOperation.Percent,Stacking=StatModifierStacking.Additive,Value=.2f});
            item.BaseStatModifiers.Add(new StatModifier{StatType=CharacterStatType.MaxHealth,Operation=StatModifierOperation.Percent,Stacking=StatModifierStacking.Additive,Value=.2f});
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.Dodge,Operation=StatModifierOperation.Maximum,Value=40});
            item.BaseStatModifiers.Add(new StatModifier{StatType=CharacterStatType.Dodge,Operation=StatModifierOperation.Maximum,Value=40});
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.CritChance,Operation=StatModifierOperation.Maximum,Value=.5f});
            item.BaseStatModifiers.Add(new StatModifier{StatType=CharacterStatType.CritChance,Operation=StatModifierOperation.Maximum,Value=.5f});
            Entries.Clear();var body=view.BuildEquipmentCalculationDetails(item,null,catalog);
            Assert.That(Plain(Entries.Values.Single(e=>e.Title=="Limit — Szansa krytyczna").Description),Does.Contain("0,5").And.Not.Contain("pp"));
            Assert.That(body,Does.Contain("Limit — Unik"));Assert.That(Entries.Values.Any(e=>Plain(e.Description).Contains("20%")),Is.True);Assert.That(Entries.Values.Single(e=>e.Title=="Limit — Unik").Description,Does.Contain("40"));
        }
    }
}
