using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonExplorationPathfinderTests
    {
        [Test]
        public void TryFindPath_UsesOpenRouteAroundWall()
        {
            var map = CreateSquareMap();

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.True);
            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(1, 0)
            }));
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenWallBlocksOnlyRoute()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenTargetIsOutsideMap()
        {
            var map = CreateSquareMap();

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(3, 3),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void PlanningThroughOpenableDoorDoesNotOpenItOrChangeDefaultMovement()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.right, "room");
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            map.TrySetDoorState(edge, false);
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var end = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _, canOpenDoor: e => e.CellEdge.Equals(edge)), Is.True);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _), Is.False);
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.right), Is.False);
            map.TrySetDoorState(edge, true);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _), Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void DoorPermissionDoesNotBypassWallsOrProgressionGates(bool wall)
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.right, "room");
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            map.TrySetDoorState(edge, false);
            if (wall) map.TryAddWall(Vector2Int.zero, Vector2Int.right);
            else map.TrySetProgressionGateState(edge, false);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)),
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right)), out _, canOpenDoor: _ => true), Is.False);
        }

        [Test]
        public void TraversalPathAvoidsOccupiedObstacleCellAndRejectsBlockedTarget()
        {
            var map = CreateSquareMap();
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var end = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.one));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out var path,
                p => p.Surface.Cell != Vector2Int.right), Is.True);
            Assert.That(path[1].Surface.Cell, Is.EqualTo(Vector2Int.up));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _, p => p != end), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void NavigationOpensOnlyDirectDoorsAtTheNextStep(bool leverOnly)
        {
            var root = new GameObject("navigation", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
            try
            {
                var mode = new UnityEditor.SerializedObject(definition);
                mode.FindProperty("<InteractionMode>k__BackingField").enumValueIndex = leverOnly ? (int)Godmist.Core.Enums.DoorInteractionMode.LeverOnly : (int)Godmist.Core.Enums.DoorInteractionMode.DirectOnly;
                mode.ApplyModifiedPropertiesWithoutUndo();
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(Vector2Int.right, Vector2Int.right * 2));
                floor.TryAddObject(door); floor.TrySetDoorOpen("door", false);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var mapView = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(mapView, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.ConfigureTerrain(map, mapView, floor); actor.SetPosition(mapView.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, mapView, null);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var open = controller.GetType().GetMethod("OpenNextDoor", flags);
                var next = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right * 2));
                open.Invoke(controller, new object[] { actor, next });
                Assert.That(door.IsOpen, Is.False, "Do not remotely open a door beyond the next edge.");
                actor.SetPosition(mapView.CellToWorldCenter(Vector2Int.right));
                open.Invoke(controller, new object[] { actor, next });
                Assert.That(door.IsOpen, Is.EqualTo(!leverOnly));
                Assert.That(map.CanMoveBetween(Vector2Int.right, Vector2Int.right * 2), Is.EqualTo(!leverOnly));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void RouteKeepsWaypointUntilItsCenterAndEndsAtExactFreePosition()
        {
            var root = new GameObject("navigation", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();for(var x=0;x<3;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
                var floor = new DungeonFloorInstance(0);floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap",flags).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();actor.transform.SetParent(root.transform);actor.ConfigureTerrain(map,view,floor);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();controller.ConfigureTraversal(floor,view,null);
                var next = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
                var route = new System.Collections.Generic.Queue<DungeonTraversalPosition>();route.Enqueue(next);
                actor.SetPosition(new Vector2(1.1f,.5f));
                var advance = controller.GetType().GetMethod("AdvanceRoute",flags);
                var final = new Vector2(1.75f,.7f);
                advance.Invoke(controller,new object[]{actor,route,(Vector2?)final,0f});
                Assert.That(route.Count,Is.EqualTo(1),"Entering the cell is not reaching the waypoint.");
                for(var i=0;i<100;i++)advance.Invoke(controller,new object[]{actor,route,(Vector2?)final,0f});
                Assert.That(route.Count,Is.Zero);Assert.That(Vector2.Distance(actor.Position,final),Is.LessThan(.03f));
            }
            finally {Object.DestroyImmediate(root);}
        }

        [Test]
        public void CityRouteUsesColliderClearanceAndRejectsInsideBuilding()
        {
            var root = new GameObject("city-test");
            try
            {
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();actor.transform.SetParent(root.transform);
                var wall = new GameObject("building").AddComponent<BoxCollider2D>();wall.transform.SetParent(root.transform);wall.transform.position=new Vector2(2,0);wall.size=new Vector2(1,2);
                Physics2D.SyncTransforms();
                Assert.That(actor.HasClearMovementSegment(Vector2.zero,new Vector2(4,0)),Is.False);
                actor.Move(new Vector2(4,0));Assert.That(actor.Position.x,Is.LessThan(1.5f));
                var find=typeof(Godmist.Views.Exploration.ExplorationPartyController).GetMethod("TryFindWorldRoute",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var args=new object[]{actor,new Vector2(4,.1f),null};
                Assert.That(find.Invoke(null,args),Is.EqualTo(true));
                var points=(System.Collections.Generic.List<Vector2>)args[2];var previous=actor.Position;
                foreach(var point in points){Assert.That(actor.HasClearMovementSegment(previous,point),Is.True);previous=point;}
                Assert.That(points[points.Count-1],Is.EqualTo(new Vector2(4,.1f)));
                Assert.That(find.Invoke(null,new object[]{actor,new Vector2(2,0),null}),Is.EqualTo(false));
            }
            finally{Object.DestroyImmediate(root);Physics2D.SyncTransforms();}
        }

        [Test]
        public void OpenDungeonFollowerKeepsFreeformDirectionAcrossCells()
        {
            var root=new GameObject("free-follow",typeof(Grid));
            try
            {
                var map=new DungeonFloorMap();for(var y=-2;y<5;y++)for(var x=-2;x<5;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var tiles=new GameObject("tiles",typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
                var view=root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap",flags).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var follower=new GameObject("follower").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();follower.transform.SetParent(root.transform);follower.Bind(new Godmist.Runtime.Characters.CharacterInstance{CharacterId="f"});follower.ConfigureTerrain(map,view,floor);follower.SetPosition(new Vector2(.3f,.7f));
                var leader=new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();leader.transform.SetParent(root.transform);leader.ConfigureTerrain(map,view,floor);leader.SetPosition(new Vector2(3.2f,2.1f));
                var controller=root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();controller.ConfigureTraversal(floor,view,null);
                var start=follower.Position;var expected=(leader.Position-start).normalized;
                controller.GetType().GetMethod("FollowTraversal",flags).Invoke(controller,new object[]{follower,leader});
                Assert.That(Vector2.Dot((follower.Position-start).normalized,expected),Is.GreaterThan(.999f));
                map.TryAddWall(Vector2Int.zero,Vector2Int.right);
                Assert.That(controller.GetType().GetMethod("CanFollowDirectly",flags).Invoke(controller,new object[]{follower,leader}),Is.EqualTo(false));
            }
            finally{Object.DestroyImmediate(root);}
        }

        private static DungeonFloorMap CreateSquareMap()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");
            map.TryAddFloorCell(new Vector2Int(0, 1), "room");
            map.TryAddFloorCell(new Vector2Int(1, 1), "room");

            return map;
        }
    }
}