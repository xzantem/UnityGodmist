using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DownedStatusExpiryTests
    {
        static BattleUnit Unit(CharacterLifeState state = CharacterLifeState.Downed) => new()
        {
            IsPlayerSide = true, DownedTurnsRemaining = 3,
            Character = new CharacterInstance
            {
                CharacterId = "downed-expiry", LifeState = state,
                CurrentHealth = state == CharacterLifeState.Alive ? 100 : 0,
                Stats = new CharacterStats { MaxHealth = new Stat(100), Speed = new Stat(100) },
                ShieldPool = new ShieldPool(),
                ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 15),
                Resistances = new ResistanceSet { BleedResistance = .5f }
            }
        };
        static PassiveEffectRuntime Effect(string id, int turns, PassiveEffectTickPhase phase, int damage = 0)
            => new(id, new List<PassiveEffectSourceRuntime>(), 1, turns, damage,
                PassiveEffectLifetime.Timed, phase);

        [TestCase("burn")][TestCase("bleed")][TestCase("poison")]
        public void MissedDotConsumesBudgetWithoutDamageRewardsOrDeathAcceleration(string id)
        {
            var unit = Unit(); var effect = Effect(id, 3, PassiveEffectTickPhase.TurnStart, 10);
            unit.Character.PassiveEffects.Add(effect);
            var processor = new CountingProcessor();
            var lifecycle = new PassiveEffectLifecycleSystem(turnProcessors: new[] { processor });
            var status = new StatusEffectSystem(passiveEffectLifecycleSystem: lifecycle);
            var feedback = 0; unit.Character.HealingReceived += (_, _, _) => feedback++;
            var rng = Random.state;
            status.ProcessDownedTurn(unit);
            Assert.That(effect.RemainingDuration, Is.EqualTo(2));
            Assert.That(effect.TotalDamage, Is.EqualTo(6));
            Assert.That(unit.Character.CurrentHealth, Is.Zero);
            Assert.That(unit.DownedTurnsRemaining, Is.EqualTo(3));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.LifeState, Is.EqualTo(CharacterLifeState.Downed));
            Assert.That(processor.Calls, Is.Zero); Assert.That(feedback, Is.Zero);
            Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(JsonUtility.ToJson(rng)));
            status.ProcessDownedTurn(unit); status.ProcessDownedTurn(unit);
            Assert.That(unit.Character.PassiveEffects, Is.Empty); Assert.That(effect.TotalDamage, Is.Zero);
        }

        [TestCase("stun")][TestCase("freeze")][TestCase("paralysis")][TestCase("sleep")]
        public void ControlAndOtherTimedEffectsExpireTogetherDespiteParalysis(string control)
        {
            var unit = Unit();
            unit.Character.PassiveEffects.Add(Effect(control, 1, PassiveEffectTickPhase.TurnEnd));
            unit.Character.PassiveEffects.Add(Effect("paralysis", 1, PassiveEffectTickPhase.TurnEnd));
            unit.Character.PassiveEffects.Add(Effect("buff.test", 1, PassiveEffectTickPhase.TurnEnd));
            unit.Character.PassiveEffects.Add(Effect("debuff.test", 1, PassiveEffectTickPhase.TurnStart));
            unit.Character.PassiveEffects.Add(Effect("timed.no_phase", 1, PassiveEffectTickPhase.None));
            var persistent = new PassiveEffectRuntime { EffectId = "permanent" };
            var combat = new PassiveEffectRuntime { EffectId = "combat", Lifetime = PassiveEffectLifetime.CombatScoped };
            unit.Character.PassiveEffects.Add(persistent); unit.Character.PassiveEffects.Add(combat);
            new StatusEffectSystem().ProcessDownedTurn(unit);
            Assert.That(unit.Character.PassiveEffects, Is.EquivalentTo(new[] { persistent, combat }));
            Assert.That(unit.Character.LifeState, Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void TimedShieldsExpireWithoutAbsorbingAndUntimedShieldsRemain()
        {
            var unit = Unit(); unit.Character.ShieldPool.AddLayer(25, "timed", 2);
            unit.Character.ShieldPool.AddLayer(7, "untimed");
            var status = new StatusEffectSystem(); status.ProcessDownedTurn(unit);
            Assert.That(unit.Character.ShieldPool.TotalShield, Is.EqualTo(32));
            status.ProcessDownedTurn(unit);
            Assert.That(unit.Character.ShieldPool.TotalShield, Is.EqualTo(7));
        }

        [Test]
        public void RegenerationAndSleepClocksExpireWithoutAutomaticRevivalOrResourceGain()
        {
            var unit = Unit(); var heal = Effect("regen", 1, PassiveEffectTickPhase.TurnEnd);
            heal.SetPayload(new PeriodicRecoveryPayload { HealthPercent = .5f, ResourcePercent = .5f });
            var sleep = Effect("sleep", 1, PassiveEffectTickPhase.TurnEnd); sleep.StatusHealPercent = .5f;
            unit.Character.PassiveEffects.Add(heal); unit.Character.PassiveEffects.Add(sleep);
            new StatusEffectSystem().ProcessDownedTurn(unit);
            Assert.That(unit.Character.CurrentHealth, Is.Zero);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.LifeState, Is.EqualTo(CharacterLifeState.Downed));
            Assert.That(unit.Character.PassiveEffects, Is.Empty);
        }

        [Test]
        public void RevivalKeepsOnlyRemainingTicksWithoutCleansingOrCatchup()
        {
            var unit = Unit(); var effect = Effect("bleed", 3, PassiveEffectTickPhase.TurnStart, 10);
            unit.Character.PassiveEffects.Add(effect);
            var status = new StatusEffectSystem(); status.ProcessDownedTurn(unit);
            Assert.That(CombatReviveSystem.StandUp(unit.Character), Is.True);
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(1));
            Assert.That(unit.Character.PassiveEffects, Does.Contain(effect));
            Assert.That(effect.TotalDamage, Is.EqualTo(6));
            unit.Character.CurrentHealth = 100;
            var session = new CombatSession { Units = new() { unit } };
            status.ProcessTurnStart(session, unit);
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(97));
            Assert.That(effect.TotalDamage, Is.EqualTo(3));
            Assert.That(effect.RemainingDuration, Is.EqualTo(1));
        }

        [Test]
        public void SchedulerAdvancesClocksOnceAndKeepsNormalThreeTurnDeathTimer()
        {
            var down = Unit(); var alive = Unit(CharacterLifeState.Alive);
            var effect = Effect("burn", 3, PassiveEffectTickPhase.TurnStart, 300);
            down.Character.PassiveEffects.Add(effect);
            var session = new CombatSession { Units = new() { down, alive },
                TurnParticipants = new() { new(down), new(alive) } };
            var normalTurns = 0;
            var turns = new CombatTurnSystem(onUnitTurnBegan: _ => normalTurns++);
            for (var i = 1; i <= 3; i++)
            {
                down.CurrentActionValue = 1; alive.CurrentActionValue = 10;
                turns.AdvanceTurn(session);
                Assert.That(down.DownedTurnsRemaining, Is.EqualTo(3-i));
                Assert.That(effect.RemainingDuration, Is.EqualTo(3-i));
                Assert.That(session.ActiveUnit, Is.SameAs(alive));
            }
            Assert.That(normalTurns, Is.EqualTo(3));
            Assert.That(down.Character.LifeState, Is.EqualTo(CharacterLifeState.Dead));
        }

        [Test]
        public void ExplorationUsesSixSecondClockAndSkipsCombatOccupants()
        {
            var unit = Unit(); var effect = Effect("burn", 3, PassiveEffectTickPhase.TurnStart, 30);
            unit.Character.PassiveEffects.Add(effect);
            var outside = new DungeonTrapOccupant(unit, null, Vector2Int.zero);
            new ExplorationStatusEffectSystem().Advance(5, new[] { outside, outside });
            Assert.That(effect.RemainingDuration, Is.EqualTo(3));
            new ExplorationStatusEffectSystem().Advance(8, new[] { outside, outside });
            Assert.That(effect.RemainingDuration, Is.EqualTo(1)); Assert.That(effect.TotalDamage, Is.EqualTo(10));
            Assert.That(unit.Character.ExplorationStatusElapsedSeconds, Is.EqualTo(1));
            new ExplorationStatusEffectSystem().Advance(60, new[] { new DungeonTrapOccupant(unit, new CombatSession(), Vector2Int.zero) });
            Assert.That(effect.RemainingDuration, Is.EqualTo(1));
            Assert.That(unit.DownedTurnsRemaining, Is.EqualTo(3));
            Assert.That(unit.Character.CurrentHealth, Is.Zero);
            new ExplorationStatusEffectSystem().Advance(5, new[] { outside });
            Assert.That(unit.Character.PassiveEffects, Is.Empty);
        }

        [TestCase(CharacterLifeState.Alive)][TestCase(CharacterLifeState.Dead)]
        public void DownedClockDoesNotAdvanceLivingOrDeadCharacters(CharacterLifeState state)
        {
            var unit = Unit(state); var effect = Effect("burn", 3, PassiveEffectTickPhase.TurnStart, 30);
            unit.Character.PassiveEffects.Add(effect); new StatusEffectSystem().ProcessDownedTurn(unit);
            Assert.That(effect.RemainingDuration, Is.EqualTo(3)); Assert.That(effect.TotalDamage, Is.EqualTo(30));
        }

        sealed class CountingProcessor : IPassiveEffectTurnProcessor
        {
            public int Calls;
            public bool CanProcess(PassiveEffectRuntime effect, PassiveEffectTickPhase phase) => true;
            public bool ProcessTurn(CombatSession session, BattleUnit unit, PassiveEffectRuntime effect, out bool shouldExpire)
            { Calls++; shouldExpire = false; return true; }
        }
    }
}
