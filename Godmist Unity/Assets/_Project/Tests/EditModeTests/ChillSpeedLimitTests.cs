using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ChillSpeedLimitTests
    {
        [TestCase("plain", 45, 20)]
        [TestCase("rage", 45, 20)]
        [TestCase("momentum", 45, 20)]
        [TestCase("mage", 45, 20)]
        [TestCase("plain", 12, 12)]
        [TestCase("momentum", 2, 18)]
        public void ChillCapsTheFinalSpeedAndExpirationRestoresIt(string build, int speed, int expected)
        {
            var target = Unit(build, speed); var system = new StatusEffectSystem();
            new CharacterStatPipelineSystem().Refresh(target.Character);
            var original = target.Character.Stats.Speed.GetValue();
            Assert.That(system.ApplyStatus(Unit("plain", 20), target, StatusEffectIds.Chill,
                1f, 1, 1, 0, 0, "test", sharedRoll: 0f), Is.True);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(expected));
            var json = JsonUtility.ToJson(target.Character);
            Assert.That(CharacterStatExplanation.Create(target.Character).Values[CharacterStatType.Speed], Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(target.Character), Is.EqualTo(json), "Breakdown must not mutate the character.");
            system.ProcessTurnEnd(target);
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Chill), Is.False);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(original));
        }

        [Test] public void RefreshAndMomentumThresholdChangesCannotEscapeTheLimit()
        {
            var target = Unit("momentum", 12); var pool = target.Character.ResourcePool;
            new StatusEffectSystem().ApplyStatus(Unit("plain", 20), target, StatusEffectIds.Chill, 1, 3, 1, 0, 0, "test", sharedRoll: 0f);
            foreach (var amount in new[] { 200, 100, 150, 200, 100 })
            {
                var previous = pool.CurrentValue; pool.CurrentValue = amount;
                MomentumResourceRule.RefreshSpeedAfterChange(target.Character, previous);
                new CharacterStatPipelineSystem().Refresh(target.Character);
                Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(Math.Min(20, 12 + Math.Max(0, amount - 100) / 25 * 4)));
            }
        }

        [Test] public void BreakdownPlacesTheLimitAfterDerivedSpeedBonuses()
        {
            var target = Unit("mage", 45);
            new StatusEffectSystem().ApplyStatus(Unit("plain", 20), target, StatusEffectIds.Chill, 1, 3, 1, 0, 0, "test", sharedRoll: 0f);
            var explanation = CharacterStatExplanation.Create(target.Character);
            var cap = explanation.Rows.Single(r => r.Stat == CharacterStatType.Speed && r.Operation == StatModifierOperation.Maximum);
            Assert.That(cap.Stage, Is.GreaterThan(explanation.Rows.Where(r => r.Stat == CharacterStatType.Speed && r.Operation != StatModifierOperation.Maximum).Max(r => r.Stage)));
        }

        [TestCase("plain", 45)] [TestCase("rage", 61)] [TestCase("momentum", 61)] [TestCase("mage", 60)]
        public void UnaffectedBuildsRetainTheirSpeed(string build, int expected)
        {
            var target = Unit(build, 45); new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(expected));
            var json = JsonUtility.ToJson(target.Character);
            Assert.That(CharacterStatPipelineSystem.CalculateEffectiveSpeed(target.Character), Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(target.Character), Is.EqualTo(json));
        }

        [Test] public void PlannerMomentumGainAndSpendingUseTheUnclippedSpeed()
        {
            var target = Unit("momentum", 12);
            new StatusEffectSystem().ApplyStatus(Unit("plain", 20), target, StatusEffectIds.Chill, 1, 3, 1, 0, 0, "test", sharedRoll: 0f);
            var update = typeof(SimpleCombatAiSystem).GetMethod("AdjustProjectedMomentumSpeed", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            foreach (var amount in new[] { 100, 200, 125, 200, 100 })
            {
                var before = target.Character.ResourcePool.CurrentValue; target.Character.ResourcePool.CurrentValue = amount;
                update.Invoke(null, new object[] { target, before });
                Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(Math.Min(20, 12 + Math.Max(0, amount - 100) / 25 * 4)));
            }
        }

        [TestCase("rage")] [TestCase("momentum")] [TestCase("mage")]
        public void SkillApplicationHasTheSameFinalLimit(string build)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var random = UnityEngine.Random.state;
            try
            {
                typeof(SkillData).GetField("<SkillId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, "test.chill");
                var effect = new SkillEffectData();
                foreach (var pair in new[] { ("statusId", (object)StatusEffectIds.Chill), ("baseChance", (object)1f), ("durationTurns", (object)2) })
                    typeof(SkillEffectData).GetField(pair.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, pair.Item2);
                var target = Unit(build, 45);
                Assert.That(new StatusEffectSystem().ApplyStatus(Unit("plain", 20), target, skill, effect, 0), Is.True);
                Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(20));
                Assert.That(CharacterStatPipelineSystem.CalculateEffectiveSpeed(target.Character), Is.EqualTo(20));
            }
            finally { UnityEngine.Random.state = random; UnityEngine.Object.DestroyImmediate(skill); }
        }

        [Test] public void LifecycleNormalizesStatusPayloadBeforeRefreshAndExpiration()
        {
            var target = Unit("mage", 45);
            var chill = new PassiveEffectRuntime { EffectId = StatusEffectIds.Chill, Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 1, TickPhase = PassiveEffectTickPhase.TurnEnd };
            Assert.That(new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character, chill, PassiveEffectStackingMode.ReplaceDurationAndValues, out _), Is.True);
            Assert.That(chill.StatModifiers.Count, Is.EqualTo(1));
            new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(20));
            var explanation = CharacterStatExplanation.Create(target.Character);
            Assert.That(explanation.Values[CharacterStatType.Speed], Is.EqualTo(20));
            Assert.That(explanation.Rows.Any(r => r.Source == StatusEffectIds.Chill && r.Operation == StatModifierOperation.Maximum && r.Stage > 6), Is.True);
            new StatusEffectSystem().ProcessTurnEnd(target);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(60));
        }

        [Test] public void ReapplicationExtendsDurationWithoutStackingCapsOrRemovingStricterLimits()
        {
            var target = Unit("momentum", 45); var source = Unit("plain", 20); var system = new StatusEffectSystem();
            Assert.That(system.ApplyStatus(source, target, StatusEffectIds.Chill, 1, 2, 1, 0, 0, "first", sharedRoll: 0f), Is.True);
            Assert.That(system.ApplyStatus(source, target, StatusEffectIds.Chill, 1, 5, 1, 0, 0, "second", sharedRoll: 0f), Is.True);
            Assert.That(target.Character.PassiveEffects.Count(e => e.EffectId == StatusEffectIds.Chill), Is.EqualTo(1));
            Assert.That(target.Character.PassiveEffects.Single(e => e.EffectId == StatusEffectIds.Chill).RemainingDuration, Is.EqualTo(5));
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(20));
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = "stricter", Lifetime = PassiveEffectLifetime.Persistent,
                StatModifiers = new() { new StatModifier { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.Maximum, Value = 15 } } });
            new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(15));
            Assert.That(CharacterStatExplanation.Create(target.Character).Values[CharacterStatType.Speed], Is.EqualTo(15));
        }

        [Test] public void ExpiringBurnRemovesMageSpeedBonusWhileChillRemains()
        {
            var target = Unit("mage", 12);
            var burn = target.Character.PassiveEffects.Single(e => e.EffectId == StatusEffectIds.Burn);
            burn.Lifetime = PassiveEffectLifetime.Timed; burn.RemainingDuration = 1; burn.TickPhase = PassiveEffectTickPhase.TurnEnd;
            var system = new StatusEffectSystem();
            system.ApplyStatus(Unit("plain", 20), target, StatusEffectIds.Chill, 1, 3, 1, 0, 0, "test", sharedRoll: 0f);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(20));
            system.ProcessTurnEnd(target);
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Chill), Is.True);
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Burn), Is.False);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(12));
            Assert.That(CharacterStatPipelineSystem.CalculateEffectiveSpeed(target.Character), Is.EqualTo(12));
        }

        private static BattleUnit Unit(string build, int speed)
        {
            var stats = new CharacterStats();
            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType))) stats.GetStat(stat) = new Stat(0);
            stats.Speed = new Stat(speed); stats.MaxHealth = new Stat(100); stats.MaxResource = new Stat(1000);
            var character = new CharacterInstance { CharacterId = Guid.NewGuid().ToString(), Stats = stats,
                CurrentHealth = 100, LifeState = CharacterLifeState.Alive,
                ResourcePool = new ResourcePool(build == "momentum" ? ResourceType.Momentum : build == "rage" ? ResourceType.Rage : ResourceType.Mana,
                    build == "momentum" ? 200 : 100, 1000, 0), Resistances = new ResistanceSet() };
            if (build == "rage" || build == "mage")
                character.SkillTreeProgress.Add(new SkillTreeProgress {
                    TreeId = build == "rage" ? WarriorCombatSystem.Brawler : MageCombatSystem.Fire,
                    NodeInvestments = new() { new SkillTreeNodeInvestment { NodeId = build == "rage" ? "node.warrior.brawler.adrenaline" : "node.mage.fire.adrenaline", Ranks = 1 } } });
            if (build == "mage") character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = StatusEffectIds.Burn,
                Lifetime = PassiveEffectLifetime.Persistent });
            return new BattleUnit { Character = character };
        }
    }
}
