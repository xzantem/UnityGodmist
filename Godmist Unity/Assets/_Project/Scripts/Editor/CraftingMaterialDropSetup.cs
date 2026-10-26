using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEditor;

namespace Godmist.Editor
{
    public static class CraftingMaterialDropSetup
    {
        public const string CatacombsPath="Assets/_Project/ScriptableObjects/Catacombs/Loot/ID_Catacombs_Resources.asset";
        public const string ForestPath="Assets/_Project/ScriptableObjects/Dungeons/Forest/Loot/ID_Forest_Resources.asset";
        const string CatalogPath="Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";
        static string F(string name)=>"<"+name+">k__BackingField";
        public readonly struct MaterialMean
        {
            public readonly string Id;
            public readonly int Tier;
            public readonly bool Runic;
            public readonly double Catacombs,Forest;
            public MaterialMean(string id,int tier,bool runic,double catacombs,double forest)
            { Id=id;Tier=tier;Runic=runic;Catacombs=catacombs;Forest=forest; }
            public double For(bool forest)=>forest?Forest:Catacombs;
        }
        // Approved per-bag means, including the volume reduction to a Normal
        // expedition budget equivalent to 2.5 average Normal weapons/armors.
        public static IReadOnlyList<MaterialMean> Materials { get; }=new MaterialMean[]
        {
            new("item.smithing.alder_wood", 5, false, 0.0414919982255, 0.0283162195904),
            new("item.smithing.bukomina_fibre", 5, false, 0.0658530772465, 0.184788643414),
            new("item.smithing.copper_ore", 5, false, 0.715736969391, 0.488454787934),
            new("item.smithing.cotton_fibre", 5, false, 0.134848994233, 0.0920277136686),
            new("item.smithing.fox_skin", 5, false, 0.414919982255, 0.283162195904),
            new("item.smithing.goat_skin", 5, false, 0.207459991128, 0.141581097952),
            new("item.smithing.goat_wool_fibre", 5, false, 0.0685969554651, 0.192488170223),
            new("item.smithing.iron_ore", 5, false, 0.710550469612, 0.484915260485),
            new("item.smithing.jute_fibre", 5, false, 0.129662494455, 0.0884881862199),
            new("item.smithing.larch_wood", 5, false, 0.0363054984473, 0.0247766921416),
            new("item.smithing.linden_wood", 5, false, 0.0195491145486, 0.053314287752),
            new("item.smithing.nickel_ore", 5, false, 0.379811368372, 1.03582044775),
            new("item.smithing.poplar_wood", 5, false, 0.0223418451984, 0.0609306145737),
            new("item.smithing.rabbit_skin", 5, false, 0.395849621533, 0.822917000603),
            new("item.smithing.tin_ore", 5, false, 0.388189560322, 1.05866942822),
            new("item.smithing.wild_dog_skin", 5, false, 0.258447273563, 0.537276388823),
            new("item.smithing.birch_wood", 15, false, 0.0406164892369, 0.0252847027719),
            new("item.smithing.boar_skin", 15, false, 0.104442400895, 0.0650178071277),
            new("item.smithing.cedar_wood", 15, false, 0.0406164892369, 0.0252847027719),
            new("item.smithing.chromium", 15, false, 0.348141336316, 0.216726023759),
            new("item.smithing.coal", 15, false, 0.353943691922, 0.220338124155),
            new("item.smithing.copper_ore", 15, false, 0.36921558838, 1.0897861113),
            new("item.smithing.cypress_wood", 15, false, 0.0218704172814, 0.0574582823981),
            new("item.smithing.deer_skin", 15, false, 0.0754306228685, 0.0469573051478),
            new("item.smithing.goat_skin", 15, false, 0.00446335046559, 0.00591021322643),
            new("item.smithing.hawthorn_wood", 15, false, 0.0218704172814, 0.0574582823981),
            new("item.smithing.iron_ore", 15, false, 1.79292788203, 1.11613902236),
            new("item.smithing.larch_wood", 15, false, 0.00892670093119, 0.0118204264529),
            new("item.smithing.llama_wool_fibre", 15, false, 0.0718599424961, 0.188791499308),
            new("item.smithing.meteorite_shard", 15, false, 0.174432561439, 0.514859580141),
            new("item.smithing.monitor_skin", 15, false, 0.156217266296, 0.206857462925),
            new("item.smithing.nickel_ore", 15, false, 0.177339770796, 0.523440573143),
            new("item.smithing.palm_fibre", 15, false, 0.133454178921, 0.0830783091076),
            new("item.smithing.sheep_wool_fibre", 15, false, 0.127651823316, 0.0794662087116),
            new("item.smithing.tiger_skin", 15, false, 0.0165143967227, 0.148074269687),
            new("item.smithing.tin_ore", 15, false, 0.177339770796, 0.523440573143),
            new("item.smithing.wasmina_fibre", 15, false, 0.0687355971701, 0.180583173251),
            new("item.smithing.zinc_piece", 15, false, 0.177339770796, 0.523440573143),
            new("item.runic.amber", 15, true, 0.0174823224223, 0.0262548565675),
            new("item.runic.obsidian", 15, true, 0.01239253235, 0.0186110375669),
            new("item.runic.solid_limestone", 15, true, 0.0331942830804, 0.0498509934826),
            new("item.runic.topaz", 15, true, 0.0174823224223, 0.0262548565675),
            new("item.vs10.ametyst", 15, true, 0.0174823224223, 0.0262548565675),
            new("item.vs10.cytryn", 15, true, 0.0177036176429, 0.0265871965241),
            new("item.vs10.diament", 15, true, 0.0174823224223, 0.0262548565675),
            new("item.vs10.granat", 15, true, 0.0177036176429, 0.0265871965241),
            new("item.vs10.onyks", 15, true, 0.0177036176429, 0.0265871965241),
            new("item.vs10.rubin", 15, true, 0.0177036176429, 0.0265871965241),
            new("item.vs10.szafir", 15, true, 0.0174823224223, 0.0262548565675),
            new("item.vs10.szmaragd", 15, true, 0.0174823224223, 0.0262548565675),
        };
        public static MaterialDiscipline Discipline(ItemDefinition item)=>item is CraftableItemDefinition recipe
            ?recipe.CraftingDiscipline:item?.MaterialDiscipline??MaterialDiscipline.None;
        public static bool IsCraftingPool(LootBagPoolData pool)=>pool?.Entries?.Count>0 && pool.Entries.All(e=>
            e?.ItemDefinition!=null && (Discipline(e.ItemDefinition)==MaterialDiscipline.Smithing || Discipline(e.ItemDefinition)==MaterialDiscipline.Runic));

