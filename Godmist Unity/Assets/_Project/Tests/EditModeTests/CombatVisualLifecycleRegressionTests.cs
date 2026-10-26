using Godmist.Core.Enums;
using Godmist.Views.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.Combat;
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
    public class CombatVisualLifecycleRegressionTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Forest="Assets/_Project/ScriptableObjects/Dungeons/Forest/";
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
        static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
        [TestCase(false)] [TestCase(true)]
        public void PoseLookupRecreatesSpritesDestroyedByPreviousPlaySession(bool destroyEffect)
        {
            bool Resolve(out CombatPose pose)=>CombatPoseResolver.TryResolveSkill("enemy.forest.elf_poacher",
                "skill.enemy.forest.poacher_bear_trap",Vector2.down,AttackType.Ranged,false,out pose);
            Assert.That(Resolve(out var before),Is.True);
            Assert.That(before.Effects.Length,Is.GreaterThan(0));
            var destroyed=destroyEffect?before.Effects[0]:before.Actor;
            Object.DestroyImmediate(destroyed);
            Assert.That(Resolve(out var after),Is.True);
            Assert.That(after.Actor==null,Is.False,"An existing cache entry is not necessarily a live Unity sprite.");
            foreach(var effect in after.Effects)Assert.That(effect==null,Is.False);
            var recreated=destroyEffect?after.Effects[0]:after.Actor;
            Assert.That(recreated,Is.Not.SameAs(destroyed));
            Assert.That(recreated.pixelsPerUnit,Is.EqualTo(32));
        }

        [TestCase("CD_Forest_PoisonGrowth.asset")]
        [TestCase("CD_Forest_HealingGrowth.asset")]
        [TestCase("CD_Forest_ElfDruid.asset")]
        public void BattlePrefabPreservesAuthoredActorScaleForGrowthAndDruid(string asset)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab");
            var root=Object.Instantiate(prefab);
            try
            {
                var view=root.GetComponent<CharacterView>();
                view.Bind(new BattleUnit{Character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Forest+asset))});
                Assert.That(view.BodyRenderer.sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(view.BodyRenderer.transform.lossyScale,Is.EqualTo(new Vector3(.7f,.7f,1f)));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test] public void ExecutingPoacherTrapKeepsBorrowedAttackPoseThroughAnimationAndPresentationRefresh()
        {
            var root=new GameObject("Production borrowed skill pose");
            var tile=ScriptableObject.CreateInstance<Tile>();
            CombatExecutionDriver driver=null;
            try
            {
                var host=root.AddComponent<CombatSandboxBootstrap>();host.enabled=false;
                var spawner=root.AddComponent<CombatUnitViewSpawner>();spawner.enabled=false;
                Set(spawner,"sandboxBootstrap",host);Set(spawner,"unitsRoot",root.transform);Set(spawner,"summonedObjectsRoot",root.transform);
                var mapView=root.AddComponent<DungeonFloorMapView>();root.AddComponent<Grid>();
                var floorObject=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));floorObject.transform.SetParent(root.transform);
                Set(mapView,"floorTilemap",floorObject.GetComponent<Tilemap>());Set(mapView,"floorTile",tile);
                Set(mapView,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                var map=new DungeonFloorMap();for(var x=0;x<8;x++)for(var y=0;y<8;y++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                mapView.Rebuild(map);spawner.ConfigureDungeon(new GameSession(),mapView);
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Forest+"CD_Forest_ElfPoacher.asset");
                var caster=new BattleUnit{Character=new CharacterInstanceFactory().Create(definition),IsPlayerSide=true,Position=new GridPosition(1,1),CurrentActionPoints=3000,MaxActionPoints=3000};
                // This is a presentation test, not a zero-momentum turn-start scenario.
                caster.Character.ResourcePool.CurrentValue=100;
                var enemy=new BattleUnit{Character=new CharacterInstanceFactory().Create(definition),Position=new GridPosition(5,5)};
                var session=new CombatSession{IsSeamless=true,IsDungeonWide=true,Grid=new BattleGrid(8,8),ActiveUnit=caster,
                    DungeonContext=new DungeonCombatContext("forest",0,"combat","room",Vector2Int.zero,Vector2Int.one,map.FloorCells,null,null,surfaceMap:map)};
                session.Units.AddRange(new[]{caster,enemy});session.ActiveParticipant=new CombatTurnParticipant(caster);session.TurnParticipants.Add(session.ActiveParticipant);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"),root.transform);
                view.Bind(caster.Character);spawner.Borrow(session,caster,view);
                Invoke(view,"Update");Invoke(view,"LateUpdate");
                driver=new CombatExecutionDriver(host,spawner,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                driver.Initialize(session);
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>(Forest+"SD_Forest_Poacher_bear_trap.asset");
                Assert.That(driver.TryExecuteSkill(skill,new Dictionary<int,GridPosition?>{[0]=caster.Position},null,new GridPosition(2,1)),Is.True);
                Assert.That(view.HasCombatPose,Is.True);Assert.That(driver.IsSkillPresentationInProgress,Is.True);
                var attack=view.BodyRenderer.sprite;
                Assert.That(attack.name,Does.Contain("trap"));
                Invoke(view,"Update");Invoke(view,"LateUpdate");Invoke(spawner,"SynchronizeDungeon",0f);
                Assert.That(view.BodyRenderer.sprite,Is.SameAs(attack),"Idle and borrowed movement refresh must not overwrite the attack freeze frame.");
                Assert.That(view.HasCombatPose,Is.True);
            }
            finally{driver?.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(tile);}
        }

        [Test] public void SpawnedTrapAndGrowthRegisterSeparateLightingPathsAndCleanUp()
        {
            var root=new GameObject("Summon presentation regression");root.SetActive(false);
            var otherMapObject=new GameObject("Replacement map");
            try
            {
                var map=root.AddComponent<DungeonFloorMapView>();
                var spawner=root.AddComponent<CombatUnitViewSpawner>();
                Set(spawner,"unitsRoot",root.transform);Set(spawner,"summonedObjectsRoot",root.transform);
                Set(spawner,"battleUnitPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab").GetComponent<CharacterView>());
                spawner.ConfigureDungeon(new GameSession(),map);
                var growth=new BattleUnit{Character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Forest+"CD_Forest_PoisonGrowth.asset"))};
                var trap=new SummonedCombatMapObject("trap",new GridPosition(1,0),"poacher","skill.enemy.forest.poacher_bear_trap",0,
                    AssetDatabase.LoadAssetAtPath<SummonedObjectDefinition>(Forest+"SOD_Forest_BearTrap.asset"));
                var session=new CombatSession();session.Units.Add(growth);session.SummonedObjects.Add(trap);
                Invoke(spawner,"EnsureViews",session);Invoke(spawner,"EnsureSummonedObjectViews",session);Invoke(spawner,"SynchronizeSummonedObjectViews",session);
                var actors=(Dictionary<BattleUnit,CharacterView>)typeof(CombatUnitViewSpawner).GetField("views",Hidden).GetValue(spawner);
                var props=(Dictionary<SummonedCombatMapObject,SpriteRenderer>)typeof(CombatUnitViewSpawner).GetField("summonedObjectViews",Hidden).GetValue(spawner);
                var body=actors[growth].BodyRenderer;var trapRenderer=props[trap];
                Assert.That(map.ActorLightingPresentationRenderers,Does.Contain(body));
                Assert.That(map.IsGroundLightingPresentation(body),Is.False,"Upright growth must use actor lighting, not floor projection.");
                Assert.That(map.IsGroundLightingPresentation(trapRenderer),Is.True);
                Assert.That(trapRenderer.sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(trapRenderer.transform.lossyScale,Is.EqualTo(Vector3.one));
                var replacement=otherMapObject.AddComponent<DungeonFloorMapView>();
                spawner.ConfigureDungeon(new GameSession(),replacement);
                Assert.That(map.ActorLightingPresentationRenderers,Is.Empty);Assert.That(map.IsGroundLightingPresentation(trapRenderer),Is.False);
                Assert.That(replacement.ActorLightingPresentationRenderers,Does.Contain(body));Assert.That(replacement.IsGroundLightingPresentation(trapRenderer),Is.True);
                session.Units.Clear();session.SummonedObjects.Clear();
                Invoke(spawner,"RemoveMissingViews",session);Invoke(spawner,"RemoveMissingSummonedObjectViews",session);
                Assert.That(replacement.ActorLightingPresentationRenderers,Is.Empty);
                Assert.That(replacement.LightingPresentationRenderers,Is.Empty);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(otherMapObject);}
        }

        [Test] public void RegisteredSummonsReceiveTheRealLightingMaterialsAndActorProperties()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Requires native render textures.");
            var root=new GameObject("Summon lighting pass");
            var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.zero,32);
            try
            {
                var map=root.AddComponent<DungeonFloorMapView>();
                var party=root.AddComponent<ExplorationPartyController>();
                var lighting=root.AddComponent<DungeonSilhouetteLighting>();
                var cameraObject=new GameObject("Lighting camera",typeof(Camera));cameraObject.transform.SetParent(root.transform);
                var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
                var gridObject=new GameObject("Lighting grid",typeof(Grid));gridObject.transform.SetParent(root.transform);
                var floorObject=new GameObject("Lighting floor",typeof(Tilemap),typeof(TilemapRenderer));floorObject.transform.SetParent(gridObject.transform);
                Set(lighting,"worldCamera",camera);Set(lighting,"floorReceiver",floorObject.GetComponent<TilemapRenderer>());
                foreach(var item in new[]{("maskShader","Godmist/Dungeon/Animated Silhouette Volume Mask"),("floorShader","Godmist/Dungeon/Silhouette Floor"),
                    ("bodyShader","Godmist/Dungeon/Torch Body"),("torchShader","Godmist/Dungeon/Pixel Torch"),("wallShader","Godmist/Dungeon/Torch Wall")})
                    Set(lighting,item.Item1,Shader.Find(item.Item2));
                Set(lighting,"torchSprite",sprite);lighting.Initialize(party,null,null,map);
                SpriteRenderer Renderer(string name)
                {var obj=new GameObject(name,typeof(SpriteRenderer));obj.transform.SetParent(root.transform);var r=obj.GetComponent<SpriteRenderer>();r.sprite=sprite;return r;}
                var trap=Renderer("ground trap");var growth=Renderer("upright growth");
                trap.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Godmist/ForestArt/Summons/bear-trap-world-64-v1.png");
                growth.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Godmist/ForestArt/Summons/poisonous-growth-world-64-v1.png");
                trap.transform.position=new Vector3(-1.2f,-1,0);growth.transform.position=new Vector3(1.2f,-1,0);
                map.RegisterGroundLightingPresentation(trap);map.RegisterActorLightingPresentation(growth);
                Invoke(lighting,"LateUpdate");
                Assert.That(trap.sharedMaterial.shader.name,Is.EqualTo("Godmist/Dungeon/Silhouette Floor"));
                Assert.That(growth.sharedMaterial.shader.name,Is.EqualTo("Godmist/Dungeon/Torch Body"));
                var block=new MaterialPropertyBlock();growth.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_IsActor"),Is.EqualTo(1));
                Assert.That(growth.sharedMaterial.GetFloat("_Ambient"),Is.GreaterThan(0));
                Assert.That(trap.sharedMaterial.GetFloat("_Ambient"),Is.GreaterThan(0));
                var render=new RenderTexture(512,256,24);
                var pixels=new Texture2D(512,256,TextureFormat.RGBA32,false);
                var previous=RenderTexture.active;
                try
                {
                    camera.targetTexture=render;camera.orthographicSize=2.5f;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    float RenderEnergy(string label,float ambient)
                    {
                        Set(lighting,"ambient",ambient);Set(lighting,"lastLightingFrame",int.MinValue);Invoke(lighting,"LateUpdate");
                        SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=render;
                        pixels.ReadPixels(new Rect(0,0,512,256),0,0);pixels.Apply();
                        var folder="C:/Users/Patryczku/Documents/Godmist/LocalHandoffs/combat-visual-lifecycle-20261015/";
                        System.IO.File.WriteAllBytes(folder+label+".png",pixels.EncodeToPNG());
                        return pixels.GetPixels().Sum(c=>c.r+c.g+c.b);
                    }
                    var dark=RenderEnergy("summons-dark",.12f);
                    var bright=RenderEnergy("summons-bright",.85f);
                    Assert.That(dark,Is.GreaterThan(1));
                    Assert.That(bright,Is.GreaterThan(dark*1.5f),"The sprites must actually change brightness, not just be present in a registry.");
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(pixels);Object.DestroyImmediate(render);}
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);}
        }
    }
}
