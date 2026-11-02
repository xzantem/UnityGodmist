using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

public static class VS22ItemArtSetup
{
    private const string ArtRoot = "Assets/_Project/Art/Icons/Items/VS22/";
    private const string LibraryAssetPath = "Assets/Resources/Godmist/ItemIcons/ItemIconLibrary.asset";
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string ReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review"));
    private static string Field(string name) => "<" + name + ">k__BackingField";

    [MenuItem("Godmist/Content/Configure VS-22 item art")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring item art.");
        EnsureFolder("Assets/Resources/Godmist/ItemIcons");
        EnsureFolder(ArtRoot);
        var definitions = All<ItemDefinition>();
        var jewelryDefinitions = AllJewelryDefinitions();
        var parts = All<EquipmentPartDefinition>();
        var unavailable = new List<string>();
        var imported = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        Sprite Import(string relativePath, string targetName = null)
        {
            var cacheKey = relativePath + "|" + targetName;
            if (imported.TryGetValue(cacheKey, out var cached)) return cached;
            var sprite = ImportSprite(relativePath, targetName ?? Path.GetFileName(relativePath), unavailable);
            imported[cacheKey] = sprite;
            return sprite;
        }
        void BindIds(Sprite sprite, params string[] ids)
        {
            if (sprite == null) return;
            foreach (var definition in definitions)
                if (definition != null && ids.Contains(definition.Id, StringComparer.Ordinal)) SetIcon(definition, sprite);
        }
        void BindNames(Sprite sprite, params string[] names)
        {
            if (sprite == null) return;
            foreach (var definition in definitions)
                if (definition != null && names.Any(name => string.Equals(definition.DisplayName, name, StringComparison.OrdinalIgnoreCase))) SetIcon(definition, sprite);
        }

        var equipment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warrior.weapon"] = "equipment-icons/long-sword-32-v1.png",
            ["warrior.armor"] = "equipment-icons/chainmail-32-v1.png",
            ["scout.weapon"] = "equipment-icons/sword-dagger-32-v1.png",
            ["scout.armor"] = "equipment-icons/tunic-32-v1.png",
            ["mage.weapon"] = "equipment-icons/claw-wand-32-v1.png",
            ["mage.armor"] = "equipment-icons/robe-32-v1.png",
            ["paladin.weapon"] = "equipment-icons/war-hammer-32-v1.png",
            ["paladin.armor"] = "equipment-icons/cuirass-32-v1.png"
        };
        foreach (var role in new[] { "warrior", "scout", "mage", "paladin" })
        foreach (var slot in new[] { "weapon", "armor" })
        {
            var sprite = Import(equipment[role + "." + slot]);
            var productionSuffix = role == "warrior"
                ? (slot == "weapon" ? "longsword" : "hauberk")
                : role == "scout"
                    ? (slot == "weapon" ? "sword_dagger" : "tunic")
                    : role == "mage"
                        ? (slot == "weapon" ? "wand" : "robe")
                        : (slot == "weapon" ? "hammer" : "plate_armor");
            BindIds(sprite, "equipment." + role + "." + productionSuffix, "equipment.start." + role + "." + slot);
            if (role == "paladin" && slot == "armor") BindIds(sprite, "unique.deadking.armor.paladin");
            if (slot == "weapon") BindIds(sprite, role == "warrior" ? "unique.deadking.sword.warrior" : role == "scout" ? "unique.deadking.dagger.scout" : role == "mage" ? "unique.deadking.staff.mage" : string.Empty);
            var roleToken = role == "warrior" ? "Warrior" : role == "scout" ? "Scout" : role == "mage" ? "Mage" : "Paladin";
            foreach (var part in parts)
            {
                var id = part?.Id ?? string.Empty;
                var roleMatch = id.IndexOf("part." + role + ".", StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf("part.start." + role + ".", StringComparison.OrdinalIgnoreCase) >= 0;
                var classMatch = part?.RequiredClass != null && part.RequiredClass.name.IndexOf(roleToken, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!roleMatch && !classMatch) continue;
                var weapon = (int)part.Kind <= (int)Godmist.Core.Enums.EquipmentPartKind.WeaponHandle || id.IndexOf("weapon", StringComparison.OrdinalIgnoreCase) >= 0;
                var armor = (int)part.Kind >= (int)Godmist.Core.Enums.EquipmentPartKind.ArmorPlate || id.IndexOf("armor", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((slot == "weapon" && weapon) || (slot == "armor" && armor)) SetIcon(part, sprite);
            }
        }

        BindIds(Import("utility-icons/antidote-32-v1.png"), "item.consumable.antidote", "recipe.vs12.antidote");
        BindIds(Import("utility-icons/bandage-32-v1.png"), "item.consumable.bandage", "recipe.vs12.bandage");
        BindIds(Import("utility-icons/dry-cloth-32-v1.png"), "item.consumable.damp_towel", "recipe.vs12.dry_towel");
        BindIds(Import("utility-icons/return-scroll-32-v1.png"), "item.consumable.return_scroll");
        BindIds(Import("loot-bag-icons/loot-bag-bone-v2-32.png"), "item.lootbag.catacombs.resources", "item.lootbag.catacombs.armor", "item.lootbag.catacombs.weapons", "item.lootbag.catacombs.galdurytes", "item.lootbag.catacombs.test.01");
        foreach (var color in new[] { "red", "blue", "purple" })
            BindIds(Import("gem-powder-icons/gem-powder-" + color + "-32.png"), "item.runic.galduryte_powder." + color);
        BindIds(Import("loot-bag-icons/loot-bag-dead-king-v2-32.png"), "item.lootbag.catacombs.dead_king");
        BindIds(Import("potion-modular/potion-neutral-normalized-20x28.png", "potion-neutral-32.png"), "item.consumable.potion");
        BindIds(Import("bomb-modular/bomb-neutral-normalized-21x28.png", "bomb-neutral-32.png"), "item.consumable.bomb");
        BindIds(Import("galduryte-modular/galduryte-base-32.png", "galduryte-neutral-32.png"), "item.galduryte.generic");
        BindIds(Import("needle-icons/thread-needle-32-v2.png"), "item.vs10.igla_do_nici", "item.smithing.thread_needle");
        BindIds(Import("needle-icons/weak-thread-needle-32-v2.png"), "item.smithing.weak_thread_needle");
        BindNames(Import("runeforging-stone-icons/solid-limestone-32-v2.png"), "Lity wapień");
        BindNames(Import("runeforging-stone-icons/limestone-galduryte-stone-32-v2.png"), "Wapienny kamień galdurytu");
        BindStaticMaterialArt(definitions, relativePath =>
            Import(relativePath, relativePath.Replace('/', '-')));
        BindJewelryArt(jewelryDefinitions, unavailable);
        var variants = BuildJewelryVariants(jewelryDefinitions, unavailable);
        SaveLibrary(variants);
        ConfigureGalduryteLayers(unavailable);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var warning in unavailable.Distinct(StringComparer.Ordinal)) Debug.LogWarning("VS-22 item art unavailable: " + warning);
        Debug.Log("VS-22 item art configured: " + definitions.Count + " definitions, " + parts.Count + " parts, " + variants.Count + " variants.");
        File.WriteAllLines(Path.Combine(ProjectRoot, "TestResults", "VS22-item-coverage.txt"),
            definitions.OrderBy(item => item.Id).Select(item =>
                (AssetDatabase.GetAssetPath(item.Icon).StartsWith(ArtRoot, StringComparison.Ordinal) ? "BOUND" : "MISSING")
                + "\t" + item.Id + "\t" + item.DisplayName + "\t" + AssetDatabase.GetAssetPath(item.Icon)));
    }

