using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class ScoutCombatSystem
    {
        public const string ForwardRoll="skill.scout.assassin.t5_2";
        public const string BottledVenom="skill.scout.blade_dancer.t5_1";
        public const string SickenedCurse="skill.scout.blade_dancer.t5_4";
        public const string RollCritical="scout.forward_roll.critical";
        public const string RecoveredStrength="scout.recovered_strength";
        public static bool HasRollCritical(BattleUnit u)=>u!=null&&(u.ScoutState.RollCriticalActive||StatusEffectSystem.HasStatus(u,RollCritical));
        public static bool ConsumeRollCritical(BattleUnit u)
        {
            if(u?.ScoutState.RollCriticalActive!=true)return false;
            u.ScoutState.RollCriticalActive=false;return true;
        }
        internal static void Marker(BattleUnit u,string id,string name,int turns=0,UniqueBuffType? unique=null)
        {
            var effect=new PassiveEffectRuntime(id,new(){PassiveEffectSourceFactory.FromRuntimeSystem(u,id)},1,turns,0,
                turns>0?PassiveEffectLifetime.Timed:PassiveEffectLifetime.CombatScoped,
                turns>0?PassiveEffectTickPhase.TurnEnd:PassiveEffectTickPhase.None){DisplayName=name};
            if(unique.HasValue)effect.SetPayload(new UniqueCombatBuffPayload{BuffType=unique.Value});
            var lifecycle=new PassiveEffectLifecycleSystem();
            if(turns>0)lifecycle.TryApplyTimedEffect(u.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
            else lifecycle.TryApplyCombatScopedEffect(u.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static void GrantRollCritical(BattleUnit u)=>Marker(u,RollCritical,"Przewrót — krytyk");
        public static void GrantRollDodge(BattleUnit u)=>Marker(u,"scout.forward_roll.dodge","Przewrót — unik",unique:UniqueBuffType.GuaranteedDodge);
        public static void RecoveredKnives(BattleUnit u,int count)
        {
            if(count<=0||BladeRank(u,"t5_2")==0)return;
            u.ScoutState.RecoveredStacks=Math.Min(3,u.ScoutState.RecoveredStacks+count);
            Marker(u,RecoveredStrength,"Odzyskana Siła");
        }
        public static float RecoveredBonus(BattleUnit u)
        {
            if(u==null)return 0;
            if(u.GuardianState.ReactionDepth>0)return 0;
            if(u.ScoutState.AttackScopeDepth>0)return u.ScoutState.RecoveredAttackBonus;
            return u.ScoutState.RecoveredStacks*(BladeRank(u,"t5_2") switch{1=>.08f,2=>.16f,3=>.25f,_=>0});
        }
        public sealed class AttackScope:IDisposable
        {
            readonly BattleUnit unit;readonly bool previousCritical;readonly float previousBonus;
            public AttackScope(BattleUnit u,SkillData skill,bool duplicate)
            {
                unit=u;var state=u.ScoutState;
                previousCritical=state.RollCriticalActive;previousBonus=state.RecoveredAttackBonus;
                var damaging=skill?.Effects?.Any(e=>e.EffectType==EffectType.DealDamage)==true;
                var bonus=RecoveredBonus(u);state.AttackScopeDepth++;
                state.RollCriticalActive=false;state.RecoveredAttackBonus=0;
                if(!duplicate&&damaging)
                {
                    state.RollCriticalActive=StatusEffectSystem.HasStatus(u,RollCritical);
                    state.RecoveredAttackBonus=bonus;
                    state.RecoveredStacks=0;
                    var lifecycle=new PassiveEffectLifecycleSystem();
                    lifecycle.RemoveEffect(u.Character,RollCritical);lifecycle.RemoveEffect(u.Character,RecoveredStrength);
                }
            }
            public void Dispose()
            {
                unit.ScoutState.AttackScopeDepth--;
                unit.ScoutState.RollCriticalActive=previousCritical;unit.ScoutState.RecoveredAttackBonus=previousBonus;
            }
        }
        public static bool ConvertMiss(BattleUnit u,float? roll=null)
        {
            var chance=.2f*AssassinRank(u,"t5_3");
            return chance>0&&(roll??UnityEngine.Random.value)<chance;
        }
        public static float MissConversionChance(BattleUnit u)=>.2f*AssassinRank(u,"t5_3");
        public static float HitChanceAfterRescue(BattleUnit source,float chance)=>Mathf.Min(
            WarriorCombatSystem.ArmoredHitChance(source,1f),chance+(1-chance)*MissConversionChance(source));
        public static bool RescueMiss(BattleUnit source,BattleUnit target,SkillData skill,float? originalChance=null)
        {
            var chance=originalChance??CombatDamageSystem.CalculateSkillHitChance(source,target,skill);
            var rescue=(HitChanceAfterRescue(source,chance)-chance)/Mathf.Max(.00001f,1-chance);
            return rescue>0&&UnityEngine.Random.value<rescue;
        }
        public static bool ExtendPoison(BattleUnit target)
        {
            var extended=false;
            foreach(var effect in target.Character.PassiveEffects)
            {
                if(effect.EffectId!=StatusEffectIds.Poison||!PassiveEffectLifecycleSystem.IsActive(effect))continue;
                var dot=effect.GetPayload<DamageOverTimePayload>();
                if(dot!=null)dot.RemainingTotalDamage+=Mathf.CeilToInt(dot.RemainingTotalDamage/(float)Math.Max(1,effect.RemainingDuration));
                effect.RemainingDuration++;extended=true;
            }
            return extended;
        }
        public static float RangeBonus(BattleUnit u,SkillData skill)=>skill?.AttackType==AttackType.Ranged
            ?2*BladeRank(u,"t5_3"):0;
        public static float ThroatChance(BattleUnit source,BattleUnit target)=>(.01f*(1+AssassinRank(source,"throat_cut")*3))
            *(AssassinRank(source,"t5_5")>0&&StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun)?2:1);
        public static void LowBlow(PassiveEffectCombatContext context,bool critical,StatusEffectSystem statuses=null)
        {
            if(!critical||AssassinRank(context?.Source,"t5_4")==0||context.Target?.Character?.CurrentHealth<=0)return;
            (statuses??new StatusEffectSystem()).ApplyStatus(context.Source,context.Target,StatusEffectIds.Stun,.3f,1,1,0,0,"scout.low_blow");
        }
        public static void StartVenom(CombatSession session,BattleUnit source,StatusEffectSystem statuses)
        {
            source.ScoutState.VenomPulses=2;
            Marker(source,BottledVenom,"Jad w Butelce",3);
            PulseVenom(session,source,statuses);
        }
        public static void PulseVenom(CombatSession session,BattleUnit source,StatusEffectSystem statuses)
        {
            if(session?.Units==null||source?.Character?.LifeState!=CharacterLifeState.Alive)return;
            foreach(var target in session.Units.ToArray())
            {
                if(target?.Character?.LifeState!=CharacterLifeState.Alive||target.IsPlayerSide==source.IsPlayerSide
                    ||target.Position.Level!=source.Position.Level
                    ||Math.Abs(target.Position.X-source.Position.X)+Math.Abs(target.Position.Y-source.Position.Y)>2)continue;
                statuses.ApplyStatus(source,target,StatusEffectIds.Poison,.7f,3,1,.4f,0,BottledVenom,
                    statusDamagePerTurnOverride:StatusEffectSystem.CalculateStatusDamagePerTurn(source,StatusEffectIds.Poison,.4f));
            }
        }
        public static void Tier5TurnStart(CombatSession session,BattleUnit u,StatusEffectSystem statuses)
        {
            if(u?.ScoutState.VenomPulses>0)
            {
                if(u.Character.LifeState!=CharacterLifeState.Alive||!StatusEffectSystem.HasStatus(u,BottledVenom))
                {u.ScoutState.VenomPulses=0;return;}
                PulseVenom(session,u,statuses);u.ScoutState.VenomPulses--;
            }
        }
    }
}
