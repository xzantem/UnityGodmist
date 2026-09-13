using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class SeamlessDungeonCombatTests
    {
        private static CombatSession Combat(string id, string room = null) => new()
        {
            CombatId = id, IsSeamless = true, Grid = new BattleGrid(3, 2),
            DungeonContext = new DungeonCombatContext("dungeon", 0, id, room ?? id,
                Vector2Int.zero, Vector2Int.right,
                new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up }, null, null)
        };
        private static GameSession Game()
        {
            var game = new GameSession();
            game.TrySetActiveDungeon(new DungeonInstance("dungeon", 12));
            return game;
        }
        private static BattleUnit Unit(string id, bool player = true) => new()
        {
            IsPlayerSide = player, Position = new GridPosition(0, 0),
            CurrentActionPoints = 7, AvailableKnives = 2,
            Character = new CharacterInstance
            {
                CharacterId = id, LifeState = CharacterLifeState.Alive,
                CurrentHealth = 12, Stats = new CharacterStats { Speed = new Stat(20f) }
            }
        };

        [Test]
        public void Focus_DoesNotChangeMembershipOrPermitSave()
        {
            var game = Game(); var first = Combat("one"); var second = Combat("two");
            first.Units.Add(Unit("a")); second.Units.Add(Unit("b"));
            Assert.That(game.TryRegisterDungeonCombat(first), Is.True);
            Assert.That(game.TryRegisterDungeonCombat(second), Is.True);
            Assert.That(game.TryFocusCombat(second), Is.True);
            Assert.That(game.ActiveDungeonCombatContext, Is.SameAs(second.DungeonContext));
            game.TryFocusCombat(null);
            Assert.That(game.HasCombat, Is.True);
            Assert.That(game.IsCharacterInCombat("a"), Is.True);
            Assert.That(game.IsCharacterInCombat("explorer"), Is.False);
            Assert.That(game.TryClearActiveDungeon(), Is.False);
            Assert.That(game.GetCombatForCharacter("a"), Is.SameAs(first));
            Assert.Throws<InvalidOperationException>(() => new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "blocked"));
            Assert.That(game.TryRemoveCombat(first), Is.True);
            Assert.That(game.HasCombat, Is.True);
            Assert.That(game.TryRemoveCombat(second), Is.True);
            Assert.That(game.HasCombat, Is.False);
        }

        [Test]
        public void Registration_RejectsDuplicateRoomAndCharacter_WithoutChangingOtherSession()
        {
            var game = Game(); var first = Combat("one", "room"); first.Units.Add(Unit("a"));
            game.TryRegisterDungeonCombat(first);
            Assert.That(game.TryRegisterDungeonCombat(Combat("two", "room")), Is.False);
            var overlapping = Combat("three"); overlapping.Units.Add(Unit("a"));
            Assert.That(game.TryRegisterDungeonCombat(overlapping), Is.False);
            Assert.That(game.Combats.Count, Is.EqualTo(1));
        }

        [Test]
        public void PositionConflict_SearchIsDeterministicAndCannotCrossClosedEdge()
        {
            var grid = new BattleGrid(3, 1);
            var occupied = new HashSet<GridPosition> { new(0, 0) };
            grid.SetBlockedEdge(new(0, 0), new(1, 0));
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out _), Is.False);
            grid.SetBlockedEdge(new(0, 0), new(1, 0), false);
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out var found), Is.True);
            Assert.That(found, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(DungeonCombatPositionResolver.TryResolve(grid, new(0, 0), occupied, out var repeated), Is.True);
            Assert.That(repeated, Is.EqualTo(found));
        }

        [Test]
        public void Exit_IsPlayerOnly_AndMovementPreviewStopsBeyondThreshold()
        {
            var session = Combat("one"); var player = Unit("a");
            session.Units.Add(player);
            var outside = new GridPosition(-1, 0);
            session.OpenExitEdges.Add(new BattleCellEdge(player.Position, outside));
            var movement = new CombatMovementSystem();
            var paths = movement.CalculateReachablePaths(session, player);
            Assert.That(paths.ContainsKey(outside), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(-2, 0)), Is.False);
            Assert.That(player.CurrentActionPoints, Is.EqualTo(7));
            Assert.That(session.CanMoveBetween(Unit("enemy", false), player.Position, outside), Is.False);
            Assert.That(session.Grid.IsWalkable(outside), Is.False, "Exit is not an extra arena tile for skills.");
        }

        [Test]
        public void ThresholdTransit_ReservesDestinationAgainstAnotherCombat()
        {
            var game = Game(); var source = Combat("source"); var target = Combat("target");
            target.DungeonContext = new DungeonCombatContext("dungeon", 0, "target", "target",
                new Vector2Int(3, 0), new Vector2Int(4, 0),
                new[] { new Vector2Int(3, 0), new Vector2Int(4, 0) }, null, null);
            var leaving = Unit("a"); leaving.Position = new GridPosition(3, 0);
            source.Units.Add(leaving);
            var other = Unit("b"); other.Position = new GridPosition(1, 0); target.Units.Add(other);
            game.TryRegisterDungeonCombat(source); game.TryRegisterDungeonCombat(target);
            Assert.That(target.CanMoveBetween(other, new(1, 0), new(0, 0)), Is.False);
            Assert.That(target.IsPositionOccupied(new(0, 0)), Is.True);
            var arrival = Unit("c");
            Assert.That(DungeonCombatParticipationSystem.TryJoin(target, arrival, new(0, 0)), Is.True);
            Assert.That(arrival.Position, Is.Not.EqualTo(new GridPosition(0, 0)));
            source.Units.Remove(leaving);
            Assert.That(target.CanMoveBetween(other, new(1, 0), new(0, 0)), Is.True);
        }

        [Test]
        public void ClosedExit_IsAbsentFromPreview()
        {
            var session = Combat("one"); var unit = Unit("a"); session.Units.Add(unit);
            Assert.That(new CombatMovementSystem().CalculateReachablePaths(session, unit)
                .ContainsKey(new GridPosition(-1, 0)), Is.False);
        }

        [Test]
        public void Reentry_PreservesResourcesAndWaitsFullActionValue_WithoutChangingActiveTurn()
        {
            var session = Combat("one"); var actor = Unit("a"); var joining = Unit("b");
            session.Units.Add(actor); session.ActiveUnit = actor;
            session.ActiveParticipant = new CombatTurnParticipant(actor);
            session.TurnParticipants.Add(session.ActiveParticipant);
            joining.CurrentActionValue = 750f;
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(1, 0)), Is.True);
            Assert.That(joining.CurrentActionValue, Is.EqualTo(750f));
            Assert.That(joining.CurrentActionPoints, Is.EqualTo(7));
            Assert.That(joining.AvailableKnives, Is.EqualTo(2));
            Assert.That(session.ActiveUnit, Is.SameAs(actor));
            joining.Position = new(-1, 0);
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, joining), Is.True);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(1, 0)), Is.True);
            Assert.That(joining.CurrentActionValue, Is.GreaterThanOrEqualTo(CombatTurnSystem.CalculateBaseActionValue(joining)));
            Assert.That(session.Units.FindAll(value => value == joining).Count, Is.EqualTo(1));
        }

        [Test]
        public void FailedJoin_DoesNotMoveOrScheduleUnit()
        {
            var session = Combat("one"); session.Grid = new BattleGrid(1, 1);
            session.Units.Add(Unit("a")); var joining = Unit("b"); joining.Position = new(8, 9);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session, joining, new(0, 0)), Is.False);
            Assert.That(joining.Position, Is.EqualTo(new GridPosition(8, 9)));
            Assert.That(session.Units.Count, Is.EqualTo(1));
            Assert.That(session.TurnParticipants, Is.Empty);
        }

        [Test]
        public void DownedWithRescuerOutside_DoesNotResolveDefeat()
        {
            var session = Combat("one"); var player = Unit("a");
            player.Character.LifeState = CharacterLifeState.Downed;
            session.Units.Add(player); session.Units.Add(Unit("enemy", false));
            session.CanReceiveReinforcements = true;
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Ongoing));
            session.CanReceiveReinforcements = false;
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Defeat));
        }

        [Test]
        public void LastLivingParticipantLeaving_ResolvesEscape_NotCampaignDefeat()
        {
            var session = Combat("one"); var player = Unit("a");
            session.Units.Add(player); session.Units.Add(Unit("enemy", false));
            player.Position = new(-1, 0);
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, player), Is.True);
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
            Assert.That(player.Character.CurrentHealth, Is.EqualTo(12));
        }

        [Test]
        public void Factory_UsesOnlyMembersInsideRoom_AndKeepsContactPositions()
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                SetProperty(definition, "Id", "enemy.definition");
                SetProperty(definition, "BaseStats", new CharacterBaseStatsData());
                SetProperty(classData, "Id", "class.warrior");
                SetProperty(classData, "ResourceType", ResourceType.Rage);
                var enemy = new EncounterEnemyData();
                SetProperty(enemy, "InstanceId", "enemy");
                SetProperty(enemy, "CharacterDefinition", definition);
                SetProperty(enemy, "ClassData", classData);
                SetProperty(encounterDefinition, "Enemies", new List<EncounterEnemyData> { enemy });
                var encounter = new DungeonEncounterInstance("one", encounterDefinition, "one", new RectInt(0, 0, 2, 2));
                var game = Game();
                foreach (var id in new[] { "inside", "outside" })
                {
                    var character = Unit(id).Character;
                    character.AvailabilityState = CharacterAvailabilityState.Available;
                    game.TryRegisterCharacter(character);
                    Assert.That(game.TryAddCharacterToActiveParty(id), Is.True);
                }
                var players = new Dictionary<string, Vector2Int>
                    { ["inside"] = Vector2Int.up, ["outside"] = new(8, 8) };
                var enemies = new Dictionary<string, Vector2Int> { ["enemy"] = Vector2Int.right };
                Assert.That(new CombatSessionFactory().TryCreateSeamless(game, encounter,
                    Combat("one").DungeonContext, players, enemies, out var session), Is.True);
                Assert.That(session.Units.Count, Is.EqualTo(2));
                Assert.That(session.Units[0].Character, Is.SameAs(game.CharactersById["inside"]));
                Assert.That(session.Units[0].Position, Is.EqualTo(new GridPosition(0, 1)));
                Assert.That(session.Units[1].Position, Is.EqualTo(new GridPosition(1, 0)));
                Assert.That(game.HasCombat, Is.False, "Factory does not commit a partially constructed session.");
            }
            finally
            {
                Object.DestroyImmediate(definition); Object.DestroyImmediate(classData);
                Object.DestroyImmediate(encounterDefinition);
            }
        }

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void BoundaryDoor_IsIncludedWithoutAddingAdjacentRoomToArena()
        {
            var doorDefinition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var encounterDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "one"); map.TryAddFloorCell(Vector2Int.right, "two");
                floor.TrySetMap(map);
                floor.TryAddObject(new DoorDungeonObjectInstance("door", doorDefinition,
                    new DungeonCellEdge(Vector2Int.zero, Vector2Int.right)));
                var encounter = new DungeonEncounterInstance("one", encounterDefinition, "one", new RectInt(0, 0, 1, 1));
                floor.TryAddEncounter(encounter);
                var dungeon = new DungeonInstance("dungeon", 1); dungeon.TryAddFloor(floor);
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context), Is.True);
                Assert.That(context.MapObjects.Count, Is.EqualTo(1));
                Assert.That(context.WorldCells.Count, Is.EqualTo(1));
                Assert.That(context.ContainsWorldCell(Vector2Int.right), Is.False);
            }
            finally { Object.DestroyImmediate(doorDefinition); Object.DestroyImmediate(encounterDefinition); }
        }

        [Test]
        public void BorrowedCharacter_SurvivesLeavingAndAnotherCombat_WithSameObject()
        {
            var root = new GameObject("shared world test");
            try
            {
                var game = Game(); var first = Combat("one"); var second = Combat("two");
                var unit = Unit("a");
                var view = root.AddComponent<ExplorationCharacterView>();
                var map = root.AddComponent<DungeonFloorMapView>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var objectId = view.gameObject.GetInstanceID();
                spawner.ConfigureDungeon(game, map);
                spawner.Borrow(first, unit, view);
                Assert.That(view.CombatUnit, Is.SameAs(unit));
                Assert.That(spawner.TryGetWorldPosition(unit, out _), Is.True);
                spawner.Release(unit);
                Assert.That(view.CombatUnit, Is.Null);
                spawner.Borrow(second, unit, view);
                Assert.That(view.gameObject.GetInstanceID(), Is.EqualTo(objectId));
                Assert.That(view.CombatUnit, Is.SameAs(unit));
                spawner.Release(unit);
                Assert.That(view, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReleasedEncounter_KeepsSurvivorObjectsAndReplacesDeadChaser()
        {
            var root = new GameObject("encounter reuse test");
            var dead = new GameObject("dead enemy");
            var alive = new GameObject("survivor");
            try
            {
                var trigger = root.AddComponent<EncounterTriggerView>();
                var deadView = dead.AddComponent<ExplorationCharacterView>();
                var survivor = alive.AddComponent<ExplorationCharacterView>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(EncounterTriggerView).GetField("enemyViews", flags).SetValue(trigger,
                    new[] { deadView, survivor });
                typeof(EncounterTriggerView).GetField("chaserView", flags).SetValue(trigger, deadView);
                dead.SetActive(false);
                trigger.ReleaseEncounter();
                Assert.That(trigger.EnemyViews, Is.EqualTo(new[] { survivor }));
                Assert.That(typeof(EncounterTriggerView).GetField("chaserView", flags).GetValue(trigger), Is.SameAs(survivor));
                trigger.ReleaseEncounter();
                Assert.That(trigger.EnemyViews[0], Is.SameAs(survivor));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(dead); Object.DestroyImmediate(alive); }
        }

        [Test]
        public void ObserverReceivesBothSessionsFeedback_AcrossFocusChanges_WithoutDuplicates()
        {
            var root = new GameObject("observer feedback test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", flags).SetValue(host, true);
                typeof(CombatSandboxBootstrap).GetField("unitViewSpawner", flags).SetValue(host, spawner);
                var first = Combat("one"); var second = Combat("two");
                host.TryBegin(first); host.TryBegin(second);
                var a = host.GetDriver(first).FeedbackBus; var b = host.GetDriver(second).FeedbackBus;
                int received = 0;
                host.DungeonFeedback.Published += _ => received++;
                host.Focus(first); host.Focus(null); host.Focus(second); host.Focus(null);
                a.Publish(new CombatFeedbackEvent(Unit("a"), CombatFeedbackType.Damage, 1));
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(2));
                host.End(first);
                a.Publish(new CombatFeedbackEvent(Unit("a"), CombatFeedbackType.Damage, 1));
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(3));
                host.End(second);
                b.Publish(new CombatFeedbackEvent(Unit("b"), CombatFeedbackType.Damage, 1));
                Assert.That(received, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BorrowedMovementContinues_WhenObserverFocusIsExploration()
        {
            var root = new GameObject("background movement test");
            var actor = new GameObject("borrowed actor");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                var map = root.AddComponent<DungeonFloorMapView>();
                typeof(CombatUnitViewSpawner).GetField("sandboxBootstrap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, host);
                typeof(CombatUnitViewSpawner).GetField("battleUnitPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(spawner, root.AddComponent<Godmist.Views.Characters.CharacterView>());
                var view = actor.AddComponent<ExplorationCharacterView>();
                var game = Game(); var session = Combat("one"); var unit = Unit("a");
                session.Units.Add(unit); game.TryRegisterDungeonCombat(session);
                spawner.ConfigureDungeon(game, map); spawner.Borrow(session, unit, view);
                game.TryFocusCombat(null); host.Focus(null);
                spawner.TryBeginPresentationMovement(unit);
                spawner.TryMovePresentationStep(unit, new GridPosition(1, 0));
                typeof(CombatUnitViewSpawner).GetMethod("SynchronizeDungeon", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(spawner, new object[] { .1f });
                Assert.That(view.Position.x, Is.EqualTo(.6f).Within(.001f));
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.True);
                typeof(CombatUnitViewSpawner).GetMethod("SynchronizeDungeon", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(spawner, new object[] { .1f });
                Assert.That(view.Position.x, Is.EqualTo(1f).Within(.001f));
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [TestCase("camera")]
        [TestCase("zoom")]
        [TestCase("unit")]
        [TestCase("removed_unit")]
        public void FloatingFeedback_ReprojectsWorldAnchor_WithoutResettingAnimation(string change)
        {
            var cameraObject = new GameObject("feedback camera");
            var root = new GameObject("feedback root", typeof(RectTransform), typeof(Canvas));
            var label = new GameObject("feedback", typeof(RectTransform));
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.pixelRect = new Rect(0, 0, 1000, 800);
                camera.transform.position = new Vector3(0, 0, -10);
                var rect = (RectTransform)root.transform;
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                label.transform.SetParent(rect, false);
                var view = label.AddComponent<Godmist.Views.UI.CombatFloatingFeedbackView>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = typeof(Godmist.Views.UI.CombatFloatingFeedbackView);
                Vector3? world = new Vector3(2, 1, 0);
                var layout = new Vector2(12, 20); var drift = new Vector2(5, 9);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                    camera.WorldToScreenPoint(world.Value), null, out var initial);
                type.GetField("startPosition", flags).SetValue(view, initial + layout);
                type.GetField("introStartPosition", flags).SetValue(view, initial + layout - Vector2.up * 18f);
                type.GetField("startTime", flags).SetValue(view, 123f);
                ((RectTransform)label.transform).anchoredPosition = initial + layout + drift;
                var lastWorld = world.Value;
                view.BindWorldAnchor(camera, rect, () => world, world.Value, layout);
                if (change == "camera" || change == "removed_unit") camera.transform.position += Vector3.right;
                if (change == "zoom") camera.orthographicSize = 3f;
                if (change == "unit") world += Vector3.up;
                if (change == "removed_unit") world = null;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                    camera.WorldToScreenPoint(world ?? lastWorld), null, out var expected);
                var refresh = type.GetMethod("RefreshWorldAnchor", flags);
                refresh.Invoke(view, null);
                refresh.Invoke(view, null); // Multiple Canvas callbacks must not accumulate offsets.
                Assert.That(Vector2.Distance(view.LayoutPosition, expected + layout), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(((RectTransform)label.transform).anchoredPosition,
                    expected + layout + drift), Is.LessThan(.001f));
                Assert.That(type.GetField("startTime", flags).GetValue(view), Is.EqualTo(123f));
            }
            finally { Object.DestroyImmediate(label); Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void FailedHostStart_DoesNotCreateDriverOrChangeFocus()
        {
            var root = new GameObject("invalid host test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, true);
                Assert.That(host.TryBegin(Combat("one")), Is.False);
                Assert.That(host.Drivers, Is.Empty);
                Assert.That(host.Session, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MultipleDrivers_FocusAndDispose_DoNotDestroyAnotherSession()
        {
            var root = new GameObject("combat host test");
            try
            {
                var host = root.AddComponent<CombatSandboxBootstrap>();
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                typeof(CombatSandboxBootstrap).GetField("seamlessDungeon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, true);
                typeof(CombatSandboxBootstrap).GetField("unitViewSpawner", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, spawner);
                var first = Combat("one"); var second = Combat("two");
                Assert.That(host.TryBegin(first), Is.True);
                Assert.That(host.TryBegin(second), Is.True);
                Assert.That(host.TryBegin(first), Is.False);
                host.Focus(second);
                var secondDispatcher = second.PassiveEffectEventDispatcher;
                host.End(first);
                Assert.That(host.Session, Is.SameAs(second));
                Assert.That(second.PassiveEffectEventDispatcher, Is.SameAs(secondDispatcher));
                host.End(second);
                Assert.That(host.Drivers, Is.Empty);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.TryBegin(Combat("third")), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
