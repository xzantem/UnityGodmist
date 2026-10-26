using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestPoacherTests
    {
        private Random.State randomState;
        [SetUp] public void PreserveRandom() => randomState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = randomState;
        private CharacterDefinition Poacher => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.PoacherPath);
        private BattleUnit Unit(bool player, int x, int y)
        {
            var unit = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(Poacher, Poacher.ClassData, 1),
                Position = new GridPosition(x, y), IsPlayerSide = player,
                CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = 100; // Skill tests start funded; spawn balance has dedicated coverage.
            unit.Character.CharacterId += $".{player}.{x}.{y}";
            unit.Character.Resistances.MoveResistance = 0;
            unit.Character.Resistances.SuppressionResistance = 0;
            return unit;
        }
        private static CombatSession Session(params BattleUnit[] units) => new()
        {
            CombatId = "test.forest.poacher", Grid = new BattleGrid(12, 12), ActiveUnit = units[0], Units = units.ToList()
        };
        private bool Place(CombatSession session, BattleUnit caster, GridPosition cell) =>
            new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, caster, Poacher.StartingSkills[1],
                new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { caster } }, null, cell);

        [Test]
        public void AuthoredStatsCostsAndSharedElfLoot()
        {
            Assert.That(Poacher.BaseStats.MaxHealth, Is.EqualTo(190));
            Assert.That(Poacher.BaseStats.MinAttack, Is.EqualTo(35));
            Assert.That(Poacher.BaseStats.MaxAttack, Is.EqualTo(53));
            Assert.That(Poacher.BaseStats.Speed, Is.EqualTo(48));
            Assert.That(Poacher.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] { 0, 25, 55 }));
            Assert.That(Poacher.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] { 60, 30, 70 }));
            Assert.That(Poacher.LootTable.Entries.Single().ItemDefinition.Id, Is.EqualTo("item.material.elf_ear"));
            var trap = Poacher.StartingSkills[1].Effects[0].SummonedObjectDefinition;
            Assert.That(trap.TriggersOnEntry, Is.True); Assert.That(trap.BlocksMovement, Is.False);
            Assert.That(trap.Effects[0].StatusId, Is.EqualTo(StatusEffectIds.Immobilize));
            Assert.That(trap.Effects[0].DurationTurns, Is.EqualTo(2));
            Assert.That(trap.Effects[1].BaseChance, Is.EqualTo(.7f));
            Assert.That(trap.Effects[1].StatusDamageMultiplier, Is.EqualTo(.5f));
        }

        [Test]
        public void EnemySteppingOnTrapStopsPathAndConsumesIt()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 3, 1);
            var session = Session(caster, enemy);
            Assert.That(Place(session, caster, new GridPosition(2, 1)), Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(75));
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(1));
            var movement = new CombatMovementSystem(statusEffectSystem: new StatusEffectSystem());
            movement.TryMoveAlongPath(session, enemy, new[] { new GridPosition(2, 1), new GridPosition(2, 2) }, out var completed);
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(enemy.Position, Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(StatusEffectSystem.IsImmobilized(enemy), Is.True);
            Assert.That(session.SummonedObjects, Is.Empty); Assert.That(session.MapObjects, Is.Empty);
        }

        [Test]
        public void AllyDoesNotTriggerAndTrapHasNoAutomaticTurnDamage()
        {
            var caster = Unit(false, 1, 1); var ally = Unit(false, 3, 1); var enemy = Unit(true, 4, 1);
            var session = Session(caster, ally, enemy);
            Assert.That(Place(session, caster, new GridPosition(2, 1)), Is.True);
            var trap = session.SummonedObjects.Single();
            new SummonedObjectSystem().ExecuteTurnEffects(session, trap);
            Assert.That(StatusEffectSystem.IsImmobilized(enemy), Is.False);
            Assert.That(new CombatMovementSystem().TryMoveOneStep(session, ally, -1, 0), Is.True);
            Assert.That(session.SummonedObjects.Single(), Is.SameAs(trap));
            Assert.That(StatusEffectSystem.IsImmobilized(ally), Is.False);
        }

        [Test]
        public void CannotPlaceUnderUnitOrBeyondRangeAndDoesNotSpend()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 2, 1); var session = Session(caster, enemy);
            Assert.That(Place(session, caster, enemy.Position), Is.False);
            Assert.That(Place(session, caster, new GridPosition(4, 1)), Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(10000));
            Assert.That(session.SummonedObjects, Is.Empty);
        }

        [Test]
        public void PoisonBagSelectsTwoByTwoWithoutAlly()
        {
            var caster = Unit(false, 1, 1); var one = Unit(true, 4, 4); var two = Unit(true, 5, 5);
            var outside = Unit(true, 3, 4); var ally = Unit(false, 4, 5);
            var targets = new CombatTargetingService().GetTargets(Session(caster, one, two, outside, ally), caster,
                Poacher.StartingSkills[2].TargetModes[0], one.Position);
            Assert.That(targets, Is.EquivalentTo(new[] { one, two }));
        }

        [Test]
        public void AiPlacesTrapTowardEnemyAndDoesNotReplaceItsActiveTrap()
        {
            var caster = Unit(false, 1, 1); var enemy = Unit(true, 3, 1); var session = Session(caster, enemy);
            // A ranged enemy able to keep firing has no reason to enter the old
            // far-away trap. Exercise placement against an approaching melee kit.
            var melee=Object.Instantiate(Poacher.StartingSkills[0]);
            try
            {
                var modeField=typeof(SkillTargetModeData).GetField("absoluteRange",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                modeField.SetValue(melee.TargetModes[0],new SkillRangeData(RangeShape.Square,1,1));
                enemy.Character.Definition=null;enemy.Character.ClassData=null;
                enemy.Character.Skills.Clear();enemy.Character.Skills.Add(melee);
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()));
            var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(Poacher.StartingSkills[1]));
            var trap = session.SummonedObjects.Single(); Assert.That(trap.Position, Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var next), Is.True);
            Assert.That(next.Skill, Is.Not.SameAs(Poacher.StartingSkills[1]));
            Assert.That(session.SummonedObjects.Single(), Is.SameAs(trap));
            }
            finally { Object.DestroyImmediate(melee); }
        }
    }
}
