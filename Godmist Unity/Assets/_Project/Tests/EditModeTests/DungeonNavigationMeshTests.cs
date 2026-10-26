using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Tests.EditMode
{
    public class DungeonNavigationMeshTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;


        [Test]
        public void NumericTerrainSnapshotTracksRootTransformAndExplicitPhysicalRefresh()
        {
            var root=new GameObject("terrain snapshot invalidation",typeof(Grid));
            var tile=ScriptableObject.CreateInstance<Tile>();
            try
            {
                var tiles=new GameObject("tiles",typeof(Tilemap));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",Private).SetValue(view,tiles.GetComponent<Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile",Private).SetValue(view,tile);
                var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab",Private).SetValue(view,prefab.GetComponent<DungeonWallSegmentView>());
                var map=new DungeonFloorMap();
                for(var x=0;x<4;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
                map.TryAddWall(new Vector2Int(1,0),new Vector2Int(2,0));view.Rebuild(map);
                Bounds[] Live()=>view.NavigationWallBounds(0).ToArray();
                Bounds[] Cached(bool refresh=false)=>view.CachedNavigationWallBounds(0,refresh).ToArray();
                Assert.That(Live(),Is.Not.Empty);CollectionAssert.AreEquivalent(Live(),Cached());
                int captures=view.NavigationBoundsCaptureCount;
                Cached();Assert.That(view.NavigationBoundsCaptureCount,Is.EqualTo(captures));
                root.transform.position=new Vector3(100,200,0);Physics2D.SyncTransforms();
                CollectionAssert.AreEquivalent(Live(),Cached());
                Assert.That(view.NavigationBoundsCaptureCount,Is.GreaterThan(captures));
                var collider=view.GetComponentsInChildren<BoxCollider2D>().First(c=>c.enabled&&!c.isTrigger);
                collider.size*=.5f;Physics2D.SyncTransforms();
                Assert.That(Cached().SequenceEqual(Live()),Is.False,"The test must change a real collider after the snapshot.");
                CollectionAssert.AreEquivalent(Live(),Cached(true),"A failed physical route can explicitly refresh modified terrain.");
            }
            finally { Object.DestroyImmediate(root);Object.DestroyImmediate(tile);Physics2D.SyncTransforms(); }
        }

        [Test]
        public void StaticSourcesReuseDoorsGatesAndRadiiButInvalidateGeometryProjectionAndMap()
        {
            var root=new GameObject("source geometry cache",typeof(Grid));
            using var mesh=new DungeonNavigationMesh();
            using var otherRadius=new DungeonNavigationMesh();
            try
            {
                var tiles=new GameObject("tiles",typeof(Tilemap));tiles.transform.SetParent(root.transform);
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",Private).SetValue(view,tiles.GetComponent<Tilemap>());
                var map=new DungeonFloorMap();
                for(var x=0;x<4;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var geometry=new DungeonNavigationMesh.SourceGeometry();
                void Build(int expected)
                {
                    Assert.That(mesh.Build(floor,view,0,.1f,null,geometry:geometry),Is.True);
                    Assert.That(mesh.StaticGeometryBuildCount,Is.EqualTo(expected));
                }
                bool Path(int x=3)=>mesh.TryFindPath(view.CellToWorldCenter(Vector2Int.zero),
                    view.CellToWorldCenter(new Vector2Int(x,0)),Vector2.zero,(a,b)=>true,out _);
                Build(1);Assert.That(Path(),Is.True);
                Assert.That(otherRadius.Build(floor,view,0,.2f,null,geometry:geometry),Is.True);
                Assert.That(otherRadius.StaticGeometryBuildCount,Is.EqualTo(1));
                otherRadius.Dispose();
                var edge=new DungeonCellEdge(new Vector2Int(1,0),new Vector2Int(2,0));
                map.TrySetProgressionGateState(edge,false);Build(1);Assert.That(Path(),Is.False);
                map.TrySetProgressionGateState(edge,true);Build(1);Assert.That(Path(),Is.True);
                map.TrySetDoorState(edge,false);Build(1);Assert.That(Path(),Is.False,"Closed map doors must work without a spawned door object.");
                map.TrySetDoorState(edge,true);Build(1);Assert.That(Path(),Is.True);
                map.SetWallType(edge,Godmist.Core.Enums.DungeonWallType.Full);Build(2);Assert.That(Path(),Is.False);
                map.SetWallType(edge,Godmist.Core.Enums.DungeonWallType.TreeTrunk);Build(3);Assert.That(Path(),Is.True);
                map.TryAddFloorCell(new Vector2Int(4,0),"room");Build(4);Assert.That(Path(4),Is.True);
                root.transform.position=new Vector3(123,456,0);Build(5);Assert.That(Path(4),Is.True);
                var replacement=new DungeonFloorMap();
                for(var x=0;x<5;x++)replacement.TryAddFloorCell(new Vector2Int(x,0),"replacement");
                floor=new DungeonFloorInstance(1);floor.TrySetMap(replacement);Build(6);Assert.That(Path(4),Is.True);
                replacement.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero,1),"upper");
                Assert.That(otherRadius.Build(floor,view,1,.1f,null,geometry:geometry),Is.True);
                Assert.That(otherRadius.StaticGeometryBuildCount,Is.EqualTo(7));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator CachedPropsTrackMovementDisablingAndNewPhysicalBlockers()
        {
            var root=new GameObject("physical cache safety",typeof(Grid));root.transform.position=new Vector3(15000,15000,0);
            try
            {
                var tiles=new GameObject("tiles",typeof(Tilemap));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",Private).SetValue(view,tiles.GetComponent<Tilemap>());
                var map=new DungeonFloorMap();
                for(var x=0;x<5;x++)for(var y=0;y<3;y++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var actorObject=new GameObject("actor",typeof(CircleCollider2D));actorObject.transform.SetParent(root.transform,false);
                var circle=actorObject.GetComponent<CircleCollider2D>();circle.radius=.1f;
                var actor=actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider",Private).SetValue(actor,circle);
                actor.ConfigureTerrain(map,view,floor);actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var controller=root.AddComponent<ExplorationPartyController>();controller.ConfigureTraversal(floor,view,null);
                var target=new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(4,0)));
                var goal=view.CellToWorldCenter(target.Surface.Cell);
                var find=typeof(ExplorationPartyController).GetMethod("FindTraversalPathWithTargetWorld",Private);
                bool Find()=>(bool)find.Invoke(controller,new object[]{actor,target,null,goal});
                DungeonNavigationMesh Mesh()=>(DungeonNavigationMesh)typeof(ExplorationPartyController).GetMethod("GetNavigationMesh",Private).Invoke(controller,new object[]{actor,0});
                Assert.That(Find(),Is.True);
                var collections=controller.NavigationExternalCollectionCount;
                var blocker=new GameObject("new physical blocker",typeof(BoxCollider2D));blocker.transform.SetParent(root.transform,false);
                var collider=blocker.GetComponent<BoxCollider2D>();collider.size=new Vector2(.25f,4);
                blocker.transform.position=view.CellToWorldCenter(new Vector2Int(2,1));Physics2D.SyncTransforms();
                Assert.That(Find(),Is.False,"New props cannot be crossed using cached open geometry.");
                Assert.That(controller.NavigationExternalCollectionCount,Is.GreaterThan(collections));
                var deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
                while(!Mesh().IsReady && UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                Assert.That(Mesh().IsReady,Is.True);
                Assert.That(Find(),Is.False);
                blocker.transform.position+=Vector3.up*10;Physics2D.SyncTransforms();
                Assert.That(Find(),Is.True,"A moved-away cached collider must release the route.");
                deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
                while(!Mesh().IsReady && UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                blocker.transform.position=view.CellToWorldCenter(new Vector2Int(2,1));Physics2D.SyncTransforms();
                Assert.That(Find(),Is.False);
                collider.enabled=false;Physics2D.SyncTransforms();
                deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
                while(!Mesh().IsReady && UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                Assert.That(Find(),Is.True,"Disabling a cached blocker must release the route.");
                controller.ConfigureTraversal(null,null,null);
            }
            finally { Object.DestroyImmediate(root);Physics2D.SyncTransforms(); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RuntimeGateRebuildCompletesInBackgroundAndCancelsSafely()
        {
            var root = new GameObject("background-navigation", typeof(Grid));
            using var mesh = new DungeonNavigationMesh();
            try
            {
                var tiles = new GameObject("tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var gate = new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(2, 0));
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                Assert.That(mesh.Build(floor, view, 0, .196f, null), Is.True);
                bool Path() => mesh.TryFindPath(new Vector2(.5f,.5f), new Vector2(3.5f,.5f), Vector2.zero, (a,b)=>true, out _);
                Assert.That(Path(), Is.True);
                foreach (var open in new[] { false, true })
                {
                    map.TrySetProgressionGateState(gate, open);
                    mesh.Build(floor, view, 0, .196f, null, background: true);
                    Assert.That(mesh.IsReady || mesh.IsBuilding, Is.True);
                    if (mesh.IsBuilding) Assert.That(Path(), Is.False, "Pending geometry must never query the stale open mesh.");
                    var deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                    while (!mesh.IsReady && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.That(mesh.IsReady, Is.True);
                    Assert.That(Path(), Is.EqualTo(open));
                }
                mesh.Build(floor, view, 0, .196f, null, background: true);
                mesh.Dispose();
                yield return null;
                Assert.That(mesh.IsReady || mesh.IsBuilding, Is.False);

                var actorObject = new GameObject("actor", typeof(CircleCollider2D)); actorObject.transform.SetParent(root.transform);
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", Private).SetValue(actor, actorObject.GetComponent<CircleCollider2D>());
                actor.ConfigureTerrain(map, view, floor); actor.SetPosition(new Vector2(.5f,.5f));
                var controller = root.AddComponent<ExplorationPartyController>(); controller.ConfigureTraversal(floor,view,null);
                var get = typeof(ExplorationPartyController).GetMethod("GetNavigationMesh", Private);
                var original = (DungeonNavigationMesh)get.Invoke(controller,new object[]{actor,0});
                Assert.That(original.IsReady, Is.True, "Initial floor preparation stays synchronous.");
                map.TrySetProgressionGateState(gate,false);
                var replacement = (DungeonNavigationMesh)get.Invoke(controller,new object[]{actor,0});
                Assert.That(replacement, Is.Not.SameAs(original));
                Assert.That(original.IsReady, Is.False);
                Assert.That(replacement.IsReady || replacement.IsBuilding, Is.True);
                controller.ConfigureTraversal(null,null,null);
                Assert.That(replacement.IsReady || replacement.IsBuilding, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0)] [TestCase(1)]
        public void CoalescedFloorSourcesCoverExactlyTheirLevelWithoutFillingHoles(int level)
        {
            var map = new DungeonFloorMap();
            var expected = new HashSet<Vector2Int>();
            for (var y=-10;y<10;y++) for(var x=-20;x<20;x++)
            {
                if (x>=-2 && x<=2 && y>=-2 && y<=2) continue;
                var cell=new Vector2Int(x,y); expected.Add(cell);
                map.TryAddFloorCell(new DungeonSurfacePosition(cell,level),"room");
            }
            map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(100,100),level+1),"other level");
            var rectangles=((IEnumerable<RectInt>)typeof(DungeonNavigationMesh)
                .GetMethod("FloorRectangles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{map,level})).ToArray();
            var covered=new HashSet<Vector2Int>();
            foreach(var rectangle in rectangles)
                foreach(var cell in rectangle.allPositionsWithin)
                    Assert.That(covered.Add(cell),Is.True,"Floor sources must not overlap.");
            Assert.That(covered.SetEquals(expected),Is.True,"Compression must preserve holes, edges and elevation.");
            Assert.That(rectangles.Length,Is.LessThan(10));
        }

        [Test]
        public void ClosedGateBlocksNativePathUntilRebuiltAfterOpening()
        {
            var root = new GameObject("native-gate", typeof(Grid));
            using var mesh = new DungeonNavigationMesh();
            try
            {
                var tiles = new GameObject("tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var gate = new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(2, 0));
                map.TrySetProgressionGateState(gate, false);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                Assert.That(mesh.Build(floor, view, 0, .196f, null), Is.True);
                Assert.That(mesh.TryFindPath(new Vector2(.5f, .5f), new Vector2(3.5f, .5f), Vector2.zero,
                    (a, b) => true, out _), Is.False);
                map.TrySetProgressionGateState(gate, true);
                Assert.That(mesh.Build(floor, view, 0, .196f, null), Is.True);
                Assert.That(mesh.TryFindPath(new Vector2(.5f, .5f), new Vector2(3.5f, .5f), Vector2.zero,
                    (a, b) => true, out _), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RubbleUsesPhysicalHalfCellBlockerInsteadOfLogicalFullCell()
        {
            var root = new GameObject("native-rubble", typeof(Grid));
            using var mesh = new DungeonNavigationMesh();
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                typeof(DungeonObjectDefinition).GetField("<Id>k__BackingField", Private)
                    .SetValue(definition, "object.obstacle.rubble");
                typeof(ObstacleDungeonObjectDefinition).GetField("<BlocksMovement>k__BackingField", Private)
                    .SetValue(definition, true);
                var tiles = new GameObject("tiles", typeof(Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private)
                    .SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0);
                floor.TrySetMap(map);
                floor.TryAddObject(new ObstacleDungeonObjectInstance(
                    "rubble", definition, new Vector2Int(1, 0)));

                var colliderObject = new GameObject("rubble-collider", typeof(BoxCollider2D));
                colliderObject.transform.SetParent(root.transform);
                colliderObject.transform.position = new Vector2(1.5f, .3f);
                var collider = colliderObject.GetComponent<BoxCollider2D>();
                collider.size = new Vector2(1f, .5f);
                Physics2D.SyncTransforms();

                Assert.That(mesh.Build(floor, view, 0, .05f, new[] { collider.bounds }), Is.True);
                Assert.That(mesh.TryFindPath(
                    new Vector2(.5f, .9f),
                    new Vector2(2.5f, .9f),
                    Vector2.zero,
                    (a, b) => true,
                    out _), Is.True);
                // Only this fixture's obstacle participates in its path validator.
                // Other tests can leave an authored scene loaded in the shared physics world.
                bool Safe(Vector2 from, Vector2 to) => !System.Array.Exists(Physics2D.CircleCastAll(
                    from, .05f, (to - from).normalized, Vector2.Distance(from, to)),
                    hit => hit.collider == collider);
                Assert.That(Safe(new Vector2(.5f, .2f), new Vector2(2.5f, .2f)), Is.False);
                Assert.That(mesh.TryFindPath(
                    new Vector2(.5f, .2f), new Vector2(2.5f, .2f), Vector2.zero,
                    Safe, out var detour), Is.True, "The upper half remains open for a safe detour.");
                Assert.That(detour.Exists(point => point.y > collider.bounds.max.y + .05f), Is.True);
                Assert.That(mesh.TryFindPath(
                    new Vector2(.5f, .2f), new Vector2(1.5f, .2f), Vector2.zero,
                    Safe, out _), Is.False, "A click inside the rubble must remain invalid.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        [Test]
        public void RubbleWorldEndpointUsesPhysicalColliderForUpperAndLowerHalf()
        {
            var root = new GameObject("world-rubble", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                typeof(DungeonObjectDefinition).GetField("<Id>k__BackingField", Private)
                    .SetValue(definition, "object.obstacle.rubble");
                typeof(ObstacleDungeonObjectDefinition).GetField("<BlocksMovement>k__BackingField", Private)
                    .SetValue(definition, true);
                var tiles = new GameObject("tiles", typeof(Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private)
                    .SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                var floor = new DungeonFloorInstance(0);
                floor.TrySetMap(map);
                floor.TryAddObject(new ObstacleDungeonObjectInstance(
                    "rubble", definition, Vector2Int.zero));

                var rubbleObject = new GameObject("rubble-collider", typeof(BoxCollider2D));
                rubbleObject.transform.SetParent(root.transform);
                rubbleObject.transform.position = new Vector2(.5f, .3f);
                rubbleObject.GetComponent<BoxCollider2D>().size = new Vector2(1f, .5f);

                var actorObject = new GameObject("actor", typeof(CircleCollider2D));
                actorObject.transform.SetParent(root.transform);
                var actorCollider = actorObject.GetComponent<CircleCollider2D>();
                actorCollider.radius = .1f;
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", Private)
                    .SetValue(actor, actorCollider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(.5f, .8f));

                var controller = root.AddComponent<ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                var rubbleCell = new DungeonTraversalPosition(
                    new DungeonSurfacePosition(Vector2Int.zero));

                Assert.That(actor.CanStandAt(new Vector2(.5f, .8f)), Is.True);
                Assert.That(actor.CanStandAt(new Vector2(.5f, .2f)), Is.False);
                Assert.That(controller.CanEnterTraversalAtWorld(
                    actor, rubbleCell, new Vector2(.5f, .8f)), Is.True);
                Assert.That(controller.CanEnterTraversalAtWorld(
                    actor, rubbleCell, new Vector2(.5f, .2f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        [Test, Repeat(16)]
        public void NativeCornersDrivePartyMovementAroundPhysicalWall()
        {
            var root = new GameObject("native-route", typeof(Grid));
            try
            {
                var tiles = new GameObject("tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var mapView = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(mapView, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 5; x++) for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var wall = new GameObject("physical-divider", typeof(BoxCollider2D)); wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector2(2.5f, 1f); wall.GetComponent<BoxCollider2D>().size = new Vector2(.25f, 2f);
                var actorObject = new GameObject("actor", typeof(CircleCollider2D)); actorObject.transform.SetParent(root.transform);
                var circle = actorObject.GetComponent<CircleCollider2D>(); circle.radius = .196f; circle.offset = new Vector2(0, .2f);
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", Private).SetValue(actor, circle);
                actor.ConfigureTerrain(map, mapView, floor); actor.SetPosition(new Vector2(1.5f, .3f));
                var controller = root.AddComponent<ExplorationPartyController>(); controller.ConfigureTraversal(floor, mapView, null);
                Physics2D.SyncTransforms();
                var goal = new Vector2(3.5f, .3f);
                Assert.That(actor.HasClearMovementSegment(actor.Position, goal), Is.False);
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(3, 0)));
                var args = new object[] { actor, target, null, goal };
                var initialPlanOk=(bool)typeof(ExplorationPartyController).GetMethod("FindTraversalPathWithTargetWorld", Private).Invoke(controller, args);
                if(!initialPlanOk)
                {
                    var diagnostic=(DungeonNavigationMesh)typeof(ExplorationPartyController).GetMethod("GetNavigationMesh",Private).Invoke(controller,new object[]{actor,0});
                    var raw=diagnostic.TryFindPath(actor.Position,goal,actor.MovementColliderCenterOffset,(_,__)=>true,out var corners);
                    Assert.Fail("Native route failed; ready="+diagnostic.IsReady+" raw="+raw+" plane="+typeof(DungeonNavigationMesh).GetField("plane",Private).GetValue(diagnostic)
                        +" corners="+string.Join(";",corners)+" nativeData="+Resources.FindObjectsOfTypeAll<UnityEngine.AI.NavMeshData>().Length
                        +" blockers="+string.Join(";",Physics2D.OverlapBoxAll(new Vector2(2.5f,2),new Vector2(5,4),0).Select(c=>c.name+":"+c.bounds)));
                }
                Assert.That((int)typeof(ExplorationPartyController).GetField("lastTraversalPathExpandedNodes", Private).GetValue(controller), Is.Zero);
                var nativeRoutes = (System.Collections.IDictionary)typeof(ExplorationPartyController).GetField("meshRoutes", Private).GetValue(controller);
                Assert.That(nativeRoutes.Contains(actor), Is.True, "The controller must actually use NavMesh, not only build an unused prototype.");
                var meshes = (System.Collections.IDictionary)typeof(ExplorationPartyController).GetField("navigationMeshes", Private).GetValue(controller);
                var cachedMesh = meshes.Values.GetEnumerator(); cachedMesh.MoveNext(); var originalMesh = cachedMesh.Current;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (var query = 0; query < 100; query++)
                {
                    args = new object[] { actor, target, null, goal };
                    Assert.That(typeof(ExplorationPartyController).GetMethod("FindTraversalPathWithTargetWorld", Private).Invoke(controller, args), Is.True);
                }
                watch.Stop();
                TestContext.WriteLine($"100 warmed native queries: {watch.Elapsed.TotalMilliseconds:F3}ms");
                Assert.That(meshes.Count, Is.EqualTo(1));
                var reused = meshes.Values.GetEnumerator(); reused.MoveNext(); Assert.That(reused.Current, Is.SameAs(originalMesh));
                var route = new Queue<DungeonTraversalPosition>(new[] { target });
                var advance = typeof(ExplorationPartyController).GetMethod("AdvanceRoute", Private);
                for (var tick = 0; tick < 600 && route.Count > 0; tick++)
                {
                    var before = actor.Position;
                    Assert.That(advance.Invoke(controller, new object[] { actor, route, goal, 0f }), Is.True);
                    Physics2D.SyncTransforms();
                    Assert.That(actor.HasClearMovementSegment(before, actor.Position), Is.True);
                }
                Assert.That(route, Is.Empty);
                Assert.That(Vector2.Distance(actor.Position, goal), Is.LessThan(.04f));
                controller.ConfigureTraversal(null, null, null);
                Assert.That(nativeRoutes, Is.Empty);
            }
            finally
            {
                var controller=root.GetComponent<ExplorationPartyController>();
                if(controller!=null)typeof(ExplorationPartyController).GetMethod("DisposeNavigationMeshes",Private).Invoke(controller,null);
                Object.DestroyImmediate(root); Physics2D.SyncTransforms();
            }
        }

        [Test]
        public void ExactEndpointsAndDisconnectedLevelsAreNotSilentlySnapped()
        {
            var root = new GameObject("native-levels", typeof(Grid));
            using var lower = new DungeonNavigationMesh();
            using var upper = new DungeonNavigationMesh();
            try
            {
                var tiles = new GameObject("tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++) for (var y = 0; y < 3; y++)
                {
                    map.TryAddFloorCell(new Vector2Int(x, y), "room");
                    map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x, y), 1), "upper");
                }
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                Assert.That(lower.Build(floor, view, 0, .196f, null), Is.True);
                Assert.That(upper.Build(floor, view, 1, .196f, null), Is.True);
                Assert.That(lower.TryFindPath(new Vector2(.5f, .5f), new Vector2(2.5f, 2.5f), Vector2.zero,
                    (a, b) => true, out var points), Is.True);
                Assert.That(points[points.Count - 1], Is.EqualTo(new Vector2(2.5f, 2.5f)));
                Assert.That(lower.TryFindPath(new Vector2(.5f, .5f), new Vector2(3.05f, 1.5f), Vector2.zero,
                    (a, b) => b.x < 3, out _), Is.False);
                lower.Dispose();
                Assert.That(lower.TryFindPath(Vector2.one, Vector2.one, Vector2.zero, (a, b) => true, out _), Is.False);
                Assert.That(upper.IsReady, Is.True, "Disposing one level must not unregister the other.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
