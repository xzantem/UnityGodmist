using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestThinDividerTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-thin-dividers-shadow-stability-20261022"));

        [TestCase(false)]
        [TestCase(true)]
        public void SolidDividerBetweenWalkableCellsHasTreeArtWithoutChangingTraversal(bool horizontal)
        {
            using var f=new Fixture();
            for(int y=-3;y<=3;y++)for(int x=-4;x<=4;x++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"glade");
            var edges=new List<DungeonSurfaceEdge>();
            for(int i=-2;i<=2;i++)
            {
                var a=horizontal?new Vector2Int(i,0):new Vector2Int(0,i);
                var b=a+(horizontal?Vector2Int.up:Vector2Int.right);
                f.Map.TryAddWall(a,b);edges.Add(new DungeonSurfaceEdge(new DungeonCellEdge(a,b)));
            }
            f.Build();
            Capture(f,horizontal?"horizontal":"vertical");
            var owners=(Dictionary<Renderer,DungeonSurfaceEdge[]>)typeof(DungeonFloorMapView).GetField("forestBoundaryOwners",Private).GetValue(f.View);
            foreach(var edge in edges)
            {
                Assert.That(f.Map.CanMoveBetween(edge.FirstCell,edge.SecondCell),Is.False);
                Assert.That(owners.Any(p=>p.Value.Contains(edge)&&p.Key is SpriteRenderer s&&f.View.IsForestCanopyRenderer(s)),Is.True,
                    "A real solid divider with floor on both sides must not be an invisible wall: "+edge);
            }
            Assert.That(f.Map.FloorCells.Count,Is.EqualTo(63));
            Assert.That(f.Map.CanMoveBetween(new Vector2Int(-3,-3),new Vector2Int(-2,-3)),Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OneCellVoidStripKeepsCompactTreesOutsideTheWalkableRoutes(bool horizontal)
        {
            using var f=new Fixture();
            for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)
                if((horizontal?y:x)!=0)f.Map.TryAddFloorCell(new Vector2Int(x,y),"route");
            f.Build();
            var footprints=(Dictionary<SpriteRenderer,RectInt>)typeof(DungeonFloorMapView).GetField("forestTreeFootprints",Private).GetValue(f.View);
            foreach(int i in Enumerable.Range(-2,5))
            {
                var cell=horizontal?new Vector2Int(i,0):new Vector2Int(0,i);
                Assert.That(footprints.Any(p=>p.Value.Contains(cell)),Is.True,"Thin void needs a compact tree at "+cell);
            }
            foreach(var footprint in footprints.Values)
                foreach(var cell in footprint.allPositionsWithin)Assert.That(f.Map.IsWalkable(cell),Is.False);
            Capture(f,horizontal?"void-horizontal":"void-vertical");
        }

        [Test]
        public void ProgressionGateDoesNotReceivePermanentDividerTrees()
        {
            using var f=new Fixture();
            for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)f.Map.TryAddFloorCell(new Vector2Int(x,y),"gate");
            var edge=new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero,Vector2Int.right));
            Assert.That(f.Map.TrySetProgressionGateState(edge,false),Is.True);
            f.Build();
            var owners=(Dictionary<Renderer,DungeonSurfaceEdge[]>)typeof(DungeonFloorMapView).GetField("forestBoundaryOwners",Private).GetValue(f.View);
            Assert.That(owners.Any(p=>p.Value.Contains(edge)&&p.Key.name=="Forest divider tree"),Is.False);
            f.Map.TrySetProgressionGateState(edge,true);
            Assert.That(f.Map.CanMoveBetween(Vector2Int.zero,Vector2Int.right),Is.True);
        }

        static void Capture(Fixture f,string label)
        {
            Directory.CreateDirectory(Output);
            var cameraObject=new GameObject("divider render",typeof(Camera));
            var target=new RenderTexture(768,768,24);var pixels=new Texture2D(768,768,TextureFormat.RGBA32,false);
            var camera=cameraObject.GetComponent<Camera>();var previous=RenderTexture.active;
            try
            {
                foreach(var t in f.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                camera.transform.position=new Vector3(.5f,1,-10);camera.orthographic=true;camera.orthographicSize=5;
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,768,768),0,0);pixels.Apply();
                string stage=File.Exists(Path.Combine(Output,"after.flag"))?"after":"before";
                File.WriteAllBytes(Path.Combine(Output,stage+"-"+label+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;
                Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }

        sealed class Fixture:IDisposable
        {
            public readonly GameObject Root=new("Thin forest divider",typeof(Grid));
            public readonly DungeonFloorMap Map=new();
            public readonly DungeonFloorMapView View;
            public Fixture()
            {
                var data=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
                Assert.That(data,Is.Not.Null);
                var floor=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));floor.transform.SetParent(Root.transform,false);
                View=Root.AddComponent<DungeonFloorMapView>();
                void Set(string field,object value)=>typeof(DungeonFloorMapView).GetField(field,Private).SetValue(View,value);
                Set("floorTilemap",floor.GetComponent<Tilemap>());Set("floorTile",data.VegetationPresentation.GroundTile);
                Set("wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                View.ConfigureVegetationPresentation(data.VegetationPresentation);
            }
            public void Build()
            {
                foreach(var cell in Map.FloorCells)
                    foreach(var direction in Directions)if(!Map.IsWalkable(cell+direction))Map.TryAddWall(cell,cell+direction);
                View.Rebuild(Map);
            }
            public void Dispose()=>Object.DestroyImmediate(Root);
        }
    }
}
