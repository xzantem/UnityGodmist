using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class WarriorCombatSystem
    {
        public static SkillEffectData ResolveEffect(BattleUnit u,SkillData skill,SkillEffectData e)
        {
            var hits=e.DamageHitCount;var turns=e.DurationTurns;var chance=e.BaseChance;var dot=e.StatusDamageMultiplier;var value=e.ModifierValue;
            switch(skill?.SkillId)
            {
                // The former bleed talents now belong to Discharge; the starter bleed has its authored values.
                case "skill.warrior.immobilize":turns+=2*C(u,"broken_bones");break;
                case "skill.warrior.shout":turns=u.LastSkillResourceSpent>=50?2:1;break;
                case "skill.warrior.whirlwind":hits+=B(u,"force_moment");break;
                case DeathWish:value+=.25f*B(u,"desperation");break;
            }
            return e.WithScoutParameters(e.AdditionalHitTrials,e.BonusDamageMultiplierChance,dot,value,turns).WithRuntimeParameters(hits,turns,chance);
        }
        public static bool ExecuteCommand(CombatSession session,BattleUnit source,BattleUnit target,SkillData skill,SkillEffectData effect,CombatRecoverySystem recovery)
        {
            if(source?.Character==null || target?.Character==null)return false;
            var w=target.WarriorState;w.Session=session;
            switch(effect.EffectType)
            {
                case EffectType.MarkForDestruction:
                    if(UnityEngine.Random.value>StatusEffectSystem.CalculateEffectChance(effect.BaseChance,StatusEffectSystem.GetResistance(target,StatusEffectIds.Weakening)))return false;
                    w.OrderSource=source;w.OrderTurns=effect.DurationTurns;return true;
                case EffectType.ProtectAlly:
                    w.ProtectorSource=source;w.ProtectionTurns=effect.DurationTurns;w.Riposte=C(source,"riposte")>0;return true;
                case EffectType.GrantFreeMovement:
                    if(C(source,"strategy")==0)return false;
                    foreach(var ally in session.Units)if(ally.IsPlayerSide==source.IsPlayerSide && ally.Character.LifeState==CharacterLifeState.Alive){ally.WarriorState.FreeMovementTurns=3;}
                    return true;
                case EffectType.HealMissingHealthPercent:
                    return recovery.Heal(target,Mathf.RoundToInt(Mathf.Max(0,target.Character.Stats.MaxHealth.GetValue()-target.Character.CurrentHealth)*effect.PercentageValue))>0;
            }
            return false;
        }
        public static void AfterHit(CombatSession session,PassiveEffectCombatContext ctx,bool critical,int beforeDefense,int hitDamage,CombatDamageSystem damage)
        {
            var source=ctx?.Source;var target=ctx?.Target;
            if(source?.Character==null || target?.Character==null)return;
            target.WarriorState.Session=session;source.WarriorState.Session=session;
            var statuses=new StatusEffectSystem(damageSystem:damage);
            if(critical)
            {
                var amok=B(source,"amok");if(amok>0)ScoutCombatSystem.TimedStat(source,source,CharacterStatType.MaxResource,10+10*amok,5,"warrior.amok");
                var wounds=C(source,"deep_wounds");if(wounds>0)statuses.ApplyStatus(source,target,StatusEffectIds.Bleed,.9f,3,1,.3f+.15f*wounds,0,"warrior.deep_wounds");
                var trauma=C(source,"trauma");if(trauma>0 && UnityEngine.Random.value<StatusEffectSystem.CalculateEffectChance(.85f,StatusEffectSystem.GetResistance(target,StatusEffectIds.Weakening))){target.WarriorState.TraumaTurns=5;target.WarriorState.TraumaBonus=.3f*trauma;}
            }
            if(ctx.SkillId=="skill.warrior.whirlwind" && B(source,"conflagration")>0)statuses.ApplyStatus(source,target,StatusEffectIds.Burn,.15f,2,1,B(source,"conflagration")==1?.3f:.5f,0,"warrior.conflagration");
            var bonus=hitDamage*.00002f*Points(source.Character,Commander)*100*(source.Character.Equipment?.Weapon?.UpgradeProgress??0);
            source.WarriorState.TrueDamageRemainder+=bonus;var extra=Mathf.FloorToInt(source.WarriorState.TrueDamageRemainder);source.WarriorState.TrueDamageRemainder-=extra;
            if(extra>0)damage.ApplyDamage(session,source,target,extra,DamageType.Inevitable,allowWarriorProcs:false);
            if(target.WarriorState.ProtectionTurns>0 && target.WarriorState.Riposte && source!=target)
                damage.ApplyDamage(session,target,source,Mathf.RoundToInt(beforeDefense*.3f),DamageType.Inevitable,allowWarriorProcs:false);
            Refresh(source);
        }
        public static void DamageTaken(BattleUnit target,int healthDamage)
        {
            if(!IsRage(target) || GainBlocked(target) || healthDamage<=0)return;
            var rank=B(target,"thirst_revenge");
            target.WarriorState.RevengeRemainder+=healthDamage/Mathf.Max(1,target.Character.Stats.MaxHealth.GetValue())*20*rank;
            var gain=Mathf.FloorToInt(target.WarriorState.RevengeRemainder+.00001f);target.WarriorState.RevengeRemainder-=gain;
            if(gain>0){target.Character.ResourcePool.Restore(gain);target.Character.ResourcePool.HasGainedResourceSinceLastTurn=true;}Refresh(target);
        }
        public static void DamageDealt(BattleUnit source,BattleUnit target,PassiveEffectCombatContext ctx)
        {
            if(!IsRage(source) || GainBlocked(source) || ctx?.SkillId == WarriorDischargeRules.SkillId)return;
            var gain=ctx?.SkillId==Cut?5*B(source,"rage"):0;
            if(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun))gain+=Mathf.RoundToInt(Godmist.Data.Configuration.GameDefinesRuntime.GetInt(Godmist.Data.Configuration.GameDefineIds.RagePerHit,10)*.5f*B(source,"unawareness"));
            if(gain>0)source.Character.ResourcePool.Restore(gain);Refresh(source);
        }
        public static bool AvoidStun(BattleUnit source,BattleUnit target,CombatDamageSystem damage)
        {
            var rank=C(target,"renewal");if(rank==0 || UnityEngine.Random.value>=rank*.05f)return false;
            new CombatRecoverySystem().Heal(target,Mathf.RoundToInt(target.Character.Stats.MaxHealth.GetValue()*.1f));
            var rise=C(target,"furious_rising");
            if(rise>0 && source?.Character!=null)
            {
                var amount=Mathf.RoundToInt(source.Character.Stats.MaxHealth.GetValue()*(rise==1?.03f:.07f));
                if(UnityEngine.Random.value<Mathf.Clamp01(target.Character.Stats.CritChance.GetValue()+.1f))amount=Mathf.CeilToInt(amount*Mathf.Max(1,target.Character.Stats.CritDamage.GetValue()));
                var final=CombatDamageSystem.ApplyDefense(target,source,amount,DamageType.Magical);
                damage.ApplyDamage(target.WarriorState.Session,target,source,final,DamageType.Magical,allowWarriorProcs:false);
            }
            return true;
        }
    }
}
