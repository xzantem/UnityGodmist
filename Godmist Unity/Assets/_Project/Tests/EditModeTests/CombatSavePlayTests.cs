using System;
using System.Collections;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Game;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatSavePlayTests
    {
        static string Output=>Path.GetFullPath("../LocalHandoffs/combat-save-20261103");
        [Serializable] sealed class Scenes {public string[] paths;public bool[] loaded,active;}
        static void Stage(string text)=>File.AppendAllText(Path.Combine(Output,"play-stages.txt"),DateTime.UtcNow.ToString("O")+" "+text+"\n");
        static IEnumerator Loaded(GameBootstrap game)
        {for(int i=0;i<1200&&game.IsSceneLoading;i++)yield return null;Assert.That(game.IsSceneLoading,Is.False);for(int i=0;i<5;i++)yield return null;}
        static IEnumerator Stable(GameBootstrap game)
        {
            var start=Time.realtimeSinceStartup;var deadline=start+45f;var last=string.Empty;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(game.Session.HasCombat&&CombatPersistence.CanSave(game.Session,out _))
                {Stage("Stable player boundary after "+(Time.realtimeSinceStartup-start).ToString("0.00")+"s");yield break;}
                var state=string.Join(";",game.Session.Combats.Select(c=>c.ActiveUnit?.Character?.CharacterId+" AP="+c.ActiveUnit?.CurrentActionPoints
                    +" busy="+c.IsExecutionBusy?.Invoke()+" outcome="+c.Outcome));
                if(state!=last) {Stage("Waiting: "+state);last=state;}
                yield return null;
            }
            Assert.Fail("Nie uzyskano stabilnej tury gracza: "+(CombatPersistence.CanSave(game.Session,out var reason)?"no combat":reason)
                +"; mode="+game.Session.CurrentMode+"; timeScale="+Time.timeScale+"; "+last);
        }
        [UnityTest]
        public IEnumerator QuicksaveLoadResumesActualForestAndCatacombsCombat()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False);
            Directory.CreateDirectory(Output);var setup=EditorSceneManager.GetSceneManagerSetup();
            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes
                {paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray()}));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            yield return new EnterPlayMode();yield return Verify();
        }
        static IEnumerator Verify()
        {
            for(int i=0;i<10;i++)yield return null;var game=GameBootstrap.Instance;Assert.That(game,Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(game,new object[]{new SaveManager(Path.Combine(Output,"isolated-saves"))});
            foreach(var theme in new[]{"forest","catacombs"})
            {
                Assert.That(game.TryStartNewGame(out var error),Is.True,error);yield return Loaded(game);
                game.Session.DebugEnemyAggroDisabled=true;
                var definition=game.ContentCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+theme+".expedition");
                Assert.That(game.TryEnterDungeon(definition,231023),Is.True);yield return Loaded(game);
                var coordinator=UnityEngine.Object.FindAnyObjectByType<DungeonCombatCoordinator>();
                var map=UnityEngine.Object.FindAnyObjectByType<DungeonFloorMapView>();
                var party=UnityEngine.Object.FindAnyObjectByType<ExplorationPartyController>();
                Assert.That(game.Session.ActiveDungeon.TryGetCurrentFloor(out var floor),Is.True);
                var leader=party.ActiveMemberViews.Single(v=>v.CharacterId==game.Session.PlayerParty.ActiveLeaderId);
                var triggers=UnityEngine.Object.FindObjectsByType<EncounterTriggerView>(FindObjectsSortMode.None);
                bool began=false;
                foreach(var trigger in triggers)
                {
                    foreach(var enemy in trigger.EnemyViews)
                    {
                        var cell=enemy.DungeonPosition.Surface.Cell;
                        var candidate=floor.Map.Surfaces.FirstOrDefault(s=>s.Level==0&&Math.Abs(s.Cell.x-cell.x)+Math.Abs(s.Cell.y-cell.y)==1
                            && !floor.Objects.OfType<Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance>().Any(o=>o.IsActive&&o.BlocksMovement&&o.OccupiesCell(s.Cell)));
                        if(Math.Abs(candidate.Cell.x-cell.x)+Math.Abs(candidate.Cell.y-cell.y)!=1)continue;
                        leader.SetPosition(map.CellToWorldCenter(candidate.Cell));enemy.FaceTowards(leader.Position);
                        floor.GetEnemyAwareness(DungeonCombatMembershipSystem.EnemyKey(trigger.Encounter.InstanceId,enemy.EnemyInstanceId))
                            .Advance(2,true,false);
                        game.Session.DebugEnemyAggroDisabled=false;
                        began=coordinator.TryStartEncounter(trigger);game.Session.DebugEnemyAggroDisabled=true;
                        if(began)break;
                    }
                    if(began)break;
                }
                Assert.That(began,Is.True,"The native coordinator must start a detected encounter.");
                yield return Stable(game);
                var host=UnityEngine.Object.FindAnyObjectByType<CombatSandboxBootstrap>();
                var combat=game.Session.ActiveCombat;Assert.That(combat,Is.Not.Null);
                var actor=combat.ActiveUnit;var paths=host.GetActiveUnitMovementPaths();
                var path=paths.Values.FirstOrDefault(p=>p.Count==2);
                Assert.That(path,Is.Not.Null,"A real paid movement action is needed before saving.");
                var fullAp=actor.CurrentActionPoints;
                Assert.That(host.TryMoveActiveUnitAlongPath(path),Is.True);
                Assert.That(game.TrySaveQuick(out error),Is.False,"Saving during the movement presentation must be rejected.");
                yield return Stable(game);Assert.That(actor.CurrentActionPoints,Is.LessThan(fullAp));
                var before=CombatSaveTests.Snapshot(game.Session);var id=combat.CombatId;var actorId=actor.Character.CharacterId;
                var ap=actor.CurrentActionPoints;var hp=actor.Character.CurrentHealth;var position=actor.Position;
                Stage(theme+": native encounter + paid move, AP="+ap);
                var pause=UnityEngine.Object.FindAnyObjectByType<PauseMenuView>();Assert.That(pause,Is.Not.Null);pause.Open();
                Assert.That(((UnityEngine.UI.Button)typeof(PauseMenuView).GetField("saveGameButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pause)).interactable,Is.True);
                Assert.That(game.TrySaveQuick(out error),Is.True,error);
                yield return BackgroundGameCapture.Capture(Path.Combine(Output,theme+"-save-menu.png"));
                Assert.That(game.TryLoadQuick(out error),Is.True,error);yield return Loaded(game);
                combat=game.Session.ActiveCombat;Assert.That(combat,Is.Not.Null);Assert.That(combat.CombatId,Is.EqualTo(id));
                Assert.That(combat.ActiveUnit.Character.CharacterId,Is.EqualTo(actorId));
                Assert.That(combat.ActiveUnit.Character,Is.SameAs(game.Session.CharactersById[actorId]));
                Assert.That(combat.ActiveUnit.CurrentActionPoints,Is.EqualTo(ap));
                Assert.That(combat.ActiveUnit.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(combat.ActiveUnit.Position,Is.EqualTo(position));
                Assert.That(game.Session.CurrentMode,Is.EqualTo(GameModeState.Combat));
                Assert.That(Time.timeScale,Is.EqualTo(1f),"Loading from the pause menu must release its old pause.");
                Assert.That(JsonUtility.ToJson(CombatSaveTests.Snapshot(game.Session).Combats[0]),Is.EqualTo(JsonUtility.ToJson(before.Combats[0])));
                yield return BackgroundGameCapture.Capture(Path.Combine(Output,theme+"-resumed-combat.png"));
                host=UnityEngine.Object.FindAnyObjectByType<CombatSandboxBootstrap>();
                Assert.That(host.CanEndTurn(),Is.True);host.RequestEndTurn();
                Assert.That(CombatPersistence.CanSave(game.Session,out var blockedReason),Is.False);
                pause=UnityEngine.Object.FindAnyObjectByType<PauseMenuView>();pause.Open();
                Assert.That(((UnityEngine.UI.Button)typeof(PauseMenuView).GetField("saveGameButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pause)).interactable,Is.False);
                var message=(TMPro.TMP_Text)typeof(PauseMenuView).GetField("messageText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pause);
                Assert.That(message,Is.Not.Null,"The native pause menu must have its reason label wired.");
                Assert.That(message.gameObject.activeInHierarchy,Is.True);Assert.That(message.text,Is.EqualTo(blockedReason));
                yield return BackgroundGameCapture.Capture(Path.Combine(Output,theme+"-save-blocked.png"),beforeRender:()=>
                {
                    Canvas.ForceUpdateCanvases();message.ForceMeshUpdate(true,true);
                    var panel=(GameObject)typeof(PauseMenuView).GetField("menuPanelRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pause);
                    var panelRect=(RectTransform)panel.transform;var corners=new Vector3[4];message.rectTransform.GetWorldCorners(corners);
                    Assert.That(message.transform.parent,Is.SameAs(panel.transform));
                    foreach(var corner in corners)Assert.That(panelRect.rect.Contains(panelRect.InverseTransformPoint(corner)),Is.True,"The reason must fit inside the pause panel.");
                    Assert.That(message.isTextOverflowing,Is.False,"The reason must not be clipped or truncated.");
                });pause.Close();
                yield return Stable(game);
                Assert.That(game.TrySaveQuick(out error),Is.True,error);
                Stage(theme+": saved, loaded exact turn, next real turn playable PASS");
                Assert.That(game.TryReturnToMainMenu(),Is.True);yield return Loaded(game);
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
            var path=Path.Combine(Output,"previous-scenes.json");if(!File.Exists(path))yield break;
            var prior=JsonUtility.FromJson<Scenes>(File.ReadAllText(path));
            if(prior.paths.Length==0||prior.paths.Any(string.IsNullOrEmpty))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(prior.paths.Select((p,i)=>new SceneSetup {path=p,isLoaded=prior.loaded[i],isActive=prior.active[i]}).ToArray());
        }
    }
}
