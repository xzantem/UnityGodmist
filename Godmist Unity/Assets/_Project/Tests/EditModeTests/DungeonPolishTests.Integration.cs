using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class DungeonPolishTests
    {
        static string Output => Path.GetFullPath("../LocalHandoffs/dungeon-polish-20261024");

        [TestCase(GameModeState.Combat)]
        [TestCase(GameModeState.Exploration)]
        public void PauseDuringCombatRestoresTimeAndModeButCannotSave(GameModeState mode)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            var dungeonScene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var prior=GameBootstrap.Instance;var priorTime=Time.timeScale;
            var instance=typeof(GameBootstrap).GetProperty("Instance",BindingFlags.Static|BindingFlags.Public);
            try
            {
                var bootstrap=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
                var pause=dungeonScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PauseMenuView>(true)).Single();
                var game=new GameSession(mode);game.TrySetActiveCombat(new CombatSession{CombatId="pause-test"});
                typeof(GameBootstrap).GetProperty("Session").SetValue(bootstrap,game);instance.SetValue(null,bootstrap);
                Get<GameObject>(pause,"overlayRoot").SetActive(false);
                Time.timeScale=.75f;pause.Open();
                Assert.That(pause.IsOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);
                Assert.That(game.CurrentMode,Is.EqualTo(GameModeState.Menu));
                Assert.That(Get<Button>(pause,"saveGameButton").interactable,Is.False);
                Assert.That(new SaveManager(Path.Combine(Output,"blocked-save-test")).TrySaveManual(game,"blocked",out _,out _),Is.False);
                pause.Close();Assert.That(Time.timeScale,Is.EqualTo(.75f));
                Assert.That(game.CurrentMode,Is.EqualTo(mode));
            }
            finally{instance.SetValue(null,prior);Time.timeScale=priorTime;EditorSceneManager.ClosePreviewScene(dungeonScene);EditorSceneManager.ClosePreviewScene(scene);}
        }

        [TestCase("forest")]
        [TestCase("catacombs")]
        [Explicit("Actual scene, floor, actor views and combat coordinator; no live game save.")]
        public void NativeAlliesJoinAcrossLowWall(string biome)
        {
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride="low-wall-membership",SeedOverride=1};
            fixture.FrameProbe=(bootstrap,game)=>
            {
                var floor=game.ActiveDungeon.Floors[0];var map=Get<DungeonFloorMapView>(bootstrap,"floorMapView");
                var party=Get<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var encounters=Get<DungeonEncounterSpawner>(bootstrap,"encounterSpawner");
                var coordinator=Get<DungeonCombatCoordinator>(bootstrap,"combatCoordinator");
                var blocked=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive&&o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells()).ToHashSet();
                var edge=floor.Map.SurfaceWallEdges.First(e=>e.Level==0&&floor.Map.GetWallType(e)==DungeonWallType.Low
                    && !blocked.Contains(e.FirstCell)&&!blocked.Contains(e.SecondCell));
                var members=party.ActiveMemberViews.ToArray();
                foreach(var member in members)
                {member.SetPosition(map.CellToWorldCenter(edge.FirstCell));member.RuntimeCharacter.Stats.Speed=new Godmist.Core.Types.Stat(10000);}
                var trigger=encounters.Triggers.First(t=>t.EnemyViews.Any(v=>v.RuntimeCharacter?.LifeState==CharacterLifeState.Alive));
                var enemy=trigger.EnemyViews.First(v=>v.RuntimeCharacter?.LifeState==CharacterLifeState.Alive);
                enemy.SetPosition(map.CellToWorldCenter(edge.SecondCell));enemy.FaceTowards(members[0].Position);
                Assert.That(coordinator.CanEnemyDetect(enemy,members[0]),Is.True,"A native low wall must not occlude detection.");
                var started=(bool)typeof(DungeonCombatCoordinator).GetMethod("TryStartSpottedEncounter",Flags)
                    .Invoke(coordinator,new object[]{trigger,enemy,members[0]});
                Assert.That(started,Is.True);
                Assert.That(game.GetCombatForCharacter(members[0].CharacterId).Units.Count(u=>u.IsPlayerSide),Is.EqualTo(members.Length),
                    "All visible allies must enter atomically, before subsequent detection ticks.");
                for(var i=0;i<3;i++)
                {Set(coordinator,"detectionElapsed",1f);typeof(DungeonCombatCoordinator).GetMethod("UpdateDetection",Flags).Invoke(coordinator,null);}
                var joined=game.Combats.SelectMany(c=>c.Units).Where(u=>u.IsPlayerSide).Select(u=>u.Character.CharacterId).Distinct().ToArray();
                Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,biome+"-membership.txt"),$"low edge {edge.FirstCell}->{edge.SecondCell}; joined {joined.Length}/{members.Length}\n"+string.Join("\n",joined));
                Assert.That(joined.Length,Is.EqualTo(members.Length));
                var combat=game.GetCombatForCharacter(members[0].CharacterId);
                var xpEvents=0;
                members[0].RuntimeCharacter.ExperienceGained+=(amount,levels)=>{if(amount>0)xpEvents++;};
                Assert.That(combat.TryMarkEnemyDefeated(enemy.CombatUnit.Character.CharacterId),Is.True);
                Assert.That(xpEvents,Is.EqualTo(1),"Kill XP must use this scene's owning session before victory.");
                Assert.That(combat.Outcome,Is.EqualTo(CombatOutcomeState.Ongoing));
            };
            fixture.MeasureProductionFloorEntry("dungeon."+biome+".expedition",DungeonLength.Short);
        }

        [Test,Explicit("Native exploration death bookkeeping, without starting combat or writing a save.")]
        public void NativeExplorationDeathGrantsExperienceOnceBeforeAnyCombat()
        {
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Output,StageOverride="exploration-kill-xp",SeedOverride=1};
            fixture.FrameProbe=(bootstrap,game)=>
            {
                var floor=game.ActiveDungeon.Floors[0];
                var encounters=Get<DungeonEncounterSpawner>(bootstrap,"encounterSpawner");
                var party=Get<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController;
                var coordinator=Get<DungeonCombatCoordinator>(bootstrap,"combatCoordinator");
                var trigger=encounters.Triggers.First(t=>!t.Encounter.IsReinforcement&&t.EnemyViews.Any());
                var enemy=trigger.EnemyViews.First();var hero=party.ActiveMemberViews.First().RuntimeCharacter;
                var events=0;hero.ExperienceGained+=(amount,levels)=>{if(amount>0)events++;};
                enemy.RuntimeCharacter.CurrentHealth=0;enemy.RuntimeCharacter.LifeState=CharacterLifeState.Dead;
                var tick=typeof(DungeonCombatCoordinator).GetMethod("AdvanceExplorationTraps",Flags);
                tick.Invoke(coordinator,null);tick.Invoke(coordinator,null);
                Assert.That(events,Is.EqualTo(1));Assert.That(game.Combats,Is.Empty);
                Assert.That(trigger.Encounter.IsEnemyDefeated(enemy.EnemyInstanceId),Is.True);
                Assert.That(floor.EnemyBodies.Any(b=>b.EnemyInstanceId==enemy.EnemyInstanceId),Is.True);
            };
            fixture.MeasureProductionFloorEntry("dungeon.catacombs.expedition",DungeonLength.Short);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Explicit("Render the shipped loot window and verify each icon changes visible pixels.")]
        public void NativeLootWindowShowsEverySampleIconIncludingElfEar(bool fromSavedCampaign)
        {
            Directory.CreateDirectory(Output);
            var cameraNode=new GameObject("loot render camera",typeof(Camera));
            var canvasNode=new GameObject("loot render canvas",typeof(RectTransform),typeof(Canvas));
            var target=new RenderTexture(800,600,24);var image=new Texture2D(800,600,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                var camera=cameraNode.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=300;
                camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.045f,.035f,1);camera.targetTexture=target;
                var canvas=canvasNode.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera;canvas.planeDistance=1;canvas.pixelPerfect=true;
                var prefab=AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab");
                var view=Object.Instantiate(prefab,canvasNode.transform,false);
                var ear=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/ContentPass1/item.vs10.ucho_elfa.asset");
                var catalogForIcons=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                Assert.That(catalogForIcons.TryGetItem("item.material.elf_ear",out var forestEar),Is.True);
                Assert.That(catalogForIcons.TryGetItem("item.material.dead_king_ectoplasm",out var ectoplasm),Is.True);
                var definitions=new[]{forestEar,ear,ectoplasm}.Concat(catalogForIcons.ItemDefinitions.Where(d=>d!=null&&d.Icon!=null&&d!=forestEar&&d!=ear&&d!=ectoplasm)).Take(8).ToArray();
                var game=new GameSession(GameModeState.Exploration);var dungeon=new DungeonInstance("icon-audit",1);var floor=new DungeonFloorInstance(0);
                floor.TrySetMap(new DungeonFloorMap());dungeon.TryAddFloor(floor);game.TrySetActiveDungeon(dungeon);
                var items=definitions.Select((d,i)=>new ItemInstance(d,"icon."+i)).ToArray();
                if(fromSavedCampaign)
                {
                    var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "AppData/LocalLow/DefaultCompany/Godmist Unity/Saves/7e8ab8e9185f41a0bf6f05e06e051c39/autosave.json");
                    if(!File.Exists(path)) Assert.Ignore("Original local campaign unavailable.");
                    Assert.That(new GameSaveJsonSerializer().TryDeserialize(File.ReadAllText(path),out var saved),Is.True);
                    var catalog=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                    Assert.That(new GameSessionRestoreFactory().TryCreate(saved,catalog,out var restored),Is.True);
                    items[1]=restored.NpcsById.Values.SelectMany(n=>n.ShopInventory?.Items??new List<ItemInstance>())
                        .First(i=>i.Definition.Id==ear.Id);
                    Assert.That(ItemIconPresentation.GetIcon(items[1]),Is.SameAs(ear.Icon));
                    // The full campaign restores above; the forest drop itself is
                    // the distinct catalog definition, not the alchemist's ear.
                    Assert.That(ItemIconPresentation.GetIcon(items[0]),Is.SameAs(forestEar.Icon));
                }
                var body=new DungeonEnemyBodyInstance("body.icons","enemy.icons",Vector2Int.zero,13,items);
                floor.TryAddEnemyBody(body);view.Initialize(game,body.InstanceId);
                view.Window.RectTransform.anchoredPosition=Vector2.zero;
                foreach(var t in canvasNode.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                Color32[] Read(){Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();return image.GetPixels32();}
                var suffix=fromSavedCampaign?"-restored":"";
                var full=Read();File.WriteAllBytes(Path.Combine(Output,"loot-window-icons"+suffix+".png"),image.EncodeToPNG());
                var report=new List<string>();
                foreach(var slot in view.GetComponentsInChildren<DungeonLootItemView>().Where(s=>s.Item!=null))
                {
                    var icon=Get<Image>(slot,"icon");Assert.That(icon.sprite,Is.Not.Null,slot.Item.Definition.Id);
                    Assert.That(icon.enabled&&icon.color.a>0,Is.True,slot.Item.Definition.Id);
                    icon.enabled=false;var hidden=Read();icon.enabled=true;
                    var count=full.Where((p,i)=>!p.Equals(hidden[i])).Count();
                    report.Add(slot.Item.Definition.Id+" visible changed pixels="+count);
                    Assert.That(count,Is.GreaterThan(10),slot.Item.Definition.Id);
                }
                File.WriteAllLines(Path.Combine(Output,"loot-icon-visibility"+suffix+".txt"),report);
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(canvasNode);Object.DestroyImmediate(cameraNode);Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
        }
    }
}
