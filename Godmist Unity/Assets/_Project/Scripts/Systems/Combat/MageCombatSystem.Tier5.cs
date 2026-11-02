using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        public const string Teleportation="skill.mage.warlock.t5_4", FirePillar="skill.mage.fire.t5_5";
        public const string MinimumRoll="mage.fire.minimum_roll", Absorption="mage.warlock.absorption";
        public static bool HasMinimumRoll(BattleUnit source)=>StatusEffectSystem.HasStatus(source,MinimumRoll);
        public static int PreviewAbsorption(BattleUnit source)=>Math.Max(0,source?.MageState.AttackBonus>0
            ?source.MageState.AttackBonus:source?.MageState.AbsorbedDamage??0);
        public static int ConsumeAbsorption(BattleUnit source)
        {var amount=source?.MageState.AttackBonus??0;if(source!=null)source.MageState.AttackBonus=0;return amount;}
        public static SkillEffectData Tier5Effect(BattleUnit source,SkillData skill,SkillEffectData effect)
        {
            if(skill?.SkillId=="skill.mage.fire_arrow"&&effect.EffectType==EffectType.ModifyVar
                &&effect.ModifierVariableType==GameplayVariableType.DamageDealtPercent&&F(source,"t5_1")>0)
                effect=effect.WithScoutParameters(effect.AdditionalHitTrials,effect.BonusDamageMultiplierChance,
                    effect.StatusDamageMultiplier,F(source,"t5_1")==1?-.25f:-.4f,effect.DurationTurns);
            if(skill?.SkillId==FirePillar)
            {
                var bonus=F(source,"t5_4") switch{1=>.15f,2=>.3f,>=3=>.5f,_=>0};
                effect=effect.WithDamageMultiplier(effect.DamageMultiplier*(1+bonus))
                    .WithScoutParameters(effect.AdditionalHitTrials,effect.BonusDamageMultiplierChance,
                        effect.StatusDamageMultiplier*(1+bonus),effect.ModifierValue,effect.DurationTurns);
            }
            return effect;
        }
        public static void ApplyMinimumRoll(BattleUnit source,BattleUnit target)
        {
            if(F(source,"t5_2")==0||target?.Character?.LifeState!=CharacterLifeState.Alive)return;
            var effect=new PassiveEffectRuntime(MinimumRoll,new(){PassiveEffectSourceFactory.FromRuntimeSystem(source,MinimumRoll)},
                1,4,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd){DisplayName="Unieszkodliwienie"};
            new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static void AfterTeleport(CombatSession session,BattleUnit source,BattleUnit target,SkillData skill)
        {
            if(skill?.SkillId!=Teleportation||W(source,"t5_5")==0)return;
            var advance=.25f*W(source,"t5_5")*CombatTurnSystem.CalculateBaseActionValue(target);
            if(session.ActiveUnit==target)target.PendingEquipmentActionValueReduction+=advance;
            else target.CurrentActionValue=Math.Max(0,target.CurrentActionValue-advance);
            CombatTurnSystem.ReorderParticipants(session);
        }
        public static void ExtendBurnOnContact(BattleUnit source,BattleUnit target,PassiveEffectCombatContext context,float? roll=null)
        {
            var rank=F(source,"t5_3");
            if(rank==0||target?.Character?.LifeState!=CharacterLifeState.Alive||source.IsPlayerSide==target.IsPlayerSide
                ||context?.Skill==null||context.IsPassiveDuplicate||context.IsPassiveReaction||string.IsNullOrEmpty(context.ActionId)
                ||context.DamageType is DamageType.Bleed or DamageType.Poison or DamageType.Burn)return;
            var burn=target.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId==StatusEffectIds.Burn&&PassiveEffectLifecycleSystem.IsActive(e));
            var dot=burn?.GetPayload<DamageOverTimePayload>();
            if(dot==null)return;
            source.MageState.BurnExtensionAttempts??=new Dictionary<string,HashSet<string>>();
            if(!source.MageState.BurnExtensionAttempts.TryGetValue(context.ActionId,out var attempts))
                source.MageState.BurnExtensionAttempts[context.ActionId]=attempts=new();
            if(!attempts.Add(target.Character.CharacterId)||(roll??UnityEngine.Random.value)>=(rank==1?.3f:.6f))return;
            var tick=Mathf.CeilToInt(dot.RemainingTotalDamage/(float)Math.Max(1,burn.RemainingDuration));
            dot.RemainingTotalDamage+=tick;dot.DamagePerTurn=tick;burn.RemainingDuration++;
        }
        public static void AbsorbDodgedDamage(CombatSession session,BattleUnit attacker,BattleUnit defender,int rolledDamage,
            DamageType type,PassiveEffectCombatContext context,float penetration=0,bool ignoreDefense=false,float? roll=null)
        {
            var rank=W(defender,"t5_3");
            if(rank==0||attacker?.Character?.LifeState!=CharacterLifeState.Alive||defender?.Character?.LifeState!=CharacterLifeState.Alive
                ||attacker.IsPlayerSide==defender.IsPlayerSide||type is not (DamageType.Physical or DamageType.Magical or DamageType.Inevitable)
                ||context?.IsPassiveReaction==true||context?.IsPassiveDuplicate==true||attacker.GuardianState.ReactionDepth>0
                ||(roll??UnityEngine.Random.value)>=.5f)return;
            var hypothetical=CombatDamageSystem.CalculateDodgedDamage(session,attacker,defender,rolledDamage,type,context,penetration,ignoreDefense);
            var gain=Mathf.FloorToInt(hypothetical*(rank==1?.1f:rank==2?.35f:.6f));
            defender.MageState.AbsorbedDamage=(int)Math.Min(int.MaxValue,(long)defender.MageState.AbsorbedDamage+gain);
            if(gain>0)ScoutCombatSystem.Marker(defender,Absorption,"Absorpcja Mocy");
        }
        public readonly struct Tier5CastScope:IDisposable
        {
            readonly BattleUnit unit;readonly CombatSession session;readonly string actionId;readonly int previous;
            public Tier5CastScope(CombatSession session,BattleUnit unit,SkillData skill,string actionId,bool duplicate)
            {
                this.session=session;this.unit=unit;this.actionId=actionId;previous=unit?.MageState.AttackBonus??0;
                if(unit!=null)unit.MageState.AttackBonus=0;
                if(unit==null||duplicate||!skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage))return;
                unit.MageState.AttackBonus=unit.MageState.AbsorbedDamage;unit.MageState.AbsorbedDamage=0;
                new PassiveEffectLifecycleSystem().RemoveEffect(unit.Character,Absorption);
            }
            public void Dispose()
            {
                if(unit==null)return;unit.MageState.AttackBonus=previous;
                var completedActionId=actionId;
                if(session?.ScheduledEffects?.Any(e=>e.ActionId==completedActionId)!=true)
                {unit.MageState.BurnExtensionAttempts?.Remove(actionId);CompleteFrostAction(unit,actionId);}
            }
        }
    }
}
