using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonFrameTimeTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Read<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Private).GetValue(owner);
        static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
        static void Invoke(object owner,string method)=>owner.GetType().GetMethod(method,Private).Invoke(owner,null);

        [TestCase(1)] [TestCase(-1)]
        public void ElevatedTopologyFlagUpdatesOnInsertionAndSurvivesSightSnapshot(int level)
        {
            var map=new DungeonFloorMap();
            Assert.That(map.HasElevatedSurfaces,Is.False);
            for(var x=0;x<100;x++)for(var y=0;y<100;y++)map.TryAddFloorCell(new Vector2Int(x,y),"ground");
            Assert.That(map.HasElevatedSurfaces,Is.False);
            Assert.That(map.CreateSightSnapshot().HasElevatedSurfaces,Is.False);
            Assert.That(map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero,level),""),Is.False);
            Assert.That(map.HasElevatedSurfaces,Is.False,"Rejected insertion must not change topology.");
            Assert.That(map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero,level),"raised"),Is.True);
            Assert.That(map.HasElevatedSurfaces,Is.True);
            var snapshot=map.CreateSightSnapshot();
            Assert.That(snapshot.HasElevatedSurfaces,Is.True);
            Assert.That(snapshot.IsWalkable(new DungeonSurfacePosition(Vector2Int.zero,level)),Is.True);
        }

        [Test]
        public void RepeatedFlatFloorTopologyQueriesHaveConstantCostAndAllocateNothing()
        {
            var map=new DungeonFloorMap();
            for(var x=0;x<100;x++)for(var y=0;y<100;y++)map.TryAddFloorCell(new Vector2Int(x,y),"ground");
            var start=GC.GetAllocatedBytesForCurrentThread();
            var elevated=false;
            var watch=Stopwatch.StartNew();
            for(var i=0;i<100000;i++) elevated|=map.HasElevatedSurfaces;
            watch.Stop();
            // Stopwatch is the only allocation measured here.
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-start,Is.LessThanOrEqualTo(64));
            Assert.That(elevated,Is.False);
            Assert.That(watch.Elapsed.TotalMilliseconds,Is.LessThan(20),"Flat maps must not rescan all cells per query.");
        }

        [TestCase("dungeon.catacombs.expedition")]
        [TestCase("dungeon.forest.expedition")]
        public void MeasureStandingProductionFloorTicks(string id)
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/dungeon-frame-pass-20261016"));
            Directory.CreateDirectory(output);
            var stage=File.ReadAllText(Path.Combine(output,"stage.txt")).Trim();
            var path=Path.Combine(output,stage+"-"+id+".json");
            var metrics=new List<Metric>();
            var probe=new DungeonEntryPerformanceTests { OutputOverride=output,StageOverride="frame-"+stage };
            probe.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var coordinator=Read<DungeonCombatCoordinator>(bootstrap,"combatCoordinator");
                var lighting=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var enemies=Read<DungeonEncounterSpawner>(bootstrap,"encounterSpawner").Triggers.SelectMany(t=>t.EnemyViews).ToArray();
                var actors=party.ActiveMemberViews.Concat(enemies).ToArray();
                var floor=session.ActiveDungeon.Floors[0];
                new DungeonFogSystem().Update(floor,party.ActiveMemberViews.Select(v=>v.DungeonPosition).ToArray());
                map.ApplyFog(floor);
                var camera=Read<Camera>(lighting,"worldCamera");
                var leader=party.ActiveLeaderTransform;
                camera.transform.position=new Vector3(leader.position.x,leader.position.y,-10);
                var renderTarget=new RenderTexture(1280,720,24);
                var oldTarget=camera.targetTexture;
                var oldActive=RenderTexture.active;
                var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);
                try
                {
                    camera.targetTexture=renderTarget;
                    void Measure(string name,Action action)
                    {
                        action();
                        var samples=new double[15];
                        for(var i=0;i<samples.Length;i++)
                        {
                            var timer=Stopwatch.StartNew(); action(); samples[i]=timer.Elapsed.TotalMilliseconds;
                        }
                        Array.Sort(samples); metrics.Add(new Metric{name=name,medianMs=samples[7],p95Ms=samples[14]});
                        File.WriteAllText(path,JsonUtility.ToJson(new Report{biome=id,stage=stage,actors=actors.Length,rooms=floor.Rooms.Count,metrics=metrics.ToArray()},true));
                        TestContext.WriteLine($"{name}: median={samples[7]:F3}ms p95={samples[14]:F3}ms");
                    }
                    Measure("detection",()=> {Set(coordinator,"detectionElapsed",.11f);Invoke(coordinator,"UpdateDetection");});
                    Measure("patrols",()=> {Set(coordinator,"patrolElapsed",.021f);Set(coordinator,"nextPatrolSense",0f);Invoke(coordinator,"UpdatePatrols");});
                    Measure("party fixed tick",()=>Invoke(party,"FixedUpdate"));
                    Measure("actor late ticks",()=> {foreach(var actor in actors)Invoke(actor,"LateUpdate");});
                    Measure("lighting refresh",()=> {Set(lighting,"lastLightingFrame",int.MinValue);Invoke(lighting,"LateUpdate");});
                    Measure("camera render and GPU sync",()=> {camera.Render();RenderTexture.active=renderTarget;pixel.ReadPixels(new Rect(0,0,1,1),0,0);});
                }
                finally
                {
                    camera.targetTexture=oldTarget; RenderTexture.active=oldActive;
                    UnityEngine.Object.DestroyImmediate(pixel); UnityEngine.Object.DestroyImmediate(renderTarget);
                }
            };
            probe.MeasureProductionFloorEntry(id,DungeonLength.Long);
            Assert.That(metrics.Count,Is.EqualTo(6));
            if(stage=="after")
            {
                Assert.That(metrics.Single(m=>m.name=="detection").medianMs,Is.LessThan(20),"Detection must not rescan the floor for each actor position.");
                Assert.That(metrics.Single(m=>m.name=="actor late ticks").medianMs,Is.LessThan(10),"Standing actor feedback must not scale with all map cells.");
            }
        }
        [Serializable] sealed class Metric {public string name;public double medianMs,p95Ms;}
        [Serializable] sealed class Report {public string biome,stage;public int actors,rooms;public Metric[] metrics;}
    }
}
