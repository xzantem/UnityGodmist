using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ScoutKnifeLandingTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        GameObject root, fogObject;
        CombatUnitViewSpawner spawner;
        CombatSession session;
        BattleUnit owner, target;
        UnityEngine.Random.State randomState;
        static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Fields).GetValue(value);
        static void Set(object value, string field, object data) => value.GetType().GetField(field, Fields).SetValue(value, data);
        static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Fields).Invoke(value, args);
        BattleUnit Unit(bool player, int x)
        {
            var c = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            c.CharacterId = Guid.NewGuid().ToString(); c.Stats.MaxHealth = new Stat(5000); c.CurrentHealth = 5000;
            c.Stats.MinAttack = new Stat(100); c.Stats.MaxAttack = new Stat(100); c.Stats.Dodge = new Stat(0);
            c.Stats.CritChance = new Stat(0); c.WeaponAccuracy = 10000; c.ResourcePool.CurrentValue = 200;
            return new BattleUnit { Character = c, IsPlayerSide = player, Position = new GridPosition(x, 3), MaxKnives = 5,
                AvailableKnives = 5, CurrentActionPoints = 10000, MaxActionPoints = 10000 };
        }
        [SetUp] public void Setup()
        {
            randomState = UnityEngine.Random.state; UnityEngine.Random.InitState(7401);
            root = new GameObject("Knife landing test"); spawner = root.AddComponent<CombatUnitViewSpawner>();
            Set(spawner, "unitsRoot", root.transform); Set(spawner, "gridOrigin", new Vector2(10, -4)); Set(spawner, "cellSize", 1.25f);
            owner = Unit(true, 1); target = Unit(false, 4);
            session = new CombatSession { Grid = new BattleGrid(12, 8), Units = new List<BattleUnit> { owner, target }, ActiveUnit = owner };
        }
        [TearDown] public void Cleanup()
        {
            foreach (var visual in root.GetComponentsInChildren<CombatUnitVisualStateView>(true)) Call(visual, "OnDestroy");
            if (fogObject != null) Object.DestroyImmediate(fogObject);
            Object.DestroyImmediate(root); UnityEngine.Random.state = randomState;
        }
        KnifeCombatMapObject Knife(bool hit = true)
        {
            var knife = new KnifeCombatMapObject("knife.test", new GridPosition(5, 3), owner.Character.CharacterId, 1)
                { LandingOrigin = target.Position, LandingFromHit = hit };
            session.MapObjects.Add(knife); return knife;
        }
        void Sync() => Call(spawner, "SynchronizeKnifeViews", session, true);
        SpriteRenderer View(KnifeCombatMapObject knife) => Get<Dictionary<KnifeCombatMapObject, SpriteRenderer>>(spawner, "knifeViews")[knife];
        Vector3 Destination(KnifeCombatMapObject knife) => new Vector3(10 + knife.Position.X * 1.25f, -4 + knife.Position.Y * 1.25f - .06f, 0);

        [TestCase(true)] [TestCase(false)] public void RealSkillHitAndMissTagTheDropWithoutChangingKnifeCost(bool hit)
        {
            var skill = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_KnifeToss.asset"));
            try
            {
                if (!hit)
                {
                    owner.Character.WeaponAccuracy = 0;
                    var state = new SerializedObject(skill); state.FindProperty("<SkillAccuracy>k__BackingField").floatValue = 0; state.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, owner, skill, target), Is.True);
                var knife = session.MapObjects.OfType<KnifeCombatMapObject>().Single();
                Assert.That(knife.LandingOrigin, Is.EqualTo(target.Position)); Assert.That(knife.LandingFromHit, Is.EqualTo(hit));
                Assert.That(owner.AvailableKnives, Is.EqualTo(4)); Assert.That(session.PendingScoutKnifeActions, Is.Empty);
                Assert.That(target.Character.CurrentHealth < 5000, Is.EqualTo(hit));
            }
            finally { Object.DestroyImmediate(skill); }
        }
        [Test] public void DeathReleaseGetsActualOriginAndEmbeddedKnifeDoesNotProduceGroundCopy()
        {
            session.PendingScoutKnifeActions["embedded"] = new PendingScoutKnives { Owner = owner, DropPosition = target.Position, Count = 0 };
            target.ScoutState.Knives.Add(new EmbeddedScoutKnife { Owner = owner });
            ScoutCombatSystem.FinishKnifeAction(session, "embedded", true); Assert.That(session.MapObjects, Is.Empty);
            ScoutCombatSystem.ReleaseKnivesOnDeath(session, target);
            var knife = session.MapObjects.OfType<KnifeCombatMapObject>().Single();
            Assert.That(knife.LandingOrigin, Is.EqualTo(target.Position)); Assert.That(knife.LandingFromHit, Is.True);
            Assert.That(target.ScoutState.Knives, Is.Empty);
        }
        [Test] public void PreservedKnifeAndFreeMeleeDoNotCreateLandingObject()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_KnifeToss.asset");
            Assert.That(ScoutKnifeSystem.TrySpendAndDrop(session, owner, skill, new[] { target }, preventConsumption: true), Is.True);
            owner.Position = new GridPosition(3, 3);
            Assert.That(ScoutKnifeSystem.TrySpendAndDrop(session, owner, skill, new[] { target }), Is.True);
            Assert.That(session.MapObjects, Is.Empty); Assert.That(owner.AvailableKnives, Is.EqualTo(5)); Sync();
        }
        [Test] public void LegacyDirectDropAlsoProvidesGroundLevelOrigin()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_KnifeToss.asset");
            ScoutKnifeSystem.TrySpendAndDrop(session, owner, skill, new[] { target });
            var knife = session.MapObjects.OfType<KnifeCombatMapObject>().Single();
            Assert.That(knife.LandingOrigin, Is.EqualTo(target.Position)); Assert.That(knife.LandingFromHit, Is.False);
        }
        [Test] public void KnifeImmediatelyRendersAtItsPickupPositionWithoutAnyMovementOrRotation()
        {
            var knife = Knife(); Sync(); var view = View(knife); var sprite = view.sprite; var scale = view.transform.localScale;
            Assert.That(view.transform.position, Is.EqualTo(Destination(knife)));
            Assert.That(sprite.name, Does.StartWith("catacombs-ground-dagger-v2"), "A stale cache must not substitute the fallback sprite.");
            Assert.That(knife.LandingOrigin, Is.Null); Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
            for (var i = 0; i < 5; i++)
            {
                Sync();
                Assert.That(view.transform.position, Is.EqualTo(Destination(knife)));
                Assert.That(view.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            Assert.That(view.transform.position, Is.EqualTo(Destination(knife))); Assert.That(view.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.sprite, Is.SameAs(sprite)); Assert.That(view.transform.localScale, Is.EqualTo(scale));
            Assert.That(session.MapObjects.Single(), Is.SameAs(knife)); Assert.That(knife.Position, Is.EqualTo(new GridPosition(5, 3)));
            Assert.That(owner.AvailableKnives, Is.EqualTo(5));
        }
        [Test] public void MissImmediatelyAppearsOnTheGroundWithoutAHeightOffset()
        {
            var knife = Knife(false); Sync();
            Assert.That(View(knife).transform.position, Is.EqualTo(Destination(knife)));
        }
        [Test] public void CachedDaggerRestoresPointFilterForNewPresentation()
        {
            var first = Knife(); Sync(); var texture = View(first).sprite.texture; texture.filterMode = FilterMode.Bilinear;
            var second = Knife(); second.Position = new GridPosition(6, 3); Sync();
            Assert.That(View(second).sprite.texture, Is.SameAs(texture));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }
        [Test] public void DestroyedCachedDaggerIsRecreatedWithoutReplayingTheLanding()
        {
            var knife = Knife(); Sync(); var old = View(knife).sprite;
            foreach (var visual in View(knife).GetComponentsInChildren<CombatUnitVisualStateView>()) Call(visual, "OnDestroy");
            Object.DestroyImmediate(old); Object.DestroyImmediate(View(knife).gameObject); Sync();
            Assert.That(View(knife).sprite, Is.Not.Null); Assert.That(View(knife).sprite.name, Does.StartWith("catacombs-ground-dagger-v2"));
            Assert.That(View(knife).sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(View(knife).transform.position, Is.EqualTo(Destination(knife)));
        }
        [Test] public void ExistingKnivesDoNotAnimateAndConsumedHintsCannotReplayAfterViewRecreation()
        {
            var old = new KnifeCombatMapObject("old", new GridPosition(3, 2), owner.Character.CharacterId, 1); session.MapObjects.Add(old); Sync();
            Assert.That(View(old).transform.position, Is.EqualTo(Destination(old)));
            var knife = Knife(); Sync();
            foreach (var visual in View(knife).GetComponentsInChildren<CombatUnitVisualStateView>()) Call(visual, "OnDestroy");
            Object.DestroyImmediate(View(knife).gameObject); Sync();
            Assert.That(View(knife).transform.position, Is.EqualTo(Destination(knife)));
        }
        [Test] public void ImmediateRecoveryRemovesOneObjectAndItsPresentation()
        {
            var knife = Knife(); Sync(); owner.AvailableKnives = 4; owner.Position = knife.Position;
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.EqualTo(1));
            foreach (var visual in View(knife).GetComponentsInChildren<CombatUnitVisualStateView>()) Call(visual, "OnDestroy");
            Sync(); Assert.That(session.MapObjects, Is.Empty);
            Assert.That(Get<Dictionary<KnifeCombatMapObject, SpriteRenderer>>(spawner, "knifeViews"), Is.Empty); Assert.That(owner.AvailableKnives, Is.EqualTo(5));
        }
        [Test] public void HoverIsAvailableImmediatelyAndReusesExistingRenderers()
        {
            var knife = Knife(); Sync(); spawner.SetKnifeHover(knife, true);
            bool Outlined() => View(knife).GetComponentsInChildren<SpriteRenderer>().Any(r => r != View(knife) && r.enabled);
            Assert.That(Outlined(), Is.True);
            var count = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            for (var i = 0; i < 5; i++) { spawner.SetKnifeHover(knife, false); spawner.SetKnifeHover(knife, true); }
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(count));
        }
        [Test] public void PresentationHintsAreExcludedFromSerialization()
        {
            foreach (var name in new[] { "LandingOrigin", "LandingFromHit" })
                Assert.That(typeof(KnifeCombatMapObject).GetField(name).GetCustomAttribute<NonSerializedAttribute>(), Is.Not.Null);
        }
        DungeonFloorInstance Fog()
        {
            var map = new DungeonFloorMap(); for (var x = 0; x < 12; x++) for (var y = 0; y < 8; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            session.DungeonContext = new DungeonCombatContext("test", 0, "encounter", "room", Vector2Int.zero, Vector2Int.right,
                map.FloorCells, Array.Empty<DungeonCellEdge>(), Array.Empty<DungeonCombatMapObjectSnapshot>(), surfaceMap: map);
            var floor = new DungeonFloorInstance(0); fogObject = new GameObject("Landing fog"); var view = fogObject.AddComponent<DungeonFloorMapView>();
            typeof(DungeonFloorMapView).GetProperty("FogFloor").SetValue(view, floor);
            Call(Get<object>(view, "elevation"), "Bind", map, 0); spawner.ConfigureDungeon(null, view); return floor;
        }
        DungeonTraversalPosition At(GridPosition p) => new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(p.X, p.Y)));
        [TestCase(true)] [TestCase(false)] public void DestinationVisibilityControlsTheStaticKnifeWithoutAnyReplay(bool hideOrigin)
        {
            var knife = Knife(); var floor = Fog(); floor.Fog.SetVisible(new[] { At(hideOrigin ? knife.Position : target.Position) }); Sync();
            Assert.That(knife.LandingOrigin, Is.Null);
            Assert.That(View(knife).forceRenderingOff, Is.EqualTo(!hideOrigin));
            floor.Fog.SetVisible(new[] { At(knife.Position), At(target.Position) }); Sync();
            Assert.That(View(knife).transform.position, Is.EqualTo(Destination(knife)));
        }
        [Test] public void FogHidesAndRevealsTheKnifeAtTheSameFixedPosition()
        {
            var knife = Knife(); var floor = Fog(); floor.Fog.SetVisible(new[] { At(knife.Position), At(target.Position) }); Sync();
            var position = View(knife).transform.position;
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); Sync(); Assert.That(View(knife).forceRenderingOff, Is.True);
            floor.Fog.SetVisible(new[] { At(knife.Position), At(target.Position) }); Sync();
            Assert.That(View(knife).forceRenderingOff, Is.False); Assert.That(View(knife).transform.position, Is.EqualTo(position));
        }
        [Test] public void RenderRepeatedStaticPickupWithApprovedSpriteAndUnchangedScale()
        {
            Set(spawner, "gridOrigin", Vector2.zero); Set(spawner, "cellSize", 1f);
            var knife = Knife(); Sync(); var view = View(knife);
            var actorObject = new GameObject("Contact reference", typeof(SpriteRenderer)); actorObject.transform.SetParent(root.transform);
            var actor = actorObject.GetComponent<SpriteRenderer>();
            CharacterAnimationResolver.TryResolveCharacter("jerzy", Vector2.down, CharacterAnimationState.Idle, out var idle);
            actor.sprite = idle.Frames[0]; actor.transform.localScale = Vector3.one * .7f; actor.transform.position = new Vector3(4, 3, 0); actor.sortingOrder = 1;
            var cameraObject = new GameObject("Landing render camera", typeof(Camera)); var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.5f; camera.transform.position = new Vector3(4.5f, 3.5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .12f); camera.cullingMask = 1 << 31;
            var image = new Texture2D(1280, 256, TextureFormat.RGBA32, false); var targetTexture = new RenderTexture(256, 256, 24); var previous = RenderTexture.active;
            try
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                targetTexture.Create(); camera.targetTexture = targetTexture;
                for (var i = 0; i < 5; i++)
                {
                    Sync(); Assert.That(view.transform.position, Is.EqualTo(new Vector3(5, 3 - .06f, 0)));
                    camera.Render(); RenderTexture.active = targetTexture; image.ReadPixels(new Rect(0, 0, 256, 256), i * 256, 0);
                }
                image.Apply(); var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/knife-static-20261020")); Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "knife-static-stages.png"), image.EncodeToPNG());
                Assert.That(image.GetPixels32().Count(c => c.r > 100 && c.g > 80), Is.GreaterThan(500));
                Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one * .7f));
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; targetTexture.Release(); Object.DestroyImmediate(image); Object.DestroyImmediate(targetTexture); Object.DestroyImmediate(cameraObject); }
        }
    }
}
