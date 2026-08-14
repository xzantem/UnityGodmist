using System;
using Godmist.Data.Effects;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public sealed class ScheduledCombatEffect
    {
        public string CasterCharacterId = string.Empty;
        public SkillData Skill;
        public SkillEffectData Effect;
        public int EffectIndex;
        public int RemainingCasterTurns;
        public string TargetCharacterId = string.Empty;
        public GridPosition? TargetPosition;

        public ScheduledCombatEffect(string casterCharacterId, SkillData skill,
            SkillEffectData effect, int effectIndex, int remainingCasterTurns,
            string targetCharacterId, GridPosition? targetPosition)
        {
            CasterCharacterId = casterCharacterId ?? string.Empty;
            Skill = skill;
            Effect = effect;
            EffectIndex = effectIndex;
            RemainingCasterTurns = Math.Max(1, remainingCasterTurns);
            TargetCharacterId = targetCharacterId ?? string.Empty;
            TargetPosition = targetPosition;
        }
    }
}
