using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Data.World;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.Combat;
using Godmist.Data.Skills;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class EnemyBaseAttackScalingTests
    {
        [Test]
        public void FlatModifierIsNotScaledByTheDefinitionCorrection()
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var characterClass = ScriptableObject.CreateInstance<CharacterClassData>();
            try
            {
                var data = new SerializedObject(definition); data.FindProperty("<Id>k__BackingField").stringValue = "scaling.test";
                var basis = data.FindProperty("<BaseStats>k__BackingField");
                basis.FindPropertyRelative("<MinAttack>k__BackingField").floatValue = 85;
                basis.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue = 170;
                data.ApplyModifiedPropertiesWithoutUndo();
                var character = new CharacterInstanceFactory().Create(definition, characterClass);
                character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { EffectId = "test.flat",
                    Lifetime = Godmist.Core.Enums.PassiveEffectLifetime.Persistent,
                    StatModifiers = new() { new Godmist.Core.Types.StatModifier {
                        StatType = Godmist.Core.Enums.CharacterStatType.MinAttack,
                        Operation = Godmist.Core.Enums.StatModifierOperation.FlatAbsolute, Value = 10 } } });
                new CharacterStatPipelineSystem().Refresh(character);
                Assert.That(character.Stats.MinAttack.GetValue(), Is.EqualTo(95));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(characterClass); }
        }

        [Test]
        public void All23ProfilesAcross50LevelsKeepTheirUnbuffedAttackAndUseTheCorrectedBase()
        {
            var definitions = Profiles().ToArray(); Assert.That(definitions.Length, Is.EqualTo(23));
            var frozen = File.ReadAllLines(Path.GetFullPath("Assets/_Project/Tests/EditModeTests/Fixtures/EnemyAttackBeforeRebase.tsv"))
                .Select(line => line.Split('\t')).ToDictionary(row => (row[0], int.Parse(row[1])),
                    row => row.Skip(2).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            var count = 0;
            foreach (var definition in definitions)
            {
                    for (var level = 1; level <= 50; level++)
                    {
                        var baseline = frozen[(definition.Id, level)];
                        var actual = new CharacterInstanceFactory().Create(definition, definition.ClassData, level);
                        var minimum = baseline[2]; var maximum = baseline[3];
                        Assert.That(actual.Stats.MinAttack.BaseValue, Is.EqualTo(minimum).Within(.001f), definition.Id + " L" + level);
                        Assert.That(actual.Stats.MaxAttack.BaseValue, Is.EqualTo(maximum).Within(.001f));
                        Assert.That(actual.Stats.MinAttack.GetValue(), Is.EqualTo(minimum).Within(.001f));
                        Assert.That(actual.Stats.MaxAttack.GetValue(), Is.EqualTo(maximum).Within(.001f));
                        Assert.That(Mathf.RoundToInt(actual.Stats.MinAttack.GetValue()), Is.EqualTo(Mathf.RoundToInt(minimum)), "Damage minimum must retain its integer range.");
                        Assert.That(Mathf.RoundToInt(actual.Stats.MaxAttack.GetValue()), Is.EqualTo(Mathf.RoundToInt(maximum)), "Damage maximum must retain its integer range.");
                        Assert.That(actual.Stats.MaxHealth.GetValue(), Is.EqualTo(baseline[4]));
                        Assert.That(actual.Stats.PhysicalDefense.GetValue(), Is.EqualTo(baseline[5]));
                        Assert.That(actual.Stats.MagicalDefense.GetValue(), Is.EqualTo(baseline[6]));
                        Assert.That(actual.Stats.Speed.GetValue(), Is.EqualTo(baseline[7]));
                        Assert.That(actual.Stats.CritChance.GetValue(), Is.EqualTo(baseline[8]));
                        AddAttack(actual, StatModifierOperation.FlatBase, 10);
                        AddAttack(actual, StatModifierOperation.FlatAbsolute, 5);
                        new CharacterStatPipelineSystem().Refresh(actual);
                        Assert.That(actual.Stats.MinAttack.GetValue(), Is.EqualTo(minimum + 15).Within(.001f));
                        Assert.That(actual.Stats.MaxAttack.GetValue(), Is.EqualTo(maximum + 15).Within(.001f));
                        AddAttack(actual, StatModifierOperation.Percent, .25f);
                        new CharacterStatPipelineSystem().Refresh(actual);
                        Assert.That(actual.Stats.MinAttack.GetValue(), Is.EqualTo((minimum + 10) * 1.25f + 5).Within(.001f));
                        Assert.That(actual.Stats.MaxAttack.GetValue(), Is.EqualTo((maximum + 10) * 1.25f + 5).Within(.001f));
                        var expected = actual.Stats.MinAttack.GetValue();
                        new CharacterLevelGrowthSystem().Apply(actual, false); new CharacterStatPipelineSystem().Refresh(actual);
                        Assert.That(actual.Stats.MinAttack.GetValue(), Is.EqualTo(expected), "Refresh/growth must not compound the correction.");
                        var json = JsonUtility.ToJson(actual);
                        Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(json));
                        count++;
                    }
            }
            Assert.That(count, Is.EqualTo(1150));
            var folder = Path.GetFullPath("../LocalHandoffs/root-repairs-20261029"); Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "profiles-after.tsv"), ProfileRows());
        }

        [TestCase(1, 85f)] [TestCase(10, 85f)] [TestCase(48, 85f)] [TestCase(50, 85f)]
        [TestCase(1, 100f)] [TestCase(1, 0f)]
        public void GrowthBoundsAndMaximumModifiersRespectTheBaseStage(int level, float baseAttack)
        {
            WithDefinition(baseAttack, (definition, cls) => {
                var character = new CharacterInstanceFactory().Create(definition, cls, level);
                Assert.That(character.Stats.MinAttack.BaseValue, Is.EqualTo(baseAttack));
                AddAttack(character, StatModifierOperation.FlatAbsolute, 10);
                AddAttack(character, StatModifierOperation.Percent, .2f, StatModifierStacking.Multiplicative);
                AddAttack(character, StatModifierOperation.Maximum, 90);
                new CharacterStatPipelineSystem().Refresh(character);
                Assert.That(character.Stats.MinAttack.GetValue(), Is.EqualTo(Mathf.Min(90, baseAttack * 1.2f + 10)).Within(.0001f));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void SnapshotRestoreRebuildsTheBaseOnceAndPreservesFullFlatEffects(bool includeEffect)
        {
            WithDefinition(85f, (definition, cls) => {
                var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
                try
                {
                    catalog.CharacterDefinitions.Add(definition); catalog.CharacterClasses.Add(cls);
                    var character = new CharacterInstanceFactory().Create(definition, cls, 17);
                    if (includeEffect) AddAttack(character, StatModifierOperation.FlatBase, 10);
                    new CharacterStatPipelineSystem().Refresh(character); character.CurrentHealth = 1;
                    var session = new GameSession(GameModeState.Exploration, "attack.restore");
                    Assert.That(session.TryRegisterCharacter(character), Is.True);
                    Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
                    var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "attack");
                    Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, catalog, out var restored, out var failure), Is.True, failure?.ToString());
                    var loaded = restored.CharactersById[character.CharacterId];
                    Assert.That(loaded.Stats.MinAttack.BaseValue, Is.EqualTo(85));
                    Assert.That(loaded.Stats.MinAttack.GetValue(), Is.EqualTo(includeEffect ? 95 : 85));
                    Assert.That(loaded.CurrentHealth, Is.EqualTo(1));
                    new CharacterLevelGrowthSystem().Apply(loaded, false);
                    Assert.That(loaded.Stats.MinAttack.GetValue(), Is.EqualTo(includeEffect ? 95 : 85));
                }
                finally { UnityEngine.Object.DestroyImmediate(catalog); }
            });
        }

        [Test]
        public void DerivedFlatAttackFromArmorIsNotScaledByTheBaseCorrection()
        {
            WithDefinition(85f, (definition, cls) => {
                var character = new CharacterInstanceFactory().Create(definition, cls);
                character.Stats.PhysicalDefense.BaseValue = 100;
                character.SkillTreeProgress.Add(new SkillTreeProgress { TreeId = WarriorCombatSystem.Brawler,
                    NodeInvestments = new() { new SkillTreeNodeInvestment { NodeId = "node.warrior.brawler.spiked_armor", Ranks = 1 } } });
                new CharacterStatPipelineSystem().Refresh(character);
                Assert.That(character.Stats.MinAttack.GetValue(), Is.EqualTo(95));
                Assert.That(character.Stats.MaxAttack.GetValue(), Is.EqualTo(180));
            });
        }

        [Test]
        public void ProductionCasterStatSummonKeepsCopiedAttackAcrossRefresh()
        {
            var definition = Profiles().First(d => d.StartingSkills.Any(skill => skill != null
                && skill.Effects.Any(effect => effect.SummonUsesCasterStats)));
            var skill = definition.StartingSkills.First(skill => skill != null && skill.Effects.Any(effect => effect.SummonUsesCasterStats));
            var effect = skill.Effects.First(effect => effect.SummonUsesCasterStats);
            var character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 1);
            AddAttack(character, StatModifierOperation.FlatBase, 10); new CharacterStatPipelineSystem().Refresh(character);
            var caster = new BattleUnit { Character = character, Position = new GridPosition(2, 2) };
            var session = new CombatSession { Grid = new BattleGrid(6, 6), ActiveUnit = caster, Units = new() { caster } };
            Assert.That(new SummonSystem().TrySummon(session, caster, skill, effect, 0, new GridPosition(3, 2), out var summoned), Is.True);
            var expected = caster.Character.Stats.MinAttack.GetValue();
            Assert.That(summoned.Character.Stats.MinAttack.GetValue(), Is.EqualTo(expected));
            new CharacterStatPipelineSystem().Refresh(summoned.Character);
            Assert.That(summoned.Character.Stats.MinAttack.GetValue(), Is.EqualTo(expected));
            AddAttack(summoned.Character, StatModifierOperation.FlatBase, 5); new CharacterStatPipelineSystem().Refresh(summoned.Character);
            Assert.That(summoned.Character.Stats.MinAttack.GetValue(), Is.EqualTo(expected + 5));
        }

        private static void AddAttack(CharacterInstance character, StatModifierOperation operation, float value,
            StatModifierStacking stacking = StatModifierStacking.Additive)
        {
            character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = "attack.test." + character.PassiveEffects.Count,
                Lifetime = PassiveEffectLifetime.Persistent, StatModifiers = new() {
                    new StatModifier { StatType = CharacterStatType.MinAttack, Operation = operation, Value = value, Stacking = stacking },
                    new StatModifier { StatType = CharacterStatType.MaxAttack, Operation = operation, Value = value, Stacking = stacking } } });
        }

        private static void WithDefinition(float baseAttack, Action<CharacterDefinition, CharacterClassData> body)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); var cls = ScriptableObject.CreateInstance<CharacterClassData>();
            try
            {
                var data = new SerializedObject(definition); data.FindProperty("<Id>k__BackingField").stringValue = "attack.fixture";
                var basis = data.FindProperty("<BaseStats>k__BackingField");
                basis.FindPropertyRelative("<MinAttack>k__BackingField").floatValue = baseAttack;
                basis.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue = baseAttack * 2;
                data.ApplyModifiedPropertiesWithoutUndo();
                data = new SerializedObject(cls); data.FindProperty("<Id>k__BackingField").stringValue = "attack.class"; data.ApplyModifiedPropertiesWithoutUndo();
                body(definition, cls);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(cls); }
        }

        private static IEnumerable<CharacterDefinition> Profiles() => AssetDatabase.FindAssets("t:CharacterDefinition", new[] { "Assets/_Project/ScriptableObjects" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterDefinition>)
            .Where(d => d != null && d.Id.StartsWith("enemy.", StringComparison.Ordinal) && d.name != "CD_Enemy_Test" && !d.Id.Contains(".test")).OrderBy(d => d.Id);

        private static IEnumerable<string> ProfileRows()
        {
            var profiles = Profiles().ToArray(); Assert.That(profiles.Length, Is.EqualTo(23));
            foreach (var definition in profiles)
                for (var level = 1; level <= 50; level++)
                {
                    var c = new CharacterInstanceFactory().Create(definition, definition.ClassData, level);
                    yield return string.Join("\t", new[] { definition.Id, level.ToString() }.Concat(new[] {
                        c.Stats.MinAttack.BaseValue, c.Stats.MaxAttack.BaseValue,
                        c.Stats.MinAttack.GetValue(), c.Stats.MaxAttack.GetValue(), c.Stats.MaxHealth.GetValue(),
                        c.Stats.PhysicalDefense.GetValue(), c.Stats.MagicalDefense.GetValue(), c.Stats.Speed.GetValue(), c.Stats.CritChance.GetValue()
                    }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                }
        }
    }
}
