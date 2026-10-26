using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class GcHotpathOptimizationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> owned = new();
        [TearDown] public void Cleanup() { foreach (var value in owned) Object.DestroyImmediate(value); owned.Clear(); }
        private T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private GameObject Node(string name) { var value = new GameObject(name); owned.Add(value); return value; }
        private static void Set(object target, string name, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, Private);
                if (field != null) { field.SetValue(target, value); return; }
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }
        private ObstacleDungeonObjectInstance Obstacle(string id, Vector2Int at)
        {
            var definition = Asset<ObstacleDungeonObjectDefinition>();
            Set(definition, "<Id>k__BackingField", id);
            Set(definition, "<BlocksLineOfSight>k__BackingField", true);
            return new ObstacleDungeonObjectInstance(id, definition, at);
        }
        private static DungeonFloorInstance Floor()
        {
            var map = new DungeonFloorMap();
            for (var x = -3; x <= 3; x++) for (var y = -3; y <= 3; y++)
                map.TryAddFloorCell(new Vector2Int(x, y), "test");
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map); return floor;
        }
        private static DungeonTraversalPosition At(int x, int y) => new(new DungeonSurfacePosition(new Vector2Int(x, y)));

        [TestCase("object.obstacle.sarcophagus")]
        [TestCase("object.obstacle.tree")]
        public void DirectFootprintMatchesAuthoredEnumerationAndDeactivation(string id)
        {
            var obstacle = Obstacle(id, new Vector2Int(-2, -1));
            var expected = obstacle.EnumerateOccupiedCells().ToArray();
            Assert.That(obstacle.EnumeratePhysicalExplorationCells(), Is.EqualTo(expected));
            for (var x = -4; x <= 2; x++) for (var y = -3; y <= 3; y++)
            {
                var cell = new Vector2Int(x, y);
                Assert.That(obstacle.OccupiesCell(cell), Is.EqualTo(expected.Contains(cell)));
                Assert.That(obstacle.OccupiesPhysicalExplorationCell(cell), Is.EqualTo(expected.Contains(cell)));
            }
            obstacle.TryDeactivate();
            foreach (var cell in expected) Assert.That(obstacle.OccupiesCell(cell), Is.False);
        }

        [Test]
        public void LiveLineOfSightMatchesPreviousTraversalAfterDoorsAndObstaclesChange()
        {
            var floor = Floor();
            var obstacle = Obstacle("object.obstacle.sarcophagus", Vector2Int.zero);
            floor.TryAddObject(obstacle);
            var door = new DoorDungeonObjectInstance("door", Asset<DoorDungeonObjectDefinition>(),
                new DungeonCellEdge(new Vector2Int(-2, 0), new Vector2Int(-1, 0)));
            floor.TryAddObject(door);
            var visibility = new DungeonVisibilitySystem();
            for (var state = 0; state < 3; state++)
            {
                if (state == 1) door.TrySetOpen(true);
                if (state == 2) obstacle.TryDeactivate();
                for (var x = -3; x <= 3; x++) for (var y = -3; y <= 3; y++)
                for (var tx = -3; tx <= 3; tx++) for (var ty = -3; ty <= 3; ty++)
                    Assert.That(visibility.HasLineOfSight(floor, At(x, y), At(tx, ty)),
                        Is.EqualTo(LegacyLineOfSight(floor, At(x, y), At(tx, ty))), $"state={state}: {x},{y} -> {tx},{ty}");
            }
        }

        [Test]
        public void OccupancyAndLiveLineOfSightDoNotAllocateAfterWarmup()
        {
            var floor = Floor(); var obstacle = Obstacle("object.obstacle.sarcophagus", Vector2Int.zero);
            floor.TryAddObject(obstacle);
            var visibility = new DungeonVisibilitySystem();
            var from = At(-3, 0); var to = At(3, 0);
            visibility.HasLineOfSight(floor, from, to); obstacle.OccupiesCell(Vector2Int.zero);
            Assert.That(CountAllocations(() => GC.KeepAlive(new byte[4096])), Is.GreaterThan(0));
            var current = CountAllocations(() => { for (var i = 0; i < 1000; i++) { visibility.HasLineOfSight(floor, from, to); obstacle.OccupiesCell(Vector2Int.zero); } });
            var legacy = CountAllocations(() => { for (var i = 0; i < 1000; i++) LegacyLineOfSight(floor, from, to); });
            TestContext.WriteLine($"1000 LOS + occupancy calls: optimized={current} allocations, legacy LOS={legacy} allocations");
            Assert.That(current, Is.Zero); Assert.That(legacy, Is.GreaterThan(0));
        }

        [Test]
        public void ExplorationScratchDoesNotAllocateBetweenTicksAndDeduplicatesCharacters()
        {
            var character = new CharacterInstance { LifeState = CharacterLifeState.Alive, CurrentHealth = 100,
                ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 20) };
            var unit = new BattleUnit { Character = character };
            var occupants = new[] { new DungeonTrapOccupant(unit, null, Vector2Int.zero), new DungeonTrapOccupant(new BattleUnit { Character = character }, null, Vector2Int.one) };
            var system = new ExplorationStatusEffectSystem(); system.Advance(.001f, occupants);
            Assert.That(CountAllocations(() => { for (var i = 0; i < 1000; i++) system.Advance(.001f, occupants); }), Is.Zero);
            Assert.That(character.ExplorationStatusElapsedSeconds, Is.EqualTo(1.001f).Within(.001f));
            system.Advance(11f, occupants);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(40));
        }

        [Test]
        public void ExplorationSharedTickServicesPreserveEachCharactersIndependentTiming()
        {
            var units = Enumerable.Range(0, 4).Select(i => new BattleUnit { Character = new CharacterInstance {
                LifeState = CharacterLifeState.Alive, CurrentHealth = 100, ExplorationStatusElapsedSeconds = i,
                ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 20) } }).ToArray();
            new ExplorationStatusEffectSystem().Advance(10f, units.Select(u => new DungeonTrapOccupant(u, null, Vector2Int.zero)).ToArray());
            Assert.That(units.Select(u => u.Character.ResourcePool.CurrentValue), Is.EqualTo(new[] { 20, 20, 40, 40 }));
            Assert.That(units.Select(u => u.Character.ExplorationStatusElapsedSeconds), Is.EqualTo(new[] { 4f, 5f, 0f, 1f }));
        }

        [Test]
        public void BodyKeyCacheStillReadsFreshFloorPositionAndRebinds()
        {
            var floor = Floor(); var view = Node("body test").AddComponent<DungeonEnemyBodyView>();
            var property = typeof(DungeonEnemyBodyView).GetProperty("BodyPosition", Private);
            var read = (Func<DungeonTraversalPosition>)Delegate.CreateDelegate(typeof(Func<DungeonTraversalPosition>), view, property.GetMethod);
            Set(view, "floor", floor);
            Set(view, "body", new DungeonEnemyBodyInstance("first", "body", Vector2Int.zero, 0, null));
            floor.TrySetActorPosition("body:first", At(1, 1)); Assert.That(read(), Is.EqualTo(At(1, 1)));
            floor.TrySetActorPosition("body:first", At(2, 1)); Assert.That(read(), Is.EqualTo(At(2, 1)));
            Assert.That(CountAllocations(() => { for (var i = 0; i < 1000; i++) read(); }), Is.Zero);
            Set(view, "body", new DungeonEnemyBodyInstance("second", "body", new Vector2Int(-1, 0), 0, null));
            Assert.That(read(), Is.EqualTo(At(-1, 0)));
            floor.TrySetActorPosition("body:second", At(-2, 0)); Assert.That(read(), Is.EqualTo(At(-2, 0)));
        }

        [Test]
        public void ReentrantExplorationFeedbackCannotClearOuterDeduplicationAndNextCallUsesNewBus()
        {
            var character = new CharacterInstance { LifeState = CharacterLifeState.Alive, CurrentHealth = 100,
                ResourcePool = new ResourcePool(ResourceType.Mana, 0, 100, 20) };
            var nestedCharacter = new CharacterInstance { LifeState = CharacterLifeState.Alive, CurrentHealth = 100 };
            var occupant = new DungeonTrapOccupant(new BattleUnit { Character = character }, null, Vector2Int.zero);
            var nested = new[] { new DungeonTrapOccupant(new BattleUnit { Character = nestedCharacter }, null, Vector2Int.zero) };
            var system = new ExplorationStatusEffectSystem();
            var firstBus = new CombatFeedbackBus(); var firstEvents = 0;
            firstBus.Published += _ => { firstEvents++; system.Advance(.1f, nested); };
            system.Advance(6f, new[] { occupant, occupant }, firstBus);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(20));
            Assert.That(firstEvents, Is.EqualTo(1));
            Assert.That(nestedCharacter.ExplorationStatusElapsedSeconds, Is.EqualTo(.1f));
            var secondBus = new CombatFeedbackBus(); var secondEvents = 0;
            secondBus.Published += _ => secondEvents++;
            system.Advance(6f, new[] { occupant }, secondBus);
            Assert.That(firstEvents, Is.EqualTo(1)); Assert.That(secondEvents, Is.EqualTo(1));
        }

        [Test]
        public void ObstacleDescriptionIsRemovedButGroundHintsRemain()
        {
            var floor = Floor(); var obstacle = Obstacle("object.obstacle.sarcophagus", Vector2Int.zero);
            floor.TryAddObject(obstacle);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Is.Empty);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.up), Is.Empty);
            floor.Map.TryAddWall(new Vector2Int(-2, 0), new Vector2Int(-1, 0));
            Assert.That(DungeonTacticalTooltip.Describe(floor, new Vector2Int(-2, 0)), Is.Not.Empty);
        }

        [Test]
        public void PreparingRichTooltipIsHiddenIdempotentAndPreservesPinnedText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            var root = Object.Instantiate(prefab); owned.Add(root);
            var tooltip = root.GetComponent<UIWorkspaceTooltipView>();
            var source = new object(); tooltip.ShowText("Test", "Pinned content", source);
            var show = (Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", Private).GetValue(tooltip);
            Assert.That(show, Is.Not.Null);
            Set(tooltip, "pendingShow", null); show();
            tooltip.PinAtCurrentPosition();
            var visible = typeof(UIWorkspaceTooltipView).GetField("visible", Private).GetValue(tooltip);
            tooltip.PrepareRichUnitContent();
            var rich = root.GetComponentInChildren<UIWorkspaceRichUnitTooltipView>(true);
            Assert.That(rich, Is.Not.Null); Assert.That(rich.gameObject.activeSelf, Is.False);
            Assert.That(rich.GetComponentsInChildren<MonoBehaviour>(true).Count(component =>
                component.GetType().Name == "UIWorkspaceRichUnitStatusIconView"), Is.EqualTo(6));
            tooltip.PrepareRichUnitContent();
            Assert.That(root.GetComponentsInChildren<UIWorkspaceRichUnitTooltipView>(true).Length, Is.EqualTo(1));
            Assert.That(tooltip.IsPinned, Is.True);
            Assert.That(typeof(UIWorkspaceTooltipView).GetField("visible", Private).GetValue(tooltip), Is.EqualTo(visible));
        }

        private static bool LegacyLineOfSight(DungeonFloorInstance floor, DungeonTraversalPosition observer, DungeonTraversalPosition target)
        {
            var map = floor?.Map;
            if (map == null || !map.TryGetSightPoint(observer, out var from) || !map.TryGetSightPoint(target, out var to)
                || map.BlocksStructuralSight(observer, target)) return false;
            foreach (var obj in floor.Objects)
            {
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door && !door.IsOpen
                    && DungeonFloorMap.SightCrossesEdge(from, to, new DungeonSurfaceEdge(door.Edge))) return false;
                if (obj is ObstacleDungeonObjectInstance obstacle && obstacle.BlocksLineOfSight)
                foreach (var occupiedCell in obstacle.EnumerateOccupiedCells())
                {
                    var cell = new DungeonSurfacePosition(occupiedCell);
                    if ((!observer.IsOnStairs && observer.Surface == cell) || (!target.IsOnStairs && target.Surface == cell)) continue;
                    if (DungeonFloorMap.SightCrossesCell(from, to, cell)) return false;
                }
            }
            return true;
        }
        private static long CountAllocations(Action action)
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid) throw new InvalidOperationException("GC.Alloc recorder unavailable");
            action(); recorder.Stop(); return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }
    }
}
