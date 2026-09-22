using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using UnityEditor;
using UnityEngine;

public static class VS11EquipmentSetup
{
    const string Root = "Assets/_Project/ScriptableObjects/";
    [Serializable] public class Input { public Jewelry[] jewelry; public Gem[] gems; }
    [Serializable] public class Jewelry { public string slot; public Metal[] metals; }
    [Serializable] public class Metal { public string material; public Modifier[] modifiers; }
    [Serializable] public class Gem { public string material; public Modifier[] modifiers; }
    [Serializable] public class Modifier { public string target, key, operation, reference; public float value; }
    static string F(string n) => "<" + n + ">k__BackingField";
    static List<T> All<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[]{Root})
        .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(x => x != null).ToList();
    static string Key(string n) => n.ToLowerInvariant().Replace("żelaza meteorytowego", "meteorytowego żelaza");
    static void Modifiers(SerializedProperty list, Modifier[] rows)
    {
        list.arraySize = rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var m = rows[i]; var p = list.GetArrayElementAtIndex(i);
            p.FindPropertyRelative(F("Target")).intValue = (int)Enum.Parse<JewelryModifierTarget>(m.target);
            var field = m.target == "Stat" ? "StatType" : m.target == "Resistance" ? "ResistanceType" : "GameplayVariable";
            p.FindPropertyRelative(F(field)).intValue = m.target == "Stat" ? (int)Enum.Parse<CharacterStatType>(m.key)
                : m.target == "Resistance" ? (int)Enum.Parse<ResistanceType>(m.key) : (int)Enum.Parse<GameplayVariableType>(m.key);
            p.FindPropertyRelative(F("Operation")).intValue = (int)Enum.Parse<StatModifierOperation>(m.operation);
            p.FindPropertyRelative(F("ReferenceId")).stringValue = m.reference ?? "";
            p.FindPropertyRelative(F("Value")).floatValue = m.value;
        }
    }
    [MenuItem("Godmist/Content/Configure VS-11 Equipment")]
    public static void Configure()
    {
        var input = JsonUtility.FromJson<Input>(File.ReadAllText("Assets/_Project/Scripts/Editor/VS11Content.json"));
        var items = All<ItemDefinition>();
        ItemDefinition Item(string name) => items.Single(x => Key(x.DisplayName) == Key(name));
        var changed = new HashSet<string>();
        void Save(SerializedObject so) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(so.targetObject); changed.Add(AssetDatabase.GetAssetPath(so.targetObject)); }
        var gems = All<JewelryGemDefinition>();
        var selected = new List<JewelryGemDefinition>();
        foreach (var row in input.gems)
        {
            var item = Item(row.material);
            var gem = gems.SingleOrDefault(g => g.CraftingMaterial == item);
            if (gem == null) { gem = ScriptableObject.CreateInstance<JewelryGemDefinition>(); AssetDatabase.CreateAsset(gem, Root + "ContentPass1/JGD_" + item.Id + ".asset"); }
            var so = new SerializedObject(gem);
            so.FindProperty(F("CraftingMaterial")).objectReferenceValue = item;
            so.FindProperty(F("Amount")).intValue = 1;
            Modifiers(so.FindProperty(F("Modifiers")), row.modifiers); Save(so); selected.Add(gem);
        }
        foreach (var row in input.jewelry)
        {
            var definition = All<JewelryItemDefinition>().Single(d => d.JewelrySlot == Enum.Parse<EquipmentSlot>(row.slot));
            var so = new SerializedObject(definition);
            var metals = so.FindProperty(F("Metals")); metals.arraySize = row.metals.Length;
            for (var i = 0; i < row.metals.Length; i++)
            {
                var metal = metals.GetArrayElementAtIndex(i);
                metal.FindPropertyRelative(F("Material")).objectReferenceValue = Item(row.metals[i].material);
                metal.FindPropertyRelative(F("Amount")).intValue = 1;
                Modifiers(metal.FindPropertyRelative(F("Modifiers")), row.metals[i].modifiers);
            }
            var refs = so.FindProperty(F("Gems")); refs.arraySize = selected.Count;
            for (var i = 0; i < selected.Count; i++) refs.GetArrayElementAtIndex(i).objectReferenceValue = selected[i];
            Save(so);
        }
        var colors = new[]{"Czerwony","Niebieski","Purpurowy","Cytrynowy","Szmaragdowy","Czarny","Różowy","Biały","Złoty","Bursztynowy"};
        foreach (var role in All<EnchanterRoleData>())
        {
            var so = new SerializedObject(role);
            var powders = so.FindProperty(F("GaldurytePowders")); powders.arraySize = 10;
            for (var i = 0; i < 10; i++)
            {
                var material = Item(colors[i] + " proszek");
                var row = powders.GetArrayElementAtIndex(i);
                row.FindPropertyRelative(F("Material")).objectReferenceValue = material;
                row.FindPropertyRelative(F("Color")).intValue = i;
            }
            Save(so);
        }
        foreach (var dungeon in All<Godmist.Data.Dungeons.DungeonDefinition>().Where(d => d.Theme == DungeonTheme.Catacombs))
        {
            var so = new SerializedObject(dungeon);
            so.FindProperty(F("EnvironmentConfigured")).boolValue = true;
            so.FindProperty(F("IsIndoor")).boolValue = true;
            so.FindProperty(F("IsCold")).boolValue = true;
            so.FindProperty(F("IsWarm")).boolValue = false;
            Save(so);
        }
        foreach (var unique in All<UniqueEquipmentDefinition>().Where(u => u.Id.StartsWith("unique.deadking.")))
        {
            var tier20 = unique.Id.Contains("sword") ? .35f : unique.Id.Contains("dagger") ? .50f : unique.Id.Contains("staff") ? .06f : .12f;
            var description = unique.Id.Contains("sword") ? "35% szansy przy ataku na powtórzenie umiejętności z 65% obrażeń. Raz na użycie, bez rekurencji."
                : unique.Id.Contains("dagger") ? "50% szansy na niezużycie sztyletu przy użyciu umiejętności."
                : unique.Id.Contains("staff") ? "6% szansy przy ataku na przywołanie szkieleta z 28% maksymalnego HP. Poziom maga, limit 1, do końca walki."
                : "12% szansy na całkowite pochłonięcie obrażeń magicznych.";
            unique.EffectsLevel20.Clear();
            foreach (var effect in unique.Effects)
                unique.EffectsLevel20.Add(JsonUtility.FromJson<Godmist.Data.Effects.PassiveEffectDefinition>(JsonUtility.ToJson(effect)));
            var so = new SerializedObject(unique);
            var effect20 = so.FindProperty(F("EffectsLevel20")).GetArrayElementAtIndex(0);
            effect20.FindPropertyRelative(F("Description")).stringValue = description;
            effect20.FindPropertyRelative(F("Operations")).GetArrayElementAtIndex(0).FindPropertyRelative(F("Value")).FindPropertyRelative(F("NumberValue")).floatValue = tier20;
            Save(so);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("vs11-authored-assets.txt", changed.OrderBy(x => x));
        Debug.Log("VS-11 jewelry configured: 32 metals/slots, 11 gems, 10 powders.");
    }
}
