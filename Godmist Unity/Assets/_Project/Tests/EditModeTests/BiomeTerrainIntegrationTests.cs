using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
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
    public class BiomeTerrainIntegrationTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static string Output=>Path.GetFullPath("../LocalHandoffs/biome-terrain-20261023");
        static T Read<T>(object instance,string name)=>(T)instance.GetType().GetField(name,Flags).GetValue(instance);
        static void Call(object instance,string name,params object[] args)=>instance.GetType().GetMethod(name,Flags).Invoke(instance,args);
        static DungeonDefinition Definition(string biome)=>AssetDatabase.LoadAssetAtPath<DungeonDefinition>(biome=="forest"
            ?Godmist.Editor.ForestExpeditionsSetup.RegularPath:"Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset");

        [TestCase("forest")] [TestCase("catacombs")]
        public void ProductionTerrainHasBypassesAndDoesNotChangeRoomContent(string biome)
        {
            var rooms=Definition(biome).RoomTemplateCatalog.GenerationTemplates;
            Assert.That(rooms.Any(r=>r.DifficultTerrainCells.Count>0),Is.True);
            Assert.That(rooms.Any(r=>r.StructuralAnchors.Any(a=>a.WallType==DungeonWallType.Low)),Is.True);
            foreach(var room in rooms)
            {
                var report=Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(room);
                Assert.That(report.IsValid,Is.True,room.Id+": "+string.Join("; ",report.Errors));
                var cells=room.Footprint.Enumerate(room.SizeInCells).ToHashSet();
                var walls=room.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Wall && a.WallType==DungeonWallType.Low)
                    .Select(a=>new DungeonCellEdge(a.LocalCell,a.LocalCell+a.GetDirectionOffset())).ToHashSet();
                Assert.That(room.DifficultTerrainCells.All(cells.Contains),Is.True);
                var reached=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(cells.First());
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();if(!reached.Add(cell))continue;
                    foreach(var d in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
                    {var other=cell+d;if(cells.Contains(other)&&!walls.Contains(new DungeonCellEdge(cell,other))&&!reached.Contains(other))queue.Enqueue(other);}
                }
                Assert.That(reached.SetEquals(cells),Is.True,room.Id+": crossing a low wall must never be compulsory.");
            }
        }

        [TestCase("forest")] [TestCase("catacombs")]
        public void GeneratedFloorsKeepTerrainAndCrossableLowWalls(string biome)
        {
            var definition=Definition(biome);var difficult=0;var low=0;
            foreach(var length in new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long})
            foreach(var seed in new[]{1,9,171026})
            {
                var dungeon=new DungeonGeneratorService().Generate(definition,seed,10,length);
                var catalog=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>(Godmist.Editor.ForestExpeditionsSetup.CatalogPath);
                new DungeonObjectPlacementSystem().Place(dungeon,catalog.EquipmentPartDefinitions);
                foreach(var floor in dungeon.Floors)
                {
                    difficult+=floor.Map.DifficultTerrainCells.Count;
                    low+=floor.Map.SurfaceWallEdges.Count(e=>floor.Map.GetWallType(e)==DungeonWallType.Low);
                    foreach(var edge in floor.Map.SurfaceWallEdges.Where(e=>floor.Map.GetWallType(e)==DungeonWallType.Low))
                    {
                        Assert.That(floor.Map.IsWalkable(edge.First)&&floor.Map.IsWalkable(edge.Second),Is.True);
                        Assert.That(floor.Map.CanMoveBetween(edge.FirstCell,edge.SecondCell),Is.True);
                        var blocked=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive&&o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells()).ToHashSet();
                        Assert.That(blocked.Contains(edge.FirstCell)||blocked.Contains(edge.SecondCell),Is.False,"A low barrier cannot be buried in a blocking prop.");
                    }
                }
            }
            Assert.That(difficult,Is.Zero,"Difficult terrain is disabled in playable dungeons.");Assert.That(low,Is.GreaterThan(0));
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,biome+"-counts.txt"),$"9 layouts: difficult cells={difficult}, low edges={low}");
        }

        [TestCase(true)] [TestCase(false)]
        public void CatacombsLowWallUsesNativeTextureWithExactlyEightBottomPixelsRemoved(bool horizontal)
        {
            var full=AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var low=AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab");
            var root=new GameObject("Low crop test",typeof(Grid));var tiles=new GameObject("Tiles",typeof(Tilemap));tiles.transform.SetParent(root.transform);
            var view=Object.Instantiate(low,root.transform);var edge=new DungeonCellEdge(Vector2Int.zero,horizontal?Vector2Int.up:Vector2Int.right);
            try
            {
                view.Bind(edge,tiles.GetComponent<Tilemap>(),false);
                var position=view.transform.position;var scale=view.transform.localScale;
                var source=horizontal?full.NorthWallSprite:full.SideWallSprite;
                Assert.That(view.ConfigureCroppedLowWall(edge,source,Vector2.zero),Is.True);
                var renderer=view.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.enabled);
                Assert.That(renderer.sprite.texture,Is.SameAs(source.texture));
                Assert.That(renderer.sprite.rect.y,Is.EqualTo(source.rect.y+8));
                Assert.That(renderer.sprite.rect.height,Is.EqualTo(source.rect.height-8));
                Assert.That(renderer.sprite.pixelsPerUnit,Is.EqualTo(source.pixelsPerUnit));
                Assert.That(renderer.bounds.size.x,Is.EqualTo(horizontal?view.VisualLength:8/source.pixelsPerUnit).Within(.001f));
                Assert.That(renderer.bounds.size.y,Is.EqualTo(horizontal?(source.rect.height-8)/source.pixelsPerUnit:view.VisualLength+.5f).Within(.001f));
                Assert.That(view.transform.position,Is.EqualTo(position));Assert.That(view.transform.localScale,Is.EqualTo(scale));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [TestCase(true)] [TestCase(false)]
        public void LowHedgesFollowFogAndTerrainMaskIsReleasedOnRebuild(bool horizontal)
        {
            var root=new GameObject("Hedge fog audit",typeof(Grid));
            try
            {
                var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                void Set(string name,object value)=>typeof(DungeonFloorMapView).GetField(name,Flags).SetValue(view,value);
                Set("floorTilemap",tiles.GetComponent<Tilemap>());
                Set("floorTile",AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset"));
                Set("wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                Set("lowWallPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab"));
                view.ConfigureVegetationPresentation(Definition("forest").VegetationPresentation);
                var map=new DungeonFloorMap();for(var y=0;y<8;y++)for(var x=0;x<8;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                map.SetWallType(new DungeonCellEdge(new Vector2Int(3,3),horizontal?new Vector2Int(3,4):new Vector2Int(4,3)),DungeonWallType.Low);
                map.SetDifficultTerrain(new Vector2Int(2,2));
                var floor=new DungeonFloorInstance(0);floor.TryAddRoom(new DungeonRoomInstance("room",DungeonRoomType.Normal));floor.TrySetMap(map);
                view.Rebuild(map);view.ApplyFog(floor);
                var hedges=view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Crossable forest low hedge").ToArray();
                Assert.That(hedges.Length,Is.EqualTo(1),"One native shrub spans a horizontal edge without duplicate renderers.");Assert.That(hedges.All(r=>r.forceRenderingOff),Is.True);
                Assert.That(hedges[0].transform.eulerAngles.z,Is.EqualTo(0).Within(.001f));
                Assert.That(hedges[0].transform.localScale,Is.EqualTo(Vector3.one),"Native hedge art must keep square pixels and upright lighting.");
                Assert.That(hedges.All(r=>!view.IsForestCanopyRenderer(r)&&r.GetComponent<Collider2D>()==null),Is.True);
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);Assert.That(hedges.All(r=>!r.forceRenderingOff),Is.True);
                var mask=Read<Texture2D>(view,"forestDifficultTerrainMask");Assert.That(mask,Is.Not.Null);
                var empty=new DungeonFloorMap();empty.TryAddFloorCell(Vector2Int.zero,"room");view.Rebuild(empty);
                Assert.That(Read<Texture2D>(view,"forestDifficultTerrainMask"),Is.Null);
                Assert.That(mask==null,Is.True,"The old runtime mask must be destroyed, not retained across floors.");
            }
            finally{Object.DestroyImmediate(root);}
        }

        [TestCase("forest")] [TestCase("catacombs")]
        [Explicit("Native production render audit, no user save or Play Mode.")]
        public void AuditNativeTerrainAndLowWalls(string biome)
        {
            Directory.CreateDirectory(Output);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride="native-"+biome,SeedOverride=1};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var floor=session.ActiveDungeon.Floors[0];var view=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");var camera=Read<Camera>(rig,"worldCamera");
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                var buffer=new SpriteRenderer[8];
                var edge=floor.Map.SurfaceWallEdges.Where(e=>floor.Map.GetWallType(e)==DungeonWallType.Low).OrderBy(e=>
                {
                    var point=(view.CellToWorldCenter(e.FirstCell)+view.CellToWorldCenter(e.SecondCell))*.5f;
                    return view.CollectForestCanopies(view.WorldToCell(point),new Bounds(point+Vector2.up*.5f,new Vector3(1,1,1)),point.y,buffer);
                }).First();
                var lowPoint=(view.CellToWorldCenter(edge.FirstCell)+view.CellToWorldCenter(edge.SecondCell))*.5f;
                var terrainPoint=view.CellToWorldCenter(floor.Map.DifficultTerrainCells.DefaultIfEmpty(edge.FirstCell).First());
                var target=new RenderTexture(1200,800,24);var image=new Texture2D(1200,800,TextureFormat.RGBA32,false);
                var old=camera.targetTexture;var active=RenderTexture.active;var layers=new Dictionary<GameObject,int>();
                try
                {
                    foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                        if(!layers.ContainsKey(r.gameObject)){layers.Add(r.gameObject,r.gameObject.layer);r.gameObject.layer=30;}
                    camera.cullingMask=1<<30;camera.targetTexture=target;camera.aspect=1.5f;camera.orthographicSize=3.5f;
                    var samples=new List<(string,Vector2)>{("low",lowPoint),("difficult",terrainPoint)};
                    var vertical=floor.Map.SurfaceWallEdges.Where(e=>floor.Map.GetWallType(e)==DungeonWallType.Low&&e.FirstCell.x!=e.SecondCell.x)
                        .OrderBy(e=>
                        {
                            var point=(view.CellToWorldCenter(e.FirstCell)+view.CellToWorldCenter(e.SecondCell))*.5f;
                            return view.CollectForestCanopies(view.WorldToCell(point),new Bounds(point+Vector2.up*.5f,new Vector3(1,1,1)),point.y,buffer);
                        }).First();
                    samples.Add(("low-vertical",(view.CellToWorldCenter(vertical.FirstCell)+view.CellToWorldCenter(vertical.SecondCell))*.5f));
                    if(biome=="catacombs")
                    {
                        var horizontal=floor.Map.SurfaceWallEdges.First(e=>floor.Map.GetWallType(e)==DungeonWallType.Low&&e.FirstCell.x==e.SecondCell.x);
                        samples.Add(("low-horizontal",(view.CellToWorldCenter(horizontal.FirstCell)+view.CellToWorldCenter(horizontal.SecondCell))*.5f));
                    }
                    foreach(var sample in samples)
                    {
                        camera.transform.position=new Vector3(sample.Item2.x,sample.Item2.y+1,-10);
                        var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                        var members=party.ActiveMemberViews.ToArray();
                        for(var i=0;i<members.Length;i++)
                        {
                            var desired=sample.Item2+new Vector2(-1+i*.7f,-1.5f);
                            var cell=floor.Map.FloorCells.OrderBy(c=>((Vector2)view.CellToWorldCenter(c)-desired).sqrMagnitude).First();
                            members[i].SetPosition(view.CellToWorldCenter(cell));Call(members[i],"LateUpdate");
                        }
                        Call(view,"LateUpdate");Call(rig,"RefreshLighting",2f);
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                        image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(Output,biome+"-"+sample.Item1+".png"),image.EncodeToPNG());
                        if(biome=="forest"&&(sample.Item1=="low"||sample.Item1=="low-vertical"))
                        {
                            var hedges=view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Crossable forest low hedge").ToArray();
                            Assert.That(hedges.Any(r=>r.enabled&&!r.forceRenderingOff),Is.True);
                            File.WriteAllLines(Path.Combine(Output,"hedge-receivers.txt"),hedges.Select(r=>$"{r.transform.position} bounds={r.bounds} enabled={r.enabled}, off={r.forceRenderingOff}, color={r.color}, shader={r.sharedMaterial.shader.name}"));
                            foreach(var hedge in hedges)hedge.gameObject.layer=31;
                            camera.cullingMask=1<<31;camera.Render();RenderTexture.active=target;
                            image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                            File.WriteAllBytes(Path.Combine(Output,"forest-"+sample.Item1+"-isolated.png"),image.EncodeToPNG());
                            foreach(var hedge in hedges)hedge.gameObject.layer=30;camera.cullingMask=1<<30;
                        }
                        if(biome=="forest"&&sample.Item1=="difficult"&&floor.Map.DifficultTerrainCells.Any())
                        {
                            var mask=Read<Texture2D>(view,"forestDifficultTerrainMask");Assert.That(mask,Is.Not.Null);
                            var material=Read<Material>(rig,"floorMaterial");Assert.That(material.GetTexture("_ForestDifficultTerrainMask"),Is.SameAs(mask));
                            var before=image.GetPixels32();material.SetTexture("_ForestDifficultTerrainMask",Texture2D.blackTexture);
                            camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                            var after=image.GetPixels32();var changed=Enumerable.Range(0,before.Length).Count(i=>Math.Abs(before[i].r-after[i].r)>2 || Math.Abs(before[i].g-after[i].g)>2);
                            Assert.That(changed,Is.GreaterThan(100),"Logical difficult terrain needs a visible GPU surface difference.");
                            File.WriteAllBytes(Path.Combine(Output,"forest-difficult-without-marker.png"),image.EncodeToPNG());
                            material.SetTexture("_ForestDifficultTerrainMask",mask);
                        }
                    }
                }
                finally{foreach(var pair in layers)if(pair.Key!=null)pair.Key.layer=pair.Value;camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
            };
            fixture.MeasureProductionFloorEntry("dungeon."+biome+".expedition",DungeonLength.Normal);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        [TestCase(3)] [TestCase(12)]
        [TestCase(5)] [TestCase(6)] [TestCase(9)] [TestCase(10)]
        [TestCase(7)] [TestCase(11)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void LowWallVertexUsesSharedTopologyAndSingleCapOwner(int arms)
        {
            var root=new GameObject("Native low topology",typeof(Grid));
            try
            {
                var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                void Set(string name,object value)=>typeof(DungeonFloorMapView).GetField(name,Flags).SetValue(view,value);
                Set("floorTilemap",tiles.GetComponent<Tilemap>());
                Set("floorTile",AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset"));
                Set("wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                Set("lowWallPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab"));
                view.ConfigureCatacombsArtPresentation(true);
                var map=new DungeonFloorMap();for(var y=0;y<8;y++)for(var x=0;x<8;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                void Edge(Vector2Int a,Vector2Int b)=>map.SetWallType(new DungeonCellEdge(a,b),DungeonWallType.Low);
                if((arms&1)!=0)Edge(new Vector2Int(3,3),new Vector2Int(3,4));
                if((arms&2)!=0)Edge(new Vector2Int(4,3),new Vector2Int(4,4));
                if((arms&4)!=0)Edge(new Vector2Int(3,3),new Vector2Int(4,3));
                if((arms&8)!=0)Edge(new Vector2Int(3,4),new Vector2Int(4,4));
                var revision=map.TraversalRevision;view.Rebuild(map);
                foreach(var wall in view.GetComponentsInChildren<DungeonWallSegmentView>().Where(w=>w.IsVerticalLowWall))
                {
                    var group=wall.GetComponent<SortingGroup>();
                    for(var row=-2;row<=10;row++) Assert.That(group.sortingOrder,Is.LessThan(view.GetActorDepthSortingOrder(row)),"Vertical low-wall caps and face must stay below actors at every Y.");
                }

                Assert.That(map.TraversalRevision,Is.EqualTo(revision));
                var renderers=view.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).ToArray();
                var patches=renderers.Where(r=>r.name=="Native low wall cap junction "+arms
                    &&Vector2.Distance(r.transform.position,new Vector2(4,4.625f))<.001f).ToArray();
                Assert.That(patches.Length,Is.EqualTo(arms==3||arms==12?0:1),"A shared vertex has one cap owner, including all L/T/cross orientations.");
                if(patches.Length>0){Assert.That(patches[0].bounds.size.x,Is.EqualTo(.25f).Within(.001f));Assert.That(patches[0].bounds.size.y,Is.EqualTo(.25f).Within(.001f));}
                foreach(var face in renderers.Where(r=>r.name=="Native low wall south face"))
                {
                    Assert.That(face.sprite.texture,Is.SameAs(AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").NorthWallSprite.texture));
                    Assert.That(face.bounds.size.y,Is.EqualTo(.5f).Within(.001f));
                    var endCap=renderers.Single(r=>r.name=="Native low wall cap junction 8"
                        &&Mathf.Abs(r.transform.position.x-face.transform.position.x)<.001f
                        &&Mathf.Abs(r.bounds.min.y-face.bounds.max.y)<.001f);
                    Assert.That(endCap.bounds.min.y,Is.EqualTo(face.bounds.max.y).Within(.001f),"The exposed front must touch its cap without a gap.");
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
