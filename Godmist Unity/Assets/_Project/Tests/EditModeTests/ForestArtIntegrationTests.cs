using System;
using System.Collections;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Godmist.Tests.EditMode
{
    public sealed class ForestArtIntegrationTests
    {
        const string Root="Assets/_Project/ScriptableObjects/Dungeons/Forest/";
        static readonly string[] Directions={"s","se","e","ne","n","nw","w","sw"};
        static Vector2 Direction(string d)=>d switch {"s"=>Vector2.down,"se"=>new(1,-1),"e"=>Vector2.right,"ne"=>Vector2.one,"n"=>Vector2.up,"nw"=>new(-1,1),"w"=>Vector2.left,_=>new(-1,-1)};
        [TestCase("enemy.forest.bandit_thug",64,3)]
        [TestCase("enemy.forest.bandit_shooter",64,3)]
        [TestCase("enemy.forest.elf_archer",64,3)]
        [TestCase("enemy.forest.elf_druid",64,3)]
        [TestCase("enemy.forest.elf_poacher",64,3)]
        [TestCase("enemy.forest.rabid_wolf",64,3)]
        [TestCase("enemy.forest.infected_tree",64,3)]
        [TestCase("enemy.forest.infected_ent",128,12)]
        [TestCase("enemy.forest.infected_ent.burning",128,12)]
        public void EveryDirectionUsesNativeScaleTimingAndSeparateReactions(string id,int size,int ground)
        {
            foreach(var direction in Directions)
            {
                foreach(var state in new[]{CharacterAnimationState.Idle,CharacterAnimationState.Walk})
                {
                    Assert.That(CharacterAnimationResolver.TryResolveCharacter(id,Direction(direction),state,out var clip),Is.True,id+direction+state);
                    Assert.That(clip.FrameCount,Is.EqualTo(4));
                    foreach(var sprite in clip.Frames){Assert.That(sprite.rect.width,Is.EqualTo(size));Assert.That(sprite.pivot,Is.EqualTo(new Vector2(size*.5f,ground)));Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));}
                    Assert.That(clip.GetFrameDuration(0),Is.EqualTo(state==CharacterAnimationState.Idle?.4f:.2f).Within(.0001f));
                }
                Assert.That(CombatPoseResolver.TryResolveAction(id,"hit",Direction(direction),out var hit),Is.True);
                Assert.That(CombatPoseResolver.TryResolveAction(id,"dodge",Direction(direction),out var dodge),Is.True);
                Assert.That(hit.Actor,Is.Not.SameAs(dodge.Actor));
                Assert.That(hit.Actor.pivot.y,Is.EqualTo(ground+(hit.Actor.rect.width-size)/2f));
            }
        }
        [Test]
        public void SerializedBindingsHaveCorrectSpritesAndImportProfiles()
        {
            var config=JsonUtility.FromJson<ForestArtSetup.Config>(System.IO.File.ReadAllText("Assets/_Project/Scripts/Editor/ForestArtBindings.json"));
            foreach(var binding in config.bindings)
            {
                var so=new SerializedObject(AssetDatabase.LoadMainAssetAtPath(binding.asset));var field=so.FindProperty("<"+binding.field+">k__BackingField")??so.FindProperty(binding.field);
                for(var i=0;i<binding.sprites.Length;i++)Assert.That((field.isArray?field.GetArrayElementAtIndex(i):field).objectReferenceValue,Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(binding.sprites[i])),binding.asset+binding.field);
            }
            foreach(var item in config.imports)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(item.path);Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point),item.path);Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
                if(item.width>0){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(item.path);Assert.That(texture.width,Is.EqualTo(item.width),item.path);}
                if(!string.IsNullOrEmpty(item.sha256)){
                    using var hash=System.Security.Cryptography.SHA256.Create();
                    Assert.That(BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(item.path))).Replace("-","").ToLowerInvariant(),Is.EqualTo(item.sha256),item.path);
                }
            }
        }
        [Test]
        public void DruidHealingUsesDifferentVfxAndEntTransitionMatchesBothEndpoints()
        {
            Assert.That(CombatPoseResolver.TryResolveSkill("enemy.forest.elf_druid","skill.enemy.forest.druid_healing_growth",Vector2.down,AttackType.Ranged,false,out var heal),Is.True);
            Assert.That(CombatPoseResolver.TryResolveSkill("enemy.forest.elf_druid","skill.enemy.forest.druid_poison_growth",Vector2.down,AttackType.Ranged,false,out var poison),Is.True);
            Assert.That(heal.Action,Is.EqualTo("forest-heal"));Assert.That(heal.Effects[0],Is.Not.SameAs(poison.Effects[0]));
            Assert.That(heal.Effects[0].pivot.y,Is.EqualTo(19));
            var ent=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"CD_Forest_InfectedEnt.asset");
            Assert.That(ent.TransformationFrames.Count,Is.EqualTo(6));Assert.That(ent.TransformationFramesPerSecond,Is.EqualTo(5));
            foreach(var pair in new[]{(ent.TransformationFrames[0],ent.VisualSprite),(ent.TransformationFrames[5],ent.SecondPhaseDefinition.VisualSprite)})
                Assert.That(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(pair.Item1)),Is.EqualTo(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(pair.Item2))));
            foreach(var path in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var unit=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                if(!unit.Id.StartsWith("enemy.forest.",StringComparison.Ordinal))continue;
                Assert.That(unit.DeadSprite,Is.Not.Null,path);Assert.That(unit.DeadSprite.pivot,Is.EqualTo(unit.DeadSprite.rect.size*.5f));
            }
        }

        [Test]
        public void EveryProductionForestSkillHasAnIconAndDirectionalPose()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{Root}))
            {
                var unit=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(!unit.Id.StartsWith("enemy.forest.",StringComparison.Ordinal))continue;
                foreach(var skill in unit.StartingSkills.Append(unit.BasicAttackSkill).Where(s=>s!=null).Distinct())
                {
                    Assert.That(skill.Icon,Is.Not.Null,skill.SkillId);
                    foreach(var d in Directions)
                        Assert.That(CombatPoseResolver.TryResolveSkill(unit.Id,skill.SkillId,Direction(d),AttackType.Ranged,false,out _),Is.True,unit.Id+":"+skill.SkillId+":"+d);
                }
            }
        }
        [UnityTest]
        public IEnumerator BorrowedEntTransformsOnItsTurnAndDriverWaitsForAllFrames()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("Ent next-turn runtime regression");
            Godmist.Views.Combat.CombatExecutionDriver driver=null;
            var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var host=root.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();host.enabled=false;
                var spawner=root.AddComponent<Godmist.Views.Combat.CombatUnitViewSpawner>();spawner.enabled=false;
                var prefab=AssetDatabase.LoadAssetAtPath<Godmist.Views.Exploration.ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
                var view=Object.Instantiate(prefab,root.transform);
                var ent=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"CD_Forest_InfectedEnt.asset");
                var character=new CharacterInstanceFactory().Create(ent,ent.ClassData,1);view.Bind(character);
                var boss=new BattleUnit{Character=character,Position=new GridPosition(4,4),IsPlayerSide=true};
                var hero=new BattleUnit{Character=new CharacterInstanceFactory().Create(ent,ent.ClassData,1),Position=new GridPosition(0,0),IsPlayerSide=true};
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                for(var x=0;x<8;x++)for(var y=0;y<8;y++)map.TryAddFloorCell(new Vector2Int(x,y),"arena");
                var mapView=root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                root.AddComponent<Grid>();
                var floorObject=new GameObject("Floor",typeof(UnityEngine.Tilemaps.Tilemap),typeof(UnityEngine.Tilemaps.TilemapRenderer));floorObject.transform.SetParent(root.transform,false);
                var mapFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTilemap",mapFlags).SetValue(mapView,floorObject.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTile",mapFlags).SetValue(mapView,tile);
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("wallSegmentPrefab",mapFlags).SetValue(mapView,AssetDatabase.LoadAssetAtPath<Godmist.Views.Dungeons.DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                mapView.Rebuild(map);
                spawner.ConfigureDungeon(new Godmist.Runtime.Game.GameSession(),mapView);
                var session=new CombatSession{Grid=new BattleGrid(8,8),ActiveUnit=hero,
                    DungeonContext=new DungeonCombatContext("dungeon.forest.boss_expedition",0,"ent","arena",Vector2Int.zero,Vector2Int.one,map.FloorCells,null,null,surfaceMap:map)};
                session.Units.AddRange(new[]{hero,boss});
                spawner.Borrow(session,boss,view);
                Assert.That(spawner.IsUnitVisible(boss),Is.True,"Production-style map visibility must be initialized before presenting the turn.");
                driver=new Godmist.Views.Combat.CombatExecutionDriver(host,spawner,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(Godmist.Views.Combat.CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
                var owner=new Godmist.Systems.Combat.ForestEntBossSystem(session,new Godmist.Systems.Combat.CombatDamageSystem(),new Godmist.Systems.Combat.StatusEffectSystem(),new Godmist.Systems.Combat.CombatRecoverySystem());
                typeof(Godmist.Views.Combat.CombatExecutionDriver).GetField("forestEntBossSystem",flags).SetValue(driver,owner);
                var begin=typeof(Godmist.Views.Combat.CombatExecutionDriver).GetMethod("HandleUnitTurnBegan",flags);
                character.CurrentHealth=(int)(character.Stats.MaxHealth.GetValue()*.5f);owner.CheckPhases();
                begin.Invoke(driver,new object[]{hero});yield return null;
                Assert.That(owner.IsPhaseTwo(boss),Is.False);Assert.That(view.IsTransforming,Is.False);Assert.That(character.Definition,Is.SameAs(ent));
                Assert.That(CombatPoseResolver.TryResolveAction(ent.Id,"branch-sweep",Vector2.left,out var oldPose),Is.True);
                view.PlayCombatPose(oldPose,Vector2.left,3);
                session.ActiveUnit=boss;begin.Invoke(driver,new object[]{boss});
                Assert.That(owner.IsPhaseTwo(boss),Is.True);Assert.That(view.BodyRenderer.sprite,Is.SameAs(ent.TransformationFrames[0]));
                Assert.That(driver.IsActionInProgress,Is.True);Assert.That(view.HasCombatPose,Is.False);
                var start=Time.time;var position=view.Position;
                while(Time.time-start<.8f)
                {
                    view.MoveTowards(position+Vector2.left,.1f,0);view.PlayCombatPose(oldPose,Vector2.left,3);
                    yield return null;
                }
                Assert.That(view.IsTransforming,Is.True,"Six 5-FPS frames must not be cut short by the AI.");
                Assert.That(driver.IsActionInProgress,Is.True);Assert.That(view.Position,Is.EqualTo(position));
                Assert.That(ent.TransformationFrames,Does.Contain(view.BodyRenderer.sprite));
                while(Time.time-start<1.4f) yield return null;
                Assert.That(view.IsTransforming,Is.False);Assert.That(driver.IsActionInProgress,Is.False);
                Assert.That(view.BodyRenderer.sprite.name,Does.StartWith("infected-ent-p2-w-idle"));
            }
            finally{driver?.Dispose();Object.Destroy(root);Object.Destroy(tile);}
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator EntPhaseChangeAndPoseLayersWorkInPlayMode()
        {
            yield return new EnterPlayMode();
            var go=new GameObject("Forest art runtime smoke");
            try
            {
                var body=go.AddComponent<SpriteRenderer>();var view=go.AddComponent<CharacterView>();var data=new SerializedObject(view);data.FindProperty("bodyRenderer").objectReferenceValue=body;data.ApplyModifiedPropertiesWithoutUndo();
                var ent=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"CD_Forest_InfectedEnt.asset");var character=new CharacterInstanceFactory().Create(ent,ent.ClassData,10);view.Bind(new BattleUnit{Character=character});
                // A still-active action must not overwrite the S-only phase transition in LateUpdate.
                Assert.That(CombatPoseResolver.TryResolveAction(ent.Id,"branch-sweep",Vector2.left,out var oldPose),Is.True);
                view.PlayCombatPose(oldPose,Vector2.left,3);
                character.Definition=ent.SecondPhaseDefinition;yield return null;
                Assert.That(ent.TransformationFrames,Does.Contain(body.sprite));
                Assert.That(view.HasCombatPose,Is.False);Assert.That(body.transform.localRotation,Is.EqualTo(Quaternion.identity));
                var transitionStart=Time.time;
                while(Time.time-transitionStart<.8f)yield return null;Assert.That(ent.TransformationFrames,Does.Contain(body.sprite),"Transition must not end at the old fixed 8 FPS.");
                while(Time.time-transitionStart<1.3f)yield return null;
                Assert.That(body.sprite.name,Does.StartWith("infected-ent-p2-w-idle"));
                Assert.That(CombatPoseResolver.TryResolveSkill(character.Definition.Id,"skill.enemy.forest.ent_ignition",Vector2.down,AttackType.Ranged,false,out var pose),Is.True);
                using(var player=new CombatPosePlayer(body))
                {
                    body.sortingOrder=100;player.Play(pose,1);Assert.That(body.sprite,Is.SameAs(pose.Actor));
                    var fx=go.GetComponentsInChildren<SpriteRenderer>().Single(r=>r!=body && r.enabled);Assert.That(fx.sortingOrder,Is.EqualTo(100+pose.EffectOrders[0]));
                    Assert.That(fx.transform.localPosition,Is.EqualTo(Vector3.zero));Assert.That(fx.sprite.pivot.y,Is.EqualTo(44));
                    body.forceRenderingOff=true;player.Tick();Assert.That(fx.forceRenderingOff,Is.True);player.Clear();Assert.That(fx.enabled,Is.False);
                }
            }
            finally{Object.Destroy(go);}
            yield return new ExitPlayMode();
        }
    }
}
