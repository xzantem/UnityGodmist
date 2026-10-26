using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using Godmist.Data.Characters;
using Godmist.Views.Exploration;
using Godmist.Views.Dungeons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NUnit.Framework;
namespace Godmist.Tests.EditMode
{
    public class ForestEnemyNavigationTests
    {
        private static DungeonFloorMap Map()
        {
            var map=new DungeonFloorMap();
            for(var y=0;y<11;y++) for(var x=0;x<11;x++) map.TryAddFloorCell(new Vector2Int(x,y),"arena");
            for(var y=0;y<11;y++) for(var x=11;x<17;x++) map.TryAddFloorCell(new Vector2Int(x,y),"next");
            return map;
        }
        [TestCase(0,5)] [TestCase(1,5)] [TestCase(10,5)] [TestCase(11,5)] [TestCase(15,5)]
        public void EntCannotStandOnBoundaryOrLeaveItsRoom(int x,int y)
            => Assert.That(ForestEnemyNavigation.CanOccupy(Map(),new Vector2Int(x,y),"arena",true),Is.False);
        [Test]
        public void EntInteriorAndRegularMobInteriorRemainUsable()
        {
            Assert.That(ForestEnemyNavigation.CanOccupy(Map(),new Vector2Int(5,5),"arena",true),Is.True);
            Assert.That(ForestEnemyNavigation.CanOccupy(Map(),new Vector2Int(1,1),"arena",false),Is.True);
        }
        [Test]
        public void MovementExecutionRejectsEntCrossingRoomBoundary()
        {
            var map=Map();var cells=map.FloorCells.ToArray();
            var context=new DungeonCombatContext("dungeon.forest.boss_expedition",0,"ent","arena",new Vector2Int(5,5),new Vector2Int(5,5),cells,null,null,surfaceMap:map,isFloorWide:true);
            var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_InfectedEnt.asset");
            var ent=new BattleUnit{Character=new CharacterInstanceFactory().Create(definition,definition.ClassData,1)};
            var session=new CombatSession{DungeonContext=context};new DungeonBattleGridFactory().TryCreate(context,out var grid);session.Grid=grid;
            var from=DungeonBattleGridFactory.ToBattlePosition(context,new Vector2Int(10,5));
            var to=DungeonBattleGridFactory.ToBattlePosition(context,new Vector2Int(11,5));
            Assert.That(session.CanMoveBetween(ent,from,to),Is.False);
            Assert.That(session.CanOccupyEnvironment(ent,to),Is.False,"Forced displacement must use the same boundary.");
            ent.Position=from;
            var displace=typeof(SkillExecutionSystem).GetMethod("CanForcedDisplacementMove",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That((bool)displace.Invoke(null,new object[]{session,ent,to,1,0}),Is.False);
            ent.ForestEntState=new ForestEntCombatState{ArenaRoomId="arena"};
            Assert.That(session.CanOccupyEnvironment(ent,to),Is.False);
            ent.IsPlayerSide=true;Assert.That(session.CanMoveBetween(ent,from,to),Is.True);
        }
        [Test]
        public void ForestFixtureUsesItsOwnNativeGroundedSprite()
        {
            var sprite=Resources.Load<Sprite>("Godmist/ForestArt/Fixtures/forest-brazier-32ppu");
            Assert.That(sprite,Is.Not.Null);Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(32,56)));
            Assert.That(sprite.pivot.y,Is.EqualTo(4).Within(.01));
            var output=System.Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
            if(!string.IsNullOrEmpty(output)) ExportFixturePreview(sprite,output);
        }
        [Test]
        public void BrazierUsesForestArtMaskAndContactRegistration()
        {
            var root=new GameObject("Forest brazier binding");
            try
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var terrain=root.AddComponent<DungeonFloorMapView>();typeof(DungeonFloorMapView).GetField("boundMap",flags).SetValue(terrain,Map());
                var rig=root.AddComponent<Godmist.Views.Lighting.DungeonSilhouetteLighting>();
                var type=rig.GetType();type.GetField("terrain",flags).SetValue(rig,terrain);
                var sprite=Resources.Load<Sprite>("Godmist/ForestArt/Fixtures/forest-brazier-32ppu");
                type.GetField("forestStandSprite",flags).SetValue(rig,sprite);
                type.GetField("flameMaterial",flags).SetValue(rig,new Material(Shader.Find("Godmist/Dungeon/Pixel Torch")));
                type.GetMethod("AddForestFixture",flags).Invoke(rig,new object[]{Vector2.zero,new DungeonSurfacePosition(Vector2Int.zero),false,null,null});
                var fixture=root.GetComponentsInChildren<SpriteRenderer>().Single();
                Assert.That(fixture.sprite,Is.SameAs(sprite));Assert.That(fixture.transform.position,Is.EqualTo(Vector3.zero));
                var block=new MaterialPropertyBlock();fixture.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ForestFixture"),Is.EqualTo(1));Assert.That(block.GetFloat("_FlameBase"),Is.EqualTo(.68f));
                var contacts=(System.Collections.Generic.List<SpriteRenderer>)type.GetField("groundContactPresentations",flags).GetValue(rig);
                Assert.That(contacts,Does.Contain(fixture));
            }
            finally{Object.DestroyImmediate(root);}
        }
        private static void ExportFixturePreview(Sprite fixture,string output)
        {
            var root=new GameObject("Forest fixture scale preview");
            var camera=root.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.8f;
            camera.transform.position=new Vector3(0,.9f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.07f,.085f,.06f,1);camera.cullingMask=1<<30;
            var target=new RenderTexture(512,384,24);var pixels=new Texture2D(512,384,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                var hero=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
                for(var i=0;i<2;i++)
                {
                    var go=new GameObject(i==0?"Native forest brazier":"Jerzy scale reference");go.layer=30;go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3(i==0?-.65f:.65f,0,0);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=i==0?fixture:hero.VisualSprite;
                }
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,512,384),0,0);pixels.Apply();
                System.IO.Directory.CreateDirectory(output);System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"forest-brazier-scale.png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(root);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
        [Test]
        public void SpawnRepairsLegacyEntOutsideArenaBeforeFogIsBound()
        {
            var data=AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonDefinition>(Godmist.Editor.ForestPrototypeSetup.DefinitionPath);
            var dungeon=new DungeonGeneratorService().Generate(data,42);new DungeonEncounterPlacementSystem().Place(dungeon);var floor=dungeon.Floors[0];
            var encounter=floor.Encounters.First(e=>e.Enemies.Any(n=>ForestEnemyNavigation.IsEnt(n.CharacterDefinition?.Id)));
            var enemy=encounter.Enemies.First(n=>ForestEnemyNavigation.IsEnt(n.CharacterDefinition?.Id));
            var invalid=floor.Map.FloorCells.First(c=>floor.Map.TryGetRoomId(c,out var room)&&room!=encounter.RoomId);
            encounter.TrySetEnemyExplorationPosition(enemy.InstanceId,invalid);
            var key="enemy:"+encounter.InstanceId+":"+enemy.InstanceId;
            floor.TrySetActorPosition(key,new DungeonTraversalPosition(new DungeonSurfacePosition(invalid)));floor.SetActorOffset(key,new Vector2(4,4));
            var root=new GameObject("Forest spawn repair");
            try
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var terrain=root.AddComponent<DungeonFloorMapView>();typeof(DungeonFloorMapView).GetField("boundMap",flags).SetValue(terrain,floor.Map);
                terrain.ConfigureVegetationPresentation(data.VegetationPresentation);
                var spawner=root.AddComponent<DungeonEncounterSpawner>();
                typeof(DungeonEncounterSpawner).GetField("floorMapView",flags).SetValue(spawner,terrain);
                typeof(DungeonEncounterSpawner).GetField("enemyViewPrefab",flags).SetValue(spawner,AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"));
                typeof(DungeonEncounterSpawner).GetMethod("SpawnEnemyViews",flags).Invoke(spawner,new object[]{encounter,floor,1});
                Assert.That(terrain.FogFloor,Is.Null);
                Assert.That(floor.TryGetActorPosition(key,out var repaired),Is.True);floor.Map.TryGetTraversalCell(repaired,out var cell);
                Assert.That(ForestEnemyNavigation.CanOccupy(floor.Map,cell,encounter.RoomId,true),Is.True);
                Assert.That(floor.GetActorOffset(key),Is.EqualTo(Vector2.zero));
                Assert.That(encounter.StationaryEnemyHomes[enemy.InstanceId],Is.EqualTo(cell));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void CanopyReadabilityKeepsRealEnemyAtFeetDepthAndPreservesVisibilityWithoutCopy()
        {
            var root=new GameObject("Forest visibility test");
            var prefab=AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var actor=Object.Instantiate(prefab,root.transform);
            try
            {
                var map=Map();var terrain=root.AddComponent<DungeonFloorMapView>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(DungeonFloorMapView).GetField("boundMap",flags).SetValue(terrain,map);
                var data=AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonDefinition>(Godmist.Editor.ForestPrototypeSetup.DefinitionPath);
                terrain.ConfigureVegetationPresentation(data.VegetationPresentation);
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_InfectedEnt.asset");
                actor.Bind(new CharacterInstanceFactory().Create(definition,definition.ClassData,1));actor.SetEnemyInstanceId("ent");
                actor.ConfigureTerrain(map,terrain);actor.SetPosition(terrain.CellToWorldCenter(new Vector2Int(5,5)));
                var front=new GameObject("tree").AddComponent<SpriteRenderer>();front.transform.SetParent(root.transform);front.sprite=data.VegetationPresentation.BoundaryTree;
                front.transform.position=actor.Position-Vector2.up;
                terrain.GetStandingObjectDepthSortingOrder(actor.Position.y);
                terrain.GetStandingObjectDepthSortingOrder(front.transform.position.y);
                front.sortingOrder=terrain.GetWallMountedDepthSortingOrder(front.transform.position.y);
                terrain.RegisterForestCanopy(front);
                Assert.That(terrain.TryGetForestCanopyOrder(terrain.WorldToCell(actor.Position),actor.Position.y,out var canopyOrder),Is.True);
                var refresh=typeof(ExplorationCharacterView).GetMethod("RefreshWorldDepthSorting",flags);
                var count=actor.GetComponentsInChildren<SpriteRenderer>().Length;
                refresh.Invoke(actor,null);
                Assert.That(actor.GetComponent<SortingGroup>().sortingOrder,Is.EqualTo(terrain.GetActorDepthSortingOrder(actor.Position.y)));
                Assert.That(actor.GetComponent<SortingGroup>().sortingOrder,Is.LessThan(canopyOrder),"Foreground foliage reveals locally; the enemy must not be lifted above the tree.");
                Assert.That(actor.BodyRenderer.color.a,Is.EqualTo(1));
                actor.BodyRenderer.forceRenderingOff=true;refresh.Invoke(actor,null);
                Assert.That(actor.BodyRenderer.forceRenderingOff,Is.True);
                Assert.That(actor.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(count));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
