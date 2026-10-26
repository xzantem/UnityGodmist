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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(8000));

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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(6000));
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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(8000));

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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(6000));
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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(10000));
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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(10000));
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
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(1900));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DoorIdentityInteractionWorksFromEitherEndpointWithoutPointerCell(bool reverse)
        {
            var actor = CreateActor();
            var door = new DoorCombatMapObject("door", new GridPosition(1, 1), new GridPosition(2, 1), DoorInteractionMode.DirectOnly);
            actor.Position = reverse ? door.Edge.SecondCell : door.Edge.FirstCell;
            var session = CreateSession(actor, door);
            var system = new CombatInteractionSystem();
            system.InitializeMapObjects(session);
            Assert.That(system.CanInteractWithDoor(session, actor, "door"), Is.True);
            Assert.That(system.TryInteractWithDoor(session, actor, "door"), Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(8000));
            Assert.That(session.Grid.CanMoveBetween(door.Edge.FirstCell, door.Edge.SecondCell), Is.True);
            Assert.That(system.TryInteractWithDoor(session, actor, "missing"), Is.False);
            actor.Position = new GridPosition(4, 4);
            Assert.That(system.TryInteractWithDoor(session, actor, "door"), Is.False);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(8000));
        }

        [Test]
        public void DoorIdentityInteractionRespectsApLeverModeAndWalls()
        {
            var actor = CreateActor();
            var door = new DoorCombatMapObject("door", actor.Position, new GridPosition(2, 1), DoorInteractionMode.DirectOnly);
            var session = CreateSession(actor, door);
            var system = new CombatInteractionSystem();
            actor.CurrentActionPoints = 1999;
            Assert.That(system.TryInteractWithDoor(session, actor, "door"), Is.False);
            actor.CurrentActionPoints = 10000;
            door.InteractionMode = DoorInteractionMode.LeverOnly;
            Assert.That(system.TryInteractWithDoor(session, actor, "door"), Is.False);
            door.InteractionMode = DoorInteractionMode.DirectOnly;
            session.Grid.SetWallEdge(door.Edge.FirstCell, door.Edge.SecondCell, DungeonWallType.Full);
            Assert.That(system.TryInteractWithDoor(session, actor, "door"), Is.False);
            Assert.That(system.TryInteract(session, actor, door.Edge.SecondCell), Is.False);
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(10000));
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void DungeonDoorViewRoutesCombatInteractionThroughExistingOwnerAndChargesAp()
        {
            var partyRoot = new GameObject("party");
            var ownerRoot = new GameObject("owner");
            var hostRoot = new GameObject("combat");
            var doorRoot = new GameObject("door");
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
            try
            {
                var party = partyRoot.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                var owner = ownerRoot.AddComponent<Godmist.Views.Dungeons.DungeonCombatCoordinator>();
                var host = hostRoot.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
                var view = doorRoot.AddComponent<Godmist.Views.Dungeons.DungeonDoorView>();
                var actor = CreateActor();
                actor.Character.CharacterId = "player";
                var door = new DoorCombatMapObject("door", actor.Position, new GridPosition(2, 1), DoorInteractionMode.DirectOnly);
                var session = CreateSession(actor, door);
                var driver = new Godmist.Views.Combat.CombatExecutionDriver(host, null, null, null, null, null, null,
                    8, 8, 0, 0, 0, null, null, new(), null, null, null);
                var game = new Godmist.Runtime.Game.GameSession();
                Assert.That(game.TryRegisterCharacter(actor.Character), Is.True);
                Assert.That(game.TryAddCharacterToActiveParty("player"), Is.True);
                void Set(object target, string field, object value) => target.GetType().GetField(field,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
                Set(driver, "<Session>k__BackingField", session);
                Set(driver, "interactionSystem", new CombatInteractionSystem());
                Set(driver, "turnSystem", new CombatTurnSystem());
                Set(host, "focused", driver);
                var drivers = (Dictionary<CombatSession, Godmist.Views.Combat.CombatExecutionDriver>)typeof(Godmist.Views.Combat.CombatSandboxBootstrap)
                    .GetField("drivers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(host);
                drivers.Add(session, driver);
                Set(owner, "initialized", true); Set(owner, "game", game); Set(owner, "combatHost", host);
                party.SetCombatCoordinator(owner);
                Set(view, "partyController", party);
                Set(view, "door", new Godmist.Runtime.Dungeons.DoorDungeonObjectInstance("door", definition,
                    new Godmist.Runtime.Dungeons.DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(2, 1))));
                Assert.That(view.CanInteract(), Is.True, "E must not reject a combat-owned door before reaching the driver");
                session.MembershipChangePending = true;
                Assert.That(view.CanInteract(), Is.False);
                session.MembershipChangePending = false;
                view.Interact();
                Assert.That(door.IsOpen, Is.True);
                Assert.That(actor.CurrentActionPoints, Is.EqualTo(8000));
            }
            finally
            {
                Object.DestroyImmediate(doorRoot); Object.DestroyImmediate(partyRoot);
                Object.DestroyImmediate(ownerRoot); Object.DestroyImmediate(hostRoot); Object.DestroyImmediate(definition);
            }
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
                CurrentActionPoints = currentActionPoints * 100,
                MaxActionPoints = maxActionPoints * 100,
                IsPlayerSide = true
            };
        }
    }
}
