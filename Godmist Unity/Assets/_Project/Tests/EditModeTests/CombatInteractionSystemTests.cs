using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatInteractionSystemTests
    {
        private readonly List<CharacterDefinition> createdDefinitions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var definition in createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            createdDefinitions.Clear();
        }

        [Test]
        public void TryInteract_DirectAndLeverDoor_TogglesOpenAndClosed()
        {
            var actor = CreateActor();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(1, 1),
                new GridPosition(2, 1),
                DoorInteractionMode.DirectAndLever);

            var session = CreateSession(actor, door);
            var system = new CombatInteractionSystem();

            system.InitializeMapObjects(session);

            var opened = system.TryInteract(
                session,
                actor,
                door.Edge.SecondCell);

            Assert.That(opened, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.False);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(80));

            var closed = system.TryInteract(
                session,
                actor,
                door.Edge.SecondCell);

            Assert.That(closed, Is.True);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(60));
        }

        [Test]
        public void TryInteract_LeverControlledDoor_TogglesThroughLever()
        {
            var actor = CreateActor();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(3, 1),
                new GridPosition(4, 1),
                DoorInteractionMode.DirectAndLever);

            var lever = new LeverCombatMapObject(
                "lever.test.01",
                new GridPosition(2, 1),
                door.ObjectId);

            var session = CreateSession(actor, door, lever);
            var system = new CombatInteractionSystem();

            system.InitializeMapObjects(session);

            var opened = system.TryInteract(
                session,
                actor,
                lever.Position);

            Assert.That(opened, Is.True);
            Assert.That(lever.IsActivated, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.False);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(80));

            var closed = system.TryInteract(
                session,
                actor,
                lever.Position);

            Assert.That(closed, Is.True);
            Assert.That(lever.IsActivated, Is.False);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(60));
        }

        [Test]
        public void TryInteract_LeverOnlyDoor_CannotBeOpenedDirectly()
        {
            var actor = CreateActor();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(1, 1),
                new GridPosition(2, 1),
                DoorInteractionMode.LeverOnly);

            var session = CreateSession(actor, door);
            var system = new CombatInteractionSystem();

            system.InitializeMapObjects(session);

            var interacted = system.TryInteract(
                session,
                actor,
                door.Edge.SecondCell);

            Assert.That(interacted, Is.False);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(100));
        }

        [Test]
        public void TryInteract_LeverCannotControlDirectOnlyDoor()
        {
            var actor = CreateActor();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(3, 1),
                new GridPosition(4, 1),
                DoorInteractionMode.DirectOnly);

            var lever = new LeverCombatMapObject(
                "lever.test.01",
                new GridPosition(2, 1),
                door.ObjectId);

            var session = CreateSession(actor, door, lever);
            var system = new CombatInteractionSystem();

            system.InitializeMapObjects(session);

            var interacted = system.TryInteract(
                session,
                actor,
                lever.Position);

            Assert.That(interacted, Is.False);
            Assert.That(lever.IsActivated, Is.False);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(100));
        }

        [Test]
        public void TryInteract_WhenActorHasTooFewAp_DoesNotChangeDoor()
        {
            var actor = CreateActor(
                currentActionPoints: 19,
                maxActionPoints: 100);

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(1, 1),
                new GridPosition(2, 1),
                DoorInteractionMode.DirectOnly);

            var session = CreateSession(actor, door);
            var system = new CombatInteractionSystem();

            system.InitializeMapObjects(session);

            var interacted = system.TryInteract(
                session,
                actor,
                door.Edge.SecondCell);

            Assert.That(interacted, Is.False);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(
                session.Grid.HasBlockedEdge(
                    door.Edge.FirstCell,
                    door.Edge.SecondCell),
                Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(19));
        }

        private CombatSession CreateSession(
            BattleUnit actor,
            params CombatMapObject[] mapObjects)
        {
            return new CombatSession
            {
                Grid = new BattleGrid(8, 8),
                ActiveUnit = actor,
                Units = new List<BattleUnit>
                {
                    actor
                },
                MapObjects = new List<CombatMapObject>(mapObjects),
                Outcome = CombatOutcomeState.Ongoing
            };
        }

        private BattleUnit CreateActor(
            int currentActionPoints = 100,
            int maxActionPoints = 100)
        {
            var definition =
                ScriptableObject.CreateInstance<CharacterDefinition>();

            createdDefinitions.Add(definition);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    Definition = definition,
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive
                },
                Position = new GridPosition(1, 1),
                CurrentActionPoints = currentActionPoints,
                MaxActionPoints = maxActionPoints,
                IsPlayerSide = true
            };
        }
    }
}