using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
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
    public sealed partial class ForestExplorationObjectTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static string Output=>Path.GetFullPath("../LocalHandoffs/forest-exploration-objects-20261020");
        static T Read<T>(object value,string name)=>(T)value.GetType().GetField(name,Flags).GetValue(value);
        static void Set(object value,string name,object field)
        {
            for(var type=value.GetType();type!=null;type=type.BaseType)
            {var member=type.GetField(name,Flags);if(member!=null){member.SetValue(value,field);return;}}
            throw new MissingFieldException(value.GetType().Name,name);
        }
        static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
        static DungeonDefinition Definition=>AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestExpeditionsSetup.RegularPath);
        static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        static DungeonInstance Generate(int seed,int level,DungeonLength length)
        {
            var dungeon=new DungeonGeneratorService().Generate(Definition,seed,level,length);
            new DungeonObjectPlacementSystem().Place(dungeon,Catalog.EquipmentPartDefinitions);return dungeon;
        }
        static HashSet<Vector2Int> Reach(DungeonFloorInstance floor,Vector2Int start)
        {
            var result=new HashSet<Vector2Int>();var pending=new Queue<Vector2Int>();pending.Enqueue(start);
            while(pending.Count>0){var cell=pending.Dequeue();if(!result.Add(cell))continue;
                foreach(var d in Directions)if(floor.Map.CanMoveBetween(cell,cell+d)&&!result.Contains(cell+d))pending.Enqueue(cell+d);}
            return result;
        }
        static void Activate(DungeonFloorInstance floor,DungeonObjectInstance source)
        {
            var condition=source switch {LeverDungeonObjectInstance l=>l.ProgressionConditionId,ConditionDungeonObjectInstance c=>c.ProgressionConditionId,_=>throw new InvalidOperationException()};
            if(source is LeverDungeonObjectInstance lever)Assert.That(lever.TryToggle(out _),Is.True);
            if(source is ConditionDungeonObjectInstance mark)Assert.That(mark.TrySatisfy(),Is.True);
            Assert.That(floor.TrySatisfyProgressionCondition(condition,source.InstanceId),Is.True);
        }
        [Test]
        public void ImportedBiomesHaveUnlockedChestsAndForestHasCompleteNativeContent()
        {
            foreach(var dungeon in Catalog.DungeonDefinitions.Where(d=>d!=null&&(d.Theme==DungeonTheme.Forest||d.Theme==DungeonTheme.Catacombs)))
                foreach(var chest in dungeon.ObjectDefinitions.OfType<ChestDungeonObjectDefinition>())
                {Assert.That(chest.LockType,Is.EqualTo(ChestLockType.Unlocked),dungeon.Id+":"+chest.Id);Assert.That(chest.RequiredKey,Is.Null);}
            Assert.That(Definition.ObjectDefinitions.OfType<SpikeTrapDungeonObjectDefinition>().Count(),Is.EqualTo(3));
            Assert.That(Definition.MinProgressionGateCount,Is.EqualTo(1));Assert.That(Definition.MaxProgressionGateCount,Is.EqualTo(2));
            var branches=Definition.RoomTemplateCatalog.Templates.Where(r=>r.CanBeRequiredProgressionBranch).ToArray();
            Assert.That(branches.Length,Is.EqualTo(2));
            Assert.That(branches.Any(r=>r.SemanticAnchors.Any(a=>a.ObjectDefinition is LeverDungeonObjectDefinition&&a.IsRequired)),Is.True);
            Assert.That(branches.Any(r=>r.SemanticAnchors.Any(a=>a.ObjectDefinition is ConditionDungeonObjectDefinition&&a.IsRequired)),Is.True);
            foreach(var name in new[]{"snare","poison","roots","lever","condition","barrier"})
            {
                var path=ForestExplorationObjectsSetup.ArtRoot+name+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Assert.That(sprite,Is.Not.Null,path);
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            }
        }
        [TestCase(42,1,DungeonLength.Short)] [TestCase(219,10,DungeonLength.Short)]
        [TestCase(587,48,DungeonLength.Normal)] [TestCase(171026,48,DungeonLength.Long)]
        public void GeneratedGatesHaveReachableControlsAndBlockTheEntirePassageUntilActivated(int seed,int level,DungeonLength length)
        {
            var floor=Generate(seed,level,length).Floors.Single();
            var start=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Start);
            var origin=floor.Map.FloorCells.First(c=>floor.Map.TryGetRoomId(c,out var id)&&id==start.RoomId);
            var gates=floor.Connections.Where(c=>c.HasProgressionGate).ToArray();Assert.That(gates.Length,Is.InRange(1,2));
            Assert.That(floor.Objects.OfType<ChestDungeonObjectInstance>().All(c=>c.IsUnlocked),Is.True);
            var remaining=new HashSet<DungeonRoomConnection>(gates);
            while(remaining.Count>0)
            {
                var reachable=Reach(floor,origin);
                var final=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final);
                Assert.That(reachable.Any(c=>floor.Map.TryGetRoomId(c,out var room)&&room==final.RoomId),Is.False,"Final room must remain behind the last gate.");
                var gate=remaining.FirstOrDefault(g=>g.RequiredProgressionSourceIds.All(id=>floor.TryGetObject(id,out var source)&&reachable.Contains(source.Position)));
                Assert.That(gate,Is.Not.Null,"A required control is behind its own gate; seed="+seed);
                Assert.That(gate.RequiredProgressionSourceIds,Is.Not.Empty);
                var normal=gate.SecondDoorCell-gate.FirstDoorCell;var lateral=normal.x!=0?Vector2Int.up:Vector2Int.right;
                for(var i=0;i<gate.DoorwayWidth;i++)Assert.That(floor.Map.CanMoveBetween(gate.FirstDoorCell+lateral*i,gate.SecondDoorCell+lateral*i),Is.False);
                var notifications=0;floor.Map.ProgressionGateStateChanged+=Changed;
                foreach(var id in gate.RequiredProgressionSourceIds){Assert.That(floor.TryGetObject(id,out var source),Is.True);Activate(floor,source);}
                floor.Map.ProgressionGateStateChanged-=Changed;
                Assert.That(notifications,Is.EqualTo(1),"Unlocking a wide passage must not rebuild the map for each cell.");
                Assert.That(gate.IsProgressionGateUnlocked,Is.True);
                for(var i=0;i<gate.DoorwayWidth;i++)Assert.That(floor.Map.CanMoveBetween(gate.FirstDoorCell+lateral*i,gate.SecondDoorCell+lateral*i),Is.True);
                remaining.Remove(gate);
                void Changed()=>notifications++;
            }
            Assert.That(Reach(floor,origin).Count,Is.EqualTo(floor.Map.FloorCells.Count));
        }
        [Test]
        public void FreshExpeditionsActuallyUseBothControlsAndAllThreeHazards()
        {
            var controls=new HashSet<string>();var hazards=new HashSet<string>();var trace=new List<string>();
            for(var seed=0;seed<12&&(controls.Count<2||hazards.Count<3);seed++)
            {
                var floor=Generate(seed,10,DungeonLength.Short).Floors.Single();
                var ids=floor.Objects.Select(o=>o.Definition.Id).Distinct().ToArray();
                foreach(var id in ids)
                {
                    if(id=="object.forest.lever"||id=="object.forest.condition")controls.Add(id);
                    if(id.StartsWith("object.forest.trap."))hazards.Add(id);
                }
                trace.Add(seed+" "+string.Join(",",ids.Where(id=>id.Contains("trap.")||id=="object.forest.lever"||id=="object.forest.condition")));
            }
            Directory.CreateDirectory(Output);File.WriteAllLines(Path.Combine(Output,"content-coverage-seeds.txt"),trace);
            Assert.That(controls.Count,Is.EqualTo(2));Assert.That(hazards.Count,Is.EqualTo(3));
        }
        [Test]
        public void BossExpeditionsRetainTheirBossWhileReceivingForestProgression()
        {
            var bosses=Catalog.DungeonDefinitions.Where(d=>d!=null&&d.Theme==DungeonTheme.Forest&&d.BossEncounterDefinition!=null&&d.IsAvailableAsOffer).ToArray();
            Assert.That(bosses.Length,Is.GreaterThanOrEqualTo(2));
            foreach(var definition in bosses)
            {
                var boss=definition.BossEncounterDefinition;
                var dungeon=new DungeonGeneratorService().Generate(definition,219,10,DungeonLength.Short);
                new DungeonObjectPlacementSystem().Place(dungeon,Catalog.EquipmentPartDefinitions);
                Assert.That(dungeon.Floors.Single().Connections.Any(c=>c.HasProgressionGate),Is.True,definition.Id);
                Assert.That(definition.BossEncounterDefinition,Is.SameAs(boss));
            }
        }
        [Test]
        public void GateBatchRejectsInvalidCellsWithoutPartiallyOpeningOrNotifying()
        {
            var map=new DungeonFloorMap();map.TryAddFloorCell(Vector2Int.zero,"room");map.TryAddFloorCell(Vector2Int.right,"room");
            var valid=new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero,Vector2Int.right));map.TrySetProgressionGateState(valid,false);
            var notifications=0;map.ProgressionGateStateChanged+=()=>notifications++;
            var invalid=new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.up,Vector2Int.up+Vector2Int.right));
            Assert.That(map.TrySetProgressionGateState(new[]{valid,invalid},true),Is.False);
            Assert.That(map.CanMoveBetween(Vector2Int.zero,Vector2Int.right),Is.False);Assert.That(notifications,Is.EqualTo(0));
        }
        [Test]
        public void ForestConditionUsesItsNativeViewAndChangesStateAfterCleansing()
        {
            var root=new GameObject("Forest condition fixture");
            try
            {
                var definition=Definition.ObjectDefinitions.OfType<ConditionDungeonObjectDefinition>().Single();
                var resolver=typeof(DungeonObjectSpawner).Assembly.GetType("Godmist.Views.Dungeons.DungeonForestObjectVisuals");
                var method=resolver.GetMethod("CreateConditionView",BindingFlags.Public|BindingFlags.Static);
                var view=(DungeonConditionView)method.Invoke(null,new object[]{root.transform,"Forest condition",definition});
                var instance=new ConditionDungeonObjectInstance("test.condition",definition,Vector2Int.zero);
                view.Initialize(null,null,instance,Vector3.zero,null);
                var renderer=view.GetComponent<SpriteRenderer>();Assert.That(renderer.sprite.name,Is.EqualTo("condition"));
                var before=renderer.color;Assert.That(instance.TrySatisfy(),Is.True);Call(view,"RefreshVisual");
                Assert.That(renderer.color,Is.Not.EqualTo(before));Assert.That(renderer.color.g,Is.GreaterThan(renderer.color.r));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void TrapPresentationScaleDoesNotAccumulateOrAlterPhysicalRoot()
        {
            var root=new GameObject("Scaled trap prefab",typeof(BoxCollider2D));
            try
            {
                root.transform.localScale=Vector3.one*.7f;var box=root.GetComponent<BoxCollider2D>();var before=box.size;
                var view=root.AddComponent<DungeonSpikeTrapView>();var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ForestExplorationObjectsSetup.ArtRoot+"snare.png");
                view.ConfigureRuntimeSprites(sprite,nativeScale:.55f);view.ConfigureRuntimeSprites(sprite,nativeScale:.55f);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Single().transform.lossyScale.x,Is.EqualTo(.55f).Within(.001f));
                Assert.That(box.size,Is.EqualTo(before));Assert.That(root.transform.localScale.x,Is.EqualTo(.7f));
                view.ConfigureRuntimeSprites(sprite);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Single().transform.lossyScale.x,Is.EqualTo(1).Within(.001f),"Default Catacombs presentation must remain native scale.");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void HistoricalLockedFlagDoesNotRelockAnUnlockedDefinitionOrRerollLoot()
        {
            var definition=ScriptableObject.CreateInstance<ChestDungeonObjectDefinition>();
            try
            {
                Set(definition,"<Id>k__BackingField","object.test.open");
                var floor=new DungeonFloorInstance(0);var chest=new ChestDungeonObjectInstance("old.chest",definition,Vector2Int.zero,null,123);
                Assert.That(floor.TryAddObject(chest),Is.True);
                Assert.That(floor.TryRestoreObjectState(chest.InstanceId,true,false,false,isChest:true,isUnlocked:false),Is.True);
                Assert.That(chest.IsUnlocked,Is.True);Assert.That(chest.Gold,Is.EqualTo(123));
            }
            finally{Object.DestroyImmediate(definition);}
        }
        [Test]
        public void ExistingUngatedForestLayoutDoesNotReceiveNewGatesOnRestore()
        {
            var historical=Object.Instantiate(Definition);
            try
            {
                Set(historical,"<MinProgressionGateCount>k__BackingField",0);
                Set(historical,"<MaxProgressionGateCount>k__BackingField",0);
                var dungeon=new DungeonGeneratorService().Generate(historical,42,10,DungeonLength.Short);
                new DungeonObjectPlacementSystem().Place(dungeon,Catalog.EquipmentPartDefinitions);
                var floor=dungeon.Floors.Single();
                Assert.That(floor.Connections.Any(c=>c.HasProgressionGate),Is.False);
                var cells=floor.Map.FloorCells.ToHashSet();
                var ids=floor.Objects.Select(o=>o.InstanceId).ToArray();
                var session=new GameSession(GameModeState.Exploration);session.TrySetActiveDungeon(dungeon);
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Historical ungated Forest");
                var json=new GameSaveJsonSerializer();
                Assert.That(json.TryDeserialize(json.Serialize(snapshot),out var data),Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(data,Catalog,out var restored),Is.True);
                var loaded=restored.ActiveDungeon.Floors.Single();
                Assert.That(loaded.Connections.Any(c=>c.HasProgressionGate),Is.False);
                Assert.That(loaded.Map.FloorCells.ToHashSet().SetEquals(cells),Is.True);
                Assert.That(loaded.Objects.Select(o=>o.InstanceId),Is.EquivalentTo(ids));
            }
            finally{Object.DestroyImmediate(historical);}
        }
        [Test]
        public void ForestProgressionTrapsAndLootRoundTripWithoutReopeningOrRerolling()
        {
            var dungeon=Generate(42,10,DungeonLength.Short);var floor=dungeon.Floors.Single();
            var gates=floor.Connections.Where(c=>c.HasProgressionGate).ToArray();
            foreach(var gate in gates)foreach(var id in gate.RequiredProgressionSourceIds){floor.TryGetObject(id,out var source);Activate(floor,source);}
            var trap=floor.Objects.OfType<SpikeTrapDungeonObjectInstance>().FirstOrDefault();Assert.That(trap,Is.Not.Null);
            Assert.That(trap.TryTrigger(),Is.True);
            var chest=floor.Objects.OfType<ChestDungeonObjectInstance>().First();chest.TryClaimGold(out _);
            var state=floor.Objects.ToDictionary(o=>o.InstanceId,o=>o is ChestDungeonObjectInstance c?c.Gold+":"+string.Join(",",c.Items.Select(i=>i.InstanceId)):o.Definition.Id);
            var session=new GameSession(GameModeState.Exploration);session.TrySetActiveDungeon(dungeon);var json=new GameSaveJsonSerializer();
            for(var pass=0;pass<2;pass++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Forest exploration content");
                Assert.That(json.TryDeserialize(json.Serialize(snapshot),out var data),Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(data,Catalog,out session),Is.True);
                var restored=session.ActiveDungeon.Floors.Single();
                Assert.That(restored.Connections.Where(c=>c.HasProgressionGate).All(c=>c.IsProgressionGateUnlocked),Is.True);
                Assert.That(restored.Objects.OfType<SpikeTrapDungeonObjectInstance>().Single(t=>t.InstanceId==trap.InstanceId).IsTriggered,Is.True);
                foreach(var obj in restored.Objects)Assert.That(obj is ChestDungeonObjectInstance c?c.Gold+":"+string.Join(",",c.Items.Select(i=>i.InstanceId)):obj.Definition.Id,Is.EqualTo(state[obj.InstanceId]));
            }
        }
        [TestCase(2,false)] [TestCase(3,false)] [TestCase(2,true)] [TestCase(3,true)]
        public void WoodenBarrierSpansPassageAndKeepsFogMemoryWithoutBecomingACanopy(int width,bool vertical)
        {
            var root=new GameObject("Forest gate fixture",typeof(Grid));root.layer=30;Tile tile=null;
            try
            {
                var child=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();Set(view,"floorTilemap",child.GetComponent<Tilemap>());
                tile=ScriptableObject.CreateInstance<Tile>();Set(view,"floorTile",tile);
                Set(view,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>(ForestPrototypeSetup.Root+"DVP_Forest_Prototype.asset"));
                var map=new DungeonFloorMap();for(var y=0;y<7;y++)for(var x=0;x<7;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                foreach(var c in map.FloorCells)foreach(var d in Directions)if(!map.IsWalkable(c+d))map.TryAddWall(c,c+d);
                var edges=Enumerable.Range(0,width).Select(i=>new DungeonSurfaceEdge(new DungeonCellEdge(vertical?new Vector2Int(2,2+i):new Vector2Int(2+i,2),vertical?new Vector2Int(3,2+i):new Vector2Int(2+i,3)))).ToArray();
                map.TrySetProgressionGateState(edges,false);view.Rebuild(map);
                var floor=new DungeonFloorInstance(0);Assert.That(floor.TrySetMap(map),Is.True);
                floor.Fog.SetVisible(map.FloorCells.Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
                floor.Fog.ClosedGates.UnionWith(edges);view.ApplyFog(floor);
                SpriteRenderer[] Barriers()=>view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null&&r.sprite.name=="barrier").ToArray();
                var barrier=Barriers().Single();Assert.That(barrier.forceRenderingOff,Is.False);Assert.That(barrier.size.x,Is.EqualTo(width));
                Assert.That(view.IsForestCanopyRenderer(barrier),Is.False,"Progression barriers must never participate in canopy fade.");
                Assert.That(barrier.sprite.border.x,Is.EqualTo(16));
                map.TrySetProgressionGateState(edges,true);view.ApplyFog(floor);
                Assert.That(Barriers().Count(r=>!r.forceRenderingOff),Is.EqualTo(1),"Previously seen closed gate remains as a nonphysical memory until observed again.");
                Assert.That(edges.All(e=>map.CanMoveBetween(e.FirstCell,e.SecondCell)),Is.True);
                floor.Fog.ClosedGates.Clear();floor.Fog.SetVisible(floor.Fog.Visible.ToArray());view.ApplyFog(floor);
                Assert.That(Barriers().Count(r=>!r.forceRenderingOff),Is.EqualTo(0));
            }
            finally{Object.DestroyImmediate(root);if(tile!=null)Object.DestroyImmediate(tile);}
        }
        [TestCase(171026)] [TestCase(219)] [TestCase(2)]
        [Explicit("Production forest preview scene only; no user session is saved.")]
        public void CaptureProductionExplorationObjects(int seed)
        {
            Directory.CreateDirectory(Output);
            var images=Path.Combine(Output,"production-"+seed);Directory.CreateDirectory(images);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride="objects-"+seed,SeedOverride=seed};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var view=Read<DungeonFloorMapView>(bootstrap,"floorMapView");var floor=session.ActiveDungeon.Floors.Single();
                var party=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;var actors=party.ActiveMemberViews.ToArray();
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");var camera=Read<Camera>(rig,"worldCamera");
                foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);var old=camera.targetTexture;var active=RenderTexture.active;
                try
                {
                    camera.cullingMask=1<<30;camera.aspect=16f/9;camera.orthographicSize=3.5f;camera.targetTexture=target;
                    var samples=floor.Objects.Where(o=>o.Definition.Id.StartsWith("object.forest.trap.")||o is LeverDungeonObjectInstance||o is ConditionDungeonObjectInstance).GroupBy(o=>o.Definition.Id).Select(g=>(Name:g.Key,Cell:g.First().Position)).ToList();
                    foreach(var gate in floor.Connections.Where(c=>c.HasProgressionGate))samples.Add(("barrier-"+gate.ProgressionGateId,gate.FirstDoorCell));
                    foreach(var sample in samples)
                    {
                        var center=view.CellToWorldCenter(sample.Cell);camera.transform.position=new Vector3(center.x,center.y+.5f,-10);
                        for(var i=0;i<actors.Length;i++)actors[i].SetPosition((Vector2)center+new Vector2(-1.5f+i*.7f,-1));
                        foreach(var actor in actors)Call(actor,"LateUpdate");Call(rig,"RefreshLighting",1f);Call(rig,"RefreshForestPartyReveal");Call(rig,"AdvanceForestCanopyFade",.5f);
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(images,"production-"+sample.Name.Replace('.','-')+".png"),image.EncodeToPNG());
                    }
                    File.WriteAllLines(Path.Combine(images,"production-objects.txt"),floor.Objects.Select(o=>o.Definition.Id+" "+o.Position));
                }
                finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Normal);
        }
    }
}
