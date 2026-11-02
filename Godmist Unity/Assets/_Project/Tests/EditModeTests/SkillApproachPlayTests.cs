using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Systems.SaveLoad;
using Godmist.Views;
using Godmist.Views.Combat;
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
    /// <summary>Captures authored game views with real runtime characters. No existing campaign is loaded or saved.</summary>
    public sealed class SkillApproachPlayTests
    {
        private Gamepad verificationPad, previousPad;
        private GamepadState padState;
        private readonly List<InputDevice> disabledDevices = new();
        private InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private bool inputConfigured;

        private void FeedPad()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic && verificationPad is { added: true })
                InputSystem.QueueStateEvent(verificationPad, padState);
        }

        private void RestoreInput()
        {
            if (!inputConfigured) return;
            InputSystem.onBeforeUpdate -= FeedPad;
            if (verificationPad is { added: true }) InputSystem.RemoveDevice(verificationPad);
            foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            if (previousPad is { added: true }) previousPad.MakeCurrent();
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            inputConfigured = false;
        }

        [Serializable] sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, runInBackground; }
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/skill-auto-approach-20261101"));
        static void Stage(string value) => File.AppendAllText(Path.Combine(Output, "gallery-stages.txt"), DateTime.UtcNow.ToString("O") + " " + value + "\n");
        static T View<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.gameObject.scene.IsValid());
        static IEnumerator Frames(int count = 5) { for (var i = 0; i < count; i++) yield return null; }
        static IEnumerator Loaded(GameBootstrap bootstrap)
        {
            var deadline = Time.realtimeSinceStartup + 60;
            while (bootstrap.IsSceneLoading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bootstrap.IsSceneLoading, Is.False);
            yield return Frames();
        }
        static IEnumerator Capture(string name)
        {
            yield return Frames();
            var path = Path.Combine(Output, name + ".png");
            if (File.Exists(path)) File.Move(path, Path.Combine(Output, name + "-previous-" + DateTime.UtcNow.Ticks + ".png"));
            yield return BackgroundGameCapture.Capture(path);
            for (var i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            Assert.That(File.Exists(path), Is.True, name);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(10000), name);
        }

        [UnityTest]
        public IEnumerator ApproachAndCastThroughProductionInput()
        {
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), Is.False,
                "Do not replace an unsaved scene for visual verification.");
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();

            File.WriteAllText(Path.Combine(Output, "previous-scenes.json"), JsonUtility.ToJson(new Scenes
            {
                paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(),
                active = setup.Select(s => s.isActive).ToArray(), runInBackground = PlayerSettings.runInBackground
            }));

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            yield return new EnterPlayMode();
            // Allocate captured pointer/actor state after the domain reload. Iterator
            // closures allocated before EnterPlayMode cannot be preserved by the runner.
            yield return CaptureRunningGame();
        }

        private IEnumerator CaptureRunningGame()
        {
            Application.runInBackground = true;
            // EnterPlayMode may restore the runner's temporary scene. Start the
            // authored application scene through its normal runtime lifecycle.
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return Frames(10);
            var bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "Bootstrap must finish entering Play Mode before setup.");
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(bootstrap,
                new object[] { new SaveManager(Path.Combine(Output, "isolated-saves")) });
            Assert.That(bootstrap.TryStartNewGame(out var message), Is.True, message);
            yield return Loaded(bootstrap);
            Assert.That(bootstrap.TryStartDebugCombat("combat.encounter.debug",out message),Is.True,message);
            yield return Loaded(bootstrap); yield return Frames(10);
            var host = View<CombatSandboxBootstrap>(); var session = host.Session;
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset");
            var actor = session.Units.First(u=>u.IsPlayerSide && u.Character.GetKnownSkills().Contains(skill));
            session.ActiveUnit = actor;
            Godmist.Systems.Combat.CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
            var enemy = session.Units.First(u=>!u.IsPlayerSide);
            enemy.Position = new GridPosition(actor.Position.X+3,actor.Position.Y,actor.Position.Level);
            Assert.That(session.Grid.IsWalkable(enemy.Position),Is.True);
            yield return Frames(10);
            var targeting = View<CombatTargetSelectionController>();
            targeting.ResolvePointerPosition = _=>enemy.Position;
            targeting.ResolvePointerUnit = _=>enemy;
            targeting.BeginTargetSelection(skill);
            yield return Frames(10);
            Assert.That(host.TryPlanSkillApproach(skill,0,enemy.Position,new System.Collections.Generic.Dictionary<int,GridPosition?>(),null,null,null,out var path,out var cost),Is.True);
            var beforePosition=actor.Position; var beforeAp=actor.CurrentActionPoints;
            var skillCost=Godmist.Systems.Combat.CombatActionCostCalculator.CalculateSkillCostForTarget(actor,skill,enemy);
            var resolved=0;
            session.SkillResolved += (u,s,id)=> {if(u==actor && s==skill)resolved++;};
            Assert.That(targeting.IsControllerTarget(enemy),Is.True,"Controller target cycling must include the approachable enemy.");
            Assert.That(targeting.ApproachPreviewCost,Is.EqualTo(cost),"Combat HUD must include movement in its AP preview.");
            yield return Capture("01-combat-approach-preview");
            Assert.That(actor.Position,Is.EqualTo(beforePosition)); Assert.That(actor.CurrentActionPoints,Is.EqualTo(beforeAp));
            targeting.ConfirmTargetAt(enemy.Position);
            yield return Capture("02-combat-moving");
            var deadline=Time.realtimeSinceStartup+15;
            while((targeting.IsSelectingTarget || host.IsPlayerMovementPresentationInProgress) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(resolved,Is.EqualTo(1),"One confirmation must resolve exactly one skill after movement.");
            Assert.That(actor.Position,Is.EqualTo(path.Last()));
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(beforeAp-cost-skillCost));
            yield return Capture("03-combat-after-cast");
            File.WriteAllText(Path.Combine(Output,"combat-evidence.txt"),$"beforeAP={beforeAp}; movement={cost}; skill={skillCost}; afterAP={actor.CurrentActionPoints}; casts={resolved}; steps={path.Count}");
            while(host.GetDriver(session).IsActionInProgress && Time.realtimeSinceStartup<deadline)yield return null;
            // An independent native encounter keeps cancellation independent of hit/crit results.
            Assert.That(host.TryForceOutcome(CombatOutcomeState.Victory),Is.True);
            deadline=Time.realtimeSinceStartup+30;
            while(bootstrap.Session.HasCombat && Time.realtimeSinceStartup<deadline)yield return null;
            yield return Loaded(bootstrap);
            Assert.That(bootstrap.TryStartDebugCombat("combat.encounter.debug",out message),Is.True,message);
            yield return Loaded(bootstrap); yield return Frames(10);
            host=View<CombatSandboxBootstrap>(); session=host.Session;
            actor=session.Units.First(u=>u.IsPlayerSide && u.Character.GetKnownSkills().Contains(skill));
            session.ActiveUnit=actor;
            Godmist.Systems.Combat.CombatTurnSystem.RefreshMaxActionPoints(session,actor,true);
            enemy=session.Units.First(u=>!u.IsPlayerSide);
            enemy.Position=new GridPosition(actor.Position.X+3,actor.Position.Y,actor.Position.Level);
            session.SkillResolved+=(u,s,id)=>{if(u==actor && s==skill)resolved++;};
            targeting=View<CombatTargetSelectionController>();
            targeting.ResolvePointerPosition=_=>enemy.Position; targeting.ResolvePointerUnit=_=>enemy;
            yield return Frames(10);
            targeting.BeginTargetSelection(skill);
            yield return Frames(10);
            var cancelAp=actor.CurrentActionPoints;
            File.WriteAllText(Path.Combine(Output,"cancel-context.txt"),$"active={session.ActiveUnit==actor}; inProgress={host.GetDriver(session).IsActionInProgress}; ap={actor.CurrentActionPoints}; steps={actor.MovementStepsThisTurn}; outcome={session.Outcome}; actor=({actor.Position.X},{actor.Position.Y}); target=({enemy.Position.X},{enemy.Position.Y}); life={enemy.Character.LifeState}");
            Assert.That(host.TryPlanSkillApproach(skill,0,enemy.Position,new Dictionary<int,GridPosition?>(),null,null,null,out _,out var cancelCost),Is.True,File.ReadAllText(Path.Combine(Output,"cancel-context.txt")));
            targeting.ConfirmTargetAt(enemy.Position);
            targeting.CancelTargetSelection();
            deadline=Time.realtimeSinceStartup+15;
            while(host.IsPlayerMovementPresentationInProgress && Time.realtimeSinceStartup<deadline)yield return null;
            yield return Frames(5);
            Assert.That(resolved,Is.EqualTo(1),"Cancellation must not execute the queued second skill.");
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(cancelAp-cancelCost));
            File.WriteAllText(Path.Combine(Output,"cancel-evidence.txt"),$"castsBefore=1; castsAfter={resolved}; beforeAP={cancelAp}; movement={cancelCost}; afterAP={actor.CurrentActionPoints}; skillCostCharged=0");
            Assert.That(host.TryForceOutcome(CombatOutcomeState.Victory),Is.True);
            deadline=Time.realtimeSinceStartup+30;
            while(bootstrap.Session.HasCombat && Time.realtimeSinceStartup<deadline)yield return null;
            yield return Loaded(bootstrap);
            Assert.That(bootstrap.TryReturnToMainMenu(),Is.True);
            yield return Loaded(bootstrap);
            Assert.That(bootstrap.TryStartNewGame(out message),Is.True,message);
            yield return Loaded(bootstrap);
            var party = View<Godmist.Views.Exploration.ExplorationPartyController>();
            var openingLeader = party.ActiveMemberViews.First(v=>v.RuntimeCharacter.GetActiveSkills().Contains(skill));
            Assert.That(party.SelectMember(openingLeader.CharacterId),Is.True);
            bootstrap.Session.DebugEnemyAggroDisabled=true;
            var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset");
            Assert.That(bootstrap.TryEnterDungeon(definition,271101),Is.True);
            yield return Loaded(bootstrap);
            // Deterministic runtime scenario: retain production input/traversal/commit,
            // but stop ambient patrols, traps and detection from interrupting the positive case.
            bootstrap.Session.ActiveDungeon.MarkAssetAuditRoom();
            yield return Frames(20);
            party = View<Godmist.Views.Exploration.ExplorationPartyController>();
            var coordinator=View<Godmist.Views.Dungeons.DungeonCombatCoordinator>();
            Assert.That(coordinator.TrySelectOpeningSkill(skill),Is.True);
            host=View<CombatSandboxBootstrap>(); targeting=View<CombatTargetSelectionController>();
            session=host.Session; actor=session.ActiveUnit;
            yield return VerifyMovementWhileTargeting(party, coordinator, targeting, actor, skill);
            BattleUnit openingTarget=null; List<GridPosition> openingPath=null; var openingCost=0;
            foreach(var candidate in session.Units.Where(u=>!u.IsPlayerSide && host.IsUnitVisible(u)))
            {
                if(host.TryPlanSkillApproach(skill,0,candidate.Position,new Dictionary<int,GridPosition?>(),null,null,
                    View<CombatGridFeedbackRenderer>().IsPositionVisible,out var planned,out var plannedCost) && planned.Count>0)
                {openingTarget=candidate;openingPath=planned;openingCost=plannedCost;break;}
            }
            Assert.That(openingTarget,Is.Not.Null,"A visible enemy outside melee range must have an approach in this seeded scenario.");
            targeting.ResolvePointerPosition=_=>openingTarget.Position;
            targeting.ResolvePointerUnit=_=>openingTarget;
            yield return Capture("04-opening-approach-preview");
            var overlayFrames = 0;
            var missingOverlayFrames = 0;
            var overlayFeedback = View<CombatGridFeedbackRenderer>();
            var overlayUntil = Time.realtimeSinceStartup + .5f;
            System.Action inspectOverlay = () =>
            {
                overlayFrames++;
                if (!overlayFeedback.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Combat pixel path gem"
                    && r.enabled && r.gameObject.activeInHierarchy)) missingOverlayFrames++;
            };
            // Batch Editor has no Game View render callback. Sample the actual
            // production renderer after each completed player-loop frame instead.
            while (Time.realtimeSinceStartup < overlayUntil)
            {
                yield return null;
                inspectOverlay();
            }
            File.WriteAllText(Path.Combine(Output, "overlay-continuity.txt"),
                $"sampledFrames={overlayFrames}; missingPathFrames={missingOverlayFrames}; openingRefreshInterval=0.1s; observedSeconds=0.5");
            Assert.That(overlayFrames, Is.GreaterThan(0));
            Assert.That(missingOverlayFrames, Is.Zero, "Opening range refresh must keep the approach path visible in every rendered frame.");
            Assert.That(targeting.ApproachPreviewCost,Is.Zero,"Opening movement must not reserve or display an AP charge.");
            var openingAp=actor.CurrentActionPoints; var openingOrigin=actor.Position;
            targeting.ConfirmTargetAt(openingTarget.Position);
            Assert.That(party.CanMoveWhileTargeting, Is.False, "A committed approach owns movement until arrival or cancellation.");
            var trackedViews = (Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView>)typeof(Godmist.Views.Dungeons.DungeonCombatCoordinator)
                .GetField("openingViews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(coordinator);
            var movingEnemy = trackedViews[openingTarget];
            var enemyBeforeMove = movingEnemy.Position;
            var targetBeforeMove = openingTarget.Position;
            // Script a patrol displacement through the ordinary collision/traversal
            // owner while the confirmed approach is active, without teleporting the caster.
            foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.left, Vector2.down })
            {
                movingEnemy.MoveTowards(enemyBeforeMove + direction * 2f, 2f, 0);
                host.SynchronizeSkillApproachArrival();
                if (openingTarget.Position != targetBeforeMove && host.IsUnitVisible(openingTarget)
                    && !Godmist.Systems.Combat.CombatTargetingService.IsPositionWithinRange(openingPath.Last(), openingTarget.Position, skill.TargetModes[0].AbsoluteRange)) break;
                movingEnemy.SetPosition(enemyBeforeMove);
                host.SynchronizeSkillApproachArrival();
            }
            Assert.That(openingTarget.Position, Is.Not.EqualTo(targetBeforeMove), "The patrol stimulus must move the target to another cell.");
            Assert.That(host.IsUnitVisible(openingTarget), Is.True);
            Assert.That(Godmist.Systems.Combat.CombatTargetingService.IsPositionWithinRange(openingPath.Last(), openingTarget.Position, skill.TargetModes[0].AbsoluteRange), Is.False,
                "The target must leave the original casting destination's range to exercise replanning.");
            var movedTargetPosition = openingTarget.Position;
            yield return Capture("05-opening-moving");
            deadline=Time.realtimeSinceStartup+25;
            var nextTrace=0f;
            while(coordinator.IsPreparingOpening && Time.realtimeSinceStartup<deadline)
            {
                if(Time.realtimeSinceStartup>=nextTrace)
                {
                    nextTrace=Time.realtimeSinceStartup+.5f;
                    var leaderView=View<Godmist.Views.Exploration.ExplorationPartyController>().ActiveMemberViews.First(v=>v.CharacterId==actor.Character.CharacterId);
                    File.AppendAllText(Path.Combine(Output,"opening-trace.txt"),$"t={Time.realtimeSinceStartup:F2}; moving={party.IsSkillApproachInProgress}; selected={targeting.SelectedSkill?.SkillId}; actor=({actor.Position.X},{actor.Position.Y}); world={leaderView.Position}; scale={Time.timeScale}; paused={EditorApplication.isPaused}\n");
                }
                yield return null;
            }
            if(coordinator.IsPreparingOpening)yield return Capture("07-opening-pending");
            Assert.That(coordinator.IsPreparingOpening,Is.False,"Opening must finish, not wait for a second confirmation.");
            Assert.That(bootstrap.Session.HasCombat,Is.True,"The ordinary opening commit must create the combat.");
            var actual=bootstrap.Session.Combats.SelectMany(c=>c.Units).First(u=>u.Character==actor.Character);
            Assert.That(actual.CurrentActionPoints,Is.EqualTo(openingAp-Godmist.Systems.Combat.CombatActionCostCalculator.CalculateSkillCostForTarget(actual,skill,openingTarget)));
            Assert.That(actual.Position,Is.Not.EqualTo(openingOrigin));
            Assert.That(actual.Position, Is.Not.EqualTo(openingPath.Last()), "The caster must follow the displaced target instead of stopping at the old destination.");
            File.WriteAllText(Path.Combine(Output, "moving-target-evidence.txt"),
                $"targetBefore=({targetBeforeMove.X},{targetBeforeMove.Y},{targetBeforeMove.Level}); targetAfter=({movedTargetPosition.X},{movedTargetPosition.Y},{movedTargetPosition.Level}); "
                + $"oldDestination=({openingPath.Last().X},{openingPath.Last().Y},{openingPath.Last().Level}); actualDestination=({actual.Position.X},{actual.Position.Y},{actual.Position.Level}); "
                + $"beforeAP={openingAp}; afterAP={actual.CurrentActionPoints}; input=one confirmation, target moved through normal traversal");
            yield return Capture("06-opening-after-cast");
            File.WriteAllText(Path.Combine(Output,"opening-evidence.txt"),$"beforeAP={openingAp}; planningMovementCost={openingCost}; chargedMovement=0; afterAP={actual.CurrentActionPoints}; steps={openingPath.Count}; ambientPatrolsAndDetectionDisabled=true");
        }

        private IEnumerator VerifyMovementWhileTargeting(Godmist.Views.Exploration.ExplorationPartyController party,
            Godmist.Views.Dungeons.DungeonCombatCoordinator coordinator, CombatTargetSelectionController targeting,
            BattleUnit actor, SkillData skill)
        {
            Assert.That(party.CanMoveWhileTargeting, Is.True);
            previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousPad = Gamepad.current;
            inputConfigured = true;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in InputSystem.devices.ToArray())
                if (device.enabled && device is Mouse or Keyboard or Gamepad)
                { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
            verificationPad = InputSystem.AddDevice<Gamepad>("Skill targeting verification pad");
            InputSystem.onBeforeUpdate += FeedPad;
            var leader = party.ActiveMemberViews.First(v => v.CharacterId == actor.Character.CharacterId);
            var before = leader.Position;
            var beforeGrid = actor.Position;
            var beforeAp = actor.CurrentActionPoints;
            try
            {
                foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
                {
                    padState = new GamepadState { leftStick = direction * .8f };
                    var until = Time.realtimeSinceStartup + .4f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    padState = new GamepadState();
                    yield return Frames(5);
                    if (Vector3.Distance(before, leader.Position) > .05f) break;
                }
                Assert.That(Vector3.Distance(before, leader.Position), Is.GreaterThan(.05f), "Real left-stick input must move the exploration leader while targeting.");
                var settleUntil = Time.realtimeSinceStartup + .15f;
                while (Time.realtimeSinceStartup < settleUntil) yield return null;
                Assert.That(actor.Position, Is.Not.EqualTo(beforeGrid), "Opening targeting must follow the moved caster's grid position.");
                Assert.That(coordinator.IsPreparingOpening, Is.True);
                Assert.That(targeting.SelectedSkill, Is.SameAs(skill), "Movement must retain the selected opening skill.");
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(beforeAp), "Free exploration movement must not charge combat AP.");
                yield return Capture("08-free-movement-while-targeting");
                File.WriteAllText(Path.Combine(Output, "manual-targeting-movement.txt"),
                    $"before={before}; after={leader.Position}; distance={Vector3.Distance(before, leader.Position)}; selected={skill.SkillId}; AP={beforeAp}->{actor.CurrentActionPoints}; input=virtual Gamepad through production actions");
            }
            finally { RestoreInput(); }
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            RestoreInput();
            if (Application.isPlaying) yield return new ExitPlayMode();
            var path = Path.Combine(Output, "previous-scenes.json");
            if (!File.Exists(path)) yield break;
            var previous = JsonUtility.FromJson<Scenes>(File.ReadAllText(path));
            PlayerSettings.runInBackground = previous.runInBackground;
            if (previous.paths.Length == 0 || previous.paths.Any(string.IsNullOrEmpty))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
                EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p, i) => new SceneSetup
                    { path = p, isLoaded = previous.loaded[i], isActive = previous.active[i] }).ToArray());

        }
    }
}
