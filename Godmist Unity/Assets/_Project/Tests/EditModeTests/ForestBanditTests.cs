using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestBanditTests
    {
        private Random.State randomState;
        private CharacterDefinition Bandit => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.BanditPath);
        [SetUp] public void PreserveRandom() => randomState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = randomState;

        private BattleUnit Unit(bool player = false)
        {
            var unit = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(Bandit, Bandit.ClassData, 1),
                Position = new GridPosition(player ? 2 : 1, 1), IsPlayerSide = player,
                CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = 100; // Skill tests start funded; spawn balance has dedicated coverage.
            unit.Character.CharacterId += player ? ".target" : ".caster";
            return unit;
        }
        private static CombatSession Session(BattleUnit caster, BattleUnit target) => new()
        {
            CombatId = "test.forest.bandit", Grid = new BattleGrid(12, 12), ActiveUnit = caster,
            Units = new List<BattleUnit> { caster, target }
        };
        private static SkillExecutionSystem Execution() => new(statusEffectSystem: new StatusEffectSystem());

        [Test]
        public void AuthoredStatsSkillsAndNoBorrowedLoot()
        {
            Assert.That(Bandit, Is.Not.Null);
            Assert.That(Bandit.BaseStats.MaxHealth, Is.EqualTo(216));
            Assert.That(Bandit.BaseStats.MinAttack, Is.EqualTo(26.35f).Within(.0001f));
            Assert.That(Bandit.BaseStats.MaxAttack, Is.EqualTo(41.65f).Within(.0001f));
            Assert.That(Bandit.BaseStats.Speed, Is.EqualTo(52));
            Assert.That(Bandit.GrowthData.MaxHealth, Is.EqualTo(23f)); // PT-06 level48 calibration.
            Assert.That(Bandit.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] { 0, 45, 35 }));
            Assert.That(Bandit.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] { 60, 25, 70 }));
            Assert.That(Bandit.BasicAttackSkill, Is.SameAs(Bandit.StartingSkills[0]));
            Assert.That(Bandit.LootTable, Is.Null);
            Assert.That(Bandit.StartingSkills[2].Effects[0].RearAttackDamageBonus, Is.EqualTo(.5f));
        }

        [Test]
        public void AmbushHidesAndAddsCritThenAttackReveals()
        {
            var caster = Unit(); var target = Unit(true); var session = Session(caster, target);
            var execution = Execution(); var crit = caster.Character.Stats.CritChance.GetValue();
            Assert.That(execution.TryExecute(session, caster, Bandit.StartingSkills[1], caster), Is.True);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(caster.Character.Stats.CritChance.GetValue(), Is.EqualTo(crit + .1f).Within(.0001f));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(55));
            Assert.That(execution.TryExecute(session, caster, Bandit.BasicAttackSkill, target), Is.True);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
            var critBuffId = Godmist.Data.Skills.SkillEffectIdentity.RuntimeId(Bandit.StartingSkills[1], 1, "modify_stat");
            var critBuff = caster.Character.PassiveEffects.Single(effect => effect.EffectId == critBuffId);
            Assert.That(critBuff.RemainingDuration, Is.EqualTo(2));
            Assert.That(caster.Character.Stats.CritChance.GetValue(), Is.EqualTo(crit + .1f).Within(.0001f));
            var lifecycle = new PassiveEffectLifecycleSystem();
            lifecycle.ProcessTurnEnd(caster);
            Assert.That(critBuff.RemainingDuration, Is.EqualTo(1));
            Assert.That(PassiveEffectLifecycleSystem.HasActiveEffect(caster.Character, critBuffId), Is.True);
            lifecycle.ProcessTurnEnd(caster);
            Assert.That(PassiveEffectLifecycleSystem.HasActiveEffect(caster.Character, critBuffId), Is.False);
        }

        [Test]
        public void AmbushCannotBeCastWithoutMomentum()
        {
            var caster = Unit(); var target = Unit(true);
            caster.Character.ResourcePool.Drain(56);
            Assert.That(Execution().TryExecute(Session(caster, target), caster, Bandit.StartingSkills[1], caster), Is.False);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(10000));
        }

        [Test]
        public void BackstabAddsFiftyPercentOnlyFromRear()
        {
            int Damage(bool rear)
            {
                var caster = Unit(); var target = Unit(true);
                caster.Character.Stats.CritChance = new Stat(0);
                caster.Character.Stats.WeaponAccuracy = new Stat(10000);
                target.Character.Stats.Dodge = new Stat(0);
                target.Character.Stats.PhysicalDefense = new Stat(0);
                target.ScoutState.Facing = new Vector2Int(rear ? 1 : -1, 0);
                var hp = target.Character.CurrentHealth;
                Random.InitState(9182);
                Assert.That(Execution().TryExecute(Session(caster, target), caster, Bandit.StartingSkills[2], target), Is.True);
                Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(65));
                return hp - target.Character.CurrentHealth;
            }
            var front = Damage(false); var rear = Damage(true);
            Assert.That(front, Is.GreaterThan(0));
            Assert.That(rear, Is.EqualTo(front * 1.5f).Within(1));
        }

        [Test]
        public void MissedKnifeDoesNotApplyBleeding()
        {
            var caster = Unit(); var target = Unit(true);
            target.Character.Stats.Dodge = new Stat(10000);
            var hp = target.Character.CurrentHealth;
            Assert.That(Execution().TryExecute(Session(caster, target), caster, Bandit.BasicAttackSkill, target), Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(hp));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Bleed), Is.False);
        }

        [Test]
        public void AiDoesNotSpendAmbushFarFromAnAttackOrRefreshActiveInvisibility()
        {
            var caster = Unit(); var target = Unit(true);
            target.Position = new GridPosition(8, 8);
            var session = Session(caster, target);
            CombatTurnSystem.RefreshMaxActionPoints(session, caster, refillCurrentActionPoints: true);
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: Execution());
            var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            Assert.That(first.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
            caster.MovementStepsThisTurn = 0;
            CombatTurnSystem.RefreshMaxActionPoints(session, caster, refillCurrentActionPoints: true);
            Assert.That(Execution().TryExecute(session, caster, Bandit.StartingSkills[1], caster), Is.True);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var next), Is.True);
            Assert.That(next.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(next.Skill, Is.Not.SameAs(Bandit.StartingSkills[1]));
        }
    }
}
