using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Runtime.Characters;
using Godmist.Core.Types;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class PotionPresentationTests
    {
        private ConsumableItemDefinition definition;
        private ConsumableItemInstance potion;
        [SetUp] public void Setup()
        {
            definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            typeof(ItemDefinition).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, "potion.presentation");
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, ConsumableUseType.Potion);
            potion = new ConsumableItemInstance(definition, "presentation");
        }
        [TearDown] public void Teardown() => Object.DestroyImmediate(definition);
        [Test] public void ExplorationPotionKeepsOneNamedBuffAfterFinalChargeIsConsumed()
        {
            potion.CustomName = "Eliksir Zwinności";
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ModifyDodge, Value = 10, DurationTurns = 8 },
                new() { EffectType = ConsumableRuntimeEffectType.ModifyCritChance, Value = .05f, DurationTurns = 8 }
            };
            var character = new CharacterInstance { CharacterId = "potion-owner", Stats = new CharacterStats() };
            character.Inventory.Items.Add(potion);
            Assert.That(new ConsumableService().TryUseInExploration(character, potion, out _), Is.True);
            var entries = Godmist.Views.UI.CombatStatusPresentationFactory.Create(character.PassiveEffects);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Effects, Has.Count.EqualTo(2));
            Assert.That(entries[0].Effect.DisplayName, Is.EqualTo("Eliksir Zwinności"));
            Assert.That(character.Inventory.Items, Has.No.Member(potion));
        }

        [Test] public void CombatPotionModifierSystemsProduceOneBuffForTwoEffects()
        {
            potion.CustomName = "Eliksir Zwinności";
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ModifyDodge, Value = 10, DurationTurns = 8 },
                new() { EffectType = ConsumableRuntimeEffectType.ModifyCritChance, Value = .05f, DurationTurns = 8 }
            };
            var unit = new Godmist.Runtime.Combat.BattleUnit
            { Character = new CharacterInstance { CharacterId = "caster", Stats = new CharacterStats() } };
            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            try
            {
                var system = new ModifyStatSystem();
                for (var i = 0; i < skill.Effects.Count; i++)
                    Assert.That(system.Apply(unit, unit, skill, skill.Effects[i], i), Is.True);
                var entries = Godmist.Views.UI.CombatStatusPresentationFactory.Create(unit.Character.PassiveEffects);
                Assert.That(entries, Has.Count.EqualTo(1)); Assert.That(entries[0].Effects, Has.Count.EqualTo(2));
                Assert.That(entries[0].Effect.DisplayName, Is.EqualTo("Eliksir Zwinności"));
            }
            finally { Object.DestroyImmediate(skill); }
        }
        [Test] public void RegenerationUsesAdjustedDurationAndPerTurnStrength()
        {
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RegenHealth, Value = .3f, DurationTurns = 10 });
            potion.CatalystEffectMultiplier = 1.5f; potion.CatalystDurationModifier = 2;
            var text = UIWorkspaceTooltipView.BuildPotionEffectSummary(potion);
            Assert.That(text, Does.Contain("3.75%").And.Contain("12 tur").And.Contain("maks. / turę"));
            Assert.That(text, Does.Not.Contain("45%"));
        }
        [Test] public void DefenseAndAllResistancesAreCollapsedWithoutLosingValues()
        {
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ModifyDefense, Value = .1f, DurationTurns = 8 },
                new() { EffectType = ConsumableRuntimeEffectType.ModifyAllResistances, Value = .2f, DurationTurns = 4 }
            };
            var text = UIWorkspaceTooltipView.BuildPotionEffectSummary(potion);
            Assert.That(text.Split('\n'), Has.Length.EqualTo(2));
            Assert.That(text, Does.Contain("Obrona fizyczna i magiczna: +10% • 8 tur")
                .And.Contain("Wszystkie odporności: +20% • 4 tur"));
        }
        [Test] public void FlatDodgeIsNotShownAsPercentAndCritRetainsItsUnit()
        {
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ModifyDodge, Value = 10, DurationTurns = 8 },
                new() { EffectType = ConsumableRuntimeEffectType.ModifyCritChance, Value = .05f, DurationTurns = 8 }
            };
            var text = UIWorkspaceTooltipView.BuildPotionEffectSummary(potion);
            Assert.That(text, Does.Contain("Unik: +10 • 8 tur").And.Contain("Szansa krytyczna: +5% • 8 tur"));
            Assert.That(text, Does.Not.Contain("1000%").And.Not.Contain("pkt."));
        }
        [Test] public void PotionUtilityUsesShortLocalizedStatusSummary()
        {
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RemoveStatus, StatusId = "poison" });
            Assert.That(UIWorkspaceTooltipView.BuildPotionEffectSummary(potion), Is.EqualTo("Usuwa: Zatrucie"));
        }

        [TestCase(ConsumableRuntimeEffectType.ModifyDamage, 8, -1, .1f, .1f * 8 / 7)]
        [TestCase(ConsumableRuntimeEffectType.ModifyDefense, 8, -1, .1f, .1f * 8 / 7)]
        [TestCase(ConsumableRuntimeEffectType.ModifyAllResistances, 8, -1, .1f, .1f * 8 / 7)]
        [TestCase(ConsumableRuntimeEffectType.ModifySpeed, 4, -1, .15f, .2f)]
        [TestCase(ConsumableRuntimeEffectType.ModifyDodge, 8, -1, 10f, 80f / 7)]
        [TestCase(ConsumableRuntimeEffectType.ModifyActionPoints, 8, -1, .1f, .1f * 8 / 7)]
        [TestCase(ConsumableRuntimeEffectType.ModifyDamage, 4, -10, .1f, .4f)]
        [TestCase(ConsumableRuntimeEffectType.ModifyDamage, 8, 2, .1f, .1f)]
        public void CatalystUsesActualDurationForEveryTimedModifier(
            ConsumableRuntimeEffectType type, int duration, int adjustment, float value, float expected)
        {
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = type, Value = value, DurationTurns = duration });
            potion.CatalystDurationModifier = adjustment;
            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            try
            {
                foreach (var effect in skill.Effects)
                {
                    Assert.That(effect.DurationTurns, Is.EqualTo(Mathf.Max(1, duration + adjustment)));
                    var actual = type == ConsumableRuntimeEffectType.ModifyActionPoints
                        ? effect.PercentageValue : effect.ModifierValue;
                    Assert.That(actual, Is.EqualTo(expected).Within(.00001f));
                }
            }
            finally { Object.DestroyImmediate(skill); }
        }

        [Test] public void CondensationPreviewMatchesPotencyAndKeepsInstantRecoveryUnchanged()
        {
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ModifyDamage, Value = .1f, DurationTurns = 8 },
                new() { EffectType = ConsumableRuntimeEffectType.RestoreHealth, Value = .15f },
                new() { EffectType = ConsumableRuntimeEffectType.RegenHealth, Value = .3f, DurationTurns = 10 }
            };
            potion.ApplyCatalyst(ConsumableCatalystType.Condensation, 1);
            var text = UIWorkspaceTooltipView.BuildPotionEffectSummary(potion);
            Assert.That(text, Does.Contain("Obrażenia: +11.43% • 7 tur").And.Contain("Przywraca zdrowie: 15% maks. HP")
                .And.Contain("3.33%").And.Contain("9 tur"));
            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            try
            {
                Assert.That(skill.Effects[1].PercentageValue, Is.EqualTo(.15f).Within(.00001f));
                Assert.That(skill.Effects[1].DurationTurns, Is.Zero);
                Assert.That(skill.Effects[2].PercentageValue * skill.Effects[2].DurationTurns,
                    Is.EqualTo(.3f).Within(.00001f));
            }
            finally { Object.DestroyImmediate(skill); }
        }

        [TestCase(1)]
        [TestCase(15)]
        [TestCase(50)]
        public void BombDamageSummaryUsesCombatLevelAndDuplicateEfficiency(int level)
        {
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, ConsumableUseType.Bomb);
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.DealPhysicalDamage, Value = .15f },
                new() { EffectType = ConsumableRuntimeEffectType.DealPhysicalDamage, Value = .15f }
            };
            var character = new CharacterInstance { Level = level, Stats = new CharacterStats() };
            character.Stats.MinAttack = new Stat(1); character.Stats.MaxAttack = new Stat(2);
            var value = CombatDamageValueCalculator.CalculateAlchemicalDamage(level, .3f * ConsumableRuntimeRules.GetDuplicateMultiplier(2));
            var text = UIWorkspaceTooltipView.BuildBombEffectSummary(potion, character);
            Assert.That(text, Is.EqualTo($"Obrażenia fizyczne: {value}"));
            character.Stats.MinAttack = new Stat(10000); character.Stats.MaxAttack = new Stat(20000);
            Assert.That(UIWorkspaceTooltipView.BuildBombEffectSummary(potion, character), Is.EqualTo(text));
        }

        [Test] public void BombSummaryPreservesStatusChanceDebuffSignAndPerTurnDamage()
        {
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, ConsumableUseType.Bomb);
            potion.RuntimeEffects = new List<ConsumableEffectRuntime>
            {
                new() { EffectType = ConsumableRuntimeEffectType.ApplyStun, Value = .6f, DurationTurns = 2 },
                new() { EffectType = ConsumableRuntimeEffectType.ModifyPhysicalDefense, Value = .1f, DurationTurns = 3 },
                new() { EffectType = ConsumableRuntimeEffectType.ApplyBleed, Value = .1f, DurationTurns = 4 }
            };
            potion.CatalystEffectMultiplier = 1.5f; potion.CatalystDurationModifier = 1;
            var character = new CharacterInstance { Stats = new CharacterStats() };
            character.Stats.MinAttack = new Stat(100); character.Stats.MaxAttack = new Stat(100);
            var text = UIWorkspaceTooltipView.BuildBombEffectSummary(potion, character);
            Assert.That(text, Does.Contain("Ogłuszenie • 3 tury • Szansa: 90%")
                .And.Contain("Obrona fizyczna: -15% • 4 tur")
                // L1 alchemical power (28), not the carrier's 100 attack: ceil(28 * .1 * 1.5) = 5.
                .And.Contain("Krwawienie: 5 obrażeń / turę • 5 tur"));
        }

        [Test] public void BombUtilityUsesShortLocalizedStatusSummary()
        {
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RemoveStatus, StatusId = "poison" });
            Assert.That(UIWorkspaceTooltipView.BuildBombEffectSummary(potion), Is.EqualTo("Usuwa: Zatrucie"));
        }
    }
}
