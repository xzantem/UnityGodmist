using System;
using Godmist.Runtime.Characters;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class BattleUnit
    {
        public CharacterInstance Character;
        public GridPosition Position;

        public int CurrentActionPoints;
        public int MaxActionPoints;
        public int MovementStepsThisTurn;
        public float CurrentActionValue;

        public bool HasReacted;
        public bool IsReadyToEscape;
        public bool IsPlayerSide;
        public int DownedTurnsRemaining;

        public const int DownedTurnLimit = 5;
    }
}