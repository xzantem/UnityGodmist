using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatStallWarningTests
    {
        private readonly List<CharacterDefinition> definitions = new();
        private readonly List<string> log = new();
        private CombatSession session;
        private BattleUnit player, enemy;
        private CombatTurnSystem turns;
        private CombatDamageSystem damage;

        [SetUp] public void Setup()
        {
            log.Clear(); player = Unit(true); enemy = Unit(false);
            session = new CombatSession { Units = new List<BattleUnit> { player, enemy } };
            turns = new CombatTurnSystem(battleLog: log.Add);
            damage = new CombatDamageSystem();
        }
        [TearDown] public void Cleanup()
        {
            foreach (var definition in definitions) Object.DestroyImmediate(definition);
            definitions.Clear();
        }
        private BattleUnit Unit(bool playerSide)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); definitions.Add(definition);
            return new BattleUnit { IsPlayerSide = playerSide, Character = new CharacterInstance
            {
                Definition = definition, CharacterId = System.Guid.NewGuid().ToString(),
                LifeState = CharacterLifeState.Alive, CurrentHealth = 100,
                Stats = new CharacterStats { Speed = new Stat(20), MaxHealth = new Stat(100) },
                ShieldPool = new ShieldPool(), Resistances = new ResistanceSet()
            }};
        }
        private void End(BattleUnit unit)
        {
            session.ActiveParticipant = new CombatTurnParticipant(unit); session.ActiveUnit = unit;
            turns.EndActiveTurn(session);
        }
        private int Warnings => log.Count(entry => entry == CombatStallWarningSystem.Warning);

        [Test] public void WarnsAfterSecondEnemyTurnOnlyAndDoesNotSpawnOrSpam()
        {
            End(enemy); Assert.That(Warnings, Is.Zero);
            End(player); End(player); End(player);
            Assert.That(session.EnemyTurnsWithoutDamage, Is.EqualTo(1));
            End(enemy); Assert.That(Warnings, Is.EqualTo(1));
            for (var i = 0; i < 5; i++) End(enemy);
            Assert.That(Warnings, Is.EqualTo(1));
            Assert.That(session.Units.Count, Is.EqualTo(2));
        }
        [TestCase(DamageType.Physical)] [TestCase(DamageType.Burn)] [TestCase(DamageType.Poison)]
        public void EnemyDamageImmediatelyResetsAndAllowsANewWarning(DamageType type)
        {
            End(enemy); End(enemy); Assert.That(Warnings, Is.EqualTo(1));
            damage.ApplyDamage(session, player, enemy, 1, type);
            Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
            End(enemy); Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
            End(enemy); Assert.That(Warnings, Is.EqualTo(1));
            End(enemy); Assert.That(Warnings, Is.EqualTo(2));
        }
        [Test] public void ShieldDamageCountsButDamageToPlayerDoesNot()
        {
            End(enemy);
            enemy.Character.ShieldPool.AddLayer(20);
            damage.ApplyDamage(session, player, enemy, 5, DamageType.Physical);
            Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(100));
            End(enemy); Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
            damage.ApplyDamage(session, enemy, player, 5, DamageType.Physical);
            End(enemy); End(enemy); Assert.That(Warnings, Is.EqualTo(1));
        }
        [Test] public void ZeroDamageDoesNotResetTheCounter()
        {
            End(enemy);
            damage.ApplyDamage(session, player, enemy, 0, DamageType.Physical);
            End(enemy); Assert.That(Warnings, Is.EqualTo(1));
        }
        [Test] public void SecondLivingEnemyDisablesAndResetsTracking()
        {
            End(enemy);
            var other = Unit(false); session.Units.Add(other);
            End(enemy); End(other); End(enemy);
            Assert.That(Warnings, Is.Zero); Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
            other.Character.LifeState = CharacterLifeState.Dead;
            End(enemy); Assert.That(Warnings, Is.Zero);
            End(enemy); Assert.That(Warnings, Is.EqualTo(1));
        }
        [Test] public void EndOfTurnDamageIsAppliedBeforeTheWarningCheck()
        {
            End(enemy);
            turns = new CombatTurnSystem(battleLog: log.Add,
                onUnitTurnEnded: unit => damage.ApplyDamage(session, player, enemy, 1, DamageType.Bleed));
            End(enemy); Assert.That(Warnings, Is.Zero);
            Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
        }
        [Test] public void FinishedCombatAndSeparateSessionsDoNotInheritTheStreak()
        {
            End(enemy);
            var otherSession = new CombatSession { Units = new List<BattleUnit> { player, enemy } };
            CombatStallWarningSystem.EndUnitTurn(otherSession, enemy, log.Add);
            Assert.That(Warnings, Is.Zero);
            session.Outcome = CombatOutcomeState.Victory;
            CombatStallWarningSystem.EndUnitTurn(session, enemy, log.Add);
            Assert.That(Warnings, Is.Zero); Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
        }
        [Test] public void ThirdEnemyTurnRequestsReinforcementsAndAllowsAnotherWaveLater()
        {
            var waves = 0;
            BattleUnit reinforcement = null;
            session.TrySpawnReinforcements = () => {
                waves++; reinforcement = Unit(false); session.Units.Add(reinforcement); return true;
            };
            End(enemy); End(enemy); Assert.That(waves, Is.Zero);
            End(enemy); Assert.That(waves, Is.EqualTo(1));
            End(enemy); End(enemy); Assert.That(waves, Is.EqualTo(1));
            reinforcement.Character.LifeState = CharacterLifeState.Dead;
            End(enemy); End(enemy); End(enemy);
            Assert.That(waves, Is.EqualTo(2));
        }

        [Test] public void InvisibleLastEnemyDoesNotAdvanceStallOrSummonReinforcements()
        {
            var waves = 0;
            session.TrySpawnReinforcements = () => { waves++; return true; };
            End(enemy);
            enemy.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Invisibility,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 3
            });
            for (var i = 0; i < 4; i++) End(enemy);
            Assert.That(Warnings, Is.Zero);
            Assert.That(waves, Is.Zero);
            Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero);
            CombatStallWarningSystem.BeginUnitTurn(session, enemy);
            enemy.Character.PassiveEffects.Clear();
            End(enemy);
            Assert.That(session.EnemyTurnsWithoutDamage, Is.Zero,
                "Wygaśnięcie niewidzialności na początku tury nie daje jeszcze graczowi okazji do ataku.");
            End(enemy);
            End(enemy);
            Assert.That(Warnings, Is.EqualTo(1));
        }
    }
}
