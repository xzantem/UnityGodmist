using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
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
    public sealed class ForestShooterTests
    {
        private Random.State randomState;
        private CharacterDefinition Shooter => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.ShooterPath);
        [SetUp] public void PreserveRandom() => randomState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = randomState;
        private BattleUnit Unit(bool player, int x, int y)
        {
            var unit = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(Shooter, Shooter.ClassData, 1),
                Position = new GridPosition(x, y), IsPlayerSide = player,
                CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = 100; // Skill tests start funded; spawn balance has dedicated coverage.
            unit.Character.CharacterId += $".{player}.{x}.{y}";
            unit.Character.Stats.WeaponAccuracy = new Stat(10000);
            return unit;
        }
        private static CombatSession Session(params BattleUnit[] units) => new()
        {
            CombatId = "test.forest.shooter", Grid = new BattleGrid(12, 12), ActiveUnit = units[0],
            Units = units.ToList()
        };

        [Test]
        public void AuthoredCostsStatsAndRanges()
        {
            Assert.That(Shooter.BaseStats.MaxHealth, Is.EqualTo(167));
            Assert.That(Shooter.BaseStats.MinAttack, Is.EqualTo(39));
            Assert.That(Shooter.BaseStats.MaxAttack, Is.EqualTo(62));
            Assert.That(Shooter.GrowthData.MaxHealth, Is.EqualTo(18f)); // PT-06 level48 calibration.
            Assert.That(Shooter.BaseStats.Speed, Is.EqualTo(55));
            Assert.That(Shooter.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] { 0, 25, 50 }));
            Assert.That(Shooter.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] { 65, 65, 35 }));
            Assert.That(Shooter.BasicAttackSkill.TargetModes[0].AbsoluteRange.MaxRange, Is.EqualTo(9));
            Assert.That(Shooter.StartingSkills[1].TargetModes[0].AbsoluteRange.RectangleWidth, Is.EqualTo(2));
            Assert.That(Shooter.StartingSkills[1].Effects[0].DamageMultiplier, Is.EqualTo(.7f));
        }

        [Test]
        public void PiercingBoltTargetsSeveralEnemiesButNotAllyOrOffAxis()
        {
            var caster = Unit(false, 1, 1); var near = Unit(true, 3, 1); var far = Unit(true, 6, 1);
            var offAxis = Unit(true, 5, 5); var ally = Unit(false, 4, 1);
            var session = Session(caster, near, far, offAxis, ally);
            var skill = Shooter.StartingSkills[1];
            var targets = new CombatTargetingService().GetTargets(session, caster, skill.TargetModes[0], new GridPosition(9, 1));
            Assert.That(targets, Is.EquivalentTo(new[] { near, far }));
            var hp = near.Character.CurrentHealth; var otherHp = offAxis.Character.CurrentHealth;
            Random.InitState(12345);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = targets }), Is.True);
            Assert.That(near.Character.CurrentHealth, Is.LessThan(hp));
            Assert.That(far.Character.CurrentHealth, Is.LessThan(hp));
            Assert.That(offAxis.Character.CurrentHealth, Is.EqualTo(otherHp));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(75));
        }

        [Test]
        public void FlareCleansesAlliesAndNeverDebuffsThem()
        {
            var caster = Unit(false, 1, 1); var ally = Unit(false, 2, 1);
            var enemy = Unit(true, 5, 1); var session = Session(caster, ally, enemy);
            var statuses = new StatusEffectSystem();
            ally.Character.Resistances.SuppressionResistance = 0;
            Assert.That(statuses.ApplyStatus(caster, ally, StatusEffectIds.Stun, 1f, 2, 1, 0, 0, "test.stun"), Is.True);
            var skill = Shooter.StartingSkills[2]; var targeting = new CombatTargetingService();
            var enemies = targeting.GetTargets(session, caster, skill.TargetModes[0], null);
            var allies = targeting.GetTargets(session, caster, skill.TargetModes[1], null);
            Assert.That(enemies, Does.Contain(enemy)); Assert.That(enemies, Has.No.Member(ally));
            Assert.That(allies, Does.Contain(ally)); Assert.That(allies, Has.No.Member(enemy));
            var accuracy = ally.Character.Stats.WeaponAccuracy.GetValue(); var hp = enemy.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem(statusEffectSystem: statuses).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = enemies, [1] = allies }), Is.True);
            Assert.That(StatusEffectSystem.HasStatus(ally, StatusEffectIds.Stun), Is.False);
            Assert.That(ally.Character.Stats.WeaponAccuracy.GetValue(), Is.EqualTo(accuracy));
            Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(hp));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [Test]
        public void RangedAiAttacksWithoutWalkingIntoMelee()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 7, 1);
            caster.Character.ResourcePool.CurrentValue = 0;
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()));
            var session = Session(caster, enemy); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var reposition), Is.True);
            Assert.That(reposition.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(reposition.MovementPath.Count, Is.InRange(1, 2));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(Shooter.BasicAttackSkill));
            Assert.That(Godmist.Data.Skills.SkillData.IsPointBlank(caster.Position.X - enemy.Position.X, caster.Position.Y - enemy.Position.Y), Is.False);
        }

        [Test]
        public void AiValuesFreeingStunnedAllyWithFlare()
        {
            var caster = Unit(false, 1, 1); var ally = Unit(false, 2, 1); var enemy = Unit(true, 7, 1);
            ally.Character.Resistances.SuppressionResistance = 0;
            enemy.Character.Resistances.SuppressionResistance = 0;
            var statuses = new StatusEffectSystem();
            statuses.ApplyStatus(caster, ally, StatusEffectIds.Stun, 1, 2, 1, 0, 0, "test.ally.stun");
            statuses.ApplyStatus(caster, enemy, StatusEffectIds.Stun, 1, 2, 1, 0, 0, "test.enemy.stun");
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(statusEffectSystem: statuses));
            var session=Session(caster, ally, enemy);var turn=ai.CreateTurnState();var usedFlare=false;
            CombatTurnSystem.RefreshMaxActionPoints(session,caster,true);
            // A bolt (65% AP) and Flare (35%) both fit. Stronger base attack can
            // change their order; the useful cleanse must still occur this turn.
            for(var i=0;i<2 && !usedFlare;i++)
            {
                Assert.That(ai.TryExecuteNextAction(session,caster,turn,out var action),Is.True);
                TestContext.WriteLine($"Flare sequence {i}: {action.Skill?.SkillId}; AP={caster.CurrentActionPoints}/{caster.MaxActionPoints}; resource={caster.Character.ResourcePool.CurrentValue}");
                usedFlare=action.Skill==Shooter.StartingSkills[2];
            }
            Assert.That(usedFlare,Is.True,"Use the funded cleanse within the same turn, allowing a useful shot first.");
            Assert.That(StatusEffectSystem.HasStatus(ally, StatusEffectIds.Stun), Is.False);
        }
    }
}
