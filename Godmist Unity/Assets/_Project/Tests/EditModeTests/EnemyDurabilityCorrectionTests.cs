using System;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class EnemyDurabilityCorrectionTests
    {
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        static CharacterDefinition Definition(string id) => Catalog.CharacterDefinitions.Single(d => d != null && d.Id == id);
        static string Folder => Path.GetFullPath("../LocalHandoffs/enemy-role-durability-implementation-20261025");
        static SerializedProperty Growth(SerializedObject data, string name) => data.FindProperty("<GrowthData>k__BackingField").FindPropertyRelative("<" + name + ">k__BackingField");

        [Test, Explicit("Author the approved ordinary-only HP/MDEF correction via Editor API. No boss or ATK changes.")]
        public void AuthorApprovedDurabilityCorrection()
        {
            var definitions = Catalog.CharacterDefinitions.Where(d => d != null && File.Exists(Path.Combine("../LocalHandoffs/enemy-role-implementation-20261025/before-definitions", d.Id + ".json"))).ToArray();
            Assert.That(definitions, Has.Length.EqualTo(23));
            foreach (var stage in new[] { "before-definitions", "after-definitions" }) Directory.CreateDirectory(Path.Combine(Folder, stage));
            foreach (var d in definitions)
            {
                var path = Path.Combine(Folder, "before-definitions", d.Id + ".json");
                if (!File.Exists(path)) File.WriteAllText(path, EditorJsonUtility.ToJson(d));
            }
            EnemyRoleDurabilitySetup.ApplyAll();
            foreach (var d in definitions) File.WriteAllText(Path.Combine(Folder, "after-definitions", d.Id + ".json"), EditorJsonUtility.ToJson(d));
            // The second pass must be a no-op, including all protected definitions.
            foreach (var d in definitions) Assert.That(EnemyRoleDurabilitySetup.ApplyToDefinition(d), Is.False, d.Id);
        }

        [TestCase("enemy.catacombs.archer", 202, 442, 9391)]
        [TestCase("enemy.catacombs.defender", 209, 478, 12548)]
        [TestCase("enemy.catacombs.ghoul", 208, 450, 9170)]
        [TestCase("enemy.catacombs.occultist", 176, 408, 8981)]
        [TestCase("enemy.catacombs.swordsman", 210, 462, 9791)]
        [TestCase("enemy.catacombs.undead", 299, 668, 14303)]
        [TestCase("enemy.catacombs.wraith", 176, 398, 8542)]
        [TestCase("enemy.forest.bandit_shooter", 176, 383, 8665)]
        [TestCase("enemy.forest.bandit_thug", 197, 427, 9290)]
        [TestCase("enemy.forest.elf_archer", 161, 343, 7501)]
        [TestCase("enemy.forest.elf_druid", 146, 326, 7526)]
        [TestCase("enemy.forest.elf_poacher", 179, 383, 8238)]
        [TestCase("enemy.forest.infected_tree", 235, 550, 14259)]
        [TestCase("enemy.forest.rabid_wolf", 138, 294, 6655)]
        public void ProductionHealthAnchorsAndEveryIntermediateLevelAreSafe(string id, int start, int ten, int fifty)
        {
            var definition = Definition(id); var factory = new CharacterInstanceFactory();
            Assert.That(factory.Create(definition, definition.ClassData, 1).CurrentHealth, Is.EqualTo(start));
            Assert.That(factory.Create(definition, definition.ClassData, 10).CurrentHealth, Is.EqualTo(ten));
            Assert.That(factory.Create(definition, definition.ClassData, 50).CurrentHealth, Is.EqualTo(fifty));
            var previous = 0f;
            for (var level = 1; level <= 50; level++)
            {
                var c = factory.Create(definition, definition.ClassData, level);
                var hp = c.Stats.MaxHealth.GetValue();
                Assert.That(hp, Is.GreaterThanOrEqualTo(previous), id + " L" + level);
                Assert.That(c.CurrentHealth, Is.EqualTo(hp), "Fresh NPC starts at full health, including L1 curve overrides.");
                c.CurrentHealth = 17;
                var growth = new CharacterLevelGrowthSystem(); growth.Apply(c, false); growth.Apply(c, false);
                Assert.That(c.CurrentHealth, Is.EqualTo(17), "Rebuilding must not heal saved NPCs.");
                Assert.That(c.Stats.MaxHealth.GetValue(), Is.EqualTo(hp), "Rebuilding must not compound.");
                previous = hp;
            }
        }

        [Test]
        public void HealthInterpolatesGrowthUnitsAndRealLevelUpAddsOnlyTheMaximumGain()
        {
            var d = Definition("enemy.catacombs.defender"); var factory = new CharacterInstanceFactory();
            var c = factory.Create(d, d.ClassData, 12);
            Assert.That(c.Stats.MaxHealth.GetValue(), Is.EqualTo(617)); // G12=13, G10=9, G15=19: 478 + (826-478)*.4.
            var previous = c.Stats.MaxHealth.GetValue(); c.CurrentHealth = 17; c.Level = 13;
            new CharacterLevelGrowthSystem().Apply(c, true);
            Assert.That(c.CurrentHealth, Is.EqualTo(17 + c.Stats.MaxHealth.GetValue() - previous));
            c.CurrentHealth = int.MaxValue;
            new CharacterLevelGrowthSystem().Apply(c, false);
            Assert.That(c.CurrentHealth, Is.EqualTo(c.Stats.MaxHealth.GetValue()), "Old saved health must be clamped to the new maximum.");
        }

        [TestCase(false), TestCase(true)]
        public void EmptyOrInvalidHealthCurveUsesTheUnchangedLegacyFormula(bool invalid)
        {
            var clone = Object.Instantiate(Definition("enemy.catacombs.defender"));
            try
            {
                var data = new SerializedObject(clone);
                data.FindProperty("<BaseStats>k__BackingField").FindPropertyRelative("<MaxHealth>k__BackingField").intValue = 100;
                Growth(data, "MaxHealth").floatValue = 10;
                Growth(data, "EarlyMaxHealth").floatValue = Growth(data, "LateMaxHealth").floatValue = Growth(data, "LateMaxHealthOffset").floatValue = 0;
                var curve = Growth(data, "HealthCurve"); curve.arraySize = invalid ? 2 : 0;
                if (invalid) for (var i = 0; i < 2; i++)
                {
                    curve.GetArrayElementAtIndex(i).FindPropertyRelative("<Level>k__BackingField").intValue = 5;
                    curve.GetArrayElementAtIndex(i).FindPropertyRelative("<MaxHealth>k__BackingField").intValue = 999;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(new CharacterInstanceFactory().Create(clone, clone.ClassData, 15).Stats.MaxHealth.GetValue(), Is.EqualTo(290));
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [TestCase("enemy.catacombs.defender"), TestCase("enemy.catacombs.occultist"), TestCase("enemy.forest.elf_druid")]
        public void BothOlderAuthoringPathsPreserveTheNewVariant(string id)
        {
            var clone = Object.Instantiate(Definition(id));
            try
            {
                var before = EditorJsonUtility.ToJson(clone);
                Assert.That(EnemyRoleStatsSetup.ApplyToDefinition(clone), Is.False);
                Assert.That(EnemyScalingBalanceSetup.ApplyToDefinition(clone), Is.False);
                Assert.That(EditorJsonUtility.ToJson(clone), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [TestCase("wladimir"), TestCase("enemy.catacombs.spectral_knight"), TestCase("enemy.catacombs.dead_king")]
        [TestCase("enemy.catacombs.dead_king.enraged"), TestCase("enemy.catacombs.skeleton_slayer"), TestCase("enemy.catacombs.skeleton_slayer.enraged")]
        [TestCase("enemy.forest.infected_ent"), TestCase("enemy.forest.infected_ent.burning"), TestCase("enemy.forest.bandit_leader"), TestCase("enemy.forest.bandit_leader.revealed")]
        public void HeroesBossesPhasesAndSpecialKnightAreExcluded(string id)
        {
            var d = Definition(id); var before = EditorJsonUtility.ToJson(d);
            Assert.That(EnemyRoleDurabilitySetup.ApplyToDefinition(d), Is.False);
            Assert.That(d.GrowthData.HealthCurve, Is.Empty);
            Assert.That(EditorJsonUtility.ToJson(d), Is.EqualTo(before));
        }
    }
}
