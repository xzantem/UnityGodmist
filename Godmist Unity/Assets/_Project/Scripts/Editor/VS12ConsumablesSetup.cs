using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Explicit, repeatable VS-12 authoring from content_pass_1.xlsx (Consumable
/// rows 3-28 and Materiały rows 148-171). Run from the Editor after VS-10.
/// It changes only the alchemist role, three utility recipes and catalog refs.
/// Dungeon drops and merchant stock belong to VS-15 and VS-13 respectively.
/// </summary>
public static class VS12ConsumablesSetup
{
    private const string Root = "Assets/_Project/ScriptableObjects/";
    private const string Output = Root + "ContentPass1/";
    private static string Field(string name) => $"<{name}>k__BackingField";

    private sealed class EffectRow
    {
        public readonly string Herb;
        public readonly string MonsterPart;
        public readonly ConsumableRuntimeEffectType Potion;
        public readonly float PotionValue;
        public readonly int PotionDuration;
        public readonly ConsumableRuntimeEffectType Bomb;
        public readonly float BombValue;
        public readonly int BombDuration;

        public EffectRow(string herb, string monsterPart,
            ConsumableRuntimeEffectType potion, float potionValue, int potionDuration,
            ConsumableRuntimeEffectType bomb, float bombValue, int bombDuration)
        {
            Herb = herb;
            MonsterPart = monsterPart;
            Potion = potion;
            PotionValue = potionValue;
            PotionDuration = potionDuration;
            Bomb = bomb;
            BombValue = bombValue;
            BombDuration = bombDuration;
        }
    }

    private static readonly EffectRow[] Effects =
    {
        new("Purchawka", "Kość szkieleta", ConsumableRuntimeEffectType.RestoreHealth, .15f, 0,
            ConsumableRuntimeEffectType.DealPhysicalDamage, .15f, 0),
        new("Kosarz pospolity", "Mózg umarlaka", ConsumableRuntimeEffectType.RegenHealth, .25f, 10,
            ConsumableRuntimeEffectType.DealMagicalDamage, .15f, 0),
        new("Szyszka", "Ucho elfa", ConsumableRuntimeEffectType.RestoreResource, .20f, 0,
            ConsumableRuntimeEffectType.ApplyBleed, .10f, 4),
        new("Trawa dusicielska", "Róg demona", ConsumableRuntimeEffectType.RegenResource, .30f, 10,
            ConsumableRuntimeEffectType.ApplyPoison, .10f, 4),
        new("Wodorosty", "Oko pirata", ConsumableRuntimeEffectType.ModifyMaxResource, .10f, 10,
            ConsumableRuntimeEffectType.ApplyBurn, .10f, 3),
        new("Kwitnąca wiśnia", "Pióro ptaka", ConsumableRuntimeEffectType.ModifyDamage, .10f, 8,
            ConsumableRuntimeEffectType.ApplyStun, .60f, 2),
        new("Aloe meda", "Owoc kaktusa", ConsumableRuntimeEffectType.ModifyDefense, .10f, 8,
            ConsumableRuntimeEffectType.ModifyPhysicalDefense, .10f, 3),
        new("Pnącza alzif", "Spaczone serce", ConsumableRuntimeEffectType.ModifyAllResistances, .10f, 8,
            ConsumableRuntimeEffectType.ModifyMagicalDefense, .10f, 3),
        new("Kwiat byliny", "Skruszona czaszka", ConsumableRuntimeEffectType.ModifySpeed, .15f, 4,
            ConsumableRuntimeEffectType.ModifyDodge, .10f, 3),
        new("Dmuchawiec tregandzki", "Nerka trolla", ConsumableRuntimeEffectType.ModifyCritChance, .05f, 8,
            ConsumableRuntimeEffectType.ModifyAccuracy, 5f, 3),
        new("Sitowie", "Żyjący liść", ConsumableRuntimeEffectType.ModifyDodge, 10f, 8,
            ConsumableRuntimeEffectType.Slow, 2f, 3),
        new("Mucha zwyczajna", "Kieł aligatora", ConsumableRuntimeEffectType.ModifyActionPoints, .10f, 8,
            ConsumableRuntimeEffectType.ApplyFreeze, .55f, 2)
    };

    private static List<T> All<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Root })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToList();

    private static ItemDefinition Material(IReadOnlyList<ItemDefinition> items, string name)
    {
        var matches = items.Where(item => item.ItemType == ItemType.Material
            && string.Equals(item.DisplayName, name, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"VS-12: oczekiwano jednego materiału '{name}', znaleziono {matches.Count}.");
        return matches[0];
    }

    private static SerializedProperty Property(SerializedObject owner, string name) =>
        owner.FindProperty(Field(name))
        ?? throw new InvalidOperationException($"VS-12: brak pola {name} w {owner.targetObject.name}.");

    private static void Apply(SerializedObject owner)
    {
        owner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner.targetObject);
    }

    private static void Describe(ItemDefinition item, string description, int? value = null)
    {
        var owner = new SerializedObject(item);
        Property(owner, "Description").stringValue = description;
        if (value.HasValue) Property(owner, "BaseValue").intValue = value.Value;
        Apply(owner);
    }

    private static void ConfigureUtilityProduct(ConsumableItemDefinition item, string description)
    {
        var owner = new SerializedObject(item);
        Property(owner, "Description").stringValue = description;
        Property(owner, "IsStackable").boolValue = true;
        Property(owner, "MaxCharges").intValue = 1;
        Apply(owner);
    }

    private static void SetEffect(SerializedProperty property, ItemDefinition material,
        ConsumableRuntimeEffectType type, float value, int duration, bool bomb)
    {
        property.FindPropertyRelative(Field("SourceIngredient")).objectReferenceValue = material;
        property.FindPropertyRelative(Field("EffectType")).intValue = (int)type;
        property.FindPropertyRelative(Field("Value")).floatValue = value;
        property.FindPropertyRelative(Field("DurationTurns")).intValue = duration;
        property.FindPropertyRelative(Field("Chance")).floatValue = 1f;
        property.FindPropertyRelative(Field("TargetModeIndex")).intValue = 0;
        property.FindPropertyRelative(Field("StatusId")).stringValue = string.Empty;
        property.FindPropertyRelative(Field("ResistanceType")).intValue = (int)ResistanceType.Bleed;
        property.FindPropertyRelative(Field("TargetType")).intValue = (int)(bomb
            ? TargetType.AreaAroundTile : TargetType.Single);
        property.FindPropertyRelative(Field("TargetingType")).intValue = (int)(bomb
            ? TargetingType.Enemies : TargetingType.Allies);
    }

    private static void SetMediums(SerializedProperty property,
        IReadOnlyList<ItemDefinition> items, string weak, string normal)
    {
        property.arraySize = 2;
        var names = new[] { weak, normal };
        for (var index = 0; index < names.Length; index++)
        {
            var entry = property.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(Field("Material")).objectReferenceValue = Material(items, names[index]);
            entry.FindPropertyRelative(Field("Tier")).intValue = index + 1;
        }
    }

    private static void SetRequirements(SerializedProperty property,
        IReadOnlyList<(ItemDefinition Material, int Amount)> requirements)
    {
        property.arraySize = requirements.Count;
        for (var index = 0; index < requirements.Count; index++)
        {
            var entry = property.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(Field("Material")).objectReferenceValue = requirements[index].Material;
            entry.FindPropertyRelative(Field("Amount")).intValue = requirements[index].Amount;
        }
    }

    private static CraftableItemDefinition ConfigureRecipe(string key, string name,
        ConsumableItemDefinition product,
        IReadOnlyList<(string Label, (ItemDefinition Material, int Amount)[] Ingredients)> variants)
    {
        if (variants.Count == 0) throw new InvalidOperationException("VS-12: pusta receptura " + name);
        var path = Output + "recipe.vs12." + key + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<CraftableItemDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<CraftableItemDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }
        var owner = new SerializedObject(recipe);
        Property(owner, "Id").stringValue = "recipe.vs12." + key;
        Property(owner, "DisplayName").stringValue = name;
        Property(owner, "Description").stringValue = product.Description;
        Property(owner, "ItemType").intValue = (int)ItemType.Tool;
        Property(owner, "CraftingDiscipline").intValue = (int)MaterialDiscipline.Alchemy;
        Property(owner, "MaterialDiscipline").intValue = (int)MaterialDiscipline.Alchemy;
        Property(owner, "OutputAmount").intValue = 1;
        Property(owner, "ResultLevel").intValue = 11;
        Property(owner, "PrimaryVariantLabel").stringValue = variants[0].Label;
        Property(owner, "CraftedResult").objectReferenceValue = product;
        SetRequirements(Property(owner, "Ingredients"), variants[0].Ingredients);
        var alternatives = Property(owner, "AlternativeRecipes");
        alternatives.arraySize = variants.Count - 1;
        for (var index = 1; index < variants.Count; index++)
        {
            var variant = alternatives.GetArrayElementAtIndex(index - 1);
            variant.FindPropertyRelative("Label").stringValue = variants[index].Label;
            SetRequirements(variant.FindPropertyRelative("Ingredients"), variants[index].Ingredients);
        }
        Apply(owner);
        return recipe;
    }

    [MenuItem("Godmist/Content/Configure VS-12 Consumables")]
    public static void Configure()
    {
        var items = All<ItemDefinition>();
        var role = All<AlchemistRoleData>().Single();
        var owner = new SerializedObject(role);
        var ingredients = Property(owner, "IngredientOptions");
        ingredients.arraySize = Effects.Length * 2;
        for (var row = 0; row < Effects.Length; row++)
        {
            var effect = Effects[row];
            for (var member = 0; member < 2; member++)
            {
                var material = Material(items, member == 0 ? effect.Herb : effect.MonsterPart);
                if (material.MaterialDiscipline != MaterialDiscipline.Alchemy)
                    throw new InvalidOperationException("VS-12: składnik nie należy do alchemii: " + material.DisplayName);
                var entry = ingredients.GetArrayElementAtIndex(row * 2 + member);
                entry.FindPropertyRelative(Field("Material")).objectReferenceValue = material;
                SetEffect(entry.FindPropertyRelative(Field("PotionEffect")), material,
                    effect.Potion, effect.PotionValue, effect.PotionDuration, false);
                SetEffect(entry.FindPropertyRelative(Field("BombEffect")), material,
                    effect.Bomb, effect.BombValue, effect.BombDuration, true);
                entry.FindPropertyRelative(Field("EffectAdjective")).stringValue = string.Empty;
            }
        }
        SetMediums(Property(owner, "PotionAlcohols"), items, "Słaby alkohol", "Alkohol");
        SetMediums(Property(owner, "BombGunpowders"), items,
            "Słaby proch strzelniczy", "Proch strzelniczy");
        var catalysts = Property(owner, "Catalysts");
        catalysts.arraySize = 4;
        var catalystNames = new[]
        {
            "Proch ziołowy: Zwykły", "Proch ziołowy: Grudkowaty",
            "Proch ziołowy: Lepki", "Proch ziołowy: Luźny"
        };
        var potionCatalysts = new[]
        {
            ConsumableCatalystType.Duration, ConsumableCatalystType.Strength,
            ConsumableCatalystType.Condensation, ConsumableCatalystType.Charges
        };
        var bombCatalysts = new[]
        {
            ConsumableCatalystType.Duration, ConsumableCatalystType.Strength,
            ConsumableCatalystType.Range, ConsumableCatalystType.Charges
        };
        for (var index = 0; index < catalystNames.Length; index++)
        {
            var entry = catalysts.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(Field("Material")).objectReferenceValue = Material(items, catalystNames[index]);
            entry.FindPropertyRelative(Field("CatalystType")).intValue = (int)potionCatalysts[index];
            entry.FindPropertyRelative(Field("BombCatalystType")).intValue = (int)bombCatalysts[index];
            entry.FindPropertyRelative(Field("HasBombCatalystOverride")).boolValue = true;
            entry.FindPropertyRelative(Field("Tier")).intValue = 1;
        }
        Apply(owner);

        var alcohol = Material(items, "Alkohol");
        var fabrics = items.Where(item => item.ItemType == ItemType.Material
            && item.MaterialDiscipline == MaterialDiscipline.Smithing
            && item.DisplayName.Contains("tkanina", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.DisplayName).ToList();
        if (fabrics.Count < 4)
            throw new InvalidOperationException("VS-12: oczekiwano tkanin z VS-10.");
        var herbs = Effects.Select(effect => Material(items, effect.Herb)).ToList();
        var bandage = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(
            Root + "Items/Consumables/CID_Bandage.asset");
        var antidote = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(
            Root + "Items/Consumables/CID_Antidote.asset");
        var towel = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(
            Root + "Items/Consumables/CID_DampTowel.asset");
        if (bandage == null || antidote == null || towel == null)
            throw new InvalidOperationException("VS-12: brak definicji jednego z przedmiotów pomocniczych.");
        ConfigureUtilityProduct(bandage, "Opatrunek tamujący krwawienie. Zwiększa odporność na krwawienie o 30% na 3 tury.");
        ConfigureUtilityProduct(antidote, "Mieszanka ziół neutralizująca truciznę. Zwiększa odporność na zatrucie o 30% na 3 tury.");
        ConfigureUtilityProduct(towel, "Sucha szmatka do stłumienia płomieni. Zwiększa odporność na ogień o 30% na 3 tury.");
        var returnScroll = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(
            Root + "Items/Consumables/CID_ReturnScroll.asset");
        if (returnScroll != null)
            Describe(returnScroll, "Jednorazowy zwój przenoszący drużynę z wyprawy do Arungardu.");

        var bandageVariants = fabrics.Select(fabric =>
            (fabric.DisplayName, new[] { (alcohol, 1), (fabric, 2) })).ToList();
        var towelVariants = fabrics.Select(fabric =>
            (fabric.DisplayName, new[] { (fabric, 4) })).ToList();
        var antidoteVariants = new List<(string, (ItemDefinition, int)[])>();
        for (var first = 0; first < herbs.Count; first++)
        for (var second = first; second < herbs.Count; second++)
            antidoteVariants.Add(($"{herbs[first].DisplayName} + {herbs[second].DisplayName}",
                first == second
                    ? new[] { (alcohol, 1), (herbs[first], 8) }
                    : new[] { (alcohol, 1), (herbs[first], 4), (herbs[second], 4) }));
        var recipes = new[]
        {
            ConfigureRecipe("bandage", "Bandaż", bandage, bandageVariants),
            ConfigureRecipe("antidote", "Antidotum", antidote, antidoteVariants),
            ConfigureRecipe("dry_towel", "Sucha szmatka", towel, towelVariants)
        };
        Describe(bandage, bandage.Description, recipes[0].CalculatedBaseValue);
        Describe(antidote, antidote.Description, recipes[1].CalculatedBaseValue);
        Describe(towel, towel.Description, recipes[2].CalculatedBaseValue);

        owner = new SerializedObject(role);
        var recipeList = Property(owner, "MaterialRecipes");
        var known = new HashSet<UnityEngine.Object>();
        for (var index = 0; index < recipeList.arraySize; index++)
            known.Add(recipeList.GetArrayElementAtIndex(index).objectReferenceValue);
        foreach (var recipe in recipes)
            if (known.Add(recipe))
                recipeList.GetArrayElementAtIndex(recipeList.arraySize++).objectReferenceValue = recipe;
        Apply(owner);

        var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            Root + "World/GC_MainContentCatalog.asset");
        if (catalog == null) throw new InvalidOperationException("VS-12: brak głównego katalogu gry.");
        owner = new SerializedObject(catalog);
        var catalogItems = Property(owner, "ItemDefinitions");
        var catalogKnown = new HashSet<UnityEngine.Object>();
        for (var index = 0; index < catalogItems.arraySize; index++)
            catalogKnown.Add(catalogItems.GetArrayElementAtIndex(index).objectReferenceValue);
        foreach (var product in new ItemDefinition[] { bandage, antidote, towel }
            .Concat(Effects.SelectMany(effect => new[]
                { Material(items, effect.Herb), Material(items, effect.MonsterPart) })))
            if (catalogKnown.Add(product))
                catalogItems.GetArrayElementAtIndex(catalogItems.arraySize++).objectReferenceValue = product;
        Apply(owner);
        AssetDatabase.SaveAssets();
        Debug.Log($"VS-12: {Effects.Length * 2} składniki, 4 katalizatory, 3 receptury użytkowe skonfigurowane.");
    }

    [MenuItem("Godmist/Content/Configure VS-12 Recipe Selector UI")]
    public static void ConfigureRecipeSelectorUi()
    {
        const string path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var materialView = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                ?? throw new InvalidOperationException("VS-12: nie znaleziono widoku receptur materiałów.");
            var alchemyView = root.GetComponentsInChildren<AlchemyCraftingWindowView>(true)
                .FirstOrDefault(view => view.gameObject.name == "AlchemyCraftingServiceRoot")
                ?? throw new InvalidOperationException("VS-12: nie znaleziono wzoru sekcji alchemika.");
            var source = alchemyView.transform.Find("AssemblyColumn/SettingsSection")
                ?? throw new InvalidOperationException("VS-12: brak sekcji ustawień alchemika.");
            var summary = materialView.transform.Find("SummaryColumn")
                ?? throw new InvalidOperationException("VS-12: brak prawej kolumny receptur.");

            var section = summary.Find("VariantSection");
            if (section == null)
            {
                section = UnityEngine.Object.Instantiate(source.gameObject, summary, false).transform;
                section.name = "VariantSection";
            }
            section.SetSiblingIndex(1);
            var title = section.Find("SectionTitle")?.GetComponent<TextMeshProUGUI>()
                ?? throw new InvalidOperationException("VS-12: brak tytułu sekcji wariantów.");
            title.text = "Wybierz składniki";
            var first = section.Find("MediumDropdown")?.GetComponent<TMP_Dropdown>()
                ?? section.Find("FirstVariantDropdown")?.GetComponent<TMP_Dropdown>()
                ?? throw new InvalidOperationException("VS-12: brak pierwszego dropdownu wariantu.");
            var second = section.Find("CatalystDropdown")?.GetComponent<TMP_Dropdown>()
                ?? section.Find("SecondVariantDropdown")?.GetComponent<TMP_Dropdown>()
                ?? throw new InvalidOperationException("VS-12: brak drugiego dropdownu wariantu.");
            first.name = "FirstVariantDropdown";
            second.name = "SecondVariantDropdown";

            var serialized = new SerializedObject(materialView);
            serialized.FindProperty("variantSection").objectReferenceValue = section.gameObject;
            serialized.FindProperty("firstVariantDropdown").objectReferenceValue = first;
            serialized.FindProperty("secondVariantDropdown").objectReferenceValue = second;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("VS-12: selektory składników receptur podłączone do prawego panelu.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
