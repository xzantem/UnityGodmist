using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

public static class ItemTooltipCopySetup
{
    [Serializable]
    private sealed class Input
    {
        public Entry[] entries;
    }

    [Serializable]
    private sealed class Entry
    {
        public string id;
        public string kind;
        public string description;
    }

    private const string InputPath = "Assets/_Project/Scripts/Editor/ItemTooltipCopy.json";
    private const string AssetRoot = "Assets/_Project/ScriptableObjects";

    [MenuItem("Godmist/Content/Update Item Tooltip Copy")]
    public static void Configure()
    {
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(InputPath));
        if (input?.entries == null || input.entries.Length == 0)
            throw new InvalidOperationException("Brak opisów przedmiotów w " + InputPath);

        var items = LoadById<ItemDefinition>(definition => definition.Id);
        var parts = LoadById<EquipmentPartDefinition>(definition => definition.Id);
        var changed = new List<string>();
        var seen = new HashSet<string>();

        foreach (var entry in input.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id)
                || string.IsNullOrWhiteSpace(entry.description)
                || (entry.kind != "item" && entry.kind != "part"))
                throw new InvalidOperationException("Niekompletny opis przedmiotu.");
            if (!seen.Add(entry.kind + ":" + entry.id))
                throw new InvalidOperationException("Powtórzony opis: " + entry.id);

            UnityEngine.Object definition = entry.kind == "item"
                ? items.TryGetValue(entry.id, out var item) ? item : null
                : parts.TryGetValue(entry.id, out var part) ? part : null;
            if (definition == null)
                throw new InvalidOperationException("Nie odnaleziono definicji: " + entry.id);

            var serialized = new SerializedObject(definition);
            var description = serialized.FindProperty("<Description>k__BackingField");
            if (description == null)
                throw new InvalidOperationException("Definicja nie ma opisu: " + entry.id);
            if (description.stringValue == entry.description) continue;
            description.stringValue = entry.description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            changed.Add(AssetDatabase.GetAssetPath(definition));
        }

        AssetDatabase.SaveAssets();
        File.WriteAllLines("item-tooltip-authored-assets.txt", changed.OrderBy(path => path));
        Debug.Log($"Opisy przedmiotów: {seen.Count} sprawdzonych, {changed.Count} zmienionych.");
    }

    private static Dictionary<string, T> LoadById<T>(Func<T, string> id) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { AssetRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(id(definition)))
            .GroupBy(id)
            .ToDictionary(group => group.Key, group => group.First());
}
