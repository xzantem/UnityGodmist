using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class HeroCombatStatusPresentationTests
    {
        static BattleUnit Unit(string id = "hero") => new()
        {
            IsPlayerSide = true, Character = new CharacterInstance { CharacterId = id, LifeState = CharacterLifeState.Alive }
        };
        static PassiveEffectRuntime Effect(string id, BattleUnit source = null) => new()
        {
            EffectId = id, DisplayName = id, RemainingDuration = 3, Lifetime = PassiveEffectLifetime.Timed,
            Sources = source == null ? new() : new() { PassiveEffectSourceFactory.FromRuntimeSystem(source, id) }
        };
        static void Rank(BattleUnit unit, string tree, string node, int rank)
        {
            var progress = unit.Character.SkillTreeProgress.FirstOrDefault(item => item.TreeId == tree);
            if (progress == null) { progress = new() { TreeId = tree }; unit.Character.SkillTreeProgress.Add(progress); }
            progress.NodeInvestments.Add(new() { NodeId = "node." + tree.Substring("tree.".Length) + "." + node, Ranks = rank });
        }

        [TestCase("warrior.amok", "Amok")]
        [TestCase("scout.knee_throw", "Rzut w Kolano")]
        [TestCase("paladin.universal", "Wszechstronna Łaska")]
        [TestCase("paladin.zeal.base", "Ożywcza Łaska")]
        [TestCase("paladin.zeal.extra", "Ożywcza Łaska")]
        [TestCase("paladin.torment", "Udręczenie")]
        [TestCase("paladin.exposure", "Odsłonięcie Słabości")]
        [TestCase("warrior.strategy", "Marsz Taktyczny")]
        [TestCase("mage.sacrifice.loss.ally", "Przejęcie Mocy")]
        [TestCase("mage.sacrifice.gain.ally", "Przejęcie Mocy")]
        [TestCase("mage.erosion.ally", "Wypalanie Odporności")]
        [TestCase("map-object.123.paladin.slow", "Pole Osądu")]
        public void LegacySaveLabelsAreCorrectedWithoutMutatingTheEffect(string id, string expected)
        {
            var saved = Effect(id); saved.DisplayName = "Stara nazwa";
            var json = JsonUtility.ToJson(saved);
            var restored = JsonUtility.FromJson<PassiveEffectRuntime>(json);
            var presentation = CombatStatusPresentationFactory.Create(new[] { restored }).Single();
            Assert.That(presentation.DisplayName, Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(json));
        }

        [TestCase(GameplayVariableType.DamageReceivedPercent, -.2f, CombatStatusKind.Buff)]
        [TestCase(GameplayVariableType.DamageReceivedPercent, .2f, CombatStatusKind.Debuff)]
        [TestCase(GameplayVariableType.DamageDealtPercent, .2f, CombatStatusKind.Buff)]
        [TestCase(GameplayVariableType.DamageDealtPercent, -.2f, CombatStatusKind.Debuff)]
        public void ClassificationUsesTheMeaningOfTheModifier(GameplayVariableType type, float value, CombatStatusKind expected)
        {
            var effect = Effect("modifier");
            effect.VariableModifiers = new() { new() { VariableType = type, Value = value } };
            Assert.That(CombatStatusPresentationFactory.Create(new[] { effect }).Single().Kind, Is.EqualTo(expected));
        }

        [Test]
        public void DeathWishRemainsOneVoluntaryBuffDespiteItsDamageReceivedPenalty()
        {
            var penalty = Effect("skill.warrior.death_wish.modify_var.1");
            penalty.Sources.Add(new() { SourceId = "skill.warrior.death_wish.effect.1", SourceType = PassiveEffectSourceType.Skill });
            penalty.VariableModifiers = new() { new() { VariableType = GameplayVariableType.DamageReceivedPercent, Value = .75f } };
            Assert.That(CombatStatusPresentationFactory.Create(new[] { penalty }).Single().Kind, Is.EqualTo(CombatStatusKind.Buff));
        }

        [TestCase(UniqueBuffType.GuaranteedCriticalHit)]
        [TestCase(UniqueBuffType.GuaranteedDodge)]
        [TestCase(UniqueBuffType.MaximumDamage)]
        public void OneUseBuffChargesHaveTheSameDescriptionInBothTooltipViews(UniqueBuffType type)
        {
            var effect = Effect("skill.test.unique.0"); effect.SetPayload(new UniqueCombatBuffPayload { BuffType = type });
            var presentation = CombatStatusPresentationFactory.Create(new[] { effect }).Single();
            var root = new GameObject("one-use tooltip");
            try
            {
                var text = root.AddComponent<UIWorkspaceTooltipView>().BuildPresentedStatusPayload(presentation);
                var legacy = (string)typeof(CombatTooltipView).GetMethod("BuildPresentedStatusPayload",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { presentation });
                Assert.That(text, Does.Contain("Ładunki: 1")); Assert.That(legacy, Does.Contain("Ładunki: 1"));
                Assert.That(text.Split(new[] { "Ładunki: 1" }, StringSplitOptions.None).Length, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void BlessingIsBeneficialAndExposureIsHarmfulDespiteItsStateOnlyPayload()
        {
            var blessing = Effect("paladin.blessing");
            blessing.VariableModifiers = new() {
                new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .2f },
                new() { VariableType = GameplayVariableType.DamageReceivedPercent, Value = -.2f }
            };
            var exposure = Effect("paladin.exposure");
            exposure.SetStateValue(PassiveEffectRuntimeStateValue.Number("strength", .5f));
            var entries = CombatStatusPresentationFactory.Create(new[] { blessing, exposure });
            Assert.That(entries.Single(item => item.Effect == blessing).Kind, Is.EqualTo(CombatStatusKind.Buff));
            Assert.That(entries.Single(item => item.Effect == exposure).Kind, Is.EqualTo(CombatStatusKind.Debuff));
            Assert.That(HeroCombatStatusText.Describe(exposure, Unit()), Does.Contain("+50%").And.Contain("Ładunki: 1"));
        }

        [Test]
        public void WeakPointAndAnalysisUseAppliedValuesRatherThanTheCurrentTalentRank()
        {
            var unit = Unit(); unit.ScoutState.WeakPointTurns = 4; unit.ScoutState.WeakPointCritDamage = .35f;
            unit.ScoutState.AnalysisTurns = 2; unit.ScoutState.AnalysisPenetration = .3f;
            var entries = CombatStatusPresentationFactory.Create(null, owner: unit);
            var weak = entries.Single(item => item.Effect.EffectId == "scout.weak_point");
            Assert.That(HeroCombatStatusText.Describe(weak.Effect, unit), Does.Contain("+10 pp").And.Contain("+35 pp"));
            Assert.That(weak.Duration, Is.EqualTo(4));
            var analysis = entries.Single(item => item.Effect.EffectId == "scout.analysis");
            Assert.That(HeroCombatStatusText.Describe(analysis.Effect, unit), Does.Contain("+30%"));
            Assert.That(analysis.Duration, Is.EqualTo(2));
            Assert.That(unit.ScoutState.WeakPointCritDamage, Is.EqualTo(.35f));
        }

        [Test]
        public void AngerUsesStrongInsultAndTheSameMaximumRuleAsCombat()
        {
            var first = Unit("first"); var second = Unit("second"); var target = Unit("enemy"); target.IsPlayerSide = false;
            Rank(first, ScoutCombatSystem.BladeTree, "strong_insult", 2);
            target.ScoutState.AngerBySource[first] = 80; target.ScoutState.AngerBySource[second] = 100;
            var expected = HeroCombatStatusText.Percent(ScoutCombatSystem.ReceivedDamageMultiplier(first, target) - 1);
            Assert.That(HeroCombatStatusText.Describe(Effect("scout.anger"), target), Does.Contain(expected).And.Contain("20%"));
            Assert.That(target.ScoutState.AngerBySource[first], Is.EqualTo(80));
        }

        [Test]
        public void RegenerationReportsItsRemainingTicksAndTalentTailFromRestoredState()
        {
            var regen = Effect("paladin.holy_regeneration");
            regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", 101));
            regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("total", 400));
            regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("lasting", 2));
            var saved = JsonUtility.ToJson(regen);
            var restored = JsonUtility.FromJson<PassiveEffectRuntime>(saved);
            var body = HeroCombatStatusText.Describe(restored, Unit());
            Assert.That(body, Does.Contain("101 HP").And.Contain("34 HP").And.Contain("100 HP przez 2 tury"));
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(saved));
            restored.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("tail", true));
            Assert.That(HeroCombatStatusText.Describe(restored, Unit()), Does.Not.Contain("następnie"));
        }

        [Test]
        public void BlessingShowsActualGraceAndSourceTalentsWithoutAssumingTheRecipientIsTheCaster()
        {
            var caster = Unit("caster"); var target = Unit("recipient");
            Rank(caster, PaladinCombatSystem.Priest, "grace_collection", 2);
            Rank(caster, PaladinCombatSystem.Priest, "salvation", 1);
            var blessing = Effect("paladin.blessing", caster);
            blessing.SetStateValue(PassiveEffectRuntimeStateValue.Integer("grace", 5));
            var combat = new CombatSession { Units = new() { caster, target } };
            var body = HeroCombatStatusText.Describe(blessing, target, combat);
            Assert.That(body, Does.Contain("5/6").And.Contain("40 (8 za ładunek").And.Contain("50%"));
            Assert.That(HeroCombatStatusText.Describe(blessing, target), Does.Contain("5/6").And.Not.Contain("40 (8"));
        }

        [Test]
        public void OfferingUsesBothOfferingAndCalmTalents()
        {
            var caster = Unit(); Rank(caster, MageCombatSystem.Warlock, "superhuman_offering", 3);
            Rank(caster, MageCombatSystem.Warlock, "calm", 2);
            var offering = Effect("skill.mage.offering.unique.0");
            offering.SetPayload(new UniqueCombatBuffPayload { BuffType = UniqueBuffType.MageOffering });
            var body = HeroCombatStatusText.Describe(offering, caster);
            Assert.That(body, Does.Contain("Zadawane obrażenia: +50%").And.Contain("Otrzymywane obrażenia: +40%"));
        }

        [Test]
        public void OrdersReportTalentsCurrentNeighboursAndRemainingFreeSteps()
        {
            var caster = Unit("commander"); var target = Unit("enemy"); target.IsPlayerSide = false;
            var ally = Unit("ally");
            caster.Position = new GridPosition(1, 2); target.Position = new GridPosition(2, 2); ally.Position = new GridPosition(2, 3);
            Rank(caster, WarriorCombatSystem.Commander, "breakdown", 3);
            Rank(caster, WarriorCombatSystem.Commander, "marking", 2);
            Rank(caster, WarriorCombatSystem.Commander, "flanking", 2);
            var combat = new CombatSession { Grid = new BattleGrid(8, 8), Units = new() { caster, target, ally } };
            target.WarriorState.OrderSource = caster; target.WarriorState.OrderTurns = 3;
            var order = HeroCombatStatusText.Describe(Effect("warrior.order"), target, combat);
            Assert.That(order, Does.Contain("+35%").And.Contain("+100% wartości bazowej").And.Contain("-20%"));
            target.IsPlayerSide = true; target.WarriorState.ProtectionTurns = 3; target.WarriorState.Riposte = true;
            var protection = HeroCombatStatusText.Describe(Effect("warrior.protection"), target, combat);
            Assert.That(protection, Does.Contain("-50%").And.Contain("30%").And.Contain("+100%"));
            target.WarriorState.FreeMovementTurns = 2; target.WarriorState.FreeStepsUsed = 1;
            Assert.That(HeroCombatStatusText.Describe(Effect("warrior.strategy"), target), Does.Contain("1/2"));
            target.WarriorState.TraumaTurns = 4; target.WarriorState.TraumaBonus = .6f;
            Assert.That(HeroCombatStatusText.Describe(Effect("warrior.trauma"), target), Does.Contain("+60%"));
            Assert.That(target.WarriorState.FreeStepsUsed, Is.EqualTo(1));
        }

        [Test]
        public void AdaptationHasIndependentSourceCountersAndPresentationDoesNotConsumeHits()
        {
            var caster = Unit(); caster.MageState.Adaptation["first"] = new() { RemainingHits = 2, Reduction = 37 };
            caster.MageState.Adaptation["second"] = new() { RemainingHits = 1, Reduction = 12 };
            caster.MageState.Adaptation["expired"] = new() { RemainingHits = 0, Reduction = 99 };
            var entries = CombatStatusPresentationFactory.Create(null, owner: caster);
            Assert.That(entries.Count, Is.EqualTo(2));
            var entry = entries.Single(item => item.Effect.EffectId.EndsWith("first"));
            Assert.That(entry.StackCount, Is.EqualTo(2));
            Assert.That(HeroCombatStatusText.Describe(entry.Effect, caster), Does.Contain("37").And.Contain("2/3"));
            Assert.That(caster.MageState.Adaptation["first"].RemainingHits, Is.EqualTo(2));
        }

        [Test]
        public void FieldSlowDoesNotExposeTheTechnical9999TurnLifetime()
        {
            var slow = Effect("object.123.paladin.slow"); slow.RemainingDuration = 9999;
            var presentation = CombatStatusPresentationFactory.Create(new[] { slow }).Single();
            Assert.That(presentation.DisplayName, Is.EqualTo("Pole Osądu")); Assert.That(presentation.Duration, Is.Zero);
            Assert.That(HeroCombatStatusText.Describe(slow, Unit()), Does.Contain("w obszarze Pola Osądu").And.Not.Contain("9999"));
            Assert.That(slow.RemainingDuration, Is.EqualTo(9999));
        }

        [Test]
        public void ErosionShowsRealHitCountAndResistancePayloadWithoutChangingTheRuntimeStack()
        {
            var target = Unit(); target.MageState.FireErosion["caster"] = 4;
            var effect = Effect("mage.erosion.caster"); effect.Lifetime = PassiveEffectLifetime.CombatScoped; effect.RemainingDuration = 0;
            effect.SetPayload(new ResistanceModifierPayload { ResistanceType = ResistanceType.Fire, Value = -.4f });
            var entry = CombatStatusPresentationFactory.Create(new[] { effect }, owner: target).Single();
            Assert.That(entry.StackCount, Is.EqualTo(4)); Assert.That(effect.Stacks, Is.EqualTo(1));
            var root = new GameObject("erosion tooltip");
            try
            {
                var body = root.AddComponent<UIWorkspaceTooltipView>().BuildPresentedStatusPayload(entry);
                Assert.That(body, Does.Contain("4/5").And.Contain("40%"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ZealCombinesTwoIndependentDurationsWithoutChangingTheirPayloads()
        {
            var caster = Unit(); var baseline = Effect("paladin.zeal.base", caster); baseline.RemainingDuration = 2;
            var extra = Effect("paladin.zeal.extra", caster); extra.RemainingDuration = 1;
            foreach (var effect in new[] { baseline, extra })
                effect.StatModifiers = new() { new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.FlatAbsolute, Value = 5 } };
            var saved = JsonUtility.ToJson(baseline) + JsonUtility.ToJson(extra);
            var presentation = CombatStatusPresentationFactory.Create(new[] { baseline, extra }).Single();
            Assert.That(presentation.DisplayName, Is.EqualTo("Ożywcza Łaska")); Assert.That(presentation.HasMixedDurations, Is.True);
            var root = new GameObject("hero tooltip test");
            try
            {
                var body = root.AddComponent<UIWorkspaceTooltipView>().BuildPresentedStatusPayload(presentation);
                Assert.That(body, Does.Contain("Szybkość: +5").And.Contain("2 t.").And.Contain("1 t."));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Assert.That(JsonUtility.ToJson(baseline) + JsonUtility.ToJson(extra), Is.EqualTo(saved));
        }

        [TestCase("warrior.amok")]
        [TestCase("warrior.shortage")]
        [TestCase("scout.knee_throw")]
        [TestCase("paladin.universal")]
        [TestCase("paladin.zeal.base")]
        [TestCase("paladin.zeal.extra")]
        [TestCase("paladin.torment")]
        [TestCase("paladin.exposure")]
        [TestCase("paladin.anointed")]
        [TestCase("object.42.paladin.slow")]
        [TestCase("mage.sacrifice.loss.hero")]
        [TestCase("mage.sacrifice.gain.hero")]
        [TestCase("mage.adaptation.enemy")]
        [TestCase("mage.erosion.enemy")]
        public void ExistingProductionArtResolvesEvenOnAnEnemyOrOldSaveWithoutSkillSourceMetadata(string id)
        {
            var trees = AssetDatabase.FindAssets("t:SkillTreeDefinition", new[] { "Assets/_Project/ScriptableObjects/Skills/SkillTrees" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            var skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/ScriptableObjects/Skills" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            var presentation = CombatStatusPresentationFactory.Create(new[] { Effect(id) }).Single();
            var icon = CombatStatusPresentationFactory.ResolveIcon(presentation, _ => null, skills, trees);
            Assert.That(icon, Is.Not.Null, "Missing art for " + id);
            var source = HeroCombatStatusText.IconSource(id);
            var expected = source.StartsWith("node.") ? trees.SelectMany(tree => tree.Nodes).Single(node => node.Id == source && node.Icon != null).Icon
                : skills.Single(skill => skill.SkillId == source).Icon;
            Assert.That(icon, Is.EqualTo(expected));
        }
    }
}
