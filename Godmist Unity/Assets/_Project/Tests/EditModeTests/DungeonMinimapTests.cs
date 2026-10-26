using System;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class DungeonMinimapTests
    {
        [Test]
        public void OverviewClaimsWorkspaceInputWhileOpenAndOnClosingFrame()
        {
            using var f=new OverviewFixture();
            var previous=Godmist.Views.UI.Workspace.UIWorkspaceWindowManager.Instance;
            var owner=new GameObject("overview workspace input");
            var manager=owner.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceWindowManager>();
            try
            {
                if(previous!=null)typeof(Godmist.Views.UI.Workspace.UIWorkspaceWindowManager)
                    .GetProperty("Instance").SetValue(null,null);
                Call(manager,"Awake");
                f.Map.TryAddFloorCell(Vector2Int.zero,"room");f.Floor.Fog.SetVisible(new[]{At(0)});
                f.View.Bind(f.Floor,f.Presentation,f.Party,null);
                f.View.SetOverviewOpen(true);
                Assert.That(manager.ConsumedContextMenuInputThisFrame,Is.True);
                Set(manager,"contextMenuInputFrame",-1);
                typeof(DungeonMinimapView).GetMethod("HandleOverviewInput",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(f.View,new object[]{false,false});
                Assert.That(manager.ConsumedContextMenuInputThisFrame,Is.True,"Background windows cannot handle Escape while the map owns input.");
                Set(manager,"contextMenuInputFrame",-1);
                f.View.SetOverviewOpen(false);
                Assert.That(manager.ConsumedContextMenuInputThisFrame,Is.True,"Closing the map must not close a background window too.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                typeof(Godmist.Views.UI.Workspace.UIWorkspaceWindowManager).GetProperty("Instance").SetValue(null,previous);
            }
        }

        [Test]
        public void MapShortcutIgnoresTextInputAndEscapeRestoresWorldInputAfterCloseFrame()
        {
            using var f=new OverviewFixture();
            f.Map.TryAddFloorCell(Vector2Int.zero,"room");f.Floor.Fog.SetVisible(new[]{At(0)});
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);
            var previousEvents=UnityEngine.EventSystems.EventSystem.current;
            var eventsObject=new GameObject("overview input test",typeof(UnityEngine.EventSystems.EventSystem));
            var events=eventsObject.GetComponent<UnityEngine.EventSystems.EventSystem>();
            // EventSystem is not ExecuteAlways: register its EditMode lifecycle
            // before assigning current, as the normal runtime OnEnable does.
            bool enableEvents=true;
            typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
            UnityEngine.EventSystems.EventSystem.current=events;
            try
            {
                var input=new GameObject("focused text",typeof(RectTransform),typeof(TMPro.TMP_InputField));input.transform.SetParent(f.Root.transform,false);
                input.GetComponent<TMPro.TMP_InputField>().enabled=false;
                events.SetSelectedGameObject(input);
                Assert.That(events.currentSelectedGameObject,Is.SameAs(input));
                Assert.That(Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedTextInput(),Is.True);
                var handle=typeof(DungeonMinimapView).GetMethod("HandleOverviewInput",BindingFlags.Instance|BindingFlags.NonPublic);
                handle.Invoke(f.View,new object[]{true,false});Assert.That(f.View.IsOverviewOpen,Is.False);
                events.SetSelectedGameObject(null);
                handle.Invoke(f.View,new object[]{true,false});Assert.That(f.View.IsOverviewOpen,Is.True);
                Assert.That(f.Party.CanAcceptExplorationInput,Is.False);
                handle.Invoke(f.View,new object[]{false,true});Assert.That(f.View.IsOverviewOpen,Is.False);
                Assert.That(f.Party.IsMapOverviewBlockingInput,Is.True,"Closing Escape must not fall through to a world action in the same frame.");
                Set(f.Party,"mapOverviewInputFrame",-1);
                Assert.That(f.Party.CanAcceptExplorationInput,Is.True);
                handle.Invoke(f.View,new object[]{true,false});Assert.That(f.View.IsOverviewOpen,Is.True);
                Set(f.Party,"gameSession",new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Combat));
                handle.Invoke(f.View,new object[]{false,false});Assert.That(f.View.IsOverviewOpen,Is.False,"A combat transition restores the compact map.");
            }
            finally
            {
                if(enableEvents)typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
                if(previousEvents!=null)UnityEngine.EventSystems.EventSystem.current=previousEvents;
                UnityEngine.Object.DestroyImmediate(eventsObject);
            }
        }
        [Test]
        public void OverviewFramesOnlyKnownTerrainAndRestoresCollapsedMinimapOnReopenAndRebind()
        {
            using var f=new OverviewFixture();
            for(int x=-8;x<=8;x++)for(int y=-4;y<=4;y++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"known");
            f.Map.TryAddFloorCell(new Vector2Int(900,700),"hidden");
            f.Map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(-900,-700),1),"upper");
            f.Floor.Fog.SetVisible(f.Map.Surfaces.Where(s=>s.Level==0&&s.Cell.x<20).Select(s=>new DungeonTraversalPosition(s)));
            f.Floor.Fog.RestoreDiscovered(f.Map,new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(-900,-700),1)));
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);
            Call(f.View,"Toggle");
            var content=(GameObject)Get(f.View,"content");Assert.That(content.activeSelf,Is.False);
            var before=f.View.GetComponentsInChildren<RectTransform>(true).Select(r=>(r,r.anchorMin,r.anchorMax,r.pivot,r.anchoredPosition,r.sizeDelta)).ToArray();
            var parent=f.View.transform.parent;var sibling=f.View.transform.GetSiblingIndex();
            f.View.SetOverviewOpen(true);f.Tick();
            Assert.That(f.View.IsOverviewOpen,Is.True);Assert.That(content.activeSelf,Is.True);
            Assert.That(f.Party.IsMapOverviewBlockingInput,Is.True);
            Assert.That(f.View.OverviewDiscoveredBounds,Is.EqualTo(new RectInt(-8,-4,17,9)));
            Assert.That(f.Floor.Fog.Discovered.Count,Is.EqualTo(154),"Opening the map must not discover new terrain.");
            var overview=((RawImage)Get(f.View,"image")).texture;
            Assert.That(overview.width,Is.LessThanOrEqualTo(1024));Assert.That(overview.height,Is.LessThanOrEqualTo(1024));
            var mapRect=((RawImage)Get(f.View,"image")).rectTransform.rect;
            Assert.That((float)overview.width/overview.height,Is.EqualTo(mapRect.width/mapRect.height).Within(.005f),"Map cells must retain their proportions.");
            var raster=typeof(DungeonMinimapView).GetMethod("RasterPoint",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var cell in new[]{new Vector2(-8,-4),new Vector2(8,4)})
            {
                var p=(Vector2)raster.Invoke(f.View,new object[]{cell});
                Assert.That(p.x,Is.InRange(1f,overview.width-1f));Assert.That(p.y,Is.InRange(1f,overview.height-1f));
            }
            f.View.SetOverviewOpen(false);
            Assert.That(content.activeSelf,Is.False);Assert.That(f.View.transform.parent,Is.SameAs(parent));
            Assert.That(f.View.transform.GetSiblingIndex(),Is.EqualTo(sibling));
            foreach(var state in before)
            {
                Assert.That((state.r.anchorMin,state.r.anchorMax,state.r.pivot,state.r.anchoredPosition,state.r.sizeDelta),
                    Is.EqualTo((state.anchorMin,state.anchorMax,state.pivot,state.anchoredPosition,state.sizeDelta)));
            }
            f.View.SetOverviewOpen(true);f.Tick();
            Assert.That(((RawImage)Get(f.View,"image")).texture,Is.SameAs(overview),"Reopening reuses and actually binds the overview texture.");
            f.View.Bind(null,null,null,null);Assert.That(f.View.IsOverviewOpen,Is.False);
            Assert.That(f.Party.DungeonMinimap,Is.Null);Assert.That(content.activeSelf,Is.False);
        }

        [Test]
        public void OverviewCachesUnchangedFramesAndTracksDiscoveryAndHeightWithoutLeakingHiddenFloor()
        {
            using var f=new OverviewFixture();
            for(int x=-100;x<100;x++)for(int y=-100;y<100;y++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"large");
            f.Map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(80,80),1),"upper");
            f.Floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero))});
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);f.View.SetOverviewOpen(true);f.Tick();
            Assert.That(f.View.OverviewDiscoveredBounds,Is.EqualTo(new RectInt(0,0,1,1)));
            int refreshes=f.View.OverviewTerrainRefreshCount;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<120;i++)f.Tick();
            for(int i=0;i<30;i++){f.Floor.Fog.SetVisible(new[]{At(0)});f.Tick();}
            Assert.That(f.View.OverviewTerrainRefreshCount,Is.EqualTo(refreshes),"Stationary UI frames must not rerasterize or upload terrain.");
            TestContext.WriteLine($"120 unchanged overview frames: {timer.Elapsed.TotalMilliseconds:F3} ms (includes reflection).");
            f.Floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(30,20)))});f.Tick();
            Assert.That(f.View.OverviewDiscoveredBounds,Is.EqualTo(new RectInt(0,0,31,21)));
            ((DungeonElevationViewState)Get(f.Presentation,"elevation")).Bind(f.Map);
            f.Presentation.FollowElevation(1,true);f.Tick();
            Assert.That(((Color32[])Get(f.View,"overviewPixels")).All(c=>c.Equals((Color32)Color.black)),Is.True,"An unexplored upper level must stay black.");
            f.Floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(80,80),1))});f.Tick();
            Assert.That(f.View.OverviewDiscoveredBounds,Is.EqualTo(new RectInt(80,80,1,1)));
        }

        [Test]
        public void OverviewProductionPrefabRenderAndLargeDiscoveryCost()
        {
            using var f=new OverviewFixture();
            var known=new System.Collections.Generic.List<DungeonTraversalPosition>();
            // Connected rooms/corridors with a large unvisited branch outside the frame.
            for(int ry=0;ry<5;ry++)for(int rx=0;rx<7;rx++)
                for(int y=0;y<10;y++)for(int x=0;x<12;x++)
                {
                    var cell=new Vector2Int(rx*17+x-55,ry*15+y-32);f.Map.TryAddFloorCell(cell,"room");
                    known.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)));
                }
            for(int y=-32;y<=37;y++)for(int x=-55;x<=58;x++)
                if(y%15==-12 || x%17==-5 || y==2)
                {var cell=new Vector2Int(x,y);f.Map.TryAddFloorCell(cell,"corridor");known.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)));}
            f.Map.TryAddFloorCell(new Vector2Int(400,0),"unknown branch");
            f.Floor.Fog.SetVisible(known);
            f.Floor.Fog.SetVisible(known.Where(n=>n.Surface.Cell.x< -30 && n.Surface.Cell.y< -10));
            f.Actor.ConfigureTerrain(f.Map,f.Presentation,f.Floor);
            f.Actor.SetPosition(f.Presentation.CellToWorldCenter(new Vector2Int(-50,-28)));
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);f.Tick();
            var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../LocalHandoffs/map-overview-20261022"));
            System.IO.Directory.CreateDirectory(output);
            f.Capture(System.IO.Path.Combine(output,"minimap-before.png"));
            var timer=System.Diagnostics.Stopwatch.StartNew();f.View.SetOverviewOpen(true);f.Tick();double opening=timer.Elapsed.TotalMilliseconds;
            f.Capture(System.IO.Path.Combine(output,"overview.png"));
            var samples=new double[12];
            for(int i=0;i<samples.Length;i++)
            {
                f.Floor.Fog.SetVisible(known.Where(n=>n.Surface.Cell.x< -30+i));
                timer.Restart();f.Tick();samples[i]=timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            var report=$"known={f.Floor.Fog.Discovered.Count}; opening+layout+first-upload={opening:F3}ms; changed-fog median={samples[6]:F3}ms; max={samples.Last():F3}ms; texture={((RawImage)Get(f.View,"image")).texture.width}x{((RawImage)Get(f.View,"image")).texture.height}";
            TestContext.WriteLine(report);System.IO.File.WriteAllText(System.IO.Path.Combine(output,"timings.txt"),report);
            f.View.SetOverviewOpen(false);f.Tick();f.Capture(System.IO.Path.Combine(output,"minimap-restored.png"));
            Assert.That(f.View.OverviewDiscoveredBounds.xMax,Is.LessThan(400));
        }

        [Test]
        public void CompactMapFollowsCameraInsteadOfLeaderAndScrollsWithoutTerrainUpload()
        {
            using var f=new OverviewFixture();
            for(int y=-20;y<=20;y++)for(int x=-20;x<=40;x++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"known");
            f.Floor.Fog.SetVisible(f.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));
            f.WorldCamera.transform.position=new Vector3(20.25f,-8.75f,-10);
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);f.Tick();
            var image=(RawImage)Get(f.View,"image");var markers=(DungeonMinimapMarkers)Get(f.View,"markers");
            Assert.That((Vector2Int)Get(f.View,"center"),Is.EqualTo(new Vector2Int(20,-9)));
            Assert.That((bool)Get(markers,"hasCameraFrame"),Is.True);
            var corners=(Vector2[])Get(markers,"cameraFrame");
            Assert.That(corners[0].x,Is.EqualTo(-6*16f/9*8).Within(.001f));
            Assert.That(corners[0].y,Is.EqualTo(-6*8).Within(.001f));
            Assert.That((corners[2]+corners[0]).magnitude,Is.LessThan(.002f));
            var viewport=image.uvRect;var terrain=((Texture2D)image.texture).GetPixels32();
            f.Actor.SetPosition(new Vector2(35.5f,18.5f));f.Tick();
            Assert.That(image.uvRect,Is.EqualTo(viewport),"Changing the leader position must not move the minimap.");
            f.WorldCamera.transform.position+=new Vector3(.125f,.0625f);f.Tick();
            Assert.That(image.uvRect.x-viewport.x,Is.EqualTo(.125f*8/252).Within(.000001f));
            Assert.That(image.uvRect.y-viewport.y,Is.EqualTo(.0625f*8/252).Within(.000001f));
            Assert.That(((Texture2D)image.texture).GetPixels32(),Is.EqualTo(terrain),"Sub-cell camera movement scrolls UVs, not the terrain raster.");
            f.WorldCamera.orthographicSize=3;f.WorldCamera.aspect=2;f.Tick();
            Assert.That(corners[0].x,Is.EqualTo(-48).Within(.001f));Assert.That(corners[0].y,Is.EqualTo(-24).Within(.001f));
            f.WorldCamera.transform.position=new Vector3(-12.5f,7.5f,-10);
            Call(f.View,"OnEnable");Canvas.ForceUpdateCanvases();
            Assert.That((Vector2Int)Get(f.View,"center"),Is.EqualTo(new Vector2Int(-13,7)),"The render callback observes the final camera pose, after camera LateUpdate.");
        }

        [Test]
        public void OverviewProjectsCameraWithoutReframingOrRevealingHiddenTerrain()
        {
            using var f=new OverviewFixture();
            for(int y=-10;y<=20;y++)for(int x=-20;x<=40;x++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"known");
            f.Floor.Fog.SetVisible(f.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));
            f.Map.TryAddFloorCell(new Vector2Int(800,800),"unknown");
            f.WorldCamera.transform.position=new Vector3(9.5f,5.5f,-10);
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);f.View.SetOverviewOpen(true);f.Tick();
            var markers=(DungeonMinimapMarkers)Get(f.View,"markers");var image=(RawImage)Get(f.View,"image");
            var corners=(Vector2[])Get(markers,"cameraFrame");var first=corners.ToArray();
            var bounds=f.View.OverviewDiscoveredBounds;int refreshes=f.View.OverviewTerrainRefreshCount;
            int known=f.Floor.Fog.Discovered.Count;var pixels=((Texture2D)image.texture).GetPixels32();
            var projectedCenter=(first[0]+first[2])*.5f;
            float scale=(float)Get(f.View,"overviewScale")*image.rectTransform.rect.width/image.texture.width;
            float scaleY=(float)Get(f.View,"overviewScale")*image.rectTransform.rect.height/image.texture.height;
            Assert.That(projectedCenter.x,Is.EqualTo(-scale).Within(.002f));Assert.That(projectedCenter.y,Is.EqualTo(0).Within(.002f));
            f.WorldCamera.transform.position+=new Vector3(7,-3);f.WorldCamera.orthographicSize*=.5f;f.Tick();
            Assert.That(((corners[0]+corners[2])*.5f-projectedCenter-new Vector2(7*scale,-3*scaleY)).magnitude,Is.LessThan(.002f));
            Assert.That((corners[2]-corners[0]).magnitude,Is.EqualTo((first[2]-first[0]).magnitude*.5f).Within(.002f));
            Assert.That(f.View.OverviewDiscoveredBounds,Is.EqualTo(bounds));Assert.That(f.View.OverviewTerrainRefreshCount,Is.EqualTo(refreshes));
            Assert.That(f.Floor.Fog.Discovered.Count,Is.EqualTo(known));Assert.That(((Texture2D)image.texture).GetPixels32(),Is.EqualTo(pixels));
            f.Presentation.ConfigurePresentationCamera(null);f.Tick();Assert.That((bool)Get(markers,"hasCameraFrame"),Is.False);
            f.Presentation.ConfigurePresentationCamera(f.WorldCamera);f.View.SetOverviewOpen(false);f.Tick();
            Assert.That((bool)Get(markers,"hasCameraFrame"),Is.True);Assert.That(image.uvRect.width,Is.EqualTo(220f/252));
            f.View.Bind(null,null,null,null);Assert.That((bool)Get(markers,"hasCameraFrame"),Is.False);
        }

        [Test]
        public void CameraOutlineIsWhiteHollowClippedAndOnlyDirtiesWhenChanged()
        {
            var root=new GameObject("Camera outline",typeof(RectTransform),typeof(CanvasRenderer),typeof(DungeonMinimapMarkers));
            try
            {
                var graphic=root.GetComponent<DungeonMinimapMarkers>();graphic.rectTransform.sizeDelta=new Vector2(100,100);
                int dirties=0;graphic.RegisterDirtyVerticesCallback(()=>dirties++);
                void Frame(float left,float right,float bottom,float top)
                {graphic.SetCameraFrame(new Vector2(left,bottom),new Vector2(right,bottom),new Vector2(right,top),new Vector2(left,top));graphic.Refresh();}
                Frame(-30,30,-20,20);Assert.That(dirties,Is.EqualTo(1));
                for(int i=0;i<80;i++)Frame(-30,30,-20,20);
                Assert.That(dirties,Is.EqualTo(1),"A stationary frame must not rebuild the UI mesh.");
                using var mesh=new VertexHelper();
                void Populate()=>typeof(DungeonMinimapMarkers).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(graphic,new object[]{mesh});
                Populate();Assert.That(mesh.currentVertCount,Is.EqualTo(16));
                var vertex=new UIVertex();
                for(int i=0;i<mesh.currentVertCount;i++)
                {mesh.PopulateUIVertex(ref vertex,i);Assert.That(vertex.color,Is.EqualTo((Color32)Color.white));Assert.That(Mathf.Abs(vertex.position.x)>=29||Mathf.Abs(vertex.position.y)>=19,Is.True,"The center must remain empty.");}
                Frame(-80,80,-80,80);Populate();Assert.That(mesh.currentVertCount,Is.Zero,"Outside camera edges must not invent a frame on the map border.");
                Frame(-80,20,-20,20);Populate();Assert.That(mesh.currentVertCount,Is.EqualTo(12));
                for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);Assert.That(vertex.position.x,Is.InRange(-50f,50f));Assert.That(vertex.position.y,Is.InRange(-50f,50f));}
                graphic.ClearCameraFrame();graphic.Refresh();Populate();Assert.That(mesh.currentVertCount,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test]
        public void CameraFrameRendersOnProductionMinimapAndOverview()
        {
            using var f=new OverviewFixture();
            for(int ry=-1;ry<=1;ry++)for(int rx=-2;rx<=2;rx++)
                for(int y=0;y<7;y++)for(int x=0;x<9;x++)f.Map.TryAddFloorCell(new Vector2Int(rx*11+x-4,ry*10+y-3),"room");
            for(int x=-26;x<=26;x++)f.Map.TryAddFloorCell(new Vector2Int(x,0),"corridor");
            for(int y=-13;y<=13;y++)f.Map.TryAddFloorCell(new Vector2Int(0,y),"corridor");
            f.Floor.Fog.SetVisible(f.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));
            f.WorldCamera.transform.position=new Vector3(-10.5f,3.5f,-10);f.WorldCamera.orthographicSize=4.5f;
            f.Actor.SetPosition(new Vector2(11.5f,-.5f));
            f.View.Bind(f.Floor,f.Presentation,f.Party,null);f.Tick();
            var output=System.IO.Path.GetFullPath("../LocalHandoffs/map-camera-20261023");System.IO.Directory.CreateDirectory(output);
            f.Capture(System.IO.Path.Combine(output,"minimap.png"));
            f.View.SetOverviewOpen(true);f.Tick();f.Capture(System.IO.Path.Combine(output,"overview.png"));
        }

        private sealed class OverviewFixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly DungeonMinimapView View;
            public readonly DungeonFloorInstance Floor=new(0);
            public readonly DungeonFloorMap Map=new();
            public readonly DungeonFloorMapView Presentation;
            public readonly Godmist.Views.Exploration.ExplorationPartyController Party;
            public readonly Godmist.Views.Exploration.ExplorationCharacterView Actor;
            public readonly Camera WorldCamera;
            private readonly Canvas canvas;
            public OverviewFixture()
            {
                Root=new GameObject("Overview verification",typeof(RectTransform),typeof(Canvas));
                canvas=Root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                ((RectTransform)Root.transform).sizeDelta=new Vector2(1600,900);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonMinimap.prefab");
                View=UnityEngine.Object.Instantiate(prefab,Root.transform).GetComponent<DungeonMinimapView>();
                if(Get(View,"texture")==null)Call(View,"Awake");
                Presentation=Child("map").AddComponent<DungeonFloorMapView>();
                Presentation.gameObject.AddComponent<Grid>();
                var tiles=new GameObject("tiles",typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(Presentation.transform,false);
                Set(Presentation,"floorTilemap",tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                WorldCamera=Child("World camera").AddComponent<Camera>();
                WorldCamera.orthographic=true;WorldCamera.orthographicSize=6;WorldCamera.aspect=16f/9;
                WorldCamera.transform.position=new Vector3(0,0,-10);WorldCamera.cullingMask=0;
                Presentation.ConfigurePresentationCamera(WorldCamera);
                Party=Child("party").AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                Actor=Child("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var character=new Godmist.Runtime.Characters.CharacterInstance {CharacterId="overview-leader"};Actor.Bind(character);
                var session=new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Exploration);
                session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);
                Set(Party,"gameSession",session);
                ((System.Collections.IDictionary)Get(Party,"viewsById")).Add(character.CharacterId,Actor);
                Floor.TrySetMap(Map);
                GameObject Child(string name){var go=new GameObject(name);go.transform.SetParent(Root.transform,false);return go;}
            }
            public void Tick()=>Call(View,"RefreshProjection");
            public void Capture(string path)
            {
                var cameraObject=new GameObject("Overview UI render",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
                var target=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGBA32,false);
                var previous=RenderTexture.active;
                try
                {
                    camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=450;camera.aspect=1600f/900;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.03f,.025f);camera.targetTexture=target;camera.cullingMask=1<<30;
                    canvas.worldCamera=camera;target.Create();
                    foreach(var child in Root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
                    Assert.That(pixels.GetPixels32().Count(c=>c.r>30||c.g>30||c.b>30),Is.GreaterThan(500),"A black render cannot verify the map UI.");
                }
                finally
                {
                    canvas.worldCamera=null;camera.targetTexture=null;RenderTexture.active=previous;
                    UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cameraObject);
                }
            }
            public void Dispose()=>UnityEngine.Object.DestroyImmediate(Root);
        }
        [TestCase(false)] [TestCase(true)]
        public void InvisibilityHidesEnemyMarkerButKeepsFriendlyMarker(bool player)
        {
            var root=new GameObject("invisibility minimap");
            try
            {
                var map=root.AddComponent<DungeonFloorMapView>();var floor=Floor();floor.Fog.SetVisible(new[]{At(0)});
                var minimap=root.AddComponent<DungeonMinimapView>();Set(minimap,"floor",floor);Set(minimap,"map",map);
                var markerObject=new GameObject("markers",typeof(RectTransform));markerObject.transform.SetParent(root.transform);
                var markers=markerObject.AddComponent<DungeonMinimapMarkers>();Set(minimap,"markers",markers);
                var actorObject=new GameObject("actor");actorObject.transform.SetParent(root.transform);
                var actor=actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var character=new Godmist.Runtime.Characters.CharacterInstance();actor.Bind(character);
                if(!player)actor.SetEnemyInstanceId("enemy");
                actor.BindCombat(new Godmist.Runtime.Combat.BattleUnit {Character=character,IsPlayerSide=player});
                character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                    EffectId=Godmist.Core.Constants.StatusEffectIds.Invisibility,
                    Lifetime=Godmist.Core.Enums.PassiveEffectLifetime.Timed,RemainingDuration=2});
                var draw=typeof(DungeonMinimapView).GetMethod("DrawActor",BindingFlags.Instance|BindingFlags.NonPublic);
                var args=new object[]{actor,Color.red,!player,Vector2.zero,(Vector2.right,Vector2.up)};
                draw.Invoke(minimap,args);
                Assert.That(((System.Collections.ICollection)Get(markers,"markers")).Count,Is.EqualTo(player?1:0));
                markers.ClearMarkers();character.PassiveEffects.Clear();draw.Invoke(minimap,args);
                Assert.That(((System.Collections.ICollection)Get(markers,"markers")).Count,Is.EqualTo(1));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        static DungeonTraversalPosition At(int x, int level = 0) => new(new DungeonSurfacePosition(new Vector2Int(x, 0), level));
        static DungeonFloorInstance Floor()
        {
            var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
            for (int x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "room");
            floor.TrySetMap(map); return floor;
        }
        [Test]
        public void IndividualTrunksDoNotDrawWallSlitsAcrossWalkableGround()
        {
            var floor = Floor();
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            floor.Map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.TreeTrunk);
            floor.Fog.ToggleDebugFog();
            var go = new GameObject("forest minimap test");
            try
            {
                var view = go.AddComponent<DungeonMinimapView>();
                view.Bind(floor, null, null, null);
                Call(view, "DrawTerrain");
                var pixels = (Color32[])Get(view, "terrain");
                var size = DungeonMinimapView.TextureSize;
                Assert.That(pixels[(size / 2) * size + size / 2 + 3],
                    Is.EqualTo(pixels[(size / 2) * size + size / 2]));
                floor.Map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.Full);
                view.Bind(floor, null, null, null);
                Call(view, "DrawTerrain");
                Assert.That(pixels[(size / 2) * size + size / 2 + 3], Is.EqualTo((Color32)Color.black));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void EnemyRequiresCurrentSightAndViewedHeightEvenWhenMapIsRevealed()
        {
            var floor = Floor();
            floor.Fog.RestoreDiscovered(floor.Map, At(0));
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            floor.Fog.ToggleDebugFog();
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            floor.Fog.SetVisible(new[] { At(0) });
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 1, true), Is.False);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, false), Is.True);
        }
        [Test]
        public void TerrainUsesExistingMemoryAndDoesNotRevealOverlappingSurface()
        {
            var floor = Floor(); floor.Fog.SetVisible(new[] { At(0) });
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Visible));
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0, 1).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Remembered));
            floor.Fog.SetMapDiscovered(false);
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
            floor.Fog.ToggleDebugFog();
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(2, 1).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
        }
        [Test]
        public void UnseenDoorChangesDoNotLeakIntoRenderedMap()
        {
            var floor = Floor(); var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var go = new GameObject("minimap test");
            try
            {
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
                floor.TryAddObject(door);
                floor.Fog.RestoreDiscovered(floor.Map, At(0));
                floor.Fog.Objects[door.InstanceId] = new DungeonObjectKnowledge { Active = true, State = false };
                var view = go.AddComponent<DungeonMinimapView>();
                Set(view, "floor", floor);
                Call(view, "DrawTerrain");
                var before = ((Color32[])Get(view, "terrain")).ToArray();
                door.TrySetOpen(true); Call(view, "DrawTerrain");
                CollectionAssert.AreEqual(before, (Color32[])Get(view, "terrain"));
                floor.Fog.SetVisible(new[] { At(0) }); Call(view, "DrawTerrain");
                Assert.That(((Color32[])Get(view, "terrain"))[126 * 252 + 130], Is.Not.EqualTo(before[126 * 252 + 130]));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(definition); }
        }
        [Test]
        public void StairActorIsShownOnlyAtItsConnectedLevels()
        {
            var floor = Floor();
            var lower = At(2).Surface; var upper = At(0, 1).Surface;
            floor.Map.TryAddStairs(lower, upper, new[] { new Vector2Int(2,0), new Vector2Int(1,0), Vector2Int.zero });
            var stair = floor.Map.Stairs.Single(); var node = new DungeonTraversalPosition(stair, 1);
            floor.Fog.SetVisible(new[] { node });
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 0, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 1, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 2, true), Is.False);
        }
        [Test]
        public void AuthoredPrefabHasReferencesAndCanCollapseWithoutDisablingItsButton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonMinimap.prefab");
            Assert.That(prefab, Is.Not.Null);
            var go = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = go.GetComponent<DungeonMinimapView>();
                var data = new SerializedObject(view);
                foreach (var field in new[] { "image", "content", "toggle", "title", "toggleLabel", "markers" })
                    Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                var image = (RawImage)Get(view, "image");
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(220,220)));
                Call(view, "Toggle");
                Assert.That(new SerializedObject((UnityEngine.Object)Get(view,"title")).FindProperty("m_text").stringValue, Is.EqualTo("Mapa - poziom 0"));
                Assert.That(new SerializedObject((UnityEngine.Object)Get(view,"toggleLabel")).FindProperty("m_text").stringValue, Is.EqualTo("+"));
                Assert.That(((GameObject)Get(view,"content")).activeSelf, Is.False);
                Assert.That(((Button)Get(view,"toggle")).gameObject.activeInHierarchy, Is.True);
                Call(view, "Toggle");
                Assert.That(((GameObject)Get(view,"content")).activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void BodiesRequireSightOrRememberedDiscoveryAndUseSavedHeight()
        {
            var floor=Floor(); var body=new DungeonEnemyBodyInstance("body", "enemy", Vector2Int.zero, 0, null);
            floor.TryAddEnemyBody(body);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            floor.Fog.SetVisible(new[]{At(0)});
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out var cell),Is.True);
            Assert.That(cell,Is.EqualTo(Vector2Int.zero));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            floor.Fog.Bodies.Add(body.InstanceId);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.True);
            floor.TrySetActorPosition("body:"+body.InstanceId,At(0,1));
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.False);
            floor.Fog.SetVisible(new[]{At(0,1)});
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.True);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            floor.Fog.SetMapDiscovered(false);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LootedBodyChangesFromBurgundyToGrayWithoutRemovingMarker(bool overview)
        {
            using var f=new OverviewFixture();
            for(var y=-2;y<=2;y++)for(var x=-2;x<=2;x++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"room");
            f.Floor.Fog.SetVisible(f.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));
            var body=new DungeonEnemyBodyInstance("body.color","enemy.color",Vector2Int.zero,1,null);
            f.Floor.TryAddEnemyBody(body);f.View.Bind(f.Floor,f.Presentation,f.Party,null);
            f.View.SetOverviewOpen(overview);f.Tick();
            var texture=(Texture2D)((RawImage)Get(f.View,"image")).texture;
            var unlooted=(Color32)new Color(.55f,.12f,.2f);
            var empty=(Color32)(Color)Get(f.View,"bodyColor");
            Assert.That(texture.GetPixels32(),Does.Contain(unlooted));
            Assert.That(body.TryClaimGold(out _),Is.True);
            // Also cover the overview's periodic content cache: an unchanged
            // visibility tick must notice that the body's payload became empty.
            if(overview)f.Floor.Fog.SetVisible(f.Floor.Fog.Visible.ToArray());
            if(!overview) typeof(DungeonMinimapView).GetMethod("OnLootClaimed",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(f.View,new object[]{body.InstanceId,"Zebrano łup"});
            f.Tick();
            var pixels=texture.GetPixels32();
            Assert.That(pixels,Does.Contain(empty));Assert.That(pixels,Has.No.Member(unlooted));
            Assert.That(f.Floor.EnemyBodies,Does.Contain(body));
        }

        [Test]
        public void BodyIsRenderedAsCrossAndRemovedWithRuntimeBody()
        {
            var floor=Floor();var body=new DungeonEnemyBodyInstance("body", "enemy", Vector2Int.zero,0,null);
            floor.TryAddEnemyBody(body);floor.Fog.SetVisible(new[]{At(0)});
            var go=new GameObject("body minimap");
            try
            {
                var view=go.AddComponent<DungeonMinimapView>();Set(view,"floor",floor);Call(view,"DrawTerrain");
                var pixels=(Color32[])Get(view,"terrain");var color=(Color32)(Color)Get(view,"bodyColor");
                Assert.That(pixels[126*252+126],Is.EqualTo(color));
                Assert.That(pixels[124*252+124],Is.EqualTo(color));
                Assert.That(pixels[126*252+125],Is.Not.EqualTo(color));
                floor.TryRemoveEnemyBody(body.InstanceId);Call(view,"DrawTerrain");
                Assert.That(pixels[126*252+126],Is.Not.EqualTo(color));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void ViewportTracksFractionalMovementWithoutCellBoundaryJump()
        {
            var before = DungeonMinimapView.Viewport(new Vector2(.49f,0), Vector2Int.zero);
            var after = DungeonMinimapView.Viewport(new Vector2(.51f,0), Vector2Int.right);
            float absoluteBefore = before.x * DungeonMinimapView.TextureSize;
            float absoluteAfter = after.x * DungeonMinimapView.TextureSize + 8;
            Assert.That(absoluteAfter - absoluteBefore, Is.EqualTo(.02f * 8).Within(.0001f));
            var middle = DungeonMinimapView.Viewport(new Vector2(.25f,0), Vector2Int.zero);
            var origin = DungeonMinimapView.Viewport(Vector2.zero, Vector2Int.zero);
            Assert.That((middle.x - origin.x) * DungeonMinimapView.TextureSize, Is.EqualTo(2).Within(.0001f));
            Assert.That(before.xMin, Is.GreaterThan(0)); Assert.That(before.xMax, Is.LessThan(1));
        }
        [TestCase("object.obstacle.sarcophagus", 14)]
        [TestCase("object.obstacle.pillar", 6)]
        public void ObstaclePixelsCoverWholeFootprintAndRetainRememberedPresence(string id, int height)
        {
            var floor = Floor();
            floor.Map.TryAddFloorCell(Vector2Int.up, "room");
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            typeof(DungeonObjectDefinition).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, id);
            var obstacle = new ObstacleDungeonObjectInstance("obstacle", definition, Vector2Int.zero);
            floor.TryAddObject(obstacle);
            var go = new GameObject("obstacle footprint minimap");
            try
            {
                var view = go.AddComponent<DungeonMinimapView>(); Set(view, "floor", floor);
                var pixels = (Color32[])Get(view, "terrain");
                var color = (Color32)(Color)Get(view, "obstacleColor");
                void Draw() => Call(view, "DrawTerrain");
                void AssertFootprint()
                {
                    for (var y = 123; y < 123 + height; y++)
                        for (var x = 123; x < 129; x++)
                            Assert.That(pixels[y * 252 + x], Is.EqualTo(color));
                    Assert.That(pixels.Count(pixel => pixel.Equals(color)), Is.EqualTo(6 * height));
                }
                Draw(); Assert.That(pixels.Contains(color), Is.False, "Unknown objects remain hidden.");
                floor.Fog.SetVisible(new[] { At(0) }); Draw(); AssertFootprint();
                floor.Fog.Objects[obstacle.InstanceId] = new DungeonObjectKnowledge { Active = true };
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                obstacle.TryDeactivate(); Draw(); AssertFootprint();
                floor.Fog.SetVisible(new[] { At(0) }); Draw();
                Assert.That(pixels.Contains(color), Is.False, "Seeing a removed obstacle clears its entire footprint.");
                Set(view, "level", 1); Draw();
                Assert.That(pixels.Contains(color), Is.False, "Ground props must not appear on an upper floor.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SemanticObjectsUseRememberedPresenceUntilSeenAgain()
        {
            var floor = Floor(); var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var go = new GameObject("minimap content test");
            try
            {
                var obj = new ObstacleDungeonObjectInstance("pillar", definition, Vector2Int.zero); floor.TryAddObject(obj);
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
                floor.Fog.SetVisible(new[] { At(0) });
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.True);
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,1), Is.False);
                floor.Fog.Objects[obj.InstanceId] = new DungeonObjectKnowledge { Active=true };
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                var view=go.AddComponent<DungeonMinimapView>(); Set(view,"floor",floor);
                Call(view,"DrawTerrain"); var remembered=((Color32[])Get(view,"terrain")).ToArray();
                obj.TryDeactivate();
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.True);
                Call(view,"DrawTerrain"); CollectionAssert.AreEqual(remembered,(Color32[])Get(view,"terrain"));
                floor.Fog.SetVisible(new[] { At(0) });
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
                Call(view,"DrawTerrain");
                Assert.That(((Color32[])Get(view,"terrain"))[126*252+126], Is.Not.EqualTo(remembered[126*252+126]));
                floor.Fog.Objects[obj.InstanceId].Active=false;
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(definition); }
        }
        [Test]
        public void MarkerGeometryRetainsSizeAndClipsAtViewportEdge()
        {
            var go=new GameObject("markers",typeof(RectTransform));
            try
            {
                ((RectTransform)go.transform).sizeDelta=new Vector2(220,220);
                var markers=go.AddComponent<DungeonMinimapMarkers>();
                markers.AddMarker(Vector2.zero,Color.red); markers.AddMarker(new Vector2(110,0),Color.blue); markers.AddMarker(new Vector2(120,0),Color.white);
                using var vh=new VertexHelper();
                typeof(DungeonMinimapMarkers).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(markers,new object[]{vh});
                Assert.That(vh.currentVertCount,Is.EqualTo(8));
                var vertex=new UIVertex(); vh.PopulateUIVertex(ref vertex,0); Assert.That(vertex.position.x,Is.EqualTo(-2.5f));
                for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);Assert.That(Mathf.Abs(vertex.position.x),Is.LessThanOrEqualTo(110));}
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void LargeFloorRenderingHasFixedViewportAndDoesNotMutateKnowledge()
        {
            var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
            for (var x = -100; x < 100; x++) for (var y = -100; y < 100; y++) map.TryAddFloorCell(new Vector2Int(x,y), "room");
            floor.TrySetMap(map); floor.Fog.SetVisible(new[] { At(0) });
            var go = new GameObject("minimap performance");
            try
            {
                var view = go.AddComponent<DungeonMinimapView>(); Set(view,"floor",floor);
                Call(view,"DrawTerrain");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < 50; i++) Call(view,"DrawTerrain");
                timer.Stop();
                TestContext.WriteLine($"50 terrain refreshes on 40,000 cells: {timer.Elapsed.TotalMilliseconds:F2} ms; includes reflection, excludes GPU upload.");
                Assert.That(((Color32[])Get(view,"terrain")).Length, Is.EqualTo(DungeonMinimapView.TextureSize * DungeonMinimapView.TextureSize));
                Assert.That(floor.Fog.Discovered.Count, Is.EqualTo(1));
                Assert.That(floor.Fog.Visible.Count, Is.EqualTo(1));
                Assert.That(((Color32[])Get(view,"terrain"))[0], Is.EqualTo((Color32)Color.black));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void SceneWiresOneMinimapBelowWorkspaceWindows()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
                var maps = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonMinimapView>(true)).ToArray();
                Assert.That(maps.Length, Is.EqualTo(1));
                var coordinator = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonCombatCoordinator>(true)).Single();
                Assert.That(new SerializedObject(coordinator).FindProperty("minimap").objectReferenceValue, Is.EqualTo(maps[0]));
                Assert.That(maps[0].transform.GetSiblingIndex(), Is.Zero);
                Assert.That(maps[0].GetComponent<Canvas>(), Is.Null);
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
        static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
