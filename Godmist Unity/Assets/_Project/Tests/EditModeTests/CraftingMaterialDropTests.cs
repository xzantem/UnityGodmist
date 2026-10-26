using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class CraftingMaterialDropTests
    {
        const string Output="../LocalHandoffs/crafting-material-drop-20261025";
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        static LootBagItemDefinition Bag(string biome)=>AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(biome=="forest"?CraftingMaterialDropSetup.ForestPath:CraftingMaterialDropSetup.CatacombsPath);
        static string N(double n)=>n.ToString("0.#########",CultureInfo.InvariantCulture);
        static double Weight(LootBagPoolEntryData entry)=>entry.SelectionWeight>0 && !float.IsNaN(entry.SelectionWeight) && !float.IsInfinity(entry.SelectionWeight)?entry.SelectionWeight:1;
        static Dictionary<string,double> Means(LootBagItemDefinition bag,int level,float bonus=0)
        {
            var bracket=level<=10?LootLevelBracket.Level1To10:LootLevelBracket.Level11To20;
            Assert.That(bag.TryGetBracket(bracket,out var data),Is.True);
            var result=new Dictionary<string,double>();
            foreach(var pool in data.Pools.Where(CraftingMaterialDropSetup.IsCraftingPool))
            {
                var sum=pool.Entries.Sum(Weight);var chance=Mathf.Clamp01(pool.DropChance+bonus);
                foreach(var e in pool.Entries)
                {
                    result.TryGetValue(e.ItemDefinition.Id,out var old);
                    result[e.ItemDefinition.Id]=old+chance*Weight(e)/sum*(e.Amount.Min+e.Amount.Max)/2;
                }
            }
            return result;
        }
        [TestCase("forest",10)] [TestCase("forest",20)] [TestCase("catacombs",10)] [TestCase("catacombs",20)]
        [Explicit("Read-only paired baseline, 12 seeds x 3 lengths per profile.")]
        public void Before(string biome,int level)=>Report("before",biome,level);
        [TestCase("forest",10)] [TestCase("forest",20)] [TestCase("catacombs",10)] [TestCase("catacombs",20)]
        [Explicit("Read-only full-clear audit after authoring the approved crafting pools.")]
        public void After(string biome,int level)=>Report("after",biome,level);
        static void Report(string phase,string biome,int level)
        {
            var folder=Output+"/"+phase;Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/"+biome+"-bag.json",EditorJsonUtility.ToJson(Bag(biome),true));
            var catalog=Catalog;var definition=catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon."+biome+".expedition");
            var coreIds=CraftingMaterialDropSetup.Materials.Where(m=>!m.Runic).Select(m=>m.Id).ToHashSet();
            var rows=new List<string>{"biome,level,length,seed,floors,rooms,encounters,enemies,chests,resourceBags,coreUnits,coreValue,runicUnits,runicValue,alchemyUnits,foodUnits"};
            var items=new List<string>{"biome,level,length,seed,source,item,quantity"};
            foreach(var length in new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long})
            for(var n=0;n<12;n++)
            {
                var seed=241026+n;var dungeon=new DungeonGeneratorService().Generate(definition,seed,level,length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                new DungeonObjectPlacementSystem().Place(dungeon,catalog.EquipmentPartDefinitions);
                new DungeonEncounterRewardPreparationSystem().Prepare(dungeon,catalog.EquipmentPartDefinitions);
                var counts=new Dictionary<(string,string),int>();var bags=0;
                double coreValue=0,runicValue=0;int core=0,runic=0,alchemy=0,food=0;
                void Count(ItemInstance item,bool resource=false,string source="enemy")
                {
                    if(item is LootBagItemInstance loot)
                    {
                        var isResource=item.Definition.Id.EndsWith(".resources",StringComparison.Ordinal);
                        if(isResource)bags++;
                        foreach(var child in loot.Contents)Count(child,resource||isResource,source);
                        return;
                    }
                    if(item?.Definition==null)return;
                    var id=item.Definition.Id;var key=(resource?"resource":source,id);counts.TryGetValue(key,out var old);counts[key]=old+1;
                    if(item.Definition.ItemType==ItemType.Food)food++;
                    if(CraftingMaterialDropSetup.Discipline(item.Definition)==MaterialDiscipline.Alchemy)alchemy++;
                    if(!resource)return;
                    if(coreIds.Contains(id)){core++;coreValue+=item.Definition.BaseValue;}
                    if(CraftingMaterialDropSetup.Discipline(item.Definition)==MaterialDiscipline.Runic){runic++;runicValue+=item.Definition.BaseValue;}
                }
                var encounters=dungeon.Floors.SelectMany(f=>f.Encounters).Where(e=>!e.IsReinforcement).ToArray();
                foreach(var encounter in encounters)
                {
                    Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId,out var rewards),Is.True);
                    foreach(var item in rewards.EnemyRewards.SelectMany(e=>e.Items))Count(item);
                    foreach(var item in rewards.Items)Count(item);
                }
                var chests=dungeon.Floors.SelectMany(f=>f.Objects).OfType<ChestDungeonObjectInstance>().ToArray();
                foreach(var chest in chests)foreach(var item in chest.Items)Count(item,false,"chest");
                var prefix=$"{biome},{level},{length},{seed}";
                foreach(var pair in counts.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2))items.Add($"{prefix},{pair.Key.Item1},{pair.Key.Item2},{pair.Value}");
                rows.Add($"{prefix},{dungeon.Floors.Count},{dungeon.Floors.Sum(f=>f.Rooms.Count)},{encounters.Length},{encounters.Sum(e=>e.Definition.Enemies.Count)},{chests.Length},{bags},{core},{N(coreValue)},{runic},{N(runicValue)},{alchemy},{food}");
            }
            File.WriteAllLines(folder+"/"+biome+"-"+level+"-clears.csv",rows);
            File.WriteAllLines(folder+"/"+biome+"-"+level+"-items.csv",items);
        }
        [Test,Explicit("Save only the two approved resource bags through targeted Editor API.")]
        public void ApplyContent()
        {
            CraftingMaterialDropSetup.Apply();var first=new[]{"forest","catacombs"}.Select(b=>EditorJsonUtility.ToJson(Bag(b))).ToArray();
            CraftingMaterialDropSetup.Apply();
            Assert.That(new[]{"forest","catacombs"}.Select(b=>EditorJsonUtility.ToJson(Bag(b))),Is.EqualTo(first));
        }
        [TestCase("forest",10)] [TestCase("forest",20)] [TestCase("catacombs",10)] [TestCase("catacombs",20)]
        public void AuthoredMeansMatchEveryApprovedMaterial(string biome,int level)
        {
            var expected=CraftingMaterialDropSetup.Materials.Where(m=>m.Tier==(level==10?5:15)).ToArray();
            var actual=Means(Bag(biome),level);
            Assert.That(actual.Keys,Is.EquivalentTo(expected.Select(m=>m.Id)));
            foreach(var m in expected)Assert.That(actual[m.Id],Is.EqualTo(m.For(biome=="forest")).Within(1e-6),m.Id);
        }
        [TestCase("forest",10,0f)] [TestCase("forest",20,0f)] [TestCase("catacombs",10,0f)] [TestCase("catacombs",20,0f)]
        [TestCase("forest",10,.1f)] [TestCase("forest",20,.1f)] [TestCase("catacombs",10,.1f)] [TestCase("catacombs",20,.1f)]
        public void ProductionBagSamplerMatchesMeansAndKeepsBonusBounded(string biome,int level,float bonus)
        {
            var expected=Means(Bag(biome),level,bonus);var actual=new Dictionary<string,int>();
            var system=new LootTableSystem(new System.Random(251025+level),Catalog.EquipmentPartDefinitions,lootLevel:level,dropChanceBonus:bonus);
            var samples=bonus==0?20000:5000;
            for(var n=0;n<samples;n++)foreach(var item in system.CreateLootBag(Bag(biome)).Contents)
            {
                if(!expected.ContainsKey(item.Definition.Id))continue;
                actual.TryGetValue(item.Definition.Id,out var old);actual[item.Definition.Id]=old+1;
            }
            foreach(var pair in expected)
            {
                actual.TryGetValue(pair.Key,out var count);
                var tolerance=Math.Max(.008,5*Math.Sqrt(Math.Max(.00001,pair.Value)/samples));
                Assert.That(count/(double)samples,Is.EqualTo(pair.Value).Within(tolerance),pair.Key);
            }
            Assert.That(Means(Bag(biome),level,bonus).Values.Sum()-Means(Bag(biome),level).Values.Sum(),Is.LessThanOrEqualTo(2*bonus+1e-5));
        }
        static void Set(object target,string name,object value)
        {
            for(var type=target.GetType();type!=null;type=type.BaseType)
            {
                var field=type.GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
                if(field==null)continue;field.SetValue(target,value);return;
            }
            throw new MissingFieldException(target.GetType().Name,name);
        }
        [TestCase("forest",10,5.577927135254414)] [TestCase("catacombs",10,3.989615213994383)]
        [TestCase("forest",20,5.9102132264263485)] [TestCase("catacombs",20,4.463350465593804)]
        public void FamilySharesAndTotalVolumeMatchTheApprovedIndependentTargets(string biome,int level,double total)
        {
            var means=Means(Bag(biome),level);
            var core=means.Where(p=>Catalog.ItemDefinitions.Any(i=>i!=null && i.Id==p.Key && CraftingMaterialDropSetup.Discipline(i)==MaterialDiscipline.Smithing)).ToArray();
            Assert.That(core.Sum(p=>p.Value),Is.EqualTo(total).Within(1e-6));
            var skins=core.Where(p=>p.Key.Contains("_skin")).Sum(p=>p.Value);
            var wood=core.Where(p=>p.Key.Contains("_wood")).Sum(p=>p.Value);
            var fibre=core.Where(p=>p.Key.Contains("_fibre")).Sum(p=>p.Value);
            Assert.That(skins/total,Is.EqualTo(level==10?.32:.08).Within(1e-6));
            Assert.That(wood/total,Is.EqualTo(.03).Within(1e-6));
            Assert.That(fibre/total,Is.EqualTo(level==10?.10:.09).Within(1e-6));
            Assert.That((total-skins-wood-fibre)/total,Is.EqualTo(level==10?.55:.80).Within(1e-6));
            if(level<=10)return;
            var rune=means.Where(p=>!core.Any(c=>c.Key==p.Key)).ToArray();
            Assert.That(rune.Single(p=>p.Key=="item.runic.solid_limestone").Value/rune.Sum(p=>p.Value),Is.EqualTo(.15).Within(1e-6));
        }
        static (LootBagItemDefinition Bag,ItemDefinition[] Values) SelectionBag(float first,float second,int pools=1,bool distinct=false)
        {
            var bag=ScriptableObject.CreateInstance<LootBagItemDefinition>();
            var values=Catalog.ItemDefinitions.Where(i=>i!=null && (i.Id=="item.smithing.iron_ore" || i.Id=="item.smithing.goat_skin")).OrderBy(i=>i.Id).ToArray();
            Set(bag,"Id","test.weighted.bag");Set(bag,"ItemType",ItemType.Lootbag);
            var entries=values.Select(i=>{var e=new LootBagPoolEntryData();Set(e,"ItemDefinition",i);Set(e,"Amount",new IntRange(1,1));return e;}).ToList();
            Set(entries[0],"SelectionWeight",first);Set(entries[1],"SelectionWeight",second);
            var list=new List<LootBagPoolData>();
            for(var n=0;n<pools;n++){var pool=new LootBagPoolData();Set(pool,"Entries",entries);Set(pool,"DistinctSelectionGroup",distinct?"legacy-distinct":string.Empty);list.Add(pool);}
            var bracket=new LootBagBracketData();Set(bracket,"Bracket",LootLevelBracket.Level1To10);Set(bracket,"Pools",list);
            Set(bag,"Brackets",new List<LootBagBracketData>{bracket});return(bag,values);
        }
        [TestCase(0f,0)] [TestCase(.24999f,0)] [TestCase(.25f,1)] [TestCase(.9999f,1)] [TestCase(1f,1)]
        public void WeightedSelectionUsesTheExpectedBoundaryAndOneSelectionDraw(float roll,int index)
        {
            var (bag,values)=SelectionBag(1,3);
            try
            {
                var draws=0;
                var result=new LootTableSystem(()=>{draws++;return roll;}).CreateLootBag(bag);
                Assert.That(result.Contents.Single().Definition,Is.SameAs(values[index]));Assert.That(draws,Is.EqualTo(2),"One pool check plus one selection draw; fixed amount needs none.");
            }
            finally{Object.DestroyImmediate(bag);}
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void LegacyOrInvalidWeightsUseTheOriginalUniformSelection(float weight)
        {
            var (bag,values)=SelectionBag(weight,1);
            try{Assert.That(new LootTableSystem(()=>.49f).CreateLootBag(bag).Contents.Single().Definition,Is.SameAs(values[0]));
                Assert.That(new LootTableSystem(()=>.5f).CreateLootBag(bag).Contents.Single().Definition,Is.SameAs(values[1]));}
            finally{Object.DestroyImmediate(bag);}
        }
        [Test]
        public void WeightedDistinctPoolsStillExcludeAlreadyChosenItems()
        {
            var (bag,_)=SelectionBag(1000,1,3,true);
            try{var result=new LootTableSystem(()=>.01f).CreateLootBag(bag);Assert.That(result.Contents.Count,Is.EqualTo(2));
                Assert.That(result.Contents.Select(i=>i.Definition.Id).Distinct().Count(),Is.EqualTo(2));}
            finally{Object.DestroyImmediate(bag);}
        }
        [Test]
        public void VeryLargeFiniteWeightsDoNotOverflowTheSelectionSum()
        {
            var (bag,values)=SelectionBag(float.MaxValue,float.MaxValue);
            try{Assert.That(new LootTableSystem(()=>.3f).CreateLootBag(bag).Contents.Single().Definition,Is.SameAs(values[0]));}
            finally{Object.DestroyImmediate(bag);}
        }
    }
}
