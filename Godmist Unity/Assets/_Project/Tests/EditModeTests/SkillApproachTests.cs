using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public class SkillApproachTests
    {
        static BattleUnit Unit(int x = 0, int y = 1)
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
            var character = new CharacterInstanceFactory().Create(def);
            character.SkillTreeProgress.Clear();
            return new BattleUnit { Character = character, IsPlayerSide = true, Position = new GridPosition(x,y),
                MaxActionPoints = 2000, CurrentActionPoints = 2000 };
        }
        static CombatSession World(BattleUnit actor) => new() { Grid = new BattleGrid(8,3), ActiveUnit = actor, Units = new() { actor } };

        [Test]
        public void SearchReservesBudgetAndNeverMovesOrSpendsOnPreview()
        {
            var actor = Unit(); var world = World(actor); var system = new CombatMovementSystem();
            Assert.That(system.TryFindSkillApproach(world, actor, 2000,
                (p,c) => p.X == 4 && 2000-c >= 1000, null, out var path, out var cost), Is.True);
            Assert.That(path.Last(), Is.EqualTo(new GridPosition(4,1))); Assert.That(cost, Is.EqualTo(1000));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0,1))); Assert.That(actor.CurrentActionPoints, Is.EqualTo(2000));
            Assert.That(system.TryFindSkillApproach(world, actor, 1999,
                (p,c) => p.X == 4 && 1999-c >= 1000, null, out _, out _), Is.False);
        }

        [Test]
        public void SearchChoosesLowerApInsteadOfFewestSteps()
        {
            var actor = Unit(0,0); var world = World(actor); world.Grid = new BattleGrid(6,2);
            var direct = Enumerable.Range(0,6).Select(x=>new GridPosition(x,0)).ToArray();
            var detour = new[] { actor.Position }.Concat(Enumerable.Range(0,6).Select(x=>new GridPosition(x,1)))
                .Concat(new[] { new GridPosition(5,0) }).ToArray();
            var graph = new Dictionary<GridPosition,List<GridPosition>>();
            foreach(var route in new[] { direct,detour })
                for(var i=0;i<route.Length-1;i++)
                { if(!graph.TryGetValue(route[i],out var next))graph[route[i]]=next=new(); next.Add(route[i+1]); }
            world.Grid.TraversalNeighbours = p => graph.TryGetValue(p,out var next) ? next : Array.Empty<GridPosition>();
            for(var i=1;i<direct.Length;i++)world.Grid.SetWallEdge(direct[i-1],direct[i],DungeonWallType.Low);
            for(var i=1;i<direct.Length-1;i++)world.Grid.SetDifficultTerrain(direct[i]);
            Assert.That(CombatMovementSystem.CalculatePathCost(world,actor.Position,0,direct.Skip(1).ToArray()),Is.EqualTo(2900));
            Assert.That(new CombatMovementSystem().TryFindSkillApproach(world, actor, 4000,
                (p,_) => p == direct.Last(), null, out var path, out var cost), Is.True);
            Assert.That(path, Is.EqualTo(detour.Skip(1))); Assert.That(cost, Is.EqualTo(2800));
        }

        [Test]
        public void SearchRejectsOccupiedAndInvisibleRouteAndStationaryActor()
        {
            var actor = Unit(0,0); var world = World(actor); world.Grid = new BattleGrid(4,1);
            var blocker = Unit(1,0); world.Units.Add(blocker);
            var system = new CombatMovementSystem();
            Assert.That(system.TryFindSkillApproach(world, actor, int.MaxValue, (p,_)=>p.X==3, null,out _,out _),Is.False);
            world.Units.Remove(blocker);
            Assert.That(system.TryFindSkillApproach(world, actor, int.MaxValue, (p,_)=>p.X==3, p=>p.X!=2,out _,out _),Is.False);
            actor.IsStationarySummon = true;
            Assert.That(system.TryFindSkillApproach(world, actor, int.MaxValue, (p,_)=>p.X==3, null,out _,out _),Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void RealCutUsesStandardRangeAndReservesItsActualCost(bool exploration)
        {
            var actor = Unit(); var enemy = Unit(4,1); enemy.IsPlayerSide = false;
            var world = World(actor); world.Units.Add(enemy);
            var driver = (CombatExecutionDriver)Activator.CreateInstance(typeof(CombatExecutionDriver), true);
            driver.InitializeTargetingPreview(world);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            var resource = actor.Character.ResourcePool.CurrentValue;
            var picks = new Dictionary<int,GridPosition?>();
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,picks,null,null,exploration,null,out var path,out var cost),Is.True);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(CombatTargetingService.IsPositionWithinRange(path.Last(),enemy.Position,skill.TargetModes[0].AbsoluteRange),Is.True);
            var skillCost = CombatActionCostCalculator.CalculateSkillCostForTarget(actor,skill,enemy);
            if (!exploration) Assert.That(cost+skillCost,Is.LessThanOrEqualTo(actor.CurrentActionPoints));
            actor.CurrentActionPoints = skillCost;
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,picks,null,null,exploration,null,out _,out _),Is.EqualTo(exploration));
            Assert.That(actor.Position,Is.EqualTo(new GridPosition(0,1)));
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(skillCost));
            Assert.That(actor.Character.ResourcePool.CurrentValue,Is.EqualTo(resource));
            Assert.That(picks,Is.Empty);
        }

        [Test]
        public void MovingTargetReplansFromCurrentActorAndAcceptsAlreadyInRange()
        {
            var actor = Unit(); var enemy = Unit(4,1); enemy.IsPlayerSide = false;
            var world = World(actor); world.Units.Add(enemy);
            var driver = (CombatExecutionDriver)Activator.CreateInstance(typeof(CombatExecutionDriver), true);
            driver.InitializeTargetingPreview(world);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,null,null,null,true,null,out var first,out _),Is.True);
            actor.Position = new GridPosition(2,1);
            enemy.Position = new GridPosition(6,1);
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,null,null,null,true,null,out var updated,out _),Is.True);
            Assert.That(updated.Last(),Is.Not.EqualTo(first.Last()));
            Assert.That(CombatTargetingService.IsPositionWithinRange(updated.Last(),enemy.Position,skill.TargetModes[0].AbsoluteRange),Is.True);
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,null,null,null,true,p=>p.X<4,out _,out _),Is.False);
            enemy.Position = new GridPosition(3,1);
            Assert.That(driver.TryPlanSkillApproach(skill,0,enemy.Position,null,null,null,true,null,out var alreadyInRange,out var cost),Is.True);
            Assert.That(alreadyInRange,Is.Empty); Assert.That(cost,Is.Zero);
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(2000));
        }

        [Test]
        public void RealSkillRejectsWrongRelationAndAnUnreachableSideOfAFullWall()
        {
            var actor = Unit(); var target = Unit(4,1); var world = World(actor); world.Units.Add(target);
            var driver = (CombatExecutionDriver)Activator.CreateInstance(typeof(CombatExecutionDriver),true);
            driver.InitializeTargetingPreview(world);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            Assert.That(driver.TryPlanSkillApproach(skill,0,target.Position,null,null,null,true,null,out _,out _),Is.False);
            target.IsPlayerSide=false;
            for(var y=0;y<world.Grid.Height;y++)world.Grid.SetWallEdge(new GridPosition(1,y),new GridPosition(2,y),DungeonWallType.Full);
            Assert.That(driver.TryPlanSkillApproach(skill,0,target.Position,null,null,null,true,null,out _,out _),Is.False);
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(2000)); Assert.That(actor.Position,Is.EqualTo(new GridPosition(0,1)));
        }

        [Test]
        public void SmoothedAndCellByCellRoutesKeepTheSamePathMarkerDensity()
        {
            var root = new UnityEngine.GameObject("approach path");
            try
            {
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                var cells = Enumerable.Range(1,5).Select(x=>new GridPosition(x,0)).ToArray();
                view.ShowMovementPreview(cells,new GridPosition(0,0),0,UnityEngine.Color.yellow,showCost:false);
                var expected = root.GetComponentsInChildren<UnityEngine.SpriteRenderer>()
                    .Where(r=>r.name=="Combat pixel path gem").Select(r=>r.transform.localPosition).ToArray();
                view.ClearMovementPreview();
                view.ShowMovementPreview(new[]{cells.Last()},new GridPosition(0,0),0,UnityEngine.Color.yellow,showCost:false);
                var actual = root.GetComponentsInChildren<UnityEngine.SpriteRenderer>()
                    .Where(r=>r.name=="Combat pixel path gem").Select(r=>r.transform.localPosition).ToArray();
                Assert.That(actual,Is.EquivalentTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ProjectionPreservesEnvironmentRulesAndLiveActorIdentity()
        {
            var actor = Unit(); var world = World(actor); var other = Unit(5,1); world.Units.Add(other);
            world.EnvironmentBlocksLineOfSight = (_,__) => true;
            var projected = world.ProjectActionQuery(new GridPosition(2,1),500);
            Assert.That(projected.ActiveUnit,Is.Not.SameAs(actor));
            Assert.That(projected.ActiveUnit.Character,Is.SameAs(actor.Character));
            Assert.That(projected.Units[0],Is.SameAs(projected.ActiveUnit));
            Assert.That(projected.Units[1],Is.SameAs(other));
            Assert.That(projected.EnvironmentBlocksLineOfSight,Is.SameAs(world.EnvironmentBlocksLineOfSight));
            Assert.That(world.Units[0],Is.SameAs(actor)); Assert.That(actor.CurrentActionPoints,Is.EqualTo(2000));
        }
    }
}
