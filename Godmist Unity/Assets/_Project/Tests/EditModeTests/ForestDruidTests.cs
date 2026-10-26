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
    public sealed class ForestDruidTests
    {
        private Random.State randomState;
        private CharacterDefinition Druid => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.DruidPath);
        [SetUp] public void PreserveRandom() => randomState=Random.state;
        [TearDown] public void RestoreRandom() => Random.state=randomState;
        private BattleUnit Unit(bool player,int x,int y) {
            var u=new BattleUnit { Character=new CharacterInstanceFactory().Create(Druid,Druid.ClassData,1),
                IsPlayerSide=player,Position=new GridPosition(x,y),CurrentActionPoints=10000,MaxActionPoints=10000 };
            u.Character.ResourcePool.CurrentValue = u.Character.ResourcePool.MaxValue; // Skill tests start funded; spawn balance has dedicated coverage.
            u.Character.CharacterId += $".{player}.{x}.{y}";
            u.Character.Stats.CritChance=new Stat(0); return u;
        }
        private static CombatSession Session(params BattleUnit[] units) {
            var s=new CombatSession { CombatId="test.forest.druid",Grid=new BattleGrid(14,14),ActiveUnit=units[0],Units=units.ToList() };
            foreach(var u in units)u.WarriorState.Session=s; return s;
        }
        private BattleUnit Grow(CombatSession s,BattleUnit caster,int index,int x,int y) {
            var skill=Druid.StartingSkills[index];
            Assert.That(new SummonSystem().TrySummon(s,caster,skill,skill.Effects[0],0,new GridPosition(x,y),out var plant),Is.True);
            return plant;
        }
        private static SkillExecutionSystem Execution() => new(statusEffectSystem:new StatusEffectSystem());
        [Test] public void AuthoredStatsCostsLootAndEncounter() {
            Assert.That(Druid.BaseStats.MaxHealth,Is.EqualTo(123));
            Assert.That(Druid.BaseStats.MinAttack,Is.EqualTo(18)); Assert.That(Druid.BaseStats.MaxAttack,Is.EqualTo(27));
            Assert.That(Druid.BaseStats.Speed,Is.EqualTo(42));
            Assert.That(Druid.StartingSkills.Select(s=>s.ResourceCost),Is.EqualTo(new[]{5,40,30}));
            Assert.That(Druid.StartingSkills.Select(s=>s.ActionCost),Is.EqualTo(new[]{50,50,50}));
            Assert.That(Druid.LootTable.Entries.Single().DropChance,Is.EqualTo(.15f));
        }
        [Test] public void GrowthHasFifteenPercentCasterHpPersistsAndCannotWalk() {
            var caster=Unit(false,1,1); var s=Session(caster); caster.Character.Stats.MaxHealth=new Stat(400);
            var plant=Grow(s,caster,1,3,1);
            Assert.That(plant.Character.CurrentHealth,Is.EqualTo(60)); Assert.That(plant.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(60));
            new CharacterStatPipelineSystem().Refresh(plant.Character);
            Assert.That(plant.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(60));
            for(var i=0;i<20;i++)Assert.That(new SummonSystem().AdvanceDurationAtTurnEnd(s,plant),Is.False);
            Assert.That(new CombatMovementSystem().TryMoveAlongPath(s,plant,new[]{new GridPosition(4,1)}),Is.False);
            Assert.That(plant.Position,Is.EqualTo(new GridPosition(3,1)));
        }
        [Test] public void NatureWrathCountsOnlyLivingOwnedGrowthAndMatchesPreview() {
            var caster=Unit(false,1,1); var other=Unit(false,2,1); var enemy=Unit(true,6,1);
            var s=Session(caster,other,enemy); var skill=Druid.BasicAttackSkill;
            Assert.That(Execution().TryExecute(s,caster,skill,enemy),Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            caster.Character.Stats.MinAttack=new Stat(100); caster.Character.Stats.MaxAttack=new Stat(100);
            enemy.Character.Stats.MaxHealth=new Stat(10000);enemy.Character.CurrentHealth=10000;
            enemy.Character.Stats.MagicalDefense=new Stat(0);
            Grow(s,caster,1,3,2); var second=Grow(s,caster,2,4,2); Grow(s,other,1,3,3);
            Assert.That(SummonSystem.CountOwnedSummons(s,caster,"forest.growth"),Is.EqualTo(2));
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,enemy,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(100));
            var hp=enemy.Character.CurrentHealth;
            Assert.That(Execution().TryExecute(s,caster,skill,enemy),Is.True);
            Assert.That(hp-enemy.Character.CurrentHealth,Is.EqualTo(100));
            second.Character.LifeState=CharacterLifeState.Dead;
            Assert.That(SummonSystem.CountOwnedSummons(s,caster,"forest.growth"),Is.EqualTo(1));
        }
        [Test] public void PoisonGrowthAffectsAdjacentEnemiesOnly() {
            var caster=Unit(false,1,1);var enemy=Unit(true,5,4);var ally=Unit(false,4,5);var far=Unit(true,7,4);
            var s=Session(caster,enemy,ally,far);var plant=Grow(s,caster,1,4,4);s.ActiveUnit=plant;
            enemy.Character.Resistances.PoisonResistance=0; Random.InitState(12345);
            var pulse=plant.Character.Definition.BasicAttackSkill;
            var targets=new CombatTargetingService().GetTargets(s,plant,pulse.TargetModes[0],null);
            Assert.That(targets,Is.EquivalentTo(new[]{enemy}));
            Assert.That(Execution().TryExecute(s,plant,pulse,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}},null,null),Is.True);
            Assert.That(StatusEffectSystem.HasStatus(enemy,StatusEffectIds.Poison),Is.True);
            Assert.That(StatusEffectSystem.HasStatus(ally,StatusEffectIds.Poison),Is.False);
        }
        [Test] public void HealingGrowthHealsNinetyPercentOfInheritedAttackOnlyNearbyAllies() {
            var caster=Unit(false,1,1);var ally=Unit(false,4,5);var enemy=Unit(true,5,4);var far=Unit(false,8,4);
            caster.Character.Stats.MinAttack=new Stat(100);caster.Character.Stats.MaxAttack=new Stat(100);
            var s=Session(caster,ally,enemy,far);var plant=Grow(s,caster,2,4,4);s.ActiveUnit=plant;
            ally.Character.Stats.MaxHealth=new Stat(1000);
            ally.Character.CurrentHealth=50;enemy.Character.CurrentHealth=50;far.Character.CurrentHealth=50;
            var pulse=plant.Character.Definition.BasicAttackSkill;
            var targets=new CombatTargetingService().GetTargets(s,plant,pulse.TargetModes[0],null);
            Assert.That(targets,Has.Member(ally)); Assert.That(targets,Has.No.Member(enemy));Assert.That(targets,Has.No.Member(far));
            Assert.That(Execution().TryExecute(s,plant,pulse,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}},null,null),Is.True);
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(140));Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(50));
        }
        [Test] public void DruidAiPlacesUsefulGrowthThenUsesWrath() {
            var caster=Unit(false,1,1);var enemy=Unit(true,5,1);var s=Session(caster,enemy);
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:Execution());
            Assert.That(ai.TryExecuteNextAction(s,caster,ai.CreateTurnState(),out var action),Is.True);
            Assert.That(action.Skill,Is.SameAs(Druid.StartingSkills[1]));
            var plant=s.Summons.Single().Unit;
            Assert.That(CombatTargetingService.IsPositionWithinRange(plant.Position,enemy.Position,plant.Character.Definition.BasicAttackSkill.TargetModes[0].AbsoluteRange),Is.True);
            caster.Character.ResourcePool.CurrentValue=5;
            Assert.That(ai.TryExecuteNextAction(s,caster,ai.CreateTurnState(),out action),Is.True);
            Assert.That(action.Skill,Is.SameAs(Druid.BasicAttackSkill));
        }
        [Test] public void GrowthCanBeDestroyedByNormalCombatDamage() {
            var caster=Unit(false,1,1);var enemy=Unit(true,3,2);var s=Session(caster,enemy);
            var plant=Grow(s,caster,1,3,1); Random.InitState(12345);
            new CombatDamageSystem().ResolveAndApplyDamage(s,enemy,plant,1000,DamageType.Magical,100);
            Assert.That(plant.Character.LifeState,Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(SummonSystem.CountOwnedSummons(s,caster,"forest.growth"),Is.EqualTo(0));
        }
        [Test] public void StationaryGrowthAiDoesNotWalkWhenOutOfRange() {
            var caster=Unit(false,1,1);var enemy=Unit(true,12,12);var s=Session(caster,enemy);
            var plant=Grow(s,caster,1,3,1);s.ActiveUnit=plant;
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:Execution());
            Assert.That(ai.TryExecuteNextAction(s,plant,ai.CreateTurnState(),out _),Is.False);
            Assert.That(plant.Position,Is.EqualTo(new GridPosition(3,1)));
        }
        [Test] public void OutOfRangeAndOccupiedPlantPlacementDoNotSpendMana() {
            var caster=Unit(false,1,1);var enemy=Unit(true,4,1);var s=Session(caster,enemy);var skill=Druid.StartingSkills[1];
            foreach(var cell in new[]{enemy.Position,new GridPosition(10,1)})
                Assert.That(Execution().TryExecute(s,caster,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>(),null,cell),Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(100));Assert.That(s.Summons,Is.Empty);
        }
    }
}
