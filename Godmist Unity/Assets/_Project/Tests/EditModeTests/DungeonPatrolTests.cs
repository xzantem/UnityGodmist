using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Core.Serialization;
using NUnit.Framework;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class DungeonPatrolTests
    {
        static DungeonTraversalPosition At(int x,int y=0,int level=0)=>new(new DungeonSurfacePosition(new Vector2Int(x,y),level));
        static DungeonFloorMap Room(int width=8,int height=6)
        {var map=new DungeonFloorMap();for(var y=0;y<height;y++)for(var x=0;x<width;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");return map;}
        [Test]
        public void TerritoriesCoverRoomWithoutOverlapAndEveryLoopEdgeIsTraversable()
        {
            var map=Room();var starts=new[]{At(0),At(1),At(2)};
            var routes=DungeonPatrolPlanner.Build(map,"room",starts,_=>true);
            var all=new HashSet<DungeonTraversalPosition>();
            foreach(var route in routes)
            {
                Assert.That(route,Is.Not.Empty);var territory=route.ToHashSet();
                Assert.That(all.Overlaps(territory),Is.False);all.UnionWith(territory);
                for(var i=0;i<route.Count;i++)Assert.That(map.GetTraversalNeighbours(route[i]),Does.Contain(route[(i+1)%route.Count]));
            }
            for (var y = 0; y < 6; y++) for (var x = 0; x < 8; x++)
                Assert.That(all.Any(p => DungeonExplorationPathfinder.TryFindPath(map, At(x,y), p, out var path) && path.Count <= 5), Is.True);
            var repeated=DungeonPatrolPlanner.Build(map,"room",starts,_=>true);
            for(var i=0;i<routes.Count;i++)Assert.That(repeated[i],Is.EqualTo(routes[i]));
        }
        [Test]
        public void GuardOutsideRoomDoesNotSeedAnInvalidTerritory()
        {
            var map = Room(4, 1);
            var routes = DungeonPatrolPlanner.Build(map, "room", new[] { At(20), At(0) }, _ => true);
            Assert.That(routes[0], Is.Empty);
            Assert.That(routes[1].ToHashSet(), Is.EquivalentTo(new[] { At(0), At(1), At(2), At(3) }));
        }
        [Test]
        public void ClosedDoorAndAnotherRoomAreNotPatrolled()
        {
            var map=Room(5,1);map.TrySetDoorState(new DungeonCellEdge(new Vector2Int(2,0),new Vector2Int(3,0)),false);
            map.TryAddFloorCell(new Vector2Int(-1,0),"outside");
            var routes=DungeonPatrolPlanner.Build(map,"room",new[]{At(0)},_=>true);
            Assert.That(routes[0].ToHashSet(),Is.EquivalentTo(new[]{At(0),At(1),At(2)}));
        }
        [Test]
        public void StairsAndUpperSurfaceAreCoveredWithoutProjectionShortcut()
        {
            var map=Room(1,1);var upper=new DungeonSurfacePosition(new Vector2Int(2,0),1);map.TryAddFloorCell(upper,"room");
            map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero),upper,new[]{Vector2Int.zero,Vector2Int.right,new Vector2Int(2,0)});
            var route=DungeonPatrolPlanner.Build(map,"room",new[]{At(0)},_=>true)[0];
            Assert.That(route.Any(p=>p.IsOnStairs),Is.True);Assert.That(route,Does.Contain(At(2,0,1)));
            for(var i=0;i<route.Count;i++)Assert.That(map.GetTraversalNeighbours(route[i]),Does.Contain(route[(i+1)%route.Count]));
        }
        [Test]
        public void JsonPreservesRoutePausePhaseAndObservationAndLegacyIsAccepted()
        {
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(Room());
            var state=new DungeonPatrolState{NextPoint=1,WaitRemaining=1.25f,Stops=7,ObservedCharacterId="player"};state.Route.AddRange(new[]{At(0),At(1)});state.StopIndices.UnionWith(new[]{0,1});floor.Patrols.Add("guard",state);
            var save=GameSaveData.CreateNew("patrol");save.ActiveDungeon=new DungeonSaveData();save.ActiveDungeon.FloorDeltas.Add(new DungeonFloorDeltaSaveData{Patrols=DungeonPatrolPersistence.Capture(floor)});
            var serializer=new GameSaveJsonSerializer();Assert.That(serializer.TryDeserialize(serializer.Serialize(save),out var decoded),Is.True);
            var restored=new DungeonFloorInstance(0);restored.TrySetMap(Room());Assert.That(DungeonPatrolPersistence.Restore(restored,decoded.ActiveDungeon.FloorDeltas[0].Patrols),Is.True);
            var next=restored.Patrols["guard"];Assert.That(next.StopIndices, Is.EquivalentTo(state.StopIndices));Assert.That(next.Route,Is.EqualTo(state.Route));Assert.That(next.NextPoint,Is.EqualTo(1));Assert.That(next.WaitRemaining,Is.EqualTo(1.25f));Assert.That(next.ObservedCharacterId,Is.EqualTo("player"));
            state.Arrive("guard");next.Arrive("guard");Assert.That(next.WaitRemaining,Is.EqualTo(state.WaitRemaining));Assert.That(next.Stops,Is.EqualTo(state.Stops));
            Assert.That(DungeonPatrolPersistence.Restore(restored,null),Is.True);
        }
        [Test]
        public void EveryStopPausesButTransitDoesNotAndLoopRepeats()
        {
            var state = new DungeonPatrolState();
            state.Route.AddRange(new[] { At(0), At(1), At(2), At(1) });
            state.StopIndices.UnionWith(new[] { 0, 2 });
            for (var loop = 0; loop < 3; loop++)
                for (var i = 0; i < 4; i++)
                {
                    Assert.That(state.NextPoint, Is.EqualTo(i));
                    state.Arrive("guard");
                    if (i % 2 == 0) Assert.That(state.WaitRemaining, Is.InRange(2f, 3f));
                    else Assert.That(state.WaitRemaining, Is.Zero);
                }
        }
        [Test]
        public void StopCountAdaptsAndEveryReversalHasAPause()
        {
            DungeonPatrolPlanner.Build(Room(1,1), "room", new[] { At(0) }, _ => true, out var tiny);
            var routes = DungeonPatrolPlanner.Build(Room(12,12), "room", new[] { At(0) }, _ => true, out var large);
            Assert.That(tiny[0].Count, Is.EqualTo(1));
            Assert.That(large[0].Count, Is.GreaterThan(3));
            Assert.That(large[0].Count, Is.LessThan(routes[0].Count));
            var route = routes[0];
            for (var i = 0; i < route.Count; i++)
                if (route[(i + route.Count - 1) % route.Count] == route[(i + 1) % route.Count])
                    Assert.That(large[0], Does.Contain(i));
            DungeonPatrolPlanner.Build(Room(12,12), "room", new[] { At(0) }, _ => true, out var repeated);
            Assert.That(repeated[0], Is.EqualTo(large[0]));
        }
        [Test]
        public void LegacyStopsAreMissingAndInvalidStopIndexIsRejected()
        {
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(Room());
            var state = new DungeonPatrolState(); state.Route.Add(At(0)); state.StopIndices.Add(0);
            floor.Patrols.Add("guard", state);
            var saved = DungeonPatrolPersistence.Capture(floor); saved[0].StopIndices = null;
            Assert.That(DungeonPatrolPersistence.Restore(floor, saved), Is.True);
            Assert.That(floor.Patrols["guard"].StopIndices, Is.Empty);
            saved[0].StopIndices = new List<int> { 1 };
            Assert.That(DungeonPatrolPersistence.Restore(floor, saved), Is.False);
            Assert.That(floor.Patrols["guard"].StopIndices, Is.Empty);
        }
        [Test]
        public void CorruptPatrolIsRejectedWithoutOverwritingExistingState()
        {
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(Room());var state=new DungeonPatrolState();state.Route.Add(At(0));floor.Patrols.Add("guard",state);
            var save=DungeonPatrolPersistence.Capture(floor);save[0].WaitRemaining=float.NaN;
            Assert.That(DungeonPatrolPersistence.Restore(floor,save),Is.False);Assert.That(floor.Patrols["guard"],Is.SameAs(state));
        }
    }
}
