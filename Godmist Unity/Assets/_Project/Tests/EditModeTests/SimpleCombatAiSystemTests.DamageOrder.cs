using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        private static IEnumerable<TestCaseData> ApprovedDamageOrderCases()
        {
            foreach (var defense in new[] { 0, 100 })
            foreach (var critical in new[] { false, true })
            foreach (var freezeTurns in new[] { 0, 1, 2, 3 })
                yield return new TestCaseData(defense, critical, freezeTurns);
        }

        [TestCaseSource(nameof(ApprovedDamageOrderCases))]
        public void RuntimeAndAiUseApprovedCriticalAndFreezeOrder(int defense, bool critical, int freezeTurns)
        {
            var skill = PlannerAttack("damage-order", 10, 1); var session = PlannerSession(1, skill);
            var source = session.ActiveUnit; var target = session.Units[1];
            source.Character.Stats.MinAttack = new Stat(100); source.Character.Stats.MaxAttack = new Stat(100);
            source.Character.Stats.CritChance = new Stat(critical ? 1 : 0); source.Character.Stats.CritDamage = new Stat(1.5f);
            SetAutoProperty(skill, "GuaranteedCritical", critical);
            target.Character.Stats.PhysicalDefense = new Stat(defense);
            if (freezeTurns > 0) target.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = StatusEffectIds.Freeze,
                Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = freezeTurns });
            var expected = Mathf.CeilToInt((defense == 0 ? 100 : 50) * (critical ? 1.5f : 1f) * (1 + .5f * freezeTurns));
            var rng = UnityEngine.Random.state;
            try
            {
                var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(source, target, skill);
                Assert.That(preview.Damage.Minimum, Is.EqualTo(expected)); Assert.That(preview.Damage.Maximum, Is.EqualTo(expected));
                Assert.That(UnityEngine.Random.state, Is.EqualTo(rng));
                var result = new CombatDamageSystem().ResolveAndApplyDamage(session, source, target, 100, DamageType.Physical, true);
                Assert.That(result.HealthDamage, Is.EqualTo(expected));
                Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Freeze), Is.False);
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(0, false)][TestCase(1, false)][TestCase(4, false)]
        [TestCase(0, true)][TestCase(1, true)][TestCase(4, true)]
        public void BiomeBonusAppliesAfterDefenseInPreviewAndExecution(int stones, bool matchingBiome)
        {
            var skill = PlannerAttack("biome-order", 10, 1); var session = PlannerSession(1, skill);
            foreach (var unit in session.Units) unit.WarriorState.Session = session;
            session.DungeonTheme = matchingBiome ? DungeonTheme.Forest : DungeonTheme.Catacombs;
            var source = session.ActiveUnit; var target = session.Units[1];
            source.Character.Stats.MinAttack = new Stat(100); source.Character.Stats.MaxAttack = new Stat(100);
            source.Character.Stats.CritChance = new Stat(0); target.Character.Stats.PhysicalDefense = new Stat(100);
            source.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = "test.authored-gem-domain-modifier",
                VariableModifiers = new List<GameplayVariableModifier> { new() { VariableType = GameplayVariableType.DamageDealtPercentInDungeonTheme,
                    ReferenceId = DungeonTheme.Forest.ToString(), Value = .035f * stones } } });
            var expected = Mathf.RoundToInt(50 * (matchingBiome ? 1 + .035f * stones : 1));
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(source, target, skill).Damage.Minimum, Is.EqualTo(expected));
            var rng = UnityEngine.Random.state;
            try { Assert.That(new CombatDamageSystem().ResolveAndApplyDamage(session, source, target, 100, DamageType.Physical, true).HealthDamage, Is.EqualTo(expected)); }
            finally { UnityEngine.Random.state = rng; }
        }

        [Test]
        public void CriticalFreezeReachesShieldAndRedirectAfterTheApprovedDefenseStage()
        {
            var session = PlannerSession(1); var source = session.ActiveUnit; var target = session.Units[1];
            source.Character.Stats.CritChance = new Stat(1); source.Character.Stats.CritDamage = new Stat(1.5f);
            target.Character.CharacterId = "patient"; target.Character.Stats.PhysicalDefense = new Stat(100);
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = StatusEffectIds.Freeze, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 1 });
            target.Character.ShieldPool = new ShieldPool(); target.Character.ShieldPool.AddLayer(13);
            var protector = CreateAiCombatUnit("protector", target.IsPlayerSide, new GridPosition(2, 0));
            protector.Character.CharacterId = "protector"; protector.Character.Stats.MaxHealth = new Stat(1000); protector.Character.CurrentHealth = 1000;
            session.Units.Add(protector); target.DamageRedirectProtectorCharacterId = "protector"; target.DamageRedirectPercent = .4f;
            var rng = UnityEngine.Random.state;
            try
            {
                new CombatDamageSystem().ResolveAndApplyDamage(session, source, target, 100, DamageType.Physical, true);
                Assert.That(target.Character.CurrentHealth + protector.Character.CurrentHealth + target.Character.ShieldPool.TotalShield,
                    Is.EqualTo(2000 + 13 - 113), "113 is split across protector, shield and HP; neither armor nor crit is applied again.");
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [Test]
        public void LifestealUsesHealthDamageAfterPostDefenseCriticalAndShield()
        {
            var skill = PlannerAttack("critical-lifesteal", 10, 1); var session = PlannerSession(1, skill);
            var source = session.ActiveUnit; var target = session.Units[1];
            source.Character.Stats.MinAttack = new Stat(100); source.Character.Stats.MaxAttack = new Stat(100);
            source.Character.Stats.MaxHealth = new Stat(1000); source.Character.CurrentHealth = 500;
            source.Character.Stats.CritChance = new Stat(1); source.Character.Stats.CritDamage = new Stat(1.5f);
            target.Character.Stats.PhysicalDefense = new Stat(100); target.Character.ShieldPool = new ShieldPool(); target.Character.ShieldPool.AddLayer(10);
            SetField(skill.Effects[0], "lifeStealPercent", .5f);
            var rng = UnityEngine.Random.state;
            try
            {
                Assert.That(new SkillExecutionSystem().TryExecute(session, source, skill, target), Is.True);
                Assert.That(target.Character.CurrentHealth, Is.EqualTo(935));
                Assert.That(source.Character.CurrentHealth, Is.EqualTo(532));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(.7f, 0, 0)][TestCase(.7f, 1, 0)][TestCase(.7f, 2, 0)]
        [TestCase(.8f, 0, 0)][TestCase(.8f, 1, 0)][TestCase(.8f, 2, 0)]
        [TestCase(.7f, 0, 100)][TestCase(.7f, 1, 100)][TestCase(.7f, 2, 100)]
        [TestCase(.8f, 0, 100)][TestCase(.8f, 1, 100)][TestCase(.8f, 2, 100)]
        public void EnergySplitTalentAddsToTheAuthoredAttackCoefficient(float authored, int rank, int defense)
        {
            var skill = PlannerAttack("split-coefficient", 10, authored); SetAutoProperty(skill, "SkillId", "skill.mage.energy_split");
            SetField(skill.Effects[0], "damageType", DamageType.Magical);
            var session = PlannerSession(1, skill); var source = session.ActiveUnit; var target = session.Units[1];
            source.Character.Stats.MinAttack = new Stat(100); source.Character.Stats.MaxAttack = new Stat(100);
            source.Character.Stats.CritChance = new Stat(0); target.Character.Stats.MagicalDefense = new Stat(defense);
            source.Character.SkillTreeProgress.Add(new SkillTreeProgress { TreeId = MageCombatSystem.Warlock,
                NodeInvestments = new() { new() { NodeId = "node.mage.warlock.power_saving", Ranks = rank } } });
            var resolved = PassiveEffectCombatExecutionSystem.ResolveEffect(source, skill, skill.Effects[0]);
            Assert.That(resolved.DamageMultiplier, Is.EqualTo(authored + .15f * rank).Within(.00001));
            Assert.That(skill.Effects[0].DamageMultiplier, Is.EqualTo(authored), "Authoring data is never mutated by talent resolution.");
            var raw = Mathf.RoundToInt(Mathf.RoundToInt(100 * (authored + .15f * rank)) * (1 + .01f * rank));
            var expected = defense == 0 ? raw : Mathf.CeilToInt((float)raw * raw / (raw + defense));
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(source, target, skill).Damage.Minimum, Is.EqualTo(expected));
            var rng = UnityEngine.Random.state;
            try
            {
                Assert.That(new SkillExecutionSystem().TryExecute(session, source, skill, target), Is.True);
                Assert.That(target.Character.CurrentHealth, Is.EqualTo(1000 - expected));
            }
            finally { UnityEngine.Random.state = rng; }
        }
    }
}
