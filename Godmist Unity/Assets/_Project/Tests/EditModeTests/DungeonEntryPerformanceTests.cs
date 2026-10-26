using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Data.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonEntryPerformanceTests
    {
        internal Action<DungeonSandboxBootstrap, GameSession> FrameProbe;
        internal string OutputOverride;
        internal string StageOverride;
        internal int SeedOverride = 171026;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void ProgressionControlActivationMeasuresSceneUpdate(string id)
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/progression-interaction-stall-20261022"));
            Directory.CreateDirectory(output);
            var stage=File.Exists(Path.Combine(output,"stage.txt"))?File.ReadAllText(Path.Combine(output,"stage.txt")).Trim():"baseline";
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=output,StageOverride=stage,SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var view=Field<DungeonFloorMapView>(bootstrap,"floorMapView");var floor=session.ActiveDungeon.Floors[0];
                var gate=floor.Connections.First(c=>c.HasProgressionGate&&!c.IsProgressionGateUnlocked);
                var center=(view.CellToWorldCenter(gate.FirstDoorCell)+view.CellToWorldCenter(gate.SecondDoorCell))*.5f;
                var normal=gate.SecondDoorCell-gate.FirstDoorCell;var lateral=normal.x!=0?Vector2Int.up:Vector2Int.right;
                var gatePoints=Enumerable.Range(0,gate.DoorwayWidth).Select(i=>(Vector3)((view.CellToWorldCenter(gate.FirstDoorCell+lateral*i)+view.CellToWorldCenter(gate.SecondDoorCell+lateral*i))*.5f)).ToArray();
                floor.Fog.SetVisible(floor.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));
                floor.Fog.ClosedGates.Clear();floor.Fog.ClosedGates.UnionWith(floor.Map.SurfaceClosedProgressionGateEdges);view.ApplyFog(floor);
                var far=view.GetComponentsInChildren<Renderer>(true).Where(r=>Vector2.Distance(r.transform.position,center)>8).ToArray();
                int rebuilds=view.MapRebuildCount;
                view.MeasureFogPhaseTimings=true;
                if(stage=="baseline")
                {
                    // Controlled legacy path, with the same map/content/fog/controls.
                    var local=(Action)typeof(DungeonFloorMapView).GetMethod("HandleProgressionGateStateChanged",Private).CreateDelegate(typeof(Action),view);
                    floor.Map.ProgressionGateStateChanged-=local;
                    floor.Map.ProgressionGateStateChanged+=()=>view.Rebuild(floor.Map);
                }
                var sources=gate.RequiredProgressionSourceIds.ToArray();
                var gateController=Field<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                int boundsCaptured=view.NavigationBoundsCaptureCount,externalCollections=gateController.NavigationExternalCollectionCount;
                for(int i=0;i<sources.Length-1;i++)Activate(sources[i]);
                Assert.That(gate.IsProgressionGateUnlocked,Is.False);
                var beforeBounds=view.NavigationWallBounds(gate.DoorwayLevel).ToArray();
                Assert.That(gatePoints.All(p=>beforeBounds.Any(b=>b.Contains(p))),Is.True,"Closed barrier needs physical blocking on every gate cell.");
                var timer=Stopwatch.StartNew();Activate(sources.Last());double activation=timer.Elapsed.TotalMilliseconds;
                timer.Restart();view.ApplyFog(floor);double fog=timer.Elapsed.TotalMilliseconds;
                var controller=Field<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var actor=((System.Collections.IDictionary)typeof(ExplorationPartyController).GetField("viewsById",Private).GetValue(controller)).Values.Cast<ExplorationCharacterView>().First();
                timer.Restart();
                var navigation=(DungeonNavigationMesh)typeof(ExplorationPartyController).GetMethod("GetNavigationMesh",Private).Invoke(controller,new object[]{actor,actor.DungeonPosition.Surface.Level});
                double navigationSchedule=timer.Elapsed.TotalMilliseconds;
                Assert.That(navigation.IsBuilding || navigation.IsReady,Is.True);
                var navigationOutput=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/navigation-preparation-20261022"));
                Directory.CreateDirectory(navigationOutput);
                var navigationStage=File.Exists(Path.Combine(navigationOutput,"stage.txt"))?File.ReadAllText(Path.Combine(navigationOutput,"stage.txt")).Trim():"baseline";
                File.WriteAllText(Path.Combine(navigationOutput,navigationStage+"-"+id+".txt"),
                    "scheduleMs="+navigationSchedule.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\n"+
                    "geometryMs="+navigation.GeometryBuildMilliseconds.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\n"+
                    "sources="+navigation.BuildSourceCount+"\n"+
                    "staticGeometryBuilds="+navigation.StaticGeometryBuildCount+"\n"+
                    "colliderBoundsCaptured="+(view.NavigationBoundsCaptureCount-boundsCaptured)+"\n"+
                    "externalCollections="+(controller.NavigationExternalCollectionCount-externalCollections)+"\n"+
                    string.Join("\n",navigation.PreparationPhaseMilliseconds.Select((ms,i)=>i+"="+ms.ToString("F4",System.Globalization.CultureInfo.InvariantCulture))));
                if(navigationStage!="baseline")
                {
                    Assert.That(navigation.StaticGeometryBuildCount,Is.EqualTo(1),"A gate cannot rebuild static source geometry.");
                    Assert.That(controller.NavigationExternalCollectionCount,Is.EqualTo(externalCollections),"A gate cannot rescan every prop/collider in the scene.");
                    Assert.That(view.NavigationBoundsCaptureCount-boundsCaptured,Is.LessThan(32),"Only newly replaced local walls may recapture their collider bounds.");
                }
                int retained=far.Count(r=>r!=null);
                string line=string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"activationMs\":{0:F4},\"fogMs\":{1:F4},\"farRenderers\":{2},\"retainedFarRenderers\":{3},\"navigationScheduleMs\":{4:F4}",activation,fog,far.Length,retained,navigationSchedule)+"}";
                File.WriteAllText(Path.Combine(output,stage+"-"+id+".json"),line);TestContext.WriteLine(line);
                File.WriteAllLines(Path.Combine(output,stage+"-"+id+"-phases.txt"),view.ProgressionGatePhaseTicks.Select((ticks,i)=>i+": "+(ticks*1000d/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" ms"));
                TestContext.WriteLine("Replaced native walls="+view.LastGateWallReplacementCount);
                Assert.That(gate.IsProgressionGateUnlocked,Is.True);
                for(int i=0;i<gate.DoorwayWidth;i++)Assert.That(floor.Map.CanMoveBetween(gate.FirstDoorCell+lateral*i,gate.SecondDoorCell+lateral*i),Is.True);
                Assert.That(view.NavigationWallBounds(gate.DoorwayLevel).Any(b=>gatePoints.Any(p=>b.Contains(p))),Is.False,"Opening must remove physical blockers immediately too.");
                if(stage!="baseline")
                {
                    Assert.That(retained,Is.EqualTo(far.Length),"Activating a control must preserve unrelated scene renderers.");
                    Assert.That(view.MapRebuildCount,Is.EqualTo(rebuilds),"Normal control activation must not rebuild the map.");
                    VerifyGateTerrain();
                }

                void VerifyGateTerrain()
                {
                    var lighting=Field<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                    var gameCamera=Field<Camera>(lighting,"worldCamera");
                    var camera=new GameObject("Gate terrain verification camera").AddComponent<Camera>();
                    camera.transform.SetParent(view.transform,false);view.ConfigurePresentationCamera(camera);
                    typeof(DungeonSilhouetteLighting).GetMethod("OnDisable",Private).Invoke(lighting,null);
                    lighting.enabled=false;
                    var oldActive=RenderTexture.active;
                    var target=new RenderTexture(512,512,24);var pixels=new Texture2D(512,512,TextureFormat.RGBA32,false);
                    try
                    {
                        camera.orthographic=true;camera.orthographicSize=8;camera.aspect=1;
                        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                        camera.transform.position=new Vector3(center.x,center.y,-10);camera.targetTexture=target;camera.cullingMask=1<<30;
                        Color32[] Render(string label)
                        {
                            CollectionAssert.AreEquivalent(view.NavigationWallBounds(gate.DoorwayLevel).ToArray(),
                                view.CachedNavigationWallBounds(gate.DoorwayLevel).ToArray(),"Numeric terrain cache must match live colliders after every gate transition.");
                            floor.Fog.ClosedGates.Clear();floor.Fog.ClosedGates.UnionWith(floor.Map.SurfaceClosedProgressionGateEdges);
                            view.ApplyFog(floor);
                            // Keep authored materials/property blocks: forcing one material across
                            // native tiled sprites corrupts the comparison's texture bindings.
                            foreach(var renderer in view.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();RenderTexture.active=target;GL.Clear(true,true,Color.black);
                            camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();
                            File.WriteAllBytes(Path.Combine(output,stage+"-"+id+"-"+label+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
                        }
                        var local=Render("local-open");
                        var edges=Enumerable.Range(0,gate.DoorwayWidth).Select(i=>new DungeonSurfaceEdge(new DungeonCellEdge(gate.FirstDoorCell+lateral*i,gate.SecondDoorCell+lateral*i),gate.DoorwayLevel)).ToArray();
                        Assert.That(floor.Map.TrySetProgressionGateState(edges,false),Is.True);var closedImage=Render("local-closed");
                        for(int i=0;i<gate.DoorwayWidth;i++)Assert.That(floor.Map.CanMoveBetween(gate.FirstDoorCell+lateral*i,gate.SecondDoorCell+lateral*i),Is.False);
                        Assert.That(gatePoints.All(p=>view.NavigationWallBounds(gate.DoorwayLevel).Any(b=>b.Contains(p))),Is.True);
                        Assert.That(floor.Map.TrySetProgressionGateState(edges,true),Is.True);var reopened=Render("local-reopened");
                        Assert.That(view.MapRebuildCount,Is.EqualTo(rebuilds),"Closing/reopening an existing barrier must stay local too.");
                        view.Rebuild(floor.Map);var reference=Render("full-rebuild-reference");
                        int Differences(Color32[] a,Color32[] b)=>Enumerable.Range(0,a.Length).Count(i=>Math.Abs(a[i].r-b[i].r)>2||Math.Abs(a[i].g-b[i].g)>2||Math.Abs(a[i].b-b[i].b)>2||Math.Abs(a[i].a-b[i].a)>2);
                        int roundtrip=Differences(local,reopened),fresh=Differences(local,reference),closedDifference=Differences(local,closedImage);
                        File.WriteAllText(Path.Combine(output,id+"-pixel-differences.txt"),"roundtrip="+roundtrip+"; fresh="+fresh+"; closed="+closedDifference);
                        Assert.That(closedDifference,Is.GreaterThan(32),"The barrier must actually be visible in the render comparison.");
                        Assert.That(roundtrip,Is.Zero,"Gate close/open must restore the same terrain image.");
                        Assert.That(fresh,Is.Zero,"Local gate update must match a fresh full terrain rebuild.");
                    }
                    finally
                    {
                        camera.targetTexture=null;RenderTexture.active=oldActive;
                        UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);
                        view.ConfigurePresentationCamera(gameCamera);UnityEngine.Object.DestroyImmediate(camera.gameObject);
                    }
                }
                void Activate(string sourceId)
                {
                    Assert.That(floor.TryGetObject(sourceId,out var source),Is.True);
                    if(source is LeverDungeonObjectInstance lever)Assert.That(lever.TryToggle(out _),Is.True);
                    else if(source is ConditionDungeonObjectInstance condition)Assert.That(condition.TrySatisfy(),Is.True);
                    else Assert.Fail("Expected lever or condition source.");
                    Assert.That(floor.TrySatisfyProgressionCondition(gate.ProgressionConditionId,sourceId),Is.True);
                }
            };
            fixture.MeasureProductionFloorEntry(id,DungeonLength.Long);
        }

        [Test]
        public void EndpointLookupTracksGateOpeningElevationAndMapReplacement()
        {
            var root=new GameObject("Wall endpoint index");
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero,"room"); map.TryAddFloorCell(Vector2Int.right,"room");
                typeof(DungeonFloorMapView).GetField("boundMap",Private).SetValue(view,map);
                var query=typeof(DungeonFloorMapView).GetMethod("WallsAtEndpoint",Private);
                Godmist.Runtime.Dungeons.DungeonSurfaceEdge[] Read(int level=0) =>
                    ((System.Collections.Generic.IEnumerable<Godmist.Runtime.Dungeons.DungeonSurfaceEdge>)query.Invoke(view,new object[]{level,new Vector2Int(1,0)})).ToArray();
                var edge=new Godmist.Runtime.Dungeons.DungeonCellEdge(Vector2Int.zero,Vector2Int.right);
                Assert.That(Read(),Is.Empty);
                map.TrySetProgressionGateState(edge,false);
                Assert.That(Read().Select(e=>e.CellEdge),Is.EqualTo(new[]{edge}));
                Assert.That(Read(1),Is.Empty);
                map.TrySetProgressionGateState(edge,true);
                Assert.That(Read(),Is.Empty);
                var replacement=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                replacement.TryAddFloorCell(Vector2Int.zero,"room"); replacement.TryAddFloorCell(Vector2Int.right,"room");
                replacement.TrySetProgressionGateState(edge,false);
                typeof(DungeonFloorMapView).GetField("boundMap",Private).SetValue(view,replacement);
                Assert.That(Read(),Has.Length.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        public void MeasureProductionFloorEntry(string id, DungeonLength length)
        {
            var output = OutputOverride ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/dungeon-entry-pass-20261016"));
            Directory.CreateDirectory(output);
            var stage = StageOverride ?? (File.Exists(Path.Combine(output,"stage.txt")) ? File.ReadAllText(Path.Combine(output,"stage.txt")).Trim() : "after");
            var log = Path.Combine(output, stage + "-" + id + "-" + length + ".txt");
            File.WriteAllText(log, "");
            var total = Stopwatch.StartNew();
            void Measure(string name, Action action)
            {
                File.AppendAllText(log, name + " started\n");
                var timer = Stopwatch.StartNew(); action();
                var result = $"{name}: {timer.Elapsed.TotalSeconds:F3}s";
                File.AppendAllText(log, result + "\n"); TestContext.WriteLine(result);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            Godmist.Runtime.Dungeons.DungeonInstance dungeon = null;
            Measure("layout", () => dungeon = new DungeonGeneratorService().Generate(definition, SeedOverride, 10, length));
            Measure("encounters", () => new DungeonEncounterPlacementSystem().Place(dungeon));
            Measure("objects", () => new DungeonObjectPlacementSystem().Place(dungeon, catalog.EquipmentPartDefinitions));
            Measure("rewards", () => new DungeonEncounterRewardPreparationSystem().Prepare(dungeon, catalog.EquipmentPartDefinitions));
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity", OpenSceneMode.Additive);
            DungeonSandboxBootstrap bootstrap = null;
            var profilerWasEnabled = UnityEditorInternal.ProfilerDriver.enabled;
            UnityEditorInternal.ProfilerDriver.enabled = false;
            try
            {
                bootstrap = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonSandboxBootstrap>(true)).Single();
                var view = Field<DungeonFloorMapView>(bootstrap, "floorMapView");
                var floor = dungeon.Floors[0];
                typeof(DungeonSandboxBootstrap).GetMethod("ConfigureFloorPresentation",Private).Invoke(bootstrap,new object[]{definition,floor,false});
                view.PrepareForestTransitionBoundaries(floor);
                Measure("map presentation", () => view.Rebuild(floor.Map, deferForestGround: true));
                var session = new GameSession(GameModeState.Exploration);
                var factory = new CharacterInstanceFactory();
                foreach (var name in new[]{"Jerzy","Wladimir_Test","Mage","Paladin"})
                {
                    var character = factory.Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"Assets/_Project/ScriptableObjects/Characters/CD_{name}.asset"));
                    session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
                }
                session.TrySetActiveDungeon(dungeon);
                var party = Field<DungeonPartySpawner>(bootstrap,"partySpawner");
                var objects = Field<DungeonObjectSpawner>(bootstrap,"objectSpawner");
                var encounters = Field<DungeonEncounterSpawner>(bootstrap,"encounterSpawner");
                var start = floor.Rooms.First(r => r.RoomType == DungeonRoomType.Start);
                Measure("party spawn", () => Assert.That(party.TrySpawn(session, view.CellToWorldCenter(start.LayoutPosition + Vector2Int.one * 2)), Is.True));
                Measure("object views", () => Assert.That(objects.Rebuild(floor,party.PartyController,definition.Theme,definition.Id),Is.True));
                Measure("enemy views", () => Assert.That(encounters.Rebuild(floor,party.PartyController,10),Is.True));
                var coordinator=Field<DungeonCombatCoordinator>(bootstrap,"combatCoordinator");
                coordinator.InitializationStageMeasured=(name,ms)=>File.AppendAllText(log,$"setup.{name}: {ms/1000d:F3}s\n");
                Measure("coordinator and navigation", () => Assert.That(coordinator.Initialize(session,floor,view,party.PartyController,encounters,objects),Is.True));
                Measure("forest mask", () => view.SetForestPathObstacles(floor.Objects.OfType<Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance>().Where(o=>o.IsActive && o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells())));
                Measure("presentation occupancy", () => view.SetPresentationOccupancy(floor.Objects.Select(o=>o.Position)));
                Measure("lighting", () => Field<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting").Initialize(party.PartyController,encounters,objects,view));
                FrameProbe?.Invoke(bootstrap, session);
                File.AppendAllText(log, $"total: {total.Elapsed.TotalSeconds:F3}s\nrooms: {floor.Rooms.Count}\ncells: {floor.Map.FloorCells.Count}\n");
                if(stage=="after") Assert.That(total.Elapsed.TotalSeconds,Is.LessThan(40),"Floor entry must not regress to minute-long builds.");
            }
            finally
            {
                // Probe methods initialize the non-ExecuteAlways rig manually.
                // EditMode teardown must release its RT arrays/materials even
                // when Unity never sent OnEnable/OnDisable for this component.
                try
                {
                    if (bootstrap != null)
                    {
                        var rig = Field<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                        if (rig != null) typeof(DungeonSilhouetteLighting).GetMethod("OnDisable",Private).Invoke(rig,null);
                    }
                }
                finally
                {
                    if (bootstrap != null)
                    {
                        // Non-ExecuteAlways controllers do not receive runtime
                        // destruction callbacks reliably in EditMode captures.
                        var partySpawner = Field<DungeonPartySpawner>(bootstrap, "partySpawner");
                        if (partySpawner != null && partySpawner.PartyController != null)
                            typeof(ExplorationPartyController).GetMethod("DisposeNavigationMeshes", Private)
                                .Invoke(partySpawner.PartyController, null);
                        var view = Field<DungeonFloorMapView>(bootstrap,"floorMapView");
                        if (view != null) typeof(DungeonFloorMapView).GetMethod("ReleasePresentationCamera",Private).Invoke(view,null);
                    }
                    EditorSceneManager.CloseScene(scene,true);
                    UnityEditorInternal.ProfilerDriver.enabled = profilerWasEnabled;
                    EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }
        }
    }
}

