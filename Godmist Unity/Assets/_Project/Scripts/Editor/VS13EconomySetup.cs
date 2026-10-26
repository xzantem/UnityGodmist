using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures the two Arungard merchants that still use legacy InitialOffer lists.
/// Smith stock, prices and restock behaviour are owned by the existing VS-18 work.
/// Run once after VS-10 through VS-12 content has been authored. Safe to rerun.
/// </summary>
public static class VS13EconomySetup
{
    private const string Root = "Assets/_Project/ScriptableObjects/";
    private const string Roles = Root + "NPC/Roles/";
    private const string CatalogPath = Root + "World/GC_MainContentCatalog.asset";

    public readonly struct StockSpec
    {
        public readonly string Name;
        public readonly int Limit;
        public readonly int From;
        public readonly int Through;

        public StockSpec(string name, int limit, int from = 1, int through = 20)
        {
            Name = name;
            Limit = limit;
            From = from;
            Through = through;
        }
    }

    public sealed class GroupSpec
    {
        public readonly string Id;
        public readonly int Slots;
        public readonly StockSpec[] Candidates;

        public GroupSpec(string id, int slots, StockSpec[] candidates)
        {
            Id = id;
            Slots = slots;
            Candidates = candidates;
        }
    }

    private static StockSpec[] Stock(int limit, int from, params string[] names) =>
        names.Select(name => new StockSpec(name, limit, from)).ToArray();

    public static StockSpec[] AlchemistLocal() => new[]
    {
        new StockSpec("Słaby alkohol", 8),
        new StockSpec("Słaby proch strzelniczy", 6),
        new StockSpec("Alkohol", 6, 11),
        new StockSpec("Proch strzelniczy", 4, 11)
    };

    public static GroupSpec[] AlchemistRotating() => new[]
    {
        new GroupSpec("herbs", 3, Stock(1, 1,
            "Purchawka", "Kosarz pospolity", "Szyszka", "Trawa dusicielska",
            "Wodorosty", "Kwitnąca wiśnia", "Aloe meda", "Pnącza alzif",
            "Kwiat byliny", "Dmuchawiec tregandzki", "Sitowie", "Mucha zwyczajna")),
        new GroupSpec("monster-parts", 3, Stock(1, 1,
            "Kość szkieleta", "Mózg umarlaka", "Ucho elfa", "Róg demona",
            "Oko pirata", "Pióro ptaka", "Owoc kaktusa", "Spaczone serce",
            "Skruszona czaszka", "Nerka trolla", "Żyjący liść", "Kieł aligatora")),
        new GroupSpec("catalysts", 2,
            Stock(2, 1, "Proch ziołowy: Zwykły", "Proch ziołowy: Grudkowaty",
                "Proch ziołowy: Lepki", "Proch ziołowy: Luźny")
            .Concat(Stock(2, 11, "Proch ziołowy: Żółty", "Proch ziołowy: Zielony",
                "Proch ziołowy: Różowy", "Proch ziołowy: Pomarańczowy")).ToArray())
    };

    public static StockSpec[] EnchanterLocal() => new[]
    {
        // One dependable metal makes basic jewelry possible without waiting for
        // the rotating offer; the other metal combinations remain variable.
        new StockSpec("Sztabka żelaza", 3),
        new StockSpec("Lity wapień", 3, 11),
        new StockSpec("Wapienny kamień galdurytu", 2, 11)
    };

    public static GroupSpec[] EnchanterRotating() => new[]
    {
        new GroupSpec("jewelry-metals", 2,
            Stock(2, 1, "Sztabka niklu", "Sztabka miedzi", "Sztabka cyny")
            .Concat(Stock(2, 11, "Sztabka stali", "Sztabka inwaru",
                "Sztabka żelaza meteorytowego", "Sztabka brązu")).ToArray()),
        // Gems are required for level-10 jewelry, so their shop gate is level 1.
        // Their material definition's level is not an equipment requirement.
        new GroupSpec("gems", 3, Stock(1, 1,
            "Granat", "Szafir", "Ametyst", "Cytryn", "Szmaragd",
            "Onyks", "Rubin", "Diament", "Topaz", "Bursztyn")),
        new GroupSpec("galduryte-powders", 2, Stock(2, 11,
            "Czerwony proszek", "Niebieski proszek", "Purpurowy proszek",
            "Cytrynowy proszek", "Szmaragdowy proszek", "Czarny proszek",
            "Różowy proszek", "Biały proszek", "Złoty proszek",
            "Bursztynowy proszek"))
    };

    // The old forest sell-only ear and the configured alchemy ear share a label.
    // Keep both catalog IDs for saved loot, but author the merchant with the
    // craftable item rather than resolving that label ambiguously.
    public static IEnumerable<ItemDefinition> StockCatalogItems(GameContentCatalog catalog) =>
        catalog.ItemDefinitions.Where(item => item != null && item.Id != "item.material.elf_ear");

    private static string Field(string name) => $"<{name}>k__BackingField";

