using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public class DungeonLayoutSnapshotTests
    {
        private readonly List<Object> assets = new();
        private GameContentCatalog catalog;
        private DungeonDefinition definition;
        private GameSession session;
        private DungeonFloorInstance floor;

        private T Asset<T>(string id) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            if (id != null) Set(asset, "Id", id);
            return asset;
        }

        private static void Set(object target, string property, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Assert.Fail("Missing property: " + property);
        }

        [SetUp]
        public void Setup()
        {
            catalog = Asset<GameContentCatalog>(null);
            definition = Asset<DungeonDefinition>("dungeon.persist");
            Set(definition, "IsProcedural", true);
            catalog.DungeonDefinitions.Add(definition);
            var dungeon = new DungeonInstance(definition, 4205, 11);
            floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("room", DungeonRoomType.Start, "template.old"));
            var map = new DungeonFloorMap();
            for (var x = 0; x < 8; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            map.SetDifficultTerrain(new Vector2Int(3, 0));
            map.SetWallType(new DungeonCellEdge(new Vector2Int(3, 0), new Vector2Int(4, 0)), DungeonWallType.Low);
            map.SetWallType(new DungeonCellEdge(new Vector2Int(3, 0), new Vector2Int(3, 1)), DungeonWallType.Full);
            floor.TrySetMap(map);
            dungeon.TryAddFloor(floor);
            session = new GameSession(GameModeState.Exploration);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            var doorDefinition = Asset<DoorDungeonObjectDefinition>("door");
            var trapDefinition = Asset<SpikeTrapDungeonObjectDefinition>("trap");
            var obstacleDefinition = Asset<ObstacleDungeonObjectDefinition>("obstacle");
            definition.ObjectDefinitions.AddRange(new DungeonObjectDefinition[] { doorDefinition, trapDefinition, obstacleDefinition });
            floor.TryAddObject(new DoorDungeonObjectInstance("door.1", doorDefinition,
                new DungeonCellEdge(Vector2Int.zero, Vector2Int.right)));
            floor.TrySetDoorOpen("door.1", true);
            floor.TryAddObject(new SpikeTrapDungeonObjectInstance("trap.1", trapDefinition, new Vector2Int(2, 0), "group.1"));
            floor.TryAddObject(new SpikeTrapDungeonObjectInstance("trap.2", trapDefinition, new Vector2Int(5, 0), "group.1"));
            floor.TryGetObject("trap.1", out var trap);
            ((SpikeTrapDungeonObjectInstance)trap).TryTrigger();
            floor.TryAddObject(new ObstacleDungeonObjectInstance("obstacle.1", obstacleDefinition, new Vector2Int(6, 0)));
            var encounterDefinition = Asset<EncounterDefinition>("encounter.def");
            definition.EncounterDefinitions.Add(encounterDefinition);
            catalog.EncounterDefinitions.Add(encounterDefinition);
            var encounter = new DungeonEncounterInstance("encounter.1", encounterDefinition, "room", new RectInt(0, 0, 8, 1));
            encounter.TryMarkEnemyDefeated("enemy.dead");
            encounter.TryMarkEnemyBodyGenerated("enemy.dead");
            encounter.TrySetEnemyExplorationPosition("enemy.alive", new Vector2Int(7, 0));
            floor.TryAddEncounter(encounter);
            dungeon.TryStorePreRolledRewards(encounter.InstanceId, new CombatRewardResult
            {
                EnemyRewards = new List<CombatEnemyReward>
                {
                    new() { EnemyInstanceId = "enemy.alive", Gold = 57, ExperiencePerActiveCharacter = 19 },
                    new() { EnemyInstanceId = "enemy.dead", Gold = 33 }
                }
            });
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void RoundTripRetainsActorOnBalconyStairAndBodyWithoutReroll()
        {
            var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            floor.Map.TryAddFloorCell(upper, "room");
            floor.Map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            var stair = new DungeonTraversalPosition(floor.Map.Stairs.Single(), 1);
            floor.TrySetActorPosition("player:p", stair);
            floor.SetActorOffset("player:p", new Vector2(.17f, -.23f));
            floor.TrySetActorPosition("enemy:encounter.1:enemy.alive", new DungeonTraversalPosition(upper));
            floor.TrySetActorPosition("body:body.1", stair);
            var json = new GameSaveJsonSerializer().Serialize(Snapshot());
            Assert.That(new GameSaveJsonSerializer().TryDeserialize(json, out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.TryGetCurrentFloor(out var restoredFloor), Is.True);
            Assert.That(restoredFloor.ActorPositions, Is.EquivalentTo(floor.ActorPositions));
            Assert.That(restoredFloor.GetActorOffset("player:p"), Is.EqualTo(new Vector2(.17f, -.23f)));
            Assert.That(restored.ActiveDungeon.TryGetPreRolledRewards("encounter.1", out var rewards), Is.True);
            Assert.That(rewards.EnemyRewards.Single(r => r.EnemyInstanceId == "enemy.alive").Gold, Is.EqualTo(57));
        }

        [Test]
        public void FacingRoundTrip_IsIndependentOfElevation_AndOldSavesUseDefaults()
        {
            Assert.That(floor.TrySetActorFacing("enemy:encounter.1:enemy.alive", Vector2.left), Is.True);
            var json = new GameSaveJsonSerializer().Serialize(Snapshot());
            Assert.That(new GameSaveJsonSerializer().TryDeserialize(json, out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            restored.ActiveDungeon.TryGetCurrentFloor(out var restoredFloor);
            Assert.That(restoredFloor.TryGetActorFacing("enemy:encounter.1:enemy.alive", out var facing), Is.True);
            Assert.That(facing, Is.EqualTo(Vector2.left));
            saved.ActiveDungeon.FloorDeltas[0].ActorFacings = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var legacy), Is.True);
            legacy.ActiveDungeon.TryGetCurrentFloor(out var legacyFloor);
            Assert.That(legacyFloor.ActorFacings, Is.Empty);
        }

        [Test]
        public void CorruptFacingIsRejected()
        {
            Assert.That(floor.TrySetActorFacing("enemy", new Vector2(float.NaN, 1)), Is.False);
            Assert.That(floor.TrySetActorFacing("enemy", Vector2.zero), Is.False);
            var saved = Snapshot();
            saved.ActiveDungeon.FloorDeltas[0].ActorFacings.Add(new Godmist.Core.Serialization.DungeonActorFacingSaveData
                { ActorId = "enemy", X = 0, Y = 0 });
            LogAssert.Expect(LogType.Error, "Nie udało się odtworzyć zmian obiektów/encounterów w lochu 'dungeon.persist'.");
            AssertRejected(saved);
        }

        private GameSaveData Snapshot() => new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "persistence");

        [Test]
        public void FullSavePreservesFogMemoryButRecomputesCurrentVisibility()
        {
            var position = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            floor.Fog.SetVisible(new[] { position });
            floor.Fog.Objects.Add("door.1", new DungeonObjectKnowledge { Active = true, State = false });
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot()), out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var fog = restored.ActiveDungeon.Floors[0].Fog;
            Assert.That(fog.Get(position), Is.EqualTo(DungeonKnowledge.Remembered));
            Assert.That(fog.Objects["door.1"].State, Is.False);
            Assert.That(fog.Visible, Is.Empty);
        }

        [Test]
        public void AwarenessRoundTripPreservesProgressAndMissingLegacyStateStartsAtZero()
        {
            floor.GetEnemyAwareness("enemy.1").Advance(1.3f, true, false);
            var saved = Snapshot();
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].GetEnemyAwareness("enemy.1").Progress, Is.EqualTo(.65f).Within(.00001f));
            saved.ActiveDungeon.FloorDeltas[0].EnemyAwareness = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].EnemyAwareness, Is.Empty);
        }

        [TestCase(-1f)]
        [TestCase(2f)]
        [TestCase(float.NaN)]
        public void CorruptAwarenessSaveIsRejected(float progress)
        {
            var saved = Snapshot();
            saved.ActiveDungeon.FloorDeltas[0].EnemyAwareness.Add(new DungeonEnemyAwarenessSaveData { EnemyKey = "enemy.1", Progress = progress });
            LogAssert.Expect(LogType.Error, "Nie udało się odtworzyć zmian obiektów/encounterów w lochu 'dungeon.persist'.");
            AssertRejected(saved);
        }

        [Test]
        public void DuplicateAwarenessSaveIsRejected()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.FloorDeltas[0].EnemyAwareness.Add(new DungeonEnemyAwarenessSaveData { EnemyKey = "enemy.1", Progress = .2f });
            saved.ActiveDungeon.FloorDeltas[0].EnemyAwareness.Add(new DungeonEnemyAwarenessSaveData { EnemyKey = "enemy.1", Progress = .3f });
            LogAssert.Expect(LogType.Error, "Nie udało się odtworzyć zmian obiektów/encounterów w lochu 'dungeon.persist'.");
            AssertRejected(saved);
        }

        private void AssertRejected(GameSaveData saved)
        {
            LogAssert.Expect(LogType.Error, "Nie udało się odtworzyć aktywnego lochu 'dungeon.persist' (seed: 4205).");
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out _), Is.False);
        }

        [Test]
        public void StationaryHomePersistsSeparatelyFromCurrentPositionAndLegacyDefaultsToPatrol()
        {
            floor.Encounters[0].StationaryEnemyHomes.Add("enemy.alive", new Vector2Int(6, 0));
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot()), out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var encounter = restored.ActiveDungeon.Floors[0].Encounters[0];
            Assert.That(encounter.StationaryEnemyHomes["enemy.alive"], Is.EqualTo(new Vector2Int(6,0)));
            Assert.That(encounter.EnemyExplorationPositions["enemy.alive"], Is.EqualTo(new Vector2Int(7,0)));
            saved.ActiveDungeon.Layout[0].Encounters[0].StationaryHomes = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].Encounters[0].StationaryEnemyHomes, Is.Empty);
        }

        [Test]
        public void RoundTripRetainsGeometryObjectsStatesAndRewardsAfterPoolReplacement()
        {
            var serializer = new GameSaveJsonSerializer();
            var json = serializer.Serialize(Snapshot());
            catalog.DungeonObjectDefinitions.AddRange(definition.ObjectDefinitions);
            definition.ObjectDefinitions.Clear();
            definition.EncounterDefinitions.Clear();
            Set(definition, "MinFloorCount", 4);
            Set(definition, "MaxFloorCount", 4);
            Assert.That(serializer.TryDeserialize(json, out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors.Count, Is.EqualTo(1));
            Assert.That(restored.ActiveDungeon.HasPlacedContent, Is.True);
            var restoredFloor = restored.ActiveDungeon.Floors[0];
            Assert.That(restoredFloor.Map.FloorCells, Is.EquivalentTo(floor.Map.FloorCells));
            Assert.That(restoredFloor.Map.IsDifficultTerrain(new Vector2Int(3, 0)), Is.True);
            Assert.That(restoredFloor.Map.GetWallType(new DungeonCellEdge(new Vector2Int(3, 0), new Vector2Int(4, 0))), Is.EqualTo(DungeonWallType.Low));
            Assert.That(restoredFloor.TryGetObject("door.1", out var door), Is.True);
            Assert.That(((DoorDungeonObjectInstance)door).IsOpen, Is.True);
            Assert.That(restoredFloor.TryGetObject("trap.1", out var trap), Is.True);
            Assert.That(((SpikeTrapDungeonObjectInstance)trap).IsTriggered, Is.True);
            Assert.That(((SpikeTrapDungeonObjectInstance)trap).ActivationGroupId, Is.EqualTo("group.1"));
            Assert.That(restoredFloor.Objects.Count, Is.EqualTo(4));
            Assert.That(restoredFloor.Encounters[0].IsEnemyDefeated("enemy.dead"), Is.True);
            Assert.That(restoredFloor.Encounters[0].EnemyExplorationPositions["enemy.alive"], Is.EqualTo(new Vector2Int(7, 0)));
            Assert.That(restored.ActiveDungeon.TryGetPreRolledRewards("encounter.1", out var rewards), Is.True);
            Assert.That(rewards.EnemyRewards.Count, Is.EqualTo(1));
            Assert.That(rewards.EnemyRewards[0].Gold, Is.EqualTo(57));
            var again = new GameSessionSaveSnapshotFactory().CreateSnapshot(restored, "again");
            Assert.That(new GameSessionRestoreFactory().TryCreate(again, catalog, out var twice), Is.True);
            Assert.That(twice.ActiveDungeon.Floors[0].Objects.Count, Is.EqualTo(4));
        }

        [TestCase(DungeonLayoutSnapshot.CurrentVersion + 1)]
        [TestCase(-1)]
        public void UnknownLayoutVersionDoesNotFallBackToGeneration(int version)
        {
            var saved = Snapshot();
            saved.ActiveDungeon.LayoutVersion = version;
            AssertRejected(saved);
        }

        [Test]
        public void ElevatedSurfacesAndStairsRoundTripWithoutFlattening()
        {
            var upper = new DungeonSurfacePosition(new Vector2Int(1, 0), 1);
            var lower = new DungeonSurfacePosition(Vector2Int.zero);
            Assert.That(floor.Map.TryAddFloorCell(upper, "room"), Is.True);
            Assert.That(floor.Map.TryAddStairs(lower, upper), Is.True);
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot()), out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var map = restored.ActiveDungeon.Floors[0].Map;
            Assert.That(map.Surfaces, Is.EquivalentTo(floor.Map.Surfaces));
            Assert.That(map.Stairs, Is.EquivalentTo(floor.Map.Stairs));
            Assert.That(map.FloorCells.Count, Is.EqualTo(8));
        }

        [Test]
        public void UpperWallsAndPassageRoundTripWithGateOnCorrectLevel()
        {
            floor.TryAddRoom(new DungeonRoomInstance("upper", DungeonRoomType.Normal));
            var first = new DungeonSurfacePosition(Vector2Int.zero, 1);
            var second = new DungeonSurfacePosition(Vector2Int.right, 1);
            floor.Map.TryAddFloorCell(first, "room");
            floor.Map.TryAddFloorCell(second, "upper");
            var edge = new DungeonSurfaceEdge(first, second);
            floor.Map.SetWallType(edge, DungeonWallType.Railing);
            floor.TryAddConnection("room", "upper");
            var c = floor.Connections[0];
            c.TrySetDirectPassage(first, second, 1);
            c.TrySetProgressionGate("upper.gate", "upper.condition", "upper");
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot()), out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var f = restored.ActiveDungeon.Floors[0];
            Assert.That(f.Map.GetWallType(edge), Is.EqualTo(DungeonWallType.Railing));
            Assert.That(f.Map.WallEdges.Contains(edge.CellEdge), Is.False);
            Assert.That(f.Connections[0].DoorwayLevel, Is.EqualTo(1));
            Assert.That(f.Map.ClosedProgressionGateEdges, Is.Empty);
            Assert.That(f.Map.SurfaceWallEdges, Is.EquivalentTo(floor.Map.SurfaceWallEdges));
        }

        [Test]
        public void WallWithoutSurfaceOnItsLevelIsRejected()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.Layout[0].Walls[0].Level = 8;
            AssertRejected(saved);
        }

        [Test]
        public void PassageWithoutSurfaceOnItsLevelIsRejected()
        {
            floor.TryAddRoom(new DungeonRoomInstance("other", DungeonRoomType.Normal));
            floor.TryAddConnection("room", "other");
            floor.Connections[0].TrySetDirectPassage(Vector2Int.zero, Vector2Int.right, 1);
            var saved = Snapshot();
            saved.ActiveDungeon.Layout[0].Connections[0].Level = 8;
            AssertRejected(saved);
        }

        [Test]
        public void StairPathRoundTripRetainsInteriorPositions()
        {
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            var path = new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.one, new Vector2Int(2, 1), new Vector2Int(2, 0) };
            floor.Map.TryAddFloorCell(second, "room"); floor.Map.TryAddStairs(first, second, path);
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot()), out var saved), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var map = restored.ActiveDungeon.Floors[0].Map;
            Assert.That(map.TryGetStairPath(new DungeonStairConnection(first, second), out var restoredPath), Is.True);
            Assert.That(restoredPath, Is.EqualTo(path));
            Assert.That(map.TryGetTraversalCell(new DungeonTraversalPosition(new DungeonStairConnection(first, second), 2), out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(Vector2Int.one));
        }

        [Test]
        public void InvalidSavedStairPathRejectsWholeSnapshot()
        {
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(Vector2Int.right, 1);
            floor.Map.TryAddFloorCell(second, "room"); floor.Map.TryAddStairs(first, second);
            var saved = Snapshot(); saved.ActiveDungeon.Layout[0].Stairs[0].PathCells[1].X = 9;
            AssertRejected(saved);
        }

        [Test]
        public void VersionTwoShortStairsRestoreWithoutPathField()
        {
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(Vector2Int.right, 1);
            floor.Map.TryAddFloorCell(second, "room"); floor.Map.TryAddStairs(first, second);
            var saved = Snapshot(); saved.ActiveDungeon.LayoutVersion = 2;
            saved.ActiveDungeon.Layout[0].Stairs[0].PathCells = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].Map.TryGetStairPath(new DungeonStairConnection(first, second), out var path), Is.True);
            Assert.That(path.Count, Is.EqualTo(2));
        }

        [Test]
        public void VersionTwoUnresolvedLongStairsArePreservedWithoutGuessingARoute()
        {
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(new Vector2Int(3, 0), 1);
            floor.Map.TryAddFloorCell(second, "room"); floor.Map.TryAddStairs(first, second);
            var saved = Snapshot(); saved.ActiveDungeon.LayoutVersion = 2;
            saved.ActiveDungeon.Layout[0].Stairs[0].PathCells = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var map = restored.ActiveDungeon.Floors[0].Map;
            Assert.That(map.Stairs.Count(), Is.EqualTo(1));
            Assert.That(map.TryGetStairPath(new DungeonStairConnection(first, second), out _), Is.False);
            Assert.That(restored.ActiveDungeon.HasPlacedContent, Is.True);
        }

        [Test]
        public void VersionOneLayoutRestoresAtLevelZero()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.LayoutVersion = 1;
            saved.ActiveDungeon.Layout[0].Stairs = null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].Map.HasElevatedSurfaces, Is.False);
        }

        [Test]
        public void StairWithMissingEndpointRejectsWholeSnapshot()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.Layout[0].Stairs.Add(new DungeonStairSaveData
            { FirstX = 0, FirstY = 0, FirstLevel = 0, SecondX = 1, SecondY = 0, SecondLevel = 1 });
            AssertRejected(saved);
        }

        [Test]
        public void MissingObjectDefinitionFailsInsteadOfReplacingTheObject()
        {
            var saved = Snapshot();
            definition.ObjectDefinitions.Clear();
            AssertRejected(saved);
        }

        [Test]
        public void DuplicateObjectIdentityIsRejected()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.Layout[0].Objects[1].Id = "door.1";
            AssertRejected(saved);
        }

        [Test]
        public void EmptyLayoutIsNotTreatedAsLegacy()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.Layout.Clear();
            AssertRejected(saved);
        }

        [Test]
        public void LegacySeedSaveUpgradesOnNextSaveAndSurvivesTemplateRemoval()
        {
            var templates = Asset<DungeonRoomTemplateCatalog>("templates");
            foreach (var roomType in new[] { DungeonRoomType.Start, DungeonRoomType.Normal, DungeonRoomType.Corridor, DungeonRoomType.Final })
            {
                var template = Asset<DungeonRoomTemplateDefinition>("template." + roomType);
                Set(template, "RoomType", roomType);
                Set(template, "SizeInCells", new Vector2Int(8, 8));
                templates.Templates.Add(template);
            }
            Set(definition, "RoomTemplateCatalog", templates);
            Set(definition, "LayoutProfile", Asset<DungeonLayoutProfile>(null));
            Set(definition, "MinRoomCountPerFloor", 3);
            Set(definition, "MaxRoomCountPerFloor", 3);
            Set(definition, "MinEncounterCountPerFloor", 0);
            Set(definition, "MaxEncounterCountPerFloor", 0);
            Set(definition, "MinObjectCountPerFloor", 0);
            Set(definition, "MaxObjectCountPerFloor", 0);
            definition.ObjectDefinitions.Clear();
            definition.EncounterDefinitions.Clear();
            var saved = GameSaveData.CreateNew("legacy");
            saved.Party.MaxActiveMembers = 5;
            // Literal old payload exercises missing-field deserialization, not field initializers.
            saved.ActiveDungeon = JsonUtility.FromJson<DungeonSaveData>(
                "{\"DungeonId\":\"dungeon.persist\",\"Seed\":731,\"DungeonLevel\":11,\"CurrentFloorIndex\":0}");
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var legacy), Is.True);
            var upgraded = new GameSessionSaveSnapshotFactory().CreateSnapshot(legacy, "upgraded");
            Assert.That(upgraded.ActiveDungeon.LayoutVersion, Is.EqualTo(DungeonLayoutSnapshot.CurrentVersion));
            templates.Templates.Clear();
            Assert.That(new GameSessionRestoreFactory().TryCreate(upgraded, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.Floors[0].Map.FloorCells,
                Is.EquivalentTo(legacy.ActiveDungeon.Floors[0].Map.FloorCells));
        }

        [Test]
        public void MissingTrapStateIsRejectedInsteadOfRearmingIt()
        {
            var saved = Snapshot();
            saved.ActiveDungeon.FloorDeltas[0].ObjectDeltas.RemoveAt(1);
            AssertRejected(saved);
        }

        [Test]
        public void ChestAndUnclaimedRewardItemsRetainIdentityWithoutDuplicatingClaimedRewards()
        {
            var itemDefinition = Asset<ItemDefinition>("item");
            catalog.ItemDefinitions.Add(itemDefinition);
            var chestDefinition = Asset<ChestDungeonObjectDefinition>("chest");
            definition.ObjectDefinitions.Add(chestDefinition);
            floor.TryAddObject(new ChestDungeonObjectInstance("chest.1", chestDefinition, new Vector2Int(1, 0),
                new[] { new ItemInstance(itemDefinition, "chest.item") }));
            session.ActiveDungeon.TryGetPreRolledRewards("encounter.1", out var rewards);
            rewards.EnemyRewards[0].Items.Add(new ItemInstance(itemDefinition, "alive.item"));
            var claimed = new ItemInstance(itemDefinition, "dead.item");
            rewards.EnemyRewards[1].Items.Add(claimed);
            floor.TryAddEnemyBody(new DungeonEnemyBodyInstance("body", "enemy.dead", new Vector2Int(7, 0), 33, new[] { claimed }));
            var saved = Snapshot();
            Assert.That(saved.ItemInstances.Count, Is.EqualTo(3));
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            Assert.That(restored.ActiveDungeon.TryGetPreRolledRewards("encounter.1", out var restoredRewards), Is.True);
            Assert.That(restoredRewards.EnemyRewards[0].Items[0].InstanceId, Is.EqualTo("alive.item"));
            Assert.That(restored.ActiveDungeon.Floors[0].EnemyBodies[0].Items[0].InstanceId, Is.EqualTo("dead.item"));
            restored.ActiveDungeon.Floors[0].TryGetObject("chest.1", out var chest);
            Assert.That(((ChestDungeonObjectInstance)chest).Items[0].InstanceId, Is.EqualTo("chest.item"));
        }

        [Test]
        public void PartiallySatisfiedGateRetainsItsSourcesAndCanStillBeOpened()
        {
            floor.TryAddRoom(new DungeonRoomInstance("branch", DungeonRoomType.Optional, isRequiredProgressionRoom: true));
            floor.TryAddConnection("room", "branch");
            var connection = floor.Connections[0];
            connection.TrySetDirectPassage(Vector2Int.zero, Vector2Int.right, 1);
            connection.TrySetProgressionGate("gate", "condition", "branch");
            connection.TrySetProgressionRequirementSources(new[] { "lever.1", "lever.2" });
            var leverDefinition = Asset<LeverDungeonObjectDefinition>("lever");
            definition.ObjectDefinitions.Add(leverDefinition);
            var first = new LeverDungeonObjectInstance("lever.1", leverDefinition, new Vector2Int(1, 0), true);
            var second = new LeverDungeonObjectInstance("lever.2", leverDefinition, new Vector2Int(7, 0), true);
            first.TryBindCondition("condition");
            second.TryBindCondition("condition");
            first.TryRestoreActivated(true);
            floor.TryAddObject(first);
            floor.TryAddObject(second);
            floor.TryRestoreProgressionGateState("gate", false);
            Assert.That(new GameSessionRestoreFactory().TryCreate(Snapshot(), catalog, out var restored), Is.True);
            var restoredFloor = restored.ActiveDungeon.Floors[0];
            Assert.That(restoredFloor.Connections[0].IsProgressionGateUnlocked, Is.False);
            Assert.That(restoredFloor.TrySatisfyProgressionCondition("condition", "lever.2"), Is.True);
            Assert.That(restoredFloor.Connections[0].IsProgressionGateUnlocked, Is.True);
        }
    }
}
