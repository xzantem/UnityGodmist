using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonEnemyBodyTests
    {
        private readonly List<Object> createdObjects = new();

        [TestCase("enemy.catacombs.archer", 64)]
        [TestCase("enemy.catacombs.defender", 64)]
        [TestCase("enemy.catacombs.occultist", 64)]
        [TestCase("enemy.catacombs.swordsman", 64)]
        [TestCase("enemy.catacombs.undead", 64)]
        [TestCase("enemy.catacombs.ghoul", 64)]
        [TestCase("enemy.catacombs.wraith", 64)]
        [TestCase("enemy.catacombs.dead_king", 128)]
        public void CatacombsCorpse_UsesDedicatedCachedSpriteAtGroundPivot(string id, int size)
        {
            Assert.That(CharacterAnimationResolver.TryResolveCorpse(id, out var sprite), Is.True);
            Assert.That(sprite.name, Does.Contain("-corpse-"));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(size, size)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(size / 2f, size / 2f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32f));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(CharacterAnimationResolver.TryResolveCorpse(id, out var cached), Is.True);
            Assert.That(cached, Is.SameAs(sprite));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GhoulBody_UsesNaturalUnrotatedCorpse_AndHonoursExplicitDeadSprite(bool explicitSprite)
        {
            CreateSessionWithDungeon(out var dungeon, out _);
            dungeon.TryGetCurrentFloor(out var floor);
            floor.TryGetEncounter("encounter.body", out var encounter);
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdObjects.Add(definition);
            SetAutoProperty(definition, "Id", "enemy.catacombs.ghoul");
            var legacyTexture = new Texture2D(2, 2);
            var legacySprite = Sprite.Create(legacyTexture, new Rect(0, 0, 2, 2), Vector2.zero);
            createdObjects.Add(legacySprite);
            createdObjects.Add(legacyTexture);
            SetAutoProperty(definition, "VisualSprite", legacySprite);
            if (explicitSprite) SetAutoProperty(definition, "DeadSprite", legacySprite);
            var enemy = new EncounterEnemyData();
            SetAutoProperty(enemy, "InstanceId", "ghoul.body");
            SetAutoProperty(enemy, "CharacterDefinition", definition);
            SetAutoProperty(encounter.Definition, "Enemies", new List<EncounterEnemyData> { enemy });
            var body = new DungeonEnemyBodyInstance("corpse", "ghoul.body", Vector2Int.zero, 13, null);
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            var view = DungeonEnemyBodyView.CreateFallback(null);
            createdObjects.Add(view.gameObject);
            view.transform.localRotation = Quaternion.Euler(0, 0, -90);
            view.Initialize(null, floor, body, new Vector3(4, 5, 0));
            var renderer = view.GetComponent<SpriteRenderer>();
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(Quaternion.Angle(view.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(view.transform.position, Is.EqualTo(new Vector3(4, 5, 0)));
            if (explicitSprite) Assert.That(renderer.sprite, Is.SameAs(legacySprite));
            else
            {
                CharacterAnimationResolver.TryResolveCorpse(definition.Id, out var expected);
                Assert.That(renderer.sprite, Is.SameAs(expected));
                Assert.That(renderer.sprite, Is.Not.SameAs(legacySprite));
            }
            Assert.That(view.GetComponent<CircleCollider2D>().isTrigger, Is.True);
            Assert.That(floor.TryGetEnemyBody(body.InstanceId, out var stored), Is.True);
            Assert.That(stored.Gold, Is.EqualTo(13));
        }

        [Test]
        public void BodyWithoutDeadOrVisualSprite_UsesIdleFrameAndRemainsLootable()
        {
            CreateSessionWithDungeon(out var dungeon, out _);
            dungeon.TryGetCurrentFloor(out var floor);
            floor.TryGetEncounter("encounter.body", out var encounter);
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdObjects.Add(definition);
            SetAutoProperty(definition, "Id", "enemy.test.animated_only");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            createdObjects.Add(sprite);
            createdObjects.Add(texture);
            definition.IdleAnimationFrames.Add(null);
            definition.IdleAnimationFrames.Add(sprite);
            var enemy = new EncounterEnemyData();
            SetAutoProperty(enemy, "InstanceId", "animated.body");
            SetAutoProperty(enemy, "CharacterDefinition", definition);
            SetAutoProperty(encounter.Definition, "Enemies", new List<EncounterEnemyData> { enemy });
            var body = new DungeonEnemyBodyInstance("corpse.idle", "animated.body", Vector2Int.zero, 13, null);
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            var view = DungeonEnemyBodyView.CreateFallback(null);
            createdObjects.Add(view.gameObject);
            view.Initialize(null, floor, body, Vector3.zero);
            Assert.That(view.GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
            Assert.That(view.GetComponent<CircleCollider2D>().isTrigger, Is.True);
            Assert.That(floor.TryGetEnemyBody(body.InstanceId, out var stored), Is.True);
            Assert.That(stored.Gold, Is.EqualTo(13));
        }

        [TestCase("BanditThug")][TestCase("BanditShooter")]
        [TestCase("ElfArcher")][TestCase("ElfDruid")][TestCase("ElfPoacher")]
        [TestCase("RabidWolf")][TestCase("InfectedTree")]
        [TestCase("InfectedEnt")][TestCase("InfectedEntBurning")]
        public void ImportedForestBodyUsesDedicatedSpriteWithoutRotationOrTint(string asset)
        {
            CreateSessionWithDungeon(out var dungeon, out _);
            dungeon.TryGetCurrentFloor(out var floor);
            floor.TryGetEncounter("encounter.body", out var encounter);
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_" + asset + ".asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.DeadSprite, Is.Not.Null);
            var enemy = new EncounterEnemyData();
            SetAutoProperty(enemy, "InstanceId", "forest.body");
            SetAutoProperty(enemy, "CharacterDefinition", definition);
            SetAutoProperty(encounter.Definition, "Enemies", new List<EncounterEnemyData> { enemy });
            var body = new DungeonEnemyBodyInstance("corpse.forest", "forest.body", Vector2Int.zero, 13, null);
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            var view = DungeonEnemyBodyView.CreateFallback(null);
            createdObjects.Add(view.gameObject);
            view.transform.localRotation = Quaternion.Euler(0, 0, -90);
            view.Initialize(null, floor, body, Vector3.zero);
            var renderer = view.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.SameAs(definition.DeadSprite));
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(view.transform.localScale, Is.EqualTo(new Vector3(.7f,.7f,1f)), "Corpses must match the authored 0.7 actor scale without changing PPU.");
            Assert.That(Quaternion.Angle(view.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(floor.TryGetEnemyBody(body.InstanceId, out _), Is.True);
        }

        [Test]
        public void CorpseGroundLaneStaysBelowActorsOnLargeMapAndRegistersGroundLighting()
        {
            CreateSessionWithDungeon(out var dungeon, out _); dungeon.TryGetCurrentFloor(out var floor);
            floor.TryGetEncounter("encounter.body", out var encounter);
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_RabidWolf.asset");
            var enemy = new EncounterEnemyData(); SetAutoProperty(enemy, "InstanceId", "wolf.body");
            SetAutoProperty(enemy, "CharacterDefinition", definition);
            SetAutoProperty(encounter.Definition, "Enemies", new List<EncounterEnemyData> { enemy });
            var body = new DungeonEnemyBodyInstance("corpse.ground", "wolf.body", Vector2Int.zero, 13, null);
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            var root = new GameObject("Corpse map", typeof(Grid)); createdObjects.Add(root);
            var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
            tiles.transform.SetParent(root.transform, false);
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>(); createdObjects.Add(tile);
            var mapView = root.AddComponent<DungeonFloorMapView>();
            var state = new UnityEditor.SerializedObject(mapView);
            state.FindProperty("floorTilemap").objectReferenceValue = tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            state.FindProperty("floorTile").objectReferenceValue = tile;
            state.FindProperty("wallSegmentPrefab").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>();
            state.ApplyModifiedPropertiesWithoutUndo();
            var map = new DungeonFloorMap();
            for (var y = 0; y < 40; y++)
            {
                map.TryAddFloorCell(new Vector2Int(0,y), "room"); map.TryAddFloorCell(new Vector2Int(1,y), "room");
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0,y), new Vector2Int(1,y)), DungeonWallType.Full);
            }
            mapView.Rebuild(map);
            var view = DungeonEnemyBodyView.CreateFallback(root.transform);
            view.Initialize(null, floor, body, Vector3.zero); view.BindElevationView(mapView);
            var renderer = view.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingOrder, Is.LessThan(0), "Legacy constant 9 overpainted distant actors.");
            Assert.That(renderer.sortingOrder, Is.EqualTo(mapView.GetDungeonObjectDepthSortingOrder()));
            Assert.That(mapView.IsGroundLightingPresentation(renderer), Is.True);
            Assert.That(mapView.ReservesGroundOutlineBand(renderer), Is.True);
            Assert.That(tiles.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingOrder, Is.LessThan(renderer.sortingOrder - 1));
            for (var y = -1; y < 42; y++)
                Assert.That(renderer.sortingOrder, Is.LessThan(mapView.GetActorDepthSortingOrder(y)));
            foreach (var outline in view.GetComponentsInChildren<SpriteRenderer>().Where(r => r != renderer))
                Assert.That(outline.sortingOrder, Is.EqualTo(renderer.sortingOrder - 1));
            Assert.That(view.GetComponent<CircleCollider2D>().isTrigger, Is.True);
            Assert.That(body.Gold, Is.EqualTo(13));
            VerifyBodyRendersBelowNativeHero(mapView, view);
            view.BindElevationView(null); Assert.That(mapView.IsGroundLightingPresentation(renderer), Is.False);
        }

        private static void VerifyBodyRendersBelowNativeHero(DungeonFloorMapView map, DungeonEnemyBodyView body)
        {
            var heroObject = new GameObject("Native hero layering preview", typeof(SpriteRenderer));
            var cameraObject = new GameObject("Body preview camera", typeof(Camera));
            var target = new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            try
            {
                body.gameObject.layer = heroObject.layer = 31;
                // Reproduce a northern room where actors have negative depth
                // orders and the old corpse constant 9 would cover the hero.
                var ground = new Vector3(0,35,0);
                body.transform.position = ground;
                heroObject.transform.position = ground + new Vector3(0,-.8f,0);
                var hero = heroObject.GetComponent<SpriteRenderer>();
                hero.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset").VisualSprite;
                hero.sortingLayerID = map.FloorFeedbackSortingLayerID;
                hero.sortingOrder = map.GetActorDepthSortingOrder(heroObject.transform.position.y);
                Assert.That(hero.sortingOrder, Is.LessThan(9), "Exercise the actual legacy overpaint case.");
                var corpse = body.GetComponent<SpriteRenderer>();
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = 1.6f; camera.aspect = 1;
                camera.transform.position = ground + new Vector3(0,.35f,-10); camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.targetTexture = target;
                Color[] Read(string name)
                {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    var output = System.Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
                    if (!string.IsNullOrEmpty(output))
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),pixels.EncodeToPNG());
                    return pixels.GetPixels();
                }
                corpse.enabled = false; var actor = Read("corpse-native-hero");
                corpse.enabled = true; hero.enabled = false; var remains = Read("corpse-native-wolf");
                hero.enabled = true; var combined = Read("corpse-native-ground-layer");
                var order = corpse.sortingOrder; corpse.sortingOrder = 9;
                var legacy = Read("corpse-legacy-layer"); corpse.sortingOrder = order;
                var overlap = 0;
                var legacyError = 0f;
                for (var i = 0; i < actor.Length; i++)
                {
                    if (actor[i].a < .99f || remains[i].a < .99f) continue;
                    overlap++;
                    // Native hero art has near-opaque (253/255) alpha, so the
                    // corpse legitimately contributes a tiny amount beneath it.
                    // Compare the expected premultiplied blend in linear space,
                    // allowing for 8-bit colour/alpha readback quantization.
                    var expected = actor[i].linear + remains[i].linear * (1-actor[i].a);
                    var actual = combined[i].linear;
                    legacyError = Mathf.Max(legacyError, Mathf.Abs(legacy[i].linear.r-expected.r),
                        Mathf.Abs(legacy[i].linear.g-expected.g), Mathf.Abs(legacy[i].linear.b-expected.b));
                    // Independent sRGB8 readbacks can differ by about .009
                    // in linear space at the bright end, plus alpha rounding.
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(.012));
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(.012));
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(.012));
                }
                Assert.That(overlap, Is.GreaterThan(12), "Native art must actually overlap to exercise the reported bug.");
                Assert.That(legacyError, Is.GreaterThan(.1f), "Negative control: old order 9 must visibly overpaint this hero.");
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(heroObject); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ChestWindowClosesOnlyAfterItsLastItemAndGoldAreCollected()
        {
            var game=CreateSessionWithDungeon(out var dungeon,out _);game.TrySetMode(GameModeState.Exploration);
            var hero=new CharacterInstance{CharacterId="chest-collector",LifeState=CharacterLifeState.Alive,Inventory=new Inventory()};
            game.TryRegisterCharacter(hero);game.TryAddCharacterToActiveParty(hero.CharacterId);game.TrySetActiveLeader(hero.CharacterId);
            dungeon.TryGetCurrentFloor(out var floor);
            var definition=ScriptableObject.CreateInstance<ChestDungeonObjectDefinition>();createdObjects.Add(definition);
            var data=new UnityEditor.SerializedObject(definition);data.FindProperty("<Id>k__BackingField").stringValue="chest.autoclose";
            data.FindProperty("<LockType>k__BackingField").enumValueIndex=(int)ChestLockType.Unlocked;data.ApplyModifiedPropertiesWithoutUndo();
            var item=new ItemInstance(CreateItemDefinition("chest.item"),"chest.item1");
            var chest=new ChestDungeonObjectInstance("chest.autoclose",definition,Vector2Int.zero,new[]{item},5);
            Assert.That(floor.TryAddObject(chest),Is.True);
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab");
            var view=Object.Instantiate(prefab);createdObjects.Add(view.gameObject);view.InitializeChest(game,chest.InstanceId);
            Assert.That(view.Window.IsOpen,Is.True);Assert.That(view.TryClaimItem(item.InstanceId),Is.True);
            Assert.That(view.Window.IsOpen,Is.True,"Remaining gold keeps the source open.");
            Assert.That(view.TryClaimGold(),Is.True);Assert.That(view.Window.IsOpen,Is.False);
            Assert.That(game.Gold,Is.EqualTo(5));
        }

        [Test]
        public void CollectAllClaimsItemsAndGoldClosesBodyAndKeepsUnclaimedOverweightItems()
        {
            var game = CreateSessionWithDungeon(out var dungeon, out _);
            game.TrySetMode(GameModeState.Exploration);
            var hero = new CharacterInstance { CharacterId = "collector", LifeState = CharacterLifeState.Alive, Inventory = new Inventory() };
            game.TryRegisterCharacter(hero); game.TryAddCharacterToActiveParty(hero.CharacterId); game.TrySetActiveLeader(hero.CharacterId);
            var definition = CreateItemDefinition("item.bulk"); SetAutoProperty(definition, "Weight", 2f);
            var items = new[] { new ItemInstance(definition,"bulk.1"), new ItemInstance(definition,"bulk.2") };
            var floor = dungeon.Floors[0];
            var body = new DungeonEnemyBodyInstance("body.bulk","enemy.bulk",Vector2Int.zero,13,items);
            floor.TryAddEnemyBody(body);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab");
            var view = Object.Instantiate(prefab); createdObjects.Add(view.gameObject);
            view.Initialize(game, body.InstanceId);
            hero.Inventory.MaxWeight = 0;
            view.CollectAll();
            Assert.That(game.Gold, Is.EqualTo(13)); Assert.That(body.Items.Count, Is.EqualTo(2));
            Assert.That(view.Window.IsOpen, Is.True, "Full inventories must not discard or hide remaining items.");
            hero.Inventory.MaxWeight = null;
            view.CollectAll();
            Assert.That(body.IsEmpty, Is.True); Assert.That(hero.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(view.Window.IsOpen, Is.False);
            view.CollectAll(); Assert.That(game.Gold, Is.EqualTo(13)); Assert.That(hero.Inventory.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void WideVictory_LeavesUnseenEnemies_AndStoresBodiesInDeathRoomWithoutReroll()
        {
            var game = CreateSessionWithDungeon(out var dungeon, out _);
            dungeon.TryGetCurrentFloor(out var floor);
            floor.TryGetEncounter("encounter.body", out var encounter);
            var guard = new EncounterEnemyData(); SetAutoProperty(guard, "InstanceId", "guard");
            var unseen = new EncounterEnemyData(); SetAutoProperty(unseen, "InstanceId", "unseen");
            SetAutoProperty(encounter.Definition, "Enemies", new List<EncounterEnemyData> { guard, unseen });
            floor.Map.TryAddFloorCell(Vector2Int.right, "other.room");
            new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context, true);
            var combat = new CombatSession { CombatId = "runtime.session", DungeonContext = context,
                IsSeamless = true, IsDungeonWide = true, Outcome = CombatOutcomeState.Victory };
            var enemy = new BattleUnit { Character = new CharacterInstance { CharacterId = "guard" } };
            var loot = new CombatRewardResult { EnemyRewards = { new CombatEnemyReward { EnemyInstanceId = "guard", Gold = 17 } } };
            DungeonCombatMembershipSystem.RegisterEnemy(combat, encounter.InstanceId, "guard", loot, enemy);
            combat.TryMarkEnemyDefeated(enemy.Character.CharacterId, new GridPosition(1, 0));
            Assert.That(new DungeonCombatOutcomeSystem().TryApplyOutcome(game, combat), Is.True);
            Assert.That(dungeon.IsEncounterCleared(encounter.InstanceId), Is.False);
            Assert.That(encounter.IsEnemyDefeated("guard"), Is.True);
            Assert.That(encounter.IsEnemyDefeated("unseen"), Is.False);
            Assert.That(floor.EnemyBodies.Count, Is.EqualTo(1));
            Assert.That(floor.EnemyBodies[0].Position, Is.EqualTo(Vector2Int.right));
            Assert.That(floor.EnemyBodies[0].EnemyInstanceId, Is.EqualTo("guard"));
            Assert.That(floor.EnemyBodies[0].Gold, Is.EqualTo(17));
            Assert.That(new DungeonCombatOutcomeSystem().TryApplyOutcome(game, combat), Is.True);
            Assert.That(floor.EnemyBodies.Count, Is.EqualTo(1));
            Assert.That(loot.EnemyRewards[0].EnemyInstanceId, Is.EqualTo("guard"));
        }

        [Test]
        public void VictoryCreatesBodiesOnlyForDefeatedEnemyRewards()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);

            var item = new ItemInstance(
                CreateItemDefinition("item.body"),
                "item.body.instance");

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.defeated",
                            Gold = 17,
                            Items = { item }
                        },
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.escaped",
                            Gold = 99
                        }
                    }
                }
            };

            combat.TryMarkEnemyDefeated("enemy.defeated");

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);

            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(
                floor.EnemyBodies[0].EnemyInstanceId,
                Is.EqualTo("enemy.defeated"));
            Assert.That(floor.EnemyBodies[0].Gold, Is.EqualTo(17));
            Assert.That(
                floor.EnemyBodies[0].Items[0].InstanceId,
                Is.EqualTo("item.body.instance"));

            var bodyInstanceId = floor.EnemyBodies[0].InstanceId;
            Assert.That(floor.TryRemoveEnemyBody(bodyInstanceId), Is.True);
            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);
            Assert.That(floor.EnemyBodies, Is.Empty);
        }

        [Test]
        public void EscapeCreatesBodiesForEnemiesThatDiedBeforeEscape()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Escaped,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.escaped",
                            Gold = 25
                        }
                    }
                }
            };

            combat.TryMarkEnemyDefeated(
                "enemy.escaped",
                new GridPosition(0, 0));

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);

            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(floor.EnemyBodies[0].Position, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void VictoryPlacesBodiesOnWalkableEncounterCells()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);
            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(
                floor.Map.TryAddFloorCell(new Vector2Int(2, 4), "room.body"),
                Is.True);
            Assert.That(
                floor.Map.TryAddFloorCell(new Vector2Int(2, 5), "room.body"),
                Is.True);
            Assert.That(
                floor.Map.TryAddFloorCell(new Vector2Int(3, 4), "room.body"),
                Is.True);

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.first",
                            Gold = 1
                        },
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.second",
                            Gold = 1
                        }
                    }
                }
            };

            combat.TryMarkEnemyDefeated(
                "enemy.first",
                new GridPosition(2, 4));
            combat.TryMarkEnemyDefeated(
                "enemy.second",
                new GridPosition(2, 4));

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(2));

            Assert.That(floor.EnemyBodies[0].Position, Is.EqualTo(new Vector2Int(2, 4)));
            Assert.That(floor.EnemyBodies[1].Position, Is.EqualTo(new Vector2Int(3, 4)));

            foreach (var body in floor.EnemyBodies)
            {
                Assert.That(floor.Map.IsWalkable(body.Position), Is.True);
                Assert.That(
                    floor.Map.TryGetRoomId(body.Position, out var roomId),
                    Is.True);
                Assert.That(roomId, Is.EqualTo(context.EncounterRoomId));
            }
        }

        [Test]
        public void EmptyBodyIsCreatedAndPersists()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);
            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.empty"
                        }
                    }
                }
            };
            combat.TryMarkEnemyDefeated(
                "enemy.empty",
                new GridPosition(0, 0));

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);
            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(floor.EnemyBodies[0].IsEmpty, Is.True);
        }

        [Test]
        public void BodyMovesToNearestFreeCellWhenDeathCellHasObstacle()
        {
            var gameSession = CreateSessionWithDungeon(
                out var dungeon,
                out var context);
            Assert.That(dungeon.TryGetCurrentFloor(out var floor), Is.True);
            Assert.That(
                floor.Map.TryAddFloorCell(new Vector2Int(1, 0), "room.body"),
                Is.True);

            var obstacleDefinition =
                ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            createdObjects.Add(obstacleDefinition);

            Assert.That(
                floor.TryAddObject(new ObstacleDungeonObjectInstance(
                    "obstacle.body.test",
                    obstacleDefinition,
                    Vector2Int.zero)),
                Is.True);

            var combat = new CombatSession
            {
                CombatId = context.EncounterInstanceId,
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.obstacle",
                            Gold = 1
                        }
                    }
                }
            };
            combat.TryMarkEnemyDefeated(
                "enemy.obstacle",
                new GridPosition(0, 0));

            Assert.That(
                new DungeonCombatOutcomeSystem().TryApplyOutcome(
                    gameSession,
                    combat),
                Is.True);
            Assert.That(floor.EnemyBodies[0].Position, Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void BodyClaimsItemAndGoldExactlyOnce()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.leader",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(
                session.TryAddCharacterToActiveParty(character.CharacterId),
                Is.True);

            var dungeon = new DungeonInstance("dungeon.body", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(floor.TrySetMap(new DungeonFloorMap()), Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var item = new ItemInstance(
                CreateItemDefinition("item.claim"),
                "item.claim.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.claim",
                "enemy.claim",
                Vector2Int.zero,
                13,
                new[] { item });

            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            var service = new LootCollectionService();

            Assert.That(
                service.TryClaimDungeonBodyItem(
                    session,
                    body.InstanceId,
                    item.InstanceId),
                Is.True);
            Assert.That(character.Inventory.Items, Is.EqualTo(new[] { item }));
            Assert.That(
                service.TryClaimDungeonBodyItem(
                    session,
                    body.InstanceId,
                    item.InstanceId),
                Is.False);

            Assert.That(
                service.TryClaimDungeonBodyGold(
                    session,
                    body.InstanceId,
                    out var claimedGold),
                Is.True);
            Assert.That(claimedGold, Is.EqualTo(13));
            Assert.That(session.Gold, Is.EqualTo(13));
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(
                service.TryClaimDungeonBodyGold(
                    session,
                    body.InstanceId,
                    out _),
                Is.False);
        }

        [Test]
        public void OverweightBodyItemDoesNotPreventClaimingItsGold()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.overweight",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory { MaxWeight = 1f }
            };

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.overweight", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var itemDefinition = CreateItemDefinition("item.overweight");
            SetAutoProperty(itemDefinition, "Weight", 2f);
            var item = new ItemInstance(itemDefinition, "item.overweight.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.overweight", "enemy.overweight", Vector2Int.zero,
                11, new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            var service = new LootCollectionService();
            Assert.That(service.TryClaimNextDungeonBodyItem(
                session, body.InstanceId, out _), Is.False);
            Assert.That(service.TryClaimDungeonBodyGold(
                session, body.InstanceId, out var gold), Is.True);
            Assert.That(gold, Is.EqualTo(11));
            Assert.That(session.Gold, Is.EqualTo(11));
            Assert.That(floor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(floor.EnemyBodies[0].Items[0], Is.SameAs(item));
        }

        [Test]
        public void BodyItemClaimCanTargetAnotherActiveCharacterAndPreservesInstanceId()
        {
            var session = new GameSession(GameModeState.Exploration);
            var leader = new CharacterInstance
            {
                CharacterId = "character.leader.target",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };
            var target = new CharacterInstance
            {
                CharacterId = "character.target",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };

            Assert.That(session.TryRegisterCharacter(leader), Is.True);
            Assert.That(session.TryRegisterCharacter(target), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(leader.CharacterId), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(target.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.target", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var item = new ItemInstance(
                CreateItemDefinition("item.target"),
                "item.target.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.target",
                "enemy.target",
                Vector2Int.zero,
                0,
                new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);

            Assert.That(new LootCollectionService().TryClaimDungeonBodyItem(
                session,
                body.InstanceId,
                target.CharacterId,
                item.InstanceId), Is.True);
            Assert.That(target.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(target.Inventory.Items[0], Is.SameAs(item));
            Assert.That(target.Inventory.Items[0].InstanceId,
                Is.EqualTo("item.target.instance"));
            Assert.That(leader.Inventory.Items, Is.Empty);
        }

        [Test]
        public void BodyClaimsAreRejectedDuringCombat()
        {
            var session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstance
            {
                CharacterId = "character.combat",
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var dungeon = new DungeonInstance("dungeon.combat", 1);
            var floor = new DungeonFloorInstance(0);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            var item = new ItemInstance(
                CreateItemDefinition("item.combat"),
                "item.combat.instance");
            var body = new DungeonEnemyBodyInstance(
                "body.combat",
                "enemy.combat",
                Vector2Int.zero,
                7,
                new[] { item });
            Assert.That(floor.TryAddEnemyBody(body), Is.True);
            Assert.That(session.TrySetActiveCombat(new CombatSession
            {
                CombatId = "combat.active"
            }), Is.True);

            var service = new LootCollectionService();
            Assert.That(service.TryClaimDungeonBodyItem(
                session,
                body.InstanceId,
                item.InstanceId), Is.False);
            Assert.That(service.TryClaimDungeonBodyGold(
                session,
                body.InstanceId,
                out _), Is.False);
            Assert.That(character.Inventory.Items, Is.Empty);
            Assert.That(session.Gold, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotCapturesBodyGoldPositionAndExactItemIds()
        {
            var session = new GameSession();
            var dungeon = new DungeonInstance("dungeon.save.body", 5);
            var floor = new DungeonFloorInstance(0);
            var item = new ItemInstance(
                CreateItemDefinition("item.save.body"),
                "item.save.body.instance");

            Assert.That(
                floor.TryAddEnemyBody(
                    new DungeonEnemyBodyInstance(
                        "body.save",
                        "enemy.save",
                        new Vector2Int(8, -2),
                        21,
                        new[] { item })),
                Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var saveData = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                session,
                "Body save");

            var bodyDelta = saveData.ActiveDungeon.FloorDeltas[0]
                .EnemyBodyDeltas[0];

            Assert.That(bodyDelta.BodyInstanceId, Is.EqualTo("body.save"));
            Assert.That(bodyDelta.EnemyInstanceId, Is.EqualTo("enemy.save"));
            Assert.That(bodyDelta.PositionX, Is.EqualTo(8));
            Assert.That(bodyDelta.PositionY, Is.EqualTo(-2));
            Assert.That(bodyDelta.Gold, Is.EqualTo(21));
            Assert.That(
                bodyDelta.ItemInstanceIds,
                Is.EqualTo(new[] { "item.save.body.instance" }));
            Assert.That(
                saveData.ItemInstances[0].InstanceId,
                Is.EqualTo("item.save.body.instance"));
        }

        [Test]
        public void SaveRestore_PreservesEnemyBodyPayload()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var dungeonDefinition = CreateRestoreDungeonDefinition();
            var item = new ItemInstance(
                CreateItemDefinition("item.body.restore"),
                "item.body.restore.instance");

            createdObjects.Add(catalog);
            catalog.DungeonDefinitions.Add(dungeonDefinition);
            catalog.ItemDefinitions.Add(item.Definition);

            var dungeon = new DungeonGeneratorService().Generate(
                dungeonDefinition,
                731);
            var floor = dungeon.Floors[0];

            Assert.That(floor.TryAddEnemyBody(new DungeonEnemyBodyInstance(
                "body.restore", "enemy.restore", new Vector2Int(4, 7),
                19, new[] { item })), Is.True);

            var session = new GameSession(GameModeState.Exploration);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            var serializer = new GameSaveJsonSerializer();
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                session, "body restore");

            Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot),
                out var restoredSave), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(
                restoredSave, catalog, out var restoredSession), Is.True);
            Assert.That(restoredSession.ActiveDungeon.TryGetCurrentFloor(
                out var restoredFloor), Is.True);
            Assert.That(restoredFloor.EnemyBodies, Has.Count.EqualTo(1));
            Assert.That(restoredFloor.EnemyBodies[0].InstanceId,
                Is.EqualTo("body.restore"));
            Assert.That(restoredFloor.EnemyBodies[0].EnemyInstanceId,
                Is.EqualTo("enemy.restore"));
            Assert.That(restoredFloor.EnemyBodies[0].Position,
                Is.EqualTo(new Vector2Int(4, 7)));
            Assert.That(restoredFloor.EnemyBodies[0].Gold, Is.EqualTo(19));
            Assert.That(restoredFloor.EnemyBodies[0].Items[0].InstanceId,
                Is.EqualTo("item.body.restore.instance"));
        }

        private DungeonDefinition CreateRestoreDungeonDefinition()
        {
            var templates = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();
            var layout = ScriptableObject.CreateInstance<DungeonLayoutProfile>();
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();
            createdObjects.Add(templates);
            createdObjects.Add(layout);
            createdObjects.Add(definition);

            SetAutoProperty(templates, "Id", "templates.body.restore");
            foreach (var roomType in new[]
                     {
                         DungeonRoomType.Start,
                         DungeonRoomType.Normal,
                         DungeonRoomType.Corridor,
                         DungeonRoomType.Final
                     })
            {
                var template = ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
                createdObjects.Add(template);
                SetAutoProperty(template, "Id", "room.body." + roomType);
                SetAutoProperty(template, "RoomType", roomType);
                SetAutoProperty(template, "SizeInCells", new Vector2Int(8, 8));
                templates.Templates.Add(template);
            }

            SetAutoProperty(definition, "Id", "dungeon.body.restore");
            SetAutoProperty(definition, "IsProcedural", true);
            SetAutoProperty(definition, "MinFloorCount", 1);
            SetAutoProperty(definition, "MaxFloorCount", 1);
            SetAutoProperty(definition, "MinRoomCountPerFloor", 3);
            SetAutoProperty(definition, "MaxRoomCountPerFloor", 3);
            SetAutoProperty(definition, "MinEncounterCountPerFloor", 0);
            SetAutoProperty(definition, "MaxEncounterCountPerFloor", 0);
            SetAutoProperty(definition, "MinObjectCountPerFloor", 0);
            SetAutoProperty(definition, "MaxObjectCountPerFloor", 0);
            SetAutoProperty(definition, "RoomTemplateCatalog", templates);
            SetAutoProperty(definition, "LayoutProfile", layout);
            return definition;
        }

        private GameSession CreateSessionWithDungeon(
            out DungeonInstance dungeon,
            out DungeonCombatContext context)
        {
            var encounterDefinition =
                ScriptableObject.CreateInstance<EncounterDefinition>();
            createdObjects.Add(encounterDefinition);

            var floor = new DungeonFloorInstance(0);
            var room = new DungeonRoomInstance(
                "room.body",
                DungeonRoomType.Normal);

            Assert.That(floor.TryAddRoom(room), Is.True);
            Assert.That(
                floor.TrySetMap(new DungeonFloorMap()),
                Is.True);
            Assert.That(
                floor.Map.TryAddFloorCell(Vector2Int.zero, room.RoomId),
                Is.True);

            var encounter = new DungeonEncounterInstance(
                "encounter.body",
                encounterDefinition,
                room.RoomId,
                new RectInt(0, 0, 1, 1));

            Assert.That(floor.TryAddEncounter(encounter), Is.True);

            dungeon = new DungeonInstance("dungeon.body", 1);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            var session = new GameSession();
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);

            context = new DungeonCombatContext(
                dungeon.DungeonId,
                0,
                encounter.InstanceId,
                room.RoomId,
                Vector2Int.zero,
                new Vector2Int(3, 4),
                new[] { Vector2Int.zero },
                null,
                null);

            Assert.That(session.TrySetActiveDungeonCombatContext(context), Is.True);
            return session;
        }

        private ItemDefinition CreateItemDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            return definition;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            field.SetValue(target, value);
        }
    }
}
