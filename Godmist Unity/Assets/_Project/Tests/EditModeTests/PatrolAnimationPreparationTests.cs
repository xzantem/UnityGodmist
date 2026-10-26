using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Godmist.Data.Combat;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class PatrolAnimationPreparationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static IDictionary Cache(string field) => (IDictionary)typeof(CharacterAnimationResolver).GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static Vector2 Facing(int index) => new(Mathf.Cos(index * Mathf.PI / 4), Mathf.Sin(index * Mathf.PI / 4));
        private static long LoadCalls(Action action) => MarkerCalls("Godmist.Animation.LoadSprite", action);
        private static long MarkerCalls(string marker, Action action)
        {
            CharacterAnimationResolver.HasImportedCharacterAnimation(string.Empty);
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, marker, 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid) throw new InvalidOperationException("Animation.LoadSprite recorder unavailable");
            action(); recorder.Stop(); return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }

        [TestCase("enemy.forest.bandit_thug")] [TestCase("enemy.forest.bandit_shooter")]
        [TestCase("enemy.forest.elf_archer")] [TestCase("enemy.forest.elf_druid")]
        [TestCase("enemy.forest.elf_poacher")] [TestCase("enemy.forest.rabid_wolf")]
        [TestCase("enemy.forest.infected_tree")] [TestCase("enemy.forest.infected_ent")]
        [TestCase("enemy.catacombs.archer")] [TestCase("enemy.catacombs.defender")]
        [TestCase("enemy.catacombs.occultist")] [TestCase("enemy.catacombs.swordsman")]
        [TestCase("enemy.catacombs.undead")] [TestCase("enemy.catacombs.ghoul")]
        [TestCase("enemy.catacombs.wraith")]
        public void PreparedEnemyDirectionsPreservePixelsGeometryTimingAndNeverLoadDuringFacingChanges(string id)
        {
            var original = new CharacterAnimationClip[16];
            for (var direction = 0; direction < 8; direction++)
            foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
            {
                Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, Facing(direction), state, out var clip), Is.True);
                original[direction * 2 + (state == CharacterAnimationState.Walk ? 1 : 0)] = clip;
            }
            Assert.That(CharacterAnimationResolver.TryGetCharacterUnit(id, out var unit), Is.True);
            var sprites = Cache("SpriteCache"); var clips = Cache("ClipCache");
            var clipKey = unit + ":n:walk"; var oldClip = clips[clipKey];
            var oldSprite = original[5].Frames[0];
            var spriteKey = sprites.Keys.Cast<string>().Single(key => ReferenceEquals(sprites[key], oldSprite));
            Sprite replacement = null;
            try
            {
                // Force exactly one real cache miss, retaining the old objects
                // so existing Editor references and pixel data remain valid.
                sprites.Remove(spriteKey); clips.Remove(clipKey);
                Assert.That(LoadCalls(() => CharacterAnimationResolver.PrepareCharacterDirections(id)), Is.EqualTo(1));
                replacement = (Sprite)sprites[spriteKey];
                Object.DestroyImmediate(replacement);
                Assert.That(LoadCalls(() => CharacterAnimationResolver.PrepareCharacterDirections(id)), Is.EqualTo(1), "Preparation must repair invalid cached Unity objects.");
                replacement = (Sprite)sprites[spriteKey];
                var prepared = new CharacterAnimationClip[16]; var spriteCount = sprites.Count;
                for (var direction = 0; direction < 8; direction++)
                foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
                {
                    Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, Facing(direction), state, out var actual), Is.True);
                    var slot = direction * 2 + (state == CharacterAnimationState.Walk ? 1 : 0); prepared[slot] = actual;
                    Assert.That(actual.FrameCount, Is.EqualTo(original[slot].FrameCount));
                    for (var frame = 0; frame < actual.FrameCount; frame++)
                    {
                        var expected = original[slot].Frames[frame]; var sprite = actual.Frames[frame];
                        Assert.That(sprite.texture, Is.SameAs(expected.texture), "Same texture pixels, no resampling.");
                        Assert.That(sprite.rect, Is.EqualTo(expected.rect)); Assert.That(sprite.pivot, Is.EqualTo(expected.pivot));
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(expected.pixelsPerUnit));
                        CollectionAssert.AreEqual(expected.vertices, sprite.vertices); CollectionAssert.AreEqual(expected.uv, sprite.uv);
                        Assert.That(actual.GetFrameDuration(frame), Is.EqualTo(original[slot].GetFrameDuration(frame)));
                    }
                }
                Assert.That(LoadCalls(() => {
                    for (var pass = 0; pass < 10; pass++) for (var direction = 0; direction < 8; direction++)
                    foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
                    {
                        CharacterAnimationResolver.TryResolveCharacter(id, Facing(direction), state, out var actual);
                        var slot = direction * 2 + (state == CharacterAnimationState.Walk ? 1 : 0);
                        Assert.That(actual, Is.SameAs(prepared[slot]));
                    }
                }), Is.Zero);
                Assert.That(sprites.Count, Is.EqualTo(spriteCount));
            }
            finally
            {
                sprites[spriteKey] = oldSprite; clips[clipKey] = oldClip;
                if (replacement != null && replacement != oldSprite) Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void UnknownCharactersKeepTheirExistingFallbackAndDoNotLoadAnything()
        {
            Assert.That(LoadCalls(() => CharacterAnimationResolver.PrepareCharacterDirections("unknown.patrol")), Is.Zero);
            Assert.That(CharacterAnimationResolver.TryResolveCharacter("unknown.patrol", Vector2.up, CharacterAnimationState.Walk, out var clip), Is.False);
            Assert.That(clip, Is.Null);
        }

        [Test]
        public void ActualEnemySpawnPreparesAllDirectionsBeforePatrolFacingAndDeduplicatesPacks()
        {
            var root = new GameObject("Prepared patrol spawn");
            var spawner = root.AddComponent<DungeonEncounterSpawner>();
            try
            {
                var map = new DungeonFloorMap();
                for (var x = 0; x < 10; x++) for (var y = 0; y < 10; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var terrain = root.AddComponent<DungeonFloorMapView>(); typeof(DungeonFloorMapView).GetField("boundMap", Private).SetValue(terrain, map);
                typeof(DungeonEncounterSpawner).GetField("floorMapView", Private).SetValue(spawner, terrain);
                typeof(DungeonEncounterSpawner).GetField("enemyViewPrefab", Private).SetValue(spawner,
                    AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"));
                var definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/ED_Forest_WolfPair.asset");
                var encounter = new DungeonEncounterInstance("prepared", definition, "room", new RectInt(2, 2, 5, 5));
                var preparations = MarkerCalls("Godmist.Animation.PrepareDirections", () =>
                    typeof(DungeonEncounterSpawner).GetMethod("SpawnEnemyViews", Private).Invoke(spawner, new object[] { encounter, floor, 1 }));
                Assert.That(preparations, Is.EqualTo(1), "Two wolves share one pack preparation.");
                var packs = (HashSet<string>)typeof(DungeonEncounterSpawner).GetField("preparedAnimationUnits", Private).GetValue(spawner);
                Assert.That(packs, Is.EquivalentTo(new[] { "rabid-wolf" })); Assert.That(spawner.EnemyViews.Count, Is.EqualTo(2));
                var prepared = (CharacterAnimationClip)Cache("ClipCache")["rabid-wolf:nw:walk"]; Assert.That(prepared, Is.Not.Null);
                var calls = LoadCalls(() => {
                    foreach (var view in spawner.EnemyViews) for (var direction = 0; direction < 8; direction++)
                        view.FaceTowards(view.Position + Facing(direction));
                });
                Assert.That(calls, Is.Zero);
                Assert.That(spawner.EnemyViews.All(view => view.BodyRenderer.sprite != null), Is.True);
                // Pack aliases share one preparation, even for another encounter.
                typeof(DungeonEncounterSpawner).GetMethod("PrepareEnemyAnimationDirections", Private).Invoke(spawner, new object[] { "enemy.catacombs.archer" });
                typeof(DungeonEncounterSpawner).GetMethod("PrepareEnemyAnimationDirections", Private).Invoke(spawner, new object[] { "enemy.catacombs.skeleton_archer" });
                Assert.That(packs.Count, Is.EqualTo(2));
                foreach (var view in spawner.EnemyViews.ToArray()) Object.DestroyImmediate(view.gameObject);
                typeof(DungeonEncounterSpawner).GetMethod("ClearSpawnedViews", Private).Invoke(spawner, null);
                Assert.That(packs, Is.Empty);
            }
            finally
            {
                foreach (var view in spawner.EnemyViews.ToArray()) if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProductionLongForestEntryPreparesEverySpawnedEnemyBeforeAnyPatrolTurn()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/patrol-animation-preload-pass-20261018/entry"));
            Directory.CreateDirectory(output);
            var initialClips = Cache("ClipCache").Count;
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = output, StageOverride = "after" };
            fixture.FrameProbe = (bootstrap, session) => {
                var spawner = (DungeonEncounterSpawner)bootstrap.GetType().GetField("encounterSpawner", Private).GetValue(bootstrap);
                var packs = (HashSet<string>)typeof(DungeonEncounterSpawner).GetField("preparedAnimationUnits", Private).GetValue(spawner);
                Assert.That(spawner.EnemyViews.Count, Is.GreaterThan(0));
                var calls = LoadCalls(() => {
                    foreach (var view in spawner.EnemyViews) for (var direction = 0; direction < 8; direction++)
                        view.FaceTowards(view.Position + Facing(direction));
                });
                Assert.That(calls, Is.Zero, "Every real enemy on the production long floor must turn without loading a sprite.");
                File.WriteAllText(Path.Combine(output, "prepared-patrols.json"), JsonUtility.ToJson(new EntryMetric {
                    initialCachedClips = initialClips, enemies = spawner.EnemyViews.Count, uniquePreparedPacks = packs.Count,
                    directionalChanges = spawner.EnemyViews.Count * 8, spriteLoadCalls = calls }, true));
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
        }

        [Serializable] private sealed class EntryMetric
        {
            public int initialCachedClips, enemies, uniquePreparedPacks, directionalChanges;
            public long spriteLoadCalls;
        }
    }
}
