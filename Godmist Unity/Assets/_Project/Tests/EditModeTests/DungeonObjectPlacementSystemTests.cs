using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRoomContentCompletenessTests
    {
        private readonly List<UnityEngine.Object> assets = new();
        private DungeonDefinition definition;
        private DungeonRoomTemplateDefinition template;

        private T Asset<T>(string id) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            Set(asset, "Id", id);
            return asset;
        }

        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new InvalidOperationException("Missing test field: " + name);
        }

        [SetUp]
        public void Setup()
        {
            definition = Asset<DungeonDefinition>("content.test");
            var catalog = Asset<DungeonRoomTemplateCatalog>("catalog");
            template = Asset<DungeonRoomTemplateDefinition>("room.test");
            Set(template, "DisplayName", "Content test");
            Set(template, "SizeInCells", new Vector2Int(4, 4));
            catalog.Templates.Add(template);
            Set(definition, "RoomTemplateCatalog", catalog);
            Set(definition, "MinObjectCountPerFloor", 0);
            Set(definition, "MaxObjectCountPerFloor", 0);
            definition.ObjectDefinitions.Add(Asset<ObstacleDungeonObjectDefinition>("pillar"));
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private void Anchor(string id, int x, bool required = false, float chance = 1f,
            string dependency = null, int width = 1, string key = "test.content")
            => template.SemanticAnchors.Add(new DungeonSemanticAnchorData(id,
                DungeonSemanticAnchorType.Obstacle, new Vector2Int(x, 0), new Vector2Int(width, 1),
                chance, required, dependency, distributionKey: key));

        private DungeonInstance Dungeon(int seed = 42, int roomCount = 1, DungeonRoomType type = DungeonRoomType.Normal)
        {
            var dungeon = new DungeonInstance(definition, seed);
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var i = 0; i < roomCount; i++)
            {
                var roomId = "room." + i;
                floor.TryAddRoom(new DungeonRoomInstance(roomId, type, template.Id, new Vector2Int(i * 5, 0)));
                for (var x = 0; x < 4; x++)
                    for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(i * 5 + x, y), roomId);
            }
            floor.TrySetMap(map);
            dungeon.TryAddFloor(floor);
            return dungeon;
        }

        [TestCase(1)]
        [TestCase(12)]
        [TestCase(30)]
        public void RequiredFurnitureIgnoresFloorCapsAndRoomCount(int count)
        {
            Anchor("a", 0, true);
            Anchor("b", 1, true);
            var dungeon = Dungeon(roomCount: count);
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(count * 2));
        }

        [Test]
        public void MatureTreesOccupyTheSameTwoByTwoFootprintTheyReserve()
        {
            definition.ObjectDefinitions.Clear();
            var tree=Asset<ObstacleDungeonObjectDefinition>("object.forest.tree.prototype");definition.ObjectDefinitions.Add(tree);
            Anchor("a",0,true,width:3);Anchor("b",1,true,width:2);
            var dungeon=Dungeon();new DungeonObjectPlacementSystem().Place(dungeon);
            var occupied=new HashSet<Vector2Int>();
            var trees=dungeon.Floors[0].Objects.OfType<ObstacleDungeonObjectInstance>().ToArray();
            Assert.That(trees.Length,Is.EqualTo(2));
            foreach(var instance in trees)
            {
                Assert.That(instance.OccupiedSizeInCells,Is.EqualTo(new Vector2Int(2,2)));
                Assert.That(instance.UsesPhysicalExplorationFootprint,Is.False,"Navigation must block all four logical cells.");
                var reservation=tree.EnumeratePlacementReservationCells(instance.Position).ToArray();
                Assert.That(reservation.Length,Is.EqualTo(4));
                Assert.That(instance.EnumerateOccupiedCells(),Is.EquivalentTo(reservation));
                foreach(var cell in reservation)Assert.That(occupied.Add(cell),Is.True);
            }
        }

        [Test]
        public void ExistingTreeReservationRejectsAnotherObjectInItsBuffer()
        {
            var dungeon=Dungeon();var floor=dungeon.Floors[0];
            var tree=Asset<ObstacleDungeonObjectDefinition>("object.forest.tree.prototype");
            floor.TryAddObject(new ObstacleDungeonObjectInstance("tree",tree,Vector2Int.zero));
            var pillar=(ObstacleDungeonObjectDefinition)definition.ObjectDefinitions[0];
            var method=typeof(DungeonObjectPlacementSystem).GetMethod("CanPlaceDungeonObject",BindingFlags.NonPublic|BindingFlags.Static);
            bool CanPlace(Vector2Int cell)=>(bool)method.Invoke(null,new object[]{floor,new ObstacleDungeonObjectInstance("pillar",pillar,cell),null});
            Assert.That(CanPlace(Vector2Int.one),Is.False);
            Assert.That(CanPlace(new Vector2Int(2,1)),Is.True);
        }

        [Test]
        public void MatureTreeReservationCannotStraddleAnInternalWall()
        {
            definition.ObjectDefinitions.Clear();definition.ObjectDefinitions.Add(Asset<ObstacleDungeonObjectDefinition>("object.forest.tree.prototype"));
            Anchor("tree",0,true);
            var dungeon=Dungeon();dungeon.Floors[0].Map.TryAddWall(Vector2Int.zero,Vector2Int.up);
            Assert.Throws<InvalidOperationException>(()=>new DungeonObjectPlacementSystem().Place(dungeon));
        }

        [TestCase(DungeonRoomType.Start)]
        [TestCase(DungeonRoomType.Normal)]
        [TestCase(DungeonRoomType.Optional)]
        [TestCase(DungeonRoomType.Corridor)]
        [TestCase(DungeonRoomType.Final)]
        public void RequiredFurnitureWorksInEveryRoomType(DungeonRoomType type)
        {
            Anchor("a", 0, true);
            var dungeon = Dungeon(type: type);
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(1));
        }

        [Test]
        public void OptionalGroupMakesOneAllOrNothingDecisionAndIsDeterministic()
        {
            Anchor("a", 0);
            Anchor("b", 1);
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, spawnChance: .33f, distributionKey: "test.pairs"));
            var successes = 0;
            for (var seed = 0; seed < 100; seed++)
            {
                var first = Dungeon(seed);
                var second = Dungeon(seed);
                new DungeonObjectPlacementSystem().Place(first);
                new DungeonObjectPlacementSystem().Place(second);
                var objects = first.Floors[0].Objects;
                Assert.That(objects.Count, Is.EqualTo(0).Or.EqualTo(2));
                Assert.That(second.Floors[0].Objects.Count, Is.EqualTo(objects.Count));
                for (var i = 0; i < objects.Count; i++)
                {
                    Assert.That(second.Floors[0].Objects[i].Position, Is.EqualTo(objects[i].Position));
                    Assert.That(second.Floors[0].Objects[i].InstanceId, Is.EqualTo(objects[i].InstanceId));
                }
                if (objects.Count == 2) successes++;
            }
            Assert.That(successes, Is.InRange(1, 99));
        }

        [TestCase(0f, 1f, 0)]
        [TestCase(1f, 0f, 1)]
        [TestCase(1f, 1f, 2)]
        public void DependenciesRemainOneWay(float rootChance, float childChance, int count)
        {
            Anchor("root", 0, chance: rootChance);
            Anchor("child", 1, chance: childChance, dependency: "root");
            Set(definition, "MinObjectCountPerFloor", 10);
            Set(definition, "MaxObjectCountPerFloor", 10);
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(count));
        }

        [Test]
        public void RequiredChildIncludesItsDependencyRegardlessOfChance()
        {
            Anchor("root", 0, chance: 0);
            Anchor("child", 1, true, dependency: "root");
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(2));
        }

        [Test]
        public void MatchingFindsCompleteLayoutWhenGreedyPlacementWouldFail()
        {
            Anchor("a.wide", 0, true, width: 2);
            Anchor("b.fixed", 0, true);
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(2));
            Assert.That(dungeon.Floors[0].Objects[0].Position, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(dungeon.Floors[0].Objects[1].Position, Is.EqualTo(Vector2Int.zero));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingSpaceRejectsRequiredGroupButSkipsOptionalGroupAtomically(bool required)
        {
            Anchor("a", 0);
            Anchor("b", 0);
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, required));
            var dungeon = Dungeon();
            if (required)
            {
                var error = Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(dungeon));
                Assert.That(error.Message, Does.Contain("group:pair").And.Contain("room.test"));
            }
            else new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyDefinitionPoolIsAnErrorEvenForOptionalContent(bool required)
        {
            Anchor("missing", 0, required, chance: 0);
            definition.ObjectDefinitions.Clear();
            var error = Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(Dungeon()));
            Assert.That(error.Message, Does.Contain("room.test").And.Contain("missing").And.Contain("puli"));
        }

        [Test]
        public void InvalidGroupDependencyCycleIsReportedByEditor()
        {
            Anchor("a", 0, dependency: "c");
            Anchor("b", 1);
            Anchor("c", 2, dependency: "b");
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }));
            var report = Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(template);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("Cykl"));
        }

        [Test]
        public void LegacyPlacementRetainsCapsAndIgnoresNewGroups()
        {
            Anchor("a", 0, true);
            template.ContentGroups.Add(new DungeonContentGroupData("not.used.by.legacy", new[] { "missing" }));
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().PlaceLegacySave(dungeon);
            Assert.That(dungeon.Floors[0].Objects, Is.Empty);
        }

        [Test]
        public void RequiredGroupIsCompleteEvenWithZeroChance()
        {
            Anchor("a", 0);
            Anchor("b", 1);
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, true, 0f));
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(2));
        }

        [Test]
        public void RequiredDependencyOnMemberIncludesWholeGroup()
        {
            Anchor("a", 0);
            Anchor("b", 1);
            Anchor("c", 2, true, dependency: "b");
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, spawnChance: 0f));
            var dungeon = Dungeon();
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(3));
        }

        [Test]
        public void GroupMembershipAndMissingReferencesAreValidated()
        {
            Anchor("a", 0);
            template.ContentGroups.Add(new DungeonContentGroupData("first", new[] { "a" }));
            template.ContentGroups.Add(new DungeonContentGroupData("second", new[] { "a" }));
            Assert.Throws<InvalidOperationException>(() => DungeonRoomContentPlanner.ValidateTemplate(template));
            template.ContentGroups.Clear();
            template.ContentGroups.Add(new DungeonContentGroupData("missing", new[] { "absent" }));
            Assert.Throws<InvalidOperationException>(() => DungeonRoomContentPlanner.ValidateTemplate(template));
        }

        [Test]
        public void EditorReportsRequiredSpaceConflictButAcceptsOptionalIneligibility()
        {
            Anchor("a", 0);
            Anchor("b", 0);
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }));
            Assert.That(Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(template).IsValid, Is.True);
            template.ContentGroups.Clear();
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "a", "b" }, true));
            var report = Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(template);
            Assert.That(report.Errors, Has.Some.Contains("Brak miejsca"));
        }

        [Test]
        public void DefinitionValidationRejectsExplicitObjectOutsidePool()
        {
            var foreign = Asset<ObstacleDungeonObjectDefinition>("foreign");
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("explicit", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.zero, Vector2Int.one, objectDefinition: foreign));
            Assert.Throws<InvalidOperationException>(() => DungeonObjectPlacementSystem.ValidateContentDefinitions(definition));
            Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(Dungeon()));
        }

        [Test]
        public void DistributionContinuesAcrossFloorsAndRepeatedPlacementDoesNotRerollEmptyFloors()
        {
            Anchor("optional", 0, chance: .33f);
            for (var seed = 0; seed < 60; seed++)
            {
                var dungeon = Dungeon(seed);
                for (var i = 1; i < 12; i++)
                {
                    var floor = new DungeonFloorInstance(i);
                    floor.TryAddRoom(new DungeonRoomInstance("room.0", DungeonRoomType.Normal, template.Id, Vector2Int.zero));
                    var map = new DungeonFloorMap();
                    map.TryAddFloorCell(Vector2Int.zero, "room.0");
                    floor.TrySetMap(map);
                    dungeon.TryAddFloor(floor);
                }
                var expected = new DungeonContentDistribution(seed);
                new DungeonObjectPlacementSystem().Place(dungeon);
                var counts = new List<int>();
                foreach (var floor in dungeon.Floors)
                {
                    counts.Add(floor.Objects.Count);
                    Assert.That(floor.Objects.Count, Is.EqualTo(expected.Roll("test.content", .33f) ? 1 : 0));
                }
                new DungeonObjectPlacementSystem().Place(dungeon);
                for (var i = 0; i < counts.Count; i++) Assert.That(dungeon.Floors[i].Objects.Count, Is.EqualTo(counts[i]));
            }
        }

        [Test]
        public void IneligibleAndGuaranteedAnchorsDoNotConsumeDistribution()
        {
            Anchor("a.fixed", 0, true);
            Anchor("b.blocked", 0, chance: .33f);
            Anchor("c.disabled", 1, chance: 0);
            Anchor("d.dependent", 2, chance: .33f, dependency: "c.disabled");
            Anchor("e.eligible", 3, chance: .33f);
            for (var seed = 0; seed < 100; seed++)
            {
                var dungeon = Dungeon(seed);
                new DungeonObjectPlacementSystem().Place(dungeon);
                var success = new DungeonContentDistribution(seed).Roll("test.content", .33f);
                Assert.That(dungeon.Floors[0].Objects.Count, Is.EqualTo(success ? 2 : 1));
            }
        }

        [Test]
        public void OptionalFractionalChanceRequiresAuthorKey()
        {
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData("missing.key", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.zero, Vector2Int.one, .33f));
            var error = Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(Dungeon()));
            StringAssert.Contains("missing.key", error.Message);
            StringAssert.Contains("klucz", error.Message);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameKeyDependenciesAreRejectedIncludingThroughGuaranteedElements(bool indirect)
        {
            Anchor("root", 0, chance: .33f);
            if (indirect) Anchor("bridge", 1, dependency: "root");
            Anchor("child", 2, chance: .33f, dependency: indirect ? "bridge" : "root");
            var dungeon = Dungeon();
            var error = Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(dungeon));
            StringAssert.Contains("test.content", error.Message);
            StringAssert.Contains("room.test", error.Message);
            Assert.That(dungeon.Floors[0].Objects, Is.Empty);
            Assert.That(dungeon.HasPlacedContent, Is.False);
        }

        [Test]
        public void SeparateKeysAllowDependencyAndSpaceCompetition()
        {
            Anchor("root", 0, chance: .33f, width: 2, key: "root");
            Anchor("child", 1, chance: .33f, dependency: "root", key: "child");
            Assert.DoesNotThrow(() => DungeonRoomContentPlanner.ValidateTemplate(template));
            for (var seed = 0; seed < 40; seed++)
                Assert.DoesNotThrow(() => new DungeonObjectPlacementSystem().Place(Dungeon(seed)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameKeySpaceCompetitionIncludesRequiredAreaBridges(bool bridge)
        {
            Anchor("first", 0, chance: .33f);
            Anchor("second", bridge ? 2 : 0, chance: .33f);
            if (bridge) Anchor("wide.required", 0, true, width: 3);
            var error = Assert.Throws<InvalidOperationException>(() => DungeonRoomContentPlanner.ValidateTemplate(template));
            StringAssert.Contains("wspolny obszar", error.Message);
        }

        [Test]
        public void AtomicGroupMembersMayDependOnEachOtherAndShareSpace()
        {
            Anchor("first", 0, width: 2);
            Anchor("second", 0, dependency: "first", width: 2);
            template.ContentGroups.Add(new DungeonContentGroupData("pair", new[] { "first", "second" },
                spawnChance: .33f, distributionKey: "pair"));
            Assert.DoesNotThrow(() => DungeonRoomContentPlanner.ValidateTemplate(template));
        }

        [Test]
        public void FeedbackBetweenKeysAcrossTemplatesIsRejectedByPoolValidation()
        {
            Anchor("first", 0, chance: .33f, key: "A");
            Anchor("second", 1, chance: .33f, dependency: "first", key: "B");
            var other = Asset<DungeonRoomTemplateDefinition>("other.room");
            other.SemanticAnchors.Add(new DungeonSemanticAnchorData("first", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.zero, Vector2Int.one, .33f, distributionKey: "B"));
            other.SemanticAnchors.Add(new DungeonSemanticAnchorData("second", DungeonSemanticAnchorType.Obstacle,
                Vector2Int.right, Vector2Int.one, .33f, requiresAnchorId: "first", distributionKey: "A"));
            definition.RoomTemplateCatalog.Templates.Add(other);
            Assert.DoesNotThrow(() => DungeonRoomContentPlanner.ValidateTemplate(template));
            Assert.DoesNotThrow(() => DungeonRoomContentPlanner.ValidateTemplate(other));
            var error = Assert.Throws<InvalidOperationException>(() => DungeonObjectPlacementSystem.ValidateContentDefinitions(definition));
            StringAssert.Contains("Konflikt historii RNG", error.Message);
        }

        [Test]
        public void ReservedCellDoesNotInvalidateRemainingArea()
        {
            Anchor("wide", 0, true, width: 2);
            var dungeon = Dungeon();
            var candidates = new DungeonSemanticAnchorResolver().GetCandidates(dungeon.Floors[0],
                definition.RoomTemplateCatalog, new[] { DungeonSemanticAnchorType.Obstacle }, _ => true, false);
            Assert.That(DungeonRoomContentPlanner.TryAssign(candidates, new HashSet<Vector2Int> { Vector2Int.zero },
                out var positions), Is.True);
            Assert.That(positions[candidates[0]], Is.EqualTo(new Vector2Int(1, 0)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SarcophagiReserveBothCellsAndRejectOverlappingOrOutOfRoomFootprints(int secondY)
        {
            var sarcophagus = Asset<ObstacleDungeonObjectDefinition>("object.obstacle.sarcophagus");
            definition.ObjectDefinitions.Add(sarcophagus);
            Set(template, "SizeInCells", new Vector2Int(1, 4));
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData(
                "cover.0",
                DungeonSemanticAnchorType.Obstacle,
                new Vector2Int(0, 0),
                Vector2Int.one,
                isRequired: true,
                objectDefinition: sarcophagus));
            template.SemanticAnchors.Add(new DungeonSemanticAnchorData(
                "cover.1",
                DungeonSemanticAnchorType.Obstacle,
                new Vector2Int(0, secondY),
                Vector2Int.one,
                isRequired: true,
                objectDefinition: sarcophagus));

            var dungeon = new DungeonInstance(definition, 42);
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance(
                "room.0", DungeonRoomType.Normal, template.Id, Vector2Int.zero));
            var map = new DungeonFloorMap();
            for (var y = 0; y < 4; y++)
                Assert.That(map.TryAddFloorCell(new Vector2Int(0, y), "room.0"), Is.True);
            Assert.That(floor.TrySetMap(map), Is.True);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            if (secondY != 2)
            {
                Assert.Throws<InvalidOperationException>(() => new DungeonObjectPlacementSystem().Place(dungeon));
                return;
            }
            new DungeonObjectPlacementSystem().Place(dungeon);

            var objects = dungeon.Floors[0].Objects;
            Assert.That(objects, Has.Count.EqualTo(2));
            Assert.That(objects[0].Definition.Id, Is.EqualTo(sarcophagus.Id));
            Assert.That(objects[1].Definition.Id, Is.EqualTo(sarcophagus.Id));
            Assert.That(objects[0].Position, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(objects[1].Position, Is.EqualTo(new Vector2Int(0, 2)));
            var first = (ObstacleDungeonObjectInstance)objects[0];
            var second = (ObstacleDungeonObjectInstance)objects[1];
            Assert.That(first.EnumerateOccupiedCells().Count(), Is.EqualTo(2));
            Assert.That(second.EnumerateOccupiedCells().Count(), Is.EqualTo(2));
            Assert.That(first.EnumeratePhysicalExplorationCells().Count(), Is.EqualTo(2));
            Assert.That(first.OccupiesPhysicalExplorationCell(new Vector2Int(0, 1)), Is.True);
            Assert.That(first.UsesPhysicalExplorationFootprint, Is.False);
        }
    }

    public class DungeonObjectPlacementSystemTests
    {
        [Test]
        public void Place_UsesSemanticAnchorsOutsideEncounterRooms()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 2,
                maxObjectCount: 2);

            var dungeon = CreateDungeon(
                definition,
                seed: 12345,
                includeEncounterInNormalRoom: true);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(dungeon);

            var floor = dungeon.Floors[0];

            Assert.That(floor.Objects, Has.Count.EqualTo(2));

            foreach (var dungeonObject in floor.Objects)
            {
                Assert.That(
                    floor.Map.TryGetRoomId(
                        dungeonObject.Position,
                        out var roomId),
                    Is.True);

                Assert.That(roomId, Is.EqualTo("optional"));
                Assert.That(dungeonObject, Is.TypeOf<ObstacleDungeonObjectInstance>());
                Assert.That(
                    dungeonObject.Position == new Vector2Int(8, 0)
                    || dungeonObject.Position == new Vector2Int(9, 0),
                    Is.True);
            }
        }

        [Test]
        public void Place_WithoutSemanticAnchors_DoesNotCreateRandomObjects()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 0,
                maxObjectCount: 2);

            definition.RoomTemplateCatalog.Templates[2].SemanticAnchors.Clear();

            var dungeon = CreateDungeon(
                definition,
                seed: 12345,
                includeEncounterInNormalRoom: false);

            new DungeonObjectPlacementSystem().Place(dungeon);

            Assert.That(dungeon.Floors[0].Objects, Is.Empty);
        }

        [Test]
        public void Place_SameSeedCreatesSameObjectPlacement()
        {
            var firstDefinition = CreateDungeonDefinition(
                minObjectCount: 3,
                maxObjectCount: 3);

            var secondDefinition = CreateDungeonDefinition(
                minObjectCount: 3,
                maxObjectCount: 3);

            var firstDungeon = CreateDungeon(
                firstDefinition,
                seed: 54321,
                includeEncounterInNormalRoom: false);

            var secondDungeon = CreateDungeon(
                secondDefinition,
                seed: 54321,
                includeEncounterInNormalRoom: false);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(firstDungeon);
            placementSystem.Place(secondDungeon);

            Assert.That(
                BuildObjectSignature(firstDungeon.Floors[0]),
                Is.EqualTo(BuildObjectSignature(secondDungeon.Floors[0])));
        }

        [Test]
        public void Place_DoesNotAddObjectsAgainWhenCalledTwice()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 2,
                maxObjectCount: 2);

            var dungeon = CreateDungeon(
                definition,
                seed: 12345,
                includeEncounterInNormalRoom: false);

            var placementSystem = new DungeonObjectPlacementSystem();

            placementSystem.Place(dungeon);

            var firstObjectCount = dungeon.Floors[0].Objects.Count;

            placementSystem.Place(dungeon);

            Assert.That(
                dungeon.Floors[0].Objects,
                Has.Count.EqualTo(firstObjectCount));
        }

        [Test]
        public void Place_DoorAnchor_CreatesClosedDoorOnAnchoredEdge()
        {
            var definition = CreateDungeonDefinition(
                minObjectCount: 0,
                maxObjectCount: 0);

            var catalog = ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();

            var template = ScriptableObject.CreateInstance<
                DungeonRoomTemplateDefinition>();

            var doorDefinition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            try
            {
                // The floor contains four room types; new generation validates every template.
                foreach (var existingTemplate in definition.RoomTemplateCatalog.Templates)
                    if (existingTemplate.Id != "template.Normal") catalog.Templates.Add(existingTemplate);
                SetAutoProperty(
                    definition,
                    "RoomTemplateCatalog",
                    catalog);

                SetAutoProperty(template, "Id", "template.Normal");
                SetAutoProperty(template, "SizeInCells", new Vector2Int(2, 2));

                template.StructuralAnchors.Add(
                    new DungeonStructuralAnchorData(
                        DungeonStructuralAnchorType.Door,
                        Vector2Int.zero,
                        DungeonEdgeDirection.Right));

                catalog.Templates.Add(template);

                SetAutoProperty(doorDefinition, "Id", "door.test");
                definition.ObjectDefinitions.Add(doorDefinition);

                var dungeon = CreateDungeon(
                    definition,
                    seed: 12345,
                    includeEncounterInNormalRoom: false);

                var placementSystem = new DungeonObjectPlacementSystem();

                placementSystem.Place(dungeon);

                var floor = dungeon.Floors[0];
                DoorDungeonObjectInstance generatedDoor = null;

                foreach (var dungeonObject in floor.Objects)
                {
                    if (dungeonObject is DoorDungeonObjectInstance door)
                    {
                        generatedDoor = door;
                        break;
                    }
                }

                Assert.That(generatedDoor, Is.Not.Null);
                Assert.That(
                    generatedDoor.Edge.FirstCell,
                    Is.EqualTo(new Vector2Int(4, 0)));
                Assert.That(
                    generatedDoor.Edge.SecondCell,
                    Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(generatedDoor.IsOpen, Is.False);
                Assert.That(
                    floor.Map.CanMoveBetween(
                        generatedDoor.Edge.FirstCell,
                        generatedDoor.Edge.SecondCell),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(doorDefinition);
            }
        }

        private static DungeonDefinition CreateDungeonDefinition(
            int minObjectCount,
            int maxObjectCount)
        {
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();

            SetAutoProperty(definition, "Id", "dungeon.test");

            var catalog = ScriptableObject.CreateInstance<
                DungeonRoomTemplateCatalog>();

            catalog.Templates.Add(
                CreateRoomTemplate(
                    "template.Start",
                    DungeonRoomType.Start,
                    new Vector2Int(2, 2)));
            catalog.Templates.Add(
                CreateRoomTemplate(
                    "template.Normal",
                    DungeonRoomType.Normal,
                    new Vector2Int(2, 2)));
            catalog.Templates.Add(
                CreateRoomTemplate(
                    "template.Optional",
                    DungeonRoomType.Optional,
                    new Vector2Int(3, 2),
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0)
                    }));
            catalog.Templates.Add(
                CreateRoomTemplate(
                    "template.Final",
                    DungeonRoomType.Final,
                    new Vector2Int(2, 2)));

            SetAutoProperty(definition, "RoomTemplateCatalog", catalog);

            SetAutoProperty(
                definition,
                "MinObjectCountPerFloor",
                minObjectCount);

            SetAutoProperty(
                definition,
                "MaxObjectCountPerFloor",
                maxObjectCount);

            var obstacle = ScriptableObject.CreateInstance<
                ObstacleDungeonObjectDefinition>();

            SetAutoProperty(obstacle, "Id", "obstacle.test");

            definition.ObjectDefinitions.Add(obstacle);

            return definition;
        }

        private static DungeonRoomTemplateDefinition CreateRoomTemplate(
            string id,
            DungeonRoomType roomType,
            Vector2Int sizeInCells,
            IReadOnlyList<Vector2Int> semanticAnchorCells = null)
        {
            var template = ScriptableObject.CreateInstance<
                DungeonRoomTemplateDefinition>();

            SetAutoProperty(template, "Id", id);
            SetAutoProperty(template, "RoomType", roomType);
            SetAutoProperty(template, "SizeInCells", sizeInCells);

            if (semanticAnchorCells != null)
            {
                for (var index = 0; index < semanticAnchorCells.Count; index++)
                {
                    template.SemanticAnchors.Add(
                        new DungeonSemanticAnchorData(
                            $"obstacle.anchor.{index:00}",
                            DungeonSemanticAnchorType.Obstacle,
                            semanticAnchorCells[index],
                            Vector2Int.one));
                }
            }

            return template;
        }

        private static DungeonInstance CreateDungeon(
            DungeonDefinition definition,
            int seed,
            bool includeEncounterInNormalRoom)
        {
            var dungeon = new DungeonInstance(definition, seed);
            var floor = new DungeonFloorInstance(0);

            AddRoom(
                floor,
                "start",
                DungeonRoomType.Start,
                new RectInt(0, 0, 2, 2));

            AddRoom(
                floor,
                "normal",
                DungeonRoomType.Normal,
                new RectInt(4, 0, 2, 2));

            AddRoom(
                floor,
                "optional",
                DungeonRoomType.Optional,
                new RectInt(8, 0, 3, 2));

            AddRoom(
                floor,
                "final",
                DungeonRoomType.Final,
                new RectInt(13, 0, 2, 2));

            var map = new DungeonFloorMap();

            AddMapCells(map, "start", new RectInt(0, 0, 2, 2));
            AddMapCells(map, "normal", new RectInt(4, 0, 2, 2));
            AddMapCells(map, "optional", new RectInt(8, 0, 3, 2));
            AddMapCells(map, "final", new RectInt(13, 0, 2, 2));

            Assert.That(floor.TrySetMap(map), Is.True);

            if (includeEncounterInNormalRoom)
            {
                var encounterDefinition =
                    ScriptableObject.CreateInstance<EncounterDefinition>();

                SetAutoProperty(
                    encounterDefinition,
                    "Id",
                    "encounter.test");

                var encounter = new DungeonEncounterInstance(
                    "encounter.test",
                    encounterDefinition,
                    "normal",
                    new RectInt(4, 0, 2, 2));

                Assert.That(floor.TryAddEncounter(encounter), Is.True);
            }

            Assert.That(dungeon.TryAddFloor(floor), Is.True);

            return dungeon;
        }

        private static void AddRoom(
            DungeonFloorInstance floor,
            string roomId,
            DungeonRoomType roomType,
            RectInt area)
        {
            var room = new DungeonRoomInstance(
                roomId,
                roomType,
                $"template.{roomType}",
                area.position);

            Assert.That(floor.TryAddRoom(room), Is.True);
        }

        private static void AddMapCells(
            DungeonFloorMap map,
            string roomId,
            RectInt area)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    Assert.That(
                        map.TryAddFloorCell(
                            new Vector2Int(x, y),
                            roomId),
                        Is.True);
                }
            }
        }

        private static string BuildObjectSignature(
            DungeonFloorInstance floor)
        {
            var result = string.Empty;

            foreach (var dungeonObject in floor.Objects)
            {
                result +=
                    $"{dungeonObject.InstanceId}|"
                    + $"{dungeonObject.Definition.Id}|"
                    + $"{dungeonObject.Position.x},{dungeonObject.Position.y};";
            }

            return result;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = FindBackingField(
                target.GetType(),
                propertyName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono backing field dla {propertyName}.");

            field.SetValue(target, value);
        }

        private static FieldInfo FindBackingField(
            Type type,
            string propertyName)
        {
            for (var currentType = type;
                currentType != null;
                currentType = currentType.BaseType)
            {
                var field = currentType.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
