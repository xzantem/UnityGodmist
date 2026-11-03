using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed class HeldPointerMovementPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/held-pointer-movement-20261103"));
        private Mouse mouse, previousMouse;
        private MouseState desired;
        private readonly List<InputDevice> disabled = new();
        private InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        private InputSettings.BackgroundBehavior previousBackground;
        private bool configured;
        private static T View<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.gameObject.scene.IsValid());
        private static IEnumerator Frames(int count=6) { for(var i=0;i<count;i++)yield return null; }
        private static IEnumerator Loaded(GameBootstrap bootstrap)
        {
            var until=Time.realtimeSinceStartup+60;
            while(bootstrap.IsSceneLoading && Time.realtimeSinceStartup<until)yield return null;
            Assert.That(bootstrap.IsSceneLoading,Is.False); yield return Frames();
        }
        private void FeedMouse()
        {
            if(InputState.currentUpdateType==InputUpdateType.Dynamic && mouse is { added:true }) InputSystem.QueueStateEvent(mouse,desired);
        }
        private IEnumerator Send(Vector2 point,bool held=false,int frames=6,MouseButton button=MouseButton.Left)
        {
            desired=new MouseState { position=point };
            if(held)desired=desired.WithButton(button);
            yield return Frames(frames);
        }
        private IEnumerator Drag(Vector2 start,Vector2 end)
        {
            yield return Send(start); yield return Send(start,true);
            for(var i=1;i<=12;i++)yield return Send(Vector2.Lerp(start,end,i/12f),true,1);
            yield return Send(end,true);
        }
        private static IEnumerator Capture(string name)
        {
            yield return Frames();
            var path=Path.Combine(Output,name+".png");
            if(File.Exists(path))File.Move(path,Path.Combine(Output,name+"-previous-"+DateTime.UtcNow.Ticks+".png"));
            yield return BackgroundGameCapture.Capture(path);
            for(var i=0;i<120 && !File.Exists(path);i++)yield return null;
            Assert.That(File.Exists(path),Is.True); Assert.That(new FileInfo(path).Length,Is.GreaterThan(10000));
        }

        [UnityTest]
        public IEnumerator HeldPrimaryMovesInCityAndDungeonWithoutLeakingAcrossUiOrTargeting()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False,"Do not replace unsaved user scenes.");
            Directory.CreateDirectory(Output);
            var setup=EditorSceneManager.GetSceneManagerSetup();

            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes {
                paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray(),
                background=PlayerSettings.runInBackground }));

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            yield return new EnterPlayMode();
            yield return RunAuthoredGame();
        }

        private static readonly System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
        private static IEnumerator Seconds(float seconds)
        {var end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        private static List<Vector2> Goals(ExplorationPartyController party,Camera camera)
        {
            var origin=(Vector2)party.ActiveLeaderTransform.position;var goals=new List<Vector2>();
            foreach(var radius in new[]{2f,3f,1.5f,4f,5f})foreach(var direction in new[]{Vector2.right,Vector2.up,Vector2.left,Vector2.down,new Vector2(1,1).normalized,new Vector2(-1,1).normalized})
            {
                var point=origin+direction*radius;var viewport=camera.WorldToViewportPoint(point);
                if(viewport.x<.22f||viewport.x>.83f||viewport.y<.35f||viewport.y>.8f
                    ||party.ActiveMemberViews.Any(v=>Vector2.Distance(v.Position,point)<.65f)
                    ||goals.Any(p=>Vector2.Distance(p,point)<1f))continue;
                if(!(bool)Call(party,"TryMoveLeaderToWorldPosition",point))continue;
                party.CancelMovement();goals.Add(point);if(goals.Count==2)return goals;
            }
            Assert.Fail("The authored scene must provide two visible reachable ground destinations.");return goals;
        }
        private IEnumerator Arrive(ExplorationPartyController party,Vector2 goal)
        {
            var end=Time.realtimeSinceStartup+8;
            while(Vector2.Distance(party.ActiveLeaderTransform.position,goal)>.12f&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(Vector2.Distance(party.ActiveLeaderTransform.position,goal),Is.LessThan(.12f),"Release must finish the last accepted route.");
        }
        private IEnumerator GroundHold(ExplorationPartyController party,Camera camera,string name)
        {
            var goals=Goals(party,camera);var before=(Vector2)party.ActiveLeaderTransform.position;
            yield return Capture(name.Replace("arrival","before"));
            yield return Send(camera.WorldToScreenPoint(goals[0]),false);
            yield return Send(camera.WorldToScreenPoint(goals[0]),true,2);
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.SameAs(mouse),"A real Input System LPM press on ground must arm steering.");
            yield return Send(camera.WorldToScreenPoint(goals[1]),true,1);yield return Seconds(.3f);
            Assert.That(Field<Vector2?>(party,"movementDestination"),Is.Not.Null);
            Assert.That(Vector2.Distance(Field<Vector2?>(party,"movementDestination").Value,goals[1]),Is.LessThan(.02f));
            yield return Send(camera.WorldToScreenPoint(goals[1]),false,1);
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.Null);
            yield return Arrive(party,goals[1]);
            Assert.That(Vector2.Distance(before,party.ActiveLeaderTransform.position),Is.GreaterThan(.5f));
            yield return Capture(name);
            File.AppendAllText(Path.Combine(Output,"positions.txt"),$"{name}: before={before}; first={goals[0]}; finalGoal={goals[1]}; arrived={party.ActiveLeaderTransform.position}\n");
        }
        private IEnumerator RunAuthoredGame()
        {
            Application.runInBackground=true;SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single);yield return Frames(10);
            var bootstrap=GameBootstrap.Instance;Assert.That(bootstrap,Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(bootstrap,new object[]{new SaveManager(Path.Combine(Output,"isolated-saves"))});
            previousBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;previousBackground=InputSystem.settings.backgroundBehavior;
            previousMouse=Mouse.current;configured=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(var device in InputSystem.devices.ToArray())if(device.enabled&&device is Mouse or Keyboard or Gamepad){disabled.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("Native hold movement verification mouse");InputSystem.onBeforeUpdate+=FeedMouse;
            Assert.That(bootstrap.TryStartNewGame(out var message),Is.True,message);yield return Loaded(bootstrap);
            var party=View<ExplorationPartyController>();var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            Assert.That(party.SelectMember(party.ActiveMemberViews.First(v=>v.RuntimeCharacter.GetActiveSkills().Contains(skill)).CharacterId),Is.True);
            var follow=View<ExplorationCameraFollow>();follow.CenterOn(party.ActiveLeaderTransform.position,false);yield return Frames();
            var camera=follow.GetComponent<Camera>();yield return GroundHold(party,camera,"01-city-arrival");

            var goals=Goals(party,camera);yield return Send(camera.WorldToScreenPoint(goals[0]),false);
            yield return Send(camera.WorldToScreenPoint(goals[0]),true,2);
            var rail=View<CharacterInfoPartyRailView>();rail.OpenPartyView();yield return Frames();
            var window=rail.PartyView;Assert.That(window,Is.Not.Null);
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.Null,"Opening a window cancels the gesture.");
            window.Close();yield return Frames();
            var last=Field<Vector2?>(party,"movementDestination");yield return Send(camera.WorldToScreenPoint(goals[1]),true);yield return Seconds(.3f);
            var after=Field<Vector2?>(party,"movementDestination");
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.Null,"Closing UI while held must not rearm.");
            Assert.That(after==null||last==after,Is.True,"Returning to world cannot issue a new destination.");
            yield return Send(camera.WorldToScreenPoint(goals[1]),false);

            // A press owned by actual UGUI must not turn into ground steering on exit.
            rail.OpenPartyView();yield return Frames();
            var header=window.GetComponent<UIWorkspaceWindow>().RectTransform;
            var uiPoint=RectTransformUtility.WorldToScreenPoint(null,header.TransformPoint(new Vector2(header.rect.center.x,header.rect.yMax-20)));
            yield return Send(uiPoint,false);Assert.That(UIWorkspaceInputRouter.IsPointerOverUi(),Is.True);
            yield return Send(uiPoint,true);window.Close();yield return Send(camera.WorldToScreenPoint(goals[1]),true);yield return Seconds(.3f);
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.Null,"A UI press cannot start movement by leaving the UI.");
            yield return Send(camera.WorldToScreenPoint(goals[1]),false);party.CancelMovement();

            bootstrap.Session.DebugEnemyAggroDisabled=true;
            Assert.That(bootstrap.TryEnterDungeon(AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset"),271101),Is.True);
            yield return Loaded(bootstrap);bootstrap.Session.ActiveDungeon.MarkAssetAuditRoom();yield return Frames(10);
            party=View<ExplorationPartyController>();follow=View<ExplorationCameraFollow>();follow.CenterOn(party.ActiveLeaderTransform.position,false);yield return Frames();camera=follow.GetComponent<Camera>();
            yield return GroundHold(party,camera,"02-dungeon-arrival");
            goals=Goals(party,camera);yield return Send(camera.WorldToScreenPoint(goals[0]),false);yield return Send(camera.WorldToScreenPoint(goals[0]),true,2);
            var coordinator=View<DungeonCombatCoordinator>();Assert.That(coordinator.TrySelectOpeningSkill(skill),Is.True);yield return Frames();
            Assert.That(Field<Mouse>(party,"pointerMovementMouse"),Is.Null,"Skill targeting owns subsequent confirmation; an old held press cannot steer.");
            yield return Send(camera.WorldToScreenPoint(goals[1]),true);yield return Seconds(.3f);
            Assert.That(bootstrap.Session.HasCombat,Is.False,"A held press cannot confirm or cast a newly selected opening skill.");
            var targeting=View<CombatTargetSelectionController>();Assert.That(targeting.SelectedSkill,Is.SameAs(skill));targeting.CancelTargetSelection();
            yield return Send(camera.WorldToScreenPoint(goals[1]),false);party.CancelMovement();
            File.WriteAllText(Path.Combine(Output,"evidence.txt"),"cityHoldRetargets=true; dungeonHoldRetargets=true; releaseFinishesLastGoal=true; windowDisarms=true; noResumeWhileHeld=true; uiStartBlocked=true; targetingDisarmsWithoutCasting=true; input=virtual Mouse through production Input System and authored scenes; screenshots=after release");
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if(configured)
            {
                InputSystem.onBeforeUpdate-=FeedMouse;
                if(mouse is { added:true })InputSystem.RemoveDevice(mouse);
                foreach(var device in disabled)if(device.added)InputSystem.EnableDevice(device);
                disabled.Clear();
                if(previousMouse is { added:true })previousMouse.MakeCurrent();
                InputSystem.settings.editorInputBehaviorInPlayMode=previousBehavior;
                InputSystem.settings.backgroundBehavior=previousBackground;
                configured=false;
            }
            if(Application.isPlaying)yield return new ExitPlayMode();
            var path=Path.Combine(Output,"previous-scenes.json");
            if(!File.Exists(path))yield break;
            var previous=JsonUtility.FromJson<Scenes>(File.ReadAllText(path));
            PlayerSettings.runInBackground=previous.background;
            if(previous.paths.Length==0 || previous.paths.Any(string.IsNullOrEmpty))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p,i)=>new SceneSetup {path=p,isLoaded=previous.loaded[i],isActive=previous.active[i]}).ToArray());

        }
    }
}
