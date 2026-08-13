using System;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public sealed class CombatTurnParticipant
    {
        public BattleUnit Unit { get; }
        public SummonedCombatMapObject SummonedObject { get; }

        public bool IsUnit => Unit != null;
        public bool IsSummonedObject => SummonedObject != null;

        public string ParticipantId
        {
            get
            {
                if (IsUnit)
                {
                    return Unit.Character?.CharacterId ?? string.Empty;
                }

                return SummonedObject?.ObjectId ?? string.Empty;
            }
        }

        public float CurrentActionValue
        {
            get
            {
                return IsUnit
                    ? Unit.CurrentActionValue
                    : SummonedObject.CurrentActionValue;
            }
            set
            {
                if (IsUnit)
                {
                    Unit.CurrentActionValue = value;
                    return;
                }

                if (IsSummonedObject)
                {
                    SummonedObject.CurrentActionValue = value;
                }
            }
        }

        public CombatTurnParticipant(BattleUnit unit)
        {
            Unit = unit;
        }

        public CombatTurnParticipant(SummonedCombatMapObject summonedObject)
        {
            SummonedObject = summonedObject;
        }
    }
}