    private static void ConfigureGalduryteLayers(ICollection<string> unavailable)
    {
        Texture2D ImportLayer(string name, string folder = "galduryte-modular")
        {
            var sprite = ImportSprite(folder + "/" + name + ".png", "modular-" + name + ".png", unavailable);
            if (sprite == null) return null;
            var path = AssetDatabase.GetAssetPath(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var library = AssetDatabase.LoadAssetAtPath<ItemIconLibrary>(LibraryAssetPath);
        var serialized = new SerializedObject(library);
        serialized.FindProperty("galduryteBase").objectReferenceValue = ImportLayer("galduryte-base-32");
        var panels = serialized.FindProperty("galdurytePanels");
        panels.arraySize = 6;
        var colors = new[] { "red", "blue", "purple", "lemon", "emerald", "black" };
        for (var index = 0; index < 6; index++)
            panels.GetArrayElementAtIndex(index).objectReferenceValue = ImportLayer("effect-layer-" + (index + 1).ToString("00") + "-" + colors[index] + "-32");
        foreach (var kind in new[] { "potion", "bomb" })
        {
            var names = kind == "potion"
                ? new[] { "potion-single-red-v3-weights", "potion-half-green-violet-v3-weights", "potion-thirds-blue-red-pink-v3-weights" }
                : new[] { "bomb-single-ochre-v3-weights", "bomb-half-ochre-violet-v3-weights", "bomb-thirds-ochre-violet-green-v3-weights" };
            var weights = serialized.FindProperty(kind + "Weights");
            weights.arraySize = 3;
            for (var index = 0; index < 3; index++) weights.GetArrayElementAtIndex(index).objectReferenceValue = ImportLayer(names[index], kind + "-modular");
            serialized.FindProperty(kind + "Shading").objectReferenceValue = ImportLayer(kind == "potion" ? "liquid-shading" : "bomb-powder-shading", kind + "-modular");
            serialized.FindProperty(kind + "Overlay").objectReferenceValue = ImportLayer(kind == "potion" ? "glass-overlay" : "bomb-body-overlay", kind + "-modular");
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
    }

    private static void BindStaticMaterialArt(IReadOnlyList<ItemDefinition> definitions, Func<string, Sprite> import)
    {
        var woods = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alder"]="olch",["birch"]="brzoz",["cedar"]="cedr",["cypress"]="cyprys",["hawthorn"]="głog",["larch"]="modrzewi",["linden"]="lip",["poplar"]="topol"
        };
        foreach (var item in woods)
        {
            BindByName(definitions, import("plank-icons/" + item.Key + "-32-v1.png"), item.Value + "owa deska");
            BindByName(definitions, import("stick-icons/" + item.Key + "-32-v1.png"), item.Value + "owy patyk");
            BindByName(definitions, import("wood-icons/" + item.Key + "-32-v1.png"), "Drewno " + item.Value + "owe");
        }

        var fibers = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["bukomina"]=("bukominy","bukominowa","Bukominowa nitka"),["cotton"]=("bawełny","bawełniana","Bawełniana nitka"),["goat-wool"]=("koziej wełny","kozia","Nitka z koziej wełny"),["jute"]=("juty","jutowa","Jutowa nitka"),["llama-wool"]=("lamiej wełny","lamia","Nitka z lamiej wełny"),["palm"]=("palmy","palmowa","Palmowa nitka"),["sheep-wool"]=("owczej wełny","owcza","Nitka z owczej wełny"),["wasmina"]=("wasminy","wasminowa","Wasminowa nitka")
        };
        foreach (var item in fibers)
        {
            BindByName(definitions, import("fiber-icons/" + item.Key + "-32-v1.png"), "Włókno " + item.Value.Item1);
            BindByName(definitions, import("fabric-icons/" + item.Key + "-32-v1.png"), item.Value.Item2 + " tkanina", "Tkanina z " + item.Value.Item1);
            BindByName(definitions, import("thread-icons/" + item.Key + "-32-v1.png"), item.Value.Item3);
        }

        var hides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["boar"]="dzika",["deer"]="jelenia",["fox"]="lisa",["goat"]="kozy",["monitor-lizard"]="warana",["rabbit"]="królika",["tiger"]="tygrysa",["wild-dog"]="dzikiego psa"
        };
        foreach (var item in hides)
        {
            BindByName(definitions, import("hide-icons/" + item.Key + "-hide-32-v1.png"), "Skóra " + item.Value);
            BindByName(definitions, import("leather-strip-icons/" + item.Key + "-leather-strips-32-v1.png"), "Paski ze skóry " + item.Value);
            BindByName(definitions, import("tanned-hide-icons/" + item.Key + "-tanned-hide-32-v1.png"), "Wyprawiona skóra " + item.Value);
        }

        var monsters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alligator-tooth"]="Kieł aligatora",["bird-feather"]="Pióro ptaka",["cactus-fruit"]="Owoc kaktusa",["corrupted-heart"]="Spaczone serce",["crushed-skull"]="Skruszona czaszka",["demon-horn"]="Róg demona",["elf-ear"]="Ucho elfa",["living-leaf"]="Żyjący liść",["pirate-eye"]="Oko pirata",["troll-kidney"]="Nerka trolla"
        };
        foreach (var item in monsters) BindByName(definitions, import("monster-ingredient-icons/" + item.Key + "-32-v1.png"), item.Value);

