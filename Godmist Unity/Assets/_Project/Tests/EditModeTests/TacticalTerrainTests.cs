using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class TacticalTerrainTests
    {
        private static BattleUnit Unit() => new() { IsPlayerSide = true, CurrentActionPoints = 3000,
            Character = new CharacterInstance { LifeState = CharacterLifeState.Alive, CurrentHealth = 100 } };
        [Test] public void TerrainAndWallAddToOnlyTheEnteredStep()
        {
            var grid = new BattleGrid(3, 1); var a = new GridPosition(0,0); var b = new GridPosition(1,0);
            grid.SetDifficultTerrain(b); grid.SetWallEdge(a,b,DungeonWallType.Low);
            var session = new CombatSession { Grid = grid };
            Assert.That(CombatMovementSystem.CalculatePathCost(session,a,2,new[]{b}),Is.EqualTo(600));
            Assert.That(CombatMovementSystem.CalculatePathCost(session,a,2,new[]{b,new GridPosition(2,0)}),Is.EqualTo(1000));
        }
        [Test] public void PlayerPrefersSafeRouteOverCheaperTrap()
        {
            var unit=Unit();var session=new CombatSession {Grid=new BattleGrid(3,2)};session.Units.Add(unit);
            session.EvaluateTrapRisk=(u,path)=>{foreach(var cell in path) if(cell==new GridPosition(1,0))return 5;return 0;};
            var path=new CombatMovementSystem().CalculateReachablePaths(session,unit)[new GridPosition(2,0)];
            Assert.That(path,Has.No.Member(new GridPosition(1,0)));Assert.That(path.Count,Is.EqualTo(4));
        }
        [Test] public void PlayerChoosesCheapestSafeTerrainRoute()
        {
            var unit=Unit();var session=new CombatSession {Grid=new BattleGrid(3,2)};session.Units.Add(unit);
            session.Grid.SetDifficultTerrain(new GridPosition(1,0));
            var path=new CombatMovementSystem().CalculateReachablePaths(session,unit)[new GridPosition(2,1)];
            Assert.That(path,Has.No.Member(new GridPosition(1,0)));
            Assert.That(CombatMovementSystem.CalculatePathCost(session,unit.Position,0,path),Is.EqualTo(600));
        }
        [Test] public void PlayerCanChooseLethalOnlyRoute()
        {
            var unit=Unit();var session=new CombatSession {Grid=new BattleGrid(3,1)};session.Units.Add(unit);
            session.EvaluateTrapRisk=(u,path)=>float.PositiveInfinity;
            Assert.That(new CombatMovementSystem().CalculateReachablePaths(session,unit).ContainsKey(new GridPosition(2,0)),Is.True);
        }
        [Test] public void EqualCostPathMinimizesTurns()
        {
            var unit=Unit();var session=new CombatSession {Grid=new BattleGrid(3,3)};session.Units.Add(unit);
            var path=new CombatMovementSystem().CalculateReachablePaths(session,unit)[new GridPosition(2,2)];
            Assert.That(CombatMovementSystem.CountPathTurns(unit.Position,path),Is.EqualTo(1));
        }
        [Test] public void ExplorationTerrainSlowsTheSharedCharacterView()
        {
            var root = new UnityEngine.GameObject("terrain test", typeof(UnityEngine.Grid));
            var tiles = new UnityEngine.GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
            tiles.transform.SetParent(root.transform);
            var mapView = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
            typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTilemap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(mapView,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
            var actor = new UnityEngine.GameObject("actor");var view=actor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            try
            {
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();map.TryAddFloorCell(new UnityEngine.Vector2Int(0,0),"room");map.SetDifficultTerrain(new UnityEngine.Vector2Int(0,0));
                view.ConfigureTerrain(map,mapView);view.SetPosition(new UnityEngine.Vector2(.2f,.2f));view.Move(new UnityEngine.Vector2(.4f,0));
                Assert.That(view.Position.x,Is.EqualTo(.4f).Within(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void LowWallCrossingLocksInputAndCompletesAtDestination()
        {
            var root=new UnityEngine.GameObject("grid",typeof(UnityEngine.Grid));
            var tiles=new UnityEngine.GameObject("tiles",typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
            var mapView=root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTilemap",flags).SetValue(mapView,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
            var actor=new UnityEngine.GameObject("actor");var view=actor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            try
            {
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();var a=new UnityEngine.Vector2Int(0,0);var b=new UnityEngine.Vector2Int(1,0);
                map.TryAddFloorCell(a,"room");map.TryAddFloorCell(b,"room");map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(a,b),DungeonWallType.Low);
                view.ConfigureTerrain(map,mapView);view.SetPosition(new UnityEngine.Vector2(.95f,.5f));view.CanCrossTo=cell=>false;view.Move(new UnityEngine.Vector2(.1f,0));
                Assert.That(view.IsCrossingLowWall,Is.False);view.CanCrossTo=cell=>true;view.Move(new UnityEngine.Vector2(.1f,0));
                Assert.That(view.IsCrossingLowWall,Is.True);view.Move(new UnityEngine.Vector2(-1,0));Assert.That(view.Position.x,Is.EqualTo(.95f));
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("crossingElapsed",flags).SetValue(view,.6f);
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetMethod("Update",flags).Invoke(view,null);
                Assert.That(view.IsCrossingLowWall,Is.False);Assert.That(view.Position.x,Is.EqualTo(1.5f).Within(.001f));
                // Reverse immediately, without leaving the landing cell first.
                view.Move(new UnityEngine.Vector2(-.6f,0));
                Assert.That(view.IsCrossingLowWall,Is.True);
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("crossingElapsed",flags).SetValue(view,.6f);
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetMethod("Update",flags).Invoke(view,null);
                Assert.That(view.Position.x,Is.EqualTo(.5f).Within(.001f));
            }
            finally {UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void TerrainDoesNotBlockDisplacement()
        {
            var grid=new BattleGrid(2,1);var a=new GridPosition(0,0);var b=new GridPosition(1,0);grid.SetDifficultTerrain(b);
            Assert.That(grid.CanDisplaceBetween(a,b),Is.True);Assert.That(grid.GetMovementSurcharge(a,b),Is.EqualTo(1));
        }
    }
}
