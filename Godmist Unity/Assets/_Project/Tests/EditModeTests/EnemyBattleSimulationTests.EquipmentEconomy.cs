using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string EquipmentEconomyFolder=>Path.GetFullPath("../LocalHandoffs/equipment-economy-20261019");
        [Serializable] sealed class EconomyPart {public string id,name,cls; public int level,kind,value;}
        [Serializable] sealed class EconomyMaterial {public string id,name;public int value;}
        [Serializable] sealed class EconomyCount {public string id;public int amount;}
        [Serializable] sealed class EconomyLayout
        {public string biome;public int seed,encounters,chests;public List<EconomyCount> enemies=new();}
        [Serializable] sealed class EconomyLoot
        {public int level,samples;public string biome,enemy,table;public bool hasThemeLoot;public double meanSell,se;}
        [Serializable] sealed class EconomyExport
        {
            public string policy="No battles, AI, saves, scene changes or balance edits. Eight normal layouts seed0-3 per biome. Loot-only fixed-seed bounded sampling, with all contents liquidated at the relevant trader multiplier; NPC liquidity, carrying capacity and resource retention are not assumed unlimited income.";
            public List<EconomyPart> parts=new();public List<EconomyMaterial> materials=new();
            public List<EconomyLayout> layouts=new();public List<EconomyLoot> loot=new();
        }
        static int EconomyMaterialValue(ItemDefinition item)=>item is CraftableItemDefinition craftable
            ?craftable.CalculatedBaseValue:Math.Max(0,item?.BaseValue??0);
        static double EconomySaleValue(ItemInstance item)
        {
            if(item is LootBagItemInstance bag)return bag.Contents.Sum(EconomySaleValue);
            var value=Math.Max(0,item?.BaseValue??0);
            if(value==0 && item?.Definition is CraftableItemDefinition craftable)value=craftable.CalculatedBaseValue;
            return Math.Floor(value*GameDefinesRuntime.GetFloat(GameDefineIds.TraderRelevantSellMultiplier,.5f));
        }
        static (double mean,double se) EconomySampleLoot(LootTableData table,int level,int seed,int samples)
        {
            if(table==null)return(0,0);
            var roller=new LootTableSystem(new System.Random(seed),PointCatalog.EquipmentPartDefinitions,lootLevel:level);
            double sum=0,squares=0;
            for(var i=0;i<samples;i++)
            {
                var value=roller.RollLoot(table).Sum(EconomySaleValue);sum+=value;squares+=value*value;
            }
            var mean=sum/samples;return(mean,Math.Sqrt(Math.Max(0,squares/samples-mean*mean)/samples));
        }
        [Test,Explicit("Bounded equipment economy audit: eight layouts and loot rolls only, no combat simulations.")]
        [Timeout(240000)]
        public void AuditEquipmentEconomyInputs()
        {
            Directory.CreateDirectory(EquipmentEconomyFolder);var report=new EconomyExport();
            foreach(var part in PointCatalog.EquipmentPartDefinitions.Where(p=>p!=null&&!p.IsStarterOnly))
                report.parts.Add(new EconomyPart{id=part.Id,name=part.DisplayName,cls=part.RequiredClass.Id,level=(int)part.LevelBracket,kind=(int)part.Kind,
                    value=part.CraftingMaterials.Sum(r=>r.Amount*EconomyMaterialValue(r.Material))});
            foreach(var material in PointCatalog.ItemDefinitions.Where(i=>i!=null&&i.ItemType==ItemType.Material))
                report.materials.Add(new EconomyMaterial{id=material.Id,name=material.DisplayName,value=EconomyMaterialValue(material)});
            foreach(var biome in new[]{"forest","catacombs"})
            {
                var definition=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                for(var seed=0;seed<4;seed++)
                {
                    File.AppendAllText(Path.Combine(EquipmentEconomyFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" "+biome+" layout "+seed+Environment.NewLine);
                    var dungeon=new DungeonGeneratorService().Generate(definition,seed,15,DungeonLength.Normal);
                    new DungeonEncounterPlacementSystem().Place(dungeon);
                    new DungeonObjectPlacementSystem().Place(dungeon,PointCatalog.EquipmentPartDefinitions);
                    var encounters=dungeon.Floors.SelectMany(f=>f.Encounters).Where(e=>!e.IsReinforcement).ToList();
                    Assert.That(encounters,Is.Not.Empty);
                    report.layouts.Add(new EconomyLayout{biome=biome,seed=seed,encounters=encounters.Count,
                        chests=dungeon.Floors.Sum(f=>f.Objects.OfType<ChestDungeonObjectInstance>().Count()),
                        enemies=encounters.SelectMany(e=>e.Enemies).Where(e=>e.CharacterDefinition!=null).GroupBy(e=>e.CharacterDefinition.Id)
                            .Select(g=>new EconomyCount{id=g.Key,amount=g.Count()}).ToList()});
                }
                foreach(var level in new[]{5,15,25,35,45,48})
                {
                    File.AppendAllText(Path.Combine(EquipmentEconomyFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" "+biome+" loot L"+level+Environment.NewLine);
                    LootTableData theme=null;var hasTheme=definition.LootTableCatalog!=null&&definition.LootTableCatalog.TryGetEnemyLootTable(definition.Theme,level,out theme);
                    var common=EconomySampleLoot(theme,level,12349+level,2048);
                    foreach(var enemy in PointEnemyDefinitions.Where(e=>e.Id.StartsWith("enemy."+biome+".")))
                    {
                        var species=EconomySampleLoot(enemy.LootTable,level,23641+level,1024);
                        report.loot.Add(new EconomyLoot{biome=biome,enemy=enemy.Id,level=level,hasThemeLoot=hasTheme,
                            table=theme?.name??"species-only",samples=hasTheme?2048:1024,meanSell=common.mean+species.mean,se=Math.Sqrt(common.se*common.se+species.se*species.se)});
                    }
                    LootTableData chest=null;definition.LootTableCatalog?.TryGetChestLootTable(definition.Theme,level,out chest);
                    var chestValue=EconomySampleLoot(chest,level,39467+level,2048);
                    report.loot.Add(new EconomyLoot{biome=biome,enemy="CHEST",level=level,hasThemeLoot=chest!=null,
                        table=chest?.name??"no-themed-table",samples=2048,meanSell=chestValue.mean,se=chestValue.se});
                }
            }
            Assert.That(report.layouts,Has.Count.EqualTo(8));
            Assert.That(report.parts.All(p=>p.value>0),Is.True);
            File.WriteAllText(Path.Combine(EquipmentEconomyFolder,"runtime.json"),JsonUtility.ToJson(report,true));
        }
    }
}
