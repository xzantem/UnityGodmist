using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Data.Dungeons;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonExplorationPathfinderTests
    {
        [TestCase(1f, 0f, true)]
        [TestCase(-1f, 0f, true)]
        [TestCase(0f, 1f, true)]
        [TestCase(0f, -1f, true)]
        [TestCase(1f, 1f, true)]
        [TestCase(1f, -1f, true)]
        [TestCase(-1f, 1f, true)]
        [TestCase(-1f, -1f, true)]
        [TestCase(1.01f, 0f, false)]
        [TestCase(-1.01f, 0f, false)]
        [TestCase(0f, 1.01f, false)]
        [TestCase(0f, -1.01f, false)]
        [TestCase(2f, 2f, false)]
        public void ExplorationReviveOnlyHappensImmediatelyWithinOneUnitIncludingDiagonals(float dx, float dy, bool expected)
        {
            var root = new GameObject("Exploration revive range");
            try
            {
                var party = CreateReviveParty(root, out _, out var rescuer, out var target);
                rescuer.SetPosition(Vector2.zero);
                target.SetPosition(new Vector2(dx, dy));
                var accepted = party.RequestReviveInteraction(target);
                if (expected) Assert.That(accepted, Is.True);
                Assert.That(target.RuntimeCharacter.LifeState, Is.EqualTo(expected
                    ? Godmist.Core.Enums.CharacterLifeState.Alive : Godmist.Core.Enums.CharacterLifeState.Downed));
                Assert.That(target.RuntimeCharacter.CurrentHealth, expected ? Is.GreaterThan(0) : Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplorationReviveInteractionRestoresMemberAndCanRescueDownedLeader(bool downedLeader)
        {
            var root = new GameObject("Exploration revive");
            try
            {
                var party = CreateReviveParty(root, out var game, out var rescuer, out var target);
                if (downedLeader)
                {
                    target.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Alive;
                    Assert.That(game.TrySetActiveLeader(target.CharacterId), Is.True);
                    target.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Downed;
                }
                var before = target.Position;
                target.Move(Vector2.right);
                Assert.That(target.Position, Is.EqualTo(before), "Downed members cannot walk before rescue.");
                target.RuntimeCharacter.ExplorationStatusElapsedSeconds = 20f;
                Assert.That(party.RequestReviveInteraction(target), Is.True);
                Assert.That(target.RuntimeCharacter.LifeState, Is.EqualTo(Godmist.Core.Enums.CharacterLifeState.Alive));
                Assert.That(target.RuntimeCharacter.CurrentHealth, Is.GreaterThan(0));
                Assert.That(target.RuntimeCharacter.ExplorationStatusElapsedSeconds, Is.Zero);
                Assert.That(game.PlayerParty.ActiveLeaderId, Is.EqualTo(rescuer.CharacterId));
                Assert.That(party.CanRequestRevive(target), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplorationReviveApproachUsesDungeonRouteAndCanBeCancelled(bool cancel)
        {
            var root = new GameObject("Revive dungeon route", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var party = CreateReviveParty(root, out _, out var rescuer, out var target);
                ConfigureReviveDungeon(root, party, rescuer, target, tile, false);
                Assert.That(party.RequestReviveInteraction(target), Is.True);
                Assert.That(target.RuntimeCharacter.LifeState, Is.EqualTo(Godmist.Core.Enums.CharacterLifeState.Downed));
                if (cancel) party.CancelMovement();
                var tick = party.GetType().GetMethod("FixedUpdateCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (var i = 0; i < 100; i++) tick.Invoke(party, null);
                Assert.That(target.RuntimeCharacter.LifeState, Is.EqualTo(cancel
                    ? Godmist.Core.Enums.CharacterLifeState.Downed : Godmist.Core.Enums.CharacterLifeState.Alive));
                if (!cancel)
                {
                    var offset = target.Position - rescuer.Position;
                    Assert.That(Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)), Is.LessThanOrEqualTo(1f));
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void ExplorationReviveCannotReachThroughClosedWallOrReviveCombatAndDeadTargets()
        {
            var root = new GameObject("Revive exclusions", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var party = CreateReviveParty(root, out _, out var rescuer, out var target);
                target.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Dead;
                Assert.That(party.RequestReviveInteraction(target), Is.False);
                target.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Downed;
                target.BindCombat(new Godmist.Runtime.Combat.BattleUnit { Character = target.RuntimeCharacter, IsPlayerSide = true });
                Assert.That(party.RequestReviveInteraction(target), Is.False);
                target.ReleaseCombat();
                rescuer.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Downed;
                Assert.That(party.RequestReviveInteraction(target), Is.False);
                rescuer.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Alive;
                ConfigureReviveDungeon(root, party, rescuer, target, tile, true);
                Physics2D.SyncTransforms();
                Assert.That(party.RequestReviveInteraction(target), Is.False);
                Assert.That(target.RuntimeCharacter.LifeState, Is.EqualTo(Godmist.Core.Enums.CharacterLifeState.Downed));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        private static Godmist.Views.Exploration.ExplorationPartyController CreateReviveParty(GameObject root,
            out Godmist.Runtime.Game.GameSession game, out Godmist.Views.Exploration.ExplorationCharacterView rescuer,
            out Godmist.Views.Exploration.ExplorationCharacterView target)
        {
            game = new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Exploration);
            Godmist.Views.Exploration.ExplorationCharacterView Member(string id)
            {
                var view = new GameObject(id).AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                view.transform.SetParent(root.transform);
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { CharacterId = id, CurrentHealth = 10 });
                return view;
            }
            rescuer = Member("rescuer"); target = Member("downed");
            foreach (var view in new[] { rescuer, target })
            {
                Assert.That(game.TryRegisterCharacter(view.RuntimeCharacter), Is.True);
                Assert.That(game.TryAddCharacterToActiveParty(view.CharacterId), Is.True);
            }
            target.RuntimeCharacter.LifeState = Godmist.Core.Enums.CharacterLifeState.Downed;
            target.RuntimeCharacter.CurrentHealth = 0;
            target.SetPosition(Vector2.right);
            var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
            party.Initialize(game, new[] { rescuer, target });
            return party;
        }

        private static void ConfigureReviveDungeon(GameObject root, Godmist.Views.Exploration.ExplorationPartyController party,
            Godmist.Views.Exploration.ExplorationCharacterView rescuer, Godmist.Views.Exploration.ExplorationCharacterView target,
            UnityEngine.Tilemaps.Tile tile, bool blocked)
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 5; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            if (blocked) map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), Godmist.Core.Enums.DungeonWallType.Full);
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
            var tileObject = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tileObject.transform.SetParent(root.transform);
            var mapView = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            mapView.GetType().GetField("floorTilemap", flags).SetValue(mapView, tileObject.GetComponent<UnityEngine.Tilemaps.Tilemap>());
            mapView.GetType().GetField("floorTile", flags).SetValue(mapView, tile);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            mapView.GetType().GetField("wallSegmentPrefab", flags).SetValue(mapView, prefab.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>());
            mapView.Rebuild(map);
            rescuer.ConfigureTerrain(map, mapView, floor); target.ConfigureTerrain(map, mapView, floor);
            rescuer.SetPosition(new Vector2(.5f, .5f)); target.SetPosition(new Vector2(blocked ? 1.5f : 3.5f, .5f));
            party.ConfigureTraversal(floor, mapView, null);
        }

        [Test]
        public void FollowerObstacleIndexMatchesLiveQueriesAndReducesEmptyCellWork()
        {
            var root = new GameObject("follower obstacle index");
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 0; x < 20; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                for (var x = 0; x < 500; x++)
                    floor.TryAddObject(new ObstacleDungeonObjectInstance("distant-" + x, definition, new Vector2Int(x, 10)));
                var blocker = new ObstacleDungeonObjectInstance("blocker", definition, new Vector2Int(10, 0));
                floor.TryAddObject(blocker);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                void Set(string field, object value) => controller.GetType().GetField(field, flags).SetValue(controller, value);
                Set("traversalFloor", floor);
                void Batch(bool enabled)
                {
                    Set("followerNavigationRevisionBatch", enabled);
                    Set("followerNavigationRevisionValid", enabled);
                    Set("followerNavigationRevision", controller.GetType().GetMethod("GetTraversalNavigationRevision", flags).Invoke(controller, null));
                }
                var positions = Enumerable.Range(0, 20).Select(x => new DungeonTraversalPosition(
                    new DungeonSurfacePosition(new Vector2Int(x, 0)))).ToArray();
                var expected = positions.Select(p => controller.CanEnterTraversal(null, p)).ToArray();
                Assert.That(expected[10], Is.False);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < 100; i++) foreach (var p in positions) controller.CanEnterTraversal(null, p);
                var original = timer.Elapsed.TotalMilliseconds;
                Batch(true);
                timer.Restart();
                for (var i = 0; i < 100; i++) foreach (var p in positions) controller.CanEnterTraversal(null, p);
                TestContext.WriteLine($"2000 footprint queries, 501 obstacles: full scan={original:F3}ms, indexed={timer.Elapsed.TotalMilliseconds:F3}ms");
                Assert.That(positions.Select(p => controller.CanEnterTraversal(null, p)), Is.EqualTo(expected));
                var candidates = controller.GetType().GetMethod("NavigationObstaclesAt", flags);
                Assert.That(((IEnumerable<ObstacleDungeonObjectInstance>)candidates.Invoke(controller, new object[] { Vector2Int.zero })), Is.Empty);
                Assert.That(((IEnumerable<ObstacleDungeonObjectInstance>)candidates.Invoke(controller, new object[] { new Vector2Int(10, 0) })).Single(), Is.SameAs(blocker));
                blocker.TryDeactivate();
                Assert.That(controller.CanEnterTraversal(null, positions[10]), Is.True,
                    "Live obstacle state must be checked even inside an existing batch.");
                var added = new ObstacleDungeonObjectInstance("new blocker", definition, Vector2Int.zero);
                floor.TryAddObject(added);
                Batch(false);
                Assert.That(controller.CanEnterTraversal(null, positions[0]), Is.False, "Non-batch queries remain live.");
                Batch(true);
                Assert.That(controller.CanEnterTraversal(null, positions[0]), Is.False, "Next batch sees added objects.");
                var replacement = new DungeonFloorInstance(1); replacement.TrySetMap(map);
                Set("traversalFloor", replacement); Batch(true);
                Assert.That(controller.CanEnterTraversal(null, positions[0]), Is.True, "A new floor must not retain old candidates.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void FollowerObstacleIndexBlocksBothSarcophagusCellsButNotElevatedSurface()
        {
            var root = new GameObject("physical follower index");
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Godmist.Data.Dungeons.DungeonObjectDefinition).GetField("<Id>k__BackingField", flags).SetValue(definition, "object.obstacle.sarcophagus");
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.up, "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.TryAddObject(new ObstacleDungeonObjectInstance("physical", definition, Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.GetType().GetField("traversalFloor", flags).SetValue(controller, floor);
                controller.GetType().GetField("followerNavigationRevisionBatch", flags).SetValue(controller, true);
                controller.GetType().GetField("followerNavigationRevisionValid", flags).SetValue(controller, true);
                controller.GetType().GetField("followerNavigationRevision", flags).SetValue(controller,
                    controller.GetType().GetMethod("GetTraversalNavigationRevision", flags).Invoke(controller, null));
                var north = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.up));
                Assert.That(controller.CanEnterTraversal(null, north), Is.False, "Sarcophagus occupies both ground cells.");
                Assert.That(controller.CanEnterTraversalAtWorld(null, north, Vector2.up), Is.False,
                    "Physical north cell must still require a valid actor footprint.");
                var upper = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.up, 1));
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.up, 1), "upper");
                Assert.That(controller.CanEnterTraversalAtWorld(null, upper, Vector2.up), Is.True,
                    "Ground props must not block an elevated surface.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OffCentreStartCanTurnViaItsOwnAnchorWithoutCrossingWall(bool blockAnchor)
        {
            var root = new GameObject("off-centre-corner", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(22, 5), "room");
                map.TryAddFloorCell(new Vector2Int(22, 6), "room");
                // This exit is clear but leads away from the goal. Exhausting
                // it must not suppress recovery via the current cell anchor.
                map.TryAddFloorCell(new Vector2Int(21, 5), "side-branch");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var wall = new GameObject("corner-wall", typeof(BoxCollider2D));
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector2(21.5f, 6f);
                wall.GetComponent<BoxCollider2D>().size = new Vector2(1.22f, .125f);
                var actorObject = new GameObject("actor", typeof(CircleCollider2D));
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.GetComponent<CircleCollider2D>();
                collider.radius = .196f; collider.offset = new Vector2(0, .2f);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(22.23f, 5.56f));
                var anchor = (Vector2)view.CellToWorldCenter(new Vector2Int(22, 5)) - actor.MovementColliderCenterOffset;
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(22, 6)));
                var targetWorld = (Vector2)view.CellToWorldCenter(new Vector2Int(22, 6)) - actor.MovementColliderCenterOffset;
                if (blockAnchor)
                {
                    var obstacle = new GameObject("blocked-anchor", typeof(BoxCollider2D));
                    obstacle.transform.SetParent(root.transform);
                    obstacle.transform.position = anchor + actor.MovementColliderCenterOffset;
                    obstacle.GetComponent<BoxCollider2D>().size = Vector2.one * .15f;
                }
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                Assert.That(actor.CanStandAt(actor.Position), Is.True,
                    string.Join(";", Physics2D.OverlapCircleAll(actor.Position + actor.MovementColliderCenterOffset, actor.MovementRadius)
                        .Select(c => c.name + ":" + c.bounds + " actor=" + actor.Position + " radius=" + actor.MovementRadius + " offset=" + actor.MovementColliderCenterOffset)));
                Assert.That(actor.HasClearMovementSegment(actor.Position, targetWorld), Is.False);
                var args = new object[] { actor, target, null, targetWorld };
                Assert.That(controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags).Invoke(controller, args), Is.EqualTo(!blockAnchor));
                if (blockAnchor) return;
                var path = (List<DungeonTraversalPosition>)args[2];
                Assert.That((int)controller.GetType().GetField("lastTraversalPathExpandedNodes", flags).GetValue(controller),
                    Is.LessThanOrEqualTo(3), "The reachable side branch must not be exhausted before trying the clear anchor exit.");
                Assert.That(path.Take(2), Is.All.EqualTo(actor.DungeonPosition));
                controller.GetType().GetMethod("SmoothTraversalPath", flags).Invoke(controller,
                    new object[] { actor, target, targetWorld, path, false });
                var route = new Queue<DungeonTraversalPosition>(path.Skip(1));
                var advance = controller.GetType().GetMethod("AdvanceRoute", flags);
                for (var tick = 0; tick < 300 && route.Count > 0; tick++)
                {
                    Assert.That(advance.Invoke(controller, new object[] { actor, route, targetWorld, 0f }), Is.EqualTo(true));
                    Physics2D.SyncTransforms();
                }
                Assert.That(route, Is.Empty);
                Assert.That(Vector2.Distance(actor.Position, targetWorld), Is.LessThan(.04f));
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void TryFindPath_UsesOpenRouteAroundWall()
        {
            var map = CreateSquareMap();

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.True);
            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(1, 0)
            }));
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenWallBlocksOnlyRoute()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenTargetIsOutsideMap()
        {
            var map = CreateSquareMap();

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(3, 3),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void PlanningThroughOpenableDoorDoesNotOpenItOrChangeDefaultMovement()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.right, "room");
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            map.TrySetDoorState(edge, false);
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var end = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _, canOpenDoor: e => e.CellEdge.Equals(edge)), Is.True);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _), Is.False);
            Assert.That(map.CanMoveBetween(Vector2Int.zero, Vector2Int.right), Is.False);
            map.TrySetDoorState(edge, true);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _), Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void DoorPermissionDoesNotBypassWallsOrProgressionGates(bool wall)
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.right, "room");
            var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            map.TrySetDoorState(edge, false);
            if (wall) map.TryAddWall(Vector2Int.zero, Vector2Int.right);
            else map.TrySetProgressionGateState(edge, false);
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map,
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)),
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right)), out _, canOpenDoor: _ => true), Is.False);
        }

        [Test]
        public void TraversalPathAvoidsOccupiedObstacleCellAndRejectsBlockedTarget()
        {
            var map = CreateSquareMap();
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var end = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.one));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out var path,
                p => p.Surface.Cell != Vector2Int.right), Is.True);
            Assert.That(path[1].Surface.Cell, Is.EqualTo(Vector2Int.up));
            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, end, out _, p => p != end), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void NavigationOpensOnlyDirectDoorsAtTheNextStep(bool leverOnly)
        {
            var root = new GameObject("navigation", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
            try
            {
                var mode = new UnityEditor.SerializedObject(definition);
                mode.FindProperty("<InteractionMode>k__BackingField").enumValueIndex = leverOnly ? (int)Godmist.Core.Enums.DoorInteractionMode.LeverOnly : (int)Godmist.Core.Enums.DoorInteractionMode.DirectOnly;
                mode.ApplyModifiedPropertiesWithoutUndo();
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(Vector2Int.right, Vector2Int.right * 2));
                floor.TryAddObject(door); floor.TrySetDoorOpen("door", false);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var mapView = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                typeof(Godmist.Views.Dungeons.DungeonFloorMapView).GetField("floorTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(mapView, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.ConfigureTerrain(map, mapView, floor); actor.SetPosition(mapView.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, mapView, null);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var open = controller.GetType().GetMethod("OpenNextDoor", flags);
                var next = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right * 2));
                open.Invoke(controller, new object[] { actor, next });
                Assert.That(door.IsOpen, Is.False, "Do not remotely open a door beyond the next edge.");
                actor.SetPosition(mapView.CellToWorldCenter(Vector2Int.right));
                open.Invoke(controller, new object[] { actor, next });
                Assert.That(door.IsOpen, Is.EqualTo(!leverOnly));
                Assert.That(map.CanMoveBetween(Vector2Int.right, Vector2Int.right * 2), Is.EqualTo(!leverOnly));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void RouteKeepsWaypointUntilItsCenterAndEndsAtExactFreePosition()
        {
            var root = new GameObject("navigation", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();for(var x=0;x<3;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
                var floor = new DungeonFloorInstance(0);floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap",flags).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();actor.transform.SetParent(root.transform);actor.ConfigureTerrain(map,view,floor);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();controller.ConfigureTraversal(floor,view,null);
                var next = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.right));
                var route = new System.Collections.Generic.Queue<DungeonTraversalPosition>();route.Enqueue(next);
                actor.SetPosition(new Vector2(1.1f,.5f));
                var advance = controller.GetType().GetMethod("AdvanceRoute",flags);
                var final = new Vector2(1.75f,.7f);
                advance.Invoke(controller,new object[]{actor,route,(Vector2?)final,0f});
                Assert.That(route.Count,Is.EqualTo(1),"Entering the cell is not reaching the waypoint.");
                for(var i=0;i<100;i++)advance.Invoke(controller,new object[]{actor,route,(Vector2?)final,0f});
                Assert.That(route.Count,Is.Zero);Assert.That(Vector2.Distance(actor.Position,final),Is.LessThan(.03f));
            }
            finally {Object.DestroyImmediate(root);}
        }

        [Test]
        public void CityRouteUsesColliderClearanceAndRejectsInsideBuilding()
        {
            var root = new GameObject("city-test");
            try
            {
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();actor.transform.SetParent(root.transform);
                var wall = new GameObject("building").AddComponent<BoxCollider2D>();wall.transform.SetParent(root.transform);wall.transform.position=new Vector2(2,0);wall.size=new Vector2(1,2);
                Physics2D.SyncTransforms();
                Assert.That(actor.HasClearMovementSegment(Vector2.zero,new Vector2(4,0)),Is.False);
                actor.Move(new Vector2(4,0));Assert.That(actor.Position.x,Is.LessThan(1.5f));
                var find=typeof(Godmist.Views.Exploration.ExplorationPartyController).GetMethod("TryFindWorldRoute",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var args=new object[]{actor,new Vector2(4,.1f),null};
                Assert.That(find.Invoke(null,args),Is.EqualTo(true));
                var points=(System.Collections.Generic.List<Vector2>)args[2];var previous=actor.Position;
                foreach(var point in points)
                {
                    Assert.That(actor.HasClearMovementSegment(previous,point),Is.True);
                    var segmentDistance = Vector2.Distance(previous, point);
                    var samples = Mathf.Max(1, Mathf.CeilToInt(segmentDistance / .2f));
                    for (var i = 1; i <= samples; i++)
                        Assert.That(actor.CanStandAt(Vector2.Lerp(previous, point, i / (float)samples)), Is.True,
                            $"Route segment {previous}->{point} has blocked intermediate footprint at sample {i}/{samples}.");
                    previous=point;
                }
                Assert.That(points[points.Count-1],Is.EqualTo(new Vector2(4,.1f)));
                Assert.That(find.Invoke(null,new object[]{actor,new Vector2(2,0),null}),Is.EqualTo(false));
            }
            finally{Object.DestroyImmediate(root);Physics2D.SyncTransforms();}
        }

        [Test]
        public void OpenDungeonFollowerKeepsFreeformDirectionAcrossCells()
        {
            var root=new GameObject("free-follow",typeof(Grid));
            try
            {
                var map=new DungeonFloorMap();for(var y=-2;y<5;y++)for(var x=-2;x<5;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);
                var tiles=new GameObject("tiles",typeof(UnityEngine.Tilemaps.Tilemap));tiles.transform.SetParent(root.transform);
                var view=root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap",flags).SetValue(view,tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var follower=new GameObject("follower").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();follower.transform.SetParent(root.transform);follower.Bind(new Godmist.Runtime.Characters.CharacterInstance{CharacterId="f"});follower.ConfigureTerrain(map,view,floor);follower.SetPosition(new Vector2(.3f,.7f));
                var leader=new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();leader.transform.SetParent(root.transform);leader.ConfigureTerrain(map,view,floor);leader.SetPosition(new Vector2(3.2f,2.1f));
                var controller=root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();controller.ConfigureTraversal(floor,view,null);
                var start=follower.Position;var expected=(leader.Position-start).normalized;
                controller.GetType().GetMethod("FollowTraversal",flags).Invoke(controller,new object[]{follower,leader});
                Assert.That(Vector2.Dot((follower.Position-start).normalized,expected),Is.GreaterThan(.999f));
                map.TryAddWall(Vector2Int.zero,Vector2Int.right);
                Assert.That(controller.GetType().GetMethod("CanFollowDirectly",flags).Invoke(controller,new object[]{follower,leader}),Is.EqualTo(false));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void AdjacentRootMovementFallsBackForOffsetFootprintEndpoints()
        {
            var root = new GameObject("offset-footprint-follow", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var collider = followerObject.AddComponent<CircleCollider2D>();
                collider.radius = .1f;
                collider.offset = new Vector2(.49f, 0f);
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, collider);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(new Vector2(.01f, .5f));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(new Vector2(1.99f, .5f));
                Assert.That(follower.DungeonPosition.Surface.Cell, Is.EqualTo(Vector2Int.zero));
                Assert.That(leader.DungeonPosition.Surface.Cell, Is.EqualTo(Vector2Int.right));
                Assert.That(view.WorldToCell(follower.Position + follower.MovementColliderCenterOffset), Is.EqualTo(Vector2Int.zero));
                Assert.That(view.WorldToCell(leader.Position + follower.MovementColliderCenterOffset), Is.EqualTo(new Vector2Int(2, 0)),
                    "The regression setup must exercise a footprint endpoint delta of two cells despite adjacent root cells.");

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                var canFollowDirectly = controller.GetType().GetMethod("CanFollowDirectly", flags);
                Assert.That(canFollowDirectly.Invoke(controller, new object[] { follower, leader }), Is.EqualTo(true));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AdjacentMovementWithFeetOffsetDoesNotRequireWallPaddingFloor()
        {
            var root = new GameObject("feet-offset-adjacent", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(6, 7), "room");
                map.TryAddFloorCell(new Vector2Int(7, 7), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var wallTemplate = new GameObject("wall-template", typeof(Godmist.Views.Dungeons.DungeonWallSegmentView));
                wallTemplate.transform.SetParent(root.transform);
                view.GetType().GetField("wallSegmentPrefab", flags)
                    .SetValue(view, wallTemplate.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var collider = followerObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, collider);
                follower.ConfigureTerrain(map, view, floor);
                collider.offset = new Vector2(0f, .2f);
                follower.SetPosition(new Vector2(7.5f, 7.5f));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(new Vector2(6.5f, 7.5f));

                Assert.That(follower.MovementRadius, Is.EqualTo(.196f).Within(.0001f));
                Assert.That(follower.MovementColliderCenterOffset.y, Is.EqualTo(.2f).Within(.0001f));
                Assert.That(follower.DungeonPosition.Surface.Cell, Is.EqualTo(new Vector2Int(7, 7)));
                Assert.That(leader.DungeonPosition.Surface.Cell, Is.EqualTo(new Vector2Int(6, 7)));

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                var canFollowDirectly = controller.GetType().GetMethod("CanFollowDirectly", flags);
                Assert.That(canFollowDirectly.Invoke(controller, new object[] { follower, leader }), Is.EqualTo(true),
                    "The runtime footprint has room to cross the adjacent floor; wall thickness must not demand an extra floor cell.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void OffsetFootprintCanPlanThroughNarrowOpenCorridor()
        {
            var root = new GameObject("offset-narrow-corridor", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 6; x <= 8; x++) map.TryAddFloorCell(new Vector2Int(x, 11), "corridor");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var wall = new GameObject("north-corridor-wall");
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector2(7.5f, 12.04f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(2.8f, .18f);

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(6.5f, 11.3f));
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(8, 11)));
                var targetWorld = new Vector2(8.5f, 11.3f);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();

                Assert.That(actor.HasClearMovementSegment(actor.Position, targetWorld), Is.True,
                    "The offset collider should fit beneath the north wall.");
                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var arguments = new object[] { actor, target, null, targetWorld };
                Assert.That(find.Invoke(controller, arguments), Is.EqualTo(true),
                    "Planning must use the collider anchor instead of rejecting the corridor cell at its raw centre.");
                var path = (List<DungeonTraversalPosition>)arguments[2];
                Assert.That(path[path.Count - 1], Is.EqualTo(target));
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerUsesSafeStopGoalWhenExactLeaderPointIsOutsideFootprint()
        {
            var root = new GameObject("follower-stop-goal", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(5, 4), "room");
                map.TryAddFloorCell(new Vector2Int(5, 5), "room");
                map.TryAddFloorCell(new Vector2Int(5, 6), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var collider = followerObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, collider);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(new Vector2(5.42f, 5.92f));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(new Vector2(5.19f, 4.88f));

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();

                var target = leader.DungeonPosition;
                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var invalidArguments = new object[] { follower, target, null, leader.Position };
                Assert.That(find.Invoke(controller, invalidArguments), Is.EqualTo(false),
                    "The exact leader point is outside the follower's circle footprint.");
                var expanded = (int)controller.GetType().GetField("lastTraversalPathExpandedNodes", flags).GetValue(controller);
                Assert.That(expanded, Is.EqualTo(0),
                    "An invalid endpoint must be rejected before any map nodes are expanded.");

                var canFollow = controller.GetType().GetMethod("CanFollowDirectly", flags);
                Assert.That(canFollow.Invoke(controller, new object[] { follower, leader }), Is.EqualTo(true),
                    "The one-cell follower stop point is valid even though the exact leader point is not.");

                var start = follower.Position;
                controller.GetType().GetMethod("FollowTraversal", flags).Invoke(
                    controller, new object[] { follower, leader });
                Assert.That(Vector2.Distance(follower.Position, start), Is.GreaterThan(.001f),
                    "Execution must use the resolved stop goal instead of repeatedly retrying the invalid exact endpoint.");
                Assert.That(Vector2.Distance(follower.Position, leader.Position), Is.GreaterThanOrEqualTo(.99f),
                    "Follower execution must preserve the configured stop distance.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerSelectsAlternateStopRingPointWhenStraightStopEntersVoid()
        {
            var root = new GameObject("follower-stop-ring", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 2; y <= 8; y++) for (var x = 2; x <= 8; x++)
                    if (new Vector2Int(x, y) != new Vector2Int(4, 5)) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var collider = followerObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, collider);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(new Vector2(3.42f, 5.92f));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(new Vector2(5.19f, 4.88f));

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();

                var resolve = controller.GetType().GetMethods(flags)
                    .Single(method => method.Name == "TryResolveFollowerGoal" && method.GetParameters().Length == 5);
                var arguments = new object[] { follower, leader, null, null, false };
                Assert.That(resolve.Invoke(controller, arguments), Is.EqualTo(true));
                var goal = (DungeonTraversalPosition)arguments[2];
                var goalWorld = (Vector2)arguments[3];
                Assert.That(goal.Surface.Cell, Is.Not.EqualTo(new Vector2Int(4, 5)),
                    "The straight stop point enters the missing cell and must not be selected.");
                Assert.That(Vector2.Distance(goalWorld, leader.Position), Is.EqualTo(1f).Within(.001f),
                    "An alternate goal must preserve the follower stop radius.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerRevalidatesCachedStopGoalWhenObstacleAppears()
        {
            var root = new GameObject("follower-stop-cache", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 1; y <= 4; y++) for (var x = 1; x <= 7; x++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var followerCollider = followerObject.AddComponent<CircleCollider2D>();
                followerCollider.radius = .196f;
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, followerCollider);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(view.CellToWorldCenter(new Vector2Int(1, 2)));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(view.CellToWorldCenter(new Vector2Int(4, 2)));

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();

                var cachedResolve = controller.GetType().GetMethods(flags)
                    .Single(method => method.Name == "TryResolveFollowerGoal" && method.GetParameters().Length == 7);
                var routeType = controller.GetType().GetNestedType("FollowerRoute", flags);
                var route = System.Activator.CreateInstance(routeType, true);
                var revisionMethod = controller.GetType().GetMethod("GetTraversalNavigationRevision", flags);
                var revision = (int)revisionMethod.Invoke(controller, null);
                var firstArguments = new object[] { follower, leader, route, revision, null, null, false };
                Assert.That(cachedResolve.Invoke(controller, firstArguments), Is.EqualTo(true));
                var firstGoalWorld = (Vector2)firstArguments[5];

                var obstacle = new GameObject("appeared-at-stop-goal");
                obstacle.transform.SetParent(root.transform);
                obstacle.transform.position = firstGoalWorld;
                obstacle.AddComponent<BoxCollider2D>().size = new Vector2(.35f, .35f);
                Physics2D.SyncTransforms();

                var secondArguments = new object[] { follower, leader, route, revision, null, null, false };
                Assert.That(cachedResolve.Invoke(controller, secondArguments), Is.EqualTo(true),
                    "A newly blocked cached stop point must be replaced by another validated ring point.");
                var secondGoalWorld = (Vector2)secondArguments[5];
                Assert.That(secondGoalWorld, Is.Not.EqualTo(firstGoalWorld),
                    "The follower must not reuse a physically invalid cached goal.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerSkipsBlockedSameCellGoalForSafeOuterRingAlternative()
        {
            var root = new GameObject("follower-stop-same-cell", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 1; y <= 5; y++) for (var x = 1; x <= 5; x++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var followerObject = new GameObject("follower");
                followerObject.transform.SetParent(root.transform);
                var followerCollider = followerObject.AddComponent<CircleCollider2D>();
                followerCollider.radius = .196f;
                var follower = followerObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.GetType().GetField("movementCollider", flags).SetValue(follower, followerCollider);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(new Vector2(3.15f, 3.5f));

                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(new Vector2(4.8f, 3.5f));

                var blockedSameCell = leader.Position
                    + (follower.Position - leader.Position).normalized;
                var blocker = new GameObject("blocked-same-cell-goal");
                blocker.transform.SetParent(root.transform);
                blocker.transform.position = (blockedSameCell + follower.Position) * .5f;
                blocker.AddComponent<BoxCollider2D>().size = new Vector2(.08f, .2f);

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                Assert.That(view.WorldToCell(blockedSameCell), Is.EqualTo(follower.DungeonPosition.Surface.Cell),
                    "The blocked candidate must remain in the follower's current logical cell.");
                Assert.That(follower.CanStandAt(blockedSameCell), Is.True,
                    "The candidate endpoint itself must be physically valid.");
                Assert.That(follower.HasClearMovementSegment(follower.Position, blockedSameCell), Is.False,
                    "Only the segment to the same-cell candidate is blocked by the thin barrier.");

                var resolve = controller.GetType().GetMethods(flags)
                    .Single(method => method.Name == "TryResolveFollowerGoal" && method.GetParameters().Length == 5);
                var arguments = new object[] { follower, leader, null, null, false };
                Assert.That(resolve.Invoke(controller, arguments), Is.EqualTo(true));
                var goal = (DungeonTraversalPosition)arguments[2];
                var goalWorld = (Vector2)arguments[3];
                Assert.That(goalWorld, Is.Not.EqualTo(blockedSameCell),
                    "A same-cell stop point whose segment is blocked must be replaced by a safe outer-ring point.");
                Assert.That(follower.HasClearMovementSegment(follower.Position, goalWorld), Is.True,
                    "The selected outer-ring goal must have a clear physical segment from the follower.");
                Assert.That(Vector2.Distance(goalWorld, leader.Position), Is.EqualTo(1f).Within(.02f),
                    "The replacement must preserve the configured follower spacing when an alternate ring point is available.");
                TestContext.WriteLine($"same-cell blocked={blockedSameCell} fallback={goal} world={goalWorld}");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerWaitsWhenNoSafeStopRingExistsInsteadOfPlanningExactLeaderPoint()
        {
            var root = new GameObject("follower-stop-no-ring", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 1; x <= 3; x++) map.TryAddFloorCell(new Vector2Int(x, 2), "corridor");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var follower = new GameObject("follower").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.transform.SetParent(root.transform);
                follower.Bind(new Godmist.Runtime.Characters.CharacterInstance { CharacterId = "follower-no-ring" });
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(view.CellToWorldCenter(new Vector2Int(1, 2)));
                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor);
                leader.SetPosition(view.CellToWorldCenter(new Vector2Int(3, 2)));

                // The only one-cell stop point is occupied. All other points on
                // both bounded rings fall outside this one-cell-wide corridor.
                var blocker = new GameObject("blocked-stop-cell");
                blocker.transform.SetParent(root.transform);
                blocker.transform.position = view.CellToWorldCenter(new Vector2Int(2, 2));
                blocker.AddComponent<BoxCollider2D>().size = new Vector2(.8f, .8f);

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                var expanded = controller.GetType().GetField("lastTraversalPathExpandedNodes", flags);
                expanded.SetValue(controller, -1);
                controller.GetType().GetMethod("FollowTraversal", flags).Invoke(
                    controller, new object[] { follower, leader });

                Assert.That(expanded.GetValue(controller), Is.EqualTo(-1),
                    "No safe follower goal must wait before invoking the any-angle planner.");
                var routes = (System.Collections.IDictionary)controller.GetType()
                    .GetField("followerRoutes", flags).GetValue(controller);
                var route = routes[follower.CharacterId];
                var routeFlags = flags | System.Reflection.BindingFlags.Public;
                Assert.That((bool)route.GetType().GetField("HasFailedPlan", routeFlags).GetValue(route), Is.True,
                    "The unavailable follower goal must be cached without creating an unsafe route.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void FollowerDoesNotTreatSameWorldPointOnAnotherLevelAsAtStopDistance()
        {
            var root = new GameObject("follower-level-distance", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 0), "ground");
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "upper");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var follower = new GameObject("follower").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                follower.transform.SetParent(root.transform);
                follower.ConfigureTerrain(map, view, floor);
                follower.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var previous = new GameObject("previous").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                previous.transform.SetParent(root.transform);
                previous.ConfigureTerrain(map, view, floor);
                previous.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                previous.GetType().GetField("localTraversalPosition", flags).SetValue(
                    previous, new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero, 1)));

                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                var canFollow = controller.GetType().GetMethod("CanFollowDirectly", flags);
                Assert.That(canFollow.Invoke(controller, new object[] { follower, previous }), Is.EqualTo(false),
                    "Equal XY positions on different levels must still enter traversal planning.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StartFootprintOverlapCanRecoverTowardValidAdjacentTarget()
        {
            var root = new GameObject("start-footprint-recovery", typeof(Grid));
            ObstacleDungeonObjectDefinition obstacleDefinition = null;
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(6, 7), "room");
                map.TryAddFloorCell(new Vector2Int(7, 7), "room");
                // Exercise the layered runtime path used by a map that contains stairs or
                // another elevated surface, while keeping this route on level zero.
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(20, 20), 1), "upper");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
                Assert.That(floor.TryAddObject(new ObstacleDungeonObjectInstance(
                    "logical-start-obstacle", obstacleDefinition, new Vector2Int(6, 7))), Is.True);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(6.81f, 7.63f));
                var obstacleView = new GameObject("narrow-logical-start-obstacle");
                obstacleView.transform.SetParent(root.transform);
                obstacleView.transform.position = view.CellToWorldCenter(new Vector2Int(6, 7));
                obstacleView.AddComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(7, 7)));
                var targetWorld = new Vector2(7.33f, 7.53f);
                var blockedStart = actor.DungeonPosition;
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                // This fixture is not initialized through the party registry, so wire the
                // same traversal predicate that ConfigureTraversal assigns to live actors.
                actor.CanEnterTraversal = position => controller.CanEnterTraversal(actor, position);
                Physics2D.SyncTransforms();
                Assert.That(actor.UsesLayeredTraversal, Is.True,
                    "The execution assertion must exercise layered MoveFreeAxis.");
                Assert.That(controller.CanEnterTraversal(actor, blockedStart), Is.False,
                    "The logical start cell must remain blocked by the dungeon obstacle.");
                Assert.That(actor.CanStandAt(actor.Position), Is.True,
                    "The actor's actual collider position must remain physically clear.");

                var direct = controller.GetType().GetMethod(
                    "CanNavigateDirectly", flags, null,
                    new[] { typeof(Godmist.Views.Exploration.ExplorationCharacterView), typeof(DungeonTraversalPosition), typeof(DungeonTraversalPosition), typeof(Vector2) }, null);
                Assert.That(direct.Invoke(controller, new object[] { actor, blockedStart, target, targetWorld }), Is.EqualTo(true),
                    "A minimal overlap above the start floor may recover toward a valid adjacent target.");
                Assert.That(direct.Invoke(controller, new object[] { actor, blockedStart, blockedStart, new Vector2(6.81f, 8.2f) }), Is.EqualTo(false),
                    "Start recovery must not permit movement farther into the missing floor cell.");

                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var blockedTargetArguments = new object[] { actor, blockedStart, null, actor.Position };
                Assert.That(find.Invoke(controller, blockedTargetArguments), Is.EqualTo(false),
                    "The physically clear fragment of a blocked start cell must not become a valid click target.");
                var arguments = new object[] { actor, target, null, targetWorld };
                Assert.That(find.Invoke(controller, arguments), Is.EqualTo(true));
                var path = (List<DungeonTraversalPosition>)arguments[2];
                Assert.That(path, Is.EqualTo(new[] { actor.DungeonPosition, target }));

                var route = new Queue<DungeonTraversalPosition>();
                route.Enqueue(target);
                var advance = controller.GetType().GetMethod("AdvanceRoute", flags);
                var before = actor.Position;
                Assert.That(advance.Invoke(controller, new object[] { actor, route, (Vector2?)targetWorld, 0f }), Is.EqualTo(true));
                Assert.That(Vector2.Distance(actor.Position, before), Is.GreaterThan(.001f),
                    "Execution must leave the recovered start instead of cancelling the route.");

                // Re-entry through the blocked logical cell must be rejected by the same
                // callback used by MoveFreeAxis, even when its physical collider is narrow.
                actor.SetPosition(view.CellToWorldCenter(new Vector2Int(7, 7)));
                actor.Move(Vector2.left * 1f);
                Assert.That(actor.DungeonPosition, Is.EqualTo(target));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(obstacleDefinition); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void BlockedLogicalStartWithSeparatingInitialOverlapCanLeaveAndRejectReentry()
        {
            var root = new GameObject("blocked-start-separating-overlap", typeof(Grid));
            ObstacleDungeonObjectDefinition obstacleDefinition = null;
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(5, 6), "room");
                map.TryAddFloorCell(new Vector2Int(5, 7), "room");
                map.TryAddFloorCell(new Vector2Int(4, 7), "room");
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(20, 20), 1), "upper");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
                Assert.That(floor.TryAddObject(new ObstacleDungeonObjectInstance(
                    "logical-start-overlap", obstacleDefinition, new Vector2Int(5, 6))), Is.True);

                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(5.79f, 6.93f));

                // The collider overlaps only the edge of the actor's feet circle. The
                // requested target moves away from that contact, matching runtime's
                // separating initial-overlap case while the logical cell remains blocked.
                var obstacleView = new GameObject("narrow-start-overlap");
                obstacleView.transform.SetParent(root.transform);
                obstacleView.transform.position = new Vector2(6.03f, 7.13f);
                obstacleView.AddComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);

                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(5, 7)));
                var targetWorld = new Vector2(5.06f, 7.13f);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                actor.CanEnterTraversal = position => controller.CanEnterTraversal(actor, position);
                Physics2D.SyncTransforms();

                Assert.That(actor.DungeonPosition.Surface.Cell, Is.EqualTo(new Vector2Int(5, 6)));
                Assert.That(controller.CanEnterTraversal(actor, actor.DungeonPosition), Is.False,
                    "The occupied logical start must stay blocked in the domain model.");
                Assert.That(actor.CanStandAt(actor.Position), Is.False,
                    "The fixture must reproduce the initial collider overlap.");
                Assert.That(actor.HasClearMovementSegment(actor.Position, targetWorld), Is.True,
                    "The requested departure must separate the initial overlap.");

                var direct = controller.GetType().GetMethod(
                    "CanNavigateDirectly", flags, null,
                    new[] { typeof(Godmist.Views.Exploration.ExplorationCharacterView), typeof(DungeonTraversalPosition), typeof(DungeonTraversalPosition), typeof(Vector2) }, null);
                Assert.That(direct.Invoke(controller, new object[] { actor, actor.DungeonPosition, target, targetWorld }), Is.EqualTo(true),
                    "A safe separating departure must be accepted from the occupied current cell.");

                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var arguments = new object[] { actor, target, null, targetWorld };
                Assert.That(find.Invoke(controller, arguments), Is.EqualTo(true));
                var route = new Queue<DungeonTraversalPosition>(); route.Enqueue(target);
                var advance = controller.GetType().GetMethod("AdvanceRoute", flags);
                var before = actor.Position;
                Assert.That(advance.Invoke(controller, new object[] { actor, route, (Vector2?)targetWorld, 0f }), Is.EqualTo(true));
                Assert.That(actor.Position.x, Is.LessThan(before.x),
                    "Execution must leave the initial overlap toward the safe target.");

                actor.SetPosition(view.CellToWorldCenter(new Vector2Int(5, 7)));
                actor.Move(Vector2.down);
                Assert.That(actor.DungeonPosition, Is.EqualTo(target),
                    "Layered movement may separate within the current cell, but must reject re-entry into the occupied logical cell.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(obstacleDefinition); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void MultiWaypointClickRouteExecutesAroundPillar()
        {
            var root = new GameObject("pillar-route", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 0; y < 5; y++) for (var x = 0; x < 5; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var pillar = new GameObject("pillar");
                pillar.transform.SetParent(root.transform);
                pillar.transform.position = view.CellToWorldCenter(new Vector2Int(2, 2));
                pillar.AddComponent<BoxCollider2D>().size = new Vector2(.65f, .65f);

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var actorCollider = actorObject.AddComponent<CircleCollider2D>();
                actorCollider.radius = .196f;
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, actorCollider);
                actor.ConfigureTerrain(map, view, floor);
                var startCell = new Vector2Int(1, 2);
                var targetCell = new Vector2Int(3, 2);
                actor.SetPosition(view.CellToWorldCenter(startCell));
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(targetCell));
                var targetWorld = (Vector2)view.CellToWorldCenter(targetCell);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                TestContext.WriteLine($"pillar fixture actor={actor.Position} pillar={pillar.transform.position} target={targetWorld} direct={actor.HasClearMovementSegment(actor.Position, targetWorld)}");
                Assert.That(actor.HasClearMovementSegment(actor.Position, targetWorld), Is.False,
                    "The fixture must make the straight click segment collide with the pillar.");

                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var arguments = new object[] { actor, target, null, targetWorld };
                Assert.That(find.Invoke(controller, arguments), Is.EqualTo(true));
                var path = (List<DungeonTraversalPosition>)arguments[2];
                Assert.That(path.Count, Is.GreaterThan(2), "The pillar must require multiple route waypoints.");

                var smooth = controller.GetType().GetMethod("SmoothTraversalPath", flags);
                smooth.Invoke(controller, new object[] { actor, target, targetWorld, path, false });
                TestContext.WriteLine($"smoothed path={string.Join(" -> ", path)}");
                Assert.That(path[path.Count - 1], Is.EqualTo(target),
                    "Smoothing must keep the logical target after the final safe turn.");
                var route = new Queue<DungeonTraversalPosition>();
                foreach (var step in path.Skip(1)) route.Enqueue(step);
                var advance = controller.GetType().GetMethod("AdvanceRoute", flags);
                for (var iteration = 0; iteration < 400 && route.Count > 0; iteration++)
                {
                    var advanced = (bool)advance.Invoke(controller, new object[] { actor, route, (Vector2?)targetWorld, 0f });
                    Assert.That(advanced, Is.True,
                        $"Execution rejected waypoint at iteration {iteration}; actor={actor.Position} cell={actor.DungeonPosition} remaining={route.Count} next={(route.Count > 0 ? route.Peek().ToString() : "<empty>")}");
                }

                Assert.That(route, Is.Empty, "Execution must consume every smoothed waypoint around the pillar.");
                Assert.That(Vector2.Distance(actor.Position, targetWorld), Is.LessThan(.1f));
                Assert.That(actor.HasClearMovementSegment(actor.Position, actor.Position), Is.True);
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void AdjacentBlockedDirectSegmentPlansDetourButRejectsOverlappingExactGoal()
        {
            var root = new GameObject("adjacent-blocked-direct", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 0; y < 5; y++)
                    for (var x = 0; x < 5; x++)
                        map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var pillar = new GameObject("adjacent-direct-pillar");
                pillar.transform.SetParent(root.transform);
                pillar.transform.position = new Vector2(2.5f, 2.5f);
                pillar.AddComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .196f;
                collider.offset = new Vector2(0f, .2f);
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(new Vector2(1.5f, 2.3f));

                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(2, 2)));
                var safeGoal = new Vector2(2.85f, 2.3f);
                var overlappingGoal = new Vector2(2.55f, 2.3f);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();
                Assert.That(actor.HasClearMovementSegment(actor.Position, safeGoal), Is.False,
                    "The adjacent target must have a blocked straight segment.");
                Assert.That(actor.CanStandAt(safeGoal), Is.True,
                    "The exact clicked goal remains physically valid beyond the pillar.");

                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var safeArguments = new object[] { actor, target, null, safeGoal };
                Assert.That(find.Invoke(controller, safeArguments), Is.EqualTo(true),
                    "A blocked adjacent direct segment must still search for a safe detour.");
                var path = (List<DungeonTraversalPosition>)safeArguments[2];
                Assert.That(path.Count, Is.GreaterThan(2), "The pillar must force an intermediate detour node.");

                var blockedArguments = new object[] { actor, target, null, overlappingGoal };
                Assert.That(find.Invoke(controller, blockedArguments), Is.EqualTo(false),
                    "An exact pointer inside the pillar must remain rejected before planning.");
                Assert.That((int)controller.GetType().GetField("lastTraversalPathExpandedNodes", flags).GetValue(controller), Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void PhysicsSegmentAllowsSeparatingInitialObstacleOverlapOnly()
        {
            var root = new GameObject("initial-obstacle-overlap", typeof(Grid));
            try
            {
                var obstacle = new GameObject("obstacle");
                obstacle.transform.SetParent(root.transform);
                obstacle.transform.position = new Vector2(.24f, 0f);
                obstacle.AddComponent<BoxCollider2D>().size = new Vector2(.4f, .4f);

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var body = actorObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.simulated = true;
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .2f;
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.GetType().GetField("body", flags).SetValue(actor, body);
                actor.SetPosition(Vector2.zero);
                Physics2D.SyncTransforms();

                Assert.That(actor.HasClearMovementSegment(Vector2.zero, new Vector2(-.15f, 0f)), Is.True,
                    "A cast may leave a small initial overlap when its endpoint reduces penetration.");
                Assert.That(actor.HasClearMovementSegment(Vector2.zero, new Vector2(.15f, 0f)), Is.False,
                    "An initial overlap must still block movement deeper into the obstacle.");

                var hasWalkableSegment = typeof(Godmist.Views.Exploration.ExplorationPartyController)
                    .GetMethod("HasWalkableCitySegment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(hasWalkableSegment.Invoke(null, new object[] { actor, Vector2.zero, new Vector2(-.15f, 0f) }),
                    Is.EqualTo(false),
                    "Initial-overlap recovery must still reject a short segment whose only sample is not standable.");
                Assert.That(hasWalkableSegment.Invoke(null, new object[] { actor, Vector2.zero, new Vector2(-.25f, 0f) }),
                    Is.EqualTo(true),
                    "Initial-overlap recovery may pass through an overlapping first sample only when a later sample reaches clear ground.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void ExactTargetWorldAvoidsBlockedCellCenterNearWall()
        {
            var root = new GameObject("exact-target-wall", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                map.TryAddFloorCell(Vector2Int.right, "room");
                map.TryAddFloorCell(new Vector2Int(1, 1), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());

                var wall = new GameObject("wall-near-cell-center");
                wall.transform.SetParent(root.transform);
                wall.transform.position = view.CellToWorldCenter(new Vector2Int(1, 1));
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.18f, .18f);

                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .1f;
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                collider.offset = Vector2.zero;
                actor.SetPosition(new Vector2(.8f, .8f));
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(1, 1)));
                var exactTarget = new Vector2(1.8f, 1.2f);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                Physics2D.SyncTransforms();

                var find = controller.GetType().GetMethod("FindTraversalPathWithTargetWorld", flags);
                var arguments = new object[] { actor, target, null, exactTarget };
                Assert.That(find.Invoke(controller, arguments), Is.EqualTo(true));
                var path = (List<DungeonTraversalPosition>)arguments[2];
                Assert.That(path, Has.Count.EqualTo(2),
                    "Planning must use the reachable pointer endpoint instead of the blocked cell center.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void DiagonalCornerChecksOnlyTheIntermediateCellCrossedBySegment()
        {
            var root = new GameObject("ordered-corner", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                map.TryAddFloorCell(Vector2Int.right, "room");
                map.TryAddFloorCell(new Vector2Int(1, 1), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actorObject = new GameObject("actor");
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.AddComponent<CircleCollider2D>();
                collider.radius = .1f;
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.GetType().GetField("movementCollider", flags).SetValue(actor, collider);
                actor.ConfigureTerrain(map, view, floor);
                collider.offset = Vector2.zero;
                actor.SetPosition(new Vector2(.8f, .8f));
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(1, 1)));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.ConfigureTraversal(floor, view, null);
                var direct = controller.GetType().GetMethod(
                    "CanNavigateDirectly", flags, null,
                    new[] { typeof(Godmist.Views.Exploration.ExplorationCharacterView), typeof(DungeonTraversalPosition), typeof(DungeonTraversalPosition), typeof(Vector2) }, null);

                Assert.That(direct.Invoke(controller, new object[] { actor, actor.DungeonPosition, target, new Vector2(1.8f, 1.2f) }), Is.EqualTo(true),
                    "The segment crosses the existing east intermediate cell first.");
                Assert.That(direct.Invoke(controller, new object[] { actor, actor.DungeonPosition, target, new Vector2(1.5f, 1.5f) }), Is.EqualTo(false),
                    "A true corner crossing must still require both supercover cells.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AnyAnglePathChoosesShorterSafeDetourThanCardinalRoute()
        {
            var map = new DungeonFloorMap();
            for (var y = 0; y < 8; y++) for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            var blocked = new HashSet<Vector2Int> { new(7, 4), new(9, 3) };
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(0, 3)));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(11, 3)));
            var canEnter = new System.Func<DungeonTraversalPosition, bool>(position => !blocked.Contains(position.Surface.Cell));

            Assert.That(DungeonExplorationPathfinder.TryFindPath(map, start, target, out var cardinal, canEnter), Is.True);
            var cardinalLength = TraversalPathLength(map, cardinal);
            var knownSafeLength = Vector2.Distance(new Vector2(0, 3), new Vector2(10, 2))
                + Vector2.Distance(new Vector2(10, 2), new Vector2(11, 3));
            Assert.That(cardinalLength, Is.GreaterThan(knownSafeLength + .5f));

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var anyAngle, canEnter,
                canNavigateDirectly: (first, second) => HasSafeGridLine(map, blocked, first, second)), Is.True);
            Assert.That(TraversalPathLength(map, anyAngle), Is.LessThanOrEqualTo(knownSafeLength + .01f),
                "Any-angle planning should select the shorter safe detour rather than smoothing the longer BFS route.");
            Assert.That(anyAngle[0], Is.EqualTo(start));
            Assert.That(anyAngle[anyAngle.Count - 1], Is.EqualTo(target));
        }

        [Test]
        public void AnyAnglePathKeepsTwoTurnCorridorDiagonalsSafe()
        {
            var map = new DungeonFloorMap();
            for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            for (var y = 1; y <= 4; y++) map.TryAddWall(new Vector2Int(2, y), new Vector2Int(3, y));
            for (var x = 3; x <= 6; x++) map.TryAddWall(new Vector2Int(x, 4), new Vector2Int(x, 5));
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(0, 2)));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(7, 6)));
            var direct = new System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool>(
                (first, second) => HasSafeGridLine(map, new HashSet<Vector2Int>(), first, second));
            var knownSafeLength = Vector2.Distance(new Vector2(0, 2), new Vector2(2, 0))
                + 1f + Vector2.Distance(new Vector2(3, 0), new Vector2(7, 3)) + 3f;

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var path, canNavigateDirectly: direct), Is.True);
            Assert.That(TraversalPathLength(map, path), Is.LessThanOrEqualTo(knownSafeLength + .01f));
            for (var index = 1; index < path.Count; index++)
                Assert.That(direct(path[index - 1], path[index]), Is.True);
        }

        [Test]
        public void AnyAnglePathDoesNotCutBlockedDiagonalCorner()
        {
            var map = CreateSquareMap();
            var blocked = new HashSet<Vector2Int> { Vector2Int.right };
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.one));

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var path,
                position => !blocked.Contains(position.Surface.Cell)), Is.True);
            Assert.That(path, Is.EqualTo(new[]
            {
                start,
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.up)),
                target
            }));
        }

        [Test]
        public void AnyAnglePathKeepsClosedDoorOutOfDiagonalShortcut()
        {
            var map = CreateSquareMap();
            var door = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
            map.TrySetDoorState(door, false);
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.one));

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var closedPath, canOpenDoor: _ => true), Is.True);
            Assert.That(closedPath, Is.EqualTo(new[]
            {
                start,
                new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.up)),
                target
            }));

            map.TrySetDoorState(door, true);
            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var openPath, canOpenDoor: _ => true), Is.True);
            Assert.That(openPath, Is.EqualTo(new[] { start, target }));
        }

        [Test]
        public void AnyAnglePathKeepsAuthoredStairStepsAdjacent()
        {
            var map = new DungeonFloorMap();
            var authored = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) };
            var first = new DungeonSurfacePosition(authored[0]);
            var second = new DungeonSurfacePosition(authored[authored.Length - 1], 1);
            map.TryAddFloorCell(first, "ground");
            map.TryAddFloorCell(second, "upper");
            Assert.That(map.TryAddStairs(first, second, authored), Is.True);

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map,
                new DungeonTraversalPosition(first),
                new DungeonTraversalPosition(second),
                out var path), Is.True);
            Assert.That(path.Exists(position => position.IsOnStairs), Is.True);
            for (var index = 1; index < path.Count; index++)
                Assert.That(map.GetTraversalNeighbours(path[index - 1]).Contains(path[index]), Is.True);
        }

        [Test]
        public void AnyAnglePathDoesNotShortcutClosedDoor()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 4; x++) for (var y = 0; y < 2; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            var door = new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(2, 0));
            map.TrySetDoorState(door, false);
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(0, 0)));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(3, 0)));
            var direct = new System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool>(
                (first, second) => HasSafeGridLine(map, new HashSet<Vector2Int>(), first, second));

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var closedPath, canOpenDoor: _ => true, canNavigateDirectly: direct), Is.True);
            Assert.That(closedPath.Count, Is.GreaterThan(2));

            map.TrySetDoorState(door, true);
            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(
                map, start, target, out var openPath, canOpenDoor: _ => true, canNavigateDirectly: direct), Is.True);
            Assert.That(openPath, Is.EqualTo(new[] { start, target }));
        }

        [Test]
        public void LeaderTraversalSmoothingDropsOpenGridZigZag()
        {
            var root = new GameObject("smooth-open", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, view, null);
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(3, 3)));
                var path = new List<DungeonTraversalPosition>
                {
                    new(new DungeonSurfacePosition(Vector2Int.zero)),
                    new(new DungeonSurfacePosition(new Vector2Int(0, 1))),
                    new(new DungeonSurfacePosition(new Vector2Int(1, 1))),
                    new(new DungeonSurfacePosition(new Vector2Int(2, 2))),
                    target
                };

                controller.GetType().GetMethod("SmoothTraversalPath", flags).Invoke(
                    controller,
                    new object[] { actor, target, view.CellToWorldCenter(new Vector2Int(3, 3)), path, false });

                Assert.That(path, Has.Count.EqualTo(2), "A direct diagonal retains the logical target for exact-pointer execution.");
                Assert.That(path[1], Is.EqualTo(target));

                path = new List<DungeonTraversalPosition>
                {
                    new(new DungeonSurfacePosition(Vector2Int.zero)),
                    new(new DungeonSurfacePosition(new Vector2Int(0, 1))),
                    new(new DungeonSurfacePosition(new Vector2Int(1, 1))),
                    target
                };
                controller.GetType().GetMethod("SmoothTraversalPath", flags).Invoke(
                    controller,
                    new object[] { actor, target, view.CellToWorldCenter(new Vector2Int(3, 3)), path, true });
                Assert.That(path, Has.Count.EqualTo(2), "Follower fallback keeps the target cell as its final waypoint.");
                Assert.That(path[1], Is.EqualTo(target));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator LeaderTraversalSmoothingKeepsRouteAroundColliderAndClosedDoor()
        {
            var root = new GameObject("smooth-blocked", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 0; y < 3; y++) for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var startCell = new Vector2Int(0, 1);
                var targetCell = new Vector2Int(3, 1);
                var doorEdge = new DungeonCellEdge(startCell, new Vector2Int(1, 1));
                map.TrySetDoorState(doorEdge, false);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(startCell));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, view, null);
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(targetCell));
                var path = new List<DungeonTraversalPosition>
                {
                    new(new DungeonSurfacePosition(startCell)),
                    new(new DungeonSurfacePosition(new Vector2Int(0, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(1, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(2, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(3, 0))),
                    target
                };
                var method = controller.GetType().GetMethod("SmoothTraversalPath", flags);
                var findMethod = controller.GetType().GetMethod("FindTraversalPath", flags);
                var findArguments = new object[] { actor, target, null };
                Assert.That(findMethod.Invoke(controller, findArguments), Is.EqualTo(true));
                var closedPlan = (List<DungeonTraversalPosition>)findArguments[2];
                Assert.That(closedPlan.Count, Is.GreaterThan(2), "The planner must keep a closed door as a route boundary.");

                method.Invoke(controller, new object[] { actor, target, view.CellToWorldCenter(targetCell), path, false });
                Assert.That(path, Has.Count.GreaterThan(1), "A closed door must remain a route boundary.");

                map.TrySetDoorState(doorEdge, true);
                findArguments = new object[] { actor, target, null };
                Assert.That(findMethod.Invoke(controller, findArguments), Is.EqualTo(true));
                var openPlan = (List<DungeonTraversalPosition>)findArguments[2];
                Assert.That(openPlan.Count, Is.EqualTo(2), "An open door can be included in the direct planner route.");
                path = new List<DungeonTraversalPosition> { new(new DungeonSurfacePosition(startCell)), new(new DungeonSurfacePosition(new Vector2Int(1, 1))), new(new DungeonSurfacePosition(new Vector2Int(2, 1))), target };
                method.Invoke(controller, new object[] { actor, target, view.CellToWorldCenter(targetCell), path, false });
                Assert.That(path, Has.Count.EqualTo(2), "An open door can be included in a direct route that retains its logical target.");

                var obstacle = new GameObject("obstacle"); obstacle.transform.SetParent(root.transform); obstacle.transform.position = view.CellToWorldCenter(new Vector2Int(2, 1));
                var collider = obstacle.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.6f, .6f);
                Physics2D.SyncTransforms();
                path = new List<DungeonTraversalPosition>
                {
                    new(new DungeonSurfacePosition(startCell)),
                    new(new DungeonSurfacePosition(new Vector2Int(0, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(1, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(2, 0))),
                    new(new DungeonSurfacePosition(new Vector2Int(3, 0))),
                    target
                };
                findArguments = new object[] { actor, target, null };
                Assert.That(findMethod.Invoke(controller, findArguments), Is.EqualTo(true));
                var obstaclePlan = (List<DungeonTraversalPosition>)findArguments[2];
                Assert.That(TraversalPathLength(map, obstaclePlan), Is.GreaterThan(TraversalPathLength(map, openPlan)));
                var nativeRoutes = (System.Collections.IDictionary)controller.GetType().GetField("meshRoutes", flags).GetValue(controller);
                // Runtime topology/prop changes now schedule a native bake.
                // Keep the safe immediate fallback assertions above, then wait
                // for the native route instead of requiring a blocking rebuild.
                for(var attempt=0;attempt<3 && nativeRoutes[actor]==null;attempt++)
                {
                    var mesh=(Godmist.Views.Exploration.DungeonNavigationMesh)controller.GetType().GetMethod("GetNavigationMesh",flags).Invoke(controller,new object[]{actor,0});
                    var deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
                    while(!mesh.IsReady && UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                    Assert.That(mesh.IsReady,Is.True,"The scheduled native bake must complete.");
                    findArguments=new object[]{actor,target,null};
                    Assert.That(findMethod.Invoke(controller,findArguments),Is.EqualTo(true));
                }
                var nativeRoute = nativeRoutes[actor];
                Assert.That(nativeRoute, Is.Not.Null, "A newly spawned physical obstacle must refresh the native mesh.");
                var points = (Queue<Vector2>)nativeRoute.GetType().GetField("Points").GetValue(nativeRoute);
                var previous = actor.Position;
                foreach (var point in points)
                {
                    Assert.That(actor.HasClearMovementSegment(previous, point), Is.True,
                        "Exact native corners must remain clear after a physical prop appears.");
                    previous = point;
                }
                method.Invoke(controller, new object[] { actor, target, view.CellToWorldCenter(targetCell), path, false });
                Assert.That(path, Has.Count.GreaterThan(1), "The actor footprint must keep a collider out of the direct segment.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void AdvanceRouteExecutesNonAdjacentSameLevelWaypoint()
        {
            var root = new GameObject("smooth-execution", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(0, 4), 1), "upper");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                Assert.That(actor.UsesLayeredTraversal, Is.True);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, view, null);
                var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(3, 3)));
                var smoothedPath = new List<DungeonTraversalPosition>
                {
                    new(new DungeonSurfacePosition(Vector2Int.zero)),
                    target
                };
                var smoothing = controller.GetType().GetMethod("SmoothTraversalPath", flags);
                var exactPointer = view.CellToWorldCenter(new Vector2Int(3, 3)) + new Vector2(.2f, .15f);
                smoothing.Invoke(controller, new object[] { actor, target, exactPointer, smoothedPath, false });
                Assert.That(smoothedPath, Has.Count.EqualTo(2),
                    "Layered traversal keeps the target waypoint so final pointer movement cannot select a stair implicitly.");
                var findArguments = new object[] { actor, target, null };
                Assert.That(controller.GetType().GetMethod("FindTraversalPath", flags).Invoke(controller, findArguments), Is.EqualTo(true));
                var plannedPath = (List<DungeonTraversalPosition>)findArguments[2];
                var route = new Queue<DungeonTraversalPosition>();
                for (var index = 1; index < plannedPath.Count; index++) route.Enqueue(plannedPath[index]);
                var start = actor.Position;
                var startTraversal = actor.DungeonPosition;

                var result = controller.GetType().GetMethod("AdvanceRoute", flags).Invoke(
                    controller,
                    new object[] { actor, route, null, 0f });

                Assert.That(result, Is.EqualTo(true));
                Assert.That(route, Has.Count.EqualTo(1), "A valid smoothed waypoint must remain active until reached.");
                Assert.That(actor.Position.x, Is.GreaterThan(start.x));
                Assert.That(actor.Position.y, Is.GreaterThan(start.y));
                Assert.That(actor.DungeonPosition, Is.EqualTo(startTraversal),
                    "Layered movement must advance through an adjacent steering cell instead of jumping to the waypoint.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ExplorationClickAcceptsUndiscoveredWalkableCellWithoutChangingFog()
        {
            var root = new GameObject("fog-click", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                view.ApplyFog(floor);

                var character = new Godmist.Runtime.Characters.CharacterInstance
                {
                    CharacterId = "fog-leader",
                    AvailabilityState = Godmist.Core.Enums.CharacterAvailabilityState.Available,
                    LifeState = Godmist.Core.Enums.CharacterLifeState.Alive
                };
                var session = new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Exploration);
                Assert.That(session.TryRegisterCharacter(character), Is.True);
                Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
                Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo(character.CharacterId));

                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.transform.SetParent(root.transform);
                actor.Bind(character); actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.Initialize(session, new[] { actor }); controller.ConfigureTraversal(floor, view, null);

                var targetCell = new Vector2Int(3, 0);
                var targetWorld = (Vector3)view.CellToWorldCenter(targetCell) + new Vector3(.15f, .1f);
                var revision = floor.Fog.Revision;
                var setDestination = controller.GetType().GetMethod("TrySetTraversalDestination", flags);
                setDestination.Invoke(controller, new object[] { targetWorld });

                var destination = controller.GetType().GetField("movementDestination", flags).GetValue(controller);
                Assert.That(destination, Is.EqualTo((Vector2)targetWorld), "An undiscovered walkable click must start exploration movement.");
                Assert.That(floor.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(targetCell))), Is.EqualTo(DungeonKnowledge.Unexplored));
                Assert.That(floor.Fog.Revision, Is.EqualTo(revision), "Clicking a hidden floor must not reveal fog.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ExplorationClickRejectsVoidAndBlockedTarget()
        {
            var root = new GameObject("fog-click-blocked", typeof(Grid));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                map.TryAddFloorCell(Vector2Int.right, "room");
                map.TryAddWall(Vector2Int.zero, Vector2Int.right);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                view.ApplyFog(floor);
                var character = new Godmist.Runtime.Characters.CharacterInstance
                {
                    CharacterId = "blocked-leader",
                    AvailabilityState = Godmist.Core.Enums.CharacterAvailabilityState.Available,
                    LifeState = Godmist.Core.Enums.CharacterLifeState.Alive
                };
                var session = new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Exploration);
                session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.transform.SetParent(root.transform);
                actor.Bind(character); actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.Initialize(session, new[] { actor }); controller.ConfigureTraversal(floor, view, null);
                var setDestination = controller.GetType().GetMethod("TrySetTraversalDestination", flags);

                setDestination.Invoke(controller, new object[] { (Vector3)view.CellToWorldCenter(new Vector2Int(2, 0)) });
                Assert.That(controller.GetType().GetField("movementDestination", flags).GetValue(controller), Is.Null,
                    "A click on void must not create a movement destination.");

                setDestination.Invoke(controller, new object[] { (Vector3)view.CellToWorldCenter(Vector2Int.right) });
                Assert.That(controller.GetType().GetField("movementDestination", flags).GetValue(controller), Is.Null,
                    "A wall-blocked floor cell must not create a movement destination.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ExplorationClickOnUnknownProjectedUpperSurfaceUsesLeaderLevel()
        {
            var root = new GameObject("fog-click-level", typeof(Grid));
            var floorTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            var wallTemplate = new GameObject("wall-template", typeof(Godmist.Views.Dungeons.DungeonWallSegmentView), typeof(SpriteRenderer));
            try
            {
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "ground");
                map.TryAddFloorCell(Vector2Int.right, "ground");
                map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, 1), "upper");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                view.GetType().GetField("floorTile", flags).SetValue(view, floorTile);
                view.GetType().GetField("wallSegmentPrefab", flags).SetValue(view, wallTemplate.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>());
                view.Rebuild(map); view.ApplyFog(floor); view.FollowElevation(1, true);

                var character = new Godmist.Runtime.Characters.CharacterInstance
                {
                    CharacterId = "level-leader",
                    AvailabilityState = Godmist.Core.Enums.CharacterAvailabilityState.Available,
                    LifeState = Godmist.Core.Enums.CharacterLifeState.Alive
                };
                var session = new Godmist.Runtime.Game.GameSession(Godmist.Core.Enums.GameModeState.Exploration);
                session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.transform.SetParent(root.transform);
                actor.Bind(character); actor.ConfigureTerrain(map, view, floor); actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.Initialize(session, new[] { actor }); controller.ConfigureTraversal(floor, view, null);

                var resolve = controller.GetType().GetMethod("TryResolveExplorationTarget", flags);
                var arguments = new object[] { actor, (Vector3)view.CellToWorldCenter(Vector2Int.right), null };
                Assert.That(resolve.Invoke(controller, arguments), Is.EqualTo(true));
                var target = (DungeonTraversalPosition)arguments[2];
                Assert.That(target.Surface.Cell, Is.EqualTo(Vector2Int.right));
                Assert.That(target.Surface.Level, Is.EqualTo(0), "An unexplored projected upper surface must not change the actor's traversal level.");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(wallTemplate); Object.DestroyImmediate(floorTile);
            }
        }

        [Test]
        public void AnyAnglePathMemoizesExpensiveDirectChecksWithinOneSearch()
        {
            var map = new DungeonFloorMap();
            for (var y = 0; y < 12; y++) for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            map.TryAddWall(new Vector2Int(5, 0), new Vector2Int(5, 1));
            var start = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(0, 0)));
            var target = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(11, 11)));
            var calls = 0;
            var pairs = new HashSet<(DungeonTraversalPosition First, DungeonTraversalPosition Second)>();
            var blocked = new HashSet<Vector2Int>();
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> direct = (first, second) =>
            {
                calls++;
                pairs.Add((first, second));
                return HasSafeGridLine(map, blocked, first, second);
            };

            Assert.That(DungeonExplorationPathfinder.TryFindAnyAnglePath(map, start, target, out var path,
                canNavigateDirectly: direct), Is.True);
            Assert.That(path, Is.Not.Empty);
            Assert.That(calls, Is.EqualTo(pairs.Count), "The same expensive direct segment should be evaluated once per search.");
            TestContext.WriteLine($"Any-angle direct callback checks: {calls}");
        }

        [Test]
        public void FollowerFailedRouteIsCachedAndReplansAfterTopologyRevision()
        {
            var root = new GameObject("follower-failed-route", typeof(Grid));
            ObstacleDungeonObjectDefinition obstacleDefinition = null;
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                var gate = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
                map.TrySetProgressionGateState(gate, false);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
                var obstacle = new ObstacleDungeonObjectInstance("follower-obstacle", obstacleDefinition, Vector2Int.right);
                Assert.That(floor.TryAddObject(obstacle), Is.True);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                view.GetType().GetField("floorTilemap", flags).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var follower = new GameObject("follower").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); follower.transform.SetParent(root.transform);
                follower.Bind(new Godmist.Runtime.Characters.CharacterInstance { CharacterId = "follower" });
                follower.ConfigureTerrain(map, view, floor); follower.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                var leader = new GameObject("leader").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); leader.transform.SetParent(root.transform);
                leader.ConfigureTerrain(map, view, floor); leader.SetPosition(view.CellToWorldCenter(new Vector2Int(2, 0)));
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); controller.ConfigureTraversal(floor, view, null);
                var follow = controller.GetType().GetMethod("FollowTraversal", flags);

                follow.Invoke(controller, new object[] { follower, leader });
                var routes = (System.Collections.IDictionary)controller.GetType().GetField("followerRoutes", flags).GetValue(controller);
                var route = routes[follower.CharacterId];
                var routeType = route.GetType();
                var routeFlags = flags | System.Reflection.BindingFlags.Public;
                var nextPlanField = routeType.GetField("NextPlan", routeFlags);
                Assert.That((bool)routeType.GetField("HasFailedPlan", routeFlags).GetValue(route), Is.True);
                nextPlanField.SetValue(route, 0f);
                leader.SetPosition(view.CellToWorldCenter(new Vector2Int(2, 0)) + Vector2.right * .01f);

                follow.Invoke(controller, new object[] { follower, leader });
                Assert.That((float)nextPlanField.GetValue(route), Is.EqualTo(0f),
                    "A target-world jitter below the cache tolerance must not launch another full search.");

                nextPlanField.SetValue(route, 0f);
                leader.SetPosition(view.CellToWorldCenter(new Vector2Int(2, 0)) + Vector2.right * .15f);
                follow.Invoke(controller, new object[] { follower, leader });
                Assert.That((float)nextPlanField.GetValue(route), Is.Not.EqualTo(0f),
                    "A meaningful target-world change inside the same cell must invalidate the failed result.");

                Assert.That(map.TrySetProgressionGateState(gate, true), Is.True);
                follow.Invoke(controller, new object[] { follower, leader });
                Assert.That((bool)routeType.GetField("HasFailedPlan", routeFlags).GetValue(route), Is.True,
                    "Opening the gate must invalidate the old failed result and retry against the remaining obstacle.");

                nextPlanField.SetValue(route, 0f);
                follower.SetPosition(view.CellToWorldCenter(Vector2Int.zero) + Vector2.right * .1f);
                follow.Invoke(controller, new object[] { follower, leader });
                Assert.That((float)nextPlanField.GetValue(route), Is.Not.EqualTo(0f),
                    "A meaningful follower start-position change inside the same cell must invalidate the failed result.");

                Assert.That(obstacle.TryRestoreIsActive(false), Is.True);
                follow.Invoke(controller, new object[] { follower, leader });
                Assert.That((bool)routeType.GetField("HasFailedPlan", routeFlags).GetValue(route), Is.False,
                    "Deactivating an obstacle must invalidate the failed result and permit replanning.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(obstacleDefinition);
            }
        }

        private static DungeonFloorMap CreateSquareMap()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");
            map.TryAddFloorCell(new Vector2Int(0, 1), "room");
            map.TryAddFloorCell(new Vector2Int(1, 1), "room");

            return map;
        }

        private static float TraversalPathLength(DungeonFloorMap map, List<DungeonTraversalPosition> path)
        {
            var length = 0f;
            for (var index = 1; index < path.Count; index++)
            {
                map.TryGetTraversalCell(path[index - 1], out var first);
                map.TryGetTraversalCell(path[index], out var second);
                length += Vector2.Distance(first, second);
            }

            return length;
        }

        private static bool HasSafeGridLine(
            DungeonFloorMap map,
            HashSet<Vector2Int> blocked,
            DungeonTraversalPosition first,
            DungeonTraversalPosition second)
        {
            if (first.IsOnStairs || second.IsOnStairs || first.Surface.Level != second.Surface.Level) return false;
            var distance = Vector2.Distance(first.Surface.Cell, second.Surface.Cell);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance * 20f));
            var previous = first.Surface.Cell;
            for (var index = 0; index <= samples; index++)
            {
                var cell = Vector2Int.RoundToInt(Vector2.Lerp(first.Surface.Cell, second.Surface.Cell, (float)index / samples));
                if (!map.IsWalkable(cell) || blocked.Contains(cell)) return false;
                if (cell != previous)
                {
                    var delta = cell - previous;
                    if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                    {
                        if (!map.CanTraverseBetweenSurfaces(new DungeonSurfacePosition(previous), new DungeonSurfacePosition(cell))) return false;
                    }
                    else if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
                    {
                        var corner = new Vector2Int(cell.x, previous.y);
                        var other = new Vector2Int(previous.x, cell.y);
                        if (blocked.Contains(corner) || blocked.Contains(other)
                            || !map.CanTraverseBetweenSurfaces(new DungeonSurfacePosition(previous), new DungeonSurfacePosition(corner))
                            || !map.CanTraverseBetweenSurfaces(new DungeonSurfacePosition(corner), new DungeonSurfacePosition(cell))
                            || !map.CanTraverseBetweenSurfaces(new DungeonSurfacePosition(previous), new DungeonSurfacePosition(other))
                            || !map.CanTraverseBetweenSurfaces(new DungeonSurfacePosition(other), new DungeonSurfacePosition(cell))) return false;
                    }
                }

                previous = cell;
            }

            return true;
        }
    }
}
