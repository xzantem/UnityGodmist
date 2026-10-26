using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonPatrolDoorTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PatrolActuallyCompletesWaypointsWithProductionCharacterPhysics(bool groundedWall)
        {
            var root = new GameObject("patrol movement", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
            var simulation = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                for (var x = 0; x < 5; x++) for (var y = 0; y < 5; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(map);
                var view = root.AddComponent<DungeonFloorMapView>();
                var tiles = new GameObject("floor", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                void Set(object target, string field, object value) => target.GetType().GetField(field,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
                Set(view, "floorTilemap", tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var party = root.AddComponent<ExplorationPartyController>(); party.ConfigureTraversal(floor, view, null);
                var prefab = AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
                var actor = Object.Instantiate(prefab, root.transform);
                actor.SetEnemyInstanceId("guard");
                Set(actor, "<RuntimeCharacter>k__BackingField", new Godmist.Runtime.Characters.CharacterInstance
                    { CharacterId = "guard", LifeState = CharacterLifeState.Alive });
                actor.ConfigureTerrain(map, view, floor);
                actor.SetIsActiveLeader(false); // DungeonEncounterSpawner disables enemy rigidbody simulation.
                var circle = actor.GetComponent<CircleCollider2D>();
                if (groundedWall)
                {
                    circle.offset = new Vector2(0, circle.radius);
                    var wall = new GameObject("north wall", typeof(BoxCollider2D)); wall.transform.SetParent(root.transform);
                    wall.transform.position = new Vector2(2.5f, 5f);
                    wall.GetComponent<BoxCollider2D>().size = new Vector2(5, DungeonWallSegmentView.DefaultThickness);
                }
                actor.SetPosition(view.CellToWorldCenter(new Vector2Int(1, 4)));
                var encounter = new DungeonEncounterInstance("patrol", definition, "room", new RectInt(0, 0, 5, 5));
                var trigger = root.AddComponent<EncounterTriggerView>();
                Set(trigger, "encounterInstance", encounter); Set(trigger, "enemyViews", new[] { actor });
                var spawner = root.AddComponent<DungeonEncounterSpawner>();
                ((System.Collections.Generic.List<EncounterTriggerView>)typeof(DungeonEncounterSpawner).GetField("spawnedTriggers",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(spawner)).Add(trigger);
                var owner = root.AddComponent<DungeonCombatCoordinator>();
                Set(owner, "floor", floor); Set(owner, "map", view); Set(owner, "party", party); Set(owner, "encounters", spawner);
                Set(owner, "game", new Godmist.Runtime.Game.GameSession());
                var faceOpen = typeof(DungeonCombatCoordinator).GetMethod("FaceOpenPatrolSpace",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                actor.FaceTowards(actor.Position + Vector2.up);
                faceOpen.Invoke(owner, new object[] { actor, true });
                Assert.That(actor.FacingDirection.y, Is.LessThan(0), "A guard at the north edge must turn into the room.");
                actor.FaceTowards(actor.Position + Vector2.right);
                faceOpen.Invoke(owner, new object[] { actor, true });
                Assert.That(actor.FacingDirection.x, Is.GreaterThan(.99f), "Keep a valid saved direction into accessible space.");
                var state = new DungeonPatrolState();
                foreach (var cell in new[] { new Vector2Int(1, 4), new Vector2Int(1, 1), new Vector2Int(3, 1), new Vector2Int(3, 4) })
                    state.Route.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)));
                state.StopIndices.UnionWith(new[] { 0, 1, 2, 3 });
                floor.Patrols.Add(DungeonCombatMembershipSystem.EnemyKey("patrol", "guard"), state);
                Physics2D.SyncTransforms();
                var tick = typeof(DungeonCombatCoordinator).GetMethod("UpdatePatrols",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (var i = 0; i < 2000; i++)
                {
                    Set(owner, "patrolElapsed", .02f);
                    tick.Invoke(owner, null);
                    Physics2D.Simulate(.02f);
                }
                Assert.That(state.Stops, Is.GreaterThanOrEqualTo(8), "Patrol must keep advancing through repeated laps, not just build a valid route.");
            }
            finally { Physics2D.simulationMode = simulation; Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [TestCase(DoorInteractionMode.DirectOnly, true)]
        [TestCase(DoorInteractionMode.DirectAndLever, true)]
        [TestCase(DoorInteractionMode.LeverOnly, false)]
        public void PatrolPlansAcrossDirectDoorAndOpensItOnlyOnApproach(DoorInteractionMode mode, bool opens)
        {
            var root = new GameObject("patrol door", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            try
            {
                var settings = new SerializedObject(definition);
                settings.FindProperty("<InteractionMode>k__BackingField").enumValueIndex = (int)mode;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
                map.TryAddFloorCell(new Vector2Int(12, 0), "other room");
                floor.TrySetMap(map);
                var edge = new DungeonCellEdge(new Vector2Int(2, 0), new Vector2Int(3, 0));
                var door = new DoorDungeonObjectInstance("door", definition, edge); floor.TryAddObject(door);
                var view = root.AddComponent<DungeonFloorMapView>();
                var tiles = new GameObject("floor", typeof(UnityEngine.Tilemaps.Tilemap));
                tiles.transform.SetParent(root.transform);
                var viewSettings = new SerializedObject(view);
                viewSettings.FindProperty("floorTilemap").objectReferenceValue = tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                viewSettings.ApplyModifiedPropertiesWithoutUndo();
                var party = root.AddComponent<ExplorationPartyController>(); party.ConfigureTraversal(floor, view, null);
                var actor = new GameObject("guard").AddComponent<ExplorationCharacterView>();
                actor.transform.SetParent(root.transform); actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(view.CellToWorldCenter(Vector2Int.zero));
                DungeonTraversalPosition At(int x) => new(new DungeonSurfacePosition(new Vector2Int(x, 0)));
                var route = DungeonPatrolPlanner.Build(map, "room", new[] { At(0) }, _ => true, party.CanOpenTraversalDoor).Single();
                Assert.That(route.Any(p => p.Surface.Cell.x > 2), Is.EqualTo(opens));
                Assert.That(route.Any(p => p.Surface.Cell.x == 12), Is.False, "Opening a door does not expand a patrol into another room.");
                Assert.That(door.IsOpen, Is.False, "Planning must not mutate doors.");
                party.OpenTraversalDoor(actor, At(3));
                Assert.That(door.IsOpen, Is.False, "A distant door must not open.");
                actor.SetPosition(view.CellToWorldCenter(new Vector2Int(2, 0)));
                Assert.That(DungeonExplorationPathfinder.TryFindPath(map, At(2), At(3), out _,
                    canOpenDoor: party.CanOpenTraversalDoor), Is.EqualTo(opens));
                party.OpenTraversalDoor(actor, At(3));
                Assert.That(door.IsOpen, Is.EqualTo(opens));
                if (!opens) return;
                Assert.That(map.GetTraversalNeighbours(At(2)), Does.Contain(At(3)));
                party.OpenTraversalDoor(actor, At(3));
                Assert.That(door.IsOpen, Is.True, "Repeated approach must not close an already-open door.");
                floor.TrySetDoorOpen(door.InstanceId, false);
                party.OpenTraversalDoor(actor, At(3));
                Assert.That(door.IsOpen, Is.True, "A door closed during a saved patrol route can be reopened.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }
    }
}
