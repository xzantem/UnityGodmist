using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
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
    public sealed class ForestGroundingReadabilityTests
    {
        static string Output=>Path.GetFullPath("../LocalHandoffs/forest-canopy-entry-20261020");
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        readonly List<Object> owned=new();
        T Keep<T>(T item) where T:Object{owned.Add(item);return item;}
        static T Read<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
        static void Call(object obj,string method,params object[] values)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,values);
        static string Stage=>File.ReadAllText(Path.Combine(Output,"stage.txt")).Trim();
        [TearDown] public void Cleanup()
        {
            foreach(var go in owned.OfType<GameObject>())if(go!=null)foreach(var camera in go.GetComponentsInChildren<Camera>(true))camera.targetTexture=null;
            foreach(var item in owned.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);
            owned.Clear();
        }
        DungeonFloorMapView Terrain()
        {
            var root=Keep(new GameObject("Grounded forest",typeof(Grid)));
            var child=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(root.transform,false);
            var view=root.AddComponent<DungeonFloorMapView>();typeof(DungeonFloorMapView).GetField("floorTilemap",Flags).SetValue(view,child.GetComponent<Tilemap>());
            typeof(DungeonFloorMapView).GetField("floorTile",Flags).SetValue(view,Keep(ScriptableObject.CreateInstance<Tile>()));
            typeof(DungeonFloorMapView).GetField("wallSegmentPrefab",Flags).SetValue(view,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
            view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            var map=new DungeonFloorMap();for(var y=0;y<6;y++)for(var x=0;x<6;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
            foreach(var c in map.FloorCells)foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})if(!map.IsWalkable(c+d))map.TryAddWall(c,c+d);
            view.Rebuild(map);return view;
        }
        [TestCase(-1, 0, 0, 2)]
        [TestCase(1, 0, 5, 2)]
        [TestCase(0, -1, 2, 0)]
        [TestCase(0, 1, 2, 5)]
        public void DeepWoodlandFadesBehindTheBorderWithoutDiscoveringOtherFloor(int dx, int dy, int x, int y)
        {
            var view = Terrain(); var map = Read<DungeonFloorMap>(view, "boundMap");
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
            var source = new Vector2Int(x, y); var outward = new Vector2Int(dx, dy);
            var observer = new DungeonTraversalPosition(new DungeonSurfacePosition(source));
            floor.Fog.SetVisible(new[] { observer });
            view.ApplyFog(floor);
            var filter = Read<Texture2D>(view, "forestKnowledgeTexture");
            var origin = Read<Vector2Int>(view, "forestKnowledgeOrigin");
            float Alpha(Vector2Int cell) => filter.GetPixel(cell.x-origin.x, cell.y-origin.y).a;
            var near = Alpha(source + outward);
            var deep = Alpha(source + outward * 4);
            var end = Alpha(source + outward * 7);
            Assert.That(near, Is.LessThan(.01f));
            Assert.That(deep, Is.InRange(.2f, .8f), "Further woodland layers must survive beyond the old three-cell cutoff.");
            Assert.That(end, Is.EqualTo(1), "The forest still disappears gradually into darkness.");
            foreach (var cell in map.FloorCells.Where(c => c != source))
                Assert.That(Alpha(cell), Is.EqualTo(1), "Decorative depth cannot uncover a real unknown floor cell.");
            Assert.That(floor.Fog.Discovered, Is.EquivalentTo(new[] { observer }));
            Assert.That(floor.Fog.Visible, Is.EquivalentTo(new[] { observer }));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); view.ApplyFog(floor);
            Assert.That(Alpha(source + outward * 4), Is.EqualTo(1 - (1-deep)*.35f).Within(2f/255),
                "Remembered woods keep the same depth contour with their ordinary fog attenuation.");
            Call(view, "ReleaseSoftFog");
        }

        [Test]
        public void FillerTreesContinueBehindTheRimAndKeepTheirAuthoredColorInFog()
        {
            var view=Terrain();var map=Read<DungeonFloorMap>(view,"boundMap");
            var trees=Read<Dictionary<SpriteRenderer,RectInt>>(view,"forestTreeFootprints");
            var compact=trees.Where(p=>p.Value.size==Vector2Int.one).ToArray();
            foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                Assert.That(compact.Any(p=>map.FloorCells.Min(c=>Math.Abs(c.x-p.Value.x)+Math.Abs(c.y-p.Value.y))>=3
                    && Vector2.Dot(p.Value.center-new Vector2(3,3),direction)>3),Is.True,
                    "Deep free slots need filler trees on every side, including the lower edge.");
            float Peak(SpriteRenderer r)=>Mathf.Max(r.color.r,Mathf.Max(r.color.g,r.color.b));
            Assert.That(trees.Keys.Min(Peak),Is.GreaterThan(trees.Keys.Max(Peak)*.5f),
                "Fog supplies the extinction; tree RGB must not vanish before the litter backdrop.");
        }

        [Test]
        public void BoundaryArtUsesTheSouthEdgeOfItsRealReservation()
        {
            var view=Terrain();var trees=Read<Dictionary<SpriteRenderer,RectInt>>(view,"forestTreeFootprints");
            Assert.That(trees,Is.Not.Empty);
            foreach(var pair in trees)
            {
                var south=view.CellToWorldCenter(pair.Value.min).y-.5f;
                var offset=pair.Key.transform.position.y-south;
                Assert.That(offset,Is.InRange(-.0001f,.1251f),pair.Key.sprite.name+" "+pair.Value);
                Assert.That(offset*16,Is.EqualTo(Mathf.Round(offset*16)).Within(.001f),"Jitter must retain a bounded number of sorting lanes.");
                Assert.That(pair.Key.transform.localScale,Is.EqualTo(Vector3.one));
                var foot=pair.Key.transform.position;
                Assert.That(view.GetActorDepthSortingOrder(foot.y+.03125f),Is.LessThan(view.ForestCanopyForegroundOrder(pair.Key)),"Every row must correctly occlude an actor north of its feet.");
                Assert.That(view.GetActorDepthSortingOrder(foot.y-.03125f),Is.GreaterThan(view.ForestCanopyForegroundOrder(pair.Key)),"An actor south of the root base must stay in front.");
            }
        }
        [Test]
        public void BoundarySoilCoversTheRimEvenWhenCompactTreeSlotsAreDisabled()
        {
            var view=Terrain();var map=Read<DungeonFloorMap>(view,"boundMap");
            var presentation=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset")));
            view.ConfigureVegetationPresentation(presentation);view.Rebuild(map);
            Bounds[] Patches()=>Read<HashSet<SpriteRenderer>>(view,"vegetationGroundRenderers").Select(r=>r.bounds).OrderBy(b=>b.center.x).ThenBy(b=>b.center.y).ToArray();
            var before=Patches();Assert.That(before,Is.Not.Empty);
            foreach(var cell in map.FloorCells)foreach(var d in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
                if(!map.IsWalkable(cell+d))
                {
                    var sample=view.CellToWorldCenter(cell)+(Vector2)d*.6f;
                    Assert.That(before.Any(b=>new Vector2((sample.x-b.center.x)/b.extents.x,(sample.y-b.center.y)/b.extents.y).magnitude<.7f),Is.True,"The opaque core of the soil apron must cover the rim at "+cell+" "+d);
                }
            typeof(Godmist.Data.Dungeons.DungeonVegetationPresentation).GetField("<SmallTreeVariants>k__BackingField",Flags).SetValue(presentation,Array.Empty<Sprite>());
            view.Rebuild(map);Assert.That(Patches(),Is.EqualTo(before),"Soil coverage must not disappear with tree candidates.");
        }
        [Test]
        public void ForestFogFeathersAtVoidWithoutRevealingTheUnknownFloor()
        {
            // Row-major neighbours: index 1 is unknown floor south of this
            // cell, index 3 is decorative woodland to its west.
            var neighbours=Enumerable.Repeat(DungeonKnowledge.Visible,8).ToArray();
            neighbours[1]=DungeonKnowledge.Unexplored;
            var original=new float[81];var softened=new float[81];
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Visible,neighbours,original,true);
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Visible,neighbours,softened,true,1<<3);
            Assert.That(original[4*9],Is.LessThan(.9f));
            Assert.That(softened[4*9],Is.EqualTo(1),"No black quad edge against decorative woodland.");
            Assert.That(softened[4],Is.EqualTo(0),"The frontier towards genuine unknown floor still blocks it.");
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Unexplored,neighbours,softened,true,255);
            Assert.That(softened.All(x=>x==0),Is.True,"Void feathering never reveals an unknown cell.");
        }
        [TestCase(true)] [TestCase(false)]
        public void FreshWoodlandTileCannotStartAsAnOpaqueBlackSquare(bool forest)
        {
            var view=Terrain();if(!forest)view.ConfigureVegetationPresentation(null);
            var type=typeof(DungeonFloorMapView).GetNestedType("FogCellVisual",BindingFlags.NonPublic);
            var visual=Activator.CreateInstance(type,true);
            type.GetField("TargetColor",Flags).SetValue(visual,Color.white);
            type.GetField("TargetBaseline",Flags).SetValue(visual,1f);
            type.GetField("Transition",Flags).SetValue(visual,new DungeonFogTransition(0,new float[81]));
            Call(view,"ApplyFogCellColor",Vector2Int.zero,visual);
            var color=Read<Tilemap>(view,"floorTilemap").GetColor(Vector3Int.zero);
            Assert.That(color.a,Is.EqualTo(forest?0:1),"Only woodland must reveal over litter; Catacombs keep the existing dark reveal.");
        }
        [TestCase("Godmist/Dungeon/Torch Body")]
        [TestCase("Godmist/Dungeon/Torch Wall")]
        public void LowSideBranchesRevealWithoutErasingTheReservedRoots(string shader)
        {
            var root=Keep(new GameObject("Local root protection"));root.layer=30;
            Sprite Solid(Color color,int width,int height)
            {
                var tex=Keep(new Texture2D(width,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point});
                tex.SetPixels(Enumerable.Repeat(color,width*height).ToArray());tex.Apply();
                return Keep(Sprite.Create(tex,new Rect(0,0,width,height),new Vector2(.5f,0),32));
            }
            var actor=new GameObject("actor",typeof(SpriteRenderer));actor.transform.SetParent(root.transform);actor.layer=30;actor.transform.position=new Vector3(1.25f,.5f);
            var body=actor.GetComponent<SpriteRenderer>();body.sprite=Solid(Color.red,64,64);body.sortingOrder=1;
            var bodyMaterial=Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body")));bodyMaterial.SetFloat("_Ambient",1);body.sharedMaterial=bodyMaterial;
            var tree=new GameObject("tree",typeof(SpriteRenderer));tree.transform.SetParent(root.transform);tree.layer=30;
            var canopy=tree.GetComponent<SpriteRenderer>();canopy.sprite=Solid(Color.green,128,128);canopy.sortingOrder=2;
            var material=Keep(new Material(Shader.Find(shader)));Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);canopy.sharedMaterial=material;
            material.SetFloat("_Ambient",1);material.SetFloat("_ForestCanopy",1);material.SetFloat("_ForestCanopyRootTopY",2);
            material.SetVector("_ForestCanopyRootBounds",new Vector4(-1,1,1,0));material.SetFloat("_ForestCanopyCellSize",1);
            material.SetFloat("_ForestCanopyFade",.8f);
            var cam=root.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=2;cam.aspect=1;cam.transform.position=new Vector3(0,2,-10);cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;
            var rt=Keep(new RenderTexture(256,256,24));var image=Keep(new Texture2D(256,256,TextureFormat.RGBA32,false));cam.targetTexture=rt;var old=RenderTexture.active;
            try
            {
                Color Pixel(float x,float y)=>image.GetPixel(Mathf.RoundToInt((x+2)*64),Mathf.RoundToInt(y*64));
                void Render(){cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();}
                canopy.enabled=false;Render();var actorRed=Pixel(1.4f,1.4f).r;
                canopy.enabled=true;material.SetFloat("_ForestCanopyFade",0);Render();var rootGreen=Pixel(.8f,1.4f).g;
                material.SetFloat("_ForestCanopyFade",.8f);Render();
                Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,"side-branches-"+shader.Split('/').Last()+".png"),image.EncodeToPNG());
                Assert.That(actorRed,Is.GreaterThan(.05f),"The isolated actor must render before evaluating canopy transmission.");
                Assert.That(Pixel(1.4f,1.4f).r,Is.GreaterThan(actorRed*.7f),"The adjacent actor must show through low leaves outside the physical roots.");
                Assert.That(Pixel(.8f,1.4f).g,Is.EqualTo(rootGreen).Within(.01f),"Occupied root cells remain opaque.");
            }
            finally{RenderTexture.active=old;}
        }
        [Test,Explicit("Deterministic production forest comparison; preview scene only, never saves the user's session.")]
        public void CaptureProductionForest()
        {
            var stage=Stage;var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride=stage,SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;var actors=party.ActiveMemberViews.ToArray();
                var enemies=Read<DungeonEncounterSpawner>(bootstrap,"encounterSpawner").EnemyViews;
                var floor=session.ActiveDungeon.Floors[0];var camera=Read<Camera>(rig,"worldCamera");
                var roots=bootstrap.gameObject.scene.GetRootGameObjects();foreach(var sr in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))sr.gameObject.layer=30;
                var treeMap=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
                File.WriteAllText(Path.Combine(Output,stage+"-tree-counts.txt"),"large="+treeMap.Count(p=>p.Value.width==2)+" compact="+treeMap.Count(p=>p.Value.width==1)+" soilPatches="+Read<HashSet<SpriteRenderer>>(map,"vegetationGroundRenderers").Count);
                var target=Keep(new RenderTexture(1280,720,24));var pixels=Keep(new Texture2D(1280,720,TextureFormat.RGBA32,false));
                var oldTarget=camera.targetTexture;var oldMask=camera.cullingMask;var active=RenderTexture.active;
                try
                {
                    camera.targetTexture=target;camera.cullingMask=1<<30;camera.aspect=16f/9;camera.orthographicSize=5.5f;
                    void Capture(string label)
                    {
                        camera.transform.position=new Vector3(actors[0].Position.x,actors[0].Position.y+1.5f,-10);
                        new DungeonFogSystem().Update(floor,actors.Select(a=>new DungeonTraversalPosition(new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                        map.ApplyFog(floor);
                        foreach(var a in actors)Call(a,"LateUpdate");
                        foreach(var a in enemies)if(a!=null)Call(a,"LateUpdate");
                        Call(rig,"RefreshLighting",1f);
                        if(label=="crowded-edge")
                        {
                            Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                            SaveFrame(label+"-fade-000ms");
                            Call(rig,"AdvanceForestCanopyFade",.1f);SaveFrame(label+"-fade-100ms");
                            Call(rig,"AdvanceForestCanopyFade",.1f);SaveFrame(label+"-fade-200ms");
                        }
                        Call(rig,"RefreshForestPartyReveal");Call(rig,"AdvanceForestCanopyFade",.5f);
                        var fogRenderer=Read<MeshRenderer>(map,"fogBlendRenderer");
                        if(fogRenderer!=null)
                        {
                            Assert.That(fogRenderer.sharedMaterial,Is.SameAs(Read<Tilemap>(map,"floorTilemap").GetComponent<TilemapRenderer>().sharedMaterial),"First-frame forest fog must already use continuous lit litter, not opaque black quads.");
                            var fogProperties=new MaterialPropertyBlock();fogRenderer.GetPropertyBlock(fogProperties);
                            Assert.That(fogProperties.GetFloat("_ForestFogLitter"),Is.EqualTo(1),"The composite must suppress the route mask.");
                        }
                        SaveFrame(label);
                        floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());map.ApplyFog(floor);
                        var remembered=Read<List<Color>>(map,"fogColors").Where(c=>c.a>.001f).ToArray();
                        Assert.That(remembered,Is.Not.Empty);
                        Assert.That(remembered.All(c=>Mathf.Abs(c.r-1)<.001f&&Mathf.Abs(c.g-1)<.001f&&Mathf.Abs(c.b-1)<.001f),Is.True,"Forest litter must remain continuous across knowledge states; remembered routes retain their separate tint.");
                    }
                    void SaveFrame(string label)
                    {
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                        File.WriteAllBytes(Path.Combine(Output,stage+"-"+label+".png"),pixels.EncodeToPNG());
                    }
                    Capture("entrance");
                    if(stage=="before-edge" || stage.EndsWith("-debug"))
                    {
                        Call(map,"ReleasePresentationCamera");
                        var floorRenderer=Read<Tilemap>(map,"floorTilemap").GetComponent<TilemapRenderer>();
                        var fog=Read<MeshRenderer>(map,"fogBlendRenderer");
                        var floorEnabled=floorRenderer.enabled;var fogEnabled=fog!=null&&fog.enabled;
                        void Raw(string label)
                        {
                            camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                            File.WriteAllBytes(Path.Combine(Output,stage+"-"+label+".png"),pixels.EncodeToPNG());
                        }
                        if(fog!=null)fog.enabled=false;Raw("no-fog-mesh");
                        var lines=new List<string>();
                        var tiles=Read<Tilemap>(map,"floorTilemap");
                        for(var py=600;py<710;py+=16)for(var px=650;px<900;px+=16)
                        {
                            var world=camera.ViewportToWorldPoint(new Vector3(px/1280f,1-py/720f,10));var cell=map.WorldToCell(world);
                            lines.Add($"pixel={px},{py} cell={cell} walkable={floor.Map.IsWalkable(cell)} fog={floor.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)))} tile={tiles.HasTile((Vector3Int)cell)} color={tiles.GetColor((Vector3Int)cell)}");
                        }
                        File.WriteAllLines(Path.Combine(Output,stage+"-tile-diagnostic.txt"),lines);
                        var floorMaterial=floorRenderer.sharedMaterial;var receive=floorMaterial.GetFloat("_ReceiveShadows");
                        floorMaterial.SetFloat("_ReceiveShadows",0);Raw("unshadowed-floor");floorMaterial.SetFloat("_ReceiveShadows",receive);
                        floorRenderer.enabled=false;Raw("no-floor-tiles");floorRenderer.enabled=floorEnabled;
                        if(fog!=null)fog.enabled=fogEnabled;
                        map.ConfigurePresentationCamera(null);map.ConfigurePresentationCamera(camera);
                    }
                    var candidates=new HashSet<Vector2Int>();
                    foreach(var pair in treeMap)
                        foreach(var cell in pair.Value.allPositionsWithin)
                            foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})if(floor.Map.IsWalkable(cell+d))candidates.Add(cell+d);
                    bool Free(Vector2Int c)=>floor.Map.IsWalkable(c)&&!floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o=>o.IsActive&&o.BlocksMovement&&o.OccupiesCell(c));
                    var shared=Read<Dictionary<Vector2Int,List<SpriteRenderer>>>(map,"forestCanopyCells");
                    var anchor=candidates.Where(Free).OrderByDescending(c=>shared.TryGetValue(c,out var trees)?trees.Count:0).ThenBy(c=>c.y).ThenBy(c=>c.x).First();
                    var baselinePosition=Path.Combine(Output,"before-edge-probe-position.txt");
                    if(stage!="before"&&File.Exists(baselinePosition))
                    {
                        var xy=File.ReadAllText(baselinePosition).Split(',');anchor=new Vector2Int(int.Parse(xy[0]),int.Parse(xy[1]));
                    }
                    var cells=floor.Map.FloorCells.Where(Free).OrderBy(c=>(c-anchor).sqrMagnitude).ThenBy(c=>c.y).ThenBy(c=>c.x).Take(6).ToArray();
                    for(var i=0;i<actors.Length;i++)actors[i].SetPosition(map.CellToWorldCenter(cells[i]));
                    for(var i=0;i<2&&i<enemies.Count;i++)
                    {
                        var a=enemies[i];a.SetPosition(map.CellToWorldCenter(cells[4+i]));
                        a.BindCombat(new BattleUnit{Character=a.RuntimeCharacter,IsPlayerSide=false,Position=new GridPosition(cells[4+i].x,cells[4+i].y)});
                    }
                    File.WriteAllText(Path.Combine(Output,stage+"-probe-position.txt"),anchor.x+","+anchor.y);
                    Capture("crowded-edge");
                }
                finally{camera.targetTexture=oldTarget;camera.cullingMask=oldMask;RenderTexture.active=active;}
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Long);
        }
    }
}
