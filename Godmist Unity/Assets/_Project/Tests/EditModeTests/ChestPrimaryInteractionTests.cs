using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ChestPrimaryInteractionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root, canvas;
        ExplorationPartyController party;
        ExplorationCharacterView actor;
        DungeonFloorMapView terrain;
        Tilemap tiles;
        DungeonFloorMap map;
        DungeonFloorInstance floor;
        GameSession session;
        ChestDungeonObjectDefinition definition;
        DungeonChestView view;
        ChestDungeonObjectInstance chest;
        DungeonLootWindowView lootPrefab;
        ItemDefinition key;

        [SetUp] public void Setup()
        {
            root = new GameObject("Chest primary tests", typeof(Grid));
            tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>(); tiles.transform.SetParent(root.transform);
            terrain = root.AddComponent<DungeonFloorMapView>(); Set(terrain, "floorTilemap", tiles);
            map = new DungeonFloorMap();
            for (var y = 0; y < 7; y++) for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            floor = new DungeonFloorInstance(0); floor.TrySetMap(map); Set(terrain, "boundMap", map);
            Get<DungeonElevationViewState>(terrain, "elevation").Bind(map);
            session = new GameSession(GameModeState.Exploration);
            var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
            session.TrySetActiveLeader(character.CharacterId);
            var dungeon = new DungeonInstance("chest.tests", 1); dungeon.TryAddFloor(floor); session.TrySetActiveDungeon(dungeon);
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"), root.transform);
            actor.Bind(character); actor.ConfigureTerrain(map, terrain); actor.SetPosition(Center(new Vector2Int(1, 3)));
            party = root.AddComponent<ExplorationPartyController>(); party.Initialize(session, new[] { actor });
            party.ConfigureTraversal(floor, terrain, null);
            definition = ScriptableObject.CreateInstance<ChestDungeonObjectDefinition>();
            typeof(Godmist.Data.Dungeons.DungeonObjectDefinition).GetField("<Id>k__BackingField", Private).SetValue(definition, "chest.tests");
            chest = new ChestDungeonObjectInstance("chest.1", definition, new Vector2Int(8, 3), null, 25); floor.TryAddObject(chest);
            view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonChestView>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonChest.prefab"), root.transform);
            lootPrefab = AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab");
            view.Initialize(party, floor, chest, Center(chest.Position), tiles, lootPrefab); view.BindElevationView(terrain);
            Physics2D.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
            foreach (var visual in root.GetComponentsInChildren<Godmist.Views.Combat.CombatUnitVisualStateView>(true))
                Invoke(visual, "OnDestroy");
            Object.DestroyImmediate(root); Object.DestroyImmediate(definition);
            if (key != null) Object.DestroyImmediate(key);
            foreach (var feedback in Object.FindObjectsByType<DungeonLootFloatingText>(FindObjectsSortMode.None))
                if (feedback.name == "Dungeon Chest Feedback") { Invoke(feedback, "OnDestroy"); Object.DestroyImmediate(feedback.gameObject); }
            Physics2D.SyncTransforms();
        }
        [Test] public void FarPrimaryClickClaimsInputAndPlansReachableApproachWithoutOpeningLoot()
        {
            Assert.That(view.TryPrimaryInteraction(view.transform.position), Is.True);
            Assert.That(party.IsWorldPrimaryClickClaimed, Is.True);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.SameAs(view));
            var goal = Get<Vector2?>(party, "movementDestination"); Assert.That(goal.HasValue, Is.True);
            Assert.That(view.CanInteractFrom(goal.Value), Is.True);
            Assert.That(Vector2.Distance(goal.Value, view.transform.position), Is.LessThan(view.InteractionRadius));
            Assert.That(chest.Gold, Is.EqualTo(25));
        }
        [Test] public void ClaimedPrimaryClickCannotIssueASecondChestOrder()
        {
            party.ClaimWorldPrimaryClick();
            Assert.That(view.TryPrimaryInteraction(view.transform.position), Is.False);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
        }
        [Test] public void NearPrimaryClickOpensExistingLootWindowAndDoesNotAutoCollect()
        {
            PrepareUi(); actor.SetPosition(Center(chest.Position + Vector2Int.left));
            Assert.That(view.TryPrimaryInteraction(view.transform.position), Is.True);
            AssertLootOpened();
            Assert.That(chest.Gold, Is.EqualTo(25));
            Assert.That(Get<Vector2?>(party, "movementDestination"), Is.Null);
        }
        [Test] public void ArrivalOpensExactlyOnceAndClearsPendingOrder()
        {
            PrepareUi(); Assert.That(party.RequestChestInteraction(view), Is.True);
            actor.SetPosition(Get<Vector2?>(party, "movementDestination").Value);
            Get<Queue<DungeonTraversalPosition>>(party, "traversalPath").Clear();
            Invoke(party, "TickTraversalMovement", actor);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.SameAs(view),
                "Arrival cleanup must preserve the action until the interaction tick.");
            Assert.That((bool)Invoke(party, "TickChestApproach"), Is.True);
            AssertLootOpened();
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
            Assert.That((bool)Invoke(party, "TickChestApproach"), Is.False);
        }
        [Test] public void ECompatibleInteractStillOpensTheSameChestInRange()
        {
            PrepareUi(); actor.SetPosition(Center(chest.Position + Vector2Int.left));
            Assert.That(view.CanInteract(), Is.True); view.Interact(); AssertLootOpened();
        }
        [Test] public void CancelMovementAndNewDestinationClearPendingChest()
        {
            Assert.That(party.RequestChestInteraction(view), Is.True); party.CancelMovement();
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
            Assert.That(Get<Vector2?>(party, "movementDestination"), Is.Null);
            Assert.That(party.RequestChestInteraction(view), Is.True);
            Invoke(party, "TrySetTraversalDestination", (Vector3)Center(new Vector2Int(2, 2)));
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
        }
        [TestCase("disabled")]
        [TestCase("deactivated")]
        [TestCase("destroyed")]
        [TestCase("combat")]
        [TestCase("dead")]
        public void InvalidatedPendingTargetCancelsWithoutOpening(string reason)
        {
            Assert.That(party.RequestChestInteraction(view), Is.True);
            if (reason == "disabled") view.enabled = false;
            if (reason == "deactivated") chest.TryDeactivate();
            if (reason == "destroyed") Object.DestroyImmediate(view.gameObject);
            if (reason == "combat") session.TrySetActiveCombat(new Godmist.Runtime.Combat.CombatSession { CombatId = "chest.combat" });
            if (reason == "dead") actor.RuntimeCharacter.LifeState = CharacterLifeState.Dead;
            Assert.That((bool)Invoke(party, "TickChestApproach"), Is.False);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
            Assert.That(Get<Vector2?>(party, "movementDestination"), Is.Null);
        }
        [Test] public void WallBetweenActorAndChestPreventsRemoteOpening()
        {
            actor.SetPosition(Center(chest.Position + Vector2Int.left));
            map.TryAddWall(chest.Position + Vector2Int.left, chest.Position);
            Assert.That(view.CanInteract(), Is.False);
            Assert.That(view.CanInteractFrom(actor.Position), Is.False);
        }
        [Test] public void EnclosedChestHasNoApproachAndNoPendingMovement()
        {
            foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                map.TryAddWall(chest.Position + direction, chest.Position);
            Assert.That(party.RequestChestInteraction(view), Is.False);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
            Assert.That(Get<Vector2?>(party, "movementDestination"), Is.Null);
        }
        [Test] public void MissingTraversalViewRejectsApproachWithoutDereferencingIt()
        {
            Set(party, "traversalMapView", null);
            Assert.That(party.RequestChestInteraction(view), Is.False);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
            Assert.That(Get<Vector2?>(party, "movementDestination"), Is.Null);
        }
        [Test] public void WrongFloorOrPartyCannotRequestThisChest()
        {
            party.ConfigureTraversal(new DungeonFloorInstance(1), terrain, null);
            Assert.That(party.RequestChestInteraction(view), Is.False);
            var other = new GameObject("Other party").AddComponent<ExplorationPartyController>(); other.transform.SetParent(root.transform);
            Assert.That(other.RequestChestInteraction(view), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void HiddenOrRememberedChestCannotBePickedOrOutlined(bool remembered)
        {
            if (remembered)
            {
                floor.Fog.RestoreDiscovered(map, new DungeonTraversalPosition(new DungeonSurfacePosition(chest.Position)));
                floor.Fog.Objects[chest.InstanceId] = new DungeonObjectKnowledge { Active = true, State = chest.IsUnlocked };
            }
            terrain.ApplyFog(floor);
            Assert.That(view.IsElevationVisible, Is.False);
            Assert.That(view.IsPointerHit(view.transform.position), Is.False);
            Assert.That(party.RequestChestInteraction(view), Is.False);
            view.SetInspectionHighlight(true); Invoke(view, "LateUpdate");
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled && r.name == "Visual State Outline"), Is.Zero);
        }
        [Test] public void HoverReusesPixelOutlinePreservesBodyAndClearsOnDisable()
        {
            var body = view.GetComponent<SpriteRenderer>(); var color = body.color;
            Assert.That(view.IsPointerHit(body.bounds.center), Is.True);
            view.SetInspectionHighlight(true); Invoke(view, "LateUpdate");
            var outlines = body.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body).ToArray();
            Assert.That(outlines.Length, Is.EqualTo(8)); Assert.That(outlines.All(r => r.enabled && r.sprite == body.sprite), Is.True);
            Assert.That(body.color, Is.EqualTo(color));
            for (var i = 0; i < 3; i++) { view.SetInspectionHighlight(false); view.SetInspectionHighlight(true); Invoke(view, "LateUpdate"); }
            Assert.That(body.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(9));
            Invoke(view, "OnDisable"); Assert.That(outlines.All(r => !r.enabled), Is.True);
        }
        [Test] public void RenderHiddenChestDoesNotKeepHoverOutline()
        {
            view.SetInspectionHighlight(true); Invoke(view, "LateUpdate");
            var body = view.GetComponent<SpriteRenderer>(); body.enabled = false;
            Invoke(view, "LateUpdate");
            Assert.That(body.GetComponentsInChildren<SpriteRenderer>().All(r => !r.enabled), Is.True);
        }
        [Test] public void LockedChestKeepsKeysDuringApproachAndConsumesOneOnArrival()
        {
            PrepareUi(); LockChest();
            actor.RuntimeCharacter.Inventory.Items.Add(new ItemInstance(key, "key.1"));
            actor.RuntimeCharacter.Inventory.Items.Add(new ItemInstance(key, "key.2"));
            Assert.That(party.RequestChestInteraction(view), Is.True);
            Assert.That(actor.RuntimeCharacter.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(chest.IsUnlocked, Is.False);
            actor.SetPosition(Get<Vector2?>(party, "movementDestination").Value);
            Assert.That((bool)Invoke(party, "TickChestApproach"), Is.True);
            Assert.That(chest.IsUnlocked, Is.True);
            Assert.That(actor.RuntimeCharacter.Inventory.Items.Count, Is.EqualTo(1)); AssertLootOpened();
            view.Interact(); Assert.That(actor.RuntimeCharacter.Inventory.Items.Count, Is.EqualTo(1));
        }
        [Test] public void MissingKeyLeavesChestLockedAndDoesNotOpenLoot()
        {
            PrepareUi(); LockChest(); actor.SetPosition(Center(chest.Position + Vector2Int.left));
            Assert.That(view.TryPrimaryInteraction(view.transform.position), Is.True);
            Assert.That(chest.IsUnlocked, Is.False);
            Assert.That(canvas.GetComponentInChildren<DungeonLootWindowView>(true), Is.Null);
            Assert.That(Get<DungeonChestView>(party, "pendingChest"), Is.Null);
        }
        [Test] public void GpuRenderShowsHoverOutlineOnActualChestSprite()
        {
            var camera = new GameObject("Chest render", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.orthographic = true; camera.orthographicSize = 1.1f;
            var center = view.GetComponent<SpriteRenderer>().bounds.center;
            camera.transform.position = new Vector3(center.x, center.y, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .06f, .04f);
            var target = new RenderTexture(256, 192, 24) { filterMode = FilterMode.Point };
            var pixels = new Texture2D(256, 192, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create(); camera.targetTexture = target;
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 256, 192), 0, 0); pixels.Apply();
                var before = pixels.GetPixels32();
                view.SetInspectionHighlight(true); Invoke(view, "LateUpdate");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 256, 192), 0, 0); pixels.Apply();
                var after = pixels.GetPixels32();
                Assert.That(before.Where((value, i) => !value.Equals(after[i])).Count(), Is.GreaterThan(20));
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/chest-primary-pass-20261015/chest-hover.png"));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target);
            }
        }
        [Test] public void FeedbackMaterialIsOwnedAndRepeatedInitializationDoesNotReuseDestroyedMaterial()
        {
            var source = TMPro.TMP_Settings.defaultFontAsset.material;
            var original = source.GetColor("_OutlineColor");
            var node = new GameObject("Feedback material test", typeof(TMPro.TextMeshPro), typeof(DungeonLootFloatingText));
            node.transform.SetParent(root.transform);
            var feedback = node.GetComponent<DungeonLootFloatingText>(); var label = node.GetComponent<TMPro.TextMeshPro>();
            feedback.Initialize("Brak klucza"); var first = label.fontSharedMaterial;
            Assert.That(first, Is.Not.SameAs(source));
            feedback.Initialize("Brak podejścia"); var second = label.fontSharedMaterial;
            Assert.That(first == null, Is.True); Assert.That(second != null, Is.True);
            Assert.That(source.GetColor("_OutlineColor"), Is.EqualTo(original));
            Assert.That(second.IsKeywordEnabled("OUTLINE_ON"), Is.True);
            // These views do not receive runtime lifecycle callbacks in EditMode.
            Invoke(feedback, "OnDestroy"); Object.DestroyImmediate(node); Assert.That(second == null, Is.True);
        }

        void LockChest()
        {
            key = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(key, "<Id>k__BackingField", "chest.test.key"); Set(key, "<ItemType>k__BackingField", ItemType.Key);
            Set(definition, "<LockType>k__BackingField", ChestLockType.KeyRequired);
            Set(definition, "<RequiredKey>k__BackingField", key);
            chest = new ChestDungeonObjectInstance("chest.locked", definition, chest.Position + Vector2Int.down, null, 25);
            Assert.That(floor.TryAddObject(chest), Is.True);
            view.Initialize(party, floor, chest, Center(chest.Position), tiles, lootPrefab);
        }

        void PrepareUi()
        {
            canvas = new GameObject("Chest UI", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            Invoke(canvas.AddComponent<UIWorkspaceWindowManager>(), "Awake");
        }
        void AssertLootOpened()
        {
            var loot = canvas.GetComponentInChildren<DungeonLootWindowView>(true); Assert.That(loot, Is.Not.Null);
            Assert.That(Get<string>(loot, "chestInstanceId"), Is.EqualTo(chest.InstanceId));
            Assert.That(loot.Session, Is.SameAs(session));
        }
        Vector2 Center(Vector2Int cell) => tiles.GetCellCenterWorld(new Vector3Int(cell.x, cell.y));
        static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        static object Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, Private).Invoke(owner, arguments);
    }
}
