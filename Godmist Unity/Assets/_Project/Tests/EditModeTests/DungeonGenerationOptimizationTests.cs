using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonGenerationOptimizationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Read<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Private).GetValue(owner);
        static string Hash(byte[] data) {using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(data));}

        static object NewPassageLookup() => Activator.CreateInstance(
            typeof(Godmist.Systems.Dungeons.DungeonGeneratorService).GetNestedType("PassageLookup", BindingFlags.NonPublic), true);

        [TestCase(0, false)] [TestCase(90, false)] [TestCase(180, false)] [TestCase(270, false)]
        [TestCase(0, true)] [TestCase(90, true)] [TestCase(180, true)] [TestCase(270, true)]
        public void CachedPassagesMatchReferenceForRotatedLevelsAndBlockedExits(int rotation, bool configured)
        {
            var first = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DungeonRoomTemplateDefinition>();
            var second = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DungeonRoomTemplateDefinition>();
            void Set(object owner, string name, object value) => owner.GetType().GetField("<"+name+">k__BackingField", Private).SetValue(owner,value);
            try
            {
                foreach (var template in new[]{first, second})
                {
                    Set(template,"SizeInCells",new Vector2Int(2,2));
                    Set(template,"BoundaryEdgesConfigured",configured);
                    template.AllowedBoundaryEdges.Clear();
                    foreach (var level in new[]{2,0,1})
                        for (var x=0;x<2;x++) for(var y=0;y<2;y++)
                        {
                            var cell=new Vector2Int(x,y);
                            template.Surfaces.Add(new Godmist.Data.Dungeons.DungeonRoomSurfaceData(cell,level));
                            foreach (Godmist.Core.Enums.DungeonEdgeDirection direction in Enum.GetValues(typeof(Godmist.Core.Enums.DungeonEdgeDirection)))
                                if (Godmist.Data.Dungeons.DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(cell,direction,template.SizeInCells,
                                    new System.Collections.Generic.HashSet<Vector2Int>{Vector2Int.zero,Vector2Int.right,Vector2Int.up,Vector2Int.one}))
                                    template.AllowedBoundaryEdges.Add(new Godmist.Data.Dungeons.DungeonRoomBoundaryEdgeData(cell,direction,level));
                        }
                }
                // Remove one exit only on one surface, retaining both full footprint and other levels.
                second.AllowedBoundaryEdges.RemoveAll(e=>e.LocalCell==Vector2Int.zero && e.Level==0);
                first.StructuralAnchors.Add(new Godmist.Data.Dungeons.DungeonStructuralAnchorData(
                    Godmist.Core.Enums.DungeonStructuralAnchorType.Wall,Vector2Int.one,Godmist.Core.Enums.DungeonEdgeDirection.Right));
                var lookup=NewPassageLookup();
                var match=lookup.GetType().GetMethod("TryFindCommonLevel");
                foreach (Godmist.Core.Enums.DungeonEdgeDirection direction in Enum.GetValues(typeof(Godmist.Core.Enums.DungeonEdgeDirection)))
                    for (var x=-1;x<=2;x++) for(var y=-1;y<=2;y++) for(var width=1;width<=2;width++)
                    {
                        var lateral=direction==Godmist.Core.Enums.DungeonEdgeDirection.Left || direction==Godmist.Core.Enums.DungeonEdgeDirection.Right ? Vector2Int.up : Vector2Int.right;
                        var expected=Godmist.Systems.Dungeons.DungeonPassageGeometry.TryFindCommonLevel(first,second,rotation,0,
                            new Vector2Int(x,y),Vector2Int.zero,width,lateral,direction,Godmist.Core.Enums.DungeonEdgeDirection.Left,out var level);
                        var args=new object[]{first,second,rotation,0,new Vector2Int(x,y),Vector2Int.zero,width,lateral,direction,Godmist.Core.Enums.DungeonEdgeDirection.Left,0};
                        Assert.That((bool)match.Invoke(lookup,args),Is.EqualTo(expected),$"{direction} {x},{y} width {width}");
                        Assert.That((int)args[10],Is.EqualTo(level));
                    }
            }
            finally {UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(second);}
        }

        [Test]
        public void NativeBuildCapturesPhysicsSourcesOnlyOnce()
        {
            var root=new GameObject("Single geometry capture",typeof(Grid));
            using var mesh=new Godmist.Views.Exploration.DungeonNavigationMesh();
            try
            {
                var tiles=new GameObject("Floor",typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",Private).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                for(var x=0;x<5;x++)for(var y=0;y<3;y++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var floor=new Godmist.Runtime.Dungeons.DungeonFloorInstance(0);floor.TrySetMap(map);
                var captures=0;
                System.Collections.Generic.IEnumerable<Bounds> Collect()
                {
                    captures++;
                    yield return new Bounds(new Vector3(2.5f,.5f,0),new Vector3(.1f,1,0));
                }
                Assert.That(mesh.Build(floor,view,0,.1f,Collect()),Is.True);
                Assert.That(captures,Is.EqualTo(1));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test]
        public void DeferredForestWithNoObstaclesStillBakesOnceAndInvalidatesChangedRoots()
        {
            var root=new GameObject("Deferred empty forest",typeof(Grid));
            var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var tiles=new GameObject("Floor",typeof(UnityEngine.Tilemaps.Tilemap),typeof(UnityEngine.Tilemaps.TilemapRenderer));tiles.transform.SetParent(root.transform);
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",Private).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile",Private).SetValue(view,tile);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab",Private).SetValue(view,UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                view.ConfigureVegetationPresentation(catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.forest.expedition").VegetationPresentation);
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                for(var x=0;x<4;x++)for(var y=0;y<4;y++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                view.Rebuild(map,deferForestGround:true);
                Assert.That(Read<int>(view,"forestSurfaceMaskBuildCount"),Is.Zero);
                view.SetForestPathObstacles(Array.Empty<Vector2Int>());
                Assert.That(Read<Texture2D>(view,"forestSurfaceMask"),Is.Not.Null);
                Assert.That(Read<int>(view,"forestSurfaceMaskBuildCount"),Is.EqualTo(1));
                view.SetForestPathObstacles(Array.Empty<Vector2Int>());
                Assert.That(Read<int>(view,"forestSurfaceMaskBuildCount"),Is.EqualTo(1));
                view.SetForestPathObstacles(new[]{Vector2Int.one});
                Assert.That(Read<int>(view,"forestSurfaceMaskBuildCount"),Is.EqualTo(2));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tile);}
        }

        public sealed class StandingProbe : DungeonObjectView
        {
            protected override int ResolveDepthSortingOrder(DungeonFloorMapView view)
                => view.GetStandingObjectDepthSortingOrder(transform.position.y);
        }

        [Test]
        public void ReserveDepthBaselinesBuildsOnceAndBindsFinalActorLanes()
        {
            var root=new GameObject("Batched object depth setup");
            try
            {
                var map=root.AddComponent<DungeonFloorMapView>();
                var objects=new DungeonObjectView[100];
                for(var i=0;i<objects.Length;i++)
                {
                    var child=new GameObject("Standing probe",typeof(SpriteRenderer));child.transform.SetParent(root.transform);
                    child.transform.position=new Vector3(0,i+.123f,0);objects[i]=child.AddComponent<StandingProbe>();
                }
                map.ReserveObjectDepthBaselines(objects);
                Assert.That(Read<int>(map,"depthSortingBuildCount"),Is.EqualTo(1));
                foreach(var obj in objects)
                {
                    obj.BindElevationView(map);
                    var order=obj.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                    Assert.That(order,Is.EqualTo(map.GetStandingObjectDepthSortingOrder(obj.transform.position.y)));
                    Assert.That(map.GetActorDepthSortingOrder(obj.transform.position.y-.03f),Is.GreaterThan(order));
                }
                Assert.That(Read<int>(map,"depthSortingBuildCount"),Is.EqualTo(1));
                map.GetStandingObjectDepthSortingOrder(150.123f);
                Assert.That(Read<int>(map,"depthSortingBuildCount"),Is.EqualTo(2),"Later individual registrations must remain immediate.");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [TestCase("dungeon.forest.expedition",DungeonLength.Long,171026)]
        [TestCase("dungeon.forest.expedition",DungeonLength.Long,0)]
        [TestCase("dungeon.forest.expedition",DungeonLength.Short,171026)]
        [TestCase("dungeon.catacombs.expedition",DungeonLength.Long,171026)]
        public void ProductionEntryReproducesCurrentAppearanceAndLayout(string id,DungeonLength length,int seed)
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/branch-counts-20261023/entry-replay"));
            Directory.CreateDirectory(output);
            var key=id+"-"+length+"-"+seed;
            string expected = null;
            // Historical optimization captures pin old content/layouts. Replay
            // current content instead; deliberate topology changes are legal.
            foreach (var stage in new[] { "first", "repeat" })
            {
            var probe=new DungeonEntryPerformanceTests {OutputOverride=output,StageOverride=stage+"-"+seed,SeedOverride=seed};
            probe.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var floor=session.ActiveDungeon.Floors[0];
                var cells=string.Join(";",floor.Map.FloorCells.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>c.ToString()));
                var records=map.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled && r.sprite!=null)
                    .Select(r=>$"{r.name}|{r.sprite.name}|{r.transform.position:R}|{r.transform.lossyScale:R}|{r.sortingLayerID}|{r.sortingOrder}")
                    .OrderBy(s=>s,StringComparer.Ordinal).ToArray();
                var mask=Read<Texture2D>(map,"forestSurfaceMask");
                if(mask!=null)
                    Assert.That(Read<int>(map,"forestSurfaceMaskBuildCount"),Is.EqualTo(1),"Initial forest ground must be baked once with final props.");
                var maskHash="none";
                if(mask!=null)
                {
                    var target=RenderTexture.GetTemporary(mask.width,mask.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                    var old=RenderTexture.active;
                    var capture=new Texture2D(mask.width,mask.height,TextureFormat.RGBA32,false,true);
                    try
                    {
                        Graphics.Blit(mask,target);RenderTexture.active=target;
                        capture.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0);capture.Apply();
                        maskHash=Hash(capture.GetRawTextureData());
                    }
                    finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(capture);}
                }
                var result=new Manifest{rooms=floor.Rooms.Count,cells=floor.Map.FloorCells.Count,renderers=records.Length,
                    layoutHash=Hash(Encoding.UTF8.GetBytes(cells)),artHash=Hash(Encoding.UTF8.GetBytes(string.Join("\n",records))),maskHash=maskHash};
                var text=JsonUtility.ToJson(result,true);
                File.WriteAllText(Path.Combine(output,stage+"-"+key+".json"),text);
                if(expected!=null)Assert.That(text,Is.EqualTo(expected),"Repeated entry must reproduce sprite transforms/orders, paths, ground mask and layout.");
                expected = text;
            };
            probe.MeasureProductionFloorEntry(id,length);
            }
        }
        [Serializable] sealed class Manifest {public int rooms,cells,renderers;public string layoutHash,artHash,maskHash;}

        [TestCase("dungeon.catacombs.expedition",DungeonLength.Long,171026)]
        [TestCase("dungeon.catacombs.expedition",DungeonLength.Long,6)]
        [TestCase("dungeon.forest.expedition",DungeonLength.Long,171026)]
        [TestCase("dungeon.forest.expedition",DungeonLength.Long,6)]
        public void ProductionGenerationAndNavigationReproduceForCurrentContent(string id,DungeonLength length,int seed)
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/branch-counts-20261023/generation-replay"));
            Directory.CreateDirectory(output);
            var key=id+"-"+length+"-"+seed;
            string expected = null, expectedNavigation = null;
            foreach (var stage in new[] { "first", "repeat" })
            {
            var probe=new DungeonEntryPerformanceTests {OutputOverride=output,StageOverride=stage+"-"+seed,SeedOverride=seed};
            probe.FrameProbe=(bootstrap,session)=>
            {
                var view=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var snapshot=new GenerationReplay {Floors=Godmist.Systems.SaveLoad.DungeonLayoutSnapshot.Capture(session.ActiveDungeon)};
                var records=view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null)
                    .Select(r=>$"{r.name}|{r.enabled}|{r.sprite.name}|{r.transform.position:R}|{r.transform.lossyScale:R}|{r.sortingLayerID}|{r.sortingOrder}")
                    .Concat(view.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).Select(m=>
                        $"{m.name}|{m.transform.position:R}|{m.transform.lossyScale:R}|"+
                        Hash(Encoding.UTF8.GetBytes(string.Join(";",m.sharedMesh.vertices.Select(v=>v.ToString("R")))+"|"+string.Join(",",m.sharedMesh.triangles)))))
                    .OrderBy(s=>s,StringComparer.Ordinal).ToArray();
                var actual=JsonUtility.ToJson(snapshot)+"\n"+string.Join("\n",records);
                File.WriteAllText(Path.Combine(output,stage+"-"+key+".manifest.txt"),actual);
                var party=Read<Godmist.Views.Dungeons.DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var meshes=Read<System.Collections.Generic.Dictionary<(int Level,float Radius),Godmist.Views.Exploration.DungeonNavigationMesh>>(party,"navigationMeshes");
                var navigation=new StringBuilder(); var metrics=new StringBuilder();
                var cells=session.ActiveDungeon.Floors[0].Map.FloorCells.OrderBy(c=>c.y).ThenBy(c=>c.x).ToArray();
                var points=Enumerable.Range(0,Math.Min(48,cells.Length)).Select(i=>(Vector2)view.CellToWorldCenter(cells[i*cells.Length/Math.Min(48,cells.Length)])).ToArray();
                foreach(var pair in meshes.OrderBy(p=>p.Key.Level).ThenBy(p=>p.Key.Radius))
                {
                    metrics.AppendLine($"{pair.Key}|sources={pair.Value.BuildSourceCount}|geometryMs={pair.Value.GeometryBuildMilliseconds:F3}|nativeMs={pair.Value.NativeBakeMilliseconds:F3}|bounds={pair.Value.BuildBounds}");
                    navigation.AppendLine(pair.Key.ToString());
                    for(int a=0;a<points.Length;a++) for(int b=a;b<points.Length;b++)
                        navigation.Append(pair.Value.TryFindPath(points[a],points[b],Vector2.zero,(from,to)=>true,out _)?'1':'0');
                    navigation.AppendLine();
                }
                File.WriteAllText(Path.Combine(output,stage+"-"+key+".navigation.txt"),navigation.ToString());
                File.WriteAllText(Path.Combine(output,stage+"-"+key+".metrics.txt"),metrics.ToString());
                if(expected!=null) Assert.That(actual,Is.EqualTo(expected),
                    "The same current seed/content must reproduce floors, rooms, walls, stairs, encounters, objects and static presentation.");
                if(expectedNavigation!=null) Assert.That(navigation.ToString(),Is.EqualTo(expectedNavigation),
                    "Native route availability must remain identical for the same sampled start/goal pairs.");
                expected = actual;
                expectedNavigation = navigation.ToString();
            };
            probe.MeasureProductionFloorEntry(id,length);
            }
        }
        [Serializable] sealed class GenerationReplay
        { public System.Collections.Generic.List<Godmist.Core.Serialization.DungeonFloorLayoutSaveData> Floors; }
    }
}
