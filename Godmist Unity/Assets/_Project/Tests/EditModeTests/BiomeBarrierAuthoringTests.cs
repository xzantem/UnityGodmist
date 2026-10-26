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
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class BiomeBarrierAuthoringTests
    {
        const string HandmadePath="Assets/_Project/ScriptableObjects/Dungeons/ProductionRooms/DRT_catacombs_4way_tomb.asset";
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
        static string Output=>Path.GetFullPath("../LocalHandoffs/authored-low-barriers-20261023");
        [Serializable] class Point {public int x,y;public Vector2Int Cell=>new(x,y);}
        [Serializable] class Probe {public Point from,to;public int minDetour;}
        [Serializable] class Spec {public string id,biome,lowWallPurpose;public Probe lowWallRouteProbe;}
        [Serializable] class Manifest {public Spec[] rooms;}
        static Manifest Plans=>JsonUtility.FromJson<Manifest>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Scripts/Editor/BiomeRoomProductionBlueprints.json").text);
        static DungeonDefinition Definition(string biome)=>AssetDatabase.LoadAssetAtPath<DungeonDefinition>(biome=="forest"
            ?Godmist.Editor.ForestExpeditionsSetup.RegularPath:"Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset");
        static T Read<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Flags).GetValue(owner);
        static Dictionary<Vector2Int,int> Distances(HashSet<Vector2Int> floor,HashSet<DungeonCellEdge> forbidden,Vector2Int start)
        {
            Assert.That(floor.Contains(start),Is.True,"Probe starts on free ground.");
            var found=new Dictionary<Vector2Int,int>{{start,0}};var queue=new Queue<Vector2Int>();queue.Enqueue(start);
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();
                foreach(var delta in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
                {var next=cell+delta;if(floor.Contains(next)&&!found.ContainsKey(next)&&(forbidden==null||!forbidden.Contains(new DungeonCellEdge(cell,next)))){found.Add(next,found[cell]+1);queue.Enqueue(next);}}
            }
            return found;
        }

        [Test]
        public void HandmadeTombIsReferencedOnceInBothProductionPoolsAndIsActuallySelected()
        {
            var room=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(HandmadePath);Assert.That(room,Is.Not.Null);
            var source=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>("Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_4wayTomb.asset");
            Assert.That(room.Id,Is.EqualTo(source.Id));Assert.That(room.SizeInCells,Is.EqualTo(source.SizeInCells));
            Assert.That(room.SemanticAnchors.Select(a=>JsonUtility.ToJson(a)),Is.EqualTo(source.SemanticAnchors.Select(a=>JsonUtility.ToJson(a))));
            Assert.That(room.StructuralAnchors.Select(a=>JsonUtility.ToJson(a)),Is.EqualTo(source.StructuralAnchors.Select(a=>JsonUtility.ToJson(a))));
            Assert.That(room.RoomFunction,Is.EqualTo("sarcophagus"));
            foreach(var path in new[]{"Assets/_Project/ScriptableObjects/Dungeons/DTC_Catacombs_Production.asset",
                "Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRTC_Catacombs_SkeletonSlayer.asset"})
            {
                var pool=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(path);
                Assert.That(pool.GenerationTemplates.Count(r=>r==room),Is.EqualTo(1));
            }
            var seen=false;
            foreach(var seed in new[]{1,9,17})
            {
                var dungeon=new DungeonGeneratorService().Generate(Definition("catacombs"),seed,10,DungeonLength.Normal);
                seen|=dungeon.Floors.SelectMany(f=>f.Rooms).Any(r=>r.TemplateId==room.Id);
            }
            Assert.That(seen,Is.True,"Registration must affect generated production layouts.");
        }

        [TestCase("forest")] [TestCase("catacombs")]
        public void CompositionsOfferRealShortcutsAndBypassesWithFurnitureInEveryRotation(string biome)
        {
            var pool=Definition(biome).RoomTemplateCatalog;
            foreach(var spec in Plans.rooms.Where(s=>s.biome==biome&&!string.IsNullOrEmpty(s.lowWallPurpose)))
            {
                var template=pool.GenerationTemplates.Single(r=>r.Id=="room."+biome+".production."+spec.id);
                Assert.That(spec.lowWallPurpose,Is.Not.Empty);
                foreach(var rotation in template.AllowedRotationDegrees)
                {
                    var floor=new DungeonFloorInstance(0);
                    floor.TryAddRoom(new DungeonRoomInstance("room",template.RoomType,template.Id,rotationDegrees:rotation));
                    var map=new DungeonFloorMapBuilder().Build(floor,pool);
                    var cells=map.FloorCells.ToHashSet();
                    Vector2Int Transform(Vector2Int c)=>DungeonRoomTransformUtility.TransformCell(c,template.SizeInCells,rotation);
                    foreach(var anchor in template.SemanticAnchors)
                        if(anchor.ObjectDefinition is ObstacleDungeonObjectDefinition prop&&prop.BlocksMovement)
                            foreach(var cell in prop.EnumerateOccupiedCells(Transform(anchor.LocalCell)))cells.Remove(cell);
                    var low=map.SurfaceWallEdges.Where(e=>map.GetWallType(e)==DungeonWallType.Low).Select(e=>e.CellEdge).ToHashSet();
                    Assert.That(low,Is.Not.Empty);
                    Assert.That(low.All(e=>cells.Contains(e.FirstCell)&&cells.Contains(e.SecondCell)),Is.True,template.Id+": do not bury a hedge/wall inside furniture.");
                    var start=Transform(spec.lowWallRouteProbe.from.Cell);var end=Transform(spec.lowWallRouteProbe.to.Cell);
                    var bypass=Distances(cells,low,start);var shortcut=Distances(cells,null,start);
                    Assert.That(bypass.Count,Is.EqualTo(cells.Count),template.Id+": low edges and furniture cannot isolate ground.");
                    Assert.That(bypass[end]-shortcut[end],Is.GreaterThanOrEqualTo(spec.lowWallRouteProbe.minDetour),template.Id+": the divider must offer a real crossing/going-around choice.");
                }
            }
        }

        [TestCase("forest")] [TestCase("catacombs")]
        [Explicit("Native composition render in the existing Editor, no user save.")]
        public void AuditPurposefulBarrierRooms(string biome)
        {
            Directory.CreateDirectory(Output);
            var definition=Definition(biome);var handmade=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(HandmadePath);
            var ids=Plans.rooms.Where(s=>s.biome==biome&&!string.IsNullOrEmpty(s.lowWallPurpose)).Select(s=>"room."+biome+".production."+s.id).ToHashSet();
            if(biome=="catacombs")ids.Add(handmade.Id);
            var seed=1;
            for(;seed<=12;seed++)
            {
                var probe=new DungeonGeneratorService().Generate(definition,seed,10,DungeonLength.Normal).Floors[0];
                if(probe.Rooms.Count(r=>ids.Contains(r.TemplateId))>=3
                    &&(biome!="catacombs"||probe.Rooms.Any(r=>r.TemplateId==handmade.Id)))break;
            }
            Assert.That(seed,Is.LessThanOrEqualTo(12));
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride="native-"+biome,SeedOverride=seed};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var floor=session.ActiveDungeon.Floors[0];var view=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");var camera=Read<Camera>(rig,"worldCamera");
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                var target=new RenderTexture(1200,900,24);var pixels=new Texture2D(1200,900,TextureFormat.RGBA32,false);
                var old=camera.targetTexture;var active=RenderTexture.active;var layers=new Dictionary<GameObject,int>();
                try
                {
                    foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                        if(!layers.ContainsKey(r.gameObject)){layers.Add(r.gameObject,r.gameObject.layer);r.gameObject.layer=30;}
                    camera.cullingMask=1<<30;camera.aspect=4f/3;camera.targetTexture=target;
                    var members=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController.ActiveMemberViews.ToArray();
                    var occupied=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive&&o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells()).ToHashSet();
                    foreach(var room in floor.Rooms.Where(r=>ids.Contains(r.TemplateId)).GroupBy(r=>r.TemplateId).Select(g=>g.First()))
                    {
                        var cells=floor.Map.FloorCells.Where(c=>floor.Map.TryGetRoomId(c,out var owner)&&owner==room.RoomId).ToArray();
                        var min=new Vector2Int(cells.Min(c=>c.x),cells.Min(c=>c.y));var max=new Vector2Int(cells.Max(c=>c.x),cells.Max(c=>c.y));
                        var center=(view.CellToWorldCenter(min)+view.CellToWorldCenter(max))*.5f;
                        camera.transform.position=new Vector3(center.x,center.y+.5f,-10);
                        camera.orthographicSize=Mathf.Max((max.y-min.y+1)*.5f+1.5f,((max.x-min.x+1)*.5f+1.5f)/camera.aspect);
                        var free=cells.Where(c=>!occupied.Contains(c)).ToArray();
                        for(var i=0;i<members.Length;i++)members[i].SetPosition(view.CellToWorldCenter(free.OrderBy(c=>((Vector2)view.CellToWorldCenter(c)-(center+new Vector2(i*.7f-1,-2))).sqrMagnitude).First()));
                        foreach(var member in members)member.GetType().GetMethod("LateUpdate",Flags).Invoke(member,null);
                        view.GetType().GetMethod("LateUpdate",Flags).Invoke(view,null);rig.GetType().GetMethod("RefreshLighting",Flags).Invoke(rig,new object[]{2f});
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();
                        File.WriteAllBytes(Path.Combine(Output,"native-"+room.TemplateId+".png"),pixels.EncodeToPNG());
                    }
                }
                finally{foreach(var pair in layers)if(pair.Key!=null)pair.Key.layer=pair.Value;camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);}
            };
            fixture.MeasureProductionFloorEntry("dungeon."+biome+".expedition",DungeonLength.Normal);
        }
    }
}
