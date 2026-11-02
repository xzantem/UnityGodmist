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
    public sealed class SkillAvailabilityPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/skill-availability"));
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
            yield return BackgroundGameCapture.Capture(path);
            for(var i=0;i<120 && !File.Exists(path);i++)yield return null;
            Assert.That(File.Exists(path),Is.True); Assert.That(new FileInfo(path).Length,Is.GreaterThan(10000));
        }

        [UnityTest]
        public IEnumerator ActualSkillBarRefreshesAndExplainsAvailability()
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
            // Disposable learned-skill profile; use the normal outside-combat
            // loadout service rather than injecting a HUD slot or changing assets.
            var warrior=View<ExplorationPartyController>().ActiveMemberViews.First(v=>v.RuntimeCharacter.ClassData.Id=="class.warrior").RuntimeCharacter;
            var dischargeSkill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorDischarge.asset");
            if(!warrior.GetKnownSkills().Contains(dischargeSkill))warrior.Skills.Add(dischargeSkill);
            var loadout=warrior.ActiveSkillLoadout.ActiveSkillIds.ToList();
            if(!loadout.Contains(dischargeSkill.SkillId))loadout[1]=dischargeSkill.SkillId;
            Assert.That(new SkillLoadoutSystem().TrySetActiveSkills(bootstrap.Session,warrior.CharacterId,loadout),Is.True);
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
            var nextTrace=0f;
            while(coordinator.IsPreparingOpening && Time.realtimeSinceStartup<entryDeadline)
            {
                if(Time.realtimeSinceStartup>=nextTrace)
                {
                    nextTrace=Time.realtimeSinceStartup+.1f;
                    var selection=View<CombatTargetSelectionController>();
                    var leaderView=View<ExplorationPartyController>().ActiveMemberViews.First(v=>v.CharacterId==warrior.CharacterId);
                    File.AppendAllText(Path.Combine(Output,"entry-trace.txt"),$"t={Time.realtimeSinceStartup}; selected={selection.SelectedSkill?.SkillId}; following={selection.IsApproachingSkillTarget}; origin=({openingHost.Session?.ActiveUnit?.Position.X},{openingHost.Session?.ActiveUnit?.Position.Y}); world={leaderView.Position}; target=({target.Position.X},{target.Position.Y}); busy={openingHost.IsSkillInputBusy}; hasCombat={bootstrap.Session.HasCombat}\n");
                }
                yield return null;
            }
            File.AppendAllText(Path.Combine(Output,"entry-trace.txt"),$"entryEnd: targetVisible={targetView.IsVisibleForInspection}; targetAlive={targetView.RuntimeCharacter.LifeState}; targetWorld={targetView.Position}; hasCombat={bootstrap.Session.HasCombat}\n");
            if(!bootstrap.Session.HasCombat)yield return Capture("entry-failed");
            Assert.That(coordinator.IsPreparingOpening,Is.False);
            Assert.That(bootstrap.Session.HasCombat,Is.True);
            while(openingHost.IsSkillInputBusy && Time.realtimeSinceStartup<entryDeadline)yield return null;
            Assert.That(openingHost.IsSkillInputBusy,Is.False);
            var host=View<CombatSandboxBootstrap>();var session=host.Session;
            var actor=session.Units.First(u=>u.IsPlayerSide && u.Character.ClassData.Id=="class.warrior");
            session.ActiveUnit=actor;
            CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
            actor.Character.ResourcePool.CurrentValue=0;
            yield return Frames(10);
            var bar=View<CombatSkillBarView>();
            var slots=Field<List<CombatSkillSlotView>>(bar,"spawnedSlots");
            var cut=slots.First(slot=>Field<SkillData>(slot,"skill").SkillId=="skill.warrior.cut");
            var discharge=slots.First(slot=>Field<SkillData>(slot,"skill").SkillId==WarriorDischargeRules.SkillId);
            var positions=slots.Select(slot=>slot.transform.GetSiblingIndex()).ToArray();
            Assert.That(Field<Button>(cut,"button").interactable,Is.True);
            Assert.That(Field<Button>(discharge,"button").interactable,Is.False);
            Assert.That(discharge.TryActivate(),Is.False,"Unavailable buttons and hotkey activation must use the same gate.");
            Assert.That(Field<string>(discharge,"unavailableReason"),Does.Contain("20 furii"));
            yield return Hover(discharge);
            yield return Capture("01-rage-requirement");
            var tooltip=Field<UIWorkspaceTooltipView>(discharge,"tooltipView");
            Assert.That(Field<TextMeshProUGUI>(tooltip,"metaText").text,Does.Contain("20 furii"));

            // Offscreen capture temporarily changes the canvas projection. Restore
            // the physical hover before testing a stationary pointer across a state change.
            yield return Hover(discharge);
            Assert.That(Field<bool>(discharge,"hovered"),Is.True);
            new CombatRecoverySystem().RestoreResource(actor,40);
            yield return Frames(10);
            Assert.That(Field<Button>(discharge,"button").interactable,Is.True);
            Assert.That(Field<TextMeshProUGUI>(tooltip,"metaText").text,Does.Not.Contain("Wymaga co najmniej 20 furii."),"Reason must update under a stationary pointer.");
            yield return Capture("02-rage-restored");

            yield return Hover(cut);
            actor.Character.Resistances.SuppressionResistance=0;
            var status=new StatusEffectSystem();
            Assert.That(StatusEffectSystem.CalculateStatusApplicationChance(actor,actor,StatusEffectIds.Stun,1),Is.GreaterThan(0));
            Assert.That(status.ApplyStatus(actor,actor,StatusEffectIds.Stun,1,3,1,0,0,"availability native verification",sharedRoll:0f),Is.True);
            yield return Frames(10);
            Assert.That(slots.All(slot=>!Field<Button>(slot,"button").interactable),Is.True);
            Assert.That(slots.All(slot=>Field<Image>(slot,"unavailableOverlay").enabled),Is.True);
            Assert.That(slots.All(slot=>Field<Image>(slot,"icon").material.shader.name=="Godmist/UI/Unavailable Skill Icon"),Is.True);
            Assert.That(cut.TryActivate(),Is.False);
            Assert.That(Field<TextMeshProUGUI>(tooltip,"metaText").text,Does.Contain("Status blokuje używanie umiejętności."));
            yield return Capture("03-stunned");
            yield return Hover(cut);

            Assert.That(status.RemoveStatus(actor,StatusEffectIds.Stun),Is.True);
            yield return Frames(10);
            Assert.That(Field<Button>(cut,"button").interactable,Is.True);
            actor.CurrentActionPoints=0;
            yield return Frames(10);
            Assert.That(Field<Button>(cut,"button").interactable,Is.False);
            Assert.That(Field<TextMeshProUGUI>(tooltip,"metaText").text,Does.Contain("Za mało punktów akcji."));
            yield return Capture("04-no-action-points");
            yield return Hover(cut);
            Assert.That(Field<bool>(cut,"hovered"),Is.True);
            CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
            yield return Frames(10);
            Assert.That(Field<Button>(cut,"button").interactable,Is.True);
            Assert.That(Field<TextMeshProUGUI>(tooltip,"metaText").text,Does.Not.Contain("Za mało punktów akcji."));
            Assert.That(Field<Image>(cut,"icon").material.shader.name,Is.Not.EqualTo("Godmist/UI/Unavailable Skill Icon"));
            Assert.That(slots.Select(slot=>slot.transform.GetSiblingIndex()),Is.EqualTo(positions),"Availability must not reorder loadout slots or shortcuts.");
            yield return Capture("05-ready-again");
            File.WriteAllText(Path.Combine(Output,"evidence.txt"),$"character={actor.Character.Definition.DisplayName}; skills={slots.Count}; rage0DischargeBlocked=true; rage40DischargeEnabled=true; allBlockedUnderStun=true; removedStunRestores=true; zeroAPBlocks=true; restoredAPEnables=true; tooltipUpdatesWithoutRehover=true; disabledTryActivateRejected=true; slotOrderUnchanged=true; nativeInput=virtual mouse hover through authored Input System UI module");
        }

        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(owner);
        private IEnumerator Hover(CombatSkillSlotView slot)
        {
            var rect=slot.GetComponent<RectTransform>();
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            yield return Send(point);
            var until=Time.realtimeSinceStartup+.8f;
            while(Time.realtimeSinceStartup<until)yield return null;
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

        }
    }
}
