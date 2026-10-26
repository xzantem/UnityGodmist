using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestWholeCanopyFadeTests
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        readonly List<Object> owned=new();
        T Keep<T>(T value) where T:Object {owned.Add(value);return value;}
        static void Set(object o,string f,object v)=>o.GetType().GetField(f,F).SetValue(o,v);
        static T Read<T>(object o,string f)=>(T)o.GetType().GetField(f,F).GetValue(o);
        static void Call(object o,string m,params object[] v)=>o.GetType().GetMethod(m,F).Invoke(o,v);
        DungeonFloorMapView map;DungeonSilhouetteLighting rig;ExplorationPartyController party;
        [SetUp] public void Setup()
        {
            var root=Keep(new GameObject("Whole canopy",typeof(Grid)));
            var child=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(root.transform,false);
            map=root.AddComponent<DungeonFloorMapView>();Set(map,"floorTilemap",child.GetComponent<Tilemap>());
            Set(map,"floorTile",Keep(ScriptableObject.CreateInstance<Tile>()));
            Set(map,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
            var floor=new DungeonFloorMap();for(var y=0;y<8;y++)for(var x=0;x<8;x++)floor.TryAddFloorCell(new Vector2Int(x,y),"test");
            map.Rebuild(floor);
            map.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            rig=root.AddComponent<DungeonSilhouetteLighting>();party=root.AddComponent<ExplorationPartyController>();Set(rig,"terrain",map);Set(rig,"party",party);
        }
        [TearDown] public void Cleanup(){if(map!=null)Call(map,"ReleaseSoftFog");foreach(var x in owned.AsEnumerable().Reverse())if(x!=null)Object.DestroyImmediate(x);owned.Clear();}
        Sprite Solid(int w=64,int h=128)
        {
            var t=Keep(new Texture2D(w,h));t.SetPixels(Enumerable.Repeat(Color.white,w*h).ToArray());t.Apply();
            return Keep(Sprite.Create(t,new Rect(0,0,w,h),new Vector2(.5f,0),32,0,SpriteMeshType.FullRect));
        }
        SpriteRenderer Tree(Vector2 p,Sprite sprite=null,bool large=false)
        {
            var go=Keep(new GameObject("tree",typeof(SpriteRenderer)));go.transform.position=p;
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprite!=null?sprite:Solid();
            if(large)Read<HashSet<SpriteRenderer>>(map,"forestLargeRoots").Add(sr);
            map.RegisterForestCanopy(sr);return sr;
        }
        ExplorationCharacterView Actor(Vector2 p)
        {
            var go=Keep(new GameObject("party",typeof(SpriteRenderer)));go.transform.position=p;
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Solid(24,48);
            var a=go.AddComponent<ExplorationCharacterView>();Set(a,"bodyRenderer",sr);
            Read<Dictionary<string,ExplorationCharacterView>>(party,"viewsById").Add(Guid.NewGuid().ToString(),a);return a;
        }
        static float Alpha(SpriteRenderer tree){var p=new MaterialPropertyBlock();tree.GetPropertyBlock(p);return 1-p.GetFloat("_ForestCanopyFade");}
        void Select()=>Call(rig,"RefreshForestPartyReveal");
        void Advance(float dt)=>Call(rig,"AdvanceForestCanopyFade",dt);

        [TestCase(false)] [TestCase(true)]
        public void NativeTreeContactStaysWithinImportedOutlineTolerance(bool flip)
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-canopy-contact-20261022"));
            Directory.CreateDirectory(output);
            var presentation=AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var report=new List<string>();int totalProbes=0,maxContourError=0;
            foreach(var sprite in new[]{presentation.BoundaryTree}.Concat(presentation.TreeVariants)
                .Concat(presentation.SmallTreeVariants).Where(s=>s!=null).Distinct())
            {
                var texture=Keep(new Texture2D(2,2));texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var tree=Tree(new Vector2(3,1),sprite);tree.flipX=flip;
                var roots=map.ForestCanopyGroundBounds(tree);int mismatches=0,probes=0,maxClear=0;
                var rect=sprite.rect;
                for(int y=4;y<(int)rect.height-4;y+=4)for(int x=4;x<(int)rect.width-4;x+=4)
                {
                    bool clear=true;
                    for(int dy=-3;dy<=3&&clear;dy++)for(int dx=-3;dx<=3;dx++)
                        if(texture.GetPixel((int)rect.x+x+dx,(int)rect.y+y+dy).a>.05f){clear=false;break;}
                    if(!clear)continue;
                    var local=new Vector2((x+.5f-sprite.pivot.x)/sprite.pixelsPerUnit,
                        (y+.5f-sprite.pivot.y)/sprite.pixelsPerUnit);
                    if(flip)local.x=-local.x;
                    var world=(Vector2)tree.transform.TransformPoint(local);
                    if(roots.Contains(world))continue;
                    probes++;
                    if(map.ForestCanopyContainsPoint(tree,world))
                    {
                        mismatches++;
                        for(int radius=4;radius<=32;radius++)
                        {
                            bool found=false;
                            for(int offset=-radius;offset<=radius;offset++)
                            {
                                float A(int px,int py)=>px<0||py<0||px>=texture.width||py>=texture.height?0:texture.GetPixel(px,py).a;
                                if(A(x+offset,y-radius)>.05f||A(x+offset,y+radius)>.05f
                                    ||A(x-radius,y+offset)>.05f||A(x+radius,y+offset)>.05f){found=true;break;}
                            }
                            if(found){maxClear=Math.Max(maxClear,radius);break;}
                        }
                    }
                }
                report.Add($"{sprite.name}: vertices={sprite.vertices.Length}; physics contours={sprite.GetPhysicsShapeCount()}; clear probes={probes}; false contact={mismatches}; max clear radius={maxClear}px; flip={flip}");
                maxContourError=Math.Max(maxContourError,maxClear);totalProbes+=probes;
            }
            File.WriteAllLines(Path.Combine(output,$"native-margins-{flip}.txt"),report);
            Assert.That(totalProbes,Is.GreaterThan(100));
            // Imported outlines simplify individual leaf pixels. Bound that
            // approximation to a quarter of a cell, instead of demanding an
            // alpha-perfect mask or allowing the whole transparent rectangle.
            Assert.That(maxContourError,Is.LessThanOrEqualTo(8),
                "A native 32-PPU tree may not trigger contact far into its transparent padding.");
        }

        DungeonFloorInstance Passage(int width)
        {
            var layout=new DungeonFloorMap();
            for(int y=0;y<8;y++)for(int x=2;x<2+width;x++)layout.TryAddFloorCell(new Vector2Int(x,y),"passage");
            map.Rebuild(layout);
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(layout);
            floor.Fog.SetVisible(layout.FloorCells.Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
            map.ApplyFog(floor);return floor;
        }

        SpriteRenderer SouthernBoundaryTree(Vector2 position)
        {
            var tree=Tree(position);
            Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints")[tree]=new RectInt(
                Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),1,1);
            map.RegisterForestCanopy(tree);return tree;
        }

        [Test]
        public void SouthernBoundaryRevealsWideClearingWithoutActorsOrNarrowPassage()
        {
            Passage(8);var tree=SouthernBoundaryTree(new Vector2(4.5f,-2));
            var candidates=new List<SpriteRenderer>();map.CollectVisibleForestPassageCanopies(candidates);
            Assert.That(candidates,Is.Empty,"This is a wide room, so the old route-only rule does not apply.");
            Select();Advance(.05f);Assert.That(Alpha(tree),Is.InRange(160f/255,.999f));
            Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
            Assert.That(rig.LastForestForegroundRevealCount,Is.EqualTo(1));
            Assert.That(rig.LastForestPartyRevealCount,Is.Zero);
        }

        [Test]
        public void AlcoveUsesItsOwnLowerBoundaryInsteadOfTheWholeMapBottom()
        {
            var floor=Passage(8);
            for(int y=12;y<18;y++)for(int x=2;x<10;x++)floor.Map.TryAddFloorCell(new Vector2Int(x,y),"alcove");
            map.Rebuild(floor.Map);
            floor.Fog.SetVisible(floor.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));map.ApplyFog(floor);
            var tree=SouthernBoundaryTree(new Vector2(4.5f,10));
            Select();Advance(.4f);
            Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
        }

        [TestCase(false)] [TestCase(true)]
        public void RememberedAndUnknownLowerRoomEdgesDoNotRevealCrowns(bool unknown)
        {
            var floor=Passage(8);var tree=SouthernBoundaryTree(new Vector2(4.5f,-2));
            if(unknown)floor.Fog.SetMapDiscovered(false);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());map.ApplyFog(floor);
            Select();Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }

        [Test]
        public void ForegroundRevealKeepsDistantAndUpperBoundaryTreesSolid()
        {
            Passage(8);var south=SouthernBoundaryTree(new Vector2(4.5f,-2));
            var north=SouthernBoundaryTree(new Vector2(4.5f,8));
            var distant=SouthernBoundaryTree(new Vector2(14.5f,-2));
            Select();Advance(.4f);
            Assert.That(Alpha(south),Is.EqualTo(160f/255).Within(.001f));
            Assert.That(Alpha(north),Is.EqualTo(1));Assert.That(Alpha(distant),Is.EqualTo(1));
        }

        [Test]
        public void ForegroundCanopyStillUsesStrongerActorTargetAndRestoresMildTarget()
        {
            Passage(8);var tree=SouthernBoundaryTree(new Vector2(4.5f,-2));
            var actor=Actor(new Vector2(4.5f,.5f));Select();Advance(.4f);
            Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
            actor.SetPosition(new Vector2(9.5f,6.5f));Select();Advance(.6f);
            Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
        }

        [Test]
        public void ForegroundLeavesCoveringOnlyTheVisibleTileEdgeStillFade()
        {
            Passage(8);var tree=SouthernBoundaryTree(new Vector2(1.2f,-2));
            Assert.That(map.ForestCanopyContainsPoint(tree,new Vector2(2.5f,.5f)),Is.False);
            Select();Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
        }

        [Test]
        public void PassageLeavesCoveringOnlyTheVisibleTileEdgeStillFade()
        {
            Passage(1);var tree=Tree(new Vector2(1.2f,1));
            Assert.That(map.ForestCanopyContainsPoint(tree,new Vector2(2.5f,3.5f)),Is.False);
            Select();Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
        }

        [Test]
        public void WarmForegroundQueriesAllocateNothingAndDeduplicateCrowns()
        {
            Passage(8);SouthernBoundaryTree(new Vector2(4.5f,-2));var result=new List<SpriteRenderer>(8);
            for(int i=0;i<10;i++)map.CollectVisibleForestForegroundCanopies(result);
            long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++)map.CollectVisibleForestForegroundCanopies(result);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-start,Is.Zero);Assert.That(result.Count,Is.EqualTo(1));
        }

        [TestCase(1,true)] [TestCase(2,true)] [TestCase(3,true)]
        [TestCase(4,false)] [TestCase(8,false)]
        public void OnlyNarrowVisiblePassagesMildlyRevealOverlappingCrowns(int width,bool fades)
        {
            Passage(width);var tree=Tree(new Vector2(2.5f,1));
            Select();Advance(.05f);
            if(fades)Assert.That(Alpha(tree),Is.InRange(160f/255,.999f),"Use the existing smooth entry transition.");
            Advance(.3f);
            Assert.That(Alpha(tree),Is.EqualTo(fades?160f/255:1).Within(.001f));
            Assert.That(rig.LastForestPartyRevealCount,Is.Zero,"A passage is not an actor.");
        }

        [TestCase(false)] [TestCase(true)]
        public void RememberedAndUnknownPassagesDoNotRevealCrowns(bool unknown)
        {
            var floor=Passage(1);var tree=Tree(new Vector2(2.5f,1));
            if(unknown)floor.Fog.SetMapDiscovered(false);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());map.ApplyFog(floor);
            var discovered=floor.Fog.Discovered.ToArray();
            Select();Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(1));
            Assert.That(floor.Fog.Discovered,Is.EquivalentTo(discovered));
            Assert.That(floor.Fog.Visible,Is.Empty);
        }

        [Test]
        public void ActorFadeOverridesPassageAndReturnsToMildTargetBeforeRestoring()
        {
            var floor=Passage(1);var tree=Tree(new Vector2(2.5f,1));
            Select();Advance(.3f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
            var actor=Actor(new Vector2(2.5f,3.5f));
            Select();Advance(.3f);Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
            actor.SetPosition(new Vector2(9,9));Select();Advance(.1f);
            Assert.That(Alpha(tree),Is.InRange(90f/255+.001f,160f/255-.001f));
            Advance(.5f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());map.ApplyFog(floor);
            Select();Advance(.05f);Assert.That(Alpha(tree),Is.LessThan(1));
            Advance(.6f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }

        [Test]
        public void MildPassageFadeDoesNotCountAsPreviousActorContact()
        {
            Passage(2);var tree=Tree(new Vector2(2.5f,1));
            Select();Advance(.3f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
            var actor=Actor(new Vector2(3.55f,3.5f));
            Assert.That(map.ForestCanopyContainsPoint(tree,actor.Position),Is.False);
            var retained=new Bounds(actor.Position,Vector3.one*.16f);
            Assert.That(map.ForestCanopyOverlapsBody(tree,retained),Is.True);
            Select();Advance(.3f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f),
                "The passage's mild fade must not allow an actor outside the crown to upgrade it.");
            actor.SetPosition(new Vector2(3.45f,3.5f));Select();Advance(.3f);
            Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
        }

        [Test]
        public void PassageSelectionUsesLeafMeshInsteadOfTransparentSpriteBounds()
        {
            var floor=Passage(1);
            var texture=Keep(new Texture2D(128,128));var colors=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)colors[y*128+x]=x+y<128?Color.white:Color.clear;
            texture.SetPixels(colors);texture.Apply();
            var sprite=Keep(Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,0),32,0,SpriteMeshType.Tight));
            var tree=Tree(new Vector2(2.5f,1),sprite);
            var cell=new Vector2Int(2,4);var point=map.CellToWorldCenter(cell);
            Assert.That(tree.bounds.Contains(point),Is.True);Assert.That(map.ForestCanopyContainsPoint(tree,point),Is.False);
            floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(cell))});map.ApplyFog(floor);
            Select();Advance(.4f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }

        [Test]
        public void WarmPassageQueriesAllocateNothingAndDeduplicateCrowns()
        {
            Passage(2);Tree(new Vector2(2.5f,1));var result=new List<SpriteRenderer>(8);
            for(int i=0;i<10;i++)map.CollectVisibleForestPassageCanopies(result);
            var start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++)map.CollectVisibleForestPassageCanopies(result);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-start,Is.Zero);
            Assert.That(result.Count,Is.EqualTo(1));
        }

        [Test]
        public void PassageIndexFollowsWallChangesOnTheSameMap()
        {
            var floor=Passage(1);var tree=Tree(new Vector2(2.5f,1));
            var cell=new Vector2Int(2,3);
            floor.Fog.SetVisible(new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(cell))});map.ApplyFog(floor);
            Select();Advance(.3f);Assert.That(Alpha(tree),Is.EqualTo(160f/255).Within(.001f));
            Assert.That(floor.Map.TryAddWall(cell,cell+Vector2Int.up),Is.True);
            Select();Advance(.6f);Assert.That(Alpha(tree),Is.EqualTo(1),"A stale topology cache cannot keep fading a blocked end.");
        }

        [TestCase("Godmist/Dungeon/Torch Body")]
        [TestCase("Godmist/Dungeon/Torch Wall")]
        public void PassageFadeKeepsLeafPixelsAtMilderAlphaOnGpu(string shader)
        {
            Passage(1);var tree=Tree(new Vector2(2.5f,1));tree.gameObject.layer=30;
            var material=Keep(new Material(Shader.Find(shader)));tree.sharedMaterial=material;
            material.SetFloat("_Ambient",1);material.SetFloat("_ForestCanopy",1);
            material.SetFloat("_ForestCanopyRootTopY",2);material.SetFloat("_ForestCanopyCellSize",1);
            Select();Advance(.3f);
            var camera=Keep(new GameObject("Passage leaf pixels",typeof(Camera))).GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=.5f;camera.aspect=1;
            camera.transform.position=new Vector3(2.5f,3.5f,-10);camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=Keep(new RenderTexture(64,64,24));camera.targetTexture=target;
            var image=Keep(new Texture2D(64,64,TextureFormat.RGBA32,false));var active=RenderTexture.active;
            try
            {
                var p=new MaterialPropertyBlock();tree.GetPropertyBlock(p);var fade=p.GetFloat("_ForestCanopyFade");
                p.SetFloat("_ForestCanopyFade",0);tree.SetPropertyBlock(p);var baseline=Pixel();
                p.SetFloat("_ForestCanopyFade",fade);tree.SetPropertyBlock(p);var mild=Pixel();
                Assert.That(baseline,Is.GreaterThan(.1f));
                Assert.That(mild/baseline,Is.EqualTo(160f/255).Within(.025f));
            }
            finally{camera.targetTexture=null;RenderTexture.active=active;}
            float Pixel(){camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();return image.GetPixel(32,32).linear.g;}
        }

        [Test]
        public void FadeIsSmoothReversibleAndLeavesOtherPropertiesUntouched()
        {
            var tree=Tree(new Vector2(2,1));var actor=Actor(new Vector2(2,2));
            var p=new MaterialPropertyBlock();p.SetFloat("_UnrelatedMarker",17);tree.SetPropertyBlock(p);
            Select();Advance(.05f);var first=Alpha(tree);Assert.That(first,Is.InRange(.36f,.99f));
            Advance(.2f);Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));Assert.That(tree.color.a,Is.EqualTo(1));
            actor.SetPosition(new Vector2(7,7));Select();Advance(.05f);Assert.That(Alpha(tree),Is.LessThan(.9f),"Exit cannot instantly restore the tree.");
            Advance(.1f);Assert.That(Alpha(tree),Is.GreaterThan(90f/255));
            actor.SetPosition(new Vector2(2,2));Select();Advance(.2f);Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
            actor.SetPosition(new Vector2(7,7));Select();Advance(.6f);Assert.That(Alpha(tree),Is.EqualTo(1));
            tree.GetPropertyBlock(p);Assert.That(p.GetFloat("_UnrelatedMarker"),Is.EqualTo(17));
        }
        [TestCase(1)] [TestCase(3)] [TestCase(6)]
        public void EachWholeCrownKeepsItsAuthoredAlphaRegardlessOfOverlapCount(int count)
        {
            var trees=Enumerable.Range(0,count).Select(_=>Tree(new Vector2(2,1))).ToArray();Actor(new Vector2(2,2));
            Select();Advance(.4f);
            foreach(var tree in trees)Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
        }
        [TestCase("Godmist/Dungeon/Torch Body",1)] [TestCase("Godmist/Dungeon/Torch Body",3)] [TestCase("Godmist/Dungeon/Torch Body",6)]
        [TestCase("Godmist/Dungeon/Torch Wall",1)] [TestCase("Godmist/Dungeon/Torch Wall",3)] [TestCase("Godmist/Dungeon/Torch Wall",6)]
        public void RealFadeKeepsVisibleLeafPixelsForEachTree(string shader,int count)
        {
            var material=Keep(new Material(Shader.Find(shader)));material.SetFloat("_Ambient",1);
            material.SetFloat("_ForestCanopy",1);material.SetFloat("_ForestCanopyRootTopY",2);material.SetFloat("_ForestCanopyCellSize",1);
            var trees=Enumerable.Range(0,count).Select(_=>Tree(new Vector2(2,1))).ToArray();
            foreach(var tree in trees){tree.gameObject.layer=30;tree.sharedMaterial=material;}
            Actor(new Vector2(2,2));Select();Advance(.3f);
            var camera=Keep(new GameObject("Leaf pixel verification",typeof(Camera))).GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=.5f;camera.aspect=1;camera.transform.position=new Vector3(2,3,-10);
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=Keep(new RenderTexture(64,64,24));camera.targetTexture=target;
            var image=Keep(new Texture2D(64,64,TextureFormat.RGBA32,false));var active=RenderTexture.active;
            var original=new MaterialPropertyBlock();var solid=new MaterialPropertyBlock();
            try
            {
                // Keep the selection of all layers, then isolate each renderer
                // to measure its actual pixel alpha rather than the aggregate.
                foreach(var tree in trees)tree.forceRenderingOff=true;
                foreach(var tree in trees)
                {
                    tree.forceRenderingOff=false;tree.GetPropertyBlock(original);tree.GetPropertyBlock(solid);
                    solid.SetFloat("_ForestCanopyFade",0);tree.SetPropertyBlock(solid);var baseline=Pixel();
                    tree.SetPropertyBlock(original);var faded=Pixel();
                    Assert.That(baseline,Is.GreaterThan(.1f),"The opaque reference leaf must render.");
                    Assert.That(faded/baseline,Is.EqualTo(90f/255).Within(.025f),shader+" must retain visible leaf pixels, even with "+count+" candidate trees.");
                    tree.forceRenderingOff=true;
                }
            }
            finally {camera.targetTexture=null;RenderTexture.active=active;}
            float Pixel(){camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();return image.GetPixel(32,32).linear.g;}
        }
        [Test]
        public void DistantBackgroundHiddenAndRootOnlyTreesStaySolid()
        {
            var front=Tree(new Vector2(2,1));var background=Tree(new Vector2(2,3));var distant=Tree(new Vector2(7,1));
            var hidden=Tree(new Vector2(2,1));hidden.forceRenderingOff=true;
            var roots=Tree(new Vector2(2,2));Actor(new Vector2(2,2.1f));
            // This short actor only overlaps the protected root cell.
            var shortActor=Actor(new Vector2(2,2.1f));Read<Dictionary<string,ExplorationCharacterView>>(party,"viewsById").Clear();
            Read<Dictionary<string,ExplorationCharacterView>>(party,"viewsById").Add("short",shortActor);shortActor.BodyRenderer.transform.localScale=new Vector3(1,.3f,1);
            Select();Advance(.5f);
            Assert.That(Alpha(front),Is.LessThan(1));
            foreach(var t in new[]{background,distant,hidden,roots})Assert.That(Alpha(t),Is.EqualTo(1),t.name+" "+t.transform.position);
        }
        [TestCase(false)] [TestCase(true)]
        public void EnteringWideCrownRevealsItEvenFarFromTrunk(bool large)
        {
            var tree=Tree(new Vector2(2,1),Solid(256,192),large);
            var roots=map.ForestCanopyGroundBounds(tree);
            var actor=Actor(new Vector2(roots.xMax+1.3f,roots.yMax+.2f));
            Assert.That(map.ForestCanopyOverlapsBody(tree,actor.BodyRenderer.bounds),Is.True,"Reproduce a distant branch actually overlapping the actor.");
            Assert.That(map.ForestCanopyContainsPoint(tree,actor.Position),Is.True);
            Select();Advance(.5f);Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f),"Being underneath the crown matters, not distance from its roots.");
            actor.SetPosition(new Vector2(tree.bounds.max.x+.2f,roots.yMax+.2f));
            Assert.That(map.ForestCanopyOverlapsBody(tree,actor.BodyRenderer.bounds),Is.True,"The body still touches the crown after the ground point leaves it.");
            Select();Advance(.6f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }
        [Test]
        public void BodyOrWeaponTouchingCrownDoesNotRevealItBeforeFeetEnter()
        {
            var tree=Tree(new Vector2(2,1));var actor=Actor(new Vector2(3.2f,2.2f));
            Assert.That(map.ForestCanopyOverlapsBody(tree,actor.BodyRenderer.bounds),Is.True);
            Select();Advance(.5f);Assert.That(Alpha(tree),Is.EqualTo(1));
            actor.SetPosition(new Vector2(2.9f,2.2f));Select();Advance(.3f);
            Assert.That(Alpha(tree),Is.EqualTo(90f/255).Within(.001f));
            actor.SetPosition(new Vector2(3.2f,2.2f));Select();Advance(.6f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }
        [Test]
        public void MerelyNearbyTreeDoesNotFadeWithoutActualCanopyOverlap()
        {
            var tree=Tree(new Vector2(2,1));Actor(new Vector2(3.4f,2));
            Select();Advance(.5f);Assert.That(Alpha(tree),Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void TightMeshTransparentCornersDoNotTriggerEvenWhenBoundsOverlap(bool flip)
        {
            var texture=Keep(new Texture2D(128,128));var pixels=new Color[128*128];
            for(var y=0;y<128;y++)for(var x=0;x<128;x++)pixels[y*128+x]=x+y<128?Color.white:Color.clear;
            texture.SetPixels(pixels);texture.Apply();
            var sprite=Keep(Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,0),32,0,SpriteMeshType.Tight));
            var tree=Tree(new Vector2(3,1),sprite);tree.flipX=flip;
            var empty=new Bounds(new Vector3(flip?1.3f:4.7f,4.6f),new Vector3(.15f,.15f,1));
            var leafy=new Bounds(new Vector3(flip?4.7f:1.3f,4.6f),new Vector3(.15f,.15f,1));
            Assert.That(tree.bounds.Intersects(empty),Is.True);
            Assert.That(map.ForestCanopyOverlapsBody(tree,empty),Is.False);
            Assert.That(map.ForestCanopyOverlapsBody(tree,leafy),Is.True);
            Assert.That(map.ForestCanopyContainsPoint(tree,empty.center),Is.False);
            Assert.That(map.ForestCanopyContainsPoint(tree,leafy.center),Is.True);
        }
        [Test]
        public void WarmOcclusionQueriesAllocateNothing()
        {
            Tree(new Vector2(2,1));var candidates=new List<SpriteRenderer>(16);
            var body=new Bounds(new Vector3(2,2.5f),new Vector3(.7f,1,1));
            for(var i=0;i<10;i++)map.CollectForestRevealCanopies(new Vector2Int(2,2),body,2,candidates);
            var start=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++)map.CollectForestRevealCanopies(new Vector2Int(2,2),body,2,candidates);
            var allocated=GC.GetAllocatedBytesForCurrentThread()-start;
            Assert.That(candidates.Count,Is.EqualTo(1));Assert.That(allocated,Is.EqualTo(0));
        }
        [Test]
        public void DisableRestoresFadingCrownsAndReleasesState()
        {
            var tree=Tree(new Vector2(2,1));Actor(new Vector2(2,2));Select();Advance(.3f);
            Assert.That(Alpha(tree),Is.LessThan(1));Call(rig,"OnDisable");Assert.That(Alpha(tree),Is.EqualTo(1));
            Assert.That(Read<System.Collections.IDictionary>(rig,"canopyFades").Count,Is.EqualTo(0));
        }
    }
}
