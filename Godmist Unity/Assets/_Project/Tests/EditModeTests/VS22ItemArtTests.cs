using System.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class VS22ItemArtTests
    {
        [Serializable] private sealed class RestylePlan { public RestyleFile[] files; }
        [Serializable] private sealed class RestyleFile
        {
            public string path;
            public string sha256;
            public string guid;
        }
        [Serializable] private sealed class RestyleReceipt
        {
            public string completedUtc;
            public int importedTextures;
            public int gpuSamples;
            public string[] paths;
        }
        private static string RestyleOutput
        {
            get
            {
                var active=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/active-item-art-import"));
                return File.Exists(Path.Combine(active,"import-plan.json")) ? active :
                    Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/item-art-import-20261022"));
            }
        }

        [Test]
        public void ApprovedRestyleImportKeepsIdentityAndPixelArtSettings()
        {
            var planPath=Path.Combine(RestyleOutput,"import-plan.json");
            if(!File.Exists(planPath)) Assert.Ignore("Explicit local art-import manifest not present.");
            var plan=JsonUtility.FromJson<RestylePlan>(File.ReadAllText(planPath));
            Assert.That(plan.files.Length,Is.EqualTo(575));
            var project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var samples=new HashSet<string> {
                "catacomb-alchemy-icons-skeleton-bone-32-v1.png", "ingot-icons-ingot-huge-iron-v2-32.png",
                "sword-dagger-32-v1.png", "chainmail-32-v1.png", "camping-kit-32-v1.png",
                "jewelry-bracelet-bronze-ruby.png", "jewelry-earrings-tin-sapphire.png",
                "jewelry-necklace-steel-emerald.png", "jewelry-ring-meteoric-iron-obsidian.png",
                "potion-neutral-32.png", "bomb-neutral-32.png", "galduryte-neutral-32.png",
                "gem-icons-amethyst-32-v1.png", "gem-icons-emerald-32-v1.png", "gem-icons-diamond-32-v1.png"
            };
            var sampled=0;
            Directory.CreateDirectory(Path.Combine(RestyleOutput,"unity-renders"));
            foreach(var file in plan.files)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(file.path),Is.EqualTo(file.guid),file.path);
                using(var hash=SHA256.Create())
                    Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(project,file.path))))
                        .Replace("-",string.Empty).ToLowerInvariant(),Is.EqualTo(file.sha256),file.path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.path);
                Assert.That(importer,Is.Not.Null,file.path);
                Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite),file.path);
                Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Single),file.path);
                Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(32),file.path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point),file.path);
                Assert.That(importer.mipmapEnabled,Is.False,file.path);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),file.path);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(file.path);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(file.path);
                Assert.That(texture,Is.Not.Null,file.path);Assert.That(sprite,Is.Not.Null,file.path);
                Assert.That(texture.width,Is.EqualTo(32),file.path);Assert.That(texture.height,Is.EqualTo(32),file.path);
                if(Path.GetFileName(file.path).StartsWith("modular-",StringComparison.Ordinal))
                {
                    Assert.That(importer.isReadable,Is.True,file.path);
                    var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try
                    {
                        Assert.That(source.LoadImage(File.ReadAllBytes(Path.Combine(project,file.path))),Is.True);
                        Assert.That(texture.GetPixels32(),Is.EqualTo(source.GetPixels32()),file.path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(source); }
                }
                if(!samples.Contains(Path.GetFileName(file.path))) continue;
                var output=Readback(texture);
                try { File.WriteAllBytes(Path.Combine(RestyleOutput,"unity-renders",Path.GetFileName(file.path)),output.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(output); }
                sampled++;
            }
            Assert.That(sampled,Is.GreaterThanOrEqualTo(11));
            File.WriteAllText(Path.Combine(RestyleOutput,"unity-import-receipt.json"),JsonUtility.ToJson(new RestyleReceipt {
                completedUtc=DateTime.UtcNow.ToString("O"),importedTextures=plan.files.Length,gpuSamples=sampled,
                paths=plan.files.Select(file=>file.path).ToArray()
            },true));
        }

        private static Texture2D Readback(Texture texture)
        {
            var prior=RenderTexture.active;var priorSrgb=GL.sRGBWrite;
            var target=RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var result=new Texture2D(32,32,TextureFormat.RGBA32,false);
            try
            {
                GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
                Graphics.Blit(texture,target);RenderTexture.active=target;
                result.ReadPixels(new Rect(0,0,32,32),0,0);result.Apply();return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result);throw; }
            finally { RenderTexture.active=prior;GL.sRGBWrite=priorSrgb;RenderTexture.ReleaseTemporary(target); }
        }

        [Test]
        public void RestyledRuntimeCompositionsRenderFromCurrentLibraryLayers()
        {
            if(!File.Exists(Path.Combine(RestyleOutput,"import-plan.json")))
                Assert.Ignore("Explicit local art-import manifest not present.");
            var library=UnityEngine.Object.Instantiate(Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath));
            Assert.That(library,Is.Not.Null);
            var folder=Path.Combine(RestyleOutput,"unity-renders");Directory.CreateDirectory(folder);
            try
            {
                var colors=new[] {new Color32(192,54,65,255),new Color32(62,164,95,255),new Color32(118,75,174,255)};
                foreach(var bomb in new[] {false,true})
                    for(var count=1;count<=3;count++)
                    {
                        var sprite=library.ComposeConsumable(bomb,colors.Take(count).ToArray());
                        Assert.That(sprite,Is.Not.Null);Assert.That(sprite.texture.width,Is.EqualTo(32));
                        File.WriteAllBytes(Path.Combine(folder,(bomb?"bomb":"potion")+"-"+count+"-colors.png"),sprite.texture.EncodeToPNG());
                    }
                for(var count=1;count<=6;count++)
                {
                    var sprite=library.Find("galduryte."+string.Join("-",Enumerable.Range(0,count)));
                    Assert.That(sprite,Is.Not.Null);
                    File.WriteAllBytes(Path.Combine(folder,"galduryte-"+count+"-runes.png"),sprite.texture.EncodeToPNG());
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
        }

        [Test]
        public void ApprovedStaticDefinitionsHaveIcons()
        {
            var definitions = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(definition => definition != null)
                .ToDictionary(definition => definition.Id);

            var expected = new[]
            {
                "equipment.warrior.longsword", "equipment.warrior.hauberk", "equipment.mage.robe", "equipment.paladin.hammer", "equipment.paladin.plate_armor", "equipment.scout.sword_dagger", "equipment.scout.tunic", "equipment.mage.wand",
                "item.consumable.antidote", "item.consumable.bandage", "item.consumable.damp_towel", "item.consumable.return_scroll",
                "item.lootbag.catacombs.resources", "item.lootbag.catacombs.dead_king",
                "item.vs10.proch_ziolowy_zolty", "item.vs10.sproszkowane_zelazo", "item.vs10.rubin",
                "item.smithing.iron_ingot", "item.smithing.iron_plate", "item.smithing.iron_chain",
                "item.runic.solid_limestone", "item.runic.galduryte_stone.limestone"
            };

            foreach (var id in expected)
            {
                Assert.That(definitions.ContainsKey(id), Is.True, id);
                Assert.That(definitions[id].Icon, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(definitions[id].Icon), Does.StartWith("Assets/_Project/Art/Icons/Items/VS22/"), id);
            }
        }

        [Test]
        public void JewelryLibraryContainsEveryConfiguredCombination()
        {
            var library = Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Entries.Count(entry => entry != null && entry.Key.StartsWith("jewelry.")), Is.EqualTo(352));
        }

        [Test]
        public void JewelryInstanceResolvesItsMetalAndGemVariant()
        {
            var path = AssetDatabase.FindAssets("", new[] { "Assets/_Project/ScriptableObjects/Equipment/Jewelry" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>)
                .First(definition => definition != null && definition.JewelrySlot == EquipmentSlot.Ring);
            var metal = path.Metals.First(row => row.Material != null);
            var gem = path.Gems.First(row => row.CraftingMaterial != null);
            var instance = new JewelryItemInstance(path)
            {
                MetalId = metal.Material.Id + ":" + metal.Amount,
                GemId = gem.CraftingMaterial.Id
            };

            Assert.That(ItemIconPresentation.GetVariantKey(instance), Does.StartWith("jewelry.ring."));
            Assert.That(ItemIconPresentation.GetIcon(instance), Is.Not.Null);
            Assert.That(ItemIconPresentation.GetIcon(instance), Is.Not.SameAs(path.Icon));
        }

        [Test]
        public void GaldurytePreservesEffectOrderAndDuplicateColors()
        {
            var definition = AssetDatabase.FindAssets("t:GalduryteItemDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GalduryteItemDefinition>)
                .FirstOrDefault(item => item != null);
            Assert.That(definition, Is.Not.Null);

            var instance = new GalduryteItemInstance(definition) { IsIdentified = true };
            instance.Effects.Add(new GalduryteEffectInstance(
                GalduryteEffectType.GeneralDamagePercent, GaldurytePowderColor.Red, GalduryteEffectGrade.D, .1f));
            instance.Effects.Add(new GalduryteEffectInstance(
                GalduryteEffectType.CriticalChancePercent, GaldurytePowderColor.Blue, GalduryteEffectGrade.D, .1f));
            Assert.That(ItemIconPresentation.GetVariantKey(instance), Is.EqualTo("galduryte.0:0-1:1"));
            Assert.That(ItemIconPresentation.GetIcon(instance), Is.Not.Null);

            instance.Effects.Add(new GalduryteEffectInstance(
                GalduryteEffectType.AccuracyPercent, GaldurytePowderColor.Red, GalduryteEffectGrade.D, .1f));
            Assert.That(ItemIconPresentation.GetVariantKey(instance), Is.EqualTo("galduryte.0:0-1:1-0:2"));
            var mixed = ItemIconPresentation.GetIcon(instance);
            Assert.That(mixed, Is.Not.Null);
            Assert.That(mixed, Is.Not.SameAs(definition.Icon));
            var reversed = Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath).Find("galduryte.1:1-0:0-0:2");
            Assert.That(reversed.texture.GetPixels32(), Is.Not.EqualTo(mixed.texture.GetPixels32()));
            Assert.That(ItemIconPresentation.GetIcon(instance), Is.SameAs(mixed));
            instance.IsIdentified = false;
            Assert.That(ItemIconPresentation.GetIcon(instance), Is.SameAs(definition.Icon));
        }

        [Test]
        public void ConsumableMixturesUseApprovedMasksAndPreserveTheirSilhouette()
        {
            var library = Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath);
            var red = new Color32(190, 42, 52, 255);
            var blue = new Color32(44, 101, 204, 255);
            foreach (var bomb in new[] { false, true })
            {
                var single = library.ComposeConsumable(bomb, new[] { red });
                var mixed = library.ComposeConsumable(bomb, new[] { red, blue });
                Assert.That(mixed, Is.Not.Null);
                Assert.That(mixed.rect.size, Is.EqualTo(new Vector2(32, 32)));
                Assert.That(mixed.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(mixed.texture.GetPixels32(), Is.Not.EqualTo(single.texture.GetPixels32()));
                Assert.That(mixed.texture.GetPixels32().Select(pixel => pixel.a), Is.EqualTo(single.texture.GetPixels32().Select(pixel => pixel.a)));
                Assert.That(library.ComposeConsumable(bomb, new[] { red, blue }), Is.SameAs(mixed));
            }
        }

        [Test]
        public void MaterialFormsHaveSeparateSpriteAssets()
        {
            var items = AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).Where(item => item != null).ToDictionary(item => item.Id);
            var wood = items["item.smithing.alder_wood"].Icon;
            var plank = items["item.vs10.olchowa_deska"].Icon;
            var stick = items["item.vs10.olchowy_patyk"].Icon;
            Assert.That(wood, Is.Not.Null);
            Assert.That(plank, Is.Not.SameAs(wood));
            Assert.That(stick, Is.Not.SameAs(wood));
            Assert.That(stick, Is.Not.SameAs(plank));
        }

        [Test]
        public void InventoryIconsKeepVisibleFootprintCenteredInTheirCanvas()
        {
            var planPath=Path.Combine(RestyleOutput,"import-plan.json");
            if(!RestyleOutput.EndsWith("active-item-art-import",StringComparison.Ordinal) || !File.Exists(planPath))
                Assert.Ignore("Current centered icon import not staged.");
            var plan=JsonUtility.FromJson<RestylePlan>(File.ReadAllText(planPath));
            var count=0;
            foreach(var file in plan.files)
            {
                if(Path.GetFileName(file.path).StartsWith("modular-",StringComparison.Ordinal)) continue;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(file.path)),Is.True,file.path);
                    var pixels=texture.GetPixels32();var left=32;var right=-1;var bottom=32;var top=-1;
                    for(var i=0;i<pixels.Length;i++)
                    {
                        if(pixels[i].a<128) continue;
                        left=Math.Min(left,i%32);right=Math.Max(right,i%32);
                        bottom=Math.Min(bottom,i/32);top=Math.Max(top,i/32);
                    }
                    Assert.That(right,Is.GreaterThanOrEqualTo(left),file.path);
                    Assert.That(Math.Abs((left+right+1)*.5f-16),Is.LessThanOrEqualTo(.5f),file.path+" horizontal center");
                    Assert.That(Math.Abs((bottom+top+1)*.5f-16),Is.LessThanOrEqualTo(.5f),file.path+" vertical center");
                    count++;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            Assert.That(count,Is.EqualTo(558));
        }

        [Test]
        public void DifferentEffectsOfSamePowderHaveDifferentSignsAndCacheEntries()
        {
            var library=UnityEngine.Object.Instantiate(Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath));
            var item=new GalduryteItemInstance { IsIdentified=true };
            item.Effects.Add(new GalduryteEffectInstance(GalduryteEffectType.GeneralDamagePercent,GaldurytePowderColor.Red,GalduryteEffectGrade.D,.1f));
            try
            {
                var firstKey=ItemIconPresentation.BuildGalduryteKey(item);var first=library.Find(firstKey);
                item.Effects[0].EffectType=GalduryteEffectType.PhysicalDamagePercent;
                var secondKey=ItemIconPresentation.BuildGalduryteKey(item);var second=library.Find(secondKey);
                Assert.That(secondKey,Is.Not.EqualTo(firstKey));Assert.That(second,Is.Not.SameAs(first));
                Assert.That(second.texture.GetPixels32(),Is.Not.EqualTo(first.texture.GetPixels32()));
                item.Effects[0].Value=.8f;item.Effects[0].Grade=GalduryteEffectGrade.S;
                Assert.That(ItemIconPresentation.BuildGalduryteKey(item),Is.EqualTo(secondKey));
                Assert.That(library.Find(secondKey),Is.SameAs(second));
                item.Effects.Add(new GalduryteEffectInstance(GalduryteEffectType.GeneralDamagePercent,GaldurytePowderColor.Red,GalduryteEffectGrade.D,.1f));
                var forward=library.Find(ItemIconPresentation.BuildGalduryteKey(item));item.Effects.Reverse();
                var reversed=library.Find(ItemIconPresentation.BuildGalduryteKey(item));
                Assert.That(forward.texture.GetPixels32(),Is.Not.EqualTo(reversed.texture.GetPixels32()));
                item.Effects.Add(item.Effects[0]);
                Assert.That(library.Find(ItemIconPresentation.BuildGalduryteKey(item)),Is.Not.Null,"Repeated marks must remain separate slots");
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
        }

        [Test]
        public void EveryEffectHasDistinctFullAndCompactRuneWithNoAlphaHoles()
        {
            var library=UnityEngine.Object.Instantiate(Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath));
            var surface=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Icons/Items/VS22/modular-galduryte-base-32.png");
            var alpha=surface.GetPixels32().Select(p=>p.a).ToArray();
            var full=new HashSet<string>();var compact=new HashSet<string>();
            try
            {
                foreach(GalduryteEffectType effect in Enum.GetValues(typeof(GalduryteEffectType)))
                {
                    var token="0:"+(int)effect;
                    var single=library.Find("galduryte."+token);
                    var six=library.Find("galduryte."+string.Join("-",Enumerable.Repeat(token,6)));
                    foreach(var sprite in new[] {single,six})
                    {
                        Assert.That(sprite,Is.Not.Null,effect.ToString());
                        Assert.That(sprite.texture.GetPixels32().Select(p=>p.a),Is.EqualTo(alpha),effect.ToString());
                        Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                    }
                    Assert.That(full.Add(Convert.ToBase64String(single.texture.EncodeToPNG())),Is.True,"Duplicate large glyph: "+effect);
                    Assert.That(compact.Add(Convert.ToBase64String(six.texture.EncodeToPNG())),Is.True,"Duplicate compact glyph: "+effect);
                }
                Assert.That(full.Count,Is.EqualTo(50));Assert.That(compact.Count,Is.EqualTo(50));
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
        }

        [Test]
        public void AllTenPowderColorsResolveAndInvalidSignsFailClosed()
        {
            var library=UnityEngine.Object.Instantiate(Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath));
            var rendered=new HashSet<string>();
            try
            {
                foreach(GaldurytePowderColor color in Enum.GetValues(typeof(GaldurytePowderColor)))
                {
                    Assert.That(ItemIconPresentation.IsSupportedGalduryteColor(color),Is.True);
                    var item=new GalduryteItemInstance { IsIdentified=true };
                    item.Effects.Add(new GalduryteEffectInstance(GalduryteEffectType.GeneralDamagePercent,color,GalduryteEffectGrade.D,.1f));
                    var sprite=library.Find(ItemIconPresentation.BuildGalduryteKey(item));
                    Assert.That(sprite,Is.Not.Null,color.ToString());
                    Assert.That(rendered.Add(Convert.ToBase64String(sprite.texture.EncodeToPNG())),Is.True,color.ToString());
                }
                foreach(var key in new[] {"galduryte.","galduryte.10:0","galduryte.0:50","galduryte.0:","galduryte.0:0:0","galduryte.0:0-0:0-0:0-0:0-0:0-0:0-0:0"})
                    Assert.That(library.Find(key),Is.Null,key);
                var invalid=new GalduryteItemInstance { IsIdentified=true };
                invalid.Effects.Add(new GalduryteEffectInstance((GalduryteEffectType)50,GaldurytePowderColor.Red,GalduryteEffectGrade.D,.1f));
                Assert.That(ItemIconPresentation.BuildGalduryteKey(invalid),Is.Empty);
                invalid.Effects[0].EffectType=GalduryteEffectType.GeneralDamagePercent;
                invalid.Effects[0].PowderColor=(GaldurytePowderColor)10;
                Assert.That(ItemIconPresentation.BuildGalduryteKey(invalid),Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
        }

        [Serializable] private sealed class RuneReview { public RuneReviewEntry[] effects; }
        [Serializable] private sealed class RuneReviewEntry { public int id;public string effect;public string description;public int color; }

        [Test]
        public void RenderEffectRuneReviewFromProductionPools()
        {
            var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/galduryte-effect-runes-20261022"));
            if(!Directory.Exists(path)) Assert.Ignore("Explicit rune art review not requested.");
            var output=Path.Combine(path,"unity-renders");Directory.CreateDirectory(output);
            var rules=Godmist.Systems.Items.GalduryteRuntimeRules.GetEffectPool(GalduryteTargetType.Weapon)
                .Concat(Godmist.Systems.Items.GalduryteRuntimeRules.GetEffectPool(GalduryteTargetType.Armor))
                .GroupBy(e=>e.EffectType).Select(group=>group.First()).OrderBy(e=>(int)e.EffectType).ToArray();
            Assert.That(rules.Length,Is.EqualTo(50));
            var library=UnityEngine.Object.Instantiate(Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath));
            try
            {
                var entries=new List<RuneReviewEntry>();
                foreach(var effect in rules)
                {
                    var instance=new GalduryteEffectInstance(effect.EffectType,effect.PowderColor,GalduryteEffectGrade.D,.1f);
                    var item=new GalduryteItemInstance { IsIdentified=true };item.Effects.Add(instance);
                    var sprite=library.Find(ItemIconPresentation.BuildGalduryteKey(item));Assert.That(sprite,Is.Not.Null);
                    File.WriteAllBytes(Path.Combine(output,"effect-"+((int)effect.EffectType).ToString("00")+".png"),sprite.texture.EncodeToPNG());
                    item.Effects=Enumerable.Repeat(instance,6).ToList();
                    var six=library.Find(ItemIconPresentation.BuildGalduryteKey(item));
                    File.WriteAllBytes(Path.Combine(output,"compact-"+((int)effect.EffectType).ToString("00")+".png"),six.texture.EncodeToPNG());
                    entries.Add(new RuneReviewEntry {id=(int)effect.EffectType,effect=effect.EffectType.ToString(),
                        description=GalduryteRuntimeText.GetEffectName(effect.EffectType),color=(int)effect.PowderColor});
                }
                for(var count=1;count<=6;count++)
                {
                    var item=new GalduryteItemInstance { IsIdentified=true };
                    item.Effects=rules.Take(count).Select(e=>new GalduryteEffectInstance(e.EffectType,e.PowderColor,GalduryteEffectGrade.D,.1f)).ToList();
                    var sprite=library.Find(ItemIconPresentation.BuildGalduryteKey(item));
                    File.WriteAllBytes(Path.Combine(output,"mixed-"+count+".png"),sprite.texture.EncodeToPNG());
                }
                File.WriteAllText(Path.Combine(path,"effects.json"),JsonUtility.ToJson(new RuneReview {effects=entries.ToArray()},true));
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
        }
    }
}
