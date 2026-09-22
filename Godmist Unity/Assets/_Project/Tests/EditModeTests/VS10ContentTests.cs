using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Data.Characters;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;
namespace Godmist.Tests.EditMode
{
    public sealed class VS10ContentTests
    {
        [Serializable] class Input { public Material[] materials; public Part[] parts; }
        [Serializable] class Material { public string name; public int price,discipline; public Variant[] variants; }
        [Serializable] class Variant { public int outputAmount; public Requirement[] ingredients; }
        [Serializable] class Requirement { public string material; public int amount; }
        [Serializable] class Part { public string name,characterClass,material;public int kind,level,cost;public StatValue[] stats; }
        [Serializable] class StatValue { public string name;public float value; }
        const string Root="Assets/_Project/ScriptableObjects/";
        static Input Source()=>JsonUtility.FromJson<Input>(File.ReadAllText("Assets/_Project/Scripts/Editor/VS10Content.json"));
        static GameContentCatalog Catalog()=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        [Test] public void MaterialsAndEveryRecipeVariantMatchSourceAndHaveStableCatalogIds()
        {
            var catalog=Catalog();var rows=Source().materials.Where(m=>m.discipline!=0).ToArray();Assert.That(rows.Length,Is.EqualTo(180));
            foreach(var row in rows)
            {
                var matches=catalog.ItemDefinitions.Where(i=>i!=null&&i.DisplayName==row.name).ToList();
                Assert.That(matches.Count,Is.EqualTo(1),row.name);var item=matches[0];Assert.That(item.Id,Is.Not.Empty,row.name);
                Assert.That(item.BaseValue,Is.EqualTo(row.price),row.name);
                if(row.variants.Length==0)continue;
                Assert.That(item,Is.InstanceOf<CraftableItemDefinition>(),row.name);var recipe=(CraftableItemDefinition)item;
                Assert.That(recipe.RecipeVariantCount,Is.EqualTo(row.variants.Length),row.name);
                for(var i=0;i<row.variants.Length;i++)
                {
                    Assert.That(recipe.OutputAmount,Is.EqualTo(row.variants[i].outputAmount),row.name);
                    Assert.That(recipe.GetIngredients(i).Select(r=>r.Material.DisplayName+":"+r.Amount),Is.EquivalentTo(row.variants[i].ingredients.Select(r=>r.material+":"+r.amount)),row.name);
                }
                Assert.That(recipe.CalculatedBaseValue,Is.GreaterThan(0),row.name);
            }
        }
        [Test] public void All192PartsMatchSourceAndPreserveStarterIsolation()
        {
            var catalog=Catalog();var parts=Source().parts;Assert.That(parts.Length,Is.EqualTo(192));var ids=new HashSet<string>();
            foreach(var row in parts)
            {
                var cls=AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_"+row.characterClass+".asset");
                var matches=catalog.EquipmentPartDefinitions.Where(p=>p!=null&&!p.IsStarterOnly&&p.RequiredClass==cls&&(int)p.Kind==row.kind&&(int)p.LevelBracket==row.level
                    &&p.CraftingMaterials.Count==1&&p.CraftingMaterials[0].Material.DisplayName==row.material).ToList();
                Assert.That(matches.Count,Is.EqualTo(1),row.name);var part=matches[0];Assert.That(ids.Add(part.Id),Is.True,part.Id);
                Assert.That(part.CraftingMaterials[0].Amount,Is.EqualTo(row.cost),row.name);
                foreach(var stat in row.stats)Assert.That((float)typeof(EquipmentPartStats).GetField(stat.name).GetValue(part.Stats),Is.EqualTo(stat.value).Within(.0001f),row.name+" "+stat.name);
            }
        }
        [Test] public void NeedleUsesMinimumRecipePriceRegardlessOfMetal()
        {
            var items=Catalog().ItemDefinitions.OfType<CraftableItemDefinition>().ToList();
            foreach(var entry in new[]{("Słaba igła do nici",4),("Igła do nici",21)})
            {var item=items.Single(i=>i.DisplayName==entry.Item1);Assert.That(item.RecipeVariantCount,Is.EqualTo(4));Assert.That(item.CalculatedBaseValue,Is.EqualTo(entry.Item2));}
        }
    }
}
