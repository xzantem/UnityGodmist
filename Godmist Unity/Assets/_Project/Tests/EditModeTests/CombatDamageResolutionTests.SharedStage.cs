using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class CombatDamageResolutionTests
    {
        private static IEnumerable<TestCaseData> ResolutionContracts()
        {
            foreach (var type in new[] { DamageType.Physical, DamageType.Magical, DamageType.Inevitable,
                         DamageType.Direct, DamageType.Bleed, DamageType.Poison, DamageType.Burn })
            foreach (var scenario in new[] { "plain", "critical", "dodge", "freeze", "exposure", "shield",
                         "lethal", "ignore-defense", "penetration", "weakening", "paralyzed" })
                yield return new TestCaseData(type, scenario);
            yield return new TestCaseData(DamageType.Physical, "miss");
        }

        [Serializable]
        private sealed class ResolutionSnapshot
        {
            public bool hit, critical, criticalGuarantee, dodgeGuarantee;
            public int healthDamage, targetHealth, shield;
            public string life, effects;
            public string[] events, trace;
            public float[] followingRandom;
        }

        private static void ResolutionGuarantee(BattleUnit unit, UniqueBuffType type)
        {
            var runtime = new PassiveEffectRuntime { EffectId = "test.m02." + type, IsUniqueCombatBuff = true,
                UniqueBuffType = type, Stacks = 1 };
            Assert.That(new PassiveEffectLifecycleSystem().TryApplyCombatScopedEffect(unit.Character, runtime,
                PassiveEffectStackingMode.ReplaceDurationAndValues, out _), Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(unit, type), Is.True);
        }

        private ResolutionSnapshot ResolveContract(DamageType type, string scenario, bool suppliedHit)
        {
            var session = CreateSession(out var source, out var target);
            source.Character.Stats.MaxHealth = new Stat(100); target.Character.Stats.MaxHealth = new Stat(100);
            source.Character.CharacterId = "source"; target.Character.CharacterId = "target";
            source.Position = new GridPosition(0, 0);
            target.Position = new GridPosition(1, 0);
            source.Character.Stats.CritChance = new Stat(.37f);
            target.Character.Stats.PhysicalDefense = new Stat(45); target.Character.Stats.MagicalDefense = new Stat(75);
            foreach (var unit in session.Units) unit.WarriorState.Session = session;
            if (scenario == "critical") ResolutionGuarantee(source, UniqueBuffType.GuaranteedCriticalHit);
            if (scenario == "dodge") ResolutionGuarantee(target, UniqueBuffType.GuaranteedDodge);
            if (scenario == "freeze" || scenario == "paralyzed")
                target.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = scenario == "freeze" ? StatusEffectIds.Freeze : StatusEffectIds.Paralysis,
                    Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            if (scenario == "weakening") source.Character.PassiveEffects.Add(new PassiveEffectRuntime {
                EffectId = StatusEffectIds.Weakening, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            if (scenario == "exposure")
            {
                var exposure = new PassiveEffectRuntime { EffectId = "paladin.exposure" };
                exposure.SetStateValue(PassiveEffectRuntimeStateValue.Number("strength", .25f));
                target.Character.PassiveEffects.Add(exposure);
            }
            if (scenario == "shield") target.Character.ShieldPool.AddLayer(17);
            var trace = new List<string>(); var events = new List<string>();
            var bus = new CombatFeedbackBus(); bus.Published += message => trace.Add("feedback:" + message.Type);
            session.PassiveEffectEventDispatcher = new PassiveEffectCombatEventDispatcher();
            session.PassiveEffectEventDispatcher.Published += message =>
            {
                events.Add(message.TriggerType.ToString()); trace.Add("event:" + message.TriggerType);
            };
            var damage = new CombatDamageSystem(battleLog: message => trace.Add("log:" + message),
                feedbackBus: bus, evaluateOutcome: _ => trace.Add("outcome"), removeUnitFromCombat: _ => trace.Add("remove"));
            var accuracy = scenario == "miss" ? 0f : 100f;
            if (scenario == "miss") source.Character.WeaponAccuracy = 0;
            var attack = scenario == "lethal" ? 10000 : 80;
            UnityEngine.Random.InitState(6127);
            CombatDamageResult result;
            if (suppliedHit)
            {
                // The bool overload receives an already resolved roll. Account for exactly that
                // roll outside it, rather than expecting both APIs to draw a hit a second time.
                var hit = !StatusEffectSystem.IsParalyzed(target) && (CombatDamageSystem.IsGuaranteedHitDamageType(type)
                    || CombatDamageSystem.RollHit(source, target, accuracy));
                result = damage.ResolveAndApplyDamage(session, source, target, attack, type, hit,
                    bonusDefensePenetration: scenario == "penetration" ? .45f : 0f, ignoreDefense: scenario == "ignore-defense");
            }
            else result = damage.ResolveAndApplyDamage(session, source, target, attack, type, accuracy,
                bonusDefensePenetration: scenario == "penetration" ? .45f : 0f, ignoreDefense: scenario == "ignore-defense");
            return new ResolutionSnapshot { hit = result.Hit, critical = result.IsCritical, healthDamage = result.HealthDamage,
                targetHealth = target.Character.CurrentHealth, shield = target.Character.ShieldPool.TotalShield,
                criticalGuarantee = CombatDamageSystem.HasUniqueBuff(source, UniqueBuffType.GuaranteedCriticalHit),
                dodgeGuarantee = CombatDamageSystem.HasUniqueBuff(target, UniqueBuffType.GuaranteedDodge),
                life = target.Character.LifeState.ToString(), effects = string.Join(",", target.Character.PassiveEffects.Select(e => e.EffectId + ":" + e.Stacks + ":" + e.RemainingDuration)),
                events = events.ToArray(), trace = trace.ToArray(), followingRandom = Enumerable.Range(0, 8).Select(_ => UnityEngine.Random.value).ToArray() };
        }

        [TestCaseSource(nameof(ResolutionContracts))]
        public void DamageEntrypointsPreserveNumbersEventsGuaranteesAndRandomSequence(DamageType type, string scenario)
        {
            var rng = UnityEngine.Random.state;
            try
            {
                var rolled = ResolveContract(type, scenario, false); var supplied = ResolveContract(type, scenario, true);
                Assert.That(supplied.hit, Is.EqualTo(rolled.hit)); Assert.That(supplied.critical, Is.EqualTo(rolled.critical));
                Assert.That(supplied.healthDamage, Is.EqualTo(rolled.healthDamage)); Assert.That(supplied.targetHealth, Is.EqualTo(rolled.targetHealth));
                Assert.That(supplied.life, Is.EqualTo(rolled.life)); Assert.That(supplied.shield, Is.EqualTo(rolled.shield));
                Assert.That(supplied.criticalGuarantee, Is.EqualTo(rolled.criticalGuarantee)); Assert.That(supplied.dodgeGuarantee, Is.EqualTo(rolled.dodgeGuarantee));
                Assert.That(supplied.effects, Is.EqualTo(rolled.effects)); Assert.That(supplied.followingRandom, Is.EqualTo(rolled.followingRandom));
                if (scenario == "paralyzed")
                {
                    // Historical pre-hit API difference is outside M02's successful-hit stage.
                    Assert.That(rolled.events, Is.EqualTo(new[] { "OnAttack" })); Assert.That(supplied.events, Is.Empty);
                }
                else Assert.That(supplied.events, Is.EqualTo(rolled.events));
                if (rolled.hit) Assert.That(supplied.trace, Is.EqualTo(rolled.trace));
                var folder = Path.GetFullPath("../LocalHandoffs/damage-resolution-shared-stage-20261030/snapshots");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, type + "-" + scenario + "-rolled.json"), JsonUtility.ToJson(rolled, true));
                File.WriteAllText(Path.Combine(folder, type + "-" + scenario + "-supplied.json"), JsonUtility.ToJson(supplied, true));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(false)][TestCase(true)]
        public void SuccessfulContactPublishesOrderedEventsAndConsumesOneCriticalGuarantee(bool suppliedHit)
        {
            var rng = UnityEngine.Random.state;
            try
            {
                var snapshot = ResolveContract(DamageType.Physical, "critical", suppliedHit);
                Assert.That(snapshot.events, Is.EqualTo(new[] { "OnAttack", "OnHit", "OnCriticalHit", "OnDamageTaken" }));
                Assert.That(snapshot.critical, Is.True); Assert.That(snapshot.criticalGuarantee, Is.False);
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(false, false, false, 50)][TestCase(true, false, false, 50)]
        [TestCase(false, true, false, 75)][TestCase(true, true, false, 75)]
        [TestCase(false, false, true, 75)][TestCase(true, false, true, 75)]
        [TestCase(false, true, true, 113)][TestCase(true, true, true, 113)]
        public void SharedStageUsesApprovedPostDefenseArmorCriticalAndFreezeNumbers(bool suppliedHit, bool critical, bool freeze, int expected)
        {
            var rng = UnityEngine.Random.state;
            try
            {
                var session = CreateSession(out var source, out var target);
                source.Character.Stats.CritChance = new Stat(0); target.Character.Stats.PhysicalDefense = new Stat(100);
                target.Character.Stats.MaxHealth = new Stat(1000); target.Character.CurrentHealth = 1000;
                if (critical) ResolutionGuarantee(source, UniqueBuffType.GuaranteedCriticalHit);
                if (freeze) target.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = StatusEffectIds.Freeze,
                    Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 1 });
                UnityEngine.Random.InitState(6127);
                var system = new CombatDamageSystem();
                var result = suppliedHit ? system.ResolveAndApplyDamage(session, source, target, 100, DamageType.Physical, true)
                    : system.ResolveAndApplyDamage(session, source, target, 100, DamageType.Physical, 100f);
                Assert.That(result.HealthDamage, Is.EqualTo(expected), "A03: critical/freeze multiply after armor, with one final ceiling.");
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(true, "ordinary", 1)][TestCase(false, "ordinary", 2)]
        [TestCase(true, "critical", 0)][TestCase(false, "critical", 1)]
        [TestCase(true, "dodge", 0)][TestCase(false, "dodge", 1)]
        [TestCase(true, "miss", 0)][TestCase(false, "miss", 1)]
        [TestCase(true, "paralyzed", 0)][TestCase(false, "paralyzed", 0)]
        public void DamageEntryDrawsOnlyItsRequiredHitAndCriticalRolls(bool suppliedHit, string scenario, int expectedDraws)
        {
            var rng = UnityEngine.Random.state;
            try
            {
                var session = CreateSession(out var source, out var target);
                if (scenario == "critical") ResolutionGuarantee(source, UniqueBuffType.GuaranteedCriticalHit);
                if (scenario == "dodge") ResolutionGuarantee(target, UniqueBuffType.GuaranteedDodge);
                if (scenario == "paralyzed") target.Character.PassiveEffects.Add(new PassiveEffectRuntime {
                    EffectId = StatusEffectIds.Paralysis, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
                if (scenario == "miss") source.Character.WeaponAccuracy = 0;
                UnityEngine.Random.InitState(9182);
                for (var draw = 0; draw < expectedDraws; draw++) { var unused = UnityEngine.Random.value; }
                var expected = UnityEngine.Random.state;
                UnityEngine.Random.InitState(9182);
                var system = new CombatDamageSystem();
                if (suppliedHit) system.ResolveAndApplyDamage(session, source, target, 10, DamageType.Physical, scenario != "miss");
                else system.ResolveAndApplyDamage(session, source, target, 10, DamageType.Physical, scenario == "miss" ? 0f : 100f);
                Assert.That(UnityEngine.Random.state, Is.EqualTo(expected));
            }
            finally { UnityEngine.Random.state = rng; }
        }
    }
}