    private static ItemDefinition Resolve(
        IReadOnlyDictionary<string, ItemDefinition> items, StockSpec spec)
    {
        if (!items.TryGetValue(spec.Name, out var item))
            throw new InvalidOperationException("VS-13: brak przedmiotu w katalogu: " + spec.Name);
        if (string.IsNullOrWhiteSpace(item.Id) || spec.Limit < 1 || spec.From < 1
            || spec.Through < spec.From)
            throw new InvalidOperationException("VS-13: błędny wpis oferty: " + spec.Name);
        return item;
    }

    private static void WriteEntry(SerializedProperty target, StockSpec spec,
        IReadOnlyDictionary<string, ItemDefinition> items)
    {
        target.FindPropertyRelative("Item").objectReferenceValue = Resolve(items, spec);
        target.FindPropertyRelative("Limit").intValue = spec.Limit;
        target.FindPropertyRelative("MinimumLevel").intValue = spec.From;
        target.FindPropertyRelative("MaximumLevel").intValue = spec.Through;
    }

    private static void ConfigureRole(TraderRoleData role, StockSpec[] local,
        GroupSpec[] rotating, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        if (role == null) throw new InvalidOperationException("VS-13: brak roli handlarza.");
        var owner = new SerializedObject(role);
        owner.FindProperty(Field("InitialOffer")).ClearArray();
        var fixedStock = owner.FindProperty(Field("LocalStock"));
        fixedStock.arraySize = local.Length;
        for (var index = 0; index < local.Length; index++)
            WriteEntry(fixedStock.GetArrayElementAtIndex(index), local[index], items);

        var groups = owner.FindProperty(Field("RotatingStock"));
        groups.arraySize = rotating.Length;
        for (var index = 0; index < rotating.Length; index++)
        {
            var spec = rotating[index];
            var group = groups.GetArrayElementAtIndex(index);
            group.FindPropertyRelative("Id").stringValue = spec.Id;
            group.FindPropertyRelative("Slots").intValue = spec.Slots;
            var candidates = group.FindPropertyRelative("Candidates");
            candidates.arraySize = spec.Candidates.Length;
            for (var candidate = 0; candidate < spec.Candidates.Length; candidate++)
                WriteEntry(candidates.GetArrayElementAtIndex(candidate),
                    spec.Candidates[candidate], items);
        }
        owner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(role);
    }

    [MenuItem("Godmist/Content/Configure VS-13 Arungard Economy")]
    public static void Configure()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("VS-13: brak katalogu treści.");
        var grouped = StockCatalogItems(catalog)
            .ToLookup(item => item.DisplayName, StringComparer.Ordinal);
        var alchemistLocal = AlchemistLocal();
        var alchemistRotating = AlchemistRotating();
        var enchanterLocal = EnchanterLocal();
        var enchanterRotating = EnchanterRotating();
        // Validate every reference before mutating either asset.
        var required = alchemistLocal.Concat(enchanterLocal)
            .Concat(alchemistRotating.Concat(enchanterRotating)
                .SelectMany(group => group.Candidates)).ToArray();
        var missingOrAmbiguous = required.Select(spec => spec.Name).Distinct()
            .Where(name => grouped[name].Count() != 1).ToArray();
        if (missingOrAmbiguous.Length > 0)
            throw new InvalidOperationException("VS-13: brak lub niejednoznaczne nazwy w katalogu: "
                + string.Join(", ", missingOrAmbiguous));
        var items = required.Select(spec => spec.Name).Distinct()
            .ToDictionary(name => name, name => grouped[name].Single(), StringComparer.Ordinal);
        foreach (var spec in required)
            Resolve(items, spec);
        var alchemist = AssetDatabase.LoadAssetAtPath<TraderRoleData>(
            Roles + "TRD_Arungard_Alchemist.asset");
        var enchanter = AssetDatabase.LoadAssetAtPath<TraderRoleData>(
            Roles + "TRD_Arungard_Enchanter.asset");
        var enchanterServices = AssetDatabase.LoadAssetAtPath<EnchanterRoleData>(
            Roles + "ER_Arungard_Enchanter.asset");
        if (alchemist == null || enchanter == null || enchanterServices == null
            || enchanterServices.GalduryteStones.Count < 1
            || enchanterServices.GalduryteStones[0].Material != items["Wapienny kamień galdurytu"])
            throw new InvalidOperationException("VS-13: brak roli Zaklinacza lub kamienia T1.");
        ConfigureRole(alchemist, alchemistLocal, alchemistRotating, items);
        ConfigureRole(enchanter, enchanterLocal, enchanterRotating, items);
        var services = new SerializedObject(enchanterServices);
        services.FindProperty(Field("GalduryteStones")).arraySize = 1;
        services.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enchanterServices);
        AssetDatabase.SaveAssets();
        Debug.Log("VS-13: skonfigurowano ofertę Alchemika i Zaklinacza Arungardu.");
    }
}
