using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Godmist.Tests.EditMode
{
    public class DungeonTrapCombatTests
    {
        private SpikeTrapDungeonObjectDefinition definition;
        private DungeonFloorInstance floor;
        private DungeonTrapSystem system;
        [SetUp] public void Setup()
        {
            definition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();
            floor = new DungeonFloorInstance(0);var map = new DungeonFloorMap();
            for(int x=0;x<5;x++)for(int y=0;y<3;y++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
            floor.TrySetMap(map);system = new DungeonTrapSystem(floor,1);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(definition);
        private void Set(string field, object value) => typeof(SpikeTrapDungeonObjectDefinition)
            .GetField("<"+field+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(definition,value);
        private static BattleUnit Unit(string id, bool player=true) => new() { IsPlayerSide=player,CurrentActionPoints=20,
            Character=new CharacterInstance { CharacterId=id,CurrentHealth=100,LifeState=CharacterLifeState.Alive,
                Stats=new CharacterStats { Speed=new Stat(20),PhysicalDefense=new Stat(0),MagicalDefense=new Stat(0) },
                Resistances=new ResistanceSet { BleedResistance=.5f,PoisonResistance=.5f },ShieldPool=new ShieldPool() } };
        private SpikeTrapDungeonObjectInstance Trap(string id,int x,string group=null)
        { var trap=new SpikeTrapDungeonObjectInstance(id,definition,new Vector2Int(x,0),group);Assert.That(floor.TryAddObject(trap),Is.True);return trap; }
        [Test] public void Group_HitsBothSidesAcrossSessionsOnlyOnce()
        {
            var a=Trap("a",1,"group");var b=Trap("b",2,"group");var player=Unit("player");var enemy=Unit("enemy",false);
            var occupants=new[] { new DungeonTrapOccupant(player,new CombatSession(),new Vector2Int(1,0)),new DungeonTrapOccupant(enemy,new CombatSession(),new Vector2Int(2,0)) };
            Assert.That(system.EnterCell(new Vector2Int(1,0),occupants),Is.True);
            Assert.That(player.Character.CurrentHealth,Is.EqualTo(90));Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(90));
            Assert.That(a.IsTriggered && b.IsTriggered,Is.True);
            Assert.That(system.EnterCell(new Vector2Int(2,0),occupants),Is.False);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(90));
        }
        [Test] public void Poison_TicksEverySixSecondsAndNeverTicksForCombatOccupant()
        {
            Set("DealsImmediateDamage",false);Set("StatusId",StatusEffectIds.Poison);Set("StatusDamage",4);Set("Duration",2);
            Trap("poison",1);var unit=Unit("player");var outside=new[] {new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))};
            system.EnterCell(new Vector2Int(1,0),outside);
            system.AdvanceExploration(5f,outside);Assert.That(unit.Character.CurrentHealth,Is.EqualTo(100));
            system.AdvanceExploration(1f,outside);Assert.That(unit.Character.CurrentHealth,Is.EqualTo(96));
            system.AdvanceExploration(60f,new[] {new DungeonTrapOccupant(unit,new CombatSession(),new Vector2Int(1,0))});
            Assert.That(unit.Character.CurrentHealth,Is.EqualTo(96));
            system.AdvanceExploration(6f,outside);Assert.That(unit.Character.CurrentHealth,Is.EqualTo(92));
            Assert.That(StatusEffectSystem.HasStatus(unit,StatusEffectIds.Poison),Is.False);
        }
        [Test] public void Movement_StopsAtLethalTrapAndPaysOnlyCompletedStep()
        {
            Set("Damage",200);Trap("lethal",1);var unit=Unit("player");var session=new CombatSession {Grid=new BattleGrid(5,3)};session.Units.Add(unit);session.ActiveUnit=unit;
            session.UnitEnteredCell=u=>system.EnterCell(new Vector2Int(u.Position.X,u.Position.Y),new[] {new DungeonTrapOccupant(u,session,new Vector2Int(u.Position.X,u.Position.Y))});
            new CombatMovementSystem().TryMoveAlongPath(session,unit,new[] {new GridPosition(1,0),new GridPosition(2,0)},out var completed);
            Assert.That(completed.Count,Is.EqualTo(1));Assert.That(unit.CurrentActionPoints,Is.EqualTo(19));
            Assert.That(unit.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }
        [Test] public void Teleport_OnlyTriggersDestination()
        {
            var middle=Trap("middle",1);var end=Trap("end",2);var unit=Unit("player");var session=new CombatSession {Grid=new BattleGrid(5,3)};session.Units.Add(unit);
            session.UnitEnteredCell=u=>system.EnterCell(new Vector2Int(u.Position.X,u.Position.Y),new[] {new DungeonTrapOccupant(u,session,new Vector2Int(u.Position.X,u.Position.Y))});
            Assert.That(new CombatTeleportSystem().TryTeleport(session,unit,unit,new GridPosition(2,0),false),Is.True);
            Assert.That(middle.IsTriggered,Is.False);Assert.That(end.IsTriggered,Is.True);
        }
        [Test] public void Risk_IsReadOnlyCountsGroupOnceAndRejectsLethalRoute()
        {
            var a=Trap("a",1,"group");Trap("b",2,"group");var unit=Unit("player");
            Assert.That(system.EstimateRisk(unit,new[] {new Vector2Int(1,0),new Vector2Int(2,0)}),Is.EqualTo(10));
            Assert.That(a.IsTriggered,Is.False);Assert.That(unit.Character.CurrentHealth,Is.EqualTo(100));
            unit.Character.CurrentHealth=10;Assert.That(system.EstimateRisk(unit,new[] {new Vector2Int(1,0)}),Is.EqualTo(float.PositiveInfinity));
        }
        [Test] public void DefenseAndShieldStillApplyWithoutDodgeOrCrit()
        {
            Trap("spikes",1);var unit=Unit("player");unit.Character.Stats.PhysicalDefense=new Stat(10);unit.Character.ShieldPool.AddLayer(3);
            system.EnterCell(new Vector2Int(1,0),new[] {new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))});
            Assert.That(unit.Character.CurrentHealth,Is.EqualTo(98));Assert.That(unit.Character.ShieldPool.TotalShield,Is.Zero);
        }
        [Test] public void Weakening_ReducesDamageAndExpiresAfterTwoTurns()
        {
            var source=Unit("source");var target=Unit("target");var status=new StatusEffectSystem();
            Assert.That(status.ApplyStatus(null,source,StatusEffectIds.Weakening,1f,2,1,0,0,"trap"),Is.True);
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            new CombatDamageSystem().ResolveAndApplyDamage(session,source,target,100,DamageType.Direct,1f);
            Assert.That(target.Character.CurrentHealth,Is.EqualTo(20));
            status.ProcessTurnEnd(source);Assert.That(StatusEffectSystem.HasStatus(source,StatusEffectIds.Weakening),Is.True);
            status.ProcessTurnEnd(source);Assert.That(StatusEffectSystem.HasStatus(source,StatusEffectIds.Weakening),Is.False);
        }
        [Test] public void PoisonResistance_PreventsApplicationAndRisk()
        {
            Set("DealsImmediateDamage",false);Set("StatusId",StatusEffectIds.Poison);Set("StatusDamage",10);
            Trap("gas",1);var unit=Unit("player");unit.Character.Resistances.PoisonResistance=1f;
            Assert.That(system.EstimateRisk(unit,new[]{new Vector2Int(1,0)}),Is.Zero);
            system.EnterCell(new Vector2Int(1,0),new[]{new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))});
            Assert.That(StatusEffectSystem.HasStatus(unit,StatusEffectIds.Poison),Is.False);
        }
        [Test] public void PartialExplorationTick_IsNotResetByCombatFocus()
        {
            Set("DealsImmediateDamage",false);Set("StatusId",StatusEffectIds.Poison);Set("StatusDamage",4);
            Trap("gas",1);var unit=Unit("player");var outside=new[]{new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))};
            system.EnterCell(new Vector2Int(1,0),outside);system.AdvanceExploration(5f,outside);
            system.AdvanceExploration(12f,new[]{new DungeonTrapOccupant(unit,new CombatSession(),new Vector2Int(1,0))});
            system.AdvanceExploration(1f,outside);Assert.That(unit.Character.CurrentHealth,Is.EqualTo(96));
        }
        [Test] public void ExplorationClock_SurvivesOwnerReplacementAndDoesNotDoubleTick()
        {
            Set("DealsImmediateDamage",false);Set("StatusId",StatusEffectIds.Poison);Set("StatusDamage",4);
            Trap("gas",1);var unit=Unit("player");var outside=new[]{new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))};
            system.EnterCell(new Vector2Int(1,0),outside);system.AdvanceExploration(5f,outside);
            var city = new ExplorationStatusEffectSystem();
            city.Advance(1f,new[]{outside[0],outside[0]});
            Assert.That(unit.Character.CurrentHealth,Is.EqualTo(96));
            Assert.That(unit.Character.ExplorationStatusElapsedSeconds,Is.Zero);
        }
        [Test] public void InactiveTrapCannotTrigger()
        {
            var trap=Trap("inactive",1);trap.TryDeactivate();var unit=Unit("player");
            Assert.That(system.EnterCell(new Vector2Int(1,0),new[]{new DungeonTrapOccupant(unit,null,new Vector2Int(1,0))}),Is.False);
            Assert.That(unit.Character.CurrentHealth,Is.EqualTo(100));
        }

        [Test] public void Ai_ChoosesSafeDetourEvenWhenLonger()
        {
            Trap("spikes",1);var enemy=Unit("enemy",false);var player=Unit("player");player.Position=new GridPosition(3,0);
            var session=new CombatSession {Grid=new BattleGrid(5,3),ActiveUnit=enemy};session.Units.Add(enemy);session.Units.Add(player);
            session.EvaluateTrapRisk=(unit,path)=>system.EstimateRisk(unit,System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(path,p=>new Vector2Int(p.X,p.Y))));
            enemy.CurrentActionPoints=1;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session,enemy,player),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(0,1)));
        }
    }
}
