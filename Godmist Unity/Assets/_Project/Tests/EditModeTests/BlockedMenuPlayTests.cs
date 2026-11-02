using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Data.Dungeons;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
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
    public sealed class BlockedMenuPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/blocked-menu-reasons-20261102"));
        private Mouse mouse, previousMouse;
        private Gamepad setupPad;
        private GamepadState setupPadState;
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
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic)return;
            if(setupPad is { added:true })InputSystem.QueueStateEvent(setupPad,setupPadState);
            if(mouse is { added:true })
            {
                var state=desired;state.delta=desired.position-mouse.position.ReadValue();
                InputSystem.QueueStateEvent(mouse,state);
            }
            if(setupPad is { added:true })InputSystem.QueueStateEvent(setupPad,setupPadState);
        }
        private IEnumerator Send(Vector2 point,bool held=false,int frames=6,MouseButton button=MouseButton.Middle)
        {
            desired=new MouseState { position=point };
            if(held)desired=desired.WithButton(button);
            yield return Frames(frames);
        }
        private static IEnumerator Capture(string name)
        {
            yield return Frames();
            var path=Path.Combine(Output,name+".png");
            if(File.Exists(path))File.Move(path,Path.Combine(Output,name+"-previous-"+DateTime.UtcNow.Ticks+".png"));
            ScreenCapture.CaptureScreenshot(path);
            for(var i=0;i<120 && !File.Exists(path);i++)yield return null;
            Assert.That(File.Exists(path),Is.True); Assert.That(new FileInfo(path).Length,Is.GreaterThan(10000));
        }

        [UnityTest]
        public IEnumerator DisabledItemActionsExplainMouseAndControllerFocus()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False,"Do not replace unsaved user scenes.");
            Directory.CreateDirectory(Output);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            var game=EditorWindow.focusedWindow;
            Assert.That(game.GetType().Name,Is.EqualTo("GameView"));
            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes {
                paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray(),
                maximized=game.maximized,background=PlayerSettings.runInBackground }));
            game.maximized=true;
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
            // Disposable learned-skill profile; use the normal outside-combat
            // loadout service rather than injecting a HUD slot or changing assets.
            var warrior=View<ExplorationPartyController>().ActiveMemberViews.First(v=>v.RuntimeCharacter.ClassData.Id=="class.warrior").RuntimeCharacter;
            Assert.That(View<ExplorationPartyController>().SelectMember(warrior.CharacterId),Is.True);
            bootstrap.Session.DebugEnemyAggroDisabled=true;
            Assert.That(bootstrap.TryEnterDungeon(AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset"),271101),Is.True);
            yield return Loaded(bootstrap);
            bootstrap.Session.ActiveDungeon.MarkAssetAuditRoom();
            yield return Frames(10);
            // Use production exploration movement to face and enter the visible
            // corridor before selecting the opening; no actor teleport or fixture wiring.
            var party=View<ExplorationPartyController>();
            var leaderStart=party.ActiveLeaderTransform.position;
            setupPad=InputSystem.AddDevice<Gamepad>("Availability scenario movement pad");
            setupPadState=new GamepadState {leftStick=Vector2.up*.8f};
            var moveUntil=Time.realtimeSinceStartup+.4f;
            while(Time.realtimeSinceStartup<moveUntil)yield return null;
            setupPadState=new GamepadState();yield return Frames();
            Assert.That(Vector3.Distance(party.ActiveLeaderTransform.position,leaderStart),Is.GreaterThan(.1f));
            InputSystem.RemoveDevice(setupPad);
            yield return Frames();
            var coordinator=View<DungeonCombatCoordinator>();
            var cutSkill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            Assert.That(coordinator.TrySelectOpeningSkill(cutSkill),Is.True);
            var openingHost=View<CombatSandboxBootstrap>();
            var target=openingHost.Session.Units.First(u=>!u.IsPlayerSide && openingHost.IsUnitVisible(u)
                && openingHost.TryPlanSkillApproach(cutSkill,0,u.Position,new Dictionary<int,GridPosition?>(),null,null,
                    View<CombatGridFeedbackRenderer>().IsPositionVisible,out _,out _));
            var targetView=Field<Dictionary<BattleUnit,ExplorationCharacterView>>(coordinator,"openingViews")[target];
            View<CombatTargetSelectionController>().ConfirmTargetAt(target.Position);
            var entryDeadline=Time.realtimeSinceStartup+30;
            while(coordinator.IsPreparingOpening && Time.realtimeSinceStartup<entryDeadline)yield return null;
            Assert.That(coordinator.IsPreparingOpening,Is.False);
            Assert.That(bootstrap.Session.HasCombat,Is.True);
            var host=View<CombatSandboxBootstrap>();var session=host.Session;
            var actor=session.Units.First(u=>u.IsPlayerSide && u.Character.ClassData.Id=="class.warrior");
            session.ActiveUnit=actor;
            CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
            while(!host.CanEndTurn() && Time.realtimeSinceStartup<entryDeadline)yield return null;
            Assert.That(host.CanEndTurn(),Is.True);
            yield return Frames(10);
            var rail=View<CharacterInfoPartyRailView>();
            rail.OpenControllerCharacterPanel(ControllerCharacterPanel.Inventory, actor.Character.CharacterId);
            yield return Frames(10);
            var window=UnityEngine.Object.FindObjectsByType<CharacterInfoWindowView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                .Single(v=>v.Character==actor.Character);
            var slot=window.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().First(v=>v.Item!=null);
            var point=RectTransformUtility.WorldToScreenPoint(null, slot.GetComponent<RectTransform>().TransformPoint(slot.GetComponent<RectTransform>().rect.center));
            yield return Send(point);yield return Send(point,true,6,MouseButton.Right);yield return Send(point);
            var popup=Field<ItemActionPopupView>(window,"itemActionPopup");
            Assert.That(popup,Is.Not.Null);
            var menu=Field<PartyRailMenuView>(popup,"menu");
            var buttons=menu.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
            var blocked=buttons.First(b=>b.GetComponentInChildren<TMP_Text>().text=="Zdejmij");
            Assert.That(blocked.interactable,Is.False);
            yield return Hover(blocked);yield return new WaitForSecondsRealtime(.8f);
            var tooltip=UIWorkspaceTooltipView.ActiveInstance;
            Assert.That(Field<TMP_Text>(tooltip,"bodyText").text,Does.Contain("podczas walki"));
            yield return Capture("01-mouse-equipment-blocked");
            var transfer=buttons.First(b=>b.GetComponentInChildren<TMP_Text>().text.StartsWith("Przekaż"));
            yield return Hover(transfer);yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Field<TMP_Text>(tooltip,"bodyText").text,Does.Contain("przekazywać podczas walki"));
            yield return Capture("02-mouse-transfer-blocked");
            setupPad=InputSystem.AddDevice<Gamepad>("Blocked menu controller verification");
            setupPadState=new GamepadState { leftStick=Vector2.up*.8f }; yield return Frames(8);
            setupPadState=new GamepadState();yield return Frames(8);
            Assert.That(ControllerInput.UiActive,Is.True);
            // Navigate using the authored controller action map, not direct focus injection.
            for(var i=0;i<12 && EventSystem.current.currentSelectedGameObject!=blocked.gameObject;i++)
            {
                setupPadState=new GamepadState().WithButton(GamepadButton.DpadUp);yield return Frames(3);
                setupPadState=new GamepadState();yield return Frames(3);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(blocked.gameObject));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Field<TMP_Text>(tooltip,"bodyText").text,Does.Contain("podczas walki"));
            yield return Capture("03-controller-equipment-blocked");
            var worn=slot.Item;
            setupPadState=new GamepadState().WithButton(GamepadButton.South);yield return Frames(4);
            setupPadState=new GamepadState();yield return Frames(4);
            Assert.That(actor.Character.Equipment.GetEquippedItems().Contains(worn),Is.True);
            Assert.That(menu.gameObject.activeInHierarchy,Is.True);
            var compare=buttons.First(b=>b.GetComponentInChildren<TMP_Text>().text=="Porównaj z założonym");
            yield return Hover(compare);yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Field<TMP_Text>(tooltip,"bodyText").text,Does.Contain("już założony"));
            yield return Capture("04-already-equipped");
            menu.Hide();yield return Frames(5);
            Assert.That(Field<bool>(tooltip,"visible"),Is.False,"Closing the menu must clear its explanation.");
            Assert.That(tooltip.HasPinnedBranch,Is.False);

        }

        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(owner);
        private static Vector2 Center(Button button)
        {
            var rect=button.GetComponent<RectTransform>();
            return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        }
        private IEnumerator Hover(Button button)
        {
            yield return Send(Center(button));
            yield return new WaitForSecondsRealtime(.7f);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if(configured)
            {
                InputSystem.onBeforeUpdate-=FeedMouse;
                if(mouse is { added:true })InputSystem.RemoveDevice(mouse);
                if(setupPad is { added:true })InputSystem.RemoveDevice(setupPad);
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
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorWindow.focusedWindow.maximized=previous.maximized;
        }
    }
}
