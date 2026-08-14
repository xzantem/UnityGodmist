using System;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public sealed class SummonedBattleUnit
    {
        public BattleUnit Unit;
        public string CreatorCharacterId = string.Empty;
        public string SourceSkillId = string.Empty;
        public int SourceEffectIndex;
        public int RemainingDurationTurns;
        public int CreationOrder;

        public SummonedBattleUnit(BattleUnit unit, string creatorCharacterId,
            string sourceSkillId, int sourceEffectIndex, int remainingDurationTurns,
            int creationOrder)
        {
            Unit = unit;
            CreatorCharacterId = creatorCharacterId ?? string.Empty;
            SourceSkillId = sourceSkillId ?? string.Empty;
            SourceEffectIndex = sourceEffectIndex;
            RemainingDurationTurns = Math.Max(1, remainingDurationTurns);
            CreationOrder = creationOrder;
        }

        public bool AdvanceDuration()
        {
            RemainingDurationTurns = Math.Max(0, RemainingDurationTurns - 1);
            return RemainingDurationTurns == 0;
        }
    }
}
