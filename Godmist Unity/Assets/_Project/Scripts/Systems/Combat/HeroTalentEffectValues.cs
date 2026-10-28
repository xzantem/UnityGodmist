using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class HeroTalentEffectValues
    {
        public static int CutRageBonus(int rank)=>5*rank;
        public static float ConflagrationMultiplier(int rank) => rank == 1 ? .3f : .5f;
        public static float DeepWoundsMultiplier(int rank) => .3f + .15f * rank;
        public static float ComboBleedMultiplier(int rank) => .8f + .2f * rank;
        public static float KnifeBleedMultiplier(int rank) => rank == 1 ? .6f : .9f;
        public static float RunningAttackMultiplier(int rank) => .25f * rank;
        public static float JudgementReleaseMultiplier(int rank) => .5f * rank;
        public static int HealingAura(BattleUnit source,int rank) => Mathf.RoundToInt(
            (source.Character.Stats.MinAttack.GetValue()+source.Character.Stats.MaxAttack.GetValue())*.5f
            *(1+.01f*PaladinCombatSystem.Points(source.Character,PaladinCombatSystem.Priest))*(rank==1?.07f:.15f));
        public static int PeriodicDamage(BattleUnit source,string statusId,float multiplier)
            => CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AverageAttack,multiplier);
        public static int InitialPeriodicDamage(BattleUnit source,SkillEffectData effect,int turns,CombatValueExplanation explanation=null)
        {
            if(effect.StatusId==StatusEffectIds.Poison)ScoutCombatSystem.PoisonMultiplier(source,explanation);
            var perTurn=StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect);
            if(StatusEffectSystem.HasStatus(source,StatusEffectIds.Weakening))explanation?.Add("weakening",.8f);
            explanation?.Add("periodic_after_source",perTurn);
            return effect.StatusId==StatusEffectIds.Burn
                ? Mathf.CeilToInt(MageCombatSystem.CalculateInitialBurnTotal(source,perTurn*Mathf.Max(1,turns),explanation)/(float)Mathf.Max(1,turns))
                : perTurn;
        }
    }
}