        [MenuItem("Godmist/Content/Apply calibrated crafting material drop")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            foreach(var path in new[]{CatacombsPath,ForestPath})
            {
                var bag=AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(path);
                if(bag==null)throw new InvalidOperationException("Missing resource bag: "+path);
                var backup=Path.GetFullPath("../LocalHandoffs/crafting-material-drop-20261025/before/"+path);
                if(!File.Exists(backup)){Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup);}
                var data=new SerializedObject(bag);Configure(data,path==ForestPath);
                data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(bag);
            }
        }
        // Used by both complete biome authoring tools as well as focused Apply.
        // Alchemy and all non-crafting fields/pools are left in place.
        public static void Configure(SerializedObject data,bool forest)
        {
            var bag=(LootBagItemDefinition)data.targetObject;
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            var items=catalog.ItemDefinitions.Where(i=>i!=null).GroupBy(i=>i.Id).ToDictionary(g=>g.Key,g=>g.Single());
            var brackets=data.FindProperty(F("Brackets"));
            if(brackets.arraySize!=2)throw new InvalidOperationException("Expected the two authored resource brackets.");
            for(var b=0;b<brackets.arraySize;b++)
            {
                var bracket=brackets.GetArrayElementAtIndex(b);var level=(LootLevelBracket)bracket.FindPropertyRelative(F("Bracket")).intValue;
                var tier=level==LootLevelBracket.Level1To10?5:level==LootLevelBracket.Level11To20?15:0;
                if(tier==0)throw new InvalidOperationException("Unaudited resource bracket.");
                var pools=bracket.FindPropertyRelative(F("Pools"));
                var original=bag.Brackets.Single(x=>x.Bracket==level).Pools;
                for(var p=pools.arraySize-1;p>=0;p--)if(IsCraftingPool(original[p]))pools.DeleteArrayElementAtIndex(p);
                AddCategory(pools,Materials.Where(x=>x.Tier==tier && !x.Runic).ToArray(),forest,items);
                AddCategory(pools,Materials.Where(x=>x.Tier==tier && x.Runic).ToArray(),forest,items);
            }
        }
        static void AddCategory(SerializedProperty pools,MaterialMean[] specs,bool forest,Dictionary<string,ItemDefinition> items)
        {
            var mean=specs.Sum(s=>s.For(forest));if(mean<=0)return;
            var whole=(int)Math.Floor(mean);var fraction=mean-whole;
            // Unit draws: no rounding a rare material up to a guaranteed unit.
            // A single fractional draw keeps additive drop bonuses bounded.
            for(var draw=0;draw<whole+(fraction>1e-10?1:0);draw++)
            {
                var index=pools.arraySize++;var pool=pools.GetArrayElementAtIndex(index);
                pool.FindPropertyRelative(F("DropChance")).floatValue=(float)(draw<whole?1:fraction);
                pool.FindPropertyRelative(F("DistinctSelectionGroup")).stringValue=string.Empty;
                var entries=pool.FindPropertyRelative(F("Entries"));entries.ClearArray();
                foreach(var spec in specs)
                {
                    if(spec.For(forest)<=0)continue;
                    if(!items.TryGetValue(spec.Id,out var item))throw new InvalidOperationException("Missing approved material: "+spec.Id);
                    var entry=entries.GetArrayElementAtIndex(entries.arraySize++);
                    entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue=item;
                    var amount=entry.FindPropertyRelative(F("Amount"));amount.FindPropertyRelative("Min").intValue=1;amount.FindPropertyRelative("Max").intValue=1;
                    entry.FindPropertyRelative(F("SelectionWeight")).floatValue=(float)spec.For(forest);
                    entry.FindPropertyRelative(F("HasFixedEquipmentQuality")).boolValue=false;
                    entry.FindPropertyRelative(F("EquipmentQuality")).intValue=(int)EquipmentQuality.Normal;
                    entry.FindPropertyRelative(F("MinimumGalduryteRarity")).intValue=(int)RarityTier.Common;
                    entry.FindPropertyRelative(F("FixedGalduryteLevel")).intValue=0;
                }
            }
        }
    }
}
