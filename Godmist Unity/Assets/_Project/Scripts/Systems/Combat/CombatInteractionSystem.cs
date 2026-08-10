using System;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatInteractionSystem
    {
        private const int InteractionApPercent = 20;

        private readonly Action<string> battleLog;
        private readonly Action<CombatMapObject> onObjectChanged;

        public CombatInteractionSystem(Action<string> battleLog = null, 
            Action<CombatMapObject> onObjectChanged = null)
        {
            this.battleLog = battleLog;
            this.onObjectChanged = onObjectChanged;
        }

        public void InitializeMapObjects(CombatSession session)
        {
            if (session?.Grid == null || session.MapObjects == null) return;

            foreach (var mapObject in session.MapObjects)
            {
                if (mapObject is DoorCombatMapObject door)
                {
                    session.Grid.SetBlockedEdge(
                        door.Edge.FirstCell,
                        door.Edge.SecondCell,
                        door.BlocksMovement);

                    continue;
                }

                if (mapObject != null && mapObject.BlocksMovement)
                {
                    session.Grid.SetBlocked(mapObject.Position, true);
                }
            }
        }

        public bool TryInteract(CombatSession session, BattleUnit actor, GridPosition targetPosition)
        {
            if (session?.Grid == null || actor?.Character == null || session.ActiveUnit != actor
                || actor.Character.LifeState != Core.Enums.CharacterLifeState.Alive) return false;

            var mapObject = FindInteractableObject(
                session,
                actor.Position,
                targetPosition);

            if (mapObject == null)
            {
                return false;
            }

            if (!mapObject.CanInteract())
            {
                if (mapObject is DoorCombatMapObject door && 
                    door.InteractionMode == Core.Enums.DoorInteractionMode.LeverOnly)
                {
                    battleLog?.Invoke($"Drzwi {door.ObjectId} mozna obsluzyc wylacznie dzwignia.");
                }

                return false;
            }

            var actionCost = CombatActionCostCalculator
                .CalculatePercentageCost(actor.MaxActionPoints, InteractionApPercent);

            if (actor.CurrentActionPoints < actionCost)
            {
                battleLog?.Invoke("Brak wystarczajacej liczby AP do interakcji.");

                return false;
            }

            if (!ExecuteInteraction(session, mapObject)) return false;

            actor.CurrentActionPoints -= actionCost;

            battleLog?.Invoke($"{actor.Character.Definition.DisplayName} wykonuje interakcje z " +
                $"{mapObject.ObjectId} za {actionCost} AP.");

            return true;
        }

        private bool ExecuteInteraction(CombatSession session, CombatMapObject mapObject)
        {
            if (mapObject is DoorCombatMapObject door) return ToggleDoorDirectly(session, door);

            if (mapObject is LeverCombatMapObject lever) return ToggleLever(session, lever);

            return false;
        }

        private bool ToggleDoorDirectly(CombatSession session, DoorCombatMapObject door)
        {
            if (!door.TryToggleDirectly()) return false;

            ApplyDoorState(session, door);

            return true;
        }

        private bool ToggleLever(CombatSession session, LeverCombatMapObject lever)
        {
            var linkedDoor = session.GetMapObject(lever.LinkedObjectId) as DoorCombatMapObject;

            if (linkedDoor == null) return false;

            if (!linkedDoor.TryToggleFromLever())
            {
                battleLog?.Invoke($"Dzwignia {lever.ObjectId} nie steruje drzwiami {linkedDoor.ObjectId}.");

                return false;
            }

            lever.Interact();
            ApplyDoorState(session, linkedDoor);

            onObjectChanged?.Invoke(lever);

            battleLog?.Invoke($"Dzwignia {lever.ObjectId} zostaje " +
                $"{(lever.IsActivated ? "aktywna" : "nieaktywna")}.");

            return true;
        }

        private void ApplyDoorState(CombatSession session, DoorCombatMapObject door)
        {
            session.Grid.SetBlockedEdge(
                door.Edge.FirstCell,
                door.Edge.SecondCell,
                door.BlocksMovement);

            onObjectChanged?.Invoke(door);

            battleLog?.Invoke($"Drzwi {door.ObjectId} zostaja {(door.IsOpen ? "otwarte" : "zamkniete")}.");
        }

        private static CombatMapObject FindInteractableObject(
            CombatSession session,
            GridPosition actorPosition,
            GridPosition targetPosition)
        {
            foreach (var mapObject in session.MapObjects)
            {
                if (mapObject is DoorCombatMapObject door)
                {
                    if (door.ContainsCell(targetPosition)
                        && door.ContainsCell(actorPosition)
                        && actorPosition != targetPosition)
                    {
                        return door;
                    }

                    continue;
                }

                if (mapObject != null
                    && mapObject.Position == targetPosition
                    && IsAdjacent(actorPosition, targetPosition))
                {
                    return mapObject;
                }
            }

            return null;
        }

        private static bool IsAdjacent(GridPosition origin, GridPosition target)
        {
            var deltaX = Math.Abs(target.X - origin.X);
            var deltaY = Math.Abs(target.Y - origin.Y);

            return deltaX + deltaY == 1;
        }
    }
}