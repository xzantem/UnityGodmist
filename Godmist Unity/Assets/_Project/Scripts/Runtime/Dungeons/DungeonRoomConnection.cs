using System;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonRoomConnection
    {
        public string FirstRoomId { get; }
        public string SecondRoomId { get; }

        public bool HasGeometry { get; private set; }
        public Vector2Int FirstDoorCell { get; private set; }
        public Vector2Int SecondDoorCell { get; private set; }
        public int DoorwayWidth { get; private set; }

        public DungeonRoomConnection(
            string firstRoomId,
            string secondRoomId)
        {
            if (string.IsNullOrWhiteSpace(firstRoomId))
            {
                throw new ArgumentException(
                    "FirstRoomId musi byc niepuste.",
                    nameof(firstRoomId));
            }

            if (string.IsNullOrWhiteSpace(secondRoomId))
            {
                throw new ArgumentException(
                    "SecondRoomId musi byc niepuste.",
                    nameof(secondRoomId));
            }

            if (string.Equals(
                    firstRoomId,
                    secondRoomId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Polaczenie nie moze prowadzic do tego samego pokoju.");
            }

            FirstRoomId = firstRoomId;
            SecondRoomId = secondRoomId;
        }

        public bool TrySetDirectPassage(
            Vector2Int firstDoorCell,
            Vector2Int secondDoorCell,
            int doorwayWidth)
        {
            if (doorwayWidth < 1)
            {
                return false;
            }

            FirstDoorCell = firstDoorCell;
            SecondDoorCell = secondDoorCell;
            DoorwayWidth = doorwayWidth;
            HasGeometry = true;

            return true;
        }

        public bool Connects(string firstRoomId, string secondRoomId)
        {
            return string.Equals(
                       FirstRoomId,
                       firstRoomId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       SecondRoomId,
                       secondRoomId,
                       StringComparison.Ordinal)
                   || string.Equals(
                       FirstRoomId,
                       secondRoomId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       SecondRoomId,
                       firstRoomId,
                       StringComparison.Ordinal);
        }
    }
}