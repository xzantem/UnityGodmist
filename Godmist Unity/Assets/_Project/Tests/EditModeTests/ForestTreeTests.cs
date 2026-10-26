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
    public sealed class ForestTreeTests
    {
        private Random.State randomState;
        private CharacterDefinition Tree => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.TreePath);
        [SetUp] public void PreserveRandom() => randomState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = randomState;
        private BattleUnit Unit(bool player, int x, int y)
        {
            var unit = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(Tree, Tree.ClassData, 1),
                Position = new GridPosition(x, y), IsPlayerSide = player,
                CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = unit.Character.ResourcePool.MaxValue; // Skill tests start funded; spawn balance has dedicated coverage.
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
        public void AuthoredTreeMatchesWorkbook()
        {
            Assert.That(Tree.BaseStats.MaxHealth, Is.EqualTo(277));
            Assert.That(Tree.BaseStats.MinAttack, Is.EqualTo(27));
            Assert.That(Tree.BaseStats.MaxAttack, Is.EqualTo(31));
            Assert.That(Tree.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] {5,45,25}));
            Assert.That(Tree.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] {70,65,30}));
            Assert.That(Tree.LootTable, Is.Null);
        }
        [Test]
        public void PlagueTargetsOnlyPoisonedOpponentsInRangeAndExtendsEveryStack()
        {
            var caster = Unit(false,1,1); var enemy = Unit(true,4,1); var healthy = Unit(true,5,1);
            var far = Unit(true,12,1); var ally = Unit(false,2,1);
            var statuses = new StatusEffectSystem();
            foreach(var target in new[] {enemy,far,ally})
            {
                target.Character.Resistances.PoisonResistance = 0;
                statuses.ApplyStatus(caster,target,Godmist.Core.Constants.StatusEffectIds.Poison,1,3,1,.55f,0,"test.a");
                statuses.ApplyStatus(caster,target,Godmist.Core.Constants.StatusEffectIds.Poison,1,2,1,.55f,0,"test.b");
            }
            var skill = Tree.StartingSkills[2]; var session = Session(caster,enemy,healthy,far,ally);
            var targets = new CombatTargetingService().GetTargets(session,caster,skill.TargetModes[0],null);
            Assert.That(targets, Is.EquivalentTo(new[] {enemy}));
            var effects = enemy.Character.PassiveEffects.Where(e=>e.EffectId == Godmist.Core.Constants.StatusEffectIds.Poison).ToArray();
            var durations = effects.Select(e=>e.RemainingDuration).ToArray();
            Assert.That(new SkillExecutionSystem(statusEffectSystem:statuses).TryExecute(session,caster,skill,healthy), Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            Assert.That(new SkillExecutionSystem(statusEffectSystem:statuses).TryExecute(session,caster,skill,enemy), Is.True);
            Assert.That(effects.Select(e=>e.RemainingDuration), Is.EqualTo(durations.Select(d=>d+1)));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(75));
        }
        [Test]
        public void AiUsesPlagueOnPoisonedEnemyBeyondRootRange()
        {
            var caster=Unit(false,1,1); var enemy=Unit(true,8,1);
            var statuses=new StatusEffectSystem();
            enemy.Character.Resistances.PoisonResistance=0;
            statuses.ApplyStatus(caster,enemy,Godmist.Core.Constants.StatusEffectIds.Poison,1,3,1,.55f,0,"test.ai");
            caster.Character.ResourcePool.CurrentValue=25;
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:new SkillExecutionSystem(statusEffectSystem:statuses));
            Assert.That(ai.TryExecuteNextAction(Session(caster,enemy),caster,ai.CreateTurnState(),out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(Tree.StartingSkills[2]));
            Assert.That(caster.Position, Is.EqualTo(new GridPosition(1,1)));
        }
        [Test]
        public void ExtensionDoesNotReviveExpiredPoison()
        {
            var caster=Unit(false,1,1); var enemy=Unit(true,3,1);
            var statuses=new StatusEffectSystem(); enemy.Character.Resistances.PoisonResistance=0;
            statuses.ApplyStatus(caster,enemy,Godmist.Core.Constants.StatusEffectIds.Poison,1,1,1,.55f,0,"test.expired");
            foreach(var effect in enemy.Character.PassiveEffects) effect.RemainingDuration=0;
            Assert.That(statuses.ExtendStatusDuration(enemy,Godmist.Core.Constants.StatusEffectIds.Poison,1), Is.False);
        }
        [Test]
        public void RootsImmobilizeForTwoTurns()
        {
            var caster=Unit(false,1,1); var enemy=Unit(true,5,1);
            enemy.Character.Resistances.SuppressionResistance=0;
            enemy.Character.Resistances.MoveResistance=0;
            Assert.That(Execution().TryExecute(Session(caster,enemy),caster,Tree.StartingSkills[1],enemy), Is.True);
            Assert.That(StatusEffectSystem.IsImmobilized(enemy), Is.True);
            var lifecycle=new PassiveEffectLifecycleSystem();
            lifecycle.ProcessTurnEnd(enemy); Assert.That(StatusEffectSystem.IsImmobilized(enemy), Is.True);
            lifecycle.ProcessTurnEnd(enemy); Assert.That(StatusEffectSystem.IsImmobilized(enemy), Is.False);
        }
    }
}
