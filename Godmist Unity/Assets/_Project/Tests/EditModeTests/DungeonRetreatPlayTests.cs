using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
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
    public class DungeonRetreatPlayTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Output => Path.GetFullPath(Application.dataPath + "/../../LocalHandoffs/dungeon-retreat-20261103");
        static void Stage(string stage) => File.AppendAllText(Path.Combine(Output, "stages.txt"), DateTime.UtcNow.ToString("O") + " " + stage + "\n");
        [Serializable] class Scenes { public string[] paths; public bool[] loaded, active; }
        static T View<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).First(v => v.gameObject.scene.IsValid());
        static IEnumerator Loaded(GameBootstrap game)
        {
            var until = Time.realtimeSinceStartup + 90;
            while (game.IsSceneLoading && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.IsSceneLoading, Is.False);
            for (var i = 0; i < 15; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator BothProductionEntrancesConfirmCancelAndReturnWithoutCompleting()
        {
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), Is.False);
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            File.WriteAllText(Path.Combine(Output, "previous-scenes.json"), JsonUtility.ToJson(new Scenes {
                paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(), active = setup.Select(s => s.isActive).ToArray() }));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            Stage("Entering Play Mode");
            yield return new EnterPlayMode();
            Stage("Play Mode entered");
            yield return VerifyRunningGame();
        }

        static IEnumerator VerifyRunningGame()
        {
            for (var i = 0; i < 10; i++) yield return null;
            var game = GameBootstrap.Instance;
            Stage("Bootstrap ready");
            Assert.That(game, Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(game,
                new object[] { new SaveManager(Path.Combine(Output, "isolated-saves")) });
            Assert.That(game.TryStartNewGame(out var error), Is.True, error);
            yield return Loaded(game);
            Stage("City loaded");
            Assert.That(game.TryRetreatFromDungeon(), Is.False);
            var successfulExits = 0;
            game.DungeonExited += _ => successfulExits++;
            foreach (var theme in new[] { "forest", "catacombs" })
            {
                var definition = game.ContentCatalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + theme + ".expedition");
                Assert.That(game.TryEnterDungeon(definition, 231023, offeredLevel: 1, length: DungeonLength.Short), Is.True);
                yield return Loaded(game);
                Stage(theme + " loaded");
                var dungeon = game.Session.ActiveDungeon;
                Assert.That(dungeon, Is.Not.Null);
                if (theme == "forest") Assert.That(dungeon.Floors.Count, Is.EqualTo(1), "The forest remains a single map.");
                var exits = UnityEngine.Object.FindObjectsByType<DungeonExitView>(FindObjectsSortMode.None);
                Assert.That(exits.Length, Is.EqualTo(2));
                var retreat = exits.Single(e => e.IsRetreatExit);
                Assert.That(retreat.IsExitBlocked, Is.False);
                var controller = View<ExplorationPartyController>();
                Assert.That(controller.TryTeleport(retreat.transform.position), Is.True);
                View<ExplorationCameraFollow>().SnapToLeader();
                for (var i = 0; i < 20; i++) yield return null;
                Assert.That(retreat.CanInteract(), Is.True);
                Stage(theme + " entrance interactable");
                Assert.That(theme == "forest" ? retreat.UsesStandingPresentation : !retreat.UsesStandingPresentation, Is.True);
                var viewport = Camera.main.WorldToViewportPoint(retreat.transform.position);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                foreach (var renderer in retreat.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.enabled && r.sprite != null))
                {
                    var top = Camera.main.WorldToViewportPoint(new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z));
                    var bottom = Camera.main.WorldToViewportPoint(new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, renderer.bounds.center.z));
                    Assert.That(top.y, Is.LessThanOrEqualTo(1.001f), renderer.name + " is clipped by camera limits.");
                    Assert.That(bottom.y, Is.GreaterThanOrEqualTo(-.001f), renderer.name + " is clipped by camera limits.");
                }
                yield return BackgroundGameCapture.Capture(Path.Combine(Output, theme + "-entrance.png"));
                Assert.That(game.Session.TrySetActiveCombat(new CombatSession { CombatId = "test.retreat.block" }), Is.True);
                Assert.That(game.TryRetreatFromDungeon(), Is.False);
                Assert.That(retreat.CanRequestInteraction, Is.False);
                Assert.That(game.Session.TryClearActiveCombat(), Is.True);

                // Scene rejection must not abandon the live instance.
                var destination = typeof(GameBootstrap).GetField("explorationSceneName", Private);
                var original = destination.GetValue(game);
                destination.SetValue(game, "MissingRetreatTestScene");
                LogAssert.Expect(LogType.Error, "SceneLoader: scena 'MissingRetreatTestScene' nie jest dodana do Build Settings.");
                Assert.That(game.TryRetreatFromDungeon(), Is.False);
                destination.SetValue(game, original);
                Assert.That(game.Session.ActiveDungeon, Is.SameAs(dungeon));

                retreat.Interact();
                var popup = (ItemActionPopupView)typeof(DungeonExitView).GetField("confirmation", Private).GetValue(retreat);
                Assert.That(popup?.IsModalOpen, Is.True);
                yield return BackgroundGameCapture.Capture(Path.Combine(Output, theme + "-confirmation.png"));
                popup.HideAll();
                Assert.That(game.Session.ActiveDungeon, Is.SameAs(dungeon), "Cancel preserves the expedition.");
                var gold = game.Session.Gold;
                var completed = game.Session.DungeonOffers.CompletedExpeditions;
                var danger = game.Session.DungeonOffers.GetDanger(definition.Theme);
                retreat.Interact();
                typeof(ItemActionPopupView).GetMethod("Confirm", Private).Invoke(popup, null);
                yield return Loaded(game);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Arungard"));
                Assert.That(game.Session.ActiveDungeon, Is.Null);
                Assert.That(dungeon.IsCompleted, Is.False);
                Assert.That(game.Session.Gold, Is.EqualTo(gold));
                Assert.That(game.Session.DungeonOffers.CompletedExpeditions, Is.EqualTo(completed));
                Assert.That(game.Session.DungeonOffers.GetDanger(definition.Theme), Is.EqualTo(danger));
                Assert.That(successfulExits, Is.Zero);
                File.AppendAllText(Path.Combine(Output, "play-evidence.txt"), theme + ": two exits; confirmation/cancel; combat/rejected scene; returned; not completed; no rewards PASS\n");
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            var file = Path.Combine(Output, "previous-scenes.json");
            if (!File.Exists(file)) yield break;
            var previous = JsonUtility.FromJson<Scenes>(File.ReadAllText(file));
            if (previous.paths.Length == 0 || previous.paths.Any(string.IsNullOrEmpty))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p, i) => new SceneSetup {
                path = p, isLoaded = previous.loaded[i], isActive = previous.active[i] }).ToArray());
        }
    }
}