        var ores = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["chromium"]="Chrom",["coal"]="Węgiel",["copper-ore"]="Ruda miedzi",["iron-ore"]="Ruda żelaza",["meteorite-shard"]="Odłamek meteorytu",["nickel-ore"]="Ruda niklu",["tin-ore"]="Ruda cyny",["zinc-piece"]="Kawałek cynku"
        };
        foreach (var item in ores) BindByName(definitions, import("smithing-raw-icons/" + item.Key + "-32-v1.png"), item.Value);

        var chains = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["iron"]="Żelazny łańcuch",["nickel"]="Niklowy łańcuch",["copper"]="Miedziany łańcuch",["tin"]="Cynowy łańcuch",["steel"]="Stalowy łańcuch",["invar"]="Inwarowy łańcuch",["meteoric-iron"]="Meteorytowy łańcuch",["bronze"]="Brązowy łańcuch"
        };
        foreach (var item in chains) BindByName(definitions, import("chain-icons/chain-" + item.Key + "-v1-32.png"), item.Value);

        var alchemy = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alcohol"]="Alkohol",["weak-alcohol"]="Słaby alkohol",["gunpowder"]="Proch strzelniczy",["weak-gunpowder"]="Słaby proch strzelniczy"
        };
        foreach (var item in alchemy) BindByName(definitions, import("alchemy-basics-icons/" + item.Key + "-32-v1.png"), item.Value);

        var catacombAlchemy = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common-harvestman"] = "Kosarz pospolity", ["puffball"] = "Purchawka",
            ["skeleton-bone"] = "Kość szkieleta", ["undead-brain"] = "Mózg umarlaka"
        };
        foreach (var item in catacombAlchemy)
            BindByName(definitions, import("catacomb-alchemy-icons/" + item.Key + "-32-v1.png"), item.Value);

        var herbs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pinecone"]="Szyszka",["seaweed"]="Wodorosty",["cherry-blossom"]="Kwitnąca wiśnia",["rush"]="Sitowie",["housefly"]="Mucha zwyczajna",["strangling-grass"]="Trawa dusicielska",["aloe-meda"]="Aloe meda",["alzif-vines"]="Pnącza alzif",["perennial-flower"]="Kwiat byliny",["tregandzki-dandelion"]="Dmuchawiec tregandzki"
        };
        foreach (var item in herbs)
        {
            var dir = item.Key is "strangling-grass" or "aloe-meda" or "alzif-vines" or "perennial-flower" or "tregandzki-dandelion" ? "fantasy-herbal-ingredient-icons" : "herbal-ingredient-icons";
            BindByName(definitions, import(dir + "/" + item.Key + "-32-v1.png"), item.Value);
        }

        var powders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clumpy"]="Proch ziołowy: Grudkowaty",["sticky"]="Proch ziołowy: Lepki",["loose"]="Proch ziołowy: Luźny",["ordinary"]="Proch ziołowy: Zwykły",["yellow-v2"]="Proch ziołowy: Żółty",["green-v2"]="Proch ziołowy: Zielony",["pink-v2"]="Proch ziołowy: Różowy",["orange-v2"]="Proch ziołowy: Pomarańczowy"
        };
        foreach (var item in powders) BindByName(definitions, import("herbal-powder-icons/herbal-powder-" + item.Key + "-32.png"), item.Value);

        var metals = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["iron"]=("żelaza","Żelazna blacha"),["nickel"]=("niklu","Niklowa blacha"),["copper"]=("miedzi","Miedziana blacha"),["tin"]=("cyny","Cynowa blacha"),["steel"]=("stali","Stalowa blacha"),["invar"]=("inwaru","Inwarowa blacha"),["meteoric-iron"]=("żelaza meteorytowego","Meteorytowa blacha"),["bronze"]=("brązu","Brązowa blacha")
        };
        foreach (var item in metals)
        {
            BindByName(definitions, import("metal-powder-icons/metal-powder-" + item.Key + "-v2-32.png"), MetalPowderName(item.Key));
            BindByName(definitions, import("plate-icons/plate-" + item.Key + "-v1-32.png"), item.Value.Item2);
            BindByName(definitions, import("ingot-icons/ingot-normal-" + item.Key + "-v2-32.png"), "Sztabka " + item.Value.Item1);
            BindByName(definitions, import("ingot-icons/ingot-huge-" + item.Key + "-v2-32.png"), "Wielka sztabka " + item.Value.Item1);
        }

        var gems = new Dictionary<string, string>
        {
            ["amber"]="Bursztyn",["amethyst"]="Ametyst",["citrine"]="Cytryn",["diamond"]="Diament",["emerald"]="Szmaragd",["garnet"]="Granat",["obsidian"]="Obsydian",["onyx"]="Onyks",["ruby"]="Rubin",["sapphire"]="Szafir",["topaz"]="Topaz"
        };
        foreach (var item in gems) BindByName(definitions, import("gem-icons/" + item.Key + "-32-v1.png"), item.Value);

        var gemPowders = new Dictionary<string, string>
        {
            ["red"]="Czerwony proszek",["blue"]="Niebieski proszek",["purple"]="Purpurowy proszek",["lemon"]="Cytrynowy proszek",["emerald"]="Szmaragdowy proszek",["black"]="Czarny proszek",["pink"]="Różowy proszek",["white"]="Biały proszek",["gold"]="Złoty proszek",["amber"]="Bursztynowy proszek"
        };
        foreach (var item in gemPowders) BindByName(definitions, import("gem-powder-icons/gem-powder-" + item.Key + "-32.png"), item.Value);
    }

    private static List<ItemIconLibrary.Entry> BuildJewelryVariants(
        IReadOnlyList<JewelryItemDefinition> definitions,
        ICollection<string> unavailable)
    {
        var entries = new List<ItemIconLibrary.Entry>();
        var generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var slot = definition.JewelrySlot.ToString().ToLowerInvariant();
            foreach (var metal in definition.Metals ?? new List<JewelryMetalData>())
            {
                var metalSlug = MetalSlug(metal?.Material?.DisplayName);
                if (string.IsNullOrWhiteSpace(metalSlug)) continue;
                foreach (var gem in definition.Gems ?? new List<JewelryGemDefinition>())
                {
                    var gemSlug = GemSlug(gem?.CraftingMaterial?.DisplayName);
                    if (string.IsNullOrWhiteSpace(gemSlug)) continue;
                    // Jade extends the committed atlas; it does not depend on local review sources.
                    if (gemSlug == "jadeite")
                    {
                        var jadeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "jewelry-" + slot + "-" + metalSlug + "-jadeite.png");
                        if (jadeSprite == null) { unavailable.Add("jewelry " + slot + "/" + metalSlug + "/jadeite"); continue; }
                        entries.Add(new ItemIconLibrary.Entry
                        {
                            Key = Godmist.Runtime.Items.ItemIconPresentation.BuildJewelryKey(definition.JewelrySlot, metal.Material.Id, gem.CraftingMaterial.Id),
                            Icon = jadeSprite
                        });
                        continue;
                    }
                    var metalSource = Path.Combine(ReviewRoot, "jewelry-metals", slot + "-" + metalSlug + "-v2-32.png");
                    var gemSource = Path.Combine(ReviewRoot, "jewelry-gems", slot + "-" + gemSlug + "-32.png");
                    var maskSource = Path.Combine(ReviewRoot, "jewelry-gems", slot + "-gem-mask.png");
                    var metalTexture = LoadTexture(metalSource);
                    var gemTexture = LoadTexture(gemSource);
                    var maskTexture = LoadTexture(maskSource);
                    if (metalTexture == null || gemTexture == null || maskTexture == null)
                    {
                        unavailable.Add("jewelry " + slot + "/" + metalSlug + "/" + gemSlug);
                        DestroyTextures(metalTexture, new[] { gemTexture, maskTexture });
                        continue;
                    }

                    var fileName = "jewelry-" + slot + "-" + metalSlug + "-" + gemSlug + ".png";
                    var targetAbsolute = Path.Combine(ProjectRoot, ArtRoot.Replace('/', Path.DirectorySeparatorChar), fileName);
                    if (generated.Add(fileName))
                        WriteJewelryComposite(targetAbsolute, metalTexture, gemTexture, maskTexture);
                    DestroyTextures(metalTexture, new[] { gemTexture, maskTexture });

                    var targetAsset = ArtRoot + fileName;
                    ConfigureImportedTexture(targetAsset);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(targetAsset);
                    var key = Godmist.Runtime.Items.ItemIconPresentation.BuildJewelryKey(
                        definition.JewelrySlot,
                        (metal?.Material?.Id ?? string.Empty) + ":" + (metal?.Amount ?? 1),
                        gem?.CraftingMaterial?.Id);
                    if (sprite != null && !string.IsNullOrWhiteSpace(key))
                        entries.Add(new ItemIconLibrary.Entry { Key = key, Icon = sprite });
                }
            }
        }
        return entries;
    }

    private static string MetalSlug(string displayName)
    {
        var value = (displayName ?? string.Empty).ToLowerInvariant();
        if (value.Contains("meteoryt")) return "meteoric-iron";
        if (value.Contains("żelaz")) return "iron";
        if (value.Contains("nikl")) return "nickel";
        if (value.Contains("miedz")) return "copper";
        if (value.Contains("cyn")) return "tin";
        if (value.Contains("stal")) return "steel";
        if (value.Contains("inwar")) return "invar";
        if (value.Contains("brąz")) return "bronze";
        return string.Empty;
    }

    private static string GemSlug(string displayName)
    {
        var value = (displayName ?? string.Empty).ToLowerInvariant();
        if (value.Contains("bursztyn")) return "bursztyn";
        if (value.Contains("ametyst")) return "amethyst";
        if (value.Contains("cytryn")) return "citrine";
        if (value.Contains("diament")) return "diamond";
        if (value.Contains("szmaragd")) return "emerald";
        if (value.Contains("granat")) return "garnet";
        if (value.Contains("obsydian")) return "obsidian";
        if (value.Contains("jadeit")) return "jadeite";
        if (value.Contains("onyks")) return "onyx";
        if (value.Contains("rubin")) return "ruby";
        if (value.Contains("szafir")) return "sapphire";
        if (value.Contains("topaz")) return "topaz";
        return string.Empty;
    }

    private static void WriteJewelryComposite(string path, Texture2D metal, Texture2D gem, Texture2D mask)
    {
        var result = metal.GetPixels32();
        var gemPixels = gem.GetPixels32();
        var maskPixels = mask.GetPixels32();
        for (var index = 0; index < result.Length; index++)
        {
            if (maskPixels[index].a == 0) continue;
            var source = gemPixels[index];
            result[index] = new Color32(source.r, source.g, source.b, result[index].a);
        }
        var texture = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(result);
        texture.Apply(false, false);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void BindJewelryArt(IReadOnlyList<JewelryItemDefinition> definitions, ICollection<string> unavailable)
    {
        foreach (var definition in definitions)
        {
            var slot = definition.JewelrySlot.ToString().ToLowerInvariant();
            SetIcon(definition, ImportSprite("jewelry-icons/" + slot + "-32-v1.png", slot + "-neutral-32.png", unavailable));
        }
    }

    private static string MetalPowderName(string slug) => slug switch
    {
        "iron" => "Sproszkowane żelazo",
        "nickel" => "Sproszkowany nikiel",
        "copper" => "Sproszkowana miedź",
        "tin" => "Sproszkowana cyna",
        "steel" => "Sproszkowana stal",
        "invar" => "Sproszkowany inwar",
        "meteoric-iron" => "Sproszkowane żelazo meteorytowe",
        "bronze" => "Sproszkowany brąz",
        _ => string.Empty
    };

    private static List<ItemIconLibrary.Entry> BuildGalduryteVariants(ICollection<string> unavailable)
    {
        var entries = new List<ItemIconLibrary.Entry>();
        var folder = Path.Combine(ReviewRoot, "galduryte-modular");
        var baseTexture = LoadTexture(Path.Combine(folder, "galduryte-base-32.png"));
        var colors = new[] { "red", "blue", "purple", "lemon", "emerald", "black" };
        var layers = colors.Select((color, index) => LoadTexture(Path.Combine(folder, "effect-layer-" + (index + 1).ToString("00") + "-" + color + "-32.png"))).ToArray();
        if (baseTexture == null || layers.Any(texture => texture == null))
        {
            unavailable.Add("galduryte-modular composite layers");
            DestroyTextures(baseTexture, layers);
            return entries;
        }
        for (var mask = 1; mask < (1 << layers.Length); mask++)
        {
            var key = "galduryte." + string.Join("-", Enumerable.Range(0, layers.Length).Where(index => (mask & (1 << index)) != 0));
            var fileName = "galduryte-" + mask.ToString("00") + ".png";
            var targetAbsolute = Path.Combine(ProjectRoot, ArtRoot.Replace('/', Path.DirectorySeparatorChar), fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolute));
            WriteComposite(targetAbsolute, baseTexture, layers, mask);
            var targetAsset = ArtRoot + fileName;
            ConfigureImportedTexture(targetAsset);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(targetAsset);
            if (sprite != null) entries.Add(new ItemIconLibrary.Entry { Key = key, Icon = sprite });
        }
        DestroyTextures(baseTexture, layers);
        return entries;
    }

    private static void SaveLibrary(IReadOnlyList<ItemIconLibrary.Entry> entries)
    {
        var library = AssetDatabase.LoadAssetAtPath<ItemIconLibrary>(LibraryAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ItemIconLibrary>();
            AssetDatabase.CreateAsset(library, LibraryAssetPath);
        }
        var serialized = new SerializedObject(library);
        var property = serialized.FindProperty("entries");
        property.arraySize = entries.Count;
        for (var index = 0; index < entries.Count; index++)
        {
            var row = property.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("Key").stringValue = entries[index].Key;
            row.FindPropertyRelative("Icon").objectReferenceValue = entries[index].Icon;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
    }

    private static Sprite ImportSprite(string relativePath, string targetName, ICollection<string> unavailable)
    {
        var source = Path.Combine(ReviewRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source))
        {
            unavailable.Add(relativePath);
            return null;
        }
        var targetAsset = ArtRoot + targetName;
        var targetAbsolute = Path.Combine(ProjectRoot, targetAsset.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolute));
        var sourceBytes = File.ReadAllBytes(source);
        if (!File.Exists(targetAbsolute) || !File.ReadAllBytes(targetAbsolute).SequenceEqual(sourceBytes))
            File.Copy(source, targetAbsolute, true);
        ConfigureImportedTexture(targetAsset);
        return AssetDatabase.LoadAssetAtPath<Sprite>(targetAsset);
    }

    private static void ConfigureImportedTexture(string targetAsset)
    {
        AssetDatabase.ImportAsset(targetAsset, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(targetAsset) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path)) return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        return texture.LoadImage(File.ReadAllBytes(path), false) ? texture : null;
    }

    private static void WriteComposite(string path, Texture2D baseTexture, Texture2D[] layers, int mask)
    {
        var result = baseTexture.GetPixels32();
        for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            if ((mask & (1 << layerIndex)) == 0) continue;
            var pixels = layers[layerIndex].GetPixels32();
            for (var index = 0; index < result.Length; index++)
            {
                var source = pixels[index];
                if (source.a == 0) continue;
                var destination = result[index];
                var sourceAlpha = source.a / 255f;
                var destinationAlpha = destination.a / 255f;
                var alpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
                if (alpha <= 0f) continue;
                result[index] = new Color32(
                    (byte)((source.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / alpha),
                    (byte)((source.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / alpha),
                    (byte)((source.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / alpha),
                    (byte)(alpha * 255f));
            }
        }
        var texture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(result);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void DestroyTextures(Texture2D baseTexture, IEnumerable<Texture2D> layers)
    {
        if (baseTexture != null) UnityEngine.Object.DestroyImmediate(baseTexture);
        foreach (var layer in layers ?? Enumerable.Empty<Texture2D>())
            if (layer != null) UnityEngine.Object.DestroyImmediate(layer);
    }

    private static void BindByName(IReadOnlyList<ItemDefinition> definitions, Sprite sprite, params string[] names)
    {
        if (sprite == null) return;
        foreach (var definition in definitions)
            if (definition != null && names.Any(name => string.Equals(definition.DisplayName, name, StringComparison.OrdinalIgnoreCase))) SetIcon(definition, sprite);
    }

    private static void SetIcon(ItemDefinition definition, Sprite sprite)
    {
        if (definition == null || sprite == null) return;
        var serialized = new SerializedObject(definition);
        serialized.FindProperty(Field("Icon")).objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void SetIcon(EquipmentPartDefinition definition, Sprite sprite)
    {
        if (definition == null || sprite == null) return;
        var serialized = new SerializedObject(definition);
        serialized.FindProperty(Field("Icon")).objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static List<T> All<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToList();

    private static List<JewelryItemDefinition> AllJewelryDefinitions() =>
        AssetDatabase.FindAssets("", new[] { "Assets/_Project/ScriptableObjects/Equipment/Jewelry" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>)
            .Where(asset => asset != null)
            .ToList();

    private static void EnsureFolder(string assetFolder)
    {
        var absolute = Path.Combine(ProjectRoot, assetFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absolute)) Directory.CreateDirectory(absolute);
    }
}
