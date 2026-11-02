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
    public sealed class MouseCameraPanPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/mouse-camera-pan-20261101"));
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
        private IEnumerator Send(Vector2 point,bool held=false,int frames=6,MouseButton button=MouseButton.Middle)
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
        public IEnumerator ProductionMouseDragPreservesExplorationTargetingAndUiOwnership()
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

        private IEnumerator RunAuthoredGame()
        {
            Application.runInBackground=true;
            SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single);
            yield return Frames(10);
            var bootstrap=GameBootstrap.Instance;
            Assert.That(bootstrap,Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(bootstrap,new object[] {new SaveManager(Path.Combine(Output,"isolated-saves"))});
            previousBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousMouse=Mouse.current; configured=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(var device in InputSystem.devices.ToArray())
                if(device.enabled && device is Mouse or Keyboard or Gamepad) {disabled.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("Native camera pan verification mouse");
            InputSystem.onBeforeUpdate+=FeedMouse;
            Assert.That(bootstrap.TryStartNewGame(out var message),Is.True,message);
            yield return Loaded(bootstrap);
            var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            var party=View<ExplorationPartyController>();
            Assert.That(party.SelectMember(party.ActiveMemberViews.First(v=>v.RuntimeCharacter.GetActiveSkills().Contains(skill)).CharacterId),Is.True);
            bootstrap.Session.DebugEnemyAggroDisabled=true;
            Assert.That(bootstrap.TryEnterDungeon(AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset"),271101),Is.True);
            yield return Loaded(bootstrap);
            bootstrap.Session.ActiveDungeon.MarkAssetAuditRoom();
            yield return Frames(10);
            party=View<ExplorationPartyController>();
            var follow=View<ExplorationCameraFollow>();
            var camera=follow.GetComponent<Camera>();
            follow.Zoom(3); follow.CenterOn(party.ActiveLeaderTransform.position);
            var until=Time.realtimeSinceStartup+.7f;
            while(Time.realtimeSinceStartup<until)yield return null;
            var start=new Vector2(Screen.width*.55f,Screen.height*.55f);
            yield return Send(start);
            var before=camera.transform.position; var leaderBefore=party.ActiveLeaderTransform.position; var zoom=camera.orthographicSize;
            var panOrigin=before;
            yield return Capture("01-unlocked-before");
            yield return Drag(start,start+Vector2.left*140);
            var after=camera.transform.position;
            Assert.That(after.x-before.x,Is.GreaterThan(.1f),"Actual MMB input must move the unlocked camera.");
            Assert.That(party.ActiveLeaderTransform.position,Is.EqualTo(leaderBefore));
            Assert.That(follow.IsFollowingLeader,Is.False);
            Assert.That(bootstrap.Session.HasCombat,Is.False);
            Assert.That(camera.orthographicSize,Is.EqualTo(zoom).Within(.001));
            yield return Capture("02-unlocked-after");
            yield return Frames(15);
            Assert.That(Vector3.Distance(camera.transform.position,after),Is.LessThan(.001),"Holding a stationary mouse must not drift or oscillate.");
            yield return Send(start);
            Assert.That(camera.transform.position,Is.EqualTo(after),"Pointer motion after releasing MMB must not pan.");

            var portrait=View<CharacterInfoPartyPortraitView>().GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            var uiPoint=RectTransformUtility.WorldToScreenPoint(null,portrait.TransformPoint(portrait.rect.center));
            yield return Send(uiPoint);
            Assert.That(UIWorkspaceInputRouter.IsPointerOverUi(),Is.True,"Use the actual party HUD for the blocked-start case.");
            yield return Drag(uiPoint,uiPoint+Vector2.right*35);
            yield return Send(start,true);
            Assert.That(camera.transform.position,Is.EqualTo(after),"A drag started over UI must not activate after entering the world.");
            yield return Send(start);

            var coordinator=View<DungeonCombatCoordinator>();
            Assert.That(coordinator.TrySelectOpeningSkill(skill),Is.True);
            yield return Frames();
            var targeting=View<CombatTargetSelectionController>();
            before=camera.transform.position;
            yield return Drag(start,start+Vector2.left*100);
            Assert.That(camera.transform.position.x-before.x,Is.GreaterThan(.05f));
            Assert.That(targeting.SelectedSkill,Is.SameAs(skill),"MMB must neither confirm nor cancel the opening skill.");
            Assert.That(targeting.IsApproachingSkillTarget,Is.False);
            Assert.That(coordinator.IsPreparingOpening,Is.True);
            yield return Send(start);
            yield return Capture("03-panning-while-targeting");

            follow.CenterOn(party.ActiveLeaderTransform.position,true);
            yield return Frames(); before=camera.transform.position;
            yield return Drag(start,start+Vector2.left*100);
            Assert.That(follow.IsFollowingLeader,Is.True);
            Assert.That(Vector3.Distance(camera.transform.position,before),Is.LessThan(.001),"MMB does not unlock a following camera.");
            yield return Send(start);
            yield return Send(start,true,button:MouseButton.Right);
            yield return Send(start+Vector2.left*3,true,button:MouseButton.Right);
            Assert.That(follow.IsFollowingLeader,Is.True,"A right press with only pointer jitter must retain follow.");
            yield return Send(start+Vector2.left*140,true,button:MouseButton.Right);
            Assert.That(follow.IsFollowingLeader,Is.False,"A real RMB drag must unlock the camera without F.");
            Assert.That(camera.transform.position.x-before.x,Is.GreaterThan(.05f));
            yield return Send(start);
            yield return Capture("04-right-drag-unlocks-camera");
            File.WriteAllText(Path.Combine(Output,"evidence.txt"),$"freeCameraBefore={panOrigin}; freeCameraAfter={after}; zoom={zoom}; leaderUnchanged=true; stationaryHoldNoDrift=true; releaseStops=true; uiStartBlocked=true; skillSelectionPreservedWithMMB=true; lockedCameraIgnoresMMB=true; rightDragUnlocks=true; rightJitterDoesNotUnlock=true; input=virtual Mouse through production Input System");
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
