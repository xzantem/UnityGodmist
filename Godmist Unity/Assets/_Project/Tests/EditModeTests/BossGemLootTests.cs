using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Items;
using Godmist.Runtime.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class BossGemLootTests
    {
        static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static GameContentCatalog Catalog => Load<GameContentCatalog>(BossGemLootSetup.CatalogPath);
        static JewelryGemDefinition Jade => Load<JewelryGemDefinition>(BossGemLootSetup.JadeGemPath);
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/boss-gems-20261027"));
        static string[] ExistingPaths => BossGemLootSetup.ResourceBags.Concat(BossGemLootSetup.CatacombsBags)
            .Concat(BossGemLootSetup.ForestBags).Concat(new[] { BossGemLootSetup.CatalogPath, BossGemLootSetup.IconLibraryPath })
            .Concat(Catalog.ItemDefinitions.OfType<JewelryItemDefinition>().Select(AssetDatabase.GetAssetPath)).ToArray();
        [Serializable] public sealed class Row { public string Path, Json; }
        [Serializable] public sealed class Snapshot { public List<Row> Rows = new(); }
        static void Record(string name)
        {
            Directory.CreateDirectory(Output);
            var snapshot = new Snapshot();
            foreach (var path in ExistingPaths.Concat(new[] { BossGemLootSetup.JadePath, BossGemLootSetup.JadeGemPath }).Distinct())
            {
                var asset = Load<UnityEngine.Object>(path);
                if (asset != null) snapshot.Rows.Add(new Row { Path = path, Json = EditorJsonUtility.ToJson(asset, true) });
            }
            File.WriteAllText(Path.Combine(Output, name + ".json"), JsonUtility.ToJson(snapshot, true));
        }

        [Test, Explicit("Author only the approved boss gems via Editor API; leave the current scene open.")]
        public void ApplyApprovedContentAndVerifyIdempotence()
        {
            if (!File.Exists(Path.Combine(Output, "before-editor.json"))) Record("before-editor");
            BossGemLootSetup.Apply();
            Record("after-editor");
            var paths = ExistingPaths.Concat(new[] { BossGemLootSetup.JadePath, BossGemLootSetup.JadeGemPath })
                .Concat(Directory.GetFiles(Path.Combine(Application.dataPath, "_Project/Art/Icons/Items/VS22"), "*jadeite*")
                    .Select(Path.GetFullPath)).ToArray();
            var bytes = paths.ToDictionary(path => path, File.ReadAllBytes);
            BossGemLootSetup.Apply();
            foreach (var path in paths) Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes[path]), "Repeat Apply changed " + path);
            Record("after-idempotence");
        }

        [Test]
        public void BothResourceBagsExcludeBothBossGemsAtEveryLevel()
        {
            foreach (var path in BossGemLootSetup.ResourceBags)
            foreach (var bracket in Load<LootBagItemDefinition>(path).Brackets)
            foreach (var entry in bracket.Pools.SelectMany(pool => pool.Entries))
                Assert.That(entry.ItemDefinition.Id, Is.Not.EqualTo(BossGemLootSetup.ObsidianId).And.Not.EqualTo(BossGemLootSetup.JadeId));
        }

        [TestCase(false, 1)] [TestCase(false, 10)] [TestCase(false, 11)] [TestCase(false, 20)]
        [TestCase(true, 1)] [TestCase(true, 10)] [TestCase(true, 11)] [TestCase(true, 20)]
        public void EachBossHasOneIndependentHalfChancePool(bool forest, int level)
        {
            foreach (var path in forest ? BossGemLootSetup.ForestBags : BossGemLootSetup.CatacombsBags)
            {
                var bag = Load<LootBagItemDefinition>(path);
                Assert.That(bag.TryGetBracket(level <= 10 ? LootLevelBracket.Level1To10 : LootLevelBracket.Level11To20, out var bracket), Is.True);
                var id = forest ? BossGemLootSetup.JadeId : BossGemLootSetup.ObsidianId;
                var pools = bracket.Pools.Where(pool => pool.Entries.Any(entry => entry.ItemDefinition.Id == id)).ToArray();
                Assert.That(pools.Length, Is.EqualTo(1));
                Assert.That(pools[0].DropChance, Is.EqualTo(.5f));
                Assert.That(pools[0].DistinctSelectionGroup, Is.Null.Or.Empty);
                Assert.That(pools[0].Entries.Count, Is.EqualTo(1));
                Assert.That(pools[0].Entries[0].Amount.Min, Is.EqualTo(1));
                Assert.That(pools[0].Entries[0].Amount.Max, Is.EqualTo(2));
                Assert.That(bracket.Pools.SelectMany(p => p.Entries).Any(entry => entry.ItemDefinition.Id == (forest ? BossGemLootSetup.ObsidianId : BossGemLootSetup.JadeId)), Is.False);
            }
        }

        [TestCase(false, 10)] [TestCase(false, 20)] [TestCase(true, 10)] [TestCase(true, 20)]
        public void RuntimeRollsUseTheConfiguredChanceAndUnitCounts(bool forest, int level)
        {
            var id = forest ? BossGemLootSetup.JadeId : BossGemLootSetup.ObsidianId;
            var bag = Load<LootBagItemDefinition>((forest ? BossGemLootSetup.ForestBags : BossGemLootSetup.CatacombsBags)[0]);
            var random = new System.Random(2163 + level);
            var loot = new LootTableSystem(() => (float)random.NextDouble(), lootLevel: level);
            var hits = 0; var units = 0;
            for (var i = 0; i < 4000; i++)
            {
                var count = loot.CreateLootBag(bag).Contents.Count(item => item.Definition.Id == id);
                Assert.That(count, Is.InRange(0, 2));
                if (count > 0) hits++;
                units += count;
            }
            Assert.That(hits / 4000f, Is.EqualTo(.5f).Within(.035f));
            Assert.That(units / 4000f, Is.EqualTo(.75f).Within(.055f));
        }

        [Test]
        public void JadeIsARegisteredForestCounterpartOfObsidian()
        {
            var jade = Jade.CraftingMaterial;
            Assert.That(Catalog.ItemDefinitions.Count(item => item?.Id == BossGemLootSetup.JadeId), Is.EqualTo(1));
            Assert.That(jade.BaseValue, Is.EqualTo(2847)); Assert.That(jade.BaseRarity, Is.EqualTo(RarityTier.Rare));
            Assert.That(jade.MaterialDiscipline, Is.EqualTo(MaterialDiscipline.Runic)); Assert.That(jade.IsStackable, Is.True);
            Assert.That(jade.Icon, Is.Not.Null); Assert.That(jade.Icon.rect.width, Is.EqualTo(32));
            Assert.That(Jade.Amount, Is.EqualTo(1)); Assert.That(Jade.Modifiers.Count, Is.EqualTo(1));
            var modifier = Jade.Modifiers.Single();
            Assert.That(modifier.Target, Is.EqualTo(JewelryModifierTarget.GameplayVariable));
            Assert.That(modifier.GameplayVariable, Is.EqualTo(GameplayVariableType.DamageDealtPercentInDungeonTheme));
            Assert.That(modifier.ReferenceId, Is.EqualTo("Forest")); Assert.That(modifier.Value, Is.EqualTo(.035f));
        }

        [Test]
        public void AllThirtyTwoJadeJewelryVariantsHaveNamesIconsAndUnchangedMetalStats()
        {
            var library = Load<ItemIconLibrary>(BossGemLootSetup.IconLibraryPath);
            var generation = new JewelryGenerationService(); var variants = 0;
            foreach (var definition in Catalog.ItemDefinitions.OfType<JewelryItemDefinition>())
            {
                Assert.That(definition.Gems.Count(gem => gem == Jade), Is.EqualTo(1));
                Assert.That(definition.Gems.Count, Is.EqualTo(12));
                var obsidian = definition.Gems.Single(gem => gem.CraftingMaterial?.Id == BossGemLootSetup.ObsidianId);
                foreach (var metal in definition.Metals)
                {
                    var item = generation.GenerateSelected(definition, metal, Jade);
                    var reference = generation.GenerateSelected(definition, metal, obsidian);
                    Assert.That(item.GemId, Is.EqualTo(BossGemLootSetup.JadeId));
                    Assert.That(item.DisplayName, Does.EndWith("z jadeitem"));
                    Assert.That(item.ItemLevel, Is.EqualTo(reference.ItemLevel));
                    Assert.That(item.BaseValue, Is.EqualTo(reference.BaseValue));
                    Assert.That(item.StatModifiers.Select(m => (m.StatType, m.Operation, m.Value, m.Stacking)),
                        Is.EqualTo(reference.StatModifiers.Select(m => (m.StatType, m.Operation, m.Value, m.Stacking))));
                    Assert.That(library.Find(ItemIconPresentation.BuildJewelryKey(item)), Is.Not.Null);
                    variants++;
                }
            }
            Assert.That(variants, Is.EqualTo(32));
        }

        [TestCase(true)] [TestCase(false)]
        public void FourBiomeGemsGiveFourteenPercentOnlyInTheirBiome(bool jade)
        {
            var character = new CharacterInstance { CharacterId = "jade-build" };
            var generation = new JewelryGenerationService();
            var reconciliation = new PassiveEffectReconciliationSystem();
            var variables = new CombatVariableSystem(); var pieces = 0;
            foreach (var definition in Catalog.ItemDefinitions.OfType<JewelryItemDefinition>())
            {
                var gem = jade ? Jade : definition.Gems.Single(g => g.CraftingMaterial?.Id == BossGemLootSetup.ObsidianId);
                var item = generation.GenerateSelected(definition, definition.Metals[0], gem);
                character.Equipment.SetItem(definition.EquipmentSlot, item);
                reconciliation.Reconcile(character); pieces++;
                Assert.That(variables.GetMultiplier(character, GameplayVariableType.DamageDealtPercentInDungeonTheme, jade ? "Forest" : "Catacombs"), Is.EqualTo(1 + pieces * .035f).Within(1e-6));
                Assert.That(variables.GetMultiplier(character, GameplayVariableType.DamageDealtPercentInDungeonTheme, jade ? "Catacombs" : "Forest"), Is.EqualTo(1));
                Assert.That(variables.GetMultiplier(character, GameplayVariableType.DamageDealtPercent), Is.EqualTo(1));
            }
            Assert.That(pieces, Is.EqualTo(4));
        }

        [Test, Explicit("Read-only atlas export to the local handoff directory.")]
        public void ExportJadeIconSheet()
        {
            var sheet = new Texture2D(256, 160, TextureFormat.RGBA32, false);
            var background = new Color32(27, 27, 31, 255);
            var pixels = Enumerable.Repeat(background, sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(pixels);
            void Tile(string path, int col, int row)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path)); Assert.That(texture.width, Is.EqualTo(32));
                    var source = texture.GetPixels32();
                    for (var i = 0; i < source.Length; i++)
                    {
                        var alpha = source[i].a / 255f;
                        var color = (Color)source[i] * alpha + (Color)background * (1 - alpha);
                        color.a = 1; source[i] = color;
                    }
                    sheet.SetPixels32(col * 32, sheet.height - (row + 1) * 32, 32, 32, source);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            Texture2D large = null;
            try
            {
                Tile(BossGemLootSetup.IconRoot + "gem-icons-obsidian-32-v1.png", 0, 0);
                Tile(BossGemLootSetup.JadeIconPath, 1, 0);
                Tile(BossGemLootSetup.IconRoot + "gem-icons-emerald-32-v1.png", 2, 0);
                var library = Load<ItemIconLibrary>(BossGemLootSetup.IconLibraryPath);
                var row = 1;
                foreach (var definition in Catalog.ItemDefinitions.OfType<JewelryItemDefinition>())
                {
                    var col = 0;
                    foreach (var metal in definition.Metals)
                    {
                        var key = ItemIconPresentation.BuildJewelryKey(definition.JewelrySlot, metal.Material.Id, BossGemLootSetup.JadeId);
                        Tile(AssetDatabase.GetAssetPath(library.Find(key)), col++, row);
                    }
                    row++;
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(Output, "jade-icons-native.png"), sheet.EncodeToPNG());
                var source = sheet.GetPixels32(); var scaled = new Color32[sheet.width * sheet.height * 16];
                for (var y = 0; y < sheet.height * 4; y++)
                for (var x = 0; x < sheet.width * 4; x++) scaled[y * sheet.width * 4 + x] = source[y / 4 * sheet.width + x / 4];
                large = new Texture2D(sheet.width * 4, sheet.height * 4, TextureFormat.RGBA32, false);
                large.SetPixels32(scaled); large.Apply();
                File.WriteAllBytes(Path.Combine(Output, "jade-icons-preview.png"), large.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(sheet); if (large != null) UnityEngine.Object.DestroyImmediate(large); }
        }
    }
}
