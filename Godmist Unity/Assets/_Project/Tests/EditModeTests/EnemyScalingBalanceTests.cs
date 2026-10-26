using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EnemyScalingBalanceTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
        static readonly string[] Heroes = { "CD_Wladimir_Test", "CD_Jerzy", "CD_Mage", "CD_Paladin" };
        static readonly string[] Trees = { "brawler", "assassin", "fire", "priest" };
        static readonly int[] Levels = { 1, 8, 10, 18, 28, 38, 48, 50 };
        static readonly Dictionary<string, EquipmentPartDefinition> ReferenceParts = new();
        static readonly List<UnityEngine.Object> Temporary = new();
        static VS10EquipmentSetup.Input reference;
        static EquipmentPartDefinition[] starters;
        [OneTimeSetUp] public void ApplyCalibration()=>Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();

        // Historical base/early/late HP assertions still exercise the legacy
        // fallback; production health overrides have their own all-level tests.
        static CharacterDefinition LegacyHealthDefinition(CharacterDefinition definition)
        {
            var clone=UnityEngine.Object.Instantiate(definition);Temporary.Add(clone);
            var data=new SerializedObject(clone);
            data.FindProperty("<GrowthData>k__BackingField").FindPropertyRelative("<HealthCurve>k__BackingField").arraySize=0;
            data.ApplyModifiedPropertiesWithoutUndo();return clone;
        }

        [TestCase("enemy.catacombs.swordsman",216,36,56,9566f,1410.175f)]
        [TestCase("enemy.forest.infected_ent",784,46,63,46896.5f,1849.05f)]
        [TestCase("enemy.forest.bandit_leader",280,55,76,16997.5f,1048.936f)]
        public void StartingBasesAndLevel50ReflectApprovedBaseBoost(string id,int hp,int min,int max,float lateHp,float lateMin)
        {
            var start=Enemy(id,1);var legacy=LegacyHealthDefinition(start.Definition);
            var end=new CharacterInstanceFactory().Create(legacy,legacy.ClassData,50);
            Assert.That(start.Definition.BaseStats.MaxHealth,Is.EqualTo(hp));
            Assert.That(start.Definition.BaseStats.MinAttack,Is.EqualTo(min));
            Assert.That(start.Definition.BaseStats.MaxAttack,Is.EqualTo(max));
            Assert.That(end.Stats.MaxHealth.BaseValue,Is.EqualTo(lateHp).Within(.02f));
            Assert.That(end.Stats.MinAttack.BaseValue,Is.EqualTo(lateMin).Within(.02f));
        }

        [TestCase("enemy.catacombs.swordsman",154,26,40,216,36,56)]
        [TestCase("enemy.catacombs.undead",220,19,30,308,27,42)]
        [TestCase("enemy.catacombs.wraith",119,16,47,167,22,66)]
        [TestCase("enemy.catacombs.occultist",110,35,50,154,49,70)]
        [TestCase("enemy.catacombs.archer",132,25,38,185,35,53)]
        [TestCase("enemy.catacombs.ghoul",176,23,42,246,32,59)]
        [TestCase("enemy.catacombs.defender",242,16,28,339,22,39)]
        [TestCase("enemy.forest.infected_tree",198,19,22,277,27,31)]
        [TestCase("enemy.forest.rabid_wolf",84,23,45,118,32,63)]
        [TestCase("enemy.forest.bandit_thug",154,22,35,216,31,49)]
        [TestCase("enemy.forest.bandit_shooter",119,28,44,167,39,62)]
        [TestCase("enemy.forest.elf_archer",106,32,44,148,45,62)]
        [TestCase("enemy.forest.elf_poacher",136,25,38,190,35,53)]
        [TestCase("enemy.forest.elf_druid",88,13,19,123,18,27)]

        [TestCase("enemy.catacombs.dead_king",360,29,58,504,41,81)]
        [TestCase("enemy.catacombs.skeleton_slayer",500,36,51,700,50,71)]
        [TestCase("enemy.forest.infected_ent",560,33,45,784,46,63)]
        [TestCase("enemy.forest.bandit_leader",200,39,54,280,55,76)]
        [TestCase("enemy.catacombs.dead_king.enraged",360,29,58,504,41,81)]
        [TestCase("enemy.catacombs.skeleton_slayer.enraged",500,36,51,700,50,71)]
        [TestCase("enemy.forest.infected_ent.burning",560,33,45,784,46,63)]
        [TestCase("enemy.forest.bandit_leader.revealed",200,39,54,280,55,76)]
        public void BaseBoostAddsOnlyRoundedBaseAtEveryMilestone(string id,int oldHp,int oldMin,int oldMax,int hp,int minimum,int maximum)
        {
            var definition=Catalog.CharacterDefinitions.Single(d=>d.Id==id);
            Assert.That(definition.BaseStats.MaxHealth,Is.EqualTo(hp));
            Assert.That(definition.BaseStats.MinAttack,Is.EqualTo(minimum));
            Assert.That(definition.BaseStats.MaxAttack,Is.EqualTo(maximum));
            var calibrated=LegacyHealthDefinition(definition);
            var prior=UnityEngine.Object.Instantiate(calibrated);Temporary.Add(prior);
            var data=new SerializedObject(prior);var basis=data.FindProperty("<BaseStats>k__BackingField");
            basis.FindPropertyRelative("<MaxHealth>k__BackingField").intValue=oldHp;
            basis.FindPropertyRelative("<MinAttack>k__BackingField").intValue=oldMin;
            basis.FindPropertyRelative("<MaxAttack>k__BackingField").intValue=oldMax;
            data.ApplyModifiedPropertiesWithoutUndo();
            var factory=new CharacterInstanceFactory();
            foreach(var level in new[]{1,5,10,20,30,40,48,50})
            {
                var before=factory.Create(prior,prior.ClassData,level);var after=factory.Create(calibrated,calibrated.ClassData,level);
                Assert.That(after.Stats.MaxHealth.BaseValue-before.Stats.MaxHealth.BaseValue,Is.EqualTo(hp-oldHp).Within(.02f),id+" HP L"+level);
                Assert.That(after.Stats.MinAttack.BaseValue-before.Stats.MinAttack.BaseValue,Is.EqualTo(minimum-oldMin).Within(.02f),id+" min ATK L"+level);
                Assert.That(after.Stats.MaxAttack.BaseValue-before.Stats.MaxAttack.BaseValue,Is.EqualTo(maximum-oldMax).Within(.02f),id+" max ATK L"+level);
                Assert.That(after.Stats.PhysicalDefense.BaseValue,Is.EqualTo(before.Stats.PhysicalDefense.BaseValue));
                Assert.That(after.Stats.MagicalDefense.BaseValue,Is.EqualTo(before.Stats.MagicalDefense.BaseValue));
            }
        }

        [TearDown]
        public void ReleaseReferenceObjects()
        {
            foreach (var value in Temporary) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            Temporary.Clear(); ReferenceParts.Clear();
        }

        static EquipmentPartDefinition Part(CharacterDefinition definition, EquipmentPartKind kind, int bracket, bool starter, int material)
        {
            if (starter)
            {
                starters ??= AssetDatabase.FindAssets("t:EquipmentPartDefinition", new[] { Root + "StartingCampaign" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentPartDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
                return starters.Single(p => p.RequiredClass == definition.ClassData && p.Kind == kind);
            }
            reference ??= JsonUtility.FromJson<VS10EquipmentSetup.Input>(File.ReadAllText(
                "Assets/_Project/Tests/EditModeTests/EnemyScalingEquipmentReference.json"));
            var candidates = reference.parts.Where(p => p.characterClass.Equals(definition.ClassData.Id.Replace("class.", ""), StringComparison.OrdinalIgnoreCase)
                && p.kind == (int)kind && p.level == bracket).ToArray();
            Assert.That(candidates, Has.Length.EqualTo(4), definition.Id + " " + kind + " " + bracket);
            var row = candidates[material % candidates.Length];
            if (ReferenceParts.TryGetValue(row.id, out var cached)) return cached;
            // Transient reference equipment only. No production asset is modified or saved.
            var template = Catalog.EquipmentPartDefinitions.First(p => p.RequiredClass == definition.ClassData && p.Kind == kind);
            var part = UnityEngine.Object.Instantiate(template);
            Temporary.Add(part);
            var data = new SerializedObject(part);
            data.FindProperty("<Id>k__BackingField").stringValue = row.id;
            data.FindProperty("<LevelBracket>k__BackingField").intValue = bracket;
            data.FindProperty("<IsStarterOnly>k__BackingField").boolValue = false;
            var stats = data.FindProperty("<Stats>k__BackingField");
            foreach (var field in typeof(EquipmentPartStats).GetFields()) stats.FindPropertyRelative(field.Name).floatValue = 0f;
            foreach (var stat in row.stats) stats.FindPropertyRelative(stat.name).floatValue = stat.value;
            data.ApplyModifiedPropertiesWithoutUndo();
            ReferenceParts.Add(row.id, part);
            return part;
        }

        [Serializable] public class Row
        {
            public string id, build, equipment;
            public int level, invested, remaining, weaponLevel, armorLevel;
            public float hp, minAttack, maxAttack, physicalDefense, magicalDefense, dodge, critChance, critDamage, speed;
        }
        [Serializable] public class Matchup
        {
            public string attacker, target, skill;
            public int level, min, max;
            public float hitChance, expectedDamage, targetHealthFraction;
        }
        [Serializable] public class SkillRow
        {
            public string caster, target, skill;
            public int min, max, apPercent, resourceCost;
            public float hitChance;
            public bool damage;
        }
        [Serializable] public class Audit
        {
            public List<Row> rows = new();
            public List<Matchup> matchups = new();
            public List<SkillRow> skillsAt48 = new();
        }

        internal static CharacterInstance Hero(int index, int level, bool developed = true, bool starter = false,
            int material = 0, bool upgraded = false, int treeIndex = -1, bool highestQuality = false)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Characters/" + Heroes[index] + ".asset");
            var character = new CharacterInstanceFactory().Create(definition, definition.ClassData, level);
            var generation = new EquipmentGenerationService(new System.Random(42));
            var bracket = level < 5 ? 1 : level < 15 ? 5 : level < 25 ? 15 : level < 35 ? 25 : level < 45 ? 35 : 45;
            var quality = level == 50 ? EquipmentQuality.Masterpiece : level >= bracket + 3 && bracket > 1
                ? EquipmentQuality.Excellent : EquipmentQuality.Normal;
            if (highestQuality && bracket > 1 && level >= bracket + 5)
                quality = EquipmentQuality.Masterpiece;
            foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
            {
                var type = slot == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor;
                var start = starter || bracket == 1;
                var item = Catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().Where(d =>
                    d.RequiredClass == definition.ClassData && d.ItemType == type && d.IsStarterEquipment == start
                    && !(d is UniqueEquipmentDefinition)).OrderBy(d => d.Id).First();
                var kinds = type == ItemType.Weapon
                    ? new[] { EquipmentPartKind.WeaponHead, EquipmentPartKind.WeaponBinder, EquipmentPartKind.WeaponHandle }
                    : new[] { EquipmentPartKind.ArmorPlate, EquipmentPartKind.ArmorBinder, EquipmentPartKind.ArmorBase };
                var parts = kinds.Select(kind => Part(definition, kind, bracket, start, material)).ToArray();
                var gear = generation.GenerateFromSelectedParts(item, parts, start ? EquipmentQuality.Normal : quality, RarityTier.Common);
                Assert.That(gear, Is.Not.Null, item.Id);
                if (upgraded) { gear.UpgradeProgress = 1f; gear.RecalculateStatModifiers(); }
                character.Equipment.SetItem(slot, gear);
            }
            if (!starter && level >= 10)
            {
                foreach (var jewelry in Catalog.ItemDefinitions.OfType<JewelryItemDefinition>())
                {
                    var metal = jewelry.Metals.Where(m => JewelryGenerationService.GetItemLevel(m) <= level)
                        .OrderByDescending(JewelryGenerationService.GetItemLevel).ThenBy(m => m.Material.Id)
                        .FirstOrDefault();
                    var gem = jewelry.Gems.FirstOrDefault(g => g.name == "JGD_Topaz") ?? jewelry.Gems.First();
                    if (metal != null) character.Equipment.SetItem(jewelry.EquipmentSlot,
                        new JewelryGenerationService().GenerateSelected(jewelry, metal, gem));
                }
            }
            if (developed && level > 1)
            {
                character.AvailableSkillPoints = Enumerable.Range(2, level - 1).Sum(CharacterProgressionSystem.GetSkillPointsGrantedOnLevelUp);
                var trees = character.ClassData.SkillTrees.Where(t => t != null).ToArray();
                var primary = treeIndex < 0 ? trees.Single(t => t.Id.EndsWith("." + Trees[index], StringComparison.Ordinal)) : trees[treeIndex];
                var ordered = new[] { primary }.Concat(trees.Where(t => t != primary));
                var progression = new SkillTreeProgressionSystem();
                var changed = true;
                while (character.AvailableSkillPoints > 0 && changed)
                {
                    changed = false;
                    foreach (var tree in ordered)
                    {
                        foreach (var node in tree.Nodes.OrderBy(n => n.Tier).ThenBy(n => n.Column))
                        {
                            if (!progression.TryInvest(character, tree.Id, node.Id)) continue;
                            changed = true;
                            break;
                        }
                        if (changed) break;
                    }
                }
                Assert.That(character.AvailableSkillPoints, Is.Zero, definition.Id + " legal build must spend its actual budget");
            }
            new CharacterStatPipelineSystem().Refresh(character);
            if (character.ResourcePool.ResourceType == ResourceType.Mana)
            {
                character.ResourcePool.CurrentValue = character.ResourcePool.MaxValue;
                new CharacterStatPipelineSystem().Refresh(character);
            }
            character.CurrentHealth = Mathf.RoundToInt(character.Stats.MaxHealth.GetValue());
            return character;
        }

        static Row Snapshot(CharacterInstance character, string build, string equipment)
        {
            var stats = character.Stats;
            return new Row { id = character.Definition.Id, level = character.Level, build = build, equipment = equipment,
                invested = character.SkillTreeProgress.Sum(t => t.NodeInvestments.Sum(n => n.Ranks)), remaining = character.AvailableSkillPoints,
                hp = stats.MaxHealth.GetValue(), minAttack = stats.MinAttack.GetValue(), maxAttack = stats.MaxAttack.GetValue(),
                physicalDefense = stats.PhysicalDefense.GetValue(), magicalDefense = stats.MagicalDefense.GetValue(),
                dodge = stats.Dodge.GetValue(), critChance = stats.CritChance.GetValue(), critDamage = stats.CritDamage.GetValue(), speed = stats.Speed.GetValue(),
                weaponLevel = character.Equipment.GetItem(EquipmentSlot.Weapon)?.ItemLevel ?? 0,
                armorLevel = character.Equipment.GetItem(EquipmentSlot.Armor)?.ItemLevel ?? 0 };
        }

        static BattleUnit Unit(CharacterInstance character, bool player = false) => new()
        { Character = character, IsPlayerSide = player, Position = new GridPosition(player ? 2 : 3, 2), CurrentActionPoints = 10, MaxActionPoints = 10 };

        static Matchup Compare(BattleUnit source, BattleUnit target)
        {
            var skill = source.Character.GetBasicAttackSkill();
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(source, target, skill);
            var stats = source.Character.Stats;
            // Basic attacks only: report independent critical expectation separately from the preview's non-critical range.
            var expected = (preview.Damage.Minimum + preview.Damage.Maximum) * .5f
                * (preview.HasHitChance ? preview.HitChance : 1f)
                * (1f + Mathf.Clamp01(stats.CritChance.GetValue()) * Mathf.Max(0f, stats.CritDamage.GetValue() - 1f));
            return new Matchup { attacker = source.Character.Definition.Id, target = target.Character.Definition.Id,
                skill = skill.SkillId, level = source.Character.Level, min = preview.Damage.Minimum, max = preview.Damage.Maximum,
                hitChance = preview.HitChance, expectedDamage = expected, targetHealthFraction = expected / target.Character.Stats.MaxHealth.GetValue() };
        }

        [Test]
        public void ExportRealContentScalingAndLegalSkillTreeBuilds()
        {
            var audit = new Audit();
            foreach (var level in Levels)
            {
                var heroes = Enumerable.Range(0, 4).Select(i => Hero(i, level)).ToArray();
                foreach (var hero in heroes) audit.rows.Add(Snapshot(hero, "developed", "level appropriate, no upgrades, Common"));
                foreach (var definition in Catalog.CharacterDefinitions.Where(d => d != null
                    && Godmist.Editor.EnemyResourceProfilesSetup.TryGetType(d.Id, out _)))
                {
                    var enemy = new CharacterInstanceFactory().Create(definition, definition.ClassData, level);
                    audit.rows.Add(Snapshot(enemy, "enemy", "none"));
                    foreach (var hero in heroes)
                    { audit.matchups.Add(Compare(Unit(enemy), Unit(hero, true))); audit.matchups.Add(Compare(Unit(hero, true), Unit(enemy))); }
                    if (level == 48)
                    {
                        foreach (var hero in heroes)
                        foreach (var pair in new[] { (source: Unit(hero, true), target: Unit(enemy)), (source: Unit(enemy), target: Unit(hero, true)) })
                        foreach (var skill in pair.source.Character.GetKnownSkills())
                        {
                            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(pair.source, pair.target, skill);
                            audit.skillsAt48.Add(new SkillRow { caster = pair.source.Character.Definition.Id, target = pair.target.Character.Definition.Id,
                                skill = skill.SkillId, min = preview.Damage.Minimum, max = preview.Damage.Maximum,
                                hitChance = preview.HitChance, damage = preview.Damage.HasDamage,
                                apPercent = skill.ActionCost, resourceCost = skill.ResourceCost });
                        }
                    }
                }
            }
            for (var i = 0; i < 4; i++)
            {
                audit.rows.Add(Snapshot(Hero(i, 10, false, true), "unspent", "starter level1"));
                audit.rows.Add(Snapshot(Hero(i, 48, true, false, 0, true), "developed", "fully upgraded"));
                var treeCount = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Characters/" + Heroes[i] + ".asset").ClassData.SkillTrees.Count(t => t != null);
                for (var tree = 0; tree < treeCount; tree++) for (var material = 0; material < 4; material++)
                    audit.rows.Add(Snapshot(Hero(i, 48, true, false, material, false, tree), "tree " + tree, "material " + material));
            }
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/enemy-scaling-pass-20261016"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "scaling-audit.json"), JsonUtility.ToJson(audit, true));
        }

        [Test]
        public void ApprovedCurveRemains199AndLevel48EquipmentIsNotUpgradedOrUnique()
        {
            Assert.That(new CharacterLevelGrowthSystem().GetCumulativeGrowthMultiplier(50), Is.EqualTo(199));
            for (var i = 0; i < 4; i++)
            {
                var hero = Hero(i, 48);
                if (hero.ResourcePool.ResourceType == ResourceType.Mana)
                    Assert.That(hero.ResourcePool.CurrentValue, Is.EqualTo(hero.ResourcePool.MaxValue));
                foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    var gear = hero.Equipment.GetItem(slot);
                    Assert.That(gear.ItemLevel, Is.EqualTo(48));
                    Assert.That(gear.UpgradeProgress, Is.Zero);
                    Assert.That(gear.Definition, Is.Not.InstanceOf<UniqueEquipmentDefinition>());
                }
            }
        }

        static CharacterInstance Enemy(string id, int level = 48)
        {
            var definition = Catalog.CharacterDefinitions.Single(d => d.Id == id);
            return new CharacterInstanceFactory().Create(definition, definition.ClassData, level);
        }

        [Test]
        public void FocusFireThreatensAnUnhealedMageWhileArmorAndUpgradesStillHelp()
        {
            var mage = Unit(Hero(2, 48), true);
            var warrior = Unit(Hero(0, 48), true);
            var upgraded = Unit(Hero(2, 48, upgraded: true), true);
            var swordsman = Unit(Enemy("enemy.catacombs.swordsman"));
            var occultist = Unit(Enemy("enemy.catacombs.occultist"));
            var shooter = Unit(Enemy("enemy.forest.bandit_shooter"));
            Assert.That(Compare(swordsman, mage).targetHealthFraction, Is.InRange(.1f, .3f));
            Assert.That(Compare(occultist, mage).targetHealthFraction, Is.InRange(.15f, .4f));
            Assert.That(Compare(swordsman, warrior).targetHealthFraction, Is.LessThan(Compare(swordsman, mage).targetHealthFraction));
            Assert.That(Compare(swordsman, upgraded).targetHealthFraction, Is.LessThan(Compare(swordsman, mage).targetHealthFraction));
            Assert.That(Compare(upgraded, swordsman).expectedDamage, Is.GreaterThan(Compare(mage, swordsman).expectedDamage));
            // A pressure envelope, not a claim that AI will focus perfectly or a simulated win rate.
            var focusedTwoCycles = 2f * (2f * Compare(occultist, mage).expectedDamage + 2f * Compare(shooter, mage).expectedDamage);
            Assert.That(focusedTwoCycles, Is.GreaterThan(mage.Character.Stats.MaxHealth.GetValue()));
        }

        [Test]
        public void LevelTenStarterWithoutDevelopmentIsMoreVulnerableThanPreparedParty()
        {
            var starter = Unit(Hero(2, 10, false, true), true);
            var prepared = Unit(Hero(2, 10), true);
            var enemy = Unit(Enemy("enemy.catacombs.swordsman", 10));
            Assert.That(Compare(enemy, starter).targetHealthFraction, Is.GreaterThan(Compare(enemy, prepared).targetHealthFraction));
            Assert.That(starter.Character.Equipment.GetItem(EquipmentSlot.Armor).ItemLevel, Is.EqualTo(1));
        }

        [Test]
        public void ReauthoringIsIdempotentPhasesAgreeAndSpecialHealthRulesStayIntact()
        {
            var definitions = Catalog.CharacterDefinitions.Where(d => d != null && Godmist.Editor.EnemyResourceProfilesSetup.TryGetType(d.Id, out _)).ToArray();
            Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();
            var before = definitions.Select(d => EditorJsonUtility.ToJson(d)).ToArray();
            Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();
            Assert.That(definitions.Select(d => EditorJsonUtility.ToJson(d)).ToArray(), Is.EqualTo(before));
            foreach (var definition in definitions.Where(d => d.SecondPhaseDefinition != null
                && d.SecondPhaseDefinition.Id.StartsWith(d.Id + ".", StringComparison.Ordinal)))
            {
                var phase = definition.SecondPhaseDefinition;
                Assert.That(phase.GrowthData.MaxHealth, Is.EqualTo(definition.GrowthData.MaxHealth), definition.Id);
                Assert.That(phase.GrowthData.MinAttack, Is.EqualTo(definition.GrowthData.MinAttack), definition.Id);
                Assert.That(phase.GrowthData.MaxAttack, Is.EqualTo(definition.GrowthData.MaxAttack), definition.Id);
                Assert.That(phase.GrowthData.LateMaxHealth, Is.EqualTo(definition.GrowthData.LateMaxHealth), definition.Id);
                Assert.That(phase.GrowthData.LateMinAttack, Is.EqualTo(definition.GrowthData.LateMinAttack), definition.Id);
                Assert.That(phase.GrowthData.LateMaxAttack, Is.EqualTo(definition.GrowthData.LateMaxAttack), definition.Id);
                Assert.That(phase.GrowthData.EarlyMaxHealth, Is.EqualTo(definition.GrowthData.EarlyMaxHealth), definition.Id);
                Assert.That(phase.GrowthData.EarlyMinAttack, Is.EqualTo(definition.GrowthData.EarlyMinAttack), definition.Id);
                Assert.That(phase.GrowthData.EarlyMaxAttack, Is.EqualTo(definition.GrowthData.EarlyMaxAttack), definition.Id);
                Assert.That(phase.GrowthData.LateMaxHealthOffset, Is.EqualTo(definition.GrowthData.LateMaxHealthOffset), definition.Id);
                Assert.That(phase.GrowthData.LateMinAttackOffset, Is.EqualTo(definition.GrowthData.LateMinAttackOffset), definition.Id);
                Assert.That(phase.GrowthData.LateMaxAttackOffset, Is.EqualTo(definition.GrowthData.LateMaxAttackOffset), definition.Id);
            }
            Assert.That(Enemy("enemy.catacombs.spectral_knight", 50).Stats.MaxHealth.GetValue(), Is.EqualTo(3));
            var leader = Enemy("enemy.forest.bandit_leader.revealed");
            Assert.That(leader.Definition.GrowthData.MinAttack, Is.EqualTo(4.23f));
            Assert.That(leader.Definition.GrowthData.MaxAttack, Is.EqualTo(5.85f));
            Assert.That(leader.Definition.GrowthData.MinAttack + leader.Definition.GrowthData.LateMinAttack,
                Is.EqualTo(4.865f + (65-39)/199f).Within(.001f));
        }

        [Test]
        public void EarlyAndLateBonusesKeepHeroesAndLevel48AnchorAndTheSame199Curve()
        {
            var curve=new CharacterLevelGrowthSystem();
            Assert.That(curve.GetLateGrowthMultiplier(1),Is.Zero);
            Assert.That(curve.GetLateGrowthMultiplier(10),Is.Zero);
            Assert.That(curve.GetLateGrowthMultiplier(48),Is.EqualTo(181));
            Assert.That(curve.GetLateGrowthMultiplier(50),Is.EqualTo(199));
            var previous=0f;
            for(var level=1;level<=50;level++)
            {
                var value=curve.GetLateGrowthMultiplier(level);
                Assert.That(value,Is.InRange(previous,curve.GetCumulativeGrowthMultiplier(level)));
                previous=value;
            }
            foreach(var definition in Catalog.CharacterDefinitions.Where(d=>d!=null && Godmist.Editor.EnemyResourceProfilesSetup.TryGetType(d.Id,out _)))
            {
                var growth=definition.GrowthData;var start=definition.BaseStats;
                var legacy=LegacyHealthDefinition(definition);
                foreach(var level in new[]{1,10,48,50})
                {
                    var character=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                    var regular=curve.GetCumulativeGrowthMultiplier(level);var late=curve.GetLateGrowthMultiplier(level);
                    var blend=regular>0?late/regular:0;
                    var legacyHealth=new CharacterInstanceFactory().Create(legacy,legacy.ClassData,level).Stats.MaxHealth.BaseValue;
                    Assert.That(legacyHealth,Is.EqualTo(start.MaxHealth+growth.MaxHealth*regular+growth.LateMaxHealth*late+growth.EarlyMaxHealth*(regular-late)+growth.LateMaxHealthOffset*blend).Within(.01f));
                    Assert.That(character.Stats.MinAttack.BaseValue,Is.EqualTo(start.MinAttack+growth.MinAttack*regular+growth.LateMinAttack*late+growth.EarlyMinAttack*(regular-late)+growth.LateMinAttackOffset*blend).Within(.01f));
                    if(level>=48)Assert.That(regular-late,Is.Zero,"Early calibration cannot change the verified level48/50 anchor.");
                    var savedHealth=Math.Min(17,character.CurrentHealth);character.CurrentHealth=savedHealth;
                    curve.Apply(character,grantCurrentHealthGrowth:false);
                    Assert.That(character.CurrentHealth,Is.EqualTo(savedHealth),"Restoring recalibrated stats must not heal saved enemies.");
                }
            }
            for(var i=0;i<4;i++)
            {
                var growth=Hero(i,48).Definition.GrowthData;
                Assert.That(growth.LateMaxHealth,Is.Zero);Assert.That(growth.LateMinAttack,Is.Zero);Assert.That(growth.LateMaxAttack,Is.Zero);
                Assert.That(growth.EarlyMaxHealth,Is.Zero);Assert.That(growth.EarlyMinAttack,Is.Zero);Assert.That(growth.EarlyMaxAttack,Is.Zero);
                Assert.That(growth.LateMaxHealthOffset,Is.Zero);Assert.That(growth.LateMinAttackOffset,Is.Zero);Assert.That(growth.LateMaxAttackOffset,Is.Zero);
            }
        }

        [Test]
        public void StrongerOrdinaryEnemiesAddBaseBoostAndGrowMonotonicallyWithoutHealingOnRestore()
        {
            var curve=new CharacterLevelGrowthSystem();
            var ordinary=Catalog.CharacterDefinitions.Where(d=>d!=null && Godmist.Editor.EnemyScalingBalanceSetup.UsesEarlyThreatCalibration(d.Id)).ToArray();
            Assert.That(ordinary,Has.Length.EqualTo(14));
            foreach(var definition in ordinary)
            {
                Assert.That(Godmist.Editor.EnemyScalingBalanceSetup.TryGetOriginalBase(definition.Id,out var original),Is.True);
                var oldBase=new Vector3(Mathf.RoundToInt(original.x*.4f),Mathf.RoundToInt(original.y*.6f),Mathf.RoundToInt(original.z*.6f));
                var growth=definition.GrowthData;
                var baseNow=new Vector3(definition.BaseStats.MaxHealth,definition.BaseStats.MinAttack,definition.BaseStats.MaxAttack);
                var offsets=new Vector3(growth.LateMaxHealthOffset,growth.LateMinAttackOffset,growth.LateMaxAttackOffset);
                var priorBase=new Vector3(Mathf.RoundToInt(original.x*.44f),Mathf.RoundToInt(original.y*.63f),Mathf.RoundToInt(original.z*.63f));
                var increase=new Vector3(Mathf.RoundToInt(priorBase.x*1.4f),Mathf.RoundToInt(priorBase.y*1.4f),Mathf.RoundToInt(priorBase.z*1.4f))-priorBase;
                Assert.That(baseNow+offsets,Is.EqualTo(oldBase+increase),definition.Id+" only the rounded base increase is added at the endpoints");
                var previous=Vector3.zero;
                var legacy=LegacyHealthDefinition(definition);
                for(var level=1;level<=50;level++)
                {
                    var enemy=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                    var legacyHealth=new CharacterInstanceFactory().Create(legacy,legacy.ClassData,level).Stats.MaxHealth.GetValue();
                    var actual=new Vector3(enemy.Stats.MaxHealth.GetValue(),enemy.Stats.MinAttack.GetValue(),enemy.Stats.MaxAttack.GetValue());
                    Assert.That(actual.x,Is.GreaterThanOrEqualTo(previous.x),definition.Id+" HP L"+level);
                    Assert.That(actual.y,Is.GreaterThanOrEqualTo(previous.y),definition.Id+" min ATK L"+level);
                    Assert.That(actual.z,Is.GreaterThanOrEqualTo(previous.z),definition.Id+" max ATK L"+level);
                    var regular=curve.GetCumulativeGrowthMultiplier(level);var late=curve.GetLateGrowthMultiplier(level);
                    var originalGrowth=new Vector3(growth.MaxHealth,growth.MinAttack,growth.MaxAttack);
                    var oldEarly=new Vector3(Mathf.Round(growth.MaxHealth*.6f*1000)/1000-growth.MaxHealth,
                        Mathf.Round(growth.MinAttack*.6f*1000)/1000-growth.MinAttack,
                        Mathf.Round(growth.MaxAttack*.6f*1000)/1000-growth.MaxAttack);
                    var baseline=oldBase+originalGrowth*regular
                        +new Vector3(growth.LateMaxHealth,growth.LateMinAttack,growth.LateMaxAttack)*late+oldEarly*(regular-late);
                    if(level<48)
                    {
                        Assert.That(legacyHealth,Is.GreaterThan(baseline.x),definition.Id+" legacy HP L"+level);
                        // GetValue includes the existing global ATK multiplier;
                        // the hypothetical historical baseline is a raw stat.
                        Assert.That(actual.y,Is.GreaterThan(baseline.y*definition.AttackMultiplier),definition.Id+" min ATK L"+level);
                        Assert.That(actual.z,Is.GreaterThan(baseline.z*definition.AttackMultiplier),definition.Id+" max ATK L"+level);
                    }
                    else
                    {
                        Assert.That(legacyHealth,Is.EqualTo(baseline.x+increase.x).Within(.02f),definition.Id+" legacy HP anchor L"+level);
                        Assert.That(actual.y,Is.EqualTo((baseline.y+increase.y)*definition.AttackMultiplier).Within(.02f),definition.Id+" min ATK anchor L"+level);
                        Assert.That(actual.z,Is.EqualTo((baseline.z+increase.z)*definition.AttackMultiplier).Within(.02f),definition.Id+" max ATK anchor L"+level);
                    }
                    enemy.CurrentHealth=17;
                    curve.Apply(enemy,grantCurrentHealthGrowth:false);
                    Assert.That(enemy.CurrentHealth,Is.EqualTo(17),"Recalibration must not heal saved enemies.");
                    previous=actual;
                }
            }
        }
    }
}
