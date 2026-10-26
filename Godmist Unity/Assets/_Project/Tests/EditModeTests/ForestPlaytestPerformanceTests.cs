using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestPlaytestPerformanceTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test] public void MeasureBoundedLightingPresentationWorkload()
        {
            var root = new GameObject("Forest lighting workload"); var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.one*.5f, 1);
            try
            {
                var floor = new GameObject("floor",typeof(Tilemap),typeof(TilemapRenderer)); floor.transform.SetParent(root.transform);
                var terrain = root.AddComponent<DungeonFloorMapView>(); var light = root.AddComponent<DungeonSilhouetteLighting>();
                void Set(string name, object value) => typeof(DungeonSilhouetteLighting).GetField(name,Private).SetValue(light,value);
                Set("terrain",terrain); Set("floorMaterial",material); Set("floorReceiver",floor.GetComponent<TilemapRenderer>());
                Set("cameraGroundBounds",new Rect(-5,-5,10,10));
                var apply = (Action<SpriteRenderer,bool,bool>)Delegate.CreateDelegate(typeof(Action<SpriteRenderer,bool,bool>),light,
                    typeof(DungeonSilhouetteLighting).GetMethod("ApplyGroundLighting",Private));
                var renderers = new SpriteRenderer[4096];
                for (var i=0;i<renderers.Length;i++)
                {
                    var go = new GameObject("prop",typeof(SpriteRenderer));go.transform.SetParent(root.transform,false);
                    go.transform.position = i<64 ? new Vector3(i%8-4,i/8-4) : new Vector3(100+i%64,100+i/64);
                    renderers[i]=go.GetComponent<SpriteRenderer>();renderers[i].sprite=sprite;
                }
                foreach(var r in renderers)apply(r,true,false); // warm shader/property caches
                var samples=new double[15];
                for(var pass=0;pass<samples.Length;pass++)
                {
                    var watch=Stopwatch.StartNew();foreach(var r in renderers)apply(r,true,false);watch.Stop();samples[pass]=watch.Elapsed.TotalMilliseconds;
                }
                Array.Sort(samples);
                var materials=(IDictionary)typeof(DungeonSilhouetteLighting).GetField("presentationMaterials",Private).GetValue(light);
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-playtest-pass-20261015"));Directory.CreateDirectory(output);
                var stage=File.ReadAllText(Path.Combine(output,"benchmark-stage.txt")).Trim();
                File.WriteAllText(Path.Combine(output,"lighting-benchmark-"+stage+".json"),JsonUtility.ToJson(new Metrics {
                    props=renderers.Length,visibleProps=64,registeredProps=materials.Count,medianMilliseconds=samples[7],p95Milliseconds=samples[14],
                    workload="CPU microbenchmark of existing ApplyGroundLighting;4096 props,64 in prepared camera bounds;15 warmed ticks. Not full-game FPS."
                },true));
                Assert.That(materials.Count,Is.GreaterThanOrEqualTo(64));
                if(stage=="after")Assert.That(materials.Count,Is.EqualTo(64),"offscreen props must not allocate/upload lighting property blocks");
            }
            finally {Object.DestroyImmediate(root);if(material!=null)Object.DestroyImmediate(material);Object.DestroyImmediate(sprite);}
        }
        [Serializable] sealed class Metrics { public int props,visibleProps,registeredProps;public double medianMilliseconds,p95Milliseconds;public string workload; }
    }
}
