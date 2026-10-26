using System.Collections.Generic;
using System.Linq;
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
    public sealed class ForestArcherTests
    {
        private Random.State randomState;
        private CharacterDefinition Archer => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.ArcherPath);
        [SetUp] public void PreserveRandom() => randomState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = randomState;
        private BattleUnit Unit(bool player, int x, int y)
        {
            var unit = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(Archer, Archer.ClassData, 1),
                Position = new GridPosition(x, y), IsPlayerSide = player,
                CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = 100; // Skill tests start funded; spawn balance has dedicated coverage.
            unit.Character.CharacterId += $".{player}.{x}.{y}";
            unit.Character.Stats.CritChance = new Stat(0);
            return unit;
        }
        private static CombatSession Session(params BattleUnit[] units) => new()
        {
            CombatId = "test.forest.archer", Grid = new BattleGrid(14, 14), ActiveUnit = units[0],
            Units = units.ToList()
        };
        private static SkillExecutionSystem Execution() => new(statusEffectSystem: new StatusEffectSystem());

        [Test]
        public void AuthoredStatsCostsAndLoot()
        {
            Assert.That(Archer.BaseStats.MaxHealth, Is.EqualTo(148));
            Assert.That(Archer.BaseStats.MinAttack, Is.EqualTo(45));
            Assert.That(Archer.BaseStats.MaxAttack, Is.EqualTo(62));
            Assert.That(Archer.GrowthData.MaxHealth, Is.EqualTo(16f)); // PT-06 level48 calibration.
            Assert.That(Archer.BaseStats.Speed, Is.EqualTo(54));
            Assert.That(Archer.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] { 0, 30, 50 }));
            Assert.That(Archer.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] { 60, 70, 30 }));
            var drop = Archer.LootTable.Entries.Single();
            Assert.That(drop.DropChance, Is.EqualTo(.15f));
            Assert.That(drop.Amount.Min, Is.EqualTo(1)); Assert.That(drop.Amount.Max, Is.EqualTo(2));
            Assert.That(drop.ItemDefinition.Id, Is.EqualTo("item.material.elf_ear"));
            Assert.That(drop.ItemDefinition.BaseValue, Is.EqualTo(17));
        }

        [TestCase(1)] [TestCase(5)] [TestCase(9)]
        public void HawkeyeGuaranteesHitAndAddsFivePercentPerCell(int distance)
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 1 + distance, 1);
            enemy.Character.Stats.MaxHealth = new Stat(10000); enemy.Character.CurrentHealth = 10000;
            enemy.Character.Stats.Dodge = new Stat(10000);
            enemy.Character.Stats.PhysicalDefense = new Stat(0);
            caster.Character.Stats.MinAttack = new Stat(100); caster.Character.Stats.MaxAttack = new Stat(100);
            var session = Session(caster, enemy);
            Assert.That(CombatDamageSystem.RollHit(caster, enemy, 0), Is.False);
            Assert.That(Execution().TryExecute(session, caster, Archer.StartingSkills[2], caster), Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            Assert.That(CombatDamageSystem.RollHit(caster, enemy, 0), Is.True);
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, enemy, Archer.BasicAttackSkill);
            Assert.That(preview.HitChance, Is.EqualTo(1));
            Assert.That(preview.Damage.Minimum, Is.EqualTo(Mathf.RoundToInt(100 * (1 + distance * .05f))));
            var hp = enemy.Character.CurrentHealth;
            Assert.That(Execution().TryExecute(session, caster, Archer.BasicAttackSkill, enemy), Is.True);
            Assert.That(hp - enemy.Character.CurrentHealth, Is.EqualTo(Mathf.RoundToInt(100 * (1 + distance * .05f))));
            Assert.That(CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.Hawkeye), Is.True);
        }

        [Test]
        public void HawkeyeExpiresAndDoesNotMultiplyBleeding()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 6, 1);
            Assert.That(Execution().TryExecute(Session(caster, enemy), caster, Archer.StartingSkills[2], caster), Is.True);
            Assert.That(CombatDamageSystem.HawkeyeDamageMultiplier(caster, enemy, DamageType.Bleed), Is.EqualTo(1));
            var lifecycle = new PassiveEffectLifecycleSystem();
            for (var turn = 0; turn < 3; turn++) lifecycle.ProcessTurnEnd(caster);
            Assert.That(CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.Hawkeye), Is.False);
            Assert.That(CombatDamageSystem.HawkeyeDamageMultiplier(caster, enemy, DamageType.Physical), Is.EqualTo(1));
        }

        [Test]
        public void ArrowRainCoversThreeByThreeAndExcludesAlly()
        {
            var caster = Unit(false, 1, 1); var middle = Unit(true, 5, 5);
            var edge = Unit(true, 6, 6); var oppositeEdge = Unit(true, 4, 4); var outside = Unit(true, 7, 5); var ally = Unit(false, 4, 5);
            var skill = Archer.StartingSkills[1];
            var targets = new CombatTargetingService().GetTargets(Session(caster, middle, edge, oppositeEdge, outside, ally),
                caster, skill.TargetModes[0], new GridPosition(5, 5));
            Assert.That(targets, Is.EquivalentTo(new[] { middle, edge, oppositeEdge }));
            Assert.That(skill.Effects[0].DamageMultiplier, Is.EqualTo(.6f));
            Assert.That(skill.Effects[1].BaseChance, Is.EqualTo(.5f));
        }

        [Test]
        public void AiApproachesIntoRangeThenUsesHawkeyeWithoutRefreshingIt()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 11, 1);
            var session = Session(caster, enemy);
            CombatTurnSystem.RefreshMaxActionPoints(session, caster, refillCurrentActionPoints: true);
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: Execution()); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var approach), Is.True);
            Assert.That(approach.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.Hawkeye), Is.False);
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var buff), Is.True);
            Assert.That(buff.Skill, Is.SameAs(Archer.StartingSkills[2]));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var attack), Is.True);
            Assert.That(attack.Skill, Is.SameAs(Archer.BasicAttackSkill));
        }
    }
}
