using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CombatEscapeSystemTests
    {
        [Test]
        public void TryEscape_WhenPreparationFails_IncreasesSharedChance()
        {
            var actor = CreatePlayerUnit();
            var session = CreateSession(actor);
            var system = new CombatEscapeSystem(rollPercent: () => 61);

            var result = system.TryEscape(session, actor);

            Assert.That(result, Is.True);
            Assert.That(actor.IsReadyToEscape, Is.False);
            Assert.That(session.CurrentEscapeChancePercent, Is.EqualTo(70));
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Ongoing));
        }

        [Test]
        public void TryEscape_WhenPreparationSucceeds_MarksActorReady()
        {
            var actor = CreatePlayerUnit();
            var session = CreateSession(actor);
            var system = new CombatEscapeSystem(rollPercent: () => 60);

            var result = system.TryEscape(session, actor);

            Assert.That(result, Is.True);
            Assert.That(actor.IsReadyToEscape, Is.True);
            Assert.That(session.CurrentEscapeChancePercent, Is.EqualTo(60));
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Ongoing));
        }

        [Test]
        public void TryEscape_WhenReadyActorEscapes_KillsUnpreparedAlliesAndKeepsUnitsForCleanup()
        {
            var escapingActor = CreatePlayerUnit(isReadyToEscape: true);
            var abandonedAlly = CreatePlayerUnit();
            var enemy = CreateEnemyUnit();

            var session = CreateSession(
                escapingActor,
                abandonedAlly,
                enemy);

            var system = new CombatEscapeSystem();
            var lifecycleSystem = new PassiveEffectLifecycleSystem();

            escapingActor.Character.PassiveEffects.Add(
                new PassiveEffectRuntime(
                    "effect.combat_scoped",
                    new List<PassiveEffectSourceRuntime>(),
                    stacks: 1,
                    remainingDuration: 0,
                    totalDamage: 0,
                    lifetime: PassiveEffectLifetime.CombatScoped));

            var result = system.TryEscape(session, escapingActor);

            Assert.That(result, Is.True);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
            Assert.That(abandonedAlly.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Dead));
            CollectionAssert.Contains(session.Units, escapingActor);
            CollectionAssert.Contains(session.Units, abandonedAlly);
            CollectionAssert.Contains(session.Units, enemy);

            var removedCount = lifecycleSystem.RemoveCombatScopedEffects(session);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(escapingActor.Character.PassiveEffects, Is.Empty);
        }

        private static CombatSession CreateSession(params BattleUnit[] units)
        {
            return new CombatSession
            {
                ActiveUnit = units[0],
                Units = new List<BattleUnit>(units),
                TurnOrder = new List<BattleUnit>(units),
                Outcome = CombatOutcomeState.Ongoing,
                CurrentEscapeChancePercent = 60
            };
        }

        private static BattleUnit CreatePlayerUnit(bool isReadyToEscape = false)
        {
            return new BattleUnit
            {
                IsPlayerSide = true,
                IsReadyToEscape = isReadyToEscape,
                Character = new CharacterInstance
                {
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive
                }
            };
        }

        private static BattleUnit CreateEnemyUnit()
        {
            return new BattleUnit
            {
                IsPlayerSide = false,
                Character = new CharacterInstance
                {
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive
                }
            };
        }
    }
}
