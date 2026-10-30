using System;
using System.Reflection;
using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class MaximumHealthRoundingTests
    {
        [TestCase(100.49f, 100)] [TestCase(100.5f, 100)] [TestCase(100.51f, 101)]
        [TestCase(101.49f, 101)] [TestCase(101.5f, 102)] [TestCase(101.51f, 102)]
        public void FullHealingAndAllHealthTextAgree(float rawMaximum, int expected)
        {
            var character = Character(rawMaximum); var unit = new BattleUnit { Character = character };
            Assert.That(new CombatRecoverySystem().Heal(unit, 10000), Is.EqualTo(expected - 1));
            Assert.That(character.CurrentHealth, Is.EqualTo(expected));
            Assert.That(CombatHealthPresentation.Format(character), Is.EqualTo($"{expected}/{expected}"));
            Assert.That(CombatHealthPresentation.Format(expected, rawMaximum, 0), Is.EqualTo($"{expected}/{expected}"));
            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(rawMaximum));
        }

        [TestCase(100.49f, 100)] [TestCase(100.5f, 100)] [TestCase(100.51f, 101)]
        [TestCase(101.49f, 101)] [TestCase(101.5f, 102)] [TestCase(101.51f, 102)]
        public void FullHealthAlsoFillsTheActualSlider(float rawMaximum, int expected)
        {
            var root = new GameObject("Fractional HP test", typeof(RectTransform), typeof(CombatVitalsView));
            var sliderRoot = new GameObject("Health", typeof(RectTransform), typeof(Slider)); sliderRoot.transform.SetParent(root.transform, false);
            try
            {
                var view = root.GetComponent<CombatVitalsView>(); var slider = sliderRoot.GetComponent<Slider>();
                typeof(CombatVitalsView).GetField("healthSlider", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, slider);
                var character = Character(rawMaximum); character.CurrentHealth = expected;
                view.Bind(new BattleUnit { Character = character });
                Assert.That(slider.maxValue, Is.EqualTo(expected));
                Assert.That(slider.normalizedValue, Is.EqualTo(1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(100.49f, 100)] [TestCase(100.5f, 100)] [TestCase(100.51f, 101)]
        [TestCase(101.49f, 101)] [TestCase(101.5f, 102)] [TestCase(101.51f, 102)]
        public void HealingPreviewHasNoPhantomMissingHealthAtFullHp(float maximum, int expected)
        {
            var character = Character(maximum); character.CurrentHealth = expected;
            var unit = new BattleUnit { Character = character }; var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                typeof(SkillData).GetField("<SkillId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, "test.heal");
                var effect = new SkillEffectData();
                typeof(SkillEffectData).GetField("effectType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, EffectType.Heal);
                typeof(SkillEffectData).GetField("baseValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, 10000);
                typeof(SkillData).GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, new List<SkillEffectData> { effect });
                Assert.That(CombatSkillPreviewCalculator.CalculateHealing(unit, unit, skill), Is.Zero);
                Assert.That(new CombatRecoverySystem().Heal(unit, 10000), Is.Zero);
                var missing = typeof(SimpleCombatAiSystem).GetMethod("GetMissingHealth", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(missing.Invoke(null, new object[] { unit }), Is.EqualTo(0f));
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }

        [TestCase(100.49f, 100)] [TestCase(100.5f, 100)] [TestCase(101.5f, 102)]
        public void RemovingHealthEquipmentClampsThePoolWithoutChangingItsBase(float maximum, int expected)
        {
            var character = Character(maximum); var pipeline = new CharacterStatPipelineSystem();
            character.Equipment.Weapon = new EquipmentItemInstance { StatModifiers = new() {
                new StatModifier { StatType = CharacterStatType.MaxHealth, Operation = StatModifierOperation.FlatAbsolute, Value = 20 } } };
            pipeline.Refresh(character); new CombatRecoverySystem().Heal(new BattleUnit { Character = character }, 10000);
            Assert.That(character.CurrentHealth, Is.EqualTo(expected + 20));
            character.Equipment.Weapon = null; pipeline.Refresh(character);
            Assert.That(character.MaximumHealth, Is.EqualTo(expected)); Assert.That(character.CurrentHealth, Is.EqualTo(expected));
            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(maximum));
            character.CurrentHealth = 30;
            character.Equipment.Weapon = new EquipmentItemInstance { StatModifiers = new() {
                new StatModifier { StatType = CharacterStatType.MaxHealth, Operation = StatModifierOperation.FlatAbsolute, Value = 20 } } };
            pipeline.Refresh(character); Assert.That(character.CurrentHealth, Is.EqualTo(30), "Equipping HP does not heal.");
        }

        [Test] public void MaximumIsDerivedWithoutCachingOrSerializedDuplication()
        {
            var character = Character(100.5f); Assert.That(character.MaximumHealth, Is.EqualTo(100));
            character.Stats.MaxHealth.SetEffectiveValue(101.5f); Assert.That(character.MaximumHealth, Is.EqualTo(102));
            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(100.5f));
            Assert.That(JsonUtility.ToJson(character), Does.Not.Contain("MaximumHealth"));
        }

        [TestCase(25, true)] [TestCase(26, false)] [TestCase(50, false)] [TestCase(100, false)]
        public void PortraitLowHealthThresholdStillUsesFloatingPointDivision(int current, bool low)
        {
            var character = Character(100.5f); character.CurrentHealth = current;
            var body = typeof(Godmist.Views.UI.Workspace.CharacterInfoPartyPortraitView)
                .GetMethod("BuildPortraitTooltipBody", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { character }) as string;
            Assert.That(body.Contains("Niskie zdrowie"), Is.EqualTo(low));
        }

        private static CharacterInstance Character(float maximum)
        {
            var stats = new CharacterStats();
            foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) stats.GetStat(type) = new Stat(0);
            stats.MaxHealth = new Stat(maximum);
            return new CharacterInstance { CharacterId = "rounding", Stats = stats, CurrentHealth = 1, LifeState = CharacterLifeState.Alive };
        }
    }
}
