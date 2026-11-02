using System;
using System.Collections;
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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    /// <summary>Captures authored game views with real runtime characters. No existing campaign is loaded or saved.</summary>
    public sealed class HighlightPlayGalleryTests
    {
        [Serializable] sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, runInBackground; }
        static string Output => Path.Combine(Path.GetDirectoryName(HighlightVerificationRunner.Output), "area-polish");
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
        public IEnumerator CaptureProductionHighlights()
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
            yield return Frames(10);
            var bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "Bootstrap must finish entering Play Mode before test setup.");
            // Exercise normal autosave behaviour without ever touching player saves.
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(bootstrap,
                new object[] { new SaveManager(Path.Combine(Output, "isolated-saves")) });
            Assert.That(bootstrap.TryStartNewGame(out var message), Is.True, message);
            yield return Loaded(bootstrap);
            yield return Capture("01-city");
            View<CharacterInfoPartyRailView>().OpenControllerCharacterPanel(ControllerCharacterPanel.Inventory, bootstrap.Session.PlayerParty.ActiveLeaderId);
            var card = View<CharacterInfoWindowView>();
            Assert.That(card.GetComponent<UIWorkspaceWindow>().IsOpen, Is.True);
            card.SelectEquipmentSlot(card.GetComponentsInChildren<CharacterInfoEquipmentSlotView>().First(s => s.Item != null));
            yield return Capture("02-inventory-selection");
            card.ClearItemSelection();
            card.OpenSkillTree();
            yield return Frames();
            var node = UnityEngine.Object.FindObjectsByType<SkillTreeNodeView>(FindObjectsSortMode.None).First();
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            node.OnPointerEnter(pointer);
            yield return Capture("02b-tree-hover");
            node.OnPointerExit(pointer);
            foreach (var window in UnityEngine.Object.FindObjectsByType<UIWorkspaceWindow>(FindObjectsSortMode.None)) window.Close();
            card.GetComponent<UIWorkspaceWindow>().Close();

            Assert.That(bootstrap.TryStartDebugCombat("combat.encounter.debug", out message), Is.True, message);
            yield return Loaded(bootstrap);
            var combat = View<CombatSandboxBootstrap>();
            yield return Frames(10);
            var session = combat.Session;
            Assert.That(session?.ActiveUnit?.IsPlayerControlled, Is.True);
            File.WriteAllLines(Path.Combine(Output, "gallery-characters.txt"), session.Units.Select(u =>
                $"{u.Character.Definition?.DisplayName}; player={u.IsPlayerSide}; position={u.Position}; skills={string.Join(",", u.Character.GetKnownSkills().Select(s => s.SkillId))}"));
            var movement = View<CombatMovementInputController>();
            var targeting = View<CombatTargetSelectionController>();
            var feedback = View<CombatGridFeedbackRenderer>();
            Stage("Combat views resolved");
            var actor = session.ActiveUnit;
            Stage("Reading movement paths");
            var paths = combat.GetActiveUnitMovementPaths();
            Stage("Movement paths=" + paths.Count);
            Assert.That(paths, Is.Not.Empty);
            var destination = paths.OrderByDescending(p => p.Value.Count).First().Key;
            // Use the production input seam: only pointer hit resolution is controlled by this fixture.
            // Geometry, PA costs, eligibility, styles, HUD and characters remain production data.
            movement.ResolvePointerPosition = _ => destination;
            Stage("Movement preview configured");
            yield return Capture("03-movement");
            movement.ResolvePointerPosition = _ => null;
            yield return Frames();
            Assert.That(feedback.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Combat pixel path gem" && r.gameObject.activeSelf), Is.False);
            var hostile = actor.Character.GetKnownSkills().First(s => s.TargetModes.Count > 0
                && s.TargetModes[0].TargetType != TargetType.Caster && s.TargetModes[0].TargetingType == TargetingType.Enemies);
            var enemy = session.Units.First(u => !u.IsPlayerSide);
            targeting.ResolvePointerPosition = _ => enemy.Position;
            targeting.ResolvePointerUnit = _ => enemy;
            targeting.BeginTargetSelection(hostile);
            Assert.That(targeting.SelectedSkill, Is.SameAs(hostile));
            yield return Capture("04-hostile-targets");
            // Controlled renderer state for the confirmation stroke, using the actual target position.
            feedback.ShowOutline(CombatGridFeedbackLayer.ChosenSkillTargets, new[] { enemy.Position }, HighlightStyle.Hostile);
            yield return Capture("05-confirmed-target");
            targeting.CancelTargetSelection();

            var friendly = session.Units.Where(u => u.IsPlayerControlled)
                .SelectMany(u => u.Character.GetKnownSkills().Select(s => (unit: u, skill: s)))
                .FirstOrDefault(x => x.skill.SkillId == "skill.paladin.heal");
            Assert.That(friendly.skill, Is.Not.Null, "The real starting paladin must have Uzdrowienie.");
            if (friendly.skill != null)
            {
                session.ActiveUnit = friendly.unit;
                // Let the real skill bar finish rebinding to the new actor before
                // selecting a skill; its normal rebuild cancels previous selections.
                yield return Frames();
                targeting.ResolvePointerPosition = _ => actor.Position;
                targeting.ResolvePointerUnit = _ => actor;
                targeting.BeginTargetSelection(friendly.skill);
                Assert.That(targeting.SelectedSkill, Is.SameAs(friendly.skill));
                yield return Capture("06-friendly-targets");
                Assert.That(targeting.SelectedSkill, Is.SameAs(friendly.skill));
                targeting.CancelTargetSelection();
            }
            var area = session.Units.Where(u => u.IsPlayerControlled)
                .SelectMany(u => u.Character.GetKnownSkills().Select(s => (unit: u, skill: s)))
                .FirstOrDefault(x => x.skill.TargetModes.Count > 0 && x.skill.TargetModes[0].TargetType == TargetType.AreaAroundTile);
            if (area.skill == null)
            {
                // A documented test build: real Reimond + an existing class skill, granted
                // only to this disposable runtime campaign. No content asset is altered.
                area = (friendly.unit, AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinGroundingWave.asset"));
                Assert.That(area.skill, Is.Not.Null);
                area.unit.Character.Skills.Add(area.skill);
                File.WriteAllText(Path.Combine(Output, "gallery-build.txt"), "Area preview: Reimond with existing Fala Przyciągająca granted to this isolated runtime test build; no asset or player save changes.");
            }
            if (area.skill != null)
            {
                session.ActiveUnit = area.unit;
                yield return Frames();
                targeting.ResolvePointerPosition = _ => enemy.Position;
                targeting.ResolvePointerUnit = _ => enemy;
                targeting.BeginTargetSelection(area.skill);
                Assert.That(targeting.SelectedSkill, Is.SameAs(area.skill));
                yield return Capture("07-area-preview");
                Assert.That(targeting.SelectedSkill, Is.SameAs(area.skill));
                var secondEnemy = session.Units.Last(u => !u.IsPlayerSide);
                targeting.ResolvePointerPosition = _ => secondEnemy.Position;
                targeting.ResolvePointerUnit = _ => secondEnemy;
                yield return Capture("07b-area-preview-second-target");
                Assert.That(targeting.SelectedSkill, Is.SameAs(area.skill));
                targeting.ResolvePointerPosition = _ => null;
                yield return Frames();
                yield return Capture("08-area-cleared-outside-map");
                targeting.CancelTargetSelection();
            }
            session.ActiveUnit = actor;
            movement.ResolvePointerPosition = null;
            targeting.ResolvePointerPosition = null; targeting.ResolvePointerUnit = null;
            Assert.That(combat.TryForceOutcome(CombatOutcomeState.Victory), Is.True);
            // The driver's ordinary outcome path clears combat membership before
            // returning to town. Do not bypass it with a direct scene transition.
            var returnDeadline = Time.realtimeSinceStartup + 30;
            while (bootstrap.Session.HasCombat && Time.realtimeSinceStartup < returnDeadline) yield return null;
            Assert.That(bootstrap.Session.HasCombat, Is.False);
            yield return Loaded(bootstrap);
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset");
            Assert.That(bootstrap.TryEnterDungeon(definition, 271101), Is.True);
            yield return Loaded(bootstrap);
            yield return Frames(20);
            var leader = View<Godmist.Views.Exploration.ExplorationPartyController>().ActiveMemberViews
                .First(v => v.CharacterId == bootstrap.Session.PlayerParty.ActiveLeaderId);
            UnityEngine.Events.UnityAction hover = () => leader.SetInspectionHighlight(true);
            Application.onBeforeRender += hover;
            try { yield return Capture("09-catacombs-hover"); }
            finally { Application.onBeforeRender -= hover; leader.SetInspectionHighlight(false); }
            var coordinator = View<Godmist.Views.Dungeons.DungeonCombatCoordinator>();
            var openingActor = coordinator.GetExplorationSkillActor();
            Assert.That(openingActor, Is.Not.Null);
            Assert.That(coordinator.TrySelectOpeningSkill(openingActor.Character.GetActiveSkills().First()), Is.True);
            yield return Capture("10-catacombs-skill-range");
            View<CombatTargetSelectionController>().CancelTargetSelection();
            yield return Frames();
            Assert.That(bootstrap.TryReturnToArungardFromDungeon(), Is.True);
            yield return Loaded(bootstrap);
            Assert.That(bootstrap.TryAbandonDungeon(), Is.True);
            var forest = AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_Prototype.asset");
            Assert.That(bootstrap.TryEnterDungeon(forest, 271102), Is.True);
            yield return Loaded(bootstrap);
            yield return Frames(20);
            coordinator = View<Godmist.Views.Dungeons.DungeonCombatCoordinator>();
            openingActor = coordinator.GetExplorationSkillActor();
            Assert.That(openingActor, Is.Not.Null);
            Assert.That(coordinator.TrySelectOpeningSkill(openingActor.Character.GetActiveSkills().First()), Is.True);
            yield return Capture("11-forest-skill-range");
            View<CombatTargetSelectionController>().CancelTargetSelection();
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
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
