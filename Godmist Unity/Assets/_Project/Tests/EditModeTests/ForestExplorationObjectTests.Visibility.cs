using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class ForestExplorationObjectTests
    {
        static string AuditOutput=>Path.GetFullPath("../LocalHandoffs/forest-visibility-object-audit-20261020");
        static Type ForestVisuals=>typeof(DungeonObjectView).Assembly.GetType("Godmist.Views.Dungeons.DungeonForestObjectVisuals");
        static T NativeView<T>(Transform parent,string kind,DungeonObjectDefinition definition) where T:DungeonObjectView
            =>(T)ForestVisuals.GetMethod("Create"+kind+"View",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{parent,"Native "+kind,definition});
        static DungeonFloorMapView AuditMap(GameObject root,DungeonFloorMap map)
        {
            var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));tiles.transform.SetParent(root.transform,false);
            var view=root.AddComponent<DungeonFloorMapView>();Set(view,"floorTilemap",tiles.GetComponent<Tilemap>());
            Set(view,"floorTile",AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset"));
            Set(view,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
            Set(view,"lowWallPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab").GetComponent<DungeonWallSegmentView>());
            Set(view,"railingPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonRailing.prefab").GetComponent<DungeonWallSegmentView>());
            Set(view,"lowWallPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab"));
            view.ConfigureVegetationPresentation(Definition.VegetationPresentation);view.Rebuild(map);return view;
        }
        [Test]
        public void UndiscoveredBoundaryTreesRemainRenderedWithoutRevealingRoutesOrObjects()
        {
            var root=new GameObject("Static woodland fog",typeof(Grid));
            try
            {
                var map=new DungeonFloorMap();for(var y=0;y<10;y++)for(var x=0;x<10;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                foreach(var c in map.FloorCells)foreach(var d in Directions)if(!map.IsWalkable(c+d))map.TryAddWall(c,c+d);
                var view=AuditMap(root,map);var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero))});view.ApplyFog(floor);
                var trees=Read<Dictionary<Renderer,DungeonSurfaceEdge[]>>(view,"forestBoundaryOwners");Assert.That(trees.Count,Is.GreaterThan(10));
                var distant=trees.Where(p=>p.Value.All(e=>view.EdgeKnowledge(e)==DungeonKnowledge.Unexplored)).Select(p=>p.Key).ToArray();
                Assert.That(distant.Length,Is.GreaterThan(5));
                Assert.That(distant.All(r=>r.enabled&&!r.forceRenderingOff),Is.True,"Static crowns must exist before togglefog, even when their arbitrary edge owners are unknown.");
                Assert.That(view.Knowledge(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(9,9)))),Is.EqualTo(DungeonKnowledge.Unexplored));
                Assert.That(Read<Tilemap>(view,"floorTilemap").HasTile(new Vector3Int(9,9,0)),Is.False,"Drawing static woods must not reveal the route.");
                var conditionDefinition=Definition.ObjectDefinitions.OfType<ConditionDungeonObjectDefinition>().Single();
                var condition=new ConditionDungeonObjectInstance("unknown mark",conditionDefinition,new Vector2Int(9,9));floor.TryAddObject(condition);
                var mark=NativeView<DungeonConditionView>(root.transform,"Condition",conditionDefinition);
                mark.Initialize(null,floor,condition,view.CellToWorldCenter(condition.Position),Read<Tilemap>(view,"floorTilemap"));mark.BindElevationView(view);
                typeof(DungeonObjectView).GetMethod("LateUpdate",Flags).Invoke(mark,null);
                Assert.That(mark.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),Is.True,"Static woodland must not disclose unknown interactive objects.");
                var colors=distant.OfType<SpriteRenderer>().ToDictionary(r=>r,r=>r.color);
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                foreach(var pair in colors)Assert.That(pair.Key.color,Is.EqualTo(pair.Value),"togglefog must not brighten static trees into view.");
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                Assert.That(distant.All(r=>r.enabled&&!r.forceRenderingOff),Is.True);
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());view.ApplyFog(floor);
                Assert.That(distant.All(r=>!r.forceRenderingOff),Is.True,"Walking away must not remove a static tree.");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void NativeForestLayersAndWinchColliderReachLeaderAndFollowerNavigation()
        {
            var root=new GameObject("Native prop navigation",typeof(Grid));root.transform.position=new Vector3(15000,15000,0);
            using var mesh=new DungeonNavigationMesh();
            try
            {
                var map=new DungeonFloorMap();for(var y=0;y<3;y++)for(var x=0;x<6;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);var view=AuditMap(root,map);
                var lever=NativeView<DungeonLeverView>(root.transform,"Lever",Definition.ObjectDefinitions.OfType<LeverDungeonObjectDefinition>().Single());
                lever.transform.position=view.CellToWorldCenter(new Vector2Int(2,1));
                var collider=lever.GetComponent<BoxCollider2D>();Assert.That(collider,Is.Not.Null);Assert.That(collider.isTrigger,Is.False);
                Assert.That(collider.size.x,Is.LessThan(1));Assert.That(lever.gameObject.layer,Is.EqualTo(root.layer));
                var party=root.AddComponent<ExplorationPartyController>();Set(party,"traversalFloor",floor);Set(party,"traversalMapView",view);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
                for(var i=0;i<4;i++)
                {
                    var actor=Object.Instantiate(prefab,root.transform).GetComponent<ExplorationCharacterView>();Physics2D.SyncTransforms();
                    Assert.That(Physics2D.GetLayerCollisionMask(actor.gameObject.layer)&(1<<lever.gameObject.layer),Is.Not.Zero);
                    var target=(Vector2)collider.bounds.center-actor.MovementColliderCenterOffset;
                    Assert.That(actor.CanStandAt(target),Is.False,"Both leader and each companion must collide with the winch base.");
                    var physical=((IEnumerable<Bounds>)typeof(ExplorationPartyController).GetMethod("NavigationBlockerBounds",Flags).Invoke(party,new object[]{actor,0})).ToArray();
                    Assert.That(physical.Any(b=>b.Contains(collider.bounds.center)),Is.True,"The real follower navmesh provider must include the lever.");
                    Assert.That(mesh.Build(floor,view,0,actor.MovementRadius,physical),Is.True);
                    Assert.That(mesh.TryFindPath(view.CellToWorldCenter(new Vector2Int(0,1)),view.CellToWorldCenter(new Vector2Int(5,1)),actor.MovementColliderCenterOffset,(a,b)=>true,out _),Is.True,"A route around the small base must remain available.");
                }
                root.layer=12;
                var condition=NativeView<DungeonConditionView>(root.transform,"Condition",Definition.ObjectDefinitions.OfType<ConditionDungeonObjectDefinition>().Single());
                Assert.That(condition.GetComponentsInChildren<Collider2D>(),Is.Empty,"A floor mark is walkable.");
                Assert.That(condition.gameObject.layer,Is.EqualTo(root.layer));
                var trapRoot=new GameObject("Native trap layer",typeof(SpriteRenderer));trapRoot.transform.SetParent(root.transform,false);trapRoot.layer=12;
                var trap=trapRoot.AddComponent<DungeonSpikeTrapView>();var trapSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Godmist.Editor.ForestExplorationObjectsSetup.ArtRoot+"snare.png");
                trap.ConfigureRuntimeSprites(trapSprite);
                Assert.That(trap.GetComponentsInChildren<SpriteRenderer>().All(r=>r.gameObject.layer==12),Is.True);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void ForestTrapArtIsOriginalAndDistinctFromNpcSkillSprites()
        {
            var files=new[]{("snare","forest-poacher-snare-v1-64.png"),("poison","forest-poison-puffballs-v1-64.png"),("roots","forest-cursed-roots-v1-64.png")};
            foreach(var (name,file) in files)
            {
                var source="Assets/_Project/Art/ForestExploration/"+file;
                var imported=Godmist.Editor.ForestExplorationObjectsSetup.ArtRoot+name+".png";
                Assert.That(File.ReadAllBytes(imported),Is.EqualTo(File.ReadAllBytes(source)));
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(imported);Assert.That(texture.width,Is.EqualTo(64));Assert.That(texture.height,Is.EqualTo(64));
                foreach(var npc in new[]{"bear-trap-world-64-v1.png","poisonous-growth-world-64-v1.png"})
                    Assert.That(File.ReadAllBytes(imported).SequenceEqual(File.ReadAllBytes("Assets/Resources/Godmist/ForestArt/Summons/"+npc)),Is.False);
            }
        }
        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void GeneratedForestFinalHasAnExteriorExitAcrossSeeds(DungeonLength length)
        {
            var failures=new List<int>();var examples=new List<string>();
            for(var seed=0;seed<24;seed++)
            {
                var floor=Generate(seed,10,length).Floors.Single();
                var final=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final);
                if(!DungeonForestTransitionBoundary.TryResolve(floor,final.RoomId,out var cell,out var normal))
                { failures.Add(seed);continue; }
                DungeonForestTransitionBoundary.TryResolve(floor,final.RoomId,out var previousCell,out var previousNormal,requireExterior:false);
                if(previousCell!=cell || previousNormal!=normal)examples.Add($"seed={seed}: old={previousCell}/{previousNormal}, exterior={cell}/{normal}");
                foreach(var other in floor.Map.FloorCells)
                    Assert.That(Mathf.Abs(other.x-cell.x)>1 || (other.y-cell.y)*normal.y<=0,Is.True,
                        $"{length}, seed={seed}: another room/corridor is behind the exit opening.");
            }
            var output=Path.GetFullPath("../LocalHandoffs/exit-exterior-edge-20261023");Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output,"survey-"+length+".txt"),examples);
            Assert.That(failures,Is.Empty,"Final room has no exterior gate edge. Seeds: "+string.Join(",",failures));
        }
        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void ForestBossFloorsHaveExteriorTransitions(DungeonLength length)
        {
            var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Godmist.Editor.ForestExpeditionsSetup.BossPath);
            foreach(var seed in new[]{9,171026})
            {
                var dungeon=new Godmist.Systems.Dungeons.DungeonGeneratorService().Generate(definition,seed,10,length);
                new Godmist.Systems.Dungeons.DungeonObjectPlacementSystem().Place(dungeon,Catalog.EquipmentPartDefinitions);
                foreach(var floor in dungeon.Floors)
                    foreach(var room in floor.Rooms.Where(r=>r.RoomType==DungeonRoomType.Final || r.RoomType==DungeonRoomType.Start && floor.FloorIndex>0))
                        Assert.That(DungeonForestTransitionBoundary.TryResolve(floor,room.RoomId,out _,out _),Is.True,
                            $"Boss {length}, seed={seed}, floor={floor.FloorIndex}, {room.RoomType}");
            }
        }
        [Test]
        public void HistoricalEnclosedForestLayoutRetainsItsExitWithoutMovingSavedRooms()
        {
            var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
            foreach(var (id,yOffset) in new[]{("final",0),("north",6),("south",-6)})
            {
                floor.TryAddRoom(new DungeonRoomInstance(id,id=="final"?DungeonRoomType.Final:DungeonRoomType.Normal,
                    layoutPosition:new Vector2Int(-3,yOffset)));
                for(var x=-3;x<=3;x++)for(var y=0;y<=4;y++)map.TryAddFloorCell(new Vector2Int(x,y+yOffset),id);
            }
            floor.TrySetMap(map);var revision=map.StaticGeometryRevision;
            var positions=floor.Rooms.Select(r=>r.LayoutPosition).ToArray();
            Assert.That(DungeonForestTransitionBoundary.TryResolve(floor,"final",out _,out _),Is.False);
            var root=new GameObject("Saved enclosed exit audit");
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();view.ConfigureVegetationPresentation(Definition.VegetationPresentation);
                Assert.DoesNotThrow(()=>view.PrepareForestTransitionBoundaries(floor));
                Assert.That(Read<Dictionary<DungeonRoomType,Vector2>>(view,"forestTransitions"),Does.ContainKey(DungeonRoomType.Final));
                Assert.That(map.StaticGeometryRevision,Is.EqualTo(revision));
                Assert.That(floor.Rooms.Select(r=>r.LayoutPosition),Is.EqualTo(positions));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(6)] [TestCase(100)]
        public void ForestExitFacesOutsideTheWholeMapRatherThanAnAdjacentOrDistantRoom(int otherRoomY)
        {
            var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
            floor.TryAddRoom(new DungeonRoomInstance("final",DungeonRoomType.Final));
            floor.TryAddRoom(new DungeonRoomInstance("other",DungeonRoomType.Normal));
            for(var x=-3;x<=3;x++)for(var y=0;y<=4;y++)
            {
                map.TryAddFloorCell(new Vector2Int(x,y),"final");
                map.TryAddFloorCell(new Vector2Int(x,y+otherRoomY),"other");
            }
            floor.TrySetMap(map);
            var root=new GameObject("Exterior exit regression");
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();view.ConfigureVegetationPresentation(Definition.VegetationPresentation);
                view.PrepareForestTransitionBoundaries(floor);
                var threshold=Read<Dictionary<DungeonRoomType,Vector2>>(view,"forestTransitions")[DungeonRoomType.Final];
                Assert.That(threshold.y,Is.LessThan(0),"The northern boundary faces another room: this is not an external exit.");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void ForestFinalExitAlwaysHasReachableUnoccupiedGround(DungeonLength length)
        {
            var root=new GameObject("Exit audit",typeof(Grid));
            try
            {
                var floor=Generate(2,10,length).Floors.Single();var view=AuditMap(root,floor.Map);
                view.SetForestPathObstacles(floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive&&o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells()));
                view.SetPresentationOccupancy(floor.Objects.Select(o=>o.Position));
                var final=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final);
                var fallback=floor.Map.FloorCells.First(c=>floor.Map.TryGetRoomId(c,out var id)&&id==final.RoomId);
                var world=view.ResolveForestTransitionPosition(floor,view.CellToWorldCenter(fallback),DungeonRoomType.Final);var cell=view.WorldToCell(world);
                Assert.That(floor.Map.TryGetRoomId(cell,out var room),Is.True);Assert.That(room,Is.EqualTo(final.RoomId));
                Assert.That(floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o=>o.IsActive&&o.BlocksMovement&&o.OccupiesCell(cell)),Is.False);
                Assert.That(new[]{Vector2Int.up,Vector2Int.down}.Count(d=>!floor.Map.IsWalkable(cell+d)),Is.EqualTo(1),"Interaction approach must be immediately inside the boundary.");
                Assert.That(floor.Map.IsWalkable(cell+Vector2Int.left)&&floor.Map.IsWalkable(cell+Vector2Int.right),Is.True);
                foreach(var gate in floor.Connections.Where(c=>c.HasProgressionGate))foreach(var id in gate.RequiredProgressionSourceIds){floor.TryGetObject(id,out var source);Activate(floor,source);}
                var start=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Start);var origin=floor.Map.FloorCells.First(c=>floor.Map.TryGetRoomId(c,out var id)&&id==start.RoomId);
                Assert.That(Reach(floor,origin),Does.Contain(cell));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(171026)] [TestCase(2)] [TestCase(9)] [TestCase(1)]
        [Explicit("Production integration and images in an additive preview; no user save.")]
        public void AuditProductionForestFogLightingLayersNavigationAndExit(int seed)
        {
            Directory.CreateDirectory(AuditOutput);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=AuditOutput,StageOverride="audit-"+seed,SeedOverride=seed};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var view=Read<DungeonFloorMapView>(bootstrap,"floorMapView");var floor=session.ActiveDungeon.Floors.Single();
                var spawner=Read<DungeonObjectSpawner>(bootstrap,"objectSpawner");var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var ground=Read<HashSet<DungeonObjectView>>(rig,"groundObjectViews");
                var props=spawner.SpawnedObjects.Where(v=>v is DungeonSpikeTrapView||v is DungeonConditionView||v is DungeonLeverView).ToArray();
                Assert.That(props.Length,Is.GreaterThan(0));foreach(var prop in props)Assert.That(ground.Contains(prop),Is.True,prop.name+" is missing from forest light receivers.");
                var parent=Read<Transform>(spawner,"objectsRoot")??spawner.transform;
                foreach(var prop in props)foreach(var sr in prop.GetComponentsInChildren<SpriteRenderer>(true))Assert.That(sr.gameObject.layer,Is.EqualTo(parent.gameObject.layer),prop.name);
                var args=new object[]{session.ActiveDungeon,floor,DungeonRoomType.Final,view,Vector2.zero};
                Assert.That((bool)typeof(DungeonSandboxBootstrap).GetMethod("TryGetRoomCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),Is.True);
                Call(bootstrap,"SpawnFloorTransitions",session.ActiveDungeon,floor,(Vector2)args[4],bootstrap.transform);
                var exit=bootstrap.GetComponentsInChildren<DungeonExitView>(true).Single();
                Assert.That(exit.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.enabled&&r.sprite!=null),Is.EqualTo(1));
                Assert.That(exit.UsesStandingPresentation, Is.True);
                var gate = exit.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.enabled&&r.sprite!=null);
                Assert.That(gate.sprite, Is.SameAs(Resources.Load<Sprite>("Godmist/ForestArt/Exploration/exit-gate-v2")));
                Assert.That(Read<List<SpriteRenderer>>(rig,"groundContactPresentations"), Does.Contain(gate));
                var rails=view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Forest exit boundary rail").OrderBy(r=>r.bounds.min.x).ToArray();
                Assert.That(rails.Length,Is.GreaterThan(0),"Gate wings must connect to the woodland boundary.");
                var spans=rails.Select(r=>(a:r.bounds.min.x,b:r.bounds.max.x)).Append((a:gate.bounds.min.x,b:gate.bounds.max.x)).OrderBy(p=>p.a).ToArray();
                var covered=spans[0].b;
                foreach(var span in spans.Skip(1)) { Assert.That(span.a,Is.LessThanOrEqualTo(covered+.02f),"An air gap would make the gate look bypassable.");covered=Mathf.Max(covered,span.b); }
                Assert.That(rails.All(r=>!r.GetComponents<Collider2D>().Any()),Is.True,"The fence may not add blockers on walkable floor.");
                // Later props/crowns reserve additional grounded rows. Static
                // fence wings must follow the same rebuilt lanes as the wicket.
                view.GetStandingObjectDepthSortingOrder(gate.transform.position.y + .123f);
                Call(exit,"LateUpdate");
                foreach(var rail in rails)
                {
                    Assert.That(rail.sortingLayerID,Is.EqualTo(view.FloorFeedbackSortingLayerID));
                    Assert.That(rail.sortingOrder,Is.EqualTo(view.GetStandingObjectDepthSortingOrder(rail.transform.position.y)),
                        "Fence wing retained a stale depth lane after a new baseline was reserved.");
                    Assert.That(rail.sortingOrder,Is.EqualTo(gate.GetComponent<SortingGroup>().sortingOrder),
                        "Gate and fence must share their grounded threshold lane.");
                    Assert.That(view.GetActorDepthSortingOrder(rail.transform.position.y-.1f),Is.GreaterThan(rail.sortingOrder));
                    Assert.That(view.GetActorDepthSortingOrder(rail.transform.position.y+.1f),Is.LessThan(rail.sortingOrder));
                }
                var report=new List<string>{"exit="+exit.transform.position,"props="+props.Length};
                var camera=Read<Camera>(rig,"worldCamera");var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);var active=RenderTexture.active;var old=camera.targetTexture;
                var masks=new List<(GameObject Object,int Layer)>();
                try
                {
                    foreach(var sceneRoot in bootstrap.gameObject.scene.GetRootGameObjects())foreach(var renderer in sceneRoot.GetComponentsInChildren<Renderer>(true))
                        if(!masks.Any(p=>p.Object==renderer.gameObject)){masks.Add((renderer.gameObject,renderer.gameObject.layer));renderer.gameObject.layer=30;}
                    camera.cullingMask=1<<30;camera.targetTexture=target;camera.aspect=16f/9;camera.orthographicSize=4.5f;
                    var samples=props.GroupBy(v=>floor.TryGetObject(v.ObjectInstanceId,out var model)?model.Definition.Id:v.GetType().Name).Select(g=>g.First()).Cast<Component>().Append(exit).ToArray();
                    floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                    foreach(var prop in spawner.SpawnedObjects)typeof(DungeonObjectView).GetMethod("LateUpdate",Flags).Invoke(prop,null);
                    foreach(var sample in samples)
                    {
                        camera.transform.position=new Vector3(sample.transform.position.x,sample.transform.position.y+1,-10);
                        var insideSign=sample is DungeonExitView ? Mathf.Sign(exit.transform.position.y-gate.transform.position.y) : -1;
                        var actors=party.ActiveMemberViews.ToArray();for(var i=0;i<actors.Length;i++)actors[i].SetPosition((Vector2)sample.transform.position+new Vector2(-1+i*.7f,insideSign));
                        Call(view,"LateUpdate");
                        foreach(var propView in spawner.SpawnedObjects)typeof(DungeonObjectView).GetMethod("LateUpdate",Flags).Invoke(propView,null);
                        foreach(var actor in actors)Call(actor,"LateUpdate");Call(rig,"RefreshLighting",2f);
                        if(sample is DungeonObjectView prop)
                        {
                            var renderers=prop.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).ToArray();
                            Assert.That(renderers.All(r=>r.sharedMaterial.shader.name=="Godmist/Dungeon/Silhouette Floor"),Is.True,prop.name+" must use floor-plane lighting: "+string.Join(",",renderers.Select(r=>r.sharedMaterial.shader.name)));
                            report.Add(prop.name+" "+string.Join(",",renderers.Select(r=>r.sharedMaterial.shader.name)));
                        }
                        if(sample is DungeonExitView)
                        {
                            Call(exit,"LateUpdate");
                            Assert.That(gate.sharedMaterial.shader.name, Is.EqualTo("Godmist/Dungeon/Silhouette Floor"), "Exit gate must receive world lighting.");
                            Assert.That(gate.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(view.GetStandingObjectDepthSortingOrder(gate.transform.position.y)));
                        }
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                        var sampleName=sample is DungeonObjectView objectView&&floor.TryGetObject(objectView.ObjectInstanceId,out var model)?model.Definition.Id:sample.GetType().Name;
                        File.WriteAllBytes(Path.Combine(AuditOutput,"production-"+seed+"-"+sampleName+".png"),image.EncodeToPNG());
                        if(sample is DungeonExitView)
                        {
                            AuditExitLightResponse(seed,gate,rails,camera,target,image,report);
                            // Native overlap render from the reachable side.
                            // The opposite side is off-map (actors are hidden by
                            // traversal there); its depth relation is asserted above.
                            foreach(var offset in new[]{insideSign*.15f})
                            {
                                for(var i=0;i<actors.Length;i++)
                                {
                                    actors[i].SetPosition((Vector2)gate.transform.position+new Vector2(-2.2f+i*1.45f,offset));
                                    Call(actors[i],"LateUpdate");
                                }
                                Call(rig,"RefreshLighting",3f+offset);
                                Call(exit,"LateUpdate");
                                foreach(var rail in rails)
                                    Assert.That(rail.sortingOrder,Is.EqualTo(gate.GetComponent<SortingGroup>().sortingOrder),
                                        "Lighting must preserve the rebuilt fence depth lane.");
                                SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                                File.WriteAllBytes(Path.Combine(AuditOutput,"production-"+seed+"-exit-front.png"),image.EncodeToPNG());
                            }
                        }
                    }
                    File.WriteAllLines(Path.Combine(AuditOutput,"production-"+seed+".txt"),report);
                }
                finally{foreach(var entry in masks)if(entry.Object!=null)entry.Object.layer=entry.Layer;camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Normal);
        }
        // Controlled GPU comparison on the actual production receivers. Keep
        // their property blocks, texture/tint and shader; vary only light inputs.
        static void AuditExitLightResponse(int seed,SpriteRenderer gate,SpriteRenderer[] rails,
            Camera camera,RenderTexture target,Texture2D image,List<string> report)
        {
            var output=Path.GetFullPath("../LocalHandoffs/exit-light-response-20261023");Directory.CreateDirectory(output);
            var receivers=rails.Append(gate).ToArray();
            var materials=receivers.Select(r=>r.sharedMaterial).Distinct().ToDictionary(m=>m,m=>new Material(m));
            var layers=receivers.Select(r=>r.gameObject.layer).ToArray();var culling=camera.cullingMask;
            var background=camera.backgroundColor;var clear=camera.clearFlags;
            Color[] dark=null;Color[] lit=null;
            try
            {
                foreach(var receiver in receivers)receiver.gameObject.layer=31;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                foreach(var mode in new[]{"ambient","torch"})
                {
                    foreach(var material in materials.Keys)
                    {
                        Assert.That(material.shader.name,Is.EqualTo("Godmist/Dungeon/Silhouette Floor"));
                        if(mode=="ambient")material.SetInt("_TorchCount",0);
                        else
                        {
                            material.SetInt("_TorchCount",1);
                            material.SetVectorArray("_Torches",Enumerable.Repeat(new Vector4(gate.transform.position.x,gate.transform.position.y+1,5,1),8).ToArray());
                            material.SetVectorArray("_TorchColors",Enumerable.Repeat(new Vector4(1.45f,.55f,.14f,1),8).ToArray());
                        }
                    }
                    SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    if(mode=="ambient")dark=image.GetPixels();else lit=image.GetPixels();
                    File.WriteAllBytes(Path.Combine(output,"gate-"+seed+"-"+mode+".png"),image.EncodeToPNG());
                }
                float L(Color c)=>c.r*.2126f+c.g*.7152f+c.b*.0722f;
                var pixels=Enumerable.Range(0,dark.Length).Where(i=>L(dark[i])>.005f).ToArray();
                Assert.That(pixels.Length,Is.GreaterThan(100));
                var ambient=pixels.Average(i=>L(dark[i]));var torch=pixels.Average(i=>L(lit[i]));
                var changed=pixels.Count(i=>L(lit[i])-L(dark[i])>.01f);
                Assert.That(torch,Is.GreaterThan(ambient*1.15f),"Production gate pixels must brighten under a nearby torch.");
                Assert.That(changed,Is.GreaterThan(pixels.Length*.5f));
                report.Add($"exit light response: ambient luminance={ambient:F4}, torch={torch:F4}, ratio={torch/ambient:F2}, changed={changed}/{pixels.Length}; material ambient={gate.sharedMaterial.GetFloat("_Ambient"):F2}; tint={gate.color}");
                File.WriteAllLines(Path.Combine(output,"response-"+seed+".txt"),report);
            }
            finally
            {
                foreach(var pair in materials){pair.Key.CopyPropertiesFromMaterial(pair.Value);Object.DestroyImmediate(pair.Value);}
                for(var i=0;i<receivers.Length;i++)receivers[i].gameObject.layer=layers[i];
                camera.cullingMask=culling;camera.backgroundColor=background;camera.clearFlags=clear;
            }
        }
    }
}
