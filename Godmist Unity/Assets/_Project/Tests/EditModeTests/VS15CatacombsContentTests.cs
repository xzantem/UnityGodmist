using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class VS15CatacombsContentTests
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static void Set(object target, string field, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var backing = type.GetField("<" + field + ">k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (backing == null) continue;
                backing.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        [Test]
        public void SevenEnemiesHaveThreeUsableSkillsAndSharedManaPool()
        {
            var ids = new[] { "swordsman", "undead", "wraith", "occultist",
                "archer", "ghoul", "defender" };
            var catalog = Load<GameContentCatalog>("World/GC_MainContentCatalog.asset");
            foreach (var id in ids)
            {
                var enemy = Load<CharacterDefinition>(
                    "Catacombs/Enemies/CD_Catacombs_" + id + ".asset");
                Assert.That(enemy.StartingSkills, Has.Count.EqualTo(3), id);
                Assert.That(enemy.StartingSkills, Has.All.Not.Null, id);
                Assert.That(enemy.BasicAttackSkill, Is.SameAs(enemy.StartingSkills[0]), id);
                Assert.That(catalog.CharacterDefinitions, Does.Contain(enemy), id);
                Assert.That(enemy.BaseStats.MaxResource, Is.EqualTo(100), id);
                Assert.That(enemy.BaseStats.ResourceRegeneration, Is.EqualTo(15), id);
                foreach (var skill in enemy.StartingSkills)
                {
                    Assert.That(catalog.SkillDefinitions, Does.Contain(skill), skill.name);
                    Assert.That(skill.SkillId, Does.StartWith("skill.enemy.catacombs."), id);
                    Assert.That(skill.ResourceType, Is.EqualTo(ResourceType.Mana), skill.name);
                    Assert.That(skill.TargetModes, Is.Not.Empty, skill.name);
                    Assert.That(skill.Effects, Is.Not.Empty, skill.name);
                }
            }
        }

        [Test]
        public void EnemySpritesUseCrispStandaloneFramesAtCharacterScale()
        {
            var ids = new[] { "swordsman", "undead", "wraith", "occultist",
                "archer", "ghoul", "defender" };
            foreach (var id in ids)
            {
                var enemy = Load<CharacterDefinition>(
                    "Catacombs/Enemies/CD_Catacombs_" + id + ".asset");
                Assert.That(enemy.VisualSprite, Is.Not.Null, id);
                Assert.That(enemy.IdleAnimationFrames, Has.Count.EqualTo(4), id);
                Assert.That(enemy.VisualSprite, Is.SameAs(enemy.IdleAnimationFrames[0]), id);
                foreach (var sprite in enemy.IdleAnimationFrames.Concat(enemy.RunAnimationFrames))
                {
                    Assert.That(sprite, Is.Not.Null, id);
                    var path = AssetDatabase.GetAssetPath(sprite);
                    Assert.That(path, Does.Contain("/Core/frames/"), id);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16f), path);
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.textureCompression,
                        Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                }
            }
        }

        [Test]
        public void EncounterPoolUsesOnlyProductionEnemiesAndAtMostFourPerRoom()
        {
            var dungeon = Load<DungeonDefinition>("Dungeons/DD_Catacombs_Expedition.asset");
            Assert.That(dungeon.EncounterDefinitions, Has.Count.EqualTo(8));
            foreach (EncounterDefinition encounter in dungeon.EncounterDefinitions)
            {
                Assert.That(encounter, Is.Not.Null);
                Assert.That(encounter.Enemies.Count, Is.InRange(2, 4), encounter.name);
                Assert.That(encounter.Enemies.All(e => e.CharacterDefinition != null
                    && e.CharacterDefinition.Id.StartsWith("enemy.catacombs.", StringComparison.Ordinal)),
                    Is.True, encounter.name);
            }
        }

        [Test]
        public void ResourceBagsHaveTwoBracketsWithoutMissingEnergyDust()
        {
            var bag = Load<LootBagItemDefinition>("Catacombs/Loot/ID_Catacombs_Resources.asset");
            Assert.That(bag.TryGetBracket(LootLevelBracket.Level1To10, out var low), Is.True);
            Assert.That(bag.TryGetBracket(LootLevelBracket.Level11To20, out var high), Is.True);
            foreach (var bracket in new[] { low, high })
                Assert.That(bracket.Pools.SelectMany(p => p.Entries)
                    .All(e => e.ItemDefinition != null &&
                        !e.ItemDefinition.DisplayName.Contains("pył energetyczny")), Is.True);
            Assert.That(low.Pools, Has.Count.GreaterThan(4));
            Assert.That(high.Pools, Has.Count.GreaterThan(low.Pools.Count));
        }

        [Test]
        public void DropTablesUseApprovedRatesAndGaldurytesStartAtEleven()
        {
            var catalog = Load<DungeonLootTableCatalog>("Dungeons/DLC_Catacombs_Loot.asset");
            Assert.That(catalog.TryGetEnemyLootTable(DungeonTheme.Catacombs, 5, out var low), Is.True);
            Assert.That(catalog.TryGetEnemyLootTable(DungeonTheme.Catacombs, 15, out var high), Is.True);
            Assert.That(low.Entries.Select(e => e.DropChance), Is.EquivalentTo(new[] { .5f, .02f, .02f }));
            Assert.That(high.Entries.Select(e => e.DropChance), Is.EquivalentTo(new[] { .5f, .02f, .02f, .01f }));
            Assert.That(low.Entries.Any(e => e.ItemDefinition.Id.Contains("galduryte")), Is.False);
            Assert.That(high.Entries.Any(e => e.ItemDefinition.Id.Contains("galduryte")), Is.True);
            var lowRoll = new LootTableSystem(new System.Random(15), null, lootLevel: 5);
            Assert.That(lowRoll.RollLoot(low).OfType<LootBagItemInstance>()
                .SelectMany(b => b.Contents).Any(i => i is GalduryteItemInstance), Is.False);
        }

        [Test]
        public void ConsecutivePoolsInOneGroupNeverRepeatTheSameEntry()
        {
            var bag = ScriptableObject.CreateInstance<LootBagItemDefinition>();
            try
            {
                Set(bag, "Id", "test.catacombs.distinct");
                Set(bag, "ItemType", ItemType.Lootbag);
                var a = Load<ItemDefinition>("Items/Materials/Smithing/ID_IronOre.asset");
                var b = Load<ItemDefinition>("Items/Materials/Smithing/ID_GoatSkin.asset");
                var bracket = new LootBagBracketData();
                Set(bracket, "Bracket", LootLevelBracket.Level1To10);
                var pools = new List<LootBagPoolData>();
                for (var i = 0; i < 3; i++)
                {
                    var pool = new LootBagPoolData();
                    Set(pool, "DropChance", 1f);
                    Set(pool, "DistinctSelectionGroup", "materials");
                    var entries = new List<LootBagPoolEntryData>();
                    foreach (var item in new[] { a, b })
                    {
                        var entry = new LootBagPoolEntryData();
                        Set(entry, "ItemDefinition", item);
                        entries.Add(entry);
                    }
                    Set(pool, "Entries", entries);
                    pools.Add(pool);
                }
                Set(bracket, "Pools", pools);
                Set(bag, "Brackets", new List<LootBagBracketData> { bracket });
                for (var seed = 0; seed < 100; seed++)
                {
                    var loot = new LootTableSystem(new System.Random(seed), null, lootLevel: 5)
                        .CreateLootBag(bag);
                    Assert.That(loot.Contents.Select(i => i.Definition.Id),
                        Is.EquivalentTo(new[] { a.Id, b.Id }), "seed " + seed);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(bag); }
        }

        [Test]
        public void EquipmentAndGalduryteBagsCreateRealItemsAtTheirTier()
        {
            var catalog = Load<GameContentCatalog>("World/GC_MainContentCatalog.asset");
            var weaponBag = Load<LootBagItemDefinition>("Catacombs/Loot/ID_Catacombs_Weapons.asset");
            var galduryteBag = Load<LootBagItemDefinition>("Catacombs/Loot/ID_Catacombs_Galdurytes.asset");
            foreach (var level in new[] { 5, 15 })
            {
                var seenQuality = new HashSet<EquipmentQuality>();
                for (var seed = 0; seed < 100; seed++)
                {
                    var generator = new LootTableSystem(new System.Random(seed),
                        catalog.EquipmentPartDefinitions, lootLevel: level);
                    var weapons = generator.CreateLootBag(weaponBag).Contents;
                    Assert.That(weapons, Is.Not.Empty, "level " + level + ", seed " + seed);
                    Assert.That(weapons, Has.All.TypeOf<EquipmentItemInstance>(),
                        "level " + level + ", seed " + seed);
                    foreach (var weapon in weapons.Cast<EquipmentItemInstance>())
                    {
                        Assert.That(weapon.ItemLevel, Is.InRange(level - 3, level + 3));
                        seenQuality.Add(weapon.Quality);
                    }
                }
                Assert.That(seenQuality, Is.EquivalentTo(new[] {
                    EquipmentQuality.Poor, EquipmentQuality.Normal, EquipmentQuality.Excellent }));
            }
            var low = new LootTableSystem(new System.Random(1),
                catalog.EquipmentPartDefinitions, lootLevel: 5).CreateLootBag(galduryteBag);
            Assert.That(low.Contents, Is.Empty);
            var high = new LootTableSystem(new System.Random(1),
                catalog.EquipmentPartDefinitions, lootLevel: 15).CreateLootBag(galduryteBag);
            Assert.That(high.Contents, Is.Not.Empty);
            Assert.That(high.Contents, Has.All.TypeOf<GalduryteItemInstance>());
            Assert.That(high.Contents.Cast<GalduryteItemInstance>()
                .All(item => item.ItemLevel == 11 && item.Effects.Count > 0), Is.True);
        }
    }
}
