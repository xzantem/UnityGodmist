using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.Characters;
using Godmist.Data.NPC;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

// Explicit authoring; source values are extracted from content_pass_1.xlsx.
public static class VS10EquipmentSetup
{
    const string Root = "Assets/_Project/ScriptableObjects/";
    const string Output = Root + "ContentPass1/";
    [Serializable] public class Input { public MaterialRow[] materials; public PartRow[] parts; }
    [Serializable] public class MaterialRow { public string name,id,source; public int level,price,discipline,row; public Variant[] variants; }
    [Serializable] public class Variant { public int outputAmount; public Requirement[] ingredients; }
    [Serializable] public class Requirement { public string material; public int amount; }
    [Serializable] public class PartRow { public string id,name,characterClass,material; public int kind,level,cost,row; public StatRow[] stats; }
    [Serializable] public class StatRow { public string name; public float value; }
    public static Input ReadInput() => JsonUtility.FromJson<Input>(File.ReadAllText("Assets/_Project/Scripts/Editor/VS10Content.json"));
    static string F(string n) => "<" + n + ">k__BackingField";
    static void Set(SerializedObject so,string name,object value)
    {
        var p=so.FindProperty(F(name)); if(p==null)throw new InvalidOperationException(name);
        if(value is string text)p.stringValue=text;
        else if(value is bool flag)p.boolValue=flag;
        else if(value is int number)p.intValue=number;
        else p.objectReferenceValue=(UnityEngine.Object)value;
    }
    static List<T> All<T>() where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name,new[]{Root})
        .Select(g=>AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a=>a!=null).ToList();
    static string Key(string s)=>(s??"").Trim().ToLowerInvariant().Replace("głóg","głog").Replace("od nici","do nici");
    static void Save(SerializedObject so){so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(so.targetObject);}
    static void Requirements(SerializedProperty list,Requirement[] rows,Dictionary<string,ItemDefinition> items)
    {
        list.arraySize=rows.Length;
        for(var i=0;i<rows.Length;i++)
        {
            var p=list.GetArrayElementAtIndex(i);
            p.FindPropertyRelative(F("Material")).objectReferenceValue=items[rows[i].material];
            p.FindPropertyRelative(F("Amount")).intValue=rows[i].amount;
        }
    }
    static void AddReferences<T>(SerializedObject so,string name,IEnumerable<T> values) where T:UnityEngine.Object
    {
        var p=so.FindProperty(F(name));var known=new HashSet<UnityEngine.Object>();
        for(var i=0;i<p.arraySize;i++)known.Add(p.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach(var value in values)if(known.Add(value)){var i=p.arraySize++;p.GetArrayElementAtIndex(i).objectReferenceValue=value;}
    }
    [MenuItem("Godmist/Content/Configure VS-10 Equipment")]
    public static void Configure()
    {
        if(!AssetDatabase.IsValidFolder(Output.TrimEnd('/')))AssetDatabase.CreateFolder(Root.TrimEnd('/'),"ContentPass1");
        var input=ReadInput();var existing=All<ItemDefinition>();var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        var items=new Dictionary<string,ItemDefinition>();var changed=new HashSet<string>();
        foreach(var row in input.materials.Where(m=>m.discipline!=0))
        {
            var matches=existing.Where(i=>Key(i.DisplayName)==Key(row.name)).ToList();
            if(matches.Count>1)throw new InvalidOperationException("Ambiguous material: "+row.name);
            var item=matches.FirstOrDefault();
            if(item==null)
            {
                if(row.discipline==0)throw new InvalidOperationException("Missing reward definition: "+row.name);
                item=row.variants.Length>0?ScriptableObject.CreateInstance<CraftableItemDefinition>():ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item,Output+row.id+".asset");
                var init=new SerializedObject(item);Set(init,"Id",row.id);Set(init,"ItemType",(int)ItemType.Material);Set(init,"IsStackable",true);Save(init);
            }
            if(row.variants.Length>0 && !(item is CraftableItemDefinition))throw new InvalidOperationException("Recipe requires type migration: "+row.name);
            var so=new SerializedObject(item);Set(so,"DisplayName",row.name);Set(so,"BaseValue",row.price);Set(so,"MaterialDiscipline",row.discipline);
            if(row.discipline!=0)Set(so,"BaseRarity",row.level==1?(int)RarityTier.Common:(int)RarityTier.Uncommon);
            // Source descriptions document future dungeon drops; stock routing is configured separately.
            if(string.IsNullOrWhiteSpace(item.Description))Set(so,"Description",row.source);
            Save(so);items.Add(row.name,item);changed.Add(AssetDatabase.GetAssetPath(item));
        }
        foreach(var row in input.materials.Where(m=>m.variants.Length>0))
        {
            var so=new SerializedObject(items[row.name]);Set(so,"CraftingDiscipline",row.discipline);Set(so,"OutputAmount",row.variants[0].outputAmount);
            Requirements(so.FindProperty(F("Ingredients")),row.variants[0].ingredients,items);
            var alternatives=so.FindProperty(F("AlternativeRecipes"));alternatives.arraySize=row.variants.Length-1;
            for(var i=1;i<row.variants.Length;i++)
            {
                if(row.variants[i].outputAmount!=row.variants[0].outputAmount)throw new InvalidOperationException("Different output amounts: "+row.name);
                var alt=alternatives.GetArrayElementAtIndex(i-1);alt.FindPropertyRelative("Label").stringValue=string.Join(", ",row.variants[i].ingredients.Select(r=>r.material));
                Requirements(alt.FindPropertyRelative("Ingredients"),row.variants[i].ingredients,items);
            }
            Save(so);
        }
        var parts=All<EquipmentPartDefinition>();var authored=new List<EquipmentPartDefinition>();
        foreach(var row in input.parts)
        {
            var cls=AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_"+row.characterClass+".asset");
            var material=items[row.material];
            var matches=parts.Where(p=>!p.IsStarterOnly && p.RequiredClass==cls && (int)p.Kind==row.kind && (int)p.LevelBracket==row.level
                && p.CraftingMaterials.Count==1 && p.CraftingMaterials[0].Material==material).ToList();
            if(matches.Count>1)throw new InvalidOperationException("Ambiguous part: "+row.name);
            var part=matches.FirstOrDefault();
            if(part==null){part=ScriptableObject.CreateInstance<EquipmentPartDefinition>();AssetDatabase.CreateAsset(part,Output+row.id+".asset");var init=new SerializedObject(part);Set(init,"Id",row.id);Save(init);}
            var so=new SerializedObject(part);Set(so,"DisplayName",row.name);Set(so,"RequiredClass",cls);Set(so,"Kind",row.kind);Set(so,"LevelBracket",row.level);
            Set(so,"Description","content_pass_1.xlsx / Wyposażenie, wiersz "+row.row);
            var stats=so.FindProperty(F("Stats"));foreach(var field in typeof(EquipmentPartStats).GetFields())stats.FindPropertyRelative(field.Name).floatValue=0;
            foreach(var value in row.stats)stats.FindPropertyRelative(value.name).floatValue=value.value;
            Requirements(so.FindProperty(F("CraftingMaterials")),new[]{new Requirement{material=row.material,amount=row.cost}},items);
            Save(so);authored.Add(part);changed.Add(AssetDatabase.GetAssetPath(part));
        }
        var cs=new SerializedObject(catalog);AddReferences(cs,"ItemDefinitions",items.Values);AddReferences(cs,"EquipmentPartDefinitions",authored);Save(cs);changed.Add(AssetDatabase.GetAssetPath(catalog));
        foreach(var role in All<NpcRoleData>().Where(r=>r is IMaterialCraftingSpecialist))
        {
            var discipline=((IMaterialCraftingSpecialist)role).CraftingDiscipline;
            var recipes=items.Values.OfType<CraftableItemDefinition>().Where(r=>r.CraftingDiscipline==discipline);
            var so=new SerializedObject(role);AddReferences(so,"MaterialRecipes",recipes);Save(so);changed.Add(AssetDatabase.GetAssetPath(role));
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("vs10-authored-assets.txt",changed.OrderBy(s=>s));
        Debug.Log($"VS-10 configured: {items.Count} items, {authored.Count} parts.");
    }
}
