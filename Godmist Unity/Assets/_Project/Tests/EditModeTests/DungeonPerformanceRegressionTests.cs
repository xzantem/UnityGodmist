using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonPerformanceRegressionTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void FloorWideContextKeepsUniqueColumnsAndLayeredGrid()
        {
            var map = new DungeonFloorMap();
            for (var x = -20; x < 20; x++) for (var y = -20; y < 20; y++)
                map.TryAddFloorCell(new Vector2Int(x, y), "room");
            var upper = new DungeonSurfacePosition(Vector2Int.zero, 1);
            map.TryAddFloorCell(upper, "upper");
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
            var dungeon = new DungeonInstance("perf", 1); dungeon.TryAddFloor(floor);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
            try
            {
                var encounter = new DungeonEncounterInstance("encounter", definition, "room", new RectInt(0, 0, 1, 1));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Assert.That(new Godmist.Systems.Combat.DungeonCombatContextFactory().TryCreate(dungeon, encounter,
                    out var context, true), Is.True);
                Assert.That(context.WorldCells.Count, Is.EqualTo(1600));
                Assert.That(new HashSet<Vector2Int>(context.WorldCells).Count, Is.EqualTo(1600));
                foreach (var cell in context.WorldCells) Assert.That(context.ContainsWorldCell(cell), Is.True);
                Assert.That(context.ContainsWorldCell(new Vector2Int(30, 0)), Is.False);
                Assert.That(new Godmist.Systems.Combat.DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
                var groundPosition = Godmist.Systems.Combat.DungeonBattleGridFactory.ToBattleTraversalPosition(context,
                    new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)));
                var upperPosition = Godmist.Systems.Combat.DungeonBattleGridFactory.ToBattleTraversalPosition(context,
                    new DungeonTraversalPosition(upper));
                Assert.That(grid.IsWalkable(groundPosition), Is.True);
                Assert.That(grid.IsWalkable(upperPosition), Is.True);
                TestContext.WriteLine($"1600-column context and layered grid: {watch.Elapsed.TotalMilliseconds:F2} ms");
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ContactMeshReusesStaticGeometryAndInvalidatesMovedHiddenAndRebuiltSources()
        {
            var root = new GameObject("contact cache test", typeof(Grid));
            var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floor.transform.SetParent(root.transform, false);
            var prop = new GameObject("Prop", typeof(SpriteRenderer));
            prop.transform.SetParent(root.transform, false);
            var texture = new Texture2D(32, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var type = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows", true);
            object shadows = null;
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Fields).SetValue(view, floor.GetComponent<Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile", Fields).SetValue(view, tile);
                var wall = new GameObject("Wall template", typeof(SpriteRenderer), typeof(BoxCollider2D));
                wall.transform.SetParent(root.transform, false);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab", Fields).SetValue(view, wall.AddComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "room");
                view.Rebuild(map);
                var renderer = prop.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite; prop.transform.position = new Vector3(.5f, 1.5f);
                shadows = Activator.CreateInstance(type, root.transform, Shader.Find("Sprites/Default"));
                var rebuild = type.GetMethod("Rebuild");
                var camera = new Rect(-5, -5, 10, 10);
                void Refresh() => rebuild.Invoke(shadows, new object[] { view, floor.GetComponent<TilemapRenderer>(), camera,
                    null, Array.Empty<SpriteRenderer>(), null, new[] { renderer } });
                int Builds() => (int)type.GetProperty("RebuildCount").GetValue(shadows);
                int Quads() => (int)type.GetProperty("QuadCount").GetValue(shadows);
                Refresh();
                Assert.That(Quads(), Is.GreaterThan(0));
                for (var i = 0; i < 200; i++) Refresh();
                Assert.That(Builds(), Is.EqualTo(1), "Stationary frames must not upload another contact mesh.");
                var mesh = (Mesh)type.GetField("mesh", Fields).GetValue(shadows);
                var before = mesh.vertices;
                prop.transform.position += Vector3.right;
                Refresh(); Assert.That(Builds(), Is.EqualTo(2));
                Assert.That(mesh.vertices, Is.Not.EqualTo(before));
                renderer.enabled = false;
                Refresh(); Assert.That(Builds(), Is.EqualTo(3)); Assert.That(Quads(), Is.Zero);
                renderer.enabled = true;
                Refresh(); Assert.That(Builds(), Is.EqualTo(4)); Assert.That(Quads(), Is.GreaterThan(0));
                camera.x += .25f;
                Refresh(); Assert.That(Builds(), Is.EqualTo(5));
                map.TryAddFloorCell(new Vector2Int(-1, 1), "room");
                view.Rebuild(map);
                Refresh(); Assert.That(Builds(), Is.EqualTo(6));
            }
            finally
            {
                if (shadows != null)
                    foreach (var name in new[] { "root", "mesh", "material" })
                        Object.DestroyImmediate((Object)type.GetField(name, Fields).GetValue(shadows));
                Object.DestroyImmediate(root); Object.DestroyImmediate(tile);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SkillSlotsAreReusedAndReboundWhenActiveCharacterChanges()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
            try
            {
                var bar = root.GetComponentInChildren<CombatSkillBarView>(true);
                var method = typeof(CombatSkillBarView).GetMethod("RebuildSlots", Fields);
                var slots = (List<CombatSkillSlotView>)typeof(CombatSkillBarView).GetField("spawnedSlots", Fields).GetValue(bar);
                var factory = new Godmist.Systems.Characters.CharacterInstanceFactory();
                var definition = AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
                var first = new Godmist.Runtime.Combat.BattleUnit { Character = factory.Create(definition), CurrentActionPoints = 10000 };
                var second = new Godmist.Runtime.Combat.BattleUnit { Character = factory.Create(definition), CurrentActionPoints = 0 };
                var skills = first.Character.GetActiveSkills();
                method.Invoke(bar, new object[] { first, skills, "first" });
                Assert.That(slots.Count, Is.GreaterThan(0));
                var original = new HashSet<CombatSkillSlotView>(slots);
                var reordered = new List<Godmist.Data.Skills.SkillData>(second.Character.GetActiveSkills());
                reordered.Reverse();
                method.Invoke(bar, new object[] { second, reordered, "second" });
                Assert.That(new HashSet<CombatSkillSlotView>(slots).SetEquals(original), Is.True);
                for (var i = 0; i < slots.Count; i++)
                {
                    Assert.That(typeof(CombatSkillSlotView).GetField("skill", Fields).GetValue(slots[i]), Is.SameAs(reordered[i]));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
