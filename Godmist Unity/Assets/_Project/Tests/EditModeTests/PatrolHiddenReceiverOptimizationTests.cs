using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Data.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class PatrolHiddenReceiverOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var field = type.GetField(name, Flags); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static T Read<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
        static T Bind<T>(object owner, string name) where T : Delegate => (T)owner.GetType().GetMethod(name, Flags).CreateDelegate(typeof(T), owner);

        [Test]
        public void ForestCacheMatchesEveryFootprintAndInvalidatesGrowthRoomPolicyAndMapBinding()
        {
            var root = new GameObject("Forest footprint cache");
            try
            {
                var party = root.AddComponent<ExplorationPartyController>();
                var query = Bind<Func<DungeonFloorMap, Vector2Int, string, bool, bool>>(party, "CanOccupyForestCell");
                var map = new DungeonFloorMap();
                for (int y = 0; y < 9; y++) for (int x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, y), x < 9 ? "a" : "b");
                for (int pass = 0; pass < 2; pass++)
                for (int y = -1; y <= 9; y++) for (int x = -1; x <= 12; x++)
                foreach (var ent in new[] { false, true }) foreach (var room in new[] { "a", "b", null })
                {
                    var cell = new Vector2Int(x, y);
                    Assert.That(query(map, cell, room, ent), Is.EqualTo(ForestEnemyNavigation.CanOccupy(map, cell, room, ent)));
                }
                int before = party.ForestOccupancyEvaluationCount;
                for (int i = 0; i < 100; i++) Assert.That(query(map, new Vector2Int(4, 4), "a", true), Is.True);
                Assert.That(party.ForestOccupancyEvaluationCount, Is.EqualTo(before));
                var growth = new DungeonFloorMap();
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) if (x != 2 || y != 2) growth.TryAddFloorCell(new Vector2Int(x, y), "a");
                Assert.That(query(growth, Vector2Int.one, "a", false), Is.False);
                growth.TryAddFloorCell(new Vector2Int(2, 2), "a");
                Assert.That(query(growth, Vector2Int.one, "a", false), Is.True);
                Assert.That(query(map, new Vector2Int(8, 4), "a", true), Is.False, "Ent room constraints cannot reuse a regular mob result.");
                Assert.That(query(map, new Vector2Int(8, 4), "a", false), Is.True);
                party.ConfigureTraversal(null, null, null);
                Assert.That(Read<IDictionary>(party, "forestOccupancy").Count, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ReceiverBatchDeduplicatesWithoutLosingSortingUpgradesOrForeignBlocks(bool forest)
        {
            var root = new GameObject("Receiver batch");
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero, 4);
            try
            {
                var terrain = root.AddComponent<DungeonFloorMapView>();
                if (forest) terrain.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var rig = root.AddComponent<DungeonSilhouetteLighting>(); Set(rig, "terrain", terrain); Set(rig, "floorMaterial", material);
                var floorRenderer = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<TilemapRenderer>(); floorRenderer.transform.SetParent(root.transform);
                Set(rig, "floorReceiver", floorRenderer); Set(rig, "receiverFloorOrder", 10);
                var apply = Bind<Action<SpriteRenderer, bool, bool>>(rig, "ApplyGroundLighting");
                var sources = new SpriteRenderer[12];
                for (int i = 0; i < sources.Length; i++)
                {
                    sources[i] = new GameObject("Source").AddComponent<SpriteRenderer>(); sources[i].transform.SetParent(root.transform); sources[i].sprite = sprite;
                    var block = new MaterialPropertyBlock(); block.SetFloat("_External", i + 1); sources[i].SetPropertyBlock(block);
                }
                Set(rig, "receiverSortingBatch", true);
                for (int pass = 0; pass < 3; pass++) foreach (var source in sources) apply(source, false, false);
                Assert.That(rig.ReceiverPreparationCount, Is.EqualTo(12));
                Assert.That(rig.ReceiverSpriteInputReadCount, Is.EqualTo(1));
                apply(sources[0], true, true);
                Assert.That(sources[0].sortingOrder, Is.EqualTo(12)); Assert.That(rig.ReceiverPreparationCount, Is.EqualTo(13));
                foreach (var source in sources)
                {
                    var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block);
                    Assert.That(block.GetTexture("_MainTex"), Is.EqualTo(texture));
                    Assert.That(block.GetFloat("_External"), Is.GreaterThan(0));
                    if (forest) Assert.That(block.GetVector("_ForestPropTexel"), Is.EqualTo(new Vector4(.25f, .25f, 0, 0)));
                }
                // A new refresh always reads the live MPB, even for a prepared renderer.
                Read<IDictionary>(rig, "preparedReceivers").Clear(); Read<IDictionary>(rig, "receiverSpriteInputs").Clear();
                var foreign = new MaterialPropertyBlock(); foreign.SetFloat("_External", 99); sources[0].SetPropertyBlock(foreign);
                apply(sources[0], true, true); sources[0].GetPropertyBlock(foreign);
                Assert.That(foreign.GetFloat("_External"), Is.EqualTo(99)); Assert.That(foreign.GetTexture("_MainTex"), Is.EqualTo(texture));
                Read<IDictionary>(rig, "preparedReceivers").Clear(); Read<IDictionary>(rig, "receiverSpriteInputs").Clear();
                Assert.That(texture.Reinitialize(8, 8), Is.True);
                apply(sources[1], false, false); sources[1].GetPropertyBlock(foreign);
                if (forest) Assert.That(foreign.GetVector("_ForestPropTexel"), Is.EqualTo(new Vector4(.125f, .125f, 0, 0)));
                Set(rig, "receiverSortingBatch", false);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PatrolQueryResultsCannotLeakIntoNextTickAfterPhysicalBlockerChanges()
        {
            var type = typeof(PatrolMaterialOptimizationTests).GetNestedType("PatrolFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, true);
            var owner = Read<DungeonCombatCoordinator>(fixture, "Owner");
            var actor = Read<ExplorationCharacterView>(fixture, "Actor");
            var root = Read<GameObject>(fixture, "Root");
            var query = Bind<Func<ExplorationCharacterView, DungeonTraversalPosition, bool>>(owner, "CanEnterPatrolQuery");
            var next = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(2, 1)));
            Assert.That(query(actor, next), Is.True);
            var blocker = new GameObject("New physical blocker", typeof(BoxCollider2D)); blocker.transform.SetParent(root.transform);
            blocker.transform.position = new Vector3(2.5f, 1.5f); Physics2D.SyncTransforms();
            // Even a tick below the movement cadence must close its previous batch.
            Bind<Action<float>>(owner, "UpdatePatrolsForElapsed")(0);
            Assert.That(Read<IDictionary>(owner, "patrolEnterability").Count, Is.Zero);
            Assert.That(query(actor, next), Is.False);
            blocker.GetComponent<BoxCollider2D>().enabled = false;
            Bind<Action<float>>(owner, "UpdatePatrolsForElapsed")(0);
            Assert.That(query(actor, next), Is.True);
        }

        [Test]
        public void UnknownObjectSkipsRepeatedPresentationButKeepsCollisionAndWakesForFogDebugAndExternalChanges()
        {
            var root = new GameObject("Unknown object", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var tile = ScriptableObject.CreateInstance<Tile>(); var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
            try
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var terrain = root.AddComponent<DungeonFloorMapView>(); Set(terrain, "floorTilemap", tiles.GetComponent<Tilemap>()); Set(terrain, "floorTile", tile);
                Set(terrain, "wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "room"); terrain.Rebuild(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map); terrain.ApplyFog(floor);
                var instance = new ObstacleDungeonObjectInstance("obstacle", definition, Vector2Int.zero); floor.TryAddObject(instance);
                var art = new GameObject("Obstacle", typeof(SpriteRenderer), typeof(BoxCollider2D)); art.transform.SetParent(root.transform);
                var renderer = art.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var obstacle = art.AddComponent<DungeonObstacleView>(); Set(obstacle, "blockingCollider", art.GetComponent<BoxCollider2D>());
                obstacle.Initialize(instance, terrain.CellToWorldCenter(Vector2Int.zero)); obstacle.BindElevationView(terrain);
                var tick = Bind<Action>(obstacle, "LateUpdate"); tick();
                for (int i = 0; i < 20; i++) tick();
                Assert.That(obstacle.HiddenPresentationSkipCount, Is.EqualTo(20)); Assert.That(renderer.forceRenderingOff, Is.True);
                Assert.That(art.GetComponent<BoxCollider2D>().enabled, Is.EqualTo(instance.BlocksMovement));
                renderer.forceRenderingOff = false; tick(); Assert.That(renderer.forceRenderingOff, Is.True);
                floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)) });
                floor.Fog.Objects[instance.InstanceId] = new DungeonObjectKnowledge { Active = true }; terrain.ApplyFog(floor); tick();
                Assert.That(renderer.forceRenderingOff, Is.False); int skips = obstacle.HiddenPresentationSkipCount;
                tick(); Assert.That(obstacle.HiddenPresentationSkipCount, Is.EqualTo(skips));
                floor.Fog.Objects.Clear(); terrain.ApplyFog(floor); tick(); tick(); Assert.That(renderer.forceRenderingOff, Is.True);
                floor.Fog.ToggleDebugFog(); terrain.ApplyFog(floor); tick(); Assert.That(renderer.forceRenderingOff, Is.False);
                floor.Fog.ToggleDebugFog(); terrain.ApplyFog(floor); tick(); tick(); instance.TryDeactivate(); tick();
                Assert.That(art.GetComponent<BoxCollider2D>().enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); Object.DestroyImmediate(tile); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void BatchedCameraQueryMatchesIndividualQueriesThroughPanZoomAndRendererMotion()
        {
            var root = new GameObject("Camera queries", typeof(Grid));
            var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
            try
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var terrain = root.AddComponent<DungeonFloorMapView>(); Set(terrain, "floorTilemap", tiles.GetComponent<Tilemap>());
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform); camera.orthographic = true; camera.aspect = 1; camera.orthographicSize = 3;
                terrain.ConfigurePresentationCamera(camera);
                var renderers = new Renderer[8];
                for (int i = 0; i < 8; i++) { var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>(); renderer.transform.SetParent(root.transform); renderer.sprite = sprite; renderer.transform.position = new Vector3(i * 7, i % 2); renderers[i] = renderer; }
                for (int pass = 0; pass < 12; pass++)
                {
                    camera.transform.position = new Vector3(pass * 4, 0, -10); camera.orthographicSize = pass % 2 + 1;
                    renderers[0].transform.position += Vector3.up;
                    Assert.That(terrain.AreAnyInPresentationCamera(renderers), Is.EqualTo(renderers.Any(renderer => terrain.IsInPresentationCamera(renderer.bounds))));
                    var expected = renderers.Select(renderer => terrain.IsInPresentationCamera(renderer.bounds)).ToArray();
                    int reads = terrain.PresentationCameraReadCount;
                    using (terrain.BeginPresentationCameraQueries())
                    using (terrain.BeginPresentationCameraQueries())
                    {
                        for (int i = 0; i < renderers.Length; i++)
                            Assert.That(terrain.IsInPresentationCamera(renderers[i].bounds), Is.EqualTo(expected[i]));
                        Assert.That(terrain.PresentationCameraReadCount, Is.EqualTo(reads + 1));
                        renderers[0].transform.position = camera.transform.position + Vector3.forward * 10;
                        Assert.That(terrain.IsInPresentationCamera(renderers[0].bounds), Is.True, "Only camera inputs are shared; renderer bounds stay live.");
                    }
                    camera.transform.position += Vector3.right * 200;
                    Assert.That(terrain.IsInPresentationCamera(renderers[0].bounds), Is.False, "A later query must observe camera movement immediately.");
                }
                camera.enabled = false; Assert.That(terrain.AreAnyInPresentationCamera(renderers), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PatrolPersistenceMatchesOriginalWritesThroughWithinCellMovementCrossingAndExternalStateChanges()
        {
            var type = typeof(PatrolMaterialOptimizationTests).GetNestedType("PatrolFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, true);
            var owner = Read<DungeonCombatCoordinator>(fixture, "Owner");
            var actor = Read<ExplorationCharacterView>(fixture, "Actor");
            var floor = Read<DungeonFloorInstance>(fixture, "Floor");
            var mapView = Read<DungeonFloorMapView>(fixture, "view");
            var trigger = Read<GameObject>(fixture, "Root").AddComponent<EncounterTriggerView>();
            var definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/ED_Forest_WolfPair.asset");
            var encounter = new DungeonEncounterInstance("persist-test", definition, "room", new RectInt(0, 0, 4, 4));
            Set(trigger, "encounterInstance", encounter);
            var persist = Bind<Action<EncounterTriggerView, ExplorationCharacterView>>(owner, "PersistPatrolPosition");
            const string id = "enemy:persist-test:test";
            foreach (var world in new[] { new Vector2(1.2f, 1.3f), new Vector2(1.8f, 1.7f), new Vector2(2.3f, 1.8f), new Vector2(2.3f, 1.8f) })
            {
                actor.transform.position = world;
                var position = actor.DungeonPosition;
                Assert.That(floor.Map.TryGetTraversalCell(position, out var cell), Is.True);
                persist(trigger, actor);
                Assert.That(floor.TryGetActorPosition(id, out var saved), Is.True);
                Assert.That(saved, Is.EqualTo(position));
                Assert.That(floor.GetActorOffset(id), Is.EqualTo(world - (Vector2)mapView.CellToWorldCenter(cell)));
                Assert.That(encounter.EnemyExplorationPositions[actor.EnemyInstanceId], Is.EqualTo(cell));
            }
            // Avoid a last-value cache: another system may restore/move the actor.
            floor.TrySetActorPosition(id, new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)));
            encounter.TrySetEnemyExplorationPosition(actor.EnemyInstanceId, Vector2Int.zero);
            persist(trigger, actor);
            Assert.That(floor.ActorPositions[id], Is.EqualTo(actor.DungeonPosition));
            Assert.That(encounter.EnemyExplorationPositions[actor.EnemyInstanceId], Is.EqualTo(new Vector2Int(2, 1)));
            actor.transform.position = new Vector3(200, 200);
            persist(trigger, actor);
            Assert.That(encounter.EnemyExplorationPositions[actor.EnemyInstanceId], Is.EqualTo(new Vector2Int(2, 1)), "Invalid cells retain the original no-write behavior.");
        }
    }
}
